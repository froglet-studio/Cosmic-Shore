# Branch archive: `claude/confident-cerf-qaaq4z`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-06-14 by Claude
- **Unmerged commits:** 2
- **Forked from:** `d41afbf0f` (2026-06-14, Merge pull request #551 from froglet-studio/claude/vigilant-feynman-4tejxd)
- **Tip:** `b100f8f46`
- **Files touched (6):**
  - `Assets/_Scripts/Controller/Prisms/PrismFactory.cs`
  - `Assets/_Scripts/Controller/Projectiles/BlockProjectilePoolManager.cs`
  - `Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs`
  - `Assets/_Scripts/Utility/Effects/PrismExplosionPoolManager.cs`
  - `Assets/_Scripts/Utility/Effects/PrismImplosionPoolManager.cs`
  - `Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs`

### `a83f25c01` — fix(pools): stop NRE from destroyed instances in pooled VFX stacks

_Claude, 2026-06-12 23:32:36 +0000_

```text
Boid-driven prism consumption crashed in PrismImplosionPoolManager.Get
(line 40) dereferencing a null from Get_. Root cause chain:

- ReleaseAllActive/ReleaseAllActiveAsync pushed instances back via
  pool.Release directly, bypassing the subclass Release(T) override, so
  OnReturnToPool kept a stale Release subscription across scene changes
  (the pools are persistent, in PrismManagers.prefab in Bootstrap).
- The next Get subscribed Release again; on effect completion the
  duplicated delegate invoked Release twice and Release_ pushed
  unconditionally — with collectionCheck:false the same instance entered
  the inactive stack twice.
- Once CountInactive hit maxSize, ObjectPool.Release destroyed the
  instance while its phantom twin entries stayed in the stack; popping
  one later made Get_ return null and Get dereferenced it.

Fixes, defense in depth:
- Release_ is now idempotent: only instances tracked in _activeObjects
  may re-enter the pool, killing duplicate stack entries at the source.
- ReleaseAllActive/Async route through the virtual Release(T) so bulk
  sweeps detach OnReturnToPool like the normal path does.
- Get_ discards destroyed entries left in the stack (with a warning
  canary) instead of returning null, self-healing poisoned pools.
- Implosion/Explosion/InteractivePrism Get overrides null-guard and
  de-dup their OnReturnToPool subscription (-= before +=).
- Guard the remaining unguarded Get dereferences with the same crash
  signature: PrismFactory.SpawnGrow and BlockProjectilePoolManager.Get.
```

```text
 Assets/_Scripts/Controller/Prisms/PrismFactory.cs                    |  1 +
 Assets/_Scripts/Controller/Projectiles/BlockProjectilePoolManager.cs |  5 ++--
 Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs       |  1 +
 Assets/_Scripts/Utility/Effects/PrismExplosionPoolManager.cs         |  3 ++
 Assets/_Scripts/Utility/Effects/PrismImplosionPoolManager.cs         |  3 ++
 Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs        | 54 ++++++++++++++++++++++------------
 6 files changed, 46 insertions(+), 21 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 177 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Prisms/PrismFactory.cs b/Assets/_Scripts/Controller/Prisms/PrismFactory.cs
index 05b363292..a4e90b7ad 100644
--- a/Assets/_Scripts/Controller/Prisms/PrismFactory.cs
+++ b/Assets/_Scripts/Controller/Prisms/PrismFactory.cs
@@ -223,6 +223,7 @@ namespace CosmicShore.Gameplay
         GameObject SpawnGrow(PrismEventData data)
         {
             var obj = implosionPool?.Get(data.SpawnPosition, data.Rotation, implosionPool.transform);
+            if (obj == null) return null;
             obj.transform.localScale = data.Scale;
             ConfigureForTeam(obj.gameObject, data.ownDomain);
 
diff --git a/Assets/_Scripts/Controller/Projectiles/BlockProjectilePoolManager.cs b/Assets/_Scripts/Controller/Projectiles/BlockProjectilePoolManager.cs
index 93092b7f3..1f13ae92e 100644
--- a/Assets/_Scripts/Controller/Projectiles/BlockProjectilePoolManager.cs
+++ b/Assets/_Scripts/Controller/Projectiles/BlockProjectilePoolManager.cs
@@ -8,8 +8,9 @@ namespace CosmicShore.Gameplay
     {
         public override Prism Get(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStays)
         {
-            var p = Get_(position, rotation, null);     
-            p.transform.SetParent(null, true);       
+            var p = Get_(position, rotation, null);
+            if (p == null) return null;
+            p.transform.SetParent(null, true);
             return p;
         }
 
diff --git a/Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs b/Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs
index 2ce136183..42c24b932 100644
--- a/Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs
+++ b/Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs
@@ -58,6 +58,7 @@ namespace CosmicShore.Utility
             var instance = Get_(position, rotation, parent, worldPositionStays);
             if (instance != null)
             {
+                instance.OnReturnToPool -= Release; // de-dup any stale subscription
                 instance.OnReturnToPool += Release;
             }
             return instance;
diff --git a/Assets/_Scripts/Utility/Effects/PrismExplosionPoolManager.cs b/Assets/_Scripts/Utility/Effects/PrismExplosionPoolManager.cs
index 2bd1fa158..cc1dc597d 100644
--- a/Assets/_Scripts/Utility/Effects/PrismExplosionPoolManager.cs
+++ b/Assets/_Scripts/Utility/Effects/PrismExplosionPoolManager.cs
@@ -38,6 +38,9 @@ namespace CosmicShore.Utility
         public override PrismExplosion Get(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
         {
             var explosion = Get_(position, rotation, parent, worldPositionStays);
+            if (explosion == null) return null;
+
+            explosion.OnReturnToPool -= Release; // de-dup any stale subscription
             explosion.OnReturnToPool += Release;
             return explosion;
         }
diff --git a/Assets/_Scripts/Utility/Effects/PrismImplosionPoolManager.cs b/Assets/_Scripts/Utility/Effects/PrismImplosionPoolManager.cs
index bfb5ee574..23ea2e8c1 100644
--- a/Assets/_Scripts/Utility/Effects/PrismImplosionPoolManager.cs
+++ b/Assets/_Scripts/Utility/Effects/PrismImplosionPoolManager.cs
@@ -37,6 +37,9 @@ namespace CosmicShore.Utility
         public override PrismImplosion Get(Vector3 spawnPosition, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
         {
             var implosion = Get_(spawnPosition, rotation, parent, worldPositionStays);
+            if (implosion == null) return null;
+
+            implosion.OnReturnToPool -= Release; // de-dup any stale subscription
             implosion.OnReturnToPool += Release; // auto return when done
             return implosion;
         }
diff --git a/Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs b/Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs
index bc4f3de2e..30b92e684 100644
--- a/Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs
+++ b/Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs
@@ -72,9 +72,22 @@ namespace CosmicShore.Utility
 
         protected T Get_(Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)
         {
-            var instance = pool.Get();
+            // ObjectPool has no liveness check: if a pooled instance was destroyed while
+            // sitting in the inactive stack (e.g., an overflow-destroy of one stack entry
+            // while a duplicate entry for the same instance remained), Get() hands back a
+            // dead object. Discard dead entries until a live one comes out — once the
+            // stack empties, ObjectPool falls through to CreateFunc and returns a fresh
+            // instance, so the bound of CountInactive + 1 always terminates.
+            var attempts = CountInactive + 1;
+            T instance = null;
+            for (int i = 0; i < attempts; i++)
+            {
+                instance = pool.Get();
+                if (instance) break;
+                CSDebug.LogWarning($"[PoolManager] {name}: discarded a destroyed instance found in the pool.");
+            }
             if (!instance) return default;
-            
+
             // [Optimization] Add to tracking set
             _activeObjects.Add(instance);
 
@@ -87,13 +100,18 @@ namespace CosmicShore.Utility
         protected void Release_(T instance)
         {
             if (!instance) return;
-            
-            // [Optimization] Remove from tracking set
-            if (_activeObjects.Contains(instance))
-                _activeObjects.Remove(instance);
+
+            // Idempotency guard: only instances currently tracked as active may re-enter
+            // the pool. A leaked or duplicated OnReturnToPool subscription can invoke
+            // Release twice for a single Get; with collectionCheck off, a second push
+            // would put the same instance into the inactive stack twice, and a later
+            // overflow-destroy of one entry would leave its twin pointing at a destroyed
+            // object — which then pops out of Get_ as null.
+            if (!_activeObjects.Remove(instance))
+                return;
 
             // Clean hierarchy before disabling
-            instance.transform.SetParent(transform); 
+            instance.transform.SetParent(transform);
             pool.Release(instance);
         }
 
@@ -102,29 +120,29 @@ namespace CosmicShore.Utility
         /// </summary>
         public async UniTask ReleaseAllActiveAsync(int batchSize = 50)
         {
-            // Copy list to avoid "Collection Modified" errors while iterating
+            // Copy list to avoid "Collection Modified" errors while iterating.
+            // Route every item through the subclass Release(T) so per-instance state
+            // (e.g., OnReturnToPool subscriptions) is detached — pushing an instance
+            // back with a stale subscription is what minted duplicate stack entries.
+            // Release_ removes each item from _activeObjects itself and no-ops on
+            // anything already released, so double release stays impossible.
             var itemsToRelease = new List<T>(_activeObjects);
-            _activeObjects.Clear(); // Clear tracking immediately so we don't double release
 
             int processed = 0;
             foreach (var item in itemsToRelease)
             {
                 if (item)
-                {
-                    // Direct release to pool (bypass _activeObjects check since we already cleared it)
-                    item.transform.SetParent(transform);
-                    pool.Release(item);
-                }
+                    Release(item);
 
                 processed++;
-                
+
                 // Yield every 'batchSize' items to let the Network Heartbeat pass through
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
