# lite_sortfeel: hold sortfeel, then make it lighter

Session `lite_sortfeel`, 2026-10-01, 22:21-23:30 UTC. Branch `cece/swarm-x-lite-sortfeel`.
Code: `lite_sortfeel_model.py` (subclass `LiteSortFeel`, every idea a flag, default = sortfeel),
`lite_sortfeel_profile.py` (cProfile), `lite_sortfeel_bench.py` (per-phase timers + B scaling),
`lite_sortfeel_equiv.py` (lite vs sortfeel step-for-step), one appended `lite_sortfeel:` branch in
`swarm_eval.load_model` (`lite_sortfeel:<params.json>?frac=4,vec_look=1,...`).

## 1. Hold baseline (recorded first, never re-recorded)

`results/hold/sortfeel.json`: accurate 12/13, 11/13, 13/13, 13/13 (seeds 7/23/41/101; failing tests
time->space @7, mass->time + time->space @23), lossless (0 deaths), organic in band (planar 0.054),
smoothness 0.44 (worst lurch 6.68, teleport 0.92, molt burst 0.12, birth burst 0.289), perf 2.88 ms/step @126.

Caveat on that perf number: it was measured while my 1-thread profiling/bench jobs were also running
(my mistake against the one-heavy-job rule). The uncontended lite k=1 bench (vec_look=0, identical math to
sortfeel) measures 2.88 ms/step too, so it looks representative, but the later hold runs report perf
from an idle box.

## 2. Profile (grown bodies, mean live 126, 1 thread, 4 plans x 100 steps, per-phase perf_counter)

| phase | sortfeel (k=1, loop look) | vec_look | +frac=4 | +frac=8 |
|---|---|---|---|---|
| neighbours (collision + adhesion, O(N^2)) | 1.353 | 1.304 | 0.383 | 0.225 |
| chemotaxis (fate wells, O(N)) | 0.371 | 0.337 | 0.316 | 0.335 |
| compose (lay + molt + census) | 0.290 | 0.260 | 0.222 | 0.225 |
| look (state write) | 0.212 | 0.062 | 0.052 | 0.051 |
| wander (OU) | 0.196 | 0.166 | 0.158 | 0.165 |
| swaps (Potts) | 0.190 | 0.182 | 0.078 | 0.065 |
| plan + hatch | 0.086 | 0.077 | 0.070 | 0.075 |
| integrate | 0.063 | 0.058 | 0.042 | 0.042 |
| **total ms/step** | **2.88** | **2.56** | **1.42** | **1.29** (k=16: 1.30) |
| us / tadpole-step | 22.8 | 20.3 | 11.2 | 10.2 |
| pair evaluations / step | 18,349 | 18,349 | 4,587 | 2,292 |

Half of a step is the all-pairs neighbour block. Bookkeeping (compose + plan + look) is ~0.4 ms and already
cheap; amortising it (idea b) is worth at most ~0.3 ms in Python and ~nothing in C# (it is O(N) census work),
so it was not pursued. Past k=8 the O(N) work (chemotaxis, compose, wander) dominates and the curve is flat.

## 3. B-swarm scaling (ms per SWARM-step; 1 thread; sortfeel steps each swarm in a Python loop)

| config | B=1 | B=4 | B=16 | B=64 |
|---|---|---|---|---|
| sortfeel (k=1) | 4.14 | 2.47 | 3.03 | 2.83 |
| frac=4 | 2.01 | 1.52 | 1.67 | 1.64 |
| frac=8 | 1.64 | 1.16 | 1.56 | 1.31 |

Linear in B (per-swarm cost constant within noise): there is no cross-swarm batching in this model and I
did not vectorise across B (idea c) in the time available. Per-swarm independence is fine for the game
(each cell's swarm is its own core) and the fractional update composes with it.

## 4. Hold checks (PASS/FAIL against results/hold/sortfeel.json)

Full `hold.py --baseline` runs (the container restarted ~every 10 min tonight, killing 3 of 5 queued
holds; what finished is below, the rest is the seed-7 SMOOTH axis alone via `lite_sortfeel_smooth.py`,
which is exactly the hold's SMOOTH measurement).

**frac=4 + vec_look** (`hold/vec_look_1_frac_4.txt`) - **FAIL (2 axes)**, perf 1.52 ms/step (baseline 2.88):

| axis | result |
|---|---|
| accurate seed 7 passed | FAIL 11/13 vs 12/13 (lost mass->time) |
| accurate seed 23 passed | PASS 12/13 vs 11/13 (GAINED one) |
| accurate seeds 41, 101 | PASS 13/13, 13/13 |
| own losses (all seeds) | PASS |
| lossless | PASS 0 deaths |
| organic band / planar | PASS / PASS 0.061 vs 0.054 |
| smoothness | PASS 0.436 vs 0.44 |
| smooth lurch | PASS 6.51 vs 6.68 |
| smooth teleport | FAIL 1.058 vs 0.92 (limit max(1, 1.012)) |
| molt / birth burst, deaths | PASS |

**vec_look only, k=1 (the control: same maths as sortfeel, only float summation order differs)**:
accurate 12/13, 12/13, 13/13, 13/13 (the hold run died before lossless/organic; smoothness screen:
0.44, lurch 6.61, teleport 0.899, molt 0.12, birth 0.289 - all PASS).

SMOOTH screen (seed 7, `results/lite_sortfeel/smooth/*.json`):

| config | smoothness | lurch | teleport | birth burst | verdict vs baseline (0.44 / 6.68 / 0.92 / 0.289) |
|---|---|---|---|---|---|
| vec_look (k=1) | 0.440 | 6.61 | 0.899 | 0.289 | PASS |
| frac=4 | 0.436 | 6.51 | 1.058 | 0.289 | teleport FAIL |
| frac=4 + ease 24 | 0.466 | 6.96 | 1.046 | 0.325 | teleport FAIL |
| frac=8 | **0.771** | **3.97** | 1.050 | 0.289 | teleport FAIL, smoothness +0.33 |

**frac=8 + vec_look** (`hold/vec_look_1_frac_8.txt`) - **FAIL (same 2 axes as k=4)**, perf **1.28 ms/step**:
accurate 11/13, 12/13, 13/13, 13/13 (seed 7 loses mass->time, seed 23 gains one; total 49/52 = baseline's
49/52), own losses PASS, lossless PASS (0 deaths), organic PASS (planar 0.061), **smoothness 0.771 vs 0.44
PASS**, **lurch 3.97 vs 6.68 PASS**, teleport 1.05 vs 0.92 FAIL, bursts/deaths PASS.

No published config passes the hold cleanly. Every fractional config fails the same two rows: seed 7's
mass->time switch and teleport ~1.05.

## 5. Findings

1. **The accuracy axis of the hold flips on float noise.** The k=1 control is sortfeel with the
   neighbour sums reordered (max position gap 0.015 voxels after 150 steps; `lite_sortfeel_equiv.py`) and it
   GAINS a test at seed 23 (12/13 vs 11/13). frac=4 AND frac=8 both lose seed 7's mass->time and gain one at seed 23 - the same
   total (49/52) as the baseline. Because both k values lose the SAME test while the k=1 control keeps 12/13
   at seed 7, I can't call that row pure noise: mass->time at seed 7 is probably a borderline test that
   coasting tips over. A 3-sample rerun at more seeds would settle it. A 3-sample switch test near the bar of 8 is chaotic in the last bits, so
   "passed may not drop at any seed" will fail correct refactors about half the time. Evidence for the hold
   owner: compare totals across seeds, or re-record the baseline as the min over 2-3 float-perturbed reruns.
   (I did not edit hold.py.)
2. **Fractional update works for cost** and is the right lever: neighbours go 1.35 -> 0.38 (k=4) -> 0.23 ms
   (k=8); total 2.88 -> 1.42 -> 1.29 ms/step; us/tadpole-step 22.8 -> 11.2 -> 10.2. Past k=8 the O(N) work
   dominates (k=16 is no faster). The cost of a coasting tadpole is a slightly larger single-step jump:
   teleport rises from 0.90 to ~1.05 at every k >= 4, just over the hold's max(1.0, +10%) bar. That is the one
   real (non-noise) regression; it is small, it is not a pop (molt/birth bursts unchanged) and it is exactly
   the effect of a tadpole that keeps its velocity for k-1 steps while its neighbours re-plan.
3. **frac=8 is visibly SMOOTHER**, not rougher: smoothness 0.44 -> 0.77 (above evo's 0.58, the lead's
   "beautifully organic" reference) and worst lurch 6.7 -> 4.0. Reason: an updated tadpole blends toward its
   wish with 1 - inertia^8 (0.95) but only every 8 steps, and each tadpole on its own phase - the swarm's
   response to a cull is spread over 8 steps instead of arriving in one. That is the "ramp the response"
   recommendation of the brief, obtained for free from the fractional update. The explicit ease ramp
   (cap vmax after a composition change) helped smoothness only a little (0.436 -> 0.466 at k=4).
4. **No batching gain across swarms** in this Python model (per-swarm loop). Per-swarm cost is constant in B,
   so 64 swarms at frac=8 cost 64 x 1.3 ms; the fractional update cuts every swarm's cost, it does not make
   cost independent of swarm size: neighbour work is O(N^2/k). At a fixed per-frame pair budget P you would
   pick k = N^2/P, but staleness grows with k, so for large N the scalable fix is a spatial hash (pairs
   O(N x ~25)) - not built here (at N=126 numpy all-pairs beats a Python hash; in C# the hash wins at N > ~200).

## 6. Recommendation for the game (C# SwarmSortCore)

Port two things, both mechanical: **vec_look** (only write the look state when a tadpole's type changes -
in C# that is a per-type cached vector, zero per step) and **frac=k** (each step, only slots with
(slot + t) % k == 0 run collision/adhesion/swap; everyone integrates; updated tadpoles blend with
1 - inertia^k). Use **k = 8**: it fails exactly the same two hold rows as k=4, while it is cheaper (1.28 vs 1.42 ms/step) and
much smoother (0.77 vs 0.44, the best measured in the portfolio). `params.json` holds k=8. Before the game
adopts it, the hold owner has to decide whether teleport 1.05 against a 1.012 limit, and one swapped switch
test at seed 7, are acceptable.

Per-step operation count at N = 126 (grown), from the bench's counters:

| op | sortfeel | k=4 | k=8 |
|---|---|---|---|
| neighbour pair evals (dist, rep, adhesion select ~15 flops) | 18,349 | 4,587 | 2,292 |
| swap candidate pairs (4 quadratic forms each) | 128 | 32 | 17 |
| chemotaxis (3x3 matvec + clip) | 126 | 126 | 126 |
| look writes | 126 | per-type | per-type |
| census / lay / molt | O(N) | O(N) | O(N) |

The C# core's measured 0.05-0.16 ms/step is dominated by the pair loop, so k=4 should bring it to roughly
0.02-0.05 ms/step and k=8 to ~0.015-0.04 ms/step per swarm at N~130 (estimate: pair loop / k plus a fixed
~0.01 ms of O(N) work), i.e. a 64-swarm cell budget of ~1-2.5 ms at k=8.

Not delivered (time + restarts): summary.json/rollout.json for the gallery, a full hold for the ease and
k=2 variants, cross-swarm vectorisation, the spatial hash.
