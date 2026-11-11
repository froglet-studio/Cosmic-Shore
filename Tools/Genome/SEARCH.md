# The genome search: what it does, and the A/B that chose it (2026-11-10)

`agenome/search.py` searches genomes for two things at once: distance to Amoebius (down) and compressed bytes (down).
Four pieces decide how it searches (`agenome/selection.py`). Each has a flag, and the flags that reproduce the
search as it was before 2026-11-10 (`--survival diamond --parents tournament --operators fixed --tuner random`)
replay an old run bit for bit (checked on the A/B's first seed).

| piece | default | the alternative | A/B verdict |
|---|---|---|---|
| survival | `--survival pareto`: non-dominated fronts inside the band (dist within `--span` 5% of the best, bytes under `--budget`), each front ordered by hypervolume contribution (SMS-EMOA); the best genome always first | `diamond`: the COMPLEXIFY / COMPRESS regime switch of the NCA lab's `pareto_complexity.py` | the default together with the CMA tuner: the only setup never worse than the old search on either target |
| tuner (the `--tune` share, 25%) | `--tuner cma`: a sep-CMA-ES emitter on 24 parameter bytes of the leader, a block every 8 generations | `random`: one or two byte tweaks around the best few | the best single piece near the seeds, the worst far from them: on only with the band |
| parents (60% from the population) | `--parents tournament` of 3 on dist | `lexicase`: epsilon-lexicase over the 8 feature groups, judge nets, critic and log bytes | a wash: won 1 of 3, worse anytime and held out |
| operators | `--operators fixed`: the regime's mix (growth: 15% crossover, else one or two mutations at 30% structural; PARETO shrinks a quarter) | `adaptive`: probability matching over param / struct / xover / shrink, credited by each child's gain over its parent | a wash: 2 of 3, but worse on average |

## The A/B

`bench/ab_search.py` makes a target whose optimum is known: a hidden genome's own renders (8 windows of 2 s, 0-14 s)
are the target statistics, 4 later windows (16-22 s) are the held-out check, and every arm starts from the same 11
seeds (eight random genomes and the three hand-built ones; the hidden genome is none of them). The fitness is the
statistics distance alone (no reels, critic or judge needed), so the hidden genome itself scores about 1.0. Budget:
40 generations of 24 children at mu 24, 971 renders per run, 30-frame clips, the same seed per arm.

- near target: `amoebius_v2` after five structural edits and fresh bytes on a fifth of its parameters (253 B, a
  violet scene with prism trails; scores 1.048 on its own windows, 1.068 held out). The seeds start close.
- far target: a random 5-node genome (79 B, a textured sphere on stripes; 1.009 on its own windows, 2.814 held
  out, its look drifts in time). No seed resembles it.

Columns: best dist at the budget (mean ± standard error over seeds), anytime (the best-so-far averaged over the run,
lower = got there sooner), hold (the final best's distance on the held-out windows), HV (hypervolume of the final
archive against one reference per target), bytes @5% (the smallest archive genome within 5% of the best dist).

### Forward ablation: the old search plus one piece (near target, 3 seeds)

| arm | best dist | per seed | anytime | hold | HV | bytes @5% |
|---|---|---|---|---|---|---|
| base (the search before 2026-11-10) | 1.779 ± 0.096 | 1.641 / 1.734 / 1.963 | 2.339 | 2.562 | 1798 | 273 |
| + pareto survival | **1.615 ± 0.048** | 1.566 / 1.569 / 1.710 | 2.266 | 2.404 | 2019 | 265 |
| + CMA tuner, 24 bytes | **1.596 ± 0.155** | 1.287 / 1.775 / 1.728 | **2.095** | **2.303** | 2014 | 295 |
| + CMA tuner, 6 bytes | 1.961 ± 0.184 | 1.777 / 2.145 / - | 2.405 | 2.754 | 1572 | 338 |
| + lexicase parents | 1.731 ± 0.102 | 1.881 / 1.775 / 1.538 | 2.398 | 2.625 | 1831 | 298 |
| + adaptive operators | 1.811 ± 0.174 | 2.067 / 1.479 / 1.886 | 2.381 | 2.665 | 1693 | 356 |

### Both targets: each winner alone, and the two together (3 seeds each)

| arm | near: best dist | near: anytime | far: best dist (per seed) | far: anytime | far: bytes @5% |
|---|---|---|---|---|---|
| base | 1.779 | 2.339 | 2.465 (2.349 / 2.822 / 2.223) | 3.271 | 199 |
| + pareto survival | **1.615** | 2.266 | 2.853 (2.082 / 3.067 / 3.410) | 3.288 | 204 |
| + CMA tuner | **1.596** | **2.095** | 3.042 (3.191 / 3.258 / 2.678) | 3.411 | 328 |
| pareto + CMA (the defaults) | 1.677 | 2.197 | **2.433** (1.771 / 2.720 / 2.808) | **2.909** | 229 |

Near the seeds (the near target starts in the right basin) every piece that exploits the leader pays, and each
piece alone beats the pair. Far from the seeds the CMA tuner alone is the worst arm on every seed (it spends a
quarter of the renders polishing a leader of the wrong structure), and the band alone is mixed; together they are
the best arm there, and the only setup that is never worse than the old search on either target. The real target
can be near or far from the seed bank, so the defaults are the pair: the band supplies the exploration that makes
the tuner safe.

### Next
A steward loop on a follow-up branch: an evidence pack per run (which edits made each improvement, what the leader's
distance is made of), an insight ledger, and a scheduled pass that turns what worked into tested changes. Its first
test: CMA blocks of whole operators' parameters beat 24 random bytes for the tuner alone near the seeds (1.537
against 1.596), but were a wash inside the pair, so random blocks stay.

## What the logs showed (and what was fixed before the table above)

The first run of all four pieces together lost to the old search (2.01 against 1.64 on seed 1). The per-operator
lines every generation now logs (`ops`: children, survivors and mean gain over the parent; `best_op`; `aos`; the CMA
state) showed why, and the runs above use the fixes:
- **Adaptive operators credited by survival** paid `shrink` for filling the compact end of the band while the best
  distance stalled (shrink rose to 31%). The credit is now each child's relative gain over its parent's dist. Even so
  it drifted to crossover (45%): a crossover of a poor archive parent with a good donor shows a large gain over its
  parent without moving the leader. Off by default.
- **The CMA tuner halved its step after every block**, including blocks cut short because a new structure took the
  lead: the step fell from 12 to 1.7 bytes in 40 generations and the emitter stopped mattering. Only a block that ran
  its course now judges the step.
- **6-byte blocks lost to 24-byte blocks**, against the expectation that sparse steps suit bytes. A likely reason,
  not yet tested: a genome's bytes act in whole-operator groups (a colour's hue, saturation and value; the parameters
  of one shape), which small random blocks split.
- **The diamond never left COMPLEXIFY** in any of the 14 diamond runs (40 generations, patience 8): in these
  budgets it is a pure distance climber. So pareto survival (with its quarter of shrinks) beat a search that spent
  everything on distance, at distance: keeping compact near-leaders alive helps fidelity as well. Why is the next
  thing to test (are compact near-leaders better parents, or do shrinks remove nodes that hurt?).

## Re-running it

    python3 bench/ab_search.py target OUT                    # hidden genomes, target stats, seeds (a minute)
    python3 bench/ab_search.py run OUT --arm base --target near --seed 1      # about 5 minutes on 2 cores
    python3 bench/ab_search.py report OUT

Arms: `base`, `new` (all four pieces), the forward arms `base+pareto`, `base+lexicase`, `base+aos`, `base+cma24`,
`base+cma6`, the combined `pareto+cma` and `pareto+cma+aos`, and the leave-one-out arms `new-survival`,
`new-parents`, `new-operators`, `new-tuner`. Extra search flags go after `--` with a `--tag` for the run name.
The synthetic A/B measures search efficiency on the real renderer and features; the islands' fitness adds the critic
and the judge, and an island A/B (`SURVIVAL=diamond TUNER=random bash island.sh <name>` beside a default island) is
the check on the real target.

## Where the pieces come from
- Hypervolume survival in a region of interest: SMS-EMOA (Beume, Naujoks and Emmerich 2007) and preference-based EMO;
  Eureqa and age-fitness Pareto optimisation (Schmidt and Lipson) kept the same front, PySR keeps a per-complexity
  hall of fame.
- Parameter tuning inside the structural search: CMA-ME emitters (Fontaine et al. 2020, pyribs), PySR's BFGS on
  constants, Operon's Levenberg-Marquardt; sep-CMA-ES (Ros and Hansen 2008) is `agenome.tune`'s optimiser.
- Epsilon-lexicase (La Cava, Spector and McPhee 2016) leads symbolic-regression benchmarks when the cases are many
  data points; here the cases are 8 feature-group distances, and it did not help.
- Adaptive operator selection: probability matching and adaptive pursuit (Goldberg 1990; Thierens 2005).
