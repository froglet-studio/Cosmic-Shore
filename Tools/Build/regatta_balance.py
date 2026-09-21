#!/usr/bin/env python3
"""
The Regatta's balance model - an offline LAP-TIME ESTIMATE for every playable hull on the
shipped circuits, and the solver that turns it into the card's per-hull STARTING ELEMENT table.

    python3 Tools/Build/regatta_balance.py            # print the model and the tuned table
    import regatta_balance as RB; RB.solve()          # what author_regatta_assets.py does

WHAT IT READS, AND FROM WHERE. Every flight number is read out of the shipped prefab or ability
asset by key (never remembered), and the course is the MEASURED one -
Tools/Build/regatta_course_measurements.json, produced by running the real C# through
Tools/Build/regatta_course_harness/run.sh. The two exceptions are stated: the Scarab's speed
constants are C# initializers (its prefab carries no override - SCARAB.md 13), and the Dolphin's
peak is BoostMultiplier x ChargedBoostCharge = 68 x 2.259^2 = 347, which is what
VesselTransformer.CurrentBoostAmount computes today (the 357 some comments quote is stale; the
squaring is flagged as a possible defect, and this model follows the CODE).

WHAT THE MODEL IS. For each hull, a competent pilot on the best line: straights at the hull's
top speed under its own boost economy, every corner at the fastest speed that hull can HOLD
through a corner of that radius (v = r x omega(v) solved for v, or the ramp/Soar curve the
Headlong and Redline courses are cut against), acceleration by the hull's own model (a 1.5/s
lerp for the fleet, a 220 u/s^2 ramp for the Rhino, a 90 u/s^2 integrator for the Scarab,
a charge/discharge cycle for the Dolphin, a charge-duty cycle for the Serpent), braking
instant (a simplification that is uniform across hulls). Rail riders - the Urchin grinding its
own-colour rail and the Squirrel skimming it - take the rail's own length at the rail speed
and pay nothing at corners, because the rail rides the corner for them.

WHAT THE SOLVER DOES. Only ONE element reaches a hull's speed, and only on some hulls: TIME is
Soar on the Manta (x0.7..x1.3), the boost on the Sparrow (x0.5..x1.5) and the Serpent
(x0.25..x1.6, on speed AND duration), the throttle ceiling on the Scarab (x0.75..x1.5), the
charge fill rate on the Dolphin (x0.75..x1.5). The Rhino's ramp, the Urchin's grind and the
Squirrel's skim answer to no element. So the solver picks, per intensity, the integer Time
level in [-5, 10] for each tunable hull that brings its lap time closest to the fleet's
geometric mean, iterated to a fixed point, and REPORTS the residual spread - which is the
honest number: what elements cannot close is the course's and the comeback system's job.

WHAT IT IS NOT. Not a frame time, not a playtest, and not the AI (an AI holds 0.6 throttle and
several hulls' boosts have no autopilot drive - REGATTA.md). It is the same class of artefact
as skein_budget.py: the arithmetic that makes the authored numbers defensible before the first
editor run, and the thing to re-run when a vessel's numbers move.
"""
import json
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PREFABS = os.path.join(ROOT, "Assets", "_Prefabs", "Spacevessels")
MEASUREMENTS = os.path.join(ROOT, "Tools", "Build", "regatta_course_measurements.json")

K = math.pi / 180.0
LERP_AMOUNT = 1.5            # VesselTransformer.LERP_AMOUNT - the fleet's speed lag
LAPS = 3
HULLS = ["Manta", "Dolphin", "Rhino", "Urchin", "Squirrel", "Serpent", "Sparrow", "Scarab"]
TUNABLE = ["Manta", "Dolphin", "Serpent", "Sparrow", "Scarab"]   # Time reaches their speed
MIN_LEVEL, MAX_LEVEL = -5, 10                                    # the resource system's band


# ── reading the shipped numbers ──────────────────────────────────────────────

def _read(rel):
    with open(os.path.join(ROOT, rel), encoding="utf-8") as fh:
        return fh.read()


def _key(text, key, label):
    m = re.search(rf"^\s*{re.escape(key)}:\s*(-?[0-9.]+)\s*$", text, re.M)
    assert m, f"{label}: '{key}' not found"
    return float(m.group(1))


def _nested_key(text, owner, key, label):
    """
    A key INSIDE a named block: `owner:` then the first `key:` beneath it -- OR the owner read
    as a plain scalar, because the same field can be serialized either way.

    An `ElementalFloat` serializes as a MAPPING (`owner:` / `  Value: 3`); a plain `float`
    serializes as a SCALAR (`owner: 3`). The 2026-09-20 pass that turned nine SO-hosted
    ElementalFloats into plain floats therefore silently changed the SHAPE this reader has to
    match, and a nested-only read then fails with "block not found" -- which is a crash rather
    than a wrong number, and so at least fails loudly. Accepting both is what makes the read
    survive the type either way; the owner is still named, which is the property the rest of
    this docstring is about.

    Use this for any generic child name (`Value`, `Min`, `Max`, `Enabled`) rather than reaching
    for `_key`, whose pattern is `^\\s*key:` -- any indent, first match wins. That read is a
    landmine on a serialized sub-object: the Serpent's boost multiplier was `_key(serpent,
    "Value")` with a `# boostMultiplier.Value` comment explaining what it MEANT, and it silently
    became a DIFFERENT ElementalFloat's Value the moment one was added earlier in the same asset
    (the element-scaling unification added `timeDurationMultiplier`). The model then priced the
    Serpent with no boost at all, doubling the fleet's lap-time spread from 5.99x to 13.11x --
    and `--check` still exited 0, because the assert it trips is on the spread, not on the read.
    A name that does not identify its owner is not a measurement.
    """
    scalar = re.search(rf"^\s*{re.escape(owner)}:\s*(-?[0-9.]+)\s*$", text, re.M)
    if scalar:
        return float(scalar.group(1))
    m = re.search(rf"^\s*{re.escape(owner)}:\s*$", text, re.M)
    assert m, f"{label}: '{owner}' found neither as a scalar nor as a block"
    m2 = re.search(rf"^\s*{re.escape(key)}:\s*(-?[0-9.]+)\s*$", text[m.end():], re.M)
    assert m2, f"{label}: '{owner}.{key}' not found"
    return float(m2.group(1))


def _first_key(text, key, label):
    """The FIRST occurrence - used on prefabs where a key appears on one component only."""
    m = re.search(rf"^  {re.escape(key)}:\s*(-?[0-9.]+)\s*$", text, re.M)
    assert m, f"{label}: '{key}' not found"
    return float(m.group(1))


def read_vessel(name):
    """Transformer + status numbers off the hull's prefab (all at two-space indent, once each)."""
    t = _read(f"Assets/_Prefabs/Spacevessels/{name}.prefab")
    return {
        "ts": _first_key(t, "DefaultThrottleScaler", name),
        "ms": _first_key(t, "DefaultMinimumSpeed", name),
        "pitch": _first_key(t, "PitchScaler", name),
        "yaw": _first_key(t, "YawScaler", name),
        "rts": _first_key(t, "RotationThrottleScaler", name),
        "boost": _first_key(t, "boostMultiplier", name),
    }


def read_constants():
    v = {h: read_vessel(h) for h in HULLS}
    c = {"vessels": v}

    manta = _read("Assets/_Prefabs/Spacevessels/Manta.prefab")
    c["manta_trigger_yaw"] = _first_key(manta, "maxYawDegPerSec", "Manta")

    ramp = _read("Assets/_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset")
    c["rhino_max_boost"] = _key(ramp, "maxBoostMultiplier", "RhinoRampBoost")
    c["rhino_accel"] = _key(ramp, "accelerationPerSecond", "RhinoRampBoost")
    c["rhino_grace"] = _key(ramp, "straightnessGraceBand", "RhinoRampBoost")
    c["rhino_plateau"] = 0.3          # HeadlongCircuitSettings.BoostPlateauDeviation

    urchin = _read("Assets/_Prefabs/Spacevessels/Urchin.prefab")
    c["urchin_rail_speed"] = _first_key(urchin, "FriendlyTerrainSpeed", "Urchin")

    boost_max_meta = _read("Assets/_SO_Assets/Effects/Skimmer Prism Effects/SkimmerBoostPrismEffect.asset")
    m = re.search(r"boostMaxMultiplier: \{fileID: 11400000, guid: ([0-9a-f]{32})", boost_max_meta)
    assert m, "SkimmerBoostPrismEffect boostMaxMultiplier reference"
    for dirpath, _d, files in os.walk(os.path.join(ROOT, "Assets", "_SO_Assets")):
        for fn in files:
            if not fn.endswith(".meta"):
                continue
            full = os.path.join(dirpath, fn)
            with open(full, encoding="utf-8", errors="ignore") as fh:
                if f"guid: {m.group(1)}" in fh.read():
                    c["squirrel_boost_max"] = _key(open(full[:-5], encoding="utf-8").read(), "_value", "SquirrelBoostMax")
    assert "squirrel_boost_max" in c, "Squirrel boost max FloatVariable not found"

    serpent = _read("Assets/_SO_Assets/VesselActions/Serpent/ConsumeBoostAction.asset")
    c["serpent_boost"] = _nested_key(serpent, "boostMultiplier", "Value", "SerpentConsumeBoost")
    c["serpent_duration"] = _key(serpent, "boostDuration", "SerpentConsumeBoost")
    c["serpent_cost"] = _key(serpent, "resourceCost", "SerpentConsumeBoost")
    sp = _read("Assets/_Prefabs/Spacevessels/Serpent.prefab")
    rates = [float(x) for x in re.findall(r"^\s*resourceGainRate:\s*([0-9.]+)", sp, re.M)]
    assert len(rates) >= 2, "Serpent prefab resource rates"
    c["serpent_regen"] = rates[1]                                           # resource index 1 = Boost

    dolphin = _read("Assets/_SO_Assets/VesselActions/Dolphin/ChargeBoostAction.asset")
    c["dolphin_boost"] = _key(dolphin, "maxBoostMultiplier", "DolphinChargeBoost")
    c["dolphin_fill"] = _key(dolphin, "chargeTimeToFull", "DolphinChargeBoost")
    c["dolphin_discharge"] = _key(dolphin, "dischargeTimeToEmpty", "DolphinChargeBoost")

    scarab = _read("Assets/_Scripts/Controller/Vessel/ScarabVesselTransformer.cs")
    c["scarab_top"] = float(re.search(r"baseTopSpeed = ([0-9.]+)f", scarab).group(1))
    c["scarab_accel"] = float(re.search(r"accelerationPerSecond = ([0-9.]+)f", scarab).group(1))
    return c


def read_course():
    with open(MEASUREMENTS, encoding="utf-8") as fh:
        return json.load(fh)


# ── element multipliers (ElementalScaling.Multiplier / ElementalFloat.EvaluateLive) ──

def map_mul(level, at_full, floor):
    """ElementalScaling.Multiplier: 1 at rest, at_full at level 10, linear, floored."""
    return max(floor, 1.0 + (at_full - 1.0) * (level / 10.0))


def elemental_float(level, lo, hi):
    """ElementalFloat.EvaluateLive: LerpUnclamped(Min, Max, level / 10)."""
    return lo + (hi - lo) * (level / 10.0)


# ── per-hull speed models ────────────────────────────────────────────────────

def corner_speed_simple(r, rts, turn, top):
    """v = r x omega(v), omega = v x rts + turn (deg/s). Any speed fits once r x k x rts >= 1."""
    denom = 1.0 - r * K * rts
    if denom <= 1e-6:
        return top
    return min(top, r * K * turn / denom)


def manta_model(c, level):
    v = c["vessels"]["Manta"]
    mul = map_mul(level, 1.3, 0.7)                     # Time -> Soar (map x1.3, floor 0.7)

    def speed(b):
        return v["ts"] * (1 + (v["boost"] - 1) * b) * mul + v["ms"]

    def radius(b):
        s = speed(b)
        omega = s * v["rts"] + min(v["pitch"], v["yaw"]) + c["manta_trigger_yaw"] * (1 - b)
        return s / (omega * K)

    def corner(r):
        if r >= radius(1.0):
            return speed(1.0)
        if r <= radius(0.0):
            return speed(0.0)
        lo, hi = 0.0, 1.0
        for _ in range(40):
            mid = 0.5 * (lo + hi)
            if radius(mid) > r:
                hi = mid
            else:
                lo = mid
        return speed(lo)

    return dict(top=speed(1.0), corner=corner, accel=("lerp", LERP_AMOUNT))


def rhino_model(c, level):
    v = c["vessels"]["Rhino"]
    turn = min(v["pitch"], v["yaw"])

    def speed_at_stick(s):
        straight01 = 1.0 - min(1.0, max(0.0, (s - c["rhino_plateau"]) / max(1e-4, c["rhino_grace"] - c["rhino_plateau"])))
        return v["ts"] * (1 + (c["rhino_max_boost"] - 1) * straight01) + v["ms"]

    def radius_at_stick(s):
        sp = speed_at_stick(s)
        return sp / ((sp * v["rts"] + turn) * K) / s

    top = speed_at_stick(c["rhino_plateau"])

    def corner(r):
        if r >= radius_at_stick(c["rhino_plateau"]):
            return top
        lo, hi = c["rhino_plateau"], 1.0
        for _ in range(40):
            mid = 0.5 * (lo + hi)
            if radius_at_stick(mid) > r:
                lo = mid
            else:
                hi = mid
        return speed_at_stick(hi)

    return dict(top=top, corner=corner, accel=("linear", c["rhino_accel"]))


def scarab_model(c, level):
    v = c["vessels"]["Scarab"]
    top = c["scarab_top"] * elemental_float(level, 1.0, 1.5)   # ThrottleScalerMultiplier, Time 1->1.5
    turn = min(v["pitch"], v["yaw"])
    return dict(top=top, corner=lambda r: corner_speed_simple(r, v["rts"], turn, top),
                accel=("linear", c["scarab_accel"]))


def sparrow_model(c, level):
    v = c["vessels"]["Sparrow"]
    mul = map_mul(level, 1.5, 0.5)                     # Time -> Afterburner (map x1.5, floor 0.5)
    top = v["ts"] * v["boost"] * mul + v["ms"]
    turn = min(v["pitch"], v["yaw"])
    return dict(top=top, corner=lambda r: corner_speed_simple(r, v["rts"], turn, top),
                accel=("lerp", LERP_AMOUNT))


def serpent_model(c, level):
    v = c["vessels"]["Serpent"]
    mul = map_mul(level, 1.6, 0.25)                    # Time -> boost duration AND boost amount
    cruise = v["ts"] + v["ms"]
    boosted = v["ts"] * c["serpent_boost"] * mul + v["ms"]
    duration = c["serpent_duration"] * mul
    per_charge = c["serpent_cost"] / c["serpent_regen"]
    duty = min(1.0, duration / per_charge)             # sustained, ignoring the 4-charge opening bank
    top = duty * boosted + (1 - duty) * cruise
    turn = min(v["pitch"], v["yaw"])
    return dict(top=top, corner=lambda r: corner_speed_simple(r, v["rts"], turn, top),
                accel=("lerp", LERP_AMOUNT), duty=duty, boosted=boosted)


def dolphin_model(c, level):
    v = c["vessels"]["Dolphin"]
    mul = map_mul(level, 1.5, 0.25)                    # Time -> charge fill rate
    cruise = v["ts"] + v["ms"]
    peak = v["ts"] * c["dolphin_boost"] * c["dolphin_boost"] + v["ms"]   # the CODE's product (see header)
    fill = c["dolphin_fill"] / mul
    turn = min(v["pitch"], v["yaw"])
    return dict(top=peak, corner=lambda r: corner_speed_simple(r, v["rts"], turn, peak),
                accel=("dolphin", fill, c["dolphin_discharge"], cruise), peak=peak, fill=fill)


def urchin_model(c, level):
    return dict(rail=c["urchin_rail_speed"])


def squirrel_model(c, level):
    v = c["vessels"]["Squirrel"]
    top = v["ts"] * c["squirrel_boost_max"] + v["ms"]  # skim-saturated on the rail
    turn = min(v["pitch"], v["yaw"])
    return dict(rail=None, top=top, corner=lambda r: corner_speed_simple(r, v["rts"], turn, top),
                accel=("lerp", LERP_AMOUNT))


MODELS = {
    "Manta": manta_model, "Dolphin": dolphin_model, "Rhino": rhino_model, "Urchin": urchin_model,
    "Squirrel": squirrel_model, "Serpent": serpent_model, "Sparrow": sparrow_model, "Scarab": scarab_model,
}


# ── the lap ──────────────────────────────────────────────────────────────────

def leg_time(d, v0, model, dt=0.02):
    """Seconds to cover d units starting at v0 under the hull's acceleration model."""
    kind = model["accel"][0]
    top = model["top"]
    t, x, v = 0.0, 0.0, v0
    if kind == "dolphin":
        _k, fill, discharge, cruise = model["accel"]
        # First cycle: drift-charge at the entry speed, then discharge at the peak; every later
        # cycle drifts at the peak (drift holds speed), so the leg is peak from there on.
        phase, clock, hold = "charge", 0.0, v0
        while x < d:
            if phase == "charge":
                speed = hold
                if clock >= fill:
                    phase, clock = "discharge", 0.0
            else:
                speed = top
                if clock >= discharge:
                    phase, clock, hold = "charge", 0.0, top
            x += speed * dt
            t += dt
            clock += dt
        return t
    while x < d:
        if kind == "lerp":
            v += (top - v) * model["accel"][1] * dt
        else:
            v = min(top, v + model["accel"][1] * dt)
        x += v * dt
        t += dt
    return t


def lap_time(hull, model, course):
    """One lap, seconds. Rail riders take the rail; everyone else flies the spine leg by leg."""
    if model.get("rail"):
        return sum(course["laneLengths"]) / len(course["laneLengths"]) / model["rail"]

    legs = course["legLengths"]
    radii = course["cornerRadii"]
    spine = course["spineLength"]
    scale = spine / sum(legs)                          # the spline is longer than the chords
    n = len(legs)
    corners = [model["corner"](radii[i]) for i in range(n)]
    total = 0.0
    for i in range(n):
        v_in = min(corners[i], model["top"])           # exit corner i at its speed
        total += leg_time(legs[i] * scale, v_in, model)
    return total


# ── the solve ────────────────────────────────────────────────────────────────

def solve(constants=None, course_data=None, laps=LAPS):
    """Per intensity: {hull: (level, lapTime)} plus the fleet's spread. Deterministic."""
    c = constants or read_constants()
    data = course_data or read_course()
    out = {}
    for key, course in sorted(data["intensities"].items(), key=lambda kv: int(kv[0])):
        intensity = int(key)
        # Every hull's lap time at every level it can be authored at.
        table = {}
        for hull in HULLS:
            levels = range(MIN_LEVEL, MAX_LEVEL + 1) if hull in TUNABLE else [0]
            table[hull] = {lv: lap_time(hull, MODELS[hull](c, lv), course) * laps for lv in levels}

        chosen = {h: 0 for h in HULLS}
        for _ in range(12):
            target = math.exp(sum(math.log(table[h][chosen[h]]) for h in HULLS) / len(HULLS))
            for h in TUNABLE:
                # Closest to the target in log time; on a tie the level nearest REST wins, so a
                # hull whose lever is inert at some rung is not handed a pointless handicap.
                chosen[h] = min(table[h], key=lambda lv: (round(abs(math.log(table[h][lv]) - math.log(target)), 4), abs(lv)))

        times = {h: table[h][chosen[h]] for h in HULLS}
        rest = {h: table[h][0] for h in HULLS}
        out[intensity] = {
            "levels": chosen,
            "times": times,
            "restTimes": rest,
            "spread": max(times.values()) / min(times.values()),
            "restSpread": max(rest.values()) / min(rest.values()),
        }
    return out


def describe(result):
    lines = []
    for intensity, r in result.items():
        lines.append(f"intensity {intensity}: spread at rest {r['restSpread']:.2f}x -> tuned {r['spread']:.2f}x")
        for h in HULLS:
            lv = r["levels"][h]
            lines.append(f"    {h:<9} Time {lv:>3}   race {r['times'][h]:6.1f}s   (at rest {r['restTimes'][h]:6.1f}s)")
    return "\n".join(lines)


if __name__ == "__main__":
    c = read_constants()
    print("constants:", json.dumps({k: v for k, v in c.items() if k != "vessels"}, indent=1))
    for h in HULLS:
        print(f"  {h:<9} {c['vessels'][h]}")
    print()
    print(describe(solve(c)))
