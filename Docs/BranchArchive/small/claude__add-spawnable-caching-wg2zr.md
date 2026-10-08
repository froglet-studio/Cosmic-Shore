# Branch archive: `claude/add-spawnable-caching-wg2zr`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-04 by Claude
- **Unmerged commits:** 1
- **Forked from:** `87efdde2c` (2026-03-04, Merge pull request #355 from froglet-studio/claude/improve-elemental-effects-V)
- **Tip:** `0b4b69ec8`
- **Files touched (60):**
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/SpawnableCord.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableArrow.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBaseballCurve.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBatman.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCircle.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCliffordTorus.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableComet.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCrystal.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCylinder.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDiamond.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDriftCourse.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableEllipsoid.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableFiveRings.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableFlora.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableGyroid.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableHeart.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableHelicoid.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableHelix.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableHopfFibration.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableInfinity.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableLSystem.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableLightning.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableLinkedRings.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnablePumpkin.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableRaceTrack.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableSchwarzPSurface.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableSingleTrailBlock.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableSmiley.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableSpherene.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableSpiral.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableStar.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableTorusKnot.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableTube.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableWall.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableWave.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableWaypointTrack.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableZigzag.cs`
  - `Assets/_Scripts/Game/Environment/Spawning/Generators/AtOriginGenerator.cs`
  - … and 20 more

### `0b4b69ec8` — Include intensityLevel, domain, and seed in base cache key so spawnable caching works across intensity changes

_Claude, 2026-03-04 09:11:03 +0000_

```text
The spawnable caching system hashed parameters via GetParameterHash() in each
subclass, but most subclasses omitted intensityLevel from their hash. This meant
changing intensity (e.g. joust level 1 → 2) would serve stale cached trail data.

Fix: SpawnableBase.GetTrailData() now combines the subclass hash with
intensityLevel, domain, and seed automatically. Subclasses only need to hash
their own specific parameters. Removed redundant seed/domain/intensityLevel
from all 59 subclass hashes for consistency.
```

```text
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableSpiral.cs               | 2 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableStar.cs                 | 2 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableTorusKnot.cs            | 2 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableTube.cs                 | 2 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableWall.cs                 | 2 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableWave.cs                 | 2 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableWaypointTrack.cs        | 4 ++--
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableZigzag.cs               | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/AtOriginGenerator.cs         | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/BranchingLineGenerator.cs    | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/ConcentricLayersGenerator.cs | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/CubicGenerator.cs            | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/CurvyTubeGenerator.cs        | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/CylinderSurfaceGenerator.cs  | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/HexRingGenerator.cs          | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/HilbertCurveGenerator.cs     | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/HoneycombGridGenerator.cs    | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/KinkyLineGenerator.cs        | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/KinkyTubeGenerator.cs        | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/MazeGridGenerator.cs         | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/SavedMazeGenerator.cs        | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/SphereSurfaceGenerator.cs    | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/SphereUniformGenerator.cs    | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/SpiralTowerGenerator.cs      | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/StraightLineGenerator.cs     | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/Generators/ToroidSurfaceGenerator.cs    | 2 +-
 Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs                        | 9 ++++++---
 Assets/_Scripts/Game/Projectiles/SpawnableFlower.cs                               | 2 +-
 Assets/_Scripts/Game/Projectiles/SpawnableRings.cs                                | 2 +-
 60 files changed, 66 insertions(+), 64 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 781 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/SpawnableCord.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/SpawnableCord.cs
index d780079b0..f3275cf72 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/SpawnableCord.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/SpawnableCord.cs
@@ -52,7 +52,7 @@ public class SpawnableCord : SpawnableBase
 
     protected override int GetParameterHash()
     {
-        return System.HashCode.Combine(seed, blockCount, verticesCount, length, blockScale);
+        return System.HashCode.Combine(blockCount, verticesCount, length, blockScale);
     }
 
     private void Start()
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableArrow.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableArrow.cs
index 0b457c648..9b5e87234 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableArrow.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableArrow.cs
@@ -90,7 +90,6 @@ public class SpawnableArrow : SpawnableShapeBase
 
     protected override int GetParameterHash()
     {
-        return System.HashCode.Combine(headWidth, headHeight, shaftLength, shaftWidth, baseBlockCount, intensityLevel,
-            System.HashCode.Combine(seed, domain));
+        return System.HashCode.Combine(headWidth, headHeight, shaftLength, shaftWidth, baseBlockCount);
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBaseballCurve.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBaseballCurve.cs
index 093946582..07bf11908 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBaseballCurve.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBaseballCurve.cs
@@ -48,6 +48,6 @@ public class SpawnableBaseballCurve : SpawnableBase
 
     protected override int GetParameterHash()
     {
-        return System.HashCode.Combine(seed, radius, numSegments, seamWidth, b, c);
+        return System.HashCode.Combine(radius, numSegments, seamWidth, b, c);
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBatman.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBatman.cs
index 05602f32e..c949026e6 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBatman.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableBatman.cs
@@ -115,6 +115,6 @@ public class SpawnableBatman : SpawnableBase
 
     protected override int GetParameterHash()
     {
-        return System.HashCode.Combine(seed);
+        return 0;
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs
index 740470ecf..edb660182 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs
@@ -43,6 +43,6 @@ public class SpawnableCardioidSmear : SpawnableEllipsoid
 
     protected override int GetParameterHash()
     {
-        return System.HashCode.Combine(maxlength, maxwidth, maxheight, seed);
+        return System.HashCode.Combine(maxlength, maxwidth, maxheight);
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCircle.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCircle.cs
index 16b441727..7db484d2d 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCircle.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCircle.cs
@@ -52,6 +52,6 @@ public class SpawnableCircle : SpawnableShapeBase
 
     protected override int GetParameterHash()
     {
-        return System.HashCode.Combine(radius, baseBlockCount, intensityLevel, seed, domain);
+        return System.HashCode.Combine(radius, baseBlockCount);
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCliffordTorus.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCliffordTorus.cs
index 43a0a05ef..4a314774f 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCliffordTorus.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCliffordTorus.cs
@@ -124,7 +124,7 @@ namespace CosmicShore
 
         protected override int GetParameterHash()
         {
-            return System.HashCode.Combine(uSamples, vSamples, projectionScale, projectionPole, blockScale, seed);
+            return System.HashCode.Combine(uSamples, vSamples, projectionScale, projectionPole, blockScale);
         }
 
         Vector3 StereographicProject(float x1, float x2, float x3, float x4)
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableComet.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableComet.cs
index 950810c5b..8ae10ca9d 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableComet.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableComet.cs
@@ -87,7 +87,7 @@ namespace CosmicShore
 
         protected override int GetParameterHash()
         {
-            return System.HashCode.Combine(seed, blockCount, ringCountHead, ringCountTail, headRadius, tailLength, blockScale, Orgin);
+            return System.HashCode.Combine(blockCount, ringCountHead, ringCountTail, headRadius, tailLength, blockScale, Orgin);
         }
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCrystal.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCrystal.cs
index 9841b9d45..9cab58651 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCrystal.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCrystal.cs
@@ -22,7 +22,7 @@ namespace CosmicShore.Game
 
         protected override int GetParameterHash()
         {
-            return System.HashCode.Combine(seed);
+            return 0;
         }
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCylinder.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCylinder.cs
index fbcb03873..04c473f98 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCylinder.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCylinder.cs
@@ -68,7 +68,7 @@ namespace CosmicShore
 
         protected override int GetParameterHash()
         {
-            return System.HashCode.Combine(blockCount, ringCount, radius, height, blockScale, seed);
+            return System.HashCode.Combine(blockCount, ringCount, radius, height, blockScale);
         }
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs
index 9ed253ff4..a60eacdc6 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs
@@ -93,6 +93,6 @@ public class SpawnableDartBoard : SpawnableBase
 
     protected override int GetParameterHash()
     {
-        return System.HashCode.Combine(seed, blockCount, ringCount, ringThickness, gap);
+        return System.HashCode.Combine(blockCount, ringCount, ringThickness, gap);
     }
 }
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDiamond.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDiamond.cs
index 42dfda03c..e04b02387 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDiamond.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDiamond.cs
@@ -82,6 +82,6 @@ public class SpawnableDiamond : SpawnableShapeBase
 
     protected override int GetParameterHash()
     {
-        return System.HashCode.Combine(halfWidth, halfHeight, baseBlockCount, intensityLevel, seed, domain);
+        return System.HashCode.Combine(halfWidth, halfHeight, baseBlockCount);
     }
 }
```

</details>
