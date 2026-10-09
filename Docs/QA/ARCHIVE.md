# QA archive — items that passed

Written by the `/qa-backlog` skill. Its job is memory: a passed item is removed
from `QA_BACKLOG.md`, and this file is what stops the next scan from resurrecting
it when it re-reads the same old PR body.

An item can come back onto the backlog only if the code it covers **changes again**
— then it returns as a new item with a fresh scan date, and the row below stays as
the record of when it last passed and on what build.

<!-- qa-archive -->

| ID | Passed on (commit) | Date | Tester | Notes |
|---|---|---|---|---|
| QA-BUILD-COMPILE | `5144ad269` | 2026-08-14 | Caleb |  |
| QA-P2-SPAWN-MATRIX-MOONS | `3ba8ea1d2` | 2026-10-05 | akouroshm | The four element-crystal moons on the Spawn Matrix bench are visible and distinct, sitting clear of the toy body sphere. |
| QA-STATE-RESET | `5663cc4b3` | 2026-10-08 | akouroshm | Runtime game state resets cleanly between sessions — score, intensity, player count and domain all start fresh on a second launch, including via Play Again. No leakage observed. (Observed on 71d67ba9b.) |
| QA-AI-SKIMRACE | `5663cc4b3` | 2026-10-08 | akouroshm | The Squirrel Skim Race AI races and wins cleanly — it cleared the course in **83 s** and **86 s** over two takes with no issues (orbiting, stalling, off-track or failing to finish). Both within the ~80 s target band. (Observed on 71d67ba9b.) |

<!-- /qa-archive -->
