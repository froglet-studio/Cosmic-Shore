# Research portfolio, round 1 (1 Oct 2026)

The user asked for breadth: several distinct approaches to a fun, element-driven swarm creature,
run in parallel, with the best ideas from each feeding the next round. Each file here is the brief
one autonomous session works from, on its own branch (`cece/swarm-x-<tag>`), reporting to
`Tools/NCA/results/<tag>/NOTE.md`. All are scored by the same yardstick: `swarm_nca.rollout` +
`tests_passed` (8 strict tests) and `swarm_probe.probe` (a vessel strike and how well it heals).

| Tag | Idea |
|---|---|
| hgrid | a coarse 3D cellular automaton steering the tadpoles (CA over collision automaton) |
| colony | a shared colony brain: one slow latent decides the plan and broadcasts it |
| evo | gradient-free search (ES / GA), including adding and removing behaviours |
| field | designed attractor fields + boids: the game-ready baseline, morphing switches |
| play | the learned rule hardened by strikes and a predator: fun to fly through |
| meta | relaxed rules: metamorphosis and richer actuators so a swarm can steer its own mix |

The gradient-trained runs (E, F, G, H series) continue alongside; see the parent README.
