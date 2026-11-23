#!/usr/bin/env python3
"""Brood Rush's step-5 levers: each hull's TRAIL VOLUME (Plan step: massspace-arena).

Brood Rush is balanced on net control of space (VC + VD, the VolumeLedger), and a hull's VC is mostly the volume of
the trail it lays. The lever is the hull's own trail prism volume multiplier, `trailVolume` (an ElementalFloat on its
VesselPrismController, `Value` when the float is off), so a hull lays more or less mass per second in every game, not
only here: a vessel change, never a per-mode handicap (Garrett, 2026-11-18: "don't cheat").

The numbers were found by the subset-only tuning loop of Tools/Compute/evaluators/brood_judge.py (3v3 matches in the
real game, 15-20 rounds an iteration), capped at 0.4x-2.5x of what shipped. A hull that needed more than the cap is
flagged for redesign instead (Docs/ElementalLoop/REDESIGN.md). Results: /mnt/project-files/regatta-flywheel/night3/
broodrush/RESULT.md.

    python3 Tools/Build/brood_trail_levers.py           write TABLE into the prefabs
    python3 Tools/Build/brood_trail_levers.py --check   exit 1 when a prefab differs from TABLE
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PREFABS = os.path.join(ROOT, "Assets/_Prefabs/Spacevessels")
SCRIPTS = ("VesselPrismController", "SparrowPrismController", "ShrikePrismController", "BrittleStarPrismController")

# hull: trailVolume.Value. Refit 2026-11-21 at the game's real setup (Medium AI with its mistakes, 3 teams of 3, the
# 17-hull card with #2329 Rhino, #2330 Swordfish, #2331 Manta and #2332's Mass-Space AI rows, shield shells netted),
# capped at 0.4x-2.5x of the pre-#2261 value (1.35 for Squirrel, Stoat, Meerkat, Thresher; else 1).
# /mnt/project-files/regatta-flywheel/broodrush-deep/REFIT.md.
TABLE = {
    "Orca": 0.9784,  # PROVISIONAL (night integration branch only): refit value, lane occupied
    "Grizzly": 0.4,       # the floor; 1.04 at Medium
    "Sparrow": 0.4705,
    "Wrasse": 0.4869,
    "Scarab": 0.9953,
    "Honeyguide": 1.5417,
    "Squirrel": 2.1557,
    "Stoat": 2.2858,  # PROVISIONAL (night integration branch only): refit value, lane occupied
    "Meerkat": 1.9244,
    "Dolphin": 1.6466,  # PROVISIONAL (night integration branch only): refit value, lane occupied
    "Urchin": 2.5,        # the cap, and still 0.61 at Medium: flagged, its Mass kit needs a lever of its own
    "TigerMoth": 0.4,     # the cap, and still 1.21 at Medium on #2332's throttle row: flagged
    "Thresher": 0.9141,  # PROVISIONAL (night integration branch only): refit value, lane occupied
    "Swordfish": 0.7268,  # with #2330's 1.7x afterimages
    "Shrike": 0.0675,     # "shrink shrike's trail so it is almost nothing" (Garrett, 2026-11-19); its lever is the nest (NEST)
}

# The Shrike's mass is its nest (ShrikeRules.NestPieces): its lever is the shell's plate thickness (0.7 shipped), not the trail.
SHRIKE_CONFIG = os.path.join(ROOT, "Assets/_SO_Assets/VesselActions/Shrike/ShrikeConfig.asset")
NEST = {"nestShellThickness": 1.2273}

BLOCK = "  trailVolume:\n    Enabled: 0\n    Value: {v}\n    Min: 1\n    Max: 2.5\n    element: 0\n    UseFloor: 0\n    Floor: 0\n"


def _guids():
    out = set()
    for d, _, files in os.walk(os.path.join(ROOT, "Assets/_Scripts/Controller/Vessel")):
        for f in files:
            if f.endswith(".cs.meta") and f[:-8] in SCRIPTS:
                out.add(re.search(r"guid: (\w+)", open(os.path.join(d, f)).read()).group(1))
    return out


def _fmt(v):
    return f"{v:.4f}".rstrip("0").rstrip(".")


def apply(write):
    guids = _guids()
    drift = []
    for hull, value in TABLE.items():
        path = os.path.join(PREFABS, hull + ".prefab")
        text = open(path).read()
        docs = text.split("\n--- !u!")
        hit = 0
        for i, doc in enumerate(docs):
            m = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+)", doc)
            if not m or m.group(1) not in guids:
                continue
            hit += 1
            want = _fmt(value)
            tv = re.search(r"\n  trailVolume:\n    Enabled: (\d)\n    Value: ([^\n]+)\n", doc)
            if tv:
                if tv.group(1) != "0":
                    raise SystemExit(f"{hull}: trailVolume is Enabled (scales with an element); edit by hand")
                if tv.group(2).strip() != want:
                    drift.append(f"{hull}: {tv.group(2).strip()} -> {want}")
                    docs[i] = doc[:tv.start(2)] + want + doc[tv.end(2):]
            else:
                sh = re.search(r"\n  shielded: \d\n", doc)
                if not sh:
                    raise SystemExit(f"{hull}: no trailVolume block and no 'shielded' line to put one after")
                drift.append(f"{hull}: (default 1) -> {want}")
                docs[i] = doc[:sh.end()] + BLOCK.format(v=want) + doc[sh.end():]
        if hit != 1:
            raise SystemExit(f"{hull}: {hit} prism controllers in {path}; expected exactly one")
        if write:
            open(path, "w").write("\n--- !u!".join(docs))
    text = open(SHRIKE_CONFIG).read()
    for field, value in NEST.items():
        m = re.search(r"\n  " + field + r": ([^\n]+)\n", text)
        if not m:
            raise SystemExit(f"Shrike: no {field} in {SHRIKE_CONFIG}")
        if m.group(1).strip() != _fmt(value):
            drift.append(f"Shrike {field}: {m.group(1).strip()} -> {_fmt(value)}")
            text = text[:m.start(1)] + _fmt(value) + text[m.end(1):]
    if write:
        open(SHRIKE_CONFIG, "w").write(text)
    return drift


def main():
    check = "--check" in sys.argv
    drift = apply(write=not check)
    for d in drift:
        print(("drift: " if check else "wrote: ") + d)
    if check and drift:
        sys.exit(1)
    if not drift:
        print(f"ok: {len(TABLE)} hulls match TABLE, the Shrike's nest matches NEST")


if __name__ == "__main__":
    main()
