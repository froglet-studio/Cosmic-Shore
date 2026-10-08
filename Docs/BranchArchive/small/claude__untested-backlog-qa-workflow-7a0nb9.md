# Branch archive: `claude/untested-backlog-qa-workflow-7a0nb9`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-08-14 by FenrysUnchained
- **Unmerged commits:** 3
- **Forked from:** `3c607fe03` (2026-08-04, Merge branch 'bleeding-edge' of https://github.com/froglet-studio/Cosmic-Shore)
- **Tip:** `2bbd63d52`
- **Files touched (1):**
  - `Docs/QA/RESULTS/2026-08-14-<caleb>.md`

### `d85403a1c` — This is the first failing attempt to use this process

_FenrysUnchained, 2026-08-14 11:50:18 -0700_

```text
This file serves as a the first result in this process experiment. A genuine attempt to use the process as described in the first version of the files on this branch.
```

```text
 Docs/QA/RESULTS/2026-08-14-<caleb>.md | 42 ++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 42 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-14-<caleb>.md b/Docs/QA/RESULTS/2026-08-14-<caleb>.md
new file mode 100644
index 000000000..74dba1040
--- /dev/null
+++ b/Docs/QA/RESULTS/2026-08-14-<caleb>.md
@@ -0,0 +1,42 @@
+# QA session results
+
+Copy this file to `Docs/QA/RESULTS/YYYY-MM-DD-<tester>.md`, fill it in, commit it.
+Do not edit `QA_BACKLOG.md` — the `/qa-backlog` skill reads this file and updates
+the backlog, the archive and the dev-task list for you.
+
+## Session
+
+| Field | Value |
+|---|---|
+| Tester | *your name* |
+| Date | YYYY-MM-DD |
+| Branch | bleeding-edge |
+| Commit | `git rev-parse --short HEAD` output |
+| Unity version | 6000.x.y |
+| Platform(s) | Editor (Windows/macOS) · Android device · iOS device · MPPM Nx |
+
+## Results
+
+One row per item you ran. `Result` must be exactly one of
+`PASS` · `FAIL` · `PARTIAL` · `BLOCKED` · `SKIP`.
+The `Notes` column is required for anything that is not `PASS`: say **which step
+number** and **what you saw**, verbatim where possible.
+
+<!-- qa-results-table -->
+
+| ID | Result | Notes |
+|---|---|---|
+| QA-BUILD-COMPILE | PASS |  |
+| QA-PRISM-OCCLUSION | FAIL | Step 1: every prism magenta on load. Console: `Shader error in 'Prism/BlockGraph': undeclared identifier 'UNITY_MATRIX_V' at line 88`. Screenshot attached. |
+
+<!-- /qa-results-table -->
+
+## Evidence
+
+Attach or link screenshots, clips, console dumps, profiler captures. Reference them
+by item ID.
+
+## Anything else
+
+Feel, tuning opinions, and things that were not on the list but looked wrong. These
+do not change any item's status, but they are read when the backlog is regenerated.
```

</details>

### `343c39300` — Revise QA results for 2026-08-14 session

_FenrysUnchained, 2026-08-14 11:58:54 -0700_

```text
Updated tester name, date, and branch in QA results.
```

```text
 Docs/QA/RESULTS/2026-08-14-<caleb>.md | 9 ++++-----
 1 file changed, 4 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-14-<caleb>.md b/Docs/QA/RESULTS/2026-08-14-<caleb>.md
index 74dba1040..1d502f88c 100644
--- a/Docs/QA/RESULTS/2026-08-14-<caleb>.md
+++ b/Docs/QA/RESULTS/2026-08-14-<caleb>.md
@@ -8,9 +8,9 @@ the backlog, the archive and the dev-task list for you.
 
 | Field | Value |
 |---|---|
-| Tester | *your name* |
-| Date | YYYY-MM-DD |
-| Branch | bleeding-edge |
+| Tester | caleb |
+| Date | 2026-08-14 |
+| Branch | untested-backlog-qa-workflow |
 | Commit | `git rev-parse --short HEAD` output |
 | Unity version | 6000.x.y |
 | Platform(s) | Editor (Windows/macOS) · Android device · iOS device · MPPM Nx |
@@ -26,8 +26,7 @@ number** and **what you saw**, verbatim where possible.
 
 | ID | Result | Notes |
 |---|---|---|
-| QA-BUILD-COMPILE | PASS |  |
-| QA-PRISM-OCCLUSION | FAIL | Step 1: every prism magenta on load. Console: `Shader error in 'Prism/BlockGraph': undeclared identifier 'UNITY_MATRIX_V' at line 88`. Screenshot attached. |
+| QA-BUILD-COMPILE |  |  |
 
 <!-- /qa-results-table -->
 
```

</details>

### `2bbd63d52` — Delete Docs/QA/RESULTS/2026-08-14-<caleb>.md

_FenrysUnchained, 2026-08-14 11:59:59 -0700_

```text
 Docs/QA/RESULTS/2026-08-14-<caleb>.md | 41 -----------------------------------------
 1 file changed, 41 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/QA/RESULTS/2026-08-14-<caleb>.md b/Docs/QA/RESULTS/2026-08-14-<caleb>.md
deleted file mode 100644
index 1d502f88c..000000000
--- a/Docs/QA/RESULTS/2026-08-14-<caleb>.md
+++ /dev/null
@@ -1,41 +0,0 @@
-# QA session results
-
-Copy this file to `Docs/QA/RESULTS/YYYY-MM-DD-<tester>.md`, fill it in, commit it.
-Do not edit `QA_BACKLOG.md` — the `/qa-backlog` skill reads this file and updates
-the backlog, the archive and the dev-task list for you.
-
-## Session
-
-| Field | Value |
-|---|---|
-| Tester | caleb |
-| Date | 2026-08-14 |
-| Branch | untested-backlog-qa-workflow |
-| Commit | `git rev-parse --short HEAD` output |
-| Unity version | 6000.x.y |
-| Platform(s) | Editor (Windows/macOS) · Android device · iOS device · MPPM Nx |
-
-## Results
-
-One row per item you ran. `Result` must be exactly one of
-`PASS` · `FAIL` · `PARTIAL` · `BLOCKED` · `SKIP`.
-The `Notes` column is required for anything that is not `PASS`: say **which step
-number** and **what you saw**, verbatim where possible.
-
-<!-- qa-results-table -->
-
-| ID | Result | Notes |
-|---|---|---|
-| QA-BUILD-COMPILE |  |  |
-
-<!-- /qa-results-table -->
-
-## Evidence
-
-Attach or link screenshots, clips, console dumps, profiler captures. Reference them
-by item ID.
-
-## Anything else
-
-Feel, tuning opinions, and things that were not on the list but looked wrong. These
-do not change any item's status, but they are read when the backlog is regenerated.
```

</details>
