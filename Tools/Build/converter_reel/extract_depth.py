#!/usr/bin/env python3
"""
Extracts what the reel lab's depth-prism port (src/depthPrisms.js, src/depthPrismsShader.js) reads from the game:

    data/depth.json
      profile        CrystalDepthPrismProfile.asset (guid a92a2f63...), the ratchet's numbers
      materials      MassDepthPrisms.mat (the converter's massMaterial) and OmniDepthPrisms.mat (omniTrianglesMaterial):
                     every OmniDepthPrismShader property (material value, else the shader's default); colours as stored
                     (sRGB) AND linearised as Unity uploads them in this Linear-colour-space project
      combine        OmniCombineData.json's depth block, omniTriangleRotation, the Mass crystal's meshScale, and the omni
                     body plates the converter cuts out (plateOwner == Mass: the triangle holes the prisms fill)
      triangles      OmniCrystalTriangles.asset (guid 148e2440...): positions, normals, TEXCOORD2 (stored as a plate index
                     per vertex + one centre per plate, which is exactly what the mesh carries), indices. The shader reads
                     POSITION, NORMAL and TEXCOORD2 only.

The mesh is read with author_omni_combine_asset.read_mesh (imported, not copied; it needs numpy). Only NORMAL is read
here, because read_mesh does not return it.

    python3 Tools/Build/converter_reel/extract_depth.py          # write data/depth.json
    python3 Tools/Build/converter_reel/extract_depth.py --check  # exit 1 if data/depth.json is stale
"""

import argparse
import json
import os
import re
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import author_omni_combine_asset as combine  # noqa: E402  (read_mesh, the paths)

ROOT = combine.ROOT
OUT = os.path.join(HERE, "data", "depth.json")
PROFILE = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "CrystalDepthPrismProfile.asset")
SHADER = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "Shaders", "OmniDepthPrismShader.shader")
MASS_MAT = os.path.join(combine.MATERIALS, "MassDepthPrisms.mat")
OMNI_MAT = os.path.join(combine.MATERIALS, "OmniDepthPrisms.mat")
COMBINE = combine.OUT
TRI_MESH = combine.TRI_MESH
GUIDS = {PROFILE: "a92a2f63c4862a0cb00ecb413644b936", MASS_MAT: "18f7a3895e246fc4b4c7765da0d4ddbf",
         OMNI_MAT: "99b4e2819e8a90b9b0127553d67518b9", TRI_MESH: "148e244052c4a0499c66e90863d9d5ee"}
POS_DP, NRM_DP = 6, 5


def rel(p):
    return os.path.relpath(p, ROOT)


def check_guids():
    for path, g in GUIDS.items():
        meta = open(path + ".meta", encoding="utf-8").read()
        if f"guid: {g}" not in meta:
            raise SystemExit(f"{rel(path)}: expected guid {g}")


def shader_properties():
    """name -> ('float'|'color'|'vector', default) from the shader's Properties block."""
    text = open(SHADER, encoding="utf-8").read()
    block = text[text.index("Properties"):text.index("SubShader")]
    props = {}
    for m in re.finditer(r"(?:\[[^\]]*\]\s*)*(_\w+)\s*\(\"[^\"]*\",\s*(\w+)(?:\([^)]*\))?\)\s*=\s*([^\n]+)", block):
        name, kind, default = m.group(1), m.group(2), m.group(3).strip()
        if kind in ("Color", "Vector"):
            props[name] = ("color" if kind == "Color" else "vector", [float(x) for x in default.strip("()").split(",")])
        else:
            props[name] = ("float", float(default))
    return props


def srgb_to_linear(c):
    # Mathf.GammaToLinearSpace (the exact sRGB curve); Unity applies it to every Color property in Linear space.
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def material(path, props):
    text = open(path, encoding="utf-8").read()
    saved = text[text.index("m_SavedProperties"):]
    out = {"name": os.path.splitext(os.path.basename(path))[0]}
    for name, (kind, default) in props.items():
        if kind == "float":
            m = re.search(rf"- {name}: (-?[0-9.eE+-]+)", saved)
            out[name] = float(m.group(1)) if m else default
        else:
            m = re.search(rf"- {name}: {{r: (-?[0-9.eE+-]+), g: (-?[0-9.eE+-]+), b: (-?[0-9.eE+-]+), a: (-?[0-9.eE+-]+)}}", saved)
            v = [float(x) for x in m.groups()] if m else default
            out[name] = v
            if kind == "color":
                out[name + "Linear"] = [round(srgb_to_linear(x), 7) for x in v[:3]] + [v[3]]
    return out


def profile():
    text = open(PROFILE, encoding="utf-8").read()
    keys = ["openDepth", "sealDepth", "profilePower", "reseatDepth", "reseatFadeSeconds", "sealAtHorizon",
            "horizonMargin", "riseWhileHidden", "riseRate", "occluderRadius", "orientationJitter"]
    out = {}
    for k in keys:
        v = float(re.search(rf"^  {k}: (-?[0-9.eE+-]+)$", text, re.M).group(1))
        out[k] = bool(v) if k in ("sealAtHorizon", "riseWhileHidden") else v
    return out


def read_normals(path, count):
    """NORMAL: floats 3..5 of the 64-byte prism vertex (read_mesh checked the layout)."""
    text = open(path, encoding="utf-8").read()
    raw = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]+)", text).group(1))
    stride = len(raw) // count
    return [struct.unpack_from("<3f", raw, i * stride + 12) for i in range(count)]


def triangles():
    pos, uv2, idx = combine.read_mesh(TRI_MESH)
    n = len(pos)
    nrm = read_normals(TRI_MESH, n)
    plate = [int(round(w)) for w in uv2[:, 3]]
    count = max(plate) + 1
    centres = [None] * count
    for i in range(n):
        c = [float(x) for x in uv2[i, :3]]
        if centres[plate[i]] is None:
            centres[plate[i]] = c
        elif max(abs(a - b) for a, b in zip(c, centres[plate[i]])) > 0:
            raise SystemExit(f"{rel(TRI_MESH)}: plate {plate[i]} carries more than one centre in TEXCOORD2")
    if any(c is None for c in centres):
        raise SystemExit(f"{rel(TRI_MESH)}: a plate index has no vertex")
    return {
        "source": rel(TRI_MESH),
        "vertexCount": n,
        "positions": [round(float(x), POS_DP) for x in pos.ravel()],
        "normals": [round(float(x), NRM_DP) for v in nrm for x in v],
        "plate": plate,
        "plateCentres": [round(x, POS_DP) for c in centres for x in c],
        "indices": [int(i) for i in idx],
    }


def combine_block():
    d = json.load(open(COMBINE, encoding="utf-8"))
    mass_index = d["order"].index("Mass")
    mass = next(c for c in d["crystals"] if c["element"] == "Mass")
    holes = [q for q, o in enumerate(d["omni"]["plateOwner"]) if o == mass_index]
    return {
        "source": rel(COMBINE),
        "depth": d["depth"],
        "omniTriangleRotation": d["omniTriangleRotation"],
        "massMeshScale": mass["meshScale"],
        "massIndex": mass_index,
        "omniHolePlates": holes,
    }


def build():
    check_guids()
    props = shader_properties()
    out = {
        "generator": "Tools/Build/converter_reel/extract_depth.py",
        "profile": profile(),
        "materials": {"mass": material(MASS_MAT, props), "omni": material(OMNI_MAT, props)},
        "combine": combine_block(),
        "triangles": triangles(),
    }
    if len(out["combine"]["omniHolePlates"]) != len(out["triangles"]["plateCentres"]) // 3:
        raise SystemExit("the omni's triangle holes and OmniCrystalTriangles' prisms disagree in number")
    return json.dumps(out, separators=(",", ":")) + "\n"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    text = build()
    if a.check:
        cur = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else ""
        if cur != text:
            print(f"STALE: {os.path.relpath(OUT, ROOT)} - re-run Tools/Build/converter_reel/extract_depth.py")
            return 1
        print(f"OK: {os.path.relpath(OUT, ROOT)} is current")
        return 0
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    open(OUT, "w", encoding="utf-8").write(text)
    print(f"wrote {os.path.relpath(OUT, ROOT)} ({len(text)} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
