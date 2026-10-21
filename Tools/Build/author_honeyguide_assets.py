#!/usr/bin/env python3
"""Author the Honeyguide and Ratel (the Sparrow hull with the Ratel's Dynamo seat) and register it, then prove the shipped set.

    python3 Tools/Build/author_honeyguide_assets.py             # author what is missing, then check
    python3 Tools/Build/author_honeyguide_assets.py --check     # validate the SHIPPED assets, write nothing
    python3 Tools/Build/author_honeyguide_assets.py --self-test # prove every check fires

"Bring it to Unity" means the Toybox's Vessel Changer offers it (Garrett, 2026-10-17). The changer offers what
ToyVesselRoster.Default names AND the Vessel Prefab Container can spawn, so the Ratel - a SEAT on a hull - ships as its own
hull class, the Wrasse's way (author_wrasse_assets.py is the pattern; this tool is that one without the Wrasse's retrofits).

WHAT IT MAKES (the design: Assets/_Scripts/Controller/Vessel/Crew/HONEYGUIDE.md; the lab: Docs/Studios/HoneyguideLab.html)
    Assets/_Prefabs/Spacevessels/Honeyguide.prefab (+ .meta)   a text clone of Sparrow.prefab with:
        - the root GameObject and VesselStatus renamed, `vesselType: 21` (VesselClassType.Honeyguide)
        - a FRESH Netcode GlobalObjectIdHash for this guid, InScenePlacedSourceGlobalObjectIdHash cleared
        - HoneyguideCrew on the root, wired to HoneyguideConfig.asset
        - the triggers retargeted (lab round 6): RightStickAction -> HoneyguideRTAction, LeftStickAction -> HoneyguideLTAction,
          Button2 keeps the boost, the turret-stance toggle and the guns are gone; its AIPilot keeps only the boost (the crew
          presses the AI's triggers itself). Applied to the shipped prefab too, so it is not a one-shot.
      Everything else is the Sparrow's: the lab flies the Sparrow's airframe.
    Assets/_SO_Assets/VesselActions/Honeyguide/: HoneyguidePeckAction.asset (RT's Peck, a FullAutoActionSO: the lab's peckRate
      and peckRange through the Sparrow's gun, no spread, no growth) and HoneyguideRTAction / HoneyguideLTAction.asset (the
      two HoneyguideTriggerActionSO the input maps).
    Assets/_SO_Assets/VesselActions/Honeyguide/HoneyguideConfig.asset (+ .meta): every number, card and word, READ FROM THE
      LAB (CARDS, CARD_EFFECTS, EFFECTS, ACTIVES, ENGINES, ABILITY_TEXT, ESKILL, SHIPPED) with the C# initializers for the effect
      numbers the lab keeps in code. No card is parked (round 6: the trigger cards are the upgrades).
    SO_Class_Honeyguide (the Sparrow's icons, owned from the start, its card reads "Honeyguide and Ratel") + its entry in
    SO_Classlist_All / _Classes; one entry each in `Vessel Prefab Container.asset` (the root TRANSFORM) and
    `DefaultNetworkPrefabs.asset` (the root GAMEOBJECT); Resources/ElementalAbilityMaps/Honeyguide.asset (MAP_ENTRIES: Charge
    Peck on RT, Mass Flit on LT, Space the Slip, Time the Sparrow's Afterburner; written whenever it differs); and four
    CrystalHullFusionConfig entries (the Sparrow's: it draws the Sparrow's mesh).
    The toybox roster (ToyVesselRoster.Default) is CODE and is not written here.

THE CLONE AND THE CONFIG ARE SPENT ONE-SHOTS ONCE COMMITTED (CLAUDE.md): each is written only while absent, and `--check`
validates the SHIPPED files. The config check holds the lab's tables (cards, shapes, effects, engines, ability words) to
the asset - the lab is the record for those - and leaves the numbers to the team's inspector after the first write.
To re-author from scratch, delete the file and its .meta and run again.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from author_stoat_assets import global_object_id_hash, replace_once  # noqa: E402  (the shared Netcode hash)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
VESSELS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
DONOR = os.path.join(VESSELS, "Sparrow.prefab")
PREFAB = os.path.join(VESSELS, "Honeyguide.prefab")
CONTAINER = os.path.join(ROOT, "Assets", "_SO_Assets", "Vessel Prefab Container.asset")
NETWORK_PREFABS = os.path.join(ROOT, "Assets", "DefaultNetworkPrefabs.asset")
CLASSES = os.path.join(ROOT, "Assets", "_SO_Assets", "Classes")
DONOR_CLASS = os.path.join(CLASSES, "SO_Class_Sparrow.asset")
CLASS_ASSET = os.path.join(CLASSES, "SO_Class_Honeyguide.asset")
CLASS_LISTS = [os.path.join(CLASSES, "SO_Classlist_All.asset"), os.path.join(CLASSES, "SO_Classlist_Classes.asset")]
MAPS = os.path.join(ROOT, "Assets", "Resources", "ElementalAbilityMaps")
DONOR_MAP = os.path.join(MAPS, "Sparrow.asset")
MAP = os.path.join(MAPS, "Honeyguide.asset")
FUSION = os.path.join(ROOT, "Assets", "Resources", "CrystalHullFusionConfig.asset")
CREW_DIR = os.path.join(ROOT, "Assets", "_Scripts", "Controller", "Vessel", "Crew")
SEAT_SCRIPT_META = os.path.join(CREW_DIR, "HoneyguideCrew.cs.meta")
CONFIG_SCRIPT = os.path.join(CREW_DIR, "HoneyguideConfigSO.cs")
CONFIG_DIR = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "Honeyguide")
CONFIG = os.path.join(CONFIG_DIR, "HoneyguideConfig.asset")
LAB = os.path.join(ROOT, "Docs", "Studios", "HoneyguideLab.html")
GAME_DATA_GUID = "b35f33752bb10a44cb5033b5670f50aa"   # GameData.asset (KiteNavigatorConfig wires the same)

# 19 is the Wrasse, 20 the Orca (cece/happy-noether-m54hz6); 21 was reserved for the Honeyguide on bleeding-edge.
HONEYGUIDE_CLASS_ID = 21
SPARROW_CLASS_ID = 11
PREFAB_GUID = "f6abe5afafae41909d25da175bc23493"
CLASS_GUID = "7fcf1c753fc8440198a6dc26e5d5d63a"
MAP_GUID = "ad162ad2c1c9459592213a738c424fb8"
CONFIG_GUID = "f39120e072fe4d5e9de0ca6e6180d575"
CONFIG_DIR_GUID = "59ab63c3bba049f3b27c75eba5071b21"
ROOT_GO = "8279817603024118164"              # the Sparrow's root GameObject
ROOT_TRANSFORM_ID = "4853044355858455827"
NETWORK_OBJECT_ID = "2386735669300085014"    # the NetworkObject component on it
SEAT_ID = "2121000000000000021"              # HoneyguideCrew on the root
TRIGGER_SCRIPT_META = os.path.join(CREW_DIR, "HoneyguideTriggerActionSO.cs.meta")
DONOR_PECK = os.path.join(ROOT, "Assets", "_SO_Assets", "VesselActions", "Sparrow", "FullAutoAction.asset")
PECK = os.path.join(CONFIG_DIR, "HoneyguidePeckAction.asset")
RT_ACTION = os.path.join(CONFIG_DIR, "HoneyguideRTAction.asset")
LT_ACTION = os.path.join(CONFIG_DIR, "HoneyguideLTAction.asset")
PECK_GUID = "0b7f3c1e5a2d4e6f8a9b1c2d3e4f5a6b"
RT_GUID = "1c8e4d2f6b3e4f7a9b0c2d3e4f5a6b7c"
LT_GUID = "2d9f5e3a7c4f4a8b8c1d3e4f5a6b7c8d"
BOOST_GUID = "8346bd7ce3239e541830985234265532"   # Common Vessel Action/BoostAction.asset
# The Sparrow's: RT ModeSwitchingFire, LT SkyBurstGun, Button1 the turret-stance toggle, and its AIPilot's FullAutoAction
SPARROW_TRIGGER_GUIDS = ["c23cd2f2729292e468689b36e50be258", "7c369aa0e2711144b9ca8c1eccfda538",
                         "af52deeb6df808440a057ec5db100610", "072a19a85e76fe34ab83b891e8536d4d"]

# Cards that are never drawn. Round 6 parks none: the trigger cards are the triggers' upgrades.
PARKED = []

# The lab's ACTIVES use -> HoneyguideTriggerUse
USES = {"press": 0, "hold": 1, "charge": 2}


def guid_of(meta_path: str) -> str:
    with open(meta_path, encoding="utf-8") as fh:
        m = re.search(r"^guid: ([0-9a-f]{32})$", fh.read(), re.M)
    if not m:
        raise RuntimeError(f"{os.path.relpath(meta_path, ROOT)}: no guid")
    return m.group(1)


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    print("wrote", os.path.relpath(path, ROOT))


# ----------------------------------------------------------------------------- the lab's tables
JS_STR = r"'((?:[^'\\]|\\.)*)'"


def js_unescape(s: str) -> str:
    return re.sub(r"\\(.)", r"\1", s)


def lab_block(html: str, name: str) -> str:
    m = re.search(r"^  const %s = [\[{].*\n((?:.*\n)*?)^  [\]}];\n" % name, html, re.M)
    if not m:
        raise RuntimeError(f"HoneyguideLab.html: const {name} not found")
    return m.group(1)


def read_lab(html: str):
    """The lab's tables, as plain Python."""
    lab = {}
    lab["abilities"] = re.findall(r"'(\w+)'", re.search(r"^  const ABILITIES = \[(.*)\];", html, re.M).group(1))
    lab["cardEffects"] = re.findall(r"'(\w+)'", re.search(r"^  const CARD_EFFECTS = \[((?:.|\n)*?)\];", html, re.M).group(1))
    lab["cards"] = [(js_unescape(m.group(1)), int(m.group(2)), m.group(3)) for m in re.finditer(
        r"^    \[%s, (\d), '([MCS0-9, ]+)', %s" % (JS_STR, JS_STR), lab_block(html, "CARDS"), re.M)]
    lab["effects"] = {m.group(1): js_unescape(m.group(2)) for m in re.finditer(
        r"^    (\w+): \{.*text: %s \},$" % JS_STR, lab_block(html, "EFFECTS"), re.M)}
    lab["abilityText"] = {m.group(1): (js_unescape(m.group(2)), js_unescape(m.group(3))) for m in re.finditer(
        r"^    (\w+): \[%s, %s\],$" % (JS_STR, JS_STR), lab_block(html, "ABILITY_TEXT"), re.M)}
    lab["engines"] = [(js_unescape(m.group(2)), re.findall(r"'([^']*)'", m.group(3))) for m in re.finditer(
        r"^    (\w+): \{ label: %s, ports: parseEngine\(\[(.*)\]\) \},$" % JS_STR, lab_block(html, "ENGINES"), re.M)]
    lab["eskill"] = [(float(m.group(2)), int(m.group(3)), float(m.group(4))) for m in re.finditer(
        r"^    (easy|medium|hard): \{ think: ([\d.]+), top: (\d+), blunder: ([\d.]+)", lab_block(html, "ESKILL"), re.M)]
    lab["shipped"] = {m.group(1): m.group(2) for m in re.finditer(r"^    (\w+): (-?[\d.]+),", lab_block(html, "SHIPPED"), re.M)}
    active_rows = list(re.finditer(r"^    (\w+): \{ btn: (\d), use: '(\w+)', cd: ([\d.]+), reach: ([\d.]+), text: %s \},$" % JS_STR,
                                   lab_block(html, "ACTIVES"), re.M))
    lab["actives"] = [(m.group(1), int(m.group(2)), m.group(3), m.group(4), m.group(5)) for m in active_rows]
    # an upgrade card's words are its ACTIVES text (EFFECTS names it: `text: ACTIVES.lasso.text`)
    for m in active_rows:
        if re.search(r"^    %s: \{.*text: ACTIVES\.%s\.text \},$" % (m.group(1), m.group(1)), lab_block(html, "EFFECTS"), re.M):
            lab["effects"][m.group(1)] = js_unescape(m.group(6))
    return lab


def effect_members(cs: str):
    """HoneyguideEffect's members, value by lab key (Plow -> plow, BigRed -> bigred, Printer5D -> printer5d)."""
    body = re.search(r"public enum HoneyguideEffect\s*\{((?:.|\n)*?)\}", cs).group(1)
    return {name.lower(): int(v) for name, v in re.findall(r"^\s*(\w+) = (\d+),", body, re.M)}


# ----------------------------------------------------------------------------- the config asset
def cs_defaults(cs: str):
    """The serialized scalar fields of HoneyguideConfigSO with their C# initializers, in declaration order."""
    out = []
    for m in re.finditer(r"^        \[SerializeField[^\]]*\] (float|int|bool|string|HoneyguideAILevel) (\w+) = (.*);$", cs, re.M):
        t, name, v = m.groups()
        if t == "float":
            v = v.rstrip("f")
        elif t == "bool":
            v = "1" if v == "true" else "0"
        elif t == "HoneyguideAILevel":
            v = {"Easy": "0", "Medium": "1", "Hard": "2"}[v.split(".")[-1]]
        elif t == "string":
            v = yaml_str(js_unescape(v[1:-1].replace("\\n", "\n")))
        out.append((name, t, v))
    return out


def yaml_str(text: str) -> str:
    if "\n" in text:   # a single-quoted YAML scalar folds a line break into a space
        return '"' + text.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'
    return "'" + text.replace("'", "''") + "'"


def num(v: str) -> str:
    f = float(v)
    return str(int(f)) if f == int(f) else repr(f)


EMPTY_EVENT = "    Guid:\n      Data1: 0\n      Data2: 0\n      Data3: 0\n      Data4: 0\n    Path: \n"
SFX = ("patternMadeSfx", "slipSfx", "jumpSfx", "flitSfx", "upgradeSfx", "spinSfx", "revealSfx")


def upgrades_yaml(lab, effects) -> str:
    """The lab's ACTIVES as HoneyguideConfigSO.upgrades (each sfx slot ships empty)."""
    text = "  upgrades:\n"
    for key, btn, use, cd, reach in lab["actives"]:
        text += (f"  - effect: {effects[key]}\n    trigger: {btn}\n    use: {USES[use]}\n    cooldown: {num(cd)}\n"
                 f"    reach: {num(reach)}\n    sfx:\n" + EMPTY_EVENT.replace("\n    ", "\n      ").replace("    Guid", "      Guid", 1))
    return text


def config_asset(lab, cs: str, script_guid: str) -> str:
    fields = cs_defaults(cs)
    effects = effect_members(cs)
    text = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            "  m_Name: HoneyguideConfig\n  m_EditorClassIdentifier: \n")
    for name, t, v in fields:
        if name in lab["shipped"] and t in ("float", "int"):
            v = num(lab["shipped"][name])
        if name == "peckRange":
            text += f"  peckAction: {{fileID: 11400000, guid: {PECK_GUID}, type: 2}}\n"
        text += f"  {name}: {v}\n"
        if name == "activeLevelGain":
            text += upgrades_yaml(lab, effects)
        if name == "upgradeToastTemplate":
            text += "  triggerNames:\n  - RT\n  - LT\n  basePowerNames:\n  - Peck\n  - Flit\n"
        if name == "engineerLevel":
            text += "  engineerSkills:\n" + "".join(f"  - think: {num(str(th))}\n    top: {top}\n    blunder: {num(str(b))}\n"
                                                   for th, top, b in lab["eskill"])
        if name == "aiCrewEngineers":
            text += ("  engineerRect:\n    serializedVersion: 2\n    x: 0.18\n    y: 0.04\n    width: 0.64\n    height: 0.92\n"
                     "  pilotRect:\n    serializedVersion: 2\n    x: 0.01\n    y: 0.3\n    width: 0.2\n    height: 0.42\n"
                     "  colours:\n  - {r: 1, g: 0, b: 0.976, a: 1}\n  - {r: 0.075, g: 1, b: 0.949, a: 1}\n  - {r: 1, g: 0.655, b: 0, a: 1}\n")
        if name == "ratelLabel":
            text += "  colourNames:\n  - Ruby\n  - Jade\n  - Gold\n"
    text += "  patterns:\n"
    for (cname, family, shape), key in zip(lab["cards"], lab["cardEffects"]):
        text += (f"  - name: {yaml_str(cname)}\n    family: {family}\n    shape: {shape}\n    effect: {effects[key]}\n"
                 f"    effectText: {yaml_str(lab['effects'][key])}\n    parked: {1 if key in PARKED else 0}\n")
    text += "  engines:\n"
    for label, rows in lab["engines"]:
        text += f"  - label: {yaml_str(label)}\n    rows:\n" + "".join(f"    - {yaml_str(r)}\n" for r in rows)
    text += "  abilities:\n"
    for k, key in enumerate(lab["abilities"]):
        n, t = lab["abilityText"][key]
        text += f"  - ability: {k}\n    name: {yaml_str(n)}\n    text: {yaml_str(t)}\n"
    for sfx in SFX:
        text += f"  {sfx}:\n" + EMPTY_EVENT
    text += f"  gameData: {{fileID: 11400000, guid: {GAME_DATA_GUID}, type: 2}}\n"
    return text


def yaml_unquote(v: str) -> str:
    v = v.strip()
    return v[1:-1].replace("''", "'") if v.startswith("'") else v


def config_tables(text: str):
    """(patterns [(name, family, shape, effect, effectText, parked)], engines [(label, rows)], abilities [(ability, name, text)])."""
    pats = [(yaml_unquote(m.group(1)), int(m.group(2)), m.group(3).strip(), int(m.group(4)), yaml_unquote(m.group(5)), m.group(6) == "1")
            for m in re.finditer(r"^  - name: (.*)\n    family: (\d)\n    shape: (.*)\n    effect: (\d+)\n    effectText: (.*)\n"
                                 r"    parked: (\d)\n", text, re.M)]
    engines = [(yaml_unquote(m.group(1)), [yaml_unquote(r) for r in re.findall(r"^    - (.*)$", m.group(2), re.M)])
               for m in re.finditer(r"^  - label: (.*)\n    rows:\n((?:    - .*\n)+)", text, re.M)]
    abilities = [(int(m.group(1)), yaml_unquote(m.group(2)), yaml_unquote(m.group(3)))
                 for m in re.finditer(r"^  - ability: (\d)\n    name: (.*)\n    text: (.*)\n", text, re.M)]
    return pats, engines, abilities


def check_config(text: str, lab, cs: str, script_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    effects = effect_members(cs)
    want(f"m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}" in text, "HoneyguideConfig: does not name HoneyguideConfigSO")
    pats, engines, abilities = config_tables(text)
    want(len(lab["cards"]) == 39 and len(lab["cardEffects"]) == 39, f"HoneyguideLab.html: {len(lab['cards'])} cards read, not 39 (the lab moved: fix this reader)")
    want(len(pats) == len(lab["cards"]), f"HoneyguideConfig: {len(pats)} patterns, the lab has {len(lab['cards'])}")
    for k, ((cname, family, shape), key) in enumerate(zip(lab["cards"], lab["cardEffects"])):
        if k >= len(pats):
            break
        p = pats[k]
        want(p[0] == cname and p[1] == family, f"HoneyguideConfig pattern {k}: '{p[0]}' ({p[1]}), the lab's is '{cname}' ({family})")
        want(p[2] == shape, f"HoneyguideConfig pattern {k} ({cname}): shape '{p[2]}', the lab's is '{shape}'")
        want(p[3] == effects.get(key), f"HoneyguideConfig pattern {k} ({cname}): effect {p[3]}, the lab's is {key}")
        want(p[4] == lab["effects"].get(key), f"HoneyguideConfig pattern {k} ({cname}): the effect text is not the lab's")
        want(p[5] == (key in PARKED), f"HoneyguideConfig pattern {k} ({cname}): parked is {p[5]}, should be {key in PARKED}")
    want(engines == lab["engines"], "HoneyguideConfig: the engines are not the lab's ENGINES (the port sets would differ)")
    want(len(abilities) == len(lab["abilities"]) and all(
        a == (k, *lab["abilityText"][key]) for k, (a, key) in enumerate(zip(abilities, lab["abilities"]))),
        "HoneyguideConfig: the Ratel's ability words are not the lab's ABILITY_TEXT, in DynamoAbility order")
    want(f"  gameData: {{fileID: 11400000, guid: {GAME_DATA_GUID}, type: 2}}" in text, "HoneyguideConfig: gameData is not wired")
    for sfx in SFX:
        want(f"  {sfx}:\n" in text, f"HoneyguideConfig: no {sfx} slot")
    want(f"  peckAction: {{fileID: 11400000, guid: {PECK_GUID}, type: 2}}" in text,
         "HoneyguideConfig: peckAction is not HoneyguidePeckAction.asset (RT would do nothing until an upgrade)")
    want(len(lab["actives"]) == 13, f"HoneyguideLab.html: {len(lab['actives'])} ACTIVES read, not 13 (the lab moved: fix this reader)")
    ups = [(int(m.group(1)), int(m.group(2)), int(m.group(3)), float(m.group(4)), float(m.group(5))) for m in re.finditer(
        r"^  - effect: (\d+)\n    trigger: (\d)\n    use: (\d)\n    cooldown: ([\d.]+)\n    reach: ([\d.]+)\n", text, re.M)]
    want(ups == [(effects[k], b, USES[u], float(cd), float(r)) for k, b, u, cd, r in lab["actives"]],
         "HoneyguideConfig: the upgrades are not the lab's ACTIVES (card, trigger, use, cooldown, reach)")
    for key, *_ in lab["actives"]:
        want(key in lab["cardEffects"], f"HoneyguideLab.html: ACTIVES.{key} is no card's effect")
    return errs


# ----------------------------------------------------------------------------- the prefab
def seat_block(script_guid: str) -> str:
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
            f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}\n")


def build_clone(donor: str, script_guid: str) -> str:
    text = donor
    text = replace_once(text, "  m_Name: Sparrow\n", "  m_Name: Honeyguide\n", "root GameObject name")
    text = replace_once(text, "  _name: Sparrow\n", "  _name: Honeyguide\n", "VesselStatus _name")
    text = replace_once(text, f"  vesselType: {SPARROW_CLASS_ID}\n", f"  vesselType: {HONEYGUIDE_CLASS_ID}\n", "vesselType")
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}{global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID)}\n", text, count=1)
    text = re.sub(r"(--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: )\d+\n" % NETWORK_OBJECT_ID,
                  lambda m: f"{m.group(1)}0\n", text, count=1)
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    if not root:
        raise RuntimeError("donor drifted: the root GameObject's m_Component list was not found")
    text = text[:root.end(1)] + f"  - component: {{fileID: {SEAT_ID}}}\n" + text[root.end(1):]
    if not text.endswith("\n"):
        text += "\n"
    return text + seat_block(script_guid)


INPUT_BLOCK = ("  _inputEventShipActions:\n"
               "  - InputEvent: 1\n    ShipActions:\n    - {fileID: 11400000, guid: %s, type: 2}\n"
               "  - InputEvent: 2\n    ShipActions:\n    - {fileID: 11400000, guid: %s, type: 2}\n"
               "  - InputEvent: 7\n    ShipActions:\n    - {fileID: 11400000, guid: %s, type: 2}\n") % (RT_GUID, LT_GUID, BOOST_GUID)
AI_BLOCK = ("  abilities:\n"
            "  - Ability: {fileID: 11400000, guid: %s, type: 2}\n    Duration: 2\n    Cooldown: 10\n") % BOOST_GUID


def retarget_triggers(text: str) -> str:
    """RT and LT to the crew's trigger actions, Button2 the boost; the AIPilot boosts and the crew presses its triggers."""
    text, n = re.subn(r"  _inputEventShipActions:\n(?:  - InputEvent: \d+\n    ShipActions:\n(?:    - \{[^\n]*\}\n)*)+",
                      lambda m: INPUT_BLOCK, text, count=1)
    if n != 1:
        raise RuntimeError("Honeyguide.prefab: _inputEventShipActions not found")
    text, n = re.subn(r"  abilities:\n(?:  - Ability: \{[^\n]*\}\n    Duration: [\d.]+\n    Cooldown: [\d.]+\n)+",
                      lambda m: AI_BLOCK, text, count=1)
    if n != 1:
        raise RuntimeError("Honeyguide.prefab: the AIPilot's abilities list not found")
    return text


def prefab_meta() -> str:
    return ("fileFormatVersion: 2\n"
            f"guid: {PREFAB_GUID}\n"
            "PrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def native_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\n"
            "NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def folder_meta(guid: str) -> str:
    return (f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ----------------------------------------------------------------------------- the class asset
DESCRIPTION = ("  Description: Two seats, one hull, like the honeyguide bird and the honey badger it leads.\n"
               "    The Honeyguide flies the hull and rides trails: touch a trail of a domain the battery holds,\n"
               "    nose along it, and slip to its end. The Ratel plays the Dynamo, a hex puzzle: every pattern\n"
               "    made charges the battery and fires its own card. D-pad left or right swaps seats; the AI\n"
               "    takes the empty one.\n")
CREW = "  CrewNames:\n  - Honeyguide\n  - Ratel\n  CrewNameTemplate: '{0} and {1}'\n"


def class_asset(donor_class: str) -> str:
    text = replace_once(donor_class, "  m_Name: SO_Class_Sparrow\n", "  m_Name: SO_Class_Honeyguide\n", "class m_Name")
    text = replace_once(text, f"  Class: {SPARROW_CLASS_ID}\n", f"  Class: {HONEYGUIDE_CLASS_ID}\n", "class Class")
    text = replace_once(text, "  Name: Sparrow\n", "  Name: Honeyguide\n", "class Name")
    text, n = re.subn(r"  Description: .*\n(?:    .*\n)*", lambda m: DESCRIPTION + CREW, text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class Description was not found")
    text, n = re.subn(r"  isLocked: \d\n", "  isLocked: 0\n  ownedFromStart: 1\n", text, count=1)
    if n != 1:
        raise RuntimeError("donor drifted: the class isLocked line was not found")
    return text


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


# The four cards, charge -> mass -> space -> time (the LOCKED four-icon row). Peck and Flit are the triggers' base powers;
# the Slip is the Honeyguide's ride; the Afterburner and its Elemental Ward are the Sparrow's, unchanged.
MAP_ENTRIES = [
    (1, "Peck", "Hold RT for a weak shot, two and a half a second, out to 45 u. When the Ratel makes an attack card (Hard "
                "Light Lasso, Genesis Lance, Little Bang Grenade, Sepia Drone, Particle Harmonizer, Fugacity Discharger) RT "
                "is that card's for 20 s, then Peck comes back.", 1, "", ""),
    (2, "Flit", "Press LT for a short thrust 18 u ahead, every 2 s. When the Ratel makes a move card (Hexapod Eyelet Jet, "
                "Thruster Swarm, Blink Drive, All Terrain Portals, Rosen Bridge, FTL Chromatool, Monte Cannon) LT is that "
                "card's for 20 s, then Flit comes back.", 2, "", ""),
    (3, "Slip", "Touch a trail of a domain the battery holds with your nose along it and ride it to its end, fast: the "
                "charge is spent. The Ratel charges the battery by making patterns on the Dynamo.", 0, "Piercing Pecks",
     "Pecks pierce through prisms instead of stopping at the first."),
    (4, "Afterburner", "Hold to boost indefinitely - there is no heat and no meter. Boosting at full stick deflection "
                       "performs one strafing roll per press (base, no unlock). Time raises the boost speed; at level 5 "
                       "boosting also makes you immune to elemental debuffs.", 7, "Elemental Ward",
     "While boosting, elemental debuffs cannot land - danger prisms and skimmer overtakes leave your levels untouched. "
     "Buffs still apply."),
]


def build_map(donor_map: str) -> str:
    """The Honeyguide's four cards (MAP_ENTRIES) on the Sparrow's map's header."""
    text = replace_once(donor_map, "  m_Name: Sparrow\n", "  m_Name: Honeyguide\n", "map name")
    text = replace_once(text, f"  vesselClass: {SPARROW_CLASS_ID}\n", f"  vesselClass: {HONEYGUIDE_CLASS_ID}\n", "map vesselClass")
    head = text[:text.index("  entries:\n")]
    body = "  entries:\n" + "".join(
        f"  - Element: {el}\n    AbilityLabel: {yaml_str(label)}\n    AbilityDescription: {yaml_str(desc)}\n    Input: {inp}\n"
        f"    UnlockLevel: 5\n    RelockBelowLevel: 4\n    LatchPolicy: 0\n    UpgradeLabel: {yaml_str(ul) if ul else ''}\n"
        f"    UpgradeDescription: {yaml_str(ud) if ud else ''}\n" for el, label, desc, inp, ul, ud in MAP_ENTRIES)
    return head + body


def peck_asset(donor: str) -> str:
    """The Sparrow's FullAutoAction with the lab's Peck: peckRate shots a second, a range of peckRange (range = speed x
    2T/pi), no growth and no spread (a zero cap and growth is GunSpreadProfile's sanctioned opt-out)."""
    text = replace_once(donor, "  m_Name: FullAutoAction\n", "  m_Name: HoneyguidePeckAction\n", "peck name")
    text = re.sub(r"  firingRate: [\d.]+\n", "  firingRate: 2.5\n", text, count=1)
    text = re.sub(r"  projectileTime: [\d.]+\n", "  projectileTime: 0.3\n", text, count=1)
    text = re.sub(r"  speedValue: [\d.]+\n", "  speedValue: 236\n", text, count=1)
    text = re.sub(r"  growthFactorAtRestingMass: [\d.]+\n", "  growthFactorAtRestingMass: 1\n", text, count=1)
    text = re.sub(r"  growthFactorAtFullMass: [\d.]+\n", "  growthFactorAtFullMass: 1\n", text, count=1)
    text = re.sub(r"    growthDegreesPerSecond: [\d.]+\n", "    growthDegreesPerSecond: 0\n", text, count=1)
    text = re.sub(r"    maxHalfAngleDegrees: [\d.]+\n", "    maxHalfAngleDegrees: 0\n", text, count=1)
    return replace_once(text, "  ammoCost: 0\n", "  ammoCost: 0\n", "peck ammoCost")


def trigger_asset(script_guid: str, name: str, k: int) -> str:
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier: \n  trigger: {k}\n")


def check_actions(peck: str, rt: str, lt: str, trigger_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want("guid: 3e69e441921845c0809b6c506eee66e0" in peck, "HoneyguidePeckAction: not a FullAutoActionSO")
    for key, v in (("firingRate", "2.5"), ("projectileTime", "0.3"), ("speedValue", "236"), ("growthFactorAtRestingMass", "1"),
                   ("growthFactorAtFullMass", "1"), ("ammoCost", "0"), ("maxHalfAngleDegrees", "0")):
        want(f"  {key}: {v}\n" in peck or f"    {key}: {v}\n" in peck, f"HoneyguidePeckAction: {key} is not {v} (the lab's Peck: 2.5 a second, 45 u, no growth)")
    for name, text, k in (("HoneyguideRTAction", rt, 0), ("HoneyguideLTAction", lt, 1)):
        want(f"guid: {trigger_guid}, type: 3" in text and f"  trigger: {k}\n" in text, f"{name}: not HoneyguideTriggerActionSO trigger {k}")
    return errs


def fusion_entries(text: str, vessel: int):
    return re.findall(r"  - vessel: %d\n(?:    .*\n)+" % vessel, text)


def register_fusion(text: str) -> str:
    if fusion_entries(text, HONEYGUIDE_CLASS_ID):
        return text
    donor = fusion_entries(text, SPARROW_CLASS_ID)
    if len(donor) != 4:
        raise RuntimeError(f"CrystalHullFusionConfig: expected 4 Sparrow entries, found {len(donor)}")
    last = None
    for m in re.finditer(r"  - vessel: \d+\n(?:    .*\n)+", text):
        last = m
    added = "".join(e.replace(f"  - vessel: {SPARROW_CLASS_ID}\n", f"  - vessel: {HONEYGUIDE_CLASS_ID}\n", 1) for e in donor)
    return text[:last.end()] + added + text[last.end():]


# ----------------------------------------------------------------------------- checks
def check_prefab(text: str, script_guid: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want("  m_Name: Honeyguide\n" in text, "Honeyguide.prefab: the root is not named Honeyguide")
    want("  _name: Honeyguide\n" in text, "Honeyguide.prefab: VesselStatus _name is not Honeyguide")
    want(f"  vesselType: {HONEYGUIDE_CLASS_ID}\n" in text, f"Honeyguide.prefab: vesselType is not {HONEYGUIDE_CLASS_ID}")
    want(f"  vesselType: {SPARROW_CLASS_ID}\n" not in text, "Honeyguide.prefab: still carries the Sparrow's vesselType")
    h = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  GlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(h and int(h.group(1)) == global_object_id_hash(PREFAB_GUID, NETWORK_OBJECT_ID),
         "Honeyguide.prefab: GlobalObjectIdHash is not this guid's (a copied hash collides with the donor in Netcode)")
    ip = re.search(r"--- !u!114 &%s\n(?:.*\n)*?  InScenePlacedSourceGlobalObjectIdHash: (\d+)\n" % NETWORK_OBJECT_ID, text)
    want(ip and ip.group(1) == "0", "Honeyguide.prefab: InScenePlacedSourceGlobalObjectIdHash is not cleared")
    root = re.search(r"--- !u!1 &%s\nGameObject:\n(?:.*\n)*?  m_Component:\n((?:  - component: \{fileID: -?\d+\}\n)+)" % ROOT_GO, text)
    want(root and f"{{fileID: {SEAT_ID}}}" in root.group(1), "Honeyguide.prefab: HoneyguideCrew is not on the root's component list")
    seat = re.search(r"--- !u!114 &%s\n((?:.*\n)*?)(?=--- !u!|\Z)" % SEAT_ID, text)
    want(seat and f"guid: {script_guid}" in seat.group(1), "Honeyguide.prefab: the HoneyguideCrew document is missing or names another script")
    want(seat and f"  config: {{fileID: 11400000, guid: {CONFIG_GUID}, type: 2}}" in seat.group(1),
         "Honeyguide.prefab: HoneyguideCrew.config is not HoneyguideConfig.asset (a null config never seats the Ratel)")
    want(INPUT_BLOCK in text, "Honeyguide.prefab: RT / LT are not the Honeyguide's triggers (or Button2 is not the boost)")
    want(AI_BLOCK in text, "Honeyguide.prefab: the AIPilot's abilities are not the boost alone (it would fire the Sparrow's guns)")
    for g in SPARROW_TRIGGER_GUIDS:   # its executors may still point at them (default actions, profiles): the input may not
        want(f"  - Ability: {{fileID: 11400000, guid: {g}, type: 2}}" not in text and f"    - {{fileID: 11400000, guid: {g}, type: 2}}" not in text,
             f"Honeyguide.prefab: the input or the AIPilot still names the Sparrow's action {g}")
    return errs


def check_registrations(container: str, network: str, lists, cls: str, kmap: str, fusion: str):
    errs = []
    def want(cond, msg):
        if not cond:
            errs.append(msg)
    want(f"{{fileID: {ROOT_TRANSFORM_ID}, guid: {PREFAB_GUID}, type: 3}}" in container,
         "Vessel Prefab Container: no Honeyguide entry on its root TRANSFORM (the Vessel Changer offers only what is here)")
    want(f"Prefab: {{fileID: {ROOT_GO}, guid: {PREFAB_GUID}, type: 3}}" in network,
         "DefaultNetworkPrefabs: no Honeyguide entry on its root GAMEOBJECT (a swap to it cannot spawn)")
    for name, text in lists:
        want(text.count(CLASS_GUID) == 1, f"{name}: SO_Class_Honeyguide listed {text.count(CLASS_GUID)} times, not once")
    want(f"  Class: {HONEYGUIDE_CLASS_ID}\n" in cls and "  ownedFromStart: 1\n" in cls and "  isLocked: 0\n" in cls,
         "SO_Class_Honeyguide: wrong class, locked, or not owned from the start")
    want(CREW in cls, "SO_Class_Honeyguide: the card does not name both seats (CrewNames Honeyguide, Ratel)")
    want(f"  vesselClass: {HONEYGUIDE_CLASS_ID}\n" in kmap and "  m_Name: Honeyguide\n" in kmap,
         "ElementalAbilityMaps/Honeyguide: wrong class or name")
    want(kmap == build_map(read(DONOR_MAP)), "ElementalAbilityMaps/Honeyguide: its cards are not MAP_ENTRIES (Peck, Flit, Slip, Afterburner)")
    hg, sparrow = fusion_entries(fusion, HONEYGUIDE_CLASS_ID), fusion_entries(fusion, SPARROW_CLASS_ID)
    want(len(hg) == 4, f"CrystalHullFusionConfig: {len(hg)} Honeyguide entries, not 4 (CrystalHullFusionConfigTests needs one per element)")
    want([e.split("\n", 1)[1] for e in hg] == [e.split("\n", 1)[1] for e in sparrow],
         "CrystalHullFusionConfig: the Honeyguide's entries are not the Sparrow's (it draws the Sparrow's mesh)")
    return errs


def config_script_guid():
    return guid_of(CONFIG_SCRIPT + ".meta")


def check_all():
    script_guid = guid_of(SEAT_SCRIPT_META)
    errs = []
    if not os.path.exists(PREFAB) or not os.path.exists(PREFAB + ".meta"):
        return ["Honeyguide.prefab (+ .meta) is missing: run Tools/Build/author_honeyguide_assets.py"]
    errs += [] if guid_of(PREFAB + ".meta") == PREFAB_GUID else ["Honeyguide.prefab.meta: guid is not PREFAB_GUID"]
    errs += check_prefab(read(PREFAB), script_guid)
    for path in (CLASS_ASSET, MAP, CONFIG):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
    errs += [] if guid_of(CONFIG + ".meta") == CONFIG_GUID else ["HoneyguideConfig.asset.meta: guid is not CONFIG_GUID"]
    errs += check_config(read(CONFIG), read_lab(read(LAB)), read(CONFIG_SCRIPT), config_script_guid())
    for path, guid in ((PECK, PECK_GUID), (RT_ACTION, RT_GUID), (LT_ACTION, LT_GUID)):
        if not os.path.exists(path) or not os.path.exists(path + ".meta"):
            return errs + [f"{os.path.relpath(path, ROOT)} (+ .meta) is missing: run the tool"]
        if guid_of(path + ".meta") != guid:
            errs.append(f"{os.path.relpath(path, ROOT)}.meta: guid is not the tool's")
    errs += check_actions(read(PECK), read(RT_ACTION), read(LT_ACTION), guid_of(TRIGGER_SCRIPT_META))
    errs += check_registrations(read(CONTAINER), read(NETWORK_PREFABS),
                                [(os.path.basename(p), read(p)) for p in CLASS_LISTS],
                                read(CLASS_ASSET), read(MAP), read(FUSION))
    return errs


def author():
    script_guid = guid_of(SEAT_SCRIPT_META)
    if not os.path.exists(PREFAB):   # the spent one-shots stand down once committed
        write(PREFAB, build_clone(read(DONOR), script_guid))
        write(PREFAB + ".meta", prefab_meta())
    old = read(PREFAB); new = retarget_triggers(old)   # not a one-shot: the shipped prefab is held to it
    if new != old:
        write(PREFAB, new)
    if not os.path.isdir(CONFIG_DIR):
        os.makedirs(CONFIG_DIR)
        write(CONFIG_DIR + ".meta", folder_meta(CONFIG_DIR_GUID))
    if not os.path.exists(CONFIG):
        write(CONFIG, config_asset(read_lab(read(LAB)), read(CONFIG_SCRIPT), config_script_guid()))
        write(CONFIG + ".meta", native_meta(CONFIG_GUID))
    if not os.path.exists(CLASS_ASSET):
        write(CLASS_ASSET, class_asset(read(DONOR_CLASS))); write(CLASS_ASSET + ".meta", native_meta(CLASS_GUID))
    if not os.path.exists(MAP) or read(MAP) != build_map(read(DONOR_MAP)):   # the cards are this tool's
        write(MAP, build_map(read(DONOR_MAP)))
        if not os.path.exists(MAP + ".meta"):
            write(MAP + ".meta", native_meta(MAP_GUID))
    if not os.path.exists(PECK):
        write(PECK, peck_asset(read(DONOR_PECK))); write(PECK + ".meta", native_meta(PECK_GUID))
    trigger_guid = guid_of(TRIGGER_SCRIPT_META)
    for path, guid, name, k in ((RT_ACTION, RT_GUID, "HoneyguideRTAction", 0), (LT_ACTION, LT_GUID, "HoneyguideLTAction", 1)):
        if not os.path.exists(path):
            write(path, trigger_asset(trigger_guid, name, k)); write(path + ".meta", native_meta(guid))
    for path, fn in [(CONTAINER, register_container), (NETWORK_PREFABS, register_network), (FUSION, register_fusion)] + \
                    [(p, register_class_list) for p in CLASS_LISTS]:
        old = read(path); new = fn(old)
        if new != old:
            write(path, new)


def self_test():
    """Every check must fire on a broken copy of the set (negative controls), and pass on it intact."""
    script_guid = guid_of(SEAT_SCRIPT_META)
    good = retarget_triggers(build_clone(read(DONOR), script_guid))
    fails = []
    def expect(errs, n_min, what):
        if not ((len(errs) == 0) if n_min == 0 else (len(errs) >= n_min)):
            fails.append(f"{what}: {errs}")
    expect(check_prefab(good, script_guid), 0, "a fresh clone passes")
    expect(check_prefab(good.replace(f"  vesselType: {HONEYGUIDE_CLASS_ID}\n", f"  vesselType: {SPARROW_CLASS_ID}\n"), script_guid), 1, "the Sparrow's vesselType")
    expect(check_prefab(re.sub(r"(  GlobalObjectIdHash: )\d+", r"\g<1>1124080984", good, count=1), script_guid), 1, "the donor's hash")
    expect(check_prefab(good.replace(f"  - component: {{fileID: {SEAT_ID}}}\n", ""), script_guid), 1, "the seat off the root")
    expect(check_prefab(good.replace(f"guid: {CONFIG_GUID}, type: 2", "guid: 00000000000000000000000000000000, type: 2"), script_guid), 1, "the seat's config unwired")
    expect(check_prefab(read(DONOR), script_guid), 3, "the Sparrow itself")
    expect(check_prefab(build_clone(read(DONOR), script_guid), script_guid), 3, "a clone that still carries the Sparrow's triggers")
    expect(check_prefab(good.replace(RT_GUID, SPARROW_TRIGGER_GUIDS[0], 1), script_guid), 2, "RT back on the Sparrow's fire")
    if retarget_triggers(good) != good:
        fails.append("retarget_triggers is not idempotent")
    tg = guid_of(TRIGGER_SCRIPT_META)
    peck, rt, lt = peck_asset(read(DONOR_PECK)), trigger_asset(tg, "HoneyguideRTAction", 0), trigger_asset(tg, "HoneyguideLTAction", 1)
    expect(check_actions(peck, rt, lt, tg), 0, "fresh trigger actions pass")
    expect(check_actions(read(DONOR_PECK), rt, lt, tg), 3, "the Sparrow's full-auto as Peck")
    expect(check_actions(peck, lt, rt, tg), 2, "RT and LT swapped")
    if "    maxHalfAngleDegrees: 0\n" not in peck or "    growthDegreesPerSecond: 0\n" not in peck:
        fails.append("the Peck's spread is not off")

    lab, cs, cg = read_lab(read(LAB)), read(CONFIG_SCRIPT), config_script_guid()
    if len(lab["cards"]) != 39 or len(lab["effects"]) != 39 or len(lab["engines"]) != 4 or len(lab["abilityText"]) != 9 or len(lab["eskill"]) != 3:
        fails.append(f"the lab reader: {len(lab['cards'])} cards, {len(lab['effects'])} effects, {len(lab['engines'])} engines, "
                     f"{len(lab['abilityText'])} ability texts, {len(lab['eskill'])} skills (39, 39, 4, 9, 3)")
    if set(lab["cardEffects"]) - set(effect_members(cs)):
        fails.append(f"HoneyguideEffect lacks {sorted(set(lab['cardEffects']) - set(effect_members(cs)))}")
    cfg = config_asset(lab, cs, cg)
    expect(check_config(cfg, lab, cs, cg), 0, "a fresh config passes")
    expect(check_config(cfg.replace("shape: C2,0 M1,1 S0,2 M0,3\n", "shape: C2,0 M1,1 S0,2\n", 1), lab, cs, cg), 1, "a card's shape changed")
    expect(check_config(re.sub(r"(    parked: )0", r"\g<1>1", cfg, count=1), lab, cs, cg), 1, "a live card parked")
    expect(check_config(re.sub(r"(    reach: )90\n", r"\g<1>91\n", cfg, count=1), lab, cs, cg), 1, "an upgrade's reach drifted")
    expect(check_config(re.sub(r"(    trigger: )0\n", r"\g<1>1\n", cfg, count=1), lab, cs, cg), 1, "an attack upgrade on LT")
    expect(check_config(cfg.replace(PECK_GUID, "0" * 32), lab, cs, cg), 1, "Peck unwired")
    if len(lab["actives"]) != 13 or sum(1 for a in lab["actives"] if a[1] == 0) != 6:
        fails.append(f"the lab reader: {len(lab['actives'])} ACTIVES (13: 6 on RT, 7 on LT)")
    expect(check_config(re.sub(r"(    effect: )1\n", r"\g<1>2\n", cfg, count=1), lab, cs, cg), 1, "a card's effect swapped")
    expect(check_config(cfg.replace("    - '. M C C .'\n", "    - '. M C . .'\n", 1), lab, cs, cg), 1, "an engine port dropped")
    expect(check_config(cfg.replace("name: 'Overclock'", "name: 'Overclocked'"), lab, cs, cg), 1, "an ability renamed")
    expect(check_config(cfg.replace(GAME_DATA_GUID, "0" * 32), lab, cs, cg), 1, "gameData unwired")
    for key in ("tickSeconds: 15\n", "usesPerPattern: 3\n", "patternsPerShuffle: 4\n", "aimCone: 45\n", "revealAbility: 3.5\n",
                "peckRange: 45\n", "flitDistance: 18\n", "flitCooldown: 2\n", "activeSeconds: 20\n", "activeLevelGain: 0.25\n"):
        if f"  {key}" not in cfg:
            fails.append(f"the fresh config does not carry the lab's {key.strip()}")

    container, network, fusion = register_container(read(CONTAINER)), register_network(read(NETWORK_PREFABS)), register_fusion(read(FUSION))
    lists = [(os.path.basename(p), register_class_list(read(p))) for p in CLASS_LISTS]
    cls, kmap = class_asset(read(DONOR_CLASS)), build_map(read(DONOR_MAP))
    expect(check_registrations(container, network, lists, cls, kmap, fusion), 0, "a registered set passes")
    expect(check_registrations(read(CONTAINER).replace(PREFAB_GUID, "x"), network, lists, cls, kmap, fusion), 1, "no container entry")
    expect(check_registrations(container, read(NETWORK_PREFABS).replace(PREFAB_GUID, "x"), lists, cls, kmap, fusion), 1, "no network entry")
    expect(check_registrations(container, network, [(n, t.replace(CLASS_GUID, "x")) for n, t in lists], cls, kmap, fusion), 1, "no class list entry")
    expect(check_registrations(container, network, lists, cls.replace("  isLocked: 0\n", "  isLocked: 1\n"), kmap, fusion), 1, "a locked class")
    expect(check_registrations(container, network, lists, cls.replace(CREW, ""), kmap, fusion), 1, "a card without its crew names")
    expect(check_registrations(container, network, lists, cls, kmap, fusion.replace(f"  - vessel: {HONEYGUIDE_CLASS_ID}\n", "  - vessel: 99\n", 1)), 1, "a missing fusion entry")
    expect(check_registrations(container, network, lists, cls, read(DONOR_MAP), fusion), 1, "the Sparrow's own map")
    expect(check_registrations(container, network, lists, cls, kmap.replace("AbilityLabel: 'Peck'", "AbilityLabel: 'Pulsefire'"), fusion), 1,
           "a card renamed")
    if register_container(container) != container or register_fusion(fusion) != fusion or register_network(network) != network:
        fails.append("registration is not idempotent")
    for f in fails:
        print("SELF-TEST FAIL:", f)
    print("author_honeyguide_assets self-test:", "ok" if not fails else f"{len(fails)} failure(s)")
    return 1 if fails else 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        author()
    errs = check_all()
    for e in errs:
        print("FAIL:", e)
    print("author_honeyguide_assets:", "ok" if not errs else f"{len(errs)} problem(s)")
    return 1 if errs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
