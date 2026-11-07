# The Urchin's Skein AI: a model, two searches, and what limits it (2026-11-07)

Plan step: `time-arcade` (`Docs/AI_SYSTEM/AI_PLAN.md`). Owner of the area: Yash (vessel AI, the Urchin autopilot, Skein).
**Every number here comes from a model.** No Unity editor ran, and nobody has finished Skein, so there is no human mark.

## What was built

| Piece | What it is | Gate |
|---|---|---|
| `Tools/Build/skein_course_harness/` | Exports Skein's shipped cables from `SkeinCourse.cs`, using the `SpawnableSkein{1..4}` prefab seeds and the seed ladder (24 rings; pinned rings per intensity 0 / 7 / 11 / 22). It also exports two hold-out cables per intensity. | `export_course.sh --check` checks parity with the C#. As a negative control, a changed seed fails it. |
| `Tools/Build/skein_urchin_sim.py` | One AI Urchin racing Skein. | `--self-test` checks the vessel numbers against the assets, the `Decide` cases, the pinned counts and determinism. As a negative control, a genome that slips off every rail must race I1 slower. |
| `Tools/Compute/evaluators/skein_fit.py` (VERSION 1) | Scores the 16 decision fields of `UrchinAutopilotConfigSO` on I1-I4. Each race scores time / ideal; the composite is mean + 0.5 × worst. The search is seeded from the shipped asset. | `self_test` includes a negative control. |
| `Docs/Compute/jobs/skein-urchin-time.json` | The job: plan `regatta-balance`, stage 1. Its status is **proposed**, and it is run by hand while Compute is paused. | `compute.py validate` passes. |

The sim models these parts of the game:
- the gate aim and Skein's rail lead (260);
- AIPilot's stick and skill throttle, plus the free hull's turn law and nose lag;
- the ride: own colour at 300 u/s, a rival's at 20 u/s, the launch kick at 1.2× and its bleed, and the 0.35 s re-latch grace;
- the latch, as a trigger sampled every 0.04 s;
- the Track Projector;
- `UrchinAutopilotDriver` / `UrchinRailAssessment`, ported line for line.

The sim leaves these out (`LIMITS` in the sim):
- rivals and repainting;
- the 4.5 u surface offset;
- Catmull-Rom curves (rails are straight chords between prisms);
- the spikes' conversions;
- crystals.

`race()` also takes two what-if args, `steering="regatta"` and `seek=True`. They are explained under "Why the genome cannot fix Skein".

Ideal seconds (the rings' polyline at 300 u/s): I1 27, I2 51, I3 83, I4 91.

## The shipped AI in Skein, as it ships

The shipped cable, three start slots, and two hold-out cables per intensity give 9 races per intensity:

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| finished | 9/9 | 5/9 | 0/9 | 0/9 |
| median s / rings | 73 s | 342 s | 9.6 of 24 | 6.8 of 24 |
| share of time on a rail | 0.14 | 0.05 | 0.02 | 0.02 |

The AI almost never rides. On I2-I4 it misses the pinned rings: their mouths are 27-40 u wide, and the gate aim orbits the approach point 300 u out because `aiCommitFromApproachPoint` is off in Skein. It then crosses those rings off-centre. **This is the mode's aim, not the genome.** No genome makes the Urchin look for a rail, because nothing in Skein steers a free Urchin toward one. Regatta has `TryOwnLaneAhead` for that; Skein does not.

## The two restarts (about 1500 evaluations each, sep-CMA-ES, 2 cores each, about 67 min)

Training used the shipped cable from three slots. The hold-out score uses all 9 races per intensity.

| Genome | training loss | hold-out composite | I1 | I2 finished | I3 rings | I4 rings |
|---|---|---|---|---|---|---|
| shipped | 18.14 | 19.13 | 73 s | 5/9 | 9.6 | 6.8 |
| restart B | 15.39 | **16.43** | 65 s | **9/9** (337 s) | 14.8 | 4.8 |
| restart C | 15.33 | 18.97 | 73 s | 6/9 | 17.3 | 7.0 |

- **The restarts do not agree**, so the landscape is noisy. Which start slot is used decides whether I2 finishes.
- B holds up on the hold-out cables: every I2 race finishes and the composite is 14% better.
- C is over-fit to the training cable.
- Neither restart finishes a single I3 or I4 race. **By rule 11 the Urchin is not "well trained"** in Skein as it ships.

What B and C agree on, compared with shipped:
- `ParkedSpeed` goes from 6 to about 19. A slowed rival-colour crawl now counts as parked and is slipped off sooner.
- `DryCrawlArc` goes from 80 to 260-360.
- `SpikeMinAmmo` goes from 0.2 to about 0.75, which keeps ammo for slips.
- `MinSlipIntervalSeconds` goes from 1.5 to 2.5-4.

The rest of the two genomes differs. Full genomes: `/mnt/project-files/regatta-training/results/skein-urchin/`.

## Why the genome cannot fix Skein: the what-if search

A third search (W, 1500 evals, run as a scratch copy of the job with `steering=regatta, seek=1`; **not** a job file) raced Skein with two mode-side changes:
1. Regatta's commit-from-approach aim, which Regatta and Switchback already use.
2. A seek toward the rail that threads the next ring, following the `TryOwnLaneAhead` pattern.

Hold-out results (9 races per intensity):

| | composite | I1 | I2 | I3 | I4 |
|---|---|---|---|---|---|
| shipped genome, shipped Skein | 19.13 | 73 s | 5/9 finish | 0/9 | 0/9 |
| shipped genome, what-if Skein | 8.77 | 75 s | 177 s | 284 s | 372 s, **all 36 finish** |
| genome W, what-if Skein | **7.94** | 75 s | 152 s | 226 s | 352 s, all finish |
| genome W, shipped Skein | 20.48 | 73 s | 3/9 | 0/9 | 0/9 |

- **The mode's aim is the big lever.** It takes the loss from 19.1 to 8.8 and makes every race finish.
- Once the aim is fixed, the genome is worth another 9.5%: I2 is 14% faster and I3 20% faster.
- The genome that is best for each Skein is wrong for the other.

So training the genome first and then changing the aim would undo the training. The order should be:
1. Yash decides Skein's AI aim and seek.
2. Then search the genome.

What genome W changes, compared with shipped:
- it leaves dry crawls sooner: `MaxDryCrawlSeconds` 4 → 1.3, `DryCrawlArc` 80 → 46;
- it rides anything that closes at all: `ClosingFraction` 0.7 → 0.12;
- it launches off ends 500 u out: `LaunchPreferArc` 160 → 502;
- it scans further: `ScanArc` 1600 → 2278.

Even then, I4 takes about 3.9× the ideal time and the rail share is under 0.1. A wider seek, or the Track aimed at the rings, are the next levers, and both are mode-side.

These mode changes are allowed: a mode makes itself interesting with its AI, and the Urchin's kit is untouched. **They are proposals for Yash.** Nothing under `Assets/` changed.

## What the trained decisions change in Regatta

- **In the models, nothing.**
  - `race_models.rail` and `regatta_measured_roster.py`'s Urchin both race "own lane at RAIL u/s, free stretch at FREE u/s" (288.3 / 112.7, fitted to editor races). Neither model has a genome input.
  - A trained genome would only show up there if the free and rail speeds were measured again in the editor.
- **In the game, the genome is shared.** `Assets/Resources/UrchinAutopilotConfig.asset` is the one config Skein, Regatta and Hijack load (`LoadDefault`; there is no per-mode file).
  - On Regatta's closed own-colour lanes, `Decide` always rides, so only these fields fire: the dry-crawl exit (`MaxDryCrawlSeconds`, `DryCrawlArc`), `ParkedSpeed` / `ParkedSeconds`, `MinSlipIntervalSeconds`, and the Track fields. They act when the Urchin first touches a rival's super-shielded lane.
  - Restart B moves those fields the wrong way for Regatta. It crawls a rival lane longer (`MaxDryCrawlSeconds` 4 → 5.1) and slips less often (`MinSlipIntervalSeconds` 1.5 → 4.0).
  - Genome W moves them the right way. It leaves a dry crawl after 1.3 s.
  - Neither effect is measured. Regatta has no Urchin decision model.
- **Proposal:** give the config a per-mode file, following the `MantaSoarAITuningSO.LoadForMode` pattern, so that a Skein genome never re-tunes the Regatta Urchin by accident. This is a code change in Yash's area.

## What is promoted

Nothing. These are the candidates and their conditions:
- B is a candidate for Skein as it ships. It finishes every I2 race on the hold-out cables, but it fails "well trained", and it pushes Regatta the wrong way through the shared asset.
- W is the candidate if Skein takes the aim and seek proposal. In that case, run `skein-urchin-time` again with `steering`/`seek` changed to match the shipped mode, and promote by the job's `promote` rule.

## Reproduce

```
bash Tools/Build/skein_course_harness/export_course.sh --check
python3 Tools/Build/skein_urchin_sim.py --self-test
python3 Tools/Compute/evaluators/skein_fit.py --self-test
RUN_DIR=$HOME/rt python3 /mnt/project-files/regatta-training/local_run.py skein-urchin-time B 1500 2
```
