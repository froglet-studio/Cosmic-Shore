#!/usr/bin/env python3
"""Measure the Squirrel's ONE-THUMB touch drift, and fail if it scrubs speed.

WHY THIS EXISTS
---------------
A touch drift is flown with a single thumb, and TouchInputStrategy mirrors that thumb onto
both virtual sticks so it can reach the two-thumb turn ceiling.  The vessel ALSO multiplies
every rotation scaler by the drift's own `Mult` (VesselTransformer.ApplyAnalogDrift).  Those
two multipliers stack, and each was calibrated as if it were the only one.

Past ~90 degrees of SLIP (the angle between the velocity and the nose) the vector flight
model's nose-ward thrust starts subtracting from the velocity's magnitude -
`ComputeNoseAcceleration` always adds along +transform.forward, while the velocity's forward
component has gone negative - so the racing drift becomes a brake.  Nothing logs it; it is
only ever felt.

So the invariant is stated and MEASURED rather than eyeballed:

    a held, full-deflection one-thumb drift must not reach 90 degrees of slip
    within HOLD_SECONDS, and must not end slower than it started.

Every input is read from the SHIPPED files, so a retune of any one of them is checked:
  * OneThumbDriftTurnGain   - Assets/_Scripts/Controller/IO/TouchInputStrategy.cs
  * Mult / driftDamping     - the drift action assets bound to the Squirrel's TOUCH override
  * YawScaler               - Assets/_Prefabs/Spacevessels/Squirrel.prefab

Usage:  python3 Tools/Build/touch_drift_slip.py [--check] [--sweep]
Read-only: writes nothing, opens no scenes, needs no Unity.
"""

import argparse
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STRATEGY = ROOT / "Assets/_Scripts/Controller/IO/TouchInputStrategy.cs"
SQUIRREL = ROOT / "Assets/_Prefabs/Spacevessels/Squirrel.prefab"
ACTIONS = ROOT / "Assets/_SO_Assets/VesselActions"

# VesselTransformer constants (source of truth: VesselTransformer.cs).
DT = 1.0 / 60.0
LERP_AMOUNT = 1.5

# The bar.  90 degrees is not a taste threshold - it is where nose thrust changes sign.
SLIP_LIMIT_DEG = 90.0
HOLD_SECONDS = 2.0

# InputEvents.OnlyLeftStickAction - the single-thumb event the Squirrel binds its drift to.
DRIFT_TOUCH_EVENT = 12


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def ease(x: float) -> float:
    """TouchInputStrategy.Ease - NOT BaseInputStrategy's cosine (0.4625 vs 0.2926 at x=1)."""
    t = max(-1.0, min(1.0, x * 0.5))
    return t * t * t * 0.1 + t * 0.9


def guid_to_asset() -> dict:
    out = {}
    for meta in ACTIONS.rglob("*.asset.meta"):
        m = re.search(r"^guid: ([0-9a-f]{32})", read(meta), re.M)
        if m:
            out[m.group(1)] = meta.with_suffix("")  # strip .meta
    return out


def touch_drift_actions() -> list:
    """The drift assets on the Squirrel's TOUCH override for the one-thumb drift event."""
    text = read(SQUIRREL)
    block = re.search(
        r"^  _touchActionOverrides:\n(.*?)^  _\w+:", text, re.S | re.M)
    if not block:
        sys.exit("could not find _touchActionOverrides on Squirrel.prefab")
    entry = re.search(
        rf"^  - InputEvent: {DRIFT_TOUCH_EVENT}\n    ShipActions:\n((?:    - .*\n)*)",
        block.group(1), re.M)
    if not entry:
        sys.exit(f"Squirrel touch override has no InputEvent {DRIFT_TOUCH_EVENT}")
    guids = re.findall(r"guid: ([0-9a-f]{32})", entry.group(1))

    lookup = guid_to_asset()
    tiers = []
    for g in guids:
        path = lookup.get(g)
        if not path or not path.exists():
            continue
        body = read(path)
        mult = re.search(r"^  Mult: ([\d.]+)", body, re.M)
        damp = re.search(r"^  driftDamping: ([\d.]+)", body, re.M)
        sharp = re.search(r"^  isSharpDrifting: (\d)", body, re.M)
        if mult and damp and sharp:
            tiers.append(dict(name=path.stem, mult=float(mult.group(1)),
                              grip=float(damp.group(1)), sharp=sharp.group(1) == "1"))
    return tiers


def scalar(pattern: str, text: str, what: str) -> float:
    m = re.search(rf"^  {pattern}: (-?[\d.]+)", text, re.M)
    if not m:
        sys.exit(f"could not read {what}")
    return float(m.group(1))


def constant(name: str) -> float:
    m = re.search(rf"const float {name} = ([\d.]+)f", read(STRATEGY))
    if not m:
        sys.exit(f"could not read {name} from TouchInputStrategy.cs")
    return float(m.group(1))


def simulate(yaw_scaler, mult, grip, gain, throttle_target,
             seconds=HOLD_SECONDS, overshoot_ceiling=1.25):
    """One thumb held at FULL deflection into a sustained drift. Returns per-frame
    (slip degrees, speed). Transcribes VesselTransformer's vector path in 2D: the drift
    is planar, so a third axis adds nothing but noise."""
    x_sum = ease(2.0 * gain)                       # mirrored thumb at |stick| = 1
    omega = math.radians(x_sum * yaw_scaler * mult)  # RotationThrottleScaler is 0 here
    angle, vel = 0.0, [throttle_target, 0.0]
    trace = []

    for _ in range(int(seconds / DT)):
        angle += omega * DT
        fwd = (math.cos(angle), math.sin(angle))

        # 1) GRIP: slerp the velocity DIRECTION toward the nose, magnitude preserved.
        speed = math.hypot(*vel)
        if speed > 1e-6:
            conv = 1.0 - math.exp(-grip * DT)      # GripFraction, driftAmount == 1
            u = (vel[0] / speed, vel[1] / speed)
            dot = max(-1.0, min(1.0, u[0] * fwd[0] + u[1] * fwd[1]))
            ang = math.acos(dot)
            if ang > 1e-9:
                s = math.sin(ang)
                w0, w1 = math.sin((1 - conv) * ang) / s, math.sin(conv * ang) / s
                u = (w0 * u[0] + w1 * fwd[0], w0 * u[1] + w1 * fwd[1])
            vel = [u[0] * speed, u[1] * speed]

        before = math.hypot(*vel)

        # 2) THRUST ALONG THE NOSE. Always +fwd - which is exactly the sign trap past 90.
        along = vel[0] * fwd[0] + vel[1] * fwd[1]
        vel[0] += fwd[0] * (target_step(along, throttle_target) - along)
        vel[1] += fwd[1] * (target_step(along, throttle_target) - along)

        # 3) ShapeSpeed: bounds GAIN only, floored at the pre-thrust magnitude.
        now = math.hypot(*vel)
        cap = max(before, throttle_target * overshoot_ceiling)
        if now > cap and now > 1e-9:
            vel = [vel[0] * cap / now, vel[1] * cap / now]
            now = cap

        u = (vel[0] / now, vel[1] / now) if now > 1e-9 else fwd
        dot = max(-1.0, min(1.0, u[0] * fwd[0] + u[1] * fwd[1]))
        trace.append((math.degrees(math.acos(dot)), now))

    return trace


def target_step(current, target):
    return current + (target - current) * LERP_AMOUNT * DT


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="fail the build on a violation")
    ap.add_argument("--sweep", action="store_true", help="print the gain sensitivity table")
    args = ap.parse_args()

    prefab = read(SQUIRREL)
    yaw = scalar("YawScaler", prefab, "YawScaler")
    throttle = scalar("DefaultThrottleScaler", prefab, "DefaultThrottleScaler")
    gain = constant("OneThumbDriftTurnGain")

    tiers = touch_drift_actions()
    if not tiers:
        sys.exit("no drift actions found on the Squirrel's touch override")
    # GetTriggerSum's non-gamepad branch is binary and prefers SHARP, so the tier that
    # actually runs on touch is the sharp one if any is bound, else the single one.
    tier = next((t for t in tiers if t["sharp"]), tiers[0])

    print(f"Squirrel one-thumb TOUCH drift  (YawScaler {yaw:g}, throttle scaler {throttle:g})")
    print(f"  bound touch drift tiers : {', '.join(t['name'] for t in tiers)}")
    print(f"  tier that runs on touch : {tier['name']}  "
          f"(Mult {tier['mult']:g}, Grip {tier['grip']:g}, sharp={tier['sharp']})")
    print(f"  OneThumbDriftTurnGain   : {gain:g}")

    x_sum = ease(2.0 * gain)
    print(f"  commanded yaw at full deflection: "
          f"{x_sum * yaw * tier['mult']:.1f} deg/s   (Ease({2 * gain:g}) = {x_sum:.4f})")
    print()

    worst_slip = 0.0
    ok = True
    for xdiff in (0.5, 0.75, 1.0):
        target = xdiff * throttle
        trace = simulate(yaw, tier["mult"], tier["grip"], gain, target)
        peak = max(s for s, _ in trace)
        worst_slip = max(worst_slip, peak)
        start, end = target, trace[-1][1]
        kept = end / start if start else 0.0
        flag = "ok " if peak < SLIP_LIMIT_DEG and end >= start else "BAD"
        ok &= peak < SLIP_LIMIT_DEG and end >= start
        print(f"  [{flag}] XDiff {xdiff:<4} target {target:5.1f} -> "
              f"peak slip {peak:5.1f} deg, speed {start:5.1f} -> {end:5.1f} "
              f"({kept * 100:5.1f}% carried)")

    if args.sweep:
        print("\n  gain sensitivity (XDiff 0.75) - the cliff is at 90 deg:")
        for g in (0.5, 0.6, 0.7, 0.8, 0.9, 1.0):
            tr = simulate(yaw, tier["mult"], tier["grip"], g, 0.75 * throttle)
            print(f"    gain {g:<4} yaw {ease(2 * g) * yaw * tier['mult']:6.1f} deg/s  "
                  f"peak slip {max(s for s, _ in tr):5.1f} deg  "
                  f"end speed {tr[-1][1]:5.1f}")

    if args.check and not ok:
        print(f"\nFAIL: a held one-thumb drift reaches {worst_slip:.1f} deg of slip "
              f"(limit {SLIP_LIMIT_DEG:g}) or ends slower than it began.\n"
              f"Past 90 deg the nose thrust brakes: lower OneThumbDriftTurnGain, raise the "
              f"tier's driftDamping, or stop binding the SHARP tier to the touch override.")
        return 1
    if args.check:
        print("\nOK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
