# QA — the untested-development backlog and how it stays current

Most work on this project is authored by sessions that **cannot open Unity**: no
compile, no import, no play-test. Those branches merge anyway, each carrying a
"verification status / verify in editor" note in its PR body. Historically those
notes scrolled away with the PR, so nobody could answer *"what on this branch has
never actually been run?"*

This folder is the answer. It is the single record of untested development and
the loop that keeps it honest.

| File | What it is | Who writes it |
|---|---|---|
| `QA_BACKLOG.md` | **THE list.** Every open, untested item, prioritised, with step-by-step instructions and pass/fail criteria. | The `/qa-backlog` skill |
| `RESULTS/TEMPLATE.md` | The form a tester copies to report a session's results. | — |
| `RESULTS/YYYY-MM-DD-<tester>.md` | One submitted test session. | QA |
| `DEV_TASKS.md` | Failures, converted into handoff-ready development tasks. | The `/qa-backlog` skill |
| `ARCHIVE.md` | Items that passed. Kept so a re-scan never resurrects them. | The `/qa-backlog` skill |
| `Tools/QA/apply_results.py` | Deterministic part of the loop: applies submitted results to the three files above. | — |

`Docs/UNITY_VERIFICATION_CHECKLIST.md` is the older, hand-maintained version of
this idea. It is now a **pointer** — new unverified work goes here.

---

## The loop

```
  new merges land (each PR body carries a verification-status note)
            │
            ▼
  /qa-backlog  ──►  rescans merges since the last run, adds NEW items,
            │       applies any RESULTS files that arrived since,
            │       archives passes, opens dev tasks for failures
            ▼
  QA_BACKLOG.md (prioritised, self-contained instructions)
            │
            ▼
  QA runs items, fills in a copy of RESULTS/TEMPLATE.md, commits it
            │
            ▼
  /qa-backlog  ──►  passes leave the list forever (ARCHIVE.md)
                    failures become DEV_TASKS.md entries with repro notes
                    (and stay on the list, marked FAILED, until re-fixed)
```

## For QA — how to run a session

1. **Get the current list.** Ask Claude Code for `/qa-backlog` (no arguments).
   It refreshes and prints the list; `Docs/QA/QA_BACKLOG.md` is the same content
   on disk. Work top-down — the list is already prioritised.
2. **Record the build you tested.** Branch + commit SHA (`git rev-parse --short HEAD`),
   Unity version, platform. A result without a build identifier cannot be trusted
   later.
3. **Run items.** Each item is self-contained: preconditions, numbered steps, an
   explicit PASS definition and an explicit FAIL definition. If you cannot get far
   enough to judge, that is **BLOCKED**, not FAIL — say what blocked you.
4. **Submit.** Copy `RESULTS/TEMPLATE.md` to `RESULTS/2026-08-06-<yourname>.md`,
   fill the table, commit it on any branch and open a PR (or hand the file to
   whoever runs the skill). One file per session. Never edit `QA_BACKLOG.md`
   by hand — the skill owns it, and hand edits are lost on the next run.
5. **Attach evidence for every FAIL**: the console error text, a screenshot or
   clip, and the exact step number that failed. That text becomes the dev task.

### Result values

| Value | Meaning | Effect on the next list |
|---|---|---|
| `PASS` | Every step met the PASS criteria. | Removed from the backlog → `ARCHIVE.md` |
| `FAIL` | A step met the FAIL criteria. | Stays, marked 🔴 FAILED; a `DEV_TASKS.md` entry is opened |
| `PARTIAL` | Some steps passed, at least one is unproven (not failed). | Stays, marked 🟡, with your note about what is left |
| `BLOCKED` | Could not run (build broken, missing asset, no second machine…). | Stays, marked ⛔ with the blocker |
| `SKIP` | Deliberately not run this session. | Unchanged |

## For engineering — how failures come back

`DEV_TASKS.md` is the handoff surface. Each failure becomes one task with the
originating QA item ID, the build it failed on, the observed symptom, the
suspected files (carried over from the item's source PR) and a definition of
done that is literally "QA item `<ID>` passes". Pick tasks from there; when the
fix merges, the QA item is automatically back at the top of the next list
because it is still marked FAILED.

## For whoever adds new work

If you land a change you could not verify in the editor, you do not need to
touch this folder — write a **Verification status** section in the PR body
saying plainly what was *not* run and what a human must do. The `/qa-backlog`
scan reads PR bodies and merge-commit messages, and that is where new items come
from. Being vague there is how work goes untested silently.
