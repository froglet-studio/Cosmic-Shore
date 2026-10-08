#!/usr/bin/env python3
"""
Authors the OMNI crystal's Shepard-tone geometry.

    python3 Tools/Build/author_omni_crystal_triangles.py [--check]

The omni crystal is an EXPLODED polyhedron: 122 separate plates - 20 triangular
prisms, 90 boxes, 12 pentagonal prisms - one component per face of the solid.
Each family of plates is the shape that stands for one element, and each element
expresses ITS OWN effect on ITS OWN shapes. Mass owns the SHEPARD TONE, so the
tone belongs on the 20 triangles and on nothing else.

Before this, `Crystal.prefab` ran the Shepard shader over FOUR copies of the
WHOLE omni model, so the tone dragged the entire crystal - squares and pentagons
included - through every pulse, and the crystal had no body of its own: the one
static shell (`ActiveMassCrystalMaterial 3`) resolves to alpha 0.02..0.07, a
ghost. What ships now is a BODY, THREE tone shells and a stationary RIM:

    slot 0      the body - the whole omni model, static, on OmniCrystalFresnelShader
                (the elemental crystals' SpreadFresnel look + the Scarab morph path)
    slot 1..3   the Shepard chain, on the TRIANGLES ONLY, falling from outside the
                crystal onto its surface (MassCrystalExport3ExpandedTri, baked), on
                OmniShepardFresnelShader - the body's colour formula and colour pair
    slot 4      the stationary rim at the tone's birth radius (hides the pop)

and OriginalMaterialSet's CrystalMaterial..CrystalMaterial4 point at those five
per-slot materials, so a TEAM crystal is the same crystal in its domain colours.

Five slots, and the fifth is real: `ThemeManagerDataContainerSO.GetTeamCrystalMaterial`
answers indices 0..4 (SO_MaterialSet.CrystalMaterial4 was added for the rim), so a
team crystal paints every slot. Past 4 it still warns and reuses slot 0's material.

WHAT THIS SCRIPT OWNS - the things that must not be typed by hand (the body and
tone materials too; see the materials block below):

 1. THE SHELL SCALE. `MassCrystalExport3ExpandedTri_10-23-25.fbx` is EXACTLY the
    omni model's 20 triangular prisms scaled about the origin - measured 2.241676
    on every one of its 120 vertices (max residual 8.3e-07 against a 1.6172 mesh
    radius, i.e. 5e-07 relative). The shells carry OUTER_REACH / 2.241676 as their
    local scale, so the tone's s = 1 is OUTER_REACH x the crystal's triangles and
    s = 1/OUTER_REACH lands exactly ON them. Re-export either model at a different
    scale and --check fails here rather than in play.

 2. THE TRIANGLE MESH. The shells cannot reference the FBX's own mesh: Unity
    mints an FBX sub-asset's fileID inside the editor by a generator that is
    not reproducible offline (an earlier pass "borrowed" one by renaming the
    node to match another model's recorded id - Unity did not honour it and
    the shells rendered nothing). So this script BAKES the triangle FBX into a
    native Unity Mesh asset, `OmniCrystalTriangles.asset`, whose reference is
    `{fileID: 4300000, guid: <its own .meta>}` - deterministic by construction.
    The FBX stays the source: positions, normals and UVs are read from it and
    converted the way Unity imports this export (`bakeAxisConversion: 0`, so the
    mesh stays in node space; right->left handedness negates x and reverses
    winding; `UnitScaleFactor 100` x `useFileScale` = 1:1). Re-export the FBX and
    re-run this script; --check fails until you do.

If the triangle shells ever render nothing or sit rotated against the body,
the mesh asset's axis conversion is the one thing to check.
"""

import argparse
import hashlib
import math
import os
import re
import struct
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary as F

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

OMNI_FBX = "Assets/_Models/OmniCrystalExport1_8-21-25.fbx"
TRI_FBX = "Assets/_Models/MassCrystalExport3ExpandedTri_10-23-25.fbx"
SHELL_PREFAB = "Assets/_Prefabs/Environment/OmniCrystalTriangles.prefab"
CRYSTAL_PREFAB = "Assets/_Prefabs/Environment/Crystal.prefab"

TRI_MESH_ASSET = "Assets/_Models/OmniCrystalTriangles.asset"
TRI_MESH_GUID = "148e244052c4a0499c66e90863d9d5ee"   # md5("CosmicShore/OmniCrystal/OmniCrystalTriangles.asset")
TRI_MESH_FILE_ID = 4300000                          # a native .asset's main object
SHELL_PREFAB_GUID = "59cbf929dd08bc1e2d4c6d5f1e48b76a"
TRUNC_OCTA_GUID = "a089e5ab0159cc54aa784d6ebf15d2e4"   # TrucatedOctahedron.prefab (whole omni model)
FADE_IN_GUID = "318b4eb62a2693f4e98368eb975997cd"

# ── materials ──
# The BODY wears the omni's own shader (OmniCrystalFresnelShader: SpreadFresnelShader's look,
# transcribed, plus the morph + dissolve only the omni needs). Its two
# materials are clones of the elemental crystals' LimeCrystalFresnelMaterial /
# BlueCrystalFresnelMateriall - same colours and spread - moved onto that shader.
OMNI_SHADER_GUID = "554d96e9b67505208a3fbf0acb165350"           # OmniCrystalFresnelShader.shader
TONE_SHADER_GUID = "b55e562cd5045859607550ce7ba8267f"           # OmniShepardFresnelShader.shader
MAT_DIR = "Assets/_Graphics/Materials/CrystalMaterials"
BODY_DONORS = [("OmniCrystalBody", "LimeCrystalFresnelMaterial"),
               ("OmniCrystalBodyInactive", "BlueCrystalFresnelMateriall")]
MAT_BODY_EXPLODING = "383f21e8586fd7243a19e1d0f26110d0"  # CrystalMaterial: the spent husk animates
                                                         # CrystalGraph's _velocity, which the
                                                         # Fresnel shaders do not carry

# The TONE shells are on OmniShepardFresnelShader: the BODY's colour formula on the body's own
# colour pair (so a triangle in flight is the body's triangle, see-through, and can never drift to a
# different lime), with ShepardGraph's motion and alpha transcribed. ShepardGraph-style band:
# s sweeps _Start -> _Stop (falling when _Start > _Stop), the mesh is scaled by s about the origin,
# Alpha = (1.05 - s) * _Opacity. The shell prefab is scaled so s = 1 is OUTER_REACH x the crystal's
# own triangles and s = 1/OUTER_REACH is ON them, and the three bands tile that range - so the
# triangles fall in from outside the crystal and land on its surface, brightening as they arrive
# (alpha 0.05 at the outer edge, 1.05 - 1/OUTER_REACH at the surface).
OUTER_REACH = 2.0
SHEPARD_PERIOD = 3                       # the Mass crystal's period
SHEPARD_QUEUES = [2999, 3000, 3001]      # the Mass shells' draw order, outermost band first

def mat_guid(name):
    return hashlib.md5(f"CosmicShore/OmniCrystal/{name}.mat".encode()).hexdigest()


def shepard_bands():
    """(start, stop) per shell: three contiguous falling bands from s = 1 down to 1/OUTER_REACH."""
    lo = 1.0 / OUTER_REACH
    edges = [1.0 - (1.0 - lo) * k / 3 for k in range(4)]
    return [(round(edges[k], 6), round(edges[k + 1], 6)) for k in range(3)]


MAT_BODY = mat_guid("OmniCrystalBody")
MAT_BODY_INACTIVE = mat_guid("OmniCrystalBodyInactive")
MAT_SHEPARD = [mat_guid(f"OmniShepardTriangles {i}") for i in range(3)]
MAT_SHEPARD_INACTIVE = [mat_guid(f"OmniShepardTrianglesInactive {i}") for i in range(3)]
MAT_RIM_INACTIVE = mat_guid("OmniShepardTrianglesRimInactive")

# The STATIONARY rim shell - the Mass crystal's fourth shell, carried over. It never moves
# (_ScaleDistance 0), so at the shell prefab's scale it sits exactly at s = 1, the radius where
# each tone shell is born, and its band (1.03 -> 0.98) holds it at alpha 0.02..0.07 - the birth
# alpha of the incoming shell (0.05). That is what hides the pop: a shell appearing at the outer
# edge appears ON a faint copy of itself instead of out of nothing. An omni-only copy of
# ActiveMassCrystalMaterial 3's motion values (band 1.03 -> 0.98, no scaling, queue 3001) on the
# tone shader, so it wears the same colours as everything else.
RIM_NAME = "OmniShepardTrianglesRim"
RIM_BAND = (1.03, 0.98)
RIM_QUEUE = 3001
MAT_RIM = mat_guid(RIM_NAME)


def _donor(name):
    return open(os.path.join(ROOT, MAT_DIR, name + ".mat")).read()


def _sub1(text, pattern, repl, label):
    out, n = re.subn(pattern, repl, text, count=1, flags=re.M)
    if n != 1:
        raise SystemExit(f"material clone: {label} not found exactly once")
    return out


def _tone_material(name, body_name, body_text, start, stop, scale_distance, queue):
    """A tone-shell material: the body material (same colours) moved onto the tone shader."""
    t = _sub1(body_text, rf"^  m_Name: {re.escape(body_name)}$", f"  m_Name: {name}", name + " m_Name")
    t = _sub1(t, r"^  m_Shader: \{fileID: 4800000, guid: \w+, type: 3\}",
              f"  m_Shader: {{fileID: 4800000, guid: {TONE_SHADER_GUID}, type: 3}}", name + " m_Shader")
    t = _sub1(t, r"^  m_CustomRenderQueue: .*$", f"  m_CustomRenderQueue: {queue}", name + " queue")
    t = _sub1(t, r"^  stringTagMap: \{\}$", "  stringTagMap:\n    RenderType: Transparent", name + " tags")
    floats = (f"    - _Opacity: 1\n    - _Period: {SHEPARD_PERIOD}\n"
              f"    - _ScaleDistance: {scale_distance}\n    - _Start: {start}\n    - _Stop: {stop}\n")
    return _sub1(t, r"^    m_Floats:\n", lambda m: m.group(0) + floats, name + " m_Floats")


def material_texts():
    out = {}
    for name, donor in BODY_DONORS:
        t = _sub1(_donor(donor), rf"^  m_Name: {re.escape(donor)}$", f"  m_Name: {name}", name + " m_Name")
        t = _sub1(t, r"^  m_Shader: \{fileID: [-0-9]+, guid: \w+,\s*\n?\s*type: 3\}",
                  f"  m_Shader: {{fileID: 4800000, guid: {OMNI_SHADER_GUID}, type: 3}}", name + " m_Shader")
        out[name] = t
    body, body_inactive = out["OmniCrystalBody"], out["OmniCrystalBodyInactive"]
    for i, (start, stop) in enumerate(shepard_bands()):
        out[f"OmniShepardTriangles {i}"] = _tone_material(
            f"OmniShepardTriangles {i}", "OmniCrystalBody", body, start, stop, 1, SHEPARD_QUEUES[i])
        out[f"OmniShepardTrianglesInactive {i}"] = _tone_material(
            f"OmniShepardTrianglesInactive {i}", "OmniCrystalBodyInactive", body_inactive, start, stop, 1, SHEPARD_QUEUES[i])
    out[RIM_NAME] = _tone_material(RIM_NAME, "OmniCrystalBody", body, *RIM_BAND, 0, RIM_QUEUE)
    out[RIM_NAME + "Inactive"] = _tone_material(RIM_NAME + "Inactive", "OmniCrystalBodyInactive", body_inactive, *RIM_BAND, 0, RIM_QUEUE)
    return {f"{MAT_DIR}/{n}.mat": t for n, t in out.items()}


def material_meta(name):
    return (f"fileFormatVersion: 2\nguid: {mat_guid(name)}\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n")


# Stable ids inside OmniCrystalTriangles.prefab.
SHELL_GO, SHELL_TR, SHELL_MF, SHELL_MR, SHELL_FADE = (
    3086241560104200011, 3086241560104200012, 3086241560104200013,
    3086241560104200014, 3086241560104200015)
# Ids inside TrucatedOctahedron.prefab, for the body instance.
OCTA_GO, OCTA_TR, OCTA_MR = 2448508127246415652, 1114034561007818392, 2073129193669181770

# Crystal.prefab: the four child slots, in crystalModels order. The stripped
# Transform/GameObject ids are PRESERVED across this rewrite so the root's
# m_Children list and the crystalModels model references stay valid.
#
# The BODY is slot 0, and that is load-bearing: every consumer that wants "the crystal's shape"
# reads crystalModels[0] (ElementCrystalModelBuilder, SpawnMatrixToy's element visual), and the
# Scarab's crystal->ball forge (ScarabCrystalMorph.AdoptShells) builds its morph mesh from shell 0
# and folds every shell that draws the same mesh - the whole-model body, here.
SLOTS = [
    # (PrefabInstance id, stripped Transform id, stripped GameObject id, name)
    (302729943389656567,  812437664975045487,  2722806873126380243, "OmniCrystalBody"),
    (693643822389642704,  492451356381860680,  2907786364588143348, "OmniShepardTriangles"),
    (3428511433788108504, 2369276566672717888, 1039871714678528508, "OmniShepardTriangles (1)"),
    (5888802945109814545, 6831084959271910281, 8089558117201123893, "OmniShepardTriangles (2)"),
    # The stationary rim is slot 4, a real model, so a team crystal paints it in its domain like
    # every other slot: SO_MaterialSet.CrystalMaterial4 / GetTeamCrystalMaterial case 4.
    (3086241560104200021, 3086241560104200022, 3086241560104200023, RIM_NAME),
]

# Per slot: (default, inactive). Slot 0 is the body; 1..3 the falling tone; 4 the rim.
SLOT_MATERIALS = ([(MAT_BODY, MAT_BODY_INACTIVE)]
                  + list(zip(MAT_SHEPARD, MAT_SHEPARD_INACTIVE))
                  + [(MAT_RIM, MAT_RIM_INACTIVE)])


# ── measurement ──────────────────────────────────────────────────────────────

def _load(path):
    nodes, _, _ = F.read(os.path.join(ROOT, path))
    objs = [n for n in nodes if n.name == "Objects"][0]
    geo = [c for c in objs.children if c.name == "Geometry"][0]
    flat = geo.first("Vertices").props[0][1]
    pvi = geo.first("PolygonVertexIndex").props[0][1]
    verts = [(flat[i], flat[i + 1], flat[i + 2]) for i in range(0, len(flat), 3)]
    faces, cur = [], []
    for idx in pvi:
        if idx < 0:
            cur.append(-idx - 1)
            faces.append(tuple(cur))
            cur = []
        else:
            cur.append(idx)
    return verts, faces


def _components(verts, faces):
    parent = list(range(len(verts)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    for f in faces:
        for i in range(len(f)):
            ra, rb = find(f[i]), find(f[(i + 1) % len(f)])
            if ra != rb:
                parent[ra] = rb
    out = defaultdict(list)
    for fi, f in enumerate(faces):
        out[find(f[0])].append(fi)
    return out


def _triangular_prisms(verts, faces):
    """The plates that are triangular prisms: 2 triangles + 3 quads, 6 vertices."""
    out = []
    for _, fl in _components(verts, faces).items():
        sizes = Counter(len(faces[fi]) for fi in fl)
        if sizes.get(3) == 2 and sizes.get(4) == 3:
            out.append([verts[v] for v in sorted({v for fi in fl for v in faces[fi]})])
    return out


def measure():
    """The shell scale, re-derived from the two shipped models. Fails loud on drift."""
    ov, of = _load(OMNI_FBX)
    tv, tf = _load(TRI_FBX)

    omni_tris = _triangular_prisms(ov, of)
    tri_tris = _triangular_prisms(tv, tf)
    if len(omni_tris) != 20:
        raise SystemExit(f"{OMNI_FBX}: expected 20 triangular prisms, found {len(omni_tris)}")
    if len(tri_tris) != 20 or len(tv) != 120:
        raise SystemExit(f"{TRI_FBX}: expected 20 triangular prisms / 120 verts, "
                         f"found {len(tri_tris)} / {len(tv)}")

    omni_pts = [p for prism in omni_tris for p in prism]
    tri_pts = [p for prism in tri_tris for p in prism]

    def norm(v):
        return math.dist((0.0, 0.0, 0.0), v)

    # Least-squares uniform scale over the direction-matched vertex pairs.
    num = den = 0.0
    worst_cos = 0.0
    pairs = []
    for t in tri_pts:
        rt = norm(t)
        best = max(omni_pts, key=lambda o: sum(a * b for a, b in zip(t, o)) / (rt * norm(o)))
        cos = sum(a * b for a, b in zip(t, best)) / (rt * norm(best))
        worst_cos = max(worst_cos, 1.0 - cos)
        num += rt * norm(best)
        den += norm(best) ** 2
        pairs.append((t, best))
    scale = num / den
    residual = max(math.dist(t, tuple(c * scale for c in o)) for t, o in pairs)

    if worst_cos > 1e-9:
        raise SystemExit("the triangle model's prisms no longer point the same way as the omni "
                         f"model's (worst 1-cos {worst_cos:.3e}) - it is not the same 20 plates")
    if residual > 1e-4:
        raise SystemExit(f"the triangle model is not a UNIFORM scale of the omni triangles "
                         f"(max residual {residual:.3e}) - re-derive the shell scale by hand")
    return scale, residual


# ── triangle mesh asset ──────────────────────────────────────────────────────

def _layer(geo, name, data, index):
    layer = geo.first(name)
    assert layer.first("MappingInformationType").props[0][1] == b"ByPolygonVertex", name
    assert layer.first("ReferenceInformationType").props[0][1] == b"IndexToDirect", name
    return layer.first(data).props[0][1], layer.first(index).props[0][1]


def bake_tri_mesh():
    """The triangle FBX as Unity imports it, flattened to one vertex per polygon corner."""
    nodes, _, _ = F.read(os.path.join(ROOT, TRI_FBX))
    objs = [n for n in nodes if n.name == "Objects"][0]
    geo = [c for c in objs.children if c.name == "Geometry"][0]
    flat = geo.first("Vertices").props[0][1]
    pvi = geo.first("PolygonVertexIndex").props[0][1]
    normals, n_index = _layer(geo, "LayerElementNormal", "Normals", "NormalsIndex")
    uvs, uv_index = _layer(geo, "LayerElementUV", "UV", "UVIndex")

    verts, indices, corner, poly = [], [], 0, []
    for raw in pvi:
        last = raw < 0
        poly.append((-raw - 1 if last else raw, corner))
        corner += 1
        if not last:
            continue
        base = len(verts)
        for cp, c in poly:
            x, y, z = flat[3 * cp: 3 * cp + 3]
            n = n_index[c]
            nx, ny, nz = normals[3 * n: 3 * n + 3]
            u = uv_index[c]
            # Right-handed FBX -> left-handed Unity: negate x (positions AND normals).
            verts.append([(-x, y, z), (-nx, ny, nz), (uvs[2 * u], uvs[2 * u + 1])])
        # Fan-triangulate (every polygon here is a convex tri or quad), and reverse
        # the winding, because the handedness flip mirrored it.
        for k in range(1, len(poly) - 1):
            indices += [base, base + k + 1, base + k]
        poly = []

    # Per-face tangent from the UV gradient (the graph's Tangent block passes it through).
    tangents = [None] * len(verts)
    for t in range(0, len(indices), 3):
        i0, i1, i2 = indices[t:t + 3]
        (p0, n0, w0), (p1, _, w1), (p2, _, w2) = verts[i0], verts[i1], verts[i2]
        e1 = [p1[k] - p0[k] for k in range(3)]
        e2 = [p2[k] - p0[k] for k in range(3)]
        du1, dv1, du2, dv2 = w1[0] - w0[0], w1[1] - w0[1], w2[0] - w0[0], w2[1] - w0[1]
        det = du1 * dv2 - du2 * dv1
        tan = [(dv2 * e1[k] - dv1 * e2[k]) for k in range(3)] if abs(det) > 1e-12 else e1
        # Gram-Schmidt against the normal, then normalize.
        d = sum(tan[k] * n0[k] for k in range(3))
        tan = [tan[k] - n0[k] * d for k in range(3)]
        m = math.sqrt(sum(c * c for c in tan)) or 1.0
        for i in (i0, i1, i2):
            if tangents[i] is None:
                tangents[i] = (tan[0] / m, tan[1] / m, tan[2] / m, 1.0)

    assert len(verts) == 360 and len(indices) == 3 * 160, (len(verts), len(indices))
    assert all(t is not None for t in tangents)
    return verts, tangents, indices


def tri_mesh_text():
    verts, tangents, indices = bake_tri_mesh()
    vbuf = bytearray()
    for (p, n, uv), t in zip(verts, tangents):
        vbuf += struct.pack("<3f3f4f2f", *p, *n, *t, *uv)        # stride 48, Prism.asset layout
    ibuf = struct.pack("<%dH" % len(indices), *indices)
    lo = [min(v[0][k] for v in verts) for k in range(3)]
    hi = [max(v[0][k] for v in verts) for k in range(3)]
    ctr = [(lo[k] + hi[k]) / 2 for k in range(3)]
    ext = [(hi[k] - lo[k]) / 2 for k in range(3)]

    def v3(a):
        return "{x: %s, y: %s, z: %s}" % tuple(repr(float(struct.unpack("<f", struct.pack("<f", c))[0])) for c in a)

    # Channel table: position, normal, tangent, colour, uv0..7, blend weights, blend indices.
    used = {0: (0, 3), 1: (12, 3), 2: (24, 4), 4: (40, 2)}
    channels = "".join(
        "    - stream: 0\n      offset: %d\n      format: 0\n      dimension: %d\n"
        % used.get(i, (0, 0)) for i in range(14))
    donor = open(os.path.join(ROOT, "Assets/_Models/Testing/Prism.asset")).read()
    head = donor[:donor.index("  m_SubMeshes:")].replace("m_Name: Prism", "m_Name: OmniCrystalTriangles")
    tail = donor[donor.index("  m_CompressedMesh:"):]
    tail = re.sub(r"  m_LocalAABB:\n    m_Center: .*\n    m_Extent: .*\n",
                  f"  m_LocalAABB:\n    m_Center: {v3(ctr)}\n    m_Extent: {v3(ext)}\n", tail)
    return (head
            + "  m_SubMeshes:\n  - serializedVersion: 2\n    firstByte: 0\n"
            + f"    indexCount: {len(indices)}\n    topology: 0\n    baseVertex: 0\n"
            + f"    firstVertex: 0\n    vertexCount: {len(verts)}\n    localAABB:\n"
            + f"      m_Center: {v3(ctr)}\n      m_Extent: {v3(ext)}\n"
            + donor[donor.index("  m_Shapes:"):donor.index("  m_IndexBuffer:")]
            + f"  m_IndexBuffer: {ibuf.hex()}\n"
            + "  m_VertexData:\n    serializedVersion: 3\n"
            + f"    m_VertexCount: {len(verts)}\n    m_Channels:\n{channels}"
            + f"    m_DataSize: {len(vbuf)}\n    _typelessdata: {vbuf.hex()}\n"
            + tail)


TRI_MESH_META = f"""fileFormatVersion: 2
guid: {TRI_MESH_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 4300000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


# ── prefab authoring ─────────────────────────────────────────────────────────

def shell_prefab_text(scale):
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{SHELL_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {SHELL_TR}}}
  - component: {{fileID: {SHELL_MF}}}
  - component: {{fileID: {SHELL_MR}}}
  - component: {{fileID: {SHELL_FADE}}}
  m_Layer: 0
  m_Name: OmniCrystalTriangles
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{SHELL_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHELL_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: {scale}, y: {scale}, z: {scale}}}
  m_ConstrainProportionsScale: 1
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{SHELL_MF}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHELL_GO}}}
  m_Mesh: {{fileID: {TRI_MESH_FILE_ID}, guid: {TRI_MESH_GUID}, type: 2}}
--- !u!23 &{SHELL_MR}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHELL_GO}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {MAT_SHEPARD[0]}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
--- !u!114 &{SHELL_FADE}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {SHELL_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {FADE_IN_GUID}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: 
  fadeInRate: 0
"""


SHELL_META = f"""fileFormatVersion: 2
guid: {SHELL_PREFAB_GUID}
PrefabImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def _instance_block(inst_id, source_guid, target_go, target_tr, target_mr, name, material_guid):
    def mod(target, path, value="", ref="{fileID: 0}"):
        return (f"    - target: {{fileID: {target}, guid: {source_guid},\n"
                f"        type: 3}}\n"
                f"      propertyPath: {path}\n"
                f"      value: {value}\n"
                f"      objectReference: {ref}\n")

    mods = "".join(
        [mod(target_tr, f"m_LocalPosition.{a}", "0") for a in "xyz"]
        + [mod(target_tr, "m_LocalRotation.w", "1")]
        + [mod(target_tr, f"m_LocalRotation.{a}", "0") for a in "xyz"]
        + [mod(target_tr, f"m_LocalEulerAnglesHint.{a}", "0") for a in "xyz"]
        + [mod(target_mr, "m_Materials.Array.size", "1")]
        + [mod(target_mr, "'m_Materials.Array.data[0]'", "",
               f"{{fileID: 2100000, guid: {material_guid}, type: 2}}")]
        + [mod(target_go, "m_Name", name)]
    )
    return f"""--- !u!1001 &{inst_id}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 5535990081244205889}}
    m_Modifications:
{mods}    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {{fileID: 100100000, guid: {source_guid}, type: 3}}
"""


def _stripped_blocks(inst_id, source_guid, stripped_tr, stripped_go, target_tr, target_go):
    return f"""--- !u!4 &{stripped_tr} stripped
Transform:
  m_CorrespondingSourceObject: {{fileID: {target_tr}, guid: {source_guid},
    type: 3}}
  m_PrefabInstance: {{fileID: {inst_id}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!1 &{stripped_go} stripped
GameObject:
  m_CorrespondingSourceObject: {{fileID: {target_go}, guid: {source_guid},
    type: 3}}
  m_PrefabInstance: {{fileID: {inst_id}}}
  m_PrefabAsset: {{fileID: 0}}
"""


def crystal_children_text():
    """The five child PrefabInstances of Crystal.prefab, in crystalModels order."""
    out = []
    for i, (inst, stripped_tr, stripped_go, name) in enumerate(SLOTS):
        body = i == 0
        guid = TRUNC_OCTA_GUID if body else SHELL_PREFAB_GUID
        t_go, t_tr, t_mr = ((OCTA_GO, OCTA_TR, OCTA_MR) if body
                            else (SHELL_GO, SHELL_TR, SHELL_MR))
        mat = SLOT_MATERIALS[i][0]
        out.append(_instance_block(inst, guid, t_go, t_tr, t_mr, name, mat))
        out.append(_stripped_blocks(inst, guid, stripped_tr, stripped_go, t_tr, t_go))
    return "".join(out)


def crystal_models_text():
    """The Crystal component's crystalModels list - the body, the three tone shells, the rim.

    Every slot explodes on CrystalMaterial: the spent husk animates CrystalGraph's _velocity, which
    neither Fresnel shader carries, and a tone material on a husk would play its band, not burst."""
    rows = []
    for i, (_, _, stripped_go, _) in enumerate(SLOTS):
        default, inactive = SLOT_MATERIALS[i]
        exploding = MAT_BODY_EXPLODING
        rows.append(
            f"  - model: {{fileID: {stripped_go}}}\n"
            f"    defaultMaterial: {{fileID: 2100000, guid: {default}, type: 2}}\n"
            f"    explodingMaterial: {{fileID: 2100000, guid: {exploding}, type: 2}}\n"
            f"    inactiveMaterial: {{fileID: 2100000, guid: {inactive}, type: 2}}\n"
            f"    spaceCrystalAnimator: {{fileID: 0}}\n")
    return "  crystalModels:\n" + "".join(rows)


# ── team crystals ──
# A domain-owned crystal (Skim Race track crystals via CrystalManager, the Dolphin's TeamCrystal)
# swaps each model to ThemeManagerDataContainerSO.GetTeamCrystalMaterial(domain, slot), and
# ThemeManager builds those by cloning the BASE set's CrystalMaterial..CrystalMaterial4 and painting
# the domain pair (_BrightColor = BrightCrystalColor, _DarkColor = DullCrystalColor). So the base set
# must point at exactly the omni's per-slot materials, or a team omni wears some other geometry's
# bands - which is what the Mass-era set did (a band on the body, inward bands on the triangles).
BASE_MATERIAL_SET = "Assets/_SO_Assets/MaterialSets/OriginalMaterialSet.asset"
BASE_SET_FIELDS = ["CrystalMaterial", "CrystalMaterial1", "CrystalMaterial2", "CrystalMaterial3",
                   "CrystalMaterial4"]


def build_base_material_set(existing):
    t = existing
    for field, (default, _) in zip(BASE_SET_FIELDS, SLOT_MATERIALS):
        line = f"  {field}: {{fileID: 2100000, guid: {default}, type: 2}}"
        t, n = re.subn(rf"^  {field}: \{{fileID: \d+, guid: \w+,\s*\n?\s*type: 2\}}$", line, t,
                       count=1, flags=re.M)
        if n == 0:
            # A field the asset has never serialized (CrystalMaterial4) goes after its predecessor.
            prev = BASE_SET_FIELDS[BASE_SET_FIELDS.index(field) - 1]
            t, n = re.subn(rf"(^  {prev}: \{{[^}}]*\}}\n)", lambda m: m.group(1) + line + "\n", t,
                           count=1, flags=re.M)
        if n != 1:
            raise SystemExit(f"{BASE_MATERIAL_SET}: could not author {field}")
    return t


CHILDREN_START = re.compile(r"^--- !u!1001 &", re.M)
MODELS_BLOCK = re.compile(r"^  crystalModels:\n(?:  - model:.*?\n(?:    .*\n)*)+", re.M)


def build_crystal_prefab(existing):
    head = existing[:CHILDREN_START.search(existing).start()]
    head, n = MODELS_BLOCK.subn(crystal_models_text(), head)
    children = "".join(f"  - {{fileID: {tr}}}\n" for _, tr, _, _ in SLOTS)
    head, m = re.subn(r"(^  m_Children:\n)(?:  - \{fileID: \d+\}\n){4,5}", lambda x: x.group(1) + children,
                      head, count=1, flags=re.M)
    if m != 1:
        raise SystemExit(f"{CRYSTAL_PREFAB}: root m_Children (4-5 entries) not found")
    if n != 1:
        raise SystemExit(f"{CRYSTAL_PREFAB}: expected exactly one crystalModels block, found {n}")
    return head + crystal_children_text()


# ── entry point ──────────────────────────────────────────────────────────────

def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true",
                    help="verify the shipped assets match what this script would author")
    args = ap.parse_args()

    scale, residual = measure()
    shell_scale = round(OUTER_REACH / scale, 9)
    print(f"omni triangles -> triangle model: uniform scale {scale:.9f} "
          f"(max residual {residual:.3e})")
    print(f"shell localScale: {shell_scale}  (s = 1 is {OUTER_REACH}x the crystal's triangles)")
    print(f"tone bands (start -> stop): {shepard_bands()}")

    want = {
        TRI_MESH_ASSET: tri_mesh_text(),
        TRI_MESH_ASSET + ".meta": TRI_MESH_META,
        SHELL_PREFAB: shell_prefab_text(shell_scale),
        SHELL_PREFAB + ".meta": SHELL_META,
    }
    for path, text in material_texts().items():
        want[path] = text
        want[path + ".meta"] = material_meta(os.path.basename(path)[:-4])
    crystal_path = os.path.join(ROOT, CRYSTAL_PREFAB)
    want[CRYSTAL_PREFAB] = build_crystal_prefab(open(crystal_path).read())
    want[BASE_MATERIAL_SET] = build_base_material_set(open(os.path.join(ROOT, BASE_MATERIAL_SET)).read())

    failures = []
    for rel, text in want.items():
        path = os.path.join(ROOT, rel)
        have = open(path).read() if os.path.exists(path) else None
        if have == text:
            print(f"  ok      {rel}")
            continue
        if args.check:
            failures.append(rel)
            print(f"  DRIFT   {rel}")
        else:
            open(path, "w").write(text)
            print(f"  wrote   {rel}")

    if failures:
        raise SystemExit("author_omni_crystal_triangles: " + ", ".join(failures) +
                         " differ from what this script authors. Run it without --check.")


if __name__ == "__main__":
    main()
