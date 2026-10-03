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
