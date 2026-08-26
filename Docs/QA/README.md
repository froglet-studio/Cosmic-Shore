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
| `RESULTS/TEMPLATE.md` | The blank form. You normally get a pre-filled copy instead — see below. | — |
| `Tools/QA/submit.py` | **The Submit button.** Checks the required fields, refuses while any are missing, then publishes. | — |
| `RESULTS/YYYY-MM-DD-<tester>.md` | One submitted test session. | You |
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
  QA runs items, fills in a results file, presses Submit
            │       (Tools/QA/submit.py — validates, then publishes)
            │
            ▼
  /qa-backlog  ──►  passes leave the list forever (ARCHIVE.md)
                    failures become DEV_TASKS.md entries with repro notes
                    (and stay on the list, marked FAILED, until re-fixed)
```

---

# For QA — how to run a session

**The easiest way to run a session is inside Unity: `FrogletTools ▸ QA ▸ QA Session`.**
That window is this whole workflow as a form — it creates your session file, lists the
backlog, and for every item shows **the numbered steps, the exact PASS and FAIL
definitions, and the known pre-existing defects you must not fail on**, right next to
the verdict dropdown. It has the Submit button, an Attach button for evidence, and a
one-click "Console" button that saves the Editor log against an item. If you use the
window, steps 1 and 4–6 below all happen in it; you still do steps 2–3 (get the build,
run the items) yourself, because looking at the game is the whole job. The rest of
this section is the same workflow by hand, for when Unity or the window is unavailable.

**Read this whole section once before your first session.** It assumes no prior
knowledge of this repo, the backlog, or git. Everything you need is a copy-paste
command or a decision with the answer written down.

**The one thing to understand first:** Claude cannot open Unity. That is the entire
reason this backlog exists. You are the only one who can actually *see* whether any
of this works. Your job is to look, and to write down exactly what you saw. Claude's
job is the paperwork around that.

**Your deliverable is one file:** `Docs/QA/RESULTS/<date>-<yourname>.md`, containing
a table of verdicts. Everything below is how to produce it.

---

## Step 1 — ask for a session

In Claude Code, say:

> **"Set up a QA session for me — I want to run the top N items."**

Claude will:
- tell you **which branch to test** and the exact commands to get it,
- create `Docs/QA/RESULTS/<today>-<yourname>.md` — a pre-filled form with the session
  metadata (branch, commit, Unity version) already correct and one scratch section per
  item you're running,
- push it so it exists on your machine after you pull.

You *can* work from `RESULTS/TEMPLATE.md` by hand instead, but don't — the pre-filled
form has the commit SHA, the known-exception lists and per-step scratch space already
in it, and those are the parts people get wrong.

**There is nothing to rename, ever.** The file keeps one name from creation until it
merges. Nothing in it is published until you press Submit (step 6), so you can leave
it half-finished for as long as you like — including across several days — without
anyone else picking up your unfinished work.

## Step 2 — get the build

Run these in a terminal at the repo root. Claude gives you the branch name in step 1;
unless told otherwise it is `bleeding-edge`.

```bash
git fetch origin
git checkout <branch-name>
git pull origin <branch-name>
git rev-parse --short HEAD      # note this — it must match the Commit row in your file
```

If the SHA does not match the `Commit` row in your form, Submit will stop you in
step 6 and offer `--accept-head`. Use that **only if the checked-out build really is
the one you tested** — otherwise check out the build you tested. A result recorded
against the wrong build is worse than no result: it sends engineering hunting through
the wrong diff.

Then open the project in Unity **6000.3.17f1** and:

1. **Let it fully reimport before you judge anything.** A stale `Library/` folder
   hides asset changes and is by far the most common cause of a false failure. If
   assets look wrong, use *Assets ▸ Reimport All* once, wait, and look again.
2. In the Console window, turn **Error Pause OFF** and **Clear on Play OFF**. You
   need the whole log to survive the session.
3. Leave the Console open and visible the entire time.

---

## Step 3 — run items, top-down

Open `Docs/QA/QA_BACKLOG.md`. **It is already prioritised — work from the top.**
P0 items are gates: if one fails, most of P1 cannot be judged, so a failure high on
the list is a reason to stop and report, not to push on.

Every item is self-contained. It gives you numbered steps, an explicit **PASS**
definition and an explicit **FAIL** definition. You never need to open a PR or read
code to run one.

Three habits that make a session worth having:

**Do the steps in order.** The cheapest disqualifying check is deliberately first.

**Honour "Known, do not fail on".** Many items list pre-existing defects that are
already logged elsewhere. Seeing one is not a failure of that item. Filing it as one
buries a real signal in noise.

**Do not fix anything.** If you find a compile error, a missing script, a bad value —
write it down and move on. Fixing it destroys the evidence and means the fix itself
lands untested, which is how things got here. The exception is where an item
explicitly asks you to change something (a test-harness slider, a tuning field); put
it back afterwards.

### Multi-day sessions

Working through a long list across several days is expected, not a special case.
Add rows as you finish items and press Submit whenever you want what you have so far
to reach engineering — a failed P0 gate on day one should not wait until day three.
Each Submit publishes only what is new; finished rows stay in the file untouched.

**One rule, and it is the whole reason this stays clean: a retest is a NEW session
file, never an edit.** Once a verdict is published it is frozen. If an item you
failed gets fixed and you retest it, that is a new session on a new build — start a
new file. Editing a published row is refused by name, so you cannot do it by accident.

### Recording as you go

Fill in the scratch section for the item **while you are looking at it**, not from
memory afterwards. Paste console text **verbatim** — full text, including
`file:line`. "Some null ref errors" is not actionable; the exact exception with its
stack frame is a dev task someone can pick up cold.

---

## Step 4 — decide a verdict

One verdict per item you ran. Use **exactly** one of these five words:

| Verdict | Use it when | What happens next |
|---|---|---|
| `PASS` | Every condition in the item's PASS line held. | Item is **deleted** from the backlog forever and archived. |
| `FAIL` | Any symptom in the item's FAIL line occurred. | Item stays, marked 🔴, and a `DT-NNN` dev task opens. |
| `PARTIAL` | Some steps passed; at least one is **unproven** — not failed. | Item stays, marked 🟡, with your note about what is left. |
| `BLOCKED` | You could not get far enough to judge (build broken, no second machine, no device). | Item stays, marked ⛔, with the blocker. |
| `SKIP` | You deliberately did not run it. | Nothing changes. |

**`PASS` is a strong claim.** It removes the item permanently — a future rescan will
not bring it back. If you did not actually check every condition on the PASS line,
the honest verdict is `PARTIAL`, and that is a perfectly good session outcome.

**"It didn't work but I'm not sure it's the code" is `BLOCKED`, not `FAIL`.** Say what
stopped you and let someone else decide.

### Notes: required for anything that is not PASS

The Notes cell becomes the dev task's symptom line, so write it for someone who was
not there. Name **which step number** and **what you saw**.

Good:

> `Step 3: every prism renders magenta on load. Console: Shader error in 'Prism/BlockGraph': undeclared identifier 'UNITY_MATRIX_V' at line 88. Reimport All did not clear it. Screenshot: evidence/QA-PRISM-OCCLUSION-magenta.png`

Not good:

> `broken` · `didn't work` · `looks wrong` · `see screenshot`

If the item ends with **"Judgement call to report"** or **"Report the feel"**, that is
not optional — it is a real question someone is waiting on, and a one-sentence opinion
from a person who just played it is the whole point. Put it in the *Anything else*
section.

---

## Step 5 — fill the table

At the top of your form is the results table, fenced by two HTML comments:

```
<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|
| QA-BUILD-COMPILE | PASS |  |
| QA-PRISM-OCCLUSION | FAIL | Step 3: every prism magenta on load. Console: ... |

<!-- /qa-results-table -->
```

Three rules. Submit checks all three, so getting one wrong costs you a re-run, not a
lost result — but knowing them saves the round trip:

1. **Never delete the `<!-- qa-results-table -->` markers.** They confine parsing to
   this table. Without them the *whole file* is scanned and example rows in your
   scratch notes become real verdicts.
2. **Spell the verdict exactly** — `PASS`, `FAIL`, `PARTIAL`, `BLOCKED`, `SKIP`.
   Case does not matter (`pass` is fine). Anything else — `PASSED`, `OK`, `✓`, `n/a` —
   would be dropped silently by the applier, which is exactly why Submit refuses it
   first.
3. **Copy item IDs exactly** from the backlog, e.g. `QA-BUILD-COMPILE`.

Leave out any item you did not run. There is no need to write `SKIP` rows.

### Evidence

Put screenshots, clips, console dumps and profiler captures in:

```
Docs/QA/RESULTS/evidence/<your-session-filename>/
```

Name each file after the item it belongs to (`QA-PRISM-OCCLUSION-magenta.png`) and
reference it from the Notes cell. **Attach evidence for every FAIL** — a screenshot
of the Console is usually enough, and it is what turns "I saw it" into something
reproducible.

---

## Step 6 — press Submit

```bash
python3 Tools/QA/submit.py
```

This is the Submit button. It checks the form, and **refuses while anything required
is missing**, naming the row and what to do:

```
Checking Docs/QA/RESULTS/2026-08-14-caleb.md
  ✗ QA-MENU-CAMERA-RIG    has no verdict
      → fill it in, or delete the row if you did not run this item
  ✗ QA-BUILD-WINDOWS-PLAYER  is FAIL but Notes is empty
      → say which step number failed and what you saw — this text becomes the
        dev task an engineer picks up cold
  ⚠ QA-BUILD-WINDOWS-PLAYER  is a FAIL with no evidence file referenced

NOT SUBMITTED — 2 problem(s) to fix.
```

`✗` blocks; `⚠` is advice you can ignore. Fix and re-run until it says:

```
SUBMITTED — 3 new verdict(s) ready to apply.
```

**What it checks:** every row has a verdict spelled exactly right (a misspelled one
would otherwise be dropped without a word); anything that is not `PASS` carries a
note; no row is left blank; the commit you recorded matches the build you have
checked out; the table markers are intact; every ID is a real open backlog item; no
published verdict has been edited.

Two flags worth knowing:

- `--check` validates and reports without publishing. Safe any time.
- `--accept-head` records the currently checked-out commit as the build you tested.
  Use it **only if that is true** — if you tested an older build, check that build
  out instead. A result filed against the wrong build sends engineering into the
  wrong diff.

## Step 7 — submit

**In the QA Session window this step does not exist** — pressing **Submit session**
validates, publishes, commits and pushes your results in one go, then tells you what
happened and offers you the next test. The rest of this step is the by-hand equivalent.

Say to Claude:

> **"My QA results are in `Docs/QA/RESULTS/<date>-<yourname>.md` — apply them."**

Claude runs `apply_results.py`, enriches each new dev task with the source PR and the
likely files, commits everything on a branch and pushes. That enrichment is what makes
a failure pickup-able by an engineer with no context, so let Claude do it.

**If Claude is not available**, land the file yourself and it will be picked up on the
next `/qa-backlog` run:

```bash
git checkout -b qa/results-<date>-<yourname>
git add Docs/QA/RESULTS/ Docs/QA/.applied.json
git commit -m "docs(qa): <yourname> session results, <date>"
git push -u origin qa/results-<date>-<yourname>
```

Then open a pull request against `bleeding-edge`. Commit `.applied.json` along with
the file — it is what records that your session was submitted.

**Never hand-edit `QA_BACKLOG.md`, `ARCHIVE.md` or `DEV_TASKS.md`.** They are
generated. Hand edits are silently overwritten on the next run.

## Troubleshooting

| Symptom | What it means | Do this |
|---|---|---|
| `not submitted yet` | You have not pressed Submit. | `python3 Tools/QA/submit.py` |
| `edited since it was submitted` | You changed the file after submitting. Nothing new publishes until you re-submit. | `python3 Tools/QA/submit.py` again. |
| `FROZEN … a retest is a NEW session file` | You edited a verdict that was already published. | Restore that row. Put the retest in a new session file. |
| A row you filled in is missing after applying | Misspelled verdict. Submit catches this, so it means Submit was skipped. | Always publish via `submit.py`, never by hand. |
| `UNKNOWN ids (ignored, check for typos)` | The ID is not in the current backlog. | Copy-paste it from `QA_BACKLOG.md`. If it looks right, it may already be in `ARCHIVE.md` — tell Claude. |
| Rows from your scratch notes got applied | The `<!-- qa-results-table -->` markers were deleted. | Restore both markers around the real table. |
| Assets look wrong / shaders magenta / prefabs empty | Very often a stale `Library/`. | *Assets ▸ Reimport All*, wait, look again. Only then record it. |
| `says <sha> but you have <sha> checked out` | The form names a different build than you have. | If you tested the checked-out build, `submit.py --accept-head`. Otherwise check out the build you tested. |
| Half the backlog is unrunnable | A P0 gate is failing. | Record the P0 failure with full console text, mark the rest `BLOCKED`, and stop. That is a complete, useful session. |

---

## For engineering — how failures come back

`DEV_TASKS.md` is the handoff surface. Each failure becomes one task with the
originating QA item ID, the build it failed on, the observed symptom, the suspected
files (carried over from the item's source PR) and a definition of done that is
literally "QA item `<ID>` passes".

Pick tasks from there. When your fix merges, the QA item returns to the top of its
priority tier automatically, because a fix that has merged is untested work again —
that is the whole point of the loop.

---

## For whoever adds new work

If you land a change you could not verify in the editor, you do not need to touch
this folder — write a **Verification status** section in the PR body saying plainly
what was *not* run and what a human must do. The `/qa-backlog` scan reads PR bodies
and merge-commit messages, and that is where new items come from.

Be specific. "Needs testing" produces a useless item; "the bay-open animation is a
cross-FBX clip binding and only the editor can prove it binds — if it fails the bay
simply never animates and the projectile still spawns" produces an item QA can
actually run. Being vague there is how work goes untested silently.
