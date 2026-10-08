# Branch archive: `claude/fix-freestyle-shape-triggers-duvEz`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-26 by Claude
- **Unmerged commits:** 1
- **Forked from:** `e4cdf4cf6` (2026-03-25, Assigned CellConfig to prefab)
- **Tip:** `fa497bb41`
- **Files touched (1):**
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableShapeBase.cs`

### `fa497bb41` — Fix shape trigger prisms ignoring prefab scale by treating SpawnPoint.Scale as multiplier

_Claude, 2026-03-26 17:07:26 +0000_

```text
The SpawnablePrism prefab has localScale (3,3,1) but GradualSpawnCoroutine
was setting TargetScale = point.Scale (Vector3.one), overriding the prefab's
authored dimensions. This caused shape trigger prisms to grow to only (1,1,1)
instead of the intended (3,3,1), making shapes invisible in Freestyle mode.
```

```text
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableShapeBase.cs | 6 +++++-
 1 file changed, 5 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableShapeBase.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableShapeBase.cs
index 2045b474a..0eb8fe564 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableShapeBase.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableShapeBase.cs
@@ -88,6 +88,10 @@ public abstract class SpawnableShapeBase : SpawnableBase
             var prismPrefab = GetPrismPrefab();
             if (prismPrefab == null) continue;
 
+            // Treat SpawnPoint.Scale as a multiplier on the prefab's authored scale
+            // so Vector3.one means "100% of prefab size" rather than absolute (1,1,1).
+            var prefabScale = prismPrefab.transform.localScale;
+
             var trail = new Trail(td.IsLoop);
             var actualDomain = td.Domain;
 
@@ -101,7 +105,7 @@ public abstract class SpawnableShapeBase : SpawnableBase
                 block.ownerID = $"{container.name}::{i}";
                 block.transform.localPosition = point.Position;
                 block.transform.localRotation = point.Rotation;
-                block.TargetScale = point.Scale;
+                block.TargetScale = Vector3.Scale(point.Scale, prefabScale);
                 block.Trail = trail;
                 block.Initialize();
                 trail.Add(block);
```

</details>
