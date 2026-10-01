# E1 (sticky switch labels) — run halted at step 500

**Status:** stopped by the overnight babysitter on 2026-10-01 ~07:30 UTC. No scored result exists:
the first evaluation is at step 1000, and the run never got past step 500.

## What happened
- Launch 05:13 UTC. Command: `gpu_run.py swarm --device cpu --tag e1 --steps 6000 --set per_kind=2
  --set pool=24 --set seed_every=6 --set roll_min=48 --set roll_max=96 --set bptt=28 --set sticky_plan=1
  --set p_switch=0.25 --set switch_cooldown=240 --set p_ratio=0.5 --set margin=0.15`
- CPU-vs-CPU check OK. Training ran normally to step 500 (05:49:59, `rule_00500.pt` + `latest.pt` written),
  then printed nothing more for 85 minutes while using ~390% CPU, until the 2 h task limit killed it.
- Relaunch at 07:15 resumed at step 500 and hung the same way: no log line in 10+ minutes.

## Cause: an infinite loop on NaN, not a slow step
A `py-spy dump --locals` of the hung process shows it in `sinkhorn_ot` (swarm_nca.py ~553), called from
`swarm_loss` (swarm_nca.py:633) ← `train` (swarm_nca.py:933), with the local **`e: NaN`**.

The eps-scaling loop in `sinkhorn_ot`:

    e = max(float(Cd.max()), eps)        # NaN if the cost matrix holds a NaN
    while True:
        ...
        if e <= eps: break               # NaN <= eps is False
        e = max(e * 0.5, eps)            # max(NaN, eps) returns NaN

never terminates once the cost matrix `Cm` contains a NaN. So a NaN somewhere in the swarm state or
loss inputs at step ~500 shows up as a silent hang instead of an exception.

`latest.pt` (step 500) has no NaN/Inf in any tensor (rule, opt, sched, pool all checked), so the NaN is
produced during the step 500/501 forward pass. The fact that it reproduced right after resume suggests
it is deterministic from that state.

## Suggested fixes (code not touched; babysitter was told not to edit code)
1. Make `sinkhorn_ot` fail loud: `assert torch.isfinite(Cd).all()` or break on `not math.isfinite(e)`.
2. Find the NaN source at step ~500 with E1's settings (sticky labels / ratio cull / cooldown). Suspects:
   a 0/0 in a per-plan normalisation (an empty plan set, or zero mass `a`/`b`), or a degenerate switched sample.
3. Resume from `runs/swarm_e1/rule_00250.pt`, or lower lr / add grad clipping, if the rule itself is diverging.

Artifacts (gitignored): `Tools/NCA/runs/e1.out`, `Tools/NCA/runs/swarm_e1/{latest.pt,rule_00250.pt,rule_00500.pt,log.jsonl}`.
