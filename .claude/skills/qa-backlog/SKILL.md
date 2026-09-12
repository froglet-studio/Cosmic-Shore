---
name: qa-backlog
description: Produce or refresh the QA test list for untested development on Cosmic Shore. Scans merges/PR bodies for work that was never opened in Unity, applies any submitted QA results (passes leave the list, failures become dev tasks), and rewrites Docs/QA/QA_BACKLOG.md as a prioritised, step-by-step, pass/fail test plan QA can run and share. Trigger on "what needs testing", "QA list", "untested backlog", "update the QA list", "we ran some tests, here are the results", "hand the failures to engineering", or any request to find development flagged as untested/unverified.
---

# QA backlog — build and maintain the untested-development test plan

You own `Docs/QA/`. Read `Docs/QA/README.md` first — it states the loop and the file
contract. Never hand-edit `QA_BACKLOG.md`, `ARCHIVE.md` or `DEV_TASKS.md` outside this
skill's steps; those three are generated artifacts.

## Modes

| Invocation | Do |
|---|---|
| `/qa-backlog` (no args) | Full refresh: apply results, rescan merges, rewrite the backlog, report the top of the list |
| `/qa-backlog results` | Only apply submitted results (steps 1–2), then report what moved |
| `/qa-backlog scan` | Only rescan for new untested work (step 3) |
| `/qa-backlog <area>` | Refresh, then print only the items matching that area (e.g. `ecology`, `vessel`, `net`) |

## Step 1 — apply submitted results

```
python3 Tools/QA/apply_results.py --dry-run   # see what will move
python3 Tools/QA/apply_results.py             # write it
```

The script is deterministic and idempotent (`Docs/QA/.applied.json` records applied
sessions). It removes PASS items to `ARCHIVE.md`, re-marks FAIL 🔴 / PARTIAL 🟡 /
BLOCKED ⛔ in place, and opens a `DEV_TASKS.md` entry per failure.

Then do the part a script cannot:

- **Enrich each new dev task.** The script fills in symptom, build and the QA item.
  You add **Source of the change** (the PR that introduced it — the QA item's
  `**Source:**` line names it) and **Likely files** (read the PR diff or the item's
  reference doc). A dev task without a file pointer is a research assignment, not a task.
- **Read the free-text sections** of each results file ("Anything else"). If a tester
  reports something real that no item covers, add it as a new backlog item.
- If a result names an unknown ID, the script prints it under `UNKNOWN ids` — resolve
  it (a typo, or an item that was already archived) rather than dropping it silently.

## Step 2 — reopen fixed items

For every `DEV_TASKS.md` entry marked 🟢 (fixed, awaiting retest), confirm the fix is
actually merged into the branch under scan (`git log --oneline <sha>..HEAD -- <files>`),
then move its QA item back to the top of its priority tier with status ⬜ and delete the
dev-task entry. A fix that merged is untested work again — that is the whole point.

## Step 3 — rescan for new untested work

Find everything merged since the backlog's `Scan covers:` commit:

```
git log --oneline <last-scanned-sha>..HEAD --merges
```

For each merged PR, read its body — that is where authors record what they could not
run. Use the GitHub MCP tools; body search is far cheaper than reading every PR:

```
search_pull_requests  repo:froglet-studio/Cosmic-Shore is:merged merged:>=<date> "verify in editor"
search_pull_requests  repo:froglet-studio/Cosmic-Shore is:merged merged:>=<date> untested
search_pull_requests  repo:froglet-studio/Cosmic-Shore is:merged merged:>=<date> "play-test"
```

Requesting `fields: ["number","title","body"]` for many PRs at once will exceed the tool
output limit and be spilled to a file — that is fine and preferred: parse the saved JSON
with python and extract only the sections whose heading matches
`verif|test|editor|follow`, rather than reading whole bodies into context.

Also sweep in-repo records, which carry items PRs do not:
`Docs/UNITY_VERIFICATION_CHECKLIST.md`, `Docs/*/BUGS.md`, `Docs/*/TODOS.md`,
`Docs/*/BACKLOG.md`, `Docs/PRISM_CLOCK_FOLLOWUP_PROMPTS.md`, and the per-mode
`_Scripts/Controller/Arcade/*.md` "In-editor verification" sections.

**A finding becomes an item only if it is testable by a human in the editor.** Skip
anything already green in `ARCHIVE.md` unless the code changed again since it passed
(then it is a *new* item — say so in its `**Source:**` line). Skip pure engineering
follow-ups with no observable symptom; those belong in the owning doc's backlog, not here.

## Step 4 — write the item

Every item is self-contained. A tester must never need to open a PR to run it.

```
### QA-<AREA>-<SLUG> ⬜ — <one line, in plain language>
**Source:** PR #NNN (+ reference doc for detail).  [+ why it is risky, if not obvious]

1. Numbered steps, in the order to perform them. Name the exact scene, vessel, tool
   menu path and expected numbers. Put the cheapest disqualifying check first.

**PASS:** every condition that must hold, stated observably.
**FAIL:** the specific symptoms that mean failure, separated by ·.
[Optional] **Judgement call / Known, do not fail on:** taste questions and already-logged gaps.
```

**Write for the person who will actually run this.** The reader is a QA intern with
little or no engineering background, reading the item inside the Unity window
(`FrogletTools ▸ QA ▸ QA Session`), which renders each item's steps and PASS/FAIL
verbatim. So the item text IS the UI copy — jargon in it is jargon on their screen.

- Name the **action**, not the concept: "Open the project in Unity" beats "open the
  project on the branch under test"; "wait until Unity finishes importing" beats
  "wait for import to settle". A beginner who meets an unexplained term goes hunting
  for a control that matches it.
- Say **where** and **how** to do a thing the first time it appears — the exact menu
  path, window name, key, or on-screen target. "Enter freestyle (click the centre of
  the screen, or press **Y** on a gamepad)", not "tap the centre crystal" (there is
  no such object — that name is an insider memory of an older build).
- Insider shorthand that must always be expanded: *freestyle*, *the Console*,
  *reimport*, *MPPM*, *the branch under test*, *the lava lamp*. Team nicknames for
  systems are fine in `**Source:**` (engineering reads that line), never in a step.
- Keep steps one instruction each, in the order performed, present tense, no
  parenthetical asides about why — reasons belong in `**Source:**`.

Rules that keep the list usable:
- IDs are stable and kebab-case; reuse the existing ID when refreshing an item so
  history in `ARCHIVE.md` and `DEV_TASKS.md` still resolves.
- PASS/FAIL must be decidable by someone who has never read the code. "Works correctly"
  is not a criterion; "the ring fills on drift and does **not** fill flying straight" is.
- Fold duplicates: one item per observable behaviour, even if three PRs touched it.
- Carry the known-good exceptions into the item (e.g. Serpent is expected to fail the
  skimmer audit) or QA will file noise as failures.

## Step 5 — prioritise

- **P0** — gates: the build compiles/imports/boots; platform laws (prism occlusion,
  speed tunnel); anything nobody has ever seen work at all; whole new game modes;
  never-executed test suites; the asset auditors. If a P0 fails, most of P1 is unrunnable.
- **P1** — merged features that have never been played, and regressions in shipped
  systems.
- **P2** — cosmetic gaps, known-open bugs being re-confirmed, and data-gathering
  (benchmarks, device soaks) where the deliverable is numbers rather than a verdict.

Within a tier, order by blast radius: platform-wide before mode-specific before
single-vessel.

## Step 6 — update the header and report

Set the backlog header to the new generation date and `Scan covers: <HEAD sha> (PRs …)`.
Then tell the human, in this order:

1. What moved: N passed (archived), N failed (dev tasks DT-xxx), N new items found.
2. The **top five** items of the refreshed list, one line each.
3. Anything that needs a decision (unknown result IDs, a failure that contradicts a
   locked design, a P0 that blocks the rest).

Do not paste the whole backlog into chat — point at `Docs/QA/QA_BACKLOG.md`.

## Step 7 — commit

Commit `Docs/QA/**` (including the new results file if the human handed you one) on a
branch, never straight to a protected branch:

```
docs(qa): refresh untested-development backlog (<N> new, <N> archived, <N> failed)
```

If dev tasks were opened, say so in the body with their IDs so engineering can find them.
