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
| Input (touch) | **Right-thumb lift = a full trigger pull** (every touch device, iOS and Android; ported from the Android strip branch 2026-10-05): lifting the right thumb raises `OnlyLeftStickAction (12)`, which the Squirrel's touch override binds to the drift. Glass measures no trigger travel, so the drift takes the BINARY fallback every non-gamepad input gets (`VesselTransformer.GetTriggerSum`: a gamepad reads its analog trigger; every other device drifts at a full pull). The left thumb flies alone - mirrored onto both sticks at FULL authority (one thumb at the rim commands what two full pad sticks command), pitch and yaw only, re-zeroed where it rests so the lift does not yank the vessel; the drift's own `Mult` then turns it sharper exactly as on a pad, and the hull follows at `touchNoseResponse` (9; local human pilot only - AI and autopilot keep the fleet's 1.5) through the drift as out of it. Putting the thumb back ends the drift; binary drifts ease in and out over `DRIFT_EASE_SPEED`. **The touch drift is the pad drift with the analog trigger replaced by a full pull** - `Tools/Build/touch_drift_slip.py --check` fails if they differ (same drift assets bound on both overrides, no one-thumb gain, the same full-deflection authority; `--self-test` proves each rule fires). A full-lock 180° hairpin at full pull scrubs ~7% on BOTH devices (reported, not gated: it is the drift action's tuning, `Mult`/`driftDamping`). The strip branch once cut the mirror to 0.70 (`OneThumbDriftTurnGain`) to stop that scrub on touch alone - a steering cut a pad pilot never had, since retired, as were a two-thumb overdrive (out of reach from cruise) and a depth-from-the-steering-thumb (the slide changed under the pilot as they steered) |
| Input (AI) | `SkimRacePilot` asks `TryGetInputForAction<DriftActionSO>` at each press — it answers for the hull's ACTIVE device (12 on touch, 2 on every PC device) — and holds the left trigger at **full pull** (`DriftTriggerPull = 1`) while the drift is held, so an AI drifts at the tier's full authored depth on every device. See §10 |
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
   Course re-aim in `SyncExternalWrites` regressed. ⚠ **Corrected 2026-10-06:** this step cannot
   pass as written. In Skim Race the Squirrel AI seats belong to `SkimRacePilot`, not `AIPilot`,
   and every shipped `SkimRaceAIConfig*.asset` has `UseDrift: 0`, so no AI in Skim Race ever asks
   to drift; the `AIPilot` course-lock drift this step describes runs in the OTHER modes a Squirrel
   AI flies. To see the Skim Race pilot drift, follow §10's verification instead.
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

---

## 10. AI drift: which control, and how deep (2026-10-06)

**The defect.** An AI Squirrel in Skim Race could never drift — nor lay a Boost Ring — on a PC.
`Squirrel.prefab` binds both abilities ONLY in its `R_VesselActionHandler` device-override maps
(touch: drift on `OnlyLeftStickAction (12)`, ring on `OnlyRightStickAction (11)`; pad: drift on
`LeftStickAction (2)`, ring on `RightStickAction (1)`; the shared map is empty).
`TryGetInputForAction<T>` swept shared → touch → gamepad regardless of device, so it handed the
pilot the TOUCH controls, and on a PC — `GetActiveOverrides` resolves Gamepad, Keyboard, DualMouse
and MouseKeyboard all against the pad overrides — `PerformShipControllerActions(12)` was refused at
`HasAction`. An AI player's `ActiveInputDevice` is decided once, by its own `InputController`, from
the host's hardware: Gamepad with a pad connected, Keyboard without, Touch on a handheld. So only a
handheld host ever drifted.

**The fix (lookup).** `TryGetInputForAction` / `TryGetBoundAction` now resolve with the same rule a
press does — `R_VesselActionHandler.TryGetPressedActions`: the ACTIVE device's override map first,
then the shared entries that map does not shadow. One function serves the press gate, the press and
the lookup, so a control the lookup hands out is by construction one the press accepts. An ability
bound only for ANOTHER device is deliberately not a fallback (that control does nothing here, or
fires whatever the shared map puts on it). `CollectBoundActions` / `HasBinding` — the HUD's
all-devices view — are untouched. Every other caller (`AIPilot`'s aim telegraph, Tollway, Waystation,
the Butterfly mode driver, and the AI boost policies' `CreateDriver` for the Sparrow, Serpent and
Dolphin) binds its ability in the shared map, so its answer is unchanged; `SkimRingAIPolicySO` asks
for the Squirrel's ring, which is bound in BOTH override maps, so it is found on every device as it
was before (the policies start the returned action themselves rather than pressing its control).
`SkimRacePilot` now asks at every press instead of caching the first answer for the match, and
releases on the control its own press used. Pinned by `DeviceAwareActionLookupTests`, which also
reads the shipped prefab and asserts both abilities are pressable on all five devices.

**The depth (a deliberate choice).** With the press accepted, the drift's depth still depended on
the device: on a PAD `VesselTransformer.GetTriggerSum` scales the drift by `LeftTriggerAnalog`, and
an AI has no physical trigger, so a pad-device AI drifted at depth 0 — inert, while still reporting
`IsDrifting` — and a keyboard-device AI at full depth. **An AI drift is now full depth on every
device**: `SkimRacePilot` writes `LeftTriggerAnalog = 1` (`DriftTriggerPull`) every frame its drift
is held and 0 otherwise, as one more input channel beside the sticks. Reasons:
- full pull is the only depth every device agrees on — keyboard, mouse and touch already treat a
  started drift as full depth, so a partial pull would only ever take effect on a pad;
- the race must not depend on whether a controller is plugged into the host;
- the driver's drift decision is on/off (enter past `DriftEnterDegrees`, leave under
  `DriftExitDegrees`), and on/off means the tier's authored drift;
- a buried trigger is a human input, inside the input-only contract (`Docs/SKIM_RACE_AI.md` §3).

It is a constant, not a config field, for the first reason. Written every frame (not once at the
press) so a neutral frame — a stationary hull — cannot leave a held drift at depth 0 when flight
resumes. `LeftTriggerAnalog` replicates (`n_lTrig`), so remote peers' drift audio
(`DriftAudioController.singleTriggerDepth`) reads the same buried trigger.

**What this does NOT change: the shipped policy never asks to drift.** Every
`SkimRaceAIConfig*.asset` ships `UseDrift: 0` and `UseLaunchRing: 0` (the C# defaults). That "off"
was never measured against a working drift: the offline simulator does not model drift at all, and
an in-editor benchmark on a PC had its drift and ring presses refused. Whether either should be on
is a tuning question for the benchmark (`Docs/SKIM_RACE_AI.md` §7), not something this fix decides.

**Verification (NOT EDITOR-VERIFIED at the time of writing).** Out of editor: the shipped
`DeviceAwareActionLookupTests` were compiled with Roslyn against the shipped lookup methods and RUN
against the real `Squirrel.prefab` (8/8, with three negative controls each failing the test aimed at
it), and a harness drove the shipped `SkimRacePilot` actuation through the shipped press path on all
five devices — the pre-fix code reproduced the report exactly (drift and ring start on Touch only),
the fixed code drifts, holds the trigger at 1, releases it to 0 and lays the ring on every device.
In the editor:
1. Set `UseDrift: 1` on the config the intensity you play reads (`SkimRaceAIConfig_I1` for I1) —
   a test edit, do not commit it.
2. Skim Race with AI seats, **no pad connected**: an AI drifts on sharp heading changes — the hull
   swings off its travel direction and its trail curves; `IsDrifting` toggles on the AI's
   `VesselStatus`.
3. Same with a **pad connected** (the AI's device becomes Gamepad): the same drift at the same
   depth — with the AI's `InputStatus.LeftTriggerAnalog` reading 1 while it drifts. Before the fix
   no drift started at all; with the lookup fixed but no trigger write, this run would show
   `IsDrifting` with no visible drift (depth 0).
4. HUD unchanged: your own Squirrel's control chips and ability row read exactly as before, on pad
   and on keyboard.
5. Revert step 1.

---

## 11. What a press runs on the OTHER machines (2026-10-06)

§10 made the autopilot's lookup agree with the press gate on ONE machine. This is the cross-machine
half: whether the same press runs the same actions on every peer. It is fleet-wide (the Squirrel is
one of three hulls it hit), recorded here beside §10 because the Squirrel's override-only drift is
what surfaced it.

**The defect.** A press replicates by RE-EXECUTION: owner → `SendButtonPressed_ServerRpc` →
`SendButtonPressed_ClientRpc` → every peer resolves the pressed INPUT to actions itself, against
`R_VesselActionHandler`'s device override maps (Touch reads `_touchActionOverrides`; Gamepad,
Keyboard, DualMouse and MouseKeyboard read `_gamepadActionOverrides`). The device came from
`InputStatus.ActiveInputDevice`, which was the one `InputStatus` field that did not replicate — and
every peer runs `InputController.Initialize` for every player (`Player.OnNetworkSpawn`), which picks
a strategy from THAT machine's hardware (`SystemInfo.deviceType == Handheld` → Touch). So a phone saw
a PC pilot as Touch and a PC saw a phone pilot as Keyboard, and each resolved the remote press
against the wrong map. Measured from the shipped prefabs by `Tools/Build/peer_press_harness`, which
compiles the real handler and routes its RPCs between an owner copy and a peer copy: of 825 presses
on the 2026-10-08 tree (13 vessels × owner device × what the peer thinks the device is × bound input), **72 ran something
different on the peer**, all on the three hulls that author device overrides:

| Hull | Owner → peer | What diverged |
|---|---|---|
| Squirrel | PC → phone, phone → PC | Drift + `DriftTrailAction` (2 / 12) and the Boost Ring (1 / 11) **refused** on the other side — no drift, no drift-trail prisms, no ring |
| Manta | phone → PC | **A different ability ran**: a touch pilot's both-thumbs boost (13) ran `BoostAction` locally and `MantaAnalogTurnBoostAction` on the PC; one-thumb yaw (11 / 12) likewise |
| Rhino | PC → phone | Shield swipes (1 / 2) refused on the phone |

The same 72 for an AI's `PerformShipControllerActionsReplicated` (the server's hardware decides the
AI's device). Every hull without overrides is unaffected. The prismscape diverged with it, plus a
second defect on the same path: **a release resolved against the device at RELEASE time**, so a
device switch mid-hold (a phone pilot picking up a pad) released against the other map — 48 cases
stranded the held ability, and on the Manta the release stopped an ability that was never started.

**The fix — two halves, because each closes a gap the other cannot.**

1. **`InputStatus.ActiveInputDevice` is an owner-write NetworkVariable** (`n_device`), the same
   pattern as every other field there. Every reader on a replica now sees the OWNER's device, which
   matters well beyond the action maps: `MantaAnalogTurnBoostExecutor` shapes the Yastri trail
   (`SetTurnTrail`) only for pad/keyboard pilots; `VesselTransformer.GetTriggerSum` reads trigger
   depth as analog on a pad and full pull otherwise, and eases non-pad input; `DriftAudioController`
   does the same for the drift sound. The trigger analogs those interpret already replicated; the
   device that says how to interpret them did not.
2. **The press and release RPCs carry the device** the owner resolved with, as one byte
   (`(byte)InputDeviceType`, or `R_VesselActionHandler.NoDevice` = 255 when the vessel has no pilot
   input), and every peer — the owner's own copy included — resolves with THAT. Needed because a
   NetworkVariable and an RPC are not ordered against each other (different objects — the device
   lives on the Player, the press on the vessel — and a variable's delta goes out at the network
   tick while an RPC goes out at once), so the first press after a device switch would otherwise
   reach peers ahead of the switch. The press ledger (`_heldInputs`) now records the device each
   held input was pressed with: a release resolves with the press's device, so it stops what the
   press started; and the owner's release carries that device, for a peer that never ran the press
   (joined mid-hold).

**A parallel fix for the release half, superseded in the merge.** `7e5c950e4` ("a release stops
the actions its press started", bleeding-edge 2026-10-08) attacked the same release defect from the
other side: it recorded each press's resolved action LIST in a second ledger (`_startedActions`) and
stopped that list on release. But the release still ran the `HasAction` gate against the LIVE device
first, so an input the new device does not bind at all was refused before the record was ever read.
Measured with `peer_press_harness --rev origin/bleeding-edge` on that handler: stranded holds 48 →
24 (the Manta's fixed; the Squirrel's ring and drift and the Rhino's swipes still stuck), late-joiner
releases still 48 wrong, and the cross-peer 72 untouched. This branch keeps ONE ledger, the device
record above, because the device is what the wire needs anyway (a release carries it to a peer that
never ran the press); the merge removed `_startedActions` rather than leave two bookkeepers for one
release.

**Rejected:** carrying the resolved action LIST (needs an action-index scheme every peer agrees on,
costs more than a byte per press, and still leaves the executors' own device reads divergent); the
NetworkVariable alone (the first press after a switch races it, and the release mismatch remains);
the RPC alone (fixes the maps, leaves the Manta trail and the drift depth simulating the wrong
device on every replica). Bandwidth: +1 byte per press and per release; the variable changes only
when a pilot switches device.

**What changes on screen beyond the fix.** A replica now simulates the owner's device, not the
watching machine's: a PC watching a pad pilot's Squirrel reads that pilot's analog trigger depth
(it read full pull when the PC had no pad); a phone watching a pad Manta now runs its trigger turn
and boost exactly as a PC peer always did. An AI's device is the host's on every machine. Surfaces
gated on the LOCAL pilot (`VesselTransformer.IsLocalHumanTouchPilot`,
`ShieldSwipeActionExecutor.IsLocalAnalogPilot`) are unchanged. No asset changed.

**Files.**

| File | Change |
|---|---|
| `Controller/IO/InputStatus.cs` | `n_device`; `ActiveInputDevice` follows the owner-write pattern |
| `Controller/Vessel/R_VesselActionHandler.cs` | RPCs carry `byte device`; `StartPressedActions` / `StopPressedActions` resolve with it; `_heldInputs` is `input → device`; `CurrentDevice`, `PressedDevice`, `NoDevice`, `OverridesForCarried` |
| `Tests/Editor/CarriedInputDeviceTests.cs` | the byte encoding: every device fits and round-trips, the sentinel collides with none, an unknown byte resolves to the shared map |
| `Tools/Build/peer_press_harness/` | the two-machine harness; `--rev <git-rev>` reproduces the defect on an older handler, `--self-test` removes each mechanism above and requires a failure |

**Verification.** NOT EDITOR-VERIFIED: no editor and no `unity` CLI in the authoring session, so
`/verify-unity` did not run. Out of editor: `Tools/Build/unity_refcompile/run.sh` compiled the
branch against the real Netcode 2.5.0 source and Unity reference assemblies with 0 errors in project
code (negative control: a wrong-arity call planted in the ClientRpc fails it with CS7036, tagged as
a changed file). `peer_press_harness` against the 2026-10-08 prefabs: the pre-fix handler
(`--rev 40c9b744`) runs 72 of 825 presses differently on the peer (and 72 AI presses), strands 48
holds and stops the wrong thing on 48 late-joiner releases; bleeding-edge's handler 72 / 24 / 48;
this branch: 0 everywhere, plus the 4 shipped
`CarriedInputDeviceTests` run green; `--self-test` catches all three removals. What none of that
covers: Netcode delivery itself, the variable's replication, and what an executor DOES with a press
— those need two real machines.

In the editor (the Touch half needs a phone build: `InputController` keys Touch on
`UnityEngine.SystemInfo.deviceType`, which reads Desktop in the editor and in every MPPM player; the
Device Simulator overrides only `UnityEngine.Device.SystemInfo`):
1. Edit-mode tests: `CarriedInputDeviceTests` (4) and `DeviceAwareActionLookupTests` pass.
2. **Phone build + PC host, both on Squirrels.** PC pilot drifts (left trigger or Left Shift): on
   the phone the PC Squirrel slides AND lays its drift trail — before the fix the trail never
   appeared there. Phone pilot lifts the right thumb to drift: the PC sees the trail. Each pilot's
   Boost Ring appears on the other machine. Prism counts in the drift trail match on both screens.
3. **Same pair on Mantas.** Phone pilot lifts both thumbs (boost): the PC sees a straight boost, not
   a trigger turn. PC pilot (pad) holds one trigger: the phone sees the Yastri flared trail.
4. **Same pair on Rhinos.** PC pilot swipes the shield (pad triggers): the phone shows the swipe.
5. **Mid-hold switch** (phone with a Bluetooth pad): hold a touch drift, touch the pad, let go — the
   drift ends on BOTH screens (before: it could stay engaged).
6. MPPM / two desktop players (no Touch available): a match plays exactly as before — presses, AI
   abilities (Tollway, Waystation, Butterfly modes, Skim Race AI drift per §10), no console errors.

**Follow-ups.** None required by this change. The harness covers what a press RESOLVES to; an
executor that reads the local machine's hardware directly (`Gamepad.current`, as
`ShieldSwipeActionExecutor` does for the local pilot) is a separate question and is gated correctly
today.
