# Regatta AI roadmap — six racing AIs, one way to build them

**The goal (the user, 2026-10-14):** six vessels whose AI can race **Regatta** (`GameModes.Regatta = 56`,
`Arcade/REGATTA.md`), built so that a seventh is cheap, and running the same way in Unity, in Amoebius and in
the Vessel Studio. This file supersedes the per-vessel order in `Docs/Studios/VESSEL_STUDIO_ROLLOUT.md` §3: the
studios now follow the Regatta six. Measured on `Ys-bleeding-edge` at bleeding-edge `8faad7652`.

## 1. The six

Regatta is a closed circuit of 8 switch rings, 3 laps, with three super-shielded domain rails braided along the
racing line. The table is `Tools/Build/regatta_balance.py` (its offline lap-time model over the measured
circuits: a competent pilot on the best line, three laps, intensity 1). It ranks **hulls**, not AIs. No AI
has been timed in Regatta yet.

| Hull | Model race (I1, card's elements) | Its speed verb | AI today | Pick |
|---|---|---|---|---|
| Rhino | **17.8 s** (fastest) | ramp: straight stick → 1200 u/s; a corner costs the wind-up | none of its own (the ramp engages on a straight stick) | **Yes**: the fastest hull, and the hardest AI (when to straighten) |
| Manta | 23.9 s | Soar: boost costs yaw, 720 u/s | layer-4 Soar drive (`MantaAnalogTurnBoostExecutor`) | **Yes**: fast, and its drive exists |
| Dolphin | 41.9 s | drift charge → discharge, 347 u/s | `ChargeBoostAIPolicySO` | **Yes**: a skill mechanic the AI already half-owns |
| Scarab | 44.9 s (Time 10) | throttle ceiling + Time-5 Snap Dash | full throttle + `SnapDashAIPolicySO` | Reserve: fast and nearly free to drive, but nothing to learn |
| Urchin | 48.3 s | rides its own rail at 300 u/s | `UrchinAutopilotDriver`, already wired into Regatta | **Yes**: the rails are Regatta's identity |
| Squirrel | 48.5 s | skims the rail for boost | **`SkimRacePilot` + `RegattaRingObjective`**, the best AI in the game | **Yes**: done; verify only |
| Serpent | 58.1 s (Time 10) | 4 pellets | `PelletBoostAIPolicySO` | No: slow |
| Sparrow | 73.6 s (Time 10) | indefinite boost | `HoldBoostAIPolicySO` | No: slowest |
| **Stoat** | **not on the card yet** | the black-hole / white-hole pair: hold to warp the path and fly it ×2–×4 | dipole autopilot (path-watching, one-lap cap) + `GateRaceHandicap` difficulty | **Yes, next** (the user's pick) |

**The six: Squirrel, Stoat, Manta, Rhino, Urchin, Dolphin.** Six different speed verbs (skim, sling, soar,
ramp, ride, charge), so each AI teaches the framework something new. Scarab is the reserve if one stalls.
Grizzly, Thresher and Butterfly are not on the card and have no race-useful drive.

## 2. How we build every racing AI from now on (the simplification)

Today a hull's AI lives in up to six places (`ARCHITECTURE.md` §1): the Manta's, the Grizzly's and the Stoat's
autopilots hide inside their ability executors, the boost policies sit under `AI/Boost/`, the Urchin has its own
driver, and each studio page re-implements its AI in JavaScript. The pattern that already works is
**`AIBoostPolicySO` → `AIBoostDriver`** (`AI/AI_BOOST.md`): one small class per MECHANIC, built per pilot from the
hull's own bindings, ticked with a read-only context, input-only. Make that the only shape:

```
  Mode (Regatta, any gate race)   →  COURSE      next ring, the ring after, lap      (GateRaceController, shared)
  Lobby difficulty                →  BELIEF      noticed late / misjudged            (GateRaceHandicap, shared)
  Platform autopilot              →  STEERING    commit / lead / through, orbit cap  (AIPilot, shared)
  The hull                        →  DRIVE       when to spend its speed verb        (ONE class per hull: the only new code)
  Everyone                        →  DIAGNOSTICS aim, state, reason                  (one struct: Third Eye, Amoebius, studio)
```

1. **One drive contract.** A racing drive is an `AIBoostDriver`. Its context gains the course look-ahead it
   lacks: the ring after next, and the corner angle between them. That is the one fact a Rhino, Manta or Stoat
   needs to decide "spend now, or set up the turn". It gains a `Diagnostics` (state + reason), which the Third
   Eye reads (`ThirdEyePilots.Read`). The serialized field `AIPilot.boostPolicy` and every asset keep their names.
2. **Fold the hidden autopilots into drives, one per commit, measured**: Stoat dipole, then Manta Soar, then the
   Grizzly bomb-jump. Each is a pure move: same decisions, now in `Controller/AI/Drives/<Hull>…`. The executor
   keeps only the human input path.
3. **A drive is pure C#.** It takes the context and writes the vessel's input channels. Nothing reads Unity
   scene state, so the same class compiles in the offline harness (§4) and in Amoebius (§5), and its numbers live
   in its config SO, which is what `studio_to_unity.py` writes (`/artifact-to-unity`). Amoebius compiles
   `Assets/_Scripts` live, so a drive needs no port step (§5).
4. **One scaffold**: `Tools/Build/new_racing_drive.py <Hull>` writes the drive class, its config SO and
   generator stage, its `…DriveTests`, a studio drive module stub with its parity test (§5), its
   `studio_to_unity/<hull>.json` map, and the roster and status-board rows. A new racing AI becomes "fill in one
   `Tick`, one test, one studio module".
5. **One race studio, not six.** The Regatta studio page (built on the gate-race core the rollout plan already
   called for, `gate_race.js`) loads each hull as a drive module. Course I1–I4 is emitted from `RegattaCourse`,
   and the scorecard is lap time per hull × difficulty against the balance model. A hull studio (the Squirrel's,
   the Stoat's) stays for the hull's FEEL; the AI race lives in one place.
6. **The rules stay** (`/vessel-ai` §1): input-only (widen `check_ai_no_state_writes.py` to `AI/Drives/`),
   difficulty edits belief and never the stick, and no per-frame allocation.

## 3. The order

| # | Work | Done when |
|---|---|---|
| **0** | **Groundwork**: Regatta runs in Amoebius (`engine_smoke`, `--arcade Regatta`) and the `ai_state` tool (§5); Regatta in `AIDifficultyRules.IsOfferedFor` (every non-Squirrel AI gets `GateRaceHandicap`; check the Squirrel's `SkimRaceHandicap` follows the same lobby pick); gate-race belief readable for the Third Eye (status #10); context gains the look-ahead; the `Diagnostics` struct | Third Eye shows any Regatta AI's aim and its mistakes; tests pin the look-ahead |
| **1** | **Stoat** (§3.1) | A Hard AI Stoat finishes Regatta I1–I4 within the target band; Third Eye shows clean pairs |
| 2 | **Squirrel** verify in Regatta (it already flies the Skim Race pilot) | Measured lap times per difficulty; the 2026-10-06 "threaded no rings" note is confirmed gone |
| 3 | **Manta**: fold the Soar drive in, add corner look-ahead (cut Soar before a corner it cannot yaw round) | Lap times; no ring missed at Soar speed |
| 4 | **Rhino**: a NEW drive. Straighten for the ramp only when the runway to the next corner pays back the wind-up; otherwise hold the line | Beats its cruise time on every intensity; misses no corner mouth at 1200 u/s |
| 5 | **Urchin**: measure the lane-stay rate (REGATTA.md §6's open number); fix re-latching rivals' lanes if it is low | Rides its own lane through ≥ N% of the race |
| 6 | **Dolphin**: measure the charge policy in Regatta; tune the discharge runway | Lap times per difficulty |
| 7 | **Mixed grid**: clear `SO_ArcadeGame.OpponentAIVessel` in `author_regatta_assets.py` (the Squirrel pin is a stopgap, REGATTA.md §6); re-run `regatta_balance.py` against AI times; set `StartingElements` from them | An all-AI domain of each of the six can win; the spread holds |

**2026-10-15 status:** an audit of the five racing hulls (all except the Stoat) found and fixed the race-breakers listed in
`Arcade/REGATTA.md` §6.1. Step 0 is partly done: Regatta now offers difficulty, and every gate-race AI reports its
phase, ring, lap and boost decision to the Third Eye (`AIPilot.Objective`, `AIBoostDriver.Status`), with trails.
The Rhino now has a drive (`RampBoostAIPolicySO`, step 4's first cut). Nothing has been timed yet.

**2026-10-17: step 4's drive.** The Rhino brakes ON the ramp (its 300 u/s² bleed, not the 120 u/s² coast) to a corner
speed sized by the corner after the objective (a new `AIPilot.SetNextLegProvider`, filled by `GateRaceController`), and
presses the ramp only where the leg pays the wind-up back (`AI/AI_BOOST.md`, the Rhino ramp section). In the offline model
(`Tools/Build/rhino_drive_sim.py`) it finished I1 43.0 s (old 48.2), I2 56.1 s (old 55.4), I3 56.3 s (old: stalled). Not
editor-verified; `cornerSpeed` is learned from Yash's Rhino flights when they arrive.

**2026-10-17: the Regatta AIs learn from people's editor flights.** The Squirrel's skim line came from 29 flights
(`SITUATION_RECORDING_PLAN.md` §8); the Rhino, Dolphin and Manta drives learn the same way through Regatta-only variants
(`learn_drives_from_flights.py`, §9), waiting on flights of those hulls. The Stoat joined the card on 2026-10-17 and learns the same way.

**Decision (Yash, 2026-10-16): the Regatta AIs are tested in the Unity editor only, and the Regatta artifact is
deleted.** "Delete the artifact, it's not needed; only tests will happen in the Unity editor itself." The loop is: change
the AI in Unity, watch the real vessels race in the Third Eye (trajectories, behaviour, the You / AI toggle on every
pilot), measure, repeat. Training data likewise comes only from playing in the editor (`SITUATION_RECORDING_PLAN.md`).
The page (Regatta AI, then Training AI for Regatta, 2026-10-15/16) and its README are in git history up to the commit
that deleted `Docs/Studios/RegattaAI/`; its course export moved to `Tools/Build/regatta_course_harness/`.

**What the page measured before it was deleted (2026-10-15; a simplified flight model, Hard, seed 11, the card's
elements; seconds for three laps, `regatta_balance.py`'s skilled-pilot model in brackets).** Re-measure in the editor;
these are leads, not results.

| Hull | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Rhino | 67.9 (17.8) | 57.5 (21.8) | 103.5 (25.6) | 150.5 (25.9) |
| Manta | 31.4 (23.9) | 53.9 (27.5) | 59.9 (30.4) | 116.3 (30.2) |
| Dolphin | 86.0 (41.9) | 102.3 (48.0) | 127.2 (53.0) | 132.7 (54.1) |
| Urchin | 49.8 (48.3) | 57.1 (54.3) | 60.0 (58.9) | 60.9 (58.7) |
| Squirrel | 66.9 (48.5) | 97.5 (54.6) | 99.1 (59.2) | 83.3 (59.5) |
| Serpent | 63.8 (58.1) | 87.2 (65.4) | 104.8 (70.9) | 107.8 (70.6) |
| Sparrow | 89.1 (73.6) | 112.1 (82.7) | 130.3 (89.6) | 143.5 (89.5) |
| Scarab | 58.7 (44.9) | 73.8 (50.5) | DNF (54.8) | DNF (55.1) |

- **The shipped gate steering finished 17 of these 32 races; with the fix (commit no earlier than the approach point;
  after a missed mouth re-latch the side and extend at once), 30.** **Ported (2026-10-15, `Ys-bleeding-edge`, for
  testing):** `GateRaceController` (`CommitDistanceFor`, `RaceGateRing.PlaneCrossing`, `AIPilot.BreakOffNow`), on in
  Regatta only; the other gate races keep their shipped steering until their owners turn `aiCommitFromApproachPoint` on.
  Not editor-verified.
- **The card's Time-10 Scarab (324 u/s, ~140 u turning circle) cannot make I3–I4** under the full-throttle floor (the
  corner floors were cut for a 216 u/s Scarab).
- **The Urchin matched the model** (it rides its own lane at 300 u/s); its difficulty does nothing while it rides.
- **The fast hulls were far off the model** (the Rhino 3-6×): it coasts down from 840 u/s and misses mouths. A lead:
  letting go of the ramp 300 u early (instead of 60) raced I1 in 29.0 s instead of 67.9 s.
- **Levels ordered correctly** for the Serpent and the Manta; the Squirrel's Medium beat its Hard on two courses (rail
  rams reset its boost: noise larger than the belief mistakes).

### 3.1 Stoat, the first one

The user's brief: **better speed control and manoeuvring with the black-hole / white-hole pair.**

1. **Studio first** ([Vessel Studio](https://claude.ai/artifact/3igBJJbNvJjsfJoBJnAMPa), Stoat page). Read
   `requests` and `decisions` first.
   - **Speed control**: hold the pair to a TARGET speed for the leg. Let go early into a corner, carry the
     boost down a straight. Today it holds while the path is warped, with a one-lap cap.
   - **Manoeuvring**: lay the pair for the TURN, not only for straights. Pick the trigger whose side bends the
     path through the next ring mouth, using the corner angle from the look-ahead.
   - Scorecard columns: lap time on the Regatta course I1–I4, rings missed, overshoot past a ring, time over
     cruise, pairs per lap. The current Hard result in the studio (Warpline courses) is 49–59 s.
2. **Clone to Unity** (`/artifact-to-unity`): the drive's numbers through `studio_to_unity.py`; the decision
   logic ported line for line, as the first drive in `Controller/AI/Drives/` (the fold in §2.2), with
   page-golden tests.
3. **Put the Stoat on the Regatta card** - **done 2026-10-17** (Yash): `author_regatta_assets.py` lists it, and
   `regatta_balance.py`'s Stoat row prices its warp (Time 10 at every intensity, 48.5 s at I1 beside the Squirrel and
   the Urchin; the fleet's tuned spread unchanged at 4.13x). Its autopilot learns from editor flights through
   `AIModeTuning` (`SITUATION_RECORDING_PLAN.md` §9).
4. **Verify**: Third Eye in Regatta (Arcade ▸ Regatta, ally AI set to Stoat); Easy vs Hard; profile the
   pathfinder near a sink.

## 4. Measuring: race the real game headless, in Amoebius

Amoebius already compiles the game's own `Assets/_Scripts` live (`Port/src/CosmicShore.Live`, `SyncUnitySources.targets`;
`Port/CLAUDE.md`: never hand-port a game file). So every drive, `GateRaceController`, `RegattaController` and
`SkimRacePilot` already runs there unchanged. That makes Amoebius the race harness, with the game's real flight
model instead of `skimrace_sim_harness`'s hand model (`Sim.cs`):

1. **Regatta runs in Amoebius**: `MinigameRegatta` passes `engine_smoke`, and `--arcade Regatta` reaches the
   race (no Port doc mentions Regatta yet; Skim Race is the precedent: `engine_smoke` and parity case `skimrace-fly`).
2. **A headless race verb**: `--race Regatta --intensity N --seats Stoat:Hard,Manta:Hard,... --runs N`, next to
   the existing `--auto bench:` and `--train`. It writes one JSON record per race (finish time per seat, rings
   missed, recoveries), the Skim Race benchmark's shape (`SKIM_RACE_AI.md` §7).
3. **The table** every step in §3 is "done" against, and the regression gate when a drive changes:
   seat median, p10–p90, finished, per hull × difficulty × intensity (`/vessel-ai` §3.2).

`skimrace_sim_harness` stays for what it is good at: thousands of seeds a minute while tuning the Skim Race pilot.

## 5. Amoebius: the same AI in the engine, the studio and Unity

| Piece | Today | Plan |
|---|---|---|
| The AI code | Compiled live from `Assets/` (`CosmicShore.Live`). The legacy hand copy `Port/src/CosmicShore.Game/Controller/AI/AIPilot.cs` has drifted, but only legacy tests use it | Nothing to port: a drive written in `Assets/` runs in Amoebius on the next build. Keep drives pure (§2.3) so they never need an engine feature the port lacks |
| Watching an AI | MCP tools are generic (`game_get`, `game_hierarchy`); no AI tool | One **`ai_state`** MCP tool and control-port verb that return every pilot's `Diagnostics` (aim, state, reason, belief): the same struct the Third Eye draws. Claude, a test script and a human all read one answer |
| Racing headless | `--arcade MODE`, `--auto bench:`, `--train` (Skim Race, `TrainingPilot`) | `--race` (§4). Regatta first |
| The studio | VESSEL STUDIO page (`StudioCatalog.cs`, `studios.json`): Open, PLAY IN ENGINE (`engineMode` → `--arcade`), Open live. The studio's AI is JavaScript in a browser window | A **Regatta** studio card with `engineMode: Regatta`, so PLAY IN ENGINE opens the real race with the AIs. The Stoat card's note goes away once the game flies the dipole sling the studio does |
| Studio ↔ game parity | Planned, not built (`VESSEL_STUDIO_PLAN.md` §4a step 2: Node runs the page's functions, dotnet runs the C#) | Per drive: a **parity test** that feeds the same recorded contexts to the page's drive module and to the C# drive, and requires the same presses. That keeps the browser AI and the game AI one AI. The drive's config SO is mapped in `Tools/Build/studio_to_unity/<hull>.json` (only `stoat.json` exists today) |
| The studio inside the engine | WebAssembly build of Amoebius: not scheduled (`VESSEL_STUDIO_PLAN.md` §4) | Out of scope here. PLAY IN ENGINE plus the parity tests give "one AI" without it |

## 6. What the user tests, per step

The Third Eye (`FrogletTools > AI > Third Eye`) on the hull being built, in Regatta, Easy and Hard. Each step
files its script in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
