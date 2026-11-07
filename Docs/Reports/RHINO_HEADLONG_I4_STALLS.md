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
