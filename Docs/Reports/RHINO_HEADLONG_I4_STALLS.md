# The Rhino's I4 stalls in Headlong (2026-11-06/07)

Plan step: `time-arcade` (Docs/AI_SYSTEM/AI_PLAN.md). Brief 5 of the Regatta balance plan. **Every number here is a
model** (`Tools/Build/rhino_drive_sim.py`: a point mass, proportional stick, no nose lag, no rivals). None of it has
been raced in the Unity editor.

## The answer: the steering, not the drive

The three earlier runs of `drive-rhino-headlong` agreed at I1-I3 and each stalled on one I4 course seed. Raced again
with a trace, **every drive stalled at the same ring**, the shipped one included (I4 seed 3, ring 2 of 24). So the drive
was not the cause. The legacy model was not Headlong's steering at all: it flew Regatta's 480 / 420 / 320 distances
in every mode and had no orbit break-off.

What Headlong ships (`MinigameHeadlong.unity`, `GateRaceController`): approach lead 300, commit 260, through 220, and
`AICommitFromApproachPoint` **off** (Headlong was the last gate race with it off). Ported line for line into the model
together with `AIPilot.UpdateOrbitBreak`, it fails in two ways, whatever the drive:

1. **After a missed mouth** (I3-I4's 46-58 u rings at 600-800 u/s) the side stays latched, so the far-side "through"
   point is on the pilot's own side. It flies there, overshoots past 260 u, turns for the approach point, comes back
   inside 260 u and turns for the through point again. The orbit detector never fires, because each swap between the
   two points is a range jump of more than 1.6x and resets it; the turning-circle test never fires at 50 u/s (R 29 u).
2. **On a leg over 650 u** the 300 u approach point lies outside the 260 u commit radius and the AI circles it
   (115 break-offs in one I2 race). This is the bug Switchback had (Yash, 2026-10-26).

`AICommitFromApproachPoint` on fixes both (the lead capped at 0.8 x commit, re-latch and break off after a miss).

## Numbers (three laps; mean of seeds 1-3; a DNF is priced at limit + 30 s + 10 s a ring)

| drive | steering | I1 | I2 | I3 | I4 | DNF of 12 |
|---|---|---|---|---|---|---|
| shipped (`RhinoAIBoostPolicy`) | Headlong as shipped | 49.4 | DNF | DNF | DNF | 7 |
| best drive any search found (2002 evals, control) | Headlong as shipped | 29.6 | 39.7 | DNF | DNF | 5 |
| shipped | switch on | 53.9 | 52.9 | 68.1 | 65.0 | 0 |
| **trained B** (3014 evals) | switch on | **36.1** | **34.5** | **38.4** | **44.5** | **0** |
| **trained C** (3003 evals) | switch on | **35.2** | **34.7** | **40.3** | **42.6** | **0** |
| legacy-model best (B, 2026-11-06) | switch on | 56.0 | 60.6 | 50.6 | 49.9 | 0 |

- No drive finishes Headlong I3-I4 on the shipped steering: the control search still had 5 DNFs after 2000 evaluations.
- With the switch on, the two restarts agree on the composite within 0.7% (mean 38.1 vs 37.9 s) and within 2.7% / 0.6% /
  5% / 4.5% per intensity; neither improved in its last 20% of evaluations (last gains at 814 and 1705 of ~3000). Their
  fields differ widely (cornerSpeed 694 vs 1027), so the loss is flat there: the times are settled, the fields are not.
- Against the shipped drive on the same steering the trained drive is 1.5x faster (59.3 -> 38 s composite).
- No human has finished Headlong, so there is no human mark (well-trained test 3 cannot be applied).

## On Regatta's measured I1 course

`regatta_measured_roster.MEASURED_ON` (origin/claude/regatta-overnight-balance), raced with `rhino_line_sim.py` (the
race line, the drive, AIPilot's stick and nose lag; fields not searched come from the Regatta variant):

| drive | model s | scaled to the measured 95 s | missed mouths |
|---|---|---|---|
| shipped `RhinoAIBoostPolicy_Regatta` | 77.4 | 95 (the anchor) | 0 |
| trained B | 94.2 | ~116 | 4 |
| trained C | 102.0 | ~125 | 2 |

The Headlong drive is slower in Regatta and misses mouths there: it should stay a `_Headlong` variant and leave the
Regatta variant alone.

## What is proposed (nothing written to an asset)

1. `HeadlongController.AICommitFromApproachPoint => true` (in the PR, a separate commit; Yash's call, after a playtest).
2. Trained drive B or C as `RhinoAIBoostPolicy_Headlong`, written only with `learn_drives_from_flights.py`'s writer if
   Yash agrees, and only together with (1): on the shipped steering it does not help.

## Not verified (needs the editor)

- A four-Rhino Headlong race at I3 and I4 with the switch on: no ring missed and left, and the model within 10%.
- The model has no nose lag and a proportional stick; the real AI has both (`rhino_line_sim` has them for Regatta).
- The EditMode test `Headlong_FliesTheRegattaSteering_WhateverTheSceneSays` compiled (unity_refcompile) but has not run.

Results: `/mnt/project-files/regatta-training/results/rhino-i4-stalls/`. Reproduce: `python3 Tools/Build/rhino_drive_sim.py --headlong`
and `--self-test`; the searches are `drive-rhino-headlong-on` (B, C) and `drive-rhino-headlong-shipped` (A).

## With the nose lead on (2026-11-07)

Brief 21, the lead-on twin `drive-rhino-headlong-on-lead` (the parent's search, seeds and space, with `nose_rate` 1.5 =
the fleet AI nose lag and `nose_lead` 2.5 = `AIPilot.NoseLeadGain` from PR #1786, which ships at 0). **Every number is
the `rhino_drive` model with Headlong's twelve courses (I1-I4 x seeds 1-3) and the gate steering switched on; no
editor race.** Composite = the geometric mean of the twelve race times (the job's loss, exp'd).

**The lag-free assumption did not hold.** The parent's numbers came from a model with no nose lag. Turning on the lag
every AI hull really has (1.5/s, `VesselTransformer.LERP_AMOUNT`) makes the parent's winner 4-5x slower, with about
100 missed mouths over the twelve races. The Rhino "never missed" only because the model had no lag. The lead alone
does not fix it on Headlong: the parent's winner with lag and lead still misses 115-132 mouths. The drive has to change
too. Re-searched with lag and lead, the drive corners much slower (`cornerSpeed` about 155 against 694-1027) and
finishes the twelve in 88.7 / 90.3 s composite with 10 / 12 missed mouths.

The comparison on the same twelve courses ("lead only", without the lag, is identical to "off" by design: the
effective gain is 0 when the nose has no lag):

| drive | nose | composite s | I1 / I2 / I3 / I4 s | missed mouths I1 / I2 / I3 / I4 | DNF |
|---|---|---|---|---|---|
| job init (shipped-like) | off | 59.3 | 53.87 / 52.93 / 68.1 / 64.97 | 0 / 0 / 0 / 0 (0) | 0 |
| job init (shipped-like) | lag only | 164.3 | 107.13 / 135.0 / 227.4 / 226.6 | 0 / 12 / 45 / 47 (104) | 0 |
| job init (shipped-like) | lag + lead | 157.3 | 111.2 / 115.57 / 211.17 / 237.7 | 5 / 11 / 49 / 55 (120) | 0 |
| parent B | off | 38.1 | 36.13 / 34.53 / 38.37 / 44.53 | 0 / 0 / 1 / 1 (2) | 0 |
| parent B | lag only | 179.6 | 130.07 / 151.6 / 244.57 / 224.17 | 0 / 13 / 45 / 44 (102) | 0 |
| parent B | lag + lead | 149.5 | 102.9 / 124.97 / 186.5 / 212.1 | 6 / 16 / 39 / 54 (115) | 0 |
| parent C | off | 37.9 | 35.17 / 34.73 / 40.33 / 42.57 | 0 / 0 / 1 / 0 (1) | 0 |
| parent C | lag only | 168.0 | 113.77 / 135.5 / 243.07 / 215.33 | 2 / 10 / 46 / 40 (98) | 0 |
| parent C | lag + lead | 169.5 | 111.03 / 154.37 / 206.17 / 236.0 | 7 / 25 / 44 / 56 (132) | 0 |
| lead-on B | off | 59.5 | 56.13 / 55.2 / 61.53 / 66.43 | 0 / 0 / 0 / 2 (2) | 0 |
| lead-on B | lag only | 138.9 | 97.53 / 106.53 / 182.03 / 200.3 | 0 / 4 / 32 / 38 (74) | 0 |
| lead-on B | lag + lead | 88.7 | 81.37 / 80.63 / 102.4 / 93.27 | 0 / 0 / 6 / 4 (10) | 0 |
| lead-on C | off | 64.3 | 62.57 / 61.6 / 64.53 / 69.3 | 0 / 0 / 0 / 3 (3) | 0 |
| lead-on C | lag only | 133.2 | 100.6 / 105.5 / 167.03 / 179.63 | 0 / 0 / 28 / 33 (61) | 0 |
| lead-on C | lag + lead | 90.3 | 78.5 / 81.5 / 98.53 / 107.47 | 0 / 0 / 2 / 10 (12) | 0 |

- **Converged:** restarts B and C, 2,717 evaluations each, composites 88.7 / 90.3 s (1.8% apart; losses 4.4856 /
  4.5036); neither improved in its last 20% (last gains at 1,507 and 1,639). The fields differ (minCornerSpeed 144 vs
  215, releaseRunwaySeconds 0.16 vs 0.98), so the loss is flat there as in the parent.
- **The lead pays only together with a re-searched drive:** lead-on winner, lag + lead 88.7 s against the parent's
  winner, lag + lead 149.5-169.5 s. Without the lead the lead-on winner is 133-139 s, so it is tuned for the lead.
- **Still not clean:** 10-12 missed mouths over twelve races, all at I3/I4; the well-trained test asks for none.
- **Regatta's measured I1 course** (`rhino_line_sim`, lag on, `regI1_lead.py` in the results folder): with the lead
  2.5 the shipped `RhinoAIBoostPolicy_Regatta` races 37.9 s (77.4 s without the lead, 0 misses either way), the
  lead-on B / C Headlong drives 52.5 / 43.9 s (1 / 0 misses), the parent B / C 58.8 / 45.8 s. The Headlong drive
  still belongs in a `_Headlong` variant and leaves the Regatta variant alone.
- No human has finished Headlong, so well-trained test 3 cannot be applied.

**Proposed (nothing written to an asset):** for the day Yash turns `AIPilot.NoseLeadGain` on, lead-on drive B
(fields in `drive-rhino-headlong-on-lead__B.json`) as `RhinoAIBoostPolicy_Headlong`, together with
`AICommitFromApproachPoint` on, and only through `learn_drives_from_flights.py`'s writer. Until the lead is on, the
parent's drive is the proposal, but its published 38 s ignores the lag: the editor check is what says which is right.

**Not verified (needs the editor):** a four-Rhino Headlong race at I3 and I4 with the lead at 0 and at 2.5, counting
missed mouths, against these model times (within 10%).

Results: `/mnt/project-files/regatta-training/results/rhino-lead/` (run JSONs, `cells_*.json`, `TWO_BY_TWO.md`).
