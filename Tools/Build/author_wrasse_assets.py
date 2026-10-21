#!/usr/bin/env python3
"""Author the Wrasse (the Sparrow hull with the Kite & Tail navigator seat) and register it, then prove the shipped set.

    python3 Tools/Build/author_wrasse_assets.py             # author what is missing, then check
    python3 Tools/Build/author_wrasse_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_wrasse_assets.py --self-test # prove every check fires

"Bring it to Unity" means the Toybox's Vessel Changer offers it (Garrett, 2026-10-17: "my workflow is artifact to
vessel selector in toybox"). The changer offers what ToyVesselRoster.Default names AND the Vessel Prefab Container
can spawn, so the navigator - a SEAT on a hull, not a hull - ships as its own hull class, the way the Goby & Pistol
duo does on cece/sharp-tesla-j0kd7z (this tool is that branch's author_goby_assets.py, cloned for the Sparrow).

WHAT IT MAKES (the design: Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md)
    Assets/_Prefabs/Spacevessels/Wrasse.prefab (+ .meta)   a text clone of Sparrow.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 19` (VesselClassType.Wrasse)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
          (a disk copy keeps the donor's hash and Netcode then keys two prefabs on one entry)
        - KiteNavigator on the root, wired to KiteNavigatorConfig.asset
        - NO BUTTONS: the Sparrow's ability bindings are emptied (Garrett, 2026-10-18: "the pilot should be a one thumb flyer
          that has no buttons"); the navigator's saved Time boosts are spent on the pilot's trigger (KiteNavigator)
        - TWO THUMBS: the Sparrow's SingleStickVesselTransformer becomes its base, VesselTransformer (Garrett, round 4d:
          "lets go back to two thumb flying for the pilot so it can choose the fly in a way that brings more mass into
          plane for the kite"); the subclass adds no serialized field, so the swap is the script guid alone
        - its VesselImpactor on WrasseImpactorDataContainer: the Sparrow's container plus KiteOmniCrystalEffect in the omni
          list (an Omni crystal drops motes on the navigator's board) and without the Sparrow's Omni ward, so the Sparrow's own
          container is untouched
        - NO SPARROW KIT (Garrett, 2026-10-18: "clean up the vestiges from using the sparrow. the pilot is shooting when i am
          controlling the kite"): the guns, the bullet and Skyburst missile factories and their four action executors
          (SPARROW_SUBTREES), the missile rearm, the barrel roll, the Omni ward and the Time-L5 immunity
          (SPARROW_COMPONENTS), the AI's ability cycle (AIPilot.abilities: the gun and missile actions it fired whenever the
          autopilot flew - i.e. whenever the human was on the kite; its boost policy stays wired, as check_vessel_shared_channels
          requires, and is inert with no boost binding); the Sparrow's gun telemetry
          becomes DefaultVesselTelemetry. Every local reference into a removed part is dropped, and --check holds the prefab
          to having no dangling local fileID
        - TWO BODIES (round 7; Garrett, 2026-10-20: "give the wrasse and grouper proper 3d vessels with animations etc." -
          "the wrasse and grouper should be visible to others"): Grouper.fbx and WrasseKite.fbx (/vessel-model,
          specs/grouper.py and specs/wrassekite.py) nested under the root by name; the Grouper is the hull's drawn geometry
          (VesselCustomization._shipGeometries, so the domain paint lands on it) and the Sparrow's mesh renderer is switched
          off (its bones, jets, colliders and Animator stay: the hull still flies on them); WrasseCrewBodies on the root
          places, swims and replicates the Wrasse and puppets the Grouper (bodies_retrofit)
      Everything else is the Sparrow's: the lab flew the Sparrow's numbers, and the hull is the pilot's half.
      Unlike the clone, the no-buttons and container edits are RETROFITS: applied to the committed prefab too, idempotently.
    SO_Class_Wrasse (the Sparrow's icons and abilities, owned from the start) + its entry in SO_Classlist_All / _Classes;
    one entry each in `Vessel Prefab Container.asset` (the root TRANSFORM) and `DefaultNetworkPrefabs.asset` (the root
    GAMEOBJECT); Resources/ElementalAbilityMaps/Wrasse.asset (the Sparrow's map, unchanged but for its name and class);
    and four CrystalHullFusionConfig entries (the Sparrow's: the Wrasse draws the Sparrow's mesh so its bakes apply).
    The toybox roster (ToyVesselRoster.Default) is CODE and is not written here (vessel skill rule 40).

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while Wrasse.prefab is absent,
and `--check` validates the SHIPPED prefab's invariants rather than diffing it against a fresh clone of a donor that
will move on. To re-author from scratch, delete Wrasse.prefab and its .meta and run again.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Sparrow.prefab")
PREFAB = os.path.join(VESSELS, "Wrasse.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
DONOR_CLASS = os.path.join(CLASSES, "SO_Class_Sparrow.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Wrasse.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
DONOR_MAP = os.path.join(MAPS, "Sparrow.asset")
MAP = os.path.join(MAPS, "Wrasse.asset")
FUSION = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
SEAT_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "Crew", "KiteNavigator.cs.meta")
CONFIG_META = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "KiteNavigator", "KiteNavigatorConfig.asset.meta")
CONTAINERS = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Effect Containers", "VesselContainers")
DONOR_CONTAINER = os.path.join(CONTAINERS, "SparrowImpactorDataContainer.asset")
WRASSE_CONTAINER = os.path.join(CONTAINERS, "WrasseImpactorDataContainer.asset")
OMNI_EFFECT = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Vessel Crystal Effects", "KiteOmniCrystalEffect.asset")
OMNI_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "ImpactEffects", "EffectsSO", "Vessel Crystal Effects",
                                "KiteOmniCrystalEffectSO.cs.meta")

# 16 is BrittleStar (bleeding-edge) and the Goby (cece/sharp-tesla-j0kd7z), 17 the Angler (cece/zealous-feynman-po8k8d);
# 18 is left for the Goby, which has to move off 16 when it merges. Checked across every origin branch 2026-10-17.
WRASSE_CLASS_ID = 19
SPARROW_CLASS_ID = 11
PREFAB_GUID = "c81d6c43058343b796fee1128ce6ccda"
CLASS_GUID = "db96e34a6c3b4cb1b498f0529f765db1"
MAP_GUID = "007a16964bc0490a937e8384d4e27eec"
DONOR_CONTAINER_GUID = "bd2b4a654fb302e47a23cf303280df2d"
WRASSE_CONTAINER_GUID = "dac5775f7c7b4048bab9186e40e50977"
SINGLE_STICK_TRANSFORMER_GUID = "b6b54993a316e0041b4bf5981190ac67"   # the Sparrow's SingleStickVesselTransformer
TWO_STICK_TRANSFORMER_GUID = "95baa921cf22958449a3143749d5e0e3"      # VesselTransformer (same fields, its base class)
OMNI_EFFECT_GUID = "a5c175fbce18440abb3570fbb8672a2a"
ROOT_GO = "8279817603024118164"              # the Sparrow's root GameObject
ROOT_TRANSFORM_ID = "4853044355858455827"
NETWORK_OBJECT_ID = "2386735669300085014"    # the NetworkObject component on it
SEAT_ID = "5137264980016000019"              # KiteNavigator on the root


def guid_of(meta_path: str) -> str:
    with open(meta_path, encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
    if not m:
        raise RuntimeError(f"{os.path.relpath(meta_path, ROOT)}: no guid")
    return m.group(1)


# ----------------------------------------------------------------------------- the prefab
def seat_block(script_guid: str, config_guid: str) -> str:
    return (f"--- !u!114 &{SEAT_ID}\n"
            "MonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n"
            "  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n"
            f"  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            "  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: \n"
            "  m_EditorClassIdentifier: \n"
            f"  config: {{fileID: 11400000, guid: {config_guid}, type: 2}}\n")


def build_clone(donor: str, script_guid: str, config_guid: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Sparrow\n", "  m_Name: Wrasse\n", "root GameObject name")
    text = replace_once(text, "  _name: Sparrow\n", "  _name: Wrasse\n", "VesselStatus _name")
    text = replace_once(text, f"  vesselType: {SPARROW_CLASS_ID}\n", f"  vesselType: {WRASSE_CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)
    # KiteNavigator: on the root's component list (after its last entry) and as a document at the end
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    if not root:
        raise RuntimeError("donor drifted: the root GameObject's m_Component list was not found")
    text = text[:root.end(1)] + f"  - component: {{fileID: {SEAT_ID}}}\n" + text[root.end(1):]
    if not text.endswith("\n"):
        text += "\n"
    return retrofit(text + seat_block(script_guid, config_guid))


BINDINGS = re.compile(r"(  _inputEventShipActions:)\n(?:  [- ] .*\n)+")

SPARROW_SUBTREES = ["FullAutoBlockActionExecutor", "FullAutoActionExecutor", "SkyBurstMissileActionExecutor", "ExhaustBarrage",
                    "GunSprayAccuracy"]
SPARROW_COMPONENTS = ["VesselRearmOnPrismDestruction", "BarrelRollController", "VesselTimedElementalWard", "VesselElementalImmunity"]
SPARROW_WARD_EFFECT_GUID = "d926fa11a6134e259eef87c71cfe1573"   # SparrowVesselWardByCrystalEffect
SPARROW_TELEMETRY_KEYS = ["prismBlocksShotStat", "skyburstMissilesShotStat", "dangerBlocksSpawnedStat"]
DOC = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\n", re.M)


def guid_of_script(name: str) -> str:
    for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets", "_Scripts")):
        if f"{name}.cs.meta" in files:
            return guid_of(os.path.join(dirpath, f"{name}.cs.meta"))
    raise RuntimeError(f"no script {name}.cs")


def split_docs(text: str):
    """(header, [(kind, anchor, doc text incl. its header line)]) in file order."""
    starts = [m.start() for m in DOC.finditer(text)]
    head = text[:starts[0]] if starts else text
    docs = []
    for i, a in enumerate(starts):
        b = starts[i + 1] if i + 1 < len(starts) else len(text)
        m = DOC.match(text, a)
        docs.append((m.group(1), m.group(2), text[a:b]))
    return head, docs


def strip_sparrow_kit(text: str) -> str:
    """The Sparrow's weapons and Sparrow-only parts off the Wrasse (see the module doc). Idempotent."""
    head, docs = split_docs(text)
    by_anchor = {a: d for _, a, d in docs}
    go_name = {a: (re.search(r"^  m_Name: (.*)$", d, re.M) or [None, None])[1] for k, a, d in docs if k == "1"}
    transform_of, children = {}, {}
    for k, a, d in docs:
        if k in ("4", "224") and " stripped" not in d.split("\n", 1)[0]:
            go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", d)
            if go:
                transform_of[go.group(1)] = a
            ch = re.search(r"  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", d)
            children[a] = re.findall(r"fileID: (-?\d+)", ch.group(1)) if ch else []
    go_of_transform = {t: g for g, t in transform_of.items()}

    gos = set()
    def take(go):
        if go in gos:
            return
        gos.add(go)
        for child in children.get(transform_of.get(go), []):
            if child in go_of_transform:
                take(go_of_transform[child])
    for go, name in go_name.items():
        if name in SPARROW_SUBTREES:
            take(go)

    kill_scripts = {guid_of_script(n) for n in SPARROW_COMPONENTS}
    removed = set(gos) | {transform_of[g] for g in gos if g in transform_of}
    for k, a, d in docs:
        go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", d, re.M)
        if go and go.group(1) in gos:
            removed.add(a)
        sc = re.search(r"^  m_Script: \{fileID: 11500000, guid: (\w+)", d, re.M)
        if k == "114" and sc and sc.group(1) in kill_scripts:
            removed.add(a)
    if not removed and "  abilities: []\n" in text:
        pass

    ai_guid, sparrow_tel, default_tel = guid_of_script("AIPilot"), guid_of_script("SparrowVesselTelemetry"), guid_of_script("DefaultVesselTelemetry")
    out = []
    for k, a, d in docs:
        if a in removed:
            continue
        for r in removed:
            d = re.sub(r"^  - component: \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"^  - \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"\{fileID: %s\}" % r, "{fileID: 0}", d)
        if f"guid: {ai_guid}, type: 3" in d:
            d = re.sub(r"(  abilities:)\n(?:  [- ] .*\n)+", r"\1 []\n", d)
        if f"guid: {sparrow_tel}, type: 3" in d:
            d = d.replace(f"guid: {sparrow_tel}, type: 3", f"guid: {default_tel}, type: 3")
            for key in SPARROW_TELEMETRY_KEYS:
                d = re.sub(r"^  %s: \{[^}]*\}\n" % key, "", d, flags=re.M | re.S)
        out.append(d)
    # an emptied list prints as []
    text = head + "".join(out)
    return re.sub(r"^(  (?:m_Children|_executors|m_Component)):\n(?=  [a-zA-Z_<]|--- |\Z)", r"\1: []\n", text, flags=re.M)


def dangling_local_refs(text: str):
    """Local {fileID: N} references (no guid) that name no document in the file."""
    anchors = {m.group(2) for m in DOC.finditer(text)}
    return sorted({r for r in re.findall(r"\{fileID: (-?\d+)\}", text) if r != "0" and r not in anchors})


SPARROW_HUD_KEYS = ["view", "fireGunExecutor", "barrelRollController", "gunSprayAccuracy", "stationaryModeChanged",
                    "onInputEventBlocked"]


def hud_retrofit(text: str) -> str:
    """SparrowHUDController (guns, missiles, roll) becomes WrasseHUDController, wired to the seat. Idempotent."""
    sparrow_hud, kite_hud = guid_of_script("SparrowHUDController"), guid_of_script("WrasseHUDController")
    head, docs = split_docs(text)
    out = []
    for _, _, d in docs:
        if f"guid: {sparrow_hud}, type: 3" in d:
            d = d.replace(f"guid: {sparrow_hud}, type: 3", f"guid: {kite_hud}, type: 3")
            for key in SPARROW_HUD_KEYS:
                d = re.sub(r"^  %s: \{[^}]*\}\n" % key, "", d, flags=re.M)
            d = d.rstrip("\n") + f"\n  navigator: {{fileID: {SEAT_ID}}}\n"
        out.append(d)
    return head + "".join(out)


# The pilot a bit more maneuverable, the roll especially (Garrett, round 4e: "the roll speed was especially slow"). On two
# sticks RollScaler is a real roll axis, where the Sparrow's single stick used it only to bank into turns (30). Written
# ONCE, while the transformer still carries the Sparrow's numbers, so a value play-tuned in Unity is never overwritten.
SPARROW_TURN = {"PitchScaler": "80", "YawScaler": "80", "RollScaler": "30"}
WRASSE_TURN = {"PitchScaler": "100", "YawScaler": "100", "RollScaler": "120"}


def tuning_retrofit(text: str) -> str:
    """The Wrasse's own turn rates, over the Sparrow's untouched ones only. Idempotent."""
    head, docs = split_docs(text)
    out = []
    for _, _, d in docs:
        if f"guid: {TWO_STICK_TRANSFORMER_GUID}, type: 3" in d and \
                all(f"\n  {k}: {v}\n" in d for k, v in SPARROW_TURN.items()):
            for k, v in WRASSE_TURN.items():
                d = d.replace(f"\n  {k}: {SPARROW_TURN[k]}\n", f"\n  {k}: {v}\n")
        out.append(d)
    return head + "".join(out)


# ----------------------------------------------------------------------------- the two bodies (round 7)
SPARROW_FBX_GUID = "151e3f0352963624abf4da579618e66d"
SPARROW_INSTANCE_ID = "3134011750752858212"      # the Sparrow model nested under the root
SPARROW_SMR_SOURCE = "2692722512241470911"       # its SkinnedMeshRenderer, in the FBX
SPARROW_MODEL_GO = "2864103291091619125"         # its root GameObject (stripped): the ship geometry until round 7
GROUPER_FBX_GUID = "e8c932601ef7e35b1f3017557298424c"
KITE_FBX_GUID = "c56064d56acc061a124729744a5966fa"
MODEL_ROOT_GO = "919132149155446097"             # a model's root GameObject / Transform: constants of the importer
MODEL_ROOT_TRANSFORM = "-8679921383154817045"
GROUPER_INSTANCE_ID = "7319000000000000001"
GROUPER_TRANSFORM_ID = "7319000000000000002"
GROUPER_GO_ID = "7319000000000000003"
KITE_INSTANCE_ID = "7319000000000000011"
KITE_TRANSFORM_ID = "7319000000000000012"
BODIES_ID = "7319000000000000021"                # WrasseCrewBodies on the root
BODIES_SCRIPT_META = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "Crew", "WrasseCrewBodies.cs.meta")
# WrasseCrewBodies' serialized fields at the C# initializers, written once (the team tunes them in the inspector after)
BODIES_FIELDS = (
    "  kiteModelName: WrasseKite\n  grouperModelName: Grouper\n  kiteModelEuler: {x: 0, y: 0, z: 0}\n  kiteModelScale: 1\n"
    "  swimHz: 1.2\n  swimHzPer100: 1.4\n  swimDegrees: {x: 8, y: 14, z: 24}\n  swimLagBeats: 0.18\n  finFlapDegrees: 28\n"
    "  swimTurnBendDegrees: 18\n  grouperCruiseSpeed: 60\n  grouperBoostSpeed: 260\n  grouperTailHz: {x: 0.6, y: 1.4}\n"
    "  grouperTailDegrees: {x: 7, y: 16}\n  grouperTurnBendDegrees: 22\n  grouperFullYawRate: 110\n"
    "  grouperFinScullDegrees: 14\n  grouperFinBankDegrees: 26\n  grouperJawDegrees: 24\n  grouperDorsalDegrees: 30\n"
    "  remoteSmoothing: 14\n")
SPARROW_SMR_OFF = (f"    - target: {{fileID: {SPARROW_SMR_SOURCE}, guid: {SPARROW_FBX_GUID},\n        type: 3}}\n"
                   "      propertyPath: m_Enabled\n      value: 0\n      objectReference: {fileID: 0}\n")


def model_instance(instance_id: str, transform_id: str, fbx_guid: str, name: str, go_id: str = None) -> str:
    """A model nested under the root in its own frame (the fleet's: nose +Z, up +Y, where the Sparrow's sits)."""
    mods = [(MODEL_ROOT_GO, "m_Name", name)] + [(MODEL_ROOT_TRANSFORM, k, v) for k, v in (
        ("m_LocalPosition.x", "0"), ("m_LocalPosition.y", "0"), ("m_LocalPosition.z", "0"), ("m_LocalRotation.w", "1"),
        ("m_LocalRotation.x", "0"), ("m_LocalRotation.y", "0"), ("m_LocalRotation.z", "0"),
        ("m_LocalEulerAnglesHint.x", "0"), ("m_LocalEulerAnglesHint.y", "0"), ("m_LocalEulerAnglesHint.z", "0"))]
    out = (f"--- !u!1001 &{instance_id}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
           f"  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {{fileID: {ROOT_TRANSFORM_ID}}}\n"
           "    m_Modifications:\n" +
           "".join(f"    - target: {{fileID: {fid}, guid: {fbx_guid},\n        type: 3}}\n      propertyPath: {path}\n"
                   f"      value: {v}\n      objectReference: {{fileID: 0}}\n" for fid, path, v in mods) +
           "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
           f"  m_SourcePrefab: {{fileID: 100100000, guid: {fbx_guid}, type: 3}}\n"
           f"--- !u!4 &{transform_id} stripped\nTransform:\n"
           f"  m_CorrespondingSourceObject: {{fileID: {MODEL_ROOT_TRANSFORM}, guid: {fbx_guid},\n    type: 3}}\n"
           f"  m_PrefabInstance: {{fileID: {instance_id}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    if go_id:
        out += (f"--- !u!1 &{go_id} stripped\nGameObject:\n"
                f"  m_CorrespondingSourceObject: {{fileID: {MODEL_ROOT_GO}, guid: {fbx_guid},\n    type: 3}}\n"
                f"  m_PrefabInstance: {{fileID: {instance_id}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return out


def bodies_block(script_guid: str) -> str:
    return (f"--- !u!114 &{BODIES_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n" + BODIES_FIELDS)


def bodies_retrofit(text: str, script_guid: str) -> str:
    """The Grouper and the Wrasse as models (round 7). Idempotent: every step looks before it writes."""
    if f"--- !u!1001 &{GROUPER_INSTANCE_ID}\n" not in text:
        text += model_instance(GROUPER_INSTANCE_ID, GROUPER_TRANSFORM_ID, GROUPER_FBX_GUID, "Grouper", GROUPER_GO_ID)
    if f"--- !u!1001 &{KITE_INSTANCE_ID}\n" not in text:
        text += model_instance(KITE_INSTANCE_ID, KITE_TRANSFORM_ID, KITE_FBX_GUID, "WrasseKite")
    if f"--- !u!114 &{BODIES_ID}\n" not in text:
        text += bodies_block(script_guid)
    # the root lists the component and both model transforms
    m = re.search(r"(--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n(?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    if m and f"  - component: {{fileID: {BODIES_ID}}}\n" not in m.group(1):
        text = text[:m.end(1)] + f"  - component: {{fileID: {BODIES_ID}}}\n" + text[m.end(1):]
    m = re.search(r"(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:\n(?:  - \{fileID: -?\d+\}\n)*)" % ROOT_TRANSFORM_ID, text)
    if m:
        add = "".join(f"  - {{fileID: {t}}}\n" for t in (GROUPER_TRANSFORM_ID, KITE_TRANSFORM_ID)
                      if f"  - {{fileID: {t}}}\n" not in m.group(1))
        text = text[:m.end(1)] + add + text[m.end(1):]
    # the Grouper is the drawn hull: the domain paint lands on it, and the Sparrow's mesh is switched off (its bones, jets,
    # colliders and Animator stay - the hull still flies on them)
    text = text.replace(f"  _shipGeometries:\n  - {{fileID: {SPARROW_MODEL_GO}}}\n", f"  _shipGeometries:\n  - {{fileID: {GROUPER_GO_ID}}}\n")
    m = re.search(r"--- !u!1001 &%s\nPrefabInstance:\n(?:.*\n)*?    m_Modifications:\n" % SPARROW_INSTANCE_ID, text)
    if m and SPARROW_SMR_OFF not in text:
        text = text[:m.end()] + SPARROW_SMR_OFF + text[m.end():]
    return text


def check_bodies(text: str, script_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    for iid, fbx, name in ((GROUPER_INSTANCE_ID, GROUPER_FBX_GUID, "Grouper"), (KITE_INSTANCE_ID, KITE_FBX_GUID, "WrasseKite")):
        inst = re.search(r"--- !u!1001 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % iid, text)
        want(inst and f"m_SourcePrefab: {{fileID: 100100000, guid: {fbx}, type: 3}}" in inst.group(1)
             and f"m_TransformParent: {{fileID: {ROOT_TRANSFORM_ID}}}" in inst.group(1)
             and f"      value: {name}\n" in inst.group(1),
             f"Wrasse.prefab: {name}.fbx is not nested under the root as '{name}' (WrasseCrewBodies finds it by that name)")
    root_t = re.search(r"--- !u!4 &%s\nTransform:\n((?:.*\n)*?)(?=--- !u!|\Z)" % ROOT_TRANSFORM_ID, text)
    want(root_t and all(f"  - {{fileID: {t}}}\n" in root_t.group(1) for t in (GROUPER_TRANSFORM_ID, KITE_TRANSFORM_ID)),
         "Wrasse.prefab: the root's children do not list both model instances")
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    want(root and f"{{fileID: {BODIES_ID}}}" in root.group(1), "Wrasse.prefab: WrasseCrewBodies is not on the root's component list")
    body = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % BODIES_ID, text)
    want(body and f"guid: {script_guid}, type: 3" in body.group(1),
         "Wrasse.prefab: the WrasseCrewBodies document is missing or names another script (no one would see the Wrasse)")
    want(f"  _shipGeometries:\n  - {{fileID: {GROUPER_GO_ID}}}\n" in text,
         "Wrasse.prefab: the ship geometry is not the Grouper (the domain colour would paint the hidden Sparrow)")
    want(SPARROW_SMR_OFF in text, "Wrasse.prefab: the Sparrow's mesh still draws (a Sparrow inside the Grouper)")
    return errs


def retrofit(text: str) -> str:
    """No buttons, and the Wrasse's own impactor container. Idempotent: run on the clone and on the committed prefab."""
    text = BINDINGS.sub(lambda m: m.group(1) + " []\n", text, count=1)
    text = strip_sparrow_kit(text)
    text = hud_retrofit(text)
    text = text.replace(f"guid: {SINGLE_STICK_TRANSFORMER_GUID},", f"guid: {TWO_STICK_TRANSFORMER_GUID},")
    text = tuning_retrofit(text)
    text = text.replace(f"guid: {DONOR_CONTAINER_GUID},", f"guid: {WRASSE_CONTAINER_GUID},")
    return bodies_retrofit(text, guid_of(BODIES_SCRIPT_META))


def container_retrofit(text: str) -> str:
    """The Sparrow's Omni ward is not the Wrasse's (its Omni crystal drops motes on the board). Idempotent."""
    return text.replace(f"  - {{fileID: 11400000, guid: {SPARROW_WARD_EFFECT_GUID}, type: 2}}\n", "")


def container_asset(donor: str) -> str:
    text = container_retrofit(donor)
    text = replace_once(text, "  m_Name: SparrowImpactorDataContainer\n", "  m_Name: WrasseImpactorDataContainer\n", "container name")
    m = re.search(r"  vesselCrystalEffects:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)*)", text)
    if not m:
        raise RuntimeError("donor drifted: the Sparrow container's vesselCrystalEffects list was not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {OMNI_EFFECT_GUID}, type: 2}}\n" + text[m.end(1):]


def omni_effect_asset(script_guid: str) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: KiteOmniCrystalEffect\n  m_EditorClassIdentifier: \n")


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the class asset
# The class card (a cleaner wrasse and its host: Garrett, 2026-10-18, "a good name based on a mutualistic pair").
DESCRIPTION = ("  Description: Two seats, one hull, like a cleaner wrasse and the grouper it keeps clean.\n"
               "    One player flies the Grouper, the hull; the other flies the Wrasse, a kite on the hull's\n"
               "    own plane - its head picks enemy prisms off, its tail lassos motes into blasts, prism\n"
               "    clusters, saved boosts and crystal rams. D-pad left or right swaps seats; the AI takes\n"
               "    the empty one.\n")
# The card names both subvessels (Garrett, 2026-10-18: "so its card will read wrasse and ___. pick a fish that the
# wrasse cleans"): SO_Vessel.CardName joins CrewNames with CrewNameTemplate.
CREW = "  CrewNames:\n  - Wrasse\n  - Grouper\n  CrewNameTemplate: '{0} and {1}'\n"


def class_asset(donor_class: str) -> str:
    text = replace_once(donor_class, "  m_Name: SO_Class_Sparrow\n", "  m_Name: SO_Class_Wrasse\n", "class m_Name")
    text = replace_once(text, f"  Class: {SPARROW_CLASS_ID}\n", f"  Class: {WRASSE_CLASS_ID}\n", "class Class")
    text = replace_once(text, "  Name: Sparrow\n", "  Name: Wrasse\n", "class Name")
    text, n = re.subn(r"  Description: .*\n(?:    .*\n)*", lambda m: DESCRIPTION + CREW, text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class Description was not found")
    text, n = re.subn(r"  isLocked: \d\n", "  isLocked: 0\n  ownedFromStart: 1\n", text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class isLocked line was not found")
    return text


def native_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\n"
            "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def register_class_list(text: str) -> str:
    if CLASS_GUID in text:
        return text
    m = re.search(r"  VesselList:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    if not m:
        raise RuntimeError("class list: VesselList not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {CLASS_GUID}, type: 2}}\n" + text[m.end(1):]


# ----------------------------------------------------------------------------- registrations
def register_container(text: str) -> str:
    if PREFAB_GUID in text:
        return text
    m = re.search(r"  _shipPrefabs:\n((?:  - \{fileID: -?\d+, guid: [0-9a-f]{32}, type: 3\}\n)+)", text)
    if not m:
        raise RuntimeError("Vessel Prefab Container: _shipPrefabs list not found")
    return text[:m.end(1)] + f"  - {{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}\n" + text[m.end(1):]


def register_network(text: str) -> str:
    if PREFAB_GUID in text:
        return text
    if not text.endswith("\n"):
        text += "\n"
    return text + ("  - Override: 0\n"
                   f"    Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}\n"
                   "    SourcePrefabToOverride: {fileID: 0}\n"
                   "    SourceHashToOverride: 0\n"
                   "    OverridingTargetPrefab: {fileID: 0}\n")


# The Wrasse's own cards (Garrett, 2026-10-18 round 4d: the icons show the navigator's loops). Input 0 is
# FullSpeedStraightAction, the fleet's "passive, no button" value, so no Sparrow button chip is drawn. The words are
# authored data the team edits in the inspector; this tool writes them once and then only holds the inputs at 0.
WRASSE_MAP_ENTRIES = [
    (3, "Space Blast", "The kite closes a loop of Space motes and a blast goes off where it closed; a bigger loop, a "
        "bigger blast. The card counts what the last blast destroyed.",
     "Shield Breaker", "Space blasts destroy shielded and super-shielded prisms."),
    (4, "Saved Boosts", "Every loop of Time motes the kite closes saves boosts for the pilot. The pilot spends them on "
        "the trigger; the card counts how many are saved.",
     "No Cooldown", "Saved boosts chain back to back with no cooldown."),
    (2, "Mass Cluster", "A loop of Mass motes lays a cluster of prisms on the kite's plane. The card flashes when one "
        "is laid.",
     "Shielded Clusters", "Mass clusters are laid shielded."),
    (1, "Kite Ram", "Loops of Charge motes power the kite's ram: strike a rival hull to knock its crystals loose, or "
        "strike wildlife. The card counts the rams held.",
     "Crystal Steal", "A ram steals the rival's crystals instead of knocking them loose."),
]


def yaml_str(text: str) -> str:
    return "'" + text.replace("'", "''") + "'"


def map_entries() -> str:
    out = "  entries:\n"
    for element, label, desc, up_label, up_desc in WRASSE_MAP_ENTRIES:
        out += (f"  - Element: {element}\n    AbilityLabel: {yaml_str(label)}\n    AbilityDescription: {yaml_str(desc)}\n"
                f"    Input: 0\n    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n"
                f"    UpgradeLabel: {yaml_str(up_label)}\n    UpgradeDescription: {yaml_str(up_desc)}\n")
    return out


def build_map(donor_map: str) -> str:
    text = replace_once(donor_map, "  m_Name: Sparrow\n", "  m_Name: Wrasse\n", "map name")
    text = replace_once(text, f"  vesselClass: {SPARROW_CLASS_ID}\n", f"  vesselClass: {WRASSE_CLASS_ID}\n", "map vesselClass")
    return text[:text.index("  entries:\n")] + map_entries()


def map_retrofit(text: str) -> str:
    """The Sparrow's cards become the Wrasse's once (the words are then the team's); every input is held at 0. Idempotent."""
    if "AbilityLabel: Pulsefire Cannons" in text:
        text = text[:text.index("  entries:\n")] + map_entries()
    return re.sub(r"^    Input: -?\d+\n", "    Input: 0\n", text, flags=re.M)


def fusion_entries(text: str, vessel: int):
    """Every `  - vessel: N` entry block of the fusion config's list, in order."""
    return re.findall(r"  - vessel: %d\n(?:    .*\n)+" % vessel, text)


def register_fusion(text: str) -> str:
    if fusion_entries(text, WRASSE_CLASS_ID):
        return text
    donor = fusion_entries(text, SPARROW_CLASS_ID)
    if len(donor) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Sparrow entries, found {len(donor)}")
    last = None
    for m in re.finditer(r"  - vessel: \d+\n(?:    .*\n)+", text):
        last = m
    added = "".join(e.replace(f"  - vessel: {SPARROW_CLASS_ID}\n", f"  - vessel: {WRASSE_CLASS_ID}\n", 1) for e in donor)
    return text[:last.end()] + added + text[last.end():]


# ----------------------------------------------------------------------------- checks
def check_prefab(text: str, script_guid: str, config_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want("  m_Name: Wrasse\n" in text, "Wrasse.prefab: the root is not named Wrasse")
    want("  _name: Wrasse\n" in text, "Wrasse.prefab: VesselStatus _name is not Wrasse")
    want(f"  vesselType: {WRASSE_CLASS_ID}\n" in text, f"Wrasse.prefab: vesselType is not {WRASSE_CLASS_ID}")
    want(f"  vesselType: {SPARROW_CLASS_ID}\n" not in text, "Wrasse.prefab: still carries the Sparrow's vesselType")
    h = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(h and int(h.group(1)) == global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "Wrasse.prefab: GlobalObjectIdHash is not this guid's (a copied hash collides with the donor in Netcode)")
    ip = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(ip and ip.group(1) == "0", "Wrasse.prefab: InScenePlacedSourceGlobalObjectIdHash is not cleared")
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    want(root and f"{{fileID: {SEAT_ID}}}" in root.group(1), "Wrasse.prefab: KiteNavigator is not on the root's component list")
    seat = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % SEAT_ID, text)
    want(seat and f"guid: {script_guid}" in seat.group(1), "Wrasse.prefab: the KiteNavigator document is missing or names another script")
    want(seat and f"  config: {{fileID: 11400000, guid: {config_guid}, type: 2}}" in seat.group(1),
         "Wrasse.prefab: KiteNavigator.config is not KiteNavigatorConfig.asset (a null config never seats the navigator)")
    want("  _inputEventShipActions: []\n" in text, "Wrasse.prefab: the hull still binds buttons (the pilot has none)")
    want(f"guid: {TWO_STICK_TRANSFORMER_GUID}, type: 3" in text and SINGLE_STICK_TRANSFORMER_GUID not in text,
         "Wrasse.prefab: the hull is not on the two-stick VesselTransformer (the pilot flies with two thumbs)")
    want(f"guid: {WRASSE_CONTAINER_GUID}," in text and f"guid: {DONOR_CONTAINER_GUID}," not in text,
         "Wrasse.prefab: the VesselImpactor is not on WrasseImpactorDataContainer (an Omni crystal would not drop motes)")
    names = set(re.findall(r"^  m_Name: (.*)$", text, re.M))
    for n in SPARROW_SUBTREES:
        want(n not in names, f"Wrasse.prefab: the Sparrow's {n} is still on the hull")
    for n in SPARROW_COMPONENTS + ["SparrowVesselTelemetry"]:
        want(f"guid: {guid_of_script(n)}, type: 3" not in text, f"Wrasse.prefab: the Sparrow's {n} is still on the hull")
    ai = re.search(r"guid: %s, type: 3\}\n((?:.*\n)*?)(?=--- !u!|\Z)" % guid_of_script("AIPilot"), text)
    want(ai and "  abilities: []\n" in ai.group(1),
         "Wrasse.prefab: the AIPilot still cycles abilities (the Sparrow's guns fire whenever the AI flies the hull)")
    hud = re.search(r"guid: %s, type: 3\}\n((?:.*\n)*?)(?=--- !u!|\Z)" % guid_of_script("WrasseHUDController"), text)
    want(hud and f"  navigator: {{fileID: {SEAT_ID}}}\n" in hud.group(1) and guid_of_script("SparrowHUDController") not in text,
         "Wrasse.prefab: the HUD is not WrasseHUDController wired to the seat (the ability cards would not show the counts)")
    dangling = dangling_local_refs(text)
    want(not dangling, f"Wrasse.prefab: dangling local fileID reference(s) {dangling[:5]}")
    return errs


def check_container(container: str, effect: str, omni_script_guid: str):
    errs = []
    if f"  - {{fileID: 11400000, guid: {OMNI_EFFECT_GUID}, type: 2}}\n" not in container.split("  vesselElementalCrystalEffects:")[0]:
        errs.append("WrasseImpactorDataContainer: KiteOmniCrystalEffect is not in its omni (vesselCrystalEffects) list")
    if f"guid: {omni_script_guid}, type: 3" not in effect:
        errs.append("KiteOmniCrystalEffect.asset: does not name KiteOmniCrystalEffectSO")
    if SPARROW_WARD_EFFECT_GUID in container:
        errs.append("WrasseImpactorDataContainer: still carries the Sparrow's Omni ward (SparrowVesselWardByCrystalEffect)")
    return errs


def check_registrations(container: str, network: str, lists, cls: str, kmap: str, fusion: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in container,
         "Vessel Prefab Container: no Wrasse entry on its root TRANSFORM (the Vessel Changer offers only what is here)")
    want(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in network,
         "DefaultNetworkPrefabs: no Wrasse entry on its root GAMEOBJECT (a swap to it cannot spawn)")
    for name, text in lists:
        want(text.count(CLASS_GUID) == 1, f"{name}: SO_Class_Wrasse listed {text.count(CLASS_GUID)} times, not once")
    want(f"  Class: {WRASSE_CLASS_ID}\n" in cls and "  ownedFromStart: 1\n" in cls and "  isLocked: 0\n" in cls,
         "SO_Class_Wrasse: wrong class, locked, or not owned from the start")
    want(CREW in cls, "SO_Class_Wrasse: the card does not name both subvessels (CrewNames Wrasse, Grouper)")
    want(f"  vesselClass: {WRASSE_CLASS_ID}\n" in kmap and "  m_Name: Wrasse\n" in kmap, "ElementalAbilityMaps/Wrasse: wrong class or name")
    inputs = re.findall(r"^    Input: (-?\d+)\n", kmap, re.M)
    want(len(inputs) == 4 and set(inputs) == {"0"},
         f"ElementalAbilityMaps/Wrasse: inputs {inputs} (the Wrasse has no buttons: every card is passive, Input 0)")
    want("Pulsefire" not in kmap and "Skyburst" not in kmap, "ElementalAbilityMaps/Wrasse: still the Sparrow's cards")
    kite, sparrow = fusion_entries(fusion, WRASSE_CLASS_ID), fusion_entries(fusion, SPARROW_CLASS_ID)
    want(len(kite) == 4, f"CrystalHullFusionConfig: {len(kite)} Wrasse entries, not 4 (CrystalHullFusionConfigTests needs one per element)")
    want([e.split("\n", 1)[1] for e in kite] == [e.split("\n", 1)[1] for e in sparrow],
         "CrystalHullFusionConfig: the Wrasse's entries are not the Sparrow's (it draws the Sparrow's mesh)")
    return errs


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print("wrote", os.path.relpath(path, ROOT))


def check_all():
    script_guid, config_guid = guid_of(SEAT_SCRIPT_META), guid_of(CONFIG_META)
    errs = []
    if not os.path.exists(PREFAB) or not os.path.exists(PREFAB + ".meta"):
        return ["Wrasse.prefab (+ .meta) is missing: run Tools/Build/author_wrasse_assets.py"]
    errs += [] if guid_of(PREFAB + ".meta") == PREFAB_GUID else ["Wrasse.prefab.meta: guid is not PREFAB_GUID"]
    errs += check_prefab(read(PREFAB), script_guid, config_guid)
    errs += check_bodies(read(PREFAB), guid_of(BODIES_SCRIPT_META))
    for path in (CLASS_ASSET, MAP):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    for path in (WRASSE_CONTAINER, OMNI_EFFECT):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    errs += check_container(read(WRASSE_CONTAINER), read(OMNI_EFFECT), guid_of(OMNI_SCRIPT_META))
    errs += check_registrations(read(CONTAINER), read(NETWORK_PREFABS),
                                [(os.path.basename(p), read(p)) for p in CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(FUSION))
    return errs


def author():
    script_guid, config_guid = guid_of(SEAT_SCRIPT_META), guid_of(CONFIG_META)
    if not os.path.exists(PREFAB):   # the spent one-shot stands down once the prefab is committed
        write(PREFAB, build_clone(read(DONOR), script_guid, config_guid))
        write(PREFAB + ".meta", prefab_meta())
    else:                            # the retrofits are not one-shots: they apply to the committed prefab
        old = read(PREFAB); new = retrofit(old)
        if new != old:
            write(PREFAB, new)
    if os.path.exists(WRASSE_CONTAINER):
        old = read(WRASSE_CONTAINER); new = container_retrofit(old)
        if new != old:
            write(WRASSE_CONTAINER, new)
    if not os.path.exists(OMNI_EFFECT):
        write(OMNI_EFFECT, omni_effect_asset(guid_of(OMNI_SCRIPT_META))); write(OMNI_EFFECT + ".meta", native_meta(OMNI_EFFECT_GUID))
    if not os.path.exists(WRASSE_CONTAINER):
        write(WRASSE_CONTAINER, container_asset(read(DONOR_CONTAINER))); write(WRASSE_CONTAINER + ".meta", native_meta(WRASSE_CONTAINER_GUID))
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset(read(DONOR_CLASS))); write(CLASS_ASSET + ".meta", native_meta(CLASS_GUID))
    if not os.path.exists(MAP):
        write(MAP, build_map(read(DONOR_MAP))); write(MAP + ".meta", native_meta(MAP_GUID))
    else:
        old = read(MAP); new = map_retrofit(old)
        if new != old:
            write(MAP, new)
    for path, fn in [(CONTAINER, register_container), (NETWORK_PREFABS, register_network), (FUSION, register_fusion)] + \
                    [(p, register_class_list) for p in CLASS_LISTS]:
        old = read(path); new = fn(old)
        if new != old:
            write(path, new)


def self_test():
    """Every check must fire on a broken copy of the shipped set (negative controls), and pass on it intact."""
    script_guid, config_guid = guid_of(SEAT_SCRIPT_META), guid_of(CONFIG_META)
    good = build_clone(read(DONOR), script_guid, config_guid)
    fails = []
    def expect(errs, n_min, what):
        if not ((len(errs) == 0) if n_min == 0 else (len(errs) >= n_min)):
            fails.append(f"{what}: {errs}")
    expect(check_prefab(good, script_guid, config_guid), 0, "a fresh clone passes")
    expect(check_prefab(good.replace(f"  vesselType: {WRASSE_CLASS_ID}\n", f"  vesselType: {SPARROW_CLASS_ID}\n"), script_guid, config_guid), 1, "the Sparrow's vesselType")
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>1124080984", good, count=1), script_guid, config_guid), 1, "the donor's hash")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {SEAT_ID}}}\n", ""), script_guid, config_guid), 1, "the seat off the root")
    expect(check_prefab(good.replace(f"guid: {config_guid}, type: 2", "guid: 00000000000000000000000000000000, type: 2"), script_guid, config_guid), 1, "the seat's config unwired")
    expect(check_prefab(good.replace("  _inputEventShipActions: []\n", "  _inputEventShipActions:\n  - InputEvent: 1\n    ShipActions: []\n"), script_guid, config_guid), 1, "a button bound")
    expect(check_prefab(good.replace(WRASSE_CONTAINER_GUID, DONOR_CONTAINER_GUID), script_guid, config_guid), 1, "the Sparrow's container")
    expect(check_prefab(good.replace(guid_of_script("WrasseHUDController"), guid_of_script("SparrowHUDController")), script_guid, config_guid), 1, "the Sparrow's HUD controller")
    expect(check_prefab(good.replace(TWO_STICK_TRANSFORMER_GUID, SINGLE_STICK_TRANSFORMER_GUID), script_guid, config_guid), 1, "the one-thumb transformer")
    expect(check_prefab(read(DONOR).replace("  m_Name: Sparrow\n", "  m_Name: Wrasse\n"), script_guid, config_guid), 3, "the Sparrow's kit, untouched")
    anchor = DOC.search(good).group(2)
    expect(check_prefab(good + f"  stray: {{fileID: 987654321}}\n", script_guid, config_guid), 1, "a dangling local reference")
    if strip_sparrow_kit(good) != good:
        fails.append("the Sparrow-kit strip is not idempotent")
    assert anchor
    if "\n  RollScaler: 120\n" not in good:
        fails.append("a fresh clone does not get the Wrasse's turn rates")
    tuned = good.replace("\n  RollScaler: 120\n", "\n  RollScaler: 95\n")
    if tuning_retrofit(tuned) != tuned:
        fails.append("the turn-rate retrofit overwrote a value tuned after it")
    if retrofit(good) != good:
        fails.append("the retrofit is not idempotent")
    bodies_guid = guid_of(BODIES_SCRIPT_META)
    expect(check_bodies(good, bodies_guid), 0, "a fresh clone carries both bodies")
    expect(check_bodies(good.replace(SPARROW_SMR_OFF, ""), bodies_guid), 1, "the Sparrow's mesh left drawing")
    expect(check_bodies(good.replace(f"  - component: {{fileID: {BODIES_ID}}}\n", ""), bodies_guid), 1, "the bodies off the root")
    expect(check_bodies(good.replace(f"  - {{fileID: {GROUPER_GO_ID}}}\n", f"  - {{fileID: {SPARROW_MODEL_GO}}}\n"), bodies_guid), 1,
           "the Sparrow still the ship geometry")
    expect(check_bodies(good.replace("      value: WrasseKite\n", "      value: Kite\n"), bodies_guid), 1, "the kite model renamed")
    expect(check_bodies(good.replace(KITE_FBX_GUID, "0" * 32), bodies_guid), 1, "the kite model not nested")
    omni_script = guid_of(OMNI_SCRIPT_META)
    kc, oe = container_asset(read(DONOR_CONTAINER)), omni_effect_asset(omni_script)
    expect(check_container(kc, oe, omni_script), 0, "a Wrasse container passes")
    expect(check_container(read(DONOR_CONTAINER), oe, omni_script), 1, "the Sparrow's container")
    expect(check_container(kc, oe.replace(omni_script, "0" * 32), omni_script), 1, "the effect names another script")
    container, network, fusion = register_container(read(CONTAINER)), register_network(read(NETWORK_PREFABS)), register_fusion(read(FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in CLASS_LISTS]
    cls, kmap = class_asset(read(DONOR_CLASS)), build_map(read(DONOR_MAP))
    expect(check_registrations(container, network, lists, cls, kmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, kmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, kmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, kmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls.replace("  isLocked: 0\n", "  isLocked: 1\n"), kmap, fusion), 1, "a locked class")
    expect(check_registrations(container, network, lists, cls.replace(CREW, ""), kmap, fusion), 1, "a card without its crew names")
    expect(check_registrations(container, network, lists, cls, kmap, fusion.replace(f"  - vessel: {WRASSE_CLASS_ID}\n", "  - vessel: 99\n", 1)), 1, "a missing fusion entry")
    expect(check_registrations(container, network, lists, cls, read(DONOR_MAP), fusion), 1, "the Sparrow's own map")
    expect(check_registrations(container, network, lists, cls, kmap.replace("    Input: 0\n", "    Input: 7\n", 1), fusion), 1, "a card with a button")
    if map_retrofit(kmap) != kmap:
        fails.append("the map retrofit is not idempotent")
    if register_container(container) != container or register_fusion(fusion) != fusion or register_network(network) != network:
        fails.append("registration is not idempotent")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_wrasse_assets self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errs = check_all()
    for e in errs:
        print("FAIL:", e)
    print("author_wrasse_assets:", "ok" if not errs else f"{len(errs)} problem(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
