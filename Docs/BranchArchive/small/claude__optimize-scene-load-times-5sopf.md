# Branch archive: `claude/optimize-scene-load-times-5sopf`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-05 by Claude
- **Unmerged commits:** 3
- **Forked from:** `5fc002309` (2026-03-04, Fix multiplayer team crystals not spawning for non-host players)
- **Tip:** `59aefd306`
- **Files touched (19):**
  - `Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs`
  - `Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs`
  - `Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs`
  - `Assets/_Scripts/Game/Managers/GameManager.cs`
  - `Assets/_Scripts/Game/Managers/PrismAOERegistry.cs`
  - `Assets/_Scripts/Game/Managers/PrismActivationQueue.cs`
  - `Assets/_Scripts/Game/Managers/PrismEffectsManager.cs`
  - `Assets/_Scripts/Game/Managers/PrismStateManager.cs`
  - `Assets/_Scripts/Game/Managers/PrismTimerManager.cs`
  - `Assets/_Scripts/Game/Projectiles/TrailBlockBufferManager.cs`
  - `Assets/_Scripts/Game/Settings/GameSetting.cs`
  - `Assets/_Scripts/Game/Ship/Prism.cs`
  - `Assets/_Scripts/Integrations/Playfab/Economy/CatalogManager.cs`
  - `Assets/_Scripts/Integrations/Playfab/Groups/GroupController.cs`
  - `Assets/_Scripts/Integrations/Playfab/PlayStream/AnalyticsController.cs`
  - `Assets/_Scripts/Integrations/Playfab/PlayStream/LeaderboardManager.cs`
  - `Assets/_Scripts/Integrations/Playfab/PlayerData/PlayerDataController.cs`
  - `Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs`
  - `Assets/_Scripts/Utility/Singleton.cs`

### `323bf0959` — Fix O(N²) scene teardown and stale singleton refs causing slow scene loads

_Claude, 2026-03-04 21:36:14 +0000_

```text
Root causes of the load time regression:

1. Singleton<T>.Instance never cleared on destroy — stale C# references
   survived scene transitions, preventing proper null-conditional short-
   circuiting and blocking GC of old manager state.

2. PrismTimerManager.CancelTimers used List.RemoveAt (O(N) shift) called
   once per prism during teardown — O(N²) total for N prisms with timers.
   At 3000 prisms this is ~4.5M element shifts blocking the main thread.

3. SceneManager.LoadScene (sync) blocked the entire frame during unload+load.

4. GenericPoolManager.Prewarm instantiated all objects synchronously in Awake.

Fixes:
- Singleton: add virtual OnDestroy that clears Instance when the owning
  instance is destroyed. All subclasses with OnDestroy now override+call base.
- PrismTimerManager: add _disposing flag set in OnDisable — makes all
  subsequent CancelTimers calls O(1) no-ops. Swap-remove replaces RemoveAt
  for O(1) element removal during normal gameplay too.
- GameManager: use SceneManager.LoadSceneAsync for non-blocking transitions.
- GenericPoolManager: cap sync prewarm to 8 objects; maintenance loop fills
  the rest across frames. Clear _activeObjects on disable to release refs.
- Remove noisy print() calls from all Singleton Awake methods.
```

```text
 Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs             |  3 ++-
 Assets/_Scripts/Game/Managers/GameManager.cs                          |  8 ++++---
 Assets/_Scripts/Game/Managers/PrismAOERegistry.cs                     |  5 +++-
 Assets/_Scripts/Game/Managers/PrismEffectsManager.cs                  |  3 ++-
 Assets/_Scripts/Game/Managers/PrismTimerManager.cs                    | 39 +++++++++++++++++++++++++-----
 Assets/_Scripts/Game/Projectiles/TrailBlockBufferManager.cs           |  3 ++-
 Assets/_Scripts/Game/Settings/GameSetting.cs                          |  3 ++-
 Assets/_Scripts/Integrations/Playfab/Economy/CatalogManager.cs        |  3 ++-
 Assets/_Scripts/Integrations/Playfab/Groups/GroupController.cs        |  3 ++-
 .../_Scripts/Integrations/Playfab/PlayStream/AnalyticsController.cs   |  3 ++-
 Assets/_Scripts/Integrations/Playfab/PlayStream/LeaderboardManager.cs |  3 ++-
 .../_Scripts/Integrations/Playfab/PlayerData/PlayerDataController.cs  |  3 ++-
 Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs         | 26 ++++++++++++++++----
 Assets/_Scripts/Utility/Singleton.cs                                  | 42 ++++++++++++++++++++-------------
 14 files changed, 108 insertions(+), 39 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 458 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
index 2391b9167..eadaf0422 100644
--- a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
+++ b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
@@ -198,9 +198,10 @@ namespace CosmicShore.Core
             CleanupResources();
         }
 
-        protected virtual void OnDestroy()
+        protected override void OnDestroy()
         {
             CleanupResources();
+            base.OnDestroy();
         }
 
         protected virtual void CleanupResources()
diff --git a/Assets/_Scripts/Game/Managers/GameManager.cs b/Assets/_Scripts/Game/Managers/GameManager.cs
index 9d7116492..b60ea9d58 100644
--- a/Assets/_Scripts/Game/Managers/GameManager.cs
+++ b/Assets/_Scripts/Game/Managers/GameManager.cs
@@ -62,12 +62,14 @@ namespace CosmicShore.Core
             _onSceneTransition.Raise(false);
 
             gameData.ResetRuntimeData();
-            
+
             // Delay is realtime so it still works if Time.timeScale = 0
-            await UniTask.Delay(TimeSpan.FromSeconds(WAIT_FOR_SECONDS_BEFORE_SCENELOAD), 
+            await UniTask.Delay(TimeSpan.FromSeconds(WAIT_FOR_SECONDS_BEFORE_SCENELOAD),
                 DelayType.UnscaledDeltaTime);
 
-            SceneManager.LoadScene(sceneName);
+            var op = SceneManager.LoadSceneAsync(sceneName);
+            if (op != null)
+                await op.ToUniTask();
         }
 
         private void OnApplicationQuit()
diff --git a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
index 5c48c89c9..2f7822584 100644
--- a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
+++ b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
@@ -425,11 +425,14 @@ namespace CosmicShore.Game
 
         #region Cleanup
 
-        private void OnDestroy()
+        protected override void OnDestroy()
         {
             if (_spatial.IsCreated) _spatial.Dispose();
             if (_damage.IsCreated) _damage.Dispose();
             if (_hitIndices.IsCreated) _hitIndices.Dispose();
+            _highWaterMark = 0;
+            _freeList.Clear();
+            base.OnDestroy();
         }
 
         #endregion
diff --git a/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs b/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
index 957c3da50..2deb3e4e4 100644
--- a/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
+++ b/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
@@ -303,7 +303,7 @@ namespace CosmicShore.Game
             activeImplosions.Clear();
         }
 
-        private void OnDestroy()
+        protected override void OnDestroy()
         {
             if (explosionJobData.IsCreated) explosionJobData.Dispose();
             if (implosionJobData.IsCreated) implosionJobData.Dispose();
@@ -311,6 +311,7 @@ namespace CosmicShore.Game
             activeImplosions.Clear();
             tempExplosionList.Clear();
             tempImplosionList.Clear();
+            base.OnDestroy();
         }
 
         #endregion
diff --git a/Assets/_Scripts/Game/Managers/PrismTimerManager.cs b/Assets/_Scripts/Game/Managers/PrismTimerManager.cs
index fbcfb883a..f1457e886 100644
--- a/Assets/_Scripts/Game/Managers/PrismTimerManager.cs
+++ b/Assets/_Scripts/Game/Managers/PrismTimerManager.cs
@@ -18,6 +18,13 @@ namespace CosmicShore.Core
     /// </summary>
     public class PrismTimerManager : Singleton<PrismTimerManager>
     {
+        /// <summary>
+        /// When true, the manager is shutting down (scene unload). Individual
+        /// CancelTimers calls become no-ops since the entire list is already cleared.
+        /// This prevents O(N²) teardown when thousands of prisms each try to cancel.
+        /// </summary>
+        private bool _disposing;
+
         /// <summary>
         /// Ensures a PrismTimerManager instance exists. If none was placed in the scene,
         /// creates one automatically so timed shield operations don't silently fail.
@@ -54,7 +61,7 @@ namespace CosmicShore.Core
         /// </summary>
         public void ScheduleShieldDeactivation(PrismStateManager target, float delay)
         {
-            if (target == null) return;
+            if (_disposing || target == null) return;
 
             // Cancel any existing timer for this target to avoid duplicates
             CancelTimers(target);
@@ -70,14 +77,21 @@ namespace CosmicShore.Core
         /// <summary>
         /// Cancel all pending timers for the given PrismStateManager.
         /// Call this when the prism is destroyed or returned to pool.
+        /// Uses swap-remove to avoid O(N) element shifting per removal.
         /// </summary>
         public void CancelTimers(PrismStateManager target)
         {
+            if (_disposing || activeTimers.Count == 0) return;
+
             for (int i = activeTimers.Count - 1; i >= 0; i--)
             {
                 if (activeTimers[i].Target == target)
                 {
-                    activeTimers.RemoveAt(i);
+                    // Swap with last element for O(1) removal instead of O(N) shift
+                    int last = activeTimers.Count - 1;
+                    if (i != last)
+                        activeTimers[i] = activeTimers[last];
+                    activeTimers.RemoveAt(last);
                 }
             }
         }
@@ -89,7 +103,7 @@ namespace CosmicShore.Core
             float currentTime = Time.time;
             completionTargets.Clear();
 
-            // Process expired timers
+            // Process expired timers (swap-remove to avoid element shifting)
             for (int i = activeTimers.Count - 1; i >= 0; i--)
             {
                 var entry = activeTimers[i];
@@ -97,13 +111,17 @@ namespace CosmicShore.Core
                 // Null check: target may have been destroyed
                 if (entry.Target == null)
                 {
-                    activeTimers.RemoveAt(i);
+                    int last = activeTimers.Count - 1;
+                    if (i != last) activeTimers[i] = activeTimers[last];
+                    activeTimers.RemoveAt(last);
                     continue;
                 }
 
                 if (currentTime >= entry.EndTime)
```

</details>

### `9d85d12d0` — Add [ScenePerf] debug timing markers for scene load diagnostics

_Claude, 2026-03-05 01:32:04 +0000_

```text
Timestamped Debug.Log calls at every critical point in the scene
transition pipeline. All prefixed with [ScenePerf] for easy filtering.

Markers cover:
- GameManager.LoadSceneAsync: start, event raise, reset, pre-load, complete
- GameManager.Start: new scene ready timestamp
- Singleton<T>: Awake creation + OnDestroy clearing
- PrismTimerManager: OnDisable/OnDestroy with timer count
- PrismAOERegistry: OnDestroy with high-water mark
- AdaptiveAnimationManager: OnDisable with registered/active counts
- PrismEffectsManager: OnDisable with explosion/implosion counts
- GenericPoolManager: Awake with prewarm timing, OnDisable with active count
- TrailBlockBufferManager: OnDestroy with block count + duration
- PrismStateManager: batched counter (every 500) + summary method
- MaterialPropertyAnimator: batched counter (every 500)
- PrismScaleAnimator: batched counter (every 500)

Filter in Unity console: [ScenePerf]
Remove after diagnosis by searching for [ScenePerf].
```

```text
 Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs |  9 +++++++++
 Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs       |  9 +++++++++
 Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs           |  2 ++
 Assets/_Scripts/Game/Managers/GameManager.cs                        | 12 +++++++++++-
 Assets/_Scripts/Game/Managers/PrismAOERegistry.cs                   |  1 +
 Assets/_Scripts/Game/Managers/PrismEffectsManager.cs                |  2 ++
 Assets/_Scripts/Game/Managers/PrismStateManager.cs                  | 24 ++++++++++++++++++++++++
 Assets/_Scripts/Game/Managers/PrismTimerManager.cs                  |  2 ++
 Assets/_Scripts/Game/Projectiles/TrailBlockBufferManager.cs         |  4 ++++
 Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs       |  4 ++++
 Assets/_Scripts/Utility/Singleton.cs                                |  4 ++++
 11 files changed, 72 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 284 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs b/Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs
index 840d47f45..40a419168 100644
--- a/Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs
+++ b/Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs
@@ -93,8 +93,17 @@ namespace CosmicShore.Core
             TryRegisterWithManager();
         }
 
+        // [ScenePerf] teardown counter
+        private static int _disableCount;
+        private static float _firstDisableTime;
+
         private void OnDisable()
         {
+            _disableCount++;
+            if (_disableCount == 1) _firstDisableTime = Time.realtimeSinceStartup;
+            if (_disableCount % 500 == 0)
+                Debug.Log($"[ScenePerf] MaterialPropertyAnimator.OnDisable #{_disableCount} elapsed={((Time.realtimeSinceStartup - _firstDisableTime)*1000f):F0}ms t={Time.realtimeSinceStartup:F3}");
+
             if (MaterialStateManager.Instance != null && isRegistered)
             {
                 MaterialStateManager.Instance.UnregisterAnimator(this);
diff --git a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
index 86ee2f4d5..054e8298d 100644
--- a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
+++ b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
@@ -73,8 +73,17 @@ namespace CosmicShore.Core
             isRegistered = true;
         }
 
+        // [ScenePerf] teardown counter
+        private static int _disableCount;
+        private static float _firstDisableTime;
+
         private void OnDisable()
         {
+            _disableCount++;
+            if (_disableCount == 1) _firstDisableTime = Time.realtimeSinceStartup;
+            if (_disableCount % 500 == 0)
+                Debug.Log($"[ScenePerf] PrismScaleAnimator.OnDisable #{_disableCount} elapsed={((Time.realtimeSinceStartup - _firstDisableTime)*1000f):F0}ms t={Time.realtimeSinceStartup:F3}");
+
             if (PrismScaleManager.Instance == null || !isRegistered) return;
             PrismScaleManager.Instance.UnregisterAnimator(this);
             isRegistered = false;
diff --git a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
index eadaf0422..731b7e439 100644
--- a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
+++ b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
@@ -195,11 +195,13 @@ namespace CosmicShore.Core
 
         protected virtual void OnDisable()
         {
+            UnityEngine.Debug.Log($"[ScenePerf] {GetType().Name}.OnDisable — registered={registeredAnimators.Count} active={activeAnimators.Count} t={UnityEngine.Time.realtimeSinceStartup:F3}");
             CleanupResources();
         }
 
         protected override void OnDestroy()
         {
+            UnityEngine.Debug.Log($"[ScenePerf] {GetType().Name}.OnDestroy t={UnityEngine.Time.realtimeSinceStartup:F3}");
             CleanupResources();
             base.OnDestroy();
         }
diff --git a/Assets/_Scripts/Game/Managers/GameManager.cs b/Assets/_Scripts/Game/Managers/GameManager.cs
index b60ea9d58..cfd2683ce 100644
--- a/Assets/_Scripts/Game/Managers/GameManager.cs
+++ b/Assets/_Scripts/Game/Managers/GameManager.cs
@@ -29,7 +29,11 @@ namespace CosmicShore.Core
             gameData.OnLaunchGameScene += LaunchGameScene;
         }
 
-        private void Start() => _onSceneTransition.Raise(true);
+        private void Start()
+        {
+            Debug.Log($"[ScenePerf] GameManager.Start — scene ready t={Time.realtimeSinceStartup:F3}");
+            _onSceneTransition.Raise(true);
+        }
 
         private void OnDisable()
         {
@@ -59,17 +63,23 @@ namespace CosmicShore.Core
 
         private async UniTaskVoid LoadSceneAsync(string sceneName)
         {
+            Debug.Log($"[ScenePerf] LoadSceneAsync START → '{sceneName}' t={Time.realtimeSinceStartup:F3}");
+
             _onSceneTransition.Raise(false);
+            Debug.Log($"[ScenePerf] _onSceneTransition.Raise(false) done t={Time.realtimeSinceStartup:F3}");
 
             gameData.ResetRuntimeData();
+            Debug.Log($"[ScenePerf] ResetRuntimeData done t={Time.realtimeSinceStartup:F3}");
 
             // Delay is realtime so it still works if Time.timeScale = 0
             await UniTask.Delay(TimeSpan.FromSeconds(WAIT_FOR_SECONDS_BEFORE_SCENELOAD),
                 DelayType.UnscaledDeltaTime);
 
+            Debug.Log($"[ScenePerf] Pre-delay done, calling LoadSceneAsync t={Time.realtimeSinceStartup:F3}");
             var op = SceneManager.LoadSceneAsync(sceneName);
             if (op != null)
                 await op.ToUniTask();
+            Debug.Log($"[ScenePerf] LoadSceneAsync COMPLETE t={Time.realtimeSinceStartup:F3}");
         }
 
         private void OnApplicationQuit()
diff --git a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
index 2f7822584..80fb6a855 100644
--- a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
+++ b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
@@ -427,6 +427,7 @@ namespace CosmicShore.Game
 
         protected override void OnDestroy()
         {
+            Debug.Log($"[ScenePerf] PrismAOERegistry.OnDestroy — hwm={_highWaterMark} freeList={_freeList.Count} t={Time.realtimeSinceStartup:F3}");
             if (_spatial.IsCreated) _spatial.Dispose();
             if (_damage.IsCreated) _damage.Dispose();
             if (_hitIndices.IsCreated) _hitIndices.Dispose();
diff --git a/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs b/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
index 2deb3e4e4..6c3d45189 100644
--- a/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
+++ b/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
@@ -299,12 +299,14 @@ namespace CosmicShore.Game
 
         private void OnDisable()
         {
+            Debug.Log($"[ScenePerf] PrismEffectsManager.OnDisable — explosions={activeExplosions.Count} implosions={activeImplosions.Count} t={Time.realtimeSinceStartup:F3}");
             activeExplosions.Clear();
             activeImplosions.Clear();
         }
 
         protected override void OnDestroy()
         {
+            Debug.Log($"[ScenePerf] PrismEffectsManager.OnDestroy t={Time.realtimeSinceStartup:F3}");
             if (explosionJobData.IsCreated) explosionJobData.Dispose();
             if (implosionJobData.IsCreated) implosionJobData.Dispose();
             activeExplosions.Clear();
diff --git a/Assets/_Scripts/Game/Managers/PrismStateManager.cs b/Assets/_Scripts/Game/Managers/PrismStateManager.cs
index add0d7e8c..1dc34251e 100644
--- a/Assets/_Scripts/Game/Managers/PrismStateManager.cs
+++ b/Assets/_Scripts/Game/Managers/PrismStateManager.cs
@@ -13,6 +13,10 @@ namespace CosmicShore.Core
 
     public class PrismStateManager : MonoBehaviour
     {
+        // [ScenePerf] Teardown counters — static so they accumulate across all instances
+        private static int _disableCount;
+        private static float _firstDisableTime;
+
         [Header("Data Containers")] [SerializeField]
         ThemeManagerDataContainerSO _themeManagerData;
 
@@ -134,12 +138,32 @@ namespace CosmicShore.Core
```

</details>

### `59aefd306` — Replace per-prism coroutine with centralized PrismActivationQueue

_Claude, 2026-03-05 01:44:02 +0000_

```text
Profiler showed 49,856 CreateBlockCoroutine coroutines resuming in a
single frame (1.9s stall, 10.1 MB GC alloc). All prisms used the same
WaitForSeconds(0.6f), so every timer expired on the same frame — a
thundering herd that froze the main thread for 5+ seconds.

Fix: new PrismActivationQueue singleton replaces per-prism coroutines.
- Prism.Initialize() calls Enqueue(prism, scale, delay) instead of
  StartCoroutine(CreateBlockCoroutine())
- Queue processes up to maxActivationsPerFrame (default 200) per Update,
  spreading 50K activations across ~250 frames instead of 1
- Prism.ResetState() cancels pending activations (swap-remove, O(1))
- Eliminates 50K coroutine heap allocations and WaitForSeconds objects
- Also removes unused System.Collections import and StopAllCoroutines

Also cleans up all [ScenePerf] debug markers from the previous commit.
```

```text
 Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs |   9 ---
 Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs       |   9 ---
 Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs           |   2 -
 Assets/_Scripts/Game/Managers/GameManager.cs                        |  12 +---
 Assets/_Scripts/Game/Managers/PrismAOERegistry.cs                   |   1 -
 Assets/_Scripts/Game/Managers/PrismActivationQueue.cs               | 116 ++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Managers/PrismEffectsManager.cs                |   2 -
 Assets/_Scripts/Game/Managers/PrismStateManager.cs                  |  24 -------
 Assets/_Scripts/Game/Managers/PrismTimerManager.cs                  |   2 -
 Assets/_Scripts/Game/Projectiles/TrailBlockBufferManager.cs         |   4 --
 Assets/_Scripts/Game/Ship/Prism.cs                                  |  39 +++++-------
 Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs       |   4 --
 Assets/_Scripts/Utility/Singleton.cs                                |   4 --
 13 files changed, 134 insertions(+), 94 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 498 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs b/Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs
index 40a419168..840d47f45 100644
--- a/Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs
+++ b/Assets/_Scripts/Game/Environment/Prisms/MaterialPropertyAnimator.cs
@@ -93,17 +93,8 @@ namespace CosmicShore.Core
             TryRegisterWithManager();
         }
 
-        // [ScenePerf] teardown counter
-        private static int _disableCount;
-        private static float _firstDisableTime;
-
         private void OnDisable()
         {
-            _disableCount++;
-            if (_disableCount == 1) _firstDisableTime = Time.realtimeSinceStartup;
-            if (_disableCount % 500 == 0)
-                Debug.Log($"[ScenePerf] MaterialPropertyAnimator.OnDisable #{_disableCount} elapsed={((Time.realtimeSinceStartup - _firstDisableTime)*1000f):F0}ms t={Time.realtimeSinceStartup:F3}");
-
             if (MaterialStateManager.Instance != null && isRegistered)
             {
                 MaterialStateManager.Instance.UnregisterAnimator(this);
diff --git a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
index 054e8298d..86ee2f4d5 100644
--- a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
+++ b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
@@ -73,17 +73,8 @@ namespace CosmicShore.Core
             isRegistered = true;
         }
 
-        // [ScenePerf] teardown counter
-        private static int _disableCount;
-        private static float _firstDisableTime;
-
         private void OnDisable()
         {
-            _disableCount++;
-            if (_disableCount == 1) _firstDisableTime = Time.realtimeSinceStartup;
-            if (_disableCount % 500 == 0)
-                Debug.Log($"[ScenePerf] PrismScaleAnimator.OnDisable #{_disableCount} elapsed={((Time.realtimeSinceStartup - _firstDisableTime)*1000f):F0}ms t={Time.realtimeSinceStartup:F3}");
-
             if (PrismScaleManager.Instance == null || !isRegistered) return;
             PrismScaleManager.Instance.UnregisterAnimator(this);
             isRegistered = false;
diff --git a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
index 731b7e439..eadaf0422 100644
--- a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
+++ b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
@@ -195,13 +195,11 @@ namespace CosmicShore.Core
 
         protected virtual void OnDisable()
         {
-            UnityEngine.Debug.Log($"[ScenePerf] {GetType().Name}.OnDisable — registered={registeredAnimators.Count} active={activeAnimators.Count} t={UnityEngine.Time.realtimeSinceStartup:F3}");
             CleanupResources();
         }
 
         protected override void OnDestroy()
         {
-            UnityEngine.Debug.Log($"[ScenePerf] {GetType().Name}.OnDestroy t={UnityEngine.Time.realtimeSinceStartup:F3}");
             CleanupResources();
             base.OnDestroy();
         }
diff --git a/Assets/_Scripts/Game/Managers/GameManager.cs b/Assets/_Scripts/Game/Managers/GameManager.cs
index cfd2683ce..b60ea9d58 100644
--- a/Assets/_Scripts/Game/Managers/GameManager.cs
+++ b/Assets/_Scripts/Game/Managers/GameManager.cs
@@ -29,11 +29,7 @@ namespace CosmicShore.Core
             gameData.OnLaunchGameScene += LaunchGameScene;
         }
 
-        private void Start()
-        {
-            Debug.Log($"[ScenePerf] GameManager.Start — scene ready t={Time.realtimeSinceStartup:F3}");
-            _onSceneTransition.Raise(true);
-        }
+        private void Start() => _onSceneTransition.Raise(true);
 
         private void OnDisable()
         {
@@ -63,23 +59,17 @@ namespace CosmicShore.Core
 
         private async UniTaskVoid LoadSceneAsync(string sceneName)
         {
-            Debug.Log($"[ScenePerf] LoadSceneAsync START → '{sceneName}' t={Time.realtimeSinceStartup:F3}");
-
             _onSceneTransition.Raise(false);
-            Debug.Log($"[ScenePerf] _onSceneTransition.Raise(false) done t={Time.realtimeSinceStartup:F3}");
 
             gameData.ResetRuntimeData();
-            Debug.Log($"[ScenePerf] ResetRuntimeData done t={Time.realtimeSinceStartup:F3}");
 
             // Delay is realtime so it still works if Time.timeScale = 0
             await UniTask.Delay(TimeSpan.FromSeconds(WAIT_FOR_SECONDS_BEFORE_SCENELOAD),
                 DelayType.UnscaledDeltaTime);
 
-            Debug.Log($"[ScenePerf] Pre-delay done, calling LoadSceneAsync t={Time.realtimeSinceStartup:F3}");
             var op = SceneManager.LoadSceneAsync(sceneName);
             if (op != null)
                 await op.ToUniTask();
-            Debug.Log($"[ScenePerf] LoadSceneAsync COMPLETE t={Time.realtimeSinceStartup:F3}");
         }
 
         private void OnApplicationQuit()
diff --git a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
index 80fb6a855..2f7822584 100644
--- a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
+++ b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
@@ -427,7 +427,6 @@ namespace CosmicShore.Game
 
         protected override void OnDestroy()
         {
-            Debug.Log($"[ScenePerf] PrismAOERegistry.OnDestroy — hwm={_highWaterMark} freeList={_freeList.Count} t={Time.realtimeSinceStartup:F3}");
             if (_spatial.IsCreated) _spatial.Dispose();
             if (_damage.IsCreated) _damage.Dispose();
             if (_hitIndices.IsCreated) _hitIndices.Dispose();
diff --git a/Assets/_Scripts/Game/Managers/PrismActivationQueue.cs b/Assets/_Scripts/Game/Managers/PrismActivationQueue.cs
new file mode 100644
index 000000000..b61f597f5
--- /dev/null
+++ b/Assets/_Scripts/Game/Managers/PrismActivationQueue.cs
@@ -0,0 +1,116 @@
+using System.Collections.Generic;
+using CosmicShore.Utilities;
+using UnityEngine;
+
+namespace CosmicShore.Core
+{
+    /// <summary>
+    /// Replaces per-prism CreateBlockCoroutine with a centralized queue that
+    /// activates a bounded number of prisms per frame. This eliminates the
+    /// thundering-herd problem where 50K WaitForSeconds(0.6) coroutines all
+    /// resume on the same frame, causing a multi-second stall.
+    ///
+    /// Each prism queues itself via <see cref="Enqueue"/> with a target activation
+    /// time. Each Update, the queue processes up to <see cref="maxActivationsPerFrame"/>
+    /// prisms whose delay has elapsed, spreading the cost across frames.
+    /// </summary>
+    public class PrismActivationQueue : Singleton<PrismActivationQueue>
+    {
+        [Header("Throughput")]
+        [Tooltip("Max prisms to activate per frame. Higher = faster but more frame cost.")]
+        [SerializeField] private int maxActivationsPerFrame = 200;
+
+        private struct PendingActivation
+        {
+            public Prism Prism;
+            public Vector3 AuthoredTargetScale;
+            public float ActivateAtTime;
+        }
+
```

</details>
