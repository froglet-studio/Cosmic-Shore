#!/usr/bin/env python3
"""What PrismClearance takes out of a cell environment's BASELINE, for the generators that author
a cell's PhaseThresholds.

A cell's phase ladder climbs on Cell.LiveVolume, and every laid prism counts - the environment's
too - so each authored threshold is the environment's baseline (prism count and volume) plus the
mode's deltas. Since NO PRISM MAY INTERPENETRATE ANOTHER is held by PrismClearance at lay time
(Docs/ECOSYSTEM.md §49.12), an environment lays fewer and smaller prisms than its generator
authors, and a baseline priced on the authored lays would put every threshold that far above
where the cell really starts: a cell whose generator clipped heavily (the Orrery kept 40% of its
volume) would never reach Restless at all.

The correction is MEASURED, not modelled: `audit_prism_clipping.py --write-baselines` runs the
shipped generators and the shipped pass and records, per generator and intensity, what is authored
and what is laid (`prism_clearance_baselines.json`). A budget model keeps pricing what its
generator AUTHORS - the mirror it was built to be - and subtracts this:

    n, v = prism_clearance.rebase(n, v, "Assets/_Prefabs/Spawnables/SpawnableBoneyard1.prefab")

Readers (re-run each after --write-baselines changes the record): author_dogfight_assets.py
(boneyard_budget), author_cleave_assets.py (cleave_budget), author_wildlife_liberation_assets.py
(wildlife_cage_budget), author_regatta_assets.py, author_skein_assets.py,
author_breakwater_assets.py (breakwater_arena), and rebase_freestyle_thresholds.py for the
hand-measured cells.
"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASELINES = ROOT / "Tools/Build/prism_clearance_baselines.json"
_DATA = None


def _record(prefab, intensity=None):
    global _DATA
    if _DATA is None:
        _DATA = json.loads(BASELINES.read_text())["generators"]
    rows = [g for g in _DATA.values() if g["prefab"] == str(prefab)]
    assert len(rows) == 1, f"{prefab}: {len(rows)} generators recorded in {BASELINES.name}"
    by_i = rows[0]["intensities"]
    if intensity is None:
        values = list(by_i.values())
        assert all(v == values[0] for v in values), \
            f"{prefab}: the clearance differs by intensity - pass the intensity the cell builds at"
        return values[0]
    return by_i[str(intensity)]


def correction(prefab, intensity=None):
    """(prisms, volume) PrismClearance removes from what the generator authors."""
    r = _record(prefab, intensity)
    return r["authored_count"] - r["laid_count"], r["authored_volume"] - r["laid_volume"]


def rebase(count, volume, prefab, intensity=None):
    """A modelled AUTHORED baseline (count, volume) moved to what the game LAYS."""
    dn, dv = correction(prefab, intensity)
    return count - dn, volume - dv


if __name__ == "__main__":
    for key, g in sorted(json.loads(BASELINES.read_text())["generators"].items()):
        r = next(iter(g["intensities"].values()))
        if r["authored_count"] != r["laid_count"] or r["authored_volume"] != r["laid_volume"]:
            print(f"{g['prefab']:<70} -{r['authored_count'] - r['laid_count']:>6} prisms  "
                  f"-{r['authored_volume'] - r['laid_volume']:>14,.0f} volume")
