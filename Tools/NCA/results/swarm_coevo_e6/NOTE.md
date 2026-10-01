# E6 (sticky labels + scale-invariant loss): run stopped, training hangs

The E6 run never reached its first scored snapshot (step 1000). It **hung twice**: no
crash and no traceback, the process pinned at ~390% CPU with the log silent. Nothing was published.

| attempt | started (UTC) | last logged step | log went silent |
|---|---|---|---|
| 1 | 06:13 | 170 | 06:28, then killed by the 2h task timeout at 08:13 |
| 2 | 08:13 (fresh: no checkpoint had been written) | 360 | ~08:47, stopped by PID at 08:58 |

## Where it hangs (py-spy, attempt 2)

```
_lse            swarm_nca.py:559
sinkhorn_ot     swarm_nca.py:572-573   <- `while True` eps-scaling loop
_divergence     swarm_nca.py:627       <- the `if L.scale_inv:` branch: oaa = sinkhorn_ot(self_cost(x, L), a, a, L.eps)
swarm_loss      swarm_nca.py:656
train           swarm_nca.py:956
```
`py-spy dump --locals` shows **`e: NaN`** in `sinkhorn_ot` (eps 0.05).

## Why that is an infinite loop

`e = max(float(Cd.max()), eps)` is NaN when the cost matrix contains a NaN. Then
`e <= eps` is False forever, and `e = max(e * 0.5, eps)` stays NaN: Python's `max(nan, x)`
returns its first argument whenever `x > nan` is False. So any NaN cost hangs training instead of raising.

## Likely trigger (unconfirmed)

The hang is on the new scale-invariant path, so the NaN cost probably comes from the rescaled
positions (`_rescaled`, swarm_nca.py:505). Before the second hang, every swarm's headcount had just
collapsed: the step-360 line reads `n 82 / 65 / 86 / 143`, where it had been 280 each. A
near-degenerate swarm (tiny RMS radius `r`, clamped only to 1e-3) scaled up by `plan_rms / r` can
overflow the cost to inf. `inf - inf` in the log-sum-exp then gives NaN. A NaN already present in
`x["p"]` would cause the same hang. The first hang (step 170) had full headcounts, so collapse is not the only route.

## Suggested fixes (not applied; the overnight babysitter does not edit code)

- `sinkhorn_ot`: guard against non-finite cost (`if not math.isfinite(e): raise` or skip the sample), and add an iteration cap.
- `_rescaled`: use a larger floor on `r`, or skip `scale_inv` for samples with tiny weight or radius.
- Then rerun the same command; it resumes from `Tools/NCA/runs/swarm_e6`. That run has no checkpoint yet, so it will start fresh.

Command: `NCA_THREADS=4 python3 -u -W ignore Tools/NCA/gpu_run.py swarm --device cpu --tag e6 --steps 6000
--set per_kind=2 --set pool=24 --set seed_every=6 --set roll_min=48 --set roll_max=96 --set bptt=28
--set sticky_plan=1 --set p_switch=0.25 --set switch_cooldown=240 --set p_ratio=0.5 --set margin=0.15 --set scale_inv=1`
(CPU, torch 2.14.1+cpu, 4 threads, ~5 s/step.)
