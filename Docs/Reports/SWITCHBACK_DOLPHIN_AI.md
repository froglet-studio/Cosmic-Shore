# The Dolphin's Switchback AI (switchback-dolphin-time), model only

Yash owns this AI (vessel AI, the Switchback AI and its simulator). The parent search below was run by the Regatta plan's hub thread
(2026-11-06/07, results in `/mnt/project-files/regatta-training/results/planning/DOLPHIN_SWITCHBACK.md`, copied here so the
repo holds it); the lead-on section was added by brief 16. Nothing is promoted.

## The parent search (lead off)

Job `switchback-dolphin-time` (`switchback_fit`), run in two cloud threads, 1,200 evals each, 16 courses per intensity.

| run | loss | best found at | I1 s (finished) | I2 | I3 | I4 |
|---|---:|---:|---|---|---|---|
| B | 1.7266 | 580 | 72.3 (16/16) | 109.8 (16/16) | 109.4 (16/16) | 162.5 (16/16) |
| C | 1.7410 | 700 | 78.0 (16/16) | 109.1 (16/16) | 107.8 (16/16) | 160.6 (16/16) |
| shipped | 1.9362 | | | | | |

Genomes (B / C): alignDegrees 53.406 / 58.853; engageRunwaySeconds 0.653 / 0.597; spendAtCharge01 0.596 / 0.607; peakMarginUnits 7.692 / 6.767; minDischargeSeconds 0.199 / 0.068; minPulseGainUnits 12.498 / 54.782; maxHoldOffSeconds 6.279 / 3.811

The four-part "well trained" test:
1. Restarts: B and C 0.8% apart, both found their best before half their evals and never gained after: PASS.
2. Beats every genome: 1.727 vs the shipped 1.936 (10.8% better): PASS.
3. Within 15% of the best finished human: the ledger (66 Switchback flights on origin/bugledger/*) has finished human races
   at I1 (7: best 58.0 s, median 69.5), I2 (12: best 79.8, median 151.4), I4 (4: best 105.6, median 183.4) and none at I3.
   Against the BEST human the model's medians are 1.34x (I1), 1.37x (I2) and 1.52x (I4): FAIL on the rule as written; against
   the median human it is 1.12x, 0.72x and 0.88x. Two caveats before this counts: the flights race 20 or 26 rings and the
   model's ring count must be matched to the mark; and the model carries the nose lag with no lead, and thread 7's fleet model
   (PR #1786) takes the Dolphin from 926 s to 545 s over I1-I4 with the lead on, so the human gap is plausibly the lag again.
4. Real game: not run.

Verdict: trained as far as the bands can go at today's lag; not "well trained" until the nose lead is decided and the ring
count is matched to the human marks. Nothing promoted; the genome is a proposal for Yash (nothing written to assets).

## With the nose lead on (2026-11-07)

Brief 16 of the Regatta plan (`Plan step: time-arcade`): the lead-on twin `switchback-dolphin-time-lead` (the parent's
search, seeds and genomes with `nose_lead` 2.5, the model of `AIPilot.NoseLeadGain` from PR #1786, which ships at 0),
run by hand in two cloud restarts, 1,400 evals each (the job's `min_evals` + `window_evals`). **Everything here is the
offline Blast Run model** (`switchback_sim_harness/Sim.cs`), not the game: real races ran about 1.6x the model's times
in the 2026-11-02 soak, so read the ranking, not the seconds. The harness already flies each level's nose-lag row,
the I4 gauntlet's `aiNoseFollowGauntlet` included; the lead is added on top of those rows (its gain is scaled per row
by `EffectiveNoseLeadGain`, never under 1).

**The two restarts.** B 1.6764 (genome `_I4`), C 1.6732 (genome `_I1`), 0.2% apart; both finish 16/16 at every
intensity. Both still found small gains in the last 20% (B 0.17%, C 0.5%), so convergence is marginal, like the parent's.
Genomes (B / C): alignDegrees 67.23 / 75.03; engageRunwaySeconds 0.643 / 0.631; spendAtCharge01 0.669 / 0.662;
peakMarginUnits 8.74 / 7.58; minDischargeSeconds 0.167 / 0.147; minPulseGainUnits 3.31 / 12.57; maxHoldOffSeconds
5.08 / 4.25. Against the lead-off winners (alignDegrees 53-59, spendAtCharge01 0.60-0.61) the lead lets the drive
spend its charge at a wider angle off the line, later in the charge.

**The 2x2** (composite, lower is better; misses = ring mouths missed over all races of that intensity, 16 races on the
job's courses, 52 on fresh courses = 12 seeds from 99000 + the tester seed, x 4 levels):

| drive | lead | job courses | I1 / I2 / I3 / I4 misses | fresh courses | I1 / I2 / I3 / I4 misses |
|---|---|---:|---|---:|---|
| shipped mode-wide | off | 1.936 | 7 / 9 / 4 / 26 | 1.952 | 22 / 18 / 17 / 94 |
| shipped mode-wide | on | 1.883 | 12 / 4 / 3 / 24 | 1.894 | 30 / 12 / 10 / 63 |
| parent winner (B) | off | 1.726 | 1 / 0 / 0 / 8 | 1.776 | 5 / 0 / 0 / 23 |
| parent winner (B) | on | 1.734 | 0 / 0 / 0 / 4 | 1.742 | 2 / 0 / 0 / 16 |
| lead-on winner (C) | off | 1.781 | 1 / 0 / 0 / 10 | 1.772 | 2 / 1 / 0 / 28 |
| lead-on winner (C) | on | **1.673** | 0 / 0 / 0 / 4 | **1.724** | 2 / 0 / 1 / 31 |

(Restart B on its own courses: off 1.758, on 1.676, misses with the lead 0 / 0 / 0 / 3.)

What it says:
1. **The Dolphin is not a miss-limited hull.** The searched drives already miss almost nothing at I1-I3 with the lead
   off; the lead takes those last misses to about zero. The I4 gauntlet's cost is prism contact (about 105-115 prism hits
   a race on every drive, lead or not), which the nose lead does not touch. That is why the Dolphin's gain is small next
   to the Scarab's or the Manta's.
2. **The lead alone does not close the gap; the drive has to move too.** On the parent's winner the lead is worth -0.4%
   on the job's courses and 1.9% on fresh ones. The searched lead-on drive is 3.5% better than the parent's winner with the
   lead on (1.673 vs 1.734) on the job's courses and 1.0% better on fresh ones (1.724 vs 1.742), so part of the 3.5% is
   fitted to the job's 16 courses. Against what the game flies (the mode-wide drive) with the lead on, it is 11% better
   on the job's courses and 9% on fresh ones.
3. **Off, the lead-on drive is worse than the parent's** (1.781 vs 1.726): it is a drive for the day the switch is on, not
   a drive for today.
4. **Gain.** On fresh courses, parent winner / lead-on winner: gain 0 1.776 / 1.772, 1.0 1.925 / 2.013 (104 / 136 I4
   misses), 1.5 1.820 / 1.827, 2.5 **1.742 / 1.724**, 3.5 1.766 / 1.738. 2.5 is the best of those; 1.0 and 1.5 are worse
   than no lead, because any gain switches the pilot to judging its error from the command, and too little lead then
   under-turns. No roll stick: the Dolphin model flies roll "free" (`FlyRoll` off; roll only sets a blast's gape), so the
   Manta's corkscrew (brief 15) cannot happen here, and no lead-on race DNFs.

**Against the human marks** (best finished race; the model's median over all levels with the lead on, fresh courses, and
the job's courses in brackets): I1 58.0 s vs 66.3 (69.7) = 1.14x (1.20x); I2 79.8 vs 108.9 (106.6) = 1.36x (1.34x);
I4 105.6 vs 162.3 (156.0) = 1.54x (1.48x). The lead closes I1 to about the 15% rule (the parent was 1.34x); I2 and I4
stay far off. The parent's caveat stands: the human flights raced 20 or 26 rings and the model's ring count is not
matched to them, and the model's seconds run fast or slow against the editor by up to 1.6x.

**The four-part test** (section 0 rule 11 of the briefs): (1) restarts within 2% PASS, late gains small (marginal);
(2) beats every genome with the lead on, on the job's and on fresh courses PASS; (3) within 15% of the best human:
I1 borderline, I2 and I4 FAIL (ring count unmatched); (4) the real game: not run (the editor check of #1786, then the
Switchback soak at I1-I4 with the lead on).

**Verdict:** with the lead on, restart C's drive is the Dolphin's best Switchback drive in the model (1.673; 1.724 on
fresh courses) and the proposal for the day Yash turns `NoseLeadGain` on. Nothing is promoted and no asset is written;
the twin's `promote` rule (12 fresh courses per intensity, seedbase 99000, every level) is the gate, and the fresh run
above already passes its finish half (52/52 everywhere) and its composite half against the shipped genome and the
parent's winner. Files: `/mnt/project-files/regatta-training/results/dolphin-lead/` (run JSONs and logs, the 2x2
script `twobytwo.py`, every cell's JSON).
