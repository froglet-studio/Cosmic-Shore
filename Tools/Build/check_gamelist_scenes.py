#!/usr/bin/env python3
"""Verify that every card in every `SO_GameList` names a scene the player can
actually load.

WHY THIS EXISTS.

An arcade card is a `SO_ArcadeGame` asset carrying a `SceneName` string, and the
launch pipeline does nothing with that string but hand it to Unity's scene
manager. So a card whose scene was deleted, renamed, or never added to the build
settings renders normally, sits in the grid looking playable, and fails at the
one moment a player has committed to it. Nothing in the Editor reports it: the
string field is still a valid string.

Measured on 2026-09-08, before this gate landed: of six `SO_GameList` assets,
FIVE carried cards whose scene does not exist on disk -- `PreviousAllGames` (15),
`LaunchPartyAllGames` (8), `AllGames` (9 rows / 8 distinct, one card listed
twice), `ArcadeGames` (4), `LeaderboardGames` (3). Only the live arcade roster,
`OrganicRematchGames`, was clean at 16/16.

The reason that was a landmine rather than a curiosity is
`ArcadeExploreView.rosterOverride`: the Arena screen is "the arcade pointed at a
second roster", one serialized field away. Pointing it at any of those five --
all of them plausible by name -- would have shipped a screen of dead cards, and
the failure would have read as a broken Arena rather than as a stale asset that
had been wrong for years.

WHAT IT CHECKS -- every one a hard failure, because every one is a card a player
can press and not get a game from:

  null-entry        an empty slot in `Games`. Worse than a dead scene: the
                    consumers iterate the list and dereference.
  missing-asset     the entry's guid resolves to no asset in the project.
  not-a-card        the entry resolves to an asset that is not a game card.
  empty-scene       the card's `SceneName` is blank.
  missing-scene     no `.unity` file of that name exists under Assets/.
  ambiguous-scene   two or more do. Loading by bare name is then a coin toss.
  not-in-build      the scene exists but is absent from, or disabled in,
                    `ProjectSettings/EditorBuildSettings.asset`. A scene outside
                    the build settings cannot be loaded by name at runtime, so
                    this fails exactly like a deleted scene while looking fine
                    in the project window.
  duplicate-card    the same card appears twice in one roster, which draws the
                    same mode twice in the grid.
  archived-reference  an asset outside `Games/_Archive/` still points at a DEAD
                    asset inside it: an archived card whose scene cannot be
                    loaded, or an archived asset that points at one. Archiving a
                    card takes it off the rosters, but a vessel's `Games` list
                    and a training game's `Game` field are not rosters, so the
                    card stays in the build and something launches it. Measured
                    2026-11-20: the vessel class assets still listed five cards
                    archived on 2026-10-13, and all four entries of
                    `TrainingGames` (the daily challenge's whole pool) led to
                    deleted scenes; a development build logged "Scene
                    'MinigameElimination' couldn't be loaded". An archived card
                    whose scene still loads (parked, not retired) is not a
                    failure.

WHAT IT DELIBERATELY DOES NOT DO: there is no `--fix`. Two of the failures above
have two possible repairs and the tool cannot tell them apart -- `not-in-build`
usually means "add the scene to the build settings", not "delete the card", and
an automatic prune would quietly answer a new mode's missing build entry by
deleting its card. Pruning a roster is a decision; this reports.

    python3 Tools/Build/check_gamelist_scenes.py
    python3 Tools/Build/check_gamelist_scenes.py --self-test

Exit code 0 when clean, 1 when any card fails, 2 on a usage/setup error.
"""

from __future__ import annotations

import argparse
import os
import re
import sys

# Tools/Build/<this file> -> Tools/Build -> Tools -> the repository root.
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# The script asset every roster's `m_Script` points at. Resolved from the .cs.meta
# rather than hardcoded, so moving the file does not silently reduce this gate to
# "found no rosters, OK" -- the one failure mode a scanner cannot report on itself.
# Forward slashes, as Unity writes paths (the build settings, every asset reference): on
# Windows `os.path.join` and `os.path.relpath` answer backslashes, no scene path then
# matched the build settings, and this gate failed every card on every roster.
GAMELIST_SCRIPT = "Assets/_Scripts/ScriptableObjects/SO_GameList.cs"
# Both the concrete card type and its base, so a roster holding a bare SO_Game is
# still read rather than reported as not-a-card.
CARD_SCRIPTS = [
    "Assets/_Scripts/ScriptableObjects/SO_ArcadeGame.cs",
    "Assets/_Scripts/ScriptableObjects/SO_Game.cs",
]

# Where a retired card (and anything retired with it) is parked.
ARCHIVE_MARK = "/Games/_Archive/"

GUID_RE = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
SCRIPT_RE = re.compile(r"^\s*m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})", re.M)
NAME_RE = re.compile(r"^\s*m_Name: (.*)$", re.M)
SCENE_NAME_RE = re.compile(r"^\s*SceneName: (.*)$", re.M)
ENTRY_GUID_RE = re.compile(r"guid: ([0-9a-f]{32})")
GAMES_RE = re.compile(r"^(\s*)Games:\s*(\S*)\s*$", re.M)

# `- enabled: 1` / `path: ...` pairs in EditorBuildSettings.
BUILD_ENTRY_RE = re.compile(r"-\s+enabled:\s*(\d)\s*\n\s*path:\s*(.*)")


def rel_path(path: str) -> str:
    """Repo-relative, with forward slashes on every OS."""
    return os.path.relpath(path, ROOT).replace(os.sep, "/")


def read(path: str) -> str:
    with open(path, encoding="utf-8", errors="replace") as fh:
        return fh.read()


def unquote(value: str) -> str:
    """A Unity YAML scalar as a plain string.

    Unity quotes a value only when it has to (leading space, special chars), so
    both `SceneName: MinigameJoust` and `SceneName: 'Minigame Joust'` occur.
    """
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "'\"":
        return value[1:-1]
    return value


def index_guids(assets_dir: str) -> "dict[str, str]":
    """guid -> repo-relative asset path, over every `.meta` under Assets/."""
    out: "dict[str, str]" = {}
    for dirpath, _, files in os.walk(assets_dir):
        for name in files:
            if not name.endswith(".meta"):
                continue
            meta = os.path.join(dirpath, name)
            try:
                head = read(meta)[:400]
            except OSError:
                continue
            m = GUID_RE.search(head)
            if m:
                out[m.group(1)] = rel_path(meta[:-5])
    return out


def index_scenes(assets_dir: str) -> "dict[str, list[str]]":
    """scene name (no extension) -> every `.unity` on disk with that name."""
    out: "dict[str, list[str]]" = {}
    for dirpath, _, files in os.walk(assets_dir):
        for name in files:
            if name.endswith(".unity"):
                out.setdefault(name[:-6], []).append(rel_path(os.path.join(dirpath, name)))
    return out


def dead_archived(archived: "dict[str, str]", card_guids: "set[str]",
                  scenes: "dict[str, list[str]]",
                  build: "dict[str, bool]") -> "dict[str, str]":
    """guid -> why, for every archived asset that can only fail: a card whose scene cannot
    be loaded, and (to a fixed point) any archived asset that points at one."""
    texts = {}
    for guid, path in archived.items():
        if path.endswith(".asset"):
            try:
                texts[guid] = read(os.path.join(ROOT, path))
            except OSError:
                continue
    dead: "dict[str, str]" = {}
    for guid, text in texts.items():
        m = SCRIPT_RE.search(text)
        if not m or m.group(1) not in card_guids:
            continue
        sm = SCENE_NAME_RE.search(text)
        scene = unquote(sm.group(1)) if sm else ""
        found = scenes.get(scene, []) if scene else []
        if len(found) != 1 or (build and not build.get(found[0], False)):
            dead[guid] = f"its scene '{scene}' cannot be loaded"
    changed = True
    while changed:
        changed = False
        for guid, text in texts.items():
            if guid in dead:
                continue
            hit = next((x for x in ENTRY_GUID_RE.findall(text) if x in dead), None)
            if hit:
                dead[guid] = f"it points at {archived[hit]}"
                changed = True
    return dead


def index_build_settings(path: str) -> "dict[str, bool]":
    """scene path (as written in the build settings) -> enabled."""
    if not os.path.isfile(path):
        return {}
    out: "dict[str, bool]" = {}
    for enabled, scene_path in BUILD_ENTRY_RE.findall(read(path)):
        out[unquote(scene_path)] = enabled == "1"
    return out


def parse_entries(text: str) -> "list[str | None]":
    """The `Games` list of a roster asset, in order.

    Each element is an entry guid, or None for a null slot. Unity wraps a long
    inline mapping across lines, so continuation lines are joined before the guid
    is read -- a per-line parse drops the guid of any wrapped entry and would
    report a live card as null.
    """
    m = GAMES_RE.search(text)
    if not m:
        return []
    if m.group(2) in ("[]", "[ ]"):
        return []

    # Step past the remainder of the `Games:` line itself. Slicing at m.end()
    # leaves an empty first element, which the loop below reads as "not an entry"
    # and stops on -- the whole scan then reports every roster as 0 entries and
    # therefore clean. The self-test exists because that is exactly what shipped
    # in the first draft of this file.
    rest = text[m.end():]
    rest = rest.split("\n", 1)[1] if "\n" in rest else ""

    entries: "list[str | None]" = []
    buf = ""
    for line in rest.splitlines():
        stripped = line.strip()
        if buf:                                   # mid-entry continuation
            buf += " " + stripped
        elif stripped.startswith("- "):
            buf = stripped[2:]
        else:
            break                                 # first line that is not an entry
        if "{" in buf and "}" not in buf:
            continue                              # wrapped; keep collecting
        g = ENTRY_GUID_RE.search(buf)
        entries.append(g.group(1) if g else None)
        buf = ""
    return entries


# The three rosters whose relationship is a stated contract:
# Docs/HomeHub/ARCHITECTURE.md - "ArcadeGames | the Arcade grid's rosterOverride |
# the master minus the arena cards". MASTER is the asset AppManager.prefab wires
# for [Inject] SO_GameList, so a card absent from BOTH grids is launchable by the
# lookups and reachable from no screen.
MASTER_ROSTER = os.path.join("Assets", "_SO_Assets", "Games", "GameLists",
                             "OrganicRematchGames.asset")
GRID_ROSTERS = [
    os.path.join("Assets", "_SO_Assets", "Games", "GameLists", "ArcadeGames.asset"),
    os.path.join("Assets", "_SO_Assets", "Games", "GameLists", "ArenaGames.asset"),
]


def report_grid_coverage(assets_dir: str) -> "list[str]":
    """Master-roster cards that reach NEITHER the Arcade nor the Arena grid.

    REPORTS, never fails -- deliberately. Withholding a finished mode from the
    grid while keeping it launchable is a legitimate state (an unannounced mode,
    one still being tuned), so this cannot be a hard gate without being wrong
    about that case. What it is for is the opposite case, which has now happened
    twice in a row and is silent both times: a mode ships, its card is added to
    the master roster because the AI spawner and the config sync resolve through
    that list, and nobody adds it to `ArcadeGames` -- so the mode is complete,
    launchable, in the build settings, drawn by no screen, and reported by
    nothing. Breakwater (50) and Skein (51) sat in exactly that state.

    Returns display lines; an empty list means every master card is on a grid.
    """
    guids = index_guids(assets_dir)

    def entries(rel: str) -> "set[str]":
        path = os.path.join(ROOT, rel)
        if not os.path.isfile(path):
            return set()
        return {g for g in parse_entries(read(path)) if g}

    master = entries(MASTER_ROSTER)
    if not master:
        return []                      # no master to compare against; say nothing
    on_a_grid: "set[str]" = set()
    for rel in GRID_ROSTERS:
        on_a_grid |= entries(rel)

    lines = []
    for g in sorted(master - on_a_grid):
        path = guids.get(g)
        label = g
        if path:
            text = read(os.path.join(ROOT, path))
            name = re.search(r"^  DisplayName: (.*)$", text, re.M)
            mode = re.search(r"^  Mode: (.*)$", text, re.M)
            label = (unquote(name.group(1).strip()) if name
                     else os.path.basename(path))
            if mode:
                label += f" (mode {mode.group(1).strip()})"
        lines.append(label)
    return lines


def scan(assets_dir: str, build_settings: str) -> "tuple[list[str], list[str]]":
    """Returns (failure lines, report lines)."""
    guids = index_guids(assets_dir)
    scenes = index_scenes(assets_dir)
    build = index_build_settings(build_settings)
    by_path = {v: k for k, v in guids.items()}

    list_guid = by_path.get(GAMELIST_SCRIPT)
    if not list_guid:
        return ([f"error: cannot resolve {GAMELIST_SCRIPT} - has SO_GameList.cs "
                 f"moved? This gate cannot find any roster without it."], [])
    card_guids = {by_path[p] for p in CARD_SCRIPTS if p in by_path}

    archived = {g: p for g, p in guids.items() if ARCHIVE_MARK in p}
    dead = dead_archived(archived, card_guids, scenes, build)
    archive_refs: "list[str]" = []

    rosters = []
    for dirpath, _, files in os.walk(assets_dir):
        for name in files:
            if not name.endswith(".asset"):
                continue
            path = os.path.join(dirpath, name)
            text = read(path)
            rel = rel_path(path)
            m = SCRIPT_RE.search(text)
            if m and m.group(1) == list_guid:
                rosters.append((rel, text))
            if dead and ARCHIVE_MARK not in rel:
                for g in sorted({x for x in ENTRY_GUID_RE.findall(text) if x in dead}):
                    archive_refs.append(
                        f"{rel}  [ref]  archived-reference: still points at {archived[g]} "
                        f"({dead[g]}); take the entry out")
    rosters.sort()

    failures: "list[str]" = sorted(archive_refs)
    report: "list[str]" = []

    if not rosters:
        return ([f"error: no SO_GameList asset found under {assets_dir}. The "
                 f"scan cannot be clean and empty at the same time."], [])

    for rel, text in rosters:
        entries = parse_entries(text)
        seen: "dict[str, int]" = {}
        bad = 0
        for index, guid in enumerate(entries):
            def fail(code: str, detail: str) -> None:
                nonlocal bad
                bad += 1
                failures.append(f"{rel}  [{index}]  {code}: {detail}")

            if guid is None:
                fail("null-entry", "empty slot in Games")
                continue
            if guid in seen:
                fail("duplicate-card",
                     f"same card as entry [{seen[guid]}] "
                     f"({guids.get(guid, guid)})")
                continue
            seen[guid] = index

            card_path = guids.get(guid)
            if not card_path:
                fail("missing-asset", f"guid {guid} resolves to no asset")
                continue
            card_text = read(os.path.join(ROOT, card_path))
            m = SCRIPT_RE.search(card_text)
            if card_guids and (not m or m.group(1) not in card_guids):
                fail("not-a-card", f"{card_path} is not an SO_ArcadeGame/SO_Game")
                continue

            sm = SCENE_NAME_RE.search(card_text)
            scene = unquote(sm.group(1)) if sm else ""
            if not scene:
                fail("empty-scene", f"{card_path} names no scene")
                continue

            found = scenes.get(scene, [])
            if not found:
                fail("missing-scene", f"{card_path} -> '{scene}.unity' does not exist")
                continue
            if len(found) > 1:
                fail("ambiguous-scene",
                     f"{card_path} -> '{scene}' matches {len(found)} scenes: "
                     + ", ".join(found))
                continue
            if build and not build.get(found[0], False):
                why = ("disabled in" if found[0] in build else "absent from")
                fail("not-in-build",
                     f"{card_path} -> {found[0]} is {why} the build settings, "
                     f"so it cannot be loaded by name")
                continue

        total = len(entries)
        mark = "OK  " if bad == 0 else "FAIL"
        report.append(f"  {mark} {rel}  {total - bad}/{total} launchable")

    return failures, report


# --------------------------------------------------------------------------
# Self-test
#
# CLAUDE.md's rule: a gate nobody has watched FAIL is a gate nobody should
# trust. This one is especially prone to a silent no-op -- it finds its work by
# matching a script guid, so a moved file or a changed YAML shape turns it into
# "scanned nothing, OK". The fixtures build a throwaway project on disk and run
# the real `scan()` over it, so what is exercised is the shipped code path and
# not a re-implementation of it.
# --------------------------------------------------------------------------
def run_self_test() -> int:
    import shutil
    import tempfile

    tmp = tempfile.mkdtemp(prefix="gamelist-selftest-")
    try:
        global ROOT
        real_root = ROOT
        ROOT = tmp
        assets = os.path.join(tmp, "Assets")
        scripts = os.path.join(assets, "_Scripts", "ScriptableObjects")
        scenedir = os.path.join(assets, "_Scenes")
        cards = os.path.join(assets, "Cards")
        archive = os.path.join(assets, "Games", "_Archive")
        for d in (scripts, scenedir, cards, archive, os.path.join(tmp, "ProjectSettings")):
            os.makedirs(d, exist_ok=True)

        def meta(path: str, guid: str) -> None:
            with open(path + ".meta", "w", encoding="utf-8") as fh:
                fh.write(f"fileFormatVersion: 2\nguid: {guid}\n")

        def write(path: str, body: str, guid: str) -> None:
            with open(path, "w", encoding="utf-8") as fh:
                fh.write(body)
            meta(path, guid)

        g = {n: f"{i:032x}" for i, n in enumerate(
            ["list", "card", "arcadecard", "gamecard", "sceneok", "scenegone",
             "scenedup", "scenenobuild", "sceneempty", "dupA", "wrongtype",
             "notacard", "roster", "oldcard", "oldtraining", "vessel", "parkedcard"],
            start=1)}

        write(os.path.join(scripts, "SO_GameList.cs"), "// list\n", g["list"])
        write(os.path.join(scripts, "SO_ArcadeGame.cs"), "// card\n", g["arcadecard"])
        write(os.path.join(scripts, "SO_Game.cs"), "// base\n", g["gamecard"])

        def card(name: str, scene: str, guid: str, script: str = None) -> None:
            write(os.path.join(cards, name + ".asset"),
                  "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n"
                  f"  m_Script: {{fileID: 11500000, guid: {script or g['arcadecard']}, type: 3}}\n"
                  f"  m_Name: {name}\n  SceneName: {scene}\n", guid)

        card("Ok", "SceneOk", g["sceneok"])
        card("Gone", "SceneGone", g["scenegone"])
        card("Dup", "SceneDup", g["scenedup"])
        card("NoBuild", "SceneNoBuild", g["scenenobuild"])
        card("Empty", "", g["sceneempty"])
        card("DupA", "SceneOk", g["dupA"])
        card("NotACard", "SceneOk", g["notacard"], script=g["wrongtype"])

        for rel in ("SceneOk.unity", "SceneNoBuild.unity", "SceneDup.unity"):
            write(os.path.join(scenedir, rel), "scene\n", f"9{rel:0>31}"[:32])
        # a second SceneDup somewhere else -> ambiguous by bare name
        os.makedirs(os.path.join(scenedir, "Other"), exist_ok=True)
        write(os.path.join(scenedir, "Other", "SceneDup.unity"), "scene\n",
              "a" * 32)

        with open(os.path.join(tmp, "ProjectSettings", "EditorBuildSettings.asset"),
                  "w", encoding="utf-8") as fh:
            fh.write("EditorBuildSettings:\n  m_Scenes:\n"
                     "  - enabled: 1\n    path: Assets/_Scenes/SceneOk.unity\n"
                     "  - enabled: 1\n    path: Assets/_Scenes/SceneDup.unity\n"
                     "  - enabled: 1\n    path: Assets/_Scenes/Other/SceneDup.unity\n"
                     "  - enabled: 0\n    path: Assets/_Scenes/SceneNoBuild.unity\n")

        def roster(name: str, guids: "list[str]") -> None:
            rows = "".join(
                f"  - {{fileID: 0}}\n" if x is None else
                f"  - {{fileID: 11400000, guid: {x}, type: 2}}\n" for x in guids)
            write(os.path.join(assets, name + ".asset"),
                  "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n"
                  f"  m_Script: {{fileID: 11500000, guid: {g['list']}, type: 3}}\n"
                  f"  m_Name: {name}\n  m_EditorClassIdentifier: \n  Games:\n{rows}",
                  f"b{name:0>31}"[:32])

        cases = [
            ("a clean roster passes", ["Clean", [g["sceneok"]]], []),
            ("an empty roster passes", ["Empty", []], []),
            ("a null slot fails", ["Null", [None]], ["null-entry"]),
            ("a dangling guid fails", ["Dangling", ["f" * 32]], ["missing-asset"]),
            ("a non-card asset fails", ["NotCard", [g["notacard"]]], ["not-a-card"]),
            ("a blank SceneName fails", ["Blank", [g["sceneempty"]]], ["empty-scene"]),
            ("a deleted scene fails", ["Gone", [g["scenegone"]]], ["missing-scene"]),
            ("two scenes of one name fail", ["Ambig", [g["scenedup"]]],
             ["ambiguous-scene"]),
            ("a scene outside the build fails", ["NoBuild", [g["scenenobuild"]]],
             ["not-in-build"]),
            ("the same card twice fails", ["Dupe", [g["sceneok"], g["sceneok"]]],
             ["duplicate-card"]),
            ("two cards on one scene is fine", ["TwoCards",
                                                [g["sceneok"], g["dupA"]]], []),
        ]

        failures = 0
        for label, (name, guids), expected in cases:
            roster(name, guids)
            found, _ = scan(assets, os.path.join(tmp, "ProjectSettings",
                                                 "EditorBuildSettings.asset"))
            mine = sorted(line.split("  ")[2].split(":")[0]
                          for line in found if line.startswith("Assets/" + name))
            ok = mine == sorted(expected)
            print(f"  {'PASS' if ok else 'FAIL'}  {label}")
            if not ok:
                failures += 1
                print(f"        expected {sorted(expected) or 'nothing'}, got "
                      f"{mine or 'nothing'}")
            os.remove(os.path.join(assets, name + ".asset"))
            os.remove(os.path.join(assets, name + ".asset.meta"))

        # Archived assets: a reference from inside the archive is fine, one from outside
        # fails, whatever kind of asset holds it (a vessel's list is not a roster).
        settings = os.path.join(tmp, "ProjectSettings", "EditorBuildSettings.asset")
        roster("Clean", [g["sceneok"]])
        write(os.path.join(archive, "Old.asset"),
              "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n"
              f"  m_Script: {{fileID: 11500000, guid: {g['arcadecard']}, type: 3}}\n"
              "  m_Name: Old\n  SceneName: SceneGone\n", g["oldcard"])
        write(os.path.join(archive, "OldTraining.asset"),
              "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n  m_Name: OldTraining\n"
              f"  Game: {{fileID: 11400000, guid: {g['oldcard']}, type: 2}}\n", g["oldtraining"])
        write(os.path.join(archive, "Parked.asset"),
              "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n"
              f"  m_Script: {{fileID: 11500000, guid: {g['arcadecard']}, type: 3}}\n"
              "  m_Name: Parked\n  SceneName: SceneOk\n", g["parkedcard"])
        found, _ = scan(assets, settings)
        archive_cases = [("an archived asset pointing at an archived card passes", not found)]

        def vessel(points_at: str) -> "list[str]":
            write(os.path.join(assets, "Vessel.asset"),
                  "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n  m_Name: Vessel\n  Games:\n"
                  f"  - {{fileID: 11400000, guid: {points_at}, type: 2}}\n", g["vessel"])
            return scan(assets, settings)[0]

        def one_archive_failure(lines: "list[str]") -> bool:
            return (len(lines) == 1 and lines[0].startswith("Assets/Vessel.asset")
                    and "archived-reference" in lines[0])

        archive_cases.append(("a live asset pointing at an archived card with no scene fails",
                              one_archive_failure(vessel(g["oldcard"]))))
        archive_cases.append(("a live asset pointing at an archived asset that leads to one fails",
                              one_archive_failure(vessel(g["oldtraining"]))))
        archive_cases.append(("a live asset pointing at an archived card that still loads passes",
                              not vessel(g["parkedcard"])))
        for label, ok in archive_cases:
            print(f"  {'PASS' if ok else 'FAIL'}  {label}")
            if not ok:
                failures += 1
        for name in ("Vessel.asset", "Clean.asset"):
            os.remove(os.path.join(assets, name))
            os.remove(os.path.join(assets, name + ".meta"))
        extra = len(archive_cases)

        # The no-op guard: with SO_GameList.cs gone the scan must ERROR, never
        # report a clean project.
        os.remove(os.path.join(scripts, "SO_GameList.cs"))
        os.remove(os.path.join(scripts, "SO_GameList.cs.meta"))
        found, _ = scan(assets, os.path.join(tmp, "ProjectSettings",
                                             "EditorBuildSettings.asset"))
        ok = bool(found) and found[0].startswith("error:")
        print(f"  {'PASS' if ok else 'FAIL'}  a moved SO_GameList.cs errors "
              f"rather than scanning nothing")
        if not ok:
            failures += 1

        ROOT = real_root
        total = len(cases) + 1 + extra
        print(f"\nself-test: {total - failures}/{total} passed")
        return 1 if failures else 0
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def main() -> int:
    ap = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default=os.path.join(ROOT, "Assets"),
                    help="directory to scan (default: <repo>/Assets)")
    ap.add_argument("--build-settings",
                    default=os.path.join(ROOT, "ProjectSettings",
                                         "EditorBuildSettings.asset"),
                    help="EditorBuildSettings.asset to read the scene list from")
    ap.add_argument("--check", action="store_true",
                    help="accepted for symmetry with the other Tools/Build gates; "
                         "this tool only ever checks")
    ap.add_argument("--self-test", action="store_true",
                    help="verify the checker still detects its own fixtures, then exit")
    args = ap.parse_args()

    if args.self_test:
        return run_self_test()

    if not os.path.isdir(args.root):
        print(f"error: no such directory: {args.root}", file=sys.stderr)
        return 2

    failures, report = scan(args.root, args.build_settings)

    if report:
        print("SO_GameList rosters:")
        for line in report:
            print(line)
        print()

    stranded = report_grid_coverage(args.root)
    if stranded:
        print("on the master roster but on NEITHER grid (Arcade or Arena) - "
              "launchable, reachable from no screen:")
        for line in stranded:
            print(f"  {line}")
        print("  Add the card to GameLists/ArcadeGames.asset (or ArenaGames.asset), "
              "or leave it if the mode is deliberately unlisted. Reported, not failed.")
        print()

    if failures:
        print(f"{len(failures)} problem(s):", file=sys.stderr)
        for line in failures:
            print(f"  {line}", file=sys.stderr)
        print("\nA card must name a scene that exists on disk AND is enabled in "
              "ProjectSettings/EditorBuildSettings.asset. Add the scene to the "
              "build settings, or remove the card from the roster. Nothing outside "
              "Games/_Archive/ may point at an asset inside it.", file=sys.stderr)
        return 1

    print("OK: every SO_GameList card names a loadable scene, and nothing live points into the archive.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
