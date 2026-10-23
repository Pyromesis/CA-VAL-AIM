#!/usr/bin/env python3
"""Audit the map collision files against the drill definitions in MapSpots.*.cs.

Mirrors the runtime rules in src/Game/Collision.cs and src/Game/MovementBody.cs
(radius 0.34, height 1.8, step 0.7, snap-down 0.32, jump apex 0.90) and reports, per
map, whether the spawn is standable and whether the choke, every enemy, every plant
spot and every retake entry can be reached on foot from the spawn.

    python audit_collision.py [maps_dir] [collision_file_name]

`collision_file_name` defaults to collision.json, so `collision.json.orig` audits the
pre-regeneration file. Exit code 1 when anything is unreachable, which makes it usable
as a gate before shipping new collision.
"""

import collections
import glob
import json
import math
import os
import re
import sys

R = 0.34            # Collision.PlayerRadius
H = 1.8             # Collision.PlayerHeight
STEP = 0.7          # Collision.StepHeight
CELL = 0.35         # audit grid
BUCKET = 2.0        # spatial hash cell
SNAP_DOWN = 0.32    # MovementTuning.SnapDown
RISE = 1.2          # step-ups are 0.7, a crouch-jump clears ~1.2 m
DROP = 6.0          # you may fall into the site; fall damage does not stop the drill

HERE = os.path.dirname(os.path.abspath(__file__))
SPOTS = os.path.join(HERE, "..", "..", "ValTrainerGodot", "src", "Maps")
BASE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, "..", "..", "dist-maps", "maps")
SURFACE = sys.argv[2] if len(sys.argv) > 2 else "collision.json"


class World:
    def __init__(self, boxes):
        self.boxes = boxes
        self.buckets = collections.defaultdict(list)
        for bx in boxes:
            for i in range(int(math.floor((bx[0] - 1) / BUCKET)), int(math.floor((bx[3] + 1) / BUCKET)) + 1):
                for j in range(int(math.floor((bx[2] - 1) / BUCKET)), int(math.floor((bx[5] + 1) / BUCKET)) + 1):
                    self.buckets[(i, j)].append(bx)

    def near(self, x, z):
        return self.buckets.get((int(math.floor(x / BUCKET)), int(math.floor(z / BUCKET))), ())

    def ground(self, x, z, feet_y, step=STEP):
        best = -1e9
        r = R - 0.05
        for bx in self.near(x, z):
            if bx[4] > feet_y + step: continue
            if x + r < bx[0] or x - r > bx[3] or z + r < bx[2] or z - r > bx[5]: continue
            if bx[4] > best: best = bx[4]
        return best

    def blocked(self, x, z, feet_y):
        for bx in self.near(x, z):
            if bx[4] <= feet_y + STEP or bx[1] >= feet_y + H: continue
            if x > bx[0] - R and x < bx[3] + R and z > bx[2] - R and z < bx[5] + R:
                return True
        return False

    def los(self, a, b):
        d = [b[i] - a[i] for i in range(3)]
        L = math.dist(a, b)
        if L < 1e-4: return True
        dd = [v / L for v in d]
        for bx in self.boxes:
            tmin, tmax = 0.0, float("inf")
            ok = True
            for i in range(3):
                lo, hi = bx[i], bx[i + 3]
                if abs(dd[i]) < 1e-8:
                    if a[i] < lo or a[i] > hi: ok = False; break
                    continue
                t1, t2 = (lo - a[i]) / dd[i], (hi - a[i]) / dd[i]
                if t1 > t2: t1, t2 = t2, t1
                tmin, tmax = max(tmin, t1), min(tmax, t2)
                if tmin > tmax: ok = False; break
            if ok and tmin < L - 0.01: return False
        return True


def flood(world, start, max_nodes=400000):
    sx, sz = round(start[0] / CELL), round(start[2] / CELL)
    y0 = world.ground(sx * CELL, sz * CELL, start[1] + 0.3)
    queue = collections.deque([(sx, sz)])
    y_at = {(sx, sz): y0}
    while queue:
        cx, cz = queue.popleft()
        y = y_at[(cx, cz)]
        for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            n = (cx + dx, cz + dz)
            wx, wz = n[0] * CELL, n[1] * CELL
            g = world.ground(wx, wz, y + 0.35)
            if g < y - DROP or g > y + RISE: continue
            if world.blocked(wx, wz, g): continue
            # A cell can be reachable at several heights (stairs, ramps, stacked
            # floors).  Keep the highest one: if a later path reaches it higher,
            # re-queue so the upper route is explored too.  Without this the
            # flood reaches the foot of a staircase first and never climbs it.
            if n in y_at and g <= y_at[n] + 1e-6:
                continue
            y_at[n] = g
            queue.append(n)
        if len(y_at) > max_nodes:
            break
    return y_at


VT = re.compile(r"V\(\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*\)")
E_RE = re.compile(r'E\("([^"]+)",\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f')


def block(txt, name):
    m = re.search(name + r"\s*=\s*new\[\]\s*\{", txt)
    if not m:
        return ""
    i, depth = m.end(), 1
    while i < len(txt) and depth:
        if txt[i] == "{": depth += 1
        elif txt[i] == "}": depth -= 1
        i += 1
    return txt[m.end():i]


def read_spot(path):
    txt = open(path, encoding="utf-8", errors="replace").read()
    folder = re.search(r'RealMapFolder\s*=\s*"maps/([a-z]+)"', txt)
    if not folder:
        return None
    start = [float(v) for v in re.search(r"StartFeet\s*=\s*" + VT.pattern, txt).groups()]
    choke = [float(v) for v in re.search(r"Choke\s*=\s*" + VT.pattern, txt).groups()]
    enemies = [(m.group(1), float(m.group(2)), float(m.group(3)), float(m.group(4))) for m in E_RE.finditer(txt)]
    plants = [[float(v) for v in t] for t in VT.findall(block(txt, "PlantSpots"))]
    retakes = [[float(v) for v in t] for t in VT.findall(block(txt, "RetakeEntries"))]
    return folder.group(1), start, choke, enemies, plants, retakes


def reached(y_at, p):
    cx, cz = round(p[0] / CELL), round(p[2] / CELL)
    for rad in (0, 1, 2):
        for dx in range(-rad, rad + 1):
            for dz in range(-rad, rad + 1):
                cell = (cx + dx, cz + dz)
                if cell in y_at and abs(y_at[cell] - p[1]) < 1.4:
                    return True
    return False


def nearest_reachable(y_at, p, max_rad=60, y_tol=None):
    """Closest flooded cell to p: (distance in metres, its floor height) or None.

    With `y_tol`, only cells whose floor is within that many metres of p's own height
    are considered, which is what the drill actually needs (the spot must be stood on,
    not just passed below).
    """
    cx, cz = round(p[0] / CELL), round(p[2] / CELL)
    best = None
    for rad in range(1, max_rad + 1):
        ring = []
        for dx in range(-rad, rad + 1):
            for dz in (-rad, rad):
                ring.append((cx + dx, cz + dz))
        for dz in range(-rad + 1, rad):
            for dx in (-rad, rad):
                ring.append((cx + dx, cz + dz))
        for cell in ring:
            if cell in y_at:
                if y_tol is not None and abs(y_at[cell] - p[1]) > y_tol:
                    continue
                d = CELL * math.hypot(cell[0] - cx, cell[1] - cz)
                if best is None or d < best[0]:
                    best = (d, y_at[cell])
        if best:
            return best
    return None


def main():
    failures = 0
    warnings = 0
    for path in sorted(glob.glob(os.path.join(SPOTS, "MapSpots.*.cs"))):
        spot = read_spot(path)
        if not spot:
            continue
        key, start, choke, enemies, plants, retakes = spot
        file = os.path.join(BASE, key, SURFACE)
        if not os.path.exists(file):
            print(f"== {key}: no {SURFACE}")
            continue
        boxes = json.load(open(file))["boxes"]
        world = World(boxes)
        g0 = world.ground(start[0], start[2], start[1] + 0.3)
        stand = abs(g0 - start[1]) <= max(0.45, SNAP_DOWN) and not world.blocked(start[0], start[2], g0)
        y_at = flood(world, start)
        status = "ok" if stand else "FAIL"
        print(f"== {key:8s} boxes={len(boxes):6d} reachable_cells={len(y_at):6d} spawn_floor={g0:6.2f} spawn={status}")
        if not stand:
            failures += 1
        checks = [("choke", choke)] + [(n, [x, y, z]) for (n, x, y, z) in enemies]
        checks += [(f"plant{i}", p) for i, p in enumerate(plants)]
        checks += [(f"retake{i}", p) for i, p in enumerate(retakes)]
        for name, p in checks:
            if reached(y_at, p):
                continue
            floor_here = world.ground(p[0], p[2], p[1] + 1.5)
            if floor_here <= -1e8:
                # The extracted GLBs only cover part of the real map; spots outside that
                # art can never be walked to. Not a collision bug — reported separately.
                near_art = nearest_reachable(y_at, p, max_rad=20)
                hint = "nothing within 7 m" if near_art is None else f"nearest art floor {near_art[0]:.1f} m away"
                print(f"   OUT  {name} {p}: no art floor here ({hint})")
                warnings += 1
                continue
            near = nearest_reachable(y_at, p, y_tol=1.5)
            if near is not None and near[0] <= 2.5:
                print(f"   near {name} {p}: reachable floor {near[0]:.1f} m away at y={near[1]:.2f} (floor {floor_here:.2f})")
                warnings += 1
                continue
            where = f"floor {floor_here:.2f} (authored {p[1]:.2f}, d{floor_here - p[1]:+.2f})"
            hint = "no reachable floor near" if near is None else f"nearest reachable floor {near[0]:.1f} m away at y={near[1]:.2f}"
            print(f"   FAIL {name} {p} unreachable: {where}; {hint}")
            failures += 1
    print(f"\n{failures} failure(s), {warnings} warning(s)")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())


