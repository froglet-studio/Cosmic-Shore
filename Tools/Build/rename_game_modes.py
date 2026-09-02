#!/usr/bin/env python3
"""Bring the GameModes code names into line with the names players actually see.

The display name is the source of truth: every rename below was read off that mode's own
``SO_ArcadeGame.DisplayName``, not invented here.

TWO RULES decide what gets renamed, and both matter:

1. **Rename the MODE, never the NOUN.** ``SpawnableRibcage`` really is a ribcage-shaped arena
   and ``CrystalCaptureConfigSO`` really is the crystal *collection* feel (Snatch/Suction/Absorb,
   ``Docs/ECOSYSTEM.md`` §31) - neither has anything to do with the game mode that happens to
   share the word. Those live in PROTECTED and are sentinel-swapped out before any substitution
   runs. A blanket replace would have renamed both, and the second one silently, because nothing
   in the ecology reads the word "capture" as a mode.

2. **Longest key first.** ``MultiplayerCellularDuel`` must resolve before ``CellularDuel``, or
   the short key rewrites the long one's tail and produces ``MultiplayerDuelForTheCell``.
   REPLACEMENTS is ordered and the script asserts that ordering rather than trusting it.

Enum VALUES are never touched - they are pinned forever (``GameModes.cs``), and the whole point
of pinning them is that a name can move without a saved selection moving with it.

The cost this pays for is real and is handled elsewhere: enum member names are persisted as
STRINGS in Cloud Save (``GameModeProgressionData.UnlockedModes``, ``ModeStatsCloudData.MakeKey``),
so every rename here orphans a key. ``GameModeRenameMigration`` translates them on load; this
script and that migration must be edited together.

Usage:
    python3 Tools/Build/rename_game_modes.py --check    # report, write nothing
    python3 Tools/Build/rename_game_modes.py --apply
"""

import argparse
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# --------------------------------------------------------------------------------------
# The map
# --------------------------------------------------------------------------------------

# Identifiers that CONTAIN a renamed token but name a thing, not a mode. Sentinel-swapped
# before substitution and restored after, so no ordering trick is needed to protect them.
PROTECTED = [
    "CrystalCaptureConfigTests",
    "CrystalCaptureConfigSO",
    "CrystalCaptureConfig",
    "SpawnableRibcage",
]

# (old, new, enum_id, note). Order is load-bearing: longest key first.
REPLACEMENTS = [
    ("MultiplayerWildlifeBlitzMiniGame", "CoOpWildlifeBlitzMiniGame", 32, "the controller"),
    ("MultiplayerWildlifeBlitzGame", "CoOpWildlifeBlitz",    32, "Multiplayer Co-Op Wildlife Blitz"),
    ("MultiplayerCrystalCapture",    "Scurry",               35, "Scurry"),
    ("MultiplayerCellularDuel",      "OnlineDuelForTheCell", 29, "Online Duel for the Cell"),
    ("MultiplayerJoust",             "Joust",                34, "Joust"),
    ("CrystalCapture",               "Scurry",               35, "Scurry (non-prefixed forms)"),
    ("CellularDuel",                 "DuelForTheCell",        8, "Duel for the Cell"),
    ("NucleusRush",                  "BroodRush",            38, "Brood Rush"),
    ("HexRace",                      "SkimRace",             33, "Skim Race"),
    ("Ribcage",                      "PeelTheCage",          39, "Peel the Cage"),
    ("Tournament",                   "Maelstrom",            36, "Maelstrom"),
]

# Dead modes (no scene on disk) whose only footprint is the bare enum member plus their
# own asset. Swept the same way; listed apart because the footprint is one identifier.
ENUM_ONLY = [
    ("MazeRunner", "MazeRun",     25, "Maze Run"),
    ("Darts",      "DolphinDarts", 3, "Dolphin Darts"),
]

# Deliberately NOT renamed, with the reason, so the next person does not re-derive it:
#   Bends -> TheBends            an ARTICLE, not a word. `TheBendsController` is worse
#                                English than `BendsController`; identifiers drop leading
#                                articles by convention.
#   MultiplayerFreestyle         "Freestyle" alone already means the Menu_Main lava lamp
#                                (CLAUDE.md, "Lava-Lamp Mode"). Dropping the prefix would
#                                point the name at a different feature.
#   Multiplayer2v2CoOpVsAI       display name "Online 2v2 CoOp vs AI" is already what the
#                                identifier says.

TEXT_EXTENSIONS = (".cs", ".md", ".py", ".json")

SKIP_DIRS = {
    ".git", "Library", "Temp", "obj", "Build", "Builds", "Logs",
    "PlayFabSDK", "Wwise", "NiceVibrations", "Plugins", "Packages",
}


def all_replacements():
    return REPLACEMENTS + ENUM_ONLY


def assert_order():
    """A SHORT key must never run before a longer key that contains it.

    Replacement is plain substring, not word-boundary: `\bHexRace\b` would decline to match
    inside `HexRaceController`, which is most of what needs renaming. Substring replacement is
    therefore correct AND order-sensitive, so the ordering is asserted rather than assumed.
    """
    entries = all_replacements()
    keys = [old for old, _, _, _ in entries]

    for i, key in enumerate(keys):
        for later in keys[i + 1:]:
            if key in later:
                raise SystemExit(
                    f"ORDER BUG: '{key}' runs before '{later}' and is a substring of it. "
                    f"The short key would corrupt the long key's tail. Move '{later}' earlier."
                )

    for old, new, _, _ in entries:
        for other_old, _, _, _ in entries:
            if other_old != old and other_old in new:
                raise SystemExit(
                    f"RE-MATCH BUG: '{old}' -> '{new}', but '{other_old}' is a substring of the "
                    f"result and would be substituted again."
                )


def substitute(text):
    """Apply the map to one blob, protecting the nouns."""
    for i, name in enumerate(PROTECTED):
        text = text.replace(name, f"\x00PROT{i}\x00")

    for old, new, _, _ in all_replacements():
        # A value that contains its own key (Darts -> DolphinDarts) is not idempotent under
        # plain replacement: a second run yields DolphinDolphinDarts. Guard with a lookbehind
        # for whatever the value prepends, so re-running the script is always a no-op.
        prefix = new.split(old)[0] if old in new else ""
        pattern = rf"(?<!{re.escape(prefix)}){re.escape(old)}" if prefix else re.escape(old)
        text = re.sub(pattern, new, text)

    for i, name in enumerate(PROTECTED):
        text = text.replace(f"\x00PROT{i}\x00", name)

    return text


def walk(roots):
    for root in roots:
        base = os.path.join(REPO, root)
        if os.path.isfile(base):
            yield base
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for fn in filenames:
                if fn.endswith(TEXT_EXTENSIONS):
                    yield os.path.join(dirpath, fn)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="write the changes")
    ap.add_argument("--check", action="store_true", help="report only (default)")
    ap.add_argument("--roots", nargs="*",
                    default=["Assets/_Scripts", "Docs", "Tools", "CLAUDE.md"])
    args = ap.parse_args()

    assert_order()

    changed, total_hits = [], 0
    for path in walk(args.roots):
        try:
            with open(path, encoding="utf-8-sig") as fh:
                original = fh.read()
        except (UnicodeDecodeError, OSError):
            continue

        updated = substitute(original)
        if updated == original:
            continue

        hits = sum(
            original.count(old) for old, _, _, _ in all_replacements()
        )
        total_hits += hits
        changed.append((os.path.relpath(path, REPO), hits))

        if args.apply:
            with open(path, "w", encoding="utf-8") as fh:
                fh.write(updated)

    verb = "Rewrote" if args.apply else "Would rewrite"
    print(f"{verb} {len(changed)} files, {total_hits} identifier occurrences.\n")
    for rel, hits in sorted(changed, key=lambda r: -r[1])[:25]:
        print(f"  {hits:5d}  {rel}")
    if len(changed) > 25:
        print(f"  ... and {len(changed) - 25} more")

    if not args.apply:
        print("\n(--check: nothing written. Re-run with --apply.)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
