# The Loop Atlas

Two artifacts, one source (both private until Garrett shares them):
- **The full-page map**: https://claude.ai/artifact/8XkvFKuTFeEpRoLfp8QQpw. Use this one to work in; it has a Full screen button where the browser allows it.
- **The Design System artifact**: https://claude.ai/artifact/1g4Xc6uSEt1EQFeQmHqsgs. The vocabulary (tiers, drivers, how a loop is drawn), the layers as text, the model as data for agents, and the same map as a card. That card cannot go full screen: its frame does not allow it.

Everything about a loop is on a card inside its ring (the top slot, where its name is; a loop with nothing inside it is all card). Zoom into the card to read it. A card is laid out at reading size and scaled into its slot, so a loop with more to say is read by zooming further, never by leaving the map (Garrett, 2026-11-18).

Garrett, 2026-11-18: "I want to visualize a model for how all of our gameplay loops fit together into a single
structure ... I want to use this to frame where a problem sits so discussions can focus on the current challenges
... a very important tool for humans and AI to keep our shared understanding of the whole product up to date."

One zoomable map of every loop the game offers, from one input to the months-long game, on three layers:
**Now** (what is in the game), **Planned** (the stabilised version the team decided on) and **Proposed** (the plan
with changes, every hole filled by an invention). It is not yet reviewed by anyone: the Proposed layer is Claude's.

## The words (use them in docs, tickets and reviews)

| Tier | One pass | What it is |
|---|---|---|
| Era | 2 wk to 6 mo | what changes without you: the living sea, sagas, the meta, new vessels |
| Arc | 1 d to 4 wk | what changes between sittings: unlocks, the fleet, the squad, the crew, the hoard |
| Session | 10 to 45 min | a sitting in one place: a ladder run, an arena set, a voyage |
| Round | 60 s to 6 min | one objective, won or lost: a match, or a cell on a voyage |
| Role | 20 s to 3 min | the kit pointed at the game's goals: what this vessel does for its team |
| Combo | 8 to 60 s | verbs chained inside one kit |
| Verb | 2 to 20 s | one elemental ability, from trigger to payoff |
| Pulse | 0.1 to 3 s | one input and the hull's answer |

A loop **steps up** or **steps down** between tiers (hierarchy). A move between loops at the same tier, such as Arcade
to Arena, is a **crossing** (heterarchy). A **driver** keeps a player in a loop; a **hand-off** moves them out of it.
A **hole** is something a layer needs and does not have.

## Where things are

| File | What |
|---|---|
| `loop_model.json` | **The source of truth, written by hand**: tiers, drivers, hand-offs, every loop above the kit with its layers (`in`: n now, p planned, x proposed), steps, timescale `t` (seconds a pass), `reps` (passes before stepping up), holes with their proposed fills, edges, role loops, each vessel's combos, and the twelve proposals |
| `src/atlas.js`, `src/atlas.css` | the page's code and styles (plain JS, a canvas map with semantic zoom, three list views) |
| `artifact/project/` | the Design System artifact's files, GENERATED: never edit them |
| `artifact/standalone/loop-atlas-map.html` | the full-page map, GENERATED (tokens, styles, model and code in one file) |
| `Tools/Build/loop_atlas.py` | validates the model and builds `artifact/project/`; `--check` is the drift gate, `--self-test` proves the validator, `--stats` counts |

The kits are not typed here. Each vessel's abilities come from `Docs/FleetCodex/abilities.json` (Shombith's Fleet
Codex), and its games, steps 1-3 and arenas from `Docs/ElementalLoop/STATUS.md`. Fix those at their source.

## Updating it

1. Edit `loop_model.json`. When a decision lands, move the loop's `in` from `x` to `px` or from `px` to `npx` and move
   its note to that layer. Close a hole by deleting it and naming the source that closed it in the loop's `src`.
2. `python3 Tools/Build/loop_atlas.py` (then `--check`).
3. Republish: the Artifact tool with `url` above, `root` = `Docs/LoopAtlas/artifact`, `file_path` = one changed file,
   `files` = the other changed files by their `project/...` path. `components/index.d.ts` needs
   `contentType: "text/plain"`. Send `project/design-system.json` only to change the title or `lastChange`, and read
   it from the artifact first.
4. Republish the full page: the Artifact tool with `url` https://claude.ai/artifact/8XkvFKuTFeEpRoLfp8QQpw and
   `file_path` = `Docs/LoopAtlas/artifact/standalone/loop-atlas-map.html`.

## What is measured and what is not

- Tested in headless Chromium against a copy of the type's preview frame: the map at five zoom levels, cards
  opened by click on a ring's card, all three layers, the three list views, both themes, a 390 px phone. Not looked at on the published page itself.
- Timescales above the Round and every `reps` marked "not set" are estimates or unknown: the atlas lists each as a
  hole. Analytics has no funnel that reads them yet.
- Verb steps and feelings on the Proposed layer are derived from each ability's trigger, and every combo but the
  Dolphin's three (Garrett's brief) is invented from the kit text. Each vessel's owner corrects them.
