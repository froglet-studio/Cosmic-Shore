# E5 run note — training hangs in `sinkhorn_ot` (no code changed)

Run: `gpu_run.py swarm --tag e5 ... --set scale_inv=1 --set learned_lay=1` on CPU, 4 threads, ~5 s/step.

## Symptom
The run silently stops logging while the process keeps burning 100% CPU. Seen twice:
- launch 1: last log line step 320 (06:41 UTC), hung ~90 min until the 2 h job limit killed it;
- launch 2 (resumed from the step-250 checkpoint): last line step 370 (08:25 UTC), hung ~60 min.

`py-spy dump` of the hung process, sampled repeatedly, always shows:

    sinkhorn_ot (swarm_nca.py:572-573)  <- _divergence (627) <- swarm_loss (656) <- train (956)

## Cause (by reading the code)
`sinkhorn_ot` runs an eps-scaling loop that exits only on `e <= eps`:

    e = max(float(Cd.max()), eps)
    while True:
        ...
        if e <= eps: break
        e = max(e * 0.5, eps)

If the cost matrix contains NaN, `max(nan, eps)` returns NaN and `nan <= eps` is never true; with
Inf, halving Inf stays Inf. So any NaN/Inf in a cost matrix produces an infinite loop rather than a
crash. The trigger is presumably new in E5 (`scale_inv` rescales the swarm to the plan's size: a
degenerate swarm with ~zero spread, or similar, would divide by ~0). Not confirmed — I did not edit
code to instrument it.

## Suggested fix (for the morning, not applied)
Guard the loop: bail/skip the sample if `not torch.isfinite(Cd).all()`, cap the eps-scaling
iterations, and find where `scale_inv` can produce a non-finite scale (clamp the spread).

## Effect on this run
Checkpoints are every 250 steps (`snap_every`). Progress only persists when a launch survives past
the next multiple of 250. The babysitter now relaunches automatically when the log goes stale for
more than 8 min, so the run keeps retrying from the latest checkpoint until 15:00 UTC.
