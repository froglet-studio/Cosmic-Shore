#!/usr/bin/env python3
"""Author the first-login railroad (Docs/ModePreview/TRAINING_PLAN.md §5).

Writes:
  Assets/Resources/GameOfTheWeek.asset                          the rotation (GameOfTheWeekSO)
  Assets/FTUE/DataContainer/Phases/MainQuest_Phase0_Railroad.asset  the new Phase 0
  Assets/FTUE/DataContainer/Quests/MainQuest.asset              phases[0] re-pointed at it

The railroad is one phase: lock navigation to the Arcade, open the Game of the Week's microgame
with the vessel flown in and HOLD there until the Lesson ends, unlock, end the phase. The open node
holds rather than handing to a separate wait node because a quest resumes at its saved node: a
player who quits mid-Lesson must resume where the microgame is opened again.
The phase opts in to running under the master developer unlock (its lock nodes pass through), so
the walk-in works in a default checkout; the phases after it do not, and the runner stands down
when it reaches them.

The two new assets are SEED content a designer then edits (the rotation in the inspector, the
phase in the Quest Graph editor), so they are authored only while they do not exist. The
MainQuest re-point is enforced every run. The old Phase 0 (flight school in freestyle) is left on
disk, unreferenced, for reference.

`--check` verifies what is on disk: files + metas exist; MainQuest's first phase is the railroad;
the railroad opts in, its entry node exists, every edge resolves, every node's script is the class
it claims to be; and every mode in the rotation has an arcade card AND a flyable preview.
`--self-test` proves the checks fire.

Usage: author_first_login_railroad.py [--check | --force | --self-test]
"""
import hashlib
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(ROOT, "Assets")
assert os.path.isdir(ASSETS), f"Assets/ not under {ROOT}"

GOTW = "Assets/Resources/GameOfTheWeek.asset"
PHASE = "Assets/FTUE/DataContainer/Phases/MainQuest_Phase0_Railroad.asset"
QUEST = "Assets/FTUE/DataContainer/Quests/MainQuest.asset"
ARCADE_LIST = "Assets/_SO_Assets/Games/GameLists/ArcadeGames.asset"
PREVIEWS = "Assets/_SO_Assets/Mode Previews"

SCRIPTS = {
    "GameOfTheWeekSO": "Assets/_Scripts/ScriptableObjects/GameOfTheWeekSO.cs",
    "QuestPhaseGraphSO": "Assets/FTUE/Scripts/Graph/QuestPhaseGraphSO.cs",
    "QuestLockNavigationNode": "Assets/FTUE/Scripts/Graph/Nodes/QuestLockNavigationNode.cs",
    "QuestOpenMicrogameNode": "Assets/FTUE/Scripts/Graph/Nodes/QuestOpenMicrogameNode.cs",
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
NODES = [
    ("lock", "QuestLockNavigationNode", "Lock Nav - Arcade Only", "  unlock: 0\n", "open"),
    ("open", "QuestOpenMicrogameNode", "Open Game of the Week - until the Lesson ends",
     "  source: 0\n  mode: 33\n  forceEntry: 1\n  holdUntilLessonEnds: 1\n  settleSeconds: 0.6\n", "unlock"),
    ("unlock", "QuestLockNavigationNode", "Unlock Nav Buttons", "  unlock: 1\n", "end"),
    ("end", "QuestPhaseEndNode", "Phase 0 Complete", "", None),
]
POSITIONS = {"lock": (60, 40), "open": (380, 40), "unlock": (700, 40), "end": (1020, 40)}


def h(text):
    return hashlib.md5(("cosmic-shore-railroad:" + text).encode()).hexdigest()


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
    out.append(header(11400000, "QuestPhaseGraphSO", "MainQuest_Phase0_Railroad", "CosmicShore.Core")
               + "  graphId: MainQuest_Phase0_Railroad\n  phaseName: First Login - Game of the Week\n"
               + "  phaseEnabled: 1\n  runsUnderDeveloperUnlock: 1\n"
               + "  designerNotes: Walks a new player into the Game of the Week microgame and holds\n"
               + "    navigation until their first Lesson is done. TRAINING_PLAN section 5.\n"
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
        errors.append(f"{QUEST}: phases[0] is not the railroad")

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
    if not re.search(r"^  holdUntilLessonEnds: 1$", phase, re.M):
        errors.append(f"{PHASE}: the open node does not hold until the Lesson ends - navigation "
                      "would unlock before the Lesson, and a resume would not re-open the microgame")

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

    def fires(path, a, b, what):
        saved = store[path]
        assert a in saved, f"negative control '{what}' has nothing to mutate"
        store[path] = saved.replace(a, b, 1)
        assert check(read), f"negative control did not fire: {what}"
        store[path] = saved

    fires(QUEST, h("guid:" + PHASE), "0" * 32, "quest not re-pointed")
    fires(PHASE, "runsUnderDeveloperUnlock: 1", "runsUnderDeveloperUnlock: 0", "no gate opt-in")
    fires(PHASE, f"targetNodeId: {node_id('end')}", "targetNodeId: deadbeef", "dangling edge")
    fires(PHASE, "CosmicShore.Core.QuestOpenMicrogameNode", "CosmicShore.Core.QuestPhaseEndNode", "class/guid mismatch")
    fires(PHASE, "holdUntilLessonEnds: 1", "holdUntilLessonEnds: 0", "no hold")
    fires(GOTW, f"  - {MODES['Skein']}\n", "  - 31\n", "mode with no card")
    print("self-test: clean seed passes; 6 negative controls fire")
    return 0


def main(argv):
    if "--self-test" in argv:
        return self_test()
    if "--check" not in argv:
        force = "--force" in argv
        for path, text in ((GOTW, gotw_asset()), (PHASE, phase_asset())):
            full = os.path.join(ROOT, path)
            if os.path.exists(full) and not force:
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
            print(f"re-pointed {QUEST} phases[0] at the railroad")
    errors = check(disk)
    for e in errors:
        print("ERROR: " + e)
    print("first-login railroad: " + ("FAIL" if errors else "ok"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
