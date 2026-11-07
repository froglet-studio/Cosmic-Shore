# Thresher Time model for the Thresher AI (2026-11-06)

Plan step: `time-arcade` (`Docs/AI_SYSTEM/AI_PLAN.md`). Brief 8 of the Regatta balancing project (asked by Garrett,
2026-11-06). The Thresher is Garrett's vessel; its AI and Thresher Time are Yash's. **Every number below is a MODEL's**,
raced offline, never a measurement of the game. The editor decides (`ThresherTimeSoakRunner`, THRESHERTIME.md §4).
Nothing here changes a game asset: the searched genomes are proposals for Yash.

## 1. What was built

| Piece | What it is |
|---|---|
| `Tools/Build/thresher_time_sim/` (`Driver.cs`, `Stubs.cs`, `run_probe.sh`, `plant_probe.json`) | The PLANT PROBE: compiles the shipped `ThresherChainSolver` from its real path (never copied) with .NET 8, flies it at `ThresherConfig.asset`'s own dials the way the AI plants (Plant and Winch together), and writes the planted orbit (speed, radius, spin, launch-view grip against time and entry speed) and the aimed launch (wait, leave speed, how the ball's tug brakes it) to JSON. |
| `Tools/Build/thresher_time_sim.py` | The race model: the shipped course (`ThresherTimeCourse.ForIntensity` through course_port's HeadlongCircuit port), the 2026-11-04 NESTS (core blocks per intensity, break on any touch by hull, ball or chain, credit radius, regrow), the gate aim (GateRaceController's non-Regatta steering), AIPilot (stick, skill throttle, orbit break-off, nose lag), Thresher.prefab's turn and speed law, `ThresherSlingAIPolicySO`'s driver line for line, the aimed launch on the probed orbit, the exit buff. `--rings` races the mode as it was before the nests. `--self-test` (with a negative control: unbreakable nests credit no gate). |
| `Tools/Compute/evaluators/threshertime_fit.py` | The evaluator. Space: the seven numeric sling fields by their asset names (`plantMinTurnDegrees`, `plantMinDistance`, `slingIntervalSeconds`, `releaseConeDegrees`, `maxPlantSeconds`, `aimedHoldSeconds`, `aimToleranceDegrees`); the switches (`aimedLaunch`, `windWhilePlanted`) are not searched. Loss per race time / (path / 125 u/s), a DNF scored worse than any finish; per intensity mean + 0.5 x worst; overall mean + 0.5 x worst intensity, + 0.01 x distance to the nearest genome. Seeds: every Thresher genome (the `_ThresherTime` variant, then the base policy). `--self-test` passes. |
| `Docs/Compute/jobs/threshertime-thresher-time.json` | The job, status **proposed**, run by hand (Froglet Compute is paused). `compute.py validate` passes. |

NOT modelled: rivals and their trails, the cage plates (I2-I4 cost no speed in the model), the reward crystal in
every third nest, the cell's plants and creatures (THRESHERTIME.md's intensity table), the plant's hook, the planted steer, the comeback, the lobby
handicap, frame hitches. The ball is taken to trail at rest length while towing.

## 2. Calibration against the real game

The only real-game AI races are the 2026-11-02 soak (rings, before the nests; THRESHERTIME.md §3), raced again in the
model on the same courses (`--soak`):

| I1, seeds 7001 / 7002 (rings) | Game | Model |
|---|---|---|
| Shipped genome, aimed launch on | won 77.0 s / 115.7 s (2/2) | 122.5 s on one, 1/16 rings on the other (1/2) |
| Cruise only (no sling) | 159.6 s / 142.0 s | 175.0 s / 175.0 s |
| Launch speed, median | 2,410-4,069 u/s | 2,288 u/s |
| Launch angle off the ring, median | 4.1-9.6° | 5.0° |

So the model is **slow and pessimistic**: cruise is 10-23% slow, and its aimed-launch ring races finish less often
than the game's (1/2 against 2/2). Launch speed, angle and cadence are in the game's range. A model genome that does
well here should do at least as well in the game, but the ranking between two good genomes is not proven.

No AI has raced the nests in the game yet, and no person has finished a nest race (the people's races in
`LEARNED_DRIVES_ThresherTime.md` are all on rings: Garrett I1 64.6 s, I2 106.1 s; Shombith I4 120.1 s).

## 3. What the model says about the shipped AI on the nests

The shipped genome (`ThresherAIBoostPolicy_ThresherTime`, learned 2026-11-02 from people's ring races) **finishes no
nest race in the model** (0 of 24 fresh courses). It launches on schedule (about 135 launches a race at about 2,290
u/s, 5° off the ring) but its launches pass about 60 u from the nest's core, so they rarely touch a block; one or two
nests break a race. The base policy does the same (0/24). Cruise-only (no sling) finishes 9 of 24, mostly at I1, by
ramming each core with the hull.

Why: the launch leaves from the orbit's tangent point, which sits about one orbit radius off the line to the ring, and
the AI aims at the ring centre with a 3° tolerance. On rings, passing 60 u off the centre is inside the mouth; on a
nest it is a miss. The searched genomes instead **plant beside the nest** (within 7-21 u, on a turn of 109° or more)
so the ball and chain sweep through the core: "the Thresher's way through a nest" that THRESHERTIME.md's opening
section designs for (plant beside it and orbit, so the chain sweeps the cage).

## 4. The search

Two restarts per pass (labels B and C), sep-CMA-ES, sigma 0.2, 3 courses per intensity (seeds 1-3), limit 600 s,
run by hand with `local_run.py` on this cloud container.

### Pass 1 (423 evals each)

| Genome | Loss | I1 | I2 | I3 | I4 |
|---|---|---|---|---|---|
| Shipped | 22.113 | 0/3 | 0/3 | 0/3 | 0/3 |
| B | 4.664 | 163 s | 160 s | 145 s | 142 s |
| C | 3.004 | 109 s | 87 s | 101 s | 98 s |

B: turn 108.7°, distance 21.5, interval 4.5 s, cone 21.2°, max plant 4.7 s, hold 0.46 s, tolerance 29.4°.
C: turn 110.0°, distance 7.3, interval 0.22 s, cone 4.8°, max plant 2.6 s, hold 0.01 s, tolerance 41.4°.
The restarts did **not** agree (35% apart), so pass 1 had not converged.

Fresh courses (6 per intensity, seeds 1001-1006, never searched on), median finish:

| Genome | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Shipped | 0/6 | 0/6 | 0/6 | 0/6 |
| Base policy | 0/6 | 0/6 | 0/6 | 0/6 |
| Cruise only | 6/6, 471 s | 2/6, 584 s | 1/6, 527 s | 0/6 |
| Pass-1 B | 6/6, 166 s | 6/6, 176 s | 6/6, 167 s | 6/6, 166 s |
| Pass-1 C | 6/6, 118 s | 6/6, 109 s | 6/6, 95 s | 6/6, 108 s |

Both hold up on fresh courses, so neither is an over-fit to seeds 1-3.

### Pass 2 (new epoch; both stopped at 585 evals on the job's convergence rule)

| Genome | Loss | I1 | I2 | I3 | I4 |
|---|---|---|---|---|---|
| B | 3.000 | 95 s | 95 s | 106 s | 98 s |
| C | 4.951 | 187 s | 156 s | 157 s | 135 s |

B: turn 108.3°, distance 1.1, interval 0.45 s, cone 6.9°, max plant 1.1 s, hold 0.05 s, tolerance 30.0°.
C: turn 112.2°, distance 10.0, interval 6.6 s, cone 36.4°, max plant 0.49 s, hold 2.37 s, tolerance 4.8°
(it never launches at I1: it plants briefly and lets the chain sweep, then cruises).

**Two basins across the four restarts.** Pass-2 B (3.000) and pass-1 C (3.004) agree within 0.1% and sit in the
same place: plant on a turn of about 110° within a few units of the nest, short plants, no hold, a wide aim
tolerance (30-41°), so the AI sweeps the core and launches toward the next nest at once. Pass-1 B (4.66) and pass-2
C (4.95) found a slower basin (long sling intervals, fewer launches). Pass-2 B was still improving in its last 20%
(3.027 to 3.000 at eval 540 of 585), so the fast basin is found but not finished.

The promotion race (the job's `promote` rule: 12 FRESH courses per intensity, seeds 1001-1012, never searched on):

| Genome | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Shipped | 0/12 | 0/12 | 0/12 | 0/12 |
| Base policy | 0/12 | 0/12 | 0/12 | 0/12 |
| Cruise only | 10/12, 489 s | 8/12, 503 s | 2/12, 527 s | 0/12 |
| **Pass-2 B** | **12/12, 109 s** | **12/12, 104 s** | **12/12, 101 s** | **12/12, 103 s** |
| Pass-1 C | 12/12, 113 s | 12/12, 106 s | 12/12, 95 s | 12/12, 108 s |

Pass-2 B is the proposal: it finishes every fresh course at every intensity and is within a few percent of pass-1 C,
its twin from the other pass.

## 5. Is the Thresher's Time AI "well trained"?

| Test (the project's definition) | Result |
|---|---|
| Restarts within 2%, no improvement in the last 20% of evaluations | **No.** Two of four restarts agree within 0.1% (3.000, 3.004); the other two land in a slower basin (4.66, 4.95); the best was still improving in its last 20%. |
| Beats every genome on the composite | Yes in the model: the shipped and base genomes finish no nest race. |
| Within 15% of the best human time | Cannot be judged: no person has finished a nest race. Against the ring-era times (Garrett I1 64.6 s, I2 106.1 s; Shombith I4 120.1 s) the model's pass-2 B runs 109 / 104 / 103 s at I1 / I2 / I4: 69% slower than Garrett at I1, level with him at I2, faster than Shombith at I4. Different courses, a different gate, and a model that runs slow. |
| No missed rings in the real game | **Not run**: needs the Unity editor (`ThresherTimeSoakRunner` at I1-I4 on the nests). |

**Verdict: not yet well trained.** The model shows the shipped genome cannot clear nests and that a "plant beside the
nest" genome can, but the real-game soak is the test that counts.

## 6. What is unverified, and what to do next

1. **The editor soak on the nests** (the one that decides): `ThresherTimeSoakRunner` with the shipped genome and the
   pass-2 winner, I1-I4, two seeds each. Needs Unity; it goes on Garrett's editor-check list.
2. **Promotion** only after that soak, through `learn_drives_from_flights.py`'s writer, by Yash. Nothing is written
   to `ThresherAIBoostPolicy_ThresherTime` here.
3. **Proposals for Yash** (code, not numbers; the model suggests them, the editor must confirm):
   - aim the launch from the launch point (the tangent point), not from the hull, so a 3° tolerance means 3° at the
     nest's core (the ring centre is already the core; the offset comes from where the launch leaves);
   - plant on a sharp turn close to the nest (the searched genomes' choice) rather than far back on the approach.
4. The model's limits (section 1) bias it slow; a person's nest race would let the human-time test be judged.

Results JSONs: `/mnt/project-files/regatta-training/results/thresher-time/`.
