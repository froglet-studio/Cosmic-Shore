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

HOLD_TABLE_PLACEHOLDER

## 5. Findings

FINDINGS_PLACEHOLDER
