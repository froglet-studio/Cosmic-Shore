#!/usr/bin/env python3
"""Measure the Squirrel's TOUCH drift, and fail if it scrubs speed.

WHY THIS EXISTS
---------------
On the stripped Android branch a touch drift is a RIGHT-thumb LIFT, and a lift is a FULL trigger
pull: glass cannot measure trigger travel, so it takes the binary fallback every non-analog input
gets (VesselTransformer.GetTriggerSum - a drift that is on is a full pull). The left thumb then
flies alone, mirrored onto both sticks at OneThumbDriftTurnGain (TouchInputStrategy), pitch and
yaw only. So the worst case is the steering thumb at full deflection with the drift's full-pull
`Mult` multiplying every rotation scaler (VesselTransformer.ApplyAnalogDrift) and its full-pull
grip letting the velocity lag the nose.

The invariant is the FELT one, measured rather than eyeballed:

    a full-deflection lift drift held through a CORNER_DEG hairpin must never be slower than it
    was on entry, and must leave the corner at least as fast as it entered.

Peak SLIP (the angle between the velocity and the nose) is reported too. Past 90 degrees the
vector flight model's nose-ward thrust (`ComputeNoseAcceleration`, always along +forward) points
partly against the velocity - Round 9 gated on that angle as a proxy for braking. It is a proxy:
`ShapeSpeed` floors the speed at its pre-thrust magnitude, so a slide can pass 90 degrees for a
moment and still carry speed out of the corner. The gate is on what the pilot feels - speed -
and the gain is the dial (the full-pull drift itself is the action's authored tuning).

A corner, not a fixed hold: at full deflection a slide past 90 degrees is only a matter of time.
What a pilot flies is a corner.

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
CORNER_DEG = 180.0

# InputEvents.OnlyLeftStickAction - the RIGHT thumb lifted (the left remains), which the Squirrel
# binds its drift to on touch.
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
    ap.add_argument("--sweep", action="store_true", help="print the one-thumb gain sensitivity table")
    args = ap.parse_args()

    prefab = read(SQUIRREL)
    yaw = scalar("YawScaler", prefab, "YawScaler")
    throttle = scalar("DefaultThrottleScaler", prefab, "DefaultThrottleScaler")
    tiers = touch_drift_actions()
    if not tiers:
        sys.exit("no drift actions found on the Squirrel's touch override")
    tier = next((t for t in tiers if t["sharp"]), tiers[0])

    # VesselTransformer.GetTriggerSum: a touch lift has no measured trigger travel, so it is the
    # binary fallback - a FULL pull - and ApplyAnalogDrift runs the action's full-pull Mult and grip.

    gain = constant("OneThumbDriftTurnGain")   # the mirrored steering thumb at full deflection
    x_sum = ease(2.0 * gain)
    omega = x_sum * yaw * tier["mult"]
    corner_s = CORNER_DEG / omega

    print(f"Squirrel TOUCH lift drift, full pull  (YawScaler {yaw:g}, throttle scaler {throttle:g}, "
          f"one-thumb gain {gain:g})")
    print(f"  bound touch drift tiers : {', '.join(t['name'] for t in tiers)}")
    print(f"  tier (full pull)        : {tier['name']}  "
          f"(Mult {tier['mult']:.3g}, Grip {tier['grip']:.3g}, sharp={tier['sharp']})")
    print(f"  commanded yaw           : {omega:.1f} deg/s -> a {CORNER_DEG:g} deg corner "
          f"takes {corner_s:.2f} s")
    print()

    def corner(g, target):
        om = ease(2.0 * g) * yaw * tier["mult"]
        trace = simulate(yaw, tier["mult"], tier["grip"], g, target, seconds=CORNER_DEG / om)
        peak = max(sl for sl, _ in trace)
        floor = min(v for _, v in trace)
        return om, peak, floor, trace[-1][1]

    # Speeds are compared with a hair of float slack: the model's ShapeSpeed floor holds the
    # magnitude exactly, and a 1e-9 wobble is not a brake.
    eps = 1e-6
    ok = True
    for xdiff in (0.5, 0.75, 1.0):
        target = xdiff * throttle
        _, peak, floor, end = corner(gain, target)
        good = floor >= target - eps and end >= target - eps
        ok &= good
        print(f"  [{'ok ' if good else 'BAD'}] XDiff {xdiff:<4} target {target:5.1f} -> "
              f"slowest {floor / target * 100:5.1f}%, exit {end / target * 100:5.1f}% of entry speed "
              f"(peak slip {peak:5.1f} deg{', past 90' if peak >= SLIP_LIMIT_DEG else ''})")

    if args.sweep:
        print(f"\n  one-thumb gain sensitivity (XDiff 0.75, full pull, {CORNER_DEG:g} deg corner):")
        for g in (0.5, 0.6, 0.7, 0.8, 0.9, 1.0):
            target = 0.75 * throttle
            om, peak, floor, end = corner(g, target)
            print(f"    gain {g:<4} yaw {om:6.1f} deg/s  slowest {floor / target * 100:5.1f}%  "
                  f"exit {end / target * 100:5.1f}%  peak slip {peak:5.1f} deg")

    if args.check and not ok:
        print(f"\nFAIL: a full-pull lift drift through a {CORNER_DEG:g} deg corner drops below, or "
              f"leaves slower than, its entry speed - the drift is a brake.\n"
              f"Lower OneThumbDriftTurnGain (TouchInputStrategy) or raise the drift action's "
              f"driftDamping.")
        return 1
    if args.check:
        print("\nOK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
