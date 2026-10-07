# Species 4: co-evolved predator and prey swarms (the arms race)

Branch `cece/swarm-x-arms`. Code: `Tools/NCA/arms_sim.py` (world + policies), `arms_seed.py` (generation 0),
`arms_train.py` (co-evolution), `arms_eval.py` + `arms_metrics.py` (measurement), `arms_plots.py` (figures).

__RESULTS__

## The setup

One 3D cell: a 200 u pond, a sixth of a game cell's radius, with a soft membrane. 120 prey tadpoles and 8 predators.
Units are game units (`Tools/Ecology/common/arena.py`: a tadpole is ~5 u, a vessel cruises at ~120 u/s). The step
is 0.1 s.

### The asymmetry, and why

| | prey (tadpole grazer) | predator (hunter) | why |
|---|---|---|---|
| population | 120 | 8 | Biomass pyramid: one predator needs many prey. 8 is a "pack" a player can count. |
| body radius | 2.5 u | 5 u | The predator is bigger. It is the readable silhouette a player tracks. |
| top speed | 60 u/s, sustained | 45 u/s cruise; 100 u/s BURST on stamina (3 s full, 8 s to refill) | The classic sprint-vs-endurance asymmetry. A predator that is not bursting cannot catch a healthy prey, so every catch is a visible burst: a built-in telegraph. |
| acceleration | 300 u/s^2 | 150 u/s^2 | Big bodies are sluggish. |
| turn rate | 10 rad/s (6 u turn radius at full speed) | 3 rad/s (33 u at burst) | Agility versus speed: the prey's counterplay is the juke. It keeps the race from being won on speed alone. |
| perception radius | 50 u | 80 u | Hunters see farther. Prey see predators in time to react but cannot see the whole pond. Both stay far below the 400 u cell diameter, so the policies are LOCAL. |
| eats | the food field (grazing) | prey (a catch moves the prey's whole mass into the predator) | |

A pursuit check calibrated this before training. A scripted pursue-and-burst predator against scripted fleeing prey
catches ~70-88 per minute per encounter, so the physics leaves room for either side to win.

### The economy (mass is conserved; nothing dies on a timer)

- **Food field**: a coarse 12^3 grid of grazeable mass in 6 patches. Prey graze it (Michaelis-Menten, shared out when
  a cell is crowded). It regrows only from a nutrient pool.
- **Metabolism**: both species burn mass, a base rate plus a speed^2 term. Burnt mass goes to the pool. In
  training the burn is only ledgered and charged in fitness. In the open-economy evaluation it is deducted.
- **Catch** = active mass transfer. A predator in contact with a prey catches it with probability
  `1/(1 + 0.3 * crowd)`, where crowd is the other prey within 10 u of the target. This is the confusion effect, a
  DESIGNED perceptual limit of the predator. The prey's mass moves into the predator, which then handles it for
  1 s (slowed, cannot catch).
- **Starvation** (open economy only): below half a birth mass, an agent dies and its body returns to the pool.
- **Birth** (open economy only): at twice a birth mass, an agent splits in two. Births are funded only by mass eaten.
- The audit `food + pool + prey + predators` is constant to float precision (see `eval.json` eco
  `mass_residual_max`).

**Fixed roster in training, open economy in evaluation.** Training encounters are 30 s with no births and no
starvation. Within 30 s a birth or a starvation is a rare, high-variance event, and an evolution-strategies
gradient needs a stable denominator ("share of the 120 prey caught"). The open economy, with births and
starvation over 5 minutes, is where collapse is tested: prey extinction, or predators starving out.

### The policies (LOCAL, one shared MLP per species)

Each policy is a 2-layer tanh MLP (32 hidden). The prey MLP has 31 inputs and 4 outputs; the predator MLP has 36
inputs and 5 outputs. Every vector is expressed in the agent's own body frame, a forward/right/up frame carried by
parallel transport, so there is no world "up".

- **Self**: speed; stamina and handling (predator only); the membrane's proximity and outward direction, only when
  the membrane is within R; the food at its own position and the food gradient.
- **Own kind within R**: count, mean offset, mean velocity, nearest offset, and the mean of the neighbours' 1-channel
  SIGNAL. The signal is the only secretion: a call each agent emits and its neighbours hear.
- **Other kind within R**: count, mean offset, nearest offset, the nearest's relative velocity. Predators also see
  the crowding at the nearest prey.
- **Outputs**: acceleration (3, body frame) and the signal. Predators also output a burst gate.

**Locality, declared: local.** There is no census, no global frame, and no cell centre unless the membrane itself
is within perception.

### Training

- **OpenAI-ES**, antithetic, one parameter vector per species, Adam. 4 CPU cores. torch is not installed here, so
  numpy runs everything batched over encounters.
- **Factorial blocks against the current opponent.** Four encounters share one seed: (pred ± e) × (prey ± d).
  Each species' antithetic difference is averaged over the other's two signs, so both species learn from the same
  rollouts.
- **Anti-cycling: an opponent pool.** Both policies are snapshotted every 10 generations. Each generation also
  plays antithetic pairs against pool opponents, chosen by **PFSP** (prioritised fictitious self-play, as in
  AlphaStar). A predator draws prey snapshots weighted by s(1-s), where s is its success. Prey draw the predator
  snapshots that still hurt them.
- **Fitness.**
  - Predator: catches minus 0.25 × burn, per predator.
  - Prey: minus the share caught, plus 0.5 × net mass grazed.

__REST__
