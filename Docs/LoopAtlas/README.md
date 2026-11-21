# The Loop Atlas model

The Loop Atlas is the studio's model of Cosmic Shore's gameplay loops: every loop from a single input (Pulse) to what
changes over months (Era), nested by timescale, with what is built today, what the plan says, what is proposed, the
holes nobody has decided, and the design doc each loop came from. The map is the claude.ai artifact
https://claude.ai/artifact/8XkvFKuTFeEpRoLfp8QQpw. **The YAML files here are its source of truth** (Garrett,
2026-11-19: he owns the model for now; pieces are handed to their area's owners over time). The why and the plan
beyond this step: the strategy doc "Loop Map as a Game Modeling Standard",
https://claude.ai/artifact/QUiM5ztvnHhkpWgsn4S43K.

## Files

| Path | Holds |
| --- | --- |
| `model/meta.yaml` | Schema version, last update, the fleet's order, the default vessel, the link to the vessel page. |
| `model/vocabulary.yaml` | The ladder of words every loop uses: layers (Now, Planned, Proposed), the eight tiers with their timescales, the twelve drivers, the seven handoffs. |
| `model/loops/NN-*.yaml` | The loops, one file per place or group, read in file-name order. |
| `model/edges.yaml` | Links between loops that are not parent and child: `train`, `cross`, `feed`. |
| `model/kit.yaml` | What every vessel's kit shares: roles per game type, the flight-feel pulse, the verb archetypes, which drivers each element serves. |
| `model/proposals.yaml` | Proposals P1 to P12 and the loops each touches. |
| `model/items.yaml` | Product work beyond the loops: the areas (Game, App, AI, ...) and the items in them, which move through the layers like loops. |
| `model/fleet/<Vessel>.yaml` | One vessel: its verbs, combos (with nested `sub` loops and each combo's AI `gene`), games, arenas and board. |
| `model/genes.yaml` | What turns a combo into an AI gene: the rule, the activation expectation for combos, the `senses` a gene's cues name and the pilot `sights` (callsigns). |
| `model/players.yaml` | Personas, the session cultures they keep (sitting lengths, returns) and the stages of their journeys through the loops. Proposed layer; every number is a hypothesis to calibrate with analytics. |
| `genes-out/` | **The gene search's output** (`Tools/Build/gene_search.py run`): one genome per vessel and element (best, MAP-Elites archive, I1-I4 ladder, plateau, bloat), `pool.json` (the fixed AI pool arena balancing races against), `retrain-<arena>.json`, `discovered.json` (candidate loops) and `SUMMARY.md`. |
| `balance-in/` | Proposed tuning from the arena balancing work, one JSON per arena, read by `gene_search.py retrain --balance`. |
| `discover-in/` | Strategies other searches observed, offered as loops (`README.md` there has the shape). |
| `journeys-out/` | **Player journeys** (`Tools/Build/player_journeys.py`): `journeys.json`, `JOURNEYS.md`, and `PlayerJourneys.html`, the animated page (https://claude.ai/artifact/4nfBMbpPw6t1PSDa261Mm8). |
| `GENES.md` | Every combo as an AI gene, the coverage per vessel and genome, and the activation expectations of every measured loop, **generated**. |
| `schema.json` | JSON Schema for the assembled model. |
| `model.json` | The assembled model, **generated**; never edit it by hand. Other tools read this. |
| `page/atlas.template.html` | The published page with the model taken out. The map's runtime lives here. |
| `PROCESS.md` | **How work moves**: Proposed is brainstorming, Planned is planning, Now is development; graduation and build orders; the map's Board (anyone asks, the owner decides) and the daily steward run. |
| `ledger.jsonl` | One line per lifecycle step (who graduated what, when). Appended by the tool. |
| `BOARD.md` | The atlas as a work board, **generated**: build orders, decided plans, proposals, holes, recent moves. |
| `CODE.md` | **The Code Atlas**: how the map zooms from a loop into the whole codebase down to every value (equivalent places, no links), the data model, loading, the anchored camera, the pictures. |
| `code/links.yaml` | Hand links from loops to the code that implements them (the automatic ones need none). |
| `code-out/` | The Code Atlas build (`Tools/Build/code_atlas.py build`), **generated and ignored**; published beside the page as `code/`. |

## Editing the model

Moving an idea between layers (propose, plan, develop, start, done, drop) goes through the tool's lifecycle steps in
`PROCESS.md`, which log every move. Other edits:

1. Edit the YAML. Field meanings are below; `schema.json` is the full list.
2. `python3 Tools/Build/loop_atlas.py build` validates and rewrites `model.json` and `BOARD.md`. Commit them with the YAML.
3. To publish the map, `python3 Tools/Build/loop_atlas.py build --html /tmp/LoopAtlas.html` and republish that file
   to the artifact's URL. The page is the template with the model put back, so a build with no YAML change is byte for
   byte the published page.

`python3 Tools/Build/loop_atlas.py --check` is what CI runs (`.github/workflows/loop-atlas-check.yml`): the model is
valid and `model.json` and `BOARD.md` match the YAML. `report` lists drift that does not fail the build yet (source docs that are not
in the repo, a loop whose timescale lies outside its tier). `self-test` proves every check refuses what it must.

The tool needs PyYAML and jsonschema (`pip install pyyaml jsonschema`).

## What the checks enforce

- The YAML matches `schema.json`: no unknown fields, the required ones present, statuses and kinds from the fixed sets.
- One root loop (`era`), every other loop reachable from it through `parent`, no parent cycles.
- A loop's `at` is a phase of its parent, and a loop never sits on a higher tier than its parent.
- Every driver, handoff, layer letter, tier and archetype a loop, edge, role or vessel names is defined in
  `vocabulary.yaml` or `kit.yaml`.
- Every edge joins two loops, every proposal touches only loops that exist, the default vessel is in the fleet.
- Every gene's cues, sight and dials are well formed (above); every `measure` names an Activation phase that exists and
  an event no other loop uses.

## Fields of a loop

| Field | Meaning |
| --- | --- |
| `id`, `name` | Stable id (never reuse one) and the name the map shows. |
| `parent`, `at` | The loop this one runs inside, and which of the parent's phases (counting from 0). |
| `tier` | 0 Pulse, 1 Verb, 2 Combo, 3 Role, 4 Round, 5 Session, 6 Arc, 7 Era (`vocabulary.yaml`). |
| `kind` | `loop`, `place` (Toybox, Arcade, Arena, a voyage), `hub` (a sitting), `fleet`. |
| `t` | One pass, in seconds: `[shortest, longest]`. |
| `phases` | The steps of one pass: `n` the step, `d` an optional line on it. |
| `drivers` | What pulls a player into this loop (`vocabulary.yaml` drivers). |
| `reps`, `repsOf` | How many passes make one pass of the parent, and of what. `null` means not measured. |
| `now`, `plan`, `prop` | The loop on each layer: `s` a status, `note` a line. `now.s`: signed, built, part, dark. |
| `in` | Layers the loop exists on when not all: `x` only Proposed, `px` Planned and Proposed. |
| `holes` | Open questions: `on` the layers, `q` the question, `fill` the proposed answer. |
| `src` | Where the loop came from: a repo path (optionally with a section), or a dated brief. |
| `build` | A build order: the change graduated to Now that the game does not have yet (`s` queued or building, `what`, `by`, `at`, `branch`). |
| `kit`, `element`, `core` | The role it plays a vessel's kit in, its element, and whether it is on the core path. |
| `measure` | How a pass is counted and what activation expects of it: `event` (the analytics event one pass fires, `params`), `target`, `first` (the phase of `arc.activation` where a new player first meets it) and `expect` (what they have done by then). `in` its layers, `x` until planned. |

## Genes: a combo as an AI behaviour

Garrett, 2026-11-21: "your proposed gameplay loop combos are spot on for most vessels. expand that work. turn them into
genes for ai behaviors". Every combo carries a `gene`, one gene of the vessel's genome for one element (the AI plan's
one AI per vessel per element, `Docs/AI_SYSTEM/AI_PLAN.md`):

| Field | Meaning |
| --- | --- |
| `genome` | Time, Charge, Mass or Space: the element genome that carries it, usually the element of the arcade game it pays in. |
| `cue` | When the AI starts the combo: ids from `genes.yaml` senses (what the AI reads of the world, each with where the game knows it). |
| `dials` | What a search learns: `[name, what it sets, low, high, unit]`. The seed is what the shipped AI does; once a search writes a dial nobody types it (the `RaceAICrystalGenesSO` rule). |
| `done` | The measured moment the combo counts as done: the AI's fitness term and the player's `combo_completed` event are the same count. |
| `sight` | The pilot callsign whose sight leans into it (`Docs/Pilots/CALLSIGNS.md`): a pilot changes what the AI goes after, never what the vessel can do. |
| `in` | Its layers; `x` (Proposed) until the AI owner plans it. |

`genes.search` is how a search scores a genome (Garrett, 2026-11-21): it keeps **bloat** small (a dial, cue or
branch nobody can see change anything, that widens the search or costs effectiveness) and weights toward **style**
(switching between noticeably different behaviours of roughly comparable effectiveness, kept in a MAP-Elites archive
beside the best genome). Skill comes down the ladder by leaning on the archive's fun, less effective behaviours, never
by adding random mistakes to the best one. GENES.md prints it under "How a search scores a genome".

**Genes and loops inform each other** (Garrett, 2026-11-21). `Tools/Build/gene_search.py` trains every genome from
the atlas's genes (atlas to genes) and may also compose the vessel's own verbs into chains no combo names. A chain the
archive keeps, effective and used, is a candidate loop: `discover` lists it beside strategies other projects drop in
`discover-in/`, and `propose --top N` writes the best into the vessel's YAML as a Proposed combo with its gene (genes to
atlas). Once a person keeps one, the next run seeds it as an atlas gene. Until Froglet Compute runs again and a
harness flies a gene in the vessel's own games, the search scores genomes on a surrogate: its numbers measure the
search, not the vessels.

The check refuses a cue that is not a sense, a sight that is not a callsign, a dial range that is empty, and a genome
the vessel has no verb for. `report` lists a vessel with a game or arena for an element and no gene for it.

## Next phases

From the strategy doc: (1) doc sync, section anchors with hashes so a changed doc marks its loops stale, generated
blocks in docs, and accepted map notes turned into pull requests; (2) target bands per loop checked against analytics;
(3) resource `flows` and an economy simulation; (4) persona driver weights and a session simulation; (5) loop `goal`
blocks as agent tasks, and loop coverage as the behaviour descriptor in the Pareto search's MAP-Elites archive. Each
adds optional fields, so schema 1 files stay valid.
