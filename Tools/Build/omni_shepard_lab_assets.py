#!/usr/bin/env python3
"""
Bakes the omni crystal's SHIPPED Shepard-triangle numbers and meshes into the Omni Shepard Lab
(Docs/Studios/OmniShepardLab.html), between the `// <assets>` and `// </assets>` markers.

The lab is a READER of these assets, never their authority (/labmaker §2.1). Everything the page
calls "shipped" comes from here:

  Crystal.prefab                      which OmniShepardTriangles layers the crystal carries, in order,
                                      and the root scale (10)
  OmniShepardTriangles {0,1,2}.mat    _Start / _Stop / _Period / _Opacity / _ScaleDistance, colours
  OmniShepardTrianglesInactive*.mat   the inactive (blue) palette
  OmniShepardTrianglesRim.mat         the static outer shell
  OmniCrystalBody{,Inactive}.mat      the body's colours
  OmniCrystalTriangles.prefab         the shells' local scale (0.892...)
  OmniCrystalTriangles.asset          the 20-plate triangle mesh (positions, normals, indices)
  OmniCrystalExport1_8-21-25.fbx      the body mesh and its authored normals (UnitScaleFactor 100 + useFileScale -> scale 1;
                                      its frame matches the triangle mesh: at _Stop 0.5 every plate
                                      lands within 0.01 local units of the body face behind it)

Usage:
  python3 Tools/Build/omni_shepard_lab_assets.py           # rewrite the block in the lab
  python3 Tools/Build/omni_shepard_lab_assets.py --check   # exit 1 if the lab's block is stale
  python3 Tools/Build/omni_shepard_lab_assets.py --self-test
"""

import json
import math
import os
import re
import struct
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Build"))
import fbx_binary  # noqa: E402

LAB = os.path.join(ROOT, "Docs", "Studios", "OmniShepardLab.html")
MATS = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "CrystalMaterials")
CRYSTAL_PREFAB = os.path.join(ROOT, "Assets", "_Prefabs", "Environment", "Crystal.prefab")
TRI_PREFAB = os.path.join(ROOT, "Assets", "_Prefabs", "Environment", "OmniCrystalTriangles.prefab")
TRI_MESH = os.path.join(ROOT, "Assets", "_Models", "OmniCrystalTriangles.asset")
BODY_FBX = os.path.join(ROOT, "Assets", "_Models", "OmniCrystalExport1_8-21-25.fbx")
SHEPARD_SHADER_GUID = "b55e562cd5045859607550ce7ba8267f"   # OmniShepardFresnelShader.shader
BEGIN, END = "// <assets>", "// </assets>"


def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()


def guid_of(asset_path):
    m = re.search(r"^guid: ([0-9a-f]{32})", read(asset_path + ".meta"), re.M)
    if not m:
        raise SystemExit(f"no guid in {asset_path}.meta")
    return m.group(1)


def mat_props(path):
    t = read(path)
    floats = {k: float(v) for k, v in re.findall(r"^\s+- (_\w+): (-?[0-9.eE+-]+)\s*$", t, re.M)}
    cols = {}
    for k, r, g, b, a in re.findall(r"^\s+- (_\w+): \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}", t, re.M):
        cols[k] = [round(float(x), 6) for x in (r, g, b, a)]
    shader = re.search(r"m_Shader: \{fileID: \d+, guid: ([0-9a-f]+)", t).group(1)
    return floats, cols, shader


def shepard_layer(path):
    f, c, shader = mat_props(path)
    if shader != SHEPARD_SHADER_GUID:
        raise SystemExit(f"{os.path.basename(path)} is not on OmniShepardFresnelShader - the lab models only that shader")
    return {
        "start": f["_Start"], "stop": f["_Stop"], "period": f["_Period"], "opacity": f.get("_Opacity", 1.0),
        "scaleDistance": f.get("_ScaleDistance", 1.0), "rimPower": f.get("_RimPower", 1.0),
        "faceForward": f.get("_FaceForward", 0.0), "bright": c["_BrightColor"], "dark": c["_DarkColor"],
    }


def crystal_layers():
    """The crystalModels entries of Crystal.prefab whose default material is a Shepard material, in order."""
    t = read(CRYSTAL_PREFAB)
    by_guid = {}
    for name in os.listdir(MATS):
        if name.endswith(".mat"):
            by_guid[guid_of(os.path.join(MATS, name))] = os.path.join(MATS, name)
    moving, rims = [], []
    for g, ig in re.findall(r"defaultMaterial: \{fileID: 2100000, guid: ([0-9a-f]+), type: 2\}\s*\n"
                            r"\s+explodingMaterial: [^\n]*\n\s+inactiveMaterial: \{fileID: 2100000, guid: ([0-9a-f]+)", t):
        path = by_guid.get(g)
        if not path:
            continue
        _, _, shader = mat_props(path)
        if shader != SHEPARD_SHADER_GUID:
            continue
        layer = shepard_layer(path)
        layer["material"] = os.path.basename(path)[:-4]
        inactive = shepard_layer(by_guid[ig])
        layer["inactiveBright"], layer["inactiveDark"] = inactive["bright"], inactive["dark"]
        (moving if layer["scaleDistance"] > 0.5 else rims).append(layer)
    root_scale = float(re.search(r"m_LocalScale: \{x: ([0-9.]+)", t).group(1))
    return moving, rims, root_scale


def tri_mesh():
    t = read(TRI_MESH)
    ib = bytes.fromhex(re.search(r"m_IndexBuffer: ([0-9a-f]+)", t).group(1))
    vd = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]+)", t).group(1))
    n = int(re.search(r"m_VertexCount: (\d+)", t).group(1))
    stride = len(vd) // n
    idx = list(struct.unpack("<%dH" % (len(ib) // 2), ib))
    pos, nrm = [], []
    for i in range(n):
        pos.append(struct.unpack_from("<3f", vd, i * stride))
        nrm.append(struct.unpack_from("<3f", vd, i * stride + 12))
    # Plates: connected components over shared vertex POSITIONS (the mesh is split per face).
    key = lambda p: tuple(round(c, 4) for c in p)
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    first = {}
    for i, p in enumerate(pos):
        k = key(p)
        if k in first:
            parent[find(i)] = find(first[k])
        else:
            first[k] = i
    for a in range(0, len(idx), 3):
        parent[find(idx[a + 1])] = find(idx[a])
        parent[find(idx[a + 2])] = find(idx[a])
    plates = {}
    for a in range(0, len(idx), 3):
        plates.setdefault(find(idx[a]), []).extend(idx[a:a + 3])
    plate_list = sorted(plates.values(), key=lambda tri: min(tri))
    return pos, nrm, plate_list


def body_mesh():
    nodes = fbx_binary.read(BODY_FBX)
    nodes = nodes[0] if isinstance(nodes, tuple) else nodes

    def walk(ns):
        for x in ns:
            yield x
            yield from walk(x.children)
    geos = [x for x in walk(nodes) if x.name == "Geometry"]
    if len(geos) != 1:
        raise SystemExit(f"expected one Geometry in the body FBX, found {len(geos)}")
    g = geos[0]
    V = g.first("Vertices").props[0][1]
    I = g.first("PolygonVertexIndex").props[0][1]
    # The AUTHORED normals, which is what Unity imports (normalImportMode 0). The plates are hollow
    # bevelled shells: their inner faces point at the centre and their walls point sideways, so a
    # normal re-derived from winding or "flipped outward" draws them wrong.
    ln = g.first("LayerElementNormal")
    if ln.first("MappingInformationType").props[0][1] != b"ByPolygonVertex":
        raise SystemExit("body FBX normals are not ByPolygonVertex; the baker reads only that mapping")
    N = ln.first("Normals").props[0][1]
    ref = ln.first("ReferenceInformationType").props[0][1]
    NI = ln.first("NormalsIndex").props[0][1] if ref == b"IndexToDirect" else list(range(len(I)))
    pts = [tuple(V[i:i + 3]) for i in range(0, len(V), 3)]
    tris, nrm, cur = [], [], []
    for j, x in enumerate(I):
        cur.append((~x if x < 0 else x, NI[j]))
        if x < 0:
            for k in range(1, len(cur) - 1):
                for v, n in (cur[0], cur[k], cur[k + 1]):
                    tris.append(v)
                    nrm += N[3 * n:3 * n + 3]
            cur = []
    return pts, tris, nrm


def build():
    moving, rims, root_scale = crystal_layers()
    if not moving:
        raise SystemExit("Crystal.prefab carries no moving Shepard layer")
    tri_scale = float(re.search(r"m_LocalScale: \{x: ([0-9.]+)", read(TRI_PREFAB)).group(1))
    _, body_c, _ = mat_props(os.path.join(MATS, "OmniCrystalBody.mat"))
    _, body_ic, _ = mat_props(os.path.join(MATS, "OmniCrystalBodyInactive.mat"))
    pos, nrm, plates = tri_mesh()
    bpos, btris, bnrm = body_mesh()
    r6 = lambda v: round(v, 6)
    r5 = lambda v: round(v, 5)
    return {
        "source": "Tools/Build/omni_shepard_lab_assets.py",
        "rootScale": root_scale,
        "triScale": r6(tri_scale),
        "layers": [{k: (r6(v) if isinstance(v, float) else v) for k, v in L.items()} for L in moving],
        "rims": [{k: (r6(v) if isinstance(v, float) else v) for k, v in L.items()} for L in rims],
        "body": {"bright": body_c["_BrightColor"], "dark": body_c["_DarkColor"],
                 "inactiveBright": body_ic["_BrightColor"], "inactiveDark": body_ic["_DarkColor"]},
        "triMesh": {
            "p": [r5(c) for p in pos for c in p],
            "n": [round(c, 4) for q in nrm for c in q],
            "plates": plates,
        },
        "bodyMesh": {"p": [r5(c) for p in bpos for c in p], "i": btris, "n": [round(c, 4) for c in bnrm]},
    }


def block(data):
    return BEGIN + "\n  const ASSETS = " + json.dumps(data, separators=(",", ":")) + ";\n  " + END


def splice(html, data):
    # A marker counts only when it is alone on its line: the page's own header comment NAMES the
    # markers, and matching that prose once spliced the whole document head away.
    a = list(re.finditer(r"^[ \t]*" + re.escape(BEGIN) + r"[ \t]*$", html, re.M))
    b = list(re.finditer(r"^[ \t]*" + re.escape(END) + r"[ \t]*$", html, re.M))
    if len(a) != 1 or len(b) != 1 or a[0].start() > b[0].start():
        raise SystemExit(f"{LAB}: expected exactly one {BEGIN} line before one {END} line")
    start = a[0].start() + len(a[0].group(0)) - len(a[0].group(0).lstrip())
    return html[:start] + block(data) + html[b[0].end():]


def summary(d):
    L = d["layers"]
    return (f"{len(L)} moving layers " + ", ".join(f"{x['material']} {x['start']}->{x['stop']} / {x['period']}s" for x in L)
            + f"; {len(d['rims'])} rim; triScale {d['triScale']}; root {d['rootScale']}; "
            f"{len(d['triMesh']['plates'])} plates; body {len(d['bodyMesh']['i']) // 3} tris")


def self_test():
    d = build()
    ok = True

    def check(name, cond):
        nonlocal ok
        print(("PASS  " if cond else "FAIL  ") + name)
        ok = ok and cond
    check("three moving layers read from Crystal.prefab", len(d["layers"]) == 3)
    check("bands are contiguous (layer i _Stop == layer i+1 _Start)",
          all(abs(d["layers"][i]["stop"] - d["layers"][i + 1]["start"]) < 1e-5 for i in range(len(d["layers"]) - 1)))
    check("20 plates of 8 triangles", len(d["triMesh"]["plates"]) == 20 and all(len(p) == 24 for p in d["triMesh"]["plates"]))
    # Negative control: a stale block must be reported by --check's comparison.
    html = "names `" + BEGIN + "` in prose\n  " + BEGIN + "\n  stale\n  " + END + "\ntail"
    out = splice(html, d)
    check("a stale block differs from a fresh splice", out != html)
    # Planted defect (it happened): a marker named in prose must not be taken for the real one.
    check("a marker mentioned in prose is left alone", out.startswith("names `" + BEGIN + "` in prose\n") and out.endswith("\ntail"))
    # Planted defect: a non-Shepard material must be refused.
    try:
        mat = os.path.join(MATS, "OmniCrystalBody.mat")
        shepard_layer(mat)
        check("a non-Shepard material is refused", False)
    except SystemExit:
        check("a non-Shepard material is refused", True)
    return ok


def main():
    if "--self-test" in sys.argv:
        sys.exit(0 if self_test() else 1)
    d = build()
    html = read(LAB)
    out = splice(html, d)
    if "--check" in sys.argv:
        if out != html:
            print(f"STALE: {os.path.relpath(LAB, ROOT)} does not carry the shipped numbers. {summary(d)}")
            sys.exit(1)
        print(f"OK: lab matches the assets. {summary(d)}")
        return
    if out != html:
        with open(LAB, "w", encoding="utf-8") as f:
            f.write(out)
        print(f"wrote {os.path.relpath(LAB, ROOT)}: {summary(d)}")
    else:
        print(f"no change. {summary(d)}")


if __name__ == "__main__":
    main()
