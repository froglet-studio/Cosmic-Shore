#!/usr/bin/env python3
"""web_build_scope.py - what the browser (WebGL) build carries beyond the freestyle Toybox.

Shombith, 2026-11-21: "web has the toybox, all of squirrel's games and all the time games and arena's regetta
unlocked. Maelstrom only picks between time games lets say: I1 - 2 games I2 - 4 games I3 - 6 games and I4 - 8 games."

This script turns that rule into Assets/Resources/WebBuildScope.asset (WebBuildScopeSO), read by the WebGL build
(WebToyboxBuild adds every card's scene), the Arcade and Arena windows (only these cards) and the Maelstrom (only
these buckets). The cards come from the shipped assets, never typed by hand:

  - every card on ArcadeGames that seats the Squirrel;
  - every non-party Time card on ArcadeGames (ModeGenre's own answer, through elemental_loop.cards());
  - the Regatta arena card;
  - the Maelstrom card itself.

The Maelstrom buckets are exclusive, like MaelstromDataSO's own (a run at intensity N draws only bucket N), and are
the one hand-picked part: TIERS below. They were ordered by the Maelstrom's own tier for each card (Garrett's
Beginner / Great / Good / Floor), then by measured maturity (Docs/MaelstromSystem/maelstrom_tiers.json), with
Garrett's "beginner example" Scarab Rally in I1. Change TIERS and run --write to re-deal them.

    python3 Tools/Build/web_build_scope.py            print the scope
    python3 Tools/Build/web_build_scope.py --write    write the asset
    python3 Tools/Build/web_build_scope.py --check    exit 1 when the asset or a rule is wrong
    python3 Tools/Build/web_build_scope.py --self-test  the checks fail on planted mistakes
"""
import argparse
import glob
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import elemental_loop  # noqa: E402  - the board's reading of every card (hulls, lists, elements)

ASSET = os.path.join(REPO, "Assets", "Resources", "WebBuildScope.asset")
SCRIPT_GUID = "723b8b27b1254c149e1d4d56ecb637ce"     # WebBuildScopeSO.cs.meta
ASSET_GUID = "5b1f0c7e3d8a4a96b2e4f1c0a9d7e351"      # WebBuildScope.asset.meta
MAELSTROM = os.path.join(REPO, "Assets", "_SO_Assets", "Maelstrom", "MaelstromData.asset")
GAMES_DIR = os.path.join(REPO, "Assets", "_SO_Assets", "Games")
SCENES_DIR = os.path.join(REPO, "Assets", "_Scenes")

# How many games each Maelstrom intensity deals in the browser (Shombith, 2026-11-21).
TIER_SIZES = {1: 2, 2: 4, 3: 6, 4: 8}

# The buckets, by GameModes name. Exclusive: no card in two buckets.
TIERS = {
    1: ["ScarabRally", "Slingshot"],
    2: ["Waystation", "SkimRace", "Switchback", "ThresherTime"],
    3: ["Breakwater", "SerpentTime", "GrizzlyTime", "Skein", "Warpline", "WrasseRun"],
    4: ["Headlong", "Redline", "BurrowRun", "LanternRun", "BrittleStarTime", "SlipLine", "Breach", "Thornline"],
}

ARENA_CARDS = ["Regatta"]
SQUIRREL = "Squirrel"


def _read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


def card_files():
    """GameModes name -> (card path, guid, SceneName) for every ArcadeGame*.asset."""
    modes = elemental_loop.game_modes(REPO)
    out = {}
    for path in sorted(glob.glob(os.path.join(GAMES_DIR, "ArcadeGame*.asset"))):
        text = _read(path)
        guid = re.search(r"^guid: (\w+)", _read(path + ".meta"), re.M).group(1)
        mode = modes.get(int(re.search(r"^\s+Mode: (\d+)", text, re.M).group(1)))
        scene = re.search(r"^\s+SceneName: (.*)$", text, re.M)
        out[mode] = (path, guid, scene.group(1).strip() if scene else "")
    return out


def scope_modes(all_cards):
    """The rule, as GameModes names in the asset's order: Maelstrom, Squirrel's cards, Time cards, arena cards."""
    arcade = [c for c in all_cards if "ArcadeGames" in c["lists"]]
    squirrel = [c["mode"] for c in arcade if SQUIRREL in c["hulls"] and c["mode"] != "Maelstrom"]
    time = [c["mode"] for c in arcade if "Time" in c["elements"] and not c["party"]]
    arena = [c["mode"] for c in all_cards if c["mode"] in ARENA_CARDS and "ArenaGames" in c["lists"]]
    ordered = []
    for mode in ["Maelstrom"] + squirrel + time + arena:
        if mode not in ordered:
            ordered.append(mode)
    return ordered, set(time)


def scene_exists(name):
    return bool(name) and bool(glob.glob(os.path.join(SCENES_DIR, "**", name + ".unity"), recursive=True))


def queue_guids():
    text = _read(MAELSTROM)
    block = text[text.find("GameQueue:"):text.find("IntensityTiers:")]
    return set(re.findall(r"guid: (\w+)", block))


def errors_for(modes, time_modes, tiers, files, queue, scene_ok=scene_exists):
    """Every way the scope can be wrong. Pure, so --self-test can plant mistakes."""
    errs = []
    for mode in modes:
        if mode not in files:
            errs.append(f"{mode}: no ArcadeGame card")
            continue
        scene = files[mode][2]
        if not scene_ok(scene):
            errs.append(f"{mode}: its scene '{scene}' is not under Assets/_Scenes - the build could not carry it")
    for must in ["Maelstrom", "SkimRace", "Joust"] + ARENA_CARDS:
        if must not in modes:
            errs.append(f"{must}: missing from the scope (the rule names it)")
    seen = {}
    for level, size in TIER_SIZES.items():
        names = tiers.get(level, [])
        if len(names) != size:
            errs.append(f"I{level}: {len(names)} games, the rule is {size}")
        for name in names:
            if name in seen:
                errs.append(f"{name}: in I{seen[name]} and I{level} (buckets are exclusive)")
            seen[name] = level
            if name not in time_modes:
                errs.append(f"{name} (I{level}): not a Time game - the browser Maelstrom deals only Time games")
            if name not in modes:
                errs.append(f"{name} (I{level}): not in the scope, so its scene would not be in the build")
            if name in files and files[name][1] not in queue:
                errs.append(f"{name} (I{level}): not in MaelstromData's GameQueue, so a draw could not travel")
    return errs


def ref(guid):
    return f"{{fileID: 11400000, guid: {guid}, type: 2}}"


def asset_text(modes, tiers, files):
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}",
        "  m_Name: WebBuildScope",
        "  m_EditorClassIdentifier: ",
        "  games:",
    ]
    lines += [f"  - {ref(files[m][1])}" for m in modes]
    lines.append("  maelstromTiers:")
    for level in sorted(tiers):
        lines.append(f"  - Intensity: {level}")
        lines.append("    Games:")
        lines += [f"    - {ref(files[m][1])}" for m in tiers[level]]
    return "\n".join(lines) + "\n"


META = f"""fileFormatVersion: 2
guid: {ASSET_GUID}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def build():
    all_cards = elemental_loop.cards(REPO)
    modes, time_modes = scope_modes(all_cards)
    files = card_files()
    return modes, time_modes, files


def self_test():
    files = {m: (f"{m}.asset", f"g{m}", f"Minigame{m}") for m in
             ["Maelstrom", "SkimRace", "Joust", "Regatta", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J",
              "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T"]}
    time = set("ABCDEFGHIJKLMNOPQRST")
    tiers = {1: list("AB"), 2: list("CDEF"), 3: list("GHIJKL"), 4: list("MNOPQRST")}
    modes = ["Maelstrom", "SkimRace", "Joust", "Regatta"] + sorted(time)
    queue = {v[1] for v in files.values()}
    ok = lambda _: True  # noqa: E731
    assert errors_for(modes, time, tiers, files, queue, ok) == [], "a correct scope must pass"
    planted = {
        "a bucket of the wrong size": ({**tiers, 1: ["A"]}, modes, time, queue, ok),
        "a card in two buckets": ({**tiers, 2: ["A", "D", "E", "F"]}, modes, time, queue, ok),
        "a non-Time card dealt": ({**tiers, 1: ["A", "Joust"]}, modes, time, queue, ok),
        "a dealt card outside the scope": (tiers, [m for m in modes if m != "T"], time, queue, ok),
        "a dealt card outside GameQueue": (tiers, modes, time, queue - {"gA"}, ok),
        "Regatta dropped": (tiers, [m for m in modes if m != "Regatta"], time, queue, ok),
        "a card whose scene is missing": (tiers, modes, time, queue, lambda s: s != "MinigameJoust"),
    }
    for what, (t, m, tm, q, s) in planted.items():
        assert errors_for(m, tm, t, files, q, s), f"the check missed {what}"
    print(f"web_build_scope self-test: a correct scope passes; {len(planted)} planted mistakes each fail.")


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args(argv)
    if args.self_test:
        self_test()
        return 0

    modes, time_modes, files = build()
    errs = errors_for(modes, time_modes, TIERS, files, queue_guids())
    want = asset_text(modes, TIERS, files) if not any("no ArcadeGame card" in e for e in errs) else None

    if args.write:
        if errs:
            print("\n".join("ERROR " + e for e in errs))
            return 1
        with open(ASSET, "w", encoding="utf-8", newline="\n") as f:
            f.write(want)
        if not os.path.exists(ASSET + ".meta"):
            with open(ASSET + ".meta", "w", encoding="utf-8", newline="\n") as f:
                f.write(META)
        print(f"wrote {os.path.relpath(ASSET, REPO)}: {len(modes)} cards, Maelstrom "
              + ", ".join(f"I{k} {len(v)}" for k, v in sorted(TIERS.items())))
        return 0

    if args.check:
        if not os.path.exists(ASSET):
            errs.append(f"{os.path.relpath(ASSET, REPO)} is missing (run --write)")
        elif want is not None and _read(ASSET) != want:
            errs.append(f"{os.path.relpath(ASSET, REPO)} drifted from the rule (run --write)")
        if errs:
            print("\n".join("ERROR " + e for e in errs))
            return 1
        print(f"web_build_scope: OK ({len(modes)} cards; Maelstrom "
              + ", ".join(f"I{k} {len(v)}" for k, v in sorted(TIERS.items())) + ")")
        return 0

    print(f"The browser build carries {len(modes)} cards:")
    for m in modes:
        print(f"  {m:18} {files.get(m, ('', '', '?'))[2]}{'  (Time)' if m in time_modes else ''}")
    for level in sorted(TIERS):
        print(f"Maelstrom I{level}: {', '.join(TIERS[level])}")
    left = sorted(time_modes - {n for v in TIERS.values() for n in v})
    print(f"Time games the Maelstrom never deals here: {', '.join(left) or 'none'}")
    if errs:
        print("\n".join("ERROR " + e for e in errs))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
