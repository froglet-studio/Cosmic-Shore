# E3 overnight run — hang note (babysitter, 2026-10-01)

**Symptom:** training stops logging but the process stays at ~390% CPU forever.
Seen twice:

1. First launch (05:15 UTC): silent after step 200 for ~1 h (no checkpoint yet, restarted from warm start).
2. Second launch, resumed from step 250: silent after step 450 (10:28 UTC). Diagnosed with `py-spy dump --locals`.

**Cause (diagnosed, not fixed — no code was edited):** `sinkhorn_ot` in `Tools/NCA/swarm_nca.py`
(the `while True` eps-scaling loop, ~line 549) never exits when the cost matrix contains NaN/inf:
`e = max(float(Cd.max()), eps)` yields `nan` (Python `max(nan, x)` returns `nan`), so
`if e <= eps: break` is never true and `e = max(e * 0.5, eps)` stays `nan`. py-spy showed `e: NaN`
inside `sinkhorn_ot <- swarm_loss <- train`.

**Precursor:** the step logged just before the hang had every plan's population at `n 16`
(collapse from ~280), i.e. the swarm state diverged and fed NaN into the OT cost.

**Suggested fixes (for the human):** guard the loop (`if not math.isfinite(e): raise/skip`),
cap its iterations, and/or skip the step / reset the pool sample when `loss` or positions are
non-finite. The E3 loss weights (w_mix=20, w_con=10) may be destabilising training.

**Workaround in use:** kill the hung PID and relaunch; it resumes from the last 250-step
checkpoint and, because CPU training is not bit-deterministic, may get past the bad step.
