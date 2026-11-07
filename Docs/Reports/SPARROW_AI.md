# The Sparrow's AI: Regatta's "rail rams", and its Breakwater training (2026-11-07)

Brief 4 of the Regatta balancing plan (`Plan step: time-arcade`). Every number below is from an offline MODEL unless it
says "measured"; the editor is the measurement. Nothing here writes an asset: the trained drive is a PROPOSAL for Yash.

## 1. Why the AI Sparrow is Regatta's slowest hull (measured)

The brief's premise was "its AI rams the rails and stops". The recordings say otherwise.

- **Garrett's 59 Regatta Sparrow flights are not AI Sparrow races.** They are the Regatta Hull Benchmark of
  2026-11-02/03: Garrett's Sparrow is parked (35 u/s, 1 ring) while the hull being benchmarked races as the rival.
- **The AI Sparrow raced in 16 stored flights** (`Tools/Build/sparrow_regatta_sim.py --measure`): 11 on 2026-10-16/17
  (I3, before the race line) and 5 on 2026-11-03 (I1, the line, state and velocity recorded at 10 Hz).
- **Rail rams are small since the line.** Within 15 u of a rail: 26-48% of a race before the line, 0-3% after.
  Slowed against a rail 0-5 times a race, costing 0.4-6.9 s of a 58-150 s race (median about 1.4 s). Four of the five
  were once at the START, beside ring 0. That start-line ram happens to most hulls (Manta, Stoat, Dolphin, Squirrel: 1-5
  s into the race in 2026-11-03..06 grids): it belongs to Regatta's start, not to the Sparrow (handed to brief 9).
- **The real loss is the afterburner.** The Sparrow cruises at **35 u/s** and its afterburner reaches **197.5 u/s** at
  the card's Time 10 (least squares on the 5 races: targets 34-35 and 196-197, easing 1.39-1.44 / s; Sparrow.prefab
  MinimumSpeed 10 + DefaultThrottleScaler 25, boost x5 x Time's 1.5). The AI presses it and lets go after exactly
  0.6 s (`minHoldSeconds`), rests 0.4-0.6 s, presses again: 93 presses in the 150 s benchmark race, boosted 41-55% of
  every race, mean speed about 110 u/s. The base `SparrowAIBoostPolicy` lets go when the stick passes 0.6, and on the
  race line AIPilot's stick law (0.6 at 10 deg of error) passes it on every bend of the braid.
- **A Sparrow flown with the afterburner held** (one stored I1 flight, 2026-10-22): 63.9 s for three laps at a constant
  197.5 u/s, never within 15 u of a rail.

## 2. The Regatta model (`Tools/Build/sparrow_regatta_sim.py`)

`regatta_line_sim`'s race line and steering plus the measured speed, `HoldBoostAIPolicySO`'s hold line for line and
rail contact (`SparrowVesselChangeSpeedByPrism`). Shipped course, alone, seconds for three laps:

| Drive | I1 | I2 | I3 | I4 |
|---|---:|---:|---:|---:|
| shipped base drive (what Regatta flies) | 113.8 | 145.8 | 166.4 | 178.3 |
| `SparrowAIBoostPolicy_Breakwater` | 126.3 | 144.8 | 150.2 | 159.5 |
| the trained drive (section 3): the afterburner held | 74.3 | 85.3 | 113.9 (3 misses) | 115.1 (3 misses) |

The model's shipped I1 (113.8 s, 53 presses, 56% boosted) sits a little under the benchmark's measured 117-150 s (93
presses, 41-55%). Scaled by the measured anchor (`regatta_measured_roster.py`'s 133 s), the trained drive's I1 is about
**87 s**: from 2x the field to mid-pack (Manta 67, Squirrel 75, Rhino 95, Dolphin 97.5, Serpent 100 s measured). At
I3/I4 (72 / 54 u mouths) the held afterburner rams the rails 46-66 times a race and misses 3 mouths, and is still 32-35%
faster; a corner-aware let-go is the next thing to try there, after the editor shows how the real hull behaves.

## 3. Breakwater: the universal Time drive (`breakwater-sparrow-time`)

Model `Tools/Build/breakwater_sparrow_sim.py` (the course mirror `breakwater_arena.generate`, 29 crossings, the
Regatta steering Breakwater flies, measured hull: cruise 35, afterburner 135 u/s; the station's eye, Plain collar
x0.5 / 1 s, Danger weave x0 / 3 s, the rocket's 50 u hole). Evaluator `Tools/Compute/evaluators/breakwater_fit.py`
(VERSION 2; HoldBoostAIPolicySO's 12 fields; loss ln(mean + 0.5 x worst intensity); seeded from both shipped genomes).
Run by hand with `local_run.py` (Compute is paused), 1500 evaluations a restart, 3 courses an intensity.

- **Version 1, restarts B and C**: both settled within 0.1% (5.6915 / 5.6903) on a drive that lets go 1.4 s before each
  station and holds at least 2.4 s. But holding the afterburner all race scored 5.613 on the same courses: both
  restarts sat in the same local minimum, unable to cross the extra weave clips between the genomes and that point.
- **Version 2, restarts D and E**: the held-afterburner point joins the first generation (`candidates: ["held"]`). Both
  restarts returned it unchanged (5.6082); nothing improved on it in 1500 evaluations each. It is a corner of the space
  (align 90, release align 120, stick bands 1, engage and release runway 0); the rocket keeps the variant's numbers.

Promotion check on 8 FRESH courses per intensity (seeds 99000-99007), seconds for 29 crossings, all finished:

| Drive | I1 | I2 | I3 | I4 | composite | Danger clips a race |
|---|---:|---:|---:|---:|---:|---:|
| base `SparrowAIBoostPolicy` | 236.4 | 247.6 | 252.6 | 255.6 | 578.0 | 0 |
| `SparrowAIBoostPolicy_Breakwater` (what the game flies) | 160.8 | 161.9 | 161.0 | 157.8 | 364.1 | 0 |
| version 1 local minimum | 137.0 | 145.2 | 144.1 | 142.0 | 332.1 | 4-5 |
| **trained: the afterburner held** | **115.4** | **120.8** | **123.0** | **123.5** | **278.5** | 13-22 |
| best human finished race (measured) | 83.8 | - | 127.8 | 89.3 | | |

Against the plan's four tests: (1) the v2 restarts agree exactly and never improved, but on a seeded corner, so
"converged" is weak; (2) it beats both genomes on fresh courses at every intensity; (3) within 15% of the human at I3
(0.96x) but not at I1 or I4 (1.38x): people hold the afterburner too (80-99% of a race at 135 u/s) and thread the
eye cleanly, so the gap is the AI's STEERING into the eye at speed, not its drive; (4) the real-game test is not run.

**What the model may get wrong.** The trained drive clips the Danger weave 13-22 times a race and wins anyway, because
a clip costs about 1.5 s here (x0, easing back over 3 s). Not modelled: the danger prism's element debuff (-0.1 to
-0.5 of a level for 4 s), the 60 deg roll, rivals, the shoals and the dish. If a clip costs more in the game, the
version 1 drive (let go 1.4 s before a station) is the fallback. The editor decides.

## 4. Proposals (for Yash; nothing written)

1. The Sparrow's universal Time drive: hold the afterburner (alignDegrees 90, releaseAlignDegrees 120, stickBand 1,
   releaseStickBand 1, engageRunwaySeconds 0, releaseRunwaySeconds 0), the rocket as `SparrowAIBoostPolicy_Breakwater`
   has it. Written with `learn_drives_from_flights.py`'s writer into the base `SparrowAIBoostPolicy` (every mode, and so
   Regatta) and the Breakwater variant, only on Yash's yes. Note `learn_drives_from_flights.FLOORS` holds Breakwater's
   release at 1.25 s or more; the trained drive goes under it, which is the editor's call.
2. Editor checks (`Docs/UNITY_VERIFICATION_CHECKLIST.md` when promoted): Breakwater I1-I4 alone (time, weave clips),
   and the Regatta Hull Benchmark's Sparrow at I1 against its 117-150 s.
3. Not done: the AI's line into the eye at speed (the I1/I4 human gap), a corner-aware let-go for Regatta I3/I4, the
   start-line rail ram (brief 9).

Results: `/mnt/project-files/regatta-training/results/sparrow-breakwater/`.
