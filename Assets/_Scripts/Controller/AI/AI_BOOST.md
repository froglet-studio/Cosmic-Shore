# AI boost policies — how an autopilot spends a hull's speed

**Status:** worked on `Ys-bleeding-edge` (first shipped on `claude/fleet-ai-boost`, PR #974). The base five reached
`bleeding-edge`; the Rhino rebuild, the Dolphin peak re-drift and the Regatta centre steering (2026-10-17) are on
`Ys-bleeding-edge` only. **None of it has been run in the Editor.** (Committed: Rhino `658fda119`, Dolphin + centre steering `9b064721b`.)

## The gap

`AIPilot` writes the stick and a 0.6–0.9 throttle and nothing else. A hull whose speed is an
ABILITY needs something to press it, and before this only two had one: the Manta
(`MantaAnalogTurnBoostExecutor`'s autopilot drive, boost = how straight the stick is) and the
Rhino (the ramp engages off a straight stick — **wrong, found 2026-10-14**: the ramp starts only on
`FullSpeedStraightAction`, which only a human input strategy raises, and the action handler drops presses
under autopilot, so an AI Rhino never ramped; `RampBoostAIPolicySO` below now drives it). Everyone else raced at cruise — in Regatta an AI
Sparrow, Serpent, Dolphin or Scarab "finishes but never wins", and the Squirrel's AI charged its
skim boost only inside Skim Race (`SkimRacePilot`).

What each hull did before, measured off the prefabs:

| hull | boost mechanic | AI before |
|---|---|---|
| Sparrow | `BoostActionSO`, free hold | blind cycler: 2 s on, 10 s off, regardless of line |
| Serpent | `ConsumeBoostActionSO`, 4 pellets, overlap-stacking | blind cycler: one pellet per 8.1 s |
| Dolphin | `ChargeBoostActionSO` on the drift control | the commit drift banks charge, but is released only once the course has LEFT the objective — the discharge was spent in the turn |
| Squirrel | skim energy (+0.1/contact) | never flies near mass on purpose: ~no boost |
| Scarab | throttle ceiling (autopilot = full throttle) | already at the ceiling; the Time-5 Snap Dash is a double-tap no autopilot can make |
| Rhino | `RampBoostActionSO`, graded ramp armed by the full-speed-straight gesture | **never ramped** — nothing raises the gesture under autopilot (added 2026-10-16) |

## The shape

`AIPilot.boostPolicy` (one serialized field) → an `AIBoostPolicySO` asset → at `Initialize` it
builds a per-pilot `AIBoostDriver` against the vessel's OWN bindings
(`R_VesselActionHandler.TryGetBoundAction<T>`), or null when the hull binds no such ability. That
lookup answers for the vessel's ACTIVE input device (since 2026-10-06, `SQUIRREL_DRIFT.md` §10), so an
ability bound only in ONE device family's override map gives null on the other; the Squirrel's ring is
bound in both, so `SkimRingAIPolicySO` builds on every device. One
subclass per MECHANIC, never per mode:

| policy | asset | mechanic |
|---|---|---|
| `HoldBoostAIPolicySO` | `SparrowAIBoostPolicy` | hold the boost while straight, release on a wide stick / course error / approaching the objective; min hold 0.6 s, min rest 0.4 s |
| `PelletBoostAIPolicySO` | `SerpentAIBoostPolicy` | light a pellet on a straight; the n-th concurrent pellet needs n × engage-runway of straight (max 3 burning); never light into a turn — a lit pellet cannot be put out |
| `ChargeBoostAIPolicySO` | `DolphinAIBoostPolicy` | while the commit drift is held, locked on the objective (course and nose), meter ≥ 0.95, ≥ 2.5 s of runway and a release that lifts the speed by ≥ 30 u/s: ask the pilot to release the drift NOW (discharge down the straight), then hold the next commit off until the discharge PEAKS, so the next drift freezes the top speed (since 2026-10-17; see "Charge (the Dolphin)" below) |
| `SkimRingAIPolicySO` | `SquirrelAIBoostPolicy` | lay the Boost Ring (`SquirrelTubeActionSO`) on a dead-straight leg (nose and course within 2.5°) while skim boost < 3 and the objective is beyond the ring, then hold the stick centred until through it — `SkimRaceDriver`'s launch-ring gates |
| `SnapDashAIPolicySO` | `ScarabAIBoostPolicy` | fire `ScarabVesselTransformer.TryAutopilotSnapDash()` on a straight, at most every 2 s; refused below Time 5 exactly as a human's double-tap is |
| `RampBoostAIPolicySO` | `RhinoAIBoostPolicy` | ramp on a straight whose leg pays the wind-up back, brake ON the ramp to a corner speed, let go at the corner (section below) |
| `BrittleStarSpeedModeAIPolicySO` | `BrittleStarAIBoostPolicy` | the shared straight / runway test raises a Speed Mode WISH (min hold 1.2 s, min rest 0.8 s); `BrittleStarExecutor` presses the mode itself (`R_VesselActions/BRITTLE_STAR.md` §3.1) |
| `SkimRingAIPolicySO` (a copy) | `StoatAIBoostPolicy` | the Squirrel's asset cloned onto `Stoat.prefab`. The Stoat binds no `SquirrelTubeActionSO`, so `CreateDriver` returns null and it is a quiet no-op; the Stoat's pace comes from its own executors' autopilot (dipole / sling) |

The Rhino's **sword** has an autopilot too, and it is deliberately **not** a boost policy:
`RhinoSwordAutopilot` (Rhino root) writes the trigger mirrors (`InputStatus.LeftTriggerAnalog /
RightTriggerAnalog`) on the machine that owns the AI's input, so the shipped sword code poses,
swipes and energizes exactly as for a human and replicates the same way. Every `thinkSeconds`
(0.15 s) it strokes at the nearest hostile prism in reach (stroke 0.3 s, rest 0.45 s), or holds the
energize stance when the meter can pay (cost + 0.05) and super-shielded mass is near, or the meter
is ≥ 0.75. It never steers and drives only while the autopilot is on. It never combos.

The shared law (base class): **straight** = course within `alignDegrees` (12°) of the objective
and stick within `stickBand` (0.35, the Manta drive's band); **runway** = distance / current speed
≥ `engageRunwaySeconds` (1.5 s); **turn ahead** = runway < `releaseRunwaySeconds` (0.5 s) — stop
spending, the next leg starts at the objective. A break-off (orbit break) and a restricted vessel
(stance/stop) never spend.

## Hooks in `AIPilot` (all in the `Boost policy` region)

1. `Initialize` builds the driver from the mode's variant of the asset (`AIBoostPolicySO.ForMode`, below);
   `StartAIPilot` resolves `_boostActive` (mode gate, AI-player gate).
2. The ability cycler skips an entry the active driver presses itself (`Drives`) — the twin of the
   commit-control exclusion, for the same reason (its StopAction would cut the held boost).
3. `Update` ticks the driver right after the steer target is known, before the commit branches;
   a driver may request a commit release (Dolphin) and hold the next commit off (`HoldsOffCommit`
   → the branch chain only puts the telegraph down), or hold the line (`HoldsLine` → stick centred).
   Since 2026-10-15/17 a driver can also hold the throttle full (`HoldsFullThrottle`), write an exact throttle
   (`Throttle`, ≥ 0; the Rhino's brake), and cap the total steering (`RotationLimit`, ≥ 0; `AIPilot.LimitRotation`
   scales the three axes together).
4. Every exit (`StopAIPilot`, `OnDisable`, stationary, re-start) calls `Release()`.
5. The context carries the course look-ahead: `AIBoostContext.NextLeg` (from the objective to where the pilot flies
   after it) and `CornerDegrees` (the turn there, -1 when the mode does not say). A mode fills it with
   `AIPilot.SetNextLegProvider`; `GateRaceController` gives the mouth after the approach point, then the next ring.
   Only the Rhino reads it today. The Manta was modelled with it (drop Soar before a corner) and was slower on every
   course, so it does not (`manta_drive_sim.py`, `REGATTA_AI_ROADMAP.md` step 3).

## Per-mode gating — a listed mode is a pure revert

`disabledInModes` on each asset: the driver never starts and the cycler keeps the entry it would
have handed over, so the hull flies exactly as before. Shipped lists:

- Sparrow: Dog Fight, Wildlife Liberation (gun hunts — engagement range, not speed).
- Dolphin: The Bends, Rampage, Broadside — the commit drift is how the AI AIMS the crystal blast
  those modes score by; releasing it early trades aim for speed.
- Squirrel: Skim Race (its own pilot), Joust, Astro League; plus `requireAIPlayer`, because the
  ring is permanent mass and the menu's lava-lamp autopilot must not wall up Menu_Main.
- Scarab: Astro League, Scarab Scramble, Tollway (ball modes).
- Serpent: none.

**Per-mode variants.** `AIBoostPolicySO.ForMode(policy, mode)` loads
`Resources/AIBoostPolicies/<asset name>_<GameModes name>` (e.g. `RhinoAIBoostPolicy_Regatta`) and flies it instead of
the base in that mode only. A variant of a different class than the base is ignored. `learn_drives_from_flights.py`
writes them from people's editor flights; `AIModeTuning.For` does the same for the Stoat's three configs and
`MantaSoarAITuningSO.LoadForMode` for the Manta's Soar bands. The folder holds one variant so far, `MantaSoarAITuning_Regatta`
(Soar bands 0.8 / 0.85 against the executor's 0.35 / 0.75 (since 2026-10-24: 0.975 / 0.975, learned from Yash's 3 fastest of 9 finished Manta flights; `LEARNED_DRIVES.md`), 2026-10-17, from `manta_drive_sim.py`); every boost-policy
drive flies its base asset. Renaming a base asset orphans its variants.

## Known limits

- **Not measured.** No race has been run with these; the numbers are first guesses on the human
  trade. Tune on the assets.
- **AI presses are local** (server), like the cycler and `SkimRacePilot`. Motion replicates through
  the transform; the Squirrel's ring prisms are laid on the simulating machine only, the same
  limitation `SkimRacePilot`'s ring already has.
- The Dolphin still discharges whatever is banked when the commit loop's own overshoot release
  fires at a turn; the policy moves the FULL-meter spend onto straights, and since 2026-10-17 holds that
  corner discharge to its peak too.

## What the drivers judge: the objective, not the steering point (2026-10-17)

`AIBoostContext.ToTarget` is the offset to the mode's OBJECTIVE (`AIPilot.ObjectivePosition`: a gate race's ring), not
to the point the pilot steers for, whenever the mode reports one and the pilot is not flying a difficulty mistake.
`NextLeg` is measured from that objective too. Reason: a mode that steers along a race line (Regatta, `REGATTA.md`
§6.2) keeps the steering point one look-ahead ahead, so `RunwaySeconds` never reached any driver's
`engageRunwaySeconds` and no speed verb fired. A mode that reports nothing is unchanged.

## Ramp (the Rhino) — `RampBoostAIPolicySO`, 2026-10-14, rebuilt 2026-10-17

Three states, ticked from `AIPilot.TickBoostPolicy`:

- **Ramp.** On a straight at the objective (`IsStraight` + `HasRunway`) whose leg PAYS THE WIND-UP BACK, the driver
  presses the ramp (`RampBoostActionSO.StartAction` → `RampBoostActionExecutor.Begin`), holds the throttle full
  (`HoldsFullThrottle`) and keeps the total steering inside the ramp's full-power plateau (`RotationLimit` = 0.9 ×
  `StraightLineGesture.EngageThreshold`; `AIPilot.LimitRotation` scales the three axes together). "Pays back":
  `PeakSpeed` (climb at `accelerationPerSecond`, brake back to the corner speed at `bleedPerSecond`, inside the leg)
  beats the current speed by `minSpeedGain`.
- **Brake.** From the point where the ramp's bleed only just reaches the corner speed by the objective (`BrakePoint`:
  `BrakeDistance` + `brakeMarginUnits` + `brakeLeadSeconds` of travel), or off the line (`!StillStraight`), or inside
  `releaseRunwaySeconds` (`TurnAhead`), the driver stays ON the ramp and lifts the throttle (`AIBoostDriver.Throttle`).
  The graded ramp then tracks down at `bleedPerSecond` (300 u/s²), 2.5× faster than the coast a released ramp falls back
  at (`returnPerSecond`, 120 u/s²). The lift is a governor read off `VesselTransformer.CurrentThrottleTarget` (never a
  copy of the throttle formula): down while the ramp tracks above the corner speed, up while under it. Steering plus
  lift stays under `graceShare` of the grace band, so braking never lets the ramp fall off, and the lift never takes
  the last `brakeSteerFloor` of steering.
- **Off.** At the corner itself (course `cornerReleaseDegrees` off the objective, speed down to the corner speed) it
  ends the ramp explicitly (`RampBoostActionExecutor.End`: a graded ramp's own release is the top of its slope, not
  an end) and steers freely; the next straight that pays back presses it again.

**The corner speed** is `cornerSpeed` for a 90° corner, scaled by 1/tan(θ/2) for a turn of θ (a turn at radius R needs
a lead of R·tan(θ/2)), floored at `minCornerSpeed`. θ is `AIBoostContext.CornerDegrees`: the angle between the leg into
the objective and the leg after it, from the mode's `AIPilot.SetNextLegProvider` (the gate races give the mouth after the
approach point, then the next ring after the mouth). A mode that does not say gets a 90° corner. In Regatta,
`learn_drives_from_flights.py` learns `cornerSpeed` from people's flights (their median speed at each ring they thread).

**The first cut's defect** (2026-10-14 to 10-17): it let go 0.5 s from every objective, after which the released ramp
coasted down from 840 u/s at 120 u/s² and the Rhino overshot mouths (`REGATTA_AI_ROADMAP.md`: 67.9 s at I1 in the
deleted page's model). It also never re-armed a ramp the grading let fall off. The re-arm (`minRearmSeconds`) and the
explicit end are taken from the other cut of this driver on `cece/relaxed-hopper-r62hbd` (Garrett's Rhino branch). That
branch's merge of bleeding-edge (`5b09c6896`, 2026-10-18) kept this file's driver whole, repointed `Rhino.prefab` at this
asset's guid, and kept that branch's executor changes (`VesselTransformer.TrackedSpeed`, live Time).

**Offline check** (`Tools/Build/rhino_drive_sim.py`, a point-mass model of the ramp executor and the gate steering on
the Regatta courses; a model without break-off or rotation-follow lag, not the game). It defaults to Regatta's
steering through the ring's centre (`THROUGH_CENTRE = True`, `GateRaceController.ArmRacer` since 2026-10-17). Under that
steering, with `cornerSpeed` 600, `cornerReleaseDegrees` 30 and `minSpeedGain` 50, old drive vs new, seconds for three
laps: I1 43.8 / 44.9, I2 58.8 / 62.9, I3 53.7 / 50.1, I4 51.8 / 55.4. That is about a tie (208 vs 213 s in all). The
brake is kept for hairpins, and `cornerSpeed` will be learned from Yash's Rhino flights. Under the OLD axis steering the
new drive (then at `cornerReleaseDegrees` 50, `minSpeedGain` 150) raced 43.0 / 56.1 / 56.3 / stalled against the old
drive's 48.2 / 55.4 / stalled / stalled; the retune (50 → 30, 150 → 50) is for the centre steering. `--sweep` re-runs
the parameter grid. The editor is the measurement.

Asset: `AI Boost Policies/RhinoAIBoostPolicy`, wired on `Rhino.prefab`'s `AIPilot`; a Regatta variant
(`Resources/AIBoostPolicies/RhinoAIBoostPolicy_Regatta`) appears once flights are learned. Tests:
`RhinoRampDriveTests`. Not editor-verified.

The 2026-10-14 audit added two hooks every driver can use (`AIBoostDriver.HoldsFullThrottle`, `RotationLimit`), a
`Status` string for the Third Eye, and to the Dolphin's `ChargeBoost` driver a nose check: it spends only with the
NOSE on the objective too (the discharge fires along the nose, and in a drift the nose is free of the course). The
2026-10-17 rebuild added a third, `Throttle` (the brake), and the look-ahead `NextLeg` / `CornerDegrees`.

## Charge (the Dolphin) — `ChargeBoostAIPolicySO`, re-drift at the peak 2026-10-17

The Dolphin flies the SCALAR model (`Dolphin.prefab`: `vectorFlightModel: 0`, `holdSpeedWhileDrifting: 1`). The drift
FREEZES the speed it starts at (`VesselTransformer.holdSpeedWhileDrifting`) and banks charge from what is left. A
discharge climbs toward cruise × m² (m = 1 + (max − 1) × meter; 347 u/s off a full meter,
`R_VesselActions/DOLPHIN_ENERGY_ECONOMY.md` §2) while its target decays with the meter. So the fastest a Dolphin can
carry is the PEAK of a discharge, and that is the moment to drift again.

- **Every discharge is held to its peak.** The driver's own straight-line release AND the commit loop's corner release
  both hold the next commit drift off until the speed meets the transformer's falling target (`AtPeak`: at least
  `minDischargeSeconds` 0.2 s in, speed within `peakMarginUnits` 3 u/s of `CurrentThrottleTarget`). The drift then
  freezes that peak. `maxHoldOffSeconds` (4 s) and the discharge ending still release the hold.
- **A straight-line release only when it pays.** `PulseWorthIt`: `DischargeCeiling(cruise, max, meter)` − speed ≥
  `minPulseGainUnits` (30). A Dolphin already sliding near the top gains nothing from another release.
- **Before:** it held the commit off until the discharge ENDED, so the next drift froze a speed already sliding back to
  cruise.
- **Third Eye status:** "discharging - drifts again at the peak (N u/s)".

**Offline check** (`Tools/Build/dolphin_drive_sim.py`, a model without break-off or rotation-follow lag, centre
steering; its Python drivers mirror `Driver.Tick`, change both together): on a straight the average speed goes 202 →
299 u/s. Regatta, three laps, old vs new: I1 56.9 / 54.0, I2 66.0 / 64.6, I3 80.8 / 73.1, I4 74.0 / 71.9 s.
`learn_drives_from_flights.py` reports (does not write) `redriftShareOfPeak` for Dolphin flights: the speed a drift froze
over the top speed of the discharge before it (1.0 = drifting again at the peak).

Asset: `AI Boost Policies/DolphinAIBoostPolicy` (`peakMarginUnits` 3, `minDischargeSeconds` 0.2, `minPulseGainUnits`
30). Tests: `DolphinChargeDriveTests`. Not editor-verified (T-20261016-b719).
