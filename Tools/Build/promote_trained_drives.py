#!/usr/bin/env python3
"""promote_trained_drives.py - write the Time-arcade AIs the 2026-11-06/07 Regatta-plan searches trained into the
assets the game flies, through the learner's own writer (learn_drives_from_flights.copy_with / write_variant rules).

Plan step: time-arcade (Docs/AI_SYSTEM/ai_plan.json). Garrett, 2026-11-08: "stop gating things through yash he is busy.
Get things merged and ready to test." Every number below is a MODEL winner (the job named on each row, run by hand in
cloud threads while Compute is paused); none was raced in the Unity editor. The report, with every OLD value, is
Docs/Reports/TRAINED_DRIVES_PROMOTED.md, and OLD below keeps each overwritten value so a row can be put back with
`--revert <variant>`.

A searched genome is written as a mode variant (Resources/AIBoostPolicies/<asset>_<mode>[_I<n>]): the hull's own asset
and every other mode are untouched. A search that raced ONE drive at every intensity over a genome (Switchback) is
written into every intensity file of that mode, so the game flies at each intensity what the model raced.

The Squirrel's Skim Race policy is not a boost policy: it is written by author_skimrace_ai_config.py (its POLICIES table
holds the winner), and only checked here.

    python3 Tools/Build/promote_trained_drives.py            # write every row (idempotent)
    python3 Tools/Build/promote_trained_drives.py --check    # fail if an asset drifted from its promoted row
    python3 Tools/Build/promote_trained_drives.py --revert SparrowAIBoostPolicy_Breakwater   # put OLD back (then edit ROWS)
"""
import argparse
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import learn_drives_from_flights as L  # noqa: E402  (the writer: copy_with keeps every field it does not name)

R = "/mnt/project-files/regatta-training/results/"   # where each search's JSON was left (the project's shared folder)

# variant -> (base asset path, the genome file its untouched fields come from, {field: value}, source)
# "start": the asset whose text the variant starts from when it is not the variant itself (copy_with's fallback).
ROWS = {
    # Serpent, Serpentine: run4 in the real game (time_ai_search.py with the nose and refuel genes, races at Regatta
    # length, 2026-11-17), flown with SerpentTimeController's Serpent row at lead 2.09 and nose 10.42/s. It lays a wall
    # to refuel at 4 pellets or under (#2089, FINDINGS 23). Confirm on 40 fresh flights a side against run3b, no DNF:
    # loss 2.40 -> 2.25 and every condition faster (I1 121 -> 108 s, I4 104 -> 99, Regatta-length L2 160 -> 137, L4
    # 202 -> 176). Replaced run3b (#2088), which replaced C (job serpenttime-serpent-time, PR #1788).
    "SerpentAIBoostPolicy_SerpentTime": dict(
        base=L.POLICY_DIR + "/SerpentAIBoostPolicy.asset",
        fields={"alignDegrees": 84.525, "releaseAlignDegrees": 66.04, "stickBand": 0.584, "releaseStickBand": 0.845,
                "engageRunwaySeconds": 3.049, "releaseRunwaySeconds": 1.661, "maxConcurrentBurns": 3,
                "minSecondsBetweenPellets": 0.415, "reservePellets": 0,
                "refuelBelowPellets": 4, "refuelCoastSeconds": 0.291, "lowTankSecondsBetweenPellets": 0.175},
        source="/mnt/project-files/regatta-flywheel/train/Serpent/run4/best.json"),
    # Thresher, Thresher Time: run1 in the real game (time_ai_search.py with the nose genes, 2026-11-17), flown with
    # ThresherTimeController's Thresher row at lead 2.77 and nose 6.37/s. Confirm on 24 fresh flights a side (seeds
    # 101-104): shipped 2 DNF, trained none; I1 134 -> 94 s, I2 163 -> 115, I4 289 -> 185, I4@T5 373 -> 143, wide 188 -> 118
    # and 169 -> 115. Replaced lead-on B (job threshertime-thresher-time-lead, PR #1830).
    "ThresherAIBoostPolicy_ThresherTime": dict(
        base=L.POLICY_DIR + "/ThresherAIBoostPolicy.asset",
        fields={"alignDegrees": 21.684, "releaseAlignDegrees": 32.697, "stickBand": 0.828, "releaseStickBand": 0.641,
                "engageRunwaySeconds": 1.384, "releaseRunwaySeconds": 1.24, "plantMinTurnDegrees": 100.163,
                "plantMinDistance": 94.095, "slingIntervalSeconds": 0.301, "releaseConeDegrees": 9.92,
                "maxPlantSeconds": 2.134, "aimedHoldSeconds": 1.836, "aimToleranceDegrees": 10.628},
        source="/mnt/project-files/regatta-flywheel/train/Thresher/run1/best.json"),
    # Thresher, Regatta: the same run1 drive (Regatta flew the hull's own ThresherAIBoostPolicy). The real-game judge,
    # alone, the shipped course plus archive3six a9/a6/a0, seeds 11-12, card and L5 starts: 98.5 / 102 s -> 91.4 / 91.4 s,
    # all 16 finished, with the race line's trained nose pair (author_race_lines.py); the drive alone at 10/s gave 95.4.
    "ThresherAIBoostPolicy_Regatta": dict(
        base=L.POLICY_DIR + "/ThresherAIBoostPolicy.asset",
        fields={"alignDegrees": 21.684, "releaseAlignDegrees": 32.697, "stickBand": 0.828, "releaseStickBand": 0.641,
                "engageRunwaySeconds": 1.384, "releaseRunwaySeconds": 1.24, "plantMinTurnDegrees": 100.163,
                "plantMinDistance": 94.095, "slingIntervalSeconds": 0.301, "releaseConeDegrees": 9.92,
                "maxPlantSeconds": 2.134, "aimedHoldSeconds": 1.836, "aimToleranceDegrees": 10.628},
        source="/mnt/project-files/regatta-flywheel/train/Thresher/run1/best.json"),
    # Stoat, Slingshot: Stoat run1 in the real game (time_ai_search.py with the nose genes, 2026-11-17), flown with
    # SlingshotController's Stoat row at lead 2.62 and nose 10.43/s. Confirm on seeds 101-104, 32 flights a side, no DNF:
    # every rung 18-40% faster (I1 68 -> 51 s, I2 51 -> 36, I4 62 -> 42, I1@T10 39 -> 26, I4@T0 95 -> 56). It presses for
    # the warp on almost any leg (11.5 u) and holds for a 59 deg window. autopilotHold01 was not searched and keeps B's.
    # Replaced the parent's trained B (job slingshot-stoat-time, PR #1792 and #1831).
    "StoatSlingConfig_Slingshot": dict(
        base=L.STOAT_DIR + "/StoatSlingConfig.asset",
        fields={"aiSlingMinTurnDegrees": 154.471, "aiReleaseConeDegrees": 63.666, "aiCatchGiveUpSeconds": 5.181,
                "aiSlingIntervalSeconds": 1.594, "aiSlingMinDistance": 115.742, "aiWarpMinDistance": 11.523,
                "aiHoldForWindowDegrees": 58.721, "trampolineAiTurnScale": 67.159, "autopilotHold01": 0.012},
        source="/mnt/project-files/regatta-flywheel/train/Stoat/run1/best.json"),
    # Sparrow, Breakwater: Sparrow run4 in the real game (time_ai_search.py, 2026-11-17, seeded from run3 and the #2095
    # drive, on bleeding-edge with the #2112 afterburner and #2109's difficulty rates), flown at the Legendary nose 13.9/s.
    # Confirm on seeds 101-104, 32 flights a side, no DNF: I1 99 -> 98 s, I2 83 -> 76, I4 78 -> 73, I1@T10 95 -> 81,
    # I4@T0 98 -> 93. Replaced run3 (#2095), which replaced D (PR #1793).
    "SparrowAIBoostPolicy_Breakwater": dict(
        base=L.POLICY_DIR + "/SparrowAIBoostPolicy.asset",
        fields={"alignDegrees": 79.394, "releaseAlignDegrees": 89.527, "stickBand": 0.73, "releaseStickBand": 0.993, "engageRunwaySeconds": 0.192, "releaseRunwaySeconds": 1.674, "minHoldSeconds": 1.631, "minRestSeconds": 0.183},
        source="/mnt/project-files/regatta-flywheel/train/Sparrow/run4/best.json"),
    # Rhino, Headlong: Rhino run1 in the real game (time_ai_search.py with the nose genes, 2026-11-17), flown with
    # HeadlongController's Rhino row at lead 2.47 and nose 7.5/s. The shipped AI orbited a gate at the fleet nose lag
    # (FINDINGS row 21): confirm, 28 fresh flights a side, shipped finished 6, trained 28 (98-120 s). minSpeedGain and
    # throttleSlewPerSecond were not searched and keep lead-on B's (job drive-rhino-headlong-on-lead, PR #1828).
    "RhinoAIBoostPolicy_Headlong": dict(
        base=L.POLICY_DIR + "/RhinoAIBoostPolicy.asset",
        fields={"cornerSpeed": 274.153, "minCornerSpeed": 64.441, "brakeMarginUnits": 25.574,
                "brakeLeadSeconds": 0.943, "cornerReleaseDegrees": 0.877, "engageRunwaySeconds": 2.211,
                "releaseRunwaySeconds": 0.083, "alignDegrees": 56.368, "releaseAlignDegrees": 41.815,
                "stickBand": 0.398, "releaseStickBand": 0.372, "brakeSteerFloor": 0.694, "minSpeedGain": 17.571,
                "throttleSlewPerSecond": 7.366},
        source="/mnt/project-files/regatta-flywheel/train/Rhino/run1/best.json"),
}

# Dolphin, Switchback: lead-on C (job switchback-dolphin-time-lead, PR #1833), ONE drive raced at every intensity over
# the _I1 genome (switchback_fit: the genome's other fields are _I1's). Written into the mode file and each intensity's.
# Dolphin run2 in the real game (time_ai_search.py with the nose genes, 2026-11-17), flown with SwitchbackController's
# Dolphin row at lead 2.03 and Legendary nose 11.18/s (13.41 at I3-I4 and the gauntlet). Confirm on 32 fresh flights a
# side, no DNF: I1 110 -> 86 s, I1@T10 110 -> 97, wide courses 130 -> 119 and 117 -> 111, I2 119 -> 112, I4 even.
# Replaced C (job dolphin-lead, switchback-dolphin-time-lead__C).
# Dolphin step-5 run in the real game (2026-11-18), searched at the #2196 Drift Charge (charge rate x1.375 -> x3.4375),
# flown at lead 2.15, Legendary nose 10.57/s (12.69 at I3-I4 and the gauntlet), no-dodge 278 u. Confirm on seeds
# 101-103 against run2, no DNF: loss 2.34 -> 2.24; I1 106 -> 87 s, I3 141 -> 130, I4 240 -> 229, I2 126 -> 132.
# Replaced run2 {"alignDegrees": 76.312, "stickBand": 0.023, "engageRunwaySeconds": 0.658, "spendAtCharge01": 0.724,
# "peakMarginUnits": 9.768, "minDischargeSeconds": 0.047, "minPulseGainUnits": 41.811, "maxHoldOffSeconds": 0.65}.
_DOLPHIN_C = {"alignDegrees": 59.861, "stickBand": 0.024, "engageRunwaySeconds": 0.606, "spendAtCharge01": 0.438,
              "peakMarginUnits": 12.996, "minDischargeSeconds": 0.175, "minPulseGainUnits": 99.15, "maxHoldOffSeconds": 0.954}
for _n in ("DolphinAIBoostPolicy_Switchback", "DolphinAIBoostPolicy_Switchback_I1", "DolphinAIBoostPolicy_Switchback_I2",
           "DolphinAIBoostPolicy_Switchback_I4"):
    ROWS[_n] = dict(base=L.POLICY_DIR + "/DolphinAIBoostPolicy.asset", start="DolphinAIBoostPolicy_Switchback_I1",
                    genome_fields=("releaseAlignDegrees", "releaseStickBand", "releaseRunwaySeconds"),
                    fields=_DOLPHIN_C, source="/mnt/project-files/regatta-flywheel/step5/dolphin-ai/best.json")

# Every value each row overwrote, as it shipped on bleeding-edge 8197bd92 (2026-11-07). --revert writes these back.
OLD = {
    # run3b, as it shipped before run4 (2026-11-17; #2088), refuel off
    "SerpentAIBoostPolicy_SerpentTime": {"alignDegrees": 68.568, "releaseAlignDegrees": 72.845, "stickBand": 0.372,
                                         "releaseStickBand": 0.497, "engageRunwaySeconds": 3.14, "releaseRunwaySeconds": 0.646,
                                         "maxConcurrentBurns": 3, "minSecondsBetweenPellets": 0.351, "reservePellets": 0,
                                         "refuelBelowPellets": -1, "refuelCoastSeconds": 0, "lowTankSecondsBetweenPellets": 0},
    "ThresherAIBoostPolicy_ThresherTime": {"plantMinTurnDegrees": 6.691, "plantMinDistance": 115.249,
                                           "slingIntervalSeconds": 2.866, "releaseConeDegrees": 15, "maxPlantSeconds": 3.156,
                                           "aimedHoldSeconds": 1.54, "aimToleranceDegrees": 3,
                                           "alignDegrees": 12, "releaseAlignDegrees": 25, "stickBand": 0.35,
                                           "releaseStickBand": 0.6, "engageRunwaySeconds": 1.5, "releaseRunwaySeconds": 0.5},
    # trained B, as it shipped before Stoat run1 (2026-11-17; #1792, #1831)
    "StoatSlingConfig_Slingshot": {"aiSlingMinTurnDegrees": 147.951, "aiReleaseConeDegrees": 15.999,
                                   "aiCatchGiveUpSeconds": 4, "aiSlingIntervalSeconds": 2.896, "aiSlingMinDistance": 13.329,
                                   "aiWarpMinDistance": 310.106, "aiHoldForWindowDegrees": 120,
                                   "trampolineAiTurnScale": 96.362, "autopilotHold01": 0.012},
    # run3, as it shipped before run4 (2026-11-17; #2095)
    "SparrowAIBoostPolicy_Breakwater": {"alignDegrees": 63.004, "releaseAlignDegrees": 94.815, "stickBand": 0.978, "releaseStickBand": 0.804, "engageRunwaySeconds": 1.559, "releaseRunwaySeconds": 0.049, "minHoldSeconds": 1.996, "minRestSeconds": 0.243},
    # lead-on B, as it shipped before Rhino run1 (2026-11-17); before #1828 Headlong flew the hull's own policy
    "RhinoAIBoostPolicy_Headlong": {"cornerSpeed": 153.536, "minCornerSpeed": 144.359, "brakeMarginUnits": 0.705, "brakeLeadSeconds": 0.44, "cornerReleaseDegrees": 24.321, "engageRunwaySeconds": 1.439, "releaseRunwaySeconds": 0.161, "alignDegrees": 22.708, "releaseAlignDegrees": 26.95, "stickBand": 0.454, "releaseStickBand": 0.682, "brakeSteerFloor": 0.408},
    "DolphinAIBoostPolicy_Switchback": {"alignDegrees": 35.308, "stickBand": 0.262, "engageRunwaySeconds": 1.422,
                                        "spendAtCharge01": 0.835, "maxHoldOffSeconds": 4, "peakMarginUnits": 3,
                                        "minDischargeSeconds": 0.2, "minPulseGainUnits": 30},
    "DolphinAIBoostPolicy_Switchback_I1": {"alignDegrees": 34.14, "stickBand": 0.29, "engageRunwaySeconds": 0.26,
                                           "spendAtCharge01": 0.96, "maxHoldOffSeconds": 4, "peakMarginUnits": 3,
                                           "minDischargeSeconds": 0.2, "minPulseGainUnits": 30},
    "DolphinAIBoostPolicy_Switchback_I2": {"alignDegrees": 35.308, "stickBand": 0.262, "engageRunwaySeconds": 1.422,
                                           "spendAtCharge01": 0.835, "maxHoldOffSeconds": 4, "peakMarginUnits": 3,
                                           "minDischargeSeconds": 0.2, "minPulseGainUnits": 30},
    "DolphinAIBoostPolicy_Switchback_I4": {"alignDegrees": 51.947, "stickBand": 0.327, "engageRunwaySeconds": 1.105,
                                           "spendAtCharge01": 0.502, "maxHoldOffSeconds": 4, "peakMarginUnits": 3,
                                           "minDischargeSeconds": 0.2, "minPulseGainUnits": 30},
}

FIELD_RE = r"(?m)^  {k}: ([^\n]*)$"


def field_of(text, k):
    m = re.search(FIELD_RE.format(k=re.escape(k)), text)
    return m.group(1).strip() if m else None


def fmt(v):
    return str(int(v)) if float(v).is_integer() else repr(round(float(v), 3))


def wanted(name):
    """{field: value} the variant must carry: the row's fields, plus (Dolphin) the genome's untouched fields."""
    row = ROWS[name]
    want = {k: fmt(v) for k, v in row["fields"].items()}
    if row.get("genome_fields"):
        g = open(os.path.join(L.OUT_DIR, row["start"] + ".asset"), encoding="utf-8").read()
        for k in row["genome_fields"]:
            want.setdefault(k, field_of(g, k))
    return want


def text_for(name, want):
    path, text = L.copy_with(ROWS[name]["base"], name, want, fallback=ROWS[name].get("start"))
    for k, v in want.items():                  # a field the copied text lacks but the base asset has: add it
        if field_of(text, k) is None:
            base = open(ROWS[name]["base"], encoding="utf-8").read()
            if field_of(base, k) is None:
                raise SystemExit(f"{name}: {k} is in neither the variant nor its base asset")
            text = text.rstrip("\n") + f"\n  {k}: {v}\n"
            L.UNWRITTEN.discard((name, k))
    return path, text


def write(names):
    L.ensure_folder()
    # Dolphin: the genome file is read before it is overwritten, so every intensity gets the same genome fields
    wants = {n: wanted(n) for n in names}
    for n in names:
        path, text = text_for(n, wants[n])
        open(path, "w", encoding="utf-8").write(text)
        if not os.path.exists(path + ".meta"):
            open(path + ".meta", "w").write(L.native_meta(L.guid("asset/AIBoostPolicies/" + os.path.basename(path))))
        print(f"wrote {os.path.relpath(path, L.REPO)}")
    if L.UNWRITTEN:
        raise SystemExit(f"fields not written: {sorted(L.UNWRITTEN)}")


def check():
    bad = []
    for n, row in ROWS.items():
        path = os.path.join(L.OUT_DIR, n + ".asset")
        if not os.path.exists(path):
            bad.append(f"{n}: missing")
            continue
        text = open(path, encoding="utf-8").read()
        for k, v in row["fields"].items():
            have = field_of(text, k)
            if have is None or abs(float(have) - float(v)) > 1e-6:
                bad.append(f"{n}: {k} is {have}, promoted {v}")
    # the Dolphin's four files fly one drive: their genome fields agree too
    dol = [n for n in ROWS if n.startswith("DolphinAIBoostPolicy_Switchback")]
    for k in ROWS[dol[0]]["genome_fields"]:
        vals = {field_of(open(os.path.join(L.OUT_DIR, n + ".asset")).read(), k) for n in dol}
        if len(vals) != 1:
            bad.append(f"Dolphin Switchback files disagree on {k}: {sorted(vals)}")
    # the Squirrel's Skim Race general policy is author_skimrace_ai_config.py's; it must carry the promoted version
    sk = open(os.path.join(L.REPO, "Assets", "Resources", "SkimRaceAIConfig.asset"), encoding="utf-8").read()
    if field_of(sk, "PolicyVersion") != "skimrace-v4-time-arcade":
        bad.append("SkimRaceAIConfig: not the promoted skimrace-v4-time-arcade (run author_skimrace_ai_config.py)")
    if L.check():
        bad.append("learn_drives_from_flights.py --check failed")
    for b in bad:
        print("check:", b)
    print("check:", "OK" if not bad else "FAILED")
    return 1 if bad else 0


def revert(name):
    path = os.path.join(L.OUT_DIR, name + ".asset")
    if OLD.get(name) is None:
        for p in (path, path + ".meta"):
            if os.path.exists(p):
                os.remove(p)
        print(f"removed {name} (the mode flies the hull's own policy again)")
        return 0
    text = open(path, encoding="utf-8").read()
    for k, v in OLD[name].items():
        text = re.sub(FIELD_RE.format(k=re.escape(k)), f"  {k}: {v}", text, count=1)
    open(path, "w", encoding="utf-8").write(text)
    print(f"reverted {name}; remove its row from ROWS too, or --check will fail")
    return 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--revert", metavar="VARIANT")
    a = ap.parse_args()
    if a.check:
        return check()
    if a.revert:
        return revert(a.revert)
    write(list(ROWS))
    return 0


if __name__ == "__main__":
    sys.exit(main())
