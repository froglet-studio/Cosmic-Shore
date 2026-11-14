# Handing a swim lane on (/handoff-to-<person>, /handoff-open, /handoff-for-test, /handoff-feedback)

Shombith, 2026-11-13: when a task in a lane is done for now, its person hands the lane on, and the hand-off shows on the
swim lanes board (`swimlanes.py`, and the Swim Lanes page, `PAGE.json`) for everyone. The tool is
`Tools/Build/handoff.py`; this file is the procedure every `/handoff-*` skill follows. The skills themselves are generated
(`handoff.py skills`, one `/handoff-to-<person>` per confirmed person in `Docs/TEAM.json`; the `handoff_skills` gate in
`ship_gates.py` fails when they drift).

## The four moves

| Skill | Command | What the board shows |
|---|---|---|
| `/handoff-to-<person>` | `handoff.py to <Person> --note "..."` | HANDED OFF tier; on that person's board (`swimlanes.py --me <Person>`, the page's For you) as **NEW** until they read it (`handoff.py seen <lane>`, the page's Take it) or push work in it. It counts as their lane in `approvals.py check`. |
| `/handoff-open` | `handoff.py open --note "..."` | The lane is open even while the branch is unmerged; the note (and the branch to start from) is the first item of its work. |
| `/handoff-for-test` | `handoff.py test --what "..."` | FOR TEST tier, held by the tester (`Docs/Handoffs/config.json` `testers`: Andrew) until they answer. |
| `/handoff-feedback` (the tester) | `handoff.py feedback <id or lane> good|back --note "..."` | `good`: the lane opens, marked tested. `back` (a note is required): open again with the tester's note at the top of its work. |

Plus `handoff.py claim <lane>` (take a lane before pushing; lapses after 7 days, the page's Claim button) and
`handoff.py keep` (the person said "keep it" at `/ship` §6.5; recorded only so the suggestion learns).

## Doing one

1. **The lane.** Without `--lane`, the tool takes the lane this branch's change is most about (the diff from
   bleeding-edge, read as `approvals.py check` reads it). Name it with `--lane <key>` when the branch spans lanes or the
   lane is one you did not touch (`swimlanes.py --json` lists the keys).
2. **Who you are.** `--by <name>`, else your git identity, else `FROGLET_PERSON`. A cloud session's git identity is
   Claude: pass `--by` with your person's name.
3. **The note is the brief.** Where the work stands, the next step, what is untested, the branch. For test, `--what` is
   the tester's whole brief: the steps, the build or branch, what good looks like, and the editor hand tests the branch
   added (`Docs/UNITY_VERIFICATION_CHECKLIST.md`).
4. **Commit and push** `Docs/Handoffs/` on the branch (`docs(handoff): <lane> to <person>` / `back to open` / `for
   test` / `tested`). Files are read from every branch pushed in the last 60 days, so it shows on every board as soon
   as it is pushed, and lands with the work.
5. **Tell your person** in one line who sees it now, and offer a page sync (`/swimlanes` section 6).

Never hand off a lane without your person's yes, and never hand a lane to someone as an assignment they did not
agree to: a hand-off is a heads-up with a brief, and the recipient can hand it straight back to open.

## A brief (a hand-off with more than a note)

A hand-off can carry a **brief**: `--brief <file.json>` with `summary`, `decisions` (what the sender settled),
`open` (`[[where, what]]`: the calls waiting on the recipient), `steps` (the next steps, in order), `reading`
(`[[doc, read first|read|skim|reference]]`) and `doc` (the document it came from). The recipient's **For you** page
shows it whole, with the steps as a checklist; their ticks and notes live in the page's per-person space
(`data/users/<them>/<hand-off id>`), which nobody else can read, the page owner included. The hand-off itself stays a
file in git, so the board can show who holds the lane. A hand-off someone wrote as a doc (Garrett's
`Docs/Voyages/HANDOFF.md`, 2026-11-12) is recorded with `--by <them> --source <the doc> --brief <file>`.

## How the board reads them (swimlanes.py)

The newest hand-off of a lane wins. To a person, the tester or a claimer: that person holds it, and the work pushed
before the hand-off stops occupying it. Open or sent back: nobody holds it. **Work anybody else pushes after the
hand-off still occupies the lane** (a hand-off never hides a real collision). `swimlanes.py --self-test` and
`handoff.py --self-test` hold these rules, with negative controls.
