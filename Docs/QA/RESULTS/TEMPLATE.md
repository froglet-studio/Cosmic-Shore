# QA session results

**You normally do not start from this file.** Ask Claude Code to *"set up a QA
session"* and you get a pre-filled copy with the branch, commit SHA and Unity version
already correct, plus scratch space for each item you are running. Those are the parts
people get wrong by hand. Full instructions: `Docs/QA/README.md` § "For QA — how to
run a session".

Use this blank form only if Claude is unavailable. Copy it to
`Docs/QA/RESULTS/YYYY-MM-DD-<yourname>.md` and fill it in. **There is nothing to
rename.** Nothing is published until you press Submit:

```
python3 Tools/QA/submit.py
```

Submit checks the required fields, refuses while any are missing, tells you exactly
what to fix, and only then publishes. Add rows across as many days as you like and
run it again each time — only the new verdicts are published.

**A published verdict is frozen: a retest is a NEW session file, never an edit.**

Do not edit `QA_BACKLOG.md`, `ARCHIVE.md` or `DEV_TASKS.md`. They are generated.

## Session

| Field | Value |
|---|---|
| Tester | *your name* |
| Date | YYYY-MM-DD |
| Branch | bleeding-edge |
| Commit | `git rev-parse --short HEAD` output |
| Unity version | 6000.3.17f1 |
| Platform(s) | Editor (Windows/macOS) · Android device · iOS device · MPPM Nx |
| Submitted | *written by submit.py — leave alone* |

## Results

One row per item you ran. Omit items you did not run.

**`Result` must be spelled exactly** `PASS` · `FAIL` · `PARTIAL` · `BLOCKED` · `SKIP`
(case does not matter). Anything else — `PASSED`, `OK`, `✓` — would be dropped
silently when the results are applied, which is why Submit refuses it first.

**Never delete the two `qa-results-table` markers.** They confine parsing to this
table. Without them the whole file is scanned and stray example rows get applied as
real verdicts.

`Notes` is required for anything that is not `PASS`: say **which step number** and
**what you saw**, verbatim where possible.

<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|
| QA-BUILD-COMPILE | PASS |  |
| QA-PRISM-OCCLUSION | FAIL | Step 3: every prism magenta on load. Console: `Shader error in 'Prism/BlockGraph': undeclared identifier 'UNITY_MATRIX_V' at line 88`. Reimport All did not clear it. Screenshot: `evidence/2026-08-14-caleb/QA-PRISM-OCCLUSION-magenta.png` |

<!-- /qa-results-table -->

`python3 Tools/QA/submit.py --check` validates without publishing — safe any time.

## Evidence

Put files in `Docs/QA/RESULTS/evidence/<this-filename>/`, name them after the item,
and reference them from the Notes cell. Attach evidence for every `FAIL`.

## Anything else

Feel, tuning opinions, and things that were not on the list but looked wrong. These
do not change any item's status, but they are read when the backlog is regenerated
and can become new items.

If an item ended with **"Judgement call to report"** or **"Report the feel"**, answer
it here — that is a real question someone is waiting on.
