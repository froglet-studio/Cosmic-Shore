# Branch archive: `claude/lucid-keller-0cvVl`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-05-22 by Claude
- **Unmerged commits:** 2
- **Forked from:** `6be3ed0b2` (2026-05-22, Merge pull request #526 from froglet-studio/claude/relaxed-bell-OXLPT)
- **Tip:** `4ed848f3d`
- **Files touched (2):**
  - `.github/workflows/unity-tests.yml`
  - `Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs`

### `20b50bd73` — fix(elementals): keep comeback baseline free of temporary modifiers

_Claude, 2026-05-22 21:26:20 +0000_

```text
ElementalComebackSystem.OnTurnStarted snapshotted each vessel's baseline
by reading GetNormalizedLevel back after ApplyInitialValues. Since the
537e309 buff/debuff refactor, GetNormalizedLevel returns the effective
level (base + active temporary modifiers), so a Squirrel overtake debuff
still decaying at a turn boundary was baked into the baseline. Because the
comeback target is recomputed as baseline + bonus from that frozen
baseline every tick, the debuff stayed depressed for the whole turn
instead of decaying — the exact "slam-and-recover clobbers progress" bug
the refactor set out to eliminate.

ApplyInitialValues now returns the configured base levels it writes, and
OnTurnStarted uses them directly. Behavior is identical when no temporary
modifier is active.
```

```text
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs | 14 ++++++++------
 1 file changed, 8 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index 2572137d7..a186fefd6 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -105,11 +105,9 @@ namespace CosmicShore.Gameplay
                 var vesselType = player.Vessel.VesselStatus.VesselType;
                 var config = comebackProfile.GetConfig(vesselType);
 
-                ApplyInitialValues(rs, config);
-
-                var baseline = new float[AllElements.Length];
-                for (int i = 0; i < AllElements.Length; i++)
-                    baseline[i] = rs.GetNormalizedLevel(AllElements[i]);
+                // Use the configured base directly as the baseline — GetNormalizedLevel returns the
+                // effective level and would fold a still-decaying overtake modifier into it.
+                var baseline = ApplyInitialValues(rs, config);
 
                 _baselines[player.Name] = baseline;
 
@@ -183,15 +181,19 @@ namespace CosmicShore.Gameplay
             }
         }
 
-        void ApplyInitialValues(ResourceSystem rs, SO_ElementalComebackProfile.VesselComebackConfig config)
+        // Writes the configured initial base levels onto the vessel and returns them as the comeback baseline.
+        float[] ApplyInitialValues(ResourceSystem rs, SO_ElementalComebackProfile.VesselComebackConfig config)
         {
+            var baseline = new float[AllElements.Length];
             for (int i = 0; i < AllElements.Length; i++)
             {
                 float initialLevel = config.GetInitialLevel(AllElements[i]);
                 // Clamp initial values to 0.0–1.5 range
                 float normalized = Mathf.Clamp(initialLevel / 10f, 0f, 1.5f);
                 rs.SetElementLevel(AllElements[i], normalized);
+                baseline[i] = normalized;
             }
+            return baseline;
         }
 
         // ---------------------------------------------------------------
```

</details>

### `4ed848f3d` — ci: add GameCI Unity Test Framework pipeline

_Claude, 2026-05-22 21:39:41 +0000_

```text
Adds a GitHub Actions workflow that runs the EditMode + PlayMode test
suites in Unity (version auto-detected from ProjectVersion.txt) on every
push to a claude/** branch and every pull request. This gives changes a
real verification loop — compile + test — without needing a local Unity
install.

Requires a one-time Unity license secret (UNITY_LICENSE for Personal, or
UNITY_SERIAL/UNITY_EMAIL/UNITY_PASSWORD for Pro/Plus) in repo Actions
secrets; runs fail at the activation step until one is set.
```

```text
 .github/workflows/unity-tests.yml | 74 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 74 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/.github/workflows/unity-tests.yml b/.github/workflows/unity-tests.yml
new file mode 100644
index 000000000..1de28e0a0
--- /dev/null
+++ b/.github/workflows/unity-tests.yml
@@ -0,0 +1,74 @@
+name: Unity Tests
+
+# Unity Test Framework CI — runs EditMode + PlayMode tests on every push to a
+# claude/** branch and on every pull request, so code changes can be verified
+# without a local Unity install. The Unity version is auto-detected from
+# ProjectSettings/ProjectVersion.txt.
+#
+# REQUIRED ONE-TIME SETUP — add a Unity license to the repo's Actions secrets
+# (Settings > Secrets and variables > Actions):
+#   * Unity Personal:  UNITY_LICENSE  — full contents of the .ulf license file
+#                      from the Unity activation flow.
+#   * Unity Pro/Plus:  UNITY_SERIAL + UNITY_EMAIL + UNITY_PASSWORD
+# Until one of those is set, runs fail at the Unity activation step.
+
+on:
+  push:
+    branches:
+      - 'claude/**'
+  pull_request:
+  workflow_dispatch:
+
+concurrency:
+  group: unity-tests-${{ github.ref }}
+  cancel-in-progress: true
+
+permissions:
+  checks: write
+  contents: read
+
+jobs:
+  test:
+    name: ${{ matrix.testMode }}
+    runs-on: ubuntu-latest
+    strategy:
+      fail-fast: false
+      matrix:
+        testMode:
+          - editmode
+          - playmode
+    steps:
+      - name: Checkout
+        uses: actions/checkout@v4
+        with:
+          lfs: false
+
+      - name: Cache Library
+        uses: actions/cache@v4
+        with:
+          path: Library
+          key: Library-${{ matrix.testMode }}-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}
+          restore-keys: |
+            Library-${{ matrix.testMode }}-
+            Library-
+
+      - name: Run ${{ matrix.testMode }} tests
+        id: tests
+        uses: game-ci/unity-test-runner@v4
+        env:
+          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
+          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
+          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
+          UNITY_SERIAL: ${{ secrets.UNITY_SERIAL }}
+        with:
+          testMode: ${{ matrix.testMode }}
+          githubToken: ${{ secrets.GITHUB_TOKEN }}
+          checkName: ${{ matrix.testMode }} results
+
+      - name: Upload test results
+        uses: actions/upload-artifact@v4
+        if: always()
+        with:
+          name: test-results-${{ matrix.testMode }}
+          path: ${{ steps.tests.outputs.artifactsPath }}
+          if-no-files-found: ignore
```

</details>
