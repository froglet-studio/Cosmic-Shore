# AI boost policies — how an autopilot spends a hull's speed

**Status:** shipped on `claude/fleet-ai-boost`, **not yet run in the Editor**.

## The gap

`AIPilot` writes the stick and a 0.6–0.9 throttle and nothing else. A hull whose speed is an
ABILITY needs something to press it, and before this only two had one: the Manta
(`MantaAnalogTurnBoostExecutor`'s autopilot drive, boost = how straight the stick is) and the
Rhino (the ramp engages off a straight stick). Everyone else raced at cruise — in Regatta an AI
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

## The shape

`AIPilot.boostPolicy` (one serialized field) → an `AIBoostPolicySO` asset → at `Initialize` it
builds a per-pilot `AIBoostDriver` against the vessel's OWN bindings
(`R_VesselActionHandler.TryGetBoundAction<T>`), or null when the hull binds no such ability. One
subclass per MECHANIC, never per mode:

| policy | asset | mechanic |
|---|---|---|
| `HoldBoostAIPolicySO` | `SparrowAIBoostPolicy` | hold the boost while straight, release on a wide stick / course error / approaching the objective; min hold 0.6 s, min rest 0.4 s |
| `PelletBoostAIPolicySO` | `SerpentAIBoostPolicy` | light a pellet on a straight; the n-th concurrent pellet needs n × engage-runway of straight (max 3 burning); never light into a turn — a lit pellet cannot be put out |
| `ChargeBoostAIPolicySO` | `DolphinAIBoostPolicy` | while the commit drift is held, locked on the objective, meter ≥ 0.95 and ≥ 2.5 s of runway: ask the pilot to release the drift NOW (discharge down the straight), then hold the next commit off until the discharge has run out |
| `SkimRingAIPolicySO` | `SquirrelAIBoostPolicy` | lay the Boost Ring (`SquirrelTubeActionSO`) on a dead-straight leg (nose and course within 2.5°) while skim boost < 3 and the objective is beyond the ring, then hold the stick centred until through it — `SkimRaceDriver`'s launch-ring gates |
| `SnapDashAIPolicySO` | `ScarabAIBoostPolicy` | fire `ScarabVesselTransformer.TryAutopilotSnapDash()` on a straight, at most every 2 s; refused below Time 5 exactly as a human's double-tap is |

The shared law (base class): **straight** = course within `alignDegrees` (12°) of the objective
and stick within `stickBand` (0.35, the Manta drive's band); **runway** = distance / current speed
≥ `engageRunwaySeconds` (1.5 s); **turn ahead** = runway < `releaseRunwaySeconds` (0.5 s) — stop
spending, the next leg starts at the objective. A break-off (orbit break) and a restricted vessel
(stance/stop) never spend.

## Hooks in `AIPilot` (all in the `Boost policy` region)

1. `Initialize` builds the driver; `StartAIPilot` resolves `_boostActive` (mode gate, AI-player gate).
2. The ability cycler skips an entry the active driver presses itself (`Drives`) — the twin of the
   commit-control exclusion, for the same reason (its StopAction would cut the held boost).
3. `Update` ticks the driver right after the steer target is known, before the commit branches;
   a driver may request a commit release (Dolphin) and hold the next commit off (`HoldsOffCommit`
   → the branch chain only puts the telegraph down), or hold the line (`HoldsLine` → stick centred).
4. Every exit (`StopAIPilot`, `OnDisable`, stationary, re-start) calls `Release()`.

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

## Known limits

- **Not measured.** No race has been run with these; the numbers are first guesses on the human
  trade. Tune on the assets.
- **AI presses are local** (server), like the cycler and `SkimRacePilot`. Motion replicates through
  the transform; the Squirrel's ring prisms are laid on the simulating machine only, the same
  limitation `SkimRacePilot`'s ring already has.
- The Dolphin still discharges whatever is banked when the commit loop's own overshoot release
  fires at a turn; the policy only moves the FULL-meter spend onto straights.
