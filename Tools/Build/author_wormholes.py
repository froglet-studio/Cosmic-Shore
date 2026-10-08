#!/usr/bin/env python3
"""Author the assets behind the Butterfly's fold WORMHOLES (Assets/_Scripts/Controller/Environment/
Wormhole/, Docs/WORMHOLES.md, BUTTERFLY_FOLD.md § "The gates became wormholes").

Emits, deterministically (guid = md5 of a stable name, so a re-run never re-mints a reference):

  Assets/_Scripts/Controller/Environment/Wormhole(.meta, /*.cs.meta)  the runtime scripts' metas
  Assets/_Scripts/Tests/Editor/WormholeGeometryTests.cs.meta          the edit-mode tests' meta
  Assets/_Graphics/Materials/Graphs/Wormhole.shader.meta              the surface shader's meta
  Assets/_Graphics/Materials/Wormhole.mat                             the surface material

and wires ButterflyFoldAction.asset onto that material: every fold leaves a domain-tolled wormhole
pair (they replaced the ring gates; anyone rides, a rival pays petals), so the asset carries
wormholeMaterial / portalWindowFadeBand / panoramaFaceSize / rivalTollPetalsPerElement /
rivalTollShedSpeed and not the two ring-only keys (gateExitClearance, portalWindowFadeSeconds).
Tuning values a designer may change are only ADDED when absent; the material reference is enforced.

History: this began as author_wormhole_cell.py, which also authored a "Wormhole" Cell Selector
world used to prototype the idea. That cell is retired (the fold carries the idea), and --check now
asserts it stays gone from Menu_Main. The guid salt below is kept from that script on purpose:
changing it would re-mint every guid above and dangle the committed references.

Validates BEFORE writing anything: the fold asset's keys are fields of FoldActionSO; every guid
this script owns is owned by no OTHER .meta; every guid it references resolves.

    python3 Tools/Build/author_wormholes.py           # write / re-sync
    python3 Tools/Build/author_wormholes.py --check   # exit 1 on any drift
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
SCRIPTS = ["WormholeGeometry", "WormholeMouth", "WormholeView"]
TEST = A("_Scripts", "Tests", "Editor", "WormholeGeometryTests.cs")
SHADER = A("_Graphics", "Materials", "Graphs", "Wormhole.shader")
MATERIAL = A("_Graphics", "Materials", "Wormhole.mat")
FOLD_ACTION = A("_SO_Assets", "VesselActions", "Butterfly", "ButterflyFoldAction.asset")
FOLD_ACTION_SCRIPT = A("_Scripts", "Controller", "Vessel", "R_VesselActions", "Data Containers", "FoldActionSO.cs")
FOLD_RETIRED_KEYS = ("gateExitClearance", "portalWindowFadeSeconds")
FOLD_ADDED_DEFAULTS = (("portalWindowFadeBand", "600"), ("panoramaFaceSize", "256"))
# The rival toll's tuning: anchored after gateSettleSeconds (the order FoldActionSO declares them),
# and like the pair above only ADDED when absent, so a designer's retune is never overwritten.
FOLD_TOLL_DEFAULTS = (("rivalTollPetalsPerElement", "15"), ("rivalTollShedSpeed", "25"))
MENU_SCENE = A("_Scenes", "Menu_Main.unity")


def read(path):
    with open(path, encoding="utf-8", errors="ignore") as fh:
        return fh.read()


def guid_for(name):
    """Deterministic asset GUID. The salt says "wormhole-cell" for history's sake - see above."""
    return hashlib.md5(f"cosmicshore/wormhole-cell/{name}".encode()).hexdigest()


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




def outputs():
    """path -> text for every file this script owns."""
    out = {
        WORMHOLE_DIR + ".meta": FOLDER_META % guid_for("folder/wormhole-scripts"),
        TEST + ".meta": SCRIPT_META % guid_for("WormholeGeometryTests"),
        SHADER + ".meta": SHADER_META % guid_for("shader"),
        MATERIAL: material_text(),
        MATERIAL + ".meta": NATIVE_META % (guid_for("material"), 2100000),
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

    # The retired prototype cell must stay retired: its config guid in Menu_Main would be a
    # dangling Cell Selector entry (the asset is deleted).
    if guid_for("config") in read(MENU_SCENE):
        problems.append("Menu_Main still lists the retired Wormhole cell config")
    return problems


# ── The Butterfly's fold: its pair is a wormhole ──────────────────────────────────

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
    text = text[:anchor.end()] + insert + text[anchor.end():]
    toll = re.search(r"^  gateSettleSeconds: [^\n]*\n", text, re.M)
    assert toll, "ButterflyFoldAction.asset has no gateSettleSeconds to anchor the toll on"
    insert = "".join(f"  {k}: {v}\n" for k, v in FOLD_TOLL_DEFAULTS
                     if not re.search(rf"^  {k}: ", text, re.M))
    return text[:toll.end()] + insert + text[toll.end():]


def fold_problems(text):
    problems = []
    keys = set(re.findall(r"^  ([A-Za-z]\w*):", text.split("m_EditorClassIdentifier:")[1], re.M))
    want = fields(FOLD_ACTION_SCRIPT)
    for k in sorted(keys - want):
        problems.append(f"ButterflyFoldAction.asset key '{k}' is not a field of FoldActionSO")
    for k in ("wormholeMaterial", "portalWindowFadeBand", "panoramaFaceSize",
              "rivalTollPetalsPerElement", "rivalTollShedSpeed"):
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

    if args.check:
        drift = [p for p, t in out.items() if not os.path.exists(p) or read(p) != t]
        if fold_now != fold_want:
            drift.append(FOLD_ACTION)
        if drift or problems:
            print("FAIL")
            for p in problems:
                print(f"  - {p}")
            for d in drift:
                print(f"  - differs from what this script authors: {os.path.relpath(d, REPO)}")
            return 1
        print("OK - the wormhole assets match, and the Butterfly's fold is wired to them.")
        return 0

    if problems:
        print("FAIL - refusing to write:")
        for p in problems:
            print(f"  - {p}")
        return 1

    for path, text in out.items():
        with open(path, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
    print(f"wrote {len(out)} files")
    if fold_now != fold_want:
        with open(FOLD_ACTION, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(fold_want)
        print("  Butterfly fold: ButterflyFoldAction.asset wired to the wormhole material")
    return 0


if __name__ == "__main__":
    sys.exit(main())
