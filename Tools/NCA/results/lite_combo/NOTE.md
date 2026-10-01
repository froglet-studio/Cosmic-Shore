# lite_combo — hold the combo front runner, then make it lighter

Branch `cece/swarm-x-lite-combo`. Session ran 22:21–23:30 UTC (about 70 minutes), so this is a short,
honest first pass, not a finished optimisation.

## 1. The hold (recorded first, untouched since)

`results/hold/combo.json` — `hold.py --model combo:results/combo/params.json --record ...`:

| axis | combo baseline |
|---|---|
| accurate (seeds 7 / 23 / 41 / 101, 3 samples, loss-8 bar) | **16/16, 15/16, 15/16, 15/16** |
| lossless | **0 deaths** |
| organic | in band, planar excess 0.024 |
| smooth | **0.51** (worst lurch 6.26, worst teleport 1.32, molt burst 0.18, birth burst 0.29) |
| performant (reported) | 8.4 ms/step @ n = 134 (hold's own measurement, 1 thread) |

## 2. Profile (`lite_combo_profile.py`, 1 thread, body grown 240 steps, 100 steps timed, B = 1)

`profile_base.json`. ms/step per phase (phases nest: "coarse.step" includes plan_field + lay):

| plan | total | coarse step (grid splat/blur/grad/sample + collision) | fine layer | migrate | proxy/transfer2 | molt controller | decide | lay |
|---|---|---|---|---|---|---|---|---|
| mass | 9.2 | **4.1** | 3.3 | 1.2 | 0.8 | 0.3 | 0.2 | 0.2 |
| space | 7.3 | **3.8** | 1.0 | 0.6 | 0.6 | **1.7** | 0.1 | 0.2 |
| charge | 8.4 | **4.2** | 2.4 | 1.2 | 0.7 | 0.3 | 0.2 | 0.2 |
| time | 5.9 | **3.9** | 0.9 | 0.6 | 0.5 | 0.3 | 0.1 | 0.2 |

What it says:
- **The coarse grid step is half the cost and does not care about N** (3.8–4.2 ms for 77–193 tadpoles): at
  B = 1 it is Python/torch per-op overhead on a G = 8 grid, not arithmetic. The plan-field cache already
  makes the field itself ~0.07 ms.
- The **O(n·M + n²) fine layer** is the only phase that tracks body size (0.9 ms at 77 → 3.3 ms at 193).
- **Bookkeeping is small but not free**: proxy/transfer2 (per-element per-domain Python loops) 0.5–0.8 ms
  every step, and the molt controller 0.3 ms — except on space, where steady-state molting churn makes its
  per-candidate Python loop cost 1.7 ms. These are the `for b in range(B)` / `.tolist()` loops the brief
  names; they are what makes cost LINEAR in B (below).

## 3. Scalability (ms per swarm-step, batched B swarms, 1 thread)

| config | B = 1 | B = 4 | B = 16 | µs per tadpole-step @ B = 16 |
|---|---|---|---|---|
| combo (baseline) | 7.7 | 5.1 | 4.7 | 36.5 |
| lite frac_k = 4 | 7.2 | 4.3 | 3.8 | 31.1 |

Batching amortises the per-op overhead only down to ~4.7 ms/swarm: the per-sample Python loops (fine,
migrate, proxy, molt) scale with B, so cost per swarm is roughly flat past B = 4. **A fractional update
does NOT make cost independent of swarm size here**: the coarse grid step (all tadpoles, every step) and the
per-sample loops dominate, so updating 1/4 of the fine layer per step saves only ~6% at B = 1 and ~19% at
B = 16. (B = 64 was not measured — out of time.)

## 4. The lite flags (`lite_combo_model.py`, subclass, all default off, bit-exact when off)

- `frac_k` — FRACTIONAL fine layer: each step only slots with `slot % k == step % k` (plus newly hatched)
  recompute the fine displacement, evaluated against the WHOLE live body (centre, settle gain and the
  same-class push see everyone; only the selected rows are computed); the rest coast on their last fine
  displacement. The displacement is a per-step velocity, so nothing is rescaled by k. Coarse step,
  collisions, laying and the molt clock run for everybody every step. The coasting cache resets per rollout.
- `mig_frac` — also restrict the migrant search to the share (off in every published run).
- `molt_every` — molt controller every m steps with its clock rate × m (built, not measured).
- `fine_ease` — low-pass on the fine displacement (built, not measured).

## 5. Hold results

| config | acc 7 / 23 / 41 / 101 | lossless | organic (planar) | smoothness | worst lurch | worst teleport | ms/step (hold) | verdict |
|---|---|---|---|---|---|---|---|---|
| combo (baseline) | 16 / 15 / 15 / 15 | 0 | in (0.024) | 0.51 | 6.26 | 1.32 | 8.4 | — |
| **frac_k = 4** | 16 / 15 / 15 / 15, no lost test, own losses held | 0 | in (0.045) | **0.597** | **4.53** | **1.73 (limit 1.45)** | 6.9 | **FAIL (teleport only)** |
| frac_k = 2 | 16 / 15 / 15 / 15, no lost test; **own mass @41 1.99 vs 1.33 (limit 1.83)** | 0 | in (0.054) | 0.517 | 5.65 | 1.29 | 7.5 | **FAIL (one own-plan loss)** |
| frac_k = 4, coast 0.75 | 16 / 15 / **14** / **14**; lost time->space @41, charge->time @101; own time worse @7, @41 | 0 | in (0.053) | 0.522 | 6.44 | **1.72** | 7.0 | **FAIL (accuracy + teleport)** |

`hold_frac4.json` / `hold_frac4.log`, `hold_frac2.json`.

**frac_k = 4 is a near-miss, and the shape of it is the finding.** Every accuracy, lossless and organic
axis holds, and smoothness RISES 0.51 → 0.60 (worst lurch 6.26 → 4.53, every event's lurch is lower, the
mass heal's 6.26 → 2.54): coasting on a slightly stale intent is itself a low-pass, which is exactly the
"ease in after a composition change" the brief asked for. What fails is one tadpole on one event — the
charge → time switch, teleport 1.23 → 1.73 (3.5 voxels in a step into Time's 2.0 vmax body): a coasting
tadpole re-applies its last fine step on top of its coarse velocity and a collision push in a frame where
the fine field would have turned it. Per-event teleport is otherwise within ±0.25 of the baseline. The
obvious repair, a coast DECAY (coasting rows apply 0.75·d_prev, compounding while stale), was run and is
WORSE on every axis: teleport is unchanged (1.72 — so the jump is not the stale fine step itself), two
tests are lost (seeds 41 and 101, both into/out of Time) and the smoothness gain vanishes (0.52). Decaying
the intent starves the fine layer between refreshes; the undecayed coast is what carried the smoothness.
The remaining untested repair is a cap on the summed per-step displacement. I do not consider the teleport tolerance wrong here — a
3.5-voxel jump is a real (if single-tadpole) artefact.

## 6. Recommendation

**No lite config passes the hold, so the config I recommend for the game is the held combo itself
(`params.json` here = `results/combo/params.json`, unchanged; its summary/rollout are the ones already
published in `results/combo/`).** Honest negative result for this session:

- The fractional update buys **18% in Python (8.4 → 6.9 ms/step in the hold's own measurement)** and,
  unexpectedly, **raises smoothness 0.51 → 0.60** at k = 4 — but every k tried breaks exactly one held
  number (k = 4: one tadpole's teleport on one switch; k = 2: one own-plan loss at one seed). Both misses
  are single-event, so some of it may be rollout noise — the hold has no repeat of the smooth/teleport run,
  and I recommend the owner of hold.py consider whether worst-teleport on ONE tadpole in ONE event at ONE
  seed should be a hard gate (I do not edit it; the 3.5-voxel jump is real).
- **The cost is not where a fractional update reaches.** Half of the step is the coarse grid (constant in
  N), and a further 1-2.5 ms is per-sample Python bookkeeping that makes cost linear in B. In the C# core
  those terms are cheap; the O(N²) fine layer is the term that grows with population, and that is where
  frac_k pays. For the GAME, frac_k = 4 on the fine layer is the right shape of optimisation once the
  teleport is capped: it divides the only super-linear term by 4 and smooths the response. It is not yet a
  held config.

## 7. Estimated C# cost (per swarm-step, per-operation counts; n ≈ 134 tadpoles, M ≈ n plan units, G = 8)

- coarse grid: splat n × 8 corners × 12 classes, blur + gradient over 512 cells × 13 channels
  (~20k flops), trilinear sample n × 53 channels, collision via spatial hash ≈ n × ~8 neighbours. Constant
  in practice; the Python cost here is call overhead the C# core does not have.
- fine layer: n × M Gaussian bumps + n × n same-class bumps ≈ 2 × 134² ≈ 36k exp per step at k = 1;
  **frac_k divides this by k** (k = 4: ~9k). This is the term that grows as N², so it is where a fractional
  update pays in C# — at N = 500 it is ~500k exp/step at k = 1 vs ~125k at k = 4.
- migrate: n × M (own-want sweep) + migrants × M.
- bookkeeping: census 12 bins (n adds), ratio target (12 × 3 ops), molt quota (candidates × 4), proxy
  (12 classes × excess) — all O(n) and should be incremental in C# (update on birth/molt/death).

## Next steps (not done)
1. coast decay `c` ∈ {0.5, 0.75} at frac_k = 4 — the likeliest config to pass the hold with smoothness ≥ 0.55.
2. vectorise proxy/transfer2 and the molt candidate loop (the linear-in-B cost); measure B = 64.
3. `molt_every` m = 4, 8 against the hold.
