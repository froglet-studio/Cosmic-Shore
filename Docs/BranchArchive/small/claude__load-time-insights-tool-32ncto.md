# Branch archive: `claude/load-time-insights-tool-32ncto`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-28 by Claude
- **Unmerged commits:** 3
- **Forked from:** `0acec710e` (2026-07-28, perf(load): multithreaded batched prism cloning + stream the two heaviest buil)
- **Tip:** `c45ea3f48`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs`
  - `Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`

### `7f3efdb68` — fix(load): disable batched instantiate - its integration budget throttles the build

_Claude, 2026-07-28 18:47:10 +0000_

```text
Batched InstantiateAsync regressed load times (some levels 30s+). The clones
integrate on the main thread under AsyncInstantiateOperation's own per-frame
budget (~10ms default), so that budget governs the build instead of ours: a
structure needing ~15s of integration gets throttled to ~10ms/frame and
stretches well past the sync path it was meant to beat. The worker-thread
clone win is real but far smaller than the throttle it imposes.

Default to the per-item path (the behavioural baseline) and keep the batched
code for a measured retry with a raised integration budget. Per-item chunks
drop to 24 so a chunk cloned between budget checks can't overshoot the frame
budget enough to stall the build readout.
```

```text
 Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs | 27 ++++++++++++++++++++++-----
 1 file changed, 22 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs b/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
index 5e18ed48a..fe56317d2 100644
--- a/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
+++ b/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
@@ -384,12 +384,24 @@ namespace CosmicShore.Gameplay
         /// </summary>
         const int CloneBatchSize = 256;
 
+        /// <summary>Clones per chunk on the per-item path — small enough that overshooting the
+        /// frame budget by at most one chunk stays invisible in the build readout.</summary>
+        const int SyncCloneChunk = 24;
+
         /// <summary>
-        /// Set false to force the per-item clone path (kept as a live escape hatch: batched
-        /// instantiate is an engine fast path, and the sync path is the behavioural baseline).
-        /// Flipped automatically if the batched call ever fails.
+        /// OFF by default — measured REGRESSION, do not flip without re-measuring.
+        /// <para>
+        /// AsyncInstantiateOperation integrates its clones on the main thread under its own
+        /// per-frame time budget (~10 ms by default). That budget, not our lay budget, then
+        /// governs the build: a structure needing ~15 s of integration is throttled to ~10 ms
+        /// per frame, so it stretches to 25 s+ instead of speeding up. The worker-thread clone
+        /// win is real but far smaller than the throttle it imposes.
+        /// </para>
+        /// Re-enabling requires raising the integration budget for the covered window
+        /// (AsyncInstantiateOperation.SetIntegrationTimeMS) and proving the result with a Load
+        /// Time Insights capture — the code path is kept for exactly that experiment.
         /// </summary>
-        public static bool UseBatchedInstantiate = true;
+        public static bool UseBatchedInstantiate = false;
 
         /// <summary>
         /// Clone <paramref name="count"/> prisms as children of <paramref name="parent"/> using
@@ -483,7 +495,12 @@ namespace CosmicShore.Gameplay
                 {
                     if (!parent) return; // container destroyed — stop laying
 
-                    int batch = Mathf.Min(CloneBatchSize, count - i);
+                    // On the per-item path a batch is cloned in one go with no budget check
+                    // inside it, so keep batches small there or the frame budget is overshot by
+                    // a whole batch (the progress readout stops ticking). The batched path wants
+                    // the opposite: bigger batches parallelize better.
+                    int batchCap = UseBatchedInstantiate ? CloneBatchSize : SyncCloneChunk;
+                    int batch = Mathf.Min(batchCap, count - i);
                     var clones = await CloneBatchAsync(prefab, batch, parent);
                     if (clones == null || !parent) return;
 
```

</details>

### `4e0adec88` — fix(load): restore the clone accumulator, break down the reveal tick, fix comeback wiring

_Claude, 2026-07-28 18:59:01 +0000_

```text
The intensity-3 Joust capture attributed 22.6s to the prism lay but its
hot-path breakdown summed to 1.6s - splitting LayOne into clone +
ConfigureLaid dropped the Instantiate sample, hiding the dominant cost. It is
measured again per item, so the next capture shows the real per-prism clone
time instead of a 21s hole.

Also instruments the 11.8s 'Arena settle' span, which had no breakdown at
all: the per-prism reveal tick now accumulates renderer+collider enable,
scale/volume+growth start, the SOAP created-raise, and the spatial index +
cell bind + LOD notify as separate stages.

ElementalComebackSystem: AddComponent runs OnEnable before the next line can
assign gameData, so the auto-created instance logged 'GameDataSO is not
assigned' and returned WITHOUT subscribing - the comeback system was dead for
the whole match in every mode that relies on auto-creation. Subscription is
now an idempotent WireEvents() that EnsureExists calls once the reference
exists (both the scene-authored and auto-created paths).
```

```text
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs         | 35 ++++++++++++++++++++++++++--------
 Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs |  8 ++++++++
 Assets/_Scripts/Controller/Vessel/Prism.cs                           |  9 +++++++++
 3 files changed, 44 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index be45bba8b..049ec0d60 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -59,6 +59,7 @@ namespace CosmicShore.Gameplay
             if (existing)
             {
                 existing.gameData ??= gameData;
+                existing.WireEvents(); // its own OnEnable may have run before gameData existed
                 return existing;
             }
 
@@ -80,6 +81,10 @@ namespace CosmicShore.Gameplay
                     system.differenceSource = ScoreDifferenceSource.Score;
                     break;
             }
+            // AddComponent already ran OnEnable with a null gameData, so wire explicitly now
+            // that the reference is set (WireEvents is idempotent).
+            system.WireEvents();
+
             CSDebug.Log($"[ElementalComebackSystem] Auto-created for {gameData?.GameMode} " +
                         $"(source={system.differenceSource}, rate={gameData?.ComebackRatePerScoreDeficit ?? 0f}).");
             return system;
@@ -118,29 +123,43 @@ namespace CosmicShore.Gameplay
         // Index matches AllElements order: Mass=0, Charge=1, Space=2, Time=3.
         readonly float[] _lastComebackAudioTime = { -999f, -999f, -999f, -999f };
 
-        void OnEnable()
+        // True while subscribed, so WireEvents is idempotent. Required because the auto-created
+        // path can only assign gameData AFTER AddComponent has already run OnEnable (see
+        // EnsureExists) — that pass found a null gameData and returned, leaving the system
+        // silently unsubscribed for the whole match. Now EnsureExists wires explicitly once the
+        // reference exists, and this guard makes the later OnEnable a no-op instead of a
+        // double-subscribe.
+        bool _subscribed;
+
+        void OnEnable() => WireEvents();
+
+        void OnDisable() => UnwireEvents();
+
+        /// <summary>
+        /// Subscribe to the game-flow events. Safe to call repeatedly and before gameData is
+        /// assigned (a null reference simply defers wiring to whoever assigns it).
+        /// </summary>
+        internal void WireEvents()
         {
-            if (gameData == null)
-            {
-                CSDebug.LogError("[ElementalComebackSystem] GameDataSO is not assigned!");
-                return;
-            }
+            if (_subscribed || gameData == null) return;
             // Profile is optional now (initial-levels only) - the system runs without one.
 
             gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
             gameData.OnMiniGameEnd.OnRaised += OnGameEnded;
+            _subscribed = true;
 
             if (debugLogging)
                 CSDebug.Log("[ElementalComebackSystem] Enabled and subscribed to game events.");
         }
 
-        void OnDisable()
+        void UnwireEvents()
         {
-            if (gameData == null) return;
+            if (!_subscribed || gameData == null) return;
             gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
             gameData.OnMiniGameEnd.OnRaised -= OnGameEnded;
+            _subscribed = false;
         }
 
         void OnTurnStarted()
diff --git a/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs b/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
index fe56317d2..5ece00ad7 100644
--- a/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
+++ b/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
@@ -439,7 +439,15 @@ namespace CosmicShore.Gameplay
             if (!parent) return null;
             var clones = new Prism[count];
             for (int i = 0; i < count; i++)
+            {
+                // Per-item accumulator: the clone is the single biggest slice of a mass lay, so
+                // it must stay measurable in the hot-path breakdown (splitting LayOne into
+                // clone + ConfigureLaid dropped this sample and made the dominant cost invisible
+                // in the report — the breakdown summed to 1.6s of a 22.6s span).
+                long t = LoadInsights.AccumulateStart();
                 clones[i] = UnityEngine.Object.Instantiate(prefab, parent);
+                LoadInsights.AccumulateSample("Prism lay: Instantiate + component Awakes", t);
+            }
             return clones;
         }
 
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index c9977ff52..9d6728b2e 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -699,11 +699,17 @@ namespace CosmicShore.Gameplay
             }
             s_creationCompletionsThisFrame++;
 
+            // Hot-path accumulators mirror the ProfilerMarkers below: on a mass environment lay
+            // this reveal tick runs ~50k times and was measured at 11.8s of a 37.5s load
+            // ("Arena settle"), with no breakdown. Inert unless a load recording is armed.
+            long tRe = LoadInsights.AccumulateStart();
+
             using (s_createVisibilityMarker.Auto())
             {
                 SetRenderVisible(true);
                 blockCollider.enabled = true;
             }
+            tRe = LoadInsights.AccumulateSample("Prism reveal: renderer + collider on", tRe);
             IsCreationComplete = true; // visible from here — the arena-ready gate may now count this prism
 
             if (scaleAnimator.TargetScale == Vector3.zero)
@@ -712,6 +718,7 @@ namespace CosmicShore.Gameplay
             prismProperties.volume = scaleAnimator.GetCurrentVolume();
 
             scaleAnimator.BeginGrowthAnimation();
+            tRe = LoadInsights.AccumulateSample("Prism reveal: scale/volume + growth start", tRe);
 
             using (s_createSoapMarker.Auto())
             {
@@ -721,6 +728,7 @@ namespace CosmicShore.Gameplay
                     Volume = prismProperties.volume,
                 });
             }
+            tRe = LoadInsights.AccumulateSample("Prism reveal: SOAP created-raise (all listeners)", tRe);
 
             // Register with the spatial index - one registration, every view:
             // cache-friendly batch AOE processing, growth occupancy (consumes the
@@ -746,6 +754,7 @@ namespace CosmicShore.Gameplay
                 // keeps its collider until a bubble boundary happens to cross it.
                 PrismColliderLodManager.NotifyPrismActivated(this);
             }
+            LoadInsights.AccumulateSample("Prism reveal: spatial index + cell bind + LOD", tRe);
         }
 
         /// <summary>
```

</details>

### `c45ea3f48` — fix(build): add missing LoadInsights using to Prism.cs

_Claude, 2026-07-28 19:01:01 +0000_

```text
The reveal-tick accumulators added in 4e0adec88 call LoadInsights from a file
that never imported CosmicShore.Utility.PerformanceBenchmark (its 'using
CosmicShore.Utility' only reaches the parent namespace), so the bare name did
not resolve: CS0103 at Prism.cs(721). Swept every non-tool file that
references LoadInsights - Prism.cs was the only one missing the import; the
rest either import it or use the qualified name.
```

```text
 Assets/_Scripts/Controller/Vessel/Prism.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index 9d6728b2e..1a8adaa71 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -4,6 +4,7 @@ using UnityEngine;
 using System.Collections;
 using CosmicShore.Core;
 using CosmicShore.Utility;
+using CosmicShore.Utility.PerformanceBenchmark;
 using CosmicShore.Gameplay;
 using CosmicShore.ScriptableObjects;
 using UnityEngine.Serialization;
```

</details>
