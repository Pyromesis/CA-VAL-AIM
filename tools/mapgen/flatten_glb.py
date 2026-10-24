#!/usr/bin/env python3
"""Bake each material's real colour into a composed level GLB and drop its textures.

The runtime (``src/World/GltfStrip.cs``) never uploads map textures: it renders every
surface with a flat material. Without help it can only guess that colour from the Unreal
material name, so Ascent, Icebox and Bind all end up in the same dozen greys and browns.

This pass reads the albedo PNG of every material, takes its mean colour (in linear light,
weighted by alpha), multiplies it by the material's base colour factor and stores the
result in the material's ``extras.vt_color`` as an sRGB triple. Then it removes the
images, textures and samplers and compacts the binary chunk, so a 50 MB level becomes a
few MB. GltfStrip turns ``vt_color`` into the surface colour; files without it keep the
name-based palette.

Usage:
  python flatten_glb.py <maps/key or a .glb> [...]      # rewrites in place
"""

from __future__ import annotations

import io
import json
import struct
import sys
from pathlib import Path

import numpy as np
from PIL import Image

GLB_MAGIC = 0x46546C67
JSON_CHUNK = 0x4E4F534A
BIN_CHUNK = 0x004E4942


def srgb_to_linear(c):
    c = np.asarray(c, dtype=np.float64)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(np.asarray(c, dtype=np.float64), 0.0, 1.0)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def read_glb(path: Path):
    data = path.read_bytes()
    magic, _, length = struct.unpack_from("<III", data, 0)
    if magic != GLB_MAGIC:
        raise ValueError("not a GLB")
    off, js, binchunk = 12, None, b""
    while off + 8 <= min(length, len(data)):
        clen, ctype = struct.unpack_from("<II", data, off)
        off += 8
        chunk = data[off:off + clen]
        off += clen
        if ctype == JSON_CHUNK:
            js = json.loads(chunk.decode("utf-8"))
        elif ctype == BIN_CHUNK:
            binchunk = chunk
    if js is None:
        raise ValueError("GLB has no JSON chunk")
    return js, binchunk


def write_glb(path: Path, js, binchunk: bytes):
    jb = json.dumps(js, separators=(",", ":")).encode("utf-8")
    jb += b" " * ((4 - len(jb) % 4) % 4)
    bb = binchunk + b"\0" * ((4 - len(binchunk) % 4) % 4)
    total = 12 + 8 + len(jb) + 8 + len(bb)
    with path.open("wb") as f:
        f.write(struct.pack("<III", GLB_MAGIC, 2, total))
        f.write(struct.pack("<II", len(jb), JSON_CHUNK))
        f.write(jb)
        f.write(struct.pack("<II", len(bb), BIN_CHUNK))
        f.write(bb)


def image_mean(js, binchunk, tex_index, cache):
    """Alpha-weighted mean colour of a texture in linear light, or None."""
    if tex_index in cache:
        return cache[tex_index]
    result = None
    try:
        src = js["textures"][tex_index]["source"]
        img = js["images"][src]
        bv = js["bufferViews"][img["bufferView"]]
        raw = binchunk[bv.get("byteOffset", 0):bv.get("byteOffset", 0) + bv["byteLength"]]
        im = Image.open(io.BytesIO(raw)).convert("RGBA")
        im.thumbnail((64, 64))
        a = np.asarray(im, dtype=np.float64) / 255.0
        w = a[..., 3]
        if w.sum() < 1e-6:
            w = np.ones_like(w)
        lin = srgb_to_linear(a[..., :3])
        result = (lin * w[..., None]).sum(axis=(0, 1)) / w.sum()
    except Exception:
        result = None
    cache[tex_index] = result
    return result


def compact(js, binchunk: bytes) -> bytes:
    """Keep only the buffer views the accessors read (drops every embedded PNG)."""
    views = js.get("bufferViews", [])
    used = sorted({a["bufferView"] for a in js.get("accessors", []) if "bufferView" in a})
    remap, out, at = {}, bytearray(), 0
    for i in used:
        v = views[i]
        off, ln = v.get("byteOffset", 0), v["byteLength"]
        out += binchunk[off:off + ln]
        pad = (4 - ln % 4) % 4
        out += b"\0" * pad
        nv = dict(v)
        nv["byteOffset"] = at
        nv["buffer"] = 0
        remap[i] = len(remap)
        at += ln + pad
        views[i] = nv
    js["bufferViews"] = [views[i] for i in used]
    for a in js.get("accessors", []):
        if "bufferView" in a:
            a["bufferView"] = remap[a["bufferView"]]
    js["buffers"] = [{"byteLength": len(out)}]
    return bytes(out)


def flatten(path: Path) -> str:
    js, binchunk = read_glb(path)
    if not js.get("images") and all("vt_color" in (m.get("extras") or {}) for m in js.get("materials", [])):
        return f"{path.name}: already flat"
    cache = {}
    baked = 0
    for m in js.get("materials", []):
        if (m.get("extras") or {}).get("vt_fixed"):
            continue  # an override material whose colour MapPipe measured from the game's own texture
        pbr = m.get("pbrMetallicRoughness", {})
        factor = np.array(pbr.get("baseColorFactor", [1, 1, 1, 1])[:3], dtype=np.float64)
        mean = None
        tex = pbr.get("baseColorTexture")
        if tex is not None:
            mean = image_mean(js, binchunk, tex["index"], cache)
        lin = factor * (mean if mean is not None else np.array([0.5, 0.5, 0.5]))
        srgb = linear_to_srgb(lin)
        extras = m.setdefault("extras", {})
        extras["vt_color"] = [round(float(v), 4) for v in srgb]
        if mean is not None:
            baked += 1
        for key in ("baseColorTexture", "metallicRoughnessTexture"):
            pbr.pop(key, None)
        for key in ("normalTexture", "occlusionTexture", "emissiveTexture"):
            m.pop(key, None)
    for key in ("images", "textures", "samplers"):
        js.pop(key, None)
    before = path.stat().st_size
    binchunk = compact(js, binchunk)
    write_glb(path, js, binchunk)
    after = path.stat().st_size
    return (f"{path.name}: {len(js.get('materials', []))} materials, {baked} colours from textures, "
            f"{before / 2**20:.1f} -> {after / 2**20:.1f} MB")


def main(argv):
    if not argv:
        print(__doc__)
        return 2
    for a in argv:
        p = Path(a)
        files = sorted(p.glob("*.glb")) if p.is_dir() else [p]
        for f in files:
            try:
                print(flatten(f), flush=True)
            except Exception as e:
                print(f"{f.name}: FAILED {e}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
