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
    # The ARENA is a ribcage and keeps its name; only the MODE that peels it was renamed. These
    # are the arena's own prefabs, cell configs and spawn profile - the same set
    # rename_game_mode_files.py protects, restated here because the two scripts protect at
    # different granularities (a path fragment there, an identifier here) and a name protected
    # in one and not the other is a script that points at a file that was never moved.
    "SpawnableRibcage",
    "Ribcage Spawn Profile",
    "Ribcage Cell",
    "RibcageSpawnProfile",
    "RibcageCellConfig",
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

# Files that must KEEP the old names, and would be destroyed by a second run.
#
# The migration map is the sharpest case: rewriting `{"HexRace": "SkimRace"}` into
# `{"SkimRace": "SkimRace"}` turns the migration into a no-op WITHOUT breaking a build or
# failing a test that does not check the map's sources - so the next player to load a
# pre-rename save silently loses every unlock, quest and best. The tests are excluded for the
# same reason (they assert on the old names by construction), and the two docs deliberately
# record what a mode USED to be called.
EXCLUDED_PATHS = (
    "Assets/_Scripts/System/CloudData/GameModeRenameMigration.cs",
    "Assets/_Scripts/Tests/Editor/GameModeRenameMigrationTests.cs",
    "Docs/ShuffleSystem/ARCHITECTURE.md",
    "CLAUDE.md",   # its ShuffleSystem row states the old code name on purpose
    "AGENTS.md",   # a copy of CLAUDE.md, carrying the same row for the same reason
    # These two ARE the map. Sweeping them rewrites `("HexRace", "SkimRace")` into
    # `("SkimRace", "SkimRace")`, which is the same self-erasure the migration map suffers, and
    # it takes PROTECTED and DOC_NAMES with it.
    "Tools/Build/rename_game_modes.py",
    "Tools/Build/rename_game_mode_files.py",
    # Its `guid()` seeds are md5 INPUTS, not names - see that file's docstring. Its paths and
    # dict keys were renamed by hand with the seeds held frozen; a mechanical re-sweep would
    # move the seeds and silently re-mint every guid the mode's assets are addressed by.
    "Tools/Build/author_ribcage_assets.py",
)

TEXT_EXTENSIONS = (".cs", ".md", ".py", ".json")

# Matched on the BARE directory name at any depth, so a name here must be one that could
# never legitimately hold first-party source. `Build` looked like it belonged and does not:
# it is the build OUTPUT directory at the repo root AND the name of `Tools/Build/`, where every
# build script lives - so listing it silently excluded the whole tooling directory from the
# sweep, and the scripts there kept referencing scenes and prefabs this rename had moved.
# Output directories are skipped by PATH instead (SKIP_PATHS), which is what they always were.
SKIP_DIRS = {
    ".git", "Library", "Temp", "obj", "Logs",
    "PlayFabSDK", "Wwise", "NiceVibrations", "Plugins", "Packages",
}

# Repo-root-relative directories to skip whole. Anchored, so a same-named directory deeper in
# the tree (`Tools/Build`) is unaffected.
SKIP_PATHS = ("Build", "Builds")


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
        #
        # The identifier form is not the only form the prefix appears in: PROSE writes the
        # display name "Dolphin Darts", where a bare `(?<!Dolphin)` does not fire and the
        # rewrite produced "Dolphin DolphinDarts" in README.md. So the guard also refuses a
        # prefix followed by one separator. Python lookbehinds are fixed-width, hence two.
        prefix = new.split(old)[0] if old in new else ""
        if prefix:
            esc = re.escape(prefix)
            pattern = rf"(?<!{esc})(?<!{esc}[ _-]){re.escape(old)}"
        else:
            pattern = re.escape(old)
        text = re.sub(pattern, new, text)

    for i, name in enumerate(PROTECTED):
        text = text.replace(f"\x00PROT{i}\x00", name)

    return text


def walk(roots):
    for root in roots:
        base = os.path.join(REPO, root)
        if os.path.isfile(base):
            if os.path.relpath(base, REPO).replace(os.sep, "/") not in EXCLUDED_PATHS:
                yield base
            continue
        for dirpath, dirnames, filenames in os.walk(base):
            rel_dir = os.path.relpath(dirpath, REPO).replace(os.sep, "/")
            if rel_dir in SKIP_PATHS or any(rel_dir.startswith(p + "/") for p in SKIP_PATHS):
                dirnames[:] = []
                continue
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for fn in filenames:
                if not fn.endswith(TEXT_EXTENSIONS):
                    continue
                path = os.path.join(dirpath, fn)
                rel = os.path.relpath(path, REPO).replace(os.sep, "/")
                if rel in EXCLUDED_PATHS:
                    continue
                yield path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true", help="write the changes")
    ap.add_argument("--check", action="store_true", help="report only (default)")
    # `Assets/_Scripts` alone missed `Assets/FTUE` (a compile error: the FTUE adapter names a
    # renamed CallToActionTargetType member), and the two root-level docs are separate files that
    # each restate the mode table. Root at `Assets` rather than enumerating subdirectories.
    ap.add_argument("--roots", nargs="*",
                    default=["Assets", "Docs", "Tools", "CLAUDE.md", "AGENTS.md", "README.md"])
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
