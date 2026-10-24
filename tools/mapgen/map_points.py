#!/usr/bin/env python3
"""Collect the gameplay points of an extracted map from MapPipe's actor dumps.

``MapPipe --actors <dir>`` writes one text file per gameplay level (CalloutVolumes,
TeamSpawnPoints, Gameplay). This script reads them and prints / writes the named callout
regions, the attacker and defender spawns and the bomb sites, in Unreal centimetres and
in the game frame for a given origin:

    game = (uy, uz, -ux) / 100 + origin

Usage:
  python map_points.py <actors dir> [--origin ox,oy,oz] [--json out.json]
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

ACTOR = re.compile(r"^ACTOR\s+(\S+)\s+(\S+)\s+label='([^']*)'(?:\s+loc=\(([-\d.]+),([-\d.]+),([-\d.]+)\))?(.*)$")
REGION = re.compile(r"RegionName\(TextProperty\)=(.*?)(?:\s+\w+\((?:Object|Text|Str|Name)Property\)=|$)")
SUPER = re.compile(r"SuperRegion\w*\((?:Text|Str|Name|Enum|Byte)Property\)=(\S+)")


def to_game(p, origin):
    ux, uy, uz = p
    return [round(uy / 100 + origin[0], 3), round(uz / 100 + origin[1], 3), round(-ux / 100 + origin[2], 3)]


def parse(folder: Path):
    callouts, atk, dfn, sites = [], [], [], {}
    for f in sorted(folder.glob("*.txt")):
        for line in f.read_text(encoding="utf-8", errors="replace").splitlines():
            m = ACTOR.match(line)
            if not m or m.group(4) is None:
                continue
            cls, name, label = m.group(1), m.group(2), m.group(3)
            loc = (float(m.group(4)), float(m.group(5)), float(m.group(6)))
            rest = m.group(7)
            if name.startswith("CalloutRegion"):
                rn = REGION.search(rest)
                region = rn.group(1).strip() if rn else name.split("_", 1)[-1]
                parts = name.split("_")[1:]
                sup = parts[0] if parts and parts[0] in ("A", "B", "C") else ""
                full = region if (not sup or region.startswith(sup + " ") or region.startswith(sup)) else f"{sup} {region}"
                if region.lower() in ("site", "main", "lobby", "link", "long", "short", "window", "exit", "tower",
                                      "elbow", "hall", "rafters", "heaven", "garage", "lane", "door", "pit",
                                      "teleporter", "back", "top", "bottom", "rope", "drop", "tunnel", "stairs"):
                    full = f"{sup} {region}".strip() if sup else region
                callouts.append({"id": name, "name": full, "region": region, "super": sup, "ue": loc})
            elif cls == "APlayerStart" or "TeamStart" in name or "Spawn" in name and cls == "APlayerStart":
                (atk if "Attack" in name else dfn if "Defend" in name else []).append(loc)
            elif name.startswith("MinimapSite") or label.startswith("MinimapSite"):
                key = (label or name).replace("MinimapSite", "")[:1]
                if key in "ABC":
                    sites.setdefault(key, loc)
    def avg(pts):
        return [sum(p[i] for p in pts) / len(pts) for i in range(3)] if pts else None
    return {"callouts": callouts, "attacker": avg(atk), "defender": avg(dfn), "sites": sites,
            "n_attacker": len(atk), "n_defender": len(dfn)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("--origin", default="0,0,0")
    ap.add_argument("--json")
    a = ap.parse_args()
    origin = [float(v) for v in a.origin.split(",")]
    d = parse(Path(a.folder))
    for c in d["callouts"]:
        c["game"] = to_game(c["ue"], origin)
    for k in ("attacker", "defender"):
        if d[k]:
            d[k + "_game"] = to_game(d[k], origin)
    d["sites_game"] = {k: to_game(v, origin) for k, v in d["sites"].items()}
    if a.json:
        Path(a.json).write_text(json.dumps(d, indent=1))
    print(f"attacker {d.get('attacker_game')} ({d['n_attacker']})  defender {d.get('defender_game')} ({d['n_defender']})")
    for k, v in sorted(d["sites_game"].items()):
        print(f"site {k}: {v}")
    for c in sorted(d["callouts"], key=lambda c: c["name"]):
        print(f"  {c['name']:<24} {c['game']}")


if __name__ == "__main__":
    main()
