# QA session results

## Session

| Field | Value |
|---|---|
| Tester | akouroshm |
| Date | 2026-10-08 |
| Branch | bleeding-edge |
| Commit | `5663cc4b3` |
| Unity version | 6000.3.17f1 |
| Platform(s) | Editor |
| Submitted | yes — 2026-10-08 03:57 · 3 verdict(s), 3 new this run |
## Results

<!-- qa-results-table -->

| ID | Result | Notes |
|---|---|---|
| QA-STATE-RESET | PASS | Runtime game state resets cleanly between sessions — score, intensity, player count and domain all start fresh on a second launch, including via Play Again. No leakage observed. (Observed on 71d67ba9b.) |
| QA-MENU-VEIL-PAUSE | FAIL | The menu-return/loading veil does not hide the build: prisms are visibly **popping in during loading**, and on entering a game the player sees a **"metal seal" opening up** — an animation that appears to be a remnant of an old loading-screen concept, not the current veil. The teardown/build is not being covered. (Observed on 71d67ba9b.) |
| QA-AI-SKIMRACE | PASS | The Squirrel Skim Race AI races and wins cleanly — it cleared the course in **83 s** and **86 s** over two takes with no issues (orbiting, stalling, off-track or failing to finish). Both within the ~80 s target band. (Observed on 71d67ba9b.) |

<!-- /qa-results-table -->

## Evidence

(none attached)

## Anything else

The "metal seal opening" animation on game entry (QA-MENU-VEIL-PAUSE) looks like leftover
art/behaviour from a previous loading-screen design — worth confirming whether it is an
intended element of the current veil or a vestige that should be removed.
