#!/usr/bin/env python3
"""Hold the Squirrel's TOUCH drift to its PAD drift, and report what that drift does to speed.

WHY THIS EXISTS
---------------
On the stripped Android branch a touch drift is a RIGHT-thumb LIFT, and a lift is a FULL trigger
pull: glass cannot measure trigger travel, so it takes the binary fallback every non-analog input
gets (VesselTransformer.GetTriggerSum - a drift that is on is a full pull). The left thumb then
flies alone, mirrored onto both sticks (TouchInputStrategy.Reparameterize), pitch and yaw only.

The rule is PARITY: the touch drift is the pad drift with the analog trigger replaced by a full
pull. Rounds 12-15 broke it on purpose - the mirrored thumb was cut to 0.70 authority while a
thumb was lifted, so a full-lock drift could not scrub speed - and that was a touch-only steering
cut a pad pilot never had: on a pad, both sticks at full plus a full trigger command exactly the
full-lock yaw the cut was removing. So --check fails if touch and pad drift DIFFER:

  1. the touch drift event and the pad drift event bind the SAME drift action assets (one Mult,
     one grip - nothing tuned for touch alone);
  2. the one-thumb mirror applies NO gain (no OneThumb*Gain below 1, no scaled oneThumbStick);
  3. the touch curve reaches the pad curve's full-deflection authority (Ease(2) = 1 on both), so
     one thumb at the rim commands what two full sticks command.

What the shared drift then does to SPEED through a full-lock hairpin is reported, not gated: it is
the drift action's tuning and it is identical on both devices. Past 90 degrees of slip the vector
model's nose-ward thrust (ComputeNoseAcceleration, always +forward) points partly against the
velocity; ShapeSpeed floors the speed at its pre-thrust magnitude, so how much a full-lock corner
scrubs is what the report shows. If it scrubs too much, the dials are the drift action's Mult and
driftDamping - for both devices at once.

Every input is read from the SHIPPED files, so a retune of any one of them is checked:
  * Ease, the mirror           - Assets/_Scripts/Controller/IO/TouchInputStrategy.cs
  * pad Ease                    - Assets/_Scripts/Controller/IO/BaseInputStrategy.cs (PI_OVER_FOUR)
  * Mult / driftDamping         - the drift action assets on the Squirrel's touch AND pad overrides
  * YawScaler, throttle scaler  - Assets/_Prefabs/Spacevessels/Squirrel.prefab

Usage:  python3 Tools/Build/touch_drift_slip.py [--check] [--sweep] [--self-test]
Read-only: writes nothing, opens no scenes, needs no Unity.
"""

import argparse
import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
STRATEGY = ROOT / "Assets/_Scripts/Controller/IO/TouchInputStrategy.cs"
BASE_STRATEGY = ROOT / "Assets/_Scripts/Controller/IO/BaseInputStrategy.cs"
SQUIRREL = ROOT / "Assets/_Prefabs/Spacevessels/Squirrel.prefab"
ACTIONS = ROOT / "Assets/_SO_Assets/VesselActions"

# VesselTransformer constants (source of truth: VesselTransformer.cs).
DT = 1.0 / 60.0
LERP_AMOUNT = 1.5

SLIP_LIMIT_DEG = 90.0   # where nose thrust changes sign - reported, not gated
CORNER_DEG = 180.0

# InputEvents.OnlyLeftStickAction - the RIGHT thumb lifted (the left remains): the touch drift.
DRIFT_TOUCH_EVENT = 12
# InputEvents.LeftStickAction - which GamepadInputStrategy raises from the LEFT TRIGGER: the pad drift.
DRIFT_PAD_EVENT = 2

# A full-deflection authority mismatch smaller than this is the pad cosine's own rounding
# (PI_OVER_FOUR is authored as 0.785, so the pad tops out at 0.99920), not a design difference.
AUTHORITY_TOLERANCE = 2e-3


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


# ------------------------------------------------------------------------- shipped curves

def touch_curve(strategy_text: str):
    """(linear, cubic) coefficients of TouchInputStrategy.Ease: cubic * C + t * L."""
    m = re.search(r"return cubic \* ([\d.]+)f \+ t \* ([\d.]+)f;", strategy_text)
    if not m:
        sys.exit("could not read TouchInputStrategy.Ease (expected `return cubic * C + t * L;`)")
    return float(m.group(2)), float(m.group(1))


def touch_ease(x: float, curve) -> float:
    lin, cub = curve
    t = max(-1.0, min(1.0, x * 0.5))
    return t * t * t * cub + t * lin


def pad_ease(x: float, base_text: str) -> float:
    m = re.search(r"PI_OVER_FOUR = ([\d.]+)f", base_text)
    if not m:
        sys.exit("could not read PI_OVER_FOUR from BaseInputStrategy.cs")
    k = float(m.group(1))
    c = math.cos(x * k) - 1.0
    return c if x < 0 else -c


# ------------------------------------------------------------------------- shipped tuning

def guid_to_asset() -> dict:
    out = {}
    for meta in ACTIONS.rglob("*.asset.meta"):
        m = re.search(r"^guid: ([0-9a-f]{32})", read(meta), re.M)
        if m:
            out[m.group(1)] = meta.with_suffix("")  # strip .meta
    return out


def drift_tiers(prefab_text: str, override_field: str, event: int, lookup: dict) -> list:
    """The drift assets (those carrying Mult + driftDamping) bound to one override's event."""
    block = re.search(rf"^  {override_field}:\n(.*?)^  _\w+:", prefab_text, re.S | re.M)
    if not block:
        sys.exit(f"could not find {override_field} on Squirrel.prefab")
    entry = re.search(
        rf"^  - InputEvent: {event}\n    ShipActions:\n((?:    - .*\n)*)", block.group(1), re.M)
    if not entry:
        return []
    tiers = []
    for g in re.findall(r"guid: ([0-9a-f]{32})", entry.group(1)):
        path = lookup.get(g)
        if not path or not path.exists():
            continue
        body = read(path)
        mult = re.search(r"^  Mult: ([\d.]+)", body, re.M)
        damp = re.search(r"^  driftDamping: ([\d.]+)", body, re.M)
        sharp = re.search(r"^  isSharpDrifting: (\d)", body, re.M)
        if mult and damp and sharp:
            tiers.append(dict(guid=g, name=path.stem, mult=float(mult.group(1)),
                              grip=float(damp.group(1)), sharp=sharp.group(1) == "1"))
    return tiers


def scalar(pattern: str, text: str, what: str) -> float:
    m = re.search(rf"^  {pattern}: (-?[\d.]+)", text, re.M)
    if not m:
        sys.exit(f"could not read {what}")
    return float(m.group(1))


# ------------------------------------------------------------------------- the parity gate

def parity_failures(strategy_text: str, base_text: str, prefab_text: str, lookup: dict) -> list:
    fails = []

    touch = drift_tiers(prefab_text, "_touchActionOverrides", DRIFT_TOUCH_EVENT, lookup)
    pad = drift_tiers(prefab_text, "_gamepadActionOverrides", DRIFT_PAD_EVENT, lookup)
    if not touch:
        fails.append(f"the touch override binds no drift action to InputEvent {DRIFT_TOUCH_EVENT}")
    if not pad:
        fails.append(f"the pad override binds no drift action to InputEvent {DRIFT_PAD_EVENT}")
    if touch and pad and {t["guid"] for t in touch} != {t["guid"] for t in pad}:
        fails.append("touch and pad drift bind DIFFERENT drift assets "
                     f"(touch: {', '.join(t['name'] for t in touch)}; "
                     f"pad: {', '.join(t['name'] for t in pad)}) - a touch-only drift tuning")

    for name, value in re.findall(r"const float (OneThumb\w*Gain) = ([\d.]+)f", strategy_text):
        if float(value) < 1.0:
            fails.append(f"{name} = {value} cuts the one-thumb mirror below the pad's authority")
    if re.search(r"oneThumbStick\s*\*", strategy_text):
        fails.append("the one-thumb mirror scales oneThumbStick - a touch-only steering cut")

    curve = touch_curve(strategy_text)
    touch_full, pad_full = touch_ease(2.0, curve), pad_ease(2.0, base_text)
    if abs(touch_full - pad_full) > AUTHORITY_TOLERANCE:
        fails.append(f"touch full-deflection authority {touch_full:.4f} != pad {pad_full:.4f} "
                     f"(TouchInputStrategy.Ease coefficients must sum to 1)")
    return fails


# ------------------------------------------------------------------------- the speed report

def simulate(yaw_scaler, mult, grip, x_sum, throttle_target, seconds):
    """Full-pull drift with the turn held at x_sum. Returns per-frame (slip degrees, speed).
    Transcribes VesselTransformer's vector path in 2D: the drift is planar. The nose turns at
    the commanded rate - touchNoseResponse keeps it within ~24 degrees of the command through a
    full-lock drift, so this is the TOUCH nose; a pad nose lags further and slides less."""
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
        cap = max(before, throttle_target * 1.25)
        if now > cap and now > 1e-9:
            vel = [vel[0] * cap / now, vel[1] * cap / now]
            now = cap

        u = (vel[0] / now, vel[1] / now) if now > 1e-9 else fwd
        dot = max(-1.0, min(1.0, u[0] * fwd[0] + u[1] * fwd[1]))
        trace.append((math.degrees(math.acos(dot)), now))

    return trace


def target_step(current, target):
    return current + (target - current) * LERP_AMOUNT * DT


# ------------------------------------------------------------------------- self-test

def self_test(strategy_text, base_text, prefab_text, lookup) -> int:
    """Each shipped parity rule must FAIL on the change it exists to catch."""
    ok = True

    def expect(label, s, b, p, should_fail):
        nonlocal ok
        fails = parity_failures(s, b, p, lookup)
        good = bool(fails) == should_fail
        ok &= good
        print(f"  [{'ok ' if good else 'BAD'}] {label}: {'fails' if fails else 'passes'}"
              f"{' (' + fails[0] + ')' if fails else ''}")

    expect("shipped files", strategy_text, base_text, prefab_text, False)

    cut = strategy_text.replace(
        "        private bool oneThumbActive;",
        "        private const float OneThumbDriftTurnGain = 0.70f;\n        private bool oneThumbActive;", 1)
    expect("Round 12-15's 0.70 drift gain restored", cut, base_text, prefab_text, True)

    scaled = strategy_text.replace("left = oneThumbStick;", "left = oneThumbStick * 0.7f;", 1)
    expect("the mirror scaled in Reparameterize", scaled, base_text, prefab_text, True)

    weak = re.sub(r"return cubic \* [\d.]+f \+ t \* [\d.]+f;",
                  "return cubic * 0.1f + t * 0.8f;", strategy_text, count=1)
    expect("a touch curve that tops out at 0.9", weak, base_text, prefab_text, True)

    m = re.search(r"(^  _touchActionOverrides:\n.*?^  - InputEvent: 12\n    ShipActions:\n)"
                  r"(    - [^\n]*\n)", prefab_text, re.S | re.M)
    split = prefab_text
    if m:
        sharp_guid = next((g for g, pth in lookup.items() if pth.stem == "SquirrelSharpDriftAction"), None)
        if sharp_guid:
            first = re.search(r"guid: ([0-9a-f]{32})", m.group(2)).group(1)
            split = prefab_text[:m.start(2)] + m.group(2).replace(first, sharp_guid) + prefab_text[m.end(2):]
    expect("touch bound to a different drift asset than the pad", strategy_text, base_text, split, True)

    print("\nself-test OK" if ok else "\nself-test FAILED")
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true", help="fail the build if touch and pad drift differ")
    ap.add_argument("--sweep", action="store_true", help="print the thumb-deflection table")
    ap.add_argument("--self-test", action="store_true", help="prove each parity rule fires")
    args = ap.parse_args()

    strategy_text, base_text, prefab_text = read(STRATEGY), read(BASE_STRATEGY), read(SQUIRREL)
    lookup = guid_to_asset()

    if args.self_test:
        return self_test(strategy_text, base_text, prefab_text, lookup)

    yaw = scalar("YawScaler", prefab_text, "YawScaler")
    throttle = scalar("DefaultThrottleScaler", prefab_text, "DefaultThrottleScaler")
    tiers = drift_tiers(prefab_text, "_touchActionOverrides", DRIFT_TOUCH_EVENT, lookup)
    if not tiers:
        sys.exit("no drift actions found on the Squirrel's touch override")
    tier = next((t for t in tiers if t["sharp"]), tiers[0])
    curve = touch_curve(strategy_text)

    fails = parity_failures(strategy_text, base_text, prefab_text, lookup)
    print(f"Squirrel drift, touch vs pad  (YawScaler {yaw:g}, throttle scaler {throttle:g}, "
          f"touch curve {curve[0]:g}t + {curve[1]:g}t^3)")
    print(f"  drift tier (full pull)  : {tier['name']}  (Mult {tier['mult']:.3g}, "
          f"Grip {tier['grip']:.3g}) - same assets on both devices: {'yes' if not any('DIFFERENT' in f for f in fails) else 'NO'}")
    print(f"  full-lock yaw           : touch {touch_ease(2.0, curve) * yaw * tier['mult']:.1f} deg/s, "
          f"pad {pad_ease(2.0, base_text) * yaw * tier['mult']:.1f} deg/s")
    for f in fails:
        print(f"  [BAD] {f}")
    print()

    def corner(x_sum, target):
        om = x_sum * yaw * tier["mult"]
        trace = simulate(yaw, tier["mult"], tier["grip"], x_sum, target, seconds=CORNER_DEG / om)
        return om, max(sl for sl, _ in trace), min(v for _, v in trace), trace[-1][1]

    print(f"  full-lock {CORNER_DEG:g} deg hairpin at full pull (the shared drift - reported, not gated):")
    for xdiff in (0.5, 0.75, 1.0):
        target = xdiff * throttle
        _, peak, floor, end = corner(touch_ease(2.0, curve), target)
        print(f"    XDiff {xdiff:<4} target {target:5.1f} -> slowest {floor / target * 100:5.1f}%, "
              f"exit {end / target * 100:5.1f}% of entry speed (peak slip {peak:5.1f} deg"
              f"{', past 90' if peak >= SLIP_LIMIT_DEG else ''})")

    if args.sweep:
        print(f"\n  thumb deflection (XDiff 0.75, full pull, {CORNER_DEG:g} deg corner) - touch mirror vs "
              f"two pad sticks at the same deflection:")
        for d in (0.5, 0.6, 0.7, 0.8, 0.9, 1.0):
            target = 0.75 * throttle
            om, peak, floor, end = corner(touch_ease(2.0 * d, curve), target)
            pad_om = pad_ease(2.0 * d, base_text) * yaw * tier["mult"]
            print(f"    {d:<4} touch yaw {om:6.1f} deg/s (pad {pad_om:6.1f})  slowest "
                  f"{floor / target * 100:5.1f}%  exit {end / target * 100:5.1f}%  peak slip {peak:5.1f} deg")

    if args.check and fails:
        print("\nFAIL: the touch drift differs from the pad drift. A lift is a full trigger pull and "
              "nothing else - steering authority and drift tuning are the pad's.")
        return 1
    if args.check:
        print("\nOK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
