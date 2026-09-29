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
| Input (gamepad) | **Left trigger**, analog. `singleTriggerDrift: 1` on the prefab and ONE drift action bound, so **the drift amount is how far LT is pulled**: 0 = no drift, full pull = the action's full authored drift, linear in between (`GetTriggerSum` returns raw `LeftTriggerAnalog` when no sharp tier is bound) |
| Drift sound | `DriftAudioController.singleTriggerDepth: 1` on the prefab — the FMOD `Drift Amount` parameter follows the same LT pull (0 feathered → 1 buried), so the sound gets harder as the drift does; keyboard/touch read 1 |
| Input (touch) | `OnlyLeftStickAction (12)` → binary (full drift), smoothed by `DRIFT_EASE_SPEED` (12/s ≈ 83 ms ramp) so a tap still reads as an analog pull |
| Drift action | `SquirrelDriftAction` — at full pull rotation ×**1.8**, grip **0.25** (the old sharp tier's values; the old ×1.4 / 0.5 single tier now sits at ≈ half pull). `SquirrelSharpDriftAction` is no longer bound (2026-09-23) |
| Right trigger | **NOT free** — `RightStickAction (1)` is `SquirrelTubeAction` (touch: `OnlyRightStickAction (11)`). The Squirrel keeps its two-stick scissor throttle; do not propose a Scarab-style RT accelerator here |

Drift does two things at once: it **multiplies the rotation scalers** (you turn harder) and it
**lowers grip** (your momentum stops following your nose). Both ramp continuously with trigger
depth — there is no discrete "drift mode" and, since 2026-09-23, no tiers either: one action, scaled by the pull.

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

### 3.5 Minimum throttle has to END in a stop — the exponential never arrives

`StepTowardTarget` is an exponential lerp toward the throttle target, which is the right shape
everywhere except at the very bottom: **an exponential approaches zero and never lands.** A
two-thumb flier that authors no floor (`DefaultMinimumSpeed` 0 — Squirrel, Dolphin, Manta, Urchin)
therefore targets a genuine 0 when the pilot holds the scissor, and then tails off toward it
forever. From a boosted Dolphin's 347 u/s that tail is seconds long and a couple of hundred units
of travel, which is what *"it doesn't come to a stop"* looks like from the seat.

`MinimumThrottleBrake` owns that last stretch: **when the commanded target is zero**, the step
returns the LOWER of what the exponential reached and what a constant rate reached, so the speed
actually lands on 0.

**MIN, never SUM — and this is the part that is easy to get wrong.** Subtracting the constant rate
*from* the already-stepped value applies BOTH every frame, which measures **40% under the legacy
curve half a second into a Squirrel's stop**: not an end on the old deceleration but a different
one, on every affected hull — including the Squirrel, which was the reference for *correct*
behaviour when this was asked for. Taking the minimum instead lets the exponential win outright
while it is the stronger of the two, i.e. above `rate / LERP_AMOUNT` (**20 u/s** on a Squirrel,
22.7 on a Dolphin, 60 on a Manta), so the whole of the fall the pilot can see is bit-identical to
what shipped and the constant rate owns only the tail. `MinimumThrottleBrakeTests` pins both
halves: identical above the crossover, strictly stronger below it. The first cut of this shipped
the SUM while every test passed, because every test asserted that a stop HAPPENS — which a
wrongly-composed brake also satisfies.

Three properties make it safe to put in the shared step rather than per vessel:

- **It engages only on a ZERO target**, so two whole classes of vessel are untouched
  *structurally* rather than by tuning. Every **one-thumb** hull is out because
  `SingleStickVesselTransformer.ComputeThrottleTarget` is `ThrottleScaler * boost + MinimumSpeed`
  with no throttle axis in it and so can never be zero; the **Scarab** is out because it overrides
  `ComputeNoseAcceleration` wholesale and never reaches this step at all (it already brakes to a
  real stop through its own `coastDragPerSecond`). Any deceleration toward a lower-but-nonzero
  cruise is bit-identical to before, and so is accelerating away from a stop. A vessel that
  authors a non-zero `MinimumSpeed` therefore cannot be braked at all — which is why **the Rhino's
  `DefaultMinimumSpeed` went 10 → 0 in the same pass**: a floor is a speed the pilot cannot give
  back, so a two-thumb flier that is meant to be able to STOP cannot author one. See below for
  what that cost.
- **The rate is the vessel's OWN cruise** (`ThrottleScaler / minimumThrottleBrakeSeconds`, default
  2 s), not an absolute u/s — so a 180 u/s Manta and a 68 u/s Dolphin stop in the same *time*
  rather than the fast hull coasting three times as far.
- **It is in `StepTowardTarget`**, the one step BOTH flight models run through, so the no-drift
  identity of §3.2 is untouched and the scalar-model hulls get it too. In the vector model it
  brakes the NOSE component, which outside a drift *is* the whole speed (grip has already snapped
  the velocity onto the nose); inside a drift it stops feeding the slide without killing it, which
  is exactly right.

The fleet had already reached this answer twice, per vessel, for the same reason — the Scarab's
`coastDragPerSecond` and the Urchin's `detachSpeedDecayRate`, the latter authored in so many words
as a constant rate "rather than an exponential tail that never quite lands". This is that finding
promoted to the shared path instead of a third copy. The 2 s default is calibrated against the
Scarab: it sheds its 216 u/s ceiling at 120 u/s², ~1.8 s from the top, and 2 s of cruise lands a
full-boost Dolphin in ~1.85 s.

**General rule: a target a controller only ever APPROACHES is not a state the controller can
reach, so any target that is also a promise to the player ("minimum throttle means stopped") needs
a terminal approach that lands on it.**

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
  anything that would rather call than assign. The Squirrel's AI drifts in SkimRace, so this was a
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
| `Controller/Vessel/MinimumThrottleBrake.cs` | §3.5 — the terminal approach that makes a zero throttle target land on an actual stop (`MinimumThrottleBrakeTests`) |
| `Controller/Vessel/ScarabVesselTransformer.cs` | Acceleration policy only (integrator + ceiling + Snap Dash) — no flight model of its own |
| `_Prefabs/Spacevessels/Squirrel.prefab` | `vectorFlightModel: 1`, `driftOvershootCeiling: 1.25`, `driftThrottlePolicy: 0` (Live) |
| `_SO_Assets/VesselActions/Squirrel/SquirrelDriftAction.asset` | the one drift action — ×1.8 / grip 0.25 at full pull |
| `_SO_Assets/VesselActions/Squirrel/SquirrelSharpDriftAction.asset` | unbound since 2026-09-23 (safe to delete) |

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
| **Dolphin** | **scalar** | (`Locked`, not consulted) | `Dolphin.prefab` authors `vectorFlightModel: 0`, so its drift freeze is the SCALAR path's `holdSpeedWhileDrifting: 1` — the only vessel in the fleet that sets it — and its `driftThrottlePolicy: Locked` is inert (the policy is vector-only). Same outcome (speed pinned for the drift's duration, entering at speed costs nothing), different mechanism. `DOLPHIN_ENERGY_ECONOMY.md` §2a |
| Everyone else | scalar | — | Bit-identical to before the flag existed |

---

## 7. Tuning knobs

| Knob | Where | Value | Effect |
|---|---|---|---|
| `driftOvershootCeiling` | Squirrel.prefab | 1.25 | Max \|v\| during a drift, × the throttle target. 1 = no overshoot |
| `driftThrottlePolicy` | Squirrel.prefab | Live (0) | Whether thrust acts during a drift. `Locked` (the Dolphin) = no acceleration for the drift's duration |
| `Mult` / `driftDamping` | `SquirrelDriftAction` | 1.8/0.25 | Rotation multiplier and grip at full trigger pull |
| `DefaultThrottleScaler` | Squirrel.prefab | 60 | Scissor throttle's speed scale |
| `RotationThrottleScaler` | Squirrel.prefab | 0 | Turn rate vs speed — **deliberately 0** |
| `minimumThrottleBrakeSeconds` | every vessel | 2 | §3.5. Seconds to shed one cruise once the target is ZERO. 0 restores the legacy exponential tail |

---

## 8. In-editor verification

1. **Drift recovery (the point).** SkimRace or freestyle. Get to speed, hold LT into a hard drift so
   the course visibly separates from the nose, then **aim the nose out of the slide and squeeze the
   throttle**. The vessel must pull ONTO the nose direction. Before this change it accelerated
   further along the slide.
2. **The identity (the one that must be seen).** Fly with no drift at all — accelerate, brake, turn
   hard, take a danger-prism slow, ride the tube. It must feel *exactly* as it does on `main`. This
   is the claim the whole change rests on; §3.2 proves it in arithmetic, but it has to be seen.
3. **Analog depth.** Feather LT: convergence should loosen continuously with pull depth, from none at rest to full at a buried trigger.
4. **Overshoot binds, but never brakes.** (a) From cruise, hold a long clean drift at full
   throttle: speed may rise above the straight-line cruise and must plateau at 1.25×; drop
   `driftOvershootCeiling` to 1 and confirm the plateau disappears. (b) **The regression that
   shipped once:** enter a drift at BOOST speed. Speed must decay smoothly toward the cruise
   target — it must NOT snap down on the first drift frame, and the scissor throttle must not
   read as a speed dial while drifting.
5. **AI drift.** SkimRace, watch an AI approach a crystal. At drift entry its trail must continue
   toward the crystal while the hull swings off-axis. If the trail follows the nose instead, the
   Course re-aim in `SyncExternalWrites` regressed.
6. **Danger prism while drifting.** Clip a danger prism mid-drift — the slow must land.
7. **Vessel swap.** Menu freestyle → vessel changer → Squirrel at speed. The new hull inherits the
   speed rather than dropping to a stop.
8. **Minimum throttle stops the vessel (§3.5, UNFLOWN).** Menu freestyle, on each two-thumb hull in
   turn — **Dolphin**, Squirrel, Manta, Urchin. Hold the throttle scissor (both sticks full
   horizontal, opposite directions; keyboard **L + D**) from cruise: the vessel must reach a
   genuine standstill in **1.40 s**, and from a full boosted 347 u/s in **2.47 s** (both measured
   off the shipped composition, not estimated). Then check the two things a brake can
   get wrong: it must read as *settling*, not as hitting a wall, and throttling back up from the
   stop must be immediately responsive rather than feeling like a stall.
   - **Decelerating to a lower cruise is NOT braked** — ease the scissor to a mid throttle from
     top speed and confirm that fall feels exactly as it always did. Only a *minimum* throttle
     brakes.
   - **The Rhino is in the list now** — its `DefaultMinimumSpeed` went 10 → 0, so it stops like
     the rest. Its cruise is correspondingly 50 rather than 60 and its ramp top 1200 rather than
     1210; both readouts are worth a glance, and `HEADLONG.md` §2 carries the re-derived tables.
   - **Watch the COUNTDOWN on the three Rhino modes** (Astro League, Peel the Cage, Headlong).
     A paused `InputController` returns before writing `XDiff`, so the value simply holds — if
     it holds 0 (a fresh `ResetInput`/`ResetForReplay` before any AI write) the target is now
     `MinimumSpeed` 0 rather than 10, and the brake brings the hull to a dead stop during the
     countdown where it used to drift at 10 u/s. That is what the other four two-thumb hulls have
     always done, so it is a consistency change rather than a regression — but it is the one place
     the floor removal is visible outside the pilot's own throttle, and it has not been flown.
     Menu freestyle is NOT exposed: the AI writes a non-zero `XDiff` before handing over, so the
     enter-freestyle camera blend still cruises forward exactly as before.
     The thing to watch for is a **Headlong** lap feeling different — it should not: measured over
     1,600 generated circuits the gate positions are bit-identical and the corner ladder
     (99/82/65/37% of top speed) is unchanged to the digit, because the only thing that number
     fed was a safety floor that never binds.
   - **If the Dolphin's throttle still does nothing after a drift**, the cause is not this: it is
     the only vessel in the fleet with `holdSpeedWhileDrifting: 1`, and that latch pins the cruise
     speed for the drift's duration and releases on the drift's RELEASE edge. A missed release
     leaves the throttle dead at whatever speed was captured, which reads as the same complaint.
     `IsDriftSpeedHeld` is the thing to watch.

---

## 9. Follow-ups

- No edit-mode test guards the §3.2 identity — the model lives on a MonoBehaviour with a live
  vessel, so it is not reachable from `Assembly-CSharp-Editor` without a harness. If the flight
  math is ever factored into a pure static (the natural shape: `StepTowardTarget` + a grip/thrust
  step over `(velocity, forward, target, dt)`), that test becomes cheap and should be written.
- The remaining scalar-path vessels have the same latent defect wherever they drift. Manta is the
  live case (two-trigger drift, `singleTriggerDrift: 0`); flipping its flag is a one-line change
  plus a feel pass, deliberately not taken in this branch.
