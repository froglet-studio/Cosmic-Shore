#!/usr/bin/env python3
"""The Thresher's OWN VESSEL pass: its shark hull, its swim, its colliders and jets on Thresher.prefab, with
the Squirrel model it was cloned from removed. Then prove the shipped set.

    python3 Tools/Build/author_thresher_assets.py             # apply (idempotent), then check
    python3 Tools/Build/author_thresher_assets.py --check     # validate the SHIPPED files, write nothing
    python3 Tools/Build/author_thresher_assets.py --self-test # prove the checks fire (negative controls)

WHAT IT DOES (`R_VesselActions/THRESHER.md` §5b), all on the committed prefab, idempotently - the
`author_stoat_assets.py` stage 6 shape (whose document-model helpers this imports: split_docs,
remove_instance, dangling_refs, ...):
    - REMOVES the Squirrel FBX instance (SquirrelVessel_CosmicShoresTest1) and everything added to it: its
      two box colliders and ImpactCollider (re-authored below at the shark's size), two of its four jets,
      and the DriftJet steering on the other two (it turns a plume SIDEWAYS unless drifting - a Squirrel
      behaviour). The kept jets are re-homed onto the hull, flanking the peduncle, pointing back.
    - ADDS ThresherHull under the root: a Transform holding the hull MODEL (Thresher.fbx, built by
      /vessel-model from Tools/Build/vessel_model/specs/thresher.py, 2026-10-18) as a nested prefab instance,
      and the two kept jets. Until 2026-10-18 the hull carried a MeshFilter, a MeshRenderer and the procedural
      ThresherHullBuilder; re-running the pass on a prefab that still has them removes them. The builder's
      C# stays in the tree until Garrett accepts the model.
    - ADDS ThresherBody (non-trigger box + ImpactCollider, layer Default) and ThresherHitbox (trigger box,
      layer Ships) - the two shapes the FBX's two GameObjects carried, sized to the shark.
    - SWAPS the root's VesselAnimation from the clone's MantaAnimationContoller to ThresherAnimation on the
      SAME fileID (so VesselStatus's reference holds); the swim's numbers are the C# initializers
      (ThresherSwimMath.Default*), only the base fields and cruiseSpeed are written.
    - POINTS VesselCustomization's paint at the hull (which resolves to the model's SkinnedMeshRenderer) and
      names GreenAccentVesselMaterial - what the model's Domain slot is remapped to - as the material the domain
      colour replaces (paint by identity, FLEET_CONVENTION 3.3).
    - CrystalHullFusionConfig: the four bakes keyed to the Squirrel model the clone carried are stripped and
      their assets deleted (a spent one-shot since 2026-10-13: it now only stands guard). A bake against the
      Thresher's own model (the FrogletTools bake tool, in the editor) is left alone.
    - Authors the .meta of every new script (deterministic guids).

--check validates the shipped prefab: no Squirrel FBX / DriftJet / Manta animation left, the hull, its
model (no procedural builder) and the colliders present and wired, the paint on the hull, the animation swapped, two jets on the
hull, NO dangling local reference, no fusion entry on a Squirrel-keyed bake and those assets gone, every script meta
unique. --self-test mutates a copy in memory and proves each check fires.
"""
import hashlib
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import author_stoat_assets as S  # noqa: E402  (the document-model helpers; module import has no side effects)

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
PREFAB = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels", "Thresher.prefab")
FUSION_CONFIG = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
FUSION_DIR = os.path.join(ROOT, "Assets", "_SO_Assets", "CrystalHullFusion")
SCRIPTS = os.path.join(ROOT, "Assets", "_Scripts")

THRESHER_CLASS_ID = 14
ROOT_GO = "6417075533431866457"
ROOT_TRANSFORM_ID = "5928382658856804067"
ANIMATION_ID = "5500781737155866732"
CUSTOMIZATION_ID = "7779107667008759293"
VESSEL_IMPACTOR_ID = "2643412773408452782"
SQUIRREL_FBX_INSTANCE = "8410834229753696920"
SQUIRREL_MODEL_ROOT = "8301242790861813107"
SQUIRREL_MESH_GO = "3168954648161338247"
SQUIRREL_FBX_GUID = "5db69771d02b4c24ca00998adf318137"
MANTA_ANIMATION_SCRIPT = "2116872444dccfd43a29513c3af008b7"
DRIFT_JET_SCRIPT = "4fa735e2aecc47b46a59091cf80da687"
IMPACT_COLLIDER_SCRIPT = "fafcc767febd41ccbad67c5457dc432d"
VESSEL_JET_GUID = "55ceca573be662744948afc745f8840b"
BODY_MATERIAL = "54e78ce17120fe641904833c11bc1210"     # BlueBaseVesselMaterial  (slot 0, the hide)
ACCENT_MATERIAL = "539a8c65974bf0b48aae77d83884c13b"   # GreenAccentVesselMaterial (slot 1, repainted to the domain)
SHIPS_LAYER = 8
# The hull model (/vessel-model, specs/thresher.py) and its nested instance under ThresherHull. A model's root
# GameObject / Transform fileIDs are constants of the importer; the instance and its stripped Transform are ours.
MODEL_GUID = "7afd7f8333454ad7b4417f1bdab7fa29"          # Assets/_Models/Vessel Models/Thresher.fbx
MODEL_META = os.path.join(ROOT, "Assets", "_Models", "Vessel Models", "Thresher.fbx.meta")
MODEL_ROOT_GO = "919132149155446097"
MODEL_ROOT_TRANSFORM = "-8679921383154817045"
MODEL_INSTANCE_ID = "8333434907443222190"
MODEL_TRANSFORM_ID = "1945698819565915686"
# The four bakes the 2026-10-13 pass removed: keyed to the Squirrel FBX the clone carried (never the shark).
SQUIRREL_KEYED_BAKES = ("eb63e1fdbcaca2a4da87e864d4939cb2", "0628d54ba9c99e844a3eae33cca5726d",
                        "724bc06c870361f468d8e75ecda38cc9", "320ec7cbdff541243be27d82e9468bcc")

# (PrefabInstance, its root Transform (stripped), local x): the two jets kept, re-homed onto the hull.
JETS_KEPT = (("3003162917154485572", "2617076957950753345", -0.32), ("3287304896823774770", "2333430861380747575", 0.32))
JETS_DROPPED = ("4407056506752009302", "6084043296942237368")
# Flanking the peduncle, pointing back (a VesselJet's identity rotation streams along -Z, as the Stoat's do).
JET_LOCAL_Y, JET_LOCAL_Z = -0.08, -2.05
# Plume dial (VESSEL_TAIL_AND_JETS.md §3): (0.6, 0.6, 0.13) x |followOffset.z| / 20 = x2.75 at the
# Thresher camera's 55 (ThresherCameraSettingsSO); the root's scale is 1.
JET_SCALE = (1.65, 1.65, 0.3575)

# New documents: one readable family (all < INT64_MAX).
HULL_GO_ID = "7788112233445567701"
HULL_TRANSFORM_ID = "7788112233445567702"
HULL_FILTER_ID = "7788112233445567703"
HULL_RENDERER_ID = "7788112233445567704"
HULL_BUILDER_ID = "7788112233445567705"
BODY_GO_ID = "7788112233445567710"
BODY_TRANSFORM_ID = "7788112233445567711"
BODY_COLLIDER_ID = "7788112233445567712"
BODY_IMPACT_ID = "7788112233445567713"
TRIGGER_GO_ID = "7788112233445567714"
TRIGGER_TRANSFORM_ID = "7788112233445567715"
TRIGGER_COLLIDER_ID = "7788112233445567716"
# The hit box: the shark's body from the snout to the root of the scythe (ThresherHullForm at defaults:
# body |x| <= 0.62, y -0.53..0.53 before the dorsal, snout z 2.29, fork z -2.7), a little generous across;
# the thin fins and the scythe are not hull - the chain and the ball are the Thresher's reach.
BODY_BOX_CENTER = (0, 0.05, -0.2)
BODY_BOX_SIZE = (1.6, 1.4, 5.2)
TRIGGER_BOX_SIZE = (2.2, 1.9, 5.8)


def guid_for(name: str) -> str:
    return hashlib.md5(f"CosmicShore/Thresher/{name}".encode()).hexdigest()


NEW_SCRIPTS = (   # every script this pass added, its .meta authored here
    os.path.join(SCRIPTS, "Controller", "Vessel", "ThresherHullForm.cs"),
    os.path.join(SCRIPTS, "Controller", "Vessel", "ThresherHullBuilder.cs"),
    os.path.join(SCRIPTS, "Controller", "Vessel", "ThresherBallForm.cs"),
    os.path.join(SCRIPTS, "Controller", "Animation", "ThresherAnimation.cs"),
    os.path.join(SCRIPTS, "Controller", "Animation", "ThresherSwimMath.cs"),
    os.path.join(SCRIPTS, "Tests", "Editor", "ThresherHullFormTests.cs"),
)
HULL_BUILDER_SCRIPT = guid_for("ThresherHullBuilder.cs")
ANIMATION_SCRIPT = guid_for("ThresherAnimation.cs")


def v3(x, y, z) -> str:
    return f"{{x: {S.num(x)}, y: {S.num(y)}, z: {S.num(z)}}}"


# ----------------------------------------------------------------------------- the documents
def hull_blocks() -> str:
    """ThresherHull: a Transform that holds the model (nested) and the two kept jets. ThresherAnimation banks and
    sways THIS transform, so the model and the jets move together; the model's bones carry the swim."""
    model_mods = [
        (MODEL_ROOT_GO, "m_Name", "Thresher"),
        (MODEL_ROOT_TRANSFORM, "m_LocalPosition.x", 0), (MODEL_ROOT_TRANSFORM, "m_LocalPosition.y", 0),
        (MODEL_ROOT_TRANSFORM, "m_LocalPosition.z", 0), (MODEL_ROOT_TRANSFORM, "m_LocalRotation.w", 1),
        (MODEL_ROOT_TRANSFORM, "m_LocalRotation.x", 0), (MODEL_ROOT_TRANSFORM, "m_LocalRotation.y", 0),
        (MODEL_ROOT_TRANSFORM, "m_LocalRotation.z", 0), (MODEL_ROOT_TRANSFORM, "m_LocalEulerAnglesHint.x", 0),
        (MODEL_ROOT_TRANSFORM, "m_LocalEulerAnglesHint.y", 0), (MODEL_ROOT_TRANSFORM, "m_LocalEulerAnglesHint.z", 0),
    ]
    mods = "".join(
        f"    - target: {{fileID: {fid}, guid: {MODEL_GUID},\n        type: 3}}\n"
        f"      propertyPath: {path}\n      value: {v if isinstance(v, str) else S.num(v)}\n"
        "      objectReference: {fileID: 0}\n" for fid, path, v in model_mods)
    return (
        f"--- !u!1 &{HULL_GO_ID}\n"
        "GameObject:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        "  serializedVersion: 6\n"
        "  m_Component:\n"
        f"  - component: {{fileID: {HULL_TRANSFORM_ID}}}\n"
        f"  m_Layer: {SHIPS_LAYER}\n"
        "  m_Name: ThresherHull\n"
        "  m_TagString: Untagged\n"
        "  m_Icon: {fileID: 0}\n"
        "  m_NavMeshLayer: 0\n"
        "  m_StaticEditorFlags: 0\n"
        "  m_IsActive: 1\n"
        f"--- !u!4 &{HULL_TRANSFORM_ID}\n"
        "Transform:\n"
        "  m_ObjectHideFlags: 0\n"
        "  m_CorrespondingSourceObject: {fileID: 0}\n"
        "  m_PrefabInstance: {fileID: 0}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {HULL_GO_ID}}}\n"
        "  serializedVersion: 2\n"
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
        "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
        "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
        "  m_ConstrainProportionsScale: 0\n"
        "  m_Children:\n" + "".join(f"  - {{fileID: {tr}}}\n" for _, tr, _ in JETS_KEPT) +
        f"  - {{fileID: {MODEL_TRANSFORM_ID}}}\n"
        f"  m_Father: {{fileID: {ROOT_TRANSFORM_ID}}}\n"
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n"
        f"--- !u!1001 &{MODEL_INSTANCE_ID}\n"
        "PrefabInstance:\n"
        "  m_ObjectHideFlags: 0\n"
        "  serializedVersion: 2\n"
        "  m_Modification:\n"
        "    serializedVersion: 3\n"
        f"    m_TransformParent: {{fileID: {HULL_TRANSFORM_ID}}}\n"
        "    m_Modifications:\n" + mods +
        "    m_RemovedComponents: []\n"
        "    m_RemovedGameObjects: []\n"
        "    m_AddedGameObjects: []\n"
        "    m_AddedComponents: []\n"
        f"  m_SourcePrefab: {{fileID: 100100000, guid: {MODEL_GUID}, type: 3}}\n"
        f"--- !u!4 &{MODEL_TRANSFORM_ID} stripped\n"
        "Transform:\n"
        f"  m_CorrespondingSourceObject: {{fileID: {MODEL_ROOT_TRANSFORM}, guid: {MODEL_GUID},\n"
        "    type: 3}\n"
        f"  m_PrefabInstance: {{fileID: {MODEL_INSTANCE_ID}}}\n"
        "  m_PrefabAsset: {fileID: 0}\n"
    )


def animation_block() -> str:
    """The root's VesselAnimation, switched to ThresherAnimation on the SAME fileID. The base fields keep their
    authored values; the swim is the C# initializers (ThresherSwimMath.Default*), except cruiseSpeed, which is
    the hull's own DefaultThrottleScaler and is written so a retune of the prefab shows up here."""
    return (S.mb_header(ANIMATION_ID, ROOT_GO, ANIMATION_SCRIPT) +
            "  SkinnedMeshRenderer: {fileID: 0}\n"
            "  SaveNewPositions: 1\n"
            "  brakeThreshold: 0.65\n"
            "  lerpAmount: 2\n"
            "  smallLerpAmount: 0.7\n"
            f"  cruiseSpeed: {S.num(cruise_speed_placeholder())}\n"
            f"  hullRoot: {{fileID: {HULL_TRANSFORM_ID}}}\n")


_CRUISE = [110.0]


def cruise_speed_placeholder() -> float:
    return _CRUISE[0]


def collider_blocks() -> str:
    def go(fid, tr, comps, layer, name):
        return (f"--- !u!1 &{fid}\n"
                "GameObject:\n"
                "  m_ObjectHideFlags: 0\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                "  serializedVersion: 6\n"
                "  m_Component:\n" + "".join(f"  - component: {{fileID: {c}}}\n" for c in [tr] + comps) +
                f"  m_Layer: {layer}\n"
                f"  m_Name: {name}\n"
                "  m_TagString: Untagged\n"
                "  m_Icon: {fileID: 0}\n"
                "  m_NavMeshLayer: 0\n"
                "  m_StaticEditorFlags: 0\n"
                "  m_IsActive: 1\n")

    def transform(fid, gofid):
        return (f"--- !u!4 &{fid}\n"
                "Transform:\n"
                "  m_ObjectHideFlags: 0\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                f"  m_GameObject: {{fileID: {gofid}}}\n"
                "  serializedVersion: 2\n"
                "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n"
                "  m_LocalPosition: {x: 0, y: 0, z: 0}\n"
                "  m_LocalScale: {x: 1, y: 1, z: 1}\n"
                "  m_ConstrainProportionsScale: 0\n"
                "  m_Children: []\n"
                f"  m_Father: {{fileID: {ROOT_TRANSFORM_ID}}}\n"
                "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n")

    def box(fid, gofid, trigger, size):
        return (f"--- !u!65 &{fid}\n"
                "BoxCollider:\n"
                "  m_ObjectHideFlags: 0\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                f"  m_GameObject: {{fileID: {gofid}}}\n"
                "  m_Material: {fileID: 0}\n"
                "  m_IncludeLayers:\n"
                "    serializedVersion: 2\n"
                "    m_Bits: 0\n"
                "  m_ExcludeLayers:\n"
                "    serializedVersion: 2\n"
                "    m_Bits: 0\n"
                "  m_LayerOverridePriority: 0\n"
                f"  m_IsTrigger: {1 if trigger else 0}\n"
                "  m_ProvidesContacts: 0\n"
                "  m_Enabled: 1\n"
                "  serializedVersion: 3\n"
                f"  m_Size: {v3(*size)}\n"
                f"  m_Center: {v3(*BODY_BOX_CENTER)}\n")

    return (go(BODY_GO_ID, BODY_TRANSFORM_ID, [BODY_COLLIDER_ID, BODY_IMPACT_ID], 0, "ThresherBody") +
            transform(BODY_TRANSFORM_ID, BODY_GO_ID) +
            box(BODY_COLLIDER_ID, BODY_GO_ID, False, BODY_BOX_SIZE) +
            S.mb_header(BODY_IMPACT_ID, BODY_GO_ID, IMPACT_COLLIDER_SCRIPT) +
            f"  impactorObject: {{fileID: {VESSEL_IMPACTOR_ID}}}\n" +
            go(TRIGGER_GO_ID, TRIGGER_TRANSFORM_ID, [TRIGGER_COLLIDER_ID], SHIPS_LAYER, "ThresherHitbox") +
            transform(TRIGGER_TRANSFORM_ID, TRIGGER_GO_ID) +
            box(TRIGGER_COLLIDER_ID, TRIGGER_GO_ID, True, TRIGGER_BOX_SIZE))


def jet_mods(side: float) -> str:
    target = f"    - target: {{fileID: 1007919433217068805, guid: {VESSEL_JET_GUID},\n        type: 3}}\n"
    rows = [("m_LocalScale.x", JET_SCALE[0]), ("m_LocalScale.y", JET_SCALE[1]), ("m_LocalScale.z", JET_SCALE[2]),
            ("m_LocalPosition.x", side), ("m_LocalPosition.y", JET_LOCAL_Y), ("m_LocalPosition.z", JET_LOCAL_Z),
            ("m_LocalRotation.w", 1), ("m_LocalRotation.x", 0), ("m_LocalRotation.y", 0), ("m_LocalRotation.z", 0),
            ("m_LocalEulerAnglesHint.x", 0), ("m_LocalEulerAnglesHint.y", 0), ("m_LocalEulerAnglesHint.z", 0)]
    out = "".join(target + f"      propertyPath: {k}\n      value: {S.num(v)}\n      objectReference: {{fileID: 0}}\n"
                  for k, v in rows)
    return out + (f"    - target: {{fileID: 1007919433217068806, guid: {VESSEL_JET_GUID},\n        type: 3}}\n"
                  "      propertyPath: m_Name\n      value: VesselJet\n      objectReference: {fileID: 0}\n")


def rehome_jet(docs, inst_fid, side):
    chunk = S.get_doc(docs, inst_fid)
    added = re.findall(r"addedObject: \{fileID: (-?\d+)\}", chunk)
    S.remove_docs(docs, added)   # DriftJet: steers the plume SIDEWAYS (the Squirrel's side jets) unless drifting
    chunk = re.sub(r"m_TransformParent: \{fileID: -?\d+\}", f"m_TransformParent: {{fileID: {HULL_TRANSFORM_ID}}}", chunk, count=1)
    chunk = re.sub(r"    m_Modifications:\n(?:.*\n)*?(?=    m_RemovedComponents:)", "    m_Modifications:\n" + jet_mods(side), chunk, count=1)
    chunk = re.sub(r"    m_AddedComponents:\n(?:    - .*\n(?:      .*\n|        .*\n)*)*", "    m_AddedComponents: []\n", chunk, count=1)
    S.set_doc(docs, inst_fid, chunk)


def read_cruise(text: str) -> float:
    m = re.search(r"^  DefaultThrottleScaler: ([-\d.]+)$", text, re.M)
    if not m:
        raise RuntimeError("Thresher.prefab drifted: DefaultThrottleScaler not found on its transformer")
    return float(m.group(1))


def apply_prefab(text: str) -> str:
    """The own-vessel pass on the committed prefab. Idempotent; raises on a drifted anchor."""
    _CRUISE[0] = read_cruise(text)
    head, docs = S.split_docs(text)

    # 1. The hull goes in FIRST (the kept jets are re-parented onto its transform).
    if S.get_doc(docs, HULL_GO_ID) is None:
        _, hull = S.split_docs(hull_blocks())
        at = S.doc_index(docs, ROOT_TRANSFORM_ID) + 1
        docs[at:at] = hull
        S.add_child(docs, ROOT_TRANSFORM_ID, HULL_TRANSFORM_ID)
    else:
        # the procedural era's MeshFilter / MeshRenderer / ThresherHullBuilder go; the GO, its Transform and the
        # model instance are re-authored in place (idempotent)
        S.remove_docs(docs, [HULL_FILTER_ID, HULL_RENDERER_ID, HULL_BUILDER_ID, MODEL_INSTANCE_ID, MODEL_TRANSFORM_ID])
        start, end = S.doc_index(docs, HULL_GO_ID), S.doc_index(docs, HULL_TRANSFORM_ID)
        _, hull = S.split_docs(hull_blocks())
        docs[start:end + 1] = hull

    # 2. The Squirrel model and everything added to it.
    if S.get_doc(docs, SQUIRREL_FBX_INSTANCE) is not None:
        for inst, _, side in JETS_KEPT:
            rehome_jet(docs, inst, side)
        for inst in JETS_DROPPED:
            S.remove_instance(docs, inst)
        fbx = S.get_doc(docs, SQUIRREL_FBX_INSTANCE)
        for _, tr, _ in JETS_KEPT:
            fbx = re.sub(r"    - targetCorrespondingSourceObject: \{[^}]*\}\n      insertIndex: -1\n      addedObject: \{fileID: %s\}\n" % tr, "", fbx)
            fbx = re.sub(r"    - targetCorrespondingSourceObject: \{fileID: -?\d+, guid: \w+,\n        type: 3\}\n      insertIndex: -1\n      addedObject: \{fileID: %s\}\n" % tr, "", fbx)
        S.set_doc(docs, SQUIRREL_FBX_INSTANCE, fbx)
        S.remove_instance(docs, SQUIRREL_FBX_INSTANCE)
        S.remove_child(docs, ROOT_TRANSFORM_ID, SQUIRREL_MODEL_ROOT)

    # 3. The shark's own colliders under the root.
    if S.get_doc(docs, BODY_GO_ID) is None:
        _, cols = S.split_docs(collider_blocks())
        at = S.doc_index(docs, MODEL_TRANSFORM_ID) + 1
        docs[at:at] = cols
        S.add_child(docs, ROOT_TRANSFORM_ID, BODY_TRANSFORM_ID)
        S.add_child(docs, ROOT_TRANSFORM_ID, TRIGGER_TRANSFORM_ID)

    # 4. The paint and the animation.
    cust = S.get_doc(docs, CUSTOMIZATION_ID)
    cust = re.sub(r"(  _shipGeometries:\n)(?:  - \{fileID: -?\d+\}\n)+", r"\g<1>" + f"  - {{fileID: {HULL_GO_ID}}}\n", cust, count=1)
    cust = re.sub(r"  _domainReplacesMaterials:(?: \[\]\n|\n(?:  - \{[^}]*\}\n)+)",
                  f"  _domainReplacesMaterials:\n  - {{fileID: 2100000, guid: {ACCENT_MATERIAL}, type: 2}}\n", cust, count=1)
    S.set_doc(docs, CUSTOMIZATION_ID, cust)
    anim = S.get_doc(docs, ANIMATION_ID)
    if anim is None or (MANTA_ANIMATION_SCRIPT not in anim and ANIMATION_SCRIPT not in anim):
        raise RuntimeError("Thresher.prefab drifted: the root's VesselAnimation is neither the clone's nor the Thresher's")
    S.set_doc(docs, ANIMATION_ID, animation_block())
    return S.join_docs(head, docs)


def apply_fusion(text: str) -> str:
    """The Thresher's four entries: never a bake keyed to the Squirrel model the clone carried. A spent one-shot
    (2026-10-13) that now only stands guard: a bake against the Thresher's own model is left alone."""
    def fix(m):
        return re.sub(r"    bake: \{fileID: 11400000, guid: (%s), type: 2\}\n" % "|".join(SQUIRREL_KEYED_BAKES),
                      "    bake: {fileID: 0}\n", m.group(0), count=1)
    return re.sub(r"  - vessel: %d\n(?:    .*\n)*?(?=  - vessel: |\Z|\S)" % THRESHER_CLASS_ID, fix, text)


def bake_assets():
    """The Squirrel-keyed Thresher bake assets still on disk (by guid: a bake of the model is not one)."""
    out = []
    for f in sorted(os.listdir(FUSION_DIR)):
        meta = os.path.join(FUSION_DIR, f + ".meta")
        if f.startswith("Thresher_") and f.endswith("_HullFusionBake.asset") and os.path.exists(meta):
            if any(f"guid: {g}" in read(meta) for g in SQUIRREL_KEYED_BAKES):
                out.append(os.path.join(FUSION_DIR, f))
    return out


# ----------------------------------------------------------------------------- checks
def check_prefab(text: str):
    errs = []
    if SQUIRREL_FBX_GUID in text:
        errs.append("the Squirrel FBX is still referenced")
    if DRIFT_JET_SCRIPT in text:
        errs.append("a DriftJet (the Squirrel's sideways plume steering) is still on a jet")
    if MANTA_ANIMATION_SCRIPT in text:
        errs.append("the clone's MantaAnimationContoller is still on the root")
    for fid, what in ((HULL_GO_ID, "ThresherHull"), (MODEL_INSTANCE_ID, "the hull model instance"), (BODY_GO_ID, "ThresherBody"),
                      (TRIGGER_GO_ID, "ThresherHitbox"), (BODY_IMPACT_ID, "the body's ImpactCollider")):
        if f"&{fid}\n" not in text and f"&{fid} stripped\n" not in text:
            errs.append(f"{what} is missing")
    docs = S.split_docs(text)[1]
    if f"guid: {HULL_BUILDER_SCRIPT}" in text or any(S.get_doc(docs, f) for f in (HULL_FILTER_ID, HULL_RENDERER_ID, HULL_BUILDER_ID)):
        errs.append("the procedural ThresherHullBuilder (or its MeshFilter / MeshRenderer) is still on the hull - the model replaced it")
    inst = S.get_doc(docs, MODEL_INSTANCE_ID) or ""
    if f"m_SourcePrefab: {{fileID: 100100000, guid: {MODEL_GUID}, type: 3}}" not in inst:
        errs.append("the hull model instance does not source Thresher.fbx")
    if f"m_TransformParent: {{fileID: {HULL_TRANSFORM_ID}}}" not in inst:
        errs.append("the hull model is not parented under ThresherHull")
    hull_tr = S.get_doc(docs, HULL_TRANSFORM_ID) or ""
    if f"  - {{fileID: {MODEL_TRANSFORM_ID}}}\n" not in hull_tr.split("  m_Father")[0]:
        errs.append("the hull model's Transform is not a child of ThresherHull")
    if not os.path.exists(MODEL_META) or f"guid: {MODEL_GUID}\n" not in read(MODEL_META):
        errs.append("Assets/_Models/Vessel Models/Thresher.fbx.meta is missing or does not carry the model guid")
    anim = S.get_doc(S.split_docs(text)[1], ANIMATION_ID)
    if not anim or ANIMATION_SCRIPT not in anim:
        errs.append("the root's VesselAnimation is not ThresherAnimation")
    elif f"  cruiseSpeed: {S.num(read_cruise(text))}\n" not in anim:
        errs.append("ThresherAnimation.cruiseSpeed is not the hull's DefaultThrottleScaler")
    elif f"  hullRoot: {{fileID: {HULL_TRANSFORM_ID}}}\n" not in anim:
        errs.append("ThresherAnimation.hullRoot is not ThresherHull")
    if not re.search(r"  _shipGeometries:\n  - \{fileID: %s\}\n(?!  - )" % HULL_GO_ID, text):
        errs.append("VesselCustomization does not paint the hull (and only the hull)")
    if not re.search(r"  _domainReplacesMaterials:\n  - \{fileID: 2100000, guid: %s, type: 2\}\n(?!  - )" % ACCENT_MATERIAL, text):
        errs.append("VesselCustomization does not name GreenAccentVesselMaterial (the model's Domain slot) as the domain material")
    root_children = re.search(r"--- !u!4 &%s\n(?:.*\n)*?  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)" % ROOT_TRANSFORM_ID, text)
    for fid, what in ((HULL_TRANSFORM_ID, "the hull"), (BODY_TRANSFORM_ID, "the body collider"), (TRIGGER_TRANSFORM_ID, "the hitbox")):
        if not root_children or f"{{fileID: {fid}}}" not in root_children.group(1):
            errs.append(f"{what} is not a child of the root")
    jets_on_hull = sum(1 for _, chunk in docs if f"m_TransformParent: {{fileID: {HULL_TRANSFORM_ID}}}" in chunk
                       and f"m_SourcePrefab: {{fileID: 100100000, guid: {VESSEL_JET_GUID}, type: 3}}" in chunk)
    if jets_on_hull != len(JETS_KEPT):
        errs.append(f"{jets_on_hull} jets ride the hull (want {len(JETS_KEPT)})")
    dangling = S.dangling_refs(text)
    if dangling:
        errs.append(f"{len(dangling)} dangling local reference(s): {dangling[:4]}")
    ids = re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M)
    if len(ids) != len(set(ids)):
        errs.append("duplicate document fileIDs")
    if any(int(i) > 9223372036854775807 for i in ids):
        errs.append("a fileID overflows int64")
    return errs


def check_fusion(text: str):
    errs = []
    entries = re.findall(r"  - vessel: %d\n((?:    .*\n)*?)(?=  - vessel: |\Z|\S)" % THRESHER_CLASS_ID, text)
    if len(entries) != 4:
        errs.append(f"CrystalHullFusionConfig lists {len(entries)} Thresher entries (want 4)")
    for e in entries:
        if any(g in e for g in SQUIRREL_KEYED_BAKES):
            errs.append("a Thresher hull-fusion entry names a bake keyed to the Squirrel model the clone carried")
    return errs


def script_meta_files():
    return {p + ".meta": S.script_meta(guid_for(os.path.basename(p))) for p in NEW_SCRIPTS}


def check_metas():
    errs = []
    for p in NEW_SCRIPTS:
        if not os.path.exists(p):
            errs.append(f"{os.path.relpath(p, ROOT)} is missing")
    for meta, want in script_meta_files().items():
        if not os.path.exists(meta) or read(meta) != want:
            errs.append(f"{os.path.relpath(meta, ROOT)} is missing or differs from its authored guid")
    for p in NEW_SCRIPTS:
        g = guid_for(os.path.basename(p))
        owners = [m for m in walk_metas() if f"guid: {g}\n" in read(m)]
        if len(owners) != 1:
            errs.append(f"guid {g} ({os.path.basename(p)}) is owned by {len(owners)} .meta files")
    return errs


_META_CACHE = []


def walk_metas():
    if not _META_CACHE:
        for d, _, fs in os.walk(os.path.join(ROOT, "Assets")):
            _META_CACHE.extend(os.path.join(d, f) for f in fs if f.endswith(".meta"))
    return _META_CACHE


def read(path):
    with open(path, encoding="utf-8", newline="") as f:
        return f.read().replace("\r\n", "\n")


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def check_all():
    errs = check_prefab(read(PREFAB)) + check_fusion(read(FUSION_CONFIG)) + check_metas()
    if bake_assets():
        errs.append(f"{len(bake_assets())} Squirrel-keyed Thresher bake asset(s) remain: {[os.path.basename(b) for b in bake_assets()]}")
    # Idempotence: re-applying the pass to the shipped files changes nothing.
    if apply_prefab(read(PREFAB)) != read(PREFAB):
        errs.append("Thresher.prefab differs from a re-run of the pass (hand edit or drift)")
    if apply_fusion(read(FUSION_CONFIG)) != read(FUSION_CONFIG):
        errs.append("CrystalHullFusionConfig differs from a re-run of the pass")
    return errs


def self_test():
    shipped = read(PREFAB)
    controls = (
        ("re-add the Squirrel FBX guid", shipped.replace(f"guid: {ACCENT_MATERIAL}", f"guid: {SQUIRREL_FBX_GUID}", 1), "Squirrel FBX"),
        ("drop the hull model", shipped.replace(f"&{MODEL_INSTANCE_ID}\n", "&1\n", 1), "hull model instance is missing"),
        ("put the procedural builder back", shipped.replace(f"guid: {ACCENT_MATERIAL}", f"guid: {HULL_BUILDER_SCRIPT}", 1), "ThresherHullBuilder"),
        ("dangle a reference", shipped.replace(f"impactorObject: {{fileID: {VESSEL_IMPACTOR_ID}}}", "impactorObject: {fileID: 42}", 1), "dangling"),
        ("paint the wrong object", shipped.replace(f"  _shipGeometries:\n  - {{fileID: {HULL_GO_ID}}}", f"  _shipGeometries:\n  - {{fileID: {BODY_GO_ID}}}", 1), "paint"),
        ("put the clone's animation back", shipped.replace(ANIMATION_SCRIPT, MANTA_ANIMATION_SCRIPT, 1), "MantaAnimationContoller"),
    )
    fails = 0
    for what, text, needle in controls:
        found = [e for e in check_prefab(text) if needle in e]
        print(("  ok   " if found else "  FAIL ") + f"negative control: {what} -> {found[:1] or 'NOT CAUGHT'}")
        fails += 0 if found else 1
    bad_fusion = read(FUSION_CONFIG)
    bad_fusion = re.sub(r"(  - vessel: 14\n(?:    .*\n)*?    )bake: \{fileID: 0\}", r"\1bake: {fileID: 11400000, guid: eb63e1fdbcaca2a4da87e864d4939cb2, type: 2}", bad_fusion, count=1)
    found = check_fusion(bad_fusion)
    print(("  ok   " if found else "  FAIL ") + f"negative control: a Thresher fusion entry on a Squirrel-keyed bake -> {found[:1] or 'NOT CAUGHT'}")
    fails += 0 if found else 1
    print("author_thresher_assets --self-test: " + ("OK" if not fails else f"{fails} control(s) NOT caught"))
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        before = read(PREFAB)
        after = apply_prefab(before)
        if after != before:
            write(PREFAB, after)
            print("wrote Assets/_Prefabs/Spacevessels/Thresher.prefab")
        before = read(FUSION_CONFIG)
        after = apply_fusion(before)
        if after != before:
            write(FUSION_CONFIG, after)
            print("wrote Assets/Resources/CrystalHullFusionConfig.asset")
        for b in bake_assets():
            os.remove(b)
            if os.path.exists(b + ".meta"):
                os.remove(b + ".meta")
            print(f"deleted {os.path.relpath(b, ROOT)} (+ .meta)")
        for meta, text in script_meta_files().items():
            if not os.path.exists(meta) or read(meta) != text:
                write(meta, text)
                print(f"wrote {os.path.relpath(meta, ROOT)}")
        _META_CACHE.clear()
    errs = check_all()
    for e in errs:
        print(f"FAIL: {e}")
    print("author_thresher_assets: " + ("OK" if not errs else f"{len(errs)} finding(s)"))
    return 0 if not errs else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
