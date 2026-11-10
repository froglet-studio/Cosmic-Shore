# The search's insight ledger

What the genome search has taught us, one claim per entry, each with the evidence for it, the test that decided it
and the verdict. The steward loop (`agenome/steward.py`, SEARCH.md) adds entries; a claim that lost stays here so
nobody retries it blind. Open claims are the next experiments.

Verdicts: **kept** (it is a default now), **killed** (it lost its test; it stays one flag away), **open** (a
hypothesis with a prediction and the test that would decide it).

## L1 kept: survival in a band beats the regime switch, at fidelity as well as compactness (2026-11-10)
- Claim: keeping the best genome and the smallest genomes within 5% of its distance alive together (hypervolume
  order inside the band) beats the COMPLEXIFY/COMPRESS diamond.
- Evidence: synthetic A/B (SEARCH.md), near target, 3 seeds, 971 renders each: best 1.615 vs 1.779, every seed;
  held out 2.40 vs 2.56; front area 2019 vs 1798; smallest genome near the best 265 vs 273 B. FAR_PARETO
- Note: the diamond never compressed in 14 runs (patience 8 in 40 generations), so the old search was a pure
  distance climber, and the band still beat it at distance.

## L2 kept: tune the leader's bytes with sep-CMA-ES on 24-byte blocks, inside the loop
- Claim: spending the tune share on a sep-CMA-ES emitter (24 random parameter bytes of the leader, 8 generations a
  block) beats one-or-two-byte random tweaks around the best few.
- Evidence: near target, 3 seeds: best 1.596 vs 1.779 (2 of 3 seeds), anytime 2.10 vs 2.34, held out 2.30 vs 2.56.
  Per child, CMA children gained 0.018 over their parents against 0.0098 for growth (base+cma24, seed 1). FAR_CMA
- What broke it first: a step rule that halved after every block, including blocks cut short when a new structure
  took the lead, took the step from 12 to 1.7 bytes in 40 generations. Only a block that ran its course judges the
  step now.

## L3 killed: small (6-byte) CMA blocks
- Expected: sparse steps suit byte genomes, where most bytes do little. Lost both seeds (1.78, 2.15 vs 1.64, 1.73).
- See L7 for the hypothesis this leaves.

## L4 killed (for this fitness): epsilon-lexicase parents over the feature groups
- Won 1 of 3 seeds; worse anytime (2.40 vs 2.34) and held out (2.63 vs 2.56).
- Where it does pay: the lab's feature toy, whose cases are separable subproblems (one per target feature): 497
  evaluations to the ideal against 1,066 with a tournament. The genome's 8 group distances are parts of one look,
  not separate subproblems.

## L5 killed: adaptive operator selection, under two credits
- Credited by survival it paid `shrink` for filling the compact end of the band (31%) while the best stalled.
- Credited by gain over the parent it drifted to crossover (45%): a poor archive parent crossed with a good donor
  shows a large gain over its parent without moving the leader. Won 2 of 3 seeds, worse on average (1.81 vs 1.78).
- Open variant: credit against the leader (does the child beat the current best, or enter the band?) instead of
  against its own parent.

## L6 open: why the band helps fidelity
- Hypotheses: (a) compact near-leaders are better parents (fewer harmful nodes to inherit); (b) the quarter of shrink
  children removes nodes that hurt the look.
- Test: band arms `--span 0` / `0.05` / `0.10` with `--shrink 0` and `0.25` (synthetic A/B, 3 seeds). (a) predicts
  the gain survives `--shrink 0`; (b) predicts it does not.

## L7 open: bytes act in whole-operator groups
- Hypothesis: 24-byte blocks beat 6-byte ones because a genome's bytes interact within an operator (a colour's hue,
  saturation and value; one shape's radius and thickness), and small random blocks cut those groups apart.
- Test: CMA blocks drawn as whole operators' parameters up to 24 bytes (`--cma-blocks op`, 3 to 7 operators a
  block) against 24 random bytes (`bench/ab_search.py --arm base+cma24op` against `base+cma24`, near target,
  3 seeds). Predicts operator-aligned blocks win at equal renders.

## L8 killed (too small to build): crossover duplicates subgraphs the genome already has
- Seen: the steward's first pack (base+cma24, seed 1): the leader carries the same 5-node texture tree twice (nodes
  0-4 and 24-28, identical bytes), because the subgraph crossover copies the donor's subtree even when an identical
  one is already in the genome.
- Hypothesis: sharing identical texture and shape subgraphs (pure functions; agents and HUD are not, two identical
  spawns are two flocks) shrinks genomes at the same render.
- Measured before building: of 1,084 distinct genomes in the A/B's archives only 42 (4%) carry such a duplicate,
  and sharing it saves a median of 3 compressed bytes (0.7%), at most 10: zlib already turns the copy into a
  back-reference. Not worth a change to `prune()`. What would move bytes is the representation itself (a shared
  dictionary, or recurring subgraphs promoted to operators), not deduplication.

## L9 open: the band and the CMA tuner do not stack on the near target
- Seen: each alone beat the old search on the near target (1.62 and 1.60 against 1.78), but together they scored
  1.77 and 1.81 on seeds 1 and 2, behind either alone and level with the old search; on the far target the pair
  beat the old search on seed 1 by a quarter (1.77 against 2.35).
- From the steward's pack (pareto+cma, seed 2): colour is 34% of the leader's distance and barely moves in
  improving children (colour -0.10 against texture -0.84 on average); CMA blocks keep raising both perlin
  frequencies (51 -> 107-130); the largest relative gains are growth children of far-off archive parents.
- Hypotheses: (a) the lineage cap leaves the leader's lineage 6 survivors, and the band spends some on compact
  variants while the emitter needs the leader's neighbourhood; (b) the pair over-exploits one basin early.
- Tests: the pair at `--lineage-cap 0.5`; the pair with `--span 0.02`; each alone on the far target (running) to
  choose the defaults.
