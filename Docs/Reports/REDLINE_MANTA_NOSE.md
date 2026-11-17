# The Manta's AI nose lag in Redline: the case for Yash (AI_PLAN.md decision 7)

Plan step: `time-arcade`. Asked for by Garrett (2026-11-06, the Regatta balancing plan, brief 6). The question is Yash's
(decision 7, 2026-11-03): is an AI's nose follow rate part of its genome, or a difficulty setting like Switchback's
`aiNoseFollowByIntensity`? **Every number below is the offline model** (`Tools/Build/manta_redline_sim.py`: one AI,
no rivals, no lobby handicap, so it is the Legendary AI; `manta_redline_sim.LIMITS`). Nothing here changes an asset.

## The answer in one line

**The nose rate is a pure handicap, not a genome trait.** At every intensity the AI Manta gets faster as its nose
follows faster, with no rate where it starts overshooting the mouths, and the search walks to the top of its range at
every intensity. A rate of 9/s makes the AI **3x better on the composite** (22.2 to 7.3 on 12 fresh courses an
intensity) and puts it at 97-115 s. That is Redline's ~90 s design target (the 2026-10-26 pass). It also brings it close
to what a person flies: a human's nose has no lag. No Soar band comes near that. So the proposal is Switchback's shape:
**a nose-rate row per intensity and difficulty on `RedlineController`**, with Legendary at about 9/s.

## 1. The sweep: the best Soar bands at each nose rate (6 courses an intensity, seeds 1-6)

`python3 Tools/Build/manta_redline_nose_sweep.py --seeds 6 --seedbase 1` races 36 band pairs (BoostStickBand
0-1 x YawsteryStickBand 0.5-1, plus the shipped 0.35 / 0.75 and the Regatta genome 0.975 / 0.975) at seven nose rates.
Each cell shows the median of 6 races, how many finished, and the missed mouths summed over the 6. "Best" is the lowest
`redline_fit` score.

| nose rate /s | I1 shipped | I2 shipped | I3 shipped | I4 shipped | I1 best | I2 best | I3 best | I4 best |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| **1.5 (fleet, today)** | 182 s (6/6, 0) | 176 s (6/6, 0) | 183 s (6/6, 21) | **482 s (6/6, 320)** | 168 s @ .975/.975 | 170 s @ .75/.5 | 183 s @ .1/.85 | 270 s @ 0/.85 (75) |
| 2 | 156 s (0) | 163 s (0) | 171 s (17) | 272 s (99) | 153 s @ .975/.975 | 156 s @ .975/.975 | 160 s @ .2/.85 | 197 s @ 0/.5 (3) |
| 3 | 134 s (0) | 149 s (6) | 156 s (3) | 160 s (13) | 135 s @ .75/.5 | 142 s @ .75/.85 | 151 s @ .35/.65 | 160 s @ .5/.65 |
| 4 | 128 s (0) | 134 s (4) | 143 s (6) | 154 s (30) | 121 s @ .75/.65 | 129 s @ .2/1 | 136 s @ .75/1 | 149 s @ .75/.5 |
| 6 | 116 s (0) | 112 s (0) | 118 s (6) | 125 s (28) | 116 s @ .5/.65 | 112 s @ .35/.75 | 114 s @ .1/.75 | 112 s @ .975/.975 |
| 9 | 111 s (0) | 110 s (0) | 114 s (6) | 100 s (11) | 111 s @ .35/.75 | 106 s @ .1/.5 | 108 s @ .5/.65 | 99 s @ .5/.65 (4) |
| none (a human's nose) | 111 s (0) | 102 s (1) | 106 s (15) | 97 s (13) | 105 s @ .75/.75 | 95 s @ 1/.5 (13) | 97 s @ 1/.5 (26) | 86 s @ 1/.5 (24) |

All 1,512 races finished. What it shows:
- **The time falls with the rate at every intensity, and never turns back up.** At the fleet lag I4 is the slowest
  level by 2.6x. By 3/s the four levels are within 26 s of each other, and by 9/s I4 is the fastest, as a person
  would fly it.
- **Missed mouths fall with it.** At I4 they drop from 320 (fleet) to 11 at 9/s. With no lag at all they rise again
  (13-26): the instant nose takes the line too fast for the 44 u mouths. That is why 9/s, not "none", is the top of
  the search range.
- **The best bands depend on the rate.** At the fleet lag I4 wants almost no Soar (0 / 0.85), because Soar makes the
  slow nose overshoot. At 6-9/s it wants mid bands. So the bands `redline-manta-time` is searching at 1.5/s are tuned to
  a nose the AI should not keep.

## 2. The search with the rate in the genome: `redline-manta-nose` (two restarts)

New job `Docs/Compute/jobs/redline-manta-nose.json` (`"status": "proposed"`, run by hand while Compute is paused),
evaluator `Tools/Compute/evaluators/redline_nose_fit.py`. It is redline_fit's races and composite, run with a
NoseFollowI1..I4 rate in [1.5, 9] beside the two bands. A small `rate_cost` (0.02) pulls the rate toward the fleet lag,
so if two rates race the same, the smaller one wins. 4 courses an intensity (seeds 1-4), 405 evaluations each,
`local_run.py`.

| restart | loss | bands | nose I1 / I2 / I3 / I4 | last improvement |
|---|---:|---|---|---|
| B | 7.029 | 0.597 / 0.620 | 8.32 / 8.52 / 8.68 / 8.73 | eval 288 of 405 |
| C | 6.986 | 0.706 / 0.201 | 8.14 / 7.83 / 8.16 / 8.84 | eval 252 of 405 |
| shipped (0.35 / 0.75, 1.5) | 23.001 | | | |

- **The restarts agree on the loss within 0.6%**, and neither improved in its last 20%. Both put every intensity's
  rate at 7.8-8.8: at the top of the range, held just under it by the rate cost. Nothing in the search found an
  interior best rate.
- **They disagree on the bands** (0.60 / 0.62 and 0.71 / 0.20). Once the nose is fast, the bands are a flat lever.

## 3. On FRESH courses (12 an intensity, seedbase 1001: the promotion test)

| candidate | composite | I1 | I2 | I3 | I4 |
|---|---:|---:|---:|---:|---:|
| **shipped bands, fleet lag 1.5 (today)** | 22.19 | 179 s (12/12, 0 miss) | 178 s (0) | 196 s (55) | **539 s (605)** |
| Regatta genome 0.975 / 0.975, fleet lag | 26.62 | 169 s (0) | 175 s (6) | 200 s (58) | 625 s (788) |
| restart B's bands, fleet lag | 26.09 | 175 s (0) | 175 s (1) | 219 s (98) | 590 s (716) |
| restart C's bands, fleet lag | 24.35 | 176 s (0) | 176 s (3) | 198 s (62) | 601 s (763) |
| shipped bands, nose 3 | 9.99 | 134 s (0) | 147 s (6) | 159 s (33) | 164 s (36) |
| shipped bands, nose 6 | 7.66 | 117 s (0) | 119 s (1) | 116 s (6) | 116 s (21) |
| **shipped bands, nose 9** | **7.29** | 115 s (0) | 109 s (3) | 104 s (4) | **97 s (7)** |
| restart B (bands + rates) | 7.29 | 115 s (0) | 114 s (4) | 108 s (6) | 99 s (17) |
| restart C (bands + rates) | 7.40 | 112 s (0) | 116 s (1) | 112 s (15) | 100 s (20) |

Every candidate finished 48 of 48. **The searched bands do not beat the shipped bands on fresh courses**, at either
nose rate, so no band change is proposed. All of the gain is the nose rate.

## 4. The proposal (Yash decides; nothing is written)

1. **Treat the AI nose rate as a difficulty setting, not a genome field.** It is a handicap with no best value below
   the top, which is Switchback's precedent. A genome field would always be searched to the ceiling.
2. **Give Redline the same knob Switchback has**: `RedlineController` sets `VesselTransformer.AINoseFollowPerSecond` in
   `OnRacerArmed` from a per-intensity row of `SwitchbackNoseFollow`'s shape (Easy / Medium / Hard / Legendary). It is
   a mode setting on the AI's skill, like Switchback's. It does not change the vessel's kit (the Manta's own code and
   assets are untouched). A starting row from the sweep (Legendary only is modelled; Easy-Hard also fly
   `GateRaceHandicap`'s late belief, so their times will be slower than these):

   | | Easy | Medium | Hard | Legendary |
   |---|---:|---:|---:|---:|
   | I1-I4 | 2 | 3 | 6 | 9 |
   | modelled Legendary-skill time at that rate, I1 / I4 | 156 / 272 s | 134 / 160 s | 116 / 125 s | 111 / 100 s |

   Yash's Switchback bands (10 s a level, 20 s wide) would set the final numbers per intensity once a person has
   finished Redline. Nobody has yet, so there is no human mark.
3. **Re-search the bands at the rate the AI will fly.** `redline-manta-time` searches the bands at 1.5/s, where the best
   I4 band (0 / 0.85) is the opposite of the best at 6-9/s. Its result should not be promoted until the nose decision is
   made. Once it is, run it again with `rate_hi` fixed (the job takes the rate as the space's bound) or with
   `redline-manta-nose`'s winner.
4. **Regatta**: the course search races the Manta's Time AI. With the fleet lag, its I4 times are 5x too slow. The
   course step (`time-arena-course`) should wait for this decision too.

## Relation to #1786 (the pilot's nose lead)

Thread 7 (PR #1786, `results/scarab-steering/fleet_nose_lead.txt`) attacked the same lag from the pilot's side, with its
own fleet race model. Its absolute times are not this report's. A pilot nose lead (`AIPilot.NoseLeadGain` 2.5, off by
default) takes the Manta from 705 s to 314 s summed over I1-I4 on the fleet 1.5/s nose. That compares with 255 s at
this report's 9/s nose with no lead, and gains above 2.5 close no more. So the lead closes about 80% of the gap and
leaves the vessel's nose rate untouched. This report's per-intensity nose row is the **second step**, for the I3-I4
remainder (the hull's 83 deg/s turn through any lag), not an alternative. Read the two PRs as one proposal: the lead
first, then the row.

## Not verified (the cloud cannot run these)

- In the editor: AI Mantas race Redline at I1-I4 with `AINoseFollowPerSecond` set to 3 / 6 / 9, alone and in a
  six-AI grid. Check that no ring mouth is missed and that each time is within 10% of the model.
- The model has no rivals, no Yawstery trails and no lobby handicap. Easy-Hard times will be slower.
- There is no human Redline finish to compare against (brief rule 10).

## Reproduce

    python3 Tools/Build/manta_redline_nose_sweep.py --seeds 6 --seedbase 1 --out sweep.json      # section 1 (~10 min, 4 cores)
    RUN_DIR=$HOME/rt python3 /mnt/project-files/regatta-training/local_run.py redline-manta-nose B 400 2   # section 2 (~6 min)

Results: `/mnt/project-files/regatta-training/results/manta-nose/` (sweep, both restarts, the fresh-course table).

## With the nose lead on (2026-11-07)

Brief 15 of the lead-on round (`redline-manta-time-lead`, the twin of `redline-manta-time` with `"nose_lead": 2.5`;
`Plan step: time-arcade`). Everything here is the Redline MODEL (`manta_redline_sim.py`, one AI, no rivals, Legendary);
no editor run. Composite = `redline_fit`'s loss (mean of the four intensity scores + 0.5 x the worst); "m" = missed ring
mouths summed over the courses; times are medians of finished races.

**The answer: on the Manta the lead does not remove the I4 misses; it traps the hull. Keep `NoseLeadGain` at 0 on the
Manta and take this report's nose row (section 4) instead.** It also corrects the "Relation to #1786" section above:
that 80% came from thread 7's fleet model, which has no roll stick; in Redline's model, which flies AIPilot's roll rule,
the lead closes none of the gap and makes the 9/s row worse.

### The 2x2 (this job's 4 courses an intensity, seedbase 1)

| bands | lead | composite | I1 | I2 | I3 | I4 |
|---|---|---:|---|---|---|---|
| shipped 0.35 / 0.75 | off | 23.00 | 4/4 189 s m0 | 4/4 175 s m0 | 4/4 183 s m8 | 4/4 482 s m199 |
| shipped 0.35 / 0.75 | 2.5 | 58.59 | **1/4** 127 s m3 | **0/4** | **0/4** | **0/4** |
| parent winner C 0.0200 / 0.8995 | off | 15.97 | 4/4 190 s m0 | 4/4 173 s m1 | 4/4 185 s m5 | 4/4 341 s m106 |
| parent winner C 0.0200 / 0.8995 | 2.5 | 51.41 | 4/4 136 s m0 | **0/4** | **0/4** | **0/4** |

(The parent's winner is raced at its exact bands: rounded to 0.020 / 0.900 its I4 drifts to 513 s, so the band
landscape at I4 is rough at the fleet lag.) With the lead, I1 gets faster where it finishes (127-138 s against 188 s: judging
the error from the command lets the nose hold the straight stick the Soar wants), and from I2 up no race finishes in
900 s. The misses fall only because the hull never reaches the rings: it corkscrews away (the sim's docstring, check 11:
AIPilot's roll stick is its yaw stick, and with the lead it swings a command 40-90 deg from the nose round the nose's own
forward axis).

### What the bands do once the lead is on: the twin search

Two independent restarts by hand (`local_run.py redline-manta-time-lead B|C 540 2`, 300 + 240 evals, 75-78 min each on
2 cloud cores):

| run | evals | loss | BoostStickBand | YawsteryStickBand | I1 | I2 | I3 | I4 |
|---|---:|---:|---:|---:|---|---|---|---|
| B | 540 | 47.45 | 0.006 | 0.939 | 4/4 140 s m0 | 2/4 156 s m13 | 0/4 m52 | 0/4 m64 |
| C | 546 | 48.61 | 0.173 | 0.834 | 4/4 140 s m0 | 1/4 113 s m8 | 0/4 m26 | 0/4 m38 |
| shipped, lead on | | 58.59 | 0.35 | 0.75 | 1/4 | 0/4 | 0/4 | 0/4 |
| parent winner, lead OFF | | 15.97 | 0.020 | 0.900 | 4/4 190 s m0 | 4/4 173 s m1 | 4/4 185 s m5 | 4/4 341 s m106 |

No band pair finishes a single I3 or I4 race with the lead on: the restarts land 2.4% apart on a floor that is the
not-finished penalty, and C was still inching down at eval 450 of 546, so by rule 11 it is not converged, and it does
not matter: the best lead-on drive (47.5) is three times worse than the lead-off winner (16.0). The bands only buy I1
(both restarts agree on the parent's direction: Boost near 0, Yawstery 0.83-0.94, and I1 finishes in 140 s against
190 s). **Nothing here is a proposal; the twin's `promote` rule cannot be met.**

### Is it the roll rule? What-ifs on AIPilot's roll while leading (`lead_roll`, new, model only)

`manta_redline_sim.race(lead_roll=...)` and `redline_fit`'s `lead_roll` arg: `"judged"` (default, the game), `"nose"`
(the roll stick judged from the nose, as with the lead off) or `"off"` (no roll stick while leading). Absent, every number
is unchanged (self-test check 12).

| bands | lead, roll rule | composite | I1 | I2 | I3 | I4 |
|---|---|---:|---|---|---|---|
| shipped | 2.5, roll judged (game) | 58.59 | 1/4 | 0/4 | 0/4 | 0/4 |
| shipped | 2.5, roll from nose | 42.61 | 3/4 135 s | 4/4 687 s m20 | 4/4 690 s m89 | 4/4 779 s m217 |
| shipped | 2.5, no roll | 21.54 | 4/4 136 s m0 | 4/4 181 s m8 | 4/4 304 s m24 | 4/4 533 s m87 |
| parent C | 2.5, roll from nose | 34.32 | 4/4 138 s | 4/4 841 s m30 | 4/4 646 s m52 | 4/4 704 s m184 |
| parent C | 2.5, no roll | 24.60 | 4/4 155 s m0 | 4/4 148 s m5 | 4/4 298 s m20 | 4/4 524 s m71 |

Taking the roll stick away while leading un-traps the hull and halves the I4 misses, but the composite only matches the
lead-off shipped drive (21.5 against 23.0) and is worse than the parent's winner without it (24.6 against 16.0): I1
gains 35-50 s, I3 loses 110-120 s, I4 does not improve. Even a changed roll rule would not make
the lead the Manta's fix.

### Against #1780's nose row (12 FRESH courses an intensity, seedbase 1001, shipped bands: section 3's test)

| nose | lead | composite | I1 | I2 | I3 | I4 |
|---|---|---:|---|---|---|---|
| fleet 1.5/s (today) | off | 22.19 | 12/12 179 s m0 | 12/12 178 s m0 | 12/12 196 s m55 | 12/12 539 s m605 |
| fleet 1.5/s | 2.5 | 58.78 | 6/12 125 s | 0/12 | 0/12 | 0/12 |
| fleet 1.5/s | 2.5, no roll | 23.34 | 12/12 130 s m1 | 12/12 219 s m57 | 12/12 302 s m110 | 12/12 453 s m312 |
| **9/s (#1780)** | **off** | **7.29** | 12/12 115 s m0 | 12/12 109 s m3 | 12/12 104 s m4 | 12/12 97 s m7 |
| 9/s | 2.5 (effective gain 1) | 10.24 | 12/12 122 s m0 | 12/12 169 s m51 | 12/12 164 s m91 | 12/12 156 s m111 |
| 9/s | 2.5, no roll | 9.59 | 12/12 120 s m1 | 12/12 149 s m48 | 12/12 160 s m97 | 12/12 148 s m109 |

The 9/s row with no lead reproduces section 3's 7.29 exactly. Adding the lead on top of it costs 40% (7.29 to 10.24)
and 15x the I4 misses. **For the Manta, #1780's row is the whole fix and the lead should stay off**; read #1786 and
#1780 as two proposals for different hulls, not as "the lead first, then the row" (that order holds for the Scarab,
Serpent and Dolphin, whose models showed the lead helping; brief 16-21 threads say for theirs).

### Not verified

- Every number is the model. The editor check that decides it is the one in section "Not verified" above, plus: AI
  Mantas in Redline with `NoseLeadGain` 2.5 on the fleet nose (expect the corkscrew from I2 up if the model is right).
- `lead_roll` models a change to AIPilot nobody has proposed; it is a diagnostic, not a proposal.

Results: `/mnt/project-files/regatta-training/results/manta-lead/` (every cell's JSON, `cell.py` that raced them, the
twin's two restarts).
