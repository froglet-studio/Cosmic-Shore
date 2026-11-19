---
name: loop-atlas
description: Work through the Loop Atlas, the studio's model of Cosmic Shore's gameplay loops (Docs/LoopAtlas, the map https://claude.ai/artifact/8XkvFKuTFeEpRoLfp8QQpw) - brainstorm onto its Proposed layer, graduate a decided idea to Planned, open, take and close build orders that change the game (Now), inspect drift, and republish the map. Use on "/loop-atlas", "the loop map", "the atlas", "propose a loop / change", "add this to the atlas", "plan <loop or P-number>", "graduate", "build this loop", "what is planned / being built", "the atlas board", "republish the map", a note left on the map, or whenever a session finds a suggestion, insight or hole about a gameplay loop.
---

# Loop Atlas: propose, plan, develop, inspect

Read `Docs/LoopAtlas/PROCESS.md` first; it is the rule. In short: changing **Proposed** is brainstorming (anyone,
Claude included), changing **Planned** is planning (the model's owner, Garrett for now, or the area owner he hands a
piece to), changing **Now** is development (only by building the game). Ideas graduate Proposed, Planned, Now;
reaching Now opens a build order that closes when the game has it. `Docs/LoopAtlas/README.md` has the files and fields.

Every step is `python3 Tools/Build/loop_atlas.py <step> <loop or P<n>> --by <who> ...`, which rewrites the YAML, logs
one line in `Docs/LoopAtlas/ledger.jsonl`, and rebuilds `model.json` and `BOARD.md`. Commit all of them together.
`plan`, `develop` and `done` refuse `--by Claude`: name the person whose word it was, quoted in the commit.

## 1. Start

1. `python3 Tools/Build/loop_atlas.py --check` and read `Docs/LoopAtlas/BOARD.md` (build orders, plans, proposals,
   holes, recent moves).
2. `python3 Tools/Build/approvals.py check --me <person>` before committing, as every session does; the atlas lane is
   `loop-atlas` (owner Garrett).

## 2. Brainstorm (Claude does this on its own)

Wherever a session finds a suggestion, an insight or a hole about a loop, put it on the Proposed layer:
- a change to an existing loop: `propose <loop> --note "<one or two sentences>" --by Claude`;
- an open question: add a `holes` entry by hand (`on`, `q`, and a `fill` when there is a proposed answer);
- a new loop: add it by hand to the right `model/loops/*.yaml` with `in: x`, a `prop`, phases, drivers and `src`;
- a note left on the map (the artifact's `notes` collection, read with ArtifactData; see the project memory note on
  map notes): a suggestion becomes a `propose`, then mark the note handled with a `done` verdict.

Write in the map's voice: plain sentences, what the player does, the step where the feeling lands. Cite the source in
`src`. Open a PR against `Docs/LoopAtlas/` only (`docs(loop-atlas): propose ...`). Proposals never need anyone's yes.

## 3. Plan (only on a person's word)

When the owner says an idea is decided: `plan <loop> --by <person>` (or `--hole N` for a hole's fill, or `plan P<n>`).
Quote their words in the commit. Planning changes no code.

## 4. Develop (graduate to Now, then build)

1. On the owner's word: `develop <loop> --by <person>` opens the build order (`build.s: queued`).
2. Whoever takes it: check the swim lane (`/swimlanes`), then `start <loop> --branch <branch> --by <who>`.
3. Build the change in the game with the area's own skill (`/arcadegame`, `/vessel`, `/elemental-loop`, ...), keeping
   every `CLAUDE.md` rule (approvals, the verify rule, no mode changes a vessel).
4. In the same PR that lands it: `done <loop> --by <person> --note "<what the game now does>" --status built|part`.
   The atlas and the build merge together. A person signs off fun (`--status signed`), never a session.
5. A PR that changes a loop in the game without a build order edits that loop's `now` by hand in the same PR.

Drop what is no longer wanted with `drop <loop> --why "..." --by <who>`; the reason stays in the ledger.

## 5. Inspect

- `loop_atlas.py report`: drift (missing source docs, timescales outside their tier). Report, don't "fix" a loop that
  is under development (Activation, 2026-11-19: Garrett says it will become clearer as it materialises).
- Answer "what is planned / being built / proposed for X" from `BOARD.md` and the ledger, never from memory.

## 6. Republish the map

After a model change merges: `python3 Tools/Build/loop_atlas.py build --html /tmp/LoopAtlas.html`, then republish
that file to https://claude.ai/artifact/8XkvFKuTFeEpRoLfp8QQpw (Artifact tool, same URL; read the artifact first, as
it may have been edited). The page keeps its notes database. Never publish a second map.
