# How work moves through the Loop Atlas

The atlas is a tool for proposing, planning, inspecting and developing the game, not only a picture of it (Garrett,
2026-11-19). Each layer of the map is a kind of work:

| Layer | Holds | Changing it is | Who changes it |
| --- | --- | --- | --- |
| **Proposed** | Suggestions, insights, fills for holes: what could be. | **Brainstorming** | Anyone, Claude included. Cheap and reversible. |
| **Planned** | What the team decided and has not built. | **Planning** | The model's owner (Garrett, for now), or the area owner he hands it to. |
| **Now** | What the game is today. | **Development** | Only by building. Now changes when the game changes. |

An idea **graduates** upward: Proposed, then Planned, then Now. Reaching Now does not just edit text: it opens a
**build order**, and the build order is closed when the game has the change. That is how the atlas goes straight
into development.

```
 brainstorm             planning                 development
 Proposed  ── plan ──>  Planned  ── develop ──>  build order ── start ──> building ── done ──>  Now
    ^  |                                  (queued)                                          (rewritten)
    |  └─ drop (reason kept)                 └─ drop (reason kept)
    └─ propose (anyone, any time; holes too)
```

`develop` can also take an idea straight from Proposed when the owner says so. A hole's proposed fill graduates the
same way (`plan <loop> --hole N`).

## The steps

Every step is one command. Each rewrites the loop's YAML, appends one line to `ledger.jsonl` (date, step, loop, who,
what), and rebuilds `model.json` and `BOARD.md`. `BOARD.md` is the atlas as a work board: build orders, decided plans,
open proposals, holes and the latest moves.

| Step | Command | What it does |
| --- | --- | --- |
| Brainstorm | `loop_atlas.py propose <loop> --note "..." --by <who>` | Writes the loop's Proposed layer. Claude may run it. |
| Plan | `loop_atlas.py plan <loop> --by <person>` | Moves the proposal to Planned (`plan.s: decided`). `--hole N` plans a hole's fill instead. |
| Develop | `loop_atlas.py develop <loop> --by <person>` | Opens a build order (`build.s: queued`) with the plan as what to build. |
| Start | `loop_atlas.py start <loop> --branch <b> --by <who>` | The build order is being built on that branch. |
| Done | `loop_atlas.py done <loop> --by <person> [--note "..."] [--status built\|part\|signed]` | The game has it: Now is rewritten, the build order closes, a plan that was exactly this build is cleared. |
| Drop | `loop_atlas.py drop <loop> --why "..." --by <who>` | Removes a proposal or a build order; the reason stays in the ledger. |

The proposals P1 to P12 move the same way (`plan P5`, `develop P5`, `start P5`, `done P5`), through `stage`:
proposed, planned, building, current, or dropped.

`plan`, `develop` and `done` refuse `--by Claude`: they are decisions, and the person who made one is the one named.
A session that records a decision names the person whose word it was.

A brand-new loop (not an edit to an existing one) is added by hand to the right `model/loops/*.yaml` with `in: x`
and a `prop`; from there it moves like any other.

## Development: what a build order means for the game

- A build order is the atlas asking the game to change. The session that takes one runs `start` with its branch,
  builds the change in the game through the area's own skill and rules (`CLAUDE.md`: swim lanes, approvals, the
  verify rule), and runs `done` in the same PR that lands the change, so the atlas and the build merge together.
- The other way round counts too: a PR that changes a loop in the game without a build order updates that loop's
  `now` in the same PR (`done` needs a build order; for this case edit `now` by hand). Changing the game is changing
  Now.
- A build order that is not worth building after all is dropped with the reason, never left to rot. `BOARD.md` shows
  how old each one is.

## Inspecting

- `loop_atlas.py report`: drift, such as source docs that no longer exist or a timescale outside its tier. These are
  things to look at, not errors. A loop under development (Activation today) can sit outside its tier while it takes
  shape.
- `BOARD.md`: where every idea stands.
- The map: https://claude.ai/artifact/8XkvFKuTFeEpRoLfp8QQpw, rebuilt from the model with
  `loop_atlas.py build --html <file>` and republished to the same URL after a model change merges.

## Claude keeps proposing

Claude stays on top of the Proposed layer: wherever a session finds a suggestion, an insight or a hole (in a design
doc, a playtest note, a note left on the map, the analytics, an AI run), it runs `propose`, or adds a hole, in a PR
against this folder. The `/loop-atlas` skill holds the routine. Proposals are cheap; graduating them is the owner's
call.
