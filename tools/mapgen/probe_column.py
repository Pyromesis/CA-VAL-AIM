#!/usr/bin/env python3
"""Print every art surface and collision box top along a vertical line (debugging spawns that look under the map).

    python probe_column.py <maps dir> <map> x z [x z ...]

For each point: the art triangles crossed by a vertical ray (height and whether they face up or down) and the
collision boxes whose footprint contains the point (min y .. max y). Uses the same frame as gen_collision.py.
"""
import json
import math
import os
import sys

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import audit_spawns as au  # noqa: E402


def main(argv):
    maps_dir, key = argv[1], argv[2]
    pts = [(float(argv[i]), float(argv[i + 1])) for i in range(3, len(argv) - 1, 2)]
    tris, normals = au.load_triangles(os.path.join(maps_dir, key))
    idx = au.TriIndex(tris)
    boxes = json.load(open(os.path.join(maps_dir, key, "collision.json")))["boxes"]
    for (x, z) in pts:
        cand = idx.near(x - 0.05, z - 0.05, x + 0.05, z + 0.05)
        tr = idx.t[cand]
        o = np.array([x, -60.0, z])
        d = np.array([0.0, 1.0, 0.0])
        hits = []
        lo = -60.0
        rest = np.arange(len(tr))
        # collect all hits by repeatedly casting past the last one
        for _ in range(40):
            t, k = au.ray_hits(tr, np.array([x, lo, z]), d, 200.0)
            if k < 0:
                break
            y = lo + t
            hits.append((round(y, 2), "up" if normals[cand[k]][1] > 0.3 else ("down" if normals[cand[k]][1] < -0.3 else "side")))
            lo = y + 0.01
        cb = [(round(b[1], 2), round(b[4], 2)) for b in boxes if b[0] <= x <= b[3] and b[2] <= z <= b[5]]
        print(f"({x}, {z}) art: {hits}")
        print(f"          collision (min..top): {sorted(cb)}")


if __name__ == "__main__":
    main(sys.argv)
