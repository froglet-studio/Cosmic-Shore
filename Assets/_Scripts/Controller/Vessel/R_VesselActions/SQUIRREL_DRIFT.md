# Squirrel — the drift (and the vector flight model behind it)

The Squirrel is the racer: *"vaporwave arcade racer, tube-riding along player-generated trails
(F-Zero / Redout feel)"*. Drift is half its identity — the other half is the tube — and until
2026-08-15 the drift was **structurally unable to feel like driving**, for a reason that was in the
base transformer rather than in any of the Squirrel's own tuning.

This document is the standing record for the drift: what it is, the defect that shipped with it,
the vector flight model that fixes it, and the numbers.

---

## 1. What the drift is

| | |
|---|---|
| Input (gamepad) | **Left trigger**, analog. `singleTriggerDrift: 1` on the prefab, so LT's 0→1 travel is remapped across the whole 0→2 range: no-drift → single → sharp on one trigger |
| Input (touch) | `OnlyLeftStickAction (12)` → binary, smoothed by `DRIFT_EASE_SPEED` (12/s ≈ 83 ms ramp) so a tap still reads as an analog pull |
| Tier 1 | `SquirrelDriftAction` — rotation ×**1.4**, grip **0.5** |
| Tier 2 | `SquirrelSharpDriftAction` — rotation ×**1.8**, grip **0.25** |
| Right trigger | **NOT free** — `RightStickAction (1)` is `SquirrelTubeAction` (touch: `OnlyRightStickAction (11)`). The Squirrel keeps its two-stick scissor throttle; do not propose a Scarab-style RT accelerator here |

Drift does two things at once: it **multiplies the rotation scalers** (you turn harder) and it
**lowers grip** (your momentum stops following your nose). Both ramp continuously with trigger
depth — there is no discrete "drift mode", which is why the tiers interpolate rather than switch.

**Throttle is the two-stick scissor**: `XDiff = (rightStick.x − leftStick.x + 2) / 4`, linear, no
deadzone, **resting at 0.5** (`GamepadInputStrategy.cs`). Note `BaseInputStrategy.ResetInput` zeroes
`XDiff` to 0, not 0.5. Target speed is `XDiff × ThrottleScaler(60) × ThrottleScalerMultiplier ×
boost + MinimumSpeed(0)`.

`RotationThrottleScaler: 0` on the Squirrel — **its turn rate does not degrade with speed**. That is
a deliberate racer lever and is adjacent to everything here; do not change it silently.

---

## 2. The defect (fixed 2026-08-15)

`VesselTransformer.MoveShip` integrated `position += speed * VesselStatus.Course`, and
`ComputeThrottleTarget()` produced a **scalar** that knew nothing about `transform.forward`.

Outside a drift that is fine, because `Course == forward`. **Inside a drift they are different
vectors**, and a scalar can only push along one of them — Course. So the engine pushed along the
**slide**: squeezing the throttle mid-drift dug you deeper into it instead of pulling you out.

That is why the drift read as *ice* rather than as *driving*. It was not a tuning problem — no
value of grip, rotation multiplier or throttle scaler can fix a thrust vector that points the wrong
way. It needed a second vector.

---

## 3. The fix — the vector flight model

`VesselTransformer` now carries two flight models, selected per vessel by `vectorFlightModel`
(default **off**; every vessel not listed in §6 is untouched).

Under the vector model the transformer integrates a world-space `_velocity`, and `Speed` / `Course`
are **derived** from it rather than being the primitives:

```
1) GRIP    momentum rotates toward the nose   (convergence 1 outside a drift)
2) THRUST  velocity += forward * ComputeNoseAcceleration(dt)      ← along the NOSE, always
3) SHAPE   magnitude policy (drift overshoot ceiling)
4) PUBLISH speed = |v| ;  Course = v/|v| ;  position += (speed*mult*Course + velocityShift) * dt
```

**Aiming out of a slide and squeezing is now how you recover**, which is what a racer's drift is
supposed to be.

### 3.1 Order is load-bearing: grip BEFORE thrust

Thrust-then-grip leaves `|v| = √(s² + d² + 2sd·cosθ)` on a frame where the nose turned by θ, which
is not the scalar model's `s + d`. The no-drift equivalence would then hold only while flying dead
straight and drift whenever the vessel turned — measured at **0.40 u/s** at an 8°/frame turn.
Resolving grip first makes `v` exactly `forward·s` before thrust is measured, so the equivalence is
unconditional. It is also the more honest physics: this frame's thrust should not itself be rotated
by this frame's grip.

### 3.2 The identity — why this needed no fleet retune

**Outside a drift the two models are the same computation.** Grip forces `v = forward·s`, so
`dot(v, forward) == |v| == speed`; the nose step `v += forward·(step(speed,target) − speed)` leaves
`|v| = step(speed,target)`, exactly what `AdvanceSpeed` writes; `Course = v/|v| = forward`, exactly
what the scalar branch writes; and the position integration is the same line. Both paths call the
same `StepTowardTarget`, so this is one shared function rather than two implementations that happen
to agree.

Verified numerically over 4000 frames at 60 Hz with a wandering scissor throttle, periodic
throttle-multiplier slows, and turn rates from 0 to 8°/frame:

| turn | max \|Δspeed\| | max \|ΔCourse\| | max \|Δposition\| |
|---|---|---|---|
| 0°/frame | 0 | 0 | 0 |
| 0.5°/frame | 4.3e-14 | 2.2e-16 | 1.3e-13 |
| 2°/frame | 5.7e-14 | 2.2e-16 | 6.0e-14 |
| 8°/frame | 5.7e-14 | 2.2e-16 | 1.7e-14 |

Double-precision noise. **The flag changes behaviour only inside the drift window** — which is why
turning it on for one vessel is not a balance event for anything else that vessel does.

### 3.3 Grip is frame-rate independent now

The scalar path's convergence was `Grip * dt` used directly as a Slerp fraction. The vector path
uses `1 − e^(−Grip·dt)`. At 60 fps and the Squirrel's authored grip the two differ by ~0.4%, so this
does not perturb the tuning; it stops a frame-rate drop from loosening the back end. It applies only
inside the drift window, so it cannot touch §3.2.

### 3.4 Drift overshoot — a new speed payoff, bounded (and it must never brake)

Vector addition means momentum + nose-thrust can push `|v|` **above** the throttle target during a
drift. The scalar model could not produce that, and for a racer it is desirable: a clean line
through a drift should pay. `driftOvershootCeiling` (**1.25**, authored per vessel) bounds it. The
ceiling is only consulted while drifting, which is what keeps §3.2 exact.

**The ceiling takes the pre-thrust speed as a floor, so it bounds GAIN and never brakes.** The first
version clamped to `ComputeThrottleTarget() × 1.25` outright, which looks equivalent and is not: a
vessel that *entered* the drift fast gets slammed down to its current cruise target on the first
drift frame. That shipped for one round and produced two reported symptoms on the Dolphin — a large
instant speed loss on drift entry, and the throttle appearing to control speed *during* the drift
(the ceiling tracks `XDiff`, so the scissor moved the clamp). Measured on the Squirrel entering a
drift at a boosted 180 u/s against a 60 u/s cruise target:

| | frame 1 | frame 30 | frame 180 |
|---|---|---|---|
| clamp-to-target (broken) | **75.0** | 69.1 | 75.0 |
| floor at pre-thrust speed (shipped) | 177.0 | 122.4 | 110.7 |

Momentum carried in now bleeds off through `ComputeNoseAcceleration`, which targets the throttle
target and therefore produces *negative* acceleration when you are above it — deceleration belongs
to the throttle policy, not to a clamp. Entering a drift at cruise still binds exactly as intended:
peak `1.25 × 60 = 75.0`, verified.

---

## 4. Constraints this had to respect (each was a real trap)

- **The AI's `Course` write survives.** `AIPilot.cs:339` does `VesselStatus.Course = desiredDirection`
  at drift entry, and that write **is** the AI's drift: the course locks on the objective while the
  nose swings away, which is how a drifting AI lays trail, skims and fires along an axis that is not
  its heading (`ECOSYSTEM.md §27.7`, `RAMPAGE.md`). The scalar path honours it for free by reading
  Course back and slerping from it. A vector model that derived Course purely from its own state
  would overwrite the AI every frame and the manoeuvre would silently stop working — which is what
  the Scarab's first-pass transformer did. `SyncExternalWrites` detects a Course written by anyone
  else and re-aims the velocity vector onto it, symmetrically with how `speed` writes are already
  detected. **AIPilot needed no change**, and `SetCourseVelocity(dir)` is the explicit door for
  anything that would rather call than assign. The Squirrel's AI drifts in HexRace, so this was a
  blocker, not a nicety.
- **The damage channels stay live.** `throttleMultiplier` (impact slows) and `velocityShift`
  (knockback / AOE) are applied in the vector path exactly as in the scalar one, and
  `ApplyThrottleModifiers` / `ApplyVelocityModifiers` still run every frame regardless of drift.
  Freezing either during a drift would make a drifting vessel immune to danger prisms — a
  LOCKED-design violation hiding inside a feel change.
- **`_speedTrackingRate` is not consumed.** The Rhino's ramp boost latches it via
  `SetSpeedTrackingRate` and a mid-ramp boost must resume, so `StepTowardTarget` clears it only on
  landing and leaves it alone otherwise. The Rhino stays on the scalar path.
- **Replication is unchanged.** `n_Speed` / `n_Course` are owner-write, pushed every frame and
  mirrored on non-owners; a more-divergent Course goes over the wire verbatim. The transformer does
  not run on non-owners at all (`VesselController` calls `ToggleActive(false)` for
  `IsNetworkClient`), so there is no second writer.

---

## 5. Files

| File | Role |
|---|---|
| `Controller/Vessel/VesselTransformer.cs` | Both flight models; `vectorFlightModel`, `driftOvershootCeiling`, `driftThrottlePolicy`, `Grip`, `StepTowardTarget`, `ComputeNoseAcceleration`, `ShapeSpeed`, `SyncExternalWrites`, `SetCourseVelocity` |
| `Controller/Vessel/ScarabVesselTransformer.cs` | Acceleration policy only (integrator + ceiling + Snap Dash) — no flight model of its own |
| `_Prefabs/Spacevessels/Squirrel.prefab` | `vectorFlightModel: 1`, `driftOvershootCeiling: 1.25`, `driftThrottlePolicy: 0` (Live) |
| `_SO_Assets/VesselActions/Squirrel/SquirrelDriftAction.asset` | tier 1 — ×1.4 / grip 0.5 |
| `_SO_Assets/VesselActions/Squirrel/SquirrelSharpDriftAction.asset` | tier 2 — ×1.8 / grip 0.25 |

`DriftDamping` was renamed to **`Grip`** (`[FormerlySerializedAs]` migrates the prefabs). It is what
the field has always meant: the rate at which momentum rotates back onto the nose. It is a
`[HideInInspector] public` runtime mirror written every frame by `ApplyAnalogDrift` — prefab-
serialized values are stale garbage, exactly like `ThrottleScaler`.

---

## 6. Fleet status

| Vessel | Model | Policy | Notes |
|---|---|---|---|
| **Squirrel** | vector | Live | This document. Throttle semantics unchanged — only the direction of thrust |
| **Scarab** | vector | Live (own policy) | Integrator throttle; overrides `ComputeNoseAcceleration` + `ShapeSpeed` |
| **Dolphin** | vector | **Locked** | Drift freezes the velocity vector outright (grip 0 + zero thrust), so entering a drift at speed costs nothing. `DOLPHIN_ENERGY_ECONOMY.md` §2a |
| Everyone else | scalar | — | Bit-identical to before the flag existed |

---

## 7. Tuning knobs

| Knob | Where | Value | Effect |
|---|---|---|---|
| `driftOvershootCeiling` | Squirrel.prefab | 1.25 | Max \|v\| during a drift, × the throttle target. 1 = no overshoot |
| `driftThrottlePolicy` | Squirrel.prefab | Live (0) | Whether thrust acts during a drift. `Locked` (the Dolphin) = no acceleration for the drift's duration |
| `Mult` / `driftDamping` | drift action SOs | 1.4/0.5, 1.8/0.25 | Rotation multiplier and grip per tier |
| `DefaultThrottleScaler` | Squirrel.prefab | 60 | Scissor throttle's speed scale |
| `RotationThrottleScaler` | Squirrel.prefab | 0 | Turn rate vs speed — **deliberately 0** |

---

## 8. In-editor verification

1. **Drift recovery (the point).** HexRace or freestyle. Get to speed, hold LT into a hard drift so
   the course visibly separates from the nose, then **aim the nose out of the slide and squeeze the
   throttle**. The vessel must pull ONTO the nose direction. Before this change it accelerated
   further along the slide.
2. **The identity (the one that must be seen).** Fly with no drift at all — accelerate, brake, turn
   hard, take a danger-prism slow, ride the tube. It must feel *exactly* as it does on `main`. This
   is the claim the whole change rests on; §3.2 proves it in arithmetic, but it has to be seen.
3. **Analog depth.** Feather LT: convergence should loosen continuously, not snap between tiers.
4. **Overshoot binds, but never brakes.** (a) From cruise, hold a long clean drift at full
   throttle: speed may rise above the straight-line cruise and must plateau at 1.25×; drop
   `driftOvershootCeiling` to 1 and confirm the plateau disappears. (b) **The regression that
   shipped once:** enter a drift at BOOST speed. Speed must decay smoothly toward the cruise
   target — it must NOT snap down on the first drift frame, and the scissor throttle must not
   read as a speed dial while drifting.
5. **AI drift.** HexRace, watch an AI approach a crystal. At drift entry its trail must continue
   toward the crystal while the hull swings off-axis. If the trail follows the nose instead, the
   Course re-aim in `SyncExternalWrites` regressed.
6. **Danger prism while drifting.** Clip a danger prism mid-drift — the slow must land.
7. **Vessel swap.** Menu freestyle → vessel changer → Squirrel at speed. The new hull inherits the
   speed rather than dropping to a stop.

---

## 9. Follow-ups

- No edit-mode test guards the §3.2 identity — the model lives on a MonoBehaviour with a live
  vessel, so it is not reachable from `Assembly-CSharp-Editor` without a harness. If the flight
  math is ever factored into a pure static (the natural shape: `StepTowardTarget` + a grip/thrust
  step over `(velocity, forward, target, dt)`), that test becomes cheap and should be written.
- The remaining scalar-path vessels have the same latent defect wherever they drift. Manta is the
  live case (two-trigger drift, `singleTriggerDrift: 0`); flipping its flag is a one-line change
  plus a feel pass, deliberately not taken in this branch.
