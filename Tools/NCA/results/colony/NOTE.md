# Colony brain — research note (direction "colony", branch `cece/swarm-x-colony`)

**Status: in progress (preliminary, 16:30 UTC). Final numbers replace this section by 23:00.**

## What was built (`Tools/NCA/colony_nca.py`)

One shared, slowly-evolving **colony state** per swarm on top of the G2 per-tadpole rule:

- Each step a GRU (H=32) reads a pooled, domain-neutral summary of the living tadpoles: headcount,
  element counts and shares, RMS radius, gyration-tensor shape (3 eigenvalues), mean hidden state
  (channels 12-25); optionally (`--pool-kind attn`) two learned attention queries over tadpoles.
- It broadcasts a **code** (K=8) that every tadpole's rule is conditioned on (the rule's input layer
  grows 8 zero-initialised columns — at step 0 the model is bit-identical to G2, `selftest` proves it),
- and a per-element **breeding bias** added inside every tadpole's laying gate (zero-initialised) —
  the colony, not 280 tadpoles separately, can decide which elements breed.
- Variant `vote`: a soft **plan vote** bottleneck, p = softmax over 4 plans, code = p @ 4 learned plan
  codes; `w_vote` supervises it with the sticky plan label. Variant `free`: code = tanh(W h).
- The colony state lives on the `Swarm` object (`col_h`, `col_vote`, `col_lay`), so the shared
  `swarm_nca.rollout` and `swarm_probe.probe` run it unchanged; reset when `clock == 0`.
- Training: G2 config (sticky labels, learned laying gate, body floor 76/20, overflow penalty) +
  `w_mix 30` (element-share error vs the current plan) + switch culls that include the eval's own
  "excess" mode (1/3 each ratio/tie/excess). Colony parameters at lr 2e-3, rule at 5e-4.

## Results so far (strict `tests_passed`, seeds 7,8,9,10)

| model | tests per seed | mean | own plan | switches |
|---|---|---|---|---|
| G2 (warm start) | 5,4,4,4 | 4.25 | 16/16 | 1/16 |
| **V @600** (vote, mean pool, w_vote 1) — published | 5,5,5,5 | 5.00 | 16/16 | 4/16 (jelly→puffer 3/4, puffer→dragonfly 1/4) |
| V @900 | 5,5,5,4 | 4.75 | 16/16 | 3/16 |
| **C** control @600: same loss/culls, NO colony (K=0, no breeding bias) | 4,4,5,4 | 4.25 | 16/16 | 1/16 |
| **F** free latent @600 (mean pool) | 5,5,4,6 | 5.00 | 16/16 | 4/16 (jelly→puffer 3/4, puffer→dragonfly 1/4) |
| **A** vote + attention pooling @600 | 4,5,4,4 | 4.25 | 16/16 | 1/16 |

Published here: `rule.pt` (V@600), `summary.json`/`rollout.json` (seed 7, 5/8), `probe.json`,
`vote_trace.json` (the colony's vote and breeding bias every 10 steps through a switch rollout).

The control C shows the gain comes from the colony, not from the loss/cull changes (w_mix, excess
culls) that came with it. The vote bottleneck and the free latent score the same at 600 steps; the
vote's advantage is that its decision is readable (and supervisable). Attention pooling over
tadpoles did not help (4.25, no better than the control): the decision needs counts and shares,
which the mean summary already carries exactly.

## Findings

1. **The colony decides, cleanly and with hysteresis.** `vote_trace.json`: the vote sits at
   p≈1.0 on the own plan for 240 steps; 20–60 steps after the cull it flips to the new majority's
   plan and stays there (puffer→dragonfly flips at ~t+60, jelly→puffer at ~t+30).
2. **But it decides too late.** The refill after a cull is a burst: the swarm goes from ~150 back
   to 280 tadpoles within ~10 steps — *before* the vote flips — while the OLD plan's breeding bias is
   still active. In training the culls happened before the no-gradient pre-roll, so that burst was
   never inside the backpropagated window. Fix being trained now (V2): cull right before the BPTT
   window (`--late-switch 1`), w_vote 2.
3. **Two of the yardstick's four switch tests can ask for a plan that is not the majority.** The
   eval's cull ("excess") only cuts the old majority to one below the target element; any third
   element that was already larger stays larger. With the dragonfly's own plan mix (C2 M3 S18 T53),
   culling Time leaves Space (≈18–40) far ahead of Mass (≈3–13), so "dragonfly → whale" wants a whale
   from a Space-majority swarm — by the brief's own rule ("element ratios pick the plan") the right
   answer there is the jellyfish, and every model so far (G2 included) grows a jellyfish. Same for
   whale → jelly whenever the grown whale has more Charge or Time than Space. Realistic ceiling
   under the rule is therefore ~6–7/8, not 8/8, unless the grown composition happens to rank the
   test's element second. Not gamed: the training label is always the true majority.

## Probe (V@600, seed 11)

Strike removes 50–75 of 280; the swarm refills to 280 within ~20 steps but the shape does not heal:
heal fraction mass 0.13, space −0.28, charge −2.97, time 0.28 (G2: 0.04, n/a, −0.53, 0.55).
