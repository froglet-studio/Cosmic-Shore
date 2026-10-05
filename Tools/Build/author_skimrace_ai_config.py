#!/usr/bin/env python3
"""Author the Skim Race AI policy assets (Assets/Resources/SkimRaceAIConfig*.asset) and the lobby
AI difficulty settings (Assets/Resources/SkimRaceDifficulty.asset).

The policy is the SkimRaceAIConfigSO field set. Values come from POLICIES below, which is where the
tuning loop's winners are recorded (see Docs/SKIM_RACE_AI.md for how each was found and validated).
`SkimRaceAIConfigSO.LoadFor(intensity)` reads `SkimRaceAIConfig_I<n>` first and falls back to the
base `SkimRaceAIConfig`.

DIFFICULTY holds each lobby difficulty's deliberate mistakes (SkimRaceDifficultySO; section 10 of
the doc). --check also holds that class's field DEFAULTS equal to the table, so a build missing the
asset flies the same numbers.

Every per-intensity policy records the map it was tuned on - "TrackFingerprint", from
Tools/Build/skimrace_track_fingerprint.py - and the general policy records none (it is for any map).
--check holds that rule; skimrace_track_fingerprint.py --check says whether each map is still the one
its policy was tuned on, and Tools/Build/skimrace_retune.py <I> retunes one that is not (section 11).

    python3 Tools/Build/author_skimrace_ai_config.py          # write the assets
    python3 Tools/Build/author_skimrace_ai_config.py --check  # fail if an asset drifted from this file
"""
import os
import re
import sys
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
assert os.path.isdir(os.path.join(ROOT, "Assets")), "ROOT must contain Assets/"
SCRIPT = os.path.join(ROOT, "Assets/_Scripts/Controller/AI/SkimRace/SkimRaceAIConfigSO.cs")
DIFFICULTY_SCRIPT = os.path.join(ROOT, "Assets/_Scripts/Controller/AI/SkimRace/SkimRaceDifficultySO.cs")
OUT_DIR = os.path.join(ROOT, "Assets/Resources")

# Field defaults are read from the C# class so this file only states what differs.
BASE = {}

# TrackFingerprint (2026-10-05): the I1, I2 and I4 policies were tuned 2026-10-02..04 on the scene's
# race data as it still is today - the fingerprints are identical at every revision of
# MinigameSkimRace.unity back to 2026-09-12 - so each records today's fingerprint of its intensity.
# skimrace_retune.py writes a new one with every retune.

POLICIES = {
    # Base policy - flown by every intensity WITHOUT its own file: intensity 3 today, and any intensity a
    # designer adds. v2-general (2026-10-05): ONE policy tuned on all four tracks at once
    # (run.sh tuneall 1,2,3,4 4 16 sigma=0.15 final=20, started from the I1 policy; 2 AI seats, 28 ms
    # frames +-50%), every race scored against its own track's ideal time so no track dominates
    # (Docs/SKIM_RACE_AI.md 6.12). Fresh seeds, 20 per track, races to 3x ideal: finished 20/20 on EVERY
    # track; winner median I1 62.0 s, I2 105.7 s, I3 176.7 s, I4 157.1 s. The per-intensity files stay the
    # faster specialists on their own tracks. Replaces skimrace-v1: on the same 20 I3 seeds and limit, v1
    # finished 5/20 races (winner median 244.8 s) against v2's 20/20 (176.7 s).
    "SkimRaceAIConfig": {
        "PolicyVersion": "skimrace-v2-general",
        "LookaheadSeconds": 0.793,
        "LookaheadMin": 35.596,
        "LookaheadMax": 422.444,
        "SkimHeight": 5.284,
        "CrystalBumpHalfWidth": 353.842,
        "CrystalDirectDistance": 239.53,
        "LeadGain": 2.792,
        "MaxLeadDegrees": 18.94,
        "StickGainPerDegree": 0.054,
        "MinThrottle": 0.127,
        "ReachabilityMargin": 0.917,
        "PassMargin": 1.172,
        "CrossingHeightFraction": 0.332,
        "RibbonClearHeight": 4.5,
        "RibbonClearLateral": 29.411,
        "TerminalCentreBias": 0.0,
        "StallSeconds": 4.608,
        "RecoveryThrottle": 0.171,
        "MassGuardSeconds": 0.681,
        "MassGuardMargin": 1.046,
        "MassGuardSegment": 0.257,
        "LowBoostApproachScale": 0.907,
        "LowBoostFull": 2.433,
        "HullGuardSeconds": 0.0,
        "UsePlanner": False,
        "TrackGuardMargin": 1.553,
        "DirectBoost": 2.848,
        "DirectBoostHysteresis": 1.055,
        "DirectViaClearance": 2.392,
        "DirectViaLift": 16.589,
        "DirectViaLateral": 21.528,
        "TerminalChordClearance": 2.183,
        "ChordClearance": 0.469,
        "CrossingLeadSeconds": 0.093,
        "CrossingLookaheadScale": 1.0,
        "CrossingThrottle": 0.98,
        "CrossingSlowDistance": 145.75,
        "LaneHeightStep": 3.0,
    },
    # Intensity 1 (flat octagon): v4, tuned (CEM, Tools/Build/skimrace_sim_harness) against the
    # model calibrated on in-editor races (Docs/SKIM_RACE_AI.md 6.1): hull BOX contact, the
    # Squirrel's own trail rails and pickup rings, the stacked multiplicative contact slow, 26 ms
    # frames with jitter; laid-mass guard on. Sim, 120 fresh seeds: 106/120 under 70 s, median
    # ~58 s. In-editor results: Docs/SKIM_RACE_AI.md section 8.
    "SkimRaceAIConfig_I1": {
        "PolicyVersion": "skimrace-v4-i1",
        "TrackFingerprint": "ed6cd993",
        "LookaheadSeconds": 0.637,
        "LookaheadMin": 65.451,
        "LookaheadMax": 406.453,
        "SkimHeight": 6.022,
        "CrystalBumpHalfWidth": 443.319,
        "CrystalDirectDistance": 310.714,
        "LeadGain": 2.447,
        "MaxLeadDegrees": 39.649,
        "StickGainPerDegree": 0.117,
        "MinThrottle": 0.266,
        "ReachabilityMargin": 0,
        "PassMargin": 0.413,
        "CrossingHeightFraction": 0.194,
        "RibbonClearHeight": 4.961,
        "RibbonClearLateral": 27.544,
        "TerminalCentreBias": 0.007,
        "StallSeconds": 4.341,
        "RecoveryThrottle": 0.455,
        "MassGuardSeconds": 0.524,
        "MassGuardMargin": 0.878,
        "MassGuardSegment": 0.25,
        "LowBoostApproachScale": 0.937,
        "LowBoostFull": 2.685,
        "HullGuardSeconds": 0.0,
        "UsePlanner": False,
    },
    # Intensity 2 (tilted spline loop): v2, judged against the RE-BASELINED 80 s limit (product decision,
    # Docs/SKIM_RACE_AI.md 6.11). Winner-scored CEM (score=winner limit=80, 2 AI seats) over the best
    # hand config (lane step ~1, tracking MPC with a strike term, no terminal chord, hull guard on).
    # Sim validation, 40 fresh seeds (seedbase 50000), 28 ms frames: 2 AI winner median 76.1 s, 30/40
    # <= 80 s; 3 AI winner median 75.7 s, 31/40 <= 80 s - just short of the pre-set bar (median <= 76,
    # >= 80%). Tuned values the code does not read under these switches (Level*, CaptureMargin,
    # TerminalChordClearance, TrackGuardMargin) are deliberately left at their defaults.
    "SkimRaceAIConfig_I2": {
        "PolicyVersion": "skimrace-v2-i2",
        "TrackFingerprint": "19fadf77",
        "LookaheadSeconds": 0.672,
        "LookaheadMin": 79.499,
        "LookaheadMax": 396.132,
        "SkimHeight": 6.705,
        "CrystalBumpHalfWidth": 245.352,
        "LeadGain": 2.625,
        "MaxLeadDegrees": 27.759,
        "StickGainPerDegree": 0.08,
        "MinThrottle": 0.484,
        "ReachabilityMargin": 1.107,
        "PassMargin": 9.881,
        "CrossingHeightFraction": 0.165,
        "RibbonClearHeight": 6.137,
        "RibbonClearLateral": 33.559,
        "TerminalCentreBias": 0.048,
        "StallSeconds": 5.823,
        "RecoveryThrottle": 0.582,
        "MassGuardSeconds": 0.618,
        "MassGuardMargin": 1.135,
        "MassGuardSegment": 0.203,
        "LowBoostApproachScale": 0.725,
        "LowBoostFull": 1.772,
        "ChordClearance": 0.99,
        "CrossingLeadSeconds": 0.065,
        "CrossingLookaheadScale": 0.969,
        "CrossingThrottle": 0.848,
        "CrossingSlowDistance": 92.018,
        "LaneHeightStep": 1.172,
        "TrackMpcHorizon": 1.105,
        "TrackMpcLead": 20.615,
        "TrackMpcCaptureReward": 183.74,
        "TrackMpcNominalBias": 0.161,
        "TrackMpcStrikeCost": 934.534,
        "MpcHullMargin": 0.887,
        "HullGuardSeconds": 0.602,
        "HullMargin": 1.215,
        "PickupClearDistance": 1.569,
        "UseTrackMpc": True,
        "CrystalDirectDistance": 0,
        "UsePlanner": False,
    },
    # Intensity 4 (3D polyline, crystals ON the ribbon): tuned from the I2 policy (two CEM rounds,
    # calibrated sim, second round uncapped). Makes the 54-crystal sequence COMPLETABLE (sim 40/40,
    # median ~152 s) - it does NOT meet the 70 s benchmark. Docs/SKIM_RACE_AI.md 6.2.
    "SkimRaceAIConfig_I4": {
        "PolicyVersion": "skimrace-v1-i4",
        "TrackFingerprint": "227b9055",
        "LookaheadSeconds": 0.562,
        "LookaheadMin": 38.148,
        "LookaheadMax": 435.887,
        "SkimHeight": 8.064,
        "CrystalBumpHalfWidth": 204.001,
        "CrystalDirectDistance": 98.858,
        "LeadGain": 2.474,
        "MaxLeadDegrees": 25.604,
        "StickGainPerDegree": 0.133,
        "MinThrottle": 0.155,
        "ReachabilityMargin": 1.232,
        "PassMargin": 9.2,
        "CrossingHeightFraction": 0.153,
        "RibbonClearHeight": 4.737,
        "RibbonClearLateral": 29.748,
        "TerminalCentreBias": 0.1,
        "StallSeconds": 4.308,
        "RecoveryThrottle": 0.641,
        "MassGuardSeconds": 0.915,
        "MassGuardMargin": 0.857,
        "MassGuardSegment": 0.221,
        "LowBoostApproachScale": 0.637,
        "LowBoostFull": 3.758,
        "TrackGuardMargin": 1.54,
        "DirectBoost": 2.554,
        "DirectBoostHysteresis": 0.427,
        "DirectViaClearance": 2,
        "DirectViaLift": 22.619,
        "DirectViaLateral": 23.841,
        "HullGuardSeconds": 0.0,
        "UsePlanner": False,
    },
}


# The lobby AI difficulty's deliberate mistakes (SkimRaceHandicap), ONE setting per difficulty for
# every intensity: tuned once on intensity 2 with `run.sh handicap` (Docs/SKIM_RACE_AI.md section 10).
# Hard is the unhandicapped pilot and has no row.
DIFFICULTY = {
    "SkimRaceDifficulty": {
        # Easy: TUNED (2026-10-05) - run.sh handicap 2 40 120 ph.HcReaction=0.5 hi=0.25, 2 AI seats.
        # Seat median on four seed sets: 120.7 s (search seeds), 114.9 s, 123.6 s, 120.0 s (fresh); pooled
        # over the last two, 120.8 s (p10 96.1, p90 157.9), 160/160 finished, ~2.9 misjudged/race.
        "EasyReactionSeconds": 0.5,
        "EasyMistakeChance": 0.099,
        # Medium: TUNED (2026-10-05) - run.sh handicap 2 40 95 ph.HcReaction=0.25, 2 AI seats:
        # fresh seeds seat median 95.9 s (p10 81.0, p90 124.4), 80/80 finished, 1.3 misjudged/race.
        "MediumReactionSeconds": 0.25,
        "MediumMistakeChance": 0.045,
    },
}


def class_fields(script=SCRIPT):
    src = open(script, encoding="utf-8").read()
    fields = []
    for m in re.finditer(r"public (float|bool|string|int) (\w+) = ([^;]+);", src):
        typ, name, val = m.groups()
        fields.append((typ, name, val.strip()))
    return fields


def fmt(typ, val):
    if typ == "bool":
        return "1" if str(val).lower() in ("true", "1") else "0"
    if typ == "string":
        return str(val).strip('"')
    v = str(val).rstrip("f")
    f = float(v)
    return ("%d" % f) if f == int(f) and abs(f) < 1e9 else repr(f)


def script_guid(script=SCRIPT):
    meta = open(script + ".meta", encoding="utf-8").read()
    return re.search(r"guid: ([0-9a-f]{32})", meta).group(1)


def render(name, overrides, script=SCRIPT):
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {script_guid(script)}, type: 3}}", f"  m_Name: {name}",
        "  m_EditorClassIdentifier: ",
    ]
    merged = dict(BASE)
    merged.update(overrides)
    for typ, field, default in class_fields(script):
        val = merged.get(field, default)
        lines.append(f"  {field}: {fmt(typ, val)}")
    return "\n".join(lines) + "\n"


def main(argv):
    check = "--check" in argv
    bad = 0
    # A key that is not a field would silently write nothing (render only emits class fields) and
    # still pass --check, so a winning tuning value could fail to reach the asset unnoticed.
    known = {name for _, name, _ in class_fields()}
    for name, ov in POLICIES.items():
        unknown = sorted(set(ov) - known)
        if unknown:
            print(f"ERROR: {name}: not SkimRaceAIConfigSO fields: {unknown}")
            return 1
        # A per-intensity policy is only good on the map it was tuned on, so it must say which one; the
        # general policy is for every map and must not (the game would stop flying it on the others).
        fp = ov.get("TrackFingerprint", "")
        if re.fullmatch(r"SkimRaceAIConfig_I\d+", name) and not re.fullmatch(r"[0-9a-f]{8}", fp):
            print(f"ERROR: {name}: TrackFingerprint must be the 8-hex-digit map fingerprint it was tuned on "
                  f"(python3 Tools/Build/skimrace_track_fingerprint.py), has {fp!r}")
            return 1
        if name == "SkimRaceAIConfig" and fp:
            print("ERROR: SkimRaceAIConfig is the general policy for every map - it must not record a TrackFingerprint")
            return 1
    difficulty_fields = {name: default for _, name, default in class_fields(DIFFICULTY_SCRIPT)}
    for name, ov in DIFFICULTY.items():
        if set(ov) != set(difficulty_fields):
            print(f"ERROR: {name}: DIFFICULTY must state every SkimRaceDifficultySO field "
                  f"{sorted(difficulty_fields)}, has {sorted(ov)}")
            return 1
        # The class defaults ARE the fallback when the asset is missing - they must fly the same numbers.
        drift = [f for f, v in ov.items() if abs(float(difficulty_fields[f].rstrip("f")) - float(v)) > 1e-6]
        if drift:
            print(f"ERROR: SkimRaceDifficultySO field defaults differ from DIFFICULTY for {drift} - "
                  "update the initializers in SkimRaceDifficultySO.cs to match")
            return 1
    jobs = [(name, ov, SCRIPT) for name, ov in POLICIES.items()] + \
           [(name, ov, DIFFICULTY_SCRIPT) for name, ov in DIFFICULTY.items()]
    for name, ov, script in jobs:
        path = os.path.join(OUT_DIR, name + ".asset")
        text = render(name, ov, script)
        if check:
            if not os.path.exists(path) or open(path, encoding="utf-8").read() != text:
                print(f"DRIFT: {os.path.relpath(path, ROOT)}")
                bad += 1
            continue
        open(path, "w", encoding="utf-8").write(text)
        meta = path + ".meta"
        if not os.path.exists(meta):
            open(meta, "w", encoding="utf-8").write(
                "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                % uuid.uuid4().hex)
        print("wrote", os.path.relpath(path, ROOT))
    if check:
        print("--check OK" if not bad else f"--check FAILED: {bad} asset(s) drifted")
        return 1 if bad else 0
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
