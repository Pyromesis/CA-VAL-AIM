#!/usr/bin/env python3
"""Fits collision.json to the art the game actually draws.

gen_collision.py voxelises *every* triangle of *every* level at 0.5 m. The runtime, however, does not draw all of
it (World/GltfStrip.cs drops the EnvCollision hulls and the texture-only decals, World/RealMap.cs skips the
backdrop levels and Fracture's two prototype shells), and a 0.5 m voxel reaches up to half a metre past the
surface it came from. Both leave collision where the player sees nothing: bullets stop in mid-air next to a
corner or above a crate, and bodies bump into empty space.

This tool samples the *drawn* triangles into a 0.125 m occupancy set and then, for every box:

* wall / prop boxes (taller than a floor slab) with no drawn surface inside are removed;
* the others are shrunk in X, Z and at the top to the drawn surface inside them (never grown, bottoms kept);
* floor slabs are left alone unless --floors is given (then slabs with no drawn floor are removed too).

    python trim_collision.py <maps dir> [map ...] [--report] [--floors] [--backup <dir>]

--report only prints what would change. Without it the previous file is copied to <backup>/<map>/collision.json
(default: <maps dir>/../collision-pretrim) the first time, and collision.json is rewritten.
Run audit_collision.py afterwards: reachability must not get worse.
"""
import json
import os
import re
import shutil
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_collision as gc  # noqa: E402

FINE = 0.125          # occupancy resolution (m)
SAMPLE = 0.09         # lattice spacing on the triangles (m), below FINE so no crossed cell is missed
SLAB = 0.45           # boxes at most this tall are floor slabs
PAD = 0.02            # a drawn surface this close to a box still counts as inside it

SKIP_WORDS = ("decal", "graffiti", "floater", "papers", "stain", "sticker", "poster")
# RealMap.IsBackdrop
BACKDROP = re.compile(r"(vista|skybox|backdrop)", re.I)
HIDDEN_LEVELS = {"canyon_apathnomid", "canyon_asidepath"}


def drawn(name):
    """GltfStrip.Classify: P.Collision and P.Skip primitives are removed from the mesh."""
    n = (name or "").lower()
    if "envcollision" in n:
        return False
    return not any(w in n for w in SKIP_WORDS)


def level_drawn(level):
    return not BACKDROP.search(level) and level.lower() not in HIDDEN_LEVELS


def drawn_triangles(js):
    """(M,3,3) arrays in glTF world space, one per drawn primitive."""
    nodes = js.get("nodes", [])
    mats = js.get("materials", [])
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get("children", []):
            parent[c] = i
    for i, n in enumerate(nodes):
        if "mesh" not in n:
            continue
        m = gc.node_matrix(n)
        p = parent.get(i)
        while p is not None:
            m = gc.node_matrix(nodes[p]) @ m
            p = parent.get(p)
        for prim in js["meshes"][n["mesh"]].get("primitives", []):
            mi = prim.get("material")
            if mi is not None and 0 <= mi < len(mats) and not drawn(mats[mi].get("name", "")):
                continue
            pos_i = prim.get("attributes", {}).get("POSITION")
            if pos_i is None or prim.get("mode", 4) != 4:
                continue
            pos = gc.accessor(js, pos_i).astype(np.float64)
            pos = pos @ m[:3, :3].T + m[:3, 3]
            if "indices" in prim:
                idx = gc.accessor(js, prim["indices"]).astype(np.int64).ravel()
            else:
                idx = np.arange(len(pos), dtype=np.int64)
            if len(idx) < 3:
                continue
            yield pos[idx[: len(idx) // 3 * 3].reshape(-1, 3)]


class Occupancy:
    """Sparse set of FINE cells, stored as one 64-bit mask per 4x4x4 block."""
    K = 1 << 13

    def __init__(self, lo, hi):
        self.lo = np.asarray(lo, dtype=np.float64) - 1.0
        self.hi = np.asarray(hi, dtype=np.float64) + 1.0
        self.parts = []
        self.keys = np.zeros(0, dtype=np.int64)
        self.masks = np.zeros(0, dtype=np.uint64)

    def add_points(self, p):
        ok = np.all((p >= self.lo) & (p <= self.hi), axis=1)
        p = p[ok]
        if len(p) == 0:
            return
        f = np.floor((p - self.lo) / FINE).astype(np.int64)
        c = f >> 2
        bit = (f[:, 0] & 3) | ((f[:, 1] & 3) << 2) | ((f[:, 2] & 3) << 4)
        key = (c[:, 0] * self.K + c[:, 1]) * self.K + c[:, 2]
        self.parts.append(np.unique(key * 64 + bit))
        if sum(len(x) for x in self.parts) > 40_000_000:
            self._fold()

    def _fold(self):
        if self.parts:
            self.parts = [np.unique(np.concatenate(self.parts))]

    def add_triangles(self, t):
        """t: (M,3,3) in the final frame."""
        lo = t.min(axis=1)
        hi = t.max(axis=1)
        t = t[np.all((hi >= self.lo) & (lo <= self.hi), axis=1)]
        # Split triangles that would need a huge lattice (terrain plates), dropping the parts outside the volume.
        for _ in range(12):
            if len(t) == 0:
                return
            e = np.maximum.reduce([np.linalg.norm(t[:, 0] - t[:, 1], axis=1), np.linalg.norm(t[:, 1] - t[:, 2], axis=1),
                                   np.linalg.norm(t[:, 2] - t[:, 0], axis=1)])
            big = e > SAMPLE * 48
            if not big.any():
                break
            b = t[big]
            a, bb, c = b[:, 0], b[:, 1], b[:, 2]
            ab, bc, ca = (a + bb) / 2, (bb + c) / 2, (c + a) / 2
            parts = np.concatenate([np.stack([a, ab, ca], 1), np.stack([ab, bb, bc], 1), np.stack([ca, bc, c], 1), np.stack([ab, bc, ca], 1)])
            lo = parts.min(axis=1)
            hi = parts.max(axis=1)
            parts = parts[np.all((hi >= self.lo) & (lo <= self.hi), axis=1)]
            t = np.concatenate([t[~big], parts])
        if len(t) == 0:
            return
        e = np.maximum.reduce([np.linalg.norm(t[:, 0] - t[:, 1], axis=1), np.linalg.norm(t[:, 1] - t[:, 2], axis=1),
                               np.linalg.norm(t[:, 2] - t[:, 0], axis=1)])
        n = np.clip(np.ceil(e / SAMPLE).astype(np.int64), 1, 64)
        for k in np.unique(n):
            g = t[n == k]
            i, j = np.meshgrid(np.arange(k + 1), np.arange(k + 1), indexing="ij")
            keep = i + j <= k
            u = (i[keep] / k)[None, :, None]
            v = (j[keep] / k)[None, :, None]
            step = max(1, 2_000_000 // u.shape[1])
            for s in range(0, len(g), step):
                h = g[s:s + step]
                p = h[:, 0:1] * (1 - u - v) + h[:, 1:2] * u + h[:, 2:3] * v
                self.add_points(p.reshape(-1, 3))

    def finish(self):
        self._fold()
        allv = self.parts[0] if self.parts else np.zeros(0, dtype=np.int64)
        self.parts = []
        key = allv >> 6
        bit = (allv & 63).astype(np.uint64)
        if len(key) == 0:
            return
        start = np.flatnonzero(np.concatenate([[True], key[1:] != key[:-1]]))
        self.keys = key[start]
        self.masks = np.bitwise_or.reduceat(np.left_shift(np.uint64(1), bit), start)

    _bits = np.arange(64, dtype=np.uint64)
    _off = np.stack([np.arange(64) & 3, (np.arange(64) >> 2) & 3, (np.arange(64) >> 4) & 3], axis=1)

    def cells_in(self, b0, b1):
        """Fine cell indices (N,3) of the occupied cells overlapping the box [b0, b1] (already padded)."""
        f0 = np.floor((np.asarray(b0) - self.lo) / FINE).astype(np.int64)
        f1 = np.floor((np.asarray(b1) - self.lo) / FINE).astype(np.int64)
        c0, c1 = f0 >> 2, f1 >> 2
        out = []
        xs = np.arange(c0[0], c1[0] + 1)
        ys = np.arange(c0[1], c1[1] + 1)
        zs = np.arange(c0[2], c1[2] + 1)
        # Slices of at most ~200k blocks so a 60 m floor does not allocate everything at once.
        per = max(1, 200_000 // max(1, len(ys) * len(zs)))
        for s in range(0, len(xs), per):
            gx, gy, gz = np.meshgrid(xs[s:s + per], ys, zs, indexing="ij")
            key = ((gx * self.K + gy) * self.K + gz).ravel()
            pos = np.searchsorted(self.keys, key)
            pos[pos >= len(self.keys)] = 0
            hit = len(self.keys) > 0 and self.keys[pos] == key
            if not np.any(hit):
                continue
            key = key[hit]
            m = self.masks[pos[hit]]
            on = ((m[:, None] >> self._bits[None, :]) & np.uint64(1)).astype(bool)
            bi, bj = np.nonzero(on)
            k = key[bi]
            c = np.stack([k // (self.K * self.K), (k // self.K) % self.K, k % self.K], axis=1)
            f = c * 4 + self._off[bj]
            f = f[np.all((f >= f0) & (f <= f1), axis=1)]
            if len(f):
                out.append(f)
        return np.concatenate(out) if out else np.zeros((0, 3), dtype=np.int64)


def build_occupancy(folder, boxes, verbose=True):
    levels, origin, _ = gc.map_levels(folder)
    ox, oy, oz = origin
    b = np.asarray(boxes, dtype=np.float64)
    occ = Occupancy(b[:, :3].min(axis=0), b[:, 3:].max(axis=0))
    tris = 0
    for level in levels:
        if not level_drawn(level):
            if verbose:
                print(f"    not drawn: {level}")
            continue
        path = os.path.join(folder, level + ".glb")
        if not os.path.exists(path) or os.path.getsize(path) < 1024:
            continue
        js = gc.read_glb(path)
        for w in drawn_triangles(js):
            f = np.empty_like(w)
            f[:, :, 0] = w[:, :, 2] + ox
            f[:, :, 1] = w[:, :, 1] + oy
            f[:, :, 2] = -w[:, :, 0] + oz
            occ.add_triangles(f)
            tris += len(f)
        del js
    occ.finish()
    if verbose:
        print(f"    {tris} drawn triangles, {len(occ.keys)} occupied blocks")
    return occ


def trim(boxes, occ, floors=False):
    out = []
    st = dict(walls=0, wall_removed=0, wall_shrunk=0, floors=0, floor_phantom=0, vol_before=0.0, vol_after=0.0)
    for bx in boxes:
        x0, y0, z0, x1, y1, z1 = bx
        slab = (y1 - y0) <= SLAB
        f = occ.cells_in((x0 - PAD, y0 - PAD, z0 - PAD), (x1 + PAD, y1 + PAD, z1 + PAD))
        if slab:
            st["floors"] += 1
            if len(f) == 0:
                st["floor_phantom"] += 1
                if floors:
                    continue
            out.append(bx)
            continue
        st["walls"] += 1
        st["vol_before"] += (x1 - x0) * (y1 - y0) * (z1 - z0)
        if len(f) == 0:
            st["wall_removed"] += 1
            continue
        lo = f.min(axis=0) * FINE + occ.lo
        hi = (f.max(axis=0) + 1) * FINE + occ.lo
        nx0, nx1 = max(x0, lo[0]), min(x1, hi[0])
        nz0, nz1 = max(z0, lo[2]), min(z1, hi[2])
        ny1 = min(y1, hi[1])
        if nx1 - nx0 < 0.02 or nz1 - nz0 < 0.02 or ny1 - y0 < 0.05:
            st["wall_removed"] += 1
            continue
        nb = [round(float(v), 3) for v in (nx0, y0, nz0, nx1, ny1, nz1)]
        if nb != [round(float(v), 3) for v in bx]:
            st["wall_shrunk"] += 1
        st["vol_after"] += (nx1 - nx0) * (ny1 - y0) * (nz1 - nz0)
        out.append(nb)
    return out, st


def main(argv):
    args = [a for a in argv[1:] if not a.startswith("--")]
    report = "--report" in argv
    floors = "--floors" in argv
    if not args:
        print(__doc__)
        return 2
    root = args[0]
    backup = os.path.join(os.path.dirname(os.path.normpath(root)), "collision-pretrim")
    if "--backup" in argv:
        backup = argv[argv.index("--backup") + 1]
        args = [a for a in args if a != backup]
    keys = args[1:] or sorted(d for d in os.listdir(root) if os.path.exists(os.path.join(root, d, "collision.json")))
    for key in keys:
        folder = os.path.join(root, key)
        path = os.path.join(folder, "collision.json")
        saved = os.path.join(backup, key, "collision.json")
        # Always trim the untrimmed file, so a second run does not shrink twice from rounded extents.
        src = saved if os.path.exists(saved) else path
        doc = json.load(open(src))
        boxes = doc["boxes"]
        print(f"{key}: {len(boxes)} boxes ({'backup' if src == saved else 'current'})")
        occ = build_occupancy(folder, boxes)
        out, st = trim(boxes, occ, floors)
        pct = 100.0 * (1 - st["vol_after"] / st["vol_before"]) if st["vol_before"] else 0.0
        print(f"    walls {st['walls']}: removed {st['wall_removed']} with nothing drawn, shrunk {st['wall_shrunk']}, "
              f"solid volume -{pct:.1f}%")
        print(f"    floor slabs {st['floors']}: {st['floor_phantom']} with nothing drawn"
              f" ({'removed' if floors else 'kept'})")
        if report:
            continue
        if not os.path.exists(saved):
            os.makedirs(os.path.dirname(saved), exist_ok=True)
            shutil.copy2(path, saved)
        doc["boxes"] = out
        with open(path, "w") as fh:
            json.dump(doc, fh, separators=(",", ":"))
        print(f"    wrote {len(out)} boxes")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
