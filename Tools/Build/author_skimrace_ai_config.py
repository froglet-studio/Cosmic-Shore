#!/usr/bin/env python3
"""Author the Skim Race AI policy assets (Assets/Resources/SkimRaceAIConfig*.asset).

The policy is the SkimRaceAIConfigSO field set. Values come from POLICIES below, which is where the
tuning loop's winners are recorded (see Docs/SKIM_RACE_AI.md for how each was found and validated).
`SkimRaceAIConfigSO.LoadFor(intensity)` reads `SkimRaceAIConfig_I<n>` first and falls back to the
base `SkimRaceAIConfig`.

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
OUT_DIR = os.path.join(ROOT, "Assets/Resources")

# Field defaults are read from the C# class so this file only states what differs.
BASE = {}

POLICIES = {
    # Base policy (intensities without their own tuning).
    "SkimRaceAIConfig": {
        "PolicyVersion": "skimrace-v1",
        "LookaheadSeconds": 1.091,
        "LookaheadMin": 50.064,
        "LookaheadMax": 338.445,
        "SkimHeight": 5.447,
        "CrystalBumpHalfWidth": 423.02,
        "CrystalDirectDistance": 230.067,
        "LeadGain": 1.796,
        "MaxLeadDegrees": 66.396,
        "StickGainPerDegree": 0.1,
        "MinThrottle": 0.338,
        "ReachabilityMargin": 0.582,
        "PassMargin": 0.733,
        "CrossingHeightFraction": 0.305,
        "SlabGuardSeconds": 0.0,
        "RibbonClearHeight": 3.427,
        "HullGuardSeconds": 0.0,
        "TerminalCentreBias": 0.0,
        "UsePlanner": False,
    },
    # Intensity 1 (flat octagon): v4, tuned (CEM, Tools/Build/skimrace_sim_harness) against the
    # model calibrated on in-editor races (Docs/SKIM_RACE_AI.md 6.1): hull BOX contact, the
    # Squirrel's own trail rails and pickup rings, the stacked multiplicative contact slow, 26 ms
    # frames with jitter; laid-mass guard on. Sim, 120 fresh seeds: 106/120 under 70 s, median
    # ~58 s. In-editor results: Docs/SKIM_RACE_AI.md section 8.
    "SkimRaceAIConfig_I1": {
        "PolicyVersion": "skimrace-v4-i1",
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
    # Intensity 4 (Relativity, 2026-10-08): v2, re-tuned on the FINAL course from the v1 policy
    # (CEM over the pursuit set, 2 AI seats, 6 seeds per candidate x 16 iterations, calibrated
    # physics ph.Dt=0.026 ph.DtJitter=0.5, every-seat scoring: an unfinished race is 300 + 10 per
    # missing crystal, plus half the worst time). Fresh-seed validation (seedbase 50000, 40 races,
    # limit 120 s, a race cut at 180 s) against v1-i4: 1 AI seat median 149.3 s -> 141.3 s (40/40
    # both); 2 AI seats "every seat finished" 5/40 -> see Docs/SKIM_RACE_AI.md 6.13 for the
    # 2-seat row and the first-finisher medians. 70 s stays out of reach on Relativity (78 s of
    # chords at top speed); the gain is robustness with two seats and ~8 s at one.
    "SkimRaceAIConfig_I4": {
        "PolicyVersion": "skimrace-v2-i4",
        "LookaheadSeconds": 0.732,
        "LookaheadMin": 20,
        "LookaheadMax": 458.225,
        "SkimHeight": 5.894,
        "CrystalBumpHalfWidth": 195.497,
        "CrystalDirectDistance": 85.345,
        "LeadGain": 2.494,
        "MaxLeadDegrees": 23.545,
        "StickGainPerDegree": 0.087,
        "MinThrottle": 0.126,
        "ReachabilityMargin": 1.343,
        "PassMargin": 10.122,
        "CrossingHeightFraction": 0.226,
        "RibbonClearHeight": 4.625,
        "RibbonClearLateral": 29.705,
        "TerminalCentreBias": 0.077,
        "StallSeconds": 3.766,
        "RecoveryThrottle": 0.565,
        "MassGuardSeconds": 0.916,
        "MassGuardMargin": 1.051,
        "MassGuardSegment": 0.228,
        "LowBoostApproachScale": 0.559,
        "LowBoostFull": 3.808,
        "TrackGuardMargin": 1.802,
        "DirectBoost": 2.705,
        "DirectBoostHysteresis": 0.49,
        "DirectViaClearance": 2.059,
        "DirectViaLift": 32.283,
        "DirectViaLateral": 23.098,
        "TerminalChordClearance": 0.581,
        "ChordClearance": 0,
        "CrossingLeadSeconds": 0.04,
        "CrossingLookaheadScale": 0.932,
        "CrossingThrottle": 1,
        "CrossingSlowDistance": 100.333,
        "LaneHeightStep": 2.437,
        "HullGuardSeconds": 0.0,
        "UsePlanner": False,
    },
}


def class_fields():
    src = open(SCRIPT, encoding="utf-8").read()
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


def script_guid():
    meta = open(SCRIPT + ".meta", encoding="utf-8").read()
    return re.search(r"guid: ([0-9a-f]{32})", meta).group(1)


def render(name, overrides):
    lines = [
        "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
        "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {script_guid()}, type: 3}}", f"  m_Name: {name}",
        "  m_EditorClassIdentifier: ",
    ]
    merged = dict(BASE)
    merged.update(overrides)
    for typ, field, default in class_fields():
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
    for name, ov in POLICIES.items():
        path = os.path.join(OUT_DIR, name + ".asset")
        text = render(name, ov)
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
