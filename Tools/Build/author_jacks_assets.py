#!/usr/bin/env python3
"""Author the Jacks (two jacks that herd real fauna, one player: thumbs run them on the board, or they fly the hull with the
fleet's two-thumb flight) and register them, then prove it.

    python3 Tools/Build/author_jacks_assets.py             # author what is missing, then check
    python3 Tools/Build/author_jacks_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_jacks_assets.py --self-test # prove every check fires

"Bring it to Unity" means the Toybox's Vessel Changer offers it (Garrett, 2026-10-17). The changer offers what
ToyVesselRoster.Default names AND the Vessel Prefab Container can spawn, so the jacks ship as their own hull class
(author_wrasse_assets.py is the pattern; this tool reuses its YAML helpers).

This was "Marlin and Jacks" (author_marlin_assets.py) until Garrett reduced it to the jacks on 2026-10-21 ("This allows us to
bring in a rapier stabbing character at a later date and save the marlin as a proper swordsman"). The marlin's MODEL
(Marlin.fbx, specs/marlin.py, Docs/VesselModels/Marlin.md) stays in the repo for that swordsman; nothing here draws it.

WHAT IT MAKES (the design: Assets/_Scripts/Controller/Vessel/R_VesselActions/JACKS.md)
    Assets/_Prefabs/Spacevessels/Jacks.prefab (+ .meta)   a text clone of Rhino.prefab with:
        - the root GameObject and VesselStatus named Jacks, `vesselType: 22` (VesselClassType.Jacks)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
        - JacksExecutor on the ShipActions object and in its ActionExecutorRegistry, wired to JacksConfig.asset, the
          fleet's turn-end channel and Event_CombatHitStats (the Angler's assets)
        - the netting jacks' buttons: no ramp boost (stripped 2026-10-25, its registry too); the JUMP to the decoy on Input 1 (the trigger
          / a lifted thumb) and Input 4 (both thumbs lifted); CALL on Input 7 (A); the jacks' own DECOY on Input 6 (X; the
          Time level-5 upgrade). The Rhino's gamepad overrides (the shield swipes) are emptied
        - NO SWORD: the sword driver and its afterimages (ScaleSkimmerObject), the shield swipes (ShieldSwipeActionRegistry),
          the sword relay, the sword autopilot, the Momentum ward, the sword's look (RhinoSwordFXController) and its five
          streaks are stripped (RHINO_SUBTREES, RHINO_COMPONENTS). The ForceFieldSkimmer stays: it is the hull's skimmer,
          made THE JACK (Garrett, 2026-10-23: three orthogonal axes spun about (1,1,1) on the line between the fish) - two
          more trigger capsules (X, Z) beside its own Y one (jack_retrofit); JacksBodies poses and draws it
        - NO JETS: the Rhino's vestigial jets (the ten VesselJets, JetFX, the arc lightning, JetTest, the two jet particle
          systems; RHINO_JET_PREFABS, RHINO_SUBTREES) are stripped (Garrett, 2026-10-23)
        - RhinoVesselHUDController becomes JacksHUDController, wired to the executor
        - its VesselImpactor on JacksImpactorDataContainer: haptics, the danger debuff and the prism damage; the Rhino's
          plough and its surge / explosion crystals are the Rhino's alone
        - THE BODIES: two Jack.fbx under the root ('Jack A', 'Jack B'; /vessel-model spec jack.py), drawn, swum and
          replicated by JacksBodies on the root. The Rhino's rig is switched off (its bones stay: the puppetry still turns
          them, and the hull's colliders are its parts'), and the ship geometry is the Rhino's fuselage part (its collider;
          the jacks paint themselves). A committed prefab that still nests the marlin's model has it removed (drop_marlin)
        - the ability row's four icons (author_jacks_icons.py) over the Rhino's sword placeholders
        - its own JacksCameraSettingsSO (the Rhino's numbers at first) and DefaultVesselTelemetry with its stat channels
      Everything else is the Rhino's: its colliders, its two sticks, its ramp.
      Unlike the clone, the bindings, the strip, the HUD swap, the bodies and the rename are RETROFITS: applied to the
      committed prefab too.
    JacksConfig.asset and the three controls (Assets/_SO_Assets/VesselActions/Jacks/);
    SO_Class_Jacks ("Jacks", its card rendered from Jack.fbx, owned from the start) + its entry in SO_Classlist_All /
    _Classes; one entry each in `Vessel Prefab Container.asset` (the root TRANSFORM) and `DefaultNetworkPrefabs.asset`
    (the root GAMEOBJECT); Resources/ElementalAbilityMaps/Jacks.asset; and four CrystalHullFusionConfig entries (the
    Rhino's: they still bake the Rhino's hull shape until a bake is made in the editor). ToyVesselRoster.Default is CODE
    and is not written here.

THE CLONE IS A SPENT ONE-SHOT ONCE THE PREFAB IS COMMITTED (CLAUDE.md): it runs only while Jacks.prefab is absent, and
`--check` validates the SHIPPED prefab's invariants. To re-author from scratch, delete Jacks.prefab and its .meta.
"""
import hashlib
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402
from author_wrasse_assets import (DOC, dangling_local_refs, guid_of, guid_of_script, native_meta,  # noqa: E402
                                  read, split_docs, write, yaml_str, fusion_entries)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Rhino.prefab")
PREFAB = os.path.join(VESSELS, "Jacks.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
DONOR_CLASS = os.path.join(CLASSES, "SO_Class_Rhino.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Jacks.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
DONOR_MAP = os.path.join(MAPS, "Rhino.asset")
MAP = os.path.join(MAPS, "Jacks.asset")
FUSION = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
ACTIONS = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "Jacks")
CONFIG = os.path.join(ACTIONS, "JacksConfig.asset")
CONTAINERS = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Effect Containers", "VesselContainers")
JACKS_CONTAINER = os.path.join(CONTAINERS, "JacksImpactorDataContainer.asset")

# 20 is the Orca's and 21 is claimed twice in flight (the Honeyguide and the Starlings, PR #1305). Checked across every
# origin branch 2026-10-21.
JACKS_CLASS_ID = 22
RHINO_CLASS_ID = 3
PREFAB_GUID = "5d7f0a3c9e2b4f61a8c47e1b2d9f6a03"
CLASS_GUID = "8a41c6e2f0d94b7c9e35a1d7c4b8e2f6"
MAP_GUID = "c3e9a7f15b2d4e08a6f1d9c2b7e4a530"
CONFIG_GUID = "e17b4c9a2d6f4835b0a9c3e1f7d2b648"
# the jacks' own camera settings: the Rhino's numbers at first (the hull flies on the Rhino's rig), its own asset so the team
# tunes the jacks' chase camera without moving the Rhino's
CAMERA_DIR = os.path.join(ROOT, "Assets", "_SO_Assets", "Camera")
RHINO_CAMERA = os.path.join(CAMERA_DIR, "RhinoCameraSettingsSO.asset")
CAMERA = os.path.join(CAMERA_DIR, "JacksCameraSettingsSO.asset")
RHINO_CAMERA_GUID = "6f9693afb46548dd83870cfb0b1ce939"
CAMERA_GUID = "b84e1d7a3c2f4a9e8d5b6c0f1a3e7d29"
CONTAINER_GUID = "9b2e6d4f1a7c4e3890d5b8a2c6f1e473"
CONTROLS = [  # (asset name, JacksControl value, guid)
    ("JacksCall", 0, "4f8a2c6e9b1d4a7380e5c2f9d6b1a734"),
    ("JacksTeleport", 1, "a6d1e9b3c7f24058b9e2a4d6c1f8b357"),
    ("JacksDecoy", 2, "2c7e5a9f3b1d4c68a0f7e2b9d4c6a185"),
]
CONTROL = {name: guid for name, _, guid in CONTROLS}
DONOR_CONTAINER_GUID = "212ff1ef2b8c43c4ab1acfc073c79e0b"   # RhinoImpactorDataContainer
RAMP_BOOST_GUID = "cb9bc8a3724a4a67a3656d7df507b2cb"        # RhinoRampBoostAction
TURN_END_GUID = "498a06d44bde9184f985c938c803b2a1"          # the fleet's OnMiniGameTurnEnd (as the Angler wires it)
COMBAT_HIT_GUID = "45fb43a0cddb5cc3cc4dc448df994152"        # Event_CombatHitStats
COMPANION_FAUNA_GUID = "0e87f3117e7141a7b5bce66b0385a2d0"   # the Angler's companion species
ROOT_GO = "6417075533431866457"              # the Rhino's root GameObject
ROOT_TRANSFORM_ID = "5928382658856804067"
NETWORK_OBJECT_ID = "3711687605623876765"
SHIP_ACTIONS_GO = "1916516311732452555"      # the ShipActions child (its ActionExecutorRegistry)
REGISTRY_ID = "5397651131628914846"
EXECUTOR_ID = "2200000000000000001"          # JacksExecutor on ShipActions

# The flying jacks' buttons (InputEvents: 1 RightStickAction, 4 IdleAction, 6 Button1, 7 Button2). Input 0 (FullSpeedStraight)
# carried the Rhino's ramp boost until Garrett, 2026-10-25: "lets remove the rhino speed buff at full speed and straight".
BINDINGS = re.compile(r"(  _inputEventShipActions:)\n(?:  [- ] .*\n)+")
OVERRIDES = re.compile(r"(  _gamepadActionOverrides:)\n(?:  [- ] .*\n)+")
WANT_BINDINGS = (
    "  _inputEventShipActions:\n"
    f"  - InputEvent: 1\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL['JacksTeleport']}, type: 2}}\n"
    f"  - InputEvent: 4\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL['JacksTeleport']}, type: 2}}\n"
    f"  - InputEvent: 6\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL['JacksDecoy']}, type: 2}}\n"
    f"  - InputEvent: 7\n    ShipActions:\n    - {{fileID: 11400000, guid: {CONTROL['JacksCall']}, type: 2}}\n")

# the sword's five streaks and the legacy jet objects are Rhino parts too (Garrett, 2026-10-23: "the vestigal jets from the
# rhino should be gone"; "instead of a sword (1 axis) make it all three orthogonal axes")
# ... and the Rhino's ramp speed boost (Garrett, 2026-10-25: "lets remove the rhino speed buff at full speed and straight"): its
# executor's registry goes, so neither the button nor the AI's boost policy (RampBoostAIPolicySO) can arm it
RHINO_SUBTREES = ["ScaleSkimmerObject", "ShieldSwipeActionRegistry", "LeftJetParticle", "RightJetParticle", "JetTest",
                  "RampBoostActionRegistry"] + [
    f"RhinoSwordBladeTracer{i}" for i in range(5)]
RHINO_COMPONENTS = ["RhinoSwordNetworkRelay", "VesselElementalImmunity", "RhinoSwordAutopilot", "RhinoSwordFXController"]
# the Rhino's nested jets: the ten VesselJets (two pods, eight body nozzles), the pods' JetFX and the arc lightning
RHINO_JET_PREFABS = {"55ceca573be662744948afc745f8840b": "VesselJet", "f2d613e7ff9874744baa70c5dc697310": "JetFX",
                     "304ca7b3152755c4885ea01bcc7183a6": "VfxArcLightning"}
# THE SKIMMER: the sword's capsule, hidden, a small near-field sphere at the hull (JacksBodies). It was THE JACK for a day
# (2026-10-23: two more capsules on it, X and Z, spun about (1,1,1)); Garrett, 2026-10-24: "the spinning jack in the middle
# doesn't work. lets make it a net" - the net is JacksNet's, drawn by JacksBodies, and the two arm colliders come off
# (net_retrofit). Their ids are retired: never reuse them.
SKIMMER_INSTANCE = "1660592348679971898"           # ForceFieldSkimmer Variant nested under the fuselage
SKIMMER_GO = "2695624631939139826"                 # its root GameObject (stripped)
SKIMMER_VARIANT_GUID = "c8d5df1098821194eb69eec4c965548e"
SKIMMER_SOURCE_GO = "3630837686170528968"          # the variant's root GameObject, the added components' target
SKIMMER_Y_COLLIDER = "4880931740909781519"         # the sword's own capsule (direction 1)
RETIRED_ARM_COLLIDERS = ("2200000000000000061", "2200000000000000062")   # the jack's X and Z arms (2026-10-23 only)
# the omni crystal produces an omnivorous worm in our domain (Garrett, 2026-10-24): one VesselSpawnFaunaByCrystalEffectSO in
# the Jacks' container, naming the worm colony
CRYSTAL_EFFECTS = os.path.join(ROOT, "Assets", "_SO_Assets", "Effects", "Vessel Crystal Effects")
WORM_EFFECT = os.path.join(CRYSTAL_EFFECTS, "JacksOmniWormByCrystalEffect.asset")
WORM_EFFECT_GUID = "7c2e9b4d1f6a4e38a5d0b3c8e1f7a924"
WORM_FAUNA_GUID = "eca04f08bc0448c2a0150f75ac23e83f"   # WormColonyFaunaConfig ("Worm Colony Kaiju"): eats prisms and creatures
RHINO_HUD_KEYS = ["view", "swordDriver", "afterimage", "speedWard"]


# ----------------------------------------------------------------------------- the prefab
def executor_block(script_guid: str) -> str:
    return (f"--- !u!114 &{EXECUTOR_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {SHIP_ACTIONS_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n"
            f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}\n"
            f"  OnMiniGameTurnEnd: {{fileID: 11400000, guid: {TURN_END_GUID}, type: 2}}\n"
            f"  onCombatHitLanded: {{fileID: 11400000, guid: {COMBAT_HIT_GUID}, type: 2}}\n"
            "  jackColor: {r: 0.949, g: 0.784, b: 0.294, a: 1}\n  decoyColor: {r: 0.941, g: 0.627, b: 0.263, a: 1}\n"
            "  dangerColor: {r: 1, g: 0.353, b: 0.235, a: 1}\n"
            "  ringColor: {r: 0.949, g: 0.784, b: 0.294, a: 0.55}\n")


def component_list(text: str, go: str):
    return re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % go, text)


def build_clone(donor: str, script_guid: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Rhino\n", "  m_Name: Jacks\n", "root GameObject name")
    text = replace_once(text, "  _name: Rhino\n", "  _name: Jacks\n", "VesselStatus _name")
    text = replace_once(text, f"  vesselType: {RHINO_CLASS_ID}\n", f"  vesselType: {JACKS_CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)
    if not text.endswith("\n"):
        text += "\n"
    return retrofit(text + executor_block(script_guid))


def strip_rhino_kit(text: str) -> str:
    """The sword and its parts off the jacks' hull (see the module doc). Idempotent."""
    head, docs = split_docs(text)
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
        if name in RHINO_SUBTREES:
            take(go)
    kill_scripts = {guid_of_script(n) for n in RHINO_COMPONENTS}
    removed = set(gos) | {transform_of[g] for g in gos if g in transform_of}
    for k, a, d in docs:
        go = re.search(r"^  m_GameObject: \{fileID: (-?\d+)\}", d, re.M)
        if go and go.group(1) in gos:
            removed.add(a)
        sc = re.search(r"^  m_Script: \{fileID: 11500000, guid: (\w+)", d, re.M)
        if k == "114" and sc and sc.group(1) in kill_scripts:
            removed.add(a)
    # the nested jets: their PrefabInstance and its stripped stubs
    jets = {a for k, a, d in docs if k == "1001" and any(f"m_SourcePrefab: {{fileID: 100100000, guid: {g}," in d
                                                          for g in RHINO_JET_PREFABS)}
    for k, a, d in docs:
        pi = re.search(r"^  m_PrefabInstance: \{fileID: (-?\d+)\}", d, re.M)
        if a in jets or (" stripped" in d.split("\n", 1)[0] and pi and pi.group(1) in jets):
            removed.add(a)
    out = []
    for k, a, d in docs:
        if a in removed:
            continue
        for r in removed:
            d = re.sub(r"^  - component: \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"^  - \{fileID: %s\}\n" % r, "", d, flags=re.M)
            d = re.sub(r"\{fileID: %s\}" % r, "{fileID: 0}", d)
        # an added component that was removed leaves its entry naming nothing
        d = re.sub(r"^    - targetCorrespondingSourceObject: \{[^}]*\}\n      insertIndex: -?\d+\n      addedObject: \{fileID: 0\}\n",
                   "", d, flags=re.M)
        out.append(d)
    text = head + "".join(out)
    return re.sub(r"^(  (?:m_Children|_executors|m_Component)):\n(?=  [a-zA-Z_<]|--- |\Z)", r"\1: []\n", text, flags=re.M)


def net_retrofit(text: str) -> str:
    """Take the jack's two arm colliders off the sword skimmer (the net replaced the jack, 2026-10-24): their documents and
    their entries in the skimmer's added components. Idempotent."""
    head, docs = split_docs(text)
    if not any(a in RETIRED_ARM_COLLIDERS for _, a, _ in docs):
        return text
    text = head + "".join(d for _, a, d in docs if a not in RETIRED_ARM_COLLIDERS)
    for cid in RETIRED_ARM_COLLIDERS:
        text = re.sub(r"^    - targetCorrespondingSourceObject: \{[^}]*\}\n      insertIndex: -?\d+\n      addedObject: \{fileID: %s\}\n"
                      % cid, "", text, flags=re.M)
    return text


def check_jack(text: str):
    """No Rhino jet, no sword streak, and the sword skimmer is one trigger capsule (the net is code, not colliders)."""
    errs = []
    for guid, name in RHINO_JET_PREFABS.items():
        if f"m_SourcePrefab: {{fileID: 100100000, guid: {guid}," in text:
            errs.append(f"Jacks.prefab: the Rhino's {name} is still on the hull (the vestigial jets)")
    inst = re.search(r"--- !u!1001 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % SKIMMER_INSTANCE, text)
    cap = re.search(r"--- !u!136 &%s\nCapsuleCollider:\n((?:.*\n)*?)(?=--- !u!|\Z)" % SKIMMER_Y_COLLIDER, text)
    if not (cap and f"  m_GameObject: {{fileID: {SKIMMER_GO}}}\n" in cap.group(1) and "  m_IsTrigger: 1\n" in cap.group(1)
            and inst and f"addedObject: {{fileID: {SKIMMER_Y_COLLIDER}}}" in inst.group(1)):
        errs.append("Jacks.prefab: the sword skimmer has lost its trigger capsule (the hull's near-field skimmer)")
    for cid in RETIRED_ARM_COLLIDERS:
        if f"&{cid}\n" in text or f"{{fileID: {cid}}}" in text:
            errs.append(f"Jacks.prefab: the retired jack's arm collider {cid} is still on the skimmer (run the tool)")
    if inst and re.search(r"addedObject: \{fileID: 0\}", inst.group(1)):
        errs.append("Jacks.prefab: the sword skimmer lists an added component that names nothing")
    return errs


def executor_retrofit(text: str) -> str:
    """The executor on ShipActions' component list and in its registry. Idempotent."""
    m = component_list(text, SHIP_ACTIONS_GO)
    if m and f"{{fileID: {EXECUTOR_ID}}}" not in m.group(1):
        text = text[:m.end(1)] + f"  - component: {{fileID: {EXECUTOR_ID}}}\n" + text[m.end(1):]
    reg = re.search(r"(--- !u!114 &%s\n(?:.*\n)*?)  _executors:(?: \[\])?\n((?:  - \{fileID: -?\d+\}\n)*)" % REGISTRY_ID, text)
    if reg and f"  - {{fileID: {EXECUTOR_ID}}}\n" not in reg.group(2):
        text = text[:reg.start()] + reg.group(1) + "  _executors:\n" + reg.group(2) + f"  - {{fileID: {EXECUTOR_ID}}}\n" + text[reg.end():]
    return text


def hud_retrofit(text: str) -> str:
    """RhinoVesselHUDController (the sword) becomes JacksHUDController, wired to the executor. Idempotent."""
    rhino_hud, jacks_hud = guid_of_script("RhinoVesselHUDController"), guid_of_script("JacksHUDController")
    head, docs = split_docs(text)
    out = []
    for _, _, d in docs:
        if f"guid: {rhino_hud}, type: 3" in d:
            d = d.replace(f"guid: {rhino_hud}, type: 3", f"guid: {jacks_hud}, type: 3")
            for key in RHINO_HUD_KEYS:
                d = re.sub(r"^  %s: \{[^}]*\}\n" % key, "", d, flags=re.M)
            d = d.rstrip("\n") + f"\n  crew: {{fileID: {EXECUTOR_ID}}}\n"
        out.append(d)
    return head + "".join(out)


# ----------------------------------------------------------------------------- the bodies (Garrett, 2026-10-21: "lets give it
# some models and get it ready to ship"): two Jack.fbx (/vessel-model, specs/jack.py). The marlin's model was nested here
# until the jacks were made the whole vessel (2026-10-21); drop_marlin takes it out of a prefab that still carries it.
MARLIN_FBX_GUID = "6d2b8e4f0a9c4e17b3f5a8c1d2e7b904"
JACK_FBX_GUID = "3a8f1c6e2b5d4e79a0c4f7b1e9d2a658"
RHINO_RIG_GUID = "350f1f00a1b1c4041b6339bbc7504694"
RHINO_RIG_INSTANCE = "4710562938102847551"       # the Rhino's rig, nested under the fuselage part
FUSE_TRANSFORM = "5169072473205323491"           # 'Rhino_Test (1)': the part RhinoAnimation turns as the body
FUSE_GO = "5546161687949175897"                  # its GameObject (and its collider): the ship geometry
MODEL_ROOT_GO = "919132149155446097"             # a model's root GameObject / Transform: constants of the importer
MODEL_ROOT_TRANSFORM = "-8679921383154817045"
# the marlin model's nesting, taken out by drop_marlin (never reuse these ids)
MARLIN_INSTANCE_ID, MARLIN_TRANSFORM_ID, MARLIN_GO_ID = "2200000000000000011", "2200000000000000012", "2200000000000000013"
JACK_IDS = (("Jack A", "2200000000000000021", "2200000000000000022"), ("Jack B", "2200000000000000031", "2200000000000000032"))
BODIES_ID = "2200000000000000041"                # JacksBodies on the root
# the ability row's four icons (author_jacks_icons.py), over the Rhino's sword placeholders the HUD variant draws
HUD_INSTANCE = "2754590152818896375"            # RhinoHUDVariant nested in the hull
HUD_VARIANT_GUID = "2656b477a4af7304180e65f2848a84b3"
ICON_SPRITES = (("7720000000000003015", "c3a91e5d7b2f4c68a0e4d1b9f6c2a873"),   # ChargeIcon: Danger Share
                ("7720000000000003025", "5e8b2d6a1f9c4b37a2d0e7c4b1f9a645"),   # MassIcon: Breeding
                ("7720000000000003035", "9f1c4a7e3b6d4e82b5a0c8d2e7f1b396"),   # SpaceIcon: Herding Ring
                ("7720000000000003045", "2b7d5f9a4c1e4a63b8f2d0e6a9c5b718"))   # TimeIcon: Herd Speed


def icon_override(image_id: str, sprite_guid: str) -> str:
    return (f"    - target: {{fileID: {image_id}, guid: {HUD_VARIANT_GUID},\n        type: 3}}\n      propertyPath: m_Sprite\n"
            f"      value: \n      objectReference: {{fileID: 21300000, guid: {sprite_guid}, type: 3}}\n")


def icons_retrofit(text: str) -> str:
    """The HUD's four icons are the jacks'. Idempotent."""
    m = re.search(r"--- !u!1001 &%s\nPrefabInstance:\n(?:.*\n)*?    m_Modifications:\n" % HUD_INSTANCE, text)
    if not m:
        return text
    add = "".join(icon_override(i, g) for i, g in ICON_SPRITES if icon_override(i, g) not in text)
    return text[:m.end()] + add + text[m.end():]


TELEMETRY_ID = "2200000000000000051"             # DefaultVesselTelemetry on the root, its stat channels wired
# the stat channels every vessel's telemetry fires (the Wrasse's: longest drift, max boost time, prisms damaged). Without the
# component on the prefab VesselTelemetryBootstrapper adds one at runtime with these unwired, and warns.
TELEMETRY_FIELDS = ("  longestDriftStat: {fileID: 11400000, guid: c9ca3d6eff04d70468aa24ee5b1f561d, type: 2}\n"
                    "  maxBoostTimeStat: {fileID: 11400000, guid: be1a9b6401dca924585cd31ff1034c6f, type: 2}\n"
                    "  prismsDamagedStat: {fileID: 11400000, guid: 3ea71b2eaf0cdbb46a99ddaf04b2d999, type: 2}\n"
                    "  boostMultiplierThreshold: 1.5\n")
RIG_OFF = (f"    - target: {{fileID: {MODEL_ROOT_GO}, guid: {RHINO_RIG_GUID},\n        type: 3}}\n"
           "      propertyPath: m_IsActive\n      value: 0\n      objectReference: {fileID: 0}\n")
# JacksBodies' serialized fields at the C# initializers, written once (the team tunes them in the inspector after)
BODIES_FIELDS = (
    "  replacedModelName: rhino_shapekey_with_animations\n  jackModelNames:\n  - Jack A\n  - Jack B\n  jackModelScale: 2.5\n"
    "  flightModelScale: 1\n  modelScaleEase: 4\n  jackModelEuler: {x: 0, y: 0, z: 0}\n  jackSwimHz: 1\n"
    "  jackSwimHzPer100: 0.8\n  flightSwimSpeedCap: 200\n  jackSwimDegrees: {x: 6, y: 12, z: 22}\n  jackSwimLagBeats: 0.18\n"
    "  jackFinFlapDegrees: 22\n  jackTurnRate: 8\n  jackHeadingMinSpeed: 6\n  remoteSmoothing: 10\n")
# MarlinCrewBodies' fields, replaced once by JacksBodies' (a body block that still has them was never tuned as the jacks)
OLD_BODIES_FIELDS = re.compile(r"  marlinModelName: .*\n(?:  (?!m_)[\w]+:.*\n|  - .*\n)+")


def model_instance(instance_id: str, transform_id: str, parent: str, fbx_guid: str, name: str, z: str = "0",
                   go_id: str = None) -> str:
    """A model nested in its own frame (the fleet's: nose +Z, up +Y) under `parent`, `z` along the parent's nose."""
    mods = [(MODEL_ROOT_GO, "m_Name", name)] + [(MODEL_ROOT_TRANSFORM, k, v) for k, v in (
        ("m_LocalPosition.x", "0"), ("m_LocalPosition.y", "0"), ("m_LocalPosition.z", z), ("m_LocalRotation.w", "1"),
        ("m_LocalRotation.x", "0"), ("m_LocalRotation.y", "0"), ("m_LocalRotation.z", "0"),
        ("m_LocalEulerAnglesHint.x", "0"), ("m_LocalEulerAnglesHint.y", "0"), ("m_LocalEulerAnglesHint.z", "0"))]
    out = (f"--- !u!1001 &{instance_id}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
           f"  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {{fileID: {parent}}}\n"
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


def telemetry_block() -> str:
    return (f"--- !u!114 &{TELEMETRY_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid_of_script('DefaultVesselTelemetry')}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n" + TELEMETRY_FIELDS)


def bodies_block(script_guid: str) -> str:
    return (f"--- !u!114 &{BODIES_ID}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {ROOT_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: \n  m_EditorClassIdentifier: \n  ShowTopMostFoldoutHeaderGroup: 1\n" + BODIES_FIELDS)


def add_children(text: str, transform_id: str, children) -> str:
    m = re.search(r"(--- !u!4 &%s\nTransform:\n(?:.*\n)*?  m_Children:)( \[\])?\n((?:  - \{fileID: -?\d+\}\n)*)" % transform_id, text)
    if not m:
        return text
    add = "".join(f"  - {{fileID: {t}}}\n" for t in children if f"  - {{fileID: {t}}}\n" not in m.group(3))
    return text[:m.start()] + m.group(1) + "\n" + m.group(3) + add + text[m.end():]


def bodies_retrofit(text: str) -> str:
    """The two jacks as models, and no marlin. Idempotent: every step looks before it writes."""
    script_guid = guid_of_script("JacksBodies")
    text = drop_marlin(text)
    for name, iid, tid in JACK_IDS:
        if f"--- !u!1001 &{iid}\n" not in text:
            text += model_instance(iid, tid, ROOT_TRANSFORM_ID, JACK_FBX_GUID, name)
    if f"--- !u!114 &{BODIES_ID}\n" not in text:
        text += bodies_block(script_guid)
    if f"--- !u!114 &{TELEMETRY_ID}\n" not in text:
        text += telemetry_block()
    for cid in (BODIES_ID, TELEMETRY_ID):
        m = component_list(text, ROOT_GO)
        if m and f"{{fileID: {cid}}}" not in m.group(1):
            text = text[:m.end(1)] + f"  - component: {{fileID: {cid}}}\n" + text[m.end(1):]
    text = add_children(text, ROOT_TRANSFORM_ID, [tid for _, _, tid in JACK_IDS])
    # the jacks are the drawn hull (they paint themselves), and the Rhino's rig is switched off (its bones stay and
    # the puppetry still turns them; the hull's colliders are the fuselage's and the plates' own)
    text = text.replace(f"  _shipGeometries:\n  - {{fileID: {MARLIN_GO_ID}}}\n", f"  _shipGeometries:\n  - {{fileID: {FUSE_GO}}}\n")
    body = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  ShowTopMostFoldoutHeaderGroup: 1\n" % BODIES_ID, text)
    if body and OLD_BODIES_FIELDS.match(text, body.end()):
        m = OLD_BODIES_FIELDS.match(text, body.end())
        text = text[:m.start()] + BODIES_FIELDS + text[m.end():]
    m = re.search(r"--- !u!1001 &%s\nPrefabInstance:\n(?:.*\n)*?    m_Modifications:\n" % RHINO_RIG_INSTANCE, text)
    if m and RIG_OFF not in text:
        text = text[:m.end()] + RIG_OFF + text[m.end():]
    return text


def drop_marlin(text: str) -> str:
    """Take the marlin's model out of a prefab that still nests it (Marlin and Jacks became the Jacks, 2026-10-21): its
    PrefabInstance and its two stripped stubs, and the fuselage's child entry. Idempotent."""
    head, docs = split_docs(text)
    gone = {MARLIN_INSTANCE_ID, MARLIN_TRANSFORM_ID, MARLIN_GO_ID}
    if not any(a in gone for _, a, _ in docs):
        return text
    text = head + "".join(d for _, a, d in docs if a not in gone)
    return text.replace(f"  - {{fileID: {MARLIN_TRANSFORM_ID}}}\n", "")


def rename_retrofit(text: str) -> str:
    """The root and the VesselStatus named Jacks, and the executor without the marlin's colour. Idempotent."""
    text = text.replace("  m_Name: Marlin\n", "  m_Name: Jacks\n").replace("  _name: Marlin\n", "  _name: Jacks\n")
    return re.sub(r"^  marlinColor: \{[^}]*\}\n", "", text, flags=re.M)


def check_bodies(text: str):
    errs = []

    def want(cond, msg):
        if not cond:
            errs.append(msg)
    for iid, parent, fbx, name in [(iid, ROOT_TRANSFORM_ID, JACK_FBX_GUID, n) for n, iid, _ in JACK_IDS]:
        inst = re.search(r"--- !u!1001 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % iid, text)
        want(inst and f"m_SourcePrefab: {{fileID: 100100000, guid: {fbx}, type: 3}}" in inst.group(1)
             and f"m_TransformParent: {{fileID: {parent}}}" in inst.group(1) and f"      value: {name}\n" in inst.group(1),
             f"Jacks.prefab: '{name}' is not nested where JacksBodies finds it")
    for tid, kids in ((ROOT_TRANSFORM_ID, [t for _, _, t in JACK_IDS]),):
        t = re.search(r"--- !u!4 &%s\nTransform:\n((?:.*\n)*?)(?=--- !u!|\Z)" % tid, text)
        want(t and all(f"  - {{fileID: {k}}}\n" in t.group(1) for k in kids), f"Jacks.prefab: transform {tid} does not list {kids}")
    root = component_list(text, ROOT_GO)
    want(root and f"{{fileID: {BODIES_ID}}}" in root.group(1), "Jacks.prefab: JacksBodies is not on the root")
    tel = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % TELEMETRY_ID, text)
    want(root and f"{{fileID: {TELEMETRY_ID}}}" in root.group(1) and tel and TELEMETRY_FIELDS in tel.group(1),
         "Jacks.prefab: no DefaultVesselTelemetry on the root with its stat channels (the bootstrapper's unwired fallback)")
    body = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % BODIES_ID, text)
    want(body and f"guid: {guid_of_script('JacksBodies')}, type: 3" in body.group(1),
         "Jacks.prefab: the JacksBodies document is missing or names another script (no one would see the jacks)")
    want(f"  _shipGeometries:\n  - {{fileID: {FUSE_GO}}}\n" in text,
         "Jacks.prefab: the ship geometry is not the Rhino's fuselage part (its collider is the one GhostAction turns off)")
    want(MARLIN_FBX_GUID not in text and f"&{MARLIN_INSTANCE_ID}" not in text and f"{{fileID: {MARLIN_TRANSFORM_ID}}}" not in text,
         "Jacks.prefab: still nests the marlin's model (the marlin is saved for a swordsman; the jacks are the vessel)")
    body = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % BODIES_ID, text)
    want(body and "  flightModelScale:" in body.group(1) and "marlinModelName" not in body.group(1),
         "Jacks.prefab: JacksBodies still carries MarlinCrewBodies' fields (run the tool)")
    want(RIG_OFF in text, "Jacks.prefab: the Rhino's rig still draws (a Rhino between the jacks)")
    hud = re.search(r"--- !u!1001 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % HUD_INSTANCE, text)
    for image_id, sprite in ICON_SPRITES:
        want(hud and icon_override(image_id, sprite) in hud.group(1),
             f"Jacks.prefab: the HUD icon {image_id} is not the jacks' {sprite} (the Rhino's sword placeholder shows)")
    return errs


def retrofit(text: str) -> str:
    """The jacks' name, buttons, no sword, their HUD and container, the executor. Idempotent."""
    text = rename_retrofit(text)
    text = BINDINGS.sub(lambda m: WANT_BINDINGS, text, count=1)
    text = OVERRIDES.sub(lambda m: m.group(1) + " []\n", text, count=1)
    text = strip_rhino_kit(text)
    text = executor_retrofit(text)
    text = hud_retrofit(text)
    text = text.replace(f"guid: {DONOR_CONTAINER_GUID},", f"guid: {CONTAINER_GUID},")
    text = text.replace(f"  settings: {{fileID: 11400000, guid: {RHINO_CAMERA_GUID}, type: 2}}",
                        f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}")
    return net_retrofit(icons_retrofit(bodies_retrofit(text)))


def prefab_meta() -> str:
    return (f"fileFormatVersion: 2\nguid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the assets
def so_header(script_guid: str, name: str) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n  m_Name: {name}\n  m_EditorClassIdentifier: \n")


def config_asset(script_guid: str) -> str:
    """JacksConfigSO at its C# initializers (the lab's shipped values); the team tunes it in the inspector after."""
    return so_header(script_guid, "JacksConfig") + (
        "  labScale: 0.4\n  thumbMm: 22\n  thumbMargin: 1.25\n  viewSpan: 1800\n" + THUMB_VIEW_FIELDS + "  camSlack: 0.45\n  planePull: 1.6\n"
        "  separation: 58\n  neighbourRadius: 160\n  grazeSense: 130\n  minHerd: 30\n  herdCap: 150\n  floorBirthSeconds: 0.12\n"
        f"  companionFauna: {{fileID: 11400000, guid: {COMPANION_FAUNA_GUID}, type: 2}}\n" + HERD_MARK_FIELDS +
        "  dogSpeed: 520\n  padDogSpeed: 1\n  aiNoise: 0.15\n" + FLIGHT_FIELDS + NET_FIELDS + "  callTime: 2.5\n  callCooldown: 20\n  decoyCooldown: 4\n"
        "  teleportGuard: 0.6\n  pooEvery: 6\n  pooCount: 7\n  pooSpread: 22\n  pooPrismScale: {x: 2, y: 2, z: 5}\n"
        "  attackRange: 200\n  contactReach: 8\n  strikeCooldown: 1.2\n  pipSize: 0.32\n  viewMargin: 0.02\n  viewDepth: 600\n"
        + STABILITY_FIELDS + HERD_VIEW_FIELDS +
        f"  callSfx:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"
        f"  decoySfx:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"
        f"  teleportSfx:\n    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n")


# The herding player's stability (Garrett, 2026-10-21: "we need to make things as stable for the jack player as we can").
# Added to a committed config once, at the C# initializers, so a value tuned after is never overwritten.
STABILITY_FIELDS = ("  herdPlaneEase: 0.25\n  flightPlaneEase: 3\n  herdHullWeight: 0\n  flightHullWeight: 0.45\n"
                    "  viewPanEase: 1.2\n")
# Their names while there was a marlin (JacksConfigSO reads both through FormerlySerializedAs); a committed config is renamed.
STABILITY_RENAMES = (("jackSeatPlaneEase", "herdPlaneEase"), ("pilotSeatPlaneEase", "flightPlaneEase"),
                     ("jackSeatMarlinWeight", "herdHullWeight"), ("pilotSeatMarlinWeight", "flightHullWeight"))
# The jacks flying the hull, and the AI's mode (Garrett, 2026-10-21: "they can switch to 3d flight mode and use two thumb
# flight controls"). Added once, after aiNoise, where the C# declares them.
FLIGHT_FIELDS = ("  flightGap: 14\n  flightReach: 10\n  flightFormationEase: 8\n  aiHerdSeconds: 14\n  aiFlightSeconds: 6\n")
# (NET_FIELDS follow it in the asset, after aiFlightSeconds; Unity reads a field wherever it sits)


# The jacks' view framed by the thumb (Garrett, 2026-10-21: "zoomed out enough that on a full size mobile device an above
# average sized thumb barely covers the smallest jack repulsion radius") and the herd's mark ("a slight hue shift in your
# domain"). Added once, like the stability fields.
THUMB_VIEW_FIELDS = "  phoneShortMm: 71\n  thumbCover: 0.9\n"
HERD_MARK_FIELDS = "  herdHueLean: 0.35\n  herdGlow: 1.5\n"   # the glow: "fauna that get herded should light up" (2026-10-23)
# The net (Garrett, 2026-10-24: "lets make it a net that opens wider with xdiff"), after the flight's and the AI's fields;
# and the herding view zoomed in, following the jacks, with its whole-cell zoom and buttons, after viewPanEase.
NET_FIELDS = ("  netSpread: 30\n  netMinHeight: 6\n  netMaxHeight: 36\n  netDepth: 26\n  netTrailEase: 5\n  netSettle: 0.75\n"
              "  netSettleEase: 3\n  netPrismCap: 48\n  netRivalTake: 0.1\n  netRivalCooldown: 2\n  jackCrystalReach: 32\n  jackNetCrystalReach: 10\n"
              "  jackCrystalHeight: 200\n"
              "  netCameraFill: 0.8\n  netCameraEase: 3\n")
HERD_VIEW_FIELDS = ("  herdViewZoom: 0.75\n  herdFollowJacks: 1\n  cellViewMargin: 1.1\n  herdJacksFill: 0.8\n  herdJacksMargin: 40\n"
                    "  viewZoomEase: 3\n  zoomButtonSize: 0.09\n"
                    "  zoomButtonIcon: {fileID: 0}\n  callButtonIcon: {fileID: 0}\n")


def config_retrofit(text: str) -> str:
    """The stability fields after viewDepth (renamed from the marlin's era), the thumb view's after viewSpan, the herd's
    mark after companionFauna, the flight's after aiNoise; each once. Idempotent."""
    text = text.replace("  m_Name: MarlinJacksConfig\n", "  m_Name: JacksConfig\n")
    for old, new in STABILITY_RENAMES:
        text = re.sub(r"^  %s:" % old, f"  {new}:", text, flags=re.M)
    if "  flightGap:" not in text:
        text = re.sub(r"^(  aiNoise: .*\n)", lambda m: m.group(1) + FLIGHT_FIELDS, text, count=1, flags=re.M)
    if "  herdPlaneEase:" not in text:
        text = re.sub(r"^(  viewDepth: .*\n)", lambda m: m.group(1) + STABILITY_FIELDS, text, count=1, flags=re.M)
    if "  phoneShortMm:" not in text:
        text = re.sub(r"^(  viewSpan: .*\n)", lambda m: m.group(1) + THUMB_VIEW_FIELDS, text, count=1, flags=re.M)
    if "  herdHueLean:" not in text:
        text = re.sub(r"^(  companionFauna: .*\n)", lambda m: m.group(1) + HERD_MARK_FIELDS, text, count=1, flags=re.M)
    elif "  herdGlow:" not in text:
        text = re.sub(r"^(  herdHueLean: .*\n)", lambda m: m.group(1) + "  herdGlow: 1.5\n", text, count=1, flags=re.M)
    if "  netSpread:" not in text:
        text = re.sub(r"^(  aiFlightSeconds: .*\n)", lambda m: m.group(1) + NET_FIELDS, text, count=1, flags=re.M)
    if "  netSpread:" in text and "  netTrailEase:" not in text:   # 2026-10-24 round 2: the trail, the jacks' reach, the zoom-out
        text = re.sub(r"^(  netDepth: .*\n)", lambda m: m.group(1) + "  netTrailEase: 5\n", text, count=1, flags=re.M)
        text = text.replace("  jackCrystalReach: 18\n", "  jackCrystalReach: 32\n  jackNetCrystalReach: 10\n  jackCrystalHeight: 200\n")
    if "  jackCrystalReach:" in text and "  jackNetCrystalReach:" not in text:   # round 4 (2026-10-25): netting jacks collect
        text = re.sub(r"^(  jackCrystalReach: .*\n)", lambda m: m.group(1) + "  jackNetCrystalReach: 10\n", text, count=1, flags=re.M)
    if "  netSpread:" in text and "  netPrismCap:" not in text:   # round 3 (2026-10-25): the net drags prisms
        text = re.sub(r"^(  netSettleEase: .*\n)", lambda m: m.group(1) + "  netPrismCap: 48\n", text, count=1, flags=re.M)
    if "  herdViewZoom:" in text and "  herdJacksFill:" not in text:
        text = re.sub(r"^(  cellViewMargin: .*\n)", lambda m: m.group(1) + "  herdJacksFill: 0.8\n  herdJacksMargin: 40\n", text,
                      count=1, flags=re.M)
    if "  herdViewZoom:" not in text:
        text = re.sub(r"^(  viewPanEase: .*\n)", lambda m: m.group(1) + HERD_VIEW_FIELDS, text, count=1, flags=re.M)
    return text


def control_asset(script_guid: str, name: str, value: int) -> str:
    return so_header(script_guid, name) + f"  control: {value}\n"


# Haptics, the prism damage and the danger debuff (the Angler's): the marlin is a hull that flies through mass like any.
# The Rhino's plough and its surge / explosion crystals are the Rhino's.
CONTAINER_PRISM = ["ca2d1be3880b82941a0465a1f7e45280",   # VesselHapticsByPrismEffect
                   "4da62d4eefe03594ba35027dd537aff0",   # VesselDamagePrismEffect
                   "c7ccaca885824b24b716b12148d77ce1"]   # VesselElementalDebuffByDangerPrismEffect
CONTAINER_CRYSTAL = ["8de915fb00503344ca89a92a0a127b00"]  # VesselHapticsByCrystalEffect


def container_asset() -> str:
    lst = lambda gs: "".join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in gs)
    return so_header(guid_of_script("VesselImpactorDataContainerSO"), "JacksImpactorDataContainer") + (
        "  vesselPrismEffects:\n" + lst(CONTAINER_PRISM) + "  vesselCrystalEffects:\n" + lst(CONTAINER_CRYSTAL + [WORM_EFFECT_GUID]) +
        "  vesselMassCrystalEffects: []\n  vesselChargeCrystalEffects: []\n  vesselSpaceCrystalEffects: []\n"
        "  vesselTimeCrystalEffects: []\n  vesselSkimmerEffects: []\n  vesselLifeformCrystalEffects: []\n"
        "  omniCrystalRetirement: {fileID: 0}\n")


DESCRIPTION = ("  Description: Two jacks herd the cell's wildlife. Herding, run each jack with a thumb,\n"
               "    lift it to leave the decoy, and call the herd. Netting, fly them, one fish under each\n"
               "    thumb, with a net between them that opens as the sticks go apart. D-pad left or right\n"
               "    switches between herding and netting.\n")
# The marlin era's crew names (Marlin, Jacks): the Jacks are one vessel with one seat, so the card names no crew.
CREW = "  CrewNames:\n  - Marlin\n  - Jacks\n  CrewNameTemplate: '{0} and {1}'\n"


# The class card, rendered from Jack.fbx by Tools/Build/vessel_model/render_cards.py (2026-10-21), which names each new
# PNG md5("cosmic-shore-card:" + its repo path); this generator writes the class asset too, so it names the same guids.
def _model_card(rel):
    return hashlib.md5(("cosmic-shore-card:" + rel).encode()).hexdigest()


CARD_ACTIVE = _model_card("Assets/_Graphics/CardImages/Jacks.png")
CARD_FIELDS = {
    "IconActive": CARD_ACTIVE,
    "IconInactive": _model_card("Assets/_Graphics/CardImages/Jacks_Inactive.png"),
    "CardSilohoutteActive": CARD_ACTIVE,
    "CardSilohoutteInactive": _model_card("Assets/_Graphics/CardImages/Jacks_silhouette.png"),
    "SquadImage": _model_card("Assets/_Graphics/CardImages/Jacks_large.png"),
    "PreviewImage": _model_card("Assets/_Graphics/Design Assests/Hangar/Jacks_Hangar_Preview.png"),
}


def cards_retrofit(text: str) -> str:
    """Point the class asset's six card fields at the Jacks' own renders. Idempotent."""
    for field, guid in CARD_FIELDS.items():
        text = re.sub(r"(  %s: \{fileID: 21300000, guid: )[0-9a-f]{32}(,[^}]*\})" % field, r"\g<1>%s\2" % guid, text, count=1)
    return text


def class_retrofit(text: str) -> str:
    """The committed class asset as the Jacks: its name, its m_Name, its description, no crew, the jacks' cards. Idempotent."""
    text = text.replace("  m_Name: SO_Class_Marlin\n", "  m_Name: SO_Class_Jacks\n").replace("  Name: Marlin and Jacks\n", "  Name: Jacks\n")
    text = re.sub(r"  Description: .*\n(?:    .*\n)*", lambda m: DESCRIPTION, text, count=1)
    return cards_retrofit(text.replace(CREW, ""))


def class_asset(donor_class: str) -> str:
    text = replace_once(donor_class, "  m_Name: SO_Class_Rhino\n", "  m_Name: SO_Class_Jacks\n", "class m_Name")
    text = replace_once(text, f"  Class: {RHINO_CLASS_ID}\n", f"  Class: {JACKS_CLASS_ID}\n", "class Class")
    text = replace_once(text, "  Name: Rhino\n", "  Name: Jacks\n", "class Name")
    text, n = re.subn(r"  Description: .*\n(?:    .*\n)*", lambda m: DESCRIPTION, text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class Description was not found")
    text, n = re.subn(r"  isLocked: \d\n", "  isLocked: 0\n  ownedFromStart: 1\n", text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class isLocked line was not found")
    return cards_retrofit(text)


# The four cards (Garrett, 2026-10-20). The words are authored data the team edits in the inspector; written once.
MAP_ENTRIES = [
    (1, 0, "Danger Share", "Charge sets the share of the herd that is DANGER: none at -5, all of it at 15. A danger "
        "creature that touches a rival hull stings it (a Strike) and is spent.",
     "Attack", "The herd goes for rivals near it and bites any it touches."),
    (2, 0, "Breeding", "Mass sets how fast the herd breeds: a birth takes the species' own feeds at -5 and a third of "
        "them at 15. The herd never starves below its minimum size.",
     "Mass Clusters", "Every few seconds a creature of the herd lays a cluster of your mass."),
    (3, 0, "Herding Ring", "Space widens the ring both jacks and the decoy push the herd out of; it is never "
        "smaller than a big thumb.",
     "Eat Shields", "The herd eats shielded prisms."),
    (4, 7, "Herd Speed", "Time sets how fast the herd swims. The jacks' call (A) pulls the herd to them for a moment, on "
        "a long cooldown.",
     "Flying Decoy", "Flying, the jacks leave the decoy where they are (X), so they can jump back to it."),
]


def map_entries() -> str:
    out = "  entries:\n"
    for element, inp, label, desc, up_label, up_desc in MAP_ENTRIES:
        out += (f"  - Element: {element}\n    AbilityLabel: {yaml_str(label)}\n    AbilityDescription: {yaml_str(desc)}\n"
                f"    Input: {inp}\n    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n"
                f"    UpgradeLabel: {yaml_str(up_label)}\n    UpgradeDescription: {yaml_str(up_desc)}\n")
    return out


def map_retrofit(text: str) -> str:
    """The committed map named Jacks, and its cards' words rewritten where they still name the marlin (words the team
    edited since are left alone). Idempotent."""
    text = text.replace("  m_Name: Marlin\n", "  m_Name: Jacks\n")
    if "arlin" in text[text.index("  entries:\n"):]:
        text = text[:text.index("  entries:\n")] + map_entries()
    return text


def build_map(donor_map: str) -> str:
    text = replace_once(donor_map, "  m_Name: Rhino\n", "  m_Name: Jacks\n", "map name")
    text = replace_once(text, f"  vesselClass: {RHINO_CLASS_ID}\n", f"  vesselClass: {JACKS_CLASS_ID}\n", "map vesselClass")
    return text[:text.index("  entries:\n")] + map_entries()


def register_class_list(text: str) -> str:
    if CLASS_GUID in text:
        return text
    m = re.search(r"  VesselList:\n((?:  - \{fileID: 11400000, guid: [0-9a-f]{32}, type: 2\}\n)+)", text)
    if not m:
        raise RuntimeError("class list: VesselList not found")
    return text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {CLASS_GUID}, type: 2}}\n" + text[m.end(1):]


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
                   "    SourcePrefabToOverride: {fileID: 0}\n    SourceHashToOverride: 0\n    OverridingTargetPrefab: {fileID: 0}\n")


def register_fusion(text: str) -> str:
    if fusion_entries(text, JACKS_CLASS_ID):
        return text
    donor = fusion_entries(text, RHINO_CLASS_ID)
    if len(donor) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Rhino entries, found {len(donor)}")
    last = None
    for m in re.finditer(r"  - vessel: \d+\n(?:    .*\n)+", text):
        last = m
    added = "".join(e.replace(f"  - vessel: {RHINO_CLASS_ID}\n", f"  - vessel: {JACKS_CLASS_ID}\n", 1) for e in donor)
    return text[:last.end()] + added + text[last.end():]


# ----------------------------------------------------------------------------- checks
def check_prefab(text: str, script_guid: str):
    errs = []

    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want("  m_Name: Jacks\n" in text, "Jacks.prefab: the root is not named Jacks")
    want("  _name: Jacks\n" in text, "Jacks.prefab: VesselStatus _name is not Jacks")
    want("  marlinColor:" not in text, "Jacks.prefab: the executor still carries the marlin's colour")
    want(f"  vesselType: {JACKS_CLASS_ID}\n" in text, f"Jacks.prefab: vesselType is not {JACKS_CLASS_ID}")
    want(f"  vesselType: {RHINO_CLASS_ID}\n" not in text, "Jacks.prefab: still carries the Rhino's vesselType")
    h = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(h and int(h.group(1)) == global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "Jacks.prefab: GlobalObjectIdHash is not this guid's (a copied hash collides with the Rhino in Netcode)")
    ip = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(ip and ip.group(1) == "0", "Jacks.prefab: InScenePlacedSourceGlobalObjectIdHash is not cleared")
    sa = component_list(text, SHIP_ACTIONS_GO)
    want(sa and f"{{fileID: {EXECUTOR_ID}}}" in sa.group(1), "Jacks.prefab: JacksExecutor is not on ShipActions")
    reg = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % REGISTRY_ID, text)
    want(reg and f"  - {{fileID: {EXECUTOR_ID}}}\n" in reg.group(1),
         "Jacks.prefab: the executor is not in the ActionExecutorRegistry (the buttons would reach nothing)")
    ex = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % EXECUTOR_ID, text)
    want(ex and f"guid: {script_guid}, type: 3" in ex.group(1), "Jacks.prefab: the executor document names another script")
    for key, guid in (("config", CONFIG_GUID), ("OnMiniGameTurnEnd", TURN_END_GUID), ("onCombatHitLanded", COMBAT_HIT_GUID)):
        want(ex and f"  {key}: {{fileID: 11400000, guid: {guid}, type: 2}}" in ex.group(1),
             f"Jacks.prefab: JacksExecutor.{key} is not wired")
    want(WANT_BINDINGS in text, "Jacks.prefab: the netting jacks' buttons are not jump 1 + 4, decoy 6, call 7 (and no ramp boost)")
    want(f"guid: {RAMP_BOOST_GUID}, type: 2" not in text, "Jacks.prefab: the Rhino's ramp speed boost is still bound")
    want("  _gamepadActionOverrides: []\n" in text, "Jacks.prefab: the Rhino's gamepad overrides (the swipes) are still bound")
    want(f"guid: {CONTAINER_GUID}," in text and f"guid: {DONOR_CONTAINER_GUID}," not in text,
         "Jacks.prefab: the VesselImpactor is not on JacksImpactorDataContainer (the Rhino's plough would ride along)")
    names = set(re.findall(r"^  m_Name: (.*)$", text, re.M))
    for n in RHINO_SUBTREES:
        want(n not in names, f"Jacks.prefab: the Rhino's {n} is still on the hull")
    for n in RHINO_COMPONENTS + ["ShieldSkimmerScaleDriver", "RhinoSwordAfterimage", "ShieldSwipeActionExecutor",
                                 "RhinoVesselHUDController"]:
        want(f"guid: {guid_of_script(n)}, type: 3" not in text, f"Jacks.prefab: the Rhino's {n} is still on the hull")
    hud = re.search(r"guid: %s, type: 3\}\n((?:.*\n)*?)(?=--- !u!|\Z)" % guid_of_script("JacksHUDController"), text)
    want(hud and f"  crew: {{fileID: {EXECUTOR_ID}}}\n" in hud.group(1), "Jacks.prefab: the HUD is not JacksHUDController on the executor")
    dangling = dangling_local_refs(text)
    want(not dangling, f"Jacks.prefab: dangling local fileID reference(s) {dangling[:5]}")
    want(f"  settings: {{fileID: 11400000, guid: {CAMERA_GUID}, type: 2}}" in text and RHINO_CAMERA_GUID not in text,
         "Jacks.prefab: the camera settings are not JacksCameraSettingsSO (a tune would move the Rhino's camera)")
    return errs + check_bodies(text) + check_jack(text)


def check_assets(config: str, controls, container: str):
    errs = []
    if f"guid: {guid_of_script('JacksConfigSO')}, type: 3" not in config:
        errs.append("JacksConfig.asset: does not name JacksConfigSO")
    if "  herdPlaneEase:" not in config or "  viewPanEase:" not in config or "  jackSeatPlaneEase:" in config:
        errs.append("JacksConfig.asset: no herding stability fields, or still the marlin-era names (run the tool)")
    if "  flightGap:" not in config or "  aiHerdSeconds:" not in config:
        errs.append("JacksConfig.asset: no flight or AI-mode fields (run the tool)")
    if "  m_Name: JacksConfig\n" not in config:
        errs.append("JacksConfig.asset: m_Name is not JacksConfig")
    if "  phoneShortMm:" not in config or "  thumbCover:" not in config:
        errs.append("JacksConfig.asset: no thumb-framed view fields (run the tool)")
    if "  herdHueLean:" not in config:
        errs.append("JacksConfig.asset: no herd mark field (run the tool)")
    if "  herdGlow:" not in config:
        errs.append("JacksConfig.asset: no herd glow field (run the tool)")
    if ("  netSpread:" not in config or "  netCameraEase:" not in config or "  netTrailEase:" not in config
            or "  netPrismCap:" not in config or "  jackNetCrystalReach:" not in config):
        errs.append("JacksConfig.asset: no net fields (run the tool)")
    if "  herdViewZoom:" not in config or "  callButtonIcon:" not in config or "  herdJacksFill:" not in config:
        errs.append("JacksConfig.asset: no herding view zoom fields (run the tool)")
    action = guid_of_script("JacksActionSO")
    for name, value, text in controls:
        if f"guid: {action}, type: 3" not in text or f"  control: {value}\n" not in text:
            errs.append(f"{name}.asset: not a JacksActionSO with control {value}")
    for g in CONTAINER_PRISM + CONTAINER_CRYSTAL + [WORM_EFFECT_GUID]:
        if g not in container:
            errs.append(f"JacksImpactorDataContainer: missing effect {g}")
    return errs


def worm_effect_asset() -> str:
    """The omni crystal produces an omnivorous worm in our domain (Garrett, 2026-10-24)."""
    return so_header(guid_of_script("VesselSpawnFaunaByCrystalEffectSO"), "JacksOmniWormByCrystalEffect") + (
        f"  fauna: {{fileID: 11400000, guid: {WORM_FAUNA_GUID}, type: 2}}\n  spawnBehind: 30\n")


def check_worm_effect(text: str):
    errs = []
    if f"guid: {guid_of_script('VesselSpawnFaunaByCrystalEffectSO')}, type: 3" not in text:
        errs.append("JacksOmniWormByCrystalEffect.asset: does not name VesselSpawnFaunaByCrystalEffectSO")
    if f"  fauna: {{fileID: 11400000, guid: {WORM_FAUNA_GUID}, type: 2}}" not in text:
        errs.append("JacksOmniWormByCrystalEffect.asset: does not name the worm colony (the omni crystal would produce nothing)")
    return errs


def container_retrofit(text: str) -> str:
    """The worm effect in the container's crystal (omni) effects, once. Idempotent."""
    if WORM_EFFECT_GUID in text:
        return text
    return re.sub(r"^(  vesselCrystalEffects:\n(?:  - .*\n)*)",
                  lambda m: m.group(1) + f"  - {{fileID: 11400000, guid: {WORM_EFFECT_GUID}, type: 2}}\n", text, count=1, flags=re.M)


def check_registrations(container: str, network: str, lists, cls: str, kmap: str, fusion: str):
    errs = []

    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in container,
         "Vessel Prefab Container: no Jacks entry on its root TRANSFORM (the Vessel Changer offers only what is here)")
    want(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in network,
         "DefaultNetworkPrefabs: no Jacks entry on its root GAMEOBJECT (a swap to it cannot spawn)")
    for name, text in lists:
        want(text.count(CLASS_GUID) == 1, f"{name}: SO_Class_Jacks listed {text.count(CLASS_GUID)} times, not once")
    want(f"  Class: {JACKS_CLASS_ID}\n" in cls and "  ownedFromStart: 1\n" in cls and "  isLocked: 0\n" in cls,
         "SO_Class_Jacks: wrong class, locked, or not owned from the start")
    want("  CrewNames:" not in cls and "  Name: Jacks\n" in cls and "  m_Name: SO_Class_Jacks\n" in cls and "arlin" not in cls,
         "SO_Class_Jacks: not named Jacks, or still names the marlin or a crew (the Jacks are one vessel, one seat)")
    for field, guid in CARD_FIELDS.items():
        want(re.search(r"  %s: \{fileID: 21300000, guid: %s," % (field, guid), cls) is not None,
             f"SO_Class_Jacks: {field} is not the Jacks' rendered card (render_cards.py Jacks)")
    want(f"  vesselClass: {JACKS_CLASS_ID}\n" in kmap and "  m_Name: Jacks\n" in kmap, "ElementalAbilityMaps/Jacks: wrong class or name")
    want("arlin" not in kmap, "ElementalAbilityMaps/Jacks: a card still names the marlin")
    want("Sword" not in kmap, "ElementalAbilityMaps/Jacks: still the Rhino's cards")
    jacks, rhino = fusion_entries(fusion, JACKS_CLASS_ID), fusion_entries(fusion, RHINO_CLASS_ID)
    want(len(jacks) == 4, f"CrystalHullFusionConfig: {len(jacks)} Jacks entries, not 4")
    want([e.split("\n", 1)[1] for e in jacks] == [e.split("\n", 1)[1] for e in rhino],
         "CrystalHullFusionConfig: the Jacks' entries are not the Rhino's (it still bakes the Rhino's hull)")
    return errs


def control_path(name):
    return os.path.join(ACTIONS, f"{name}.asset")


def check_all():
    script_guid = guid_of_script("JacksExecutor")
    if not os.path.exists(PREFAB) or not os.path.exists(PREFAB + ".meta"):
        return ["Jacks.prefab (+ .meta) is missing: run Tools/Build/author_jacks_assets.py"]
    errs = [] if guid_of(PREFAB + ".meta") == PREFAB_GUID else ["Jacks.prefab.meta: guid is not PREFAB_GUID"]
    errs += check_prefab(read(PREFAB), script_guid)
    paths = [CLASS_ASSET, MAP, CONFIG, JACKS_CONTAINER, CAMERA] + [control_path(n) for n, _, _ in CONTROLS]
    for path in paths:
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    for path, guid in [(CLASS_ASSET, CLASS_GUID), (MAP, MAP_GUID), (CONFIG, CONFIG_GUID), (JACKS_CONTAINER, CONTAINER_GUID),
                       (CAMERA, CAMERA_GUID)] + \
                      [(control_path(n), g) for n, _, g in CONTROLS]:
        if guid_of(path + ".meta") != guid:
            errs.append(f"{os.path.relpath(path, ROOT)}.meta: guid is not the one the prefab names")
    for path in paths:
        name = re.search(r"^  m_Name: (.*)$", read(path), re.M)
        if not name or name.group(1) != os.path.splitext(os.path.basename(path))[0]:
            errs.append(f"{os.path.relpath(path, ROOT)}: m_Name is not its file name (a marlin-era name: run the tool)")
    errs += check_assets(read(CONFIG), [(n, v, read(control_path(n))) for n, v, _ in CONTROLS], read(JACKS_CONTAINER))
    if not os.path.exists(WORM_EFFECT) or guid_of(WORM_EFFECT + ".meta") != WORM_EFFECT_GUID:
        errs.append("JacksOmniWormByCrystalEffect.asset (+ .meta, its guid) is missing: run the tool")
    else:
        errs += check_worm_effect(read(WORM_EFFECT))
    errs += check_registrations(read(CONTAINER), read(NETWORK_PREFABS), [(os.path.basename(p), read(p)) for p in CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(FUSION))
    return errs


def rename_m_name(path: str, old: str, new: str):
    """A committed asset renamed from the marlin era: its m_Name follows its file name. Idempotent."""
    text = read(path)
    if f"  m_Name: {old}\n" in text:
        write(path, text.replace(f"  m_Name: {old}\n", f"  m_Name: {new}\n"))


def author():
    script_guid = guid_of_script("JacksExecutor")
    if not os.path.exists(PREFAB):   # the spent one-shot stands down once the prefab is committed
        write(PREFAB, build_clone(read(DONOR), script_guid))
        write(PREFAB + ".meta", prefab_meta())
    else:
        old = read(PREFAB); new = retrofit(old)
        if new != old:
            write(PREFAB, new)
    os.makedirs(ACTIONS, exist_ok=True)
    if not os.path.exists(ACTIONS + ".meta"):
        write(ACTIONS + ".meta", "fileFormatVersion: 2\nguid: 6b3d9e1f4a2c4875b0e7d3a9c1f6b284\nfolderAsset: yes\n"
                                 "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if not os.path.exists(CONFIG):
        write(CONFIG, config_asset(guid_of_script("JacksConfigSO"))); write(CONFIG + ".meta", native_meta(CONFIG_GUID))
    else:
        old = read(CONFIG); new = config_retrofit(old)
        if new != old:
            write(CONFIG, new)
    action = guid_of_script("JacksActionSO")
    for name, value, guid in CONTROLS:
        if not os.path.exists(control_path(name)):
            write(control_path(name), control_asset(action, name, value)); write(control_path(name) + ".meta", native_meta(guid))
        else:
            rename_m_name(control_path(name), f"Marlin{name[len('Jacks'):]}", name)
    if not os.path.exists(CAMERA):
        write(CAMERA, replace_once(read(RHINO_CAMERA), "  m_Name: RhinoCameraSettingsSO\n", "  m_Name: JacksCameraSettingsSO\n",
                                   "camera m_Name"))
        write(CAMERA + ".meta", native_meta(CAMERA_GUID))
    else:
        rename_m_name(CAMERA, "MarlinCameraSettingsSO", "JacksCameraSettingsSO")
    if not os.path.exists(WORM_EFFECT):
        write(WORM_EFFECT, worm_effect_asset()); write(WORM_EFFECT + ".meta", native_meta(WORM_EFFECT_GUID))
    if not os.path.exists(JACKS_CONTAINER):
        write(JACKS_CONTAINER, container_asset()); write(JACKS_CONTAINER + ".meta", native_meta(CONTAINER_GUID))
    else:
        rename_m_name(JACKS_CONTAINER, "MarlinImpactorDataContainer", "JacksImpactorDataContainer")
        old = read(JACKS_CONTAINER); new = container_retrofit(old)
        if new != old:
            write(JACKS_CONTAINER, new)
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset(read(DONOR_CLASS))); write(CLASS_ASSET + ".meta", native_meta(CLASS_GUID))
    else:
        old = read(CLASS_ASSET); new = class_retrofit(old)
        if new != old:
            write(CLASS_ASSET, new)
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
    """Every check must fire on a broken copy (negative controls), and pass on a fresh clone."""
    script_guid = guid_of_script("JacksExecutor")
    good = build_clone(read(DONOR), script_guid)
    fails = []

    def expect(errs, n_min, what):
        if not ((len(errs) == 0) if n_min == 0 else (len(errs) >= n_min)):
            fails.append(f"{what}: {errs}")
    expect(check_prefab(good, script_guid), 0, "a fresh clone passes")
    expect(check_prefab(good.replace(f"  vesselType: {JACKS_CLASS_ID}\n", f"  vesselType: {RHINO_CLASS_ID}\n"), script_guid), 1, "the Rhino's vesselType")
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>1124080984", good, count=1), script_guid), 1, "the donor's hash")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {EXECUTOR_ID}}}\n", ""), script_guid), 1, "the executor off ShipActions")
    expect(check_prefab(good.replace(f"  - {{fileID: {EXECUTOR_ID}}}\n", ""), script_guid), 1, "the executor out of the registry")
    expect(check_prefab(good.replace(f"guid: {CONFIG_GUID}, type: 2", "guid: " + "0" * 32 + ", type: 2"), script_guid), 1, "the config unwired")
    expect(check_prefab(good.replace(WANT_BINDINGS, "  _inputEventShipActions: []\n"), script_guid), 1, "no buttons")
    expect(check_prefab(good.replace(CONTAINER_GUID, DONOR_CONTAINER_GUID), script_guid), 1, "the Rhino's container")
    expect(check_prefab(read(DONOR).replace("  m_Name: Rhino\n", "  m_Name: Jacks\n"), script_guid), 3, "the Rhino untouched")
    expect(check_prefab(good + "  stray: {fileID: 987654321}\n", script_guid), 1, "a dangling local reference")
    expect(check_prefab(good.replace(RIG_OFF, ""), script_guid), 1, "the Rhino's rig still drawn")
    expect(check_prefab(good.replace(CAMERA_GUID, RHINO_CAMERA_GUID), script_guid), 1, "the Rhino's camera settings")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {TELEMETRY_ID}}}\n", ""), script_guid), 1, "no telemetry")
    expect(check_prefab(good.replace(icon_override(*ICON_SPRITES[2]), ""), script_guid), 1, "the Rhino's Space icon")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {BODIES_ID}}}\n", ""), script_guid), 1, "no bodies component")
    arm = (f"--- !u!136 &{RETIRED_ARM_COLLIDERS[0]}\nCapsuleCollider:\n  m_GameObject: {{fileID: {SKIMMER_GO}}}\n")
    expect(check_prefab(good + arm, script_guid), 1, "the retired jack's arm collider")
    if check_prefab(net_retrofit(good + arm), script_guid):
        fails.append("net_retrofit does not take the retired jack's arm collider off")
    jets = read(DONOR).split("--- !u!1001 &3901220500000001\n", 1)
    expect(check_jack("--- !u!1001 &3901220500000001\n" + jets[1]), 2, "a Rhino jet left on the hull")
    rhino = build_clone(read(DONOR), script_guid)
    if any(f"guid: {g}," in rhino for g in RHINO_JET_PREFABS) or "RhinoSwordBladeTracer" in rhino:
        fails.append("the strip leaves a Rhino jet or a sword streak on the clone")
    expect(check_prefab(good.replace(f"  _shipGeometries:\n  - {{fileID: {FUSE_GO}}}\n",
                                     f"  _shipGeometries:\n  - {{fileID: {MARLIN_GO_ID}}}\n"), script_guid), 1, "the marlin's ship geometry")
    marlin = good + model_instance(MARLIN_INSTANCE_ID, MARLIN_TRANSFORM_ID, FUSE_TRANSFORM, MARLIN_FBX_GUID, "Marlin", "-2", MARLIN_GO_ID)
    expect(check_prefab(marlin, script_guid), 1, "the marlin's model still nested")
    expect(check_prefab(retrofit(marlin), script_guid), 0, "drop_marlin takes the marlin's model out")
    old_bodies = good.replace(BODIES_FIELDS, "  marlinModelName: Marlin\n  replacedModelName: rhino_shapekey_with_animations\n"
                                             "  jackModelNames:\n  - Jack A\n  - Jack B\n  jackModelScale: 2.5\n  sailRate: 5\n")
    expect(check_prefab(old_bodies, script_guid), 1, "MarlinCrewBodies' fields")
    expect(check_prefab(retrofit(old_bodies), script_guid), 0, "the bodies retrofit writes JacksBodies' fields")
    old_names = good.replace("  m_Name: Jacks\n", "  m_Name: Marlin\n").replace("  _name: Jacks\n", "  _name: Marlin\n")
    expect(check_prefab(old_names, script_guid), 2, "the marlin-era names")
    expect(check_prefab(retrofit(old_names), script_guid), 0, "the rename retrofit")
    expect(check_prefab(good.replace("      value: Jack B\n", "      value: Jack\n"), script_guid), 1, "a misnamed jack")
    if retrofit(good) != good:
        fails.append("the retrofit is not idempotent")
    if DOC.search(good) is None:
        fails.append("the clone has no documents")
    controls = [(n, v, control_asset(guid_of_script("JacksActionSO"), n, v)) for n, v, _ in CONTROLS]
    cfg, cont = config_asset(guid_of_script("JacksConfigSO")), container_asset()
    expect(check_assets(cfg, controls, cont), 0, "fresh assets pass")
    expect(check_assets(cfg.replace("  herdPlaneEase: 0.25\n", ""), controls, cont), 1, "no stability fields")
    expect(check_assets(cfg.replace(FLIGHT_FIELDS, ""), controls, cont), 1, "no flight fields")
    era = cfg.replace("  herdPlaneEase:", "  jackSeatPlaneEase:").replace("  m_Name: JacksConfig\n", "  m_Name: MarlinJacksConfig\n")
    expect(check_assets(era, controls, cont), 2, "the marlin-era config")
    if config_retrofit(era) != cfg or config_retrofit(cfg.replace(FLIGHT_FIELDS, "")) != cfg:
        fails.append("the config retrofit does not rename the marlin-era fields or put the flight fields back")
    expect(check_assets(cfg.replace(THUMB_VIEW_FIELDS, ""), controls, cont), 1, "no thumb-framed view fields")
    expect(check_assets(cfg.replace(HERD_MARK_FIELDS, ""), controls, cont), 1, "no herd mark field")
    expect(check_assets(cfg.replace("  herdGlow: 1.5\n", ""), controls, cont), 1, "no herd glow field")
    expect(check_assets(cfg.replace(NET_FIELDS, ""), controls, cont), 1, "no net fields")
    expect(check_assets(cfg.replace(HERD_VIEW_FIELDS, ""), controls, cont), 1, "no herding view zoom fields")
    round1 = cfg.replace("  netTrailEase: 5\n", "").replace("  netPrismCap: 48\n", "").replace("  jackNetCrystalReach: 10\n", "").replace("  jackCrystalReach: 32\n  jackCrystalHeight: 200\n",
                                                             "  jackCrystalReach: 18\n").replace("  herdJacksFill: 0.8\n  herdJacksMargin: 40\n", "")
    expect(check_assets(round1, controls, cont), 1, "the net's first-round config")
    if config_retrofit(round1) != cfg:
        fails.append("the config retrofit does not bring the first-round net config to the second round")
    if config_retrofit(cfg.replace(NET_FIELDS, "").replace(HERD_VIEW_FIELDS, "")) != cfg:
        fails.append("the config retrofit does not put the net and herding view fields back where the asset keeps them")
    no_worm = cont.replace(f"  - {{fileID: 11400000, guid: {WORM_EFFECT_GUID}, type: 2}}\n", "")
    expect(check_assets(cfg, controls, no_worm), 1, "the container without the omni crystal's worm")
    if container_retrofit(no_worm) != cont or container_retrofit(cont) != cont:
        fails.append("the container retrofit does not add the worm effect once")
    worm = worm_effect_asset()
    expect(check_worm_effect(worm), 0, "the worm effect passes")
    expect(check_worm_effect(worm.replace(WORM_FAUNA_GUID, "0" * 32)), 1, "a worm effect naming no worm")
    if config_retrofit(cfg.replace("  herdGlow: 1.5\n", "")) != cfg:
        fails.append("the config retrofit does not put the herd glow back after herdHueLean")
    if config_retrofit(cfg) != cfg or "herdPlaneEase" not in config_retrofit(cfg.replace(STABILITY_FIELDS, "")):
        fails.append("the config retrofit is not idempotent or does not add the stability fields")
    if config_retrofit(cfg.replace(THUMB_VIEW_FIELDS, "").replace(HERD_MARK_FIELDS, "")) != cfg:
        fails.append("the config retrofit does not put the thumb view and herd mark fields back where the C# declares them")
    expect(check_assets(cfg, [(n, v, t.replace("  control: ", "  control: 9")) for n, v, t in controls], cont), 1, "a wrong control")
    expect(check_assets(cfg, controls, cont.replace(CONTAINER_PRISM[1], "0" * 32)), 1, "a missing effect")
    container, network, fusion = register_container(read(CONTAINER)), register_network(read(NETWORK_PREFABS)), register_fusion(read(FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in CLASS_LISTS]
    cls, kmap = class_asset(read(DONOR_CLASS)), build_map(read(DONOR_MAP))
    expect(check_registrations(container, network, lists, cls, kmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, kmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, kmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, kmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls.replace(DESCRIPTION, DESCRIPTION + CREW), kmap, fusion), 1,
           "a card that still names the marlin's crew")
    if class_retrofit(cls.replace(DESCRIPTION, DESCRIPTION + CREW).replace("  Name: Jacks\n", "  Name: Marlin and Jacks\n")) != cls:
        fails.append("the class retrofit does not take the marlin's crew and name off the card")
    expect(check_registrations(container, network, lists, cls, kmap.replace("Flying Decoy", "Marlin Decoy"), fusion), 1,
           "a card that still names the marlin")
    if map_retrofit(kmap.replace("Flying Decoy", "Marlin Decoy")) != kmap or map_retrofit(kmap) != kmap:
        fails.append("the map retrofit does not rewrite the marlin's words, or is not idempotent")
    expect(check_registrations(container, network, lists, cls.replace(CARD_FIELDS["SquadImage"], "2286f04c9031ee84d8d935de1926f92a"), kmap,
                              fusion), 1, "the Rhino's squad image")
    if cards_retrofit(cls) != cls:
        fails.append("the card retrofit is not idempotent")
    expect(check_registrations(container, network, lists, cls, read(DONOR_MAP), fusion), 1, "the Rhino's own map")
    expect(check_registrations(container, network, lists, cls, kmap, fusion.replace(f"  - vessel: {JACKS_CLASS_ID}\n", "  - vessel: 99\n", 1)), 1, "a missing fusion entry")
    if register_container(container) != container or register_fusion(fusion) != fusion or register_network(network) != network:
        fails.append("registration is not idempotent")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_jacks_assets self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errs = check_all()
    for e in errs:
        print("FAIL:", e)
    print("author_jacks_assets:", "ok" if not errs else f"{len(errs)} problem(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
