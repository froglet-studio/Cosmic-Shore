# E2 (E1 + learned laying gate): run halted — training hangs in `sinkhorn_ot`

**Status:** stopped by the overnight babysitter on 2026-10-01 at 08:10 UTC. No snapshot was ever scored or published.

## What happened

Command (unchanged across launches, from repo root):

```
NCA_THREADS=4 python3 -u -W ignore Tools/NCA/gpu_run.py swarm --device cpu --tag e2 --steps 6000 \
  --set per_kind=2 --set pool=24 --set seed_every=6 --set roll_min=48 --set roll_max=96 --set bptt=28 \
  --set sticky_plan=1 --set p_switch=0.25 --set switch_cooldown=240 --set p_ratio=0.5 --set margin=0.15 \
  --set learned_lay=1
```

Commit 8ade03af, torch 2.14.1+cpu, 4 threads. The CPU-vs-CPU check passed. Training ran at about 7–9 s per step.

| Launch | Last logged step | Then |
|---|---|---|
| 1 (05:16) | 200 (05:44) | No output for 92 minutes at ~390% CPU, until the 2-hour background limit killed it. |
| 2 (07:16) | 220 (07:47) | No output for more than 10 minutes at ~390% CPU. Stopped by PID at 08:10. |

Both hangs came before the first checkpoint (`snap_every=250`), so a relaunch cannot resume. Every launch starts from step 0 and reaches the same region.

## Where it hangs

`py-spy dump` of the hung process (launch 2):

```
_lse          (swarm_nca.py:540)
sinkhorn_ot   (swarm_nca.py:553)
swarm_loss    (swarm_nca.py:633)   # oaa = sinkhorn_ot(self_cost(x, L), a, a, L.eps)
train         (swarm_nca.py:933)   # main bptt branch
```

The eps-scaling loop in `sinkhorn_ot` is `while True` and exits only once `e <= eps`. `e` starts at `max(float(Cd.max()), eps)` and is halved each pass with `e = max(e * 0.5, eps)`. If the cost matrix contains NaN or inf, the loop never ends:

- NaN: `Cd.max()` is NaN, so `e` is NaN. `NaN <= eps` is False, and Python's `max(nan, eps)` returns NaN, so `e` stays NaN forever.
- inf: `e` is inf, and `inf * 0.5` is still inf.

So some swarm's decoded state (positions or features feeding `self_cost`) is becoming non-finite around steps 200–230. That is plausibly the new learned laying gate (`learned_lay=1`): launch 2's step-210 line shows two swarms collapsing to n=17 and n=20. This is diagnosis only; no code was changed.

## Suggested fixes (not applied)

1. In `sinkhorn_ot`, guard the loop: if `not math.isfinite(e)`, raise, or skip the sample with a large finite loss. Also cap the number of iterations, so a bad sample fails loudly instead of hanging.
2. Find the source of the non-finite value. Check `torch.isfinite` on `sw.pos`/`sw.s` after each `rule()` step under `learned_lay=1`, and on the laying-gate logits. Consider gradient clipping or clamping the gate.
3. Lower `snap_every` (e.g. 100) for experimental runs, so a mid-run failure leaves a resumable checkpoint.

`log.jsonl` (launch 2, steps 0–220) is included alongside this note.
