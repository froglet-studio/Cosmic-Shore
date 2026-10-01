# Brief: the swarm fauna, in the game

You are an autonomous implementation session for Cosmic Shore (Unity 6, URP, Netcode, Froglet Inc.).
The game's lead asked for this directly:

> "lets take this one all the way to a working swarm fauna in an empty cell that i can fly with and
> kill. so make both the fauna and the cell that produces 3 populations separated in the cytoplasm. if
> they need to eat them give them some flora to eat as well. you can do all this in a separate session
> with its own branch that i can test."

Work autonomously and do not ask questions. Finish the whole thing: a branch the lead can check out,
open in the editor, and play. You have no Unity editor in this container. Author C#, ScriptableObjects,
prefabs and scene edits headlessly, and state plainly what you could not verify.

## What "this one" is

It is the **field** model, designed attractor fields plus boids. It is the research portfolio's winner
on the 16-transition yardstick: 13/13 feasible transitions, full healing after a vessel strike, and
vessel reactions. The lead knows it is a designed model rather than a learned one ("basically cheating,
but it could still produce great gameplay"). Gameplay is the point here.

Read these first, from branch `cece/gifted-curie-x2cpd0` (`git fetch origin cece/gifted-curie-x2cpd0`,
then `git show FETCH_HEAD:<path>` or check the paths out):

- `Tools/NCA/results/field/NOTE.md`: what the model is, its probes and its numbers.
- `Tools/NCA/field_swarm.py`: the reference model, about 680 lines.
- `Tools/NCA/field_port/FieldSwarmCore.cs`: a plain-C# port of one swarm step. It has a struct-of-arrays
  layout, a hash grid, greedy slot assignment, plan choice with hysteresis, true-breeding laying, molting,
  the morph vortex and vessel reactions. Measured at 0.1–0.6 ms per swarm-step.
  `Tools/NCA/field_port_check.py` proves it against the Python scorer.
- `Tools/NCA/results/swarm_targets/{mass,space,charge,time}.json`: the four body plans (whale, jellyfish,
  pufferfish, dragonfly). Each plan has 8 animation frames of tadpole units: position, element, domain
  slot, prism shape and tier, facing, spindle.
- `Tools/NCA/results/field/showcase.html`: what it looks like in a browser.
- `Tools/NCA/README.md`, sections from "Swarm targets: tadpole units" onward.

## Branch

Create `cece/swarm-fauna-game` from **`origin/bleeding-edge`**, so the lead tests on the current game.
Copy over only the `Tools/NCA` files you need, or none: data can be exported into Unity assets by a
script. Push only to that branch and never open a PR. Commits end with the two attribution lines from
your system prompt. Push at every meaningful milestone.

## The deliverable

1. **The swarm fauna.**
   - A swarm is a population of tadpole fauna units. Each unit is a heart crystal, a spindle and a prism,
     and has an ELEMENT and a DOMAIN.
   - The swarm grows the body plan of its majority element: Mass is the whale, Space the jellyfish,
     Charge the pufferfish, Time the dragonfly.
   - It swims, and it reacts to vessels the way field's predator layer does: a startle wave, fleeing per
     element, Time tadpoles mobbing a loitering ship, the pufferfish inflating.
   - It morphs to a new plan when players kill enough of its majority element. That is the core loop:
     selective killing reshapes the creature.
   - Run the model as a simulation core over the swarm's struct-of-arrays, Burst/Jobs where it pays.
     Drive visible units from it; do not run per-unit MonoBehaviour logic.
2. **Killing works platform-style.**
   - A tadpole dies when its body prism is destroyed by any vessel (rams, guns, explosions).
   - It drops its elemental crystal through the sealed fauna death path, so the crystal is collectable
     and the death animates out. Continuity of existence applies.
   - Use the `/fauna` skill (and `/ecology`) for the per-creature contract and the invariants, and read
     the worm colony (`WormFauna`, `Docs/ECOSYSTEM.md §23`). It is the precedent for "a colony is a
     population; every member is a lifeform carrying its own heart".
3. **Food, because mass is conserved.**
   - In the research model laying is free. In the game it must be FUNDED. A swarm grows by eating, and
     starves (wither to crystal) without food. Never use a timer or lifespan.
   - Give the cell flora the swarms graze, through the standard flora configs. Read `/flora` before
     authoring a species; reuse existing ones if any fit.
   - Consider making the element of what a swarm eats feed the element of what it lays. That makes
     feeding grounds a second lever on the creature's shape.
4. **The cell.**
   - Add a new `CellConfigDataSO` with no `EnvironmentPrefab` and its own `SpawnProfileSO`.
   - The profile seeds **three swarm populations separated in the cytoplasm**: each starts and stays in
     its own region, with a different starting majority, so the lead meets three different creatures.
     Plus the flora they eat.
   - Use the Cell's own capabilities: per-species bands (`FaunaConfigurationSO.BandInner/BandOuterRadius`),
     containment, the nucleus. Do not build a parallel spawner.
   - Make it reachable for testing from **Menu_Main freestyle via the Cell Selector toy**: append the
     config to Menu_Main's `Cell.CellConfigs`. The Arboretum cell's `Tools/Build/author_arboretum_cell.py`
     is the precedent.
   - Author its volume phase ladder from measurement, not by inheritance.
5. **Collider budget and performance.**
   - State the always-on collider count: heart crystals and body prisms per tadpole, times the
     population. The Lattice cell's 1,080 heart colliders is the shipped ceiling to compare against.
   - Moving prisms obey the mover contract (`Prism.NotifyPositionChanged` / `PrismSpatialIndex`).
     Prism VISUAL animation obeys the clock-material law (`Docs/PRISM_ANIMATION.md`).
   - Decouple the simulation tick from the frame rate and interpolate what is rendered, so motion is
     smooth.
6. **Docs and testing.**
   - Write `Docs/SWARM_FAUNA.md`: the architecture, the invariants touched (per CLAUDE.md's ecology
     protocol), the collider budget, and EXACT in-editor test steps for the lead: open Menu_Main, enter
     freestyle, fly to the Cell Selector, pick the swarm cell, what to look for, how to kill a swarm's
     majority and watch it morph.
   - Keep a **"Findings for the research"** section in that doc and update it as you go. The research
     sessions are still training swarm rules, and what you discover in the game must flow back. Examples:
     - the tick rate and smoothing needed to look good;
     - top speeds, separation and headcounts that read well at gameplay camera distance;
     - what molting or morphing looks like in motion;
     - what the funded-laying economy does to growth;
     - which behaviours matter to a player and which do not.
     The coordinating session will read it.
   - Add an entry to the QA backlog per `Docs/QA/README.md` for everything you could not verify in the
     editor.

## Rules that will bite

- CLAUDE.md is long and binding. In particular, these are LOCKED:
  - the ecosystem invariants: no imposed death, one-colour fauna spawning per cell, conserved mass,
    continuity of existence, every lifeform drops one crystal;
  - the collider-budget gate;
  - "the Cell owns the environment";
  - the FMOD rule: every sound is an exposed, empty-by-default `EventReference`.
- **One-colour spawning:** fauna spawn in ONE colour, the cell's controlling colour. Domain breeds true.
  If three populations want different domains, that needs explicit handling: discuss it in the doc and
  choose the conservative option. Domains are a team concept, so one domain with three element mixes is
  fine.
- **Molting** (a tadpole changing element) is a constraint the lead explicitly relaxed for research. Keep
  it, flagged in the doc, and make it visibly animate.
- **Un-spawned NetworkObjects:** fauna prefabs carrying a `NetworkObject` must not be instantiated
  un-spawned. See `Tools/Build/check_fauna_replication_seam.py` and `Docs/PartySystem/BUGS.md` B16.
  Freestyle fauna are client-local by default; keep these local.
- **Out-of-editor gates:** run every gate CLAUDE.md lists for C# changes before each push:
  - `check_conditional_compilation.py`, `check_enum_member_references.py`,
    `check_switch_label_collisions.py`, `check_using_directives.py`,
    `check_self_referential_locals.py`, `check_duplicate_attributes.py`,
    `check_abstract_member_implementations.py`, `check_fauna_replication_seam.py`,
    `check_console_logging.py`, and any asset `--check` scripts you touch;
  - a Roslyn compile over your new pure-C# simulation files with a stub harness. Say in the commit
    what that did and did not prove.
- **Headless asset authoring:** use the `/asset-surgery` skill for prefab, SO and scene authoring.
  GUIDs must be owned by exactly one `.meta`.
- Use `send_later` (claude-code-remote) to schedule your own check-ins while long work runs.
- When you are finished, the working tree is clean and everything is pushed. The last commit's message
  and `Docs/SWARM_FAUNA.md` both state the verification status honestly.
