# Approvals: the queue of changes to approve after the fact

Garrett, 2026-11-10: asking for approval before changing someone else's work was slowing everyone down. So:

1. **Only a collision waits.** A change that is about a swim lane (`/swimlanes`: a vessel, game, app-shell section or system)
   that someone ELSE occupies right now - unmerged work pushed there in the last 7 days - stops, and the session tells
   its person, who talks to the occupant or decides to go ahead.
2. **Everything else goes through.** The session tells its person whose toes the change is on (a declared owner,
   whoever landed recent work in those files, whoever has unmerged work in them), so they talk to each other.
3. **And it lands in their queue.** For each person whose work the change actually changed (re-tuned, replaced,
   retired, renamed, decided otherwise, or an area they declared their own), the session queues an entry here. They
   run **`/approve`** to read their list and answer each entry: **approve** (they agree), **discuss** (they want to
   talk to the person who made it; the author sees that), and after talking, **resolve**.

4. **Answer first what the elemental loop is building on** (Garrett, 2026-11-12). A stakeholder who catches a change
   against the spirit of a vessel they worked on sends that vessel back through the loop, and every step built on
   the change since (intensities, the AI training, the arena's environment creator) is redone. So `list` ranks open
   entries by **loop impact**: the loop work a "this is wrong" answer would send back now
   (`Tools/Build/elemental_loop.py` `rework`). It grows while an entry waits, so the queue puts the costly catches
   first. `add --vessel / --element / --loop-step` names what a change is about; left out, it is read from the entry.

The rules: [`Docs/TEAM.md`](../TEAM.md) rules 3, 12 and 13. The skill: [`.claude/skills/approve/SKILL.md`](../../.claude/skills/approve/SKILL.md).
The tool: `Tools/Build/approvals.py` (`check`, `add`, `list`, `reply`, `--self-test`).

## The files

| File | Written by | When |
|---|---|---|
| `<Person>/<id>.json` | the session that made the change (`approvals.py add`) | committed with the change, so it lands with it |
| `<Person>/<id>.<time>.reply.json` | the person whose work it is (`approvals.py reply`, through `/approve`) | committed on their own branch |
| `config.json` | anyone | `shared_paths`: files everyone writes to, which are nobody's toes |

`<Person>` is a name in [`Docs/TEAM.json`](../TEAM.json). One small file per entry and per answer, so two branches
never conflict over the queue. `approvals.py list` reads `bleeding-edge`, every branch pushed in the last 60 days and
the working tree, and says where each change is: landed, or not merged yet on its branch. An entry whose branch is
deleted without merging disappears with its change.

Do not edit an entry or an answer by hand: a new answer is a new file, and the newest one wins.
