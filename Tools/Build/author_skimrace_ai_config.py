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
    # designer adds. skimrace-v3-general (2026-10-07): retuned from skimrace-v2-general by run.sh tuneall
    # 1,2,3,4 4 16 sigma=0.15 final=20 only=stated (36 numbers re-fitted; 2 AI seats, 16/28/50 ms frames
    # +-50%, contacts on the 0.04 s fixed step - Docs/SKIM_RACE_AI.md section 14.5), every race scored against
    # its own track's ideal time so no track dominates (6.12). Fresh seeds, 20 per track, races to 3x ideal:
    # I1 20/20 finished, winner median 57.6 s; I2 20/20 finished, winner median 97.1 s; I3 20/20 finished,
    # winner median 175.0 s; I4 20/20 finished, winner median 148.8 s. The per-intensity files stay the faster
    # specialists on their own tracks.
    "SkimRaceAIConfig": {
        "PolicyVersion": "skimrace-v3-general",
        "LookaheadSeconds": 1.016,
        "LookaheadMin": 26.806,
        "LookaheadMax": 544.911,
        "SkimHeight": 4.346,
        "CrystalBumpHalfWidth": 181.107,
        "CrystalDirectDistance": 165.032,
        "LeadGain": 2.832,
        "MaxLeadDegrees": 19.738,
        "StickGainPerDegree": 0.041,
        "MinThrottle": 0.1,
        "ReachabilityMargin": 0.955,
        "PassMargin": 3.01,
        "CrossingHeightFraction": 0.292,
        "RibbonClearHeight": 5.159,
        "RibbonClearLateral": 28.981,
        "TerminalCentreBias": 0.004,
        "StallSeconds": 5.579,
        "RecoveryThrottle": 0.167,
        "MassGuardSeconds": 0.705,
        "MassGuardMargin": 1.298,
        "MassGuardSegment": 0.274,
        "LowBoostApproachScale": 0.797,
        "LowBoostFull": 2.383,
        "HullGuardSeconds": 0.0,
        "UsePlanner": False,
        "TrackGuardMargin": 1.642,
        "DirectBoost": 3.138,
        "DirectBoostHysteresis": 1.507,
        "DirectViaClearance": 2,
        "DirectViaLift": 22.338,
        "DirectViaLateral": 18.61,
        "TerminalChordClearance": 2.345,
        "ChordClearance": 0.766,
        "CrossingLeadSeconds": 0,
        "CrossingLookaheadScale": 0.761,
        "CrossingThrottle": 0.944,
        "CrossingSlowDistance": 121.55,
        "LaneHeightStep": 2.992,
    },
    # Intensity 1: retuned 2026-10-06 by Tools/Build/skimrace_retune.py for map ed6cd993 (tuneall 1 4 16
    # sigma=0.15 only=stated from skimrace-v4-i1, 23 numbers searched; 2 AI seats, 16/28/50 ms frames +-50%,
    # contacts on the 0.04 s fixed step). On 20 fresh races (seedbase 99000, cut at 129 + 60 s) it finished
    # 20/20, winner median 58.3 s; the general policy (skimrace-v2-general) on the same races: 20/20, 62.0 s.
    "SkimRaceAIConfig_I1": {
        "PolicyVersion": "skimrace-v5-i1",
        "TrackFingerprint": "ed6cd993",
        "LookaheadSeconds": 0.562,
        "LookaheadMin": 69.398,
        "LookaheadMax": 521.053,
        "SkimHeight": 5.811,
        "CrystalBumpHalfWidth": 411.096,
        "CrystalDirectDistance": 280.877,
        "LeadGain": 2.451,
        "MaxLeadDegrees": 32.221,
        "StickGainPerDegree": 0.138,
        "MinThrottle": 0.371,
        "ReachabilityMargin": 0.128,
        "PassMargin": 1.408,
        "CrossingHeightFraction": 0.143,
        "RibbonClearHeight": 4.723,
        "RibbonClearLateral": 28.848,
        "TerminalCentreBias": 0.017,
        "StallSeconds": 4.447,
        "RecoveryThrottle": 0.842,
        "MassGuardSeconds": 0.554,
        "MassGuardMargin": 0.8,
        "MassGuardSegment": 0.201,
        "LowBoostApproachScale": 0.905,
        "LowBoostFull": 2.729,
        "HullGuardSeconds": 0.0,
        "UsePlanner": False,
    },
    # Intensity 2: retuned 2026-10-06 by Tools/Build/skimrace_retune.py for map 19fadf77 (tuneall 2 4 16
    # sigma=0.15 only=stated set=winner from skimrace-v2-i2, 37 numbers searched; 2 AI seats, 16/28/50 ms
    # frames +-50%, contacts on the 0.04 s fixed step). On 20 fresh races (seedbase 99000, cut at 167 + 60 s)
    # it finished 20/20, winner median 70.8 s; the general policy (skimrace-v2-general) on the same races:
    # 20/20, 101.5 s.
    "SkimRaceAIConfig_I2": {
        "PolicyVersion": "skimrace-v3-i2",
        "TrackFingerprint": "19fadf77",
        "LookaheadSeconds": 0.773,
        "LookaheadMin": 74.272,
        "LookaheadMax": 349.475,
        "SkimHeight": 6.844,
        "CrystalBumpHalfWidth": 217.36,
        "LeadGain": 2.865,
        "MaxLeadDegrees": 29.266,
        "StickGainPerDegree": 0.08,
        "MinThrottle": 0.454,
        "ReachabilityMargin": 1.15,
        "PassMargin": 9.426,
        "CrossingHeightFraction": 0.333,
        "RibbonClearHeight": 6.606,
        "RibbonClearLateral": 36.561,
        "TerminalCentreBias": 0.051,
        "StallSeconds": 5.756,
        "RecoveryThrottle": 0.673,
        "MassGuardSeconds": 0.607,
        "MassGuardMargin": 0.867,
        "MassGuardSegment": 0.168,
        "LowBoostApproachScale": 0.77,
        "LowBoostFull": 1.583,
        "ChordClearance": 1.052,
        "CrossingLeadSeconds": 0.007,
        "CrossingLookaheadScale": 0.908,
        "CrossingThrottle": 0.867,
        "CrossingSlowDistance": 103.666,
        "LaneHeightStep": 1.165,
        "TrackMpcHorizon": 1.096,
        "TrackMpcLead": 31.606,
        "TrackMpcCaptureReward": 166.575,
        "TrackMpcNominalBias": 0.092,
        "TrackMpcStrikeCost": 364.604,
        "MpcHullMargin": 1.141,
        "HullGuardSeconds": 0.763,
        "HullMargin": 1.094,
        "PickupClearDistance": 2.251,
        "UseTrackMpc": True,
        "CrystalDirectDistance": 0,
        "UsePlanner": False,
    },
    # Intensity 4: retuned 2026-10-06 by Tools/Build/skimrace_retune.py for map 227b9055 (tuneall 4 4 16
    # sigma=0.15 only=stated from skimrace-v1-i4, 29 numbers searched; 2 AI seats, 16/28/50 ms frames +-50%,
    # contacts on the 0.04 s fixed step). On 20 fresh races (seedbase 99000, cut at 164 + 60 s) it finished
    # 20/20, winner median 142.3 s; the general policy (skimrace-v2-general) on the same races: 20/20, 159.8
    # s.
    "SkimRaceAIConfig_I4": {
        "PolicyVersion": "skimrace-v2-i4",
        "TrackFingerprint": "227b9055",
        "LookaheadSeconds": 0.676,
        "LookaheadMin": 34.798,
        "LookaheadMax": 390.173,
        "SkimHeight": 8.616,
        "CrystalBumpHalfWidth": 219.591,
        "CrystalDirectDistance": 164.689,
        "LeadGain": 2.411,
        "MaxLeadDegrees": 21.942,
        "StickGainPerDegree": 0.144,
        "MinThrottle": 0.129,
        "ReachabilityMargin": 1.354,
        "PassMargin": 10.744,
        "CrossingHeightFraction": 0.223,
        "RibbonClearHeight": 4.696,
        "RibbonClearLateral": 28.856,
        "TerminalCentreBias": 0.101,
        "StallSeconds": 4.703,
        "RecoveryThrottle": 0.658,
        "MassGuardSeconds": 0.947,
        "MassGuardMargin": 1.078,
        "MassGuardSegment": 0.169,
        "LowBoostApproachScale": 0.608,
        "LowBoostFull": 4.159,
        "TrackGuardMargin": 2.137,
        "DirectBoost": 2.668,
        "DirectBoostHysteresis": 0.387,
        "DirectViaClearance": 2.109,
        "DirectViaLift": 24.447,
        "DirectViaLateral": 22.432,
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
