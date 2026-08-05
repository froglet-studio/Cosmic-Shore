# QA session results

Copy this file to `Docs/QA/RESULTS/YYYY-MM-DD-<tester>.md`, fill it in, commit it.
Do not edit `QA_BACKLOG.md` — the `/qa-backlog` skill reads this file and updates
the backlog, the archive and the dev-task list for you.

## Session

| Field | Value |
|---|---|
| Tester | *your name* |
| Date | YYYY-MM-DD |
| Branch | bleeding-edge |
| Commit | `git rev-parse --short HEAD` output |
| Unity version | 6000.x.y |
| Platform(s) | Editor (Windows/macOS) · Android device · iOS device · MPPM Nx |

## Results

One row per item you ran. `Result` must be exactly one of
`PASS` · `FAIL` · `PARTIAL` · `BLOCKED` · `SKIP`.
The `Notes` column is required for anything that is not `PASS`: say **which step
number** and **what you saw**, verbatim where possible.

<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|
| QA-BUILD-COMPILE | PASS |  |
| QA-PRISM-OCCLUSION | FAIL | Step 1: every prism magenta on load. Console: `Shader error in 'Prism/BlockGraph': undeclared identifier 'UNITY_MATRIX_V' at line 88`. Screenshot attached. |

<!-- /qa-results-table -->

## Evidence

Attach or link screenshots, clips, console dumps, profiler captures. Reference them
by item ID.

## Anything else

Feel, tuning opinions, and things that were not on the list but looked wrong. These
do not change any item's status, but they are read when the backlog is regenerated.
