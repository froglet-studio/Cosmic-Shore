# Regatta pool search: the tools and the dry run (2026-11-07)

Brief 12 of the 2026-11-06 Regatta balance plan (`Plan step: time-arena-course`). One Regatta level will shuffle between
several balanced courses (Garrett, 2026-11-06). His pass rule: a course may sit near 1.25x only if the spreads CANCEL
across the pool, each hull's mean time over the pool within about 1.10x of the others, and a hull that is fastest on
every course fails the pool. This is the search that finds such pools.

**Every number below is a MODEL** (the measured roster, `Tools/Build/regatta_measured_roster.py`). No Unity editor ran.

## 1. What was built

| Piece | What it does |
|---|---|
| `Tools/Compute/evaluators/regatta_pool.py` | Scores one course: ln(spread) over the scored hulls (the Sparrow raced and shown, not scored, until brief 4 fixes its rail rams), + 2 x CHAOS (each hull's standing, its ln time minus the field's mean, across the course's own seed and the next two seeds of the same settings: a coin-flip course scores high), + the guards (every rail through every mouth, the race 0.8-1.25x today's, every hull home). Gives the descriptors for quality-diversity: sharpness, mean corner, ridable rail, race length, tightest mouth, which hull leads and trails, gaps, alternates, crystals, contested rail. `features: true` adds brief 10's course features (#1787) in a compact form; `anchor_all` is a counterfactual (every measured AI tied on today's course). |
| `Docs/Compute/jobs/regatta-pool.json` | MAP-Elites over geometry, seed and neutral share; archive binned by sharpness x ridable rail x race length x leader (336 cells). Proposed, run by hand while Compute is paused. |
| `Docs/Compute/jobs/regatta-pool-features.json` | The same with brief 10's features: neutral and plain rail shares, a gap of N u on M legs, an alternate ring at each of the K sharpest corners, line crystals (10 numbers that a course keeps when another seed lays it, against brief 10's 82 per-leg numbers); binned by sharpness x ridable x alternates x leader. Needs #1787 on the branch it runs from. |
| `Tools/Build/regatta_pool_pick.py` | From any archives and result files: the 4-8 passing courses farthest apart in descriptor space whose per-hull means cancel to 1.10x with no hull fastest on every course. Recomputes everything from each course's per-hull seconds, so a different hull set (`--scored`) or pass line needs no new search. When nothing passes it says which rule failed and writes the closest pool. `--lifeforms` pairs each picked course with a lifeform setting from brief 11 (#1783) for variety: the model cannot see lifeforms, so the pairing changes no number. |
| `Tools/Build/regatta_pool_render.py` | Draws each candidate as a PNG still: rings (start in lime), the three rails in team colours with their kind, neutral stretches, gaps, alternate rings and line crystals, beside its numbers. |

Held by: `--self-test` on all three (negative controls: a 2.5x race is penalised; a flipped hull is chaos and a steady
field is not; a pool one hull leads every course, a chaotic course and a 1.4x course are all refused; a course with no
neutral share draws no neutral rail), `regatta_pool` in `compute.py self-test`, `compute.py validate` (18 jobs, 0 problems).

Run it: `RUN_DIR=... python3 /mnt/project-files/regatta-training/local_run.py regatta-pool <label> <evals> 4`; the
restarts from other regions, other rosters and the features job ran through `/mnt/project-files/regatta-training/pool_run.py`
(the same with the job changed in memory). Then
`python3 Tools/Build/regatta_pool_pick.py <archive.json ...> --out pool.json --lifeforms --render <dir>`.

## 2. The dry run on the measured roster: no course passes, and no course can

| Run | Space | Evals | Best loss | Best spread | Chaos | Fastest / slowest |
|---|---|---|---|---|---|---|
| today's course (I1, no neutral) | | | 1.288 | 1.99x | | Urchin / Serpent |
| MAP-Elites A | geometry, seed, neutral | 3008 | 0.378 | 1.43x | 0.010 | Manta / Dolphin |
| sep-CMA-ES from the old I1 row | same | 216 | 0.396 | 1.48x | 0.001 | Manta / Serpent |
| sep-CMA-ES from the old I2 row | same | 216 | 0.399 | 1.49x | 0.001 | Manta / Serpent |
| sep-CMA-ES from the old I3 row | same | 216 | 0.387 | 1.46x | 0.004 | Manta / Rhino |
| sep-CMA-ES from the old I4 row | same | 204 | 0.388 | 1.45x | 0.009 | Stoat / Serpent |
| MAP-Elites F | + brief 10's features | 1000 | 0.403 | 1.49x | 0.002 | Manta / Serpent |

Archive A filled 50 of 336 cells, F 41 of 189. The picker: 0 of 54 (A with the CMA bests) and 0 of 45 (F) courses pass
on their own, all on spread; the closest pools of four leave the per-hull means 1.43x and 1.45x apart.

**Why.** Every restart, from four regions, with and without the features, stops at 1.43-1.52x, and the gap is always
the same two groups: Manta, Stoat (and the Squirrel) against the Dolphin, Rhino and Serpent. Their measured races on
today's course are 67-75 s against 95-100 s, and the model moves every race-line AI by the same course ratios: a corner,
a mouth, an alternate ring (it saves each the same share of its race) or a gap (no race-line AI rides a rail) scales
all of them alike. Only two levers move one hull against the others: neutral rail and gaps slow the Urchin, and line
crystals lift the hulls Time reaches (the Stoat most). So **the course cannot close this gap in this model**; what
closes it is the AIs (time-arcade: each anchor is an untrained AI's measured race) or, last, the Time levels (stage 3,
Garrett's yes). The pipeline is ready to re-run the moment the roster changes: replace the job's `roster` anchors, no
code change.

The chaos is small everywhere (at most 0.01): the model is smooth from seed to seed. It will matter more when the
roster carries real race scatter.

## 3. The counterfactual: what pool the course finds once the AIs are even

`regatta-pool-features` with `anchor_all: 74.5` (every measured AI anchored at 74.5 s on today's course, the Urchin's
measured race at a 0.3 neutral share), 1200 evaluations: 61 of 189 cells, 13 courses pass on their own, won by six
different hulls. The picker's pool:

| Course | Spread | Fastest / slowest | Corners (hardest) | Ridable | Features | Lifeforms (variety only) |
|---|---|---|---|---|---|---|
| 1 | 1.07x | Urchin / Rhino | 84 | 74% | 17% plain, 4 gaps, 1 crystal | none |
| 2 | 1.12x | Stoat / Rhino | 114 | 99% | 35% plain, 7 alternates, 2 crystals | searched |
| 3 | 1.05x | Rhino / Urchin | 69 | 99% | 54% plain | searched |
| 4 | 1.08x | Manta / Urchin | 85 | 88% | 14% plain, 6 gaps, 7 alternates | thicket |
| 5 | 1.11x | Urchin / Rhino | 80 | 93% | 50% plain, 2 gaps, 3 alternates, 2 crystals | searched |
| 6 | 1.06x | Stoat / Urchin | 93 | 92% | 28% plain, 7 gaps, 5 alternates | searched |
| 7 | 1.13x | Serpent / Urchin | 84 | 78% | 46% plain, 3 gaps, 1 alternate | searched |
| 8 | 1.06x | Squirrel / Urchin | 108 | 79% | 36% plain, 4 gaps, 1 alternate | searched |

Per-hull means over the eight: Stoat 80.5, Manta 81.2, Serpent 82.3, Dolphin 82.5, Squirrel 82.6, Rhino 83.5, Urchin
84.1 s: **1.045x apart**, six different leaders (pools of 4-7 cancel to 1.041-1.054x). Stills:
`/mnt/project-files/regatta-training/results/pool-search/stills_counterfactual/` (one per course and `sheet.png`).
This is a demonstration that the search and the picker work end to end, not a pool to ship: its roster is invented.

## 3b. Re-run on the lead-on roster (PR #1826 knobs)

Job `Docs/Compute/jobs/regatta-pool-lead.json` (features on). Which model and knobs each hull raced on:

| Hull | Model | Nose rate | Lead |
|---|---|---|---|
| Rhino | drive sim (`RhinoAIBoostPolicy_Regatta`), anchored | 1.5 | 2.5 |
| Dolphin | drive sim, anchored | 1.5 | 2.5 |
| Serpent | line model, anchored | 1.5 | 2.5 |
| Sparrow (reported, not scored) | line model | 1.5 | 2.0 |
| Manta | measured anchor, lead off (#1829: a 2.5 lead traps it) | measured | off |
| Urchin | rail fit (speed 288.3, free 112.7) | n/a | n/a |
| Squirrel, Stoat | measured anchors | n/a | n/a |

The Manta is not on its drive sim at #1780's 9/s nose: that sim finishes in 26.5 s against the measured 67 s, so it is
too far off to use. A first run with the Manta at lead 2.5 (LA 2000 evals, three CMA restarts of 200) passed 0 of 73
courses, closest pool 1.17x; that run is kept for the record only.

Manta lead off, MAP-Elites LC and LD (2512 evals each) plus sep-CMA-ES restarts: **52 of 146 courses pass on their own,
and no pool passes Garrett's rule.** The closest is four courses at **1.106x** (rule 1.10x), four different leaders
(Urchin, Serpent, Stoat, Rhino), so no hull is fastest everywhere:

| Course | Spread | Fastest / slowest | Lifeforms (variety only) |
|---|---|---|---|
| 1 | 1.13x | Urchin / Squirrel | groves |
| 2 | 1.21x | Serpent / Urchin | reef |
| 3 | 1.15x | Stoat / Urchin | none |
| 4 | 1.15x | Rhino / Squirrel | reef-fenced |

Per-hull means: Manta 85.3, Rhino 86.5, Stoat 87.0, Dolphin 88.7, Serpent 91.8, Urchin 93.9, Squirrel 94.3 s.

Why it stops at 1.106x against the tied counterfactual's 1.045x: on every passing course the Squirrel stands 3.3-12.3%
behind the field and is never fastest, and the Manta stands 0.9-9.4% ahead. No course lever reaches the Squirrel (brief
10: only detours touch it), and none slows the Manta. The gap is small and sits in the roster, not the courses: a Manta
about 0.6% slower (or the Squirrel and Urchin each about 0.5% faster) would let this pool pass, by arithmetic on the
means above. The best losses of the restarts (0.138, 0.151, 0.129) are
not within 2%, so the search is not converged either. Stills and archives:
`/mnt/project-files/regatta-training/results/pool-search/stills_lead/`, `archive_LC.json`, `archive_LD.json`, `pick_lead.json` (the Manta-at-2.5 run: `archive_LA.json`, `pick_lead_manta25.json`).

## 4. What this does not know

- The roster. The anchors are the 2026-11-03 Regatta Hull Benchmark races of untrained AIs; the Manta model is being
  revised (the nose lead, #1786) and brief 9 (#1782) found the model 1.48x off the Manta and 1.27x off the Rhino in
  grids. Do not freeze a pool on today's Manta.
- Grid traffic, rams, a rail cut by rivals beyond brief 10's guess (`KNOBS`), lifeforms (no model term at all).
- The features in the game: they are #1787's, on its verification checklist.
- Neutral rail as a C# setting comes with #1787 (per section); bleeding-edge lays none.

Real-game checks for later: fly the picked courses of a real (trained-roster) pool in a six-AI grid, alone and with a
person; compare seconds with the model within 10%; no missed mouths.

## 5. Next

1. Re-run `regatta-pool-features` when time-arcade promotes trained AIs (new anchors, and re-solve
   `regatta_measured_roster.py`), with three or more restarts for convergence.
2. Brief 13 (`RegattaCoursePool`) takes the picked entries: each is a settings row, a generator seed and a feature set
   (`pool.json` entries carry all three) plus the paired lifeforms.
