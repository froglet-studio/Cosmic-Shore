# Brief: a second in-game swarm species, built on the grid morphogen

You are an autonomous implementation session for Cosmic Shore (Unity 6, URP, Netcode,
Froglet Inc.). Do not ask questions. Finish the work so the lead can check out one branch,
open the editor, and fly through it. You have no Unity editor in this container: author
everything headlessly and state plainly what you could not verify.

## Context

1. **The first in-game swarm.** It is on branch `cece/swarm-fauna-game` (from `bleeding-edge`).
   Read its `Docs/SWARM_FAUNA.md` in full first. It ports the research **field** model as a pure
   C# simulation core (`SwarmFieldCore`) with a headless harness, plus:
   - funded laying, starvation and swimming;
   - a Swarm cell with three populations, reachable from the Menu_Main freestyle Cell Selector;
   - tadpoles that die the platform way.

2. **The lead's verdict on field:** "the transitions are fun, but it has lost too much organic
   imperfection; its mistakes feel like bugs, not emergence."

3. **The grid morphogen family**, which the lead judged "an excellent direction: both accurate
   and organic":
   - Its second round, **hgrid2**, is the first LOCAL model to pass tier 1 under the loss-8 bar:
     own-plan losses 6.1 / 4.8 / 5.5 / 7.2 at seed 7.
   - Held out: own plans 4/4 at seed 23, 2/4 at seed 41 (whale and dragonfly at the bar's edge).
   - Every tadpole reads only fields at its own position: a coarse class-deficit grid plus a fine
     per-class morphogen. Nothing assigns it a place.

## Code to port

All on branch `cece/gifted-curie-x2cpd0`, commit `4da3612e` or later. Fetch it and read with
`git show`.

- `Tools/NCA/hgrid2_model.py` (Boid2, Cfg, Oracle2)
- the files it builds on: `hgrid_core.py`, `hgrid_boid.py`
- its config: `results/hgrid2/params.json`
- its NOTE and feel metrics: `results/hgrid2/`, `swarm_feel.py`
- the shared docs: `Tools/NCA/DISCOVERIES.md` and `README.md`

## Branch

- Continue on **`cece/swarm-fauna-game`**: fetch it, then check out its tip and keep committing
  there.
- Another session built that branch and is idle. Do not rewrite its history, and do not force-push.
- Push only there. Never open a PR.
- End every commit message with the two attribution lines from your system prompt.

## Deliverable

1. **The grid core.** Add a second simulation core, `SwarmGridCore`: pure C#, no UnityEngine,
   struct-of-arrays, the same shape as `SwarmFieldCore`. Port hgrid2's step:
   - the coarse class-deficit grid that rides the swarm;
   - the fine per-class morphogen;
   - deficit climbing, laying toward class shortage and starving of misplaced surplus;
   - plan choice by majority with its lock;
   - feed-forward, boids and the vessel reaction.

   Keep the game's mechanics that SwarmFieldCore already has: funded laying (eggs cost eaten
   volume), starvation, swimming in a band, the kill path and continuity of existence. Where
   hgrid's "starve misplaced surplus" meets the game's no-imposed-death law, handle it per
   CLAUDE.md:
   - Starvation of an unfed member is allowed.
   - A misplaced member withering on a timer is not allowed unless it is genuinely starvation
     (its class is not fed).
   - Read `/ecology`, decide, and document the decision.

2. **Selectable per population.** Add a model choice to the swarm config (Field | Grid). Make
   the Swarm cell host BOTH kinds side by side, so the lead can fly between a field swarm and a
   grid swarm and compare the feel in one session. For example: two grid swarms and one field
   swarm, each with a different starting majority. Keep everything authored by
   `Tools/Build/author_swarm_fauna.py` (`--check`).

3. **Proof.**
   - Extend the headless harness (`Tools/Build/swarm_core_harness`) to compile and RUN
     SwarmGridCore. Grow each plan, export states, and score them with the unchanged Python scorer
     (`swarm_nca.swarm_loss`/`decode`, default LossCfg).
   - Report own-plan losses next to the Python hgrid2's, over several seeds. They should land
     in the same range (under 8).
   - Measure ms/step for each plan, and state the collider count at full size.
   - Run every out-of-editor gate CLAUDE.md lists.

4. **Docs.**
   - Update `Docs/SWARM_FAUNA.md`: the second species, how it differs from field, exact test
     steps for comparing the two in the editor, and the verification status.
   - Add to its "Findings for the research".
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
