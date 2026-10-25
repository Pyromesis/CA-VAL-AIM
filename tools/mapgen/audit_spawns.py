#!/usr/bin/env python3
"""Check every spawn point the game produces against the map's real art.

    # 1. dump the spawns with the game's own code (Godot .NET console exe, dev build):
    Godot --headless --path ValTrainerGodot -- --dev --data-dir <tmp> --valorant-dir <tmp> \
          --maps-dir <folder holding maps/> --spawn-dump spawns.json
    # 2. audit them against the art GLBs:
    python audit_spawns.py <maps dir> spawns.json [map ...] [--pairs 20000] [--json report.json]
                           [--full-pool] [--write-exclude]
    # --full-pool judges every Deathmatch spawn-pool node (default: a 3000-node sample); --write-exclude also writes
    # maps/<key>/spawn_exclude.json with the rejected nodes, which the game leaves out of its spawn pool.

The dump (src/Maps/SpawnAudit.cs) holds, per map, the drill start, every enemy / plant / retake spot after
SpawnSafety, the Deathmatch navigation grid and its spawn pool. For each point this script casts rays against the
map's art triangles (same frame as gen_collision.py):

* NOFLOOR  no visible art floor within 0.6 m of the feet (collision steps on stairs/ramps sit up to ~0.5 m off the art) (standing on an invisible collision slab: typically under
           the map, inside a prop, or on a leftover slab outside the map)
* UNDER    the first surface within 3 m above the feet is the back of an up-facing floor (you are below the map)
* INWALL   art geometry within 0.30 m of the body axis at knee / chest / head height
* OUTSIDE  outside the official minimap's playable area (alpha mask, MapArt calibration)

It also measures walk-through walls: for random pairs of neighbouring nav nodes (the collision lets a body step
from one to the other), a ray at 0.9 m and 1.5 m between them that hits art on both heights means an art wall the
collision is missing (LEAK). Exit code 1 when a drill start or any spot of the DM spawn pool is bad.
"""

import json
import math
import os
import re
import sys
import time
from collections import defaultdict

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_collision as gc  # noqa: E402  (GLB reader + map frame)

BUCKET = 2.0
fresh_exclude = False  # --fresh-exclude: overwrite spawn_exclude.json instead of merging
full_pool = False  # --full-pool: judge every Deathmatch pool node instead of a 3000-node sample
SRC = os.path.join(HERE, "..", "..", "ValTrainerGodot", "src")


# ---------------------------------------------------------------- art triangles

def load_triangles(folder):
    levels, origin, _ = gc.map_levels(folder)
    ox, oy, oz = origin
    chunks = []
    for level in levels:
        if re.search(r"(Vista|Skybox|Backdrop)", level, re.I):
            continue
        path = os.path.join(folder, level + ".glb")
        if not os.path.exists(path) or os.path.getsize(path) < 1024:
            continue
        js = gc.read_glb(path)
        for pos, tri in gc.world_triangles(js):
            w = pos[tri]
            f = np.empty_like(w, dtype=np.float32)
            f[:, :, 0] = w[:, :, 2] + ox
            f[:, :, 1] = w[:, :, 1] + oy
            f[:, :, 2] = -w[:, :, 0] + oz
            chunks.append(f)
        del js
    t = np.concatenate(chunks) if chunks else np.zeros((0, 3, 3), np.float32)
    # drop degenerate triangles
    n = np.cross(t[:, 1] - t[:, 0], t[:, 2] - t[:, 0])
    ln = np.linalg.norm(n, axis=1)
    keep = ln > 1e-7
    return t[keep], (n[keep] / ln[keep, None]).astype(np.float32)


class TriIndex:
    """XZ bucket index over triangles (CSR)."""

    def __init__(self, tris):
        self.t = tris
        mn = tris.min(axis=1)
        mx = tris.max(axis=1)
        i0 = np.floor(mn[:, 0] / BUCKET).astype(np.int64)
        i1 = np.floor(mx[:, 0] / BUCKET).astype(np.int64)
        j0 = np.floor(mn[:, 2] / BUCKET).astype(np.int64)
        j1 = np.floor(mx[:, 2] / BUCKET).astype(np.int64)
        keys, ids = [], []
        small = (i0 == i1) & (j0 == j1)
        idx = np.nonzero(small)[0]
        keys.append(self.key(i0[idx], j0[idx]))
        ids.append(idx)
        big = np.nonzero(~small)[0]
        span = (i1[big] - i0[big] + 1) * (j1[big] - j0[big] + 1)
        big = big[span <= 4096]                 # skip absurd backdrop plates
        for k in big.tolist():
            ii, jj = np.meshgrid(np.arange(i0[k], i1[k] + 1), np.arange(j0[k], j1[k] + 1))
            keys.append(self.key(ii.ravel(), jj.ravel()))
            ids.append(np.full(ii.size, k, np.int64))
        keys = np.concatenate(keys)
        ids = np.concatenate(ids)
        order = np.argsort(keys, kind="stable")
        self.keys = keys[order]
        self.ids = ids[order]

    @staticmethod
    def key(i, j):
        return (np.asarray(i, np.int64) + 100000) * 200003 + (np.asarray(j, np.int64) + 100000)

    def near(self, x0, z0, x1, z1):
        out = []
        for i in range(int(math.floor(x0 / BUCKET)), int(math.floor(x1 / BUCKET)) + 1):
            for j in range(int(math.floor(z0 / BUCKET)), int(math.floor(z1 / BUCKET)) + 1):
                k = self.key(i, j)
                a = np.searchsorted(self.keys, k, "left")
                b = np.searchsorted(self.keys, k, "right")
                if b > a:
                    out.append(self.ids[a:b])
        if not out:
            return np.zeros(0, np.int64)
        return np.unique(np.concatenate(out))


def ray_hits(tris, o, d, tmax):
    """Möller–Trumbore against candidate triangles: returns (t, index) of the nearest hit in (1e-4, tmax], or (inf, -1)."""
    if len(tris) == 0:
        return math.inf, -1
    v0, v1, v2 = tris[:, 0].astype(np.float64), tris[:, 1].astype(np.float64), tris[:, 2].astype(np.float64)
    e1, e2 = v1 - v0, v2 - v0
    p = np.cross(d, e2)
    det = np.einsum("ij,ij->i", e1, p)
    ok = np.abs(det) > 1e-12
    inv = np.zeros_like(det)
    inv[ok] = 1.0 / det[ok]
    s = o - v0
    u = np.einsum("ij,ij->i", s, p) * inv
    q = np.cross(s, e1)
    v = (q @ d) * inv
    t = np.einsum("ij,ij->i", e2, q) * inv
    hit = ok & (u >= 0) & (v >= 0) & (u + v <= 1) & (t > 1e-4) & (t <= tmax)
    if not hit.any():
        return math.inf, -1
    k = np.argmin(np.where(hit, t, np.inf))
    return float(t[k]), int(k)


# ---------------------------------------------------------------- minimap mask

def minimap_calibration():
    txt = open(os.path.join(SRC, "UI", "MapArt.cs"), encoding="utf-8").read()
    cal = {}
    for m in re.finditer(r'\["(\w+)"\]\s*=\s*new\("\w+",\s*([-\d.]+)f,\s*([-\d.]+)f,\s*([-\d.]+)f', txt):
        cal[m.group(1)] = (float(m.group(2)), float(m.group(3)), float(m.group(4)))
    return cal


def load_mask(maps_dir, key):
    try:
        from PIL import Image
    except ImportError:
        return None
    path = os.path.join(maps_dir, key + ".png")
    if not os.path.exists(path):
        return None
    return np.array(Image.open(path).convert("RGBA"))[:, :, 3] > 127


# ---------------------------------------------------------------- checks

def check_point(idx, normals, p):
    x, y, z = p
    flags = []
    cand = idx.near(x - 0.5, z - 0.5, x + 0.5, z + 0.5)
    tris = idx.t[cand]
    # visible floor under the feet
    t, k = ray_hits(tris, np.array([x, y + 0.6, z]), np.array([0.0, -1.0, 0.0]), 1.2)
    floor_y = y + 0.6 - t if k >= 0 else None
    if floor_y is None or abs(floor_y - y) > 0.6:
        flags.append("NOFLOOR")
    # under the map: the first thing above is the back of an up-facing floor
    up = idx.near(x - 0.1, z - 0.1, x + 0.1, z + 0.1)
    t, k = ray_hits(idx.t[up], np.array([x, y + 0.15, z]), np.array([0.0, 1.0, 0.0]), 30.0)
    if k >= 0 and normals[up[k]][1] > 0.3 and t < 3.0:
        flags.append("UNDER")
    # inside a wall
    for h in (0.4, 1.0, 1.6):
        hit = False
        for a in range(8):
            ang = a * math.pi / 4
            d = np.array([math.cos(ang), 0.0, math.sin(ang)])
            tt, kk = ray_hits(tris, np.array([x, y + h, z]), d, 0.30)
            if kk >= 0:
                hit = True
                break
        if hit:
            flags.append("INWALL")
            break
    return flags, floor_y


def inside_mask(mask, cal, p):
    if mask is None or cal is None:
        return None
    ox, oy, sc = cal
    px, py = int(ox + p[0] / sc), int(oy + p[2] / sc)
    if px < 0 or py < 0 or py >= mask.shape[0] or px >= mask.shape[1]:
        return False
    return bool(mask[py, px])


def leak_pairs(nav, rng, n_pairs):
    cell = 0.5
    cols = defaultdict(list)
    for i, (x, y, z) in enumerate(nav):
        cols[(int(math.floor(x / cell)), int(math.floor(z / cell)))].append(i)
    pairs = []
    keys = list(cols.keys())
    for (ci, cj) in keys:
        for i in cols[(ci, cj)]:
            for (di, dj) in ((1, 0), (0, 1)):
                for j in cols.get((ci + di, cj + dj), ()):
                    if abs(nav[i][1] - nav[j][1]) <= 0.7:
                        pairs.append((i, j))
    if len(pairs) > n_pairs:
        sel = rng.choice(len(pairs), n_pairs, replace=False)
        pairs = [pairs[s] for s in sel]
    return pairs


def audit_map(maps_dir, key, data, cal, n_pairs, rng):
    t0 = time.time()
    folder = os.path.join(maps_dir, key)
    tris, normals = load_triangles(folder)
    idx = TriIndex(tris)
    mask = load_mask(maps_dir, key)
    calk = cal.get(key)
    rep = {"tris": int(len(tris)), "bad": []}

    def judge(kind, name, p, pool=False):
        flags, fy = check_point(idx, normals, p)
        ins = inside_mask(mask, calk, p)
        if ins is False:
            flags.append("OUTSIDE")
        return flags

    # drill start + authored spots
    s = data["start"]["resolved"]
    f = judge("start", "start", s)
    rep["start"] = f
    for group in ("enemies", "plants", "retakes"):
        for e in data.get(group, []):
            fl = judge(group, e["name"], e["resolved"])
            if fl:
                rep["bad"].append({"group": group, "name": e["name"], "p": e["resolved"], "a": e["authored"], "flags": fl})

    nav = np.array(data["nav"]["pos"], np.float64).reshape(-1, 3)
    pool = np.array(data["nav"]["pool"], np.int64)

    # Authored spots inside art, under a floor or on an invisible slab: the nearest Deathmatch pool node (same floor,
    # within 3 m) that passes every check. OUTSIDE alone is a minimap-mask call, not a body problem: those stay.
    rep["moves"] = []
    if full_pool:
        pp = nav[pool] if len(pool) else np.zeros((0, 3))
        for b in rep["bad"]:
            if all(f == "OUTSIDE" for f in b["flags"]):
                continue
            p = np.array(b["p"], np.float64)
            d = np.hypot(pp[:, 0] - p[0], pp[:, 2] - p[2]) if len(pp) else np.zeros(0)
            ok_dy = np.abs(pp[:, 1] - p[1]) <= 1.2 if len(pp) else np.zeros(0, bool)
            for j in np.argsort(d):
                if d[j] > 3.0:
                    break
                if not ok_dy[j]:
                    continue
                q = [round(float(v), 3) for v in pp[j]]
                if not judge("move", b["name"], q):
                    rep["moves"].append({"name": f"{b['group']}:{b['name']}", "from": b["a"], "at": b["p"], "to": q,
                                         "flags": b["flags"]})
                    break
    sample = pool if (full_pool or len(pool) <= 3000) else rng.choice(pool, 3000, replace=False)
    counts = defaultdict(int)
    examples = defaultdict(list)
    excluded = []
    for i in sample.tolist():
        fl = judge("dm", str(i), nav[i])
        for fla in fl:
            counts[fla] += 1
            if len(examples[fla]) < 6:
                examples[fla].append([round(float(v), 2) for v in nav[i]])
        if fl:
            excluded.append(([round(float(v), 3) for v in nav[i]], fl))
        else:
            counts["ok"] += 1
    rep["dm_excluded"] = excluded
    rep["dm_sampled"] = int(len(sample))
    rep["dm"] = dict(counts)
    rep["dm_examples"] = dict(examples)

    # walk-through walls
    pairs = leak_pairs(nav, rng, n_pairs)
    leaks = []
    for (i, j) in pairs:
        a, b = nav[i], nav[j]
        base = max(a[1], b[1])
        d = b - a
        d[1] = 0
        L = float(np.linalg.norm(d))
        if L < 1e-3:
            continue
        d /= L
        a0 = a
        cand = idx.near(min(a[0], b[0]) - 0.1, min(a[2], b[2]) - 0.1, max(a[0], b[0]) + 0.1, max(a[2], b[2]) + 0.1)
        if len(cand) == 0:
            continue
        tr = idx.t[cand]
        both = True
        for h in (0.9, 1.5):
            t, k = ray_hits(tr, np.array([a0[0], base + h, a0[2]]), d, L)
            if k < 0:
                both = False
                break
        if both:
            leaks.append([round(float(v), 2) for v in (a + b) / 2])
    rep["pairs"] = len(pairs)
    rep["leaks"] = len(leaks)
    # cluster leaks on a 4 m grid so one missing wall reads as one entry
    cl = defaultdict(list)
    for p in leaks:
        cl[(int(p[0] // 4), int(p[1] // 2), int(p[2] // 4))].append(p)
    rep["leak_clusters"] = sorted(([len(v), v[0]] for v in cl.values()), reverse=True)[:25]
    rep["seconds"] = round(time.time() - t0, 1)
    return rep


def write_exclude(maps_dir, key, excluded, moves=()):
    """maps/<key>/spawn_exclude.json: Deathmatch spawn-pool points the art check rejected. The game drops every pool node
    within 0.35 m of one of them (DeathmatchMode.SpawnPool), so no Deathmatch spawn is inside art geometry, under a
    floor or on an invisible slab even where the collision allows it. Regenerate it whenever collision.json changes."""
    path = os.path.join(maps_dir, key, "spawn_exclude.json")
    # Merge with the existing file: a dump made with the exclusions applied no longer contains the excluded nodes, so a
    # re-run must keep them (delete the file first, or pass --fresh-exclude, after collision.json changes).
    points, flags, old_moves = {}, {}, {}
    if os.path.exists(path) and not fresh_exclude:
        try:
            old = json.load(open(path))
            for p, f in zip(old.get("points", []), old.get("flags", [""] * len(old.get("points", [])))):
                points[tuple(round(v, 2) for v in p)] = p; flags[tuple(round(v, 2) for v in p)] = f
            old_moves = {m["name"]: m for m in old.get("moves", [])}
        except (OSError, ValueError, KeyError):
            pass
    for p, f in excluded:
        k = tuple(round(v, 2) for v in p)
        points[k] = p; flags[k] = ",".join(f)
    for m in moves:
        old_moves[m["name"]] = m
    keys = sorted(points)
    out = {"source": "tools/mapgen/audit_spawns.py --full-pool --write-exclude",
           "points": [points[k] for k in keys],
           "flags": [flags[k] for k in keys],
           # authored drill spots (enemies, plants, retakes) the art check rejected and where SpawnSafety moves them
           "moves": list(old_moves.values())}
    with open(path, "w") as f:
        json.dump(out, f, separators=(",", ":"))
    return f"{path} ({len(keys)} points in the file)"


def main(argv):
    global full_pool, fresh_exclude
    args = [a for a in argv[1:]]
    fresh_exclude = "--fresh-exclude" in args
    args = [a for a in args if a != "--fresh-exclude"]
    n_pairs = 20000
    out_json = None
    write_ex = "--write-exclude" in args
    full_pool = "--full-pool" in args or write_ex
    args = [a for a in args if a not in ("--write-exclude", "--full-pool")]
    if "--pairs" in args:
        k = args.index("--pairs"); n_pairs = int(args[k + 1]); del args[k:k + 2]
    if "--json" in args:
        k = args.index("--json"); out_json = args[k + 1]; del args[k:k + 2]
    maps_dir, dump = args[0], args[1]
    only = set(args[2:])
    data = json.load(open(dump))["maps"]
    cal = minimap_calibration()
    rng = np.random.default_rng(7)
    report = {}
    fail = 0
    for key, d in data.items():
        if only and key not in only:
            continue
        if not d.get("real"):
            print(f"== {key:9s} blockout (no art): skipped")
            continue
        r = audit_map(maps_dir, key, d, cal, n_pairs, rng)
        report[key] = r
        dm = r["dm"]
        bad_pool = r["dm_sampled"] - dm.get("ok", 0)
        if r["start"] or bad_pool:
            fail += 1
        print(f"== {key:9s} tris={r['tris']:>8d} start={','.join(r['start']) or 'ok':8s} "
              f"dm ok {dm.get('ok', 0)}/{r['dm_sampled']} "
              f"(NOFLOOR {dm.get('NOFLOOR', 0)}, UNDER {dm.get('UNDER', 0)}, INWALL {dm.get('INWALL', 0)}, OUTSIDE {dm.get('OUTSIDE', 0)}) "
              f"leaks {r['leaks']}/{r['pairs']} spots-bad {len(r['bad'])}  [{r['seconds']} s]", flush=True)
        for b in r["bad"][:8]:
            print(f"     {b['group']:8s} {b['name']:22s} {b['p']} {','.join(b['flags'])}")
        for fl, ex in r["dm_examples"].items():
            print(f"     dm {fl:8s} e.g. {ex[:3]}")
        for n, p in r["leak_clusters"][:6]:
            print(f"     leak x{n:<4d} near {p}")
        if write_ex:
            for m in r["moves"]:
                print(f"     move     {m['name']:30s} {m['at']} -> {m['to']} ({','.join(m['flags'])})")
            print(f"     wrote {write_exclude(maps_dir, key, r['dm_excluded'], r['moves'])} ({len(r['dm_excluded'])} points, "
                  f"{len(r['moves'])} spot moves)", flush=True)
        r.pop("dm_excluded", None)
    if out_json:
        json.dump(report, open(out_json, "w"), indent=1)
    return 1 if fail else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
