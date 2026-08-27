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

 2. THE MESH FILE ID. Unity generates an FBX sub-asset's fileID from the object's
    NAME, and that generator is not reproducible outside the editor - so the mesh
    reference in a hand-written prefab cannot be computed. It can be BORROWED:
    give the triangles' node a name whose generated id is already recorded in the
    project and the same id comes out. `masscrystal.fbx` is Model `mass` /
    Geometry `Solid.001` -> mesh -6009661875889629336 (SpawnedSegments.prefab).
    The triangles' Geometry is ALREADY `Solid.001`, so renaming its Model node to
    `mass` makes BOTH names match that file - and the id is then the same under
    either naming rule, which is the point: it does not rest on knowing which of
    the two Unity reads. Nothing else in the FBX changes except that node's take
    prefixes, which move with it; assimp reads the edited file as the same mesh,
    bounds, materials and 3 animations as the original.

    The cost, stated plainly: RE-EXPORT that FBX from Blender and the node reverts
    to `MassCrystal.001`, the id changes, and the triangle shells render nothing.
    That is the one thing to check if they ever do - `OmniCrystalTriangles.prefab`'s
    MeshFilter, one field - and re-running this script re-borrows the id.
"""

import argparse
import math
import os
import re
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fbx_binary as F

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

OMNI_FBX = "Assets/_Models/OmniCrystalExport1_8-21-25.fbx"
TRI_FBX = "Assets/_Models/MassCrystalExport3ExpandedTri_10-23-25.fbx"
SHELL_PREFAB = "Assets/_Prefabs/Environment/OmniCrystalTriangles.prefab"
CRYSTAL_PREFAB = "Assets/_Prefabs/Environment/Crystal.prefab"

# The node name whose Unity-generated mesh id is recorded in the project (see the
# module docstring). Both the Model and the Geometry node must match `masscrystal.fbx`.
BORROWED_NODE_NAME = "mass"
BORROWED_GEOMETRY_NAME = "Solid.001"
TRI_MESH_FILE_ID = -6009661875889629336

TRI_FBX_GUID = "011f6da725a1826409614db176340426"
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


# ── FBX node rename ──────────────────────────────────────────────────────────

def _fbx_node_names(path):
    nodes, version, footer = F.read(os.path.join(ROOT, path))
    objs = [n for n in nodes if n.name == "Objects"][0]
    out = {}
    for child in objs.children:
        if child.name in ("Model", "Geometry"):
            out[child.name] = child.props[1][1].decode("utf-8", "replace").split("\x00")[0]
    return out, nodes, version, footer


def normalize_tri_fbx(check):
    """Borrow the mesh id: rename the Model node, carrying its take prefixes along.

    Blender names every take `<object>|<action>`, so the prefixes move with the
    rename or they describe a node the file no longer has. That is cosmetic and
    provably so - assimp reads the file as the same 3 animations before and after.

    NOTHING ELSE IN THE FBX IS TOUCHED, and one thing deliberately so. This export
    alone among the crystal models carries a node offset - Lcl Translation
    (1.9394, -7e-08, 5.4726) where its 120 vertices are symmetric about the origin -
    which reads as an object left off-origin in Blender. It is not: zeroing it takes
    assimp from 3 animations to 9, because six of the nine takes hold translation
    curves that ARE that offset and were being dropped as no-ops against it. The
    offset is the pose the takes were authored around, so it stays. It never reaches
    the shells anyway - they reference the MESH, whose vertices Unity keeps in node
    space, with the node transform left on the imported GameObject we do not use.
    (If the triangles ever appear ~24 world units off-centre, that assumption is what
    broke, and the fix is the shell prefab's Transform position.)
    """
    names, nodes, version, footer = _fbx_node_names(TRI_FBX)
    if names.get("Geometry") != BORROWED_GEOMETRY_NAME:
        raise SystemExit(
            f"{TRI_FBX}: Geometry node is {names.get('Geometry')!r}, expected "
            f"{BORROWED_GEOMETRY_NAME!r}. The borrowed mesh id only holds while BOTH this "
            "and the Model node match masscrystal.fbx - see the module docstring.")

    objs = [n for n in nodes if n.name == "Objects"][0]
    model = [c for c in objs.children if c.name == "Model"][0]
    want = BORROWED_NODE_NAME.encode()
    changes = []

    if names.get("Model") != BORROWED_NODE_NAME:
        changes.append(f"Model node {names.get('Model')!r} -> {BORROWED_NODE_NAME!r} "
                       f"(mesh fileID {TRI_MESH_FILE_ID})")

    takes = [c for c in objs.children if c.name in ("AnimationStack", "AnimationLayer")]
    stale = [c for c in takes
             if b"|" in c.props[1][1].split(b"\x00")[0]
             and c.props[1][1].split(b"|")[0] != want]
    if stale:
        was = sorted({c.props[1][1].split(b"|")[0].decode() for c in stale})
        changes.append(f"re-prefixed {len(stale)} animation takes {was} -> "
                       f"{BORROWED_NODE_NAME!r}")

    if not changes:
        return []
    if check:
        raise SystemExit(f"{TRI_FBX} is not normalized ({'; '.join(changes)}). "
                         "Run without --check to author it.")

    raw = model.props[1][1]
    model.props[1] = (model.props[1][0], want + raw[raw.index(b"\x00\x01"):])
    for take in stale:
        raw = take.props[1][1]
        take.props[1] = (take.props[1][0], want + raw[raw.index(b"|"):])
    F.write(os.path.join(ROOT, TRI_FBX), nodes, version, footer)
    return changes


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
  m_Mesh: {{fileID: {TRI_MESH_FILE_ID}, guid: {TRI_FBX_GUID}, type: 3}}
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

    for change in normalize_tri_fbx(args.check):
        print(f"{TRI_FBX}: {change}")

    want = {
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
