# solo_time status

Updated: 2026-10-01T14:55:19Z  
Step: 2300 / 3000 (running)  
Rule snapshot: Tools/NCA/runs/swarm_solo_time/rule_02300.pt

```
 2290 loss   13.543 T91  3.5s rev0.00 s5 | ti>ti  13.75 n280 d0
 2300 loss   15.667 T61  3.0s rev0.00 s6 | ti>ti  17.60 n280 d0
 2310 loss   13.120 T90  3.3s rev0.00 s5 | ti>ti  11.50 n280 d0
 2320 loss   14.319 T62  2.7s rev0.00 s7 | ti>ti  18.66 n280 d0
 2300 loss   14.564 T71  3.7s rev0.00 s7 | ti>ti  16.96 n280 d0
```
    === step 1000: grow every plan from a fresh seed and score it ===
    grown from (rows) scored against each target (columns), Sinkhorn divergence, lower is closer:
    after losing the majority (another 240 steps), scored against each target:
    score 3372.3 (5/8 tests pass: own plan x4, switch x4); best so far 1000000000000000000.0
    === step 2000: grow every plan from a fresh seed and score it ===
    grown from (rows) scored against each target (columns), Sinkhorn divergence, lower is closer:
    after losing the majority (another 240 steps), scored against each target:
    score 6392.1 (2/8 tests pass: own plan x4, switch x4); best so far 3372.3
