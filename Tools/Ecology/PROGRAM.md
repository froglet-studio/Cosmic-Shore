# The Living Ecology program: fauna (and flora) beyond behavior trees

Opened 2026-10-02, overnight. The lead's brief, verbatim:

> work through the night until you have no reason to believe that we could get better results by further
> searching, exploring, learning, experimenting ... consider how we could make flora and fauna that
> performantly populate an environment to create a vibrant and living experience for players. we have
> explored fractals, crystals, minimal surfaces, NCA, continuous automata, hierarchical solutions. We have
> self assembly that steals with the serpent walls. We have so much potential to create fauna that evoke a
> range of emotions from cute to terrifying. Our goal is to have a diversity of options so players can face
> repayable threats that are not just enemy vessels, but flora of all shapes, sizes, and behaviors. imagine
> a system beyond behavior trees that is driven by swarms and more.
>
> (follow-ups) flora and fauna. -- you can focus on fauna, but feel free to explore flora too

**The bar for stopping** is the lead's: stop when we have no reason to believe further searching would give
better results. So every direction keeps a measured scorecard, and a direction is closed only when its last
round stopped moving the numbers, not when it ran out of ideas.

## 0. What "beyond behavior trees" means here

A behavior tree is a designer's script: the creature does what a branch says. Everything the swarm research
taught us (Tools/NCA/DISCOVERIES.md) points the other way. **Behaviour should be what a body does when local
rules meet a world.** So the program is one SUBSTRATE that every species runs on, with species as
PARAMETERS of it:

| layer | what it is | precedent in this repo |
|---|---|---|
| **agents** | struct-of-arrays units: pos, vel, element, caste/role, energy, a handful of continuous DRIVES (hunger, fear, curiosity, aggression, attachment) | `SwarmSortCore`, the tadpole member |
| **fields** (stigmergy) | coarse 3D grids per cell that agents write and read: food scent, alarm, trail, threat (vessel wake), territory | NCA perception, the grid morphogen |
| **steering** | context steering: every drive paints interest/danger over a few directions and the agent takes the best. No branch selects a behaviour; the drives' weights do | sortfeel wells + wander |
| **quorum** | a local-density/signal threshold flips a whole group's phase (solitary to gregarious, grazing to hunting, scattered to assembled body) | molt / majority switch |
| **bodies** | a creature's body is an assembly of agents (a swarm forming a whale), a chain (worm colony) or a lattice (serpent walls / gyroid). The SAME agents can be loose or assembled | swarm morphogenesis, worm colony |
| **selection** | populations persist by eating, breeding and dying. Fitness is survival (CLAUDE.md "Endogenous selection only") | food web, §40 |
| **hierarchy / LOD** | far from any player a population is a macro-state (counts, centroid, phase) advanced by population dynamics; near a player it EXPANDS into individuals, mass-conserving and continuous | ecosim, cell volume ladder |

The emotional range comes out of the same parameters, and that is the claim to test. A locust-style
species can be CUTE sparse and fed (small, curious, approach-and-retreat), then TERRIFYING dense and hungry
(convergent, coordinated, fast), with nothing scripted between the two.

## 1. Locked rules every experiment keeps (CLAUDE.md, Ecosystem Design Principles)

- **Mass is conserved.** Prisms leave only through active forces: eating, vessel abilities. No timers, TTLs
  or cullers.
- **No imposed death.** Populations are bounded by consumption and starvation.
- **Continuity of existence.** Nothing pops in or out. A collapse/expand LOD must be continuous too.
- **One spawn colour per cell.** Fauna spawn in the controlling domain.
- **Every lifeform drops one elemental crystal.** A colony's members are the lifeforms.
- **Shielded mass is never food or a steering target.**
- **The collider budget is a hard gate.** State the impact of everything.

## 2. Replayable threats: the design target

A threat is replayable when:

- **(a) it telegraphs.** The player can read what is about to happen.
- **(b) it has counterplay** through flying and abilities, not a stat check.
- **(c) it varies.** Emergence makes two encounters differ.
- **(d) it pays.** Killing it, outwitting it or using it yields elements and crystals.

Every species is scored against a scripted pilot on these four, plus an **emotion profile**.

## 3. Performance target

- The substrate runs Burst- or GPU-shaped: SoA, fixed tick, fractional update (1/k of agents re-steer per
  step), spatial hash, instanced render, no per-agent GameObject.
- A cell should hold **10k+ living agents** at a main-thread cost near zero.
- The game's round 7 (cece/swarm-fauna-game, Docs/SWARM_FAUNA.md §14) is the first port of this shape.

## 4. Directions (one research session each, own branch `cece/eco-<name>`, harvested here)

| # | direction | question |
|---|---|---|
| A | **substrate** | Can one agents+fields+quorum substrate express many species? What do its update and LOD cost at 10k-100k agents? |
| B | **bestiary: threat fauna** | Build 6+ species on A spanning cute to terrifying (pack hunters that encircle, leeches, locust phase change, stampede grazers, ambush lurkers, a leviathan assembled from a swarm). Do they score as replayable threats? |
| C | **emotion probe** | Can "cute / curious / eerie / majestic / menacing / terrifying" be measured from motion and size alone, then used as a search target? |
| D | **builders and thieves** | The serpent walls' steal-and-assemble, generalised: colonies that steal prisms and BUILD with them (nests, walls, traps, a body). Stigmergic construction (termite / wasp rules). |
| E | **hierarchical ecology** | Collapse/expand between macro population dynamics and individuals, conserving mass, continuous, and with predator-prey cycles that stay alive (no extinction lock, no freeze). |
| F | **threat flora (optional)** | Snap traps, spore bursts, Physarum networks, reaction-diffusion growth, mimics, walking forests. Flora that is a threat you fly through, not scenery. |

Each session writes `Tools/Ecology/<name>/` (code, results JSON, a self-contained interactive HTML viewer in
which the lead can fly a vessel among the species) and a `DISCOVERIES` section. Each run is seeded,
measured, and kept even when negative.

## 5. Log

See `Tools/Ecology/DISCOVERIES.md`.
