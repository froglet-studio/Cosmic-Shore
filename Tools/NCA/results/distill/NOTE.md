# distill — can a LOCAL rule reproduce `field`? (round 2, PROVISIONAL — updated as runs finish)

Branch `cece/swarm-x-distill`. Code: `distill_student.py` (the local student), `distill_teacher.py` (field as a
labeler on a shadow of any swarm), `distill_train.py` (BC + DAgger), `distill_finetune.py` (BPTT on swarm_loss),
`distill_publish.py`. Published here: the fully learned student after BC + DAgger iteration 6.

## What the student sees (and what it does not)

Each tadpole sees only itself, neighbours within World.R = 8 (kernel-weighted element / domain counts, mean
offset, mean offset of same-element neighbours, mean velocity, mean plan belief, separation vector, nearest
distance, neighbours' startle), the population census SwarmRule already perceives (headcount, element mix) and
a ship only inside its sensing range. No slot table, no assignment, no "nearest empty slot". Two DESIGNED local
morphogens give it a body frame: Z, a centre estimate refined by dynamic average consensus over the neighbour
graph (leaky, carries the tadpole's own motion), and CLK, an internal clock inherited from the parent. The plan
geometry lives only in the MLP weights (3×256 SiLU, 32 outputs: velocity, look deltas, lay gate + egg direction,
molt gate + target, plan belief, startle). Mechanics are designed (true-breeding eggs at r_bud, molt over 10 steps).

Sanity check: the teacher's actions executed through the STUDENT's mechanics pass field's own 7/8 on the stock
yardstick (sum 112.8 vs field 103.6), so the simulator is not the bottleneck.

## Results (swarm_eval, 16 tests, 3 samples each)

| stage | own plans | std switches | other switches | total |
|---|---|---|---|---|
| BC (teacher-driven data only) | 1/4 | 0/4 | (not run) | 1 |
| DAgger it 6, learned plan belief (**published**) | 3/4 | 1/4 | 1/8 | **5/16** |
| DAgger it 7, learned plan belief | 3/4 | 1/4 | 1/8 | 5/16 |
| (field, the teacher) | 4/4 | | | 13/13 feasible |

Published student, matrix (rows grown, columns wanted; rate over 3 samples):

```
from \ to      mass   space   charg    time
mass          1.00+   0.00-   0.00-   0.00-
space         1.00+   0.00-   0.00-   0.00-
charge        0.00-   0.00-   1.00+   0.00-
time          0.67+   0.00-   0.00-   1.00+
```

Stock yardstick (`summary.json`, rollout seed 7): **4/8**, summed wanted divergence 457 (field 104).
Strike probe: **heal −0.4 / −25.8 / −0.8 / −14.3** (it does NOT heal: the body keeps degrading after the cut).

(work in progress: census plan belief, genome features, fine-tune — see below when filled in)
