#!/usr/bin/env python3
"""
Authors the ELEMENTAL CONVERTER on the Ark screen of Menu_Main (designer, 2026-10-13: "give me an elemental
converter in app shell. place it in the ark which you will need to reactivate. for now it should just play
this animation when i use it").

What it writes (each idempotent; `--check` fails if any is stale):

  1. Assets/_SO_Assets/Environment/OmniCombine/OmniCombineConfig.asset - the OmniCombineConfigSO the converter
     plays: the combine data (OmniCombineData.json, from author_omni_combine_asset.py), the crystals' shipped
     materials, the palette (each crystal flies in its own shade of lime, ElementalCrystalColors, and lands in the
     omni's), and the per-play ranges ("separate and slow": the next crystal sets off as the last lands).
  2. The .meta of each new script (deterministic guids, so a re-run never re-mints one).
  3. Assets/_Scenes/Menu_Main.unity:
       - ScreenSwitcher.disabledScreens [ARK, PORT] -> [PORT]: the Ark screen opens.
       - ArkScreen's Image (the "SQUAD" design mockup it carried as a background) is switched off.
       - ArkScreen's OfflineUIGate is removed: the converter is a local animation and works offline (the gate
         dimmed the whole screen for the long-disabled store; OfflineMenuWirer no longer targets it).
       - A child `ElementalConverter` (RectTransform stretched over the screen + ElementalConverterView, wired to
         the config and the menu's font). The view builds its picture, title and CONVERT button in code.

Every scene edit is validated before anything is written: the new fileIDs are unique and fit a signed int64,
the child is in ArkScreen's m_Children and points back at it, the component is on its own GameObject and
points back, the removed gate leaves no reference, and the file's set of dangling local references is
unchanged.

    python3 Tools/Build/author_elemental_converter.py            # write
    python3 Tools/Build/author_elemental_converter.py --check    # exit 1 if anything is stale
    python3 Tools/Build/author_elemental_converter.py --self-test
"""

import argparse
import hashlib
import os
import re
import subprocess
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCENE = os.path.join(ROOT, "Assets", "_Scenes", "Menu_Main.unity")
FOLDER = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "OmniCombine")
CONFIG = os.path.join(FOLDER, "OmniCombineConfig.asset")
DATA_META = os.path.join(FOLDER, "OmniCombineData.json.meta")
MATS = os.path.join(ROOT, "Assets", "_Graphics", "Materials", "CrystalMaterials")
TRI_MESH = os.path.join(ROOT, "Assets", "_Models", "OmniCrystalTriangles.asset")
DEPTH_PROFILE = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "CrystalDepthPrismProfile.asset")
CADENCE = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "OmniCrystalCadence.asset")
CHARGE_ARCS_PROFILE = os.path.join(ROOT, "Assets", "_SO_Assets", "Environment", "CrystalChargeArcsProfile.asset")
PALETTE = os.path.join(ROOT, "Assets", "_SO_Assets", "Color Palettes", "OriginalColorSetSO.asset")
SCRIPTS = {
    "OmniCombineCore": "Assets/_Scripts/Utility/OmniCombineCore.cs",
    "OmniCombineMotion": "Assets/_Scripts/Utility/OmniCombineMotion.cs",
    "OmniCombineConfigSO": "Assets/_Scripts/Controller/Environment/Crystals/OmniCombineConfigSO.cs",
    "OmniCombinePlayer": "Assets/_Scripts/Controller/Environment/Crystals/OmniCombinePlayer.cs",
    "ElementalConverterView": "Assets/_Scripts/UI/Elements/ElementalConverterView.cs",
    "OmniCombineTests": "Assets/_Scripts/Tests/Editor/OmniCombineTests.cs",
}
MENU_FONT = "6ab8eca0e6e2b7c4a8a495d9afae2053"     # ALDRICH-REGULAR SDF, the font of 346 of Menu_Main's labels

# Menu_Main objects this edits (read off the scene; asserted below, never trusted).
SWITCHER = "4216411968520269688"
ARK_GO = "2879243035581748626"
ARK_RT = "4100490384345192841"
ARK_IMAGE = "1229040209808752503"
ARK_GATE = "5340227408031961628"
DISABLED_BEFORE, DISABLED_AFTER = "0100000003000000", "03000000"   # [ARK=1, PORT=3] -> [PORT]


def ark_open(switcher_doc):
    """ARK out of disabledScreens. Other screens may be locked or open beside it (CODEX, section 84 of
    Docs/MENU_REDESIGN_B.md; PORT opened in section 90), so the list is read, not matched whole."""
    import re as _re
    m = _re.search(r"^  disabledScreens: ([0-9a-f]*)$", switcher_doc, _re.M)
    ids = [int.from_bytes(bytes.fromhex(m.group(1)[i:i + 8]), "little") for i in range(0, len(m.group(1)), 8)] if m else []
    return 1 not in ids
# The converter's own documents: a readable family, unique in the file, inside a signed int64.
NEW_GO, NEW_RT, NEW_VIEW = "7700130000000000001", "7700130000000000002", "7700130000000000003"
INT64_MAX = 9223372036854775807

# The config's per-play ranges and fixed knobs (OmniCombineConfigSO's fields; held equal to its initializers).
CONFIG_VALUES = [
    ("lead", "{x: 2.2, y: 3.2}"),
    ("spread", "{x: 2.7, y: 3.3}"),
    ("layoutYawDegrees", "{x: 0, y: 360}"),
    ("flight", "{x: 4.4, y: 5.4}"),
    ("staggerOfFlight", "{x: 0.95, y: 1.05}"),
    ("arc", "{x: 0.25, y: 0.55}"),
    ("swirl", "{x: -0.15, y: 0.15}"),
    ("tumble", "{x: 0.15, y: 0.35}"),
    ("siteSpread", "{x: 0.35, y: 0.65}"),
    ("suck", "{x: 0.3, y: 0.4}"),
    ("socketSpacing", "{x: 0.14, y: 0.18}"),
    ("socketSeconds", "{x: 0.8, y: 1}"),
    ("massRaise", "0.6"),
    ("approachAboveCameraDegrees", "0"),
    ("layout", "2"),
    ("ease", "2"),
    ("morphStart", "0.1"),
    ("morphLength", "0.8"),
    ("effectFade", "0.7"),
    ("fadeInSeconds", "1"),
    ("handOffSeconds", "0.6"),
    ("omniWorldRadius", "40"),
]


def read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def guid(name):
    return hashlib.md5(f"CosmicShore/ElementalConverter/{name}".encode()).hexdigest()


def meta_guid(path):
    m = re.search(r"^guid: ([0-9a-f]{32})$", read(path + ".meta"), re.M)
    if not m:
        raise SystemExit(f"{path}.meta: no guid")
    return m.group(1)


def script_meta(g):
    return (f"fileFormatVersion: 2\nguid: {g}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n"
            f"  defaultReferences: []\n  executionOrder: 0\n  icon: {{instanceID: 0}}\n  userData: \n"
            f"  assetBundleName: \n  assetBundleVariant: \n")


def asset_meta(g):
    return (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n"
            f"  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def script_guids():
    """Each new script's guid: its existing .meta's if it has one, else a deterministic one."""
    out = {}
    for name, rel in SCRIPTS.items():
        path = os.path.join(ROOT, rel)
        out[name] = meta_guid(path) if os.path.exists(path + ".meta") else guid(name + ".cs")
    return out


def serialized_fields(cs):
    """The [SerializeField] names declared in a C# file (one declaration per line, attribute first)."""
    return re.findall(r"^\s*\[SerializeField[^\]]*\]\s*(?:readonly\s+)?[\w<>\[\],]+\s+(\w+)\s*(?:=|;)", read(cs), re.M)


def config_asset(guids):
    mat = lambda name: f"{{fileID: 2100000, guid: {meta_guid(os.path.join(MATS, name + '.mat'))}, type: 2}}"
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
             "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             f"  m_Script: {{fileID: 11500000, guid: {guids['OmniCombineConfigSO']}, type: 3}}",
             "  m_Name: OmniCombineConfig", "  m_EditorClassIdentifier: ",
             f"  combineData: {{fileID: 4900000, guid: {meta_guid(DATA_META[:-5])}, type: 3}}",
             f"  shellMaterial: {mat('OmniCrystalBody')}",
             f"  omniMaterial: {mat('OmniCrystalBody')}",
             f"  massMaterial: {mat('MassDepthPrisms')}",
             f"  omniTrianglesMaterial: {mat('OmniDepthPrisms')}",
             f"  omniTrianglesMesh: {{fileID: 4300000, guid: {meta_guid(TRI_MESH)}, type: 2}}",
             f"  depthProfile: {{fileID: 11400000, guid: {meta_guid(DEPTH_PROFILE)}, type: 2}}",
             # Omni Motion Lab round 5 (2026-10-13): the woken omni and the Charge light are the game's.
             f"  cadence: {{fileID: 11400000, guid: {meta_guid(CADENCE)}, type: 2}}",
             f"  chargeArcs: {{fileID: 11400000, guid: {meta_guid(CHARGE_ARCS_PROFILE)}, type: 2}}",
             f"  omniArcsMaterial: {mat('OmniChargeArcs')}",
             # 2026-10-27: each crystal flies in its own shade of lime (the palette's ElementalCrystalColors) and lands in
             # the omni's.
             f"  palette: {{fileID: 11400000, guid: {meta_guid(PALETTE)}, type: 2}}"]
    lines += [f"  {k}: {v}" for k, v in CONFIG_VALUES]
    return "\n".join(lines) + "\n"


# ------------------------------------------------------------------ the scene
def docs(text):
    """{fileID: (class id, body)} for every document, split first so no query spans two documents."""
    return {m.group(2): (m.group(1), m.group(3)) for m in
            re.finditer(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n(.*?)(?=^--- !u!|\Z)", text, re.S | re.M)}


def dangling(text):
    anchors = set(re.findall(r"^--- !u!\d+ &(-?\d+)", text, re.M))
    refs = set(re.findall(r"\{fileID: (-?\d+)\}", text))
    return refs - anchors - {"0"}


def edit_scene(text, view_guid):
    """Menu_Main with the converter in it. Idempotent: an already-edited scene comes back unchanged."""
    d = docs(text)
    for fid, kind, what in ((SWITCHER, "114", "ScreenSwitcher"), (ARK_GO, "1", "ArkScreen"), (ARK_RT, "224", "ArkScreen's RectTransform"),
                            (ARK_IMAGE, "114", "ArkScreen's Image")):
        if fid not in d or d[fid][0] != kind:
            raise SystemExit(f"Menu_Main: {what} (&{fid}) is not where this tool expects it - re-read the scene")
    if "m_Name: ArkScreen\n" not in d[ARK_GO][1]:
        raise SystemExit("Menu_Main: &" + ARK_GO + " is no longer ArkScreen")

    def replace_in(fid, old, new, label):
        nonlocal text
        kind, body = docs(text)[fid]
        if new in body:
            return
        if body.count(old) != 1:
            raise SystemExit(f"Menu_Main: {label}: expected one '{old.strip()}' in &{fid}")
        start = text.index(f"--- !u!{kind} &{fid}\n")
        end = start + len(f"--- !u!{kind} &{fid}\n") + len(body)
        text = text[:start] + text[start:end].replace(old, new, 1) + text[end:]

    # 1. The Ark screen opens.
    if not ark_open(docs(text)[SWITCHER][1]):   # a spent one-shot stands down once ARK is open
        replace_in(SWITCHER, f"  disabledScreens: {DISABLED_BEFORE}\n", f"  disabledScreens: {DISABLED_AFTER}\n", "disabledScreens")
    # 2. The design mockup background goes dark (the component stays, switched off).
    replace_in(ARK_IMAGE, "  m_Enabled: 1\n", "  m_Enabled: 0\n", "ArkScreen Image")
    # 3. The offline gate goes: its document and its m_Component entry.
    if f"&{ARK_GATE}\n" in text:
        refs = re.findall(rf"\b{ARK_GATE}\b", text)
        if len(refs) != 2:
            raise SystemExit(f"Menu_Main: the Ark screen's OfflineUIGate is referenced {len(refs) - 2} more time(s); not removing it")
        kind, body = docs(text)[ARK_GATE]
        if "OfflineUIGate" not in body or f"m_GameObject: {{fileID: {ARK_GO}}}" not in body:
            raise SystemExit("Menu_Main: &" + ARK_GATE + " is not ArkScreen's OfflineUIGate")
        block = f"--- !u!114 &{ARK_GATE}\n" + body
        text = text.replace(block, "", 1)
        text = text.replace(f"  - component: {{fileID: {ARK_GATE}}}\n", "", 1)
    # 4. The converter: a stretched child of ArkScreen carrying ElementalConverterView.
    if f"--- !u!1 &{NEW_GO}\n" not in text:
        for fid in (NEW_GO, NEW_RT, NEW_VIEW):
            if re.search(rf"\b{fid}\b", text):
                raise SystemExit(f"Menu_Main: fileID {fid} is already taken")
        new_docs = (
            f"--- !u!1 &{NEW_GO}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n"
            f"  - component: {{fileID: {NEW_RT}}}\n  - component: {{fileID: {NEW_VIEW}}}\n  m_Layer: 5\n"
            f"  m_Name: ElementalConverter\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n"
            f"  m_StaticEditorFlags: 0\n  m_IsActive: 1\n"
            f"--- !u!224 &{NEW_RT}\nRectTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {NEW_GO}}}\n"
            f"  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n"
            f"  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n"
            f"  m_Father: {{fileID: {ARK_RT}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
            f"  m_AnchorMin: {{x: 0, y: 0}}\n  m_AnchorMax: {{x: 1, y: 1}}\n  m_AnchoredPosition: {{x: 0, y: 0}}\n"
            f"  m_SizeDelta: {{x: 0, y: 0}}\n  m_Pivot: {{x: 0.5, y: 0.5}}\n"
            f"--- !u!114 &{NEW_VIEW}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {NEW_GO}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {view_guid}, type: 3}}\n"
            f"  m_Name: \n  m_EditorClassIdentifier: \n"
            f"  config: {{fileID: 11400000, guid: {guid('OmniCombineConfig.asset')}, type: 2}}\n"
            f"  font: {{fileID: 11400000, guid: {MENU_FONT}, type: 2}}\n")
        # After ArkScreen's RectTransform document (Unity does not care about document order).
        kind, body = docs(text)[ARK_RT]
        head = f"--- !u!224 &{ARK_RT}\n"
        at = text.index(head) + len(head) + len(body)
        text = text[:at] + new_docs + text[at:]
        kind, body = docs(text)[ARK_RT]
        m = re.search(r"^  m_Children:\n((?:  - \{fileID: -?\d+\}\n)*)", body, re.M)
        if not m:
            raise SystemExit("Menu_Main: ArkScreen's RectTransform has no m_Children list")
        new_body = body[:m.end()] + f"  - {{fileID: {NEW_RT}}}\n" + body[m.end():]
        start = text.index(head) + len(head)
        text = text[:start] + new_body + text[start + len(body):]
    return text


def validate_scene(before, after, view_guid, current=None):
    """`before` is the merge base's scene (for NEW dangling references); `current` is the file as it is on
    disk before this run's edit, which the document count is measured against. Measuring the count against
    the merge base made every other branch's Menu_Main addition read as this tool's (found 2026-10-15: the
    first-login guide's spotlight components)."""
    current = before if current is None else current
    d = docs(after)
    for fid in (NEW_GO, NEW_RT, NEW_VIEW):
        if int(fid) > INT64_MAX:
            raise SystemExit(f"fileID {fid} overflows a signed int64")
        if len(re.findall(rf"^--- !u!\d+ &{fid}$", after, re.M)) != 1:
            raise SystemExit(f"fileID {fid} is not exactly one document")
    if f"- {{fileID: {NEW_RT}}}" not in d[ARK_RT][1] or f"m_Father: {{fileID: {ARK_RT}}}" not in d[NEW_RT][1]:
        raise SystemExit("the converter is not a child of ArkScreen both ways")
    if f"- component: {{fileID: {NEW_VIEW}}}" not in d[NEW_GO][1] or f"m_GameObject: {{fileID: {NEW_GO}}}" not in d[NEW_VIEW][1]:
        raise SystemExit("the converter's view is not on its GameObject both ways")
    if f"guid: {view_guid}" not in d[NEW_VIEW][1]:
        raise SystemExit("the converter's view names the wrong script")
    if re.search(rf"\b{ARK_GATE}\b", after):
        raise SystemExit("the Ark screen's OfflineUIGate survives somewhere")
    if not ark_open(d[SWITCHER][1]):
        raise SystemExit("ARK is still disabled (or PORT opened)")
    new = dangling(after) - dangling(before)
    if new:
        raise SystemExit(f"the edit leaves {len(new)} new dangling reference(s): {sorted(new)[:5]}")
    if after.count("\n--- !u!") != current.count("\n--- !u!") + (3 if f"&{NEW_GO}\n" not in current else 0) - (1 if f"&{ARK_GATE}\n" in current else 0):
        raise SystemExit("the document count moved by something other than +3 converter / -1 gate")


# ------------------------------------------------------------------ assets
def planned():
    guids = script_guids()
    out = {}
    for name, rel in SCRIPTS.items():
        path = os.path.join(ROOT, rel)
        if not os.path.exists(path):
            raise SystemExit(f"{rel} is missing")
        out[path + ".meta"] = read(path + ".meta") if os.path.exists(path + ".meta") else script_meta(guids[name])
    if not os.path.exists(DATA_META):
        raise SystemExit("OmniCombineData.json is not authored: run Tools/Build/author_omni_combine_asset.py first")
    fields = set(serialized_fields(os.path.join(ROOT, SCRIPTS["OmniCombineConfigSO"])))
    keys = {"combineData", "shellMaterial", "omniMaterial", "massMaterial",
            "omniTrianglesMaterial", "omniTrianglesMesh", "depthProfile", "cadence", "chargeArcs",
            "omniArcsMaterial", "palette"} | {k for k, _ in CONFIG_VALUES}
    if fields != keys:
        raise SystemExit(f"OmniCombineConfig keys and OmniCombineConfigSO's fields disagree: "
                         f"asset-only {sorted(keys - fields)}, script-only {sorted(fields - keys)}")
    view_fields = set(serialized_fields(os.path.join(ROOT, SCRIPTS["ElementalConverterView"])))
    if not {"config", "font"} <= view_fields:
        raise SystemExit("ElementalConverterView no longer declares config/font")
    out[CONFIG] = config_asset(guids)
    out[CONFIG + ".meta"] = asset_meta(guid("OmniCombineConfig.asset"))
    before = read(SCENE)
    after = edit_scene(before, guids["ElementalConverterView"])
    validate_scene(base_scene(before), after, guids["ElementalConverterView"], current=before)
    out[SCENE] = after
    return out, guids


def base_scene(current):
    """The scene before this tool's edits, for the dangling-reference comparison: the merge base's copy when
    there is one, else the current file (a first run edits the current file)."""
    try:
        base = subprocess.run(["git", "merge-base", "HEAD", "origin/bleeding-edge"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
        return subprocess.run(["git", "show", f"{base}:Assets/_Scenes/Menu_Main.unity"], cwd=ROOT, capture_output=True, text=True, check=True).stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        return current


def pristine(scene):
    """`scene` as it was before this tool's first run, for the self-test's first-run refusals: the
    converter's three documents and its m_Children entry removed, ArkScreen's OfflineUIGate put back on
    its GameObject, and ARK disabled again."""
    for fid in (NEW_GO, NEW_RT, NEW_VIEW):
        if fid in docs(scene):
            kind, body = docs(scene)[fid]
            scene = scene.replace(f"--- !u!{kind} &{fid}\n" + body, "", 1)
    scene = scene.replace(f"  - {{fileID: {NEW_RT}}}\n", "", 1)
    if f"&{ARK_GATE}\n" not in scene:
        kind, body = docs(scene)[ARK_GO]
        gate = (f"--- !u!114 &{ARK_GATE}\nMonoBehaviour:\n  m_GameObject: {{fileID: {ARK_GO}}}\n"
                "  m_Name: \n  m_EditorClassIdentifier: OfflineUIGate\n")
        scene = scene.replace(f"--- !u!1 &{ARK_GO}\n" + body,
                              f"--- !u!1 &{ARK_GO}\n" + body.replace("  m_Component:\n",
                              f"  m_Component:\n  - component: {{fileID: {ARK_GATE}}}\n", 1) + gate, 1)
    return scene.replace(f"  disabledScreens: {DISABLED_AFTER}\n", f"  disabledScreens: {DISABLED_BEFORE}\n", 1)


def unedited_scene():
    """Menu_Main as it was before this tool first edited it: the parent of the oldest commit that added the converter.
    Once the converter is on the merge base, base_scene() is ALREADY edited, every edit is a no-op, and the refusals
    below would pass without ever reaching the code they guard (found 2026-10-13: two of them had gone red)."""
    try:
        log = subprocess.run(["git", "log", "--format=%H", "-S", f"--- !u!1 &{NEW_GO}", "--", "Assets/_Scenes/Menu_Main.unity"],
                             cwd=ROOT, capture_output=True, text=True, check=True).stdout.split()
        if not log:
            return base_scene(read(SCENE))
        return subprocess.run(["git", "show", f"{log[-1]}^:Assets/_Scenes/Menu_Main.unity"], cwd=ROOT, capture_output=True,
                              text=True, check=True).stdout
    except (subprocess.CalledProcessError, FileNotFoundError):
        return base_scene(read(SCENE))


def self_test():
    ok = True

    def check(name, cond):
        nonlocal ok
        print(("PASS  " if cond else "FAIL  ") + name)
        ok = ok and cond

    def refused(fn):
        try:
            fn()
            return False
        except SystemExit:
            return True
    base = unedited_scene()
    g = script_guids()["ElementalConverterView"]
    check("the self-test edits a scene the converter is not in yet", f"--- !u!1 &{NEW_GO}\n" not in base)
    after = edit_scene(base, g)
    validate_scene(base, after, g)
    check("the base scene edits and validates", True)
    check("editing an edited scene changes nothing", edit_scene(after, g) == after)
    check("ARK is open", ark_open(docs(after)[SWITCHER][1]))
    check("the new fileIDs fit a signed int64", all(int(f) <= INT64_MAX for f in (NEW_GO, NEW_RT, NEW_VIEW)))
    extra = after + "--- !u!1 &424243\nGameObject:\n  m_Name: Stray\n"
    check("a document added beside the converter's three is refused", refused(lambda: validate_scene(base, extra, g)))
    other_branch = base + "--- !u!1 &424244\nGameObject:\n  m_Name: OtherBranch\n"
    check("another branch's Menu_Main addition is not counted as this tool's",
          not refused(lambda: validate_scene(base, edit_scene(other_branch, g), g, current=other_branch)))
    broken = after.replace(f"  - {{fileID: {NEW_RT}}}\n", "", 1)
    check("a converter missing from ArkScreen's children is refused", refused(lambda: validate_scene(base, broken, g)))
    broken = after.replace(f"config: {{fileID: 11400000", "config: {fileID: 11400001", 1) + f"  stray: {{fileID: 424242}}\n"
    check("a new dangling reference is refused", refused(lambda: validate_scene(base, broken, g)))
    # The first-run refusals need a scene this tool has not edited yet. The merge base carries the
    # converter now, so plant them into a synthesised pre-converter scene: the converter's documents and
    # child entry stripped, the gate and ARK's disabled entry put back. Planted into `base` they reached
    # no code (the converter step and the gate step are skipped on an edited scene) and failed silently.
    fresh = pristine(base)
    check("the synthesised pre-converter scene edits cleanly", edit_scene(fresh, g).count(f"--- !u!1 &{NEW_GO}\n") == 1)
    taken = fresh.replace("m_Name: ArkScreen\n", f"m_Name: ArkScreen\n  m_Note: {NEW_VIEW}\n", 1)
    check("a fileID already in the scene is refused", refused(lambda: edit_scene(taken, g)))
    gated = fresh.replace(f"  disabledScreens: {DISABLED_BEFORE}\n", f"  disabledScreens: {DISABLED_BEFORE}\n  m_Extra: {{fileID: {ARK_GATE}}}\n", 1)
    check("an OfflineUIGate referenced from elsewhere is not removed", refused(lambda: edit_scene(gated, g)))
    out, _ = planned()
    check("the config asset carries every OmniCombineConfigSO field",
          all(f"  {k}: {{fileID: " in out[CONFIG] for k in ("cadence", "chargeArcs", "omniArcsMaterial")))
    check("the Charge crystal wears the omni's body (one hue at the hand-off)", "chargeMaterial" not in out[CONFIG])
    check("the retired edge-crawl material is not wired", "omniChargeEdgesMaterial" not in out[CONFIG])
    sys.exit(0 if ok else 1)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    a = ap.parse_args()
    if a.self_test:
        self_test()
    out, guids = planned()
    stale = [p for p, text in out.items() if not os.path.exists(p) or read(p) != text]
    if a.check:
        if stale:
            print("STALE: " + ", ".join(os.path.relpath(p, ROOT) for p in stale))
            sys.exit(1)
        print("OK: the Elemental Converter is authored (Ark screen open, converter wired, config current)")
        return
    for p in stale:
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(out[p])
    print(f"wrote {len(stale)} file(s): " + ", ".join(os.path.relpath(p, ROOT) for p in stale))


if __name__ == "__main__":
    main()
