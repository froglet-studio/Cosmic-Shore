#!/usr/bin/env python3
"""Rename the FILES and DIRECTORIES the game-mode rename leaves behind.

Runs after ``rename_game_modes.py`` has rewritten file CONTENTS. Two things make this a
separate script rather than a flag on that one:

* **A ``.meta`` must move with its file, in the same commit.** The guid lives in the
  ``.meta``; move the asset without it and Unity mints a new guid, which silently breaks every
  scene and prefab reference to it. ``git mv`` of the pair keeps the guid, so a class rename is
  invisible to the serialized references (CLAUDE.md, "Assembly Definitions").

* **Doc filenames are SHOUTED and the identifier map is case-sensitive.** ``HEXRACE.md`` does
  not contain ``HexRace``, so it needs its own map - and so do the references to it, which the
  content sweep likewise could not see.

PROTECTED paths are the same nouns the content sweep protects, plus the Ribcage CELL configs and
``SpawnableRibcage`` prefabs: those name the ribcage-shaped ARENA, which is a real ribcage and
keeps its name. The mode that peels it is what got renamed.

Usage:
    python3 Tools/Build/rename_game_mode_files.py --check
    python3 Tools/Build/rename_game_mode_files.py --apply
"""

import argparse
import os
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from rename_game_modes import substitute  # noqa: E402

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# All-caps documentation filenames the case-sensitive identifier map cannot see.
# Each entry points at the name the doc carries NOW, not at the next hop in its history: a
# two-generation rename (RIBCAGE -> PEEL_THE_CAGE -> CLEAVE) resolves in ONE lookup here, and a
# chained map would land a stale file on a name nothing uses.
DOC_NAMES = {
    "HEXRACE.md": "SKIMRACE.md",
    "RIBCAGE.md": "CLEAVE.md",
    "PEEL_THE_CAGE.md": "CLEAVE.md",
    "NUCLEUSRUSH.md": "BROODRUSH.md",
    "CRYSTAL_CAPTURE.md": "SCURRY.md",
}

# Path fragments that must never be renamed - they name a thing, not a mode.
PROTECTED_FRAGMENTS = (
    "CrystalCaptureConfig",   # the crystal COLLECTION feel (Docs/ECOSYSTEM.md §31)
    "SpawnableRibcage",       # ONE of Cleave's four arenas really is a ribcage - see below
)
# The cell configs and spawn profile used to be protected here as "Ribcage Cell" / "Ribcage
# Spawn Profile". They are not any more, and they are not renamed by this script either: they
# name the MODE's cell rather than the arena, so they moved to "Cleave Cell" by hand with the
# 2026-09 rename. Listing a path as protected that has since been moved is worse than omitting
# it - it reads as a standing rule about a file that no longer exists.

SEARCH_ROOTS = ["Assets", "Docs"]


def is_protected(path):
    return any(frag in path for frag in PROTECTED_FRAGMENTS)


def new_basename(name):
    if name in DOC_NAMES:
        return DOC_NAMES[name]
    return substitute(name)


def collect():
    """Deepest-first, so a file is renamed before the directory holding it."""
    pairs = []
    for root in SEARCH_ROOTS:
        for dirpath, dirnames, filenames in os.walk(os.path.join(REPO, root), topdown=False):
            for name in filenames + dirnames:
                if name.endswith(".meta"):
                    continue  # moved with its owner
                src = os.path.join(dirpath, name)
                if is_protected(src):
                    continue
                dst_name = new_basename(name)
                if dst_name != name:
                    pairs.append((src, os.path.join(dirpath, dst_name)))
    return pairs


def git_mv(src, dst, apply):
    rel_src = os.path.relpath(src, REPO)
    rel_dst = os.path.relpath(dst, REPO)
    print(f"  {rel_src}\n    -> {rel_dst}")
    if not apply:
        return
    subprocess.run(["git", "mv", rel_src, rel_dst], cwd=REPO, check=True)
    if os.path.exists(src + ".meta"):
        subprocess.run(["git", "mv", rel_src + ".meta", rel_dst + ".meta"], cwd=REPO, check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args()

    pairs = collect()
    print(f"{'Renaming' if args.apply else 'Would rename'} {len(pairs)} paths:\n")
    for src, dst in pairs:
        git_mv(src, dst, args.apply)

    if not args.apply:
        print("\n(--check: nothing moved. Re-run with --apply.)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
