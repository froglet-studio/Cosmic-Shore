#!/usr/bin/env python3
"""Rebase the HAND-MEASURED cells' PhaseThresholds onto what their environment actually LAYS.

The freestyle seven and Hesperides author their phase ladder by hand, as the environment's
baseline (FrogletTools > Ecology > Measure Cell Environment Baselines) plus the Blob deltas
(Docs/ECOSYSTEM.md §18). PrismClearance (Docs/ECOSYSTEM.md §62) now shrinks or clears every prism
that interpenetrates another before it is laid, so each of these environments lays less than the
baseline its thresholds were measured on - the Orrery 40% of its volume - and a ladder left where
it was would put Restless out of the cell's reach.

The thresholds below are the ones these cells shipped with BEFORE the pass (bleeding-edge at
c2e75379, 2026-10-20), and each is moved down by what PrismClearance takes out of that cell's
environment (prism_clearance.py, measured). Holding the pre-pass numbers here makes the rebase
idempotent: re-running after `audit_prism_clipping.py --write-baselines` re-derives every number
from the same starting point instead of subtracting twice.

    python3 Tools/Build/rebase_freestyle_thresholds.py           # write the eight configs
    python3 Tools/Build/rebase_freestyle_thresholds.py --check   # fail if any config disagrees

When one of these cells is re-measured and re-authored by hand, update its row here to the new
PRE-clearance numbers (the measurer now reports what is laid, so add the correction back), or
retire the row - a hand edit the next run would overwrite is not an edit.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Build"))
import prism_clearance  # noqa: E402

KEYS = ("RestlessEnter", "RestlessExit", "FrenzyEnter", "FrenzyExit",
        "RestlessEnterVolume", "RestlessExitVolume", "FrenzyEnterVolume", "FrenzyExitVolume")

# cell: (counts x4, volumes x4) as shipped before PrismClearance.
PRE_CLEARANCE = {
    "Orrery": (35272, 35072, 38172, 37572, 206986, 203786, 253386, 243786),
    "Daedala": (34558, 34358, 37458, 36858, 648504, 645304, 694904, 685304),
    "Yggdra": (35040, 34840, 37940, 37340, 552356, 549156, 598756, 589156),
    "Caldera": (42053, 41853, 44953, 44353, 1221953, 1218753, 1268353, 1258753),
    "Geode": (35067, 34867, 37967, 37367, 574650, 571450, 621050, 611450),
    "Hesperides": (16300, 15900, 33000, 32200, 602000, 592000, 985000, 960000),
    "Ourobor": (38589, 38389, 41489, 40889, 762649, 759449, 809049, 799449),
    "Zephyr": (36769, 36569, 39669, 39069, 437285, 434085, 483685, 474085),
}


def target(cell):
    dn, dv = prism_clearance.correction(f"Assets/_Prefabs/Spawnables/Spawnable{cell}.prefab")
    pre = PRE_CLEARANCE[cell]
    return [pre[k] - dn for k in range(4)] + [round(pre[k] - dv) for k in range(4, 8)]


def main():
    check = "--check" in sys.argv
    bad = []
    for cell in PRE_CLEARANCE:
        path = ROOT / f"Assets/_SO_Assets/Cell Configs/{cell} Cell/{cell} Cell Config.asset"
        text = path.read_text()
        out = text
        for key, value in zip(KEYS, target(cell)):
            m = re.search(rf"^(    {key}: )([\d.e+-]+)$", out, re.M)
            assert m, f"{path}: no {key}"
            out = out[:m.start(2)] + str(value) + out[m.end(2):]
        if out != text:
            if check:
                bad.append(cell)
            else:
                path.write_text(out)
        print(f"  {cell:<11} Restless {target(cell)[4]:>10,}  Frenzy {target(cell)[6]:>10,}  "
              f"(count {target(cell)[0]:,})")
    if bad:
        print(f"FAIL: {', '.join(bad)} disagree with the rebase - run without --check")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
