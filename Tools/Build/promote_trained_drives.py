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
    # Thresher, Thresher Time: lead-on B at 1,503 evaluations (job threshertime-thresher-time-lead, PR #1830). Needs the
    # Time-mode nose lead at 2.5: without it this genome DNFs 4-17 of 48 fresh courses.
    "ThresherAIBoostPolicy_ThresherTime": dict(
        base=L.POLICY_DIR + "/ThresherAIBoostPolicy.asset",
        fields={"plantMinTurnDegrees": 93.007, "plantMinDistance": 21.373, "slingIntervalSeconds": 0.586,
                "releaseConeDegrees": 7.346, "maxPlantSeconds": 1.738, "aimedHoldSeconds": 1.279,
                "aimToleranceDegrees": 9.485},
        source=R + "thresher-lead/lead_B_1503.json"),
    # Stoat, Slingshot: the parent's trained B (job slingshot-stoat-time, PR #1792), the lead-on round's pick (best with
    # the lead at 2.5, PR #1831: fresh composite 1.108 vs 2.749 shipped). trampolineAiTurnScale is not in the variant's
    # text yet (the variant was copied before the trampoline): it is added, the base asset has it.
    "StoatSlingConfig_Slingshot": dict(
        base=L.STOAT_DIR + "/StoatSlingConfig.asset",
        fields={"aiSlingMinTurnDegrees": 147.951, "aiSlingIntervalSeconds": 2.896, "aiSlingMinDistance": 13.329,
                "trampolineAiTurnScale": 96.362, "autopilotHold01": 0.012, "aiWarpMinDistance": 310.106},
        source=R + "stoat-slingshot/rows.json (trained B)"),
    # Sparrow, Breakwater: Sparrow run3 in the real game (time_ai_search.py with the nose genes, 2026-11-17), flown with
    # BreakwaterController's Sparrow row at lead 1.29 and nose 7.9/s. Confirm on 32 fresh flights a side: shipped timed
    # out on all 8 I4 races, trained finished every race (I4 82 s, I1 99, I2 85). Replaced D (PR #1793).
    "SparrowAIBoostPolicy_Breakwater": dict(
        base=L.POLICY_DIR + "/SparrowAIBoostPolicy.asset",
        fields={"alignDegrees": 63.004, "releaseAlignDegrees": 94.815, "stickBand": 0.978, "releaseStickBand": 0.804, "engageRunwaySeconds": 1.559, "releaseRunwaySeconds": 0.049, "minHoldSeconds": 1.996, "minRestSeconds": 0.243},
        source="/mnt/project-files/regatta-flywheel/train/Sparrow/run3/best.json"),
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
_DOLPHIN_C = {"alignDegrees": 76.312, "stickBand": 0.023, "engageRunwaySeconds": 0.658, "spendAtCharge01": 0.724,
              "peakMarginUnits": 9.768, "minDischargeSeconds": 0.047, "minPulseGainUnits": 41.811, "maxHoldOffSeconds": 0.65}
for _n in ("DolphinAIBoostPolicy_Switchback", "DolphinAIBoostPolicy_Switchback_I1", "DolphinAIBoostPolicy_Switchback_I2",
           "DolphinAIBoostPolicy_Switchback_I4"):
    ROWS[_n] = dict(base=L.POLICY_DIR + "/DolphinAIBoostPolicy.asset", start="DolphinAIBoostPolicy_Switchback_I1",
                    genome_fields=("releaseAlignDegrees", "releaseStickBand", "releaseRunwaySeconds"),
                    fields=_DOLPHIN_C, source=R + "dolphin-lead/switchback-dolphin-time-lead__C.json")

# Every value each row overwrote, as it shipped on bleeding-edge 8197bd92 (2026-11-07). --revert writes these back.
OLD = {
    # run3b, as it shipped before run4 (2026-11-17; #2088), refuel off
    "SerpentAIBoostPolicy_SerpentTime": {"alignDegrees": 68.568, "releaseAlignDegrees": 72.845, "stickBand": 0.372,
                                         "releaseStickBand": 0.497, "engageRunwaySeconds": 3.14, "releaseRunwaySeconds": 0.646,
                                         "maxConcurrentBurns": 3, "minSecondsBetweenPellets": 0.351, "reservePellets": 0,
                                         "refuelBelowPellets": -1, "refuelCoastSeconds": 0, "lowTankSecondsBetweenPellets": 0},
    "ThresherAIBoostPolicy_ThresherTime": {"plantMinTurnDegrees": 6.691, "plantMinDistance": 115.249,
                                           "slingIntervalSeconds": 2.866, "releaseConeDegrees": 15, "maxPlantSeconds": 3.156,
                                           "aimedHoldSeconds": 1.54, "aimToleranceDegrees": 3},
    "StoatSlingConfig_Slingshot": {"aiSlingMinTurnDegrees": 35, "aiSlingIntervalSeconds": 0.941,
                                   "aiSlingMinDistance": 123.282, "trampolineAiTurnScale": 100, "autopilotHold01": 1.0,
                                   "aiWarpMinDistance": 0},
    "SparrowAIBoostPolicy_Breakwater": {"alignDegrees": 45, "releaseAlignDegrees": 60, "stickBand": 1,
                                        "releaseStickBand": 1, "engageRunwaySeconds": 2, "releaseRunwaySeconds": 1.25, "minHoldSeconds": 0.6, "minRestSeconds": 0.4},
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
