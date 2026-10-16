#!/usr/bin/env python3
"""Author the first-login guide (Docs/ModePreview/TRAINING_PLAN.md §5).

Writes:
  Assets/Resources/GameOfTheWeek.asset                          the rotation (GameOfTheWeekSO)
  Assets/FTUE/DataContainer/Phases/MainQuest_Phase0_FirstLogin.asset  the new Phase 0
  Assets/FTUE/DataContainer/Quests/MainQuest.asset              phases[0] re-pointed at it

The guide is one phase: a QuestGuideToMicrogameNode that SPOTLIGHTS the way to the Game of the
Week's microgame - the Arcade entry, the card, the preview - for the player to press themselves
(the guided-path rule, Docs/HomeHub/ARCHITECTURE.md §8), and holds until the Lesson ends; then the
phase ends. The guide node holds rather than handing to a separate wait node because a quest
resumes at its saved node: a player who quits mid-Lesson must resume where the path is lit again.
The phase opts in to running under the master developer unlock, so the guide works in a default
checkout; the phases after it do not, and the runner stands down
when it reaches them.

The two new assets are SEED content a designer then edits (the rotation in the inspector, the
phase in the Quest Graph editor), so they are authored only while they do not exist - except a
phase written by an OLDER version of this tool (the railroad, which carried the player in), which
is replaced. The MainQuest re-point is enforced every run. The old Phase 0 (flight school in freestyle) is left on
disk, unreferenced, for reference.

`--check` verifies what is on disk: files + metas exist; MainQuest's first phase is the guide;
the guide opts in, its entry node exists, every edge resolves, every node's script is the class
it claims to be; it GUIDES (carries a QuestGuideToMicrogameNode) and never carries the player
(no Navigate / EnterFreestyle node - the guided-path rule); and every mode in the rotation has an
arcade card AND a flyable preview.
`--self-test` proves the checks fire.

Usage: author_first_login_guide.py [--check | --force | --self-test]
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "Assets")
assert os.path.isdir(ASSETS), f"Assets/ not under {ROOT}"

GOTW = "Assets/Resources/GameOfTheWeek.asset"
PHASE = "Assets/FTUE/DataContainer/Phases/MainQuest_Phase0_FirstLogin.asset"
QUEST = "Assets/FTUE/DataContainer/Quests/MainQuest.asset"
ARCADE_LIST = "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"
PREVIEWS = "Assets/_SO_Assets/Mode Previews"

SCRIPTS = {
    "GameOfTheWeekSO": "Assets/_Scripts/ScriptableObjects/GameOfTheWeekSO.cs",
    "QuestPhaseGraphSO": "Assets/FTUE/Scripts/Graph/QuestPhaseGraphSO.cs",
    "QuestGuideToMicrogameNode": "Assets/FTUE/Scripts/Graph/Nodes/QuestGuideToMicrogameNode.cs",
    "QuestWaitForLessonNode": "Assets/FTUE/Scripts/Graph/Nodes/QuestWaitForLessonNode.cs",
    "QuestPhaseEndNode": "Assets/FTUE/Scripts/Graph/Nodes/QuestPhaseEndNode.cs",
}

MODES = {"SkimRace": 33, "Switchback": 45, "Headlong": 49, "Breakwater": 50, "Skein": 51,
         "Redline": 53, "Waystation": 58}
# The racing (Time-genre, SwitchesThreaded or Crystals) single-hull cards with a flyable preview.
# Skim Race first: it is the one played end to end in the editor. Order is the weekly schedule.
ROTATION = ["SkimRace", "Switchback", "Headlong", "Redline", "Breakwater", "Skein", "Waystation"]
FALLBACK = "SkimRace"

# (key, class, display name, extra fields, next key or None)
# Captions are the guide's player-facing text: authored here as the SEED and owned by the asset
# afterwards (edit them in the Quest Graph editor). ASCII only - the UI font has 97 glyphs.
CAPTIONS = {
    "arcadeCaption": "Your first race is waiting. Open the Arcade.",
    "cardCaption": "This week's game. Open it.",
    "previewCaption": "Tap the window to fly.",
}
NODES = [
    ("guide", "QuestGuideToMicrogameNode", "Guide to the Game of the Week - until the Lesson ends",
     "  source: 0\n  mode: 33\n"
     + "".join(f"  {k}: {v}\n" for k, v in CAPTIONS.items())
     + "  dimAlpha: 0.7\n  lostStepGraceSeconds: 1.5\n", "end"),
    ("end", "QuestPhaseEndNode", "Phase 0 Complete", "", None),
]
POSITIONS = {"guide": (60, 40), "end": (380, 40)}
# Node types that CARRY the player somewhere. A first-login path must show the way and let the
# player walk it (Docs/HomeHub/ARCHITECTURE.md §8), so none of these may appear in it.
CARRYING_NODES = ("QuestNavigateNode", "QuestEnterFreestyleNode", "QuestOpenMicrogameNode")


def h(text):
    return hashlib.md5(("cosmic-shore-railroad:" + text).encode()).hexdigest()   # salt kept: ids stay stable


def file_id(key):
    v = int(h("fileid:" + key)[:15], 16) + 100000000   # positive, never the main object's 11400000
    return v


def node_id(key):
    return h("node:" + key)


def guid_of(path):
    meta = os.path.join(ROOT, path + ".meta")
    m = re.search(r"^guid: (\w+)", open(meta).read(), re.M) if os.path.exists(meta) else None
    return m.group(1) if m else None


def header(fid, script, name, cls_ns):
    return (f"--- !u!114 &{fid}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
            "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n"
            "  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {guid_of(SCRIPTS[script])}, type: 3}}\n"
            f"  m_Name: {name}\n  m_EditorClassIdentifier: Assembly-CSharp::{cls_ns}.{script}\n")


def gotw_asset():
    rows = "".join(f"  - {MODES[m]}\n" for m in ROTATION)
    return ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
            + header(11400000, "GameOfTheWeekSO", "GameOfTheWeek", "CosmicShore.ScriptableObjects")
            + f"  rotation:\n{rows}  fallback: {MODES[FALLBACK]}\n")


def phase_asset():
    out = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"]
    for key, cls, name, extra, nxt in NODES:
        x, y = POSITIONS[key]
        edges = (f"  outputs:\n  - portName: next\n    targetNodeId: {node_id(nxt)}\n    delaySeconds: 0\n"
                 if nxt else "  outputs: []\n")
        out.append(header(file_id(key), cls, name, "CosmicShore.Core")
                   + f"  nodeId: {node_id(key)}\n  displayName: {name}\n  nodeEnabled: 1\n"
                   + f"  graphPosition: {{x: {x}, y: {y}}}\n" + edges + extra)
    nodes = "".join(f"  - {{fileID: {file_id(k)}}}\n" for k, *_ in NODES)
    out.append(header(11400000, "QuestPhaseGraphSO", "MainQuest_Phase0_FirstLogin", "CosmicShore.Core")
               + "  graphId: MainQuest_Phase0_FirstLogin\n  phaseName: First Login - Game of the Week\n"
               + "  phaseEnabled: 1\n  runsUnderDeveloperUnlock: 1\n"
               + "  designerNotes: Spotlights the way to the Game of the Week microgame for a new player\n"
               + "    to walk themselves, until their first Lesson is done. TRAINING_PLAN section 5.\n"
               + f"  entryNode: {{fileID: {file_id(NODES[0][0])}}}\n  nodes:\n{nodes}"
               + "  canvasScroll: {x: 0, y: 0}\n  canvasZoom: 1\n")
    return "".join(out)


def asset_meta(path):
    return (f"fileFormatVersion: 2\nguid: {h('guid:' + path)}\nNativeFormatImporter:\n"
            "  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n"
            "  assetBundleName: \n  assetBundleVariant: \n")


def repoint_quest(text, phase_guid):
    m = re.search(r"^  phases:\n  - \{fileID: 11400000, guid: (\w+), type: 2\}$", text, re.M)
    assert m, f"{QUEST}: no phases list"
    return text[:m.start(1)] + phase_guid + text[m.end(1):]


# ── Checks ──────────────────────────────────────────────────────────────────────────────────

def check(read):
    errors = []
    for path in (GOTW, PHASE, QUEST):
        if read(path) is None or read(path + ".meta") is None:
            errors.append(f"{path} (or its .meta) missing")
    if errors:
        return errors
    for cls, src in SCRIPTS.items():
        if not guid_of(src):
            errors.append(f"script {src} has no .meta guid")

    phase_guid = re.search(r"^guid: (\w+)", read(PHASE + ".meta"), re.M).group(1)
    first = re.search(r"^  phases:\n  - \{fileID: 11400000, guid: (\w+)", read(QUEST), re.M)
    if not first or first.group(1) != phase_guid:
        errors.append(f"{QUEST}: phases[0] is not the first-login guide")

    phase = read(PHASE)
    if not re.search(r"^  runsUnderDeveloperUnlock: 1$", phase, re.M):
        errors.append(f"{PHASE}: does not opt in to running under the developer unlock")
    docs = {fid: body for fid, body in re.findall(r"^--- !u!114 &(-?\d+)\n(.*?)(?=^--- |\Z)", phase, re.M | re.S)}
    ids = {}
    script_class = {guid_of(src): cls for cls, src in SCRIPTS.items()}
    for fid, body in docs.items():
        sg = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+)", body)
        cls = re.search(r"m_EditorClassIdentifier: Assembly-CSharp::[\w.]+\.(\w+)", body)
        if not sg or not cls or script_class.get(sg.group(1)) != cls.group(1):
            errors.append(f"{PHASE}: object {fid} script guid does not match its class")
        nid = re.search(r"^  nodeId: (\w+)$", body, re.M)
        if nid:
            ids[nid.group(1)] = fid
    for fid, body in docs.items():
        for target in re.findall(r"targetNodeId: (\w+)", body):
            if target not in ids:
                errors.append(f"{PHASE}: edge from {fid} to unknown node {target}")
    entry = re.search(r"^  entryNode: \{fileID: (-?\d+)\}", phase, re.M)
    if not entry or entry.group(1) not in docs:
        errors.append(f"{PHASE}: entry node missing")
    classes = re.findall(r"m_EditorClassIdentifier: Assembly-CSharp::[\w.]+\.(\w+)", phase)
    if "QuestGuideToMicrogameNode" not in classes:
        errors.append(f"{PHASE}: no QuestGuideToMicrogameNode - nothing spotlights the way to the microgame")
    for carrier in CARRYING_NODES:
        if carrier in classes:
            errors.append(f"{PHASE}: carries a {carrier} - a first-login path must SHOW the way and "
                          "let the player walk it (guided-path rule, Docs/HomeHub/ARCHITECTURE.md §8)")
    for key in CAPTIONS:
        line = re.search(rf"^  {key}: (.*)$", phase, re.M)
        if line and any(ord(ch) > 126 for ch in line.group(1)):
            errors.append(f"{PHASE}: {key} has a non-ASCII character - it renders as an empty box")

    # Every mode in the rotation needs a card on the arcade roster and a flyable preview.
    gotw = read(GOTW)
    rot = re.search(r"^  rotation:\n((?:  - \d+\n)*)", gotw, re.M)
    modes = [int(v) for v in re.findall(r"- (\d+)", rot.group(1))] if rot else []
    fb = re.search(r"^  fallback: (\d+)$", gotw, re.M)
    if fb:
        modes.append(int(fb.group(1)))
    roster = card_modes(read)
    flyable = preview_modes(read)
    for mode in modes:
        if mode not in roster:
            errors.append(f"{GOTW}: mode {mode} has no card on {ARCADE_LIST}")
        if mode not in flyable:
            errors.append(f"{GOTW}: mode {mode} has no mode preview with a PreviewCell")
    return errors


def card_modes(read):
    out = set()
    for g in re.findall(r"guid: (\w+), type: 2", read(ARCADE_LIST) or ""):
        path = META_INDEX().get(g)
        if path:
            m = re.search(r"^  Mode: (\d+)$", read(path) or "", re.M)
            if m:
                out.add(int(m.group(1)))
    return out


def preview_modes(read):
    out = set()
    folder = os.path.join(ROOT, PREVIEWS)
    for name in os.listdir(folder):
        if not name.endswith(".asset"):
            continue
        text = read(f"{PREVIEWS}/{name}") or ""
        mode = re.search(r"^  Mode: (\d+)$", text, re.M)
        cell = re.search(r"^  PreviewCell: \{fileID: (\d+)", text, re.M)
        if mode and cell and cell.group(1) != "0":
            out.add(int(mode.group(1)))
    return out


_INDEX = None


def META_INDEX():
    global _INDEX
    if _INDEX is None:
        _INDEX = {}
        for dirpath, _, files in os.walk(os.path.join(ROOT, "Assets", "_SO_Assets", "Games")):
            for f in files:
                if f.endswith(".asset.meta"):
                    full = os.path.join(dirpath, f)
                    g = re.search(r"^guid: (\w+)", open(full).read(), re.M)
                    if g:
                        _INDEX[g.group(1)] = os.path.relpath(full, ROOT)[:-5]
    return _INDEX


def disk(path):
    full = os.path.join(ROOT, path)
    return open(full).read() if os.path.exists(full) else None


def self_test():
    store = {GOTW: gotw_asset(), GOTW + ".meta": asset_meta(GOTW),
             PHASE: phase_asset(), PHASE + ".meta": asset_meta(PHASE)}
    store[QUEST] = repoint_quest(disk(QUEST), h("guid:" + PHASE))
    store[QUEST + ".meta"] = disk(QUEST + ".meta")

    def read(path):
        return store[path] if path in store else disk(path)

    clean = check(read)
    assert not clean, f"seed fails its own checks: {clean}"

    def fires(path, a, b, what, expect=None):
        saved = store[path]
        assert a in saved, f"negative control '{what}' has nothing to mutate"
        store[path] = saved.replace(a, b, 1)
        errors = check(read)
        assert errors, f"negative control did not fire: {what}"
        assert expect is None or any(expect in e for e in errors), \
            f"negative control '{what}' fired for the wrong reason: {errors}"
        store[path] = saved

    fires(QUEST, h("guid:" + PHASE), "0" * 32, "quest not re-pointed")
    fires(PHASE, "runsUnderDeveloperUnlock: 1", "runsUnderDeveloperUnlock: 0", "no gate opt-in")
    fires(PHASE, f"targetNodeId: {node_id('end')}", "targetNodeId: deadbeef", "dangling edge")
    fires(PHASE, "CosmicShore.Core.QuestGuideToMicrogameNode", "CosmicShore.Core.QuestPhaseEndNode", "class/guid mismatch")
    fires(PHASE, "::CosmicShore.Core.QuestPhaseEndNode", "::CosmicShore.Core.QuestNavigateNode", "a node that carries the player", "carries a QuestNavigateNode")
    fires(PHASE, "Open the Arcade.", "Open the Arcade \u2192", "non-ASCII caption", "non-ASCII")
    fires(GOTW, f"  - {MODES['Skein']}\n", "  - 31\n", "mode with no card")
    print("self-test: clean seed passes; 7 negative controls fire")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        force = "--force" in argv
        for path, text in ((GOTW, gotw_asset()), (PHASE, phase_asset())):
            full = os.path.join(ROOT, path)
            stale = path == PHASE and os.path.exists(full) and "QuestGuideToMicrogameNode" not in open(full).read()
            if os.path.exists(full) and not force and not stale:
                print(f"kept (human-owned once authored): {path}")
                continue
            open(full, "w").write(text)
            if not os.path.exists(full + ".meta"):
                open(full + ".meta", "w").write(asset_meta(path))
            print(f"wrote {path}")
        phase_guid = guid_of(PHASE)
        quest_full = os.path.join(ROOT, QUEST)
        old = open(quest_full).read()
        new = repoint_quest(old, phase_guid)
        if new != old:
            open(quest_full, "w").write(new)
            print(f"re-pointed {QUEST} phases[0] at the first-login guide")
    errors = check(disk)
    for e in errors:
        print("ERROR: " + e)
    print("first-login guide: " + ("FAIL" if errors else "ok"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
