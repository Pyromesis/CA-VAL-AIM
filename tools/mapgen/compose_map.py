#!/usr/bin/env python3
"""Compose MapPipe output into one textured GLB per level.

MapPipe (tools/mappipe) extracts every static-mesh instance of a Valorant level and
one GLB per unique mesh.  Those GLBs are already in glTF axes and metres, but they
carry no textures and are not instanced.  This script:

  * applies every instance transform in glTF space,
  * merges the geometry by material (one primitive per material),
  * resolves the sidecar ``*_MI.json`` files and PNGs written by CUE4Parse,
  * embeds the PNGs in the GLB, and
  * writes ``<Level>.glb`` files that RealMap.cs can load directly.

The output is in the same glTF world frame the game uses: RealMap rotates the
node +90 degrees around Y and adds the manifest origin, so a vertex ``(x,y,z)``
ends up at ``(z, y, -x) + origin`` in game metres. For an Unreal point (cm) that is
``(uy, uz, -ux) / 100 + origin``: a true top view with north (Unreal +X) at -Z, the
same orientation as the official minimap.

Usage:
  python compose_map.py --source <MapPipe dir> --out <maps/key>
  python compose_map.py --source <dir> --out <dir> --levels Art_A,Art_B
"""

from __future__ import annotations

import argparse
import json
import math
import os
import re
import struct
import sys
import time
from pathlib import Path

import numpy as np

# UE -> glTF axis conversion: (x, y, z) -> (x, z, y), the same swap CUE4Parse applies to
# the mesh vertices. Unreal is left-handed and glTF right-handed, so the conversion has to
# be a reflection. The old matrix (x, z, -y) was a rotation: the composed world came out
# mirrored east-west and every piece was mirrored again inside it (CUE4Parse vertices use
# the swap), which tore gaps between pieces and left buildings reversed. Checked against
# the official minimaps with the valorant-api.com calibration: see tools/mapgen/README.md.
C = np.array([[1.0, 0.0, 0.0],
              [0.0, 0.0, 1.0],
              [0.0, 1.0, 0.0]], dtype=np.float64)
CT = C.T

COMPONENT = {
    5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2),
    5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4),
}
TYPE_SIZE = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
             "MAT2": 4, "MAT3": 9, "MAT4": 16}


def log(*a):
    print(*a, flush=True)


# ------------------------------------------------------------------ GLB input

def read_glb(path: Path):
    """Return (json, bin_chunk).  Handles embedded and external buffers."""
    data = path.read_bytes()
    magic, version, length = struct.unpack_from("<III", data, 0)
    if magic != 0x46546C67:
        raise ValueError(f"not a GLB: {path}")
    off = 12
    js = None
    binchunk = b""
    while off + 8 <= min(length, len(data)):
        clen, ctype = struct.unpack_from("<II", data, off)
        off += 8
        chunk = data[off:off + clen]
        off += clen
        if ctype == 0x4E4F534A:
            js = json.loads(chunk.decode("utf-8"))
        elif ctype == 0x004E4942:
            binchunk = chunk
    if js is None:
        raise ValueError(f"GLB has no JSON chunk: {path}")
    buffers = []
    for b in js.get("buffers", []):
        uri = b.get("uri")
        if not uri:
            buffers.append(binchunk)
        elif uri.startswith("data:"):
            import base64
            buffers.append(base64.b64decode(uri.split(",", 1)[1]))
        else:
            buffers.append((path.parent / uri).read_bytes())
    return js, buffers


def accessor_array(js, buffers, idx, keep_dtype=None):
    acc = js["accessors"][idx]
    ncomp = TYPE_SIZE[acc["type"]]
    ctype = acc["componentType"]
    fmt, size = COMPONENT[ctype]
    count = acc["count"]
    if "bufferView" not in acc:
        return np.zeros((count, ncomp), dtype=np.float32)
    bv = js["bufferViews"][acc["bufferView"]]
    buf = buffers[bv.get("buffer", 0)]
    base = bv.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = bv.get("byteStride") or (size * ncomp)
    if stride == size * ncomp:
        arr = np.frombuffer(buf, dtype=np.dtype(fmt), count=count * ncomp,
                            offset=base).reshape(count, ncomp)
    else:
        arr = np.ndarray((count, ncomp), dtype=np.dtype(fmt), buffer=buf,
                         offset=base, strides=(stride, size))
    if acc.get("normalized"):
        if ctype == 5121:
            arr = arr.astype(np.float32) / 255.0
        elif ctype == 5123:
            arr = arr.astype(np.float32) / 65535.0
        elif ctype == 5120:
            arr = np.maximum(arr.astype(np.float32) / 127.0, -1.0)
        elif ctype == 5122:
            arr = np.maximum(arr.astype(np.float32) / 32767.0, -1.0)
    return np.ascontiguousarray(arr)


def node_matrices(js):
    """World matrix for every node that references a mesh."""
    nodes = js.get("nodes", [])
    out = []

    def walk(i, parent):
        n = nodes[i]
        if "matrix" in n:
            m = np.array(n["matrix"], dtype=np.float64).reshape(4, 4).T
        else:
            t = n.get("translation", [0, 0, 0])
            r = n.get("rotation", [0, 0, 0, 1])
            s = n.get("scale", [1, 1, 1])
            x, y, z, w = r
            rm = np.array([
                [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
            ], dtype=np.float64)
            m = np.eye(4)
            m[:3, :3] = rm * np.array(s, dtype=np.float64)[None, :]
            m[:3, 3] = t
        m = parent @ m
        if "mesh" in n:
            out.append((n["mesh"], m))
        for c in n.get("children", []):
            walk(c, m)

    for i in range(len(nodes)):
        # only walk roots (nodes that are nobody's child)
        if not any(i in n.get("children", []) for n in nodes):
            walk(i, np.eye(4))
    if not nodes:
        for mi in range(len(js.get("meshes", []))):
            out.append((mi, np.eye(4)))
    return out


class Prim:
    __slots__ = ("material", "pos", "nrm", "tan", "uv", "col", "idx", "mesh_dir", "slot")

    def __init__(self, material, pos, nrm, tan, uv, col, idx, mesh_dir, slot=-1):
        self.material = material
        self.pos = pos
        self.nrm = nrm
        self.tan = tan
        self.uv = uv
        self.col = col
        self.idx = idx
        self.mesh_dir = mesh_dir
        self.slot = slot


_mesh_cache = {}


def load_mesh(path: Path):
    key = str(path)
    if key in _mesh_cache:
        return _mesh_cache[key]
    js, buffers = read_glb(path)
    materials = js.get("materials", [])
    prims = []
    if "meshes" not in js:
        _mesh_cache[key] = prims
        return prims
    for mesh_idx, (node_mesh, node_m) in enumerate(node_matrices(js)):
        if node_mesh >= len(js["meshes"]):
            continue
        for p in js["meshes"][node_mesh].get("primitives", []):
            attrs = p.get("attributes", {})
            if "POSITION" not in attrs:
                continue
            pos = accessor_array(js, buffers, attrs["POSITION"]).astype(np.float32)
            if node_m is not None and not np.allclose(node_m, np.eye(4)):
                pos = (node_m[:3, :3] @ pos.T).T + node_m[:3, 3]
            nrm = None
            if "NORMAL" in attrs:
                nrm = accessor_array(js, buffers, attrs["NORMAL"]).astype(np.float32)
                if node_m is not None and not np.allclose(node_m, np.eye(4)):
                    nm = np.linalg.inv(node_m[:3, :3]).T
                    nrm = (nm @ nrm.T).T
                    ln = np.linalg.norm(nrm, axis=1, keepdims=True)
                    nrm = np.divide(nrm, np.maximum(ln, 1e-9))
            tan = None
            if "TANGENT" in attrs:
                tan = accessor_array(js, buffers, attrs["TANGENT"]).astype(np.float32)
            uv = None
            if "TEXCOORD_0" in attrs:
                uv = accessor_array(js, buffers, attrs["TEXCOORD_0"]).astype(np.float32)
            col = None
            if "COLOR_0" in attrs:
                ca = js["accessors"][attrs["COLOR_0"]]
                raw = accessor_array(js, buffers, attrs["COLOR_0"])
                if ca["componentType"] == 5121 and ca.get("normalized"):
                    col = raw.astype(np.float32)
                elif ca["componentType"] == 5126:
                    col = np.clip(raw, 0.0, 1.0).astype(np.float32)
            idx = None
            if "indices" in p:
                idx = accessor_array(js, buffers, p["indices"]).astype(np.int64).ravel()
            else:
                idx = np.arange(len(pos), dtype=np.int64)
            mat = "default"
            slot = -1
            if "material" in p and p["material"] < len(materials):
                slot = p["material"]
                mat = materials[slot].get("name") or f"material_{slot}"
            prims.append(Prim(mat, pos, nrm, tan, uv, col, idx, path.parent, slot))
    _mesh_cache[key] = prims
    return prims


# --------------------------------------------------------------- materials

_material_cache = {}
_json_index = None
_png_index = None
_png_list = None


def build_indexes(root: Path):
    global _json_index, _png_index, _png_list
    if _json_index is not None:
        return
    _json_index = {}
    _png_index = {}
    _png_list = []
    for p in root.rglob("*.json"):
        _json_index.setdefault(p.stem, p)
    for p in root.rglob("*.png"):
        _png_index.setdefault(p.stem, p)
        _png_list.append(p)


def find_texture(name: str, near: Path):
    for cand in (near / (name + ".png"),):
        if cand.is_file():
            return cand
    return _png_index.get(name)


def texture_kind(key: str, name: str):
    k = key.lower()
    n = name.lower()
    if "emissive" in k or n.endswith("_e") or n.endswith("_em"):
        return "emissive"
    if "normal" in k or n.endswith("_nm"):
        return "normal"
    if ("specular" in k or "mra" in k or n.endswith("_mra") or n.endswith("_orm")):
        return "mra"
    if "diffuse" in k or "basecolor" in k or "albedo" in k or n.endswith("_df") or n.endswith("_bc"):
        return "diffuse"
    return None


def material_info(mat_name: str, mesh_dir: Path):
    key = (mat_name, str(mesh_dir))
    if key in _material_cache:
        return _material_cache[key]
    jp = mesh_dir / (mat_name + ".json")
    if not jp.is_file():
        jp = _json_index.get(mat_name + "_MI") or _json_index.get(mat_name)
    d = {}
    if jp and jp.is_file():
        try:
            d = json.loads(jp.read_text(encoding="utf-8", errors="replace"))
        except Exception:
            d = {}
    base = near = jp.parent if jp else mesh_dir

    textures = {}
    for k, v in (d.get("Textures") or {}).items():
        if not isinstance(v, dict):
            continue
        on = v.get("ObjectName", "")
        m = re.search(r"'([^']+)'", on)
        name = m.group(1) if m else on
        kind = texture_kind(k, name)
        if kind and kind not in textures:
            p = find_texture(name, near)
            if p:
                textures[kind] = p

    colors = d.get("Colors") or {}
    dc = colors.get("DiffuseColor") or {}
    if not isinstance(dc, dict):
        dc = {}
    diffuse = textures.get("diffuse")
    factor = [1.0, 1.0, 1.0, 1.0]
    if dc:
        factor = [min(1.0, float(dc.get(c, 1.0))) for c in ("R", "G", "B", "A")]
    elif not diffuse:
        factor = [0.55, 0.55, 0.55, 1.0]

    blend = int(d.get("BlendMode", 0) or 0)
    alpha_mode = "OPAQUE"
    cutoff = None
    if blend == 1:
        alpha_mode = "MASK"
        cutoff = 0.333
    elif blend == 2 or d.get("IsTranslucent"):
        alpha_mode = "BLEND"
        factor[3] = min(factor[3], 0.75)
        if not diffuse:
            factor[3] = min(factor[3], 0.45)

    info = {
        "jp": jp, "textures": textures, "factor": factor,
        "alphaMode": alpha_mode, "cutoff": cutoff,
        "doubleSided": bool(d.get("IsTranslucent") or blend == 2),
    }
    _material_cache[key] = info
    return info


# --------------------------------------------------------------- GLB output

class Bin:
    def __init__(self):
        self.data = bytearray()

    def add(self, raw: bytes, align=4):
        while len(self.data) % align:
            self.data.append(0)
        off = len(self.data)
        self.data.extend(raw)
        return off


def write_glb(path: Path, js, bin_data: bytes):
    js = dict(js)
    while len(bin_data) % 4:
        bin_data += b"\x00"
    js["buffers"] = [{"byteLength": len(bin_data)}]
    jb = json.dumps(js, separators=(",", ":")).encode("utf-8")
    while len(jb) % 4:
        jb += b" "
    total = 12 + 8 + len(jb) + (8 + len(bin_data) if bin_data else 0)
    with path.open("wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, total))
        f.write(struct.pack("<II", len(jb), 0x4E4F534A))
        f.write(jb)
        if bin_data:
            f.write(struct.pack("<II", len(bin_data), 0x004E4942))
            f.write(bin_data)


def compose_level(level_json: Path, out_path: Path, source: Path):
    data = json.loads(level_json.read_text(encoding="utf-8", errors="replace"))
    instances = data.get("instances", [])
    groups = {}
    missing = []
    skipped = 0
    t0 = time.time()

    for inst in instances:
        mesh_key = inst.get("mesh")
        m = inst.get("m")
        if not mesh_key or not m or len(m) != 16:
            skipped += 1
            continue
        if any(s in mesh_key for s in ("_For_Volumes", "Greybox", "Reference",
                                        "Skybox", "SpawnBarrier", "KillVolumes")):
            skipped += 1
            continue
        # MapPipe mesh keys are asset paths with '/' replaced by '_'.  map.json has
        # the authoritative key -> relative GLB map.
        mesh_path = _mesh_key_to_path(mesh_key, source)
        if mesh_path is None or not mesh_path.is_file():
            missing.append(mesh_key)
            continue

        m3 = np.array(m, dtype=np.float64).reshape(4, 4)[:3, :3]
        t = np.array([m[3], m[7], m[11]], dtype=np.float64) / 100.0
        rg = C @ m3 @ CT
        tg = C @ t
        det = float(np.linalg.det(rg))
        try:
            nm = np.linalg.inv(rg).T
        except np.linalg.LinAlgError:
            nm = np.eye(3)
        flip = det < 0.0

        try:
            prims = load_mesh(mesh_path)
        except Exception as e:
            missing.append(f"{mesh_key}: {e}")
            continue
        overrides = inst.get("mats") or []
        slots = (_map_json or {}).get("slots", {}).get(mesh_key, [])
        for p in prims:
            # Per-instance override materials (MapPipe "mats", one per mesh slot) win over the mesh's own
            # material: shared floor planes and trims are dressed per placement (Bind's B site sand).
            mat_name = p.material
            slot = slots.index(p.material) if p.material in slots else p.slot
            if 0 <= slot < len(overrides) and overrides[slot]:
                mat_name = overrides[slot]
            # WorldGridMaterial is the engine default material, but Riot also uses
            # it on real shipping geometry (floors, container platforms, window
            # frames).  Keep those primitives and fall back to a neutral grey
            # material instead of deleting them.
            if "WorldGrid" in mat_name:
                mat_name = "WorldGridMaterial_Fallback"
            pos = (rg @ p.pos.T).T + tg
            nrm = None
            if p.nrm is not None:
                nrm = (nm @ p.nrm.T).T
                nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
            tan = None
            if p.tan is not None:
                tan = p.tan.copy()
                tan[:, :3] = (rg @ tan[:, :3].T).T
                tan[:, :3] /= np.maximum(np.linalg.norm(tan[:, :3], axis=1, keepdims=True), 1e-9)
                if flip:
                    tan[:, 3] *= -1.0
            idx = p.idx
            if flip:
                idx = idx.reshape(-1, 3)[:, ::-1].ravel()
            uv = p.uv if p.uv is not None else np.zeros((len(pos), 2), dtype=np.float32)
            col = p.col
            g = groups.get(mat_name)
            if g is None:
                g = groups[mat_name] = {"pos": [], "nrm": [], "tan": [], "uv": [],
                                          "col": [], "idx": [], "n": 0,
                                          "dir": p.mesh_dir}
            g["pos"].append(pos.astype(np.float32))
            g["nrm"].append(nrm.astype(np.float32) if nrm is not None else None)
            g["tan"].append(tan.astype(np.float32) if tan is not None else None)
            g["uv"].append(uv.astype(np.float32))
            g["col"].append(col)
            g["idx"].append(idx + g["n"])
            g["n"] += len(pos)

    if not groups:
        log(f"    {level_json.stem}: no geometry")
        return None

    # ---- merge groups and build glTF
    bin_ = Bin()
    js = {
        "asset": {"version": "2.0", "generator": "ValTrainer compose_map.py"},
        "scene": 0, "scenes": [{"nodes": [0]}],
        "nodes": [{"mesh": 0, "name": level_json.stem}],
        "meshes": [{"name": level_json.stem, "primitives": []}],
        "materials": [], "textures": [], "images": [], "samplers": [],
        "accessors": [], "bufferViews": [],
    }
    sampler_idx = None
    if any(material_info(m, g["dir"])["textures"] for m, g in groups.items()):
        js["samplers"].append({"magFilter": 9729, "minFilter": 9987,
                               "wrapS": 10497, "wrapT": 10497})
        sampler_idx = 0

    image_index = {}
    tex_index = {}
    material_index = {}
    minv = np.array([1e30, 1e30, 1e30])
    maxv = np.array([-1e30, -1e30, -1e30])

    def add_bv(raw, target=None):
        off = bin_.add(raw)
        bv = {"buffer": 0, "byteOffset": off, "byteLength": len(raw)}
        if target:
            bv["target"] = target
        js["bufferViews"].append(bv)
        return len(js["bufferViews"]) - 1

    def add_acc(arr, ctype, typ, target, normalized=False, minmax=False):
        raw = np.ascontiguousarray(arr).tobytes()
        bv = add_bv(raw, target)
        acc = {"bufferView": bv, "componentType": ctype,
               "count": int(arr.shape[0]), "type": typ}
        if normalized:
            acc["normalized"] = True
        if minmax:
            acc["min"] = [float(v) for v in arr.min(axis=0)]
            acc["max"] = [float(v) for v in arr.max(axis=0)]
        js["accessors"].append(acc)
        return len(js["accessors"]) - 1

    for mat, g in sorted(groups.items()):
        pos = np.concatenate(g["pos"], axis=0)
        idx = np.concatenate(g["idx"], axis=0).astype(np.uint32)
        has_n = all(x is not None for x in g["nrm"])
        has_t = all(x is not None for x in g["tan"])
        has_c = all(x is not None for x in g["col"])
        nrm = np.concatenate(g["nrm"], axis=0) if has_n else None
        tan = np.concatenate(g["tan"], axis=0) if has_t else None
        uv = np.concatenate(g["uv"], axis=0)
        col = np.concatenate(g["col"], axis=0) if has_c else None

        attrs = {"POSITION": add_acc(pos, 5126, "VEC3", 34962, minmax=True)}
        if nrm is not None:
            attrs["NORMAL"] = add_acc(nrm, 5126, "VEC3", 34962)
        if tan is not None:
            attrs["TANGENT"] = add_acc(tan, 5126, "VEC4", 34962)
        attrs["TEXCOORD_0"] = add_acc(uv, 5126, "VEC2", 34962)
        if col is not None:
            ca = np.clip(col * 255.0 + 0.5, 0, 255).astype(np.uint8)
            attrs["COLOR_0"] = add_acc(ca, 5121, "VEC4", 34962, normalized=True)
        ia = add_acc(idx, 5125, "SCALAR", 34963)

        info = material_info(mat, g["dir"])
        if mat not in material_index:
            mj = {"name": mat, "pbrMetallicRoughness": {
                "baseColorFactor": [round(float(v), 4) for v in info["factor"]],
                "metallicFactor": 1.0, "roughnessFactor": 1.0}}
            if info["alphaMode"] != "OPAQUE":
                mj["alphaMode"] = info["alphaMode"]
            if info["cutoff"] is not None:
                mj["alphaCutoff"] = info["cutoff"]
            mj["doubleSided"] = info["doubleSided"]
            oc = ((_map_json or {}).get("materialColors") or {}).get(mat)
            if oc:
                # MapPipe measured this material's colour from the game's own textures and layer tints (override
                # materials are not even exported as files); flatten_glb.py keeps it.
                mj["extras"] = {"vt_color": oc["color"], "vt_fixed": True}
            for kind, mkey in (("diffuse", "baseColorTexture"),
                               ("normal", "normalTexture"),
                               ("mra", "metallicRoughnessTexture"),
                               ("emissive", "emissiveTexture")):
                p = info["textures"].get(kind)
                if not p:
                    continue
                ip = str(p.resolve())
                if ip not in image_index:
                    img = p.read_bytes()
                    bv = add_bv(img)
                    js["images"].append({"bufferView": bv, "mimeType": "image/png"})
                    image_index[ip] = len(js["images"]) - 1
                if ip not in tex_index:
                    js["textures"].append({"sampler": sampler_idx,
                                           "source": image_index[ip]})
                    tex_index[ip] = len(js["textures"]) - 1
                ti = tex_index[ip]
                if kind == "normal":
                    mj["normalTexture"] = {"index": ti}
                elif kind == "emissive":
                    mj["emissiveTexture"] = {"index": ti}
                    mj["emissiveFactor"] = [1.0, 1.0, 1.0]
                elif kind == "mra":
                    mj["pbrMetallicRoughness"]["metallicRoughnessTexture"] = {"index": ti}
                    mj["occlusionTexture"] = {"index": ti}
                else:
                    mj["pbrMetallicRoughness"]["baseColorTexture"] = {"index": ti}
            material_index[mat] = len(js["materials"])
            js["materials"].append(mj)

        js["meshes"][0]["primitives"].append({
            "attributes": attrs, "indices": ia, "material": material_index[mat]})
        minv = np.minimum(minv, pos.min(axis=0))
        maxv = np.maximum(maxv, pos.max(axis=0))

    write_glb(out_path, js, bytes(bin_.data))
    dt = time.time() - t0
    size = out_path.stat().st_size / (1024 * 1024)
    log(f"    {level_json.stem}: {len(instances)} inst, {len(groups)} materials, "
        f"{len(js['images'])} textures, {size:.1f} MB, {dt:.1f}s, "
        f"bounds {minv.round(1)} .. {maxv.round(1)}")
    if missing:
        log(f"      missing meshes: {len(missing)} (first {missing[0]})")
    return {"level": level_json.stem, "instances": len(instances),
            "materials": len(groups), "images": len(js["images"]),
            "bytes": out_path.stat().st_size,
            "min": [float(v) for v in minv], "max": [float(v) for v in maxv]}


def _mesh_key_to_path(mesh_key: str, source: Path):
    # map.json maps CUE4Parse object keys to the exported GLB relative path.
    global _map_json
    try:
        if _map_json is None:
            _map_json = json.loads((source / "map.json").read_text(encoding="utf-8"))
        files = _map_json.get("meshFiles") or {}
        rel = files.get(mesh_key)
        if rel:
            p = source / "meshes" / rel.replace("\\", "/")
            if p.is_file():
                return p
    except Exception:
        pass
    return None


_map_json = None


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--levels", default="")
    ap.add_argument("--clean", action="store_true")
    args = ap.parse_args(argv)
    source = Path(args.source)
    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    build_indexes(source / "meshes")

    names = [n.strip() for n in args.levels.split(",") if n.strip()]
    files = sorted((source / "levels").glob("*.json"))
    if names:
        want = set(names)
        files = [f for f in files
                 if f.stem in want or any(f.stem.endswith("_" + n) for n in want)]
    report = []
    for f in files:
        if args.clean:
            pass
        log(f"[{f.stem}]")
        r = compose_level(f, out / (f.stem + ".glb"), source)
        if r:
            report.append(r)
    (out / "compose_report.json").write_text(json.dumps(report, indent=1))
    log(f"done: {len(report)} levels, "
        f"{sum(r['bytes'] for r in report) / (1024 * 1024):.1f} MB")


if __name__ == "__main__":
    main()
