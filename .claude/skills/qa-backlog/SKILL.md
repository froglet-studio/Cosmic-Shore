---
name: qa-backlog
description: Maintain the untested-development QA backlog and run the QA loop. Use when the user types /qa-backlog, asks to refresh/print/regenerate the QA backlog, apply submitted QA RESULTS files, archive passes, or open dev tasks for failures. Owns Docs/QA/QA_BACKLOG.md, ARCHIVE.md and DEV_TASKS.md — the tester never hand-edits those; they only submit a RESULTS file, and this skill applies it. Reads new merges since the last scan (PR bodies / merge messages carrying a "verification status" note) to add new untested items.
---

# /qa-backlog — refresh the untested-development backlog and run the loop

You own the three skill-managed files in `Docs/QA/`:

- `QA_BACKLOG.md` — THE list (items are `### QA-... <emoji> — title` sections).
- `ARCHIVE.md` — items that PASSED (removed from the backlog).
- `DEV_TASKS.md` — handoff tasks for FAILs.

The tester never edits those. They only ever add a `Docs/QA/RESULTS/<date>-<tester>.md`
file. Your job each invocation: **(A) apply submitted results, (B) scan new merges for new
untested items, (C) print the prioritised list, (D) commit the updates.** Full design and the
data model are in `Docs/QA/README.md` — read it if anything below is unclear.

Do all of this non-interactively; do not stop to ask the tester questions unless the repo is
in a state you genuinely cannot proceed from (e.g. `Docs/QA/` does not exist at all).

## 1. Apply submitted results (the deterministic half — the "close")

Run the engine; it reads every `RESULTS/*.md` (except `TEMPLATE.md`), takes the latest verdict
per item, and rewrites the three files:

```
python3 Tools/QA/apply_results.py
```

- PASS → the item's section moves to `ARCHIVE.md` and leaves the backlog.
- FAIL → heading marked 🔴, a `> **Last result:**` line added, a `DEV_TASKS.md` entry upserted.
- PARTIAL → heading marked 🟡 + the note. BLOCKED → ⛔ + the blocker. SKIP → no change.

It is idempotent, so it is safe to run every time. Use `--check` first if you want to preview
what will change without writing. Read the script's stdout and carry its summary into your
final report to the tester (e.g. "applied QA-EDITMODE-TESTS → 🔴, opened a dev task").

If the tester's RESULTS file references an item ID that is not in the backlog, say so plainly
in your report (a typo, or an item already archived) — do not invent a section for it.

## 2. Scan new merges for new untested items (the non-deterministic half — the "open")

This is the part only you can do. Determine what has merged since the last scan and add any
new untested work as fresh `### QA-... ⬜ — title` sections.

- The `Generated:` / `Scan covers:` line at the top of `QA_BACKLOG.md` records the last scan
  point (a commit SHA and PR range). Find merges after it:
  - `git fetch origin bleeding-edge` (retry with backoff on network failure).
  - `git log --oneline --merges <last-sha>..origin/bleeding-edge`
- For each new merge, read the PR body / merge-commit message for a **Verification status**
  section (what the author could not run, what a human must verify). If it names untested
  behaviour, write a new backlog item for it: a stable `QA-<AREA>-<SHORT>` id, a one-line
  title, a `Source:` line (PR number), numbered steps, and explicit PASS / FAIL definitions —
  match the shape of the existing items exactly.
- Place each new item under the right priority section (`## Priority 0/1/2`). A gate
  (compile, platform law, a new game mode) is P0; a merged-but-never-played feature is P1;
  cosmetic / data-gathering is P2.
- Update the `Generated:` / `Scan covers:` header line to the new scan point.
- If there are zero new merges since the last scan, say so and skip this step — do not
  fabricate items.

Never duplicate an item that already exists (match on the `QA-...` id). Never resurrect an id
that is present in `ARCHIVE.md` unless a later merge genuinely re-opened that work — if so,
note why in the item's Source line.

## 2.5. Refresh the "⚡ Quick wins" section

`QA_BACKLOG.md` opens with a hand-curated `## ⚡ Quick wins` block (before `## Priority 0`).
Rewrite it every run to point at the ~5–8 **open** items (⬜, or an actionable 🟡) that are
the fastest / lowest-effort to get a clean verdict on — asset-only checks, one-glance visual
checks, a single short flight, an editor-window check. Rank by least effort, not by priority.

- **Only list open items.** Drop anything now PASS (archived), 🔴, or ⛔ — never point a tester
  at a dead/failed item as a "quick win".
- Give each a half-line on *why* it's quick (what the one check is).
- It lives before the first `### QA-` heading, so it is raw pass-through text the apply engine
  never rewrites — you are its only maintainer. Keep it a bullet list (no `### QA-` headings, or
  the engine will parse them as duplicate item sections).
- Say in the block that it is refreshed each run and can lag reality by one submission.

## 3. Print the prioritised list

After steps 1–2, print the current backlog top-down (P0 first), one line per item:
`<emoji> <QA-ID> — <title>` plus the `Last result` note if present. Call out at the top:
how many items are ⬜ never-run, 🟡 partial, 🔴 failed, ⛔ blocked; what you archived this run;
what dev tasks you opened. This printed list is the tester's working queue.

## 4. Commit the skill-owned files

Commit only the files this skill owns (never the tester's RESULTS file — they committed that
themselves, and never unrelated working-tree changes):

```
git add Docs/QA/QA_BACKLOG.md Docs/QA/ARCHIVE.md Docs/QA/DEV_TASKS.md
git commit -m "qa(backlog): apply results + rescan merges"
```

Then push to the current working branch with `git push -u origin <branch>` (retry with
backoff). Do not push to a protected branch (`bleeding-edge`, `main`) — if that is the current
branch, stop and tell the tester to move to a working branch first.

## Guardrails

- **The tester never hand-edits `QA_BACKLOG.md`.** If they ask "where do I put my results",
  the answer is always: copy `RESULTS/TEMPLATE.md` → `RESULTS/<date>-<name>.md`, fill the
  table, commit. You apply it.
- **Fail loud on a broken RESULTS file** (missing build line, an unknown result value): report
  it; the apply engine already ignores rows whose result is not one of
  PASS/FAIL/PARTIAL/BLOCKED/SKIP, so name any row it skipped.
- **Do not change item PASS/FAIL criteria to make a result fit.** The criteria are the
  contract; a result either meets them or it does not.
- The engine and this skill are the only writers of the three owned files. If you find manual
  edits in them, reconcile by re-deriving from the RESULTS files, and mention the drift.
