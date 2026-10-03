# Skim Race AI (Squirrel)

The AI that flies a Squirrel through a normal Skim Race: it waits for the countdown, chases its
own domain's crystals in the order the game activates them, and finishes the sequence by flying
the vessel through the same input channels a human uses. Code:
`Assets/_Scripts/Controller/AI/SkimRace/`. Benchmark results: §8. Status: §9.

## 1. The benchmark

| | |
|---|---|
| Scene | `MinigameSkimRace` (`GameModes.SkimRace = 33`), launched through the normal arcade path (`SyncFromArcadeGame` + `ConfigurePlayerCounts` + `InvokeGameLaunch`) |
| Field | 2 seats: the host (human seat, left idle on its own domain) + one AI backfill seat. The AI is alone on its domain, so the domain target is the AI's own work |
| Vessel | Squirrel (the card is Squirrel-only) |
| Required crystals | `CrystalTargetCount` = waypoints x laps: I1 8x3 = **24**, I2 10x3 = 30, I3 28x2 = 56, I4 27x2 = 54 |
| Crystal placement | Each player has ONE crystal in their domain; on pickup the manager moves it to the next authored anchor plus a random point on a 35 u sphere (`CrystalManager.GetSpawnPointAroundAnchor`). Randomisation is preserved; nothing is seeded for the AI |
| Timer | The game's own race clock: `SkimRaceScoreTracker` accumulates from `OnMiniGameTurnStarted`; `SkimRaceController` writes it into the winners' `Score` when the domain reaches the target |
| Success | The AI's domain wins, its collected count reaches the target, and the authoritative finish time is <= 70.0 s (`SkimRaceRaceRecorder.Evaluate`) |

Geometry that bounds what is possible (route = anchor-to-anchor, top speed 300 u/s):

| Intensity | Crystals | Route | Time at 300 u/s |
|---|---|---|---|
| 1 flat octagon | 24 | ~12,400 u | 41 s |
| 2 tilted spline loop | 30 | ~15,200 u | 51 s |
| 3 dumbbell | 56 | ~37,000 u | **124 s — 70 s is physically impossible for one pilot** |
| 4 3D polyline | 54 | ~15,800 u | 53 s |

## 2. The Squirrel, measured

Read from the prefab and from an in-editor probe (`SkimRaceRaceRecorder.WriteProbe`):

- Speed = throttle `XDiff` x 60 x skim boost (1..5): cruise 60, top 300 u/s.
- Boost: +0.1 per track prism that enters the 7.5 u skimmer sphere; decays 0.3/s. A crystal pickup
  lays a ring of 8 shielded boost prisms around the hull (`SquirrelVesselExplosionByCrystalEffect` ->
  `AOEShieldedRingSpawner`): **+0.8 per pickup**.
- **Hull touching any prism resets boost to 1x** (`VesselResetBoostPrismEffect`) and slows it
  (`SquirrelVesselChangeSpeedByPrism`): the ACTUAL speed is multiplied by
  `1 - min(volume x 0.1, 0.5)`, easing back over 1 s, and concurrent contacts **stack
  multiplicatively** (`VesselTransformer.ApplyThrottleModifiers`). A track plate, a ring prism or two
  rail prisms at once take the hull to a quarter of its speed.
- The hull is a BOX, 4.12 x 0.59 x 3.11 (`a_SquirrelShipMesh`), so a wingtip reaches 2 u beyond the
  hull centre.
- **The Squirrel lays mass of its own, and both kinds are hull hazards**: its trail is two rails of
  0.83 x 0.83 x 6.1 prisms at +-9.66 u along the hull's right axis, one pair per 7 u travelled
  (`VesselPrismController` Gap 18.5; own-trail contact grace 1 s, `SelfTrailContactConfig`), and each
  pickup lays a ring of eight 1.8 x 1.8 x 7.5 prisms on radius 8.2, 8 u ahead (no grace). Laps 2 and 3
  re-fly lap 1, so the race track gradually fills with the AI's own rails.
- Turning: 120 deg/s, and the hull slerps toward the commanded rotation at 1.5/s (tau ~0.67 s, i.e.
  up to ~80 deg of lag in a hard turn).
- Track: 10x1x3 plates every 12 u, **super-shielded**, so the contact solid is a stella octangula of
  half-extents 15 x 1.5 x 4.5 — a slab ~3 u thick the hull must never enter, and a 2.5..9 u skim band
  above or below it.
- Crystal pickup sphere: radius 24 (`SphereCollider`, bounds 48).

## 3. Architecture

| File | Role |
|---|---|
| `SkimRacePilot` | MonoBehaviour on the AI vessel: lifecycle, sensing, actuation. Inactive (neutral input) until `GameDataSO.IsTurnRunning` rises; neutral again when the turn ends; stops and disables `AIPilot` while it owns the vessel |
| `SkimRaceAIDeployment` | Installs the pilot from `ServerPlayerVesselInitializerWithAI.ConfigureAIPilot` — every Skim Race backfill seat in normal play, no scene wiring. Skipped while `IsTraining`; `TrainingDeploymentService` defers to it |
| `SkimRaceTargetTracker` | The authoritative target: a live, non-embedded crystal of this domain from `Crystal.Active` (mid-collection crystals are valid only once moved away from the pilot); nearest wins with hysteresis |
| `SkimRaceCourse` / `SkimRaceCourseSource` | The racing line: the track prisms the game actually laid, in lay order, with each prism's pose and contact shell |
| `SkimRaceObservation` / `SkimRaceAction` | The observation and action schema (feature vector, schema version, NaN sanitising, clamping) |
| `SkimRaceDriver` | The decision core (pure C#): racing line, crystal pass planning, lag-compensated steering, throttle, recovery |
| `SkimRacePlanner` | Optional model-predictive layer (rolls the Squirrel's own dynamics forward over a stick grid) |
| `SkimRaceShell` | The stella-octangula contact distance, shared by the pilot and the simulator |
| `SkimRaceAIConfigSO` | The policy: every tunable. Ships as `Resources/SkimRaceAIConfig[_I<n>].asset`, authored by `Tools/Build/author_skimrace_ai_config.py` |
| `SkimRaceRaceRecorder` / `SkimRaceBenchmarkRunner` | The benchmark referee and driver (§7) |

### Input-only contract

The pilot writes `IInputStatus.XSum` (yaw), `YSum` (pitch), `YDiff` (roll), `XDiff` (throttle) —
the same channels the dual-stick strategies write — and presses the hull's own bound controls
through `PerformShipControllerActions` (drift, Boost Ring; both off in the shipped policy). It reads
pose, speed, boost, the transformer's commanded rotation (new read-only `CommandedRotation`), the
visible track and the live crystal. It never writes a transform, speed, course, crystal, score or
timer, and grants itself nothing a human pilot does not have.

## 4. Observation and action

Observation (`SkimRaceObservation`, schema 1, 30 normalised features): hull pose and axes, commanded
forward, velocity, angular velocity, speed, boost/max, turn rate, follow rate; target position,
vector, distance, hull-local direction, alignment, capture radius; course progress, tangent,
distance from course, target distance along the course; race time, time since progress, time since
last pickup, collected/remaining; drift state. Every field is sanitised (no NaN/inf can reach the
stick).

Action (`SkimRaceAction`): yaw, pitch, roll in [-1, 1], throttle in [0, 1], drift and ring presses.

## 5. Driving

1. **Racing line.** The ribbon, lifted `SkimHeight` along its normal, on ONE face (changes face only
   for a crystal too deep on the other side, swinging round the ribbon edge first).
2. **Crystal pass.** The pass point is the point within (capture radius - margin) of the crystal
   closest to the skim line; a raised-cosine bump bends the line through it.
3. **Pure pursuit** on that line with a speed-scaled lookahead; inside `CrystalDirectDistance` with the
   crystal in front, fly at the pass point.
4. **Lag compensation.** The commanded heading is placed a bounded lead past the hull toward the
   desired heading, and the stick drives the commanded heading there.
5. **Throttle.** Full, eased only when the crystal is inside the reachable turning circle.
6. **Laid-mass guard.** Every decision the pilot reads the live prisms near its next
   `MassGuardSeconds` of flight from `PrismSpatialIndex.QuerySphere` (excluding the track's
   super-shielded plates and its own trail still inside the game's self-contact grace,
   `SelfTrailContactConfigSO.SuppressesHullContact`), rolls the commanded stick forward with the
   transformer's dynamics, and if the hull box would come within `MassGuardMargin` of a rail or ring
   prism, swaps the stick for the nearest of a 5x5 grid that clears.
7. **Recovery.** No closing progress for `StallSeconds` -> slower direct pursuit for `RecoverySeconds`.

## 6. Training

`Tools/Build/skimrace_sim_harness/` compiles the SHIPPED decision core against a Unity shim and races
it through a VesselTransformer-faithful Squirrel on the real track geometry (read from the scene):
rotation lag, speed lag, skim boost and decay, pickup rings, stella hull contacts. Calibrated against
the editor (the untuned policy: 81.9 s in-game vs 77-86 s simulated; the first tuned policy:
55-65 s in-game vs 55-66 s simulated). Policies are tuned with a cross-entropy method over 10-15
parameters, scoring every race (an unfinished race scores 300 + 10 per missing crystal) plus half the
worst time, then re-checked on 40 fresh seeds before going into the asset.

```
bash Tools/Build/skimrace_sim_harness/run.sh eval 1 20            # evaluate the default policy
bash Tools/Build/skimrace_sim_harness/run.sh eval 1 20 Field=v... # evaluate overrides
bash Tools/Build/skimrace_sim_harness/run.sh tune 1 10 25 sigma=0.08 Field=v...
```

### 6.1 What calibration taught (in order)

The simulator was wrong three times, and each time the in-game numbers said so before anything
else did. Each correction came from a diagnostic in the recorder, not from guessing:

1. **Frame rate.** Several batches ran at 115-340 ms per frame (GPU-bound in the editor) and the
   policy degrades below ~15 fps (sim at 115 ms frames: median 77 s vs 53 s). The cause was the
   ENVIRONMENT, not the scene: those batches ran with an unfocused editor and/or offline tuners
   saturating the machine; later batches at the default "Very High" quality ran at ~25 ms like
   "Low". Every race therefore records `meanFrameMs`, `maxFrameMs` and its quality level, the
   benchmark keeps the editor focused, and tuners are never run during validation. The runner can
   also set a graphics quality level (`quality` in the remote command - the same setting a player
   picks; it changes frame rate, never gameplay).
2. **Laid mass.** The recorder's `_hits.csv` (written on every frame the boost collapses without a
   pickup, naming every prism within 12 u) showed most boost resets were the AI's own trail rails and
   pickup rings, which the sim did not model. Adding them took the sim from 53 s to 71 s median -
   matching the game - and motivated the laid-mass guard.
3. **The slow.** A contact multiplies the real speed and stacks; the sim had a mild throttle-target
   cut. Fixed, the sim median for the same policy moved to 66.6 s against the editor's ~66 s.

## 7. Running the benchmark

In the editor: **FrogletTools > AI > Skim Race AI Benchmark** (races, intensity, players), or drop
`Library/SkimRaceAIRemote/command.json` = `{"op":"bench","races":10,"intensity":1,"players":2,"limit":70,"timeout":120}`
into an open editor (`SkimRaceBenchmarkRemote`; also `{"op":"tests"}` runs `SkimRaceAITests`).
Each race appends a JSON record (crystal timestamps, authoritative finish time, frame time, recoveries,
policy) to `BenchmarkResults/SkimRaceAI/` (git-ignored). Summarise with
`python3 Tools/Build/skimrace_benchmark_report.py <file.jsonl> [--markdown]`.

## 8. Results

Policy `skimrace-v4-i1` (`Resources/SkimRaceAIConfig_I1.asset`), editor 6000.3.17f1, branch base
`23442ab77` + this change, 2 seats (idle host + the AI), every race launched through the normal
arcade flow, Ready pressed through the public HUD handler, timed by the game's own clock. Crystal
placement is the game's own random draw (not seeded); the "seed" column is the controller's track
seed, which does not change the I1 waypoint track. Two separate fresh Play-mode launches; within each,
races 2..N are the in-process restart (the scoreboard's replay = network scene reload).
Reproduce: `{"op":"bench","races":12,"intensity":1,"players":2,"limit":70,"timeout":130,"quality":1}`.

### Intensity 1 — 22 races, "Low" quality (~25 ms frames)

| # | session | race | crystals | finish (s) | result | recoveries | mean u/s | frame ms |
|---|---|---|---|---|---|---|---|---|
| 1 | 20261002-191546 | 0 | 24/24 | 64.91 | PASS | 0 | 199 | 25.5 |
| 2 | 20261002-191546 | 1 | 24/24 | 52.70 | PASS | 0 | 227 | 24.5 |
| 3 | 20261002-191546 | 2 | 24/24 | 53.25 | PASS | 0 | 228 | 25.2 |
| 4 | 20261002-191546 | 3 | 24/24 | 50.66 | PASS | 0 | 242 | 25.2 |
| 5 | 20261002-191546 | 4 | 24/24 | 56.60 | PASS | 0 | 199 | 24.2 |
| 6 | 20261002-191546 | 5 | 24/24 | 58.40 | PASS | 1 | 231 | 25.2 |
| 7 | 20261002-191546 | 6 | 24/24 | 65.24 | PASS | 0 | 177 | 24.4 |
| 8 | 20261002-191546 | 7 | 24/24 | 65.61 | PASS | 0 | 186 | 24.4 |
| 9 | 20261002-191546 | 8 | 24/24 | 52.01 | PASS | 0 | 232 | 24.5 |
| 10 | 20261002-191546 | 9 | 24/24 | 89.32 | **FAIL** (> 70 s) | 0 | 141 | 24.9 |
| 11 | 20261002-191546 | 10 | 24/24 | 63.22 | PASS | 0 | 194 | 26.1 |
| 12 | 20261002-191546 | 11 | 24/24 | 57.84 | PASS | 0 | 214 | 24.1 |
| 13 | 20261003-014415 | 0 | 24/24 | 54.90 | PASS | 0 | 219 | 24.6 |
| 14 | 20261003-014415 | 1 | 24/24 | 74.22 | **FAIL** (> 70 s) | 1 | 183 | 26.1 |
| 15 | 20261003-014415 | 2 | 24/24 | 53.23 | PASS | 0 | 228 | 26.3 |
| 16 | 20261003-014415 | 3 | 24/24 | 49.47 | PASS | 0 | 247 | 24.2 |
| 17 | 20261003-014415 | 4 | 24/24 | 60.80 | PASS | 0 | 186 | 24.8 |
| 18 | 20261003-014415 | 5 | 24/24 | 57.48 | PASS | 0 | 213 | 25.2 |
| 19 | 20261003-014415 | 6 | 24/24 | 55.75 | PASS | 0 | 211 | 24.7 |
| 20 | 20261003-014415 | 7 | 24/24 | 62.03 | PASS | 0 | 188 | 24.8 |
| 21 | 20261003-014415 | 8 | 24/24 | 53.94 | PASS | 0 | 228 | 25.2 |
| 22 | 20261003-014415 | 9 | 24/24 | 59.15 | PASS | 0 | 199 | 25.0 |

- Full-sequence completion: **22/22 (100%)**
- Completed in <= 70.0 s: **20/22 (90.9%)**
- Finish time best / median / mean / worst: **49.47 / 57.66 / 59.58 / 89.32 s**

Plus 3 races at the default "Very High" quality (session 20261003-021648, ~25 ms frames):
50.83, 54.48 (PASS), 68.05 s — 3/3 complete, 3/3 <= 70 s.

Simulator (same policy, calibrated model, 120 fresh seeds): 106/120 (88%) <= 70 s, median ~58 s.

### Earlier policies (in-game, for the record)

| policy | races | <= 70 s | median | note |
|---|---|---|---|---|
| untuned | 1 | 0 | 81.9 | |
| v1 | 3 | 3 | 59.6 | small sample, frame rate not yet recorded |
| v2 (no guard) | 10 | 0 | 88.8 | ~115 ms frames (environment) |
| v2 + guard defaults | 5 | 4 | 66.9 | first laid-mass guard |
| v3 | 10 | 5 | 69.9 | batch contaminated by a concurrent tuner (400-600 ms spikes) |

## 9. Status and known limits

**Intensity 1: met, not perfect.** 22/22 races completed the whole 24-crystal sequence, 20/22 inside
70 s. The two misses (74.2 s, 89.3 s) are the residual failure mode: a hull strike on the ribbon
while the line crosses faces for a crystal on the other side (the hit log shows `trackShellClearance`
~0), each costing the whole boost bank, after which the Squirrel rebuilds speed at 60 u/s. The race
remains completable; it is not a stall.

**Intensities 2, 3 and 4: NOT achieved.** Do not read the I1 result as covering them.
- I3: 56 crystals over ~37,000 u is 124 s at top speed — 70 s is physically impossible for one pilot.
- I2 (30 crystals, ~15,200 u, 51 s at top speed) and I4 (54 crystals, ~15,800 u with a 157-degree
  hairpin) need a sustained mean speed the current policy does not reach on those tracks: in the
  calibrated simulator the I1 policy collects ~22/30 (I2) and ~27/54 (I4) in 130 s. Policies there
  fall back to the base asset and complete slowly or not at all inside a benchmark timeout.

**Conditions the result depends on.** A focused editor at normal frame rate (~25 ms). Under ~15 fps
the policy is measurably worse; the recorder states the frame time of every race so a slow machine
cannot be mistaken for a regression. Not yet run in a standalone player build or from a fresh
checkout on another machine.
