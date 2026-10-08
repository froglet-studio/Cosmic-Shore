# Branch archive: `claude/game-load-time-optimization-b3j58o`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-07-28 by Claude
- **Unmerged commits:** 2
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/630
- **Forked from:** `ddecb1dd8` (2026-07-28, Merge branch 'bleeding-edge' of https://github.com/froglet-studio/Cosmic-Shore)
- **Tip:** `9f7f12708`
- **Files touched (6):**
  - `Assets/_Prefabs/Spawnables/SpawnableHelicoid.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnableSchwarzP.prefab`
  - `Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs`
  - `Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`
  - `Docs/PERFORMANCE_OPTIMIZATION.md`

### `32f8b9527` — perf(load): cut Joust/Scurry intensity-3 load time from ~37s to ~15s

_Claude, 2026-07-28 23:00:06 +0000_

```text
Root-caused from a Load Time Insights recording (Menu_Main ->
MinigameJoust_Gameplay, MultiplayerJoust intensity 3, Editor, 37.49 s).
Load time is prism count, near-linearly: the streamed lay of 49,856 prisms
was 22.6 s and the post-lay arena settle another 11.8 s, while our own
per-prism spawn contract totalled just 1.57 s across all of them. The
remaining ~0.66 ms/prism is engine-side clone/integration plus creation
bookkeeping, so no code fix alone reaches <20 s at that count.

Content (intensity 3 only, shape envelope untouched - same periodScale /
turns / height / radii, only sampling thins):
- SpawnableSchwarzP: samplesPerPeriod 30 -> 24, surfaceThreshold 0.4 -> 0.35
  (49,856 -> 22,048 prisms). The shell was 2.2-3.8 sample layers thick while
  each prism is a 12-unit needle along the surface normal; post-change it
  still holds 1.55-2.67 layers everywhere, so the surface stays closed.
- SpawnableHelicoid: samplesPerTurn 40 -> 30, radialSamples 80 -> 40
  (57,840 -> 21,720 prisms).

Behind the load gate (connecting screen covered - throughput is the only
goal there; every gameplay path keeps its authored budgets):
- Prisms settle at creation instead of animating then being force-snapped by
  SettleGrowWatch. Same call, same post-registration position, N frames
  earlier. Not a continuity-of-existence exemption: the gate guarantees
  nothing here is on screen, and prisms born outside it still bloom in.
- Prism.LoadGateCreationCompletionsPerFrame 512 -> 4096.
- AsyncInstantiateOperation integration slice raised to 200 ms and restored
  on release; Unity's 10 ms default is tuned for a running game.
- Clone batch 256 -> 1024; SettleSnapsPerPoll 2000 -> 8192.

Also fixes the one error in the recording: ElementalComebackSystem read its
[Inject] GameDataSO in OnEnable, which runs before Reflex injection - it
logged and returned, so every scene-authored instance never subscribed to a
single game event. Now deferred-subscribe (OnEnable -> retry in Start,
double-subscribe guarded).

Intensity 4 is only ~9,859 prisms in both modes (Gyroid, BFS replayed
offline) - if it still loads slow, the cause is not the environment build
and needs its own recording. Analysis + verification steps in
Docs/PERFORMANCE_OPTIMIZATION.md section 0.0.
```

```text
 Assets/_Prefabs/Spawnables/SpawnableHelicoid.prefab                  |   4 +-
 Assets/_Prefabs/Spawnables/SpawnableSchwarzP.prefab                  |   4 +-
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs         |  30 ++++++---
 Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs |  51 +++++++++++++++-
 Assets/_Scripts/Controller/Vessel/Prism.cs                           |  23 ++++++-
 Docs/PERFORMANCE_OPTIMIZATION.md                                     | 107 +++++++++++++++++++++++++++++++++
 6 files changed, 204 insertions(+), 15 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 303 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index 77e7b067a..fb981abea 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -118,18 +118,33 @@ namespace CosmicShore.Gameplay
         // Index matches AllElements order: Mass=0, Charge=1, Space=2, Time=3.
         readonly float[] _lastComebackAudioTime = { -999f, -999f, -999f, -999f };
 
-        void OnEnable()
+        // Reflex populates [Inject] fields AFTER Awake but BEFORE Start, so a scene-authored
+        // instance reaches OnEnable with gameData still null. Deferred-subscribe (CLAUDE.md ▸ DI
+        // Patterns): attempt in OnEnable, retry in Start, guard against double-subscribe.
+        // Previously OnEnable logged "GameDataSO is not assigned!" and returned — which meant
+        // every scene-authored comeback system also never subscribed to a single game event.
+        bool _subscribed;
+
+        void OnEnable() => TrySubscribe();
+
+        void Start()
         {
-            if (gameData == null)
-            {
-                CSDebug.LogError("[ElementalComebackSystem] GameDataSO is not assigned!");
-                return;
-            }
+            TrySubscribe();
+            if (!_subscribed)
+                CSDebug.LogError("[ElementalComebackSystem] GameDataSO is not assigned — the " +
+                                 "scene needs a Reflex ContainerScope, or the component must be " +
+                                 "created via EnsureExists(host, gameData).", this);
+        }
+
+        void TrySubscribe()
+        {
+            if (_subscribed || gameData == null) return;
             // Profile is optional now (initial-levels only) - the system runs without one.
 
             gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
             gameData.OnMiniGameEnd.OnRaised += OnGameEnded;
+            _subscribed = true;
 
             if (debugLogging)
                 CSDebug.Log("[ElementalComebackSystem] Enabled and subscribed to game events.");
@@ -137,10 +152,11 @@ namespace CosmicShore.Gameplay
 
         void OnDisable()
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
index 5e18ed48a..616d23ca3 100644
--- a/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
+++ b/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
@@ -199,8 +199,11 @@ namespace CosmicShore.Gameplay
         const float ReadyStableSeconds = 0.5f;
 
         /// <summary>Grow-in snaps applied per gate poll — bounds the per-frame cost of
-        /// force-settling a 25k cohort (each snap runs full completion bookkeeping).</summary>
-        const int SettleSnapsPerPoll = 2000;
+        /// force-settling a 25k cohort (each snap runs full completion bookkeeping). Sized for
+        /// the covered screen: prisms laid while the gate holds now settle at creation
+        /// (Prism.CreateBlockCoroutine), so this only has to mop up the cohort laid BEFORE the
+        /// connecting panel armed the gate — draining it in one or two polls instead of a dozen.</summary>
+        const int SettleSnapsPerPoll = 8192;
 
         /// <summary>
         /// Announce an arena build whose SegmentSpawner.Initialize happens LATER than scene
@@ -230,6 +233,7 @@ namespace CosmicShore.Gameplay
         {
             s_loadGateHolding = holding;
             s_allClearSince = -1f;
+            ApplyIntegrationTimeForGate(holding);
             if (holding)
             {
                 s_loadGateStartTime = Time.unscaledTime;
@@ -384,6 +388,46 @@ namespace CosmicShore.Gameplay
         /// </summary>
         const int CloneBatchSize = 256;
 
+        /// <summary>
+        /// Clone batch used while the load gate holds. Bigger batches hand the engine more work
+        /// to spread across worker threads per call; the only reason to keep them small is the
+        /// progress readout, which behind the covered screen updates a spinner nobody is reading
+        /// frame-by-frame.
+        /// </summary>
+        const int LoadGateCloneBatchSize = 1024;
+
+        /// <summary>
+        /// Per-frame slice Unity spends INTEGRATING finished async clones on the main thread
+        /// (<see cref="AsyncInstantiateOperation"/>). The engine default is 10 ms — sized to keep
+        /// a running game smooth, which is exactly wrong while the connecting screen covers the
+        /// world: at 10 ms/frame a 50k-prism arena needs hundreds of frames just to integrate,
+        /// and every one of those frames is load time the player waits through. Raised for the
+        /// duration of the hold and restored the moment it ends, so gameplay streaming (the
+        /// freestyle microscene conveyor) keeps the engine default.
+        /// </summary>
+        const float LoadGateIntegrationTimeMs = 200f;
+
+        static float s_savedIntegrationTimeMs = -1f;
+
+        /// <summary>
+        /// Applies <see cref="LoadGateIntegrationTimeMs"/> while the gate holds and puts the
+        /// engine default back when it releases. Idempotent — safe to call on every edge.
+        /// </summary>
+        static void ApplyIntegrationTimeForGate(bool holding)
+        {
+            if (holding)
+            {
+                if (s_savedIntegrationTimeMs >= 0f) return; // already raised
+                s_savedIntegrationTimeMs = AsyncInstantiateOperation.GetIntegrationTimeMS();
+                AsyncInstantiateOperation.SetIntegrationTimeMS(LoadGateIntegrationTimeMs);
+                return;
+            }
+
+            if (s_savedIntegrationTimeMs < 0f) return; // never raised
+            AsyncInstantiateOperation.SetIntegrationTimeMS(s_savedIntegrationTimeMs);
+            s_savedIntegrationTimeMs = -1f;
+        }
+
         /// <summary>
         /// Set false to force the per-item clone path (kept as a live escape hatch: batched
         /// instantiate is an engine fast path, and the sync path is the behavioural baseline).
@@ -483,7 +527,8 @@ namespace CosmicShore.Gameplay
                 {
                     if (!parent) return; // container destroyed — stop laying
 
-                    int batch = Mathf.Min(CloneBatchSize, count - i);
+                    int batchSize = s_loadGateHolding ? LoadGateCloneBatchSize : CloneBatchSize;
+                    int batch = Mathf.Min(batchSize, count - i);
                     var clones = await CloneBatchAsync(prefab, batch, parent);
                     if (clones == null || !parent) return;
 
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index c9977ff52..9c2f087de 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -643,7 +643,13 @@ namespace CosmicShore.Gameplay
         // the "prisms load in batches during play" bug. Behind the covered screen the de-spike
         // rationale is void (there is no visible frame to protect), so the queue drains in a
         // handful of frames instead. Gameplay frames keep the authored cap untouched.
-        const int LoadGateCreationCompletionsPerFrame = 512;
+        //
```

</details>

### `9f7f12708` — revert(load): back out the load-gate throughput knobs; keep only the density cut

_Claude, 2026-07-28 23:20:45 +0000_

```text
The first pass raised four throughput knobs behind the arena-ready gate
(creation completions 512 -> 4096, AsyncInstantiateOperation integration
slice 10 -> 200 ms, gate clone batch 256 -> 1024, settle snaps 2000 -> 8192)
and settled prisms at creation instead of animating then force-snapping. In
the Editor that presents as a HANG on the connecting screen. Reverted in
full - Prism.cs, PrismTrailBuilder.cs and ElementalComebackSystem.cs are
byte-identical to bleeding-edge again.

Why it was wrong: every knob reduces TOTAL work, and behind a covered screen
there is nothing to keep smooth - but the connecting screen is not a static
image. It animates, and it only ticks once per frame. At 4096 creation
completions per frame (renderer on + collider on + SOAP raise + spatial
register + LOD notify + volume refresh, ~0.24 ms each) a single frame is ~1 s
of work before the 200 ms integration slice and the grow-in snaps land on
top. The sum may improve; what the player sees is a frozen spinner, which is
indistinguishable from a crash and strictly worse than a slower load that
keeps moving.

What still ships is the part that cannot change the loading flow at all -
the intensity-3 content density cut (SchwarzP 49,856 -> 22,048 prisms,
Helicoid 57,840 -> 21,720, shape envelopes untouched). That is also the only
lever that can reach the target: per-prism cost is ~0.66 ms and is engine-side
clone/integration, so the count is the load time.

ElementalComebackSystem's [Inject]-in-OnEnable bug is real but unrelated to
loading and makes a currently-dead system live mid-match, so it is left for
its own branch rather than riding along here.

Docs/PERFORMANCE_OPTIMIZATION.md section 0.0 records the measurement, the
revert, and the rule it bought: behind a loading screen, frame granularity is
a product requirement, not overhead - re-land throughput knobs in small
multiples with a recording after each, and treat "the spinner still spins" as
an acceptance criterion alongside the total.
```

```text
 Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs         |  30 ++-------
 Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs |  51 +-------------
 Assets/_Scripts/Controller/Vessel/Prism.cs                           |  23 +------
 Docs/PERFORMANCE_OPTIMIZATION.md                                     | 119 ++++++++++++++++++---------------
 4 files changed, 76 insertions(+), 147 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 340 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
index fb981abea..77e7b067a 100644
--- a/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
+++ b/Assets/_Scripts/Controller/Arcade/ElementalComebackSystem.cs
@@ -118,33 +118,18 @@ namespace CosmicShore.Gameplay
         // Index matches AllElements order: Mass=0, Charge=1, Space=2, Time=3.
         readonly float[] _lastComebackAudioTime = { -999f, -999f, -999f, -999f };
 
-        // Reflex populates [Inject] fields AFTER Awake but BEFORE Start, so a scene-authored
-        // instance reaches OnEnable with gameData still null. Deferred-subscribe (CLAUDE.md ▸ DI
-        // Patterns): attempt in OnEnable, retry in Start, guard against double-subscribe.
-        // Previously OnEnable logged "GameDataSO is not assigned!" and returned — which meant
-        // every scene-authored comeback system also never subscribed to a single game event.
-        bool _subscribed;
-
-        void OnEnable() => TrySubscribe();
-
-        void Start()
+        void OnEnable()
         {
-            TrySubscribe();
-            if (!_subscribed)
-                CSDebug.LogError("[ElementalComebackSystem] GameDataSO is not assigned — the " +
-                                 "scene needs a Reflex ContainerScope, or the component must be " +
-                                 "created via EnsureExists(host, gameData).", this);
-        }
-
-        void TrySubscribe()
-        {
-            if (_subscribed || gameData == null) return;
+            if (gameData == null)
+            {
+                CSDebug.LogError("[ElementalComebackSystem] GameDataSO is not assigned!");
+                return;
+            }
             // Profile is optional now (initial-levels only) - the system runs without one.
 
             gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised += OnTurnEnded;
             gameData.OnMiniGameEnd.OnRaised += OnGameEnded;
-            _subscribed = true;
 
             if (debugLogging)
                 CSDebug.Log("[ElementalComebackSystem] Enabled and subscribed to game events.");
@@ -152,11 +137,10 @@ namespace CosmicShore.Gameplay
 
         void OnDisable()
         {
-            if (!_subscribed || gameData == null) return;
+            if (gameData == null) return;
             gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
             gameData.OnMiniGameTurnEnd.OnRaised -= OnTurnEnded;
             gameData.OnMiniGameEnd.OnRaised -= OnGameEnded;
-            _subscribed = false;
         }
 
         void OnTurnStarted()
diff --git a/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs b/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
index 616d23ca3..5e18ed48a 100644
--- a/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
+++ b/Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs
@@ -199,11 +199,8 @@ namespace CosmicShore.Gameplay
         const float ReadyStableSeconds = 0.5f;
 
         /// <summary>Grow-in snaps applied per gate poll — bounds the per-frame cost of
-        /// force-settling a 25k cohort (each snap runs full completion bookkeeping). Sized for
-        /// the covered screen: prisms laid while the gate holds now settle at creation
-        /// (Prism.CreateBlockCoroutine), so this only has to mop up the cohort laid BEFORE the
-        /// connecting panel armed the gate — draining it in one or two polls instead of a dozen.</summary>
-        const int SettleSnapsPerPoll = 8192;
+        /// force-settling a 25k cohort (each snap runs full completion bookkeeping).</summary>
+        const int SettleSnapsPerPoll = 2000;
 
         /// <summary>
         /// Announce an arena build whose SegmentSpawner.Initialize happens LATER than scene
@@ -233,7 +230,6 @@ namespace CosmicShore.Gameplay
         {
             s_loadGateHolding = holding;
             s_allClearSince = -1f;
-            ApplyIntegrationTimeForGate(holding);
             if (holding)
             {
                 s_loadGateStartTime = Time.unscaledTime;
@@ -388,46 +384,6 @@ namespace CosmicShore.Gameplay
         /// </summary>
         const int CloneBatchSize = 256;
 
-        /// <summary>
-        /// Clone batch used while the load gate holds. Bigger batches hand the engine more work
-        /// to spread across worker threads per call; the only reason to keep them small is the
-        /// progress readout, which behind the covered screen updates a spinner nobody is reading
-        /// frame-by-frame.
-        /// </summary>
-        const int LoadGateCloneBatchSize = 1024;
-
-        /// <summary>
-        /// Per-frame slice Unity spends INTEGRATING finished async clones on the main thread
-        /// (<see cref="AsyncInstantiateOperation"/>). The engine default is 10 ms — sized to keep
-        /// a running game smooth, which is exactly wrong while the connecting screen covers the
-        /// world: at 10 ms/frame a 50k-prism arena needs hundreds of frames just to integrate,
-        /// and every one of those frames is load time the player waits through. Raised for the
-        /// duration of the hold and restored the moment it ends, so gameplay streaming (the
-        /// freestyle microscene conveyor) keeps the engine default.
-        /// </summary>
-        const float LoadGateIntegrationTimeMs = 200f;
-
-        static float s_savedIntegrationTimeMs = -1f;
-
-        /// <summary>
-        /// Applies <see cref="LoadGateIntegrationTimeMs"/> while the gate holds and puts the
-        /// engine default back when it releases. Idempotent — safe to call on every edge.
-        /// </summary>
-        static void ApplyIntegrationTimeForGate(bool holding)
-        {
-            if (holding)
-            {
-                if (s_savedIntegrationTimeMs >= 0f) return; // already raised
-                s_savedIntegrationTimeMs = AsyncInstantiateOperation.GetIntegrationTimeMS();
-                AsyncInstantiateOperation.SetIntegrationTimeMS(LoadGateIntegrationTimeMs);
-                return;
-            }
-
-            if (s_savedIntegrationTimeMs < 0f) return; // never raised
-            AsyncInstantiateOperation.SetIntegrationTimeMS(s_savedIntegrationTimeMs);
-            s_savedIntegrationTimeMs = -1f;
-        }
-
         /// <summary>
         /// Set false to force the per-item clone path (kept as a live escape hatch: batched
         /// instantiate is an engine fast path, and the sync path is the behavioural baseline).
@@ -527,8 +483,7 @@ namespace CosmicShore.Gameplay
                 {
                     if (!parent) return; // container destroyed — stop laying
 
-                    int batchSize = s_loadGateHolding ? LoadGateCloneBatchSize : CloneBatchSize;
-                    int batch = Mathf.Min(batchSize, count - i);
+                    int batch = Mathf.Min(CloneBatchSize, count - i);
                     var clones = await CloneBatchAsync(prefab, batch, parent);
                     if (clones == null || !parent) return;
 
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index 9c2f087de..c9977ff52 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -643,13 +643,7 @@ namespace CosmicShore.Gameplay
         // the "prisms load in batches during play" bug. Behind the covered screen the de-spike
         // rationale is void (there is no visible frame to protect), so the queue drains in a
         // handful of frames instead. Gameplay frames keep the authored cap untouched.
-        //
-        // Sized so a whole 50k arena drains in ~12 gate frames rather than ~100: Load Time
```

</details>
