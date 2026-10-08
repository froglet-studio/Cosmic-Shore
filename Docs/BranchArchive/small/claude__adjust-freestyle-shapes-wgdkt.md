# Branch archive: `claude/adjust-freestyle-shapes-wgdkt`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-07 by Claude
- **Unmerged commits:** 1
- **Forked from:** `fc0366c74` (2026-03-07, Merge pull request #413 from froglet-studio/claude/fix-freestyle-drawing-confl)
- **Tip:** `dcf6fb901`
- **Files touched (1):**
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs`

### `dcf6fb901` — Scale shape drawing shapes to 4x size for more maneuvering room

_Claude, 2026-03-07 22:06:52 +0000_

```text
Increase shapeScale from 10 to 40 so crystal waypoints in shape drawing
mode are 4x further apart, giving players much more room to fly between
checkpoints. Scale reveal camera distance and player start offset
proportionally so the preview cinematic and starting position still
frame correctly at the larger size.
```

```text
 Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs | 10 ++++++----
 1 file changed, 6 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs
index 889ce9434..8a6188e79 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/ShapeDrawingManager.cs
@@ -31,7 +31,7 @@ namespace CosmicShore.Game.ShapeDrawing
         [SerializeField] LineRenderer guideLine;
         [SerializeField] LineRenderer ghostLine;
         [SerializeField] Camera revealCamera;
-        [SerializeField] float shapeScale = 10f;
+        [SerializeField] float shapeScale = 40f;
 
         [Header("Shape Orientation")]
         [Tooltip("Rotation applied to shape waypoints. Default (-90,0,0) rotates XY-defined shapes to the horizontal XZ plane.")]
@@ -124,6 +124,7 @@ namespace CosmicShore.Game.ShapeDrawing
         Vector3 GetWorldPlayerStart()
         {
             Vector3 wp0 = GetWorldWaypoint(0);
+            float startOffset = 30f * (shapeScale / 10f);
 
             if (_activeShape.waypoints.Count > 1)
             {
@@ -131,13 +132,13 @@ namespace CosmicShore.Game.ShapeDrawing
                 Vector3 wp1 = GetWorldWaypoint(1);
                 Vector3 pathDir = (wp0 - wp1).normalized;
                 if (pathDir.sqrMagnitude < 0.001f) pathDir = Vector3.back;
-                return wp0 + pathDir * 30f;
+                return wp0 + pathDir * startOffset;
             }
 
             // Fallback: offset from origin toward wp0
             Vector3 dir = (_shapeOrigin - wp0).normalized;
             if (dir.sqrMagnitude < 0.001f) dir = Vector3.back;
-            return wp0 + dir * 30f;
+            return wp0 + dir * startOffset;
         }
 
         /// <summary>
@@ -164,9 +165,10 @@ namespace CosmicShore.Game.ShapeDrawing
 
         Vector3 GetRevealCameraPosition()
         {
+            float scaledDistance = _activeShape.revealCameraDistance * (shapeScale / 10f);
             return _shapeOrigin +
                 Quaternion.Euler(_activeShape.revealCameraEuler) *
-                (Vector3.back * _activeShape.revealCameraDistance);
+                (Vector3.back * scaledDistance);
         }
 
         // ── Lifecycle ────────────────────────────────────────────────────────
```

</details>
