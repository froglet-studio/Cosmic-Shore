# Swarm Vessel Lab — round 1 (2026-10-19)

**Live page:** https://claude.ai/artifact/Q7BgQYrT9xq5kmNtqCudmw (private to Garrett until shared)
**Source:** `Docs/Studios/SwarmVesselLab/src/` → `python3 Docs/Studios/SwarmVesselLab/build.py` → `Docs/Studios/SwarmVesselLab.html`
**Verify:** `node .claude/skills/labmaker/verify_lab.cjs Docs/Studios/SwarmVesselLab.html` (PASS, desktop + phone)

## Why it exists

The Termite came back as a commander queen with a card deck, and playing it was the first time the
studio held a vessel that *is* a swarm. The question this lab answers is wider than the Termite:
**what is a swarm vessel made of, and which combinations are fun?** The aim is to find enough fun moments
to cut several vessels out of one kit. Each vessel would control a different kind of swarm, play
differently and fill a different team role.

The lab's answer to "made of" is four axes, each swappable while the others hold:

| Axis | What it is | Count |
|---|---|---|
| **Body** | a species: how members move (genes: separation, alignment, cohesion, wander, orbit, agility, leash) and the ONE verb they do to the world | 19 |
| **Control** | how the pilot talks to the swarm. It may only write a focus point, an order, a mood, a bait, a throw point or a selection — never a member's position | 14 |
| **Economy** | what pays for members and cards | 7 |
| **Meta** | the layer above a match | 7 |

So the whole design space is 19 × 14 × 7 × 7 = 13,034 builds with **no code per pair**.
L-STU-115 records why that worked: the control vocabulary is narrow and every body reads only it.

## Sources (and what is proposal)

Each item carries a `source` tag in the page (`shipped` / `notion` / `ecology` / `lab`).

- **Shipped:**
  - the Termite's six cards and pheromone tank (numbers read from `TermitePheromoneAction.asset`, `TermiteDroneTuning`, `TermiteDeckExecutor`);
  - the tadpole boid;
  - the locust's phase rule (`Tools/Ecology/bestiary/species/locust.py`: crowding → gregarious; march 135 u/s; crowd radius 40).
- **Notion (read-only):**
  - the School (six fish bound by plasma) and the D-pad fauna orders;
  - Shepherd / sheepdog herding and Boldt's tiered fuel;
  - the Shuffle octopus and the Angler's fake crystal;
  - Beast Tamer bait and the 2024 termite commander ("distributing termites between cell and queen");
  - the NCA backlog's aggression knob (B5), plus slime-mold and siphonophore swarm concepts.
- **Lab proposals:** ants, pigeons, rats, bees, wasps, starlings, bats, fireflies, mosquitoes, crows,
  piranhas and spiders. The docs name few of these swarms, so their verbs are this round's guesses at
  each animal's one sentence.

## The toy box (37 fun moments)

Each toy is one hypothesis about a fun moment, a build to test it in, what to try, and the number that
shows whether it happened. **Keep / Cut / Merge into a vessel** on each toy writes to the shared
decision log.

| Body | Toys (the moment) |
|---|---|
| Termite | Mound rising (their mass walks home and becomes yours) · Paint a highway · Cell or queen? (2024 split) · Clash commander (cards only) · Workers, soldiers, scouts (castes) |
| Ant | The trail recruits (stigmergy) · Starve or thrive (food web) · Roguelike colony (draft) |
| Locust | The phase snap · Herd the plague (sheepdog) · Survival of the swarm (evolution) |
| Pigeon | The homing wheel · Message relay · Possess the lead bird |
| Rat | Through the wall (gnaws armour) · Pied Piper (rhythm) · Breeding explosion |
| Bee / Wasp | Claim the meadow · Each sting is a life · The nest boils over · Tiered fuel deck · Three into one (merge) |
| Starling / Bat / Firefly | Murmuration decoy · Shape the cloud · Echo reveal · Sync flash (Kuramoto) |
| Mosquito / Crow / Piranha | Throw the cloud · Mob and pickpocket · Crystal heist · Blood in the water |
| Slime / Spider / Siphonophore / School / Tadpole / Octopus | Grow a road · Web the lane · Living wall · Plasma ring · Plasma lasso · Metamorphosis · Shuffle arms |

## Measured: the role fingerprint

Every row is the AI commander playing that build in each element minigame (15 s, seed 1, 10 Hz).
Time is gates × 10 plus seconds to spare. Space is hostile prisms destroyed. Mass is your prisms
gained. Charge is petals taken − lost + 3 per knockout. **Hull only** is the same arena with no swarm.

| Build (default) | Time | Space | Mass | Charge | Reads as |
|---|---|---|---|---|---|
| Hull only | 20 | 0 | 13 | −3 | baseline |
| Termite | 30 | 2 | 32 | −3 | Builder |
| Ant | 20 | 8 | 25 | −2 | Builder |
| Locust | 20 | **152** | 15 | −4 | Breaker |
| Pigeon | 40 | 16 | 12 | −4 | Runner |
| Rat | 20 | 140 | 17 | −5 | Breaker |
| Bee | 20 | 2 | **43** | **26** | Builder / Hunter |
| Wasp | 20 | 0 | 20 | 10 | Hunter |
| Starling | 30 | 9 | 12 | −5 | (support: see below) |
| Bat | **50** | 0 | 9 | −4 | Runner / Scout |
| Firefly | 20 | 98 | 20 | −2 | Breaker |
| Mosquito | 20 | 1 | 19 | 13 | Hunter |
| Crow | **50** | 0 | 17 | 7 | Runner / Hunter |
| Piranha | 20 | 36 | 17 | −3 | Breaker |
| Slime mold | 20 | 3 | 21 | −4 | Builder (slow) |
| Spider | 20 | 4 | 19 | −2 | (support) |
| Siphonophore | 30 | 15 | 15 | −5 | (support) |
| Plasma School | 30 | 119 | 15 | −4 | Breaker |
| Tadpole | 20 | 81 | 22 | −5 | Breaker |
| Octopus | 30 | 6 | 27 | −2 | Flex |

The axes measured one at a time, the others held:

| Termite under each control | Time | Space | Mass | Charge |
|---|---|---|---|---|
| Commander point (shipped) | 30 | 2 | 32 | −3 |
| Rhythm | 30 | 4 | **45** | −2 |
| Two squads | 30 | 5 | 42 | −2 |
| Formations | 30 | 11 | 40 | −6 |
| D-pad orders | 30 | 2 | 32 | **26** |
| Sheepdog | **0** | 0 | 27 | −5 |

| Locust under each economy (Space) | Pheromone 125 · Biomass **152** · Crystal 126 · Hive 118 · Fixed 125 · Tiered 125 · Food web 126 |
|---|---|

| Wasp under each meta | Time | Mass | Charge |
|---|---|---|---|
| None | 20 | 20 | 10 |
| Castes | **40** | 22 | **13** |
| Roulette | 20 | **37** | 10 |
| Merge | 20 | 19 | 12 |

## Checked / Found

- **Checked:**
  - `verify_lab.cjs` PASS on desktop and phone: no console errors; `runBatch` is deterministic across two calls and across a page reload.
  - A smoke run loaded all 37 toys and ran each for 20 s with no exception.
  - The decision log is wired: an `ArtifactData list` returned the empty `decisions` collection.
- **Found in the lab:** two scores that could not discriminate (L-STU-114), plus mosquitoes that
  could never latch because a throw overrode their drain. All three are fixed.
- **Found in the tooling:** the verifier failed on the sandbox rather than on the lab, in two ways.
  Playwright was not on `NODE_PATH`, and Google Fonts failed through the TLS proxy. Both are fixed in
  `verify_lab.cjs`, and its self-test still names all six planted defects (L-GEN-18).
- **Findings worth a design conversation:**
  1. **The D-pad Attack order turns ANY body into a hunter** (Termite Charge −3 → 26). One order
     button erases the species' role. Either Attack needs a species-specific cost, or "who can attack"
     is part of what a body is.
  2. **Starlings and siphonophores do not show their value in any one score.** They absorb shots
     meant for the hull, and the Charge score barely moves (−5 vs −3), because a 15 s round with two
     hunters rarely empties the hull. Their role is protection. A fifth column (petals *saved*,
     measured against a hull-only run) would show it.
  3. **The economy axis barely matters at 15 s** (locust 118–152). Economies are slow levers, so judge
     them in the live 45 s rounds, not in this scorecard.
  4. **The rookie Termite out-built the expert** (Mass 43 vs 35). A noisy focus spreads the workers
     over more patches, so for a builder, dispersal beats precision. The AI commander is a heuristic
     and treats one point as best; take that as a hint about the Termite's scheme, not a ranking.
  5. **Sheepdog control cannot race** (Time 0): herding has no way to send a member to a point. It
     is a Space/Mass scheme only.

## Decision needed (Garrett)

1. **Play the toys and vote.** Keep / Cut / Merge on each toy goes to the shared log, and round 2 reads it.
2. **First consolidation guess, for you to overturn.** Four vessels, one per element role:
   - **Builder** (Mass): Termite or Bee body, Commander point or Rhythm, hive economy, card deck.
   - **Breaker** (Space): Locust or Plasma School, Mood dial, biomass economy.
   - **Runner/Scout** (Time): Pigeon or Bat or Crow, Escort control, crystal tithe.
   - **Hunter** (Charge): Wasp or Mosquito, Throw control, castes meta.
   - The Starling / Siphonophore / Spider family is a **Support** vessel the scorecard cannot see yet (finding 2).
3. Should **Attack** be universal (finding 1)?

## What this lab does NOT model

- It is 2D top-down. A 3D swarm surrounding a ship in a sphere reads and defends differently.
- Member counts (6–60) say nothing about the collider budget. A shipped swarm must use the Termite's
  drone path (vessel agents, never lifeforms), and its collider cost is unmeasured here.
- Rivals are scripted ships, not players. Petal transfer moves whole petals, but there is no crystal ejection.
- There is no networking and no gamepad.
- A prism here is a dot with one hit point. Mass scores count prisms, not volume.
- The AI commander is a heuristic; treat a gap under ~10% as noise.

## Round log

- **Round 1 (2026-10-19, `cece/inspiring-einstein-k6syi5`):** built the lab, the 37 toys, the scorecard and team builder, and the decision log; made `verify_lab.cjs` hermetic (L-GEN-18); learnings L-STU-114 and L-STU-115.
