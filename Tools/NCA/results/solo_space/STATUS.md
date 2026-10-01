# solo_space status

- Updated: 2026-10-01 15:43 UTC
- Newest snapshot: `rule_03000.pt` (step 3000 of 3000) -> `rule_latest.pt`
- Run: `gpu_run.py swarm --tag solo_space --set only=space ...` (see Tools/NCA/briefs/solo.md), warm start from G2

## Last 5 log lines

```
 2999 loss    6.534 T88  3.3s rev0.00 s5 | sp>sp   6.41 n280 d0
=== step 3000: grow every plan from a fresh seed and score it ===
grown from (rows) scored against each target (columns), Sinkhorn divergence, lower is closer:
after losing the majority (another 240 steps), scored against each target:
score 5387.1 (3/8 tests pass: own plan x4, switch x4); best so far 5384.6
```

## Final (training complete, 3000/3000)

- `rule_latest.pt` = `rule_03000.pt` (the final step, i.e. G2 + the space task vector).
- Step evals: 1000 -> score 5384.6 (3/8), 2000 -> 6375.0 (2/8), 3000 -> 5387.1 (3/8). Best published
  to `results/swarm_coevo_solo_space` is the step-1000 rule; step 3000 is within 0.05%.
- Step 3000, space grown from a space seed: Sinkhorn 9.12 against space (geometry only 2.72), the
  closest of any plan. It is a strong attractor: mass and time seeds also land nearest to space
  (26.99, 48.13); charge stays charge (40.69). `s` stayed 5-7 throughout.
- `probe.json`: space strike kills 66 of 200, cut 24.07, recovers to 13.46, heal 0.756.
