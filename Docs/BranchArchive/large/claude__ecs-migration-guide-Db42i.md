# Branch archive: `claude/ecs-migration-guide-Db42i`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-06-12 by Claude
- **Unmerged commits:** 12
- **Forked from:** `8a3d6d0d3` (2026-04-16, Merge pull request #483 from froglet-studio/claude/update-skimmer-prism-effect)
- **Tip:** `c33cf4a7f`
- **Files touched (17):**
  - `Assets/_Scripts/Game/ECS/Bridge.meta`
  - `Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs`
  - `Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs.meta`
  - `Assets/_Scripts/Game/ECS/Components/AOEComponents.cs`
  - `Assets/_Scripts/Game/ECS/Components/AOEComponents.cs.meta`
  - `Assets/_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs`
  - `Assets/_Scripts/Game/Managers/PrismAOERegistry.cs`
  - `Assets/_Scripts/Game/Managers/PrismEffectsManager.cs`
  - `Assets/_Scripts/Game/Projectiles/AOEExplosion.cs`
  - `Assets/_Scripts/Models/Enums/InputDeviceType.cs.meta`
  - `Assets/_Scripts/Utility/Tools/AOEBenchmarkOverlay.cs`
  - `Assets/_Scripts/Utility/Tools/AOEBenchmarkOverlay.cs.meta`
  - `Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs`
  - `Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs.meta`
  - `Assets/_Scripts/Utility/Tools/FrogletTools.cs`
  - `Docs/AOE_FIX_PORT_HANDOFF.md`
  - `Docs/ECS-Migration-Guide-Prisms.md`

### `d777cd7e3` — Add ECS migration guide for prism systems

_Claude, 2026-02-25 09:46:41 +0000_

```text
Documents the phased migration plan from MonoBehaviour to DOTS ECS,
covering the hybrid bridge, animation systems, effects, full entity
conversion, and networking considerations. Cross-references all
existing Burst-compiled managers and the PrismComponents.cs definitions.
```

```text
 Docs/ECS-Migration-Guide-Prisms.md | 247 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 247 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 253 lines)</summary>

```diff
diff --git a/Docs/ECS-Migration-Guide-Prisms.md b/Docs/ECS-Migration-Guide-Prisms.md
new file mode 100644
index 000000000..3bb9a280b
--- /dev/null
+++ b/Docs/ECS-Migration-Guide-Prisms.md
@@ -0,0 +1,247 @@
+# ECS Migration Guide: Cosmic Shore Prisms
+
+## Where You Stand
+
+The codebase is already ~60% DOTS-native in practice:
+
+| Layer | Current State |
+|-------|--------------|
+| Data layout | Cache-packed NativeArrays (`PrismSpatialData` 16B, `PrismDamageData` 8B) |
+| Compute | 5 Burst-compiled `IJobParallelFor` across AOE, scale, material, effects, density |
+| ECS components | Already defined in `PrismComponents.cs` — `PrismData`, `ScaleAnimation`, `MaterialAnimation`, `ShieldTimer`, `ExplosionEffect`, `ImplosionEffect` |
+| Lifecycle | Centralized managers with register/unregister patterns (effectively entity lifecycle) |
+| MonoBehaviour | Still the backbone — 5 RequireComponents per prism, coroutine init, event callbacks |
+
+The existing ECS components in `PrismComponents.cs` map cleanly to the current systems. The migration is essentially replacing the plumbing (MonoBehaviour lifecycle, singletons, callbacks) while keeping the data layout and job logic already in place.
+
+### Key Files
+
+| File | Path | Role |
+|------|------|------|
+| `PrismComponents.cs` | `Assets/_Scripts/Game/ECS/Components/PrismComponents.cs` | ECS component definitions |
+| `PrismAOERegistry.cs` | `Assets/_Scripts/Game/Managers/PrismAOERegistry.cs` | Hot/cold NativeArray AOE processing |
+| `PrismScaleManager.cs` | `Assets/_Scripts/Game/Managers/PrismScaleManager.cs` | Burst-compiled scale animation |
+| `MaterialStateManager.cs` | `Assets/_Scripts/Game/Managers/MaterialStateManager.cs` | Burst-compiled material animation |
+| `PrismTimerManager.cs` | `Assets/_Scripts/Game/Managers/PrismTimerManager.cs` | Centralized shield timers |
+| `PrismEffectsManager.cs` | `Assets/_Scripts/Game/Managers/PrismEffectsManager.cs` | Burst-compiled explosion/implosion VFX |
+| `Prism.cs` | `Assets/_Scripts/Game/Ship/Prism.cs` | Core MonoBehaviour (migration target) |
+| `PrismFactory.cs` | `Assets/_Scripts/Game/Prisms/PrismFactory.cs` | Spawn factory with pool managers |
+
+---
+
+## Recommended Phased Migration
+
+### Phase 0: Hybrid Bridge
+
+Convert `PrismAOERegistry` from a singleton managing NativeArrays to an `ISystem` reading from ECS components. This is the lowest-risk, highest-reward step because:
+
+- The registry already stores data in NativeArrays separate from GameObjects
+- `AOESpatialQueryJob` is already Burst-compiled and doesn't touch managed types
+- You just need to swap the data source from manual arrays to an `EntityQuery`
+
+```
+Before: PrismAOERegistry._spatial[i].Position  (manual NativeArray)
+After:  EntityQuery over PrismData components   (ECS-managed memory)
+```
+
+Each prism MonoBehaviour gets a companion entity via a `PrismEntityBridge` component that holds the `Entity` reference. The MonoBehaviour pushes state changes to the entity; the `ISystem` reads from entities for batch processing.
+
+#### Implementation sketch
+
+```csharp
+// New bridge component on the MonoBehaviour side
+public class PrismEntityBridge : MonoBehaviour
+{
+    public Entity Entity;
+    public EntityManager EntityManager;
+}
+
+// In Prism.Initialize(), after existing setup:
+var world = World.DefaultGameObjectInjectionWorld;
+var em = world.EntityManager;
+var entity = em.CreateEntity(
+    typeof(PrismData),
+    typeof(LocalTransform)
+);
+em.SetComponentData(entity, new PrismData
+{
+    Position = transform.position,
+    Domain = (int)Domain,
+    Volume = prismProperties.volume,
+    IsShielded = prismProperties.IsShielded ? (byte)1 : (byte)0,
+    // ...
+});
+bridge.Entity = entity;
```

</details>

### `93b859a70` — Implement Phase 0 hybrid bridge for ECS prism AOE processing

_Claude, 2026-02-25 10:08:26 +0000_

```text
New files:
- AOEComponents.cs: AOESpatial (16B), AOEDamage (8B), AOEManagedRef
  IComponentData structs mirroring the existing hot/cold NativeArray layout
- PrismEntityBridge.cs: companion entity lifecycle per prism, with
  static UseECS toggle for A/B testing ECS vs legacy query paths

Modified:
- PrismAOERegistry.cs: parallel _bridges[] array, companion entity
  creation in Register(), bridge sync in UpdateShieldState/MarkDestroyed/
  UpdateDomain/UpdateVolume, new ProcessExplosionFrameECS() that reads
  from EntityQuery and reinterprets AOESpatial as PrismSpatialData for
  the existing Burst job
- ECS-Migration-Guide-Prisms.md: added Phase 0 implementation section

The legacy path is unchanged and remains the default. Setting
PrismEntityBridge.UseECS = true activates the ECS-backed query path.
Both paths keep data in sync, so toggling mid-session is safe.
```

```text
 Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs | 187 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/ECS/Components/AOEComponents.cs |  46 ++++++++
 Assets/_Scripts/Game/Managers/PrismAOERegistry.cs    | 282 ++++++++++++++++++++++++++++++++++++++++++++-----
 Docs/ECS-Migration-Guide-Prisms.md                   |  73 +++++++++++++
 4 files changed, 562 insertions(+), 26 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 776 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs b/Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs
new file mode 100644
index 000000000..15c002fe7
--- /dev/null
+++ b/Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs
@@ -0,0 +1,187 @@
+using Unity.Entities;
+using Unity.Mathematics;
+using UnityEngine;
+using CosmicShore.Game;
+
+namespace CosmicShore.ECS
+{
+    /// <summary>
+    /// Hybrid bridge between MonoBehaviour Prism and ECS companion entity.
+    ///
+    /// Phase 0 of the ECS migration: each prism GameObject gets a companion entity
+    /// with AOESpatial + AOEDamage + AOEManagedRef components. The MonoBehaviour
+    /// pushes state changes to the entity; PrismAOERegistry reads from EntityQuery
+    /// for batch processing when UseECSQuery is enabled.
+    ///
+    /// Lifecycle:
+    ///   CreateCompanionEntity()  — called after AOE registry registration
+    ///   UpdatePosition()         — called when prism position is known (post-spawn)
+    ///   UpdateFlags()            — called on shield/destroy state changes
+    ///   UpdateDamageData()       — called on domain/volume changes
+    ///   DestroyCompanionEntity() — called on pool return or destroy
+    /// </summary>
+    public class PrismEntityBridge : MonoBehaviour
+    {
+        /// <summary>
+        /// Master toggle for the ECS hybrid path. When false, the bridge is inert
+        /// and PrismAOERegistry uses its legacy NativeArray path.
+        /// </summary>
+        public static bool UseECS { get; set; }
+
+        public Entity CompanionEntity { get; private set; }
+        public bool HasEntity { get; private set; }
+
+        private EntityManager _entityManager;
+        private static EntityArchetype _prismArchetype;
+        private static bool _archetypeInitialized;
+
+        private static EntityArchetype GetOrCreateArchetype(EntityManager em)
+        {
+            if (!_archetypeInitialized)
+            {
+                _prismArchetype = em.CreateArchetype(
+                    typeof(AOESpatial),
+                    typeof(AOEDamage),
+                    typeof(AOEManagedRef)
+                );
+                _archetypeInitialized = true;
+            }
+            return _prismArchetype;
+        }
+
+        /// <summary>
+        /// Creates the companion entity with initial spatial, damage, and managed ref data.
+        /// Called from Prism after AOE registry registration so the managed index is known.
+        /// </summary>
+        public void CreateCompanionEntity(
+            float3 position,
+            byte flags,
+            float volume,
+            int domain,
+            int managedIndex)
+        {
+            if (!UseECS) return;
+            if (HasEntity) return;
+
+            var world = World.DefaultGameObjectInjectionWorld;
+            if (world == null || !world.IsCreated) return;
+
+            _entityManager = world.EntityManager;
+            var archetype = GetOrCreateArchetype(_entityManager);
+            CompanionEntity = _entityManager.CreateEntity(archetype);
+
+            _entityManager.SetComponentData(CompanionEntity, new AOESpatial
+            {
```

</details>

### `f3cd38304` — Add missing Unity .meta files for ECS bridge components

_Claude, 2026-03-01 20:57:28 +0000_

```text
Bridge folder, PrismEntityBridge.cs, and AOEComponents.cs all lacked
.meta files which prevents Unity from tracking them via GUID.
```

```text
 Assets/_Scripts/Game/ECS/Bridge.meta                      |  8 ++++++++
 Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs.meta | 11 +++++++++++
 Assets/_Scripts/Game/ECS/Components/AOEComponents.cs.meta | 11 +++++++++++
 3 files changed, 30 insertions(+)
```

### `d72c373ee` — Fix Entity type clash — alias Unity.Entities.Entity as EcsEntity

_Claude, 2026-03-02 05:04:02 +0000_

```text
BoidSimulationController.cs defines a global `Entity` struct that
shadows `Unity.Entities.Entity`. Use a `using EcsEntity = ...` alias
and fully qualify EntityManager/EntityArchetype/World to resolve
all 19 CS0029/CS1503 compile errors.
```

```text
 Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs | 16 +++++++++-------
 1 file changed, 9 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs b/Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs
index 15c002fe7..2454c8367 100644
--- a/Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs
+++ b/Assets/_Scripts/Game/ECS/Bridge/PrismEntityBridge.cs
@@ -1,8 +1,10 @@
-using Unity.Entities;
 using Unity.Mathematics;
 using UnityEngine;
 using CosmicShore.Game;
 
+// Alias to avoid clash with global Entity struct in BoidSimulationController.cs
+using EcsEntity = Unity.Entities.Entity;
+
 namespace CosmicShore.ECS
 {
     /// <summary>
@@ -28,14 +30,14 @@ namespace CosmicShore.ECS
         /// </summary>
         public static bool UseECS { get; set; }
 
-        public Entity CompanionEntity { get; private set; }
+        public EcsEntity CompanionEntity { get; private set; }
         public bool HasEntity { get; private set; }
 
-        private EntityManager _entityManager;
-        private static EntityArchetype _prismArchetype;
+        private Unity.Entities.EntityManager _entityManager;
+        private static Unity.Entities.EntityArchetype _prismArchetype;
         private static bool _archetypeInitialized;
 
-        private static EntityArchetype GetOrCreateArchetype(EntityManager em)
+        private static Unity.Entities.EntityArchetype GetOrCreateArchetype(Unity.Entities.EntityManager em)
         {
             if (!_archetypeInitialized)
             {
@@ -63,7 +65,7 @@ namespace CosmicShore.ECS
             if (!UseECS) return;
             if (HasEntity) return;
 
-            var world = World.DefaultGameObjectInjectionWorld;
+            var world = Unity.Entities.World.DefaultGameObjectInjectionWorld;
             if (world == null || !world.IsCreated) return;
 
             _entityManager = world.EntityManager;
@@ -104,7 +106,7 @@ namespace CosmicShore.ECS
                 _entityManager.DestroyEntity(CompanionEntity);
             }
 
-            CompanionEntity = Entity.Null;
+            CompanionEntity = EcsEntity.Null;
             HasEntity = false;
         }
 
```

</details>

### `1e9c07432` — pushing junk

_Garrett Milliron, 2026-03-02 01:48:36 -0500_

```text
 Assets/_Scripts/Models/Enums/InputDeviceType.cs.meta | 2 ++
 1 file changed, 2 insertions(+)
```

### `2edbd1305` — Add ProfilerMarker instrumentation and AOE benchmark overlay

_Claude, 2026-03-02 09:00:31 +0000_

```text
11 ProfilerMarkers across 4 hot-path files:
- ExplosionImpactor: AOE.OnTriggerEnter, AOE.OnTriggerEnter.Skipped,
  AOE.ProcessBatchFrame
- PrismAOERegistry: AOE.ProcessExplosion, AOE.BurstJob.Schedule,
  AOE.BurstJob.ScheduleECS, AOE.ResolveDamage.Legacy,
  AOE.ResolveDamage.ECS
- AOEExplosion: AOE.ExplodeAsync.Frame
- PrismEffectsManager: Prism.ProcessExplosions, Prism.ProcessImplosions

Key changes:
- Disable SphereCollider when batch processing is active — prevents
  PhysX from computing 350+ trigger pairs per explosion frame
- Add warning when PrismAOERegistry is unavailable (silent fallback
  bug is now visible in console)
- Expose ForceLegacyPhysics toggle for A/B benchmarking
- AOEBenchmarkOverlay: F9 / FrogletTools menu IMGUI overlay showing
  per-frame and rolling-average timing via ProfilerRecorder, with
  toggle buttons for Physics / Burst Legacy / Burst ECS modes
```

```text
 Assets/_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs |  84 +++++++----
 Assets/_Scripts/Game/Managers/PrismAOERegistry.cs                 |  64 ++++++---
 Assets/_Scripts/Game/Managers/PrismEffectsManager.cs              |  16 ++-
 Assets/_Scripts/Game/Projectiles/AOEExplosion.cs                  |  80 +++++++----
 Assets/_Scripts/Utility/Tools/AOEBenchmarkOverlay.cs              | 278 ++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Tools/AOEBenchmarkOverlay.cs.meta         |  11 ++
 Assets/_Scripts/Utility/Tools/FrogletTools.cs                     |   8 ++
 7 files changed, 460 insertions(+), 81 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 783 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs b/Assets/_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs
index 40fb253ec..a3e0a3e91 100644
--- a/Assets/_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs
+++ b/Assets/_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs
@@ -1,6 +1,7 @@
 using System.Collections.Generic;
 using CosmicShore.Core;
 using CosmicShore.Game.Projectiles;
+using Unity.Profiling;
 using UnityEngine;
 
 namespace CosmicShore.Game
@@ -24,13 +25,26 @@ namespace CosmicShore.Game
         private static int _trailBlockLayer = -1;
         private HashSet<int> _batchHitTracker;
 
+        public bool IsBatchProcessing => _useBatchProcessing;
+
+        /// <summary>
+        /// When true, BeginBatchProcessing() is a no-op — forces Physics OnTriggerEnter
+        /// for all collisions. Used by AOEBenchmarkOverlay for A/B comparison.
+        /// </summary>
+        public static bool ForceLegacyPhysics { get; set; }
+
+        // --- ProfilerMarkers ---
+        private static readonly ProfilerMarker s_onTriggerEnter = new("AOE.OnTriggerEnter");
+        private static readonly ProfilerMarker s_onTriggerSkipped = new("AOE.OnTriggerEnter.Skipped");
+        private static readonly ProfilerMarker s_processBatch = new("AOE.ProcessBatchFrame");
+
         void Awake()
         {
             explosion ??= GetComponent<AOEExplosion>();
             if (_trailBlockLayer < 0)
                 _trailBlockLayer = LayerMask.NameToLayer("TrailBlocks");
         }
-        
+
         /// <summary>
         /// Begins batch AOE processing for this explosion's lifetime.
         /// Call once when the explosion starts. While active, prism collisions
@@ -38,8 +52,16 @@ namespace CosmicShore.Game
         /// </summary>
         public void BeginBatchProcessing()
         {
+            if (ForceLegacyPhysics) return;
+
             var registry = PrismAOERegistry.Instance;
-            if (registry == null || !registry.IsAvailable) return;
+            if (registry == null || !registry.IsAvailable)
+            {
+#if DEVELOPMENT_BUILD || UNITY_EDITOR
+                Debug.LogWarning("[ExplosionImpactor] PrismAOERegistry unavailable — falling back to Physics triggers");
+#endif
+                return;
+            }
             _useBatchProcessing = true;
             // Reuse cached HashSet to avoid GC allocation per explosion
             if (_batchHitTracker == null)
@@ -56,17 +78,20 @@ namespace CosmicShore.Game
         /// </summary>
         public bool ProcessBatchFrame(Vector3 center, float radius, float speed, float inertia)
         {
-            if (!_useBatchProcessing) return true;
-            var registry = PrismAOERegistry.Instance;
-            if (registry == null) return true;
-
-            return registry.ProcessExplosionFrame(
-                center, radius, speed, inertia,
-                explosion.Domain,
-                affectSelf, destructive, devastating, shielding,
-                explosion.AnonymousExplosion,
-                explosion.Vessel,
-                _batchHitTracker);
+            using (s_processBatch.Auto())
+            {
+                if (!_useBatchProcessing) return true;
+                var registry = PrismAOERegistry.Instance;
+                if (registry == null) return true;
+
+                return registry.ProcessExplosionFrame(
```

</details>

### `10930dab8` — Fix colliderDisabledForBatch scope — move declaration before try block

_Claude, 2026-03-04 01:30:37 +0000_

```text
The variable was declared inside the try block but referenced in
catch blocks, causing CS0103. Move it to method scope so both
catch blocks can restore the SphereCollider.
```

```text
 Assets/_Scripts/Game/Projectiles/AOEExplosion.cs | 3 ++-
 1 file changed, 2 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Projectiles/AOEExplosion.cs b/Assets/_Scripts/Game/Projectiles/AOEExplosion.cs
index 656b6b228..2b0400da8 100644
--- a/Assets/_Scripts/Game/Projectiles/AOEExplosion.cs
+++ b/Assets/_Scripts/Game/Projectiles/AOEExplosion.cs
@@ -134,6 +134,8 @@ namespace CosmicShore.Game.Projectiles
         {
             // Cache impactor ref — _explosionImpactor may be null after Destroy
             var impactor = _explosionImpactor;
+            // Track collider state outside try/catch so catch blocks can restore it
+            bool colliderDisabledForBatch = false;
             try
             {
                 // Start batch AOE processing — skips Physics OnTriggerEnter for prisms
@@ -141,7 +143,6 @@ namespace CosmicShore.Game.Projectiles
 
                 // When batch processing is active, disable the SphereCollider entirely.
                 // PhysX won't compute any trigger pairs — the Burst job handles spatial queries.
-                bool colliderDisabledForBatch = false;
                 if (impactor != null && impactor.IsBatchProcessing && _sphereCollider != null)
                 {
                     _sphereCollider.enabled = false;
```

</details>

### `09ddda812` — Add automated AOE benchmark runner for Physics/Burst/ECS comparison

_Claude, 2026-03-04 03:57:40 +0000_

```text
- AOEBenchmarkRunner: registers synthetic prisms, runs ProcessExplosionFrame
  across Physics, Burst Legacy, and Burst ECS modes (each twice by default),
  and prints a formatted report with avg/min/max/total ms and speedup ratios
- PrismAOERegistry: add RegisterSynthetic() and ClearAll() internal methods
  to support benchmarking without requiring full Prism MonoBehaviour setup
- FrogletTools: add "Run AOE Benchmark" menu item (Play Mode only)
```

```text
 Assets/_Scripts/Game/Managers/PrismAOERegistry.cs        |  46 ++++++++
 Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs      | 258 +++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs.meta |  11 ++
 Assets/_Scripts/Utility/Tools/FrogletTools.cs            |  21 ++++
 4 files changed, 336 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 353 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
index 07dde89a3..ae7cc29ac 100644
--- a/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
+++ b/Assets/_Scripts/Game/Managers/PrismAOERegistry.cs
@@ -294,6 +294,52 @@ namespace CosmicShore.Game
 
         #endregion
 
+        #region Benchmark Support
+
+        /// <summary>
+        /// Registers synthetic prism data for benchmarking without requiring a Prism MonoBehaviour.
+        /// The managed _prisms[index] slot is null — ResolveDamage will skip it after the
+        /// spatial query, so this isolates Burst job cost from damage application cost.
+        /// </summary>
+        internal int RegisterSynthetic(float3 position, byte flags, float volume, int domain)
+        {
+            if (!_spatial.IsCreated) return -1;
+            int index;
+            if (_freeList.Count > 0)
+                index = _freeList.Pop();
+            else
+            {
+                index = _highWaterMark++;
+                EnsureCapacity(index);
+            }
+
+            _prisms[index] = null;
+            _bridges[index] = null;
+            _spatial[index] = new PrismSpatialData { Position = position, Flags = flags };
+            _damage[index] = new PrismDamageData { Volume = volume, Domain = domain };
+            return index;
+        }
+
+        /// <summary>
+        /// Clears all registered prisms. Used by benchmark to reset between runs.
+        /// </summary>
+        internal void ClearAll()
+        {
+            if (!_spatial.IsCreated) return;
+            for (int i = 0; i < _highWaterMark; i++)
+            {
+                _prisms[i] = null;
+                _bridges[i] = null;
+                var s = _spatial[i];
+                s.Flags = 0;
+                _spatial[i] = s;
+            }
+            _freeList.Clear();
+            _highWaterMark = 0;
+        }
+
+        #endregion
+
         #region AOE Processing
 
         /// <summary>
diff --git a/Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs b/Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs
new file mode 100644
index 000000000..0b1b53422
--- /dev/null
+++ b/Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs
@@ -0,0 +1,258 @@
+#if DEVELOPMENT_BUILD || UNITY_EDITOR
+using System.Collections.Generic;
+using System.Diagnostics;
+using Cysharp.Threading.Tasks;
+using Unity.Mathematics;
+using UnityEngine;
+using CosmicShore.Core;
+using CosmicShore.ECS;
+using CosmicShore.Game;
+using Debug = UnityEngine.Debug;
+using Random = UnityEngine.Random;
+
+namespace CosmicShore.Utility.Tools
+{
+    /// <summary>
+    /// Automated AOE benchmark. Spawns synthetic prisms, runs explosions across
+    /// Physics / Burst Legacy / Burst ECS modes (each twice), and prints a report.
```

</details>

### `7f9ab15d7` — docs: add AOE fix port handoff for bleeding-edge

_Claude, 2026-06-12 12:18:15 +0000_

```text
Captures root cause analysis (76% frame time in OnTriggerEnter),
the two defects (silent fallback + redundant PhysX), Squirrel impact
analysis, and file mapping from development → bleeding-edge paths.
```

```text
 Docs/AOE_FIX_PORT_HANDOFF.md | 71 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 71 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/AOE_FIX_PORT_HANDOFF.md b/Docs/AOE_FIX_PORT_HANDOFF.md
new file mode 100644
index 000000000..8949245c6
--- /dev/null
+++ b/Docs/AOE_FIX_PORT_HANDOFF.md
@@ -0,0 +1,71 @@
+# AOE Fix Port Handoff — bleeding-edge
+
+## What this branch proved
+
+Branch `claude/ecs-migration-guide-Db42i` (based on `development`) identified and
+fixed a critical performance defect in the AOE explosion → prism damage path:
+
+- **Root cause**: `ExplosionImpactor.OnTriggerEnter` consumed 76.1% of frame time
+  (61.55 ms self, 350 calls, 0.9 MB alloc per frame).
+- **Defect 1 — silent fallback**: `BeginBatchProcessing()` returns without setting
+  `_useBatchProcessing = true` when the registry singleton is null. Every explosion
+  silently falls back to Physics OnTriggerEnter against all prisms.
+- **Defect 2 — redundant PhysX work**: Even when batch processing IS active, the
+  AOEExplosion's SphereCollider stays enabled, so PhysX still computes all trigger
+  pairs (350+ per frame) even though `OnTriggerEnter` skips them.
+
+### Fixes applied (development codebase paths)
+
+| File (development) | Change |
+|---|---|
+| `Assets/_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs` | `Debug.LogWarning` on fallback; `IsBatchProcessing` property; `ForceLegacyPhysics` static toggle for A/B; 3 ProfilerMarkers |
+| `Assets/_Scripts/Game/Projectiles/AOEExplosion.cs` | Disable SphereCollider during batch processing; restore on all exit paths; 1 ProfilerMarker |
+| `Assets/_Scripts/Game/Managers/PrismAOERegistry.cs` | 5 ProfilerMarkers; `RegisterSynthetic()` + `ClearAll()` for benchmarking |
+| `Assets/_Scripts/Game/Managers/PrismEffectsManager.cs` | 2 ProfilerMarkers |
+| `Assets/_Scripts/Utility/Tools/AOEBenchmarkOverlay.cs` | Runtime F9 IMGUI overlay (ProfilerRecorder-based) |
+| `Assets/_Scripts/Utility/Tools/AOEBenchmarkRunner.cs` | Automated Physics/Burst/ECS comparison benchmark |
+| `Assets/_Scripts/Utility/Tools/FrogletTools.cs` | Menu items for overlay + benchmark runner |
+
+### Squirrel impact analysis
+
+Squirrel triggers AOEExplosions through two paths, both terminating in the fixed code:
+
+1. **Crystal collision**: `SquirrelVesselExplosionByCrystalEffect` → `ExplosionHelper.CreateExplosion`
+   → AOEExplosion → ExplosionImpactor. Fires on every crystal pickup (0.15s cooldown, scale 400).
+2. **Skimmer jousting**: `SquirrelSkimmerImpactorDataContainer` → `VesselExplosionBySkimmerEffect`
+   → same path. Fires when a faster vessel skims past a slower one.
+
+In a racing session with trail prisms accumulating, these scale-400 explosions resolve
+against potentially thousands of prisms — the exact hot path that was profiled at 76% frame time.
+
+## bleeding-edge target mapping
+
+bleeding-edge reorganized scripts into `Assets/_Scripts/Controller/` and renamed
+`PrismAOERegistry` → `PrismSpatialIndex`. The same two defects exist there.
+
+| development path | bleeding-edge path |
+|---|---|
+| `_Scripts/Game/ImpactEffects/Impactors/ExplosionImpactor.cs` | `_Scripts/Controller/ImpactEffects/Impactors/ExplosionImpactor.cs` |
+| `_Scripts/Game/Projectiles/AOEExplosion.cs` | `_Scripts/Controller/Projectiles/AOEExplosion.cs` |
+| `_Scripts/Game/Managers/PrismAOERegistry.cs` | `_Scripts/Controller/Managers/PrismSpatialIndex.cs` |
+| `_Scripts/Game/Managers/PrismEffectsManager.cs` | `_Scripts/Controller/Managers/PrismEffectsManager.cs` |
+
+bleeding-edge already has:
+- The same Burst `AOESpatialQueryJob` and hot/cold data split
+- `MAX_NEW_HITS_PER_FRAME = 48` throttle
+- `BeginBatchProcessing` / `ProcessBatchFrame` / `EndBatchProcessing` API
+- A mature `PerformanceBenchmark` framework in `_Scripts/Utility/PerformanceBenchmark/`
+
+bleeding-edge does NOT have:
+- The SphereCollider disable during batch processing
+- The fallback warning log
+- Any ProfilerMarkers on the AOE hot path
+- The `ForceLegacyPhysics` A/B toggle
+- The AOE-specific benchmark runner (their framework measures whole-frame stats)
+
+## Port priority
+
+1. SphereCollider disable + fallback warning (highest value, smallest diff)
+2. ProfilerMarkers on AOE hot path (11 markers across 4 files)
+3. AOE benchmark runner (adapt `RegisterSynthetic`/`ClearAll` to `PrismSpatialIndex`)
+4. Benchmark overlay (optional — their `DiagnosticsHUD` may already cover this)
```

</details>

_Also contains 3 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
