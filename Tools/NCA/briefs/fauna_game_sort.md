# Brief: a third in-game swarm species, built on emergent cell sorting

You are an autonomous implementation session for Cosmic Shore (Unity 6, URP, Netcode,
Froglet Inc.). Do not ask questions. Finish the work so the lead can check out one branch,
open the editor, and fly through it. You have no Unity editor in this container: author
everything headlessly and state plainly what you could not verify.

## Context

1. **Two in-game swarm species already exist** on branch `cece/swarm-fauna-game`. Read its
   `Docs/SWARM_FAUNA.md` in full first:
   - `SwarmFieldCore`: designed slots + boids. The lead: "the transitions are fun, but it has lost
     too much organic imperfection; its mistakes feel like bugs, not emergence."
   - `SwarmGridCore` (§8): the hgrid2 grid morphogen. Accurate and organic, but in the game its
     composition corrector (hunger killing misplaced surplus) had to be switched off under the
     no-imposed-death law, so after a morph the old majority's surplus clings to the new body as
     debris (finding 17). Its cost is per step: 0.6-1.2 ms on CoreCLR.
2. **sort** (research, branch `cece/gifted-curie-x2cpd0`): emergent cell sorting.
   - Results: 12/13 feasible transitions under the loss-8 bar (own plans 1.4 / 2.4 / 1.05 / 5.0),
     held-out 13 / 13 / 13 / 12; my own held-out rescore at seed 23 passed tier 1 4/4 and the
     standard switches 4/4.
   - **Lossless by design:** surplus tadpoles MOLT into a deficit element of their own domain,
     and a tadpole with no region may transfer. Nothing dies on a clock. It is the game-legal
     composition corrector the grid species lacks.
   - **No network:** 17 numbers found by CMA-ES. Each tadpole has a TYPE (element, body region)
     and climbs a few Gaussian morphogen wells in body coordinates (positional information). A
     newborn commits to the well its type under-occupies (fate). Unlike types repel harder than
     like types (differential adhesion). A joint composition homeostat decides what is laid.
   - Its own recommendation (results/sort/NOTE.md): combine its fate-committed positional code
     with field's motion layer (swimming, yaw, predator reactions, inflation) — exactly what the
     game core already has.

## Code to port

All on branch `cece/gifted-curie-x2cpd0`, commit `94139a65` or later (fetch it; read with
`git show`):
- `Tools/NCA/sort_model.py` (SortCfg, SortSwarm)
- the files it builds on
- its config: `results/sort/params.json`
- the evidence: `results/sort/NOTE.md`, `ablations.json`, `terms.json`
- for the scoring/feel/lossless checks: `Tools/NCA/swarm_feel.py`, `scorecard.py`
- `Tools/NCA/DISCOVERIES.md` (top sections)

## Branch

- Continue on **`cece/swarm-fauna-game`**: fetch it, check out its tip and keep committing there.
- No other session is working on it now. Do not rewrite history, do not force-push.
- Push only there, never open a PR.
- End every commit message with the two attribution lines from your system prompt.

## Deliverable

1. **`SwarmSortCore`**: pure C#, no UnityEngine, struct-of-arrays, the same `ISwarmCore` shape as
   the other two cores. Port sort's step: types, wells, fate commitment, differential adhesion,
   the composition homeostat, molting and region transfer.
   - Keep the game's shared mechanics: funded laying, starvation, swimming in a band, the vessel
     reaction layer (reuse it as the grid core did), the kill path, continuity of existence.
   - Molting must visibly animate (the field core's molt is the precedent).
   - The wells must turn and travel with the swimming body, as field's slots do.
2. **Selectable per population.** Add Sort to the model choice, and make the Swarm cell host all
   three species side by side so the lead can compare them in one flight.
   - **Do NOT raise the cell's total swarm count.** The overtune pass already put 24 swarms and
     5,008 always-on heart colliders in the cell, unprofiled. Split the existing swarms among the
     three models instead. State the collider and CPU totals.
   - Everything stays authored by `Tools/Build/author_swarm_fauna.py` (`--check`).
3. **Proof.**
   - Extend the headless harness (`Tools/Build/swarm_core_harness`) to compile and RUN
     `SwarmSortCore`, and score its exported states with the unchanged Python scorer (research
     mode vs Python sort over several seeds; game mode against the one-domain plan, as §8.5 did).
   - The feel metrics next to the other two cores.
   - A LOSSLESS count: zero self-inflicted deaths while fed.
   - ms/step per plan, colliders at full size.
   - Every out-of-editor gate CLAUDE.md lists.
4. **Docs.**
   - Update `Docs/SWARM_FAUNA.md`: the third species, how it differs, exact editor test steps for
     comparing all three, and the verification status.
   - Add to its "Findings for the research" (they flow back to the research sessions).
   - Add a QA backlog entry.

## Rules that bite

These are the same as the first game brief. Read `Tools/NCA/briefs/fauna_game.md` on
`cece/gifted-curie-x2cpd0`:
- the ecosystem invariants: one-colour spawning per cell, conserved mass, continuity of
  existence, a crystal per death, the collider budget, "the Cell owns the environment";
- un-spawned NetworkObjects;
- empty FMOD `EventReference`s;
- the out-of-editor gates and their limits;
- `/asset-surgery` for headless asset authoring;
- `send_later` for your own check-ins;
- a clean, pushed tree when you finish.
