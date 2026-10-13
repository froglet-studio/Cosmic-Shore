#!/usr/bin/env python3
"""
Bakes the omni crystal's SHIPPED Shepard-triangle numbers and meshes into the Omni Shepard Lab
(Docs/Studios/OmniShepardLab.html), between the `// <assets>` and `// </assets>` markers.

The lab is a READER of these assets, never their authority (/labmaker §2.1). Everything the page
calls "shipped" comes from here:

  Crystal.prefab                      the root scale (10); the depth-prism slot (since round 8), or any
                                      OmniShepardTriangles tone layer it carries (none since round 8)
  OmniDepthPrisms.mat, MassDepthPrisms.mat   the SHIPPED look since round 8: the prisms' shape dials
  CrystalDepthPrismProfile.asset      its depths, ratchet, and (round 10) the per-crystal pose amount
  (the retired Breathe layers and Classic rim are NOT read: their materials were retired 2026-10-13 and the
   page pins their values in its RETIRED constant)
  OmniCrystalBody{,Inactive}.mat      the body's colours
  OmniCrystalTriangles.prefab         the shells' local scale (0.892...)
  OmniCrystalTriangles.asset          the 20-plate triangle mesh (normals, indices, plate centres) and, since
                                      2026-10-13, the thickness BAKED into it (the materials carry _Thickness 1);
                                      the lab's positions are the raw triangles it was baked from
                                      (MassCrystalExport3ExpandedTri FBX via author_omni_crystal_triangles.py),
                                      and the asset must be exactly that raw mesh stretched by the measured thickness
  MassCrystalPrisms.asset             the Mass prisms, read only for the thickness baked into them
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
MASS_FBX = os.path.join(ROOT, "Assets", "_Models", "MassCrystalExport1_8-21-25.fbx")
# The Space element's squares on the omni, baked from the artist's CrystalsRaw_V2.blend by
# Tools/Build/bake_omni_space_squares.py (the .blend is kept by the art team, not in this repo).
SPACE_SQUARES = os.path.join(ROOT, "Docs", "Studios", "data", "omni_space_squares.json")
MASS_PREFAB = os.path.join(ROOT, "Assets", "_Prefabs", "Environment", "CrystalMass.prefab")
# The Mass crystal's four shells, in crystalModels order (Tools/Build/author_mass_crystal_look.py SHELLS).
MASS_SHELLS = [("innerShell", "ActiveMassCrystalMaterial", "BlueMassCrystalMaterial"),
               ("secondShell", "ActiveMassCrystalMaterial 1", "BlueMassCrystalMaterial 1"),
               ("easedShell", "ActiveMassCrystalMaterial 2", "BlueMassCrystalMaterial 2"),
               ("outerShell", "ActiveMassCrystalMaterial 3", "BlueMassCrystalMaterial 3")]
SHEPARD_SHADER_GUID = "b55e562cd5045859607550ce7ba8267f"   # OmniShepardFresnelShader.shader
DEPTH_SHADER = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "Shaders", "OmniDepthPrismShader.shader")
DEPTH_PROFILE = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "CrystalDepthPrismProfile.asset")
# The squares as the game plays them (round 9): the lab's default speed is the asset's.
SQUARES_ASSET = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "OmniSpaceSquares.asset")
MASS_DEPTH_MAT = "MassDepthPrisms"
MASS_PRISMS = os.path.join(ROOT, "Assets", "_Models", "MassCrystalPrisms.asset")
# The Breathe tone layers (shipped rounds 4-7) and the static rim (Classic) are retired WITH their eight materials
# (2026-10-13, refactor row F2): the page pins their last authored values in its RETIRED constant, outside the
# generated block, and this script no longer reads them.
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
        # OmniShepardFresnelShader's opt-in dials (round 4); absent = the shader default = inert
        "breathe": f.get("_Breathe", 0.0), "phaseOffset": f.get("_PhaseOffset", 0.0),
        "plateScaleStart": f.get("_PlateScaleStart", 1.0), "thickness": f.get("_Thickness", 1.0),
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


def depth_material(name):
    """A depth-prism material's shape dials (OmniDepthPrismShader), or None when it is not on that shader."""
    path = os.path.join(MATS, name + ".mat")
    if not os.path.exists(path):
        return None
    f, c, shader = mat_props(path)
    if shader != guid_of(DEPTH_SHADER):
        return None
    return {"material": name, "reach": f["_Reach"], "plateScaleStart": f["_PlateScaleStart"],
            "thickness": f["_Thickness"], "thicknessInward": f["_ThicknessInward"], "topHeight": f["_TopHeight"],
            "faceSplit": f["_FaceSplit"], "splitUnit": f["_SplitUnit"], "bright": c["_BrightColor"], "dark": c["_DarkColor"]}


def asset_prisms(path):
    """(position, TEXCOORD2 xyzw) per vertex of a native prism mesh asset, through its own channel table."""
    t = read(path)
    vd = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]+)", t).group(1))
    n = int(re.search(r"m_VertexCount: (\d+)", t).group(1))
    chans = [tuple(int(x) for x in c) for c in re.findall(
        r"- stream: (\d+)\n\s+offset: (\d+)\n\s+format: (\d+)\n\s+dimension: (\d+)",
        t.split("m_Channels:")[1].split("m_DataSize")[0])]
    stride = len(vd) // n
    if chans[6][3] != 4:
        raise SystemExit(f"{os.path.basename(path)}: TEXCOORD2 is not (plate centre xyz, plate index)")
    return [(struct.unpack_from("<3f", vd, i * stride + chans[0][1]), struct.unpack_from("<4f", vd, i * stride + chans[6][1]))
            for i in range(n)]


def baked_thickness(path, raw, shipped=None):
    """The thickness a generator BAKED into a shipped prism mesh (since 2026-10-13; the materials carry _Thickness 1,
    so hull fusion can move the vertices): every prism's depth along its column in the asset over its depth in `raw`,
    the generator's unbaked vertices in the same order. Fails unless the ratio is one number for every prism AND the
    asset is exactly `raw` stretched by it, inward from the top face - the shader's own _Thickness term."""
    shipped = shipped if shipped is not None else asset_prisms(path)
    if len(shipped) != len(raw):
        raise SystemExit(f"{os.path.basename(path)}: {len(shipped)} vertices, the raw prisms have {len(raw)}")
    spans = {}
    for (q, c), (p, _n, _uv, rc) in zip(shipped, raw):
        if max(abs(c[k] - rc[k]) for k in range(4)) > 1e-5:
            raise SystemExit(f"{os.path.basename(path)}: plate data differs from the raw prisms'")
        r = math.sqrt(sum(x * x for x in c[:3]))
        d = [x / r for x in c[:3]]
        a = sum((p[k] - c[k]) * d[k] for k in range(3))
        b = sum((q[k] - c[k]) * d[k] for k in range(3))
        lo = spans.setdefault(int(round(c[3])), [9e9, -9e9, 9e9, -9e9])
        lo[0], lo[1], lo[2], lo[3] = min(lo[0], a), max(lo[1], a), min(lo[2], b), max(lo[3], b)
    ratios = [(s[3] - s[2]) / (s[1] - s[0]) for s in spans.values()]
    if max(ratios) - min(ratios) > 1e-4 * max(ratios):
        raise SystemExit(f"{os.path.basename(path)}: prisms are stretched by different amounts ({min(ratios)}..{max(ratios)})")
    thickness = round(sum(ratios) / len(ratios), 4)
    import author_omni_crystal_triangles as gen
    worst = max(max(abs(q[k] - p[k]) for k in range(3))
                for (q, _c), (p, *_rest) in zip(shipped, gen.thicken(raw, thickness)))
    if worst > 1e-5:
        raise SystemExit(f"{os.path.basename(path)}: not the raw prisms stretched by {thickness} (off by {worst:.2e})")
    return thickness


def raw_prisms(crystal):
    """The unbaked prisms a generator stretches into a shipped mesh: (position, normal, uv, plate) per vertex."""
    if crystal == "omni":
        import author_omni_crystal_triangles as gen
        return gen.bake_tri_mesh()[0]
    import author_mass_crystal_look as gen
    return gen.mass_prism_mesh()[0]


def depth_look():
    """The shipped Depth ratchet (round 8): Crystal.prefab's prism slot material, the Mass crystal's, and the
    profile they share. None while the crystal still ships tone layers."""
    t = read(CRYSTAL_PREFAB)
    names = {guid_of(os.path.join(MATS, n)): n[:-4] for n in os.listdir(MATS) if n.endswith(".mat")}
    omni = None
    for g in re.findall(r"defaultMaterial: \{fileID: 2100000, guid: ([0-9a-f]+), type: 2\}", t):
        omni = omni or (depth_material(names[g]) if g in names else None)
    if omni is None:
        return None
    mass = depth_material(MASS_DEPTH_MAT)
    # The lab's thickness is the one the crystal draws at: the bake times anything left on the material (both
    # stretch inward from the same top face, so they multiply).
    omni["thickness"] = round(omni["thickness"] * baked_thickness(TRI_MESH, raw_prisms("omni")), 6)
    if mass:
        mass["thickness"] = round(mass["thickness"] * baked_thickness(MASS_PRISMS, raw_prisms("Mass")), 6)
    prof = {k: float(v) for k, v in re.findall(r"^  (\w+): (-?[0-9.eE+-]+)$", read(DEPTH_PROFILE), re.M)}
    keys = ["openDepth", "sealDepth", "profilePower", "reseatDepth", "reseatFadeSeconds"]
    missing = [k for k in keys if k not in prof]
    if missing:
        raise SystemExit(f"CrystalDepthPrismProfile.asset lacks {missing}")
    out = {k: prof[k] for k in keys}
    out["orientationJitter"] = prof.get("orientationJitter", 0.0)   # round 10; absent = every crystal in its authored pose
    return {"omni": omni, "mass": mass, "profile": out}


def tri_mesh():
    t = read(TRI_MESH)
    ib = bytes.fromhex(re.search(r"m_IndexBuffer: ([0-9a-f]+)", t).group(1))
    vd = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]+)", t).group(1))
    n = int(re.search(r"m_VertexCount: (\d+)", t).group(1))
    stride = len(vd) // n
    # The channel table, by index (0 position, 1 normal, 6 uv2 = TEXCOORD2 = the plate centre).
    chans = re.findall(r"- stream: (\d+)\n\s+offset: (\d+)\n\s+format: (\d+)\n\s+dimension: (\d+)",
                       t.split("m_Channels:")[1].split("m_DataSize")[0])
    chans = [tuple(int(x) for x in c) for c in chans]
    for ch in (0, 1):
        if chans[ch][0] != 0 or chans[ch][2] != 0 or chans[ch][3] != 3:
            raise SystemExit(f"OmniCrystalTriangles.asset channel {ch} is not float3 on stream 0")
    has_centre = len(chans) > 6 and chans[6][3] in (3, 4) and chans[6][2] == 0   # (centre xyz[, plate index])
    idx = list(struct.unpack("<%dH" % (len(ib) // 2), ib))
    pos, nrm, cen = [], [], []
    # Positions are the RAW triangles (float32, exactly as the generator's bake writes them): the shipped asset
    # carries the depth prisms' thickness baked in, which the lab applies itself (depth_look checks the bake).
    raw = raw_prisms("omni")
    if len(raw) != n:
        raise SystemExit(f"OmniCrystalTriangles.asset has {n} vertices, the raw triangles {len(raw)}")
    for i in range(n):
        pos.append(struct.unpack("<3f", struct.pack("<3f", *raw[i][0])))
        nrm.append(struct.unpack_from("<3f", vd, i * stride + chans[1][1]))
        cen.append(struct.unpack_from("<3f", vd, i * stride + chans[6][1]) if has_centre else (0.0, 0.0, 0.0))
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
    return pos, nrm, plate_list, cen if has_centre else None


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


def mass_crystal():
    """The Mass crystal as it ships: 60 triangular prisms (MassCrystalExport1), four shells of them."""
    nodes = fbx_binary.read(MASS_FBX)
    nodes = nodes[0] if isinstance(nodes, tuple) else nodes

    def walk(ns):
        for x in ns:
            yield x
            yield from walk(x.children)
    g = [x for x in walk(nodes) if x.name == "Geometry"][0]
    V = g.first("Vertices").props[0][1]
    I = g.first("PolygonVertexIndex").props[0][1]
    ln = g.first("LayerElementNormal")
    N = ln.first("Normals").props[0][1]
    NI = ln.first("NormalsIndex").props[0][1] if ln.first("ReferenceInformationType").props[0][1] == b"IndexToDirect" else list(range(len(I)))
    # Plates = connected components of control points (the model is exploded: 60 disjoint prisms).
    parent = list(range(len(V) // 3))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    polys, cur = [], []
    for j, x in enumerate(I):
        cur.append((~x if x < 0 else x, NI[j]))
        if x < 0:
            polys.append(cur)
            cur = []
    for poly in polys:
        for cp, _ in poly[1:]:
            parent[find(cp)] = find(poly[0][0])
    # Unity's import of this FBX (bakeAxisConversion 0, useFileScale, UnitScaleFactor 100): x negated,
    # winding reversed; the AUTHORED normals (normalImportMode 0). One vertex per polygon corner.
    pos, nrm, plate_of, tris = [], [], [], []
    for poly in polys:
        base = len(pos)
        for cp, n in poly:
            pos.append((-V[3 * cp], V[3 * cp + 1], V[3 * cp + 2]))
            nrm.append((-N[3 * n], N[3 * n + 1], N[3 * n + 2]))
            plate_of.append(find(cp))
        for k in range(1, len(poly) - 1):
            tris += [base, base + k + 1, base + k]
    roots = sorted(set(plate_of), key=lambda r: min(i for i, q in enumerate(plate_of) if q == r))
    plates = [[] for _ in roots]
    index = {r: i for i, r in enumerate(roots)}
    for t in range(0, len(tris), 3):
        plates[index[plate_of[tris[t]]]].extend(tris[t:t + 3])
    centre = []
    for i, q in enumerate(plate_of):
        corners = {pos[k] for k in range(len(pos)) if plate_of[k] == q}
        centre.append(tuple(sum(c[a] for c in corners) / len(corners) for a in range(3)))
    if len(plates) != 60:
        raise SystemExit(f"MassCrystalExport1: expected 60 triangular prisms, found {len(plates)}")
    pre = read(MASS_PREFAB)
    shells = []
    for name, active, inactive in MASS_SHELLS:
        L = shepard_layer(os.path.join(MATS, active + ".mat"))
        Li = shepard_layer(os.path.join(MATS, inactive + ".mat"))
        L["inactiveBright"], L["inactiveDark"] = Li["bright"], Li["dark"]
        L["material"] = active
        L["queue"] = int(re.search(r"m_CustomRenderQueue: (-?\d+)", read(os.path.join(MATS, active + ".mat"))).group(1))
        # Since round 8 the shells are retired; the prism renderer kept the inner shell's transform.
        m = (re.search(r"m_Name: " + re.escape(name) + r"\n(?:.*\n)*?--- !u!4 [^\n]*\nTransform:\n(?:.*\n)*?  m_LocalScale: \{x: ([0-9.]+)", pre)
             or re.search(r"m_Name: " + re.escape(MASS_DEPTH_MAT) + r"\n(?:.*\n)*?--- !u!4 [^\n]*\nTransform:\n(?:.*\n)*?  m_LocalScale: \{x: ([0-9.]+)", pre))
        L["localScale"] = float(m.group(1)) if m else 1.0
        shells.append(L)
    r5 = lambda v: round(v, 5)
    return {
        "shells": [{k: (round(v, 6) if isinstance(v, float) else v) for k, v in L.items()} for L in shells],
        "mesh": {"p": [r5(c) for q in pos for c in q], "n": [round(c, 4) for q in nrm for c in q],
                 "plates": plates, "c": [r5(c) for q in centre for c in q]},
    }


def space_squares(body_pts):
    """The squares animation, mapped vertex for vertex onto the shipped body FBX's control points."""
    if not os.path.exists(SPACE_SQUARES):
        return None
    d = json.load(open(SPACE_SQUARES))
    B = d["basis"]
    nb = len(B) // 3
    rb = max(math.sqrt(sum(c * c for c in B[3 * i:3 * i + 3])) for i in range(nb))
    rf = max(math.sqrt(sum(c * c for c in p)) for p in body_pts)
    sc = rb / rf   # the .blend carries the model at the triangle-export scale (2.2417x the body FBX)
    # Same axes, different vertex order: match by position (a coarse grid keeps it fast).
    grid = {}
    for i, p in enumerate(body_pts):
        grid.setdefault(tuple(round(c * 50) for c in p), []).append(i)
    mapping, worst = [], 0.0
    for i in range(nb):
        q = [B[3 * i + c] / sc for c in range(3)]
        key = tuple(round(c * 50) for c in q)
        cand = [j for dx in (-1, 0, 1) for dy in (-1, 0, 1) for dz in (-1, 0, 1)
                for j in grid.get((key[0] + dx, key[1] + dy, key[2] + dz), [])]
        j = min(cand, key=lambda j: sum((body_pts[j][c] - q[c]) ** 2 for c in range(3)))
        worst = max(worst, math.sqrt(sum((body_pts[j][c] - q[c]) ** 2 for c in range(3))))
        mapping.append(j)
    if len(set(mapping)) != nb or worst > 1e-4:
        raise SystemExit(f"omni_space_squares.json does not map onto the body FBX (unique {len(set(mapping))}/{nb}, worst {worst:.2e})")
    keys = []
    for k in d["keys"]:
        keys.append({"name": k["name"], "cps": [mapping[v] for v in k["verts"]],
                     "d": [round(c / sc, 6) for c in k["delta"]]})
    speed = 1.0
    if os.path.exists(SQUARES_ASSET):
        speed = float(re.search(r"^  playbackSpeed: (\S+)$", read(SQUARES_ASSET), re.M).group(1))
    return {"source": d["source"], "fps": d["fps"], "frames": len(d["weights"]), "speed": speed,
            "weights": [w for row in d["weights"] for w in row], "keys": keys, "mapErr": float(f"{worst:.2e}")}


def build():
    moving, rims, root_scale = crystal_layers()
    tri_scale = float(re.search(r"m_LocalScale: \{x: ([0-9.]+)", read(TRI_PREFAB)).group(1))
    depth = depth_look()
    if depth:
        # The prism prefab sits SEATED (the mesh on the crystal's triangles); the lab's triScale is the shell's
        # scale at s = 1, the top of a column: _Reach times that.
        tri_scale *= depth["omni"]["reach"]
    _, body_c, _ = mat_props(os.path.join(MATS, "OmniCrystalBody.mat"))
    _, body_ic, _ = mat_props(os.path.join(MATS, "OmniCrystalBodyInactive.mat"))
    pos, nrm, plates, cen = tri_mesh()
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
            "c": [r5(x) for q in cen for x in q] if cen else None,
        },
        "bodyMesh": {"p": [r5(c) for p in bpos for c in p], "i": btris, "n": [round(c, 4) for c in bnrm]},
        "mass": mass_crystal(),
        "squares": space_squares(bpos),
        "depth": depth,
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
    dp = d.get("depth")
    ships = (f"ships the Depth ratchet ({dp['omni']['material']} thickness {dp['omni']['thickness']}, "
             f"{dp['mass']['material'] if dp['mass'] else 'no Mass'} thickness {dp['mass']['thickness'] if dp['mass'] else '-'}, "
             f"open {dp['profile']['openDepth']} / seal {dp['profile']['sealDepth']} / reseat {dp['profile']['reseatDepth']}); "
             if dp else "")
    return (ships + f"{len(L)} moving layers{' (Breathe and Classic pinned in the page)' if not L else ' '}" + ", ".join(f"{x['material']} {x['start']}->{x['stop']} / {x['period']}s"
                                                   + (f" breathe phase {x['phaseOffset']}" if x['breathe'] else "") for x in L)
            + f"; {len(d['rims'])} rim; triScale {d['triScale']}; root {d['rootScale']}; "
            f"{len(d['triMesh']['plates'])} plates; body {len(d['bodyMesh']['i']) // 3} tris")


def self_test():
    d = build()
    ok = True

    def check(name, cond):
        nonlocal ok
        print(("PASS  " if cond else "FAIL  ") + name)
        ok = ok and cond
    L = d["layers"]
    check("the crystal carries no Shepard tone layer (the Depth ratchet ships)", len(L) == 0)
    page = read(LAB)
    m = re.search(r"^  const RETIRED = (\{.*\});$", page, re.M)
    pinned = json.loads(m.group(1)) if m else None
    check("the page pins the retired Breathe layers (3, one shared band, 1/3 of a trip apart) and the Classic rim",
          pinned is not None and len(pinned["layers"]) == 3 and pinned["rimRetired"] is not None
          and all(abs(x["phaseOffset"] - i / 3) < 1e-5 and x["start"] == 1.0 and x["stop"] == 0.5 for i, x in enumerate(pinned["layers"])))
    dp = d["depth"]
    check("the crystal ships the Depth ratchet: an OmniDepthPrisms slot, the Mass prisms and the profile",
          dp is not None and dp["mass"] is not None and dp["omni"]["material"] == "OmniDepthPrisms")
    if dp:
        check("the lab's shell scale at s = 1 is unchanged by the seated prism prefab (0.89219)",
              abs(d["triScale"] - 0.89219) < 1e-4)
        check("the shipped prisms are the lab's pick: omni thickness 8, Mass 6.25, inward, split 0.5, open -0.88, seal 1",
              dp["omni"]["thickness"] == 8 and dp["mass"]["thickness"] == 6.25 and dp["omni"]["thicknessInward"] == 1
              and dp["omni"]["faceSplit"] == 0.5 and dp["profile"]["openDepth"] == -0.88 and dp["profile"]["sealDepth"] == 1)
    if not L:
        pass
    elif L[0]["breathe"]:
        check("breathing layers share one band and sit 1/N of a trip apart",
              all(abs(x["start"] - L[0]["start"]) < 1e-6 and abs(x["stop"] - L[0]["stop"]) < 1e-6
                  and abs(x["phaseOffset"] - i / len(L)) < 1e-5 for i, x in enumerate(L)))
    else:
        check("bands are contiguous (layer i _Stop == layer i+1 _Start)",
              all(abs(L[i]["stop"] - L[i + 1]["start"]) < 1e-5 for i in range(len(L) - 1)))
    check("the triangle mesh carries a plate centre per vertex, shared by every vertex of a plate",
          d["triMesh"]["c"] is not None and all(
              len({tuple(d["triMesh"]["c"][3 * v:3 * v + 3]) for v in plate}) == 1 for plate in d["triMesh"]["plates"]))
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
    # Planted defects in the thickness bake: the lab must read the thickness the crystal draws at, or refuse.
    raw = raw_prisms("Mass")
    shipped = asset_prisms(MASS_PRISMS)
    check("the Mass prisms carry thickness 6.25 baked in", baked_thickness(MASS_PRISMS, raw) == 6.25)
    unbaked = [(p, c) for (p, _n, _uv, _c), (_q, c) in zip(raw, shipped)]
    check("an unbaked mesh reads thickness 1 (the lab would show the bake missing)", baked_thickness(MASS_PRISMS, raw, unbaked) == 1)
    for name, bent in (("one prism stretched by a different amount",
                        [((q[0] * 1.1, q[1] * 1.1, q[2] * 1.1) if round(c[3]) == 0 else q, c) for q, c in shipped]),
                       ("one vertex moved off the stretch", [((q[0] + 1e-3, q[1], q[2]) if i == 7 else q, c)
                                                            for i, (q, c) in enumerate(shipped)])):
        try:
            baked_thickness(MASS_PRISMS, raw, bent)
            check(f"a bake with {name} is refused", False)
        except SystemExit:
            check(f"a bake with {name} is refused", True)
    sq = d["squares"]
    if sq is not None:
        check("space squares: three keys on the 60 square plates (480 corners each), 120 frames",
              len(sq["keys"]) == 3 and all(len(k["cps"]) == 480 for k in sq["keys"]) and sq["frames"] == 120)
        # Planted defect: a body that is off by a millimetre-scale shift must not be silently mapped.
        bpos = [(p[0] + 0.01, p[1], p[2]) for p in body_mesh()[0]]
        try:
            space_squares(bpos)
            check("space squares refuse a body they do not map onto", False)
        except SystemExit:
            check("space squares refuse a body they do not map onto", True)
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
