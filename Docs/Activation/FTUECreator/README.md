# FTUE Creator

A claude.ai artifact for designing the onboarding as Quest Graph phases without opening Unity:
https://claude.ai/artifact/UMruaarAt2D2UY85aM93rU (Shombith asked for it, 2026-11-21, for the
activation plan).

It reads and writes the same files the in-game Quest Graph Editor (`FrogletTools ▸ FTUE ▸ Quest
Graph Editor`, `Assets/FTUE/`) does: one `QuestPhaseGraphSO` `.asset` per phase with its
`QuestNodeSO` sub-assets, and the `QuestSO` that orders them. An imported phase keeps its guid and
every node's `fileID` and `nodeId`, so a re-export diffs only what changed and saved quest progress
keeps pointing at the same nodes. `build.py` proves this on every shipped phase before it writes the
page (`--check` runs only that).

What it does:
- plays a phase as a new player sees it (`sim.js`, `sim.css`): the menus, the spotlight with Settings
  left usable, the comms window with typewriter and voiced lines, the flight view for the gameplay
  steps, synthesized UI sounds and sea ambience; the words in the preview are edited in place;
- shows a phase as the chain its `next` edges make, with the editor's venue rows (app shell or
  gameplay), and edits every serialized field of the 18 node types;
- drafts steps, a whole phase, or three takes of a dialogue in a pilot's voice with Claude, from a
  "story bible" the team edits; only the node types the guided-path rule allows (no Navigate, no
  Enter Freestyle);
- checks a phase (no lines, no terminal, steps off the path, locks under the developer unlock,
  steps that carry the player) and asks Claude for a design review;
- exports a phase `.asset` and `.meta`, or the whole quest as a zip, and keeps team drafts in the
  artifact's shared store.

What it does not do: set object references (a Dialogue portrait, a new speaker asset) - those are
kept as they were and set in Unity; edit branch ports other than `next` (no shipped node has one).

## Change it

Edit `template.html` (the page), `sim.js` / `sim.css` (the preview) or `core.src.js` (schema, YAML reader and writer, checks), then:

```
python3 Docs/Activation/FTUECreator/build.py   # needs node; writes out/ftue-creator.html
```

and republish `out/ftue-creator.html` with the Artifact tool to the URL above. A new node type is
one entry in `NODE_TYPES` in `core.src.js` (its script guid is in the `.cs.meta`); the round trip
fails until it is added, so a new node in the shipped phases cannot be silently dropped.
