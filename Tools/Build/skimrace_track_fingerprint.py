#!/usr/bin/env python3
"""
The Skim Race track FINGERPRINT per intensity - the map a tuned AI policy was made for.

    python3 Tools/Build/skimrace_track_fingerprint.py              # print every intensity's fingerprint
    python3 Tools/Build/skimrace_track_fingerprint.py --check      # "retune needed" when a tuned policy's
                                                                   # map changed (exit 1), info otherwise
    python3 Tools/Build/skimrace_track_fingerprint.py --self-test  # the golden vector the C# test shares
    python3 Tools/Build/skimrace_track_fingerprint.py --emit-track <file>   # the simulator's track file
    (--scene <file.unity> reads another copy of the scene - how the --check's own tests edit a map)

WHAT IS IN IT (the user's definition, 2026-10-05: "the track's path points, its curve setting, the number
of laps, or where the crystals sit"), read from MinigameSkimRace.unity exactly the way the game reads it:

  * SpawnableWaypointTrack.waypoints[I-1]             - the path points
  * SpawnableWaypointTrack.useSplinePerIntensity[I-1] - the curve setting (missing = 0, straight)
  * CrystalCollisionTurnMonitor laps for I            - lapsPerIntensity[I-1] when > 0, else
                                                        max(1, optionalLaps) (its ResolveLaps)
  * CrystalManager.listOfCrystalPositions[clamp(I)]   - where the crystals sit (the manager clamps the
                                                        intensity into the list, so do we)

Colours, prism looks, spawn jitter and everything else in the scene are deliberately NOT in it: a change
that does not move the race does not need a retune.

HOW IT IS COMPUTED - shared, digit for digit, with SkimRaceTrackFingerprint.Compute in C#: each coordinate
is read as the game reads it (a float32), rounded to the nearest whole unit (banker's rounding, as
Math.Round does), and the int32 sequence

    [version, #points, x, y, z ..., spline, laps, #crystals, x, y, z ...]

is hashed with 32-bit FNV-1a over its little-endian bytes and written as 8 hex digits. Whole units on
purpose: a waypoint nudged by a fraction of a unit is not a different map. The C# test
(SkimRaceTrackFingerprintTests) and --self-test below assert the SAME golden value for the same input,
which is what proves the two implementations agree.

WHERE IT IS USED

  * Each tuned policy in Tools/Build/author_skimrace_ai_config.py stores the fingerprint of the map it
    was tuned on (TrackFingerprint). The general policy stores none - it is for any track.
  * In the game, SkimRaceAIDeployment compares the policy's fingerprint with the live scene's; on a
    mismatch it flies the general policy and warns once in the console.
  * --check here says the same thing before anyone presses Play; Tools/Build/skimrace_retune.py <I>
    retunes that intensity and stamps the new fingerprint.
"""
import argparse
import os
import re
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCENE = os.path.join(ROOT, "Assets", "_Scenes", "Multiplayer Scenes", "MinigameSkimRace.unity")
VERSION = 1


# ---- reading the scene -------------------------------------------------------------------------------

def _block_with(docs, key):
    hits = [d for d in docs if ("\n  " + key) in d]
    if len(hits) != 1:
        raise SystemExit(f"skimrace_track_fingerprint: expected one block with '{key}', found {len(hits)}")
    return hits[0]


def _int_list(block, key):
    """A serialized List<int>: Unity writes it as hex, 4 little-endian bytes per entry ('[]' when empty)."""
    m = re.search(r"^  %s: ([0-9a-f]*)$" % re.escape(key), block, re.M)
    if not m:
        if re.search(r"^  %s: \[\]$" % re.escape(key), block, re.M):
            return []
        raise SystemExit(f"skimrace_track_fingerprint: no {key}")
    h = m.group(1)
    return [int.from_bytes(bytes.fromhex(h[i:i + 8]), "little", signed=True) for i in range(0, len(h), 8)]


def _position_sets(block, start_key, stop_key):
    body = block.split("  " + start_key + ":", 1)[1].split("  " + stop_key, 1)[0]
    return [re.findall(r"x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)", s) for s in body.split("- positions:")[1:]]


def read_scene(path=SCENE):
    """Everything the fingerprint (and the simulator) reads, as the scene stores it (numbers as text)."""
    text = open(path, encoding="utf-8").read()
    docs = re.split(r"\n--- ", text)
    track = _block_with(docs, "useSplinePerIntensity:")
    crystals = _block_with(docs, "listOfCrystalPositions:")
    monitor = _block_with(docs, "lapsPerIntensity:")
    optional = re.search(r"^  optionalLaps: (-?\d+)$", monitor, re.M)
    return {
        "waypoints": _position_sets(track, "waypoints", "useSplinePerIntensity"),
        "spline": _int_list(track, "useSplinePerIntensity"),
        "laps": _int_list(monitor, "lapsPerIntensity"),
        "optional_laps": int(optional.group(1)) if optional else 4,
        "anchors": _position_sets(crystals, "listOfCrystalPositions", "anchorJitterRadius"),
    }


def intensities(data):
    """Every intensity the track has a path for (1-based) - a fifth set of waypoints is a fifth intensity."""
    return [i + 1 for i, s in enumerate(data["waypoints"]) if len(s) >= 2]


def resolved(data, intensity):
    """The four facts for one intensity, resolved exactly the way the game resolves them."""
    i = intensity - 1
    spline = 1 if 0 <= i < len(data["spline"]) and data["spline"][i] != 0 else 0
    laps = data["laps"][i] if 0 <= i < len(data["laps"]) and data["laps"][i] > 0 else max(1, data["optional_laps"])
    sets = data["anchors"]
    anchors = sets[min(max(intensity, 1), len(sets)) - 1] if sets else []
    return data["waypoints"][i], spline, laps, anchors


# ---- the fingerprint ---------------------------------------------------------------------------------

def _f32(text):
    return struct.unpack("<f", struct.pack("<f", float(text)))[0]


def _round(v):
    return int(round(v))   # Python rounds half to even, exactly like C#'s Math.Round


def fingerprint_of(points, spline, laps, anchors):
    seq = [VERSION, len(points)]
    for p in points:
        seq += [_round(_f32(c)) for c in p]
    seq += [1 if spline else 0, int(laps), len(anchors)]
    for p in anchors:
        seq += [_round(_f32(c)) for c in p]
    h = 0x811C9DC5
    for v in seq:
        for b in struct.pack("<i", v):
            h ^= b
            h = (h * 0x01000193) & 0xFFFFFFFF
    return f"{h:08x}"


def fingerprints(path=SCENE):
    data = read_scene(path)
    return {i: fingerprint_of(*resolved(data, i)) for i in intensities(data)}


# ---- the simulator's track file (run.sh) -------------------------------------------------------------

def emit_track(out_path, path=SCENE):
    data = read_scene(path)
    with open(out_path, "w") as fh:
        for i in intensities(data):
            points, spline, laps, anchors = resolved(data, i)
            fmt = lambda pts: ";".join(",".join(p) for p in pts)
            fh.write(f"{i}|{spline}|{laps}|{fmt(points)}|{fmt(anchors)}\n")


# ---- the check ---------------------------------------------------------------------------------------

def _policies():
    import importlib.util
    spec = importlib.util.spec_from_file_location(
        "author_skimrace_ai_config", os.path.join(os.path.dirname(os.path.abspath(__file__)), "author_skimrace_ai_config.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod.POLICIES


def check(path=SCENE):
    live = fingerprints(path)
    policies = _policies()
    stale = 0
    for i, fp in sorted(live.items()):
        name = f"SkimRaceAIConfig_I{i}"
        pol = policies.get(name)
        if pol is None:
            print(f"I{i}: no tuned file - flies the general policy (fine; to tune one: "
                  f"python3 Tools/Build/skimrace_retune.py {i})")
            continue
        tuned = pol.get("TrackFingerprint", "")
        if not tuned:
            print(f"I{i}: {name} records no map fingerprint - cannot tell whether its map changed "
                  f"(this map is {fp}; retuning stamps one)")
            stale += 1
        elif tuned != fp:
            print(f"I{i}: RETUNE NEEDED - {name} ({pol.get('PolicyVersion', '?')}) was tuned on map {tuned}, "
                  f"the scene is now {fp}. The game flies the general policy here until: "
                  f"python3 Tools/Build/skimrace_retune.py {i}")
            stale += 1
        else:
            print(f"I{i}: OK - {name} matches the map ({fp})")
    for name in sorted(policies):
        m = re.fullmatch(r"SkimRaceAIConfig_I(\d+)", name)
        if m and int(m.group(1)) not in live:
            print(f"I{m.group(1)}: {name} exists but the scene has no track for intensity {m.group(1)}")
    general = policies.get("SkimRaceAIConfig", {})
    if general.get("TrackFingerprint"):
        print("SkimRaceAIConfig (general): must not record a map fingerprint - it is for every track")
        stale += 1
    return 1 if stale else 0


# ---- self-test: the golden vector shared with SkimRaceTrackFingerprintTests ---------------------------

GOLDEN_POINTS = [("0", "0", "0"), ("100.4", "-20.6", "300"), ("-250.5", "12.5", "7")]
GOLDEN_ANCHORS = [("10", "20", "30"), ("-5.25", "0.75", "1e3")]
GOLDEN = "0cd105f6"        # spline on, 3 laps - SkimRaceTrackFingerprintTests asserts the same value
GOLDEN_EMPTY = "07b13f04"  # no points, no spline, 0 laps, no crystals


def self_test():
    got = fingerprint_of(GOLDEN_POINTS, 1, 3, GOLDEN_ANCHORS)
    empty = fingerprint_of([], 0, 0, [])
    ok = got == GOLDEN and empty == GOLDEN_EMPTY
    print(f"golden: {got} (expected {GOLDEN}), empty: {empty} (expected {GOLDEN_EMPTY}) {'OK' if ok else 'FAIL'}")
    # Negative controls: a whole-unit move, a lap and the curve flag each change it; a sub-unit nudge does not.
    moved = fingerprint_of([("0", "0", "0"), ("101.4", "-20.6", "300"), GOLDEN_POINTS[2]], 1, 3, GOLDEN_ANCHORS)
    nudged = fingerprint_of([("0", "0", "0"), ("100.3", "-20.6", "300"), GOLDEN_POINTS[2]], 1, 3, GOLDEN_ANCHORS)
    checks = [
        ("a 1-unit waypoint move changes it", moved != got),
        ("a sub-unit nudge does not", nudged == got),
        ("a lap changes it", fingerprint_of(GOLDEN_POINTS, 1, 2, GOLDEN_ANCHORS) != got),
        ("the curve flag changes it", fingerprint_of(GOLDEN_POINTS, 0, 3, GOLDEN_ANCHORS) != got),
        ("a crystal move changes it", fingerprint_of(GOLDEN_POINTS, 1, 3, GOLDEN_ANCHORS[:1]) != got),
        ("half rounds to even (2.5 -> 2, 3.5 -> 4)", _round(2.5) == 2 and _round(3.5) == 4 and _round(-2.5) == -2),
    ]
    for label, passed in checks:
        print(f"  {label}: {'OK' if passed else 'FAIL'}")
        ok = ok and passed
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--self-test", action="store_true")
    ap.add_argument("--emit-track")
    ap.add_argument("--scene", default=SCENE)
    args = ap.parse_args()
    if args.self_test:
        sys.exit(self_test())
    if args.emit_track:
        emit_track(args.emit_track, args.scene)
        return
    if args.check:
        sys.exit(check(args.scene))
    for i, fp in sorted(fingerprints(args.scene).items()):
        print(f"I{i}: {fp}")


if __name__ == "__main__":
    main()
