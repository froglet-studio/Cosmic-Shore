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

### Pass 3 (asked by the plan thread: two fresh restarts at 2.5x the evals, and the best run continued)

| Run | Evals | Loss | Last gain | I1 | I2 | I3 | I4 (training courses) |
|---|---|---|---|---|---|---|---|
| B, continued from pass 2 | 2,403 | 2.873 | eval 864 | 94 s | 95 s | 100 s | 101 s |
| E, fresh | 1,503 | **2.847** | eval 972 | 104 s | 90 s | 99 s | 85 s |
| D, fresh | 1,503 | 5.015 | gains under 0.01% after eval 1,242 | 181 s | 147 s | 149 s | 167 s |

B: turn 108.3°, distance 4.9, interval 0.42 s, cone 6.8°, max plant 1.2 s, hold 0.03 s, tolerance 29.4°.
E: turn 110.5°, distance 11.3, interval 0.22 s, cone 9.0°, max plant 4.3 s, hold 0.04 s, tolerance 3.8°.

B and E are 0.9% apart and neither gained anything in its last 20% of evals (B none after 864 of 2,403, E none after
972 of 1,503). D, like pass-1 B and pass-2 C, settled in the slow basin (long sling intervals, few launches). So
three of six restarts find the fast "plant beside the nest" basin and agree, and three stop in a slower one.

On 12 fresh courses per intensity (seeds 1001-1012), every finisher finishes 48 of 48:

| Genome | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| Pass-3 E (the proposal: lowest loss) | 109 s | 102 s | 94 s | 103 s |
| Pass-3 B | 105 s | 100 s | 102 s | 104 s |
| Pass-2 B | 109 s | 104 s | 101 s | 103 s |

On fresh courses the fast-basin genomes are level within a few percent. E is the proposal for its lower loss and
because its aim tolerance (3.8°) stays near the shipped 3°; B is its twin.

## 5. Is the Thresher's Time AI "well trained"?

| Test (the project's definition) | Result |
|---|---|
| Restarts within 2%, no improvement in the last 20% of evaluations | **Yes for the best basin, in the model.** After pass 3, the two best runs agree within 0.9% (2.847, 2.873) and neither improved in its last 20%. Three of six restarts stop in a slower basin (4.66-5.02), so a single restart is not enough. |
| Beats every genome on the composite | Yes in the model: the shipped and base genomes finish no nest race. |
| Within 15% of the best human time | **Cannot be judged**: no person has finished a nest race. Against the ring-era times (Garrett I1 64.6 s, I2 106.1 s; Shombith I4 120.1 s) the model's pass-3 E runs 109 / 102 / 103 s at I1 / I2 / I4: 69% slower than Garrett at I1, level with him at I2, faster than Shombith at I4. Different courses, a different gate, and a model that runs slow. |
| No missed rings in the real game | **Not run**: needs the Unity editor (`ThresherTimeSoakRunner` at I1-I4 on the nests). |

**Verdict: two of four parts pass, in the model only** (restarts converge; it beats every genome). The human mark
does not exist for nests, and the editor soak has not run. So the Thresher's Time AI is **not yet well trained**: the
real-game soak with pass-3 E is the test that counts.

## 6. What is unverified, and what to do next

1. **The editor soak on the nests** (the one that decides): `ThresherTimeSoakRunner` with the shipped genome and the
   pass-3 winner (E), I1-I4, two seeds each. Needs Unity; it goes on Garrett's editor-check list.
2. **Promotion** only after that soak, through `learn_drives_from_flights.py`'s writer, by Yash. Nothing is written
   to `ThresherAIBoostPolicy_ThresherTime` here.
3. **Proposals for Yash** (code, not numbers; the model suggests them, the editor must confirm):
   - aim the launch from the launch point (the tangent point), not from the hull, so a 3° tolerance means 3° at the
     nest's core (the ring centre is already the core; the offset comes from where the launch leaves);
   - plant on a sharp turn close to the nest (the searched genomes' choice) rather than far back on the approach.
4. The model's limits (section 1) bias it slow; a person's nest race would let the human-time test be judged.

Results JSONs: `/mnt/project-files/regatta-training/results/thresher-time/`.

## 7. With the nose lead on (2026-11-07)

Brief 20 of the Regatta balancing project: the lead-on twin `threshertime-thresher-time-lead` (same search, seeds and
genomes, with `nose_rate` 1.5 and `nose_lead` 2.5, the model of `AIPilot.NoseLeadGain` from PR #1786, which ships at 0).
**Every number is the MODEL's** (`thresher_time_sim.py`); nothing was raced in the game and nothing is written to an asset.

**One correction to the brief.** This model never assumed a lag-free nose: `race()`'s `nose_rate=None` is already the
fleet lag (`LERP` 1.5/s), so every number in sections 3-5 IS the lag-only cell (lag on, lead off). The extra cell
reported below is the lag-free one (`nose_rate` 0).

### The search

Two restarts (B, C), sep-CMA-ES, sigma 0.2, the twin's 3 courses per intensity, run by hand with `local_run.py`.

| Run | 540 evals | 1,503 evals | 2,511 evals | Last gain |
|---|---|---|---|---|
| B | 3.176 | 2.878 | **2.736** | eval 2,097 of 2,511 (none in the last 16%) |
| C | 3.024 | 2.943 | **2.794** | eval 2,520 (still improving) |

Both end in ONE basin, not the parent's: plant on a turn of about 95° (the parent's: 110°), a sling every 0.75-0.85 s,
and an aimed hold of 1.28 s (the parent's: 0.04 s); the lead lets the AI hold the aimed launch and trust its aim.
B (2,511): turn 96.2°, distance 20.2, interval 0.84 s, cone 10.3°, max plant 1.6 s, hold 1.28 s, tolerance 6.9°.
C (2,511): turn 95.4°, distance 0.2, interval 0.75 s, cone 15.8°, max plant 3.2 s, hold 1.28 s, tolerance 9.6°.
They are 2.1% apart and C was still improving, so by the project's rule (within 2%, no gain in the last 20%) the
lead-on search has **not quite converged**.

### The 2x2 (plus the lag-free cell and gain 1.5), 12 FRESH courses per intensity (seeds 1001-1012)

Loss as the evaluator scores it (no stay-near term); finished of 48; median finish per intensity, seconds.

| Genome | Nose | Loss | Finished | I1 | I2 | I3 | I4 |
|---|---|---|---|---|---|---|---|
| Parent's winner (pass-3 E) | lag-free | 3.364 | 48/48 | 111 | 103 | 99 | 98 |
| Parent's winner (pass-3 E) | lag, lead off (today) | 3.195 | 48/48 | 109 | 102 | 94 | 103 |
| Parent's winner (pass-3 E) | lag, lead 1.5 | 3.111 | 48/48 | 103 | 93 | 101 | 107 |
| Parent's winner (pass-3 E) | lag, lead 2.5 | 3.175 | 48/48 | 106 | 90 | 98 | 105 |
| Lead-on B, 1,503 evals | lag-free | 11.662 | 42/48 | 143 | 191 | 189 | 193 |
| Lead-on B, 1,503 evals | lag, lead off | 10.091 | 44/48 | 97 | 146 | 121 | 172 |
| Lead-on B, 1,503 evals | lag, lead 1.5 | **2.784** | 48/48 | 91 | 88 | 93 | 93 |
| Lead-on B, 1,503 evals | lag, lead 2.5 | 2.986 | 48/48 | 94 | 102 | 94 | 94 |
| Lead-on B, 2,511 evals | lag, lead off | 12.326 | 33/48 | 108 | 100 | 99 | 92 |
| Lead-on B, 2,511 evals | lag, lead 1.5 | 3.417 | 48/48 | 94 | 94 | 96 | 98 |
| Lead-on B, 2,511 evals | lag, lead 2.5 | 3.013 | 48/48 | 96 | 99 | 101 | 97 |
| Lead-on C, 2,511 evals | lag, lead off | 13.473 | 31/48 | 101 | 106 | 91 | 217 |
| Lead-on C, 2,511 evals | lag, lead 1.5 | 3.897 | 48/48 | 92 | 93 | 98 | 104 |
| Lead-on C, 2,511 evals | lag, lead 2.5 | **2.985** | 48/48 | 98 | 100 | 93 | 106 |

(Every cell, the training courses too: `/mnt/project-files/regatta-training/results/thresher-lead/cmp_*.json`.)

What it says:
1. **The lead alone changes little for the Thresher.** The parent's winner already finishes all 48 nests with the lag,
   and the 2.5 lead moves its loss by 0.6% (3.195 to 3.175; gain 1.5: 2.6%). The Thresher's launch snaps nose and
   command together, so the lag costs it little; unlike the Scarab, Serpent, Dolphin and Manta, there was no
   overshoot to remove. The lag-free cell is even slightly WORSE (3.364): the parent's genome was tuned on a lagged nose.
2. **The drive must move to profit from the lead, and then it gains about 6%**: the lead-on winners score 2.985-3.013
   at gain 2.5 on fresh courses against the parent's 3.175 with the lead (3.195 without), all 48 finished, medians
   93-106 s against 90-106 s.
3. **The lead-on genomes need the lead.** Without it they DNF 4-17 of 48 fresh courses (loss 10-13), and with no lag at
   all they collapse (loss 12-20). So a lead-on genome must never ship while the switch is off; the parent's genome is
   the right one until Yash turns `NoseLeadGain` on.
4. **Gain matters per genome.** The 1,503-eval B is the most robust: best of all at gain 1.5 (2.784) and level with the
   final winners at 2.5 (2.986). The longer runs fit the 3 training courses tighter and lost ground at gain 1.5 on
   fresh courses (3.42-3.90), the sign of an over-fit to the training seeds.

**The roll stick** (the Manta thread's warning: in a model that ports AIPilot's roll stick, the 2.5 lead can corkscrew
the hull). This model does port it (`race()`: the roll stick turns the command about the nose's forward, Roll 130).
Its docstring records the corkscrew in CRUISE (no sling): at I4 course 1 the 2.5 lead passes 1 gate where today's
cruise passes 8. In the sling races above it never shows: no lead-on race DNFs or finishes fewer nests than its
lead-off twin for the parent's genome, at gain 1.5 or 2.5, because each plant and launch snaps nose and command
together. The cruise-only seat is the one it would hurt, and no Thresher Time AI cruises.

### Is it "well trained" with the lead on?

| Test | Result |
|---|---|
| Restarts within 2%, no gain in the last 20% | **Not quite**: 2.736 / 2.794 (2.1% apart), one basin, C still improving. |
| Beats every genome on the composite | Yes, in the model, with the lead on (fresh 2.985-3.013 vs the parent's 3.175 and the shipped genome's 21.69, 0/48). |
| Within 15% of the best human time | Cannot be judged: no person has finished a nest race. |
| No missed rings in the real game | Not run: needs the editor (`ThresherTimeSoakRunner`) with `NoseLeadGain` on. |

**Proposal for the day Yash turns the switch on** (not before; the job's `promote` rule): the 1,503-eval lead-on B
(turn 93.0°, distance 21.4, interval 0.59 s, cone 7.3°, max plant 1.7 s, hold 1.28 s, tolerance 9.5°;
`results/thresher-lead/lead_B_1503.json`), because it holds up at both gains on fresh courses; the 2,511-eval C if
Yash fixes the gain at 2.5. Until then the parent's pass-3 E stays the proposal. A third restart pair, or a search
that races 6+ courses per intensity, would settle convergence and the over-fit; the editor soak decides either way.
