# Branch archive: `claude/add-unit-tests-YLJDy`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-26 by Claude
- **Unmerged commits:** 2
- **Forked from:** `6f95d5384` (2026-02-26, Merge pull request #171 from froglet-studio/claude/update-claude-md-6Sh2c)
- **Tip:** `58583ba32`
- **Files touched (3):**
  - `Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs`
  - `Assets/_Scripts/Tests/EditMode/GeometryUtilsTests.cs`
  - `Assets/_Scripts/Tests/EditMode/XpDataTests.cs`

### `5c4152baa` — fix(tests): update GameModes expected member count from 34 to 35

_Claude, 2026-02-26 06:26:04 +0000_

```text
MultiplayerCrystalCapture (35) was added to GameModes, bringing the
total from 34 to 35 members. Updated the enum integrity test to match.
```

```text
 Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs b/Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs
index 9288dadf5..692c03740 100644
--- a/Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs
+++ b/Assets/_Scripts/Tests/EditMode/EnumIntegrityTests.cs
@@ -137,7 +137,7 @@ namespace CosmicShore.Tests
         public void GameModes_HasExpectedMemberCount()
         {
             var values = Enum.GetValues(typeof(GameModes));
-            Assert.AreEqual(34, values.Length,
+            Assert.AreEqual(35, values.Length,
                 "GameModes member count changed. Update tests if a game mode was added/removed.");
         }
 
```

</details>

### `58583ba32` — fix(tests): resolve namespace issues in GeometryUtilsTests and XpDataTests

_Claude, 2026-02-26 06:34:15 +0000_

```text
- Add explicit `using CosmicShore.Utility` to GeometryUtilsTests instead of
  relying on implicit parent namespace resolution from CosmicShore.Tests
- Simplify all `Utility.GeometryUtils.*` references to `GeometryUtils.*`
- Remove unused `using CosmicShore.Data` from XpDataTests (XpData is in CosmicShore.Core)
```

```text
 Assets/_Scripts/Tests/EditMode/GeometryUtilsTests.cs | 45 +++++++++++++++++++++++----------------------
 Assets/_Scripts/Tests/EditMode/XpDataTests.cs        |  1 -
 2 files changed, 23 insertions(+), 23 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 206 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Tests/EditMode/GeometryUtilsTests.cs b/Assets/_Scripts/Tests/EditMode/GeometryUtilsTests.cs
index 837f47c87..291c28f00 100644
--- a/Assets/_Scripts/Tests/EditMode/GeometryUtilsTests.cs
+++ b/Assets/_Scripts/Tests/EditMode/GeometryUtilsTests.cs
@@ -1,6 +1,7 @@
 using System.Collections.Generic;
 using NUnit.Framework;
 using UnityEngine;
+using CosmicShore.Utility;
 
 namespace CosmicShore.Tests
 {
@@ -21,7 +22,7 @@ namespace CosmicShore.Tests
         [Test]
         public void LineData_Constructor_SetsStart()
         {
-            var line = new Utility.GeometryUtils.LineData(
+            var line = new GeometryUtils.LineData(
                 new Vector3(1, 2, 3),
                 new Vector3(4, 5, 6)
             );
@@ -32,7 +33,7 @@ namespace CosmicShore.Tests
         [Test]
         public void LineData_Constructor_DirectionIsNormalized()
         {
-            var line = new Utility.GeometryUtils.LineData(
+            var line = new GeometryUtils.LineData(
                 Vector3.zero,
                 new Vector3(10, 0, 0)
             );
@@ -48,7 +49,7 @@ namespace CosmicShore.Tests
             var start = new Vector3(0, 0, 0);
             var end = new Vector3(3, 4, 0);
 
-            var line = new Utility.GeometryUtils.LineData(start, end);
+            var line = new GeometryUtils.LineData(start, end);
 
             Assert.AreEqual(5f, line.Magnitude, 0.0001f,
                 "Magnitude should be the distance between start and end (3-4-5 triangle).");
@@ -61,12 +62,12 @@ namespace CosmicShore.Tests
         [Test]
         public void DistanceFromPointToLine_PointOnLine_ReturnsZero()
         {
-            var line = Utility.GeometryUtils.PrecomputeLineData(
+            var line = GeometryUtils.PrecomputeLineData(
                 Vector3.zero,
                 new Vector3(10, 0, 0)
             );
 
-            float distance = Utility.GeometryUtils.DistanceFromPointToLine(
+            float distance = GeometryUtils.DistanceFromPointToLine(
                 new Vector3(5, 0, 0), line
             );
 
@@ -76,13 +77,13 @@ namespace CosmicShore.Tests
         [Test]
         public void DistanceFromPointToLine_PerpendicularPoint_ReturnsPerpendicularDistance()
         {
-            var line = Utility.GeometryUtils.PrecomputeLineData(
+            var line = GeometryUtils.PrecomputeLineData(
                 Vector3.zero,
                 new Vector3(10, 0, 0)
             );
 
             // Point at (5, 3, 0) — perpendicular distance to X-axis is 3.
-            float distance = Utility.GeometryUtils.DistanceFromPointToLine(
+            float distance = GeometryUtils.DistanceFromPointToLine(
                 new Vector3(5, 3, 0), line
             );
 
@@ -92,14 +93,14 @@ namespace CosmicShore.Tests
         [Test]
         public void DistanceFromPointToLine_PointBeforeStart_ReturnsDistanceToStart()
         {
-            var line = Utility.GeometryUtils.PrecomputeLineData(
+            var line = GeometryUtils.PrecomputeLineData(
                 new Vector3(5, 0, 0),
                 new Vector3(10, 0, 0)
             );
 
             // Point at origin — before the line start.
             // Distance should be to the start point (5,0,0).
-            float distance = Utility.GeometryUtils.DistanceFromPointToLine(
+            float distance = GeometryUtils.DistanceFromPointToLine(
                 Vector3.zero, line
             );
 
@@ -109,13 +110,13 @@ namespace CosmicShore.Tests
         [Test]
         public void DistanceFromPointToLine_PointBeyondEnd_ReturnsDistanceToEnd()
         {
-            var line = Utility.GeometryUtils.PrecomputeLineData(
+            var line = GeometryUtils.PrecomputeLineData(
                 Vector3.zero,
                 new Vector3(5, 0, 0)
             );
 
             // Point at (10, 0, 0) — beyond the line end.
-            float distance = Utility.GeometryUtils.DistanceFromPointToLine(
+            float distance = GeometryUtils.DistanceFromPointToLine(
                 new Vector3(10, 0, 0), line
             );
 
@@ -125,12 +126,12 @@ namespace CosmicShore.Tests
         [Test]
         public void DistanceFromPointToLine_PointAtStart_ReturnsZero()
         {
-            var line = Utility.GeometryUtils.PrecomputeLineData(
+            var line = GeometryUtils.PrecomputeLineData(
                 new Vector3(3, 4, 5),
                 new Vector3(6, 7, 8)
             );
 
-            float distance = Utility.GeometryUtils.DistanceFromPointToLine(
+            float distance = GeometryUtils.DistanceFromPointToLine(
                 new Vector3(3, 4, 5), line
             );
 
@@ -144,7 +145,7 @@ namespace CosmicShore.Tests
         [Test]
         public void DistancesFromPointsToLine_ReturnsCorrectCount()
         {
-            var line = Utility.GeometryUtils.PrecomputeLineData(
+            var line = GeometryUtils.PrecomputeLineData(
                 Vector3.zero, new Vector3(10, 0, 0)
             );
 
@@ -155,7 +156,7 @@ namespace CosmicShore.Tests
                 new(10, 4, 0)
             };
 
-            var distances = Utility.GeometryUtils.DistancesFromPointsToLine(points, line);
+            var distances = GeometryUtils.DistancesFromPointsToLine(points, line);
 
             Assert.AreEqual(3, distances.Count);
         }
@@ -163,11 +164,11 @@ namespace CosmicShore.Tests
         [Test]
         public void DistancesFromPointsToLine_EmptyList_ReturnsEmptyList()
         {
-            var line = Utility.GeometryUtils.PrecomputeLineData(
+            var line = GeometryUtils.PrecomputeLineData(
                 Vector3.zero, Vector3.one
             );
 
-            var distances = Utility.GeometryUtils.DistancesFromPointsToLine(
+            var distances = GeometryUtils.DistancesFromPointsToLine(
                 new List<Vector3>(), line
             );
 
```

</details>
