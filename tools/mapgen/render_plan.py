#!/usr/bin/env python3
"""Top view of a map's collision.json, drawn in the official minimap's pixel space.

Floors are shaded by height, walls are dark; optional points (drill spots) are marked.
With --minimap the plan is blended over the official minimap, which is the quickest way to
check that a map's frame, mirroring and scale are right.

Usage:
  python render_plan.py <maps/key> <api_maps.json> <out.png> [--minimap file.png] [--points spots.json]
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("folder")
    ap.add_argument("api")
    ap.add_argument("out")
    ap.add_argument("--minimap")
    ap.add_argument("--points")
    ap.add_argument("--size", type=int, default=1024)
    a = ap.parse_args()
    folder = Path(a.folder)
    man = json.loads((folder / "manifest.json").read_text())
    token = man["map"]
    api = next(m for m in json.load(open(a.api, encoding="utf-8"))["data"]
               if m.get("mapUrl", "").split("/")[-1].lower() == token.lower())
    xM, yM, xA, yA = api["xMultiplier"], api["yMultiplier"], api["xScalarToAdd"], api["yScalarToAdd"]
    ox, oz = man["origin_x"], man["origin_z"]
    S = a.size

    def px(x, z):
        # game -> Unreal: uy = (x - ox) * 100, ux = (oz - z) * 100
        return S * ((x - ox) * 100 * xM + xA), S * ((oz - z) * 100 * yM + yA)

    boxes = np.asarray(json.loads((folder / "collision.json").read_text())["boxes"], dtype=np.float64)
    floors = boxes[(boxes[:, 4] - boxes[:, 1]) < 0.6]
    walls = boxes[(boxes[:, 4] - boxes[:, 1]) >= 0.6]
    img = Image.new("RGBA", (S, S), (10, 13, 18, 255))
    d = ImageDraw.Draw(img)
    lo, hi = np.percentile(floors[:, 4], [3, 97]) if len(floors) else (0, 1)
    order = np.argsort(floors[:, 4])
    for b in floors[order]:
        t = float(np.clip((b[4] - lo) / max(1e-3, hi - lo), 0, 1))
        col = (int(70 + 120 * t), int(90 + 90 * t), int(120 - 30 * t), 255)
        x0, y0 = px(b[0], b[2]); x1, y1 = px(b[3], b[5])
        d.rectangle([min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)], fill=col)
    for b in walls:
        x0, y0 = px(b[0], b[2]); x1, y1 = px(b[3], b[5])
        d.rectangle([min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)], fill=(225, 225, 230, 255))
    if a.minimap:
        mm = Image.open(a.minimap).convert("RGBA").resize((S, S))
        img = Image.blend(img, mm, 0.45)
        d = ImageDraw.Draw(img)
    if a.points:
        pts = json.loads(Path(a.points).read_text())
        for name, p, col in pts:
            x, y = px(p[0], p[2])
            d.ellipse([x - 5, y - 5, x + 5, y + 5], outline=tuple(col), width=2)
            d.text((x + 7, y - 6), name, fill=tuple(col))
    img.convert("RGB").save(a.out)
    print(a.out)


if __name__ == "__main__":
    main()
