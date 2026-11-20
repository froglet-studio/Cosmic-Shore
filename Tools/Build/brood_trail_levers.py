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

# hull: trailVolume.Value (shipped value x the loop's factor), from iterations 6-7 on the #2260 engine (2026-11-19).
TABLE = {
    "Orca": 0.8,          # shipped 1 x 0.8: back from the 0.4 cap when the 20 s bomb re-arm (#2263) cut its prism work (2026-11-20)
    "Sparrow": 0.61,      # shipped 1 x 0.61
    "Wrasse": 0.594,      # shipped 1 x 0.594
    "Scarab": 1.189,      # shipped 1 (default) x 1.189
    "Honeyguide": 1.419,  # shipped 1 x 1.419
    "Squirrel": 2.7675,   # shipped 1.35 x 2.05
    "Stoat": 1.9373,      # shipped 1.35 x 1.435
    "Meerkat": 2.214,     # shipped 1.35 x 1.64
    "Dolphin": 1.871,     # shipped 1 x 1.871
    "Urchin": 2.1,        # shipped 1 (default) x 2.1
    "TigerMoth": 0.4,     # shipped 1 x 0.4 (the cap)
    "Thresher": 1.1475,   # shipped 1.35 x 0.85, on top of its massTrailScale knee (#2258)
    "Shrike": 0.0675,     # shipped 1.35 x 0.05: "shrink shrike's trail so it is almost nothing" (Garrett, 2026-11-19);
                          # its mass is its nest (ShrikeRules.NestPieces), not a Brood Rush knee
}

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
    return drift


def main():
    check = "--check" in sys.argv
    drift = apply(write=not check)
    for d in drift:
        print(("drift: " if check else "wrote: ") + d)
    if check and drift:
        sys.exit(1)
    if not drift:
        print(f"ok: {len(TABLE)} hulls match TABLE")


if __name__ == "__main__":
    main()
