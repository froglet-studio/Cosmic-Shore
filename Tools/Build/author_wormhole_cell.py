#!/usr/bin/env python3
"""Author the WORMHOLE cell - a Cell Selector world whose environment is two sphere mouths that
are one place (Assets/_Scripts/Controller/Environment/Wormhole/, Docs/WORMHOLE_CELL.md).

Emits, deterministically (guid = md5 of a stable name, so a re-run never re-mints a reference):

  Assets/_Scripts/Controller/Environment/Wormhole/*.cs.meta     the four runtime scripts' metas
  Assets/_Scripts/Tests/Editor/WormholeGeometryTests.cs.meta    the edit-mode tests' meta
  Assets/_Graphics/Materials/Graphs/Wormhole.shader.meta        the surface shader's meta
  Assets/_Graphics/Materials/Wormhole.mat                       the surface material
  Assets/_Prefabs/Spawnables/SpawnableWormholes.prefab          the environment (SpawnableWormholePair)
  Assets/_SO_Assets/Cell Configs/Wormhole Cell/...Config.asset  the cell
  ...and appends that config to Menu_Main's Cell.CellConfigs - the Cell Selector authors no list
  of its own, it reads the cell's rotation (CellSelectorToy.BuildOptions).

  It also wires the BUTTERFLY's fold onto the same material: every fold leaves a domain-locked
  wormhole pair (they replaced the ring gates - BUTTERFLY_FOLD.md § "The gates became wormholes"),
  so ButterflyFoldAction.asset gains wormholeMaterial / portalWindowFadeBand / panoramaFaceSize
  and loses the two ring-only keys (gateExitClearance, portalWindowFadeSeconds). Tuning values a
  designer may change are only ADDED when absent; the material reference is enforced.

The cell is open water with the shared freestyle population (the Blob Cell spawn profile every
authored freestyle world uses) and NO prisms in its environment, so its PhaseThresholds are the
Blob deltas over a zero baseline - the same rule every freestyle world is authored on
(Docs/ECOSYSTEM.md §18: Restless = baseline + 700 / +11,200 volume, Frenzy = baseline + 3,600 /
+57,600), read here off the Caldera's authored ladder rather than retyped.

Validates BEFORE writing anything: every serialized key in the prefab is a field of
SpawnableWormholePair or its base; every referenced guid is owned by exactly one .meta; and
every new guid is owned by no OTHER .meta.

    python3 Tools/Build/author_wormhole_cell.py           # write / re-sync
    python3 Tools/Build/author_wormhole_cell.py --check   # exit 1 on any drift
"""
import argparse
import glob
import hashlib
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
A = lambda *p: os.path.join(REPO, "Assets", *p)

WORMHOLE_DIR = A("_Scripts", "Controller", "Environment", "Wormhole")
SCRIPTS = ["WormholeGeometry", "WormholeMouth", "WormholeView", "SpawnableWormholePair"]
TEST = A("_Scripts", "Tests", "Editor", "WormholeGeometryTests.cs")
SHADER = A("_Graphics", "Materials", "Graphs", "Wormhole.shader")
MATERIAL = A("_Graphics", "Materials", "Wormhole.mat")
PREFAB = A("_Prefabs", "Spawnables", "SpawnableWormholes.prefab")
CELL_DIR = A("_SO_Assets", "Cell Configs", "Wormhole Cell")
CONFIG = os.path.join(CELL_DIR, "Wormhole Cell Config.asset")
MENU_SCENE = A("_Scenes", "Menu_Main.unity")
FOLD_ACTION = A("_SO_Assets", "VesselActions", "Butterfly", "ButterflyFoldAction.asset")
FOLD_ACTION_SCRIPT = A("_Scripts", "Controller", "Vessel", "R_VesselActions", "Data Containers", "FoldActionSO.cs")
FOLD_RETIRED_KEYS = ("gateExitClearance", "portalWindowFadeSeconds")
FOLD_ADDED_DEFAULTS = (("portalWindowFadeBand", "600"), ("panoramaFaceSize", "256"))

# References INTO existing assets - each is resolved and asserted below, never trusted.
CELL_CONFIG_SCRIPT = A("_Scripts", "Utility", "DataContainers", "CellConfigDataSO.cs")
DONOR_CONFIG = A("_SO_Assets", "Cell Configs", "Barren Cell", "Barren Cell Config.asset")
CALDERA_CONFIG = A("_SO_Assets", "Cell Configs", "Caldera Cell", "Caldera Cell Config.asset")
SPAWN_PROFILE = A("_SO_Assets", "Cell Configs", "Blob Cell", "Blob Cell Spawn Profile.asset")
GAME_DATA = A("_SO_Assets", "Game Data", "Runtime GameData.asset")
SPAWNABLE_BASE = A("_Scripts", "Controller", "Environment", "Spawning", "SpawnableBase.cs")

PREFAB_GO, PREFAB_TR, PREFAB_MB = 5240000000000101, 5240000000000102, 5240000000000103

# The Caldera's measured baseline (Docs/ECOSYSTEM.md §18) - its authored ladder minus this is
# the Blob delta set every freestyle world rides on.
CALDERA_BASELINE = (41353, 1210753)


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def guid_for(name):
    """Deterministic asset GUID, so re-running never re-mints references."""
    return hashlib.md5(f"cosmicshore/wormhole-cell/{name}".encode()).hexdigest()


def meta_guid(path):
    m = re.search(r"^guid: ([0-9a-f]{32})$", read(path + ".meta"), re.M)
    if not m:
        raise SystemExit(f"no guid in {path}.meta")
    return m.group(1)


FOLDER_META = """fileFormatVersion: 2
guid: %s
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

SCRIPT_META = """fileFormatVersion: 2
guid: %s
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

SHADER_META = """fileFormatVersion: 2
guid: %s
ShaderImporter:
  externalObjects: {}
  defaultTextures: []
  nonModifiableTextures: []
  preprocessorOverride: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

NATIVE_META = """fileFormatVersion: 2
guid: %s
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: %d
  userData:
  assetBundleName:
  assetBundleVariant:
"""

PREFAB_META = """fileFormatVersion: 2
guid: %s
PrefabImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def material_text():
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Wormhole
  m_Shader: {{fileID: 4800000, guid: {guid_for('shader')}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _DomainRimBoost: 2
    - _FlareIntensity: 2.5
    - _ProxyRadius: 600
    - _RimDarken: 0.25
    - _RimIntensity: 0.35
    - _RimPower: 6
    - _SealedIntensity: 1.5
    - _SealedRimCutoff: 0.2
    - _SealedRimPower: 2.5
    m_Colors:
    - _RimColor: {{r: 0.45, g: 0.75, b: 1.6, a: 1}}
    - _VoidColor: {{r: 0.01, g: 0.015, b: 0.04, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocalizedText: 1
"""


def prefab_text():
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &{PREFAB_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {PREFAB_TR}}}
  - component: {{fileID: {PREFAB_MB}}}
  m_Layer: 0
  m_Name: SpawnableWormholes
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{PREFAB_TR}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {PREFAB_GO}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 0}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!114 &{PREFAB_MB}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {PREFAB_GO}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid_for('SpawnableWormholePair')}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  seed: 0
  domain: 3
  children: []
  leafPrefab: {{fileID: 0}}
  layAcrossFrames: 0
  layBudgetMsPerFrame: 6
  intensityLevel: 1
  mouthAPosition: {{x: 0, y: 0, z: 620}}
  mouthBPosition: {{x: -700, y: 450, z: -480}}
  mouthRadius: 70
  bloomSeconds: 1.5
  surfaceMaterial: {{fileID: 2100000, guid: {guid_for('material')}, type: 2}}
  exactRange: 1500
  exactFadeBand: 400
  exactRenderScale: 0.75
  panoramaFaceSize: 256
  transitEvent:
    Guid:
      Data1: 0
      Data2: 0
      Data3: 0
      Data4: 0
    Path:
  gameData: {{fileID: 11400000, guid: {meta_guid(GAME_DATA)}, type: 2}}
  modelPointsPerMouth: 420
  modelThroatPoints: 40
"""


def ladder():
    """Blob deltas over a zero baseline, read off the Caldera's authored ladder."""
    text = read(CALDERA_CONFIG)
    out = {}
    for key in ("RestlessEnter", "RestlessExit", "FrenzyEnter", "FrenzyExit"):
        v = int(re.search(rf"^    {key}: (\d+)$", text, re.M).group(1))
        out[key] = v - CALDERA_BASELINE[0]
        vv = int(re.search(rf"^    {key}Volume: (\d+)$", text, re.M).group(1))
        out[key + "Volume"] = vv - CALDERA_BASELINE[1]
    assert out["RestlessEnter"] == 700 and out["FrenzyEnter"] == 3600, \
        f"Caldera's ladder no longer encodes the Blob deltas over {CALDERA_BASELINE}: {out}"
    return out


def config_text():
    donor = read(DONOR_CONFIG)
    script = re.search(r"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}", donor).group(1)
    assert script == meta_guid(CELL_CONFIG_SCRIPT), "Barren config is not a CellConfigDataSO"

    def donor_line(key):
        m = re.search(rf"^  {key}: (\{{fileID: \d+, guid: [0-9a-f]{{32}},\n?\s*type: \d\}})$", donor, re.M)
        assert m, f"donor config has no {key}"
        return " ".join(m.group(1).split())

    L = ladder()
    return f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}
  m_Name: Wormhole Cell Config
  m_EditorClassIdentifier:
  CellName: Wormhole
  Description: Two spheres that are one place. Fly into either and come out of the other.
  Icon: {donor_line('Icon')}
  Difficulty: 2
  CellEndGameScore: 0
  MembranePrefab: {donor_line('MembranePrefab')}
  NucleusPrefab: {donor_line('NucleusPrefab')}
  CytoplasmPrefab: {donor_line('CytoplasmPrefab')}
  CellModifiers: []
  SpawnProfile: {{fileID: 11400000, guid: {meta_guid(SPAWN_PROFILE)}, type: 2}}
  EnvironmentPrefab: {{fileID: {PREFAB_MB}, guid: {guid_for('prefab')}, type: 3}}
  EnvironmentIntensity: 1
  SenseRadiusOverride: 0
  PhaseThresholds:
    RestlessEnter: {L['RestlessEnter']}
    RestlessExit: {L['RestlessExit']}
    FrenzyEnter: {L['FrenzyEnter']}
    FrenzyExit: {L['FrenzyExit']}
    RestlessEnterVolume: {L['RestlessEnterVolume']}
    RestlessExitVolume: {L['RestlessExitVolume']}
    FrenzyEnterVolume: {L['FrenzyEnterVolume']}
    FrenzyExitVolume: {L['FrenzyExitVolume']}
"""


def outputs():
    """path -> text for every file this script owns."""
    out = {
        WORMHOLE_DIR + ".meta": FOLDER_META % guid_for("folder/wormhole-scripts"),
        CELL_DIR + ".meta": FOLDER_META % guid_for("folder/cell"),
        TEST + ".meta": SCRIPT_META % guid_for("WormholeGeometryTests"),
        SHADER + ".meta": SHADER_META % guid_for("shader"),
        MATERIAL: material_text(),
        MATERIAL + ".meta": NATIVE_META % (guid_for("material"), 2100000),
        PREFAB: prefab_text(),
        PREFAB + ".meta": PREFAB_META % guid_for("prefab"),
        CONFIG: config_text(),
        CONFIG + ".meta": NATIVE_META % (guid_for("config"), 11400000),
    }
    for s in SCRIPTS:
        out[os.path.join(WORMHOLE_DIR, s + ".cs.meta")] = SCRIPT_META % guid_for(s)
    return out


# ── Validation ────────────────────────────────────────────────────────────────

SERIALIZED = re.compile(
    r'\[SerializeField(?:[^"\]]|"(?:[^"\\]|\\.)*")*\]\s*'
    r'(?:(?:public|private|protected|internal|readonly)\s+)*'
    r'[\w\.<>]+(?:\[\])?\s+(\w+)\s*(?:=(?!>)|;)')
PUBLIC_FIELD = re.compile(
    r'^\s*(?:\[[^\]\n]*\]\s*)*public\s+(?!static|const|override|abstract|virtual|sealed|class|enum|struct|interface)'
    r'[\w\.<>]+(?:\[\])?\s+(\w+)\s*(?:=(?!>)|;)', re.M)


def fields(cs_path):
    src = read(cs_path)
    return set(SERIALIZED.findall(src)) | set(PUBLIC_FIELD.findall(src))


def all_meta_owners():
    owners = {}
    for meta in glob.glob(os.path.join(REPO, "Assets", "**", "*.meta"), recursive=True):
        m = re.search(r"^guid: ([0-9a-f]{32})$", read(meta), re.M)
        if m:
            owners.setdefault(m.group(1), []).append(meta)
    return owners


def validate(out):
    problems = []

    for path in [SHADER, TEST] + [os.path.join(WORMHOLE_DIR, s + ".cs") for s in SCRIPTS]:
        if not os.path.exists(path):
            problems.append(f"missing source: {os.path.relpath(path, REPO)}")

    # Prefab keys are exactly the C#'s serialized fields (both directions).
    want = fields(os.path.join(WORMHOLE_DIR, "SpawnableWormholePair.cs")) | fields(SPAWNABLE_BASE)
    body = prefab_text().split("--- !u!114 ")[1]
    keys = set(re.findall(r"^  (\w+):", body, re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"}
    for k in sorted(keys - want):
        problems.append(f"prefab key '{k}' is not a serialized field of SpawnableWormholePair")
    for k in sorted(want - keys):
        problems.append(f"SpawnableWormholePair field '{k}' is not authored in the prefab")

    # Config keys are fields of CellConfigDataSO.
    cfg_fields = fields(CELL_CONFIG_SCRIPT)
    cfg_keys = set(re.findall(r"^  ([A-Za-z]\w*):", config_text(), re.M)) - {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"}
    for k in sorted(cfg_keys - cfg_fields):
        problems.append(f"config key '{k}' is not a field of CellConfigDataSO")

    # Every guid we OWN is owned by our meta and nobody else's; every guid we REFERENCE resolves.
    owners = all_meta_owners()
    ours = {}
    for path, text in out.items():
        if path.endswith(".meta"):
            g = re.search(r"^guid: ([0-9a-f]{32})$", text, re.M).group(1)
            assert g not in ours, f"guid collision inside the set: {g}"
            ours[g] = path
    for g, path in ours.items():
        foreign = [o for o in owners.get(g, []) if os.path.abspath(o) != os.path.abspath(path)]
        if foreign:
            problems.append(f"guid {g} ({os.path.relpath(path, REPO)}) is ALSO owned by {foreign}")
    for path, text in out.items():
        if path.endswith(".meta"):
            continue
        for g in set(re.findall(r"guid: ([0-9a-f]{32})", text)):
            if g in ours:
                continue
            if len(owners.get(g, [])) != 1:
                problems.append(f"{os.path.relpath(path, REPO)} references guid {g}, owned by "
                                f"{len(owners.get(g, []))} .meta files")
    return problems


# ── The Cell Selector's list (Menu_Main's Cell.CellConfigs) ─────────────────────

def scene_state():
    text = read(MENU_SCENE)
    m = re.search(r"propertyPath: CellConfigs\.Array\.size\n      value: (\d+)", text)
    size = int(m.group(1)) if m else -1
    return size, guid_for("config") in text


def scene_patch(text, guid):
    """Append one entry to CellConfigs, preserving every existing one (author_arboretum_cell's)."""
    m = re.search(r"( *- target: \{fileID: (\d+), guid: ([a-f0-9]{32}),\n"
                  r" *type: 3\}\n *propertyPath: CellConfigs\.Array\.size\n"
                  r" *value: )(\d+)(\n)", text)
    if not m:
        raise SystemExit("Menu_Main: no CellConfigs.Array.size override to extend")
    size = int(m.group(4))
    target_fid, target_guid = m.group(2), m.group(3)
    entries = re.findall(r"propertyPath: 'CellConfigs\.Array\.data\[(\d+)\]'", text)
    assert sorted(map(int, entries)) == list(range(size)), \
        f"Menu_Main: CellConfigs entries {entries} are not exactly 0..{size - 1}"
    text = text[:m.start(4)] + str(size + 1) + text[m.end(4):]

    last = None
    for hit in re.finditer(r" *- target: \{fileID: \d+, guid: [a-f0-9]{32},\n"
                           r" *type: 3\}\n *propertyPath: 'CellConfigs\.Array\.data\[\d+\]'\n"
                           r" *value: \n *objectReference: \{fileID: 11400000, guid: [a-f0-9]{32},\n"
                           r" *type: 2\}\n", text):
        last = hit
    if last is None:
        raise SystemExit("Menu_Main: no CellConfigs entry to append after")
    entry = (f"    - target: {{fileID: {target_fid}, guid: {target_guid},\n"
             f"        type: 3}}\n"
             f"      propertyPath: 'CellConfigs.Array.data[{size}]'\n"
             f"      value: \n"
             f"      objectReference: {{fileID: 11400000, guid: {guid},\n"
             f"        type: 2}}\n")
    out = text[:last.end()] + entry + text[last.end():]
    entries = re.findall(r"propertyPath: 'CellConfigs\.Array\.data\[(\d+)\]'", out)
    assert sorted(map(int, entries)) == list(range(size + 1)), "append did not land contiguously"
    return out


# ── The Butterfly's fold: its pair is a wormhole now ─────────────────────────────

def fold_patch(text):
    """ButterflyFoldAction.asset as it should be: retired ring keys gone, wormhole keys present."""
    for key in FOLD_RETIRED_KEYS:
        text = re.sub(rf"^  {key}: [^\n]*\n", "", text, flags=re.M)
    material = f"{{fileID: 2100000, guid: {guid_for('material')}, type: 2}}"
    anchor = re.search(r"^  portalWindowRenderScale: [^\n]*\n", text, re.M)
    assert anchor, "ButterflyFoldAction.asset has no portalWindowRenderScale to anchor on"
    insert = ""
    for key, value in FOLD_ADDED_DEFAULTS:
        if not re.search(rf"^  {key}: ", text, re.M):
            insert += f"  {key}: {value}\n"
    if re.search(r"^  wormholeMaterial: ", text, re.M):
        text = re.sub(r"^  wormholeMaterial: [^\n]*\n", f"  wormholeMaterial: {material}\n", text, flags=re.M)
    else:
        insert += f"  wormholeMaterial: {material}\n"
    return text[:anchor.end()] + insert + text[anchor.end():]


def fold_problems(text):
    problems = []
    keys = set(re.findall(r"^  ([A-Za-z]\w*):", text.split("m_EditorClassIdentifier:")[1], re.M))
    want = fields(FOLD_ACTION_SCRIPT)
    for k in sorted(keys - want):
        problems.append(f"ButterflyFoldAction.asset key '{k}' is not a field of FoldActionSO")
    for k in ("wormholeMaterial", "portalWindowFadeBand", "panoramaFaceSize"):
        if k not in keys:
            problems.append(f"ButterflyFoldAction.asset does not author '{k}'")
    return problems


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    out = outputs()
    problems = validate(out)
    fold_now = read(FOLD_ACTION)
    fold_want = fold_patch(fold_now)
    problems += fold_problems(fold_want)
    size, listed = scene_state()
    L = ladder()
    print("Wormhole cell - two mouths, no prisms; Blob spawn profile; ladder = Blob deltas over 0:")
    print("  " + ", ".join(f"{k} {v:,}" for k, v in L.items()))

    if args.check:
        drift = [p for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        if not listed:
            drift.append(f"{MENU_SCENE} (Cell.CellConfigs, size {size})")
        if fold_now != fold_want:
            drift.append(FOLD_ACTION)
        if drift or problems:
            print("FAIL")
            for p in problems:
                print(f"  - {p}")
            for d in drift:
                print(f"  - differs from what this script authors: {os.path.relpath(d, REPO) if os.path.isabs(d) else d}")
            return 1
        print("OK - every asset matches, the Wormhole cell is in the Cell Selector, and the "
              "Butterfly's fold lays wormholes.")
        return 0

    if problems:
        print("FAIL - refusing to write:")
        for p in problems:
            print(f"  - {p}")
        return 1

    os.makedirs(CELL_DIR, exist_ok=True)
    for path, text in out.items():
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
    print(f"wrote {len(out)} files")
    if fold_now != fold_want:
        with open(FOLD_ACTION, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(fold_want)
        print("  Butterfly fold: ButterflyFoldAction.asset wired to the wormhole material")

    if listed:
        print("  Cell Selector: already listed in Menu_Main")
    else:
        text = read(MENU_SCENE)
        with open(MENU_SCENE, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(scene_patch(text, guid_for("config")))
        print(f"  Cell Selector: appended to Menu_Main's Cell.CellConfigs (size {size} -> {size + 1})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
