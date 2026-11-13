---
name: elemental-loop
description: Push a vessel through Garrett's elemental vessel loop, the TOP PRIORITY for anyone with cycles (2026-11-12) - step 0 the game kit (a Time, a Charge and a Mass or Space arcade game), 1 fun (abilities and game together at one intensity), 2 the I1-I4 ladder (intro, fun, challenging, very hard), 3 one AI per vessel per element on I1-I4, 4 an environment creator for the arena game (Regatta, Broadside, Brood Rush) trained against the fixed AI pool, 5 the plateau's vessel imbalance tuned and sent back to 2-4. Shows the board and the ranked next actions (elemental_loop.py), walks a session through one step, and records the sign-off. Use on "/elemental-loop", "what should I work on", "I have cycles", "next vessel", "where does <vessel> stand", "the loop", "balance the vessels", "fun pass", "intensity ladder", "environment creator", "cultivator/destroyer", "Brood Rush balance", or before any vessel, arcade-game, AI-training or arena-balance work.
---

# The elemental vessel loop

Garrett, 2026-11-12: "I need your help in making sure that pushing every vessel through this plan in a systematic way
remains the highest priority for anyone who has any cycles." The process is
[`Docs/ElementalLoop/PLAYBOOK.md`](../../../Docs/ElementalLoop/PLAYBOOK.md). Read its section for the step you take
before you start; this skill is the procedure around it.

## 1. Find the work

```bash
git fetch origin
python3 Tools/Build/elemental_loop.py next --n 12      # ranked: element, arena-seated vessels first, earliest step
python3 Tools/Build/elemental_loop.py vessel <Name>     # one vessel, every element and step
python3 Tools/Build/elemental_loop.py                   # the whole board (STATUS.md is the same, generated)
```

Tell the person the top three actions in a sentence each, and which one you propose to take. If they asked you to
just go, take the top one nobody holds.

## 2. Claim it

The vessel is a swim lane, and every vessel is its own parallel lane through the loop: `/swimlanes` (or
`python3 .claude/skills/swimlanes/swimlanes.py`) opens with them, each with its progress and next step. Take one nobody
holds; if the vessel's lane is occupied by someone else, say who and take the next one. Run
`python3 Tools/Build/who_works_where.py query <paths>` on the files you will touch and name whoever else works there.

## 3. Do the step

| Step | Load first | The exit you are working toward (PLAYBOOK section 3) |
|---|---|---|
| 0 kit | `/arcadegame`, `Docs/MassAndSpaceGames/` prompts, `/cardart` | the vessel has a Time, a Charge and a Mass or Space game on the arcade roster |
| 1 fun | `/vessel`, `/element-ability-table`, `/vessel-studio` (offer in one line), the fun card (`fun_cards.py`) | ONE human flew it and signs it off as fun (the editor's fun card after an arcade match; `fun_cards.py apply` records it); only then step 2 |
| 2 ladder | `/arcadegame` (intensity ladder), `/EndGameConditions`, `/ecology`, `rung_bars.py` | I1 intro, I2 fun, I3 challenging, I4 very hard, each showing a vessel capability; `rung_bars.py` checks each rung against its bar from the recorded flights; ONE human signs them off as fun (fun card: "yes" and "right" on all four); only then AI training |
| 3 AI | `/vessel-ai`, `/compute-job`, `Docs/AI_SYSTEM/AI_PLAN.md` | one genome on I1-I4, four-part test, editor check; commit says `Plan step: <id>` |
| 4 creator | `/regatta-balance` (Time), `/arenagame`, `/compute-job` | the generator plateaus against the fixed AI pool; a person agrees its environments are fair and fun |
| 5 imbalance | `/vessel`, `/approve` | the smallest vessel tuning, shipped and queued for Garrett's `/approve` (never waits on him); tuned elements set to `redo` |

Vessel tunings (ability changes, step 5) go through without waiting: queue each with `python3 Tools/Build/approvals.py add --to Garrett ... --vessel <Vessel> [--element <E>] [--loop-step 1|2|3]` (and the vessel owner). The vessel names it for the loop: `/approve` ranks every queue by the loop work a late "that is not this vessel" would send back, so the stakeholders answer it before your next step builds on it. Rules that always hold: a game mode never changes the vessel (ability tuning is on the vessel, LOCKED); no AI is
trained inside an arena game; racing AIs never go for crystals; compute runs in cloud sessions, never on Garrett's PC;
Garrett's playtest beats any model number. Show a change that looks or moves (`Docs/claude/VIDEOS_AND_PREVIEWS.md`)
instead of asking someone to go and check.

## 4. Record it

```bash
python3 Tools/Build/elemental_loop.py set <Vessel> <Element> <step> <state> --by "<who>" --evidence "<doc / PR / playtest>" [--kind human|auto]
python3 Tools/Build/elemental_loop.py set <ArenaMode> <Element> <4|5> <state> --by "<who>" --evidence "..."
python3 Tools/Build/elemental_loop.py --check
```

States: `todo`, `doing`, `built` (exists, not signed off), `done` (signed off), `redo` (step 5 sent it back), `n/a`.
**Never record `done` for steps 1, 2 or 4 without a person's playtest** (for steps 1 and 2 the tool refuses anything but `--kind human`, and refuses a step `done` before the one below it), and say who in `--by`. Use `--kind auto` only
when a gate, not a person, decided it: that share is how the loop measures that each round needs fewer people. Commit
`loop.json` and `STATUS.md` with the work they describe, and add a row to the PLAYBOOK log when a step moves for a
whole element or arena.

## 5. Hand off

A playtest a person must do goes on that vessel branch's one Froglet Tasks ticket (`/team-tasks`, only with the
person's approval). Tell the person in one line what the loop's next action is after yours.
