# Brief: in-game swarm round 5 - a lossless grid species, then the evolved rule's scene test

You are an autonomous implementation session for Cosmic Shore (Unity 6, URP, Netcode, Froglet Inc.).
Do not ask questions. Finish the work so the lead can check out one branch, open the editor, and fly
through it. You have no Unity editor in this container: author everything headlessly and state
plainly what you could not verify.

## Context

Branch `cece/swarm-fauna-game` hosts three swarm species in the Swarm cell. Read its
`Docs/SWARM_FAUNA.md` in full first:
- `SwarmFieldCore`: designed slots + boids.
- `SwarmGridCore` (§8): the hgrid2 grid morphogen. In the game its hunger cull is disabled (no
  imposed death), so after a morph the old majority's surplus clings to the new body as debris
  (finding 17).
- `SwarmSortCore` (§9): emergent cell sorting. Lossless.

The research (branch `cece/gifted-curie-x2cpd0`, commit `b4af65e4` or later; read
`Tools/NCA/DISCOVERIES.md` top sections) produced two results aimed at the game:

1. **combo** (`Tools/NCA/combo_model.py`, `results/combo/params.json`, `results/combo/NOTE.md`) is
   hgrid2 made LOSSLESS. The hunger cull is replaced by molting plus three lawful mechanisms:
   - `lay_cap`: no egg while the body holds the plan's headcount, eggs included;
   - `ratio = 2`: an overfull domain's spare capacity takes the element mix the whole body still
     lacks;
   - `orphan_proxy` / `transfer2`: a member with no class in the plan steers by its element's best
     slot.

   Result: all four scorecard axes - accurate (16/15/15 of 16 at seeds 7/23/41), lossless (0
   deaths), organic band, and cheaper than hgrid2 at G = 8. It is exactly the corrector the game's
   grid species lacks.
2. **evofate** (`Tools/NCA/evofate_model.py`, `results/evofate/params.json` + `genome.npy`,
   `results/evofate/NOTE.md`) is the round-1 evolved rule (G2's learned MLP + the evo genome)
   given a FATE (one of sort's positional wells per tadpole, a dead-zone pull). The lead called the
   evolved rule "beautifully organic ... a great candidate to attempt another scene test once you
   get to the next level of scoring with it". It is now there:
   - tier 1 at every seed; 12/11/12 of 13 at seeds 7/23/41;
   - lossless, and in the organic band (at its planar edge at seed 23);
   - 5.6 ms/step in Python.

## Branch

- Continue on **`cece/swarm-fauna-game`**: fetch, check out its tip, keep committing there.
- No other session is working on it now. Do not rewrite history, do not force-push.
- Push only there, never open a PR.
- End every commit message with the two attribution lines from your system prompt.

## Deliverable, in this order (push after each)

1. **A lossless grid species.** Port combo's corrector into `SwarmGridCore`: molting animated like
   the sort core's (`MoltBegan`/`MoltDone`), `lay_cap`, and the ratio target. `orphan_proxy` and
   `transfer2` may be inert with one domain; say which. Add a G = 8 option.
   - Re-measure, with the unchanged Python scorer via the harness:
     - accuracy in research mode against Python combo, and in game mode;
     - zero self-inflicted deaths;
     - that finding 17's debris is gone: the post-morph surplus re-forms into the new body;
     - feel and ms/step at G = 16 and 8.
   - Default the cell's grid swarms to whatever reads best, and say why.
2. **The evolved rule's scene test: `SwarmEvoFateCore`.** It runs the G2 MLP (weights exported from
   the research checkpoint the evo rule uses, `results/swarm_coevo_g2/rule.pt`; see `evo_model.py`
   and `results/live/evo_rule.json`, the live page's float32 export of exactly this network) plus
   the evo genome's behaviours plus evofate's fate pull.
   - Port carefully and prove it against Python evofate with the harness.
   - The MLP is the cost: measure ms/step honestly. Use plain C# with SIMD-friendly loops, or
     Burst-ready structure if that is clean.
   - Give the cell only as many evofate swarms as the measured cost allows - at least one, so the
     lead can meet it. Do NOT raise the cell's total swarm count.
3. **Docs.**
   - Update `Docs/SWARM_FAUNA.md` (new sections, exact editor test steps to find and compare each
     species, verification status) and its "Findings for the research".
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
