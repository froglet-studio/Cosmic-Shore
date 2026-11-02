#!/usr/bin/env python3
"""Build a celestial fauna BODY from its spec (the /fauna-creation skill, section 4 step 4; section 7.4's generic builder).

A spec (Tools/Build/fauna_kit/specs/<species>.py) in, Unity prefab YAML documents out:

* every SPINDLE is a plain GameObject carrying a `Spindle`, posed at the spec's root (`from`) with its local +z pointing
  from its tip back to its root (the braid trails along its own -z), parented to its PARENT spindle (or to the creature's
  root): the spindle tree is the transform tree, so every limb keeps a path to the Etherion and an ordered wither can read
  it. Its skin is a child `Braid` GameObject drawing the shipped braid mesh (`bonita.fbx` `BezierCurve.001`, the mesh every
  living spindle draws, Docs/SPINDLE_LOD.md section 1) with the shared `SpindleMaterial`, stretched ALONG ITS LENGTH ONLY
  so the braid spans the spec's from -> to exactly (front of the mesh at the root, tip at the end), at the spec's `girth`
  across (default 1: the shipped Branch's). "A wire is judged by its thickness, never by its length" (SPINDLE_LOD.md): the
  shipped Branch (1, 1, 6.2) and TadpoleSpindle (1.5, 1.5, 5) stretch the same way, and the Space tadpole erodes such a
  braid. A uniform scale (the first build) made every long limb a fat ribbon.
* every PRISM is a nested instance of `DynamicHealthBlock.prefab` (health) or `DangerBlock.prefab` (danger), a DIRECT child
  of its spindle's GameObject (so `HealthPrism.Initialize` finds its limb and it sways with it), at the spec's centre, its
  local z along the spec's axis, its local y RADIAL (celestial.oriented: n copies round the axis are exact copies), its
  `m_LocalScale` the spec's size (the authored size `Fauna.SeatAuthoredSize` admits after Initialize).

The species generator adds the rest (the root, the heart, the behaviour class). `read_back` reconstructs every part's
world pose from a prefab's text, so a generator's `--check` can hold the prefab to the spec rather than to its own output.
"""
import hashlib
import math
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(os.path.dirname(HERE)))   # Tools/Build/fauna_kit -> the repo
sys.path.insert(0, HERE)
import celestial as C  # noqa: E402

SPINDLE_SCRIPT = "8ec45d1233573f9409107a25ab23b0c9"       # Spindle.cs
BRAID_MESH = "{fileID: 1825875415147931077, guid: c9a0b575455114844a1b087dd889626e, type: 3}"   # bonita.fbx BezierCurve.001
SPINDLE_MATERIAL = "4f44fa5c7514a2c45b5af7f45bc51acd"     # SpindleMaterial.mat (the tadpole's braid wears it)
# the braid mesh in Unity units (bonita.fbx, UnitScaleFactor 1 -> 1/100): its root at the mesh origin, a stub ahead of it
BRAID_FRONT, BRAID_TIP = 0.5527, -1.9666
BRAID_SPAN = BRAID_FRONT - BRAID_TIP

PRISMS = {   # the two prism prefabs and the fileIDs inside them a nested instance overrides (they share one base)
    "health": dict(guid="4a31e01f8b69a584ca245a1687beb4c2", hp=6313579230210663873),    # Trails/DynamicHealthBlock.prefab
    "danger": dict(guid="aad956930fb933f4b82b992b1bc2b773", hp=5825274168078934928),    # Trails/DangerBlock.prefab
}
PRISM_TR, PRISM_GO, PRISM_SCALER = 5222650486365209692, 5776304996075792891, 6474806432604849291
HEALTH_PRISM_SCRIPT = "2f76793d2a031124f900d060d1bbb2bb"  # HealthPrism.cs

RENDERER = """--- !u!23 &{rid}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
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
  m_ForceMeshLod: -1
  m_MeshLodSelectionBias: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {mat}, type: 2}}
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
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
"""


# ------------------------------------------------------------------ rotations (x, y, z, w), Unity's left-handed frame

def quat_from_axes(r, u, f):
    """The rotation whose local x, y, z are r, u, f."""
    m00, m01, m02 = r[0], u[0], f[0]
    m10, m11, m12 = r[1], u[1], f[1]
    m20, m21, m22 = r[2], u[2], f[2]
    tr = m00 + m11 + m22
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2
        return ((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s)
    if m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2
        return (0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s)
    if m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2
        return ((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s)
    s = math.sqrt(1.0 + m22 - m00 - m11) * 2
    return ((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s)


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by, aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw, aw * bw - ax * bx - ay * by - az * bz)


def qinv(q):
    return (-q[0], -q[1], -q[2], q[3])


def qrot(q, v):
    x, y, z, w = q
    p = (v[0], v[1], v[2], 0.0)
    r = qmul(qmul(q, p), qinv(q))
    return [r[0], r[1], r[2]]


def canonical(q):
    return tuple(-c for c in q) if q[3] < 0 else tuple(q)


# ------------------------------------------------------------------ ids and numbers

def make_ids(species):
    def fid(name):
        return int(hashlib.md5(f"cosmicshore-fauna-kit:{species}:{name}".encode()).hexdigest()[:15], 16)
    return fid


def stripped(inst, src):
    return (inst ^ src) & 0x7FFFFFFFFFFFFFFF


def num(x):
    x = round(float(x), 6)
    if x == 0:
        return "0"
    return ("%.6f" % x).rstrip("0").rstrip(".")


def vec(v):
    return "{x: %s, y: %s, z: %s}" % tuple(num(c) for c in v)


def quat(q):
    return "{x: %s, y: %s, z: %s, w: %s}" % tuple(num(c) for c in q)


# ------------------------------------------------------------------ the poses

def spindle_world(s):
    r, u, f = C.spindle_frame(s)
    return list(s["from"]), canonical(quat_from_axes(r, u, f))


def prism_world(p):
    r, u, f = C.prism_frame(p)
    return list(p["at"]), canonical(quat_from_axes(r, u, f))


def braid_scale(s):
    """(across, along): the braid keeps the spec's girth across and is stretched along its length to span the limb."""
    return float(s.get("girth", 1.0)), C.norm(C.sub(s["to"], s["from"])) / BRAID_SPAN


def local_pose(parent_pose, world_pose):
    (pp, pq), (wp, wq) = parent_pose, world_pose
    inv = qinv(pq)
    return qrot(inv, C.sub(wp, pp)), canonical(qmul(inv, wq))


# ------------------------------------------------------------------ the documents

def build_body(spec, root_tr, ids=None, root_pose=((0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0))):
    """The body's YAML documents. Returns (docs, root_children, parts): root_children are the transform fileIDs to list
    under the creature's root; parts maps each spec name to its fileIDs (spindles: go/tr/mb/braid_rend; prisms:
    inst/tr/go/hp, the last three STRIPPED ids a referrer uses)."""
    ids = ids or make_ids(spec["species"])
    spindles = {s["name"]: s for s in spec["spindles"]}
    parts, docs, root_children = {}, [], []
    for s in spec["spindles"]:
        n = s["name"]
        parts[n] = dict(go=ids(f"{n}/go"), tr=ids(f"{n}/tr"), mb=ids(f"{n}/spindle"),
                        braid_go=ids(f"{n}/braid-go"), braid_tr=ids(f"{n}/braid-tr"),
                        braid_mf=ids(f"{n}/braid-mf"), braid_rend=ids(f"{n}/braid-renderer"))
    for p in spec["prisms"]:
        n, kind = p["name"], PRISMS[p["kind"]]
        inst = ids(f"{n}/instance")
        parts[n] = dict(inst=inst, tr=stripped(inst, PRISM_TR), go=stripped(inst, PRISM_GO), hp=stripped(inst, kind["hp"]))

    for s in spec["spindles"]:
        n, P = s["name"], parts[s["name"]]
        parent = s.get("parent")
        parent_pose = spindle_world(spindles[parent]) if parent else root_pose
        pos, rot = local_pose(parent_pose, spindle_world(s))
        children = [P["braid_tr"]] + [parts[c["name"]]["tr"] for c in spec["spindles"] if c.get("parent") == n] + \
                   [parts[p["name"]]["tr"] for p in spec["prisms"] if p["spindle"] == n]
        father = parts[parent]["tr"] if parent else root_tr
        g, sc = braid_scale(s)
        docs.append(
            f"--- !u!1 &{P['go']}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n"
            f"  - component: {{fileID: {P['tr']}}}\n  - component: {{fileID: {P['mb']}}}\n  m_Layer: 0\n  m_Name: {n}\n"
            f"  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
            f"--- !u!4 &{P['tr']}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {P['go']}}}\n"
            f"  serializedVersion: 2\n  m_LocalRotation: {quat(rot)}\n  m_LocalPosition: {vec(pos)}\n"
            f"  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n"
            + "".join(f"  - {{fileID: {c}}}\n" for c in children) +
            f"  m_Father: {{fileID: {father}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
            f"--- !u!114 &{P['mb']}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {P['go']}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {SPINDLE_SCRIPT}, type: 3}}\n"
            f"  m_Name: \n  m_EditorClassIdentifier: \n  RenderedObject: {{fileID: {P['braid_rend']}}}\n"
            f"  additionalRenderedObjects: []\n  parentSpindle: {{fileID: {parts[parent]['mb'] if parent else 0}}}\n  LifeForm: {{fileID: 0}}\n"
            f"  retainSpindle: 0\n  permanentWither: 1\n"
            f"--- !u!1 &{P['braid_go']}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n"
            f"  - component: {{fileID: {P['braid_tr']}}}\n  - component: {{fileID: {P['braid_mf']}}}\n"
            f"  - component: {{fileID: {P['braid_rend']}}}\n  m_Layer: 0\n  m_Name: Braid\n  m_TagString: Untagged\n"
            f"  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
            f"--- !u!4 &{P['braid_tr']}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {P['braid_go']}}}\n"
            f"  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n"
            f"  m_LocalPosition: {vec([0, 0, -BRAID_FRONT * sc])}\n  m_LocalScale: {vec([g, g, sc])}\n"
            f"  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {{fileID: {P['tr']}}}\n"
            f"  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
            f"--- !u!33 &{P['braid_mf']}\nMeshFilter:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {P['braid_go']}}}\n"
            f"  m_Mesh: {BRAID_MESH}\n"
            + RENDERER.format(rid=P["braid_rend"], go=P["braid_go"], mat=SPINDLE_MATERIAL))
        if not parent:
            root_children.append(P["tr"])

    for p in spec["prisms"]:
        n, kind, P = p["name"], PRISMS[p["kind"]], parts[p["name"]]
        S = parts[p["spindle"]]
        pos, rot = local_pose(spindle_world(spindles[p["spindle"]]), prism_world(p))
        g = kind["guid"]

        def mod(target, prop, value, ref="{fileID: 0}"):
            return (f"    - target: {{fileID: {target}, guid: {g},\n        type: 3}}\n      propertyPath: {prop}\n"
                    f"      value: {value}\n      objectReference: {ref}\n")
        mods = "".join(mod(PRISM_TR, f"m_LocalPosition.{a}", num(v)) for a, v in zip("xyz", pos))
        mods += "".join(mod(PRISM_TR, f"m_LocalRotation.{a}", num(v)) for a, v in zip("xyzw", rot))
        mods += "".join(mod(PRISM_TR, f"m_LocalScale.{a}", num(v)) for a, v in zip("xyz", p["size"]))
        mods += "".join(mod(PRISM_TR, f"m_LocalEulerAnglesHint.{a}", "0") for a in "xyz")
        mods += mod(PRISM_GO, "m_Name", n)
        mods += mod(kind["hp"], "spindle", "", f"{{fileID: {S['mb']}}}")
        mods += mod(kind["hp"], "LifeForm", "", "{fileID: 0}")
        mods += mod(PRISM_SCALER, "usePrefabScaleAsDefaultTarget", "1")
        docs.append(
            f"--- !u!1001 &{P['inst']}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n"
            f"    serializedVersion: 3\n    m_TransformParent: {{fileID: {S['tr']}}}\n    m_Modifications:\n{mods}"
            f"    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
            f"  m_SourcePrefab: {{fileID: 100100000, guid: {g}, type: 3}}\n"
            f"--- !u!4 &{P['tr']} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {PRISM_TR}, guid: {g},\n"
            f"    type: 3}}\n  m_PrefabInstance: {{fileID: {P['inst']}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
            f"--- !u!1 &{P['go']} stripped\nGameObject:\n  m_CorrespondingSourceObject: {{fileID: {PRISM_GO}, guid: {g},\n"
            f"    type: 3}}\n  m_PrefabInstance: {{fileID: {P['inst']}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
            f"--- !u!114 &{P['hp']} stripped\nMonoBehaviour:\n  m_CorrespondingSourceObject: {{fileID: {kind['hp']}, guid: {g},\n"
            f"    type: 3}}\n  m_PrefabInstance: {{fileID: {P['inst']}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
            f"  m_GameObject: {{fileID: {P['go']}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {HEALTH_PRISM_SCRIPT}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n")
    return "".join(docs), root_children, parts


# ------------------------------------------------------------------ reading a built body back

def _vec_of(text, key):
    m = re.search(r"%s: \{x: (\S+), y: (\S+), z: (\S+?)(?:, w: (\S+?))?\}" % key, text)
    if not m:
        return None
    return tuple(float(v) for v in m.groups() if v is not None)


def read_back(prefab_text, spec, root_pose=((0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0))):
    """Every spec part's WORLD pose as the prefab poses it (from its transforms and its prism instances' overrides), and
    the problems: a part missing, a prism not a direct child of its spindle, a pose or size off the spec by > 1e-3."""
    docs = {}
    for d in re.split(r"(?m)^(?=--- !u!)", prefab_text):
        m = re.match(r"--- !u!(\d+) &(-?\d+)", d)
        if m:
            docs[m.group(2)] = d
    names = {}
    for fid, d in docs.items():
        if d.startswith("--- !u!1 &") and "\nGameObject:" in d:
            m = re.search(r"  m_Name: (\S+)\n", d)
            tr = re.search(r"  - component: \{fileID: (-?\d+)\}", d)
            if m and tr:
                names[m.group(1)] = tr.group(1)
    problems, world = [], {}

    def pose_of(tr_id):
        d = docs.get(tr_id, "")
        pos, rot = _vec_of(d, "m_LocalPosition"), _vec_of(d, "m_LocalRotation")
        father = re.search(r"  m_Father: \{fileID: (-?\d+)\}", d)
        local = (list(pos or (0, 0, 0)), tuple(rot or (0, 0, 0, 1)))
        if not father or father.group(1) not in docs:
            parent = root_pose
        else:
            fd = docs[father.group(1)]
            parent = root_pose if re.search(r"  m_Father: \{fileID: 0\}", fd) else pose_of(father.group(1))
        return C.add(list(parent[0]), qrot(parent[1], local[0])), canonical(qmul(parent[1], local[1]))

    for s in spec["spindles"]:
        tr = names.get(s["name"])
        if not tr:
            problems.append(f"spindle '{s['name']}' is not in the prefab")
            continue
        world[s["name"]] = pose_of(tr)
        want = spindle_world(s)
        if C.norm(C.sub(world[s["name"]][0], want[0])) > 1e-3:
            problems.append(f"spindle '{s['name']}' sits at {[round(v, 3) for v in world[s['name']][0]]}, the spec says {s['from']}")
        if s.get("parent"):
            father = re.search(r"  m_Father: \{fileID: (-?\d+)\}", docs[tr]).group(1)
            if father != names.get(s["parent"]):
                problems.append(f"spindle '{s['name']}' is not a child of its parent '{s['parent']}' (the path to the heart)")
    for p in spec["prisms"]:
        inst = None
        for fid, d in docs.items():
            if d.startswith(f"--- !u!1001 &{fid}\n") and re.search(r"propertyPath: m_Name\n      value: %s\n" % re.escape(p["name"]), d):
                inst = d
        if not inst:
            problems.append(f"prism '{p['name']}' is not in the prefab")
            continue
        parent_tr = re.search(r"    m_TransformParent: \{fileID: (-?\d+)\}", inst).group(1)
        if parent_tr != names.get(p["spindle"]):
            problems.append(f"prism '{p['name']}' is not a direct child of its spindle '{p['spindle']}'")
            continue

        def ov(prop):
            m = re.search(r"propertyPath: %s\n      value: (\S+)\n" % re.escape(prop), inst)
            return float(m.group(1)) if m else 0.0
        local = ([ov(f"m_LocalPosition.{a}") for a in "xyz"], tuple(ov(f"m_LocalRotation.{a}") for a in "xyzw"))
        size = [ov(f"m_LocalScale.{a}") for a in "xyz"]
        sp = world.get(p["spindle"]) or pose_of(parent_tr)
        at = C.add(list(sp[0]), qrot(sp[1], local[0]))
        world[p["name"]] = (at, canonical(qmul(sp[1], local[1])))
        if C.norm(C.sub(at, p["at"])) > 1e-3:
            problems.append(f"prism '{p['name']}' sits at {[round(v, 3) for v in at]}, the spec says {p['at']}")
        if any(abs(a - b) > 1e-3 for a, b in zip(size, p["size"])):
            problems.append(f"prism '{p['name']}' is {size}, the spec says {p['size']}")
        if ("DangerBlock" if p["kind"] == "danger" else "health") and PRISMS[p["kind"]]["guid"] not in inst:
            problems.append(f"prism '{p['name']}' is not a {p['kind']} prism")
    return world, problems
