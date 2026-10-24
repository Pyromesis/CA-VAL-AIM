#!/usr/bin/env python3
"""Print a map's callouts (valorant-api.com /v1/maps) in the ValTraining game frame.

    game = (uy, uz, -ux) / 100 + origin

Usage:
  python api_callouts.py <api_maps.json> <Token> [ox,oy,oz]

<api_maps.json> is a saved copy of https://valorant-api.com/v1/maps; <Token> is the map's
internal name (Ascent, Duality, Foxtrot, Triad, Port, Bonsai, Canyon, Jam, Pitt, Infinity,
Juliett, Plummet, Rook).
"""

import json
import sys


def load(api_path, token):
    for m in json.load(open(api_path, encoding="utf-8"))["data"]:
        if m.get("mapUrl", "").split("/")[-1].lower() == token.lower():
            return m
    raise SystemExit(f"no map {token}")


def to_game(loc, origin):
    return (loc["y"] / 100 + origin[0], loc.get("z", 0) / 100 + origin[1], -loc["x"] / 100 + origin[2])


def minimap_info(m, origin):
    """MapArt.Info calibration (pixel of world 0,0 and metres per pixel) for a 1024 px minimap."""
    xM, yM, xA, yA = m["xMultiplier"], m["yMultiplier"], m["xScalarToAdd"], m["yScalarToAdd"]
    scale = 1 / (102400 * xM)
    return 1024 * (xA - 100 * xM * origin[0]), 1024 * (yA + 100 * yM * origin[2]), scale


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    m = load(argv[1], argv[2])
    origin = [float(v) for v in argv[3].split(",")] if len(argv) > 3 else [0, 0, 0]
    for c in sorted(m["callouts"], key=lambda c: (c["superRegionName"], c["regionName"])):
        g = to_game(c["location"], origin)
        print(f"{c['superRegionName']:>14} | {c['regionName']:<16} ({g[0]:8.2f}, {g[1]:6.2f}, {g[2]:8.2f})")
    ox, oy, s = minimap_info(m, origin)
    print(f"minimap: origin px ({ox:.2f}, {oy:.2f}) scale {s:.6f} m/px")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
