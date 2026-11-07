# The Serpent's Serpentine AI (2026-11-07, `Plan step: time-arcade`)

Brief 3 of the Regatta balance work (Garrett, 2026-11-06). Serpentine is `GameModes.SerpentTime`, the Serpent's own Time
game. **Every number below is from an offline MODEL**, not the game; the real-game checks are listed at the end.

## What was built

| Piece | What it is |
|---|---|
| `Tools/Build/serpent_time_sim.py` | One AI Serpent racing Serpentine: the course (`SerpentTimeCourse.ForIntensity`, read from the C#, through `course_port`'s float32 HeadlongCircuit), the Regatta steering with the scene's distances (260 / 300 / 220), AIPilot's single stick and orbit break-off, the single-stick transformer's turn law and AI nose lag, `ConsumeBoostActionExecutor` (four pellets, 6 s, 1 + 2n boost, no refill) and `PelletBoostAIPolicySO.Driver`, line for line. `--self-test` (parity, driver rules, negative controls), `--flights`, `--shapes`, `--old-kit`, `--trace` |
| `Tools/Build/serpent_time_course_harness/` | Compiles the shipped course code and `SerpentTimeCourseTests` with Roslyn and runs them (6/6 pass, 400 seeds an intensity), with a negative control; `--parity` writes the gates the sim is held to (28 courses, every gate within 0.05 u) |
| `Tools/Compute/evaluators/serpent_fit.py` | The search: the six decision fields of the pellet driver, the AI plan's composite (mean + 0.5 x worst over I1-I4), seeded from both Serpent genomes |
| `Docs/Compute/jobs/serpenttime-serpent-time.json` | The job, `status: proposed`, run by hand while Froglet Compute is paused |

The dirty race (the SerpentineGate shapes, sniper shots, Seed Walls, cloak, debris) is OFF in the search. `--shapes` is a
what-if for the standing shapes with the rifle never ready: the AI aims at the nearest exit (`SerpentineGate.Exit`), the
gate counts on its ring, and a crossing outside the exit is counted as a weave hit (its burn is not modelled).

## The kit changed under the human marks

The tank has not refilled on a clock since 2026-10-31: four pellets are the whole race unless the Serpent's own wall eats
its own trail, and an AI alone never builds one. The recorded flights show it:

- Every finished human race on today's two-lap, one-ring course (I1 65.7 and 70.0 s, I3 111.2 s, Shombith and Kyoki,
  2026-10-31) boosted 69-76% of the race, which four 6 s pellets cannot do: they were flown on the clock refill.
- The one race on today's kit (`flight_SerpentTime_Serpent_20261101_025634`, I3): the human boosted 13% of the race and
  did not finish (8 of 16 gates in 180 s); the Medium AI Mark finished in 179.8 s.
- The brief's I4 98.2 s is from the older one-lap, 16-ring cluster course.

So **no human time exists for today's kit**, and test (3) of "well trained" cannot be judged yet.

## Does the model race like the real AI?

The recorded AI rivals, raced by the model on the same courses (their `ringList`), with the kit each race was flown on:

| Race | Real AI (Medium) | Model, plain | Model, shapes what-if |
|---|---:|---:|---:|
| I3, today's kit (2026-11-01 02:56) | 179.8 s | 197.1 s | 185.8 s |
| I3, clock kit (2026-10-31 20:00) | 103.6 s | 83.4 s | 102.5 s |
| I3, clock kit (2026-10-31 20:45) | 119.2 s | 89.9 s | 105.8 s |
| I4, clock kit (2026-10-31 18:55) | 131.3 s | 106.9 s | 117.6 s |

With the shapes what-if the model is within 1-11% of the real AIs. The plain model is 18-25% fast on the clock kit (it has
no shapes, rivals or Medium handicap). One race on today's kit is a thin check.

## The search

Two independent restarts, about 2000 evaluations each, 4 courses an intensity (results:
`/mnt/project-files/regatta-training/results/serpent-time/`):

| Restart | Loss | alignDegrees | stickBand | engageRunwaySeconds | maxConcurrentBurns | minSecondsBetweenPellets | reservePellets |
|---|---:|---:|---:|---:|---:|---:|---:|
| Shipped `SerpentAIBoostPolicy` | 16.018 | 12 | 0.35 | 1.5 | 3 | 0.75 | 0 |
| Shipped `_SerpentTime` (learned from people) | - | 34.29 | 0.198 | 0.985 | 3 | 0.675 | 0 |
| B | 15.716 | 14.4 | 0.085 | 5.47 | 3 | 2.31 | 0 |
| C | 15.708 | 13.1 | 0.162 | 4.46 | 3 | 2.42 | 0 |

The restarts agree within 0.05% of loss and on the shape of the answer: **wait for a long straight (4.5-5.5 s of runway
per pellet), light at most three, 2.3-2.4 s apart, keep nothing back**. B made no gain in its last 32% of evaluations; C
gained 0.01% in its last 11%, so it has not formally plateaued (criterion 1's "no gain in the last 20%").

On 12 FRESH courses an intensity (seedbase 1001, the job's promotion check), seconds, every race finished:

| Genome | I1 | I2 | I3 | I4 | Missed mouths | Composite |
|---|---:|---:|---:|---:|---:|---:|
| `_SerpentTime` (what the mode flies today) | 133.4 | 147.1 | 176.5 | 195.0 | 40 | 17.66 |
| `SerpentAIBoostPolicy` (the hull's own) | 129.8 | 134.2 | 162.8 | 180.1 | 11 | 16.26 |
| Trained C | 125.0 | 131.4 | 160.5 | 178.8 | 4 | **15.85** |

Against what the mode flies today it is 6-11% faster at every intensity and misses a tenth of the mouths; against the
hull's own policy, 1-4%. With the shapes what-if it is still the fastest at every intensity. On the one recorded course of
today's kit it races 157.7 s against `_SerpentTime`'s 197.1 s (the real Medium AI took 179.8 s).

Why the people-learned `_SerpentTime` is the worst: its 34 degree align lights pellets into turns, and with no refill a
pellet spent turning is a straight lost and a wider, missed mouth.

The pellets are a small lever: a perfect plan saves about 40 s a race (24 pellet-seconds x 100 u/s at 60 u/s cruise),
and a Serpent that never burns is only about 20% slower. The race is mostly cruise, so the course and the shapes decide
more than the drive.

## "Well trained" (Phase A)

| Test | State |
|---|---|
| 1. Restarts within 2%, none improving in the last 20% | within 0.05%; B flat, C a 0.01% gain late |
| 2. Beats every genome on the composite over I1-I4 | yes, in the model (15.85 vs 16.26 and 17.66 on fresh courses) |
| 3. Within 15% of the best human | cannot be judged: no human has finished on today's kit |
| 4. Real game: no missed mouths alone and in a grid, model within 10% | not run (needs the editor) |

## The proposal (for Yash, the AI owner)

Trained C as `SerpentAIBoostPolicy_SerpentTime`, written with `learn_drives_from_flights.py`'s writer, only with a core
four approval. Nothing was written to any asset. Regatta is unchanged by it (Regatta flies the hull's own policy). The
Serpent's kit is Garrett's and was only read.

## Real-game checks (later)

- AI Serpents race Serpentine at I1-I4 alone and in a grid, with the dirty race on: times and missed mouths against the
  table above.
- A human race on today's kit at each intensity, so test (3) has a mark.
- Whether an AI ever refills its tank in a grid (its dirty cloak's wall eating its own trail): the model assumes never.

## With the nose lead on (2026-11-07)

Brief 17 of the lead-on round (`/mnt/project-files/regatta-training/BRIEFS.md` §14, §17), `Plan step: time-arcade`.
Every number here comes from the model (`serpent_time_sim.py`, the dirty race off, as above). Nothing was raced in the editor.

**In short: give the Serpent gain 1.0, not the fleet's 2.5, and keep the drive trained C already found.** The Serpent model
carries AIPilot's single-stick roll (`SingleStickVesselTransformer`: roll -x at 0.4 v + 20 deg/s about the nose). With
the fleet default (gain 2.5, at most 40 deg past the aim) that roll corkscrews the hull the way the Manta thread found,
so about half the I3 / I4 races never finish, whatever the drive. Gain 1.0 means the pilot judges its error from the
command instead of the lagging nose, with no pull past the aim. It finishes every race, and it is the best setting found.
With it, the drive the lead-off search already found (trained C) is as good as any lead-on search's winner.

### The twins (two restarts each, 1,500 evaluations, 4 courses an intensity, run by hand)

| Job | `nose_lead` | Restarts' loss | Agree | Every training race finished |
|---|---|---|---|---|
| `serpenttime-serpent-time-lead` (the brief's twin) | 2.5, 40 deg | B 32.50 / C 31.30 | no (3.9%) | no: I3 and I4 DNF 1 of 4 in both |
| `serpenttime-serpent-time-lead20` (new) | 2.5, 20 deg (`RaceLineStyleSO.NoseLeadMaxExtraDegrees`) | D 15.335 / E 15.332 | yes (0.03%) | yes, 0 misses |
| `serpenttime-serpent-time-lead1` (new) | 1.0 | F 15.1415 / G 15.1418 | yes (0.002%) | yes, 1 miss |

All three keep the parent's shape (align 11-14 deg, 4.5-5.3 s of runway per pellet, three burns, no reserve), except
lead1 (3.1 s runway, 0.8 s between pellets). None is formally flat: each last improved inside its final 20% of
evaluations (F at 1,413 and G at 1,503 of 1,503, by about 0.001%).

### The comparison on 12 FRESH courses an intensity (seedbase 1001), composite (lower is better), seconds, misses

| Drive | Lead off | Gain 1.0 | 2.5 / 40 deg | 2.5 / 20 deg |
|---|---:|---:|---:|---:|
| `_SerpentTime` (what the mode flies) | 17.66, 40 misses | 19.17, 47 | 48.23, 19 DNF | 24.08, 25 |
| `SerpentAIBoostPolicy` (the hull's own) | 16.26, 11 | 16.52, 17 | 41.36, 12 DNF | 17.77, 8 |
| **Trained C (parent, lead off)** | **15.85**, 4 | **15.24**, 2 | 40.91, 12 DNF | 16.00, 0 |
| lead B / C (2.5 / 40 twin) | 16.24 / 15.92 | 15.27 / 15.24 | 42.63 / 41.12, 15 / 13 DNF | 16.17 / 16.01 |
| lead20 D / E | 15.95 / 15.88 | 15.27 / 15.24 | 34.91 / 42.51, 9 / 16 DNF | 16.07 / 16.00 |
| lead1 F / G | 16.02 / 15.93 | 15.65 / 15.52 | 36.63 / 38.16, 11 / 12 DNF | 16.52 / 16.52 |

Trained C with gain 1.0, per intensity (mean s, misses): I1 123.5 / 0, I2 125.8 / 0, I3 153.9 / 0, I4 171.4 / 2. Lead off,
it was 125.0 / 0, 131.4 / 3, 160.5 / 0, 178.8 / 1. That makes it 1-4% faster, with half the misses.

- **The lead alone closes the gap.** Moving the drive adds nothing on fresh courses: trained C, lead20 E and lead C all score
  15.24 with gain 1.0. The lead1 twin's own winners are worse (15.52-15.65): they fit their four training courses, at 15.14.
- **2.5 / 40 is not usable for the Serpent**: 9-19 of 48 fresh races DNF for every drive, nearly all at I3 / I4. Capping it at 20 deg finishes every
  race with no miss, but is slower than gain 1.0 (16.00 against 15.24) and no better than the lead off (15.85).
- **The roll is the cause.** In a what-if with the roll term removed (`results/serpent-lead/whatif.py`, NOROLL=1, a scratch
  copy of the model), trained C finishes all 48 races at 2.5 / 40 (19.07, against 12 DNF with the roll), all 48 at 1.5 / 40 (16.80) and all 48 at gain 1.0
  (15.25). With the roll, gain 1.5 already DNFs 2 of 48 (31.73). The real Serpent rolls, so this is a diagnosis, not a setting.

Human marks (I1 64.2, I3 111.2, I4 98.2 s) were all flown on the pre-2026-10-31 refill kit. As above, they do not compare
with today's no-refill model.

### The proposal (for Yash, the AI owner)

For the day `AIPilot.NoseLeadGain` is switched on (PR #1786): **the Serpent's `RaceLineStyleSO.NoseLeadGain` = 1.0**, not
the fleet 2.5, with trained C as its Serpentine drive (the proposal above, unchanged). Nothing was written to any asset.
Real-game check (later, `Docs/UNITY_VERIFICATION_CHECKLIST.md`): AI Serpents race Serpentine I1-I4 at gain 0, 1.0 and 2.5.
Watch for the corkscrew stall at 2.5 (the stick pinned at full x and y while the hull rolls away from the ring). The
model's claim is that 2.5 stalls and 1.0 does not. Results: `/mnt/project-files/regatta-training/results/serpent-lead/`.
