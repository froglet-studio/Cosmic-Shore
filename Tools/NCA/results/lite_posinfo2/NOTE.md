# lite_posinfo2 - holding posinfo2, then trying to make it light

Session: lite_posinfo2 (branch cece/swarm-x-lite-posinfo2), 22:24-23:40 UTC, brief `briefs/lite.md`.
Code: `lite_posinfo2_model.py` (subclass of `posinfo2_rule.PosInfo2Rule`, every lever a flag, default off;
flags-off is BIT-IDENTICAL to posinfo2 - checked over 120 steps), `lite_posinfo2_profile.py`,
`lite_posinfo2_distil.py`, and one appended `lite_posinfo2:` branch in `swarm_eval.load_model`
(spec `lite_posinfo2:<rule.pt>?fire_k=4&homeo_every=8&frame_every=4&half=1&rng_burn=1`).

## Honest headline

**No lite config PASSES the hold, so I am not recommending a lite config for the game.** The recommended
config is the unchanged posinfo2 (baseline = `results/hold/posinfo2.json`). What this session did establish:

1. **The HOLD baseline** (recorded first, committed, never re-recorded): seeds 7/23/41/101 = 13/13, 12/13, 13/13,
   13/13 (51/52), lossless (0 deaths), organic in band (planar excess 0.12), **smoothness 0.497** (worst lurch
   4.22, molt burst 0.32, birth burst 0.50), 6.99 ms/step @134.
2. **Where a C# port pays**: the network, not the bookkeeping (cost model below). posinfo2 already evaluates the
   network on only HALF the swarm per step (its `fire_rate` 0.5 - it was trained that way), so it already
   runs a k=2 fractional update: **46k multiply-adds per tadpole-step** today.
3. **The fractional update makes it SMOOTHER** (k=4 with coasting: smoothness 0.519 vs 0.497, worst teleport
   0.87 vs 1.67, birth burst 0.34 vs 0.50, lurch 4.06 vs 4.22) and halves network cost - but fails the hold on
   ACCURACY (time->space and charge->time lost at one seed each) and on molt burst (+0.09, from the amortised
   homeostat, not from k).
4. **Distillation fails in closed loop.** A 64-wide student fits the teacher's per-step velocity at R^2 0.98
   and grows nothing (0/13 everywhere, own losses 27-98). Two rounds of DAgger (student drives, teacher
   labels) halve the error each round (30-98 -> 19-42 -> 7-13 at H=96), so it is converging, but it is not
   there and a learned-rule student needs the hold as its training signal, not as a final check.
5. **Evidence about the hold itself** (for the lead, who owns hold.py): see "Is the accuracy gate noise?" -
   the single switch every candidate loses, time->space, sits on the loss-8 bar in the BASELINE itself
   (7.49 at 2/3 samples, 7.99 at 2/3, 7.44 at 3/3, 10.38 at 1/3), so ANY change that re-rolls the dice can
   lose it. **The noise control (the unchanged rule, generator stream shifted by one draw per step) FAILS the
   hold too** - time->space lost at 3 of 4 seeds and smoothness 0.432 vs 0.497. The hold's per-seed accuracy and
   smoothness gates are inside the noise of a single realisation, so every FAIL above that is only on those
   gates is NOT evidence of a regression. Details and a suggested fix below.

## Hold PASS/FAIL table (every config checked; `hold_<name>.json` / `.txt` here)

| config | ms/step (hold, 4 thr, contended) | passed 7/23/41/101 | lost tests | own-loss fails | smoothness | lurch | teleport | molt / birth burst | verdict |
|---|---|---|---|---|---|---|---|---|---|
| **baseline posinfo2** | 6.99 | 13 / 12 / 13 / 13 | - | - | 0.497 | 4.22 | 1.67 | 0.32 / 0.50 | - |
| k4_h8_f4 (fire_k 4, homeo every 8, frame every 4) | **4.71** | 11 / 12 / 13 / 12 | charge->time @7, time->space @7, @101 | own charge 2.76, own time 3.14 @101 | **0.519** | 4.06 | **0.87** | 0.41 FAIL / 0.34 | FAIL |
| h4_f4 (homeo every 4, frame every 4; no fractional update) | 5.74 | 12 / **13** / 13 / 13 | time->space @7 (9.44) | own charge 2.26 @41 (+0.63) | 0.497 | 5.52 FAIL | 0.98 | 0.32 / 0.46 | FAIL |
| (all lite rows above except s64 ran BEFORE the cache-reset fix: their frame cache could carry one stale frame for <= 3 steps across batched rollouts; slightly pessimistic) | | | | | | | | | |
| k2 (fire_k 2, round-robin + coast) | - | 12 / (killed) | time->space @7 | - | - | - | - | - | incomplete: the hold ran 6x slower than the others and was killed to make room for the noise control |
| s64 (64-wide student, behaviour cloning) | 5.86 | 0 / 0 / 0 / 0 | everything | own 27-98 | 0.287 | 1.86 | 1.78 | 0.25 / 0.37 | FAIL (collapse) |
| **k4_f4** (fire_k 4 + frame every 4, AFTER the cache fix, homeostat every step) | 6.30 | 12 / 11 / 12 / 13 | charge->time @7, @23; time->space @41 | own mass 2.30 @101 | 0.460 | 3.64 | 0.90 | 0.385 FAIL / 0.54 | **FAIL - real: organic band lost (planar 0.18 > 0.15), and charge->time is not the noise-floor switch** |
| noise control (unchanged rule, generator stream shifted by one draw per step) | 6.435 | 12 / 12 / 12 / 12 | time->space @7, @41, @101 | own charge 3.10 @23 | 0.432 FAIL | | | | **FAIL** (lurch 4.99, molt 0.33, birth 0.54 pass) |

Note on accuracy totals: h4_f4 passes **52/52 - 1 = 51 of 52** feasible, exactly the baseline's 51/52 - it
loses time->space at seed 7 and WINS it at seed 23. The hold forbids any lost test per seed, so it fails.

## Is the accuracy gate noise?

**Yes, mostly.** The noise control is the UNCHANGED posinfo2 rule with one extra `torch.rand(1)` drawn per
step - behaviourally neutral, it only shifts the generator stream (flag `rng_burn=1`). It **FAILS its own hold**:
12/13 at all four seeds (time->space lost at 7, 41, 101), own charge 3.10 vs 2.01 at seed 23, and
**smoothness 0.432 vs 0.497** (a drop of 0.065 against the 0.05 tolerance). So under the hold as written:
- `no lost test` is decided by time->space, which the baseline passes at 2/3, 1/3 (fail), 2/3, 3/3 samples with
  losses 7.49 / 10.38 / 7.99 / 7.44 against the bar 8. A re-roll loses it at most seeds. The noise control loses
  MORE than any lite candidate did (k4_h8_f4 lost it at 2 seeds, h4_f4 at 1, while h4_f4 WON it at seed 23).
- the smoothness gate (0.05) is smaller than the seed-to-seed spread of smoothness itself at one seed
  (0.497 vs 0.432 for the same rule). k4_h8_f4 (0.519) and h4_f4 (0.497) both sit ABOVE the noise control.
- own-loss tolerance max(0.5, 15%) is also inside the noise (own charge 2.01 -> 3.10 on a re-roll).
Suggested fix for hold.py (the lead owns it; not edited): compare SUMMED passes over the four seeds (with a
binomial allowance) rather than per-seed `no lost test`, and either measure smoothness over several seeds or
set its tolerance from a noise-control run like this one. **Under such a rule, k4_h8_f4 and h4_f4 would be
read as "inside noise on accuracy, better on smoothness" (k4_h8_f4), with only k4_h8_f4's molt burst and
h4_f4's worst lurch as real candidates for regressions**. I then checked k4_h8_f4 minus the amortised homeostat
(k4_f4, after the cache fix): it is a REAL regression, not noise - it leaves the organic band (planar excess 0.18,
bodies flatten when 3/4 of the swarm coasts) and loses charge->time at two seeds. So the fractional update on
this LEARNED rule needs retraining under the new update schedule; it cannot be bolted on. Contrast with the
designed front runners, where the rule has no learned dependence on its own firing statistics.

## Profile (per step, grown 192-tadpole Mass body, 1 thread, perf_counter per phase, 100 steps)

| phase | base ms | k4 | homeo 8 | k4+homeo 8+frame 4 | what it is |
|---|---|---|---|---|---|
| edges (all-pairs cdist + nonzero) | 0.81 | 0.87 | 0.87 | 0.88 | O(N^2) per swarm |
| frame (census, body frame: centroid, eigh of 3x3, per-domain/element centroids) | 1.05 | 1.12 | 1.10 | 0.84 | O(N) |
| perceive (neighbour means + gradients, 37 channels) | 1.65 | 1.02 | 1.55 | 0.93 | O(edges x 37 x 6) |
| **mlp** (252 -> 192 -> 192 -> 35) | **0.61** | 0.47 | 0.63 | 0.46 | the network |
| physics (collision, membrane) | 0.55 | 0.63 | 0.57 | 0.60 | O(edges) |
| hatch bookkeeping | 0.32 | 0.30 | 0.34 | 0.30 | O(N) |
| **homeo** (scaled quota + guarded molting + laying: Python loops per swarm, per domain, per element) | **2.94** | 2.84 | 1.09 | 1.08 | O(B) Python |
| total | 8.0 | 7.3 | 6.2 | 5.2 | (profiled while a hold ran: noisy) |

At B = 12 batched swarms (the hold's own layout), 120 steps: homeo is **48%** of the time and the network 4%.
In Python the composition controller is the cost and it scales LINEARLY with the number of swarms (its loops
are per swarm); the network is batched and nearly free. **In C# it is the other way round** (next section).

**Scaling with batched swarms** (Mass body, 1 thread, grown once with the base rule, `profile.json`; run while a hold ran, so absolute ms are noisy, ratios are not):

| config | B | ms per swarm-step | us per tadpole-step | homeo ms | perceive ms | mlp ms | net MACs/tadpole-step |
|---|---|---|---|---|---|---|---|
| base | 1 | 7.877 | 41.03 | 3.024 | 1.545 | 0.607 | 45920 |
| base | 4 | 5.716 | 29.77 | 10.423 | 4.905 | 1.166 | 45921 |
| base | 16 | 4.812 | 25.06 | 38.834 | 17.979 | 3.676 | 45709 |
| base | 64 | 5.371 | 27.97 | 159.702 | 103.194 | 15.811 | 46016 |
| h4_f4 | 1 | 6.288 | 32.75 | 1.658 | 1.494 | 0.632 | 46086 |
| h4_f4 | 4 | 4.866 | 25.35 | 6.855 | 5.1 | 1.246 | 45827 |
| h4_f4 | 16 | 4.169 | 21.72 | 27.197 | 18.77 | 3.864 | 45843 |
| h4_f4 | 64 | 4.486 | 23.36 | 100.814 | 103.716 | 15.665 | 45884 |
| k4 | 1 | 8.213 | 42.78 | 3.225 | 1.088 | 0.537 | 22992 |
| k4 | 4 | 5.089 | 26.51 | 10.204 | 2.693 | 0.797 | 22992 |
| k4 | 16 | 4.402 | 22.93 | 40.703 | 9.385 | 2.265 | 22992 |
| k4 | 64 | 4.387 | 22.85 | 166.664 | 38.653 | 8.919 | 22992 |
| k4_h8_f4 | 1 | 5.74 | 29.9 | 1.085 | 1.081 | 0.494 | 22992 |
| k4_h8_f4 | 4 | 3.562 | 18.55 | 3.987 | 2.788 | 0.93 | 22992 |
| k4_h8_f4 | 16 | 2.857 | 14.88 | 16.842 | 9.444 | 2.199 | 22992 |
| k4_h8_f4 | 64 | 3.051 | 15.89 | 82.42 | 38.642 | 8.634 | 22992 |
| s64 | 1 | 8.123 | 42.31 | 3.057 | 1.693 | 0.336 | 11206 |
| s64 | 4 | 6.043 | 31.48 | 11.125 | 5.532 | 0.642 | 11197 |
| s64 | 16 | 4.871 | 25.37 | 40.369 | 18.756 | 1.526 | 11193 |
| s64 | 64 | 5.446 | 28.37 | 169.165 | 104.344 | 6.656 | 11207 |
| s32 | 1 | 8.203 | 42.72 | 3.083 | 1.872 | 0.233 | 5065 |
| s32 | 4 | 5.844 | 30.44 | 10.733 | 5.415 | 0.444 | 5104 |
| s32 | 16 | 5.254 | 27.37 | 42.674 | 21.48 | 1.002 | 5076 |
| s32 | 64 | 5.148 | 26.81 | 157.161 | 101.356 | 3.245 | 5095 |

Read: batching amortises Python overhead (base 7.9 -> 5.4 ms per swarm-step from B 1 to 64), and at B 64 the
PERCEPTION gather is the largest phase (103 ms of 344). fire_k 4 cuts perception 2.7x and the network 1.8x
because both now run only on the firing rows; amortised bookkeeping cuts homeo further. The best config
(k4_h8_f4) is **3.05 ms per swarm-step at B 64 vs 5.37 (1.76x)**. With a fixed per-frame budget a fractional
update makes the per-step NETWORK cost proportional to N/k, i.e. a 4x larger swarm at k 4 costs the same network
and perception as today's swarm - but edges (all-pairs) and physics still run on everyone every step.
The students (H 64 / 32) cut the MLP 2.4x / 4.9x at B 64 - it barely moves Python totals, it is the C# lever.

## Cost model for the C# port (per tadpole-step, at the grown size)

| piece | multiply-adds per tadpole-step | note |
|---|---|---|
| network, as shipped (H 192, fire 0.5) | **45,984** (91,968 per evaluation x 0.5) | 252x192 + 192x192 + 192x35 |
| network, fire_k 4 (k4 configs) | 22,992 | 0.25 evaluations per tadpole-step |
| network, fire_k 8 | 11,496 | |
| student H 64 (if it worked) x fire 0.5 | 11,232 | 22,464 per evaluation |
| student H 32 | 5,104 | 10,208 per evaluation |
| perception (degree ~10 neighbours, 37 channels: 2 means + a 3x37 gradient) | ~3,000 x fire share | gathers, not MACs |
| body frame + census | ~30 | O(N) per swarm, amortisable (frame_every) |
| composition controller | ~10-50 integer ops | per swarm per step; a few hundred ops per swarm in C# |
| collision / membrane | ~10 x degree | |

So **the network is ~90% of a C# step**: at 134 tadpoles x 46k MACs = 6.2M MACs per swarm-step. A C# core doing
~1-2 GMAC/s scalar per thread (Burst/SIMD 4-8x more) puts posinfo2 at ~0.5-3 ms per swarm-step, an order more than
the designed cores (sort 0.05-0.16 ms). The two levers that address that number are exactly the two that
failed the hold today: fewer evaluations (fractional update) and a smaller network (distillation). The
bookkeeping levers (homeo_every, frame_every) matter only to the Python prototype.

## What failed, and why I think so

- **homeo_every 8 creates the molt burst** (0.41 vs 0.32 + 0.05 allowed): the per-call molt probability is
  scaled 8x (0.1 -> 0.8) so the same molts land in one step. homeo_every 4 holds the burst (0.315) but raised
  the worst lurch (5.52, one strike recovery). Amortising a DESIGNED controller is a free speed-up only in
  Python; not worth gating on.
- **fire_k 4 + coasting is smoother but drifts on accuracy**: round-robin re-steering with every tadpole
  coasting on half its last velocity has the same mean drift as the trained stop-go (fire 0.5), but the network
  was BPTT-trained under random 50% firing; its inputs now come from a body whose motion statistics differ.
  A short fine-tune under fire_k would be the honest test; there was no time.
- **Distillation**: per-step fit is not closed-loop fit. The body is a 240-step feedback system and the
  teacher's own data never shows the student's mistakes. DAgger works (each round halves the error) - the next
  step is 3-5 more rounds, then fine-tune the student with the BPTT loss (posinfo2_train), with the hold last.

## Recommendation

For the game: **posinfo2 unchanged** (it is already a k=2 fractional update), port it with the network in
float16 / SIMD, and budget ~6M MACs per swarm-step. Next research steps, in order of expected C# payoff:
(1) DAgger-distil to H 64 then BPTT-fine-tune, gated by the hold (4x fewer MACs); (2) fine-tune under fire_k 4
round-robin + coasting (2x fewer evaluations, and it was SMOOTHER); (3) the noise question above, so a lite
candidate is not failed by a coin the baseline itself flips.
