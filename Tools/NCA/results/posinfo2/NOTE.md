# posinfo2 - the learned rule carried to the full scorecard

**Headline (published checkpoint = run D step 300, `rule.pt`, `posinfo2:` spec in swarm_eval):**

| | seed 7 | seed 23 | seed 41 | held-out 1000 |
|---|---|---|---|---|
| 16-transition yardstick (loss-8 bar, 3 samples) | **13/13** | 12/13 | **13/13** | **13/13** |
| own plans whale / jelly / puffer / dragonfly | 1.76 / 1.65 / 1.83 / 2.68 | 1.65 / 1.62 / 2.01 / 2.72 | 1.57 / 1.82 / 1.63 / 2.70 | 1.81 / 1.80 / 1.94 / 2.48 |

(3 transitions are n/a at every seed: the grown body holds < 2 of the target element.)
**51 of 52 feasible transitions under the bar over four seeds.** The only miss is dragonfly -> jellyfish at seed 23
(10.4, 1/3 samples); it passes at 7 / 41 / 1000 at 7.0-7.5. Every switch reshapes: switched losses 1.6-7.4.

Scorecard (`scorecard.json`, `python Tools/NCA/scorecard.py --model posinfo2:Tools/NCA/results/posinfo2/rule.pt --seeds 7,23,41 --locality mixed`):
- ACCURATE 13 / 12 / 13 of 13 (worst seed 0.92)
- LOSSLESS **yes: 0 self-inflicted deaths** over a grow + every standard switch (~264k tadpole-steps)
- PERFORMANT 5.2 ms/step @ grown body, 1 thread, Python/torch prototype (posinfo/G2 MLP: 192 hidden, ~0.2M params)
- EMERGENT organic band **in** (jerk_rel 2.05, osc 0.04, stuck 0.009, planar excess 0.105); locality declared **mixed**
  (neighbour perception + a body frame - centroid / principal axes - and an element census computed over the whole swarm)
- old 8-test rollout: 7/8 (`summary.json`). Its miss is the old yardstick's known quirk, not a reshape failure:
  `lose_majority(to=Mass)` cuts only the Time majority, so SPACE (18) - not Mass (3) - takes the majority, and the
  swarm correctly becomes a jellyfish (n 90). The fair-cull yardstick (swarm_eval) passes that transition.
- probe (vessel strike removes ~a third): heal 1.05 / 0.95 / 0.44 / 0.74 (whale / jelly / puffer / dragonfly); every
  body regrows to its exact headcount; recovered losses 2.1 / 2.2 / 3.6 / 5.7. (posinfo: 3.2-6.9 own, no switches; G2 heal
  much weaker.)

## What it is

`posinfo2_rule.PosInfo2Rule` = posinfo's learned rule (G2 + body-frame positional inputs, BPTT) with three DESIGNED parts
around it. The network still decides where every tadpole swims (the sorting / the shape); the controller decides only
who is laid and who molts into what - the round-3 cross-family pairing.

1. **Death masked (lossless).** The rule's output on the DIE channel is zeroed, so a tadpole can never decide to die.
   posinfo needed learned death; without it (and with the old quota) its dragonfly fell to 12.
2. **Scaled quota (homeo 5).** posinfo's quota was the plan's (element, domain) table at its NATIVE headcount. Nothing
   dies and a domain breeds true, so after a big -> small switch (whale -> dragonfly) a domain holds more tadpoles than the
   plan's slot for it; its surplus had no legal element to molt into and clung on as debris (+elem 6 -> 16; exactly the
   game port's finding 17). The loss is a divergence between DISTRIBUTIONS, so the fix is lossless and free: scale the
   plan table until every living domain fits, molt the surplus into that larger quota, lay the rest. After a switch the
   composition is now exact. **Orphan domains (homeo 6):** a 3-domain dragonfly becoming a 2-slot jellyfish leaves a
   domain the plan has no slot for; it now molts toward the plan's overall element mix (19.4 -> 12.4 on that switch
   before retraining; it is the residual hard case because a third domain the plan does not have costs ~3-4 of domain loss
   no rule can remove - domain breeds true).
3. **Majority guard (homeo 7).** A fair cull can leave the new majority ahead by ONE tadpole in a remnant of ~20
   (jellyfish -> dragonfly: Time 7, Space 6, Charge 6, Mass 2). The homeostat re-reads the majority every step, and three
   leaks flipped the plan back (loss 63): its own molting/laying drawing another element level, it molting HATCHED
   majority tadpoles out (Time in the domain whose slot wants little Time), and EGGS laid before the cull (the cull counts
   only hatched tadpoles) hatching past the lead. The guard: no molt or egg may bring a non-majority element level with the
   majority; the majority is never thinned while its lead is slim; such eggs re-form as the majority element before they
   hatch (a molt, not a death). It only changes the ORDER things are made in, never the final quota.

Training (`posinfo2_train.py`, reusing swarm_nca.train unchanged): warm start from results/posinfo/rule.pt; switches ON
(p_switch 0.35, sticky plan, cooldown 60) with 60% of training switches made by the yardstick's FAIR cull (`--fair`,
monkey-patching sn.lose_majority by module name); the molting homeostat ON during training (meta's m3 lesson).
Runs: A 600 steps (homeo 4, 6/13) -> B 800 (homeo 5 + fair, 9-11/13) -> C 800 (homeo 6, lr 2e-4 decayed; C800 13/13,
13/13, 12/13 under homeo 7) -> D 300 (homeo 7, lr 1e-4) = published. ~2,500 steps total, ~2.8 s/step on 4 CPU cores.
Per-snapshot yardstick + death counts: evals_a / evals_b / evals.jsonl (C and D logs in runs/, gitignored). Every
snapshot was lossless (0 deaths). Checkpoint choice by `posinfo2_select.py` over seeds 7/23/41 (runs/p2_select*.jsonl).

## Feel, next to evo (results/hgrid2/feel_calibration.json, "beautifully organic")

| | speed | jerk_rel | coherence | jitter | phase | stuck | osc |
|---|---|---|---|---|---|---|---|
| evo | 0.182 | 1.93 | 0.117 | 2.53 | 0.46 | 0.000 | 0.058 |
| posinfo2 | 0.097 | 2.05 | 0.175 | 2.71 | 0.20 | 0.009 | 0.040 |

Same family of motion as evo (brownian-organic jerk, low coherence, high jitter - it is still the G2 lineage), about half
evo's speed, more in-phase, slightly fewer reversals. It sits inside the band but near two of its edges: C800 (38/39) fell
out on stuck 0.022 (bar 0.02), and planar excess is 0.105 (bar 0.15) - the bodies are flatter than evo's (planar_frac 0.50
vs 0.14) because they actually match their plans now (a jellyfish bell IS a surface).

**What a player would see:** a loose, shimmering cloud of tadpoles that never marches in step, which settles into a
recognisable whale / jellyfish / pufferfish / dragonfly with each element and team sorted into its region. Eat enough of
one element and nobody dies on a timer: misplaced members visibly change element (molt) where they hover, the body grows
new members (switches into a small plan grow the body - headcount is not a goal), and the cloud drifts and re-sorts into
the new animal over ~200 steps. Strike it and it closes the wound and regrows to its exact headcount. The flips are calm,
not churny (the guard stops back-and-forth).

## What failed / honest caveats

- **Dragonfly -> jellyfish is the residual** (7.0-10.4): the third domain has no slot in a 2-domain plan. Ways out are a
  design call: let a jellyfish carry a third (team) region, or score that transition on 2-of-3 domains.
- **The controller changed after most training**: C was trained under homeo 6 and evaluated under homeo 7; D (published)
  was trained 300 steps under homeo 7. The guard rarely acts in ordinary growth, so this mattered little, but it is a
  designed, swappable part.
- **Locality is mixed, not local**: the positional inputs (centroid, principal axes, per-domain/element centroids) and the
  homeostat's census are swarm-wide reductions. In the game they are O(N) per step per swarm (cheap, the Cell already tracks
  fauna) but they are not "a tadpole reads only its own position".
- **Switches into a small plan grow the body** (dragonfly after a whale ~ 1.3x its native 76). Fine under "headcount is not
  a goal" and the divergence loss; a game may want a cap.
- Molting changes a living tadpole's element (relaxed constraint, allowed by the brief); molts are visible events.
- Cost 5.2 ms/step in the Python prototype; it is an MLP per tadpole (posinfo's 192-wide), so a C# port should land in the
  same order as the other learned ports, not the field/grid ports.

## Recommendation for the next round

**Yes - this is the learned rule to try as the second scene test.** It is the first learned/evolved rule to pass the
whole yardstick at the bar (51/52 feasible over four seeds, own losses under 3 everywhere), it is lossless, it is in the
organic band with evo-like motion, and its composition controller is ~150 lines of designed, port-ready Python.
Concretely:
1. Port `PosInfo2Rule` = G2 MLP + the positional block (posinfo_rule.posfeat) + the homeo-7 controller
   (`scaled_quota` + `guarded_homeo_lay` + `egg_guard`). The controller ports directly onto hgrid2's game port, which
   has the same debris problem (finding 17) - homeo 5's scaled quota is a drop-in fix for it there too.
2. Combine with hgrid2: feed hgrid2's coarse class-deficit grid as an extra PERCEIVED input instead of the swarm-wide frame,
   to make the rule genuinely local (the scaled quota and the guard are already per-swarm censuses the Cell tracks).
3. Decide the 3-domain -> 2-slot rule (the only remaining miss) as a design question, not a training one.
4. Raise speed toward evo's (it is ~half) with a small w on motion energy if the lead finds it sluggish in the viewer.

Files: posinfo2_rule.py (rule + homeostats), posinfo2_train.py, posinfo2_select.py, posinfo2_publish.py,
posinfo2_quick.py. Artifacts here: summary.json, rollout.json (sn.pack, as the publisher), probe.json, eval16.json (seed 7),
scorecard.json, feel.json, rule.pt, log.jsonl (run D), evals_*.jsonl, config.json, posinfo2.json.
