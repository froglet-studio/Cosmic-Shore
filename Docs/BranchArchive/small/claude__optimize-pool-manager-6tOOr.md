# Branch archive: `claude/optimize-pool-manager-6tOOr`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-07 by Claude
- **Unmerged commits:** 1
- **Forked from:** `78915d84b` (2026-03-07, Update Squirrel.prefab)
- **Tip:** `15723d85b`
- **Files touched (1):**
  - `Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs`

### `15723d85b` — Optimize GenericPoolManager for faster scene loading

_Claude, 2026-03-07 03:07:41 +0000_

```text
Cap synchronous prewarm in Awake to maxSyncPrewarm (default 8) so the
remaining buffer fills are deferred to the async maintenance loop,
reducing scene-load hitches.  Clear _activeObjects in OnDisable/OnDestroy
to prevent stale references after teardown.
```

```text
 Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs | 22 ++++++++++++++++++----
 1 file changed, 18 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs b/Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs
index a5714d37a..6b2cb7dda 100644
--- a/Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs
+++ b/Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs
@@ -15,6 +15,10 @@ namespace CosmicShore.Core
         [SerializeField] private int defaultCapacity = 10;
         [SerializeField] private int maxSize = 100;
 
+        [Header("Prewarm")]
+        [Tooltip("Max objects to instantiate synchronously in Awake. The rest are deferred to the maintenance loop.")]
+        [SerializeField] private int maxSyncPrewarm = 8;
+
         [Header("Buffer Maintenance (Optional)")]
         [SerializeField] private bool enableBufferMaintenance = true;
         [SerializeField] private int bufferSizeTarget = 20;
@@ -41,8 +45,9 @@ namespace CosmicShore.Core
                 maxSize
             );
 
-            if (defaultCapacity > 0)
-                Prewarm(Mathf.Max(defaultCapacity, bufferSizeTarget));
+            var target = Mathf.Max(defaultCapacity, bufferSizeTarget);
+            if (target > 0)
+                Prewarm(Mathf.Min(target, maxSyncPrewarm));
 
             if (enableBufferMaintenance)
             {
@@ -51,8 +56,17 @@ namespace CosmicShore.Core
             }
         }
 
-        protected virtual void OnDisable() => CancelMaintenance();
-        protected virtual void OnDestroy() => CancelMaintenance();
+        protected virtual void OnDisable()
+        {
+            CancelMaintenance();
+            _activeObjects.Clear();
+        }
+
+        protected virtual void OnDestroy()
+        {
+            CancelMaintenance();
+            _activeObjects.Clear();
+        }
 
         private void CancelMaintenance()
         {
```

</details>
