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
ghost. What ships now is a BODY plus THREE tone shells:

    slot 0..2   the Shepard chain, on the TRIANGLES ONLY
                (MassCrystalExport3ExpandedTri, one component per omni triangle)
    slot 3      the body - the whole omni model, static, CrystalMaterial

Four slots exactly, because `ThemeManagerDataContainerSO.GetTeamCrystalMaterial`
answers indices 0..3 and warns past them; a fifth model would be a warning on
every domain-owned activation and would silently reuse slot 0's team material.

WHAT THIS SCRIPT OWNS - the two things that must not be typed by hand:

 1. THE SHELL SCALE. `MassCrystalExport3ExpandedTri_10-23-25.fbx` is EXACTLY the
    omni model's 20 triangular prisms scaled about the origin - measured 2.241676
    on every one of its 120 vertices (max residual 8.3e-07 against a 1.6172 mesh
    radius, i.e. 5e-07 relative). So the shells carry the RECIPROCAL as their
    local scale and the tone's outermost reach lands precisely on the body's own
    triangles instead of 2.24x outside the crystal - which is also what keeps the
    whole crystal inside the 1.2 pickup collider it has always sat in. Re-export
    either model at a different scale and --check fails here rather than in play.

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

# Materials (guid -> what it is), all pre-existing assets.
MAT_SHEPARD = ["20e4974a2de18e44880740ec87d82e44",   # ActiveMassCrystalMaterial    band 0.00-0.33
               "2438fa6e25f42f04f9cff49f3f505acc",   # ActiveMassCrystalMaterial 1  band 0.33-0.66
               "0605bc709a4621e48984803a3ebca8a3"]   # ActiveMassCrystalMaterial 2  band 0.66-1.00
MAT_SHEPARD_INACTIVE = ["650830ed7524d074991cd928a5356f37",   # BlueCrystalMaterial
                        "77544dd168c53564e81549be625b0955",   # BlueMassCrystalMaterial 1
                        "abcf956848542144a91c42a841a1f21d"]   # BlueMassCrystalMaterial 2
MAT_BODY = "383f21e8586fd7243a19e1d0f26110d0"            # CrystalMaterial      (free-pickup lime CTA)
MAT_BODY_INACTIVE = "650830ed7524d074991cd928a5356f37"   # BlueCrystalMaterial

# Stable ids inside OmniCrystalTriangles.prefab.
SHELL_GO, SHELL_TR, SHELL_MF, SHELL_MR, SHELL_FADE = (
    3086241560104200011, 3086241560104200012, 3086241560104200013,
    3086241560104200014, 3086241560104200015)
# Ids inside TrucatedOctahedron.prefab, for the body instance.
OCTA_GO, OCTA_TR, OCTA_MR = 2448508127246415652, 1114034561007818392, 2073129193669181770

# Crystal.prefab: the four child slots, in crystalModels order. The stripped
# Transform/GameObject ids are PRESERVED across this rewrite so the root's
# m_Children list and the crystalModels model references stay valid.
SLOTS = [
    # (PrefabInstance id, stripped Transform id, stripped GameObject id, name)
    (693643822389642704,  492451356381860680,  2907786364588143348, "OmniShepardTriangles"),
    (3428511433788108504, 2369276566672717888, 1039871714678528508, "OmniShepardTriangles (1)"),
    (5888802945109814545, 6831084959271910281, 8089558117201123893, "OmniShepardTriangles (2)"),
    (302729943389656567,  812437664975045487,  2722806873126380243, "OmniCrystalBody"),
]


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
    """The four child PrefabInstances of Crystal.prefab, in crystalModels order."""
    out = []
    for i, (inst, stripped_tr, stripped_go, name) in enumerate(SLOTS):
        body = i == 3
        guid = TRUNC_OCTA_GUID if body else SHELL_PREFAB_GUID
        t_go, t_tr, t_mr = ((OCTA_GO, OCTA_TR, OCTA_MR) if body
                            else (SHELL_GO, SHELL_TR, SHELL_MR))
        mat = MAT_BODY if body else MAT_SHEPARD[i]
        out.append(_instance_block(inst, guid, t_go, t_tr, t_mr, name, mat))
        out.append(_stripped_blocks(inst, guid, stripped_tr, stripped_go, t_tr, t_go))
    return "".join(out)


def crystal_models_text():
    """The Crystal component's crystalModels list - three tone shells, then the body."""
    rows = []
    for i, (_, _, stripped_go, _) in enumerate(SLOTS):
        body = i == 3
        default = MAT_BODY if body else MAT_SHEPARD[i]
        inactive = MAT_BODY_INACTIVE if body else MAT_SHEPARD_INACTIVE[i]
        rows.append(
            f"  - model: {{fileID: {stripped_go}}}\n"
            f"    defaultMaterial: {{fileID: 2100000, guid: {default}, type: 2}}\n"
            f"    explodingMaterial: {{fileID: 2100000, guid: {default}, type: 2}}\n"
            f"    inactiveMaterial: {{fileID: 2100000, guid: {inactive}, type: 2}}\n"
            f"    spaceCrystalAnimator: {{fileID: 0}}\n")
    return "  crystalModels:\n" + "".join(rows)


CHILDREN_START = re.compile(r"^--- !u!1001 &", re.M)
MODELS_BLOCK = re.compile(r"^  crystalModels:\n(?:  - model:.*?\n(?:    .*\n)*)+", re.M)


def build_crystal_prefab(existing):
    head = existing[:CHILDREN_START.search(existing).start()]
    head, n = MODELS_BLOCK.subn(crystal_models_text(), head)
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
    shell_scale = round(1.0 / scale, 9)
    print(f"omni triangles -> triangle model: uniform scale {scale:.9f} "
          f"(max residual {residual:.3e})")
    print(f"shell localScale: {shell_scale}")

    want = {
        TRI_MESH_ASSET: tri_mesh_text(),
        TRI_MESH_ASSET + ".meta": TRI_MESH_META,
        SHELL_PREFAB: shell_prefab_text(shell_scale),
        SHELL_PREFAB + ".meta": SHELL_META,
    }
    crystal_path = os.path.join(ROOT, CRYSTAL_PREFAB)
    want[CRYSTAL_PREFAB] = build_crystal_prefab(open(crystal_path).read())

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
