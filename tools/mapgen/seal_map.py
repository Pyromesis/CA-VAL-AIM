#!/usr/bin/env python3
"""Patch a map's shipped collision.json against its art, additively (no box is removed or moved).

    python seal_map.py <in maps dir> <out maps dir> [map ...] [--no-leaks] [--no-hidden]

Two fixes, both measured against the art GLBs (same frame as gen_collision.py):

1. Hidden ground under raised floors ("spawned under the map"). Where a near-horizontal art floor lies
   0.6-4 m above another floor with no ceiling modelled in between, the lower one is terrain hidden under
   the map (Bind: the alley ground under Hookah's floor, where the drill start and Deathmatch spawns were).
   The gap is filled with solid boxes (gen_collision.seal_hidden_floors), which walls the crawl space off
   from the open ground around it and makes every spawn check reject it.

2. Walk-through walls. The collision is flooded from the drill start (audit_collision.flood). For every
   pair of neighbouring reachable cells, a ray at 0.9 m and at 1.5 m above the floor between them that hits
   art on both heights means a full-height art wall the collision lets a body pass. A thin wall box is
   added across that crossing (0.25-2.2 m above the floor).

The input folder is never written to: the patched collision.json goes to <out>/<map>/ (the shipped file is
copied next to it as collision.json.orig the first time). Check the result with audit_collision.py and
audit_spawns.py before shipping it.
"""
import json
import math
import os
import re
import shutil
import sys
import time

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_collision as gc     # noqa: E402
import audit_collision as ac   # noqa: E402
import audit_spawns as au      # noqa: E402


def art_grid(folder, cell=0.5):
    levels, origin, _ = gc.map_levels(folder)
    ox, oy, oz = origin
    key = os.path.basename(os.path.normpath(folder)).lower()
    grid = gc.Grid(cell, gc.spot_bounds(key))
    for level in levels:
        if re.search(r"(Vista|Skybox|Backdrop)", level, re.I):
            continue
        path = os.path.join(folder, level + ".glb")
        if not os.path.exists(path) or os.path.getsize(path) < 1024:
            continue
        js = gc.read_glb(path)
        for pos, tri in gc.world_triangles(js):
            w = pos[tri]
            f = np.empty_like(w)
            f[:, :, 0] = w[:, :, 2] + ox
            f[:, :, 1] = w[:, :, 1] + oy
            f[:, :, 2] = -w[:, :, 0] + oz
            # only the horizontal-ish triangles matter here (floors and ceilings)
            n = np.cross(f[:, 1] - f[:, 0], f[:, 2] - f[:, 0])
            ln = np.linalg.norm(n, axis=1)
            ok = ln > 1e-9
            ny = np.zeros(len(f))
            ny[ok] = n[ok, 1] / ln[ok]
            for t in f[np.abs(ny) >= 0.35]:
                gc.rasterise(grid, t[0], t[1], t[2])
        del js
    return grid


def start_of(dump, key):
    d = dump["maps"].get(key)
    return d["start"]["resolved"] if d else None


def leak_walls(folder, boxes, seeds):
    """Flood the walkable cells from several seeds (the drill start and a spread of Deathmatch spawn points, so the
    whole map is covered even when the start itself sat on sealed ground)."""
    world = ac.World(boxes)
    y_at = {}
    for sd in seeds:
        c = (round(sd[0] / ac.CELL), round(sd[2] / ac.CELL))
        if c in y_at:
            continue
        if world.blocked(sd[0], sd[2], world.ground(sd[0], sd[2], sd[1] + 0.3)):
            continue
        part = ac.flood(world, sd)
        if len(part) < 50:
            continue
        for k, v in part.items():
            if k not in y_at or v > y_at[k]:
                y_at[k] = v
    tris, normals = au.load_triangles(folder)
    idx = au.TriIndex(tris)
    C = ac.CELL
    walls = []
    pairs = 0
    for (cx, cz), y in y_at.items():
        for dx, dz in ((1, 0), (0, 1)):
            n = (cx + dx, cz + dz)
            if n not in y_at:
                continue
            y2 = y_at[n]
            if abs(y2 - y) > 0.7:
                continue
            pairs += 1
            a = np.array([cx * C, 0.0, cz * C])
            b = np.array([n[0] * C, 0.0, n[1] * C])
            base = max(y, y2)
            d = np.array([float(dx), 0.0, float(dz)])
            cand = idx.near(min(a[0], b[0]) - 0.05, min(a[2], b[2]) - 0.05, max(a[0], b[0]) + 0.05, max(a[2], b[2]) + 0.05)
            if len(cand) == 0:
                continue
            tr = idx.t[cand]
            t1, k1 = au.ray_hits(tr, np.array([a[0], base + 0.9, a[2]]), d, C)
            if k1 < 0:
                continue
            t2, k2 = au.ray_hits(tr, np.array([a[0], base + 1.5, a[2]]), d, C)
            if k2 < 0:
                continue
            hit = a + d * min(t1, t2)
            half = C * 0.5 + 0.02
            if dx:   # crossing along X: a wall in the YZ plane
                walls.append([round(hit[0] - 0.05, 3), round(base + 0.25, 3), round(hit[2] - half, 3),
                              round(hit[0] + 0.05, 3), round(base + 2.2, 3), round(hit[2] + half, 3)])
            else:
                walls.append([round(hit[0] - half, 3), round(base + 0.25, 3), round(hit[2] - 0.05, 3),
                              round(hit[0] + half, 3), round(base + 2.2, 3), round(hit[2] + 0.05, 3)])
    return walls, pairs, len(y_at)


def main(argv):
    args = list(argv[1:])
    do_leaks = "--no-leaks" not in args
    do_hidden = "--no-hidden" not in args
    dump = None
    if "--dump" in args:
        k = args.index("--dump"); dump = json.load(open(args[k + 1])); del args[k:k + 2]
    args = [a for a in args if not a.startswith("--no-")]
    src, dst = args[0], args[1]
    names = args[2:] or sorted(d for d in os.listdir(src) if os.path.isdir(os.path.join(src, d)))
    for key in names:
        t0 = time.time()
        sf = os.path.join(src, key)
        if not os.path.exists(os.path.join(sf, "collision.json")):
            continue
        df = os.path.join(dst, key)
        os.makedirs(df, exist_ok=True)
        shipped = os.path.join(sf, "collision.json.orig")
        if not os.path.exists(shipped):
            shipped = os.path.join(sf, "collision.json")
        boxes = json.load(open(shipped))["boxes"]
        if not os.path.exists(os.path.join(df, "collision.json.orig")):
            shutil.copy2(shipped, os.path.join(df, "collision.json.orig"))
        added_fill, added_wall, pairs, cells = [], [], 0, 0
        if do_hidden:
            grid = art_grid(sf)
            added_fill = gc.seal_hidden_floors(grid)
        if do_leaks and dump is not None and start_of(dump, key):
            d = dump["maps"][key]
            nav = np.array(d["nav"]["pos"], np.float64).reshape(-1, 3)
            pool = d["nav"]["pool"]
            rng = np.random.default_rng(3)
            pick = rng.choice(len(pool), min(60, len(pool)), replace=False) if pool else []
            seeds = [start_of(dump, key)] + [list(nav[pool[i]]) for i in pick]
            added_wall, pairs, cells = leak_walls(sf, boxes + added_fill, seeds)
        out = boxes + added_fill + added_wall
        with open(os.path.join(df, "collision.json"), "w") as f:
            json.dump({"boxes": out}, f, separators=(",", ":"))
        print(f"[{key}] {len(boxes)} shipped + {len(added_fill)} hidden-ground fills + {len(added_wall)} leak walls "
              f"({pairs} crossings of {cells} reachable cells checked) -> {len(out)} boxes  [{time.time() - t0:.0f} s]", flush=True)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
