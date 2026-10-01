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
