#!/usr/bin/env python3
"""Author THE BEES (Garrett, 2026-10-31; Docs/ECOSYSTEM.md §66) - every asset, from one model.

    python3 Tools/Build/author_bee_assets.py            # write
    python3 Tools/Build/author_bee_assets.py --check    # FAIL on any drift (reads the disk)
    python3 Tools/Build/author_bee_assets.py --self-test

The bees are the first POLLINATOR (FaunaDiet.Pollinator): a fauna whose effect is productive. Their population is
raised by a HIVE, which is a FLORA (BeeHiveFlora) - so a cell grows bees by listing a hive in its spawn profile's
SupportedFloras, and they count against the flora budget, not the fauna one.

What it owns (one owner per file):
  * the .meta of every script in FloraAndFauna/Bees, of that folder and of Tests/Editor/BeeMathTests.cs;
  * Assets/_Prefabs/FloraAndFauna/Bees/BeeFauna.prefab - a fork of TadPoleFauna.prefab: the root script swapped to
    BeeFauna (a Boid that never forages), diet Pollinator, no starvation, no collision effects, a fresh Netcode
    hash, the tadpole's loop sound emptied, BeeWingDriver added, and two more TadpoleSpindle instances as the WINGS
    (their spindle skin hidden, each carrying one thin HealthPrism the driver flaps about its root). The tadpole's
    own body instance is the abdomen; its crystal is the heart, at the front, the body trailing (§23.9);
  * Assets/_Prefabs/FloraAndFauna/Bees/BeeHive.prefab - a fork of BorromeanFlora.prefab with the root script
    swapped to BeeHiveFlora, the Borromean-only fields replaced by the comb's, and the four bee configs wired;
  * _SO_Assets/Lifeforms/Bee Fauna <Element>.asset (the four bee species, one per element) and
    _SO_Assets/Lifeforms/Bee Hive Flora <Element>.asset (the four hive species: the element palette and the
    Spawn Matrix bench's source);
  * the Hesperides garden's hive: Cell Configs/Hesperides Cell/Hesperides Bee Hive Flora Config Data.asset (the four
    elements spread from the palette). The garden's spawn profile is Tools/ecosim/gen_hesperides_assets.py's: it lists
    this config by the guid minted here, and --check fails if the shipped profile does not;
  * the bee prefab's entry in Assets/DefaultNetworkPrefabs.asset (a hatched bee is a networked fauna);
  * Assets/_Models/Fauna/Bee.fbx.meta (the model's import settings and its material remap: BeeBody -> the shared
    SpindleMaterial, BeeWing -> BranchingMembraneMaterial) and the model's place in the bee (round 2, Garrett 2026-11-01:
    "make a real bee model with /fauna"): a `BeeModel` GameObject carrying a Spindle with the Bee.fbx instance under it
    (the Clawfish's pattern: the spindle resolves the model's body renderer itself). The FBX is built by
    Tools/Build/bee_model/build_bee.py (Blender-as-a-module); this script checks it is there and carries the parts the
    prefab and BeeWingDriver name. The tadpole skin of the abdomen instance is hidden: the model is the body, and the
    abdomen's prism (the bee's conserved mass) sits inside the model's abdomen, the two "wing" prisms are the forewings'
    leading-edge veins.

It does NOT write the Spawn Matrix toy (author_spawn_matrix_roster.py owns it and lists "Bee Hive") or the flora
populations of other species (author_flora_populations.py hands "Bee Hive" to this script by name).
"""
import argparse
import hashlib
import os
import re
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
A = lambda *p: os.path.join(REPO, "Assets", *p)
SCRIPT_DIR = A("_Scripts", "Controller", "Environment", "FloraAndFauna", "Bees")
SCRIPTS = ["BeeMath", "BeeHiveFlora", "BeeFauna", "BeeWingDriver"]
PREFAB_DIR = A("_Prefabs", "FloraAndFauna", "Bees")
LIFEFORMS = A("_SO_Assets", "Lifeforms")
HESP_DIR = A("_SO_Assets", "Cell Configs", "Hesperides Cell")
HESP_PROFILE = os.path.join(HESP_DIR, "Hesperides Cell Spawn Profile.asset")
HESP_HIVE = "Hesperides Bee Hive Flora Config Data"
NETWORK = A("DefaultNetworkPrefabs.asset")
TEST = A("_Scripts", "Tests", "Editor", "BeeMathTests.cs")

TADPOLE = A("_Prefabs", "FloraAndFauna", "TadPoleFauna.prefab")
MODEL = A("_Models", "Fauna", "Bee.fbx")
MODEL_ROOT_TR = -8679921383154817045                      # an FBX's root transform (the same id in every model)
MODEL_ROOT_GO = 919132149155446097                        # an FBX's root GameObject
MODEL_PARTS = ("Bee_Body", "Bee_WingFL", "Bee_WingFR", "Bee_WingHL", "Bee_WingHR")
SPINDLE_SCRIPT = "8ec45d1233573f9409107a25ab23b0c9"       # Spindle.cs
SPINDLE_MATERIAL = "4f44fa5c7514a2c45b5af7f45bc51acd"     # SpindleMaterial.mat (the Clawfish's body takes it too)
MEMBRANE_MATERIAL = "BranchingMembraneMaterial"           # resolved from its .meta
BOID_SCRIPT = "19b38987ca9e2974083abe50717ed186"          # Boid.cs
TADPOLE_ROOT_GO = "5119337659833296837"
TADPOLE_ROOT_TR = "5494491424606190975"
TADPOLE_ROOT_MB = "5945480239701989318"                   # the Boid (what a FaunaConfigurationSO points at)
TADPOLE_NETOBJ = "1065634054817553135"
TADPOLE_EMITTER = "6009907184062220078"
BODY_INST = "4754995950771878307"                         # the TadpoleSpindle instance (the abdomen)
SPINDLE_PREFAB = "32182c0d19e344b4c9a9b332052037e6"       # Spindles/TadpoleSpindle.prefab
SP_ROOT_TR = 4897651091342273877                          # its root transform (the wing's pivot)
SP_GO_NAMED = 5753173635696888826                         # its root GameObject (named)
SP_PRISM_GO = 744194691747103861                          # the HealthPrism's GameObject
SP_PRISM_TR = 1297303983159372242                         # the HealthPrism's transform
SP_PRISM_MB = 1764503489717778941                         # the HealthPrism
SP_RENDERER = 4055802238885949714                         # the spindle skin's renderer

BORROMEAN = A("_Prefabs", "FloraAndFauna", "BorromeanFlora.prefab")
BORROMEAN_SCRIPT = "51d471a2f48426e98a4d4a605b2e6803"
FLORA_ROOT_MB = "8186157953239024492"
FLORA_CONFIG_SCRIPT = "a32a297a7606432885f4d3e1f83bea9a"   # FloraConfigurationSO.cs
FAUNA_CONFIG_SCRIPT = "c778cfbe4dfc4c5c8401e40c17802311"   # FaunaConfigurationSO.cs
ELEMENTS = [("Charge", 1), ("Mass", 2), ("Space", 3), ("Time", 4)]

# ── the model (every number the assets carry; the C# defaults of BeeHiveFlora/BeeFauna say the same) ─────────────
COMB = dict(cellLeaf=(2.4, 2.4, 6.0), cellSpacing=4.2, combSpacing=7.0, cellsPerComb=19, seedCells=12, maxCells=57)
COLONY = dict(minBees=3, cellsPerBee=9, maxBees=8, hatchPeriodScale=0.25, hatchRadius=12, guardRadius=260,
              forageRadius=420, senseSeconds=0.5)
HIVE_GROW_PERIOD = 2
BEE_SPEED = (14, 26)          # min / max (u/s): quicker than a tadpole (10 / 15)
BEE_BEHAVIOUR_RATE = 0.6      # s between flock steering ticks (a tadpole's 1.5)
BEE_BODY_SCALE = 3            # the root, uniform (the body is scaled to the one heart size, §64.4; the tadpole's 3)
# the abdomen's prism sits at the model abdomen's widest point (build_bee.ABDOMEN: start -3.95, end -8.2, bulge at 0.42)
ABDOMEN = dict(target=(2.0, 2.0, 2.8), z=-5.7)
# each "wing" prism is its forewing's leading-edge vein: on the model forewing's pivot (build_bee.FOREWING.pivot, in
# Unity's frame), along its span, the same dihedral (build_bee.FOREWING.dihedral_deg)
WING = dict(target=(3.9, 0.22, 0.28), pivot=(1.4, 1.1, -1.2), reach=2.15, tilt_deg=9)
HESPERIDES_HIVES = dict(initial=2, cap=4)
BENCH_HIVE_CAP = 3


def guid(name):
    return hashlib.md5(("cosmicshore-bees:" + name).encode()).hexdigest()


def rel(path):
    return os.path.relpath(path, REPO).replace(os.sep, "/")


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def file_guid(path):
    return re.search(r"^guid: (\w+)", read(path + ".meta"), re.M).group(1)


def fid(name):
    """A stable 63-bit local fileID for a new object in a prefab."""
    return int(hashlib.md5(("cosmicshore-bees-fid:" + name).encode()).hexdigest()[:15], 16)


FOLDER_META = ("fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
SCRIPT_META = ("fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
               "  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")
PREFAB_META = ("fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n")
ASSET_META = ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
              "  mainObjectFileID: 11400000\n  mainObjectFileType: 2\n  userData:\n  assetBundleName:\n"
              "  assetBundleVariant:\n")
SO_HEADER = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: %s
  m_EditorClassIdentifier:
"""


def script_guid(name):
    return guid(f"{rel(SCRIPT_DIR)}/{name}.cs")


def prefab_path(name):
    return os.path.join(PREFAB_DIR, name + ".prefab")


def bee_cfg_path(e):
    return os.path.join(LIFEFORMS, f"Bee Fauna {e}.asset")


def hive_cfg_path(e):
    return os.path.join(LIFEFORMS, f"Bee Hive Flora {e}.asset")


def hesp_hive_path():
    return os.path.join(HESP_DIR, HESP_HIVE + ".asset")


def _g(x):
    return ("%.4f" % x).rstrip("0").rstrip(".") if isinstance(x, float) else str(x)


def _v(t):
    return "{x: %s, y: %s, z: %s}" % tuple(_g(float(c)) for c in t)


def armour_period():
    """ElementalArmour.ShieldPeriod, read through author_flora_armour.py (the law's one reader)."""
    sys.path.insert(0, HERE)
    import author_flora_armour as armour
    return armour.armour_period()


def netcode_hash(prefab_guid, component_fid):
    sys.path.insert(0, HERE)
    import author_stoat_assets as stoat   # the one xxhash32 / GlobalObjectId_V1 implementation (self-tested there)
    return stoat.global_object_id_hash(prefab_guid, component_fid)


def replace_once(text, old, new, what):
    if text.count(old) != 1:
        raise SystemExit(f"donor drifted: {what} ({text.count(old)} matches for {old[:60]!r})")
    return text.replace(old, new)


# ── the bee prefab ──────────────────────────────────────────────────────────────────────────────────────────────

def instance_block(t):
    """The body TadpoleSpindle instance and every document that belongs to it (stripped + added), in order."""
    docs = re.split(r"(?m)^(?=--- !u!)", t)
    out = []
    for d in docs:
        if d.startswith(f"--- !u!1001 &{BODY_INST}\n") or f"m_PrefabInstance: {{fileID: {BODY_INST}}}" in d:
            out.append(d)
    added = re.findall(r"addedObject: \{fileID: (\d+)\}", out[0])
    for d in docs:
        if any(d.startswith(f"--- !u!114 &{a}\n") for a in added):
            out.append(d)
    return out, added


def set_mod(block, target, prop, value):
    """Set (or append) one m_Modifications entry of a TadpoleSpindle instance."""
    pat = re.compile(r"(    - target: \{fileID: %d, guid: %s,\n        type: 3\}\n      propertyPath: %s\n      value: )[^\n]*\n"
                     % (target, SPINDLE_PREFAB, re.escape(prop)))
    if pat.search(block):
        return pat.sub(lambda m: m.group(1) + value + "\n", block, count=1)
    entry = (f"    - target: {{fileID: {target}, guid: {SPINDLE_PREFAB},\n        type: 3}}\n"
             f"      propertyPath: {prop}\n      value: {value}\n      objectReference: {{fileID: 0}}\n")
    return block.replace("    m_RemovedComponents:", entry + "    m_RemovedComponents:", 1)


def wing_docs(body_docs, added, side):
    """A copy of the body instance as one wing: new ids, its pivot posed beside the thorax, the skin hidden."""
    inst = fid(f"wing-{side}-instance")
    mapping = {BODY_INST: str(inst)}
    for src in (SP_ROOT_TR, SP_PRISM_GO, SP_PRISM_MB):
        old = str((int(BODY_INST) ^ src) & 0x7FFFFFFFFFFFFFFF)
        mapping[old] = str((inst ^ src) & 0x7FFFFFFFFFFFFFFF)
    for a in added:
        mapping[a] = str(fid(f"wing-{side}-added-{a}"))
    text = "".join(body_docs)
    for old, new in mapping.items():
        text = re.sub(r"(?<!\d)%s(?!\d)" % old, new, text)
    sign = -1 if side == "L" else 1
    px, py, pz = WING["pivot"]
    text = set_mod(text, SP_GO_NAMED, "m_Name", f"BeeWing{side}")
    text = set_mod(text, SP_ROOT_TR, "m_LocalPosition.x", _g(sign * px))
    text = set_mod(text, SP_ROOT_TR, "m_LocalPosition.y", _g(py))
    text = set_mod(text, SP_ROOT_TR, "m_LocalPosition.z", _g(pz))
    # a dihedral: each wing is tilted up about the body's axis
    import math
    half = math.radians(WING["tilt_deg"]) / 2
    text = set_mod(text, SP_ROOT_TR, "m_LocalRotation.z", _g(round(sign * math.sin(half), 6)))   # up (round 1 had it down)
    text = set_mod(text, SP_ROOT_TR, "m_LocalRotation.w", _g(round(math.cos(half), 6)))
    text = set_mod(text, SP_PRISM_TR, "m_LocalPosition.x", _g(sign * WING["reach"]))
    text = set_mod(text, SP_PRISM_TR, "m_LocalPosition.z", "0")
    text = set_mod(text, SP_PRISM_TR, "m_LocalScale.x", "1")
    text = set_mod(text, SP_PRISM_TR, "m_LocalScale.y", "1")
    text = set_mod(text, SP_PRISM_TR, "m_LocalScale.z", "1")
    for axis, v in zip("xyz", WING["target"]):
        text = set_mod(text, SP_PRISM_MB, f"TargetScale.{axis}", _g(v))
    text = set_mod(text, SP_RENDERER, "m_Enabled", "0")     # a wing is its prism; the tadpole skin is not drawn
    return text, str((inst ^ SP_ROOT_TR) & 0x7FFFFFFFFFFFFFFF)


def bee_prefab():
    t = read(TADPOLE)
    pg = guid(rel(prefab_path("BeeFauna")))
    body_docs, added = instance_block(t)
    if len(body_docs) < 5 or len(added) != 2:
        raise SystemExit("TadPoleFauna.prefab: the body instance this script copies for the wings has moved")
    # the abdomen: the tadpole's own body, re-sized
    body = body_docs[0]   # the instance document holds every modification
    nb = body
    for axis, v in zip("xyz", ABDOMEN["target"]):
        nb = set_mod(nb, SP_PRISM_MB, f"TargetScale.{axis}", _g(v))
    nb = set_mod(nb, SP_PRISM_TR, "m_LocalPosition.z", _g(ABDOMEN["z"]))
    for axis, v in zip("xyz", ABDOMEN["target"]):
        nb = set_mod(nb, SP_PRISM_TR, f"m_LocalScale.{axis}", _g(v))
    nb = set_mod(nb, SP_GO_NAMED, "m_Name", "BeeAbdomen")
    nb = set_mod(nb, SP_RENDERER, "m_Enabled", "0")         # the model is the body; the tadpole skin is not drawn
    t = t.replace(body, nb, 1)

    t = replace_once(t, "  m_Name: MassTadPoleFauna\n", "  m_Name: BeeFauna\n", "root name")
    t = replace_once(t, f"guid: {BOID_SCRIPT}, type: 3", f"guid: {script_guid('BeeFauna')}, type: 3", "root script")
    root = re.search(r"--- !u!114 &%s\nMonoBehaviour:\n(?:  .*\n)+" % TADPOLE_ROOT_MB, t)
    rb = root.group(0)
    nrb = rb
    for k, v in (("diet", "2"), ("starvationSeconds", "0"), ("forager", "0"), ("collisionEffects", "[]"),
                 ("behaviorUpdateRate", _g(BEE_BEHAVIOUR_RATE)), ("minSpeed", str(BEE_SPEED[0])),
                 ("maxSpeed", str(BEE_SPEED[1]))):
        nrb, n = re.subn(r"(?m)^  %s: [^\n]*$" % k, f"  {k}: {v}", nrb, count=1)
        if n != 1:
            raise SystemExit(f"TadPoleFauna.prefab: the Boid field {k} has moved")
    t = t.replace(rb, nrb, 1)
    # a fresh Netcode identity (a copied hash collides with the tadpole)
    t, n = re.subn(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % TADPOLE_NETOBJ,
                   lambda m: f"{m.group(1)}{netcode_hash(pg, TADPOLE_NETOBJ)}\n", t, count=1)
    if n != 1:
        raise SystemExit("TadPoleFauna.prefab: the NetworkObject has moved")
    # the loop sound ships empty (wired by the audio owner; never a placeholder)
    em = re.search(r"--- !u!114 &%s\nMonoBehaviour:\n(?:  .*\n|    .*\n|      .*\n)+" % TADPOLE_EMITTER, t).group(0)
    nem = re.sub(r"  EventReference:\n    Guid:\n      Data1: -?\d+\n      Data2: -?\d+\n      Data3: -?\d+\n      Data4: -?\d+\n"
                 r"    Path: [^\n]*\n",
                 "  EventReference:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n", em)
    t = t.replace(em, nem, 1)

    # the wing driver on the root, and the two wings under it
    wd = str(fid("wing-driver"))
    t = replace_once(t, f"  - component: {{fileID: {TADPOLE_ROOT_MB}}}\n",
                     f"  - component: {{fileID: {TADPOLE_ROOT_MB}}}\n  - component: {{fileID: {wd}}}\n", "root components")
    wings, wing_tr = [], []
    for side in ("L", "R"):
        w, tr = wing_docs(body_docs, added, side)
        wings.append(w)
        wing_tr.append(tr)
    children = re.search(r"(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:\n)((?:  - \{fileID: -?\d+\}\n)+)" % TADPOLE_ROOT_TR, t)
    t = t[:children.end(2)] + "".join(f"  - {{fileID: {x}}}\n" for x in wing_tr) + t[children.end(2):]
    driver = (f"--- !u!114 &{wd}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
              f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {TADPOLE_ROOT_GO}}}\n"
              f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script_guid('BeeWingDriver')}, type: 3}}\n"
              f"  m_Name: \n  m_EditorClassIdentifier: \n  leftWings:\n  - BeeWingL\n  - Bee_WingFL\n  - Bee_WingHL\n"
              f"  rightWings:\n  - BeeWingR\n  - Bee_WingFR\n  - Bee_WingHR\n  beatsPerSecond: 9\n  strokeDegrees: 38\n")
    # the model: a Spindle on its own GameObject with the Bee.fbx instance under it
    model_go, model_tr, model_sp, model_inst = (str(fid(x)) for x in ("model-go", "model-tr", "model-spindle", "model-instance"))
    model_stripped = str((int(model_inst) ^ (MODEL_ROOT_TR & 0xFFFFFFFFFFFFFFFF)) & 0x7FFFFFFFFFFFFFFF)
    mg = guid(rel(MODEL))
    t = t[:children.end(2) + sum(len(f"  - {{fileID: {x}}}\n") for x in wing_tr)] + f"  - {{fileID: {model_tr}}}\n" + \
        t[children.end(2) + sum(len(f"  - {{fileID: {x}}}\n") for x in wing_tr):]
    mods = "".join(f"    - target: {{fileID: {MODEL_ROOT_TR}, guid: {mg},\n        type: 3}}\n      propertyPath: {k}\n"
                   f"      value: {v}\n      objectReference: {{fileID: 0}}\n"
                   for k, v in (("m_LocalPosition.x", 0), ("m_LocalPosition.y", 0), ("m_LocalPosition.z", 0),
                                ("m_LocalRotation.w", 1), ("m_LocalRotation.x", 0), ("m_LocalRotation.y", 0),
                                ("m_LocalRotation.z", 0), ("m_LocalEulerAnglesHint.x", 0),
                                ("m_LocalEulerAnglesHint.y", 0), ("m_LocalEulerAnglesHint.z", 0)))
    mods += (f"    - target: {{fileID: {MODEL_ROOT_GO}, guid: {mg},\n        type: 3}}\n      propertyPath: m_Name\n"
             f"      value: Bee\n      objectReference: {{fileID: 0}}\n")
    model = (f"--- !u!1 &{model_go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
             f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n"
             f"  - component: {{fileID: {model_tr}}}\n  - component: {{fileID: {model_sp}}}\n  m_Layer: 0\n  m_Name: BeeModel\n"
             f"  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
             f"--- !u!4 &{model_tr}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
             f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {model_go}}}\n"
             f"  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n"
             f"  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n  - {{fileID: {model_stripped}}}\n"
             f"  m_Father: {{fileID: {TADPOLE_ROOT_TR}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
             f"--- !u!114 &{model_sp}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
             f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {model_go}}}\n"
             f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {SPINDLE_SCRIPT}, type: 3}}\n"
             f"  m_Name: \n  m_EditorClassIdentifier: \n  RenderedObject: {{fileID: 0}}\n  additionalRenderedObjects: []\n"
             f"  parentSpindle: {{fileID: 0}}\n  LifeForm: {{fileID: 0}}\n  retainSpindle: 0\n  permanentWither: 1\n"
             f"--- !u!1001 &{model_inst}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n"
             f"    serializedVersion: 3\n    m_TransformParent: {{fileID: {model_tr}}}\n    m_Modifications:\n{mods}"
             f"    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
             f"  m_SourcePrefab: {{fileID: 100100000, guid: {mg}, type: 3}}\n"
             f"--- !u!4 &{model_stripped} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {MODEL_ROOT_TR}, guid: {mg},\n"
             f"    type: 3}}\n  m_PrefabInstance: {{fileID: {model_inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    if not t.endswith("\n"):
        t += "\n"
    return t + driver + "".join(wings) + model


def model_meta():
    """Bee.fbx's import settings: the Clawfish model's (a nested fauna body, 1:1, readable), with the bee's two material
    slots remapped by NAME (headless-safe: the remap is keyed by material name, not by an fbx-internal fileID)."""
    src = read(A("_Models", "Fauna", "ClawfishTest.fbx.meta"))
    src = re.sub(r"(?m)^guid: \w+$", f"guid: {guid(rel(MODEL))}", src, count=1)
    membrane = file_guid(A("_Graphics", "Materials", MEMBRANE_MATERIAL + ".mat"))
    remap = "".join(f"  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n      name: {n}\n"
                    f"    second: {{fileID: 2100000, guid: {g}, type: 2}}\n"
                    for n, g in (("BeeBody", SPINDLE_MATERIAL), ("BeeWing", membrane)))
    src, n = re.subn(r"(?ms)^  externalObjects:\n(?:  - first:\n.*?\n    second: [^\n]*\n)+", "  externalObjects:\n" + remap, src, count=1)
    if n != 1:
        raise SystemExit("ClawfishTest.fbx.meta: the externalObjects block this script copies has moved")
    # no rig, no takes: a bee's motion is the boid's and BeeWingDriver's, so no Animator rides on every bee
    for key, value in (("importAnimation", "0"), ("animationType", "0")):
        src, n = re.subn(r"(?m)^(\s+%s: )\d+$" % key, r"\g<1>" + value, src, count=1)
        if n != 1:
            raise SystemExit(f"ClawfishTest.fbx.meta: {key} has moved")
    return src


def model_parts_present():
    """The node names the prefab and BeeWingDriver rely on, read straight from the FBX's bytes (no Blender here)."""
    if not os.path.exists(MODEL):
        return list(MODEL_PARTS)
    data = open(MODEL, "rb").read()
    return [p for p in MODEL_PARTS if p.encode() not in data]


# ── the hive prefab ─────────────────────────────────────────────────────────────────────────────────────────────

def hive_prefab():
    t = read(BORROMEAN)
    root = f"--- !u!114 &{FLORA_ROOT_MB}\n"
    if root not in t or BORROMEAN_SCRIPT not in t:
        raise SystemExit("BorromeanFlora.prefab: the root MonoBehaviour this script forks has moved")
    t = t.replace(f"guid: {BORROMEAN_SCRIPT}, type: 3", f"guid: {script_guid('BeeHiveFlora')}, type: 3", 1)
    t = re.sub(r"(?m)^  m_Name: BorromeanFlora$", "  m_Name: BeeHive", t, count=1)
    t = re.sub(r"(?m)^      value: BorromeanCrystal$", "      value: BeeHiveCrystal", t, count=1)
    t = re.sub(r"(?m)^  healthBlocksForMaturity: \d+$", f"  healthBlocksForMaturity: {COMB['seedCells']}", t, count=1)
    t = re.sub(r"(?m)^  minHealthBlocks: -?\d+$", "  minHealthBlocks: -1", t, count=1)
    t = re.sub(r"(?m)^  leafSize: \{[^}]*\}$", f"  leafSize: {_v(COMB['cellLeaf'])}", t, count=1)
    t = re.sub(r"(?m)^  growPeriod: [\d.]+$", f"  growPeriod: {HIVE_GROW_PERIOD}", t, count=1)
    t = re.sub(r"(?m)^  plantRadiusCellFraction: [\d.]+$", "  plantRadiusCellFraction: 0.8", t, count=1)
    t = re.sub(r"(?m)^  plantRadiusCellFractionMin: [\d.]+$", "  plantRadiusCellFractionMin: 0.35", t, count=1)
    configs = "".join(f"  - {{fileID: 11400000, guid: {guid(rel(bee_cfg_path(e)))}, type: 2}}\n" for e, _ in ELEMENTS)
    hive = (f"  cellLeaf: {_v(COMB['cellLeaf'])}\n  cellSpacing: {_g(COMB['cellSpacing'])}\n"
            f"  combSpacing: {_g(COMB['combSpacing'])}\n  cellsPerComb: {COMB['cellsPerComb']}\n"
            f"  seedCells: {COMB['seedCells']}\n  maxCells: {COMB['maxCells']}\n  beeConfigs:\n{configs}"
            + "".join(f"  {k}: {_g(v)}\n" for k, v in COLONY.items())
            + "  hiveGrowSfx:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n")
    t, n = re.subn(r"(?m)^  maxTotalSpawnedObjects: \d+\n  surfaceScale: [\d.]+\n", hive, t, count=1)
    if n != 1:
        raise SystemExit("BorromeanFlora.prefab: its Borromean-only fields have moved")
    return t


# ── the configs ─────────────────────────────────────────────────────────────────────────────────────────────────

def bee_config(e, value):
    pg = guid(rel(prefab_path("BeeFauna")))
    name = f"Bee Fauna {e}"
    return SO_HEADER % (FAUNA_CONFIG_SCRIPT, name) + (
        f"  FaunaPrefab: {{fileID: {TADPOLE_ROOT_MB}, guid: {pg}, type: 3}}\n"
        # raised by a hive, never seeded or bred by the fauna spawner: no seed count, no feeds, no cap of its own
        "  InitialSpawnCount: 0\n  PopulationSize: 1\n  SpawnProbability: 1\n  FeedsPerOffspring: 0\n"
        "  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 10\n  MaxLivePopulation: 0\n"
        f"  Element: {value}\n"
        "  Variant:\n    Enabled: 1\n    HeartWorldScale: 0\n"
        f"    BaseBodyScale: {BEE_BODY_SCALE}\n"
        # zero: every body prism keeps its OWN authored size (a single BodyPrismScale would make the wings abdomens)
        "    BodyPrismScale: {x: 0, y: 0, z: 0}\n    BodyMaterial: {fileID: 0}\n"
        "    StarvationSeconds: 0\n    Forager: 2\n"
        "    CohesionRadius: 90\n"
        f"    BehaviorUpdateRate: {_g(BEE_BEHAVIOUR_RATE)}\n"
        "    TrailBlockInteractionRadius: -1\n    SeparationRadius: 40\n    FeedingClusterRadius: -1\n"
        "    GoalWeight: 6\n"
        f"    MinSpeed: {BEE_SPEED[0]}\n    MaxSpeed: {BEE_SPEED[1]}\n"
        "    OverrideAudio: 1\n    AudioLoopEvent:\n      Guid:\n        Data1: 0\n        Data2: 0\n        Data3: 0\n"
        "        Data4: 0\n      Path: \n    AudioMinDistance: -1\n    AudioMaxDistance: -1\n")


def flora_config(name, value, initial, cap, palette=None):
    hg = guid(rel(prefab_path("BeeHive")))
    text = SO_HEADER % (FLORA_CONFIG_SCRIPT, name) + (
        f"  FloraPrefab: {{fileID: {FLORA_ROOT_MB}, guid: {hg}, type: 3}}\n"
        f"  SpawnProbability: 1\n  InitialSpawnCount: {initial}\n  OverrideDefaultPlantPeriod: 0\n"
        f"  NewPlantPeriod: 9999999\n  PopulationSize: {initial}\n  MaxLivePopulation: {cap}\n"
        # a hive buds a daughter (a swarm) for every full comb it grows - the one flora offspring path
        f"  GrowthPerOffspring: {COMB['maxCells']}\n  OffspringPerBirth: 1\n  ReproductionCooldownSeconds: 60\n"
        "  MaturityFraction: 1\n  OffspringSpread: 220\n"
        f"  Element: {value}\n"
        "  Variant:\n    Enabled: 1\n    HeartWorldScale: 0\n"
        "    LeafSize: {x: 0, y: 0, z: 0}\n    GrowPeriod: -1\n    LatticeScale: -1\n"
        # the armour law states Mass's cadence on the asset (author_flora_armour.py); Charge authors none
        f"    ShieldPeriod: {_g(armour_period()) if value == 2 else '-1'}\n"
        "    WitherRingInterval: -1\n    MaxTotalSpawnedObjects: -1\n    MaxTotalSpawnedObjectsScale: -1\n"
        "    ItemsPerGrow: -1\n    RandomItems: -1\n    MaturationSeconds: -1\n    MaxSpawnsPerFrame: -1\n"
        "    PlantRadiusCellFraction: -1\n    PlantRadiusCellFractionMin: -1\n")
    if palette:
        text += "  SpreadElements: 1\n  ElementPalette:\n" + "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in palette)
    return text


def network_with_bee(text):
    pg = guid(rel(prefab_path("BeeFauna")))
    if pg in text:
        return text
    entry = (f"  - Override: 0\n    Prefab: {{fileID: {TADPOLE_ROOT_GO}, guid: {pg},\n      type: 3}}\n"
             "    SourcePrefabToOverride: {fileID: 0}\n    SourceHashToOverride: 0\n    OverridingTargetPrefab: {fileID: 0}\n")
    m = re.search(r"(?m)^  List:\n", text)
    if not m:
        raise SystemExit("DefaultNetworkPrefabs.asset: no List")
    return text[:m.end()] + entry + text[m.end():]


def emit():
    out = {}
    for folder in (SCRIPT_DIR, PREFAB_DIR):
        out[folder + ".meta"] = FOLDER_META % guid(rel(folder))
    for s in SCRIPTS:
        out[os.path.join(SCRIPT_DIR, s + ".cs.meta")] = SCRIPT_META % script_guid(s)
    out[TEST + ".meta"] = SCRIPT_META % guid(rel(TEST))
    for name, text in (("BeeFauna", bee_prefab()), ("BeeHive", hive_prefab())):
        p = prefab_path(name)
        out[p] = text
        out[p + ".meta"] = PREFAB_META % guid(rel(p))
    for e, value in ELEMENTS:
        out[bee_cfg_path(e)] = bee_config(e, value)
        out[hive_cfg_path(e)] = flora_config(f"Bee Hive Flora {e}", value, 1, BENCH_HIVE_CAP)
    palette = [guid(rel(hive_cfg_path(e))) for e, _ in ELEMENTS]
    out[hesp_hive_path()] = flora_config(HESP_HIVE, 2, HESPERIDES_HIVES["initial"], HESPERIDES_HIVES["cap"], palette)
    for p in [bee_cfg_path(e) for e, _ in ELEMENTS] + [hive_cfg_path(e) for e, _ in ELEMENTS] + [hesp_hive_path()]:
        out[p + ".meta"] = ASSET_META % guid(rel(p))
    out[NETWORK] = network_with_bee(read(NETWORK))
    out[MODEL + ".meta"] = model_meta()
    return out


# ── verification ────────────────────────────────────────────────────────────────────────────────────────────────

def colliders():
    """At the caps: hive comb prisms + hive hearts (flora; LOD-culled prisms) and bee bodies + bee hearts."""
    hives = HESPERIDES_HIVES["cap"]
    bees = hives * COLONY["maxBees"]
    return dict(hives=hives, comb=hives * COMB["maxCells"], hive_hearts=hives, bees=bees, bee_prisms=bees * 3, bee_hearts=bees)


def verify(out):
    problems = []
    minted = {re.search(r"^guid: (\w+)", t, re.M).group(1): p for p, t in out.items() if p.endswith(".meta")}
    for g, p in minted.items():
        owners = subprocess.run(["grep", "-rl", "--include=*.meta", f"^guid: {g}", A()], capture_output=True, text=True).stdout.splitlines()
        owners = [o for o in owners if os.path.abspath(o) != os.path.abspath(p)]
        if owners:
            problems.append(f"guid {g} of {rel(p)} is already owned by {rel(owners[0])}")
    for s in SCRIPTS:
        if not os.path.exists(os.path.join(SCRIPT_DIR, s + ".cs")):
            problems.append(f"no script {s}.cs for the meta this script owns")
    for name in ("BeeFauna", "BeeHive"):
        t = out[prefab_path(name)]
        fids = re.findall(r"(?m)^--- !u!\d+ &(-?\d+)", t)
        if len(fids) != len(set(fids)):
            problems.append(f"{name}.prefab has a duplicate fileID")
        for ref in set(re.findall(r"\{fileID: (-?\d+)\}", t)) - {"0"}:
            if ref not in fids:
                problems.append(f"{name}.prefab references a local fileID {ref} it does not contain")
    bee = out[prefab_path("BeeFauna")]
    for want, why in ((f"guid: {script_guid('BeeFauna')}", "runs BeeFauna"), ("  diet: 2\n", "is a Pollinator"),
                      ("  forager: 0\n", "never forages"), ("  starvationSeconds: 0\n", "never starves"),
                      ("value: BeeWingL", "has a left wing"), ("value: BeeWingR", "has a right wing"),
                      (f"guid: {script_guid('BeeWingDriver')}", "flaps its wings"),
                      (f"m_SourcePrefab: {{fileID: 100100000, guid: {guid(rel(MODEL))}, type: 3}}", "nests the real model"),
                      (f"guid: {SPINDLE_SCRIPT}, type: 3}}\n  m_Name: \n  m_EditorClassIdentifier: \n  RenderedObject: {{fileID: 0}}",
                       "puts the model under a Spindle"),
                      ("  - Bee_WingFL\n  - Bee_WingHL\n", "flaps the model's left wings"),
                      ("  - Bee_WingFR\n  - Bee_WingHR\n", "flaps the model's right wings")):
        if want not in bee:
            problems.append(f"BeeFauna.prefab {why}: missing {want.strip()}")
    if BOID_SCRIPT in bee:
        problems.append("BeeFauna.prefab still runs Boid")
    if bee.count(f"target: {{fileID: {SP_RENDERER}, guid: {SPINDLE_PREFAB},\n        type: 3}}\n      propertyPath: m_Enabled\n      value: 0") != 3:
        problems.append("BeeFauna.prefab: the tadpole skin of the abdomen and both wings should be hidden (the model is the body)")
    for part in model_parts_present():
        problems.append(f"Bee.fbx: no node {part} (build it: Tools/Build/bee_model/build_bee.py)")
    meta = out.get(MODEL + ".meta", "")
    if "  animationType: 0\n" not in meta or "  importAnimation: 0\n" not in meta:
        problems.append("Bee.fbx.meta imports an Animator (a rig or takes): the bee has neither")
    if f"second: {{fileID: 2100000, guid: {SPINDLE_MATERIAL}, type: 2}}" not in meta or "name: BeeWing\n" not in meta:
        problems.append("Bee.fbx.meta does not remap BeeBody to SpindleMaterial and BeeWing to the membrane")
    tad_hash = re.search(r"GlobalObjectIdHash: (\d+)", read(TADPOLE)).group(1)
    if f"GlobalObjectIdHash: {tad_hash}\n" in bee:
        problems.append("BeeFauna.prefab copies the tadpole's Netcode hash (they would collide)")
    hive = out[prefab_path("BeeHive")]
    if BORROMEAN_SCRIPT in hive or "surfaceScale" in hive:
        problems.append("BeeHive.prefab still carries BorromeanFlora")
    for e, _ in ELEMENTS:
        if guid(rel(bee_cfg_path(e))) not in hive:
            problems.append(f"BeeHive.prefab does not wire the {e} bee")
    # the C# defaults are the model's (a hive made by hand in the inspector matches the shipped one)
    cs = read(os.path.join(SCRIPT_DIR, "BeeHiveFlora.cs"))
    for k in ("cellsPerComb", "seedCells", "maxCells", "minBees", "cellsPerBee", "maxBees"):
        m = re.search(r"\b(?:int) %s = (\d+);" % k, cs)
        want = COMB.get(k, COLONY.get(k))
        if not m or int(m.group(1)) != want:
            problems.append(f"BeeHiveFlora.{k} does not default to the model's {want}")
    if COMB["seedCells"] >= COMB["maxCells"]:
        problems.append("the seed comb fills the whole hive: no cell would ever be built from brought-home mass")
    if HESPERIDES_HIVES["cap"] < HESPERIDES_HIVES["initial"]:
        problems.append("the Hesperides hive cap is below its seed count")
    profile = out.get(HESP_PROFILE) or read(HESP_PROFILE)
    if f"guid: {guid(rel(hesp_hive_path()))}, type: 2}}" not in profile.split("SupportedFaunas:")[0]:
        problems.append("the Hesperides profile does not list the hive in SupportedFloras "
                        "(Tools/ecosim/gen_hesperides_assets.py owns that file and lists it)")
    return problems


def self_test():
    out = emit()
    probs = verify(out)
    if probs:
        print("self-test: the fresh emit fails its own checks:\n  - " + "\n  - ".join(probs))
        return 1
    caught = 0
    planted = [
        ("tadpole hash kept", lambda o: o.__setitem__(prefab_path("BeeFauna"), re.sub(
            r"GlobalObjectIdHash: \d+", "GlobalObjectIdHash: " + re.search(r"GlobalObjectIdHash: (\d+)", read(TADPOLE)).group(1),
            o[prefab_path("BeeFauna")], count=1))),
        ("a wing reference dangles", lambda o: o.__setitem__(prefab_path("BeeFauna"),
            o[prefab_path("BeeFauna")] + "  stray: {fileID: 123456789}\n")),
        ("forager on", lambda o: o.__setitem__(prefab_path("BeeFauna"), o[prefab_path("BeeFauna")].replace("  forager: 0\n", "  forager: 1\n"))),
        ("the model unnested", lambda o: o.__setitem__(prefab_path("BeeFauna"), o[prefab_path("BeeFauna")].replace(
            f"guid: {guid(rel(MODEL))}, type: 3}}", "guid: 00000000000000000000000000000000, type: 3}"))),
        ("the body material unmapped", lambda o: o.__setitem__(MODEL + ".meta", o[MODEL + ".meta"].replace(SPINDLE_MATERIAL, "0" * 32))),
        ("hive unlisted", lambda o: o.__setitem__(HESP_PROFILE, read(HESP_PROFILE).replace(
            f"  - {{fileID: 11400000, guid: {guid(rel(hesp_hive_path()))}, type: 2}}\n", ""))),
    ]
    for name, plant in planted:
        o = dict(out)
        plant(o)
        if verify(o):
            caught += 1
        else:
            print(f"self-test: planted '{name}' was NOT caught")
    print(f"self-test: {caught}/{len(planted)} planted defects caught")
    return 0 if caught == len(planted) else 1


def report():
    c = colliders()
    print("The bees (Docs/ECOSYSTEM.md §66)\n")
    print(f"  hive: comb of {COMB['maxCells']} cells ({COMB['cellsPerComb']} per comb), {COMB['seedCells']} grown as a plant, "
          f"the rest built from brought-home mass; bees {COLONY['minBees']}..{COLONY['maxBees']} (+1 per {COLONY['cellsPerBee']} cells)")
    print(f"  Hesperides: {HESPERIDES_HIVES['initial']} hives seeded, cap {HESPERIDES_HIVES['cap']}, elements spread")
    print(f"  colliders at the caps: {c['comb']} comb prisms + {c['hive_hearts']} hive hearts (flora), "
          f"{c['bee_prisms']} bee prisms + {c['bee_hearts']} bee hearts ({c['bees']} bees)")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()
    if args.self_test:
        return self_test()
    out = emit()
    problems = verify(out)
    report()
    if args.check:
        drift = [rel(p) for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        if problems or drift:
            print("\nFAIL")
            for p in problems + [f"differs from what this script authors: {x}" for x in drift]:
                print("  - " + p)
            return 1
        print("\nOK - every bee asset matches the model and Hesperides lists the hive.")
        return 0
    if problems:
        print("\nFAIL - refusing to write:")
        for p in problems:
            print("  - " + p)
        return 1
    for p, t in out.items():
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(t)
    print(f"\nwrote {len(out)} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
