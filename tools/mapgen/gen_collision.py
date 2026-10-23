#!/usr/bin/env python3
"""Generate ValTrainer collision.json from the extracted map GLBs.

The shipped collision files were produced by a pipeline that only covered part of
the art, which is why some maps have a sealed-off spawn island (you walk into an
invisible wall) or a spawn inside the floor. This tool rebuilds the collision from
every GLB listed in the map manifest, in the same final frame the game uses:

    glTF (Blender Y-up)  ->  (x, z, -y)  ->  + origin  ->  metres

It emits the same compact format the runtime reads:

    { "boxes": [[minX, minY, minZ, maxX, maxY, maxZ], ...] }

Walkable surfaces (up-facing triangles) become thin slabs; steep triangles become
vertical wall columns. Both are snapped to a grid and merged with greedy meshing so
the box count stays in the range the AABB collider can handle.
"""

import glob
import json
import math
import os
import re
import struct
import sys
from collections import defaultdict

import numpy as np

# ---------------------------------------------------------------- glTF reading

_COMP = {
    5120: np.int8, 5121: np.uint8, 5122: np.int16,
    5123: np.uint16, 5125: np.uint32, 5126: np.float32,
}
_NCOMP = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def read_glb(path):
    with open(path, "rb") as f:
        magic, version, _ = struct.unpack("<III", f.read(12))
        if magic != 0x46546C67:
            raise ValueError(f"not a GLB: {path}")
        clen, ctype = struct.unpack("<II", f.read(8))
        js = json.loads(f.read(clen))
        js["_bin"] = b""
        if f.tell() < os.path.getsize(path):
            blen, btype = struct.unpack("<II", f.read(8))
            js["_bin"] = f.read(blen)
    return js


def accessor(js, index):
    a = js["accessors"][index]
    bv = js["bufferViews"][a["bufferView"]]
    dt = _COMP[a["componentType"]]
    n = _NCOMP[a["type"]]
    count = a["count"]
    itemsize = np.dtype(dt).itemsize
    off = bv.get("byteOffset", 0) + a.get("byteOffset", 0)
    stride = bv.get("byteStride") or itemsize * n
    blob = js["_bin"]
    if stride == itemsize * n:
        arr = np.frombuffer(blob, dtype=dt, count=count * n, offset=off).reshape(count, n)
    else:
        raw = np.frombuffer(blob, dtype=np.uint8, count=(count - 1) * stride + itemsize * n, offset=off)
        arr = np.lib.stride_tricks.as_strided(raw, shape=(count, n), strides=(stride, itemsize)).copy()
        arr = np.ascontiguousarray(arr).view(dt).reshape(count, n)
    return arr


def node_matrix(n):
    if "matrix" in n:
        m = np.array(n["matrix"], dtype=np.float64).reshape(4, 4).T
        return m
    t = np.array(n.get("translation", [0, 0, 0]), dtype=np.float64)
    s = np.array(n.get("scale", [1, 1, 1]), dtype=np.float64)
    x, y, z, w = n.get("rotation", [0, 0, 0, 1])
    rot = np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ], dtype=np.float64)
    m = np.eye(4)
    m[:3, :3] = rot * s[None, :]
    m[:3, 3] = t
    return m


def world_triangles(js):
    """Yield (positions (N,3), indices (M,3)) in glTF world space for one file."""
    nodes = js.get("nodes", [])
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get("children", []):
            parent[c] = i
    out = []
    for i, n in enumerate(nodes):
        if "mesh" not in n:
            continue
        m = node_matrix(n)
        p = parent.get(i)
        while p is not None:
            m = node_matrix(nodes[p]) @ m
            p = parent.get(p)
        for prim in js["meshes"][n["mesh"]].get("primitives", []):
            pos_i = prim.get("attributes", {}).get("POSITION")
            if pos_i is None:
                continue
            pos = accessor(js, pos_i).astype(np.float64)
            if prim.get("mode", 4) != 4:
                continue
            pos = pos @ m[:3, :3].T + m[:3, 3]
            if "indices" in prim:
                idx = accessor(js, prim["indices"]).astype(np.int64).ravel()
            else:
                idx = np.arange(len(pos), dtype=np.int64)
            if len(idx) < 3:
                continue
            tris = idx[: len(idx) // 3 * 3].reshape(-1, 3)
            out.append((pos, tris))
    return out


def map_levels(folder):
    """(levels, origin) for a map folder; Ascent has no manifest and uses a fixed frame."""
    mp = os.path.join(folder, "manifest.json")
    if os.path.exists(mp):
        man = json.load(open(mp))
        return man["levels"], (man.get("origin_x", 0.0), man.get("origin_y", 0.0), man.get("origin_z", 0.0)), man
    ascent = ["Ascent_Art_A", "Ascent_Art_AtkPathA", "Ascent_Art_Atk",
              "Ascent_Art_APathMid", "Ascent_Art_DefPathA", "Ascent_Art_Def"]
    return ascent, (50.0, -3.8, 61.0), None


# ---------------------------------------------------------------- rasterising

class Grid:
    def __init__(self, cell, clip=None):
        self.cell = cell
        self.clip = clip                 # (minx, miny, minz, maxx, maxy, maxz) or None
        self.floors = defaultdict(set)   # (i,j) -> {quantised y}
        self.walls = defaultdict(list)   # (i,j) -> [(y0, y1)]

    def inside(self, x, y, z):
        c = self.clip
        if c is None:
            return True
        return c[0] <= x <= c[3] and c[1] <= y <= c[4] and c[2] <= z <= c[5]

    def add_floor(self, i, j, y):
        if self.clip is not None:
            cx = (i + 0.5) * self.cell
            cz = (j + 0.5) * self.cell
            if not self.inside(cx, y, cz):
                return
        self.floors[(i, j)].add(round(y / 0.05) * 0.05)

    def add_wall(self, i, j, y0, y1):
        if y1 - y0 < 0.4:
            return
        if self.clip is not None:
            c = self.clip
            cx = (i + 0.5) * self.cell
            cz = (j + 0.5) * self.cell
            if not (c[0] <= cx <= c[3] and c[2] <= cz <= c[5]):
                return
            y0, y1 = max(y0, c[1]), min(y1, c[4])
            if y1 - y0 < 0.4:
                return
        self.walls[(i, j)].append((y0, y1))


def rasterise(grid, v0, v1, v2):
    cell = grid.cell
    n = np.cross(v1 - v0, v2 - v0)
    ln = np.linalg.norm(n)
    if ln < 1e-9:
        return
    area = ln * 0.5
    if area < 0.02:
        return
    n = n / ln

    # Ramps and stairs are walkable even when their normal leans well away from up
    # (a 40-50 degree ramp has n.y ~0.64-0.77, a steep 60 degree one 0.5). Treat every
    # surface up to ~70 degrees as floor and let the 0.5 m grid turn it into steps the
    # AABB collider can climb; only near-vertical triangles become walls.
    # Only UP-facing triangles are floors.  Using abs(n[1]) here also turned every
    # ceiling into a walkable slab, which let the reachability flood climb onto the
    # B Short ceiling (-1.65) in Bind and walk under the real site floor (0.4)
    # instead of taking the ramp.  Down-facing triangles are left out; headroom is
    # already handled by the 1.8 m player height in the collision query.
    if n[1] >= 0.35:
        # walkable surface: sample the XZ projection on a lattice
        s = cell * 0.75
        xs = np.arange(math.floor(min(v0[0], v1[0], v2[0]) / s) * s,
                       max(v0[0], v1[0], v2[0]) + s, s)
        zs = np.arange(math.floor(min(v0[2], v1[2], v2[2]) / s) * s,
                       max(v0[2], v1[2], v2[2]) + s, s)
        if len(xs) == 0 or len(zs) == 0:
            return
        if len(xs) * len(zs) > 40000:
            # Huge triangle (a terrain plate or a backdrop): split it in four and recurse
            # instead of dropping it — dropping it is how whole floors go missing.
            if area > 4.0:
                m01 = (v0 + v1) * 0.5
                m12 = (v1 + v2) * 0.5
                m20 = (v2 + v0) * 0.5
                for a, b, c in ((v0, m01, m20), (m01, v1, m12), (m20, m12, v2), (m01, m12, m20)):
                    rasterise(grid, a, b, c)
            return
        gx, gz = np.meshgrid(xs, zs)
        px, pz = gx.ravel(), gz.ravel()
        # barycentric test in XZ
        d = (v1[2] - v2[2]) * (v0[0] - v2[0]) + (v2[0] - v1[0]) * (v0[2] - v2[2])
        if abs(d) < 1e-9:
            return
        w0 = ((v1[2] - v2[2]) * (px - v2[0]) + (v2[0] - v1[0]) * (pz - v2[2])) / d
        w1 = ((v2[2] - v0[2]) * (px - v2[0]) + (v0[0] - v2[0]) * (pz - v2[2])) / d
        w2 = 1.0 - w0 - w1
        m = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
        if not m.any():
            return
        y = w0[m] * v0[1] + w1[m] * v1[1] + w2[m] * v2[1]
        ii = np.floor(px[m] / cell).astype(np.int64)
        jj = np.floor(pz[m] / cell).astype(np.int64)
        for a, b, yy in zip(ii.tolist(), jj.tolist(), y.tolist()):
            grid.add_floor(a, b, yy)
    elif abs(n[1]) <= 0.3:
        # vertical(ish): footprint is a line in XZ, mark the cells it crosses.
        # Low railings, thin dividers and window frames are visible cover: dropping them
        # is how players end up walking through walls. Only ignore tiny trim.
        y0 = float(min(v0[1], v1[1], v2[1]))
        y1 = float(max(v0[1], v1[1], v2[1]))
        if y1 - y0 < 0.6 or area < 0.4:
            return
        pts = np.stack([v0, v1, v2, v0])[:, [0, 2]]
        seen = set()
        for k in range(3):
            a, b = pts[k], pts[k + 1]
            steps = max(1, int(math.ceil(np.hypot(*(b - a)) / (cell * 0.5))))
            t = np.linspace(0.0, 1.0, steps + 1)[:, None]
            seg = a[None, :] * (1 - t) + b[None, :] * t
            ii = np.floor(seg[:, 0] / cell).astype(np.int64)
            jj = np.floor(seg[:, 1] / cell).astype(np.int64)
            for x, z in zip(ii.tolist(), jj.tolist()):
                seen.add((x, z))
        for (x, z) in seen:
            grid.add_wall(x, z, y0, y1)


# ---------------------------------------------------------------- meshing out

def greedy_rects(cells):
    """Merge a set of (i,j) cells into maximal axis-aligned rectangles."""
    remaining = set(cells)
    rects = []
    for (i, j) in sorted(remaining):
        if (i, j) not in remaining:
            continue
        # grow +i
        w = 1
        while (i + w, j) in remaining:
            w += 1
        # grow +j while the whole row is present
        h = 1
        while all((i + k, j + h) in remaining for k in range(w)):
            h += 1
        for k in range(w):
            for l in range(h):
                remaining.discard((i + k, j + l))
        rects.append((i, j, w, h))
    return rects


def merged_intervals(intervals):
    if not intervals:
        return []
    iv = sorted(intervals)
    out = [list(iv[0])]
    for a, b in iv[1:]:
        if a <= out[-1][1] + 1e-6:
            out[-1][1] = max(out[-1][1], b)
        else:
            out.append([a, b])
    return [tuple(x) for x in out]


def _bucket(boxes, size=2.0):
    b = defaultdict(list)
    for bx in boxes:
        for i in range(int(math.floor(bx[0] / size)), int(math.floor(bx[3] / size)) + 1):
            for j in range(int(math.floor(bx[2] / size)), int(math.floor(bx[5] / size)) + 1):
                b[(i, j)].append(bx)
    return b


def merge_with_existing(new_boxes, old_boxes, floor_tol=0.35, wall_overlap=0.4, gap_only=True):
    """Keep the hand-tuned old collision and add only what it is missing.

    The shipped files have good walls where they have anything at all, but whole areas
    (Lotus/Pearl/Sunset spawn, the site approaches) were never covered. Dropping a
    generated floor on top of an existing one would double the surface and create
    bumps, so floors within `floor_tol` of an existing top are skipped; walls are
    skipped when they largely overlap something already there.
    """
    buckets = _bucket(old_boxes)
    kept = []
    for bx in new_boxes:
        cx = (bx[0] + bx[3]) * 0.5
        cz = (bx[2] + bx[5]) * 0.5
        cell = (int(math.floor(cx / 2.0)), int(math.floor(cz / 2.0)))
        near = buckets.get(cell, ())
        thin = (bx[4] - bx[1]) < 0.45            # floor slab
        if thin:
            covered = False
            for o in near:
                if o[0] <= cx <= o[3] and o[2] <= cz <= o[5] and abs(o[4] - bx[4]) <= floor_tol:
                    covered = True
                    break
            if covered:
                continue
        else:
            # Walls: only fill areas the shipped file never covered at all. A generated
            # wall next to an existing one (or in a tight callout) is what turns a
            # doorway into an invisible wall, so require an empty neighbourhood.
            if gap_only:
                lonely = True
                for o in near:
                    ox = (o[0] + o[3]) * 0.5
                    oz = (o[2] + o[5]) * 0.5
                    if abs(ox - cx) <= 2.5 and abs(oz - cz) <= 2.5:
                        lonely = False
                        break
                if not lonely:
                    continue
            vol = max(1e-6, (bx[3] - bx[0]) * (bx[4] - bx[1]) * (bx[5] - bx[2]))
            overlap = 0.0
            for o in near:
                dx = min(bx[3], o[3]) - max(bx[0], o[0])
                dz = min(bx[5], o[5]) - max(bx[2], o[2])
                dy = min(bx[4], o[4]) - max(bx[1], o[1])
                if dx > 0 and dz > 0 and dy > 0:
                    overlap += dx * dz * dy
            if overlap / vol >= wall_overlap:
                continue
        kept.append(bx)
    return kept


def build_boxes(grid):
    """Two passes: thin floor slabs with their exact surface height, then wall prisms.

    Floors and walls are meshed separately so that a wall's footprint is identical on
    every level it spans (windows aside) and can be merged vertically into one box,
    while floors keep the exact art height the drills were authored against.
    """
    c = grid.cell
    thick = 0.3
    boxes = []

    # --- floors: group cells by surface height (0.25 m) and merge in XZ
    qy = 0.25
    by_height = defaultdict(set)
    heights = {}
    for (i, j), ys in grid.floors.items():
        for y in ys:
            key = round(y / qy) * qy
            by_height[key].add((i, j))
            heights.setdefault(key, y)
    for key, cells in by_height.items():
        top = heights[key]
        for (i, j, w, d) in greedy_rects(cells):
            boxes.append([round(i * c, 3), round(top - thick, 3), round(j * c, 3),
                          round((i + w) * c, 3), round(top, 3), round((j + d) * c, 3)])
    n_floor = len(boxes)

    # --- walls: solid voxel columns, grown upward greedily per rectangle
    if os.environ.get("MAPGEN_WALLS", "1") == "0":
        return boxes
    solid = set()
    for (i, j), ivs in grid.walls.items():
        for (a, b) in merged_intervals(ivs):
            k0 = int(math.floor(a / c))
            k1 = max(k0, int(math.ceil(b / c)) - 1)
            for k in range(k0, k1 + 1):
                solid.add((i, j, k))

    layers = defaultdict(set)
    for (i, j, k) in solid:
        layers[k].add((i, j))
    slices = {k: greedy_rects(cells) for k, cells in layers.items()}

    done = set()   # (cell, k) already inside an emitted box
    for k in sorted(slices):
        for (i, j, w, d) in slices[k]:
            if ((i, j), k) in done:
                continue
            h = 1
            while True:
                nxt = layers.get(k + h)
                if not nxt:
                    break
                cells = [(a, b) for a in range(i, i + w) for b in range(j, j + d)]
                if not all(cl in nxt for cl in cells):
                    break
                for cl in cells:
                    done.add((cl, k + h))
                h += 1
            i0 = i * c + 0.08
            i1 = (i + w) * c - 0.08
            j0 = j * c + 0.08
            j1 = (j + d) * c - 0.08
            if i1 - i0 < 0.12:
                i0, i1 = (i + w * 0.5) * c - 0.06, (i + w * 0.5) * c + 0.06
            if j1 - j0 < 0.12:
                j0, j1 = (j + d * 0.5) * c - 0.06, (j + d * 0.5) * c + 0.06
            boxes.append([round(i0, 3), round(k * c, 3), round(j0, 3),
                          round(i1, 3), round((k + h) * c, 3), round(j1, 3)])
    if os.environ.get("MAPGEN_DEBUG"):
        print(f"    floors={n_floor} walls={len(boxes) - n_floor} height_levels={len(by_height)} wall_layers={len(slices)}")
    return boxes


SPOT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "ValTrainerGodot", "src", "Maps")
_V = re.compile(r"V\(\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*\)")


def spot_bounds(folder_key, pad_xz=50.0, pad_up=14.0, pad_down=10.0):
    """Playable volume around the authored drill points of a map.

    The GLBs also contain skyboxes, distant backdrops and giant water/terrain plates;
    rasterising those produced thousands of boxes hundreds of metres away (Fracture
    alone reached +/-1000 m) and phantom floors far below the map. Everything outside
    this volume is dropped.
    """
    env = os.environ.get("MAPGEN_CLIP")
    if env is not None:
        if env.strip() == "0":
            return None
        vals = [float(v) for v in env.split(",")]
        return tuple(vals) if len(vals) == 6 else None
    for path in glob.glob(os.path.join(SPOT_DIR, "MapSpots.*.cs")):
        txt = open(path, encoding="utf-8", errors="replace").read()
        m = re.search(r'RealMapFolder\s*=\s*"maps/([a-z0-9]+)"', txt)
        if not m or m.group(1) != folder_key:
            continue
        pts = [tuple(float(v) for v in t) for t in _V.findall(txt)]
        if not pts:
            return None
        xs = [p[0] for p in pts]
        ys = [p[1] for p in pts]
        zs = [p[2] for p in pts]
        return (min(xs) - pad_xz, min(ys) - pad_down, min(zs) - pad_xz,
                max(xs) + pad_xz, max(ys) + pad_up, max(zs) + pad_xz)
    return None


def generate(folder, cell=0.5, verbose=True):
    levels, origin, man = map_levels(folder)
    ox, oy, oz = origin
    key = os.path.basename(os.path.normpath(folder)).lower()
    clip = spot_bounds(key)
    grid = Grid(cell, clip)
    if verbose:
        print(f"    clip={None if clip is None else [round(v,1) for v in clip]}")
    tris = 0
    for level in levels:
        # Vista / Skybox levels are mostly painted backdrops hundreds of metres
        # outside the playable map.  The clip already drops those, but some maps
        # (Bind) also keep real walkable ramps in the Vista level, so allow it to be
        # included explicitly instead of discarding the whole level.
        if (re.search(r"(Vista|Skybox|Backdrop)", level, re.I)
                and os.environ.get("MAPGEN_INCLUDE_VISTA", "0") != "1"):
            if verbose:
                print(f"    skip backdrop: {level}")
            continue
        path = os.path.join(folder, level + ".glb")
        if not os.path.exists(path) or os.path.getsize(path) < 1024:
            if verbose:
                print(f"    skip missing/empty level: {level}")
            continue
        js = read_glb(path)
        parts = world_triangles(js)
        for pos, tri in parts:
            w = pos[tri]                      # (M,3,3)
            # final frame: (x, z, -y) + origin
            f = np.empty_like(w)
            f[:, :, 0] = w[:, :, 2] + ox
            f[:, :, 1] = w[:, :, 1] + oy
            f[:, :, 2] = -w[:, :, 0] + oz
            for t in f:
                rasterise(grid, t[0], t[1], t[2])
            tris += len(tri)
        if verbose:
            print(f"    {level}: {len(tri)} tris")
        del js
    boxes = build_boxes(grid)
    return boxes, tris


def main(argv):
    base = argv[1] if len(argv) > 1 else r"C:\Users\berna\AppData\Local\Programs\ValTrainer\maps"
    names = argv[2:] or sorted(d for d in os.listdir(base) if os.path.isdir(os.path.join(base, d)))
    for name in names:
        folder = os.path.join(base, name)
        if not os.path.exists(os.path.join(folder, "collision.json")):
            continue
        print(f"[{name}]")
        boxes, tris = generate(folder)
        out = os.path.join(folder, "collision.json")
        if os.path.exists(out) and not os.path.exists(out + ".orig"):
            os.replace(out, out + ".orig")
        if os.path.exists(out + ".orig") and not os.environ.get("MAPGEN_FRESH"):
            old = json.load(open(out + ".orig"))["boxes"]
            added = merge_with_existing(boxes, old)
            print(f"    {len(old)} existing + {len(added)} added / {len(boxes)} generated")
            boxes = old + added
        with open(out, "w") as f:
            json.dump({"boxes": boxes}, f, separators=(",", ":"))
        print(f"    {tris} tris -> {len(boxes)} boxes")


if __name__ == "__main__":
    main(sys.argv)

