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
| Success | The AI's domain wins, its collected count reaches the target, and the authoritative finish time is <= the intensity's limit (`SkimRaceRaceRecorder.Evaluate`) |
| Limit | `SkimRaceRaceRecorder.DefaultLimitSeconds`: **I1 70 s, I2 80 s, I3 70 s, I4 70 s**. I2 was re-baselined from 70 s by product decision (§6.11). An explicit `limit` still overrides |

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
| `SkimRaceAIDeployment` | Installs the pilot from `ServerPlayerVesselInitializerWithAI.ConfigureAIPilot` — every Squirrel backfill seat in **Skim Race and Regatta** in normal play, no scene wiring. Skipped while `IsTraining`; `TrainingDeploymentService` defers to it for Squirrel seats. Reads the host's lobby AI difficulty (`GameDataSO.RequestedAIDifficulty`, §10). Picks the policy (`PolicyFor`): the intensity's own file only while it fits the map in the scene, else the general one (§11) |
| `SkimRaceObjective` | WHAT the pilot races for — the only mode-aware part. `CrystalTrackObjective` (Skim Race: the waypoint track's ribbon, this domain's crystal, crystals collected — the pilot's original behaviour, moved verbatim) and `RegattaRingObjective` (Regatta: this domain's rail, the pilot's next ring via `GateRaceController.TryGetNextGate` at 0.7 × the mouth, gates threaded). `SkimRaceObjective.For(gameData)` picks by mode |
| `SkimRaceTargetTracker` | Skim Race's target: a live, non-embedded crystal of this domain from `Crystal.Active` (mid-collection crystals are valid only once moved away from the pilot); nearest wins with hysteresis. The whole rule for a lone AI on its team, and the fallback for AI teammates |
| `SkimRaceTeamPlan` / `SkimRaceTeamAssignment` | Team play (§13): when two or more AI fly for one team, one plan per team per frame gives each a DIFFERENT crystal (least total distance, kept until another plan is 15% cheaper). AI only - a human teammate is never planned for. The assignment is pure C#, shared with the simulator. Feeds `CrystalTrackObjective.Planned`; rings are each pilot's own, so Regatta has no plan |
| `SkimRaceCourse` / `SkimRaceCourseSource` | The racing line: the track prisms the game actually laid, in lay order, with each prism's pose and contact shell |
| `SkimRaceObservation` / `SkimRaceAction` | The observation and action schema (feature vector, schema version, NaN sanitising, clamping) |
| `SkimRaceDriver` | The decision core (pure C#): racing line, crystal pass planning, lag-compensated steering, throttle, recovery |
| `SkimRacePlanner` | Optional model-predictive layer (rolls the Squirrel's own dynamics forward over a stick grid) |
| `SkimRaceShell` | The EXACT stella-octangula contact distance (the game's `ShieldShellMath` construction, cross-checked by `SkimRaceShellTests`), shared by the pilot and the simulator (§6.4) |
| `SkimRaceAIConfigSO` | The policy: every tunable. Ships as `Resources/SkimRaceAIConfig[_I<n>].asset`, authored by `Tools/Build/author_skimrace_ai_config.py`. A per-intensity file records the map it was tuned on (`TrackFingerprint`, §11) |
| `SkimRaceTrackFingerprint` | The map fingerprint: 8 hex digits over a track's path, curve setting, laps and crystal positions (whole units). Pure C#, shared with the simulator and `Tools/Build/skimrace_track_fingerprint.py` (§11) |
| `SkimRaceHandicap` / `SkimRaceDifficultySO` | The lobby AI difficulty's deliberate mistakes (slow reaction, misjudged crystal) and their per-difficulty numbers, one setting for every intensity (§10). Edits only the driver's BELIEF; null for Hard |
| `SkimRaceRaceRecorder` / `SkimRaceBenchmarkRunner` | The benchmark referee and driver (§7) |

### Input-only contract (enforced)

`Tools/Build/check_ai_no_state_writes.py` (`--check`, `--self-test`) fails the build if anything
under the AI trees (`Controller/AI/SkimRace`, `Utility/AITraining` - the genome path - and
`Editor/AI`) writes vessel pose or motion, speed/course/boost, crystal, score, winner or race state
or the time scale, or uses reflection. Five reviewed exceptions, each named in the script (the
genome copying READS into its own context struct; the overnight training batch's time scale, which
applies to every pilot and never runs in a benchmark). Both actuation points clamp to the human
stick/trigger ranges whatever the policy produced (`SkimRacePilot.Apply`,
`TrainingPilot.ApplyToInputStatus` - the genome's output was unclamped before this). The benchmark
no longer has a time-scale option, and the recorder fails any race during which `Time.timeScale`
left 1.

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
# fly an authored policy (run under bash: zsh does not word-split the argument list)
bash -c 'bash Tools/Build/skimrace_sim_harness/run.sh eval 2 40 $(python3 Tools/Build/skimrace_sim_harness/policy_args.py SkimRaceAIConfig_I2) ph.Seats=2 ph.Dt=0.026 ph.DtJitter=0.5'
bash Tools/Build/skimrace_sim_harness/run.sh tune 2 8 20 set=mpc over=1 ...   # over= scores seats above 70 s
bash Tools/Build/skimrace_sim_harness/run.sh geo 2     # anchor arc gaps; shell 1 = contact-distance landmarks
# ONE policy tuned on several tracks at once (the general SkimRaceAIConfig, §6.12)
bash -c 'bash Tools/Build/skimrace_sim_harness/run.sh tuneall 1,2,3,4 4 16 sigma=0.15 final=20 $(python3 Tools/Build/skimrace_sim_harness/policy_args.py SkimRaceAIConfig_I1) ph.Seats=2 ph.Dt=0.028 ph.DtJitter=0.5'
# has a map changed since its tuning? and the one command that retunes one intensity (§11)
python3 Tools/Build/skimrace_track_fingerprint.py --check
python3 Tools/Build/skimrace_retune.py 2
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

### 6.2 Intensities 2 and 4: what was tried, measured in the calibrated simulator

Every row below is a measurement over the same fresh seeds (I2, 12 races unless stated), with the
simulator attributing every boost reset to its cause. The baseline is the shipped I2 policy.

| Change | I2 median | Resets/race | Verdict |
|---|---|---|---|
| baseline (`skimrace-v1-i2`) | 106 s | ~15 (crossing 7, pull 5.5) | |
| **strikes switched off** (ceiling, not a policy) | **67-69 s** | 0 | the whole gap is hull strikes |
| direct crystal-to-crystal flight, boost-gated | 104-124 s | fewer, but circling | pickups hold 5x only above ~3.3x; approaches fail |
| + via-points round the ribbon | 104-123 s | | no gain |
| sequenced face change (swing out a look-ahead before height changes) | 104-109 s | crossing 7-9 | no gain |
| terminal flight only on a clear chord | 103-108 s | unchanged | no gain |
| line kept out of the slab band / beside-pass / side-pass | 106-123 s | pull up | no gain |
| predictive guard incl. track shells (+ tuned) | 98-106 s | ~9 | fewer strikes, evasions cost the same time |
| full MPC (50 commands x 1.5 s, strikes/skims/pickups modelled) | DNF | 1-4 | avoids strikes by leaving the track; never builds boost |
| tracking MPC (model-predictive line following) | 111 s | ~17 | cross-track p90 67 -> 28 u, strikes unchanged |

What blocks 70 s, as measured:
- **I2:** crystal anchors sit only 22-35 u from the ribbon, ringed round it, plus 35 u of random
  jitter against a 24 u capture radius. Most pickups therefore pass within a few units of a plate
  whose super-shield contact shell is 30 u wide and 3 u thick, at 200-300 u/s, with a heading that
  lags the stick by a 0.67 s time constant. Every strike costs the whole skim boost (reset to 1x)
  and up to half the speed, and rebuilding boost by skimming takes ~10 s at 60 u/s. With strikes
  removed the same policy finishes in 67-69 s; every controller tried either keeps ~9+ strikes
  per race or avoids them by flying slower or farther, and lands at ~98-110 s.
- **I4:** 54 crystals whose anchors sit ON the ribbon (distance 0), 35 u jitter, crystal-to-crystal
  gaps of 104-412 u and a 157-degree hairpin. About one crystal in five is out of reach from the
  current face, forcing a face change round the plate edge every few seconds. Best policy:
  completes, ~160 s.
- **I3:** 56 crystals over ~37,000 u needs 528 u/s; the Squirrel tops out at 300 u/s.

### 6.3 Multiple AI seats

Every Squirrel lays two trail rails at +-9.66 u, at its own height. With two AI seats on one racing
line, each strays sideways into the other's rails (I1, 2 AI seats: 13/30 sim races with BOTH seats
under 70 s). Each AI seat now flies its own **lane**: lane k skims `LaneHeightStep` (3 u) higher
than lane k-1, the lane being the seat's rank among the AI seats by domain then name - public facts
every machine agrees on (`SkimRacePilot.ResolveLane`). Sim, I1, 2 AI seats: 13/30 -> 22/30.
The simulator runs N seats in one world (`ph.Seats=N`), each with its own crystal stream, sharing
all laid mass, and scores a race by its SLOWEST seat.

### 6.4 The contact geometry was wrong (2026-10-03) - exact shell distance

`SkimRaceShell.StellaDistance` used to return a face-plane LOWER bound (the largest signed distance
to a tetrahedron's planes). For a convex solid that never overstates, but on the track plate's flat
shell (half-extents 15 x 1.5 x 4.5) every face plane is nearly horizontal, so beside the plate's long
edge it under-reported ~10.6x: a hull 11 u past the edge read **1.04 u**. The simulator uses this
one function for BOTH hull strikes and skims, and the pilot's guards use it too, so all three saw
contacts the game never registers (the game resolves the shell exactly, `ShieldShellMath`). It is
now the exact closest-point distance to the two tetrahedra (Ericson RTCD 5.1.5), proven against
`ShieldShellMath` by `SkimRaceShellTests`. Landmarks (`run.sh shell 1`): above the face +3 -> 3.000
(old 2.833); beside the edge +11 -> 11.000 (old 1.039); beyond a tip (2,1,3) -> 3.742 (old 1.700).

### 6.5 The crystal's position along the track was mis-projected

The pilot projected the crystal onto the course from the VESSEL's hint, searching +-24 segments
(~+-288 u) and accepting a windowed answer within 150 u. Measured anchor gaps along the track
(`run.sh geo`): I1 539 u each; I2 468-646 u; I4 186-415 u (21 of 27 above 250 u). So a far crystal
could match the end of the window and the swerve was placed early. The target now has its own hint,
reset on a new or moved crystal (`SkimRacePilot._targetHint`, mirrored in the simulator).

A/B on I1, 2 AI seats, 40 seeds (every seat judged): legacy shell + old projection 53/80 seats
<= 70 s; legacy shell + new projection 48/80; exact shell + old projection 49/80; both fixes 48/80.
**All within noise: neither fix regressed I1, and neither moved the needle by itself.**

### 6.6 What the simulator can and cannot be compared with

The simulator judges a race by its SLOWEST seat (the strict reading of "each AI <= 70 s"). The
editor benchmark cannot: a Skim Race ends at the FIRST finisher, so only the winning seat's time is
ever observed (the others' crystal counts at that moment are recorded). `run.sh eval` therefore also
prints the first-finisher statistic, which is the editor-comparable one. I1, 2 AI seats, current
policy: first finisher <= 70 s in 38-40 of 40 sim races (median ~60 s) - matching the editor's
9/10-10/10 - while EVERY seat <= 70 s in only 12-22 of 40.

### 6.7 Baselines and the strike-free ceiling (exact shell, both fixes, shipped policies)

40 seeds, 26 ms frames with 50% jitter, every seat judged:

| Cell | Every seat <= 70 s | Seat median | Race (slowest seat) median | Largest reset causes / race |
|---|---|---|---|---|
| I1, 2 AI | 12/40 | 68.8 s | 73.6 s | crossing 5.6, pull 5.3, pickup ring 5.1 |
| I1, 3 AI | 2/40 | 74.4 s | 86.3 s | pickup ring 13.4, pull 11.8, other seat's rail 10.8 |
| I2, 2 AI | 0/40 | 102.9 s | 112.3 s | pull 10.2, crossing 7.2 |
| I2, 3 AI | 0/40 | 105.7 s | 115.8 s | pull 13.5, other seat's rail 11.6, crossing 11.4 |
| I4, 2 AI | 0/40 | ~150 s | ~162 s | pull 9.4-10.1, crossing 5.7-6.2 |
| I4, 3 AI | 0/40 | - | none within 130 s | pull 12.7, other rail 10.5 |

**Ceiling: every hull contact switched off** (`ph.TrackHits=0 ph.MassHits=0`, 20 seeds) - the best
any pilot of this racing line could do:

| Cell | Race median | First finisher <= 70 s |
|---|---|---|
| I2, 1 AI | 66.5 s | 17/20 |
| I2, 2 AI, lane step 1 | 69.9 s | 17/20 |
| I2, 2 AI, lane step 3 | 83.9 s | 14/20 |
| I4, 1 AI | **123.7 s** | 0/20 |
| I4, 2 AI | 136.7 s | 0/20 |

**I4 cannot reach 70 s with this approach even with zero strikes.** I2 can, but only with
essentially zero strikes; it currently takes ~15-30 per race.

### 6.8 Every lever re-tested with the corrected geometry (I2/I4, 2 AI seats, 20 seeds)

Race median (slowest seat), each change on its own over the shipped policy:

| Change | I2 | I4 |
|---|---|---|
| none (shipped) | 109.4 s | 161.7 s |
| `TrackGuard=2` (guard includes the track shells) | 122.2 s | 172.1 s |
| `TerminalNeedsClearChord` | 110.4 s | 166.6 s |
| `ChordClearance=2` | 117.8 s | 171.9 s |
| `SidePassOverCrossing` | 133.1 s | 167.2 s |
| `BesidePassNoCrossing` | 118.0 s | 161.7 s |
| `SequencedCrossing` | 117.3 s | 167.5 s |
| `UseTrackMpc` + `TrackMpcStrikeCost=1000` (new) | 101.9 s | 173.8 s |
| `UseMpc` + `MpcStrikeUsesBoostLoss` (new) | DNF 0/20 | DNF 0/20 |
| `CrossingLeadSeconds=0.6` (new) | 114.0 s | 160.9 s |
| `CrossingLookaheadScale=0.5` (new) | 148.8 s | 177.3 s |
| `CrossingThrottle=0.7` (new) | 113.7 s | 167.3 s |

None reaches 70 s; the tracking-MPC strike term is the only one that improves I2 (~7%), and it costs
I4. A cross-entropy tune of 37 parameters (including every new field and `LaneHeightStep`) at 3 AI
seats, 20 iterations x 24 candidates x 8 races, validated on 40 fresh seeds: race median **115.9 s**
against the shipped policy's 115.8 s. **No policy change shipped.**

### 6.9 Lanes

`LaneHeightStep` 3 lifts the second AI seat ~1.5 u past the skimmer's reach over the plate's top, which
looked like the reason upper seats run slower (the I2 ceiling above). Re-measured on 40 fresh I1 seeds
it is within noise (2 AI: 53/80 seats <= 70 s at step 1 vs 57/80 at step 3; 3 AI: 56/120 vs 65/120) -
the stella's top is ridged, so an upper lane still skims part of the time. Left at 3.

### 6.10 Intensity 2, second pass: where the time goes, and three mechanisms (2026-10-03)

**Diagnosis** (`run.sh eval ... diag=1`, shipped I2 policy, 2 AI seats, 40 seeds, per seat per race):

| Phase | Time | Mean boost | Mean speed | Skimming | Track hits |
|---|---|---|---|---|---|
| pull (approaching a crystal) | 34.3 s | 3.11 | 168 | 38 % | 10.6 |
| post-pickup (1 s after each pickup) | 29.4 s | 3.68 | 191 | 25 % | 9.8 |
| crossing (face change) | 26.2 s | 2.51 | 138 | 18 % | 3.1 |
| line | 14.0 s | 1.84 | 97 | 41 % | 2.7 |

Face changes are NOT the main strike source; the approach to a crystal and the second after taking it
are (20 of ~26 hits). The hits are over the plate's FACE (|lateral| < 10, hull 1.6-2 u off the plate,
the shell top is 1.5) with only 10-17 deg of heading error: the straight terminal chord DESCENDS to a
pass point just above the plate, the crystal is taken ~24 u early, and the lagging hull keeps diving.
The hull also skims only 18-41 % of the time, because each crystal's raised-cosine swerve is +-266 u
wide against ~557 u gaps - the line is almost always detouring - so the boost sits at ~2.5-3.7.

**What each lever did** (20 seeds; "ceiling" = every hull contact off):

| Change | 2 AI race median | Notes |
|---|---|---|
| shipped | 109.9 s | ceiling 83.9 s |
| no terminal chord (`CrystalDirectDistance=0`) | 108.9 s | ceiling 93.0 s |
| sharper swerves (half-width 100-200, pass margin 2-6), no strikes | 151-218 s, mostly DNF | the hull cannot follow them: misses, orbits, recovery |
| `LaneHeightStep=1` | 101.3 s | ceiling **69.9 s** (11/20 races every seat <= 70 s) |
| lane 1 + tracking MPC strike term + no terminal chord | **96.8 s** | strikes pull 3.8 / crossing 3.6 per race; best real result |
| `UseLineTracker` (new, lag-inverting path tracker) | 154-218 s | tracks straights to ~0 u, lags the swerve's peak, misses the crystal |
| `CaptureThrottleSearch` (new, rollout-checked throttle) | 103.6-106.8 s | recoveries -> ~0, but the lifts cost more: 1-seat ceiling 66.5 -> 81.8 s |

**The 3-seat ceiling** (lane 1, strike-free) is 126.5 s race median although the seat median is
74 s: per-seat times such as [67.2 122.1 65.6] and [81.3 79.5 182.9] show one seat per race losing
8-21 crystal approaches to orbit-and-recover. The capture search removes that (race median 85.2 s,
recoveries 0.1/race) but only by flying slower everywhere.

**Two new mechanisms ship OFF** and are documented as negative results:
- `UseLineTracker` - commands the desired heading plus the turn the line makes over the hull's lag
  (curvature x speed x 1/FollowRate) with a cross-track correction. Correct on straights; pure
  pursuit's corner cutting is what actually takes the crystal.
- `CaptureThrottleSearch` - rolls the hull's dynamics forward at five throttles and flies the highest
  whose path does not pass the crystal outside its capture sphere. It removes the orbits, at a net loss.

**Stop condition met (prompt step 5): no line found has a strike-free ceiling under 70 s for 3 AI
seats, and the best 2 AI ceiling (69.9 s) leaves no margin for a single strike. No I2 policy change
shipped.** What the numbers say would be needed: a capture controller that takes crystals at full
speed without orbiting (the 1-seat strike-free pilot does it 17/20 times with pure pursuit) AND a
line that skims well over 40 % of the time - the two pull in opposite directions on this track,
whose crystals sit 22-70 u off the ribbon with a 24 u capture radius.

### 6.11 I2 target re-baselined to 80 s (2026-10-04)

**The decision.** The I2 limit is now **80 s**. That is a product decision made by the user, not an AI
result: 70 s is not reachable at I2 without changing the game for every pilot. Measured with the
rules changed for EVERY pilot (sim, 2 AI seats, 20 seeds, best config, untuned for each rule set):
track-plate contacts free -> winner <= 70 s in 3/20, median 73.6 s; laid-mass contacts free -> 5/20,
77.0 s; both free -> 11/20, 69.2 s. Even removing every contact penalty from the game is a coin flip, so
the game was left alone and the target moved. I1, I3 and I4 stay at 70 s
(`SkimRaceRaceRecorder.DefaultLimitSeconds`; the simulator copies the table and takes `limit=`).

**Structural levers tried first** (12 fixed seeds, 2 AI, winner median; baseline = lane step 1 +
tracking MPC strike term + no terminal chord, 79.7 s, 7/12 <= 80 s):

| Change | Winner median | <= 80 s | Verdict |
|---|---|---|---|
| `HullGuardSeconds` 0.67 (re-test on the exact shell) | 78.1 s | 7/12 | kept (tuned) |
| `HullGuardSeconds` 0.4 / 1.0 | 84.7 / 80.9 s | 3, 6 | noise |
| `SlabGuardSeconds` 0.67 | 84.7 s | 4/12 | rejected |
| `PickupClearDistance` 14 / 30 | 80.7 / 89.8 s | 5, 0 | noise / worse |
| new height-rate floor (predicted hull height over the plate, steer up past the tracking MPC) | 96-130 s | 0-4 | **rejected and removed**: the escape lifts throw the hull off the line and into the other seat's rails (other-rail resets 3.3 -> 6-7/race) |

None beat the baseline beyond 12-seed noise. **The tune did.** Winner-scored CEM (`score=winner
set=winner limit=80 over=1`, 2 AI seats, 28 ms frames with 50% jitter, 8 seeds/candidate; the
tunable set now includes `HullGuardSeconds`, `HullMargin`, `PickupClearDistance`) for 3 iterations,
then resumed from its best for 10 (the first run hit a background-job time limit). Tuner score 98.5 ->
70.4; its own final check (40 fresh seeds, seedbase 99000): winner <= 80 s in 38/40, median 74.1 s.

**Validation** (40 fresh seeds, seedbase 50000, 28 ms frames, every race scored; shipped
`skimrace-v1-i2` on the same seeds for comparison):

| | v1-i2 winner median | v1-i2 <= 80 s | **v2-i2 winner median** | **v2-i2 <= 80 s** | v2 winner p90 | v2 strikes/seat/race (track) |
|---|---|---|---|---|---|---|
| 2 AI | 97.0 s | 0/40 | **76.1 s** | **30/40** | 83.7 s | 4.6 |
| 3 AI | 98.7 s | 0/40 | **75.7 s** | **31/40** | 81.6 s | 6.7 |

The bar set before validating (winner median <= 76 s and >= 80% of winners <= 80 s at both seat
counts) was **missed narrowly** (76.1 s; 75% and 78%). Per the rule set in advance the policy ships
anyway because it beats v1-i2 by ~21 s and 0/40 -> 30-31/40. The remaining resets are mostly laid mass
(pickup rings 3.6/race at 2 AI, 8.5 at 3 AI; other seats' rails 1.9 / 7.1), which is where the next
gain is. Tuned values the code does not read under these switches (`Level*`, `CaptureMargin`,
`TerminalChordClearance`, `TrackGuardMargin`) are left at their defaults in the asset.

### 6.12 One general policy for every track (`tuneall`)

`SkimRaceAIConfigSO.LoadFor(intensity)` falls back to the general `SkimRaceAIConfig` for every
intensity with no file of its own - intensity 3 today, and any intensity a designer adds - and the
deployment also flies it on an intensity whose own file was tuned on a map that has since changed (§11). That
policy was tuned on no track in particular, so it was the weakest one shipped (I3: 2 of 20 races
finished in the simulator). `run.sh tuneall <i,j,...> <seeds> <iters>` tunes ONE policy on several
tracks together with the same cross-entropy loop as `tune` (population 24, elite 6, the same
`PursuitTunables`), so the result is the best compromise for a track it has never seen rather than
a specialist:

- **Each track is scored against its own length.** A race scores `time / ideal`, where ideal is the
  track's course length (measured along the racing line through every crystal anchor, times laps)
  at 300 u/s - so a long track cannot dominate the average. An unfinished race scores
  `(limit + 60 s) / ideal` plus twice the fraction of crystals missed, so finishing always beats not
  finishing and nearly finishing beats stalling.
- **Each track races to 3 x its ideal time** (at least the intensity's own limit), so a slow but
  finishing race is still measured instead of cut off.
- **A track scores its mean plus half its worst race; the policy scores the mean over tracks.**
- `final=N` re-checks the winner on N fresh seeds per track (seedbase 99000) and prints its table.

Ideal times on the shipped scene: I1 43.1 s, I2 55.7 s, I3 65.6 s, I4 54.7 s. The per-intensity
files stay the fast versions for the tracks they were tuned on; the general policy is what a
new or untuned track gets.

**Result: `skimrace-v2-general`** (2026-10-05; `tuneall 1,2,3,4 4 16 sigma=0.15 final=20` from the I1
policy, 2 AI seats, 28 ms frames +-50%, ~95 min). Tuner score 4.141 -> 3.666 over 16 iterations. Its
own check on 20 fresh seeds per track (seedbase 99000, races to 3x ideal):

| Track | Finished | Race median (slowest seat) | Worst | Winner median |
|---|---|---|---|---|
| I1 | 20/20 | 69.7 s | 86.7 s | 62.0 s |
| I2 | 20/20 | 112.0 s | 144.8 s | 105.7 s |
| I3 | 20/20 | 189.8 s | 219.4 s | 176.7 s |
| I4 | 20/20 | 188.3 s | 205.9 s | 157.1 s |

It is the compromise it was tuned to be: slower than each specialist on that specialist's own track
(the I2 file's winner median is ~75 s), and the only policy that finishes every track. I3 - the
one shipped track that flies it - is where it replaces `skimrace-v1`, raced on the SAME 20 seeds and
limit (`eval 3 20 limit=197 seedbase=99000`, 2 AI seats):

| I3, general policy | Races with every seat finished | Winner median | Seat median | Hull strikes / race |
|---|---|---|---|---|
| `skimrace-v1` | 5/20 | 244.8 s | 256.8 s | ~330 |
| **`skimrace-v2-general`** | **20/20** | **176.7 s** | **189.8 s** (race median) | |

## 7. Running the benchmark

In the editor: **FrogletTools > AI > Skim Race AI Benchmark** (races, intensity, players), or drop
`Library/SkimRaceAIRemote/command.json` = `{"op":"bench","races":10,"intensity":1,"players":2,"timeout":120}`
(the limit defaults to the intensity's, §1; add `"limit":N` to override)
into an open editor (`SkimRaceBenchmarkRemote`; also `{"op":"tests"}` runs `SkimRaceAITests`).
Each race appends a JSON record (crystal timestamps, authoritative finish time, frame time, recoveries,
policy) to `BenchmarkResults/SkimRaceAI/` (git-ignored). Summarise with
`python3 Tools/Build/skimrace_benchmark_report.py <file.jsonl> [--markdown]`.

## 8. Results

### 8.0 Re-validation after the geometry fixes (2026-10-03, afternoon) - NOT completed

The pilot code changed (exact shell distance in its guards, per-target course projection - §6.4,
§6.5); the policies did not. The matrix below (§8.1) was therefore re-run with the AI-seat counts
the requirement names (2 and 3 AI seats = 3 and 4 players including the idle host), and every launch
had to be thrown out for the frame-rate reason §6.1 records: the machine was in active use (Rider
indexing at ~200% CPU, Discord and Notion foreground, load average 18-28 on 8 cores) and the editor
could not hold focus.

| Session | Cell | Races | Frame ms (mean) | Disposition |
|---|---|---|---|---|
| `skimrace_I1_20261003-135603` | I1, 1 AI (wrong cell) | 0 finished | - | aborted: started with players=2 by mistake |
| `skimrace_I1_20261003-140053` | I1, 2 AI | 5 (3 complete, 107-116 s; 2 timeouts) | 151-193 (max 1031) | **invalid** - ~5x the 32-37 ms the matrix was calibrated at |
| `skimrace_I1_20261003-141329` | I1, 2 AI | 1 (complete, 120.3 s) | 207.5 (max 847) | **invalid**; focus lost to another app mid-race |

These are environment results, not AI results (the simulator at 115 ms frames already degrades the
same policy from 53 s to 77 s). **The in-editor matrix for the current code is still owed** - run it
on an idle machine with the editor focused (§7), 2 launches x 5 races per cell, players 3 and 4.

### 8.0i The AI seats no longer re-plan in the same frame (2026-10-07)

The user's choice from §8.0h's list: stagger the seats. **Mechanism:** `SkimRaceReplanGate`, one per
process (`SkimRacePilot` shares it across every AI seat), lets ONE track-planner re-plan claim a frame. A
seat that finds its frame taken flies its previous plan one frame longer and re-plans in the next. It
never waits twice: next frame it re-plans whether or not that frame is free. Each seat keeps its own
`TrackMpcHz` clock, so the re-plan rate and the average cost do not change. `TrackMpcStaggerSeats`
(on by default, in every policy asset) turns it off. Only the I2 policy flies the track planner today.

**Why seats shared frames.** Each seat schedules its next re-plan as `now + 1/Hz`. Once two seats re-plan
in the same frame, they compute the same next time and stay together. In the simulator, 86-89% of
re-plan frames had both seats; the editor's `prof` saw about half.

**Cost** (simulator on Mono in double precision, the editor's mode; I2, 2 seats, 18 ms frames ±30%,
6 races per arm; AI thinking per frame, both seats together):

| | Stagger off | Stagger on |
|---|---|---|
| Frames with 2 re-plans | 89% of re-plan frames | 0% |
| p90 / p99 | 11.0 / 15.0 ms | **6.8 / 9.6 ms** |
| Median | 0.8 ms | 4.2 ms (the same work, spread over more frames) |
| Average per seat | 1.89 ms | 1.87 ms |
| One re-plan | 4.7 ms | 4.7 ms (the editor measured ~4 ms) |

**Racing** (.NET, the shipped I2 policy, 2 seats, same seeds in each arm): no detectable change.

| Frames | Races per arm | Seat time, on - off (paired) | Seats <= 80 s, on / off | Unfinished races, on / off |
|---|---|---|---|---|
| 18 ms ±30% (the editor at 55 fps) | 280 | +0.59 s (SE 0.56, t 1.05) | 49.5% / 47.9% | 3 / 2 |
| 26 ms ±50% (the tuning setting) | 120 | +0.70 s (SE 0.65, t 1.08) | 57.1% / 59.2% | 0 / 1 |

Mann-Whitney on all seat times: z +0.42 and +0.87. The pooled estimate is +0.6 s per ~80 s seat (SE 0.4),
inside the noise. A seat waited on 4% of its re-plans at 18 ms frames and 9% at 26 ms.
The unfinished races are the policy's known orbit-and-recover (a slow seat circling a crystal it cannot
turn into, §6.10). One was traced in full: the seat orbited for 16 s after the other seat had finished, when no
stagger was active. Both arms have them.

With `TrackMpcStaggerSeats` off, the new code races byte-identically to the previous commit (I2, 12 seeds).
The I1 policy, which does not fly the track planner, is identical with it on (6 seeds).

Off-editor proof: `SkimRaceAITests` gains four tests (the gate; two seats due together, the second
waiting one frame; no seat waiting twice; no gate, no change). Three mutations of the wait rule, each
failing a test. The simulator's eval prints the planner's re-plans, the frames shared and the waits.

### 8.0h The perf branch in the editor: 35 -> 55 fps, and what is left (2026-10-07)

`diag S_SkimRace_I2 15` and one `prof` in a hand-played I2 race with 2 AI seats, same machine and
settings as §8.0f, on `perf/performance-optimization` `df25d942f` (`Ys-bleeding-edge` plus §8.0e-g).
All five `SkimRaceCourseQueryTests` pass in the editor (§8.0g's fix, first seen green here).

| What | 10-06 (§8.0f) | 10-07 |
|---|---|---|
| Frame avg / p95 / p99 | 28.3 / 39.4 / 43.4 ms (35 fps) | **18.0 / 26.9 / 33.3 ms (55 fps)** |
| PlayerLoop | 25.3 ms | 15.2 ms |
| `SkimRace.Pilot.Decide` (2 seats) avg / p50 / p95 / max | 7.20 / 2.5 / 16.1 / 18.5 ms | **3.35** / 0.5 / 11.1 / 24.1 ms |
| `SkimRace.Pilot.FillObstacles` | 2.12 ms | 0.95 ms |
| Garbage | 78 KB/frame | 27 KB/frame; `Decide` allocates nothing |
| Prism entities in the race | 8362 | 5717 |
| GPU | 6.7 ms | 3.6 ms |

**Not all of the 10 ms is this work.** The two races differ (32% fewer prisms, half the GPU time), and
systems this work never touched fell too (`Fauna.BodySync` 0.94 -> 0.27 ms, `LightFauna.Tick.PrismScan`
0.71 -> 0.06). The like-for-like number is the AI's own markers: Decide + FillObstacles 9.3 -> 4.3 ms
a frame, and FillObstacles scales with the prisms in range, so part of its drop is the smaller race.

**The `prof` was taken while Burst was still compiling.** Three `[BurstCompile]` jobs ran as managed
code (`ExecuteJobFunction.Invoke` under them; a Burst job's sample is "`<name> (Burst)`"):
`ShellContact.Query` 1.32 ms a frame against 0.08 ms in all three 10-06 captures of the same,
unchanged code, `LOD.Sweep` 0.36 vs 0.08, `PrismRender.TransformFlush` 0.15. The Editor compiles
Burst in the background after a script change or branch switch and runs the managed version until it
is done. The `diag` ran 43 s earlier and did not time those jobs, so it may carry some of this too.
`prof` now reports any managed job time and warns above 0.1 ms a frame; `diag` times
`ShellContact.Query` (its tell) and records the Editor's Code Optimization mode, which neither
capture could say.

**What is left in the AI: the track planner's bursts.** `SkimRace.Driver.TrackMpc` is 2.89 ms of the
3.35 ms Decide. It re-plans at 20 Hz with 26 rollouts of 16 steps each, about 4 ms per seat per
re-plan on editor Mono, and it lands in 35% of frames. Both seats
re-plan in the same frame about half the time (prof: 1.5 calls per frame it appears in). The 47 ms
spike frame had both, 8.1 ms, beside a managed `LOD.Sweep` (2.7 ms) and 11 boid coroutines (5.2 ms).
`GuardMass` is 0.14 ms (p95 0.9). Every remaining lever changes timing, so each needs a decision and
the simulator's 20-seed benchmark:

1. **Stagger the seats.** Offset each seat's re-plan phase so no two share a frame. The average stays
   the same and the per-frame peak halves. Each seat still re-plans at 20 Hz; only the moment it does so moves.
   **Done, §8.0i.**
2. **Spread one re-plan over the frames between.** The 26 rollouts go across about 3 frames, so the peak drops ~3x and
   the plan acted on is 1-2 frames older.
3. **Burst the rollouts.** A job over the 26 candidates, off the main thread. It is the largest win
   (likely an order of magnitude on this cost; not measured), and also a project: the course goes into native arrays. Burst floats
   differ from Mono's, so the races are not byte-identical; the benchmark has to show the policy is
   no worse.

### 8.0g The editor computes floats in double precision - and caught the float rewrite (2026-10-06)

The first editor run of `SkimRaceCourseQueryTests` failed three of five
(`Project_MatchesThePlainWindowedSearch`, `StellaDistance_FloatKernelMatchesTheVectorForm`,
`ObstacleLocalFrame_MatchesInverseRotationTimesOffset`) although all five passed on .NET and on stock
Mono. Reproduced off-editor exactly - the same three fail, the same two pass - with
`mono --optimize=-float32`: **Unity's editor Mono computes inside an expression in DOUBLE precision**
and rounds to float only where a value lands in memory. A `Vector3` component always lands in memory,
so the old code rounded after every operator; the float rewrite (§8.0f) folded several operators into
one expression and, once the JIT optimizes, kept float LOCALS in double registers too. Same algorithm,
different last bits - and in a chaotic race, different decisions. The earlier "byte-identical" proof
held only on single-precision runtimes (.NET, stock Mono, IL2CPP players).

Measured: the OLD `Vector3` code agrees with itself bit for bit between Mono double precision with and
without JIT optimizations (0 of 40,000 values differ), and differs from single precision in about half
of them. So the editor's own answer was stable, and the rewrite now reproduces it.

**Fix:** every value the `Vector3` form rounds - each component of a Vector3 it builds, each float it
returns or passes - is rounded in the rewrite with an explicit `(float)` (C#'s defined way to force
float precision, which the JIT must honour); expressions the `Vector3` form keeps whole stay whole; its
own locals stay plain locals. `SkimRaceObstacle.LocalFrame` keeps the nine quaternion products and
evaluates each component as Unity's operator does, instead of pre-rounded 3x3 terms.

**Proof:** all five `SkimRaceCourseQueryTests` pass on .NET and on Mono in single precision and in four
double-precision configurations (optimized, all optimizations, Debug IL without inlining, no
optimizations); dropping ONE of the roundings fails the stella test in double precision (and passes in
single, which is why the first suite could not see it). Simulator race output, old code vs new, Mono
double precision (the editor's mode), 2 seeds, decide cost per seat per frame: I2 identical, 2.805 -> 1.986 ms;
I4 identical, 0.845 -> 0.575 ms (~1.4x - less than §8.0f's single-precision 2.1x, because double
precision is what the editor runs and the explicit roundings cost a little). .NET, I1/I2/I4 x 6 seeds:
identical. Note the double-precision Mono cost of the OLD code (2.8 ms per seat at I2) is close to the
editor's measured 3.6 ms; single-precision Mono (1.9 ms) was not - this is the mode to predict with.

The simulator's Mono mode now runs double precision by default (`SKIMRACE_RUNTIME=mono` adds
`--optimize=-float32`; `SKIMRACE_MONO_OPTS=""` for stock Mono).

### 8.0f The first editor measurement of the pilot, and what it changed (2026-10-06)

`diag S_SkimRace_I2 15` and three `prof` captures in a hand-played I2 race with 2 AI seats (editor
6000.3.17f1, Mono, Ultra, 1920x1080 windowed, i7-8700K, RTX 4070 Ti, on `059450b16`):

| What | Measured |
|---|---|
| Frame | 28.3 ms avg (35 fps), p95 39.4 ms, CPU-bound (GPU 6.7 ms) |
| `SkimRace.Pilot.Decide` (2 seats) | 7.2 ms/frame avg, p50 2.5, p95 16.1 - the 20 Hz track planner fires as a burst |
| `SkimRace.Pilot.FillObstacles` (2 seats) | 2.1 ms/frame |
| PlayerLoop with / without the AI flying | ~24 ms / 12.6 ms (a capture taken with no pilot marker in it) |
| Garbage | 78 KB/frame; 36 KB of it in `Decide`, 840 allocations |
| Editor Mono vs this simulator's .NET | 3.6 vs 0.30 ms per seat per frame |

So the AI was the largest single cost in the frame, and it ran ~12x slower in the editor than the
simulator said. Two reasons, each fixed exactly (decisions unchanged):

1. **840 allocations a frame.** `UnityEngine.Mathf` has no 3-argument `Min`/`Max`, so
   `Mathf.Max(a, b, c)` in the steering law bound to `params float[]` and allocated per call. The
   simulator's Unity shim DEFINED 3-argument overloads, which is why it never saw them. Now
   `MathfNoAlloc` (Utility; Unity's own loop, no array), the shim matches Unity, and
   `Tools/Build/check_mathf_params_alloc.py` fails any 3-value `Mathf.Min/Max` in runtime code.
2. **Mono pays for every Vector3 operator.** The planner's innermost loops - the segment test in
   `Project`, the per-prism step and the stella kernel in `ShellClearance`, the laid-mass box test
   (`SkimRaceObstacle.LocalFrame`) - now do the same float operations in the same order on scalars.
   Measured on Mono 6.8 (the editor's runtime family), decide cost per seat per frame, 3 AI seats:
   I2 1.926 -> 0.902 ms, I4 0.562 -> 0.243 ms. The simulator now runs on Mono too:
   `SKIMRACE_RUNTIME=mono bash Tools/Build/skimrace_sim_harness/run.sh eval ...` (the SDK's Roslyn
   against Mono's class libraries; same race output as .NET, the editor's cost profile).

Proof for both: race output byte-identical to the previous code on .NET (I1/I2/I4 x 6 seeds) and on
stock Mono (I2/I4 x 2 seeds) - both SINGLE precision; the editor computes in double, and §8.0g is what
that changed; `SkimRaceCourseQueryTests` pins the float stella kernel and the box frame
to their Vector3 forms bit for bit and fails when ONE sum is re-associated.

Also from the same captures: `FillObstacles` reads its per-frame inputs once and drops redundant
liveness checks (child markers `.Query` / `.Pack` now split it); skim beams (`SkimFxRunner`) are
recycled instead of instantiated per prism contact (~0.33 ms/frame, 1.3 ms spikes); prism `Awake`s
use `TryGetComponent` (editor GC per trail prism). NOT changed: the `Squirrel Prism` pool misses
~1.7 times a frame because a race lays mass faster than its refill (40/s), but a faster
`InstantiateAsync` refill moves the cost into a per-frame integration budget rather than removing
it - `PoolMiss.Squirrel Prism` vs `PoolRefill.Squirrel Prism` in the next `prof` decides.

**Next measurement:** the same `diag` and `prof`, with the editor's Code Optimization set to
**Release** (the bug icon, bottom-right) - Debug mode turns the JIT's optimizations off - and one
`diag` in a Development build. Not yet done, and each changes timing (so the simulator's 20-seed
benchmark re-checks finish times first): stagger the seats' 20 Hz track-planner bursts, spread one
burst over the frames between, or `DecisionHz` 30.

### 8.0e The pilot's frame cost halved, every decision unchanged (2026-10-05)

Every AI seat runs `SkimRaceDriver.Decide` on every frame (`DecisionHz` is 0 in all four configs),
inside the game's own frame time. Simulator, 3 AI seats, 6 seeds, decide cost per seat per frame in
optimized .NET (the editor runs Mono, which has not been measured against it), the old and the new
code run one after the other (an interleaved re-run - old, new, old, new - gave I1 0.105 -> 0.049 and
I2 0.532 -> 0.298):

| Cell | Before (ms) | After (ms) | Where the time was |
|---|---|---|---|
| I1 | 0.108 | 0.049 | the laid-mass guard tested every gathered prism at every rollout step (0.085 of 0.114) |
| I2 | 0.563 | 0.303 | the tracking MPC's track-shell queries (`ShellClearance` 0.23, `Project` 0.11), then the guard's prism tests (0.12) |
| I4 | 0.184 | 0.079 | the guard's track-shell queries (0.09) and prism tests (0.04) |

Three changes, each exact by construction:

- **Laid-mass broadphase** (`SkimRaceDriver.BuildObstacleGrid`). Each guard decision snapshots the
  gathered boxes once (inverse rotation and reach precomputed with the same expressions) into a hash
  grid whose cell is 5% wider than the largest reach, and a rollout step walks only the 27 cells
  round it. That is a superset of every box that can pass the reach test, the test still runs on
  each, and a minimum does not depend on order. A step or box the grid cannot place exactly
  (non-finite, or beyond 65,536 cells) drops to the old scan: a NaN position measures 0 clearance
  to every box (`Mathf.Max(NaN, 0)` is 0), so it must veto from any cell, as it did.
- **`SkimRaceCourse.ShellClearance`**: starts at the hint prism and works outward, and rules a prism
  out by the sphere through its box's corners, then by the box itself (the stella is inscribed in
  it), before the exact 8-triangle test. Exact stella tests fell 3.7x (I2: 106.5 M -> 28.7 M). A
  prism is skipped only when a bound loses by 0.01 u, far beyond the float rounding of either side,
  and ties are broken by offset as the old -window..+window scan broke them.
- **`SkimRaceCourse.Project`** reads each segment's vector and squared length from a table built at
  construction with the same expressions, and walks its window without two integer modulos per
  segment.

**Proof.** Every line of the simulator's race output (finish times to 0.01 s, hull and recovery
counts, boost resets by cause, cross-track percentiles) is byte-identical to the old code at I1, I2
and I4 over 6 seeds and at I2 and I4 over 20 seeds, 3 seats each; only the cost line differs. A
single changed decision would move a whole race. `SkimRaceCourseQueryTests` pins both course queries
to their plain definitions bit for bit over thousands of seeded points (inside overlapping shells,
on spike tips, on a course short enough for the window to wrap) and fails on each of three
deliberate breaks: a flipped tie-break, an unsafe bound, a window shifted by one.

**What this does not explain.** §8.0c's ~127 ms hand-played frame. The AI's whole planning cost was
about 0.5 ms per seat per frame in .NET before this; even several times that in Mono is a small part
of 127 ms. The editor number needs measuring, not estimating: `diag` now times
`SkimRace.Pilot.Decide` and `SkimRace.Pilot.FillObstacles` by default.

### 8.0d Hand-played I2 at a normal frame rate: under 80 s (2026-10-04)

Same recorder file (`manual_I2_20261004-181400.jsonl`), races 4-9, after the editor's frame time came
down from ~127 ms to ~55 ms. Policy `skimrace-v2-i2`, 3 players (human seat + 2 AI). Every race listed:

| Race | Winner (s) | Frame ms |
|---|---|---|
| 4 | 81.9 | 57.9 |
| 5 | **69.6** | 56.1 |
| 6 | **76.6** | 52.6 |
| 7 | **74.4** | 55.9 |
| 8 | **68.5** | 55.4 |
| 9 | **67.4** | 56.2 |

**The last 5 consecutive races are under 80 s (67.4-76.6 s, median 69.6 s); 5 of 6 since the frame
rate recovered.** Race 3 (125.2 s) is invalid - the game was paused mid-race (`timeScale` 0). With
§8.0b's background benchmark (median 72.6 s) this makes the I2 80 s target met at normal frame rates.

### 8.0c Hand-played races are slower because the editor renders at ~8 fps (2026-10-04)

Hand-played I2 races, 3 players (human seat Joseph + 2 AI), recorded by the new editor-only recorder
(`manual_I2_20261004-181400.jsonl`, every race listed), policy `skimrace-v2-i2` on both AI seats:

| Race | Winner (s) | Frame ms (mean / max) | Joseph's crystals |
|---|---|---|---|
| 0 | 92.8 | 126.8 / 256 | 2 |
| 1 | 90.3 | 128.7 / 245 | 2 |
| 2 | 128.2 | 126.3 / 287 | 4 |

Earlier unrecorded hand-played races: 103.7, 105.6, 117.3, 123.0 s (one with Joseph on 0 crystals,
so the human seat's trail is not the cause). **Same policy, same race; the difference is the frame
rate.** Simulator, same 12 seeds, only the frame time changed: 35 ms -> winner median 73.7 s, 11/12
<= 80 s; 127 ms -> 94.0 s, 2/12. The hull turns and moves in 38 u steps at 127 ms and 300 u/s, so
pass points are overshot and plates are struck between samples.

Why the editor renders at 127 ms while being played by hand (and at 33-46 ms in the background
benchmark) is NOT yet established. Observed during hand play: quality Very High with vsync on, a
different editor layout. Candidates in cost order: Game view size/resolution on a Retina display,
quality level, the Scene view rendering beside the Game view. Check with the Game view's Stats
overlay. A player build is the representative frame rate; whether the AI must also hold 80 s at
~8 fps in the editor is an open decision.

Note: on macOS, Unity rewrites its preferences plist on quit and DROPS `NSAppSleepDisabled` (it was
gone after a restart). Re-apply it before every background benchmark session.

### 8.0b v2-i2 editor matrix (2026-10-04, valid - every race listed)

Branch `feat/skimrace-ai` at `02da300fe` (+ report fix), editor 6000.3.17f1, Unity in the
BACKGROUND behind Rider for the whole matrix but no longer throttled: macOS App Nap disabled for
Unity (`defaults write com.unity3d.UnityEditor5.x NSAppSleepDisabled -bool YES`) and Unity's
Interaction Mode set to No Throttling. Control first: I1 1 AI, 19.5 ms frames, 70.2 / 67.7 s. Load
average 1.8-3.0 throughout. Limit = the intensity's default (I2 80 s, I1 70 s).

| Cell | Races | Winner <= limit | Winner finishes (s) | Frame ms |
|---|---|---|---|---|
| I2, 3 players (2 AI, separate domains) | 10 | **7/10** | 69.3, 81.6, 63.9, 72.8, 127.7 (3 recoveries), 74.8, 90.1 (2 recoveries), 72.3, 72.4, 66.7 - median **72.6** | 33-43 |
| I2, 4 players (3 AI: two share Ruby, one Gold) | 10 | **10/10** | 74.5t, 62.2t, 68.0t, 70.8t, 72.6t, 74.1, 74.8, 76.1, 78.1, 79.1t - median **74.3** | 39-46 |
| I1, 3 players (2 AI) - regression | 5 | **5/5** | 65.6, 59.9, 58.8, 58.1, 53.7 - median 58.8 (2026-10-03: 10/10, median 60.6) | 26-28 |

`t` = a TEAM win: with 4 players on 3 domains the benchmark puts two AI seats on one domain, and the
game ends the race when the DOMAIN's summed crystals reach 30 (each of the pair had 12-18). Those
are real game wins but not one AI collecting all 30. Counting only solo AI wins at 4 players: 4/4 within
80 s (74.1-78.1 s). The report script now judges a race by the domain total, as the game and the
recorder do, and marks team wins.

Read against the simulator (§6.11: 2 AI winner median 76.1 s, 30/40 <= 80 s): the editor did as well or
better (median 72.6 s, 7/10). The misses are both recoveries: a missed crystal costs an orbit and a
recovery (`StallSeconds` 5.8), which is the next thing to fix.

### 8.0a v2-i2 editor runs (2026-10-04) - all INVALID (editor throttled in the background)

Every run used `skimrace-v2-i2` and every one ran at 111-134 ms frames (the matrix is calibrated at
25-45 ms), with Rider the frontmost app throughout - `osascript ... activate` did not hold Unity in
front. The control proves it is the environment, not the new policy: I1 with the UNCHANGED
`skimrace-v4-i1` (1 AI seat) ran at 112 ms frames and won in 89.1 / 114.1 s, where on 2026-10-03 it ran
at 24-26 ms and 53-74 s.

| Session | Cell | Winners (s) | Frame ms |
|---|---|---|---|
| `skimrace_I2_20261004-144446` | I2, 2 AI | 51.5*, 115.0, 115.8, 110.8, 119.0 | 74-132 (*race 0: 3.7 s stall, clock suspect) |
| `skimrace_I2_20261004-151146` | I2, 2 AI | 111.8, 107.3, 119.7, 113.5, 109.7 | 129-134 |
| `skimrace_I2_20261004-154017` | I2, 2 AI (after the perf fix) | 141.5, 116.5, 123.8, 137.9, 124.8 | 123-126 |
| `skimrace_I1_20261004-160736` | I1, 1 AI (control) | 89.1, 114.1 | 112-114 |

One real defect surfaced on the way: v2's tracking MPC cost 1.75 ms per seat per frame in the
simulator (40x v1) because it recomputed every frame instead of at `TrackMpcHz`; now 0.37 ms
(commit `perf(ai): cut the Skim Race pilot's per-frame decision cost`). **The v2-i2 editor result is
still owed**: run the matrix with Unity frontmost for the whole run (close or minimise Rider).

### 8.1 Validation matrix before the geometry fixes (2026-10-03, every race listed, none selected)

Branch `feat/skimrace-ai`, editor 6000.3.17f1, focused, no other load. Policies:
`skimrace-v4-i1`, `skimrace-v1-i2`, `skimrace-v1-i4` (lanes on). Each cell is two separate fresh
Play-mode launches of 5 races (races 2-5 of each launch are the in-process restart). Every race went
through the normal arcade flow, Ready via the HUD handler, timed by the game's own clock, with crystal
placement drawn by the game. With two AI seats the race ends at the FIRST finisher, so the judged
AI is the winning one; the other AI's crystals at that moment are listed (its own finish time is
not observable).

| Cell | Complete | Within 70 s | Best / median / mean / worst (s) | Other AI at end | Frame ms |
|---|---|---|---|---|---|
| I1, 2 seats | 10/10 | **9/10** | 53.0 / 59.1 / 60.8 / 73.9 | - | 24-26 |
| I1, 3 seats | 10/10 | **10/10** | 51.9 / 60.6 / 60.0 / 69.1 | 20-23 of 24 | 32-37 |
| I2, 2 seats | 10/10 | **0/10** | 80.5 / 100.8 / 100.1 / 116.2 | - | 28-30 |
| I2, 3 seats | 10/10 | **0/10** | 89.9 / 101.6 / 101.0 / 110.4 | 27-29 of 30 | 37-45 |
| I4, 2 seats | 10/10 | **0/10** | 137.6 / 152.3 / 152.5 / 170.5 | - | 32-34 |
| I4, 3 seats | 10/10 | **0/10** | 142.0 / 149.0 / 151.5 / 163.2 | 49-53 of 54 | 46-52 |

Runs excluded, and why (all disclosed, none are AI results):
- `skimrace_I2_20261003-080837` (I2, 3 seats): 2 races completed (97.6 s, 98.7 s, both counted
  nowhere above but consistent with the cell), then the replay stalled: `MiniGameHUD.Update` threw a
  `MissingReferenceException` every frame on a destroyed `Player` (fixed in this branch), and the
  runner had no start deadline. The cell was re-run as `skimrace_I2_20261003-105712`.
- `104807`, `104912`, `105040`, `105306`: harness bug - the runner pressed Ready every 2 s and each
  press RESTARTS the countdown, so no race started. Fixed (one press, then let the countdown run).

#### I1, 2 seats (host + 1 AI)

| # | session | race | int | crystals | finish (s) | result | other AI at end | recov | mean u/s | frame ms |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 20261003-065502 | 0 | 1 | 24/24 | 55.14 | PASS | - | 0 | 225 | 24.3 |
| 2 | 20261003-065502 | 1 | 1 | 24/24 | 59.22 | PASS | - | 0 | 198 | 24.3 |
| 3 | 20261003-065502 | 2 | 1 | 24/24 | 53.04 | PASS | - | 0 | 224 | 24.4 |
| 4 | 20261003-065502 | 3 | 1 | 24/24 | 66.71 | PASS | - | 0 | 179 | 25.0 |
| 5 | 20261003-065502 | 4 | 1 | 24/24 | 66.32 | PASS | - | 1 | 186 | 25.8 |
| 6 | 20261003-070359 | 0 | 1 | 24/24 | 73.89 | FAIL: finish 73.89s > limit | - | 0 | 165 | 25.6 |
| 7 | 20261003-070359 | 1 | 1 | 24/24 | 56.81 | PASS | - | 0 | 211 | 23.9 |
| 8 | 20261003-070359 | 2 | 1 | 24/24 | 56.43 | PASS | - | 0 | 208 | 24.1 |
| 9 | 20261003-070359 | 3 | 1 | 24/24 | 61.43 | PASS | - | 0 | 198 | 25.4 |
| 10 | 20261003-070359 | 4 | 1 | 24/24 | 58.95 | PASS | - | 0 | 209 | 25.2 |

#### I1, 3 seats (host + 2 AI)

| # | session | race | int | crystals | finish (s) | result | other AI at end | recov | mean u/s | frame ms |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 20261003-071310 | 0 | 1 | 24/24 | 51.91 | PASS | 20/24 | 0 | 218 | 32.0 |
| 2 | 20261003-071310 | 1 | 1 | 24/24 | 54.73 | PASS | 20/24 | 0 | 215 | 32.9 |
| 3 | 20261003-071310 | 2 | 1 | 24/24 | 58.07 | PASS | 22/24 | 0 | 216 | 35.4 |
| 4 | 20261003-071310 | 3 | 1 | 24/24 | 60.47 | PASS | 23/24 | 0 | 197 | 33.0 |
| 5 | 20261003-071310 | 4 | 1 | 24/24 | 56.15 | PASS | 23/24 | 0 | 214 | 32.6 |
| 6 | 20261003-072153 | 0 | 1 | 24/24 | 60.77 | PASS | 23/24 | 0 | 195 | 32.8 |
| 7 | 20261003-072153 | 1 | 1 | 24/24 | 65.37 | PASS | 22/24 | 0 | 173 | 34.9 |
| 8 | 20261003-072153 | 2 | 1 | 24/24 | 61.93 | PASS | 22/24 | 0 | 182 | 37.4 |
| 9 | 20261003-072153 | 3 | 1 | 24/24 | 61.50 | PASS | 21/24 | 0 | 196 | 34.4 |
| 10 | 20261003-072153 | 4 | 1 | 24/24 | 69.09 | PASS | 22/24 | 0 | 172 | 35.6 |

#### I2, 2 seats

| # | session | race | int | crystals | finish (s) | result | other AI at end | recov | mean u/s | frame ms |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 20261003-073115 | 0 | 2 | 30/30 | 97.76 | FAIL: finish 97.76s > limit | - | 0 | 155 | 28.2 |
| 2 | 20261003-073115 | 1 | 2 | 30/30 | 112.18 | FAIL: finish 112.18s > limit | - | 0 | 128 | 28.3 |
| 3 | 20261003-073115 | 2 | 2 | 30/30 | 85.55 | FAIL: finish 85.55s > limit | - | 0 | 181 | 28.6 |
| 4 | 20261003-073115 | 3 | 2 | 30/30 | 116.17 | FAIL: finish 116.17s > limit | - | 0 | 131 | 29.2 |
| 5 | 20261003-073115 | 4 | 2 | 30/30 | 101.17 | FAIL: finish 101.17s > limit | - | 0 | 152 | 28.5 |
| 6 | 20261003-074355 | 0 | 2 | 30/30 | 80.53 | FAIL: finish 80.53s > limit | - | 0 | 190 | 27.8 |
| 7 | 20261003-074355 | 1 | 2 | 30/30 | 96.93 | FAIL: finish 96.93s > limit | - | 0 | 156 | 29.3 |
| 8 | 20261003-074355 | 2 | 2 | 30/30 | 107.05 | FAIL: finish 107.05s > limit | - | 0 | 142 | 29.6 |
| 9 | 20261003-074355 | 3 | 2 | 30/30 | 100.50 | FAIL: finish 100.50s > limit | - | 0 | 156 | 29.5 |
| 10 | 20261003-074355 | 4 | 2 | 30/30 | 103.26 | FAIL: finish 103.26s > limit | - | 0 | 148 | 29.6 |

#### I2, 3 seats

| # | session | race | int | crystals | finish (s) | result | other AI at end | recov | mean u/s | frame ms |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 20261003-075609 | 0 | 2 | 30/30 | 105.36 | FAIL: finish 105.36s > limit | 27/30 | 0 | 135 | 37.8 |
| 2 | 20261003-075609 | 1 | 2 | 30/30 | 96.51 | FAIL: finish 96.51s > limit | 29/30 | 0 | 153 | 42.1 |
| 3 | 20261003-075609 | 2 | 2 | 30/30 | 109.31 | FAIL: finish 109.31s > limit | 29/30 | 0 | 135 | 40.4 |
| 4 | 20261003-075609 | 3 | 2 | 30/30 | 89.92 | FAIL: finish 89.92s > limit | 29/30 | 0 | 166 | 39.1 |
| 5 | 20261003-075609 | 4 | 2 | 30/30 | 102.45 | FAIL: finish 102.45s > limit | 28/30 | 0 | 149 | 45.1 |
| 6 | 20261003-105712 | 0 | 2 | 30/30 | 94.14 | FAIL: finish 94.14s > limit | 28/30 | 0 | 160 | 37.0 |
| 7 | 20261003-105712 | 1 | 2 | 30/30 | 110.43 | FAIL: finish 110.43s > limit | 28/30 | 0 | 137 | 42.7 |
| 8 | 20261003-105712 | 2 | 2 | 30/30 | 102.34 | FAIL: finish 102.34s > limit | 28/30 | 0 | 154 | 41.4 |
| 9 | 20261003-105712 | 3 | 2 | 30/30 | 98.44 | FAIL: finish 98.44s > limit | 29/30 | 0 | 143 | 37.2 |
| 10 | 20261003-105712 | 4 | 2 | 30/30 | 100.94 | FAIL: finish 100.94s > limit | 28/30 | 0 | 153 | 43.2 |

#### I4, 2 seats

| # | session | race | int | crystals | finish (s) | result | other AI at end | recov | mean u/s | frame ms |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 20261003-110710 | 0 | 4 | 54/54 | 153.30 | FAIL: finish 153.30s > limit | - | 0 | 113 | 33.5 |
| 2 | 20261003-110710 | 1 | 4 | 54/54 | 145.28 | FAIL: finish 145.28s > limit | - | 0 | 116 | 31.9 |
| 3 | 20261003-110710 | 2 | 4 | 54/54 | 151.26 | FAIL: finish 151.26s > limit | - | 0 | 120 | 33.4 |
| 4 | 20261003-110710 | 3 | 4 | 54/54 | 137.56 | FAIL: finish 137.56s > limit | - | 0 | 125 | 32.5 |
| 5 | 20261003-110710 | 4 | 4 | 54/54 | 170.47 | FAIL: finish 170.47s > limit | - | 0 | 100 | 33.9 |
| 6 | 20261003-112122 | 0 | 4 | 54/54 | 163.69 | FAIL: finish 163.69s > limit | - | 0 | 104 | 34.0 |
| 7 | 20261003-112122 | 1 | 4 | 54/54 | 139.31 | FAIL: finish 139.31s > limit | - | 0 | 124 | 33.1 |
| 8 | 20261003-112122 | 2 | 4 | 54/54 | 165.43 | FAIL: finish 165.43s > limit | - | 1 | 104 | 33.9 |
| 9 | 20261003-112122 | 3 | 4 | 54/54 | 142.56 | FAIL: finish 142.56s > limit | - | 0 | 114 | 33.2 |
| 10 | 20261003-112122 | 4 | 4 | 54/54 | 156.40 | FAIL: finish 156.40s > limit | - | 0 | 117 | 33.8 |

#### I4, 3 seats

| # | session | race | int | crystals | finish (s) | result | other AI at end | recov | mean u/s | frame ms |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 20261003-113619 | 0 | 4 | 54/54 | 146.39 | FAIL: finish 146.39s > limit | 52/54 | 0 | 123 | 49.7 |
| 2 | 20261003-113619 | 1 | 4 | 54/54 | 160.29 | FAIL: finish 160.29s > limit | 52/54 | 0 | 104 | 50.5 |
| 3 | 20261003-113619 | 2 | 4 | 54/54 | 149.30 | FAIL: finish 149.30s > limit | 49/54 | 0 | 108 | 48.5 |
| 4 | 20261003-113619 | 3 | 4 | 54/54 | 145.98 | FAIL: finish 145.98s > limit | 52/54 | 0 | 107 | 45.7 |
| 5 | 20261003-113619 | 4 | 4 | 54/54 | 155.83 | FAIL: finish 155.83s > limit | 53/54 | 0 | 124 | 50.7 |
| 6 | 20261003-115019 | 0 | 4 | 54/54 | 158.76 | FAIL: finish 158.76s > limit | 52/54 | 1 | 112 | 50.2 |
| 7 | 20261003-115019 | 1 | 4 | 54/54 | 148.70 | FAIL: finish 148.70s > limit | 51/54 | 0 | 109 | 49.8 |
| 8 | 20261003-115019 | 2 | 4 | 54/54 | 141.97 | FAIL: finish 141.97s > limit | 52/54 | 1 | 125 | 49.4 |
| 9 | 20261003-115019 | 3 | 4 | 54/54 | 144.08 | FAIL: finish 144.08s > limit | 52/54 | 1 | 132 | 50.8 |
| 10 | 20261003-115019 | 4 | 4 | 54/54 | 163.18 | FAIL: finish 163.18s > limit | 53/54 | 0 | 116 | 51.9 |

### Earlier policies (in-game, for the record)

| policy | races | <= 70 s | median | note |
|---|---|---|---|---|
| untuned | 1 | 0 | 81.9 | |
| v1 | 3 | 3 | 59.6 | small sample, frame rate not yet recorded |
| v2 (no guard) | 10 | 0 | 88.8 | ~115 ms frames (environment) |
| v2 + guard defaults | 5 | 4 | 66.9 | first laid-mass guard |
| v3 | 10 | 5 | 69.9 | batch contaminated by a concurrent tuner (400-600 ms spikes) |
| v4 (2026-10-02, 1 AI seat, before lanes) | 22 + 3 | 20/22 + 3/3 | 57.7 | sessions 20261002-191546, 20261003-014415, 20261003-021648 |
| v1-i2 (2026-10-02, 1 AI seat) | 3 | 0/3 | 111.7 | session 20261003-024656 |

## 9. Status and known limits

- **Regatta (2026-10-06, NOT editor-verified).** Regatta's opponent Squirrels fly this pilot with
  `RegattaRingObjective`: crystals swapped for rings, the waypoint ribbon for the domain's rail.
  Before this they flew the platform `AIPilot` steered at ring waypoints and threaded none. The
  policy is Skim Race's per-intensity config, untuned for a rail; the rail lanes sit 22 u off the
  ring spine and the mouths are 54–110 u, so the skim line passes well inside each mouth. A pilot
  whose hull a human swaps into stands down (no input writes) until the AI gets it back.

**Limits: I1 70 s, I2 80 s (re-baselined, §6.11), I4 70 s. Met on I1 (editor, winner); I2 80 s is met
with `skimrace-v2-i2` at normal frame rates: the last 5 consecutive hand-played races 67.4-76.6 s
(median 69.6 s, §8.0d), background benchmark median 72.6 s (§8.0b). Below ~8 fps (127 ms frames) it is
not (90-128 s, §8.0c).**

- **I2 at 80 s (§6.11, §8.0b):** `skimrace-v2-i2`. Editor: 2 AI winner <= 80 s in 7/10 (median
  72.6 s, both misses after recoveries); 3 AI 10/10 domain wins <= 80 s (6 of them two-AI team wins;
  solo AI wins 4/4, 74-78 s). Sim, 40 fresh seeds: 30/40 and 31/40, against 0/40 for v1-i2.
- **I1 regression check (§8.0b):** 5/5 within 70 s, median 58.8 s - unchanged.

- **I1:** last valid in-editor matrix (pre-fix pilot code, §8.1): 19/20 races won within 70 s across
  1 and 2 AI seats. That judges the WINNING seat - a Skim Race ends at the first finisher, so no other
  seat's time exists. The strict per-seat reading is only measurable in the simulator: every AI seat
  <= 70 s in 12-22 of 40 races with 2 AI seats and 2-4 of 40 with 3 AI seats (§6.6, §6.9).
- **I2 at the original 70 s: not met** (history; superseded by the 80 s limit above). Simulator, 2/3 AI seats: race median 112-116 s, 0/40 with every seat <= 70 s.
  The strike-free ceiling is 66-70 s, so 70 s needs essentially zero strikes; the pilot takes ~15-30
  per race and no lever or tune tried reduces that without losing more time (§6.8).
- **I4: not met, and not reachable with this approach.** Even with every hull contact switched off
  the simulator needs ~124 s for one AI seat (§6.7).
- **I3: not attempted; physically impossible** (56 crystals over ~37,000 u needs 528 u/s; the
  Squirrel tops out at 300 u/s).
- **I2 second pass (§6.10):** best real result 96.8 s race median at 2 AI seats (lane step 1 +
  tracking-MPC strike term + no terminal chord); strike-free ceilings 69.9 s (2 AI) and 85-127 s
  (3 AI). Stop condition met; no policy change shipped.
- **Owed:** the in-editor matrix for the current pilot code (§8.0) - I2 at players 3 and 4 against
  80 s, I1 at players 3 against 70 s - and an editor compile/test pass for the §6.10/§6.11 code (the
  editor was in a play session during both passes).

What blocks I2 and I4: hull strikes on the ribbon's super-shield contact shell while taking crystals
that sit within a few units of it (each resets the skim boost to 1x and cuts speed by up to half), and
on I4 the racing line itself - following the ribbon round 54 crystals, the strike-free pilot averages
well under the ~234 u/s a 70 s finish needs. Correcting the contact geometry (§6.4) removed phantom
strikes and phantom skims together and left the outcome where it was. The vessel's heading lags the
stick with a 0.67 s time constant at up to 300 u/s; that, against a 30 u-wide, 3 u-thick hazard with
crystals 0-35 u from it, is the measured constraint. Nothing here changes the Squirrel, the course, the
crystals or the timer, so none of those levers was available.

Other limits:
- Frame rate matters: below ~15 fps the policy degrades; every race records its frame time, and an
  unfocused editor or a busy machine is enough to invalidate a batch (§8.0).
- Not yet run in a standalone player build or from a fresh checkout on another machine.
- `MiniGameHUD`'s destroyed-`Player` exception (fixed here) is a game bug that a 3-seat replay hits;
  it is outside the AI and worth a separate look at why `GameDataSO.LocalPlayer` holds a destroyed
  Player after the reload.

## 10. AI difficulty (the host's lobby setting)

The host picks how well the Skim Race AI flies on the launch panel - **Easy, Medium (the default)
or Hard** - in a row under the intensity buttons (`Docs/ArcadeLaunch/ARCHITECTURE.md` §3.3 has the
lobby side: host-only, replicated to guests, remembered per card). It is independent of intensity
on purpose: intensity picks the TRACK, difficulty picks the OPPONENT, so the hardest AI can race
intensity 1.

**Hard is the shipped pilot, unchanged.** Easy and Medium are the SAME pilot with deliberate,
human-shaped mistakes (`SkimRaceHandicap`), chosen by the user from four candidates (2026-10-05):

- **Slow reaction.** For a moment after a new crystal appears the pilot has not noticed it, keeps
  flying the racing line, and turns in late. The moment varies per crystal (0.5x to 1.5x the level's
  `ReactionSeconds`).
- **Misjudged crystal.** With the level's `MistakeChance` per crystal the pilot believes the crystal
  sits further off the ribbon than it does - on the crystal's own side, so the mistake can never steer
  it into the track - flies over it, and turns back for it once it is past (or after 6 s).

Both are mistakes in DECISIONS, never jitter on the stick: the handicap edits only what the driver
believes (its observation's target), and the driver steers, guards and recovers exactly as Hard does.
Progress, and so recovery, is still judged against the real crystal. With no handicap the driver is
byte-for-byte the shipped pilot (6 simulator races identical before and after the change). Mistakes are
random every race (`System.Random` seeded per bind, never `UnityEngine.Random`, whose global state the
track generator seeds). ONE setting per difficulty serves every intensity (`SkimRaceDifficultySO`,
`Resources/SkimRaceDifficulty.asset`, authored by `Tools/Build/author_skimrace_ai_config.py`'s
DIFFICULTY table), so a new track gets the same mistakes and its times scale with its length.

**Tuning.** Each mistake measured alone first (I2 policy, 2 AI seats, 6 races, races to 300 s; Hard
seat median 77 s): reaction 0.5 s -> 101 s, 1.0 s -> 123 s; misjudge chance 0.2 -> 144 s, 0.5 -> 215 s
(about 10-12 s lost per misjudged crystal, every race still finished). The reaction time is fixed per
level at a human-plausible value and `run.sh handicap` bisects the misjudge chance to the target seat
median on intensity 2 (40 seeds x 2 seats, common seeds every step, then 40 fresh seeds):

| Difficulty | Reaction | Misjudge chance | Seat median (fresh seeds) | p10 - p90 | Misjudged / seat / race |
|---|---|---|---|---|---|
| Hard | - | - | 80.2 s (winner median 74.4 s; the shipped I2 policy, fresh seeds 50000+) | 70.9 - 95.7 s | 0 |
| **Medium** (target 95 s) | 0.25 s | **0.045** | **95.9 s** (80/80 finished) | 81.0 - 124.4 s | 1.3 |
| **Easy** (target 120 s) | 0.5 s | **0.099** | **120.8 s** (160/160 finished; 2 fresh sets pooled) | 96.1 - 157.9 s | 2.9 |

Medium's search (common seeds): 0 -> 83.3 s, 0.031 -> 90.9, 0.039 -> 93.9, 0.043 -> 94.3, 0.047 -> 97.2,
0.063 -> 102.8, 0.125 -> 120.6, 0.25 -> 153.7, 0.5 -> 213.7, 1.0 -> 292.3 s. Easy's reaction alone gives
90.5 s on the same 40 seeds (the 6-race sample above read 101 s - small samples of this race are noisy).
Easy's search (`hi=0.25`, common seeds): 0.063 -> 112.0 s, 0.094 -> 116.7, 0.098 -> 118.6, 0.100 -> 120.7,
0.102 -> 121.7, 0.109 -> 126.1, 0.125 -> 130.2, 0.25 -> 162.1 s. Easy's spread is wide (a misjudged crystal
costs ~10 s and the count per race varies), so one 40-seed set moves its median by several seconds: 0.099
read 114.9 s on its first fresh set, then 123.6 and 120.0 on two more (pooled 120.8 s), against 125.3 s
pooled for 0.107 on the same two sets - so 0.099 ships.

**On every track** (`eval <I> 20 limit=300 seedbase=50000`, 2 AI seats, 28 ms frames +-50%, each
intensity's own policy - I3 flies `skimrace-v2-general` - and the SAME difficulty numbers everywhere):

| Track | Difficulty | Seat median | p10 - p90 | Seats finished | Winner median | Misjudged / seat / race |
|---|---|---|---|---|---|---|
| I1 | Hard | 68.2 s | 61.0 - 88.0 s | 40/40 | 64.3 s | 0.00 |
| I1 | Medium | 80.1 s | 71.2 - 109.6 s | 40/40 | 75.3 s | 1.25 |
| I1 | Easy | 92.8 s | 80.8 - 123.3 s | 40/40 | 86.2 s | 2.45 |
| I2 | Hard | 80.2 s | 70.9 - 95.7 s | 40/40 | 74.4 s | 0.00 |
| I2 | Medium | 95.8 s | 77.2 - 122.0 s | 40/40 | 92.0 s | 1.45 |
| I2 | Easy | 123.7 s | 102.9 - 176.4 s | 40/40 | 112.0 s | 3.08 |
| I3 | Hard | 188.2 s | 171.2 - 205.9 s | 40/40 | 179.7 s | 0.00 |
| I3 | Medium | 215.0 s | 193.1 - 239.5 s | 40/40 | 203.5 s | 2.62 |
| I3 | Easy | 231.6 s | 216.4 - 258.9 s | 40/40 | 226.6 s | 5.70 |
| I4 | Hard | 150.5 s | 135.4 - 174.5 s | 40/40 | 145.9 s | 0.00 |
| I4 | Medium | 172.4 s | 146.5 - 188.4 s | 40/40 | 160.7 s | 2.60 |
| I4 | Easy | 187.5 s | 170.3 - 218.6 s | 40/40 | 180.1 s | 5.60 |

Every one of the 480 seats finished, and the three levels are distinct and in order on every track.
Measured against Hard on the same track, Medium is 14-19% slower everywhere; Easy is 36-54% slower on
the short tracks and 23-25% on the long ones. The mistakes cost time per CRYSTAL, while the long tracks'
Hard times are already long, so the gap narrows as a share - a fixed-setting design trades exact targets
on every track for one number a new track inherits untouched (the user's choice, 2026-10-05). Intensity 2
lands on its targets: Medium 95.8 s (95), Easy 123.7 s (120).

The editor benchmark (`FrogletTools > AI > Skim Race AI Benchmark`) now has an AI difficulty setting
(default Hard, which is what every earlier benchmark measured), and each race record names the
difficulty and the number of misjudged crystals per AI seat.

## 11. When a map changes: fingerprints and retuning

A tuned policy is only as good as the map it was tuned on: its numbers were found for those corners and
those crystals. So every per-intensity file records WHICH map that was, and the game, a check script and
one retune command all use that record (the user's choices, 2026-10-05).

**What counts as "the map changed"** - only what changes the race: an intensity's path points
(`SpawnableWaypointTrack.waypoints`), its curve setting (`useSplinePerIntensity`), its laps
(`CrystalCollisionTurnMonitor.ResolveLaps`) and where its crystals sit (`CrystalManager`'s anchor set for
that intensity, clamped into the list exactly as the spawner does). Colours, prism looks, prism spacing and
the crystal spawn jitter do not count. Positions count in whole units, so a nudge under half a unit is the
same map. `SkimRaceTrackFingerprint` hashes those four facts into 8 hex digits (32-bit FNV-1a over
`[version, #points, x, y, z ..., spline, laps, #crystals, x, y, z ...]`, rounded half to even). The same
value comes from the game's C# (`SkimRaceCourseSource.TryFingerprintFromScene`, read from the loaded scene)
and from `Tools/Build/skimrace_track_fingerprint.py` (read straight from `MinigameSkimRace.unity`): both
assert one golden value (`SkimRaceTrackFingerprintTests`, `--self-test`), and the simulator's
`run.sh fingerprint` prints the C# value for the shipped tracks - they match on all four (I1 `ed6cd993`,
I2 `19fadf77`, I3 `183f4bf3`, I4 `227b9055`). The script is also the simulator's one track reader
(`--emit-track`), byte-identical to the extraction it replaced, so the simulator races every intensity
the scene has - a fifth set of waypoints included.

**Where it is recorded.** `SkimRaceAIConfigSO.TrackFingerprint`. Each `SkimRaceAIConfig_I<n>` must carry
one and the general `SkimRaceAIConfig` must not - it is for every map (`author_skimrace_ai_config.py
--check` holds both rules). The I1, I2 and I4 files record today's maps: they were tuned 2026-10-02..04, and
the race data in the scene is identical at every revision back to 2026-09-12.

**What the game does** (`SkimRaceAIDeployment.PolicyFor`): an intensity with no file of its own - I3, or
any new intensity, since the old 1..4 clamp is gone - flies the general policy. An intensity whose file
matches the live map flies its file. One whose file was tuned on a different map flies the general policy
instead, and the console says so once per race (not once per AI seat):

```
[SkimRaceAI] Intensity 2: the AI tuning file SkimRaceAIConfig_I2 (skimrace-v2-i2) was tuned on a different
map (fingerprint 19fadf77; this scene is b8aa5b7c), so the AI flies the general settings
(skimrace-v2-general) instead. To retune it for this map: python3 Tools/Build/skimrace_retune.py 2
```

When the scene cannot be read (no track, monitor or crystal manager), nothing says the map changed and the
file is trusted. Why the general policy rather than the stale file: the general policy was tuned to finish
EVERY track (§6.12), while a stale specialist's numbers belong to corners that are no longer there.

**Before anyone presses Play:** `python3 Tools/Build/skimrace_track_fingerprint.py --check` prints one line
per intensity - `OK`, `no tuned file - flies the general policy (fine)`, or `RETUNE NEEDED` with the command
- and exits 1 when a tuned file's map changed (or a tuned file records none). Proven on edited copies of the
scene (`--scene`): a waypoint moved 5 units, I4's laps 2 -> 3, I1's curve switched on and a crystal moved
10 units each flag only their own intensity; a 0.3-unit nudge, a new track domain, prism spacing and spawn
jitter flag nothing; a fifth waypoint set appears as `I5: no tuned file`.

**One command to retune:** `python3 Tools/Build/skimrace_retune.py <intensity>` (about half an hour to an
hour - a full run on intensity 3, one of the long tracks, took 43 minutes on a 4-core machine; `--dry-run`
writes nothing; `--iters/--seeds/--final` trade time for quality). It:

1. reads the intensity's fingerprint and refuses to go on unless the game's C# reads the same value;
2. starts from the intensity's own file (or the general policy for a new intensity) and tunes it on that
   track with the general policy's search (`tuneall <n> 4 16 sigma=0.15`, 2 AI seats, 28 ms frames
   +-50%), restricted to the numbers the policy already uses (`only=stated`, plus `set=winner` for a
   tracking-MPC policy): a retune re-fits numbers and never switches a control on or off;
3. races the result and the general policy on the same fresh races (seedbase 99000) and keeps it only if
   it finishes at least as many and, on a tie, has the faster median winner - otherwise it writes nothing
   and exits 2 (the game keeps flying the general policy there, which it already does);
4. writes the `SkimRaceAIConfig_I<n>` block - new numbers, bumped `PolicyVersion`, new `TrackFingerprint`
   and a comment with the head-to-head numbers - regenerates the assets and runs both `--check`s. Nothing
   is committed; review the diff and commit it.

If the general policy keeps winning on a changed map, the honest result is that the track does not need a
specialist: delete its `SkimRaceAIConfig_I<n>` block and asset, and the `SkimRaceAITests` that name it.

Proven here (2026-10-05): the C#/Python agreement on all shipped tracks and on an edited one (`b8aa5b7c`
both sides); the deployment's choice and its warn-once rule run outside Unity against the real scene data
and the shipped assets (three seats warn once, the next scene load warns again, a sub-unit nudge does not;
negative controls - no de-duplication, no anchor clamp - fail as they should); the retune end to end on a
one-step search (`--dry-run`), and its file writer replacing I2's block and inserting new I3 and I5 blocks
in order without touching the others. Not proven here: the warning in the editor
(`Docs/UNITY_VERIFICATION_CHECKLIST.md`).

**A full run, end to end** (2026-10-05, `skimrace_retune.py 3 --dry-run`, 43 min): intensity 3 has no file
of its own, so it started from the general policy and searched its 36 stated numbers over 16 steps (tuner
score 4.377 -> 4.070). On the same 20 fresh races the result finished 20/20 with a winner median of
**173.9 s**, against the general policy's 20/20 and **179.4 s** - so a real run would have written it. It
was a dry run: intensity 3 still flies the general policy, and whether it should get a specialist of its
own (about 3% faster in the simulator) is a separate decision.

## 12. Per-frame cost (what the AI costs the game each frame)

The pilot thinks every frame (`DecisionHz` 0). Its cost is visible in the Unity Profiler under these
markers (Window > Analysis > Profiler, CPU Usage, Hierarchy view, search `SkimRace`):

| Marker | What it times |
|---|---|
| `SkimRace.Pilot.Update` | everything one AI pilot does in a frame - one call per AI |
| `SkimRace.Pilot.Sense` | reading the vessel and crystals |
| `SkimRace.Pilot.FillObstacles` | gathering nearby prisms for the laid-mass guard (a default `diag` marker) |
| `SkimRace.Pilot.Decide` | the thinking (a default `diag` marker, `MarkerBudget.DefaultMarkers`) |
| `SkimRace.Driver.TrackMpc` | intensity 2's look-ahead planner: ~26 short what-if flights, only on the frames it re-plans (20 a second) |
| `SkimRace.Driver.GuardMass` | the laid-mass guard's what-if flights |
| `SkimRace.Driver.PlanPass` / `.Guards` / `.Planner` / `.LevelApproach` / `.Mpc` | smaller parts; a part a policy switches off never appears |

The `SkimRace.Pilot.*` markers came from two sessions on the same day (`claude/bold-fermi-54nlts`'s
`Decide` / `FillObstacles`, this branch's `Update` / `Sense`) and were merged into one set, so each piece of
work is timed once. The simulator compiles the driver, not the pilot: it tallies the `SkimRace.Driver.*`
markers by name (`UnityShim`'s `ProfilerMarker` stand-in) and times the whole decision itself, and `eval`
prints them with the bytes allocated per decision and the AI's thinking per frame for all seats together.
Markers wrap whole steps at most once per decision, so they change nothing: every race in the simulator is
byte-identical with and without them.

**Measured in the simulator** (2026-10-05; a 4-core Intel Xeon 2.8 GHz cloud machine, .NET 8.0.31,
nothing else running; each track's shipped policy - I3 flies the general one - with 2 AI seats, 28 ms
frames +-50%, 20 races per track, seedbase 50000, `eval <I> 20 ... limit=300`), on the merged code:
`claude/bold-fermi-54nlts`'s exact speed-ups (§8.0e) with this branch's work:

| Track (policy) | One AI, average per frame | Both AIs in one frame: typical / worst 10% / worst 1% | Intensity 2's planner | Biggest part (per AI per frame) | Memory allocated |
|---|---|---|---|---|---|
| I1 (`skimrace-v4-i1`) | 0.04 ms | 0.02 / 0.26 / 0.57 ms | - | laid-mass guard, 0.04 ms | ~23 bytes per decision* |
| I2 (`skimrace-v2-i2`) | **0.43 ms** | 0.34 / **1.93** / **2.95** ms | **0.81 ms** per AI per re-plan, on 43% of frames | planner, 0.35 ms | ~22 bytes* |
| I3 (`skimrace-v2-general`) | 0.15 ms | 0.16 / 0.87 / 1.60 ms | - | laid-mass guard, 0.10 ms | ~18 bytes* |
| I4 (`skimrace-v1-i4`) | 0.12 ms | 0.08 / 0.65 / 1.91 ms | - | laid-mass guard, 0.10 ms | ~11 bytes* |
| I2 on Easy | 0.39 ms | 0.30 / 1.78 / 2.88 ms | 0.78 ms | planner, 0.33 ms | ~20 bytes* |

\* The laid-mass guard's grid (§8.0e) grows its arrays to the most nearby prisms a pilot has met - about
64 KB per AI per race, all while its trail builds up; averaged over every decision that reads as ~20
bytes. Once the arrays are big enough nothing more is allocated (before the grid: 0 bytes).

Every one of those 100 races is byte-identical to the ORIGINAL code's, before any speed-up, whose numbers
were: I1 0.083 ms, I2 0.82 ms (1.50 ms per re-plan, worst 1% 5.61 ms), I3 0.37 ms (worst 1% 4.62 ms), I4
0.23 ms (3.83 ms). This branch's own track-lookup change (`f0d56df55`, 11-20% on its own) is part of §8.0e's,
which was kept on merge.

A frame at 60 fps is 16.7 ms. Easy and Medium cost no more than Hard (an Easy pilot has a little less to
think about while a crystal is still unnoticed). Past the guard grid's growth (the footnote) the thinking
allocates nothing, so it does not feed the garbage collector during a race. At 60 fps the planner lands on fewer frames (20 a second is 1 frame in 3), so the
average falls; the spike does not. The single slowest frame of each run (12-29 ms) is left out of the
table: it cannot be told apart from the .NET runtime's one-off start-up work, which the game does
differently. The 0.37 ms in section 8 (commit `02da300fe`, 2026-10-04) was measured on a different machine
under settings that record does not give; compare the rows of this table with each other, not with it.

**Why intensity 2 is different.** Its policy flies the tracking MPC (`UseTrackMpc`): 20 times a second it
flies ~26 short what-if flights (one per candidate stick, 1.1 s each) and keeps the best. That is the
spike: it lands on about 2 of every 5 frames at 36 fps (1 in 3 at 60 fps), and BOTH AIs re-plan on the
same frames, because both count from the same race start (in a 3-race count: 3,406 frames had two
re-plans, 354 had one, 4,972 none). Inside a what-if flight the time goes to checking the hull against
the ribbon's contact shell (~44%), finding the nearest point on the track (~24%), sampling the racing line
(~12%) and the flight model and steering (~20%) - measured with temporary finer timers in a scratch build,
before §8.0e's speed-ups cut the first two.

**What the simulator cannot say.** Unity runs this C# on Mono in the editor and IL2CPP in a build, not on
.NET 8, so the game's numbers will differ - not measured here, but the editor is likely slower (much slower
with the editor's Code Optimization set to Debug) and an IL2CPP build likely closer. The pilot's own sensing
(`SkimRace.Pilot.Sense`, `.FillObstacles`) only runs in the game. The Profiler reading in
`Docs/UNITY_VERIFICATION_CHECKLIST.md` is the real number.

**Speed-ups** (the user's rules: no fixed budget; only speed-ups that leave every race identical, anything
that changes flying is the user's decision):

- *Identical races - applied (the user's call), then superseded on merge:* this branch's `f0d56df55`
  stepped the track lookups' windows with a wrap-around instead of an integer remainder per segment;
  `claude/bold-fermi-54nlts` made the same change and more the same day (§8.0e: precomputed segments, a
  nearest-first shell search, a grid for the laid-mass guard), and its version was kept. The merged track
  code agrees with the original bit for bit on 2.4 million random queries (tiny courses, wrapped windows,
  out-of-range hints, exact ties; a window shifted by one is caught), and its races are identical.
- *Identical races, tried and dropped:* skipping the far half of the star-shaped shell with a safe bound
  saved nothing measurable (the bound costs about what it saves); §8.0e's box bound before the exact test
  is the version that pays.
- *Would change how the AI flies (needs a decision):* stagger the AIs' re-plans so they do not share a
  frame (halves the spike with two AI); re-plan less often (`TrackMpcHz`); fewer candidate sticks. The user
chose to decide on these after reading the real numbers in the Unity Profiler. **Stagger: chosen and applied
2026-10-07 (§8.0i)** - no measurable change to racing, p90/p99 AI frame cost down ~38%.

## 13. Team races (teammates share their crystals)

**Why this section exists.** On 2026-10-05 the user raced Skim Race in co-op: two humans against two AI. The
humans won almost every race "even though we were doing a lot of mistakes". The pilot itself is not the main
reason. The team rules are.

**What the game does with teams** (read from the code, not assumed):

- The finish line is per TEAM. `SkimRaceScoringRuleSO.IsObjectiveReached` ends the race when one domain's
  SUMMED crystals reach the target, and the target does not grow with the team.
- Every player brings one crystal of their own domain. `NetworkCrystalManager` sizes its slots to the
  roster, and slot i takes `Players[i].Domain`. Each crystal walks the anchors on its own
  (`CrystalManager.CalculateNewSpawnPos` keeps a per-crystal anchor index). So a team of two has two live
  crystals and needs the same total as a pilot racing alone: about half each.
- AI seats fill the domain with the fewest pilots (`ServerPlayerVesselInitializerWithAI.GetBalancedDomain`).
  With the default three domains, two humans on Jade plus two AI puts the AI on Ruby and Gold, ONE EACH.
  Each AI must collect the whole target alone while the human pair shares it, so an AI wins only if it
  is about twice as fast as each human. Today's way to seat both AI on one team is in the launch panel:
  remove the placed AI (✕ on their chips), then arm **Add AI** and tap the same team's tile twice.
  Changing the team count alone does not move AI that are already placed: a placement is fixed once
  made (`ArcadeGameConfigureModal.ReconcileAiPlacements`).

**What the AI did on a team before team play.** The pilot flew at the nearest crystal of its domain, with
hysteresis (`SkimRaceTargetTracker`). Two AI on one team therefore started on the SAME crystal. The one that got
there second was left aiming at a crystal that had just jumped to the next anchor, and swung round for
the other one. One AI ended up doing most of the work. The pickups in 10 I1 races were 16/8, 5/19, 19/5,
15/9, 6/18, 12/12, 16/8, 8/16, 17/7 and 17/7. Meanwhile the two flew through each other's trails.

**Measured** (the simulator's team race, `ph.Team=1`; Hard, each track's shipped policy, with I3 on the
general one; 2 AI, 28 ms frames ±50%, 20 races per row, `limit=240` on I3 and I4). Times are the median
finish in seconds. "Hull hits" counts both AI together, per race.

| Track | 1 AI alone | 2 AI, separate teams (each AI's own finish) | 2-AI team, nearest rule (before) | 2-AI team, team plan (shipped) | Plan vs before |
|---|---|---|---|---|---|
| I1 | 61.9 | 69.2 | 65.0 (hull hits 34.8) | **36.5** (15.3) | −44% |
| I2 | 75.5 | 77.8 | 80.3 (11.3) | **39.6** (4.1) | −51% |
| I3 | 180.4 | 183.2 | 149.7 (40.4) | **97.2** (18.9) | −35% |
| I4 | 148.5 | 156.2 | 115.2 (45.8) | **83.1** (24.2) | −28% |

- The nearest rule made a 2-AI team SLOWER than one AI alone on I1 and I2. On I1 its cross-track error at
  the 90th percentile is 155 u against 44 u for the team plan: the AI chased crystals that had moved.
- The **team plan** gives each AI a different crystal. It picks the assignment of AI to crystals with the
  least total straight-line distance, and keeps it until another assignment is 15% cheaper. A team that
  plans finishes in 52-59% of a lone AI's time: the two really do share the work. Each AI's hull hits
  drop back to a lone AI's level.
- Tried and dropped: a heading-aware cost (a crystal behind the hull costs up to twice its distance). It
  changed nothing: I1, I3 and I4 were identical, and I2 was 0.1 s slower.
- "Separate teams" is the old two-seat model (each seat its own domain, every race in §8 to §12). The
  other AI's trails and pickup rings cost each AI 3-8% against flying alone.

**Shipped: team play** (the user's call, 2026-10-05: every difficulty). How it works in the game:

- `SkimRacePilot` joins `SkimRaceTeamPlan` when its race starts and leaves when it ends. Each frame the
  first AI of a team to sense builds that team's plan (`SkimRaceTeamAssignment`: positions in, one
  crystal per AI out). Every other AI on the team reads the same plan, so two AI cannot pick one
  crystal from two slightly different snapshots. The plan remembers its last answer per team: that is
  the 15% hysteresis (`SkimRaceTargetTracker.Hysteresis`).
- **AI only.** A human teammate is never planned for. An AI does not leave a crystal "for" a human,
  because an idle or slow human would strand it and the team would lose it. The AI just stop doubling up
  on each other.
- **A lone AI is unchanged.** A team with one AI gets no plan and flies the nearest-crystal rule exactly
  as before, so every solo race in §8 to §12 is unaffected. An AI the plan has no crystal for (more AI
  than crystals, which one-crystal-per-player rules out) falls back to the same rule.
- It reads what any pilot can see (where the team's vessels and crystals are) and writes nothing.
  `check_ai_no_state_writes.py` covers it.
- Easy and Medium keep their deliberate mistakes on top. A plan switch is a new crystal, so their
  reaction delay applies to it as to any other.

The simulator's `ph.TeamRule=1` (now the default with `ph.Team=1`) calls the same
`SkimRaceTeamAssignment`. With it, every race in the table above came out identical to the experiment
that preceded it. `SkimRaceTeamAssignmentTests` covers the plan: two AI never share a crystal, the
cheapest plan beats "nearest pair first", a near-tie keeps the last plan, a plan that cannot be kept is
replaced, and the edge cases (more AI than crystals, a team past the exact search, no AI or no crystal).
Three deliberate breaks of the code (greedy only, never keep, allow a crystal twice) are each caught by
the test written for them.

**Team size: today's seating rule is kept** (the user's call, 2026-10-06, after seeing the table below).
Backfilled AI still go to the team with the fewest pilots (`GetBalancedDomain`). To race 2 humans against a
2-AI team, the host sets it up by hand: remove the placed AI (✕), arm **Add AI**, and tap one tile twice.
How today's rule seats every shape a 4-seat Skim Race allows, all humans starting on Jade unless they pick a tile:

| Who is playing | Today's seating | Note |
|---|---|---|
| 1 human + 1 or 2 AI | everyone alone | fair |
| 1 human + 3 AI | human · **2 AI on Ruby** · 1 AI | the Ruby pair now plays as a team (about twice one AI's pace) |
| 2 humans on one team + 1 AI | the pair vs 1 AI | the lone AI must take every crystal itself |
| 2 humans on one team + 2 AI | the pair · 1 AI · 1 AI | the user's co-op race; for 2 vs 2 use Add AI |
| 2 humans on different teams + 2 AI | human · human · **2 AI on Gold** | the Gold pair plays as a team |
| 3 humans (2 + 1) + 1 AI | the pair · the lone human · 1 AI | |
| 3 humans on one team + 1 AI | the trio vs 1 AI | |
| 3 humans on 3 teams + 1 AI | the AI joins Jade's human | |

The two rules weighed and not taken: "fill the AI into a team the size of the biggest human team" (would
change only the 2 + 2 and the 2+1 + 1 rows), and "equal teams" (would also give a solo human facing 3 AI, and
each of 2 rivals facing 2 AI, an AI teammate). "2 vs 1" and "3 vs 1" cannot be equal with 4 seats under any
seating rule. Only a crystal target that grows with team size would even them, and the user declined
that change on 2026-10-05.

**Run it:**

```bash
bash -c 'bash Tools/Build/skimrace_sim_harness/run.sh eval 1 20 $(python3 Tools/Build/skimrace_sim_harness/policy_args.py SkimRaceAIConfig_I1) ph.Dt=0.028 ph.DtJitter=0.5 ph.Seats=2 ph.Team=1'
```

Add `ph.TeamRule=0` for the rule from before team play (it calls `SkimRaceTargetTracker.SelectIndex`). I3 and I4
need `limit=240`: the default 70 s limit cuts every race at 130 s. With `ph.Team=0` (the default) the
simulator is unchanged: 6 races each on I1 and I2 matched the pre-change build line for line.

**What the model leaves out.** The simulator's seats spawn 10 u apart, and the game's spawn points are
further apart. A respawned crystal's "move away from where it last was" rule is not modelled (neither is
it for a lone AI). Human teammates are not modelled at all. A hand-played editor race records itself
(`BenchmarkResults/SkimRaceAI/manual_I<n>_*.jsonl`, §1), and that record is how a human pair's time
gets compared.

## 14. Frame rate: the AI is tuned for one frame rate (2026-10-06)

**Why this section exists.** The user reported the AI racing poorly at Easy, Medium and Hard alike while
testing. Nothing in that day's bleeding-edge merge changes how the Squirrel flies on desktop (§14.3), so
the AI was measured at the frame rates people actually play at.

### 14.1 Two things in the GAME depend on frame rate

- **Contacts run on the fixed step.** `ProjectSettings/TimeManager.asset` sets Fixed Timestep 0.04 s and
  physics simulates in FixedUpdate, so skim, hull, laid-mass and crystal triggers are tested 25 times a
  second, at the positions the last frame left. Above 25 fps some frames test nothing; below it every frame
  tests once. The simulator now models this as `ph.PhysicsStep=0.04`. The default, 0, tests every frame:
  the model the shipped policies were tuned under, and still line-for-line identical (I1 and I2, 6 races).
- **The trail is laid at most once per frame.** `VesselPrismController.SpawnLoopAsync` lays a pair, then
  awaits `wavelength / speed`. The await resumes on a frame, so a slow frame leaves one pair per frame and
  the trail is SPARSER at low frame rates and evenly dense at high ones. The simulator lays its rails the
  same way (all the pairs a frame owes, at the frame's position).

The policies were tuned at 28 ms ±50% frames (about 36 fps). That means a sparser trail than a desktop at
60-144 fps lays, and longer steps between contact tests.

### 14.2 Measured: slower at every frame rate but the one it was tuned at

Simulator with the game's contact step (`ph.PhysicsStep=0.04`), each track's shipped policy, 2 AI, frames
±50%, 12 races per cell. The figure is each AI's median finish in seconds, then (races that finished
within the cut / 12) and hull hits per AI per race.

| I1 | 145 fps | 62 fps | 36 fps | 20 fps | 12 fps |
|---|---|---|---|---|---|
| Hard | 77.0 (12) h30 | 70.7 (12) h29 | **67.9** (12) h25 | 66.6 (12) h23 | 77.0 (12) h25 |
| Medium | 84.0 (9) h32 | 84.8 (11) h32 | 83.0 (12) h32 | 81.5 (11) h32 | 85.0 (12) h28 |
| Easy | 104.3 (9) h46 | 102.2 (11) h44 | 100.7 (11) h35 | 93.7 (11) h36 | 102.5 (9) h34 |

| I2 | 145 fps | 62 fps | 36 fps | 20 fps | 12 fps |
|---|---|---|---|---|---|
| Hard | 84.6 (12) h15 | 82.6 (12) h12 | **75.8** (12) h8 | 78.6 (12) h7 | 91.4 (11) h15 |
| Medium | 111.5 (9) h18 | 101.3 (11) h17 | 100.2 (10) h13 | 100.2 (12) h12 | 102.6 (10) h18 |
| Easy | 126.7 (4) h23 | 120.8 (7) h20 | 130.3 (5) h17 | 119.5 (8) h14 | 119.6 (9) h19 |

- Hard is 4-13% slower at 60-145 fps than at the 36 fps it was tuned at, and 13-21% slower at 12 fps. The
  hull hits that grow at high frame rates are mostly the AI's OWN rails (I1: 4.8 per race at 145 fps vs 2.7
  at 36), the pickup ring (6.1 vs 4.8) and other seats' rails: the denser trail of §14.1.
- At low frame rates the hull moves in large steps (38 u per frame at 127 ms and 300 u/s), so pass points
  are overshot. §8.0c measured the same thing in the editor at 8 fps.
- Easy at I2 often runs past the simulator's cut (limit + 60 s). That makes it slow, not stuck; the game
  has no cut.
- The every-frame contact model (`PhysicsStep=0`) gives the same picture, with more hull hits at high frame
  rates because it tests contacts more often than the game does.
- I2's pickup hold (`PickupClearDistance` 1.569 u, under one frame of travel at any speed) is effectively
  "hold for exactly one frame", which is a frame-rate-dependent rule. Lengthening it to 8.4 or 14 u was
  not a consistent gain across 145/62/36 fps at 12 races per cell, so it is not the main lever.

### 14.3 What the 2026-10-06 bleeding-edge merge changed for the AI

Nothing in how the Squirrel races on desktop:

- The "skim-tick rate limit" is the skim SOUND (`ProximityBoostAudioController.minTickInterval`).
- `DecayBoost` now raises its event only on change; the boost value itself is the same.
- The race trail cap (`RaceTrailCap`) attaches only on the MobileLow tier.
- The new Squirrel AI boost policy lists Skim Race (33) in `disabledInModes`, and `AIPilot` is off under
  `SkimRacePilot` anyway.

One change touches testing: commit `c13425ba5` ("drift changes") also changed `ProjectSettings/QualitySettings.asset`.
The editor's current level went from 2 (Medium) to 4 (Very High, which carries vSync on and 4x MSAA), and the
per-platform default levels were cleared. §8.0c's 8 fps hand-played editor races were observed at Very High
with vSync. `GraphicsSettingsApplier` applies the player's saved preset at runtime, so builds are governed by
the settings menu; the editor's starting point is not.

### 14.4 What the AI costs the editor (2026-10-06)

The user's report was that the GAME runs slow while testing Skim Race with AI. The simulator's .NET build
is optimized; the Unity editor runs the same C# on Mono with its Code Optimization usually at **Debug**,
which is much slower. As a stand-in, the simulator was compiled WITHOUT optimization (`csc -optimize-`) and
each track raced with 2 AI, 28 ms frames ±50%, the game's contact step, 10 races (seedbase 50000). Per AI
per frame, and both AIs together in one frame:

| Track | Optimized: per AI | Both AIs, typical / worst 10% / worst 1% | Unoptimized: per AI | Both AIs, typical / worst 10% / worst 1% | Biggest part (unoptimized) |
|---|---|---|---|---|---|
| I1 | 0.05 ms | 0.02 / 0.26 / 0.72 ms | 0.11 ms | 0.06 / 0.72 / 1.32 ms | laid-mass guard 0.10 ms |
| I2 | 0.41 ms | 0.34 / 1.88 / 2.74 ms | **2.30 ms** | 1.32 / **10.74** / **14.79** ms | planner 1.92 ms per decision (0.78 per AI per re-plan optimized, ~4.5 unoptimized) |
| I3 | 0.15 ms | 0.16 / 0.85 / 1.65 ms | 1.04 ms | 1.23 / 5.46 / 9.27 ms | laid-mass guard 0.56 ms |
| I4 | 0.09 ms | 0.06 / 0.48 / 1.40 ms | 0.56 ms | 0.40 / 2.72 / 8.74 ms | laid-mass guard 0.45 ms |

So at Debug optimization, two AI on I2 cost the editor **about 11 ms in one frame of every ten** (both
re-plan on the same frames, 20 times a second), and I3's mass guard about 5 ms. A 60 fps frame is 16.7 ms.
In a Release/IL2CPP build the same work is 2-3 ms at worst. The Unity Profiler reading (the checklist's
Profiler-timers entry) is the real number; the stand-in says where it will land.

**The planner stagger - two branches, one mechanism kept.** Both AI re-planned on the same frames because both
started at race time 0 and each scheduled its next re-plan as "now + 1/TrackMpcHz". This branch first fixed it
with a FIXED grid of 1/TrackMpcHz (50 ms), odd lanes offset by half a period, so two AI re-plan on different
frames whenever a frame is shorter than half a period. `perf/performance-optimization` fixed the same thing the
same day with `SkimRaceReplanGate` (§8.0i): one re-plan claims a frame, a seat that finds its frame taken flies
its previous plan ONE more frame. At the merge (2026-10-07) the gate was kept and the grid retired, because the
gate also holds at 25 fps and below, where every seat wants to re-plan every frame and a grid separates nothing.
The grid's measurements stay below as the independent confirmation of the problem. (A first version offset only
the start and kept "now + period": the first frame that happened to carry both re-plans locked the two in
step for the rest of the race, and nothing changed - a probe of the private schedule found it.) Measured, I2,
shipped policy, 2 AI, unoptimized build, both AIs' thinking per frame:

| Frames | Before: typical / worst 10% / worst 1% | Grid: typical / worst 10% / worst 1% |
|---|---|---|
| 16 ms (62 fps) | 0.7 / **10.1** / 14.0 ms | 4.2 / **6.1** / 8.5 ms |
| 28 ms ± 50% (36 fps) | 1.3 / 10.7 / 14.8 ms | 5.4 / 10.2 / 24.5 ms |

At 60 fps the worst frames carry one planner instead of two. At 36 fps with ±50% jitter many frames are
longer than 25 ms and span both grid points, so the worst 10% is unchanged (the worst 1% is GC noise in the
unoptimized build; the maxima were 40-50 ms in every variant). At 25 fps and below each AI re-plans every
frame whatever the phase: only a cheaper planner (the candidate grid is 5x5 sticks + nominal = 26 rollouts of
22 steps) or a one-planner-per-frame budget would help there, and neither was done. The total CPU is the
same; it is spread over more frames (the typical frame rose), which is the point for frame pacing. Race
times under the SHIPPED tuning, 24 races: at 16 ms frames the seat median went 82.6 → 79.5 s (one race
past the cut), at 28 ms 75.8 → 77.6 s - the grid also changes even lanes' schedule from a drifting ~56 ms
to an exact 50 ms, and the shipped numbers were fitted to the old one. The retune (§14.5) is done with the
grid in place.

**The zero-code lever for the editor:** the bug icon at the bottom right of the editor - Code Optimization
**Release** instead of Debug. The table in this section is the Debug-to-Release ratio: about 5x on the
planner. A player build is IL2CPP and does not have the choice.

**The combined code, measured the editor's way** (2026-10-07, after the merge with `perf/performance-optimization`:
its float inner loops, no-alloc Mathf and gate, this branch's retuned policies; `SKIMRACE_RUNTIME=mono`, Mono
6.8 in double precision - the mode that predicts the editor, §8.0g; 2 AI, 28 ms frames ±50%, the 0.04 s contact
step, 10 races, seedbase 50000):

| Track | Per AI per frame | Both AIs in one frame: typical / worst 10% / worst 1% |
|---|---|---|
| I1 | 0.16 ms | 0.09 / 1.05 / 1.99 ms |
| I2 | 2.24 ms | 5.0 / **6.7** / 9.1 ms (23,242 re-plans in 23,242 frames: no frame carried two) |
| I3 | 1.13 ms | 1.3 / 5.7 / 10.4 ms |
| I4 | 0.67 ms | 0.5 / 4.6 / 10.0 ms |

Against the unoptimized-.NET stand-in above (I2 both AIs 10.7 ms in the worst 10%), the editor-mode worst 10%
on I2 is 6.7 ms: the gate's half, with the float loops' ~1.4x on top. The I3/I4 tails are the laid-mass guard in
dense traffic, untouched by either branch.

### 14.5 Retuning across frame rates

The user's call (2026-10-06): retune each policy against SEVERAL frame rates at once. The simulator's
`dts=0.016,0.028,0.05` spreads a tune's or an eval's races over those frame times, round-robin by seed, so a
policy is scored at 62, 36 and 20 fps at once at no extra cost; `skimrace_retune.py` now tunes and judges
under `ph.PhysicsStep=0.04 dts=0.016,0.028,0.05` (its `PHYSICS`), with the planner grid of §14.4 in place.
The frame rates to weight are the ones players see; the recorder's `frameMs` per race
(`BenchmarkResults/SkimRaceAI/manual_*.jsonl`) and the Game view's Stats overlay give them.

Each retune started from the shipped policy (`only=stated`: the same numbers re-fitted, no control switched
on or off), 16 search steps of 24 candidates on 4 races each, and was kept only if it beat the general policy
on 20 fresh races (the script's own rule). Then shipped and new were raced head to head at FIVE frame rates
on 20 fresh races each (seedbase 77000; the 120 fps column was not in the tuning set). Figures: each AI's
median finish in seconds (races finished within the cut, of 20).

**I1** (`skimrace-v4-i1` → `skimrace-v5-i1`, 23 numbers re-fitted, 4 minutes):

| I1, Hard | 120 fps | 62 fps | 36 fps | 20 fps | 12 fps |
|---|---|---|---|---|---|
| shipped v4 | 66.0 (20) | 66.0 (20) | 64.2 (20) | 64.6 (20) | 73.7 (20) |
| new v5 | 64.3 (19) | 65.1 (20) | 65.4 (20) | 63.5 (20) | **66.8** (20) |

The 120 fps cell's one unfinished race was checked on 40 more races at 120 fps (seedbase 123000): new 40/40,
median 73.9 s, 0.05 recoveries per race; shipped 39/40, 75.7 s, 0.55 recoveries per race. So v5 is level at
36-62 fps and better at both ends, and more robust at 120 fps. Kept.

**I2** (`skimrace-v2-i2` → `skimrace-v3-i2`, 22 minutes; on the script's own fresh races the new tuning's winner
median was 70.8 s against the general policy's 101.5 s):

| I2, Hard | 120 fps | 62 fps | 36 fps | 20 fps | 12 fps |
|---|---|---|---|---|---|
| shipped v2 | 82.1 (20) | 79.9 (20) | 78.6 (20) | 78.0 (20) | 88.0 (19) |
| new v3 | 80.6 (20) | 79.3 (20) | 78.3 (19) | 79.3 (20) | **84.3** (20) |

A small, consistent gain at both ends and level in the middle (99 of 100 races finished either way). The
shipped I2 was already the least frame-rate-sensitive of the four; its 12 fps tail is what moved. Kept.

**I4** (`skimrace-v1-i4` → `skimrace-v2-i4`, 15 minutes; on the script's own fresh races 142.3 s against the
general policy's 159.8 s):

| I4, Hard | 120 fps | 62 fps | 36 fps | 20 fps | 12 fps |
|---|---|---|---|---|---|
| shipped v1 | 164.3 (20) | 150.2 (20) | 149.3 (20) | 145.6 (20) | 145.8 (20) |
| new v2 | **150.8** (20) | 149.1 (20) | 146.0 (20) | 144.1 (20) | 144.1 (20) |

Better at every frame rate, most at 120 fps (−13.5 s), every race finished. Kept.

**A failure mode the retune did not touch.** In 1 of 100 I2 races (shipped and new alike) one AI gets stuck at
20-22 of 30 crystals with 7 recoveries and never finishes within the cut, while its teammate finishes
normally. It is a recovery-loop case, not a tuning number, and it is the same 1% before and after.

**Easy and Medium on the new policies** (the handicap asset is unchanged: §10's reaction times and mistake
chances). 21 races per cell spread over 16/28/50 ms frames, seat medians: I1 Hard about 65 s, **Medium 73.9 s,
Easy 88.9 s**; I2 Hard about 79 s, **Medium 94.9 s, Easy 121.5 s**. The ladder §10 set (I1 75/86 s, I2 92/112 s
at 28 ms frames) holds within a few seconds; Easy at I2 runs past the simulator's cut in a third of its races
(the cut is the benchmark limit plus 60 s; the game has none), as it did before.

**The general policy** (`skimrace-v2-general` → `skimrace-v3-general`; `run.sh tuneall 1,2,3,4 4 16 sigma=0.15
final=20 only=stated` under the same conditions, 36 numbers re-fitted, about 2.5 hours; its own fresh-seed
check finished 20/20 on every track, winner medians I1 57.6, I2 97.1, I3 175.0, I4 148.8 s). It is what
intensity 3 flies, so the head to head is on I3:

| I3, Hard (general policy) | 120 fps | 62 fps | 36 fps | 20 fps | 12 fps |
|---|---|---|---|---|---|
| shipped v2-general | 188.5 (20) | 185.3 (20) | 184.5 (20) | 185.0 (20) | 193.6 (20) |
| new v3-general | 184.6 (20) | 183.4 (20) | 182.1 (20) | 181.0 (20) | **185.7** (20) |

Better at every frame rate, every race finished. Kept.

**Re-raced under the gate** (the retune above ran with this branch's grid stagger; the merge replaced it with
the perf branch's gate, which only the I2 policy's planner feels). New v3-i2, 20 fresh races per cell:

| I2, Hard, new v3 | 120 fps | 62 fps | 36 fps | 20 fps | 12 fps |
|---|---|---|---|---|---|
| under the grid (tuned) | 80.6 (20) | 79.3 (20) | 78.3 (19) | 79.3 (20) | 84.3 (20) |
| under the gate (shipped) | **79.1** (20) | **77.1** (20) | **76.3** (20) | 79.0 (20) | 89.1 (20) |

Faster at 36-120 fps, level at 20, slower at 12 fps - at 12 fps every seat wants to re-plan every frame, so the
gate makes each re-plan every other frame (about 160 ms apart) and the planner reacts later. Every race finished.
A refinement nobody has measured: let the gate stand down when the frame is longer than half the re-plan period.

### 14.6 Where this leaves the AI (2026-10-07)

- Every shipped policy is now tuned across 62 / 36 / 20 fps with the game's contact step, and checked at 120
  and 12 fps as well. Against the previous files, on the same fresh races: level at 36-62 fps, better at 120 fps
  and at 12 fps on every track, and I4 better everywhere. Nothing got slower beyond noise; no new failure mode
  (the one-in-a-hundred stranded I2 seat predates this).
- The planner gate (§8.0i, kept at the merge over this branch's grid) halves the editor's worst AI frame at any
  frame rate; with the perf branch's float loops and no-alloc Mathf the whole AI on I2 costs the editor about
  2.2 ms per AI per frame in its own Mono mode (§14.4's last table), 0.4 ms in a Release/IL2CPP build. Release
  code optimization in the editor remains the single biggest lever a tester has.
- The next measurement that matters is the one only the editor can give: a hand-played race's `frameMs` next
  to its AI finish time (the recorder writes both). `Docs/UNITY_VERIFICATION_CHECKLIST.md`, the 2026-10-06
  entry, lists the steps.
- To redo any of this after a map or code change: `python3 Tools/Build/skimrace_retune.py <I>` (per intensity),
  and for the general policy the `tuneall` line above, transcribed into `author_skimrace_ai_config.py`.
- The session that produced §10–§14 (asks, decisions by date, every commit, the verification record, open
  items) is written up in `Docs/SKIM_RACE_AI_SESSION_LOG.md`.
