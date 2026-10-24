#!/usr/bin/env python3
"""Author a map's site-attack drill (MapSpots.<Map>.cs) from its real collision and callouts.

Inputs: a built map folder (manifest.json + collision.json from build_maps.py) and the
official callouts (valorant-api.com /v1/maps). The drill is laid out the way a player walks it:

1. A walk grid is flooded from the attacker spawn with the runtime's movement rules
   (radius 0.34, height 1.8, 0.7 m steps, drops allowed, no jumps up). Columns keep every floor they
   can stand on, so Heaven and Hell, or a bridge and the lane under it, are separate.
2. The shortest walk from the spawn to the plant point gives the route. The choke is where
   it enters the site (16 m from the plant point), the start is 24 m of walking before it.
3. Every defender-side callout within 42 m of the site becomes one defender. Its spot is the
   standable cell within 7 m of the callout that sees the choke (or the site) from behind the
   most cover; spots stay 3.5 m apart.
4. Plants are the plant point plus the two best-covered cells on site; retake entries come
   from the defender spawn's walk to the site; smokes cover the widest-spread defenders;
   flashes pop over the choke, into the site, around both sides and deep.

The floor at the plant point is moved to y = 0 (manifest origin and collision shifted).

Usage:
  python author_spots.py <maps/key> <api_maps.json> <out MapSpots.X.cs> [--json points.json]
"""

from __future__ import annotations

import argparse
import collections
import heapq
import json
import math
from pathlib import Path

import numpy as np

# RISE is a walking step, not a jump: the extracted art has no invisible blocking volumes, so allowing crouch-jumps
# lets routes climb crates onto rooftops that the real game fences off.
R, H, STEP, RISE, DROP = 0.34, 1.8, 0.7, 0.75, 5.0
CELL = 0.5
EYE = 1.55

DISPLAY = {"Ascent": "Ascent", "Duality": "Bind", "Foxtrot": "Breeze", "Triad": "Haven", "Port": "Icebox",
           "Bonsai": "Split", "Canyon": "Fracture", "Jam": "Lotus", "Pitt": "Pearl", "Infinity": "Abyss",
           "Juliett": "Sunset", "Plummet": "Summit", "Rook": "Corrode"}


class World:
    def __init__(self, boxes):
        self.b = np.asarray(boxes, dtype=np.float64)
        self.bucket = collections.defaultdict(list)
        for i, bx in enumerate(self.b):
            for gx in range(int(math.floor((bx[0] - 1) / 2)), int(math.floor((bx[3] + 1) / 2)) + 1):
                for gz in range(int(math.floor((bx[2] - 1) / 2)), int(math.floor((bx[5] + 1) / 2)) + 1):
                    self.bucket[(gx, gz)].append(i)
        self.bucket = {k: np.array(v) for k, v in self.bucket.items()}

    def near(self, x, z):
        idx = self.bucket.get((int(math.floor(x / 2)), int(math.floor(z / 2))))
        return self.b[idx] if idx is not None else self.b[:0]

    def ground(self, x, z, feet_y):
        nb = self.near(x, z)
        if len(nb) == 0:
            return None
        r = R - 0.05
        sel = (nb[:, 4] <= feet_y + STEP) & (x + r >= nb[:, 0]) & (x - r <= nb[:, 3]) & (z + r >= nb[:, 2]) & (z - r <= nb[:, 5])
        if not sel.any():
            return None
        return float(nb[sel, 4].max())

    def blocked(self, x, z, y):
        nb = self.near(x, z)
        if len(nb) == 0:
            return False
        sel = (nb[:, 4] > y + STEP) & (nb[:, 1] < y + H) & (x > nb[:, 0] - R) & (x < nb[:, 3] + R) & (z > nb[:, 2] - R) & (z < nb[:, 5] + R)
        return bool(sel.any())

    def los(self, a, b):
        a = np.asarray(a, float); b = np.asarray(b, float)
        d = b - a
        L = float(np.linalg.norm(d))
        if L < 1e-4:
            return True
        lo = np.minimum(a, b) - 0.05; hi = np.maximum(a, b) + 0.05
        bx = self.b
        sel = (bx[:, 0] <= hi[0]) & (bx[:, 3] >= lo[0]) & (bx[:, 1] <= hi[1]) & (bx[:, 4] >= lo[1]) & (bx[:, 2] <= hi[2]) & (bx[:, 5] >= lo[2])
        c = bx[sel]
        if len(c) == 0:
            return True
        with np.errstate(divide="ignore", invalid="ignore"):
            inv = 1.0 / np.where(np.abs(d) < 1e-9, 1e-9, d)
            t1 = (c[:, 0:3] - a) * inv
            t2 = (c[:, 3:6] - a) * inv
        tmin = np.minimum(t1, t2).max(axis=1)
        tmax = np.maximum(t1, t2).min(axis=1)
        hit = (tmax >= np.maximum(tmin, 0)) & (tmin < 1.0 - 0.02 / L) & (tmax > 0.02 / L)
        return not bool(hit.any())

    def cover(self, x, y, z):
        """How enclosed a spot is: solid columns 0.4–1.4 m away at chest height, by direction."""
        score = 0
        low = False
        for ang in range(0, 360, 45):
            dx, dz = math.cos(math.radians(ang)), math.sin(math.radians(ang))
            for dist in (0.6, 1.0, 1.4):
                px, pz = x + dx * dist, z + dz * dist
                nb = self.near(px, pz)
                if len(nb) == 0:
                    continue
                s = (px >= nb[:, 0]) & (px <= nb[:, 3]) & (pz >= nb[:, 2]) & (pz <= nb[:, 5]) & (nb[:, 1] < y + 1.3) & (nb[:, 4] > y + 0.5)
                if s.any():
                    score += 1
                    if (nb[s, 4] < y + 1.45).all():
                        low = True
                    break
        return score, low


def flood(world, start, max_nodes=900000, band=None):
    """Walkable nodes keyed (ix, iz, iy) with the walking distance from start (Dijkstra on 8-neighbours).

    `band(x, z, y)` returns a cost factor: steps onto floors far above or below the nearest official callout (rooftops,
    awnings and ledges the real game fences off with invisible walls) cost several times more, so routes stay on the
    streets players actually use while still able to cross such a floor when there is no other way."""
    sx, sz = round(start[0] / CELL), round(start[2] / CELL)
    g0 = world.ground(sx * CELL, sz * CELL, start[1] + 0.5)
    if g0 is None:
        raise SystemExit(f"no floor at start {start}")
    k0 = (sx, sz, round(g0 * 4))
    y_of = {k0: g0}
    dist = {k0: 0.0}
    prev = {k0: None}
    pq = [(0.0, k0)]
    nbrs = [(1, 0, 1.0), (-1, 0, 1.0), (0, 1, 1.0), (0, -1, 1.0), (1, 1, 1.414), (1, -1, 1.414), (-1, 1, 1.414), (-1, -1, 1.414)]
    while pq:
        d, k = heapq.heappop(pq)
        if d > dist.get(k, 1e18):
            continue
        cx, cz, _ = k
        y = y_of[k]
        for dx, dz, w in nbrs:
            nx, nz = cx + dx, cz + dz
            wx, wz = nx * CELL, nz * CELL
            g = world.ground(wx, wz, y + 0.45)
            if g is None or g < y - DROP or g > y + RISE:
                continue
            if world.blocked(wx, wz, g):
                continue
            if dx and dz and (world.blocked(cx * CELL + dx * CELL, cz * CELL, y) or world.blocked(cx * CELL, cz * CELL + dz * CELL, y)):
                continue
            nk = (nx, nz, round(g * 4))
            nd = d + (w * CELL + max(0.0, g - y) * 0.6) * (band(wx, wz, g) if band else 1.0)
            if nd < dist.get(nk, 1e18):
                dist[nk] = nd
                y_of[nk] = g
                prev[nk] = k
                heapq.heappush(pq, (nd, nk))
        if len(dist) > max_nodes:
            break
    return dist, y_of, prev


def pos(k, y_of):
    return np.array([k[0] * CELL, y_of[k], k[1] * CELL])


def nearest_node(nodes, y_of, p, max_dy=3.0):
    best, bd = None, 1e18
    for k in nodes:
        q = pos(k, y_of)
        dy = abs(q[1] - p[1])
        if dy > max_dy:
            continue
        dd = (q[0] - p[0]) ** 2 + (q[2] - p[2]) ** 2 + dy * dy * 0.5
        if dd < bd:
            bd, best = dd, k
    return best


def path_to(prev, k):
    out = []
    while k is not None:
        out.append(k)
        k = prev[k]
    return out[::-1]


def yaw_of(d):
    return math.degrees(math.atan2(d[0], -d[2]))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("api")
    ap.add_argument("out")
    ap.add_argument("--json")
    ap.add_argument("--actors", help="MapPipe --actors dump of the map (TeamSpawnPoints): exact spawn floors")
    a = ap.parse_args()
    folder = Path(a.folder)
    man = json.loads((folder / "manifest.json").read_text())
    key, token, letter = man["key"], man["map"], man.get("site_letter", "A")
    api = next(m for m in json.load(open(a.api, encoding="utf-8"))["data"]
               if m.get("mapUrl", "").split("/")[-1].lower() == token.lower())
    origin = [man["origin_x"], man["origin_y"], man["origin_z"]]
    coll_path = folder / "collision.json"
    boxes = json.loads(coll_path.read_text())["boxes"]
    world = World(boxes)

    def game(loc):
        return np.array([loc["y"] / 100 + origin[0], loc.get("z", 0) / 100 + origin[1], -loc["x"] / 100 + origin[2]])

    callouts = [(c["superRegionName"], c["regionName"], game(c["location"])) for c in api["callouts"]]
    atk = next(p for s, r, p in callouts if s == "Attacker Side" and r == "Spawn")
    dfn = next(p for s, r, p in callouts if s == "Defender Side" and r == "Spawn")
    plant = np.array(man["site"], float)

    # Attacker spawn: the floor under the callout.
    def stand(p):
        for dy in (1.0, 2.0, 3.5, 0.0, -1.0):
            g = world.ground(p[0], p[2], p[1] + dy)
            if g is not None and not world.blocked(p[0], p[2], g):
                return np.array([p[0], g, p[2]])
        # search around
        for r in (1, 2, 3, 4, 6):
            for ang in range(0, 360, 30):
                x, z = p[0] + r * math.cos(math.radians(ang)), p[2] + r * math.sin(math.radians(ang))
                g = world.ground(x, z, p[1] + 2.0)
                if g is not None and not world.blocked(x, z, g):
                    return np.array([x, g, z])
        return None

    # The spawn callouts are region centres (often at roof height, and Summit's is outside the map); the real player
    # starts stand on the floor. Starts named Attacker/Defender are used as such; unnamed ones form two clusters and
    # each side takes the cluster nearer its callout.
    starts = []
    if a.actors:
        import re as _re
        rx = _re.compile(r"APlayerStart\s+\S+\s+label='([^']*)'\s+loc=\(([-\d.]+),([-\d.]+),([-\d.]+)\)")
        for f in Path(a.actors).glob("*.txt"):
            for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
                mm = rx.search(line)
                if mm:
                    p = game({"x": float(mm.group(2)), "y": float(mm.group(3)), "z": float(mm.group(4))}) - [0, 0.9, 0]
                    starts.append((mm.group(1).lower(), p))
    side_starts = {"atk": [p for n, p in starts if "attack" in n], "def": [p for n, p in starts if "defend" in n]}
    if starts and (not side_starts["atk"] or not side_starts["def"]):
        pts = [p for _, p in starts]
        c1 = pts[0]
        c2 = max(pts, key=lambda p: np.linalg.norm((p - c1)[[0, 2]]))
        for _ in range(10):
            g1 = [p for p in pts if np.linalg.norm((p - c1)[[0, 2]]) <= np.linalg.norm((p - c2)[[0, 2]])]
            g2 = [p for p in pts if np.linalg.norm((p - c1)[[0, 2]]) > np.linalg.norm((p - c2)[[0, 2]])]
            c1, c2 = np.mean(g1, axis=0), np.mean(g2, axis=0)
        d = lambda c, q: np.linalg.norm((c - q)[[0, 2]])
        if d(c1, atk) + d(c2, dfn) <= d(c2, atk) + d(c1, dfn):
            side_starts = {"atk": g1, "def": g2}
        else:
            side_starts = {"atk": g2, "def": g1}

    def spawn_at(callout, side):
        near = side_starts.get(side) or []
        if near:
            c = np.mean(near, axis=0)
            for best in sorted(near, key=lambda p: np.hypot(p[0] - c[0], p[2] - c[2])):
                g = world.ground(best[0], best[2], best[1] + 0.4)
                if g is not None and not world.blocked(best[0], best[2], g):
                    return np.array([best[0], g, best[2]])
        return stand(callout)

    a0 = spawn_at(atk, "atk")
    if a0 is None:
        raise SystemExit(f"[{key}] no standable attacker spawn near {np.round(atk, 1).tolist()}")
    cpts = np.array([p for s_, r_, p in callouts if not (s_ == "Attacker Side" and r_ == "Spawn")])
    # A callout's height is its volume's, a fixed offset above the floor that differs per map: measure it as the median
    # of (lowest walkable floor under each callout − callout height) over a first, unbiased walk.
    dist0, y0_of, prev0 = flood(world, a0)
    cols = collections.defaultdict(list)
    for k in dist0:
        cols[(k[0], k[1])].append(y0_of[k])
    offs = []
    for c in cpts:
        ys = [y for dx in range(-3, 4) for dz in range(-3, 4)
              for y in cols.get((round(c[0] / CELL) + dx, round(c[2] / CELL) + dz), ())]
        if ys:
            offs.append(min(ys) - c[1])
    floor_off = float(np.median(offs)) if offs else 0.0
    print(f"    callout floor offset {floor_off:+.2f} m ({len(offs)} callouts)")
    cfloor = cpts[:, 1] + floor_off

    def band(x, z, y):
        i = int(np.argmin((cpts[:, 0] - x) ** 2 + (cpts[:, 2] - z) ** 2))
        return 8.0 if (y > cfloor[i] + 2.2 or y < cfloor[i] - 3.0) else 1.0

    # The plain shortest walk is right on most maps; only when much of it runs outside the floor band (Pearl: out of the
    # raised spawn across the rooftops) is the band-weighted walk used instead.
    site0 = nearest_node(dist0, y0_of, plant + np.array([0, 0.5, 0]))
    route0 = path_to(prev0, site0) if site0 is not None else []
    off_band = sum(band(k[0] * CELL, k[1] * CELL, y0_of[k]) > 1.0 for k in route0) / max(1, len(route0))
    if off_band <= 0.4:
        dist, y_of, prev = dist0, y0_of, prev0
        print(f"    plain route ({off_band * 100:.0f}% off the floor band)")
    else:
        dist, y_of, prev = flood(world, a0, band=band)
        print(f"    band-weighted route (the plain one runs {off_band * 100:.0f}% off the floor band)")
    print(f"[{key}] walk grid: {len(dist)} nodes from attacker spawn {np.round(a0, 2).tolist()}")
    site_k = nearest_node(dist, y_of, plant + np.array([0, 0.5, 0]))
    site_p = pos(site_k, y_of)
    if np.hypot(site_p[0] - plant[0], site_p[2] - plant[2]) > 6:
        print(f"    WARNING: plant point not reached (nearest walkable {np.round(site_p, 2).tolist()})")

    # Re-level: plant floor at y = 0.
    shift = float(site_p[1])
    if abs(shift) > 0.02:
        for bx in boxes:
            bx[1] -= shift
            bx[4] -= shift
        coll_path.write_text(json.dumps({"boxes": boxes}, separators=(",", ":")))
        man["origin_y"] = round(origin[1] - shift, 3)
        origin[1] -= shift
        world = World(boxes)
        y_of = {k: v - shift for k, v in y_of.items()}
        a0 = a0 - [0, shift, 0]
        callouts = [(s, r, p - [0, shift, 0]) for s, r, p in callouts]
        dfn = dfn - [0, shift, 0]
        site_p = pos(site_k, y_of)
        print(f"    re-levelled by {-shift:+.2f} m: origin_y {man['origin_y']}")
    plant = np.array([plant[0], site_p[1], plant[2]])

    route = path_to(prev, site_k)
    rp = [pos(k, y_of) for k in route]
    cum = [0.0]
    for i in range(1, len(rp)):
        cum.append(cum[-1] + float(np.linalg.norm((rp[i] - rp[i - 1])[[0, 2]])))
    # Choke: first route point within 16 m of the plant point (horizontally).
    ci = next((i for i, p in enumerate(rp) if np.hypot(p[0] - plant[0], p[2] - plant[2]) < 16.0), len(rp) - 1)
    choke = rp[ci]
    si = max(0, next((i for i in range(ci, -1, -1) if cum[ci] - cum[i] >= 24.0), 0))
    start = rp[si]
    ahead = rp[min(len(rp) - 1, si + 16)] - start
    start_yaw = yaw_of(ahead)
    choke_d = dist[route[ci]]
    print(f"    route {cum[-1]:.0f} m; start {np.round(start, 2).tolist()} yaw {start_yaw:.0f}; choke {np.round(choke, 2).tolist()}")

    def node_cells_near(p, r, dy=2.5):
        out = []
        for k, d in dist.items():
            q = pos(k, y_of)
            if abs(q[0] - p[0]) > r or abs(q[2] - p[2]) > r:
                continue
            if abs(q[1] - p[1]) > dy:
                continue
            out.append(k)
        return out

    # Defender spawn walk (retakes).
    d0 = spawn_at(dfn, "def")
    if d0 is None:
        dk = nearest_node(dist, y_of, dfn, max_dy=10.0)
        d0 = pos(dk, y_of)
    ddist, dy_of, dprev = flood(world, d0, band=band)
    dsite = nearest_node(ddist, dy_of, plant + np.array([0, 0.5, 0]))
    retakes = []
    if dsite is not None:
        dpath = [pos(k, dy_of) for k in path_to(dprev, dsite)]
        for p in reversed(dpath):
            if np.hypot(p[0] - plant[0], p[2] - plant[2]) >= 14.0:
                retakes.append(p)
                break

    choke_eye = choke + [0, 1.6, 0]
    plant_eye = plant + [0, 1.2, 0]
    site_eyes = [plant_eye + o for o in (np.zeros(3), [4.0, 0, 0], [-4.0, 0, 0], [0, 0, 4.0], [0, 0, -4.0])]

    # Defenders: callouts on the defender side of the choke, near the site.
    names_seen = set()
    enemies = []
    cands = []
    for sup, reg, p in callouts:
        if reg == "Spawn" or sup in ("Attacker Side", "Defender Side"):
            continue
        hd = np.hypot(p[0] - plant[0], p[2] - plant[2])
        if hd > 42:
            continue
        cands.append((hd, sup, reg, p))
    cands.sort(key=lambda c: c[0])
    for hd, sup, reg, p in cands:
        k = nearest_node(dist, y_of, p + [0, 0.3, 0], max_dy=4.0)
        if k is None:
            continue
        # attacker side of the choke? (reached on the way in, or the route itself runs through it)
        if dist[k] < choke_d - 4.0:
            continue
        if any(np.hypot(p[0] - q[0], p[2] - q[2]) < 7.0 for q in rp[:ci]):
            continue
        best, bs = None, -1e9
        cells = node_cells_near(p, 7.0, dy=3.5)
        if len(cells) > 400:
            cells = cells[:: len(cells) // 400 + 1]
        for c in cells:
            q = pos(c, y_of)
            if any(np.linalg.norm((q - e[1])[[0, 2]]) < 3.5 and abs(q[1] - e[1][1]) < 2.0 for e in enemies):
                continue
            if np.linalg.norm((q - choke)[[0, 2]]) < 6.0:
                continue
            if q[1] > p[1] + floor_off + 2.2 or q[1] < p[1] + floor_off - 3.0:
                continue  # a rooftop or a lower level, not the place the callout names
            eye = q + [0, EYE, 0]
            sees_choke = world.los(eye, choke_eye)
            sees_site = any(world.los(eye, t) for t in site_eyes)
            cov, low = world.cover(q[0], q[1], q[2])
            s = (3.0 if sees_choke else 0) + (1.5 if sees_site else 0) + 0.6 * min(cov, 4) \
                - 0.18 * np.linalg.norm((q - p)[[0, 2]])
            if not sees_choke and not sees_site:
                continue  # a defender who can see neither the entrance nor the site holds nothing
            if s > bs:
                bs, best = s, (q, low)
        if best is None:
            continue
        nm = reg if sup in (letter, "") or reg in ("Site",) else f"{sup} {reg}"
        if sup == letter and reg in ("Link", "Lobby", "Main", "Long", "Short", "Exit", "Window", "Hall", "Tower", "Back", "Door", "Rafters", "Garage", "Stairs"):
            nm = f"{sup} {reg}"
        if nm in names_seen:
            continue
        names_seen.add(nm)
        enemies.append((nm, best[0], "Crouch" if best[1] else "Stand"))
        if len(enemies) >= 11:
            break

    # Fill a sparse site with covered cells on the site itself.
    site_cells = node_cells_near(plant, 9.0, dy=1.5)
    scored = []
    for c in site_cells[:: max(1, len(site_cells) // 300)]:
        q = pos(c, y_of)
        cov, low = world.cover(q[0], q[1], q[2])
        if cov >= 2:
            scored.append((cov, q, low))
    scored.sort(key=lambda s: -s[0])
    extra_names = ["Default", "Back Site", "Site Corner", "Site Box"]
    for cov, q, low in scored:
        if len(enemies) >= 8 or not extra_names:
            break
        if any(np.linalg.norm((q - e[1])[[0, 2]]) < 4.0 for e in enemies) or np.linalg.norm((q - choke)[[0, 2]]) < 7:
            continue
        nm = extra_names.pop(0)
        if nm in names_seen:
            continue
        names_seen.add(nm)
        enemies.append((nm, q, "Crouch" if low else "Stand"))

    plants = [plant]
    for cov, q, low in scored:
        if len(plants) >= 3:
            break
        if all(np.linalg.norm((q - p)[[0, 2]]) > 2.5 for p in plants) and np.linalg.norm((q - plant)[[0, 2]]) < 6:
            plants.append(q)

    # Retake entries: the defender walk plus defender-side callouts 12–28 m out.
    for sup, reg, p in callouts:
        if len(retakes) >= 3:
            break
        hd = np.hypot(p[0] - plant[0], p[2] - plant[2])
        if not 12 <= hd <= 28 or reg == "Spawn":
            continue
        k = nearest_node(ddist, dy_of, p, max_dy=3.0) if dsite is not None else None
        if k is None:
            continue
        q = pos(k, dy_of)
        if dist.get(nearest_node(dist, y_of, q) or k, 0) < choke_d:
            continue
        if world.los(q + [0, EYE, 0], plant_eye):
            continue
        if all(np.linalg.norm((q - r)[[0, 2]]) > 6 for r in retakes):
            retakes.append(q)

    # Smokes: the three most spread defenders as seen from the choke.
    far = [e for e in enemies if np.linalg.norm((e[1] - choke)[[0, 2]]) > 8]
    smokes = []
    for e in sorted(far, key=lambda e: -np.linalg.norm((e[1] - choke)[[0, 2]])):
        ang = math.atan2(e[1][0] - choke[0], -(e[1][2] - choke[2]))
        if all(abs((ang - s[2] + math.pi) % (2 * math.pi) - math.pi) > math.radians(25) for s in smokes):
            smokes.append((e[0], e[1], ang))
        if len(smokes) == 3:
            break

    # Flashes from just behind the choke.
    back = rp[max(0, ci - 6)]
    fwd = plant - choke
    fwd[1] = 0
    fwd /= max(1e-6, np.linalg.norm(fwd))
    side = np.array([-fwd[2], 0, fwd[0]])
    thr = back + [0, 1.6, 0]
    flashes = [
        ("Pop over the entrance", thr, choke + fwd * 3 + [0, 2.2, 0]),
        ("High pop into the site", thr, plant + [0, 3.2, 0]),
        ("Curve left around the entrance", thr, choke + fwd * 5 - side * 5 + [0, 2.4, 0]),
        ("Curve right around the entrance", thr, choke + fwd * 5 + side * 5 + [0, 2.4, 0]),
        ("Deep pop to the back of the site", thr, plant + fwd * 6 + [0, 3.0, 0]),
    ]

    start_name = min(((np.linalg.norm((p - start)[[0, 2]]), f"{s} {r}" if s in "ABC" else r)
                      for s, r, p in callouts if r != "Spawn"), key=lambda t: t[0])[1]
    display = DISPLAY.get(token, token)
    site_name = f"{letter} Site"
    route_name = f"{start_name} → {site_name}"
    drill = f"Push {start_name} onto {site_name}: clear " + ", ".join(e[0] for e in enemies)

    def V(p):
        return f"V({p[0]:.2f}f, {p[1]:.2f}f, {p[2]:.2f}f)"

    lines = []
    lines.append("using Godot;\n\nnamespace ValTrainer.Maps;\n\npublic static partial class MapSpots\n{")
    lines.append("    /// <summary>")
    lines.append(f"    /// {display}, {route_name}, on the full extracted map (every sublevel, see <c>maps/{key}/</c>).")
    lines.append("    /// Generated by tools/mapgen/author_spots.py from the real collision and the official callouts:")
    lines.append("    /// the route is the shortest walk from attacker spawn to the plant point, defenders hold the")
    lines.append("    /// covered cell of each defender-side callout that sees the entrance. Frame: plant point at the")
    lines.append(f"    /// origin (floor y = 0), -Z = north, +X = east, the same orientation as the official minimap.")
    lines.append("    /// </summary>")
    lines.append(f"    public static readonly MapSpot {display} = new MapSpot")
    lines.append("    {")
    lines.append(f'        Key = "{key}", Map = "{display}", Name = "{route_name}",')
    lines.append(f'        Drill = "{drill}",')
    lines.append(f"        StartFeet = {V(start)}, StartYaw = {start_yaw:.1f}f, Choke = {V(choke)},")
    lines.append("        Palette = PALETTE,")
    lines.append(f'        RealMapFolder = "maps/{key}",')
    lines.append("        LOOK")
    lines.append("        // Only used when the external real-map files are absent.")
    lines.append("        Boxes = new[] { B(-80f, -0.1f, -80f, 80f, 0f, 80f, Surf.Floor) },")
    lines.append("        Enemies = new[]\n        {")
    for nm, q, st in enemies:
        lines.append(f'            E("{nm}", {q[0]:.2f}f, {q[1]:.2f}f, {q[2]:.2f}f{", Stance.Crouch" if st == "Crouch" else ""}),')
    lines.append("        },")
    lines.append("        Flashes = new[]\n        {")
    for nm, f, t in flashes:
        lines.append(f'            F("{nm}", {V(f)}, {V(t)}),')
    lines.append("        },")
    lines.append("        Smokes = new[]\n        {")
    for nm, q, _ in smokes:
        lines.append(f'            new SmokeSpot("{nm}", {V(q)}),')
    lines.append("        },")
    lines.append("        PlantSpots = new[] { " + ", ".join(V(p) for p in plants) + " },")
    lines.append("        RetakeEntries = new[] { " + ", ".join(V(p) for p in retakes[:3]) + " },")
    lines.append("    };\n}")
    Path(a.out).write_text("\n".join(lines) + "\n", encoding="utf-8")

    man.update({"start": [round(float(v), 3) for v in start], "choke": [round(float(v), 3) for v in choke],
                "site": [round(float(v), 3) for v in plant], "start_name": start_name, "site_name": site_name})
    (folder / "manifest.json").write_text(json.dumps(man, indent=2))
    pts = [["start", start.tolist(), [80, 220, 255]], ["choke", choke.tolist(), [255, 230, 80]],
           ["plant", plant.tolist(), [80, 255, 120]]]
    pts += [[nm, q.tolist(), [255, 80, 80]] for nm, q, _ in enemies]
    pts += [["retake", q.tolist(), [200, 120, 255]] for q in retakes[:3]]
    if a.json:
        Path(a.json).write_text(json.dumps(pts))
    print(f"    {len(enemies)} defenders: {', '.join(e[0] for e in enemies)}")
    print(f"    plants {len(plants)}, retakes {len(retakes[:3])}, smokes {len(smokes)} -> {a.out}")


if __name__ == "__main__":
    main()
