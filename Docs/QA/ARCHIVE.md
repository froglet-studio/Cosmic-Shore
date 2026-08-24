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

<!-- /qa-archive -->
