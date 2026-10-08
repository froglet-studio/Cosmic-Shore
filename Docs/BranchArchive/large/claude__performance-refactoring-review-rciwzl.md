# Branch archive: `claude/performance-refactoring-review-rciwzl`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-07-02 by Claude
- **Unmerged commits:** 18
- **Forked from:** `c833c5800` (2026-07-01, Merge pull request #572 from froglet-studio/claude/freestyle-toy-system-sghk4n)
- **Tip:** `5de0cc276`
- **Files touched (73):**
  - `Assets/_Prefabs/Environment/CrystalSpace.prefab`
  - `Assets/_Scripts/Controller/AI/AIPilot.cs`
  - `Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs.meta`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitors/TurnMonitor.cs`
  - `Assets/_Scripts/Controller/Assemblers/WallAssembler.cs`
  - `Assets/_Scripts/Controller/Environment/Cell.cs`
  - `Assets/_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/BoidSimulationController.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/BoidSimulationController.cs.meta`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Controller/Environment/FloraAndFauna/Spindle.cs`
  - `Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs`
  - `Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs`
  - `Assets/_Scripts/Controller/Environment/Prisms/PrismScaleAnimator.cs`
  - `Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerAlignPrismEffectSO.cs`
  - `Assets/_Scripts/Controller/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs`
  - `Assets/_Scripts/Controller/Managers/PrismActivationQueue.cs`
  - `Assets/_Scripts/Controller/Managers/PrismActivationQueue.cs.meta`
  - `Assets/_Scripts/Controller/Managers/PrismScaleManager.cs`
  - `Assets/_Scripts/Controller/Managers/PrismStateManager.cs`
  - `Assets/_Scripts/Controller/Managers/PrismTimerManager.cs`
  - `Assets/_Scripts/Controller/Projectiles/AOEDangerHemisphereBlocks.cs`
  - `Assets/_Scripts/Controller/Projectiles/AOERadialBlocks.cs`
  - `Assets/_Scripts/Controller/Projectiles/Projectile.cs`
  - `Assets/_Scripts/Controller/Vessel/Animation/ParametricJetEffect.cs`
  - `Assets/_Scripts/Controller/Vessel/GunTransformer.cs`
  - `Assets/_Scripts/Controller/Vessel/Prism.cs`
  - `Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs`
  - `Assets/_Scripts/Controller/Vessel/ResourceSystem.cs`
  - `Assets/_Scripts/Controller/Vessel/Skimmer.cs`
  - `Assets/_Scripts/Controller/Vessel/Trail.cs`
  - `Assets/_Scripts/Controller/Vessel/VesselController.cs`
  - … and 33 more

### `c27c6a966` — docs(perf): add verified performance refactor review

_Claude, 2026-07-02 04:05:08 +0000_

```text
Codebase-wide review of which systems would yield the most performance
gain from a complete refactor. Six subsystem audits (ecosystem, vessel
core, UI, projectiles/FX/assemblers, AI/arcade, anti-pattern sweep)
produced 18 candidate findings; each was independently adversarially
verified against code, throttles, and scene/prefab GUID wiring.

Verified top targets: prism ECS/instancing (largest ceiling, already
roadmapped), AOE explosion lifecycle (unpooled Instantiate+reflection-DI
per detonation at full-auto rates, 72-144 per-block grow tasks per
burst), always-on crystal SkinnedMeshRenderers with UpdateWhenOffscreen,
spindle/crystal material clone churn (incl. two verified leaks), and the
DomainVolumeIndicator re-batching the whole Menu_Main canvas ~42Hz even
while invisible.

Also documents refuted/downgraded findings (ResourceSystem, assembler
swarm, fauna-at-current-scale, NGO NetworkVariable writes), a dead-code
purge list carrying latent perf traps, and 14 verified spot fixes.

Updates the prism audit's stale progress section (rec #1 shipped via
PrismEffectsManager; rec #4 resolved via sharedMaterial migration).
```

```text
 Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md |   5 +-
 Docs/PERFORMANCE_REFACTOR_REVIEW.md                    | 309 +++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 313 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 337 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md b/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
index efd2a8026..056bc1b3c 100644
--- a/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
+++ b/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
@@ -356,13 +356,16 @@ Similarly, replace `CreateBlockCoroutine` with a delayed-activation manager that
 
 Since the original audit, the following optimizations have been implemented or are in active development:
 
+- **PrismEffectsManager** — centralized Jobs+Burst explosion/implosion animation manager; `PrismExplosion`/`PrismImplosion` now only register state and the manager batch-applies via a shared MaterialPropertyBlock (Recommendation 1)
 - **PrismTimerManager** — centralized timer system replacing per-prism coroutines (Recommendation 6)
 - **Per-frame explosion VFX cap** — limits concurrent explosion effects (Recommendation 5)
+- **MaterialPropertyAnimator sharedMaterial migration** — the material-clone paths cited in Recommendation 4 now use `sharedMaterial` (the per-prism instancing leak is resolved)
 - **EventListenerBase GC elimination** — reduced garbage collection from event listener allocations
 - **PrismAOEData** — cache-line-aware data layout with hot/cold splitting and bit-packed flags for AOE queries
 - **Burst-compiled spatial queries** — replaces Physics-based AOE prism damage (partial implementation of Recommendation 7)
+- **PrismColliderLodManager** — proximity-based collider culling (partial implementation of Recommendation 7)
 
-Recommendations 1-3 (Jobs-based explosion manager, GPU instanced rendering, full DOTS conversion) remain unimplemented.
+Recommendations 2 (GPU instanced explosion rendering), 3 (full DOTS conversion), and the collider-replacement remainder of 7 remain unimplemented — see `Docs/PERFORMANCE_REFACTOR_REVIEW.md` (July 2026) for the verified codebase-wide follow-up.
 
 ---
 
diff --git a/Docs/PERFORMANCE_REFACTOR_REVIEW.md b/Docs/PERFORMANCE_REFACTOR_REVIEW.md
new file mode 100644
index 000000000..33c91de95
--- /dev/null
+++ b/Docs/PERFORMANCE_REFACTOR_REVIEW.md
@@ -0,0 +1,309 @@
+# Performance Refactor Review — Verified Candidates (July 2026)
+
+**What this is.** A codebase-wide review answering: *which systems would produce the most
+performance gain if completely refactored?* Six subsystem audits (ecosystem, vessel core, UI,
+projectiles/FX/assemblers, AI/arcade, codebase-wide anti-pattern sweep) produced 18 candidate
+findings; every finding was then **independently adversarially verified** against the actual
+code — line citations, throttle/gating checks, and scene/prefab GUID wiring checks. Several
+plausible-looking "hot paths" turned out to be dead code or already-mitigated; several others
+turned out worse than first reported. Only verified findings appear below.
+
+**Companion docs:** `Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md` (prism system),
+`Docs/SPATIAL_INDEX.md` (spatial queries), `Docs/ECOSYSTEM_MASTERPLAN.md` §4 (collider budget).
+
+---
+
+## The headline: this codebase is past the easy wins
+
+The historically hottest per-entity paths have already been refactored to the target
+architecture and should be **left alone**:
+
+- **`PrismSpatialIndex`** — Burst AOE/occupancy/proximity queries, no physics. AOE damage is
+  fully migrated (`AOEExplosion` batches via `ExplosionImpactor.ProcessBatchFrame`, prism layer
+  excluded from its collider). Assemblers migrated off `Physics.CheckBox`/`OverlapSphere`.
+- **`PrismEffectsManager`** — explosion/implosion animation is Burst-jobbed and batch-applied
+  (audit rec #1 **shipped**; `PrismExplosion` only registers state, no per-effect loop).
+- **`MaterialPropertyAnimator`** — now uses `sharedMaterial` (audit rec #4 **resolved**; the
+  in-repo audit doc is stale on this).
+- **`PrismScaleManager` / `MaterialStateManager` / `AdaptiveAnimationManager` /
+  `PrismTimerManager` / `PrismColliderLodManager`** — Jobs+Burst batching, frame-skipping,
+  centralized timers, proximity collider culling.
+- **Fauna sensing** — goals come from the cached Burst `BlockDensityGrid`, consumption from
+  `PrismSpatialIndex.QuerySphere`; the only remaining fauna physics probe is a masked
+  vessel-only `OverlapSphereNonAlloc`.
+- **HUD event plumbing** — per-vessel HUD views are event-driven (no polling);
+  `ObjectiveIndicator` is the model UI widget (own sub-canvas, string caching, alpha-only pulse).
+- **Textbook anti-patterns are absent from hot paths**: zero `SendMessage`, no
+  `FindObjectOfType`/`Camera.main`/LINQ in any per-frame `Update` (verified sweep), projectiles
+  and prisms pooled.
+
+What remains splits into: **(A) two structural refactors with the largest headroom**,
+**(B) three bounded medium refactors that are live costs today**, **(C) a dead-code purge that
+removes latent perf traps**, and **(D) verified spot fixes**. And, just as important, **(E) a
+list of refuted findings** so nobody re-chases them.
+
+---
+
+## A. Structural refactors — largest headroom
+
+### A1. Prism GameObject architecture → incremental DOTS/ECS + instanced rendering *(largest ceiling, already roadmapped)*
+
+Still the single biggest headroom in the project, unchanged from
+`PRISM_PERFORMANCE_AUDIT.md` recs #2/#3/#7: each prism is a full GameObject with 5–6
```

</details>

### `e49924070` — perf: land verified Phase 1-2 fixes from the performance refactor review

_Claude, 2026-07-02 14:14:43 +0000_

```text
Spot fixes (all findings adversarially verified before implementation):
- VesselPrismController: O(1) Trail.GetBlockIndex instead of O(n) IndexOf
  per spawned prism (trail-laying was cumulatively O(n^2))
- Turn monitors: single end-of-turn driver (TurnMonitorController); the
  redundant per-monitor Update evaluated every check twice per frame;
  controller loop de-LINQed
- GameDataSO: allocation-free FindByName/FindByTeam/GetTotalVolume/
  GetControllingTeamStats (closure+enumerator per combat event, hundreds
  per AOE-burst frame)
- Cell: static readonly domain arrays (allocated per prism register/
  unregister and per phase tick); drops LINQ from the hostile-domain helper
- HexRaceScoreTracker: elapsed-time Score write throttled to 10 Hz (was
  dirtying n_Score every frame on the host)
- ResourceSystem: test-harness branches behind UNITY_EDITOR; cached gain
  tick wait
- Boid: cached behavior-tick WaitForSeconds (was one alloc per tick per boid)
- VesselController: change-checked NetworkVariable writes so the benchmark
  counts actual dirties instead of a hardcoded 3 per render frame
- LightFauna: wither path de-LINQed (in-place sort, one wait per death)

UI canvas isolation:
- DomainVolumeIndicator: dedicated sub-Canvas on the runtime-created hex
  gauge (ObjectiveIndicator pattern) — its ring sweep was re-batching the
  whole Menu_Main UI canvas ~42Hz; skip updates while CanvasGroup-hidden;
  quantize the spawn-cycle input to cap rebuild cadence

Material churn/leaks:
- Spindle: condense/evaporate now drives _DeathAnimation via the existing
  MaterialPropertyBlock — deletes the per-spindle material clone path (one
  clone + broken batch per spindle per grow-in/wither, cloned en masse on
  LifeForm death) and the mid-condense overwrite leak
- Crystal: fix getter double-clone leak in the domain-change lerp; fix
  per-collection explodingMaterial leak (clone only when Impact takes
  ownership, share otherwise)
- SpaceCrystalAnimator: skip blendshape writes while camera-culled;
  CrystalSpace.prefab UpdateWhenOffscreen off (nested in 12 living
  lifeform prefabs; was skinning off-screen every frame) — needs an
  in-editor culling sanity pass

Dead code removed (zero code refs + zero scene/prefab GUID refs, re-verified):
- CurrentScore (per-frame LINQ sort + unconditional TMP write, wired nowhere)
- CellControlTurnMonitor (per-frame LINQ sort, wired nowhere)
- BoidSimulationController (blocking per-frame GPU readback, wired nowhere)

Docs: execution-status ledger added to PERFORMANCE_REFACTOR_REVIEW.md.
```

```text
 Assets/_Prefabs/Environment/CrystalSpace.prefab                       |   2 +-
 Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs              |   9 ++
 Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs            |  11 +-
 .../_Scripts/Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs |  41 -----
 .../Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs.meta     |  11 --
 Assets/_Scripts/Controller/Arcade/TurnMonitors/TurnMonitor.cs         |  19 +--
 Assets/_Scripts/Controller/Environment/Cell.cs                        |  25 +--
 .../_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs  |  14 ++
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs          |   7 +-
 .../Controller/Environment/FloraAndFauna/BoidSimulationController.cs  | 267 --------------------------------
 .../Environment/FloraAndFauna/BoidSimulationController.cs.meta        |  11 --
 Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs    |  47 ++++--
 Assets/_Scripts/Controller/Environment/FloraAndFauna/Spindle.cs       |  46 +++---
 Assets/_Scripts/Controller/Environment/FlowField/Crystal.cs           |  28 +++-
 Assets/_Scripts/Controller/Vessel/ResourceSystem.cs                   |   7 +-
 Assets/_Scripts/Controller/Vessel/VesselController.cs                 |  25 ++-
 Assets/_Scripts/Controller/Vessel/VesselPrismController.cs            |   2 +-
 Assets/_Scripts/UI/CurrentScore.cs                                    |  40 -----
 Assets/_Scripts/UI/CurrentScore.cs.meta                               |  11 --
 Assets/_Scripts/UI/DomainVolumeIndicator.cs                           |  22 ++-
 Assets/_Scripts/Utility/DataContainers/GameDataSO.cs                  |  39 ++++-
 Docs/PERFORMANCE_REFACTOR_REVIEW.md                                   |  19 +++
 22 files changed, 231 insertions(+), 472 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 1082 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs b/Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs
index 9df11234f..34cccd240 100644
--- a/Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs
+++ b/Assets/_Scripts/Controller/Arcade/HexRaceScoreTracker.cs
@@ -23,7 +23,14 @@ namespace CosmicShore.Gameplay
 
         [Inject] UGSStatsManager ugsStatsManager;
 
+        // Score is display/replication state, not simulation state — writing it every
+        // frame dirties the n_Score NetworkVariable per frame on the host for a value
+        // the HUD only needs a few times a second. 10 Hz keeps the readout smooth;
+        // HandleGameEnd writes the authoritative final score unconditionally.
+        const float ScoreWriteInterval = 0.1f;
+
         private float _elapsedRaceTime;
+        private float _nextScoreWriteTime;
         private IVesselStatus _observedVessel;
         private VesselTelemetry _vesselTelemetry;
         private bool _isTracking;
@@ -78,6 +85,8 @@ namespace CosmicShore.Gameplay
         {
             if (!_isTracking || _observedVessel == null) return;
             _elapsedRaceTime += Time.deltaTime;
+            if (Time.time < _nextScoreWriteTime) return;
+            _nextScoreWriteTime = Time.time + ScoreWriteInterval;
             if (gameData.LocalRoundStats != null)
                 gameData.LocalRoundStats.Score = _elapsedRaceTime;
         }
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs
index dd8fbdeb2..197d65d1e 100644
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs
+++ b/Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs
@@ -1,5 +1,4 @@
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Utility;
 using Unity.Netcode;
 using UnityEngine;
@@ -70,7 +69,15 @@ namespace CosmicShore.Gameplay
             if (!_isRunning)
                 return;
 
-            if (!monitors.Any(m => m.CheckForEndOfTurn()))
+            var conditionMet = false;
+            for (int i = 0; i < monitors.Count; i++)
+            {
+                if (!monitors[i].CheckForEndOfTurn()) continue;
+                conditionMet = true;
+                break;
+            }
+
+            if (!conditionMet)
                 return;
 
             _isRunning = false;
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs
deleted file mode 100644
index 248eb8fb6..000000000
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitors/CellControlTurnMonitor.cs
+++ /dev/null
@@ -1,41 +0,0 @@
-using CosmicShore.Gameplay;
-using UnityEngine;
-using CosmicShore.UI;
-using CosmicShore.ScriptableObjects;
-using CosmicShore.Utility;
-
-namespace CosmicShore.Gameplay
-{
-    public class CellControlTurnMonitor : TurnMonitor
-    {
-        // [SerializeField] private Cell monitoredNode;
-        // private MiniGame game;
-        // Can't use Minigame, use the MiniGameData instead for knowing about playing players.
-        // private Teams playerTeam;
-
-        /*private void Start()
-        {
-            game = GetComponent<MiniGame>();    
-            if (game != null && game.LocalPlayer != null)
```

</details>

### `e1bd723f3` — perf: batch 2 — trail-walk allocs, flora Branch boxing, wall sweep cap, feed pooling, blend-utility leak

_Claude, 2026-07-02 14:27:24 +0000_

```text
- Trail: allocation-free LookAhead overload filling a caller-owned list; the
  allocating overload now delegates to it
- Skimmer: FindNextBlocks writes into scratch buffers; DrawCircle computes ONE
  shared prism list per circle (NudgeShard only reads it) instead of a
  GetComponent + two list allocations per segment (up to 360/circle)
- SkimmerAlignPrismEffectSO: per-trigger-enter look-ahead now reuses a static
  scratch list (was a fresh trail-walk list per skim impact); drops unused LINQ
- AssembledFlora/BranchingFlora: Branch implements IEquatable (HashSet/List ops
  were reflection-boxing the struct per comparison); HashSet -> List kills the
  ElementAt(i) accidental O(n^2) + enumerator alloc per grow iteration; per-Grow
  scratch lists reused. Ecology invariants untouched (same budget, frenzy gate,
  reseed logic; iteration order becomes insertion-ordered — growth already
  random-skips sites). Collider impact: none
- WallAssembler: cap FindClosestMate candidates at 32 (Gyroid caps at 10) — the
  1 Hz sweep iterated ALL prisms in a 40u radius and converted (resized +
  AddComponent) every candidate lacking an assembler; the cap bounds both the
  cost and the conversion blast radius
- GameEventFeed/GameFeedEntry: pool rows (recycle to an inactive pool root via
  Release/ReturnToPool) instead of Instantiate/Destroy per event; overflow and
  ClearFeed recycle too
- MaterialBlendUtility: track the per-renderer overlay instance — the old path
  appended one fresh material instance to the renderer's array per BeginBlend
  and never destroyed any (leak + array growth on pooled danger prisms); shared
  MaterialPropertyBlock; cached shader IDs; ResetBlend retires the overlay
- SkimmerOverchargeCollectPrismEffectSO: cache the TrailBlocks layer mask (was
  re-hashed per recursion step); replace Where/OrderBy with an in-place sort

Docs: execution ledger updated in PERFORMANCE_REFACTOR_REVIEW.md.
```

```text
 Assets/_Scripts/Controller/Assemblers/WallAssembler.cs                |   8 ++-
 .../_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs   |  39 ++++++++---
 .../_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs   |  27 ++++++--
 .../EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs           | 116 +++++++++++++++++++++++---------
 .../EffectsSO/Skimmer Prism Effects/SkimmerAlignPrismEffectSO.cs      |  34 +++++-----
 .../Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs    |  20 ++++--
 Assets/_Scripts/Controller/Vessel/Skimmer.cs                          |  39 +++++++----
 Assets/_Scripts/Controller/Vessel/Trail.cs                            |  20 ++++--
 Assets/_Scripts/UI/GameEventFeed/GameEventFeed.cs                     |  67 +++++++++++++++---
 Assets/_Scripts/UI/GameEventFeed/GameFeedEntry.cs                     |  45 ++++++++++---
 Docs/PERFORMANCE_REFACTOR_REVIEW.md                                   |  15 ++++-
 11 files changed, 321 insertions(+), 109 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 557 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Assemblers/WallAssembler.cs b/Assets/_Scripts/Controller/Assemblers/WallAssembler.cs
index 5bd606896..2b5209d2a 100644
--- a/Assets/_Scripts/Controller/Assemblers/WallAssembler.cs
+++ b/Assets/_Scripts/Controller/Assemblers/WallAssembler.cs
@@ -62,6 +62,7 @@ namespace CosmicShore.Gameplay
         float separationDistance = 2f;
         [SerializeField] int colliderTheshold = 25;
         [SerializeField] float radius = 40f;
+        const int MaxMateCandidates = 32;
         bool isStopped = true;
 
         // Mate candidates come from PrismSpatialIndex.QuerySphere; this scratch is
@@ -401,7 +402,12 @@ namespace CosmicShore.Gameplay
                 ? spatialIndex.QuerySphere(bondSite, radius, s_mateScratch)
                 : 0;
             if (prismCount < colliderTheshold) return new BondMate { Mate = null };
-            for (int i = 0; i < prismCount; i++)
+            // Bound the per-search work AND the conversion blast radius: dense trail can
+            // put hundreds of prisms in a 40u radius, and every candidate lacking a
+            // WallAssembler gets converted (resized + AddComponent) below — per 1 Hz
+            // search, per chain front. Gyroid caps candidates at 10 for the same reason.
+            int candidateCount = Mathf.Min(prismCount, MaxMateCandidates);
+            for (int i = 0; i < candidateCount; i++)
             {
                 var trailBlock = s_mateScratch[i];
                 if (trailBlock == null || trailBlock.destroyed) continue;
diff --git a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
index e48830a9c..944b1a682 100644
--- a/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs
@@ -1,6 +1,5 @@
 
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Data;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
@@ -23,7 +22,7 @@ namespace CosmicShore.Gameplay
     /// </summary>
     public class AssembledFlora : Flora
     {
-        struct Branch
+        struct Branch : System.IEquatable<Branch>
         {
             public GameObject gameObject;
             public int depth;
@@ -35,6 +34,16 @@ namespace CosmicShore.Gameplay
                 depth = 0;
                 assembler = healthPrism.GetComponent<Assembler>();
             }
+
+            // Without IEquatable, every List/HashSet operation on this struct fell back
+            // to reflection-based ValueType.Equals, boxing the struct (and its two
+            // managed refs) per comparison — in the per-growPeriod Grow loop.
+            public bool Equals(Branch other) =>
+                gameObject == other.gameObject && depth == other.depth && assembler == other.assembler;
+
+            public override bool Equals(object obj) => obj is Branch other && Equals(other);
+
+            public override int GetHashCode() => gameObject ? gameObject.GetInstanceID() : 0;
         }
         
         /// <summary>
@@ -53,7 +62,13 @@ namespace CosmicShore.Gameplay
                  "lets a grazed flora 'reawaken' instead of sitting as a dead fragment.")]
         [SerializeField] int reseedBranchCount = 3;
 
-        HashSet<Branch> activeBranches = new HashSet<Branch>();
+        // List, not HashSet: the Grow loop needs indexed access (the old
+        // HashSet.ElementAt(i) re-enumerated i elements per iteration — an accidental
+        // O(n²) with a LINQ enumerator alloc each step). Entries are unique by
+        // construction (each wraps a freshly instantiated prism / distinct survivor).
+        readonly List<Branch> activeBranches = new List<Branch>();
+        readonly List<Branch> newBranchesScratch = new List<Branch>();
+        readonly List<Branch> branchesToRemoveScratch = new List<Branch>();
 
         Assembler assembler;
 
@@ -117,14 +132,16 @@ namespace CosmicShore.Gameplay
                 return;
```

</details>

### `5e26bb478` — perf: batch 3 — delete redundant AOE per-block growers, fix pooled growthRate sync, cheapen projectile launch

_Claude, 2026-07-02 14:34:12 +0000_

```text
- Prism.Initialize: re-sync scaleAnimator.GrowthRate from the growthRate field.
  Awake copied it once at pool creation, so spawn paths assigning
  prism.growthRate on pooled reuse (both AOE block spawners) were silently
  ignored — the root cause the 'fallback growers' below were papering over
- AOERadialBlocks: remove the per-block GrowToScale UniTask (72 frame-yielding
  tasks per SkyBurst detonation) — the built-in Burst growth path
  (PrismScaleAnimator -> PrismScaleManager) already animates to TargetScale;
  the fallback was a second writer fighting it on the same transform
- AOEDangerHemisphereBlocks: same — replace the per-block grow loop (144 tasks
  per detonation at the wired config) with a zero-scale + prism.ChangeSize()
  restart of the built-in grower; flag/material application unchanged
- Projectile: plain per-launch CancellationTokenSource with OnDestroy -> Stop()
  instead of a linked destroy-token pair (2 allocs + registration per shot;
  spherical volleys allocate dozens at once)

AOEExplosion instance pooling deliberately deferred (documented in the review
ledger): subclass reset semantics and the cancelled-frozen-explosion state
need in-editor verification.
```

```text
 Assets/_Scripts/Controller/Projectiles/AOEDangerHemisphereBlocks.cs | 21 ++++++---------------
 Assets/_Scripts/Controller/Projectiles/AOERadialBlocks.cs           | 29 +++++------------------------
 Assets/_Scripts/Controller/Projectiles/Projectile.cs                | 15 ++++++++++-----
 Assets/_Scripts/Controller/Vessel/Prism.cs                          |  4 ++++
 Docs/PERFORMANCE_REFACTOR_REVIEW.md                                 | 19 ++++++++++++++++---
 5 files changed, 41 insertions(+), 47 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 170 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Projectiles/AOEDangerHemisphereBlocks.cs b/Assets/_Scripts/Controller/Projectiles/AOEDangerHemisphereBlocks.cs
index 1a9b26513..76465dc62 100644
--- a/Assets/_Scripts/Controller/Projectiles/AOEDangerHemisphereBlocks.cs
+++ b/Assets/_Scripts/Controller/Projectiles/AOEDangerHemisphereBlocks.cs
@@ -251,22 +251,13 @@ namespace CosmicShore.Gameplay
             var tr = prism.transform;
             if (!tr) return;
 
+            // Restart the prism's built-in growth from zero (PrismScaleAnimator →
+            // Burst-batched PrismScaleManager) instead of driving a per-block
+            // frame-yielding grow loop here — the wired config spawns 144 blocks
+            // per detonation, i.e. 144 concurrent tasks fighting the built-in
+            // grower for the same transforms.
             tr.localScale = Vector3.zero;
-            await GrowToScale(tr, targetScale, growthRate);
-        }
-
-        private static async UniTask GrowToScale(Transform tr, Vector3 target, float rate)
-        {
-            rate = Mathf.Max(1e-5f, rate);
-
-            while (tr && (tr.localScale - target).sqrMagnitude > 0.0001f)
-            {
-                tr.localScale = Vector3.MoveTowards(tr.localScale, target, rate);
-                await UniTask.Yield();
-            }
-
-            if (tr)
-                tr.localScale = target;
+            prism.ChangeSize();
         }
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Controller/Projectiles/AOERadialBlocks.cs b/Assets/_Scripts/Controller/Projectiles/AOERadialBlocks.cs
index d1172dd5e..6018f7f47 100644
--- a/Assets/_Scripts/Controller/Projectiles/AOERadialBlocks.cs
+++ b/Assets/_Scripts/Controller/Projectiles/AOERadialBlocks.cs
@@ -7,7 +7,6 @@ using Cysharp.Threading.Tasks;
 using UnityEngine;
 using CosmicShore.ScriptableObjects;
 using Random = UnityEngine.Random;
-using System.Linq;
 
 namespace CosmicShore.Gameplay
 {
@@ -174,16 +173,15 @@ namespace CosmicShore.Gameplay
             if (shielded)
                 prism.prismProperties.IsShielded = true;
 
-            // Start at zero scale
+            // Start at zero scale — Initialize's built-in growth (PrismScaleAnimator →
+            // Burst-batched PrismScaleManager) animates to TargetScale at growthRate.
+            // The old per-block "fallback grower" UniTask fought that path (two writers
+            // on the same transform) and cost one frame-yielding task per block —
+            // 72 per SkyBurst detonation.
             prism.transform.localScale = Vector3.zero;
-
-            // built-in growth (if Prism supports it)
             prism.TargetScale = targetScale;
             prism.growthRate  = growthRate;
 
-            // fallback grower in case Prism doesn't auto grow
-            GrowToScale(prism.transform, targetScale, growthRate).Forget();
-
             prism.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");
 
             prism.Trail = trail;
@@ -192,22 +190,5 @@ namespace CosmicShore.Gameplay
             return prism;
         }
 
-        // ----------------------------------------------------------------------
-        // Block growth without coroutine
-        // ----------------------------------------------------------------------
-
-        private static async UniTaskVoid GrowToScale(Transform tr, Vector3 target, float rate)
-        {
-            rate = Mathf.Max(1e-5f, rate);
-
```

</details>

### `31aa23ee6` — fix: address review findings — feed pool-root eviction, culled blendshape reset, blend append gap

_Claude, 2026-07-02 14:53:42 +0000_

```text
Independent review of the three perf commits surfaced two live bugs and one
latent gap; all fixed:

- GameEventFeed: eviction/clear are now driven by an explicit _liveEntries
  list instead of contentContainer.childCount. In both shipped GameCanvas
  prefabs the wired contentContainer IS the feed's own transform, so the
  pool root sat inside the container: it consumed an eviction slot and,
  once enough entries aged out, GetChild(0) hit the pool root and the loop
  destroyed the entire pool while the Stack still referenced it — a
  guaranteed MissingReferenceException mid-match
- SpaceCrystalAnimator: zero the outgoing blendshape key before the
  off-screen cycle flip so a crystal doesn't re-enter view with both
  blendshapes partially applied
- MaterialBlendUtility: an add-mode blend following a replace-mode blend on
  the same renderer skipped the overlay append (else-if gap, currently
  unreachable with shipped configs but latent); append is now decided
  independently of how the previous overlay was retired
```

```text
 .../_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs  |  4 +++
 .../EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs           |  8 +++++-
 Assets/_Scripts/UI/GameEventFeed/GameEventFeed.cs                     | 48 +++++++++++++--------------------
 3 files changed, 30 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 106 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs b/Assets/_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs
index 69fdc9d67..fef1726cf 100644
--- a/Assets/_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs
+++ b/Assets/_Scripts/Controller/Environment/Crystals/SpaceCrystalAnimator.cs
@@ -43,6 +43,10 @@ namespace CosmicShore.Gameplay
             {
                 if (timer > 1.1f)
                 {
+                    // Zero the outgoing key before flipping (a value write, not
+                    // skinning) so the crystal doesn't re-enter view with both
+                    // blendshapes partially applied.
+                    crystalRenderer.SetBlendShapeWeight(currentShapeKey, 0f);
                     currentShapeKey = (currentShapeKey + 1) % 2;
                     timer = 0f;
                 }
diff --git a/Assets/_Scripts/UI/GameEventFeed/GameEventFeed.cs b/Assets/_Scripts/UI/GameEventFeed/GameEventFeed.cs
index e263d51f7..e2f2ac463 100644
--- a/Assets/_Scripts/UI/GameEventFeed/GameEventFeed.cs
+++ b/Assets/_Scripts/UI/GameEventFeed/GameEventFeed.cs
@@ -25,9 +25,13 @@ namespace CosmicShore.UI
         [Inject] private GameDataSO gameData;
 
         // Rows recycle instead of Instantiate/Destroy per event — feed events arrive
-        // in bursts (joust hits) and each row carries a TMP object. Pooled rows live
-        // under an inactive pool root so they never count toward childCount or layout.
+        // in bursts (joust hits) and each row carries a TMP object. Live rows are
+        // tracked explicitly in _liveEntries (NOT via contentContainer.childCount:
+        // in the shipped prefabs contentContainer IS the feed's own transform, so
+        // hierarchy-based counting would see the pool root as an evictable child).
+        // Pooled rows sit under an inactive pool root, invisible to layout.
         private readonly Stack<GameFeedEntry> _entryPool = new();
+        private readonly List<GameFeedEntry> _liveEntries = new();
         private Transform _poolRoot;
 
         private Transform PoolRoot
@@ -48,6 +52,7 @@ namespace CosmicShore.UI
         private void HandleEntryReturn(GameFeedEntry entry)
         {
             if (entry == null) return;
+            _liveEntries.Remove(entry); // no-op when eviction already removed it
             entry.transform.SetParent(PoolRoot, false);
             _entryPool.Push(entry);
         }
@@ -207,21 +212,13 @@ namespace CosmicShore.UI
             if (contentContainer == null || settings == null)
                 return;
 
-            // Enforce max visible entries — recycle oldest. Release() reparents to the
-            // pool root immediately, so childCount decreases right away (no infinite
-            // loop when multiple entries spawn in the same frame).
-            while (contentContainer.childCount >= settings.maxVisibleEntries)
+            // Enforce max visible entries — recycle oldest live row. Driven by the
+            // explicit live list, never by childCount (see _liveEntries note above).
+            while (_liveEntries.Count >= settings.maxVisibleEntries)
             {
-                var oldest = contentContainer.GetChild(0);
-                if (oldest.TryGetComponent(out GameFeedEntry oldEntry))
-                {
-                    oldEntry.Release();
-                }
-                else
-                {
-                    oldest.SetParent(null);
-                    Destroy(oldest.gameObject);
-                }
+                var oldest = _liveEntries[0];
+                _liveEntries.RemoveAt(0);
+                if (oldest != null) oldest.Release();
             }
 
             GameFeedEntry entry;
@@ -253,6 +250,7 @@ namespace CosmicShore.UI
             }
 
             entry.Setup(message, color, isRichText);
+            _liveEntries.Add(entry);
 
             // Rebuild layout BEFORE animation so entry gets correct Y from VerticalLayoutGroup
             LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer);
@@ -262,21 +260,13 @@ namespace CosmicShore.UI
```

</details>

### `f0f74864c` — docs(perf): audit all unmerged performance branches + sequenced merge plan

_Claude, 2026-07-02 15:45:29 +0000_

```text
Audited 20 unmerged branches carrying performance work against current
bleeding-edge (ancestry checks, merge-tree dry-runs, content greps; shallow-
clone artifacts corrected). Verdicts: 6 merge (zen-volta instanced prisms
gated on its own in-editor protocol + shipping-default decision; this branch
after it with a documented Spindle.cs union recipe; planck super-shield
cherry-pick verified conflict-free; feynman ledger; tender-pasteur benchmark
schema v2; maxwell's fauna net-sync doc), 9 extract-then-delete (headlined by
PrismActivationQueue — the per-prism spawn coroutine herd is still live on
bleeding-edge — and the flora pooling core, plus the never-merged curated
no-brainer port b7f2f8814), 8 delete outright (fully-merged ancestors,
superseded work, and two branches whose merge would REGRESS current fixes:
beautiful-dirac re-adds a removed AudioListener, loving-fermi carries the
reverted mass-conservation trail cap).

Plan is sequenced in five waves with per-wave benchmark gates and branch
hygiene; cross-checked against the fixes already landed on this branch.
```

```text
 Docs/PERF_BRANCH_MERGE_PLAN.md | 151 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 151 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 157 lines)</summary>

```diff
diff --git a/Docs/PERF_BRANCH_MERGE_PLAN.md b/Docs/PERF_BRANCH_MERGE_PLAN.md
new file mode 100644
index 000000000..b3fa2ee08
--- /dev/null
+++ b/Docs/PERF_BRANCH_MERGE_PLAN.md
@@ -0,0 +1,151 @@
+# Performance Branch Merge Plan — Path to the Most Performant Mergeable State (July 2026)
+
+**What this is.** An audit of every unmerged branch carrying performance work (20 branches
+across 273 remote refs), each verified against current `bleeding-edge` (`c833c580`) with
+ancestry checks, `git merge-tree` dry-runs, and content greps — plus the sequenced merge
+plan that lands the most performant state of the game. Companion docs:
+`Docs/PERFORMANCE_REFACTOR_REVIEW.md` (July 2026 verified review, this branch) and
+`Docs/PERFORMANCE.md` (living ledger, arrives with the `beautiful-feynman` merge below).
+
+**Method notes.** The repo clone is shallow — several "+5,000 commits ahead" branches are
+truncated-history artifacts, not real divergence; every verdict below was re-established
+with `merge-base --is-ancestor` / `git cherry` / content comparison after deepening.
+Two branches that *look* attractive would actively regress the game if merged
+(`beautiful-dirac` re-adds an AudioListener removed by the June 29 spatialization fix;
+`loving-fermi` contains the reverted mass-conservation-violating trail cap) — merged-state
+verdicts below are authoritative over branch names.
+
+---
+
+## The one-paragraph answer
+
+The most performant mergeable state is: **current `bleeding-edge` + `zen-volta`
+(instanced prism rendering — the single largest win, code-complete, zero file conflicts
+with bleeding-edge, gated only on its own in-editor verification protocol and a shipping-
+default decision) + this branch (`performance-refactoring-review-rciwzl`, rebased after
+zen-volta with one `Spindle.cs` reconciliation) + four small clean merges (super-shield
+mesh cut, benchmark schema v2, the performance ledger, the fauna net-sync design doc) +
+a curated extraction sweep re-porting the still-valuable ideas from nine older branches
+(headlined by `PrismActivationQueue` and the flora pooling core), with fourteen
+superseded/dangerous branch refs deleted.** Everything below is the evidence and order.
+
+---
+
+## 1. Verdict table — all audited branches
+
+| Branch | Last work | Verdict | Why (evidence) |
+|---|---|---|---|
+| `claude/zen-volta-t9su0i` | **2026-07-02** | **MERGE (gated)** | Instanced prism rendering (Entities Graphics companion entities, Checkpoints A–C), instanced octahedron shields + shared-mesh cache, VFX apply ~97ms→bounded(256), ~23ms volume walk→O(growing), O(N) collider-LOD sweep→O(transitions), spindle SRP-batching, diagnostics+stress tooling. Zero changed-file overlap with bleeding-edge since its Jun-29 merge-base. Gates: its own doc's 10-step in-editor verification; decide the shipping default (code defaults OFF, committed `Resources/PrismRenderConfig.asset` ships **ON**); Android GLES3 vs Vulkan min-spec decision; fix stale `PrismRenderConfigSO` docstring. PR #573 stacks it into `confident-clarke`. |
+| `claude/confident-clarke-f99p6r` | 2026-06-30 | **MERGE (as zen-volta's base)** | The prism-perf integration branch (kind-turing + Checkpoints A–C). zen-volta continues it; land via PR #573 chain. |
+| `claude/performance-refactoring-review-rciwzl` | 2026-07-02 | **MERGE (after zen-volta)** | This branch. 25 verified fixes + review docs. Conflicts with zen-volta: `Spindle.cs` (see §3 recipe) and `BoidSimulationController.cs` (our deletion wins — it also satisfies zen-volta's rename motive). `Cell.cs`/`Prism.cs` auto-merge. |
+| `claude/optimistic-planck-3f1ztv` | 2026-06-08 | **MERGE (cherry-pick `81746e28b`)** | Super-shield mesh 24→8 faces (3× tri cut) + 173-line topology/containment tests. Cherry-pick dry-run: zero conflicts. Complementary to zen-volta (touches only `Stellated*` files; zen-volta's shield work touches only regular octahedron files). Compounds if super-shields later ride the shared-mesh path. |
+| `claude/beautiful-feynman-gnchcw` | 2026-06-19 | **MERGE** | `Docs/PERFORMANCE.md` (281-line ledger) + stale-audit-doc corrections. `merge-tree` clean vs bleeding-edge. One doc-hunk conflict vs this branch: resolve by taking feynman's rewritten Progress-Update section + this branch's more precise Rec-7 wording (collider-LOD ≠ full collider replacement), cross-link both docs, refresh the ledger's §5 for the July batches. |
+| `claude/tender-pasteur-mxw3t4` | 2026-06-11 | **MERGE** | Benchmark schema v2 (CPU thread breakdown + physics time in captures). Bleeding-edge is still schema v1; the benchmark directory is untouched upstream since divergence — zero conflicts. |
+| `claude/optimistic-maxwell-uet05g` | 2026-06-14 | **EXTRACT `eb146ee63`, delete** | Sole unique content: 415-line `Docs/ECOSYSTEM_NETWORK_SYNC.md` (fauna network-sync plan for the lava lamp — the still-open ECOSYSTEM.md item 4). Cherry-picks near-clean; refresh its §1 "current state" (predates the June ecology rework) before acting on it. |
+| `claude/audit-flora-pooling-f5vmD` | 2026-06-12 | **REBASE-PORT (after zen-volta + this branch)** | Unlanded value: `SpindlePoolManager`, `HealthPrismPoolManager` (+prefab), pooled `LifeForm`/`Flora` paths, batched `SpindleAnimDriver` (48/64 caps, off-screen skip), `PrismActivationQueue`, strict prism pooling. None on bleeding-edge (grep-verified). Its Spindle rewrite supersedes this branch's Spindle fix but conflicts with zen-volta's shared-material batching — port the pooling core and re-shape the anim driver to zen-volta's model (§3). ~10-file conflict surface, no docs. Ecology protocol applies (pooled spindles must still wither — continuity law). |
+| `claude/review-optimization-branches-WDr9T` | 2026-05-05 | **EXTRACT `b7f2f8814`, delete** | The prior curated "no-brainer port" was never merged. Of its 7 fixes: 2 landed independently (ClearPrisms MPB, MaterialStateManager snapshot), 2 are covered by this branch (TurnMonitorController LINQ, CurrentScore deletion), **3 still open**: `GenericPoolManager` sync-prewarm cap (scene-load hitch), `GunTransformer` per-Update `GetComponentsInChildren`, `AIPilot` cached `WaitForSeconds` (5 sites). Hand-apply — paths moved. |
+| `claude/add-prism-activation-queue-CEoJM` | 2026-03-06 | **EXTRACT (top single idea), delete** | Thundering-herd fix: bleeding-edge `Prism.cs:175/235` still starts one coroutine + one `WaitForSeconds(0.6)` **per prism** — the branch profiled 49,856 coroutines resuming in one frame (1.9s stall, 10.1MB GC) on mass spawn. Re-port carefully (destroyed-guard, spatial-index registration ordering, `_lodCulled` interplay); prefer folding into the `PrismTimerManager` centralized-timer pattern. ~1 day incl. benchmark proof. |
+| `claude/add-spawnable-caching-wg2zr` | 2026-03-04 | **EXTRACT `0b4b69ec8`, delete** | The caching system landed; the **cache-key bug fix didn't** — `SpawnableBatman/Cord/Helix` hashes omit `intensityLevel`/`domain`, so intensity changes can serve stale trail data today. Fold the three fields into the base key, strip subclass hashes. |
+| `claude/optimize-mobile-performance-7uCEG` | 2026-03-09 | **EXTRACT residuals, delete** | Beyond the WDr9T subset, still open: `Projectile` material clones → MPB, `ParametricJetEffect` per-call `new Gradient()`, `Prism` per-init `LayerMask.NameToLayer(string)`. |
+| `claude/optimize-menu-performance-5EODy` | 2026-03-08 | **EXTRACT ideas, delete** | 4 of 5 still open on bleeding-edge: DailyChallengeModal 1Hz throttle, QuestTrackView RectTransform caching + idle gate, InfiniteScroll `ForceUpdateCanvases`→targeted rebuild, HangarScreen card reuse. Files rewritten since — re-implement, don't port. (First two were independently re-found by this branch's review, Tier-3.) |
+| `claude/benchmark-mobile-performance-SYydw` | 2026-03-09 | **EXTRACT 2 items, delete** | Still open: `ShapeDrawingManager` LineRenderer material leak (`lr.material = new Material(shader)`, no cleanup — trivial); mobile HyperSea skybox SubShader (re-implement against the reworked shader, gate via `GraphicsSettingsApplier` quality tiers). Rest superseded/moot. |
+| `claude/optimize-scene-load-times-5sopf` | 2026-03-05 | **EXTRACT 1 item, delete** | `PrismTimerManager` `_disposing` early-out + swap-remove (mass-teardown O(1)) still missing. Scene-load piece superseded by `SceneLoader`/`SceneTransitionManager`; `[ScenePerf]` markers superseded by the benchmark suite; ActivationQueue via CEoJM instead. |
+| `claude/optimize-pool-manager-6tOOr` | 2026-03-07 | **EXTRACT via WDr9T, delete** | Sync-prewarm cap — included in `b7f2f8814`. The stale-refs half landed independently (`ReleaseAllActive`). |
+| `claude/optimize-shield-effect-CgpSK` | 2026-04-15 | **EXTRACT concept, delete** | The shockwave-ring visual lost to the shipped octahedron shield language — do not port. **Still live**: per-prism shield **SFX stacking** (`PrismStateManager.cs:129/150` plays one SFX per prism per AOE wave) — extract the per-wave-origin audio coalescing concept only. |
+| `claude/add-mobile-performance-manager-IUiaA` | 2026-03-07 | **DELETE (superseded)** | `MobilePerformanceManager` merged in March and was later retired; `GraphicsSettingsApplier` is now the documented single writer of engine graphics state; its benchmark suite lost to `Utility/PerformanceBenchmark/`. Reviving it would violate the single-writer settings design. |
+| `claude/ecs-migration-guide-Db42i` | 2026-06-12 | **DELETE (rejected by successor)** | zen-volta's `Docs/PRISM_ECS_MIGRATION.md` §2 reviews it by name: stale by two generations (PrismAOERegistry→PrismSpatialIndex, VContainer→Reflex), Phase-0 double-books every prism (perf regression), defers rendering (the actual cost). Its salvageable ideas are already folded into the successor doc. |
+| `claude/kind-turing-3lh823` | 2026-06-18 | **DELETE after zen-volta merges** | `git log kind-turing --not zen-volta` = empty — fully contained. |
+| `claude/density-partitioning-sync-6OXTO` | 2026-05-09 | **DELETE (superseded + rejected)** | Every durable idea shipped in stronger form (add-time domain snapshot via the audit branch, kernel smoothing via Phase 1/2, Blue anyGrid); its `DensityPartitionSystem` singleton is rejected *by name* in `DENSITY_PARTITIONING_AUDIT.md` §4.4; its `Cell.cs` integration points were rewritten by Phase 3. |
+| `claude/audit-density-partitioning-2EvgR` | 2026-06-03 | **DELETE (fully merged)** | Branch head is an ancestor of bleeding-edge; its HANDOFF doc lives on (updated) at `Docs/DENSITY_PARTITIONING_HANDOFF.md`. |
+| `claude/beautiful-bohr-wspnmf` | 2026-06-12 | **DELETE (fully merged)** | Ancestor of bleeding-edge (merged via `cb506b88e`). |
+| `claude/sweet-pascal-N3rny` | 2026-05-28 | **DELETE (fully merged)** | Ancestor of bleeding-edge. |
+| `claude/loving-fermi-DVWsY` | 2026-06-02 | **DELETE (fully merged; do NOT cherry-pick from it)** | Ancestor of bleeding-edge — and it contains the trail ring-buffer cap (`64d8f0c8`) that was deliberately reverted (mass conservation). |
+| `claude/beautiful-dirac-K5720` | 2026-05-26 | **DELETE (superseded; merging would REGRESS)** | Its 3 commits landed via cherry-picks (`0c4e977c6`, `257927aa2`) with later refinements; its `CameraManager.prefab` still carries the AudioListener that the June-29 spatialization fix (`686935ccd`) removed. |
+
+---
+
+## 2. The sequenced path forward
+
+Measure every wave with the PerformanceBenchmark suite (baseline → change → Compare —
+the ledger's methodology). One wave per PR; don't batch unrelated waves.
+
+### Wave 0 — clean merges, no editor gate (can land today)
+1. **Cherry-pick `81746e28b`** (planck: super-shield 8 faces + tests) onto bleeding-edge.
+2. **Merge `beautiful-feynman`** (PERFORMANCE.md ledger) — clean.
```

</details>

### `8c83175ac` — perf(prism): render super-shield as 8 tetra faces instead of 24

_Claude, 2026-07-02 15:51:08 +0000_

```text
The stellated-octahedron super-shield is the union of two regular
tetrahedra. Each big tetra face is coplanar with the 3 spike lateral
faces tiling its corners, so rendering the 8 tetra faces as opaque
geometry is pixel-identical to the 24-spike-face stella octangula — the
protruding dual tetra occludes each face's center via the depth buffer.

Drops triangle/vertex count 3x (24->8 tris, 72->24 verts) for the static
shield mesh and both morph meshes (engage bloom, disengage shatter),
matching the two-tetrahedra model the collision test (ContainsPointLocal)
already uses. The convex MeshCollider (hull = bounding cube) and the
volume/mass math are unchanged. Material is opaque with ZWrite, so the
depth-buffer occlusion the trick relies on holds.

Adds StellatedOctahedronMeshGeneratorTests locking the 8-triangle
topology, outward unit normals, cube-corner vertices, and the
two-tetrahedra containment (core/tips inside, valleys/beyond-tip outside).
```

```text
 Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs   |  15 ++-
 .../_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs  | 173 ++++++++++++++++++++++++++++++++
 .../Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs.meta      |   2 +
 Assets/_Scripts/Utility/StellatedOctahedronMeshGenerator.cs           | 115 +++++++++++++--------
 4 files changed, 258 insertions(+), 47 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 399 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs b/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
index 8272923aa..6186d1a8f 100644
--- a/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
+++ b/Assets/_Scripts/Controller/Vessel/PrismStellatedOctahedronShield.cs
@@ -19,10 +19,15 @@ namespace CosmicShore.Gameplay
     ///                      mass = ρ · 108·a·b·c (exactly 13.5× box mass by default,
     ///                      3× the inscribed octahedron shield's mass)
     ///
-    /// Engage: per-face bloom morph — 24 outer faces grow outward from their
+    /// The stellation is rendered as just its 8 tetrahedral faces (the two
+    /// constituent tetrahedra), not 24 spike faces — see
+    /// <see cref="StellatedOctahedronMeshGenerator"/>. Opaque rendering makes the
+    /// 8 triangles pixel-identical to the full stella octangula at ⅓ the cost.
+    ///
+    /// Engage: per-face bloom morph — the 8 tetra faces grow outward from their
     /// centroids.
     /// Disengage: box mesh snaps back immediately, then a shatter overlay plays
-    ///   where each of the 24 faces simultaneously shrinks and flies outward
+    ///   where each of the 8 faces simultaneously shrinks and flies outward
     ///   along its face normal, mirroring the prism destruction VFX.
     ///
     /// Fast overlap test: <see cref="IsPointInsideShield"/> uses the
@@ -224,7 +229,7 @@ namespace CosmicShore.Gameplay
             else Engage();
         }
 
-        /// <summary>Engage the super-shield with per-face bloom across all 24 faces.</summary>
+        /// <summary>Engage the super-shield with per-face bloom across all 8 tetra faces.</summary>
         public void Engage(bool instant = false)
         {
             if (_isShielded && !_isEngaging) return;
@@ -250,7 +255,7 @@ namespace CosmicShore.Gameplay
 
         /// <summary>
         /// Disengage the super-shield. Box mesh snaps back immediately; a
-        /// shatter overlay plays where each of the 24 faces flies outward
+        /// shatter overlay plays where each of the 8 tetra faces flies outward
         /// along its normal while shrinking to a point.
         /// </summary>
         public void Disengage(bool instant = false)
@@ -332,7 +337,7 @@ namespace CosmicShore.Gameplay
         // --- Mesh updates ----------------------------------------------------
 
         /// <summary>
-        /// Per-face bloom for engage: all 24 faces grow from centroid points to full size.
+        /// Per-face bloom for engage: all 8 tetra faces grow from centroid points to full size.
         /// </summary>
         private void UpdateEngageMesh(float faceScale)
         {
diff --git a/Assets/_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs b/Assets/_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs
new file mode 100644
index 000000000..2d1a901bd
--- /dev/null
+++ b/Assets/_Scripts/Tests/EditMode/StellatedOctahedronMeshGeneratorTests.cs
@@ -0,0 +1,173 @@
+#if UNITY_EDITOR
+using System.Collections.Generic;
+using NUnit.Framework;
+using UnityEngine;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Tests
+{
+    /// <summary>
+    /// StellatedOctahedronMeshGenerator Tests — locks the two-tetrahedra model
+    /// of the super-shield (Stella Octangula).
+    ///
+    /// WHY THIS MATTERS:
+    /// The super-shielded prism is modeled as the union of two regular tetrahedra
+    /// — 8 half-space planes for collision (ContainsPointLocal) and, since each
+    /// big tetra face is coplanar with the spike faces it tiles, 8 opaque
+    /// triangles for rendering instead of 24. These tests guard both invariants:
+    ///   • the rendered mesh is exactly 8 triangles whose vertices are the 8
+    ///     cube corners (spike tips), with outward-pointing flat-shaded normals;
+    ///   • the containment test matches the stellation surface (octahedron core
+    ///     and spike tips inside; concave valleys and beyond-tip points outside).
+    /// If either drifts, the shield's silhouette or its overlap queries break.
+    /// </summary>
+    [TestFixture]
+    public class StellatedOctahedronMeshGeneratorTests
```

</details>

### `82392b6c6` — feat(benchmark): schema v2 — CPU thread breakdown + physics time in captures

_Claude, 2026-07-02 15:51:08 +0000_

```text
FrameSnapshot gains cpuMainThread/cpuPresentWait/cpuRenderThread (from the
FrameTiming already fetched per frame) and physicsTimeMs (Physics.Processing
marker recorder). Statistics aggregate them plus busy-CPU (wait-for-present
removed) and physicsSharePercent; boundVerdict and the CpuBound/GpuBound hint
rules now compare GPU against busy CPU so frame-capped captures aren't
misread as CPU-bound (pre-v2 reports behave identically — busy falls back to
the raw total). Compare tab gains Busy CPU / Avg GPU / Physics Share deltas;
the History tab's copy-as-text export includes the thread/physics/netcode
breakdown. Schema version bumped 1 → 2; docs + stats tests updated.
```

```text
 .../_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md   |  8 +++--
 Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md        | 10 +++++-
 Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs     | 14 +++++----
 Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs   |  5 +++
 Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkReport.cs       |  4 ++-
 Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkStatistics.cs   | 41 ++++++++++++++++++++++++
 .../Utility/PerformanceBenchmark/Editor/PerformanceBenchmarkWindow.cs |  5 ++-
 Assets/_Scripts/Utility/PerformanceBenchmark/FrameSnapshot.cs         |  8 +++++
 .../Utility/PerformanceBenchmark/PerformanceBenchmarkRunner.cs        | 12 +++++++
 .../PerformanceBenchmark/Tests/Editor/BenchmarkStatisticsTests.cs     | 56 +++++++++++++++++++++++++++++++++
 10 files changed, 151 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 352 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md
index 429141de5..dffeae5da 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_ARCHITECTURE.md
@@ -194,11 +194,13 @@ source guard**: warns when origin/platform differ (only same-source deltas are v
 ## 5. Shared data model
 
 ```
-FrameSnapshot   (one per frame: ms, cpu, gpu, draws, setpass, batches, tris, verts, gcAlloc,
-                 mem, rigidbodies, + game-load counts, + netcode counters)
+FrameSnapshot   (one per frame: ms, cpu, gpu, cpu main/wait/render thread (v2), draws,
+                 setpass, batches, tris, verts, gcAlloc, mem, rigidbodies, physics ms (v2),
+                 + game-load counts, + netcode counters)
         │ aggregated by
 BenchmarkStatistics  (avg/p95/p99/max frame ms, avg/p1 fps, stddev, avg draws/tris,
-                      total GC, CPU/GPU avgs, netcode share, collector overhead)
+                      total GC, CPU/GPU avgs + thread breakdown + busy-CPU (v2),
+                      physics share (v2), netcode share, collector overhead)
         │ scored by
 BenchmarkAnalysis    (0–100 score, A–F grade via BenchmarkGrade, boundVerdict CPU/GPU,
                       hints[] from BenchmarkHintRulesSO rules)
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
index baa313836..0c4a87ee1 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BENCHMARK_TOOL.md
@@ -209,7 +209,15 @@ instancing, `sharedMaterial` + `MaterialPropertyBlock`, SOAP over `Find*`).
 
 Rule types: `GcPerFrameKb`, `MemorySlopeKbPerFrame` (leak), `AvgDrawCalls`, `GpuBound`, `CpuBound`,
 `FrameInstability`, `SpikeMarkerName`, `NetcodeSharePercent`, `RpcsPerFrame`. Severities: `Info`,
-`Warning`, `Blocker`.
+`Warning`, `Blocker`. The `GpuBound`/`CpuBound` rules and the report's `boundVerdict` compare GPU
+time against **busy CPU** (`EffectiveCpuTimeMs` — wait-for-present removed) on schema-v2 captures,
+so frame-capped runs aren't misread as CPU-bound.
+
+Schema v2 (see `BenchmarkReport.CurrentSchemaVersion`) adds to every `FrameSnapshot`: CPU
+main-thread / present-wait / render-thread times and `physicsTimeMs` (the `Physics.Processing`
+marker), and to `statistics`: their averages plus `avgCpuBusyTimeMs` / `maxCpuBusyTimeMs` /
+`physicsSharePercent`. Pre-v2 reports load fine — the new fields read as 0 and verdicts fall back
+to the raw CPU total.
 
 ---
 
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs
index 875d6c9d3..3580bf6d3 100644
--- a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs
+++ b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkAnalysis.cs
@@ -138,8 +138,10 @@ namespace CosmicShore.Utility.PerformanceBenchmark
             return Math.Max(0, Math.Min(100, score));
         }
 
+        // Busy CPU (wait-for-present removed) when the capture has thread times (schema v2),
+        // else the raw total — identical to pre-v2 behavior on old reports.
         static string BoundVerdict(BenchmarkStatistics s) =>
-            FrameBoundness.Classify(s.avgCpuFrameTimeMs, s.avgGpuFrameTimeMs);
+            FrameBoundness.Classify(s.EffectiveCpuTimeMs, s.avgGpuFrameTimeMs);
 
         static bool TryEvaluate(HintRule rule, BenchmarkStatistics s, List<SpikeEntry> spikes, out BenchmarkHint hint)
         {
@@ -172,15 +174,15 @@ namespace CosmicShore.Utility.PerformanceBenchmark
                 case HintRuleType.GpuBound:
                 {
                     if (s.avgGpuFrameTimeMs <= 0.001f) return false;
-                    if (s.avgGpuFrameTimeMs <= s.avgCpuFrameTimeMs * rule.threshold) return false;
-                    finding = $"GPU {s.avgGpuFrameTimeMs:F1} ms vs CPU {s.avgCpuFrameTimeMs:F1} ms — GPU-bound.";
+                    if (s.avgGpuFrameTimeMs <= s.EffectiveCpuTimeMs * rule.threshold) return false;
+                    finding = $"GPU {s.avgGpuFrameTimeMs:F1} ms vs busy CPU {s.EffectiveCpuTimeMs:F1} ms — GPU-bound.";
                     break;
                 }
                 case HintRuleType.CpuBound:
                 {
-                    if (s.avgCpuFrameTimeMs <= 0.001f) return false;
-                    if (s.avgCpuFrameTimeMs <= s.avgGpuFrameTimeMs * rule.threshold) return false;
-                    finding = $"CPU {s.avgCpuFrameTimeMs:F1} ms vs GPU {s.avgGpuFrameTimeMs:F1} ms — CPU-bound.";
+                    if (s.EffectiveCpuTimeMs <= 0.001f) return false;
+                    if (s.EffectiveCpuTimeMs <= s.avgGpuFrameTimeMs * rule.threshold) return false;
+                    finding = $"Busy CPU {s.EffectiveCpuTimeMs:F1} ms vs GPU {s.avgGpuFrameTimeMs:F1} ms — CPU-bound.";
                     break;
                 }
                 case HintRuleType.FrameInstability:
diff --git a/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs b/Assets/_Scripts/Utility/PerformanceBenchmark/BenchmarkComparison.cs
```

</details>

### `42d87b8b9` — docs(ecosystem): plan networked fauna sync for the lava-lamp menu

_Claude, 2026-07-02 15:51:08 +0000_

```text
Design + sequenced backlog for replicating the Menu_Main fauna (tadpole,
brittlestar, shark) across party members: server-authoritative simulation
with per-fauna NetworkObject + server-auth NetworkTransform puppets,
cosmetic client-side grazing to preserve mass conservation, OnLaunchGame
brood despawn, and a Cell.prefab CellNetworkSync prerequisite. Resolves
ECOSYSTEM.md §7.2 caveat 4 (cross-client fauna divergence) when implemented.
```

```text
 Docs/ECOSYSTEM.md              |   2 +
 Docs/ECOSYSTEM_NETWORK_SYNC.md | 415 +++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 417 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 434 lines)</summary>

```diff
diff --git a/Docs/ECOSYSTEM.md b/Docs/ECOSYSTEM.md
index a107a74c9..bd7e07bc6 100644
--- a/Docs/ECOSYSTEM.md
+++ b/Docs/ECOSYSTEM.md
@@ -536,6 +536,8 @@ cleared.
 >    client-local (trails reconstructed from networked vessel movement; cell phase
 >    synced via `CellNetworkSync`). Fine for a per-client **perf** test and the
 >    menu; **diverges across clients**, so not yet fair for competitive play.
+>    **Sync plan (server-authoritative fauna, replicated puppets):**
+>    `Docs/ECOSYSTEM_NETWORK_SYNC.md`.
 > 5. **Net perf.** Fauna cost CPU (per-tick `OverlapSphere` per creature). Test
 >    whether trail savings beat fauna cost: start modest and profile before/after;
 >    scale `PopulationSize` only if net-positive.
diff --git a/Docs/ECOSYSTEM_NETWORK_SYNC.md b/Docs/ECOSYSTEM_NETWORK_SYNC.md
new file mode 100644
index 000000000..2ead9d77a
--- /dev/null
+++ b/Docs/ECOSYSTEM_NETWORK_SYNC.md
@@ -0,0 +1,415 @@
+# Networked Fauna Sync — Lava-Lamp (Menu_Main) Plan
+
+**Status: PLAN — not yet implemented.** This document is the design + sequenced
+backlog for making the lava-lamp fauna (tadpoles, brittlestars, sharks) one
+shared, synced population across all party members in Menu_Main, instead of a
+divergent per-client simulation. It resolves caveat #4 of
+`Docs/ECOSYSTEM.md` §7.2 ("Client-local fauna … diverges across clients").
+
+Read first: `Docs/ECOSYSTEM.md` (the ecosystem bible — §0 conserved mass, §6
+prey-linked starvation, §7 diet split), `CLAUDE.md` § "Multiplayer / Netcode"
+and § "Don't cheat emergence".
+
+---
+
+## 0. Problem statement
+
+The lava lamp *is* freestyle (one system, two names), and Menu_Main is a
+networked scene: under the locked EAGER-Relay design every player hosts a
+Relay session in the menu, and party members join as Netcode clients. The
+vessels are NetworkObjects and replicate; the **ecosystem does not**. Each
+peer runs its own `RandomLifeSpawner` with local `Random` rolls, so:
+
+- Fauna **counts, species mix, spawn positions, and trajectories** differ per
+  peer — host sees a shark chasing a tadpole; the client sees neither.
+- **Births** (`Fauna.TryReproduce`) and **deaths** (starvation, predation)
+  happen independently per peer.
+- Consumption diverges: the host's fauna trim the host's trails; the client's
+  fauna trim a *different* set of local prisms.
+
+Goal: every peer in a menu party sees **the same creatures in the same places
+doing the same things** — births, hunts, kills, and starvations included.
+
+### Non-goals (this plan)
+
+- Syncing **flora placement/growth** (separate follow-up — §6 Phase F).
+- Shared **prism identity** for trails (trails stay client-reconstructed from
+  replicated vessel motion, as today).
+- Authoritative **player→fauna damage** (impacts stay client-local, as they
+  are for all prisms today).
+- `Worm`/`BodySegmentFauna` and the manager-spawned groups (`BoidManager`,
+  `LightFaunaManager`, `WormManager`) — not in the Blob (menu) profile.
+- The `IntensityWiseLifeSpawner` scenes (WildlifeBlitz, Tournament) — same
+  architecture applies later; menu first.
+
+---
+
+## 1. Current state (verified in code)
+
+| Object | Networked? | How |
+|---|---|---|
+| Vessels | YES | NetworkObject + owner-auth `ClientNetworkTransform` |
+| Players / AI players | YES | Server-spawned NetworkObjects |
+| Crystals (game scenes) | YES | `NetworkCrystalManager` — `NetworkList<CrystalSlotData>` driving **local** pooled crystals |
+| Cell phase/domain | OPTIONAL | `CellNetworkSync` (NetworkVariables, 0.5 s server mirror) — **not present in Menu_Main**: `Cell.prefab` has no `NetworkObject` and no `CellNetworkSync` |
+| Trail prisms | NO | Reconstructed per client from replicated vessel motion |
+| Flora | NO | Local `Random` planting + growth |
+| **Fauna** | **NO** | Local seeder + reproduction + starvation per client |
+
+**Menu fauna inventory** (`Blob Cell Spawn Profile`, ≤ ~14 alive at caps):
+
+| Species | Prefab | Class | Diet | Seed floor |
```

</details>

### `737d869c9` — docs(perf): add Docs/PERFORMANCE.md ledger + methodology; correct stale audit status

_Claude, 2026-07-02 15:51:45 +0000_

```text
Consolidate the project's scattered performance history into a single index:
profiling methodology (benchmark tool + Unity Profiler, used together), a
completed-work ledger with commit evidence + live-code status, the corrected
prism-audit recommendation status, and ranked open opportunities.

Verified against source and corrected the stale notes in PRISM_PERFORMANCE_AUDIT.md:
- Rec 1 (Jobs explosion manager) shipped via PrismEffectsManager
- Rec 4 (material-clone leaks) resolved — MaterialPropertyAnimator uses sharedMaterial
- Rec 5 (per-frame VFX cap) shipped — PrismFactory.MaxExplosionVFXPerFrame = 64
- Rec 7 (spatial partitioning) shipped — PrismSpatialIndex + PrismColliderLodManager
- Rec 2 (GPU-instanced explosion rendering) and Rec 3 (DOTS) remain the open wins
- Rec 8 (global prism budget) rejected by design — conflicts with mass conservation

Register the new doc in CLAUDE.md's Documentation Index.
```

```text
 Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md |  49 ++++++---
 CLAUDE.md                                              |   1 +
 Docs/PERFORMANCE.md                                    | 281 +++++++++++++++++++++++++++++++++++++++++++++++
 3 files changed, 317 insertions(+), 14 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 359 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md b/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
index 056bc1b3c..aae247df1 100644
--- a/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
+++ b/Assets/_Scripts/Game/Prisms/PRISM_PERFORMANCE_AUDIT.md
@@ -352,20 +352,41 @@ Similarly, replace `CreateBlockCoroutine` with a delayed-activation manager that
 
 ---
 
-## Progress Update (March 2026)
-
-Since the original audit, the following optimizations have been implemented or are in active development:
-
-- **PrismEffectsManager** — centralized Jobs+Burst explosion/implosion animation manager; `PrismExplosion`/`PrismImplosion` now only register state and the manager batch-applies via a shared MaterialPropertyBlock (Recommendation 1)
-- **PrismTimerManager** — centralized timer system replacing per-prism coroutines (Recommendation 6)
-- **Per-frame explosion VFX cap** — limits concurrent explosion effects (Recommendation 5)
-- **MaterialPropertyAnimator sharedMaterial migration** — the material-clone paths cited in Recommendation 4 now use `sharedMaterial` (the per-prism instancing leak is resolved)
-- **EventListenerBase GC elimination** — reduced garbage collection from event listener allocations
-- **PrismAOEData** — cache-line-aware data layout with hot/cold splitting and bit-packed flags for AOE queries
-- **Burst-compiled spatial queries** — replaces Physics-based AOE prism damage (partial implementation of Recommendation 7)
-- **PrismColliderLodManager** — proximity-based collider culling (partial implementation of Recommendation 7)
-
-Recommendations 2 (GPU instanced explosion rendering), 3 (full DOTS conversion), and the collider-replacement remainder of 7 remain unimplemented — see `Docs/PERFORMANCE_REFACTOR_REVIEW.md` (July 2026) for the verified codebase-wide follow-up.
+## Progress Update
+
+> The consolidated, continuously-maintained status of every recommendation lives in
+> **`Docs/PERFORMANCE.md` §4** (verified against live code). The summary below is kept
+> in sync with it.
+
+Implemented since the original audit:
+
+- **PrismTimerManager** — centralized timer system replacing per-prism coroutines (Recommendation 6). ✅
+- **PrismEffectsManager** — Burst `IJobParallelFor` explosion/implosion VFX batching; `PrismExplosion`/`PrismImplosion` only register state and the manager batch-applies (Recommendation 1). ✅
+- **Per-frame explosion VFX cap** — `PrismFactory.MaxExplosionVFXPerFrame = 64` (Recommendation 5). ✅
+- **`sharedMaterial` everywhere** — `MaterialPropertyAnimator` no longer clones materials (Recommendation 4). ✅
+- **EventListenerBase GC elimination** — reduced garbage collection from event listener allocations.
+- **PrismSpatialIndex** (formerly `PrismAOERegistry`) — cache-line-aware hot/cold layout + Burst AOE
+  queries + occupancy reservations + neighborhood views; replaced `Physics.OverlapSphere`/`CheckBox`
+  across assemblers and fauna. Plus `PrismColliderLodManager` for proximity collider-LOD
+  (together: the shipped portion of Recommendation 7). ✅
+
+Still open:
+
+- **Recommendation 2** (GPU-instanced explosion rendering) — the Jobs *compute* landed via
+  `PrismEffectsManager`, but rendering is still one GameObject per explosion (N draw calls + N
+  per-frame `transform`/`SetPropertyBlock` writes). This is the biggest remaining prism win —
+  in progress on the `claude/zen-volta-t9su0i` instanced-rendering branch (see
+  `Docs/PERF_BRANCH_MERGE_PLAN.md`).
+- **Recommendation 3** (full DOTS/ECS conversion) — unimplemented; highest payoff, highest risk.
+- **Recommendation 7 remainder** — collider-LOD culls colliders by proximity, but the full
+  collider *replacement* (spatial-hash collision for moving objects, no per-prism PhysX collider)
+  envisioned by the recommendation has not shipped.
+- **Recommendation 8** (global prism budget / recycle oldest prisms) — **rejected by design**: it
+  conflicts with the locked mass-conservation invariant (no prism count caps or TTLs). The collider
+  budget (Rec 7) is the real budget; see `Docs/PERFORMANCE.md` §2.
+
+See also **`Docs/PERFORMANCE_REFACTOR_REVIEW.md`** (July 2026) — the verified codebase-wide
+review whose fix batches landed alongside this ledger.
 
 ---
 
diff --git a/CLAUDE.md b/CLAUDE.md
index f45543b57..b1ae1a0ab 100644
--- a/CLAUDE.md
+++ b/CLAUDE.md
@@ -305,6 +305,7 @@ MiniGameControllerBase (abstract, NetworkBehaviour)
 | `SCENES.md` | `Docs/` | Complete scene inventory, game modes, launch pipeline |
 | `THREADING.md` | `Docs/` | UniTask / SyncContext threading rules, `.AsMainThread()` contract, `MainThreadDispatcher`, canary, history |
 | `SPATIAL_INDEX.md` | `Docs/` | `PrismSpatialIndex` — THE canonical spatial index of prism mass (Burst AOE queries, growth occupancy reservations, bucket grid). **Read before adding any spatial query against prisms.** |
+| `PERFORMANCE.md` | `Docs/` | **The performance index.** Profiling methodology (benchmark tool + Unity Profiler, used together), the completed-work ledger (every perf change with commit + live-code status), corrected prism-audit recommendation status, and ranked open opportunities. **Start here for any performance work.** |
 | `PartySystem/` | `Docs/` | Party (Relay) layer: `ARCHITECTURE.md` (locked design, investigation Q&A, error-handling matrix, exit criteria), `REFACTOR.md` (active backlog + deferred items + per-commit protocol), `BUGS.md`, `TESTS.md`, `TODOS.md`. EAGER per-user Relay session is the locked design. |
 | `PresenceSystem/` | `Docs/` | Presence-lobby (discovery) layer: `ARCHITECTURE.md`, `REFACTOR.md`, `BUGS.md`, `TESTS.md`, `TODOS.md`. Lobby-only UGS session, coexists with NetworkManager. |
 | `NetworkDiagnostics/` | `Docs/` | NetDiag overlay: `ARCHITECTURE.md` (NetworkMonitor + `NetworkDiagnostics` helper, classification rules), `TESTS.md` (Tests A-E), `TODOS.md`. |
diff --git a/Docs/PERFORMANCE.md b/Docs/PERFORMANCE.md
new file mode 100644
index 000000000..7f31e67b9
--- /dev/null
+++ b/Docs/PERFORMANCE.md
@@ -0,0 +1,281 @@
+# Performance — Ledger, Methodology & Open Opportunities
+
```

</details>

### `885622a4c` — docs(ecosystem): flag net-sync plan §1 as pre-rework snapshot

_Claude, 2026-07-02 15:52:07 +0000_

```text
 Docs/ECOSYSTEM_NETWORK_SYNC.md | 5 ++++-
 1 file changed, 4 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/ECOSYSTEM_NETWORK_SYNC.md b/Docs/ECOSYSTEM_NETWORK_SYNC.md
index 2ead9d77a..a7fa60a3d 100644
--- a/Docs/ECOSYSTEM_NETWORK_SYNC.md
+++ b/Docs/ECOSYSTEM_NETWORK_SYNC.md
@@ -1,6 +1,9 @@
 # Networked Fauna Sync — Lava-Lamp (Menu_Main) Plan
 
-**Status: PLAN — not yet implemented.** This document is the design + sequenced
+**Status: PLAN — not yet implemented.** *(Extraction note, July 2026: this plan was
+authored June 12 on `claude/optimistic-maxwell-uet05g`, before the late-June ecology rework —
+its §1 "current state" claims predate Burst density-grid fauna queries, the 3-phase ladder,
+and fauna reproduction. Treat §1 as historical; re-verify current state before implementing.)* This document is the design + sequenced
 backlog for making the lava-lamp fauna (tadpoles, brittlestars, sharks) one
 shared, synced population across all party members in Menu_Main, instead of a
 divergent per-client simulation. It resolves caveat #4 of
```

</details>

### `bb8cf7783` — perf: Wave 3 extraction sweep — re-port still-open wins from nine unmerged branches

_Claude, 2026-07-02 16:05:31 +0000_

```text
Per Docs/PERF_BRANCH_MERGE_PLAN.md, hand-applied against current code (all
source files had drifted since March; ideas re-ported, not diffs):

- PrismActivationQueue (new, Controller/Managers): centralized deferred prism
  activation replacing one coroutine + WaitForSeconds(0.6) PER SPAWNED PRISM —
  the thundering herd profiled at 49,856 same-frame resumes / 1.9s stall /
  10.1MB GC on the source branch. Bounded 200 activations/frame, swap-remove
  Cancel wired into Prism.ResetState; destroyed-guard + spatial-index
  registration preserved in Prism.ExecuteDeferredActivation
  [from claude/add-prism-activation-queue-CEoJM]
- SpawnableBase: fold intensityLevel/domain/seed into the trail-data cache key
  at the base — most subclass hashes omitted them, so intensity changes could
  serve stale cached trail data (latent correctness bug)
  [from claude/add-spawnable-caching-wg2zr]
- GenericPoolManager: cap synchronous Awake prewarm (maxSyncPrewarm=8, only
  when the async maintenance loop will top up) — scene-load hitch
  [from claude/optimize-pool-manager-6tOOr via WDr9T]
- AIPilot: cached seek/reacquire + ability duration/cooldown waits;
  GunTransformer: cache GetComponentsInChildren (was per frame)
  [from claude/optimize-mobile-performance-7uCEG via WDr9T]
- Projectile: spike opacity via MaterialPropertyBlock (was one cloned material
  per pooled spike); ParametricJetEffect: reuse the gradient copy (was a fresh
  Gradient per frame); Prism: cached default-layer id (string hash per pooled
  init); ShapeDrawingManager: single-construct line material + OnDestroy
  cleanup; PrismTimerManager: teardown _disposing guard + swap-remove
- PrismStateManager: shield activate/deactivate SFX coalesced to one per frame
  (an AOE shielding N prisms stacked N identical sounds)
  [concept from claude/optimize-shield-effect-CgpSK]
- Menu UI sweep [from claude/optimize-menu-performance-5EODy]:
  DailyChallengeModal 1Hz countdown; QuestTrackView parallax runs only while
  content moves + cached RectTransform/CanvasGroup; InfiniteScroll targeted
  LayoutRebuilder instead of Canvas.ForceUpdateCanvases; HangarScreen grid +
  select-list card reuse instead of destroy-all/instantiate-all

Docs: review ledger, merge plan (Waves 0+3 marked done on this branch), and
PERFORMANCE.md §3.4b updated.
```

```text
 Assets/_Scripts/Controller/AI/AIPilot.cs                              |  23 +++++--
 .../Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs     |  14 +++-
 Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs      |   7 +-
 Assets/_Scripts/Controller/Managers/PrismActivationQueue.cs           | 117 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/Managers/PrismActivationQueue.cs.meta      |  11 +++
 Assets/_Scripts/Controller/Managers/PrismStateManager.cs              |  19 +++++-
 Assets/_Scripts/Controller/Managers/PrismTimerManager.cs              |  14 +++-
 Assets/_Scripts/Controller/Projectiles/Projectile.cs                  |  25 +++++--
 Assets/_Scripts/Controller/Vessel/Animation/ParametricJetEffect.cs    |  14 ++--
 Assets/_Scripts/Controller/Vessel/GunTransformer.cs                   |  10 ++-
 Assets/_Scripts/Controller/Vessel/Prism.cs                            |  36 +++++++---
 Assets/_Scripts/UI/InfiniteScroll.cs                                  |   4 +-
 Assets/_Scripts/UI/Modals/DailyChallengeModal.cs                      |  10 ++-
 Assets/_Scripts/UI/Screens/HangarScreen.cs                            |  78 +++++++++++++++++----
 Assets/_Scripts/UI/Views/QuestTrackView.cs                            |  36 ++++++++--
 Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs         |  13 +++-
 Docs/PERFORMANCE_REFACTOR_REVIEW.md                                   |  14 ++++
 Docs/PERF_BRANCH_MERGE_PLAN.md                                        |  47 +++++++------
 18 files changed, 418 insertions(+), 74 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 865 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/AI/AIPilot.cs b/Assets/_Scripts/Controller/AI/AIPilot.cs
index d59c75c9e..e19e268bb 100644
--- a/Assets/_Scripts/Controller/AI/AIPilot.cs
+++ b/Assets/_Scripts/Controller/AI/AIPilot.cs
@@ -103,6 +103,12 @@ namespace CosmicShore.Gameplay
         float _maxDistance = 50f;
         float _maxDistanceSquared;
 
+        // Cached waits — one AI seek/ability coroutine ticks for the whole match, and up
+        // to 11 backfill AI run at once; a fresh WaitForSeconds per tick is pure GC churn.
+        WaitForSeconds _waitPlayerSeek;
+        WaitForSeconds _waitPlayerReacquire;
+        static readonly WaitForSeconds s_waitAbilityStart = new WaitForSeconds(3);
+
         Vector3 _targetPosition;
         // Live opponent the AI is chasing in player-seek (Joust) mode. Chosen by the
         // UpdatePlayerTarget coroutine; Update() reads its current position every frame.
@@ -215,8 +221,7 @@ namespace CosmicShore.Gameplay
             while (AutoPilotEnabled)
             {
                 SelectClosestOpponent();
-                yield return new WaitForSeconds(
-                    _targetVesselTransform != null ? playerSeekUpdateInterval : playerReacquireInterval);
+                yield return _targetVesselTransform != null ? _waitPlayerSeek : _waitPlayerReacquire;
             }
         }
 
@@ -270,6 +275,8 @@ namespace CosmicShore.Gameplay
                 ability.Ability = inst;
             }
 
+            _waitPlayerSeek = new WaitForSeconds(playerSeekUpdateInterval);
+            _waitPlayerReacquire = new WaitForSeconds(playerReacquireInterval);
             _maxDistanceSquared = _maxDistance * _maxDistance;
             aggressiveness = defaultAggressiveness;
             throttle = defaultThrottle;
@@ -387,15 +394,19 @@ namespace CosmicShore.Gameplay
             throttle += throttleIncrease * Time.deltaTime;
         }
         
-        IEnumerator UseAbilityCoroutine(AIAbility action) 
+        IEnumerator UseAbilityCoroutine(AIAbility action)
         {
-            yield return new WaitForSeconds(3);
+            yield return s_waitAbilityStart;
+            // Duration/cooldown are constant per ability — allocate the waits once per
+            // coroutine instead of twice per use cycle.
+            var waitDuration = new WaitForSeconds(action.Duration);
+            var waitCooldown = new WaitForSeconds(action.Cooldown);
             while (AutoPilotEnabled)
             {
                 action.Ability.StartAction(actionExecutorRegistry, VesselStatus);
-                yield return new WaitForSeconds(action.Duration);
+                yield return waitDuration;
                 action.Ability.StopAction(actionExecutorRegistry, VesselStatus);
-                yield return new WaitForSeconds(action.Cooldown);
+                yield return waitCooldown;
             }
         }
         
diff --git a/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs b/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
index 43fb78ee1..0b3fc4586 100644
--- a/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
+++ b/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
@@ -694,11 +694,21 @@ namespace CosmicShore.Gameplay
                       ?? Shader.Find("Universal Render Pipeline/Unlit");
             if (shader)
             {
-                lr.material = new Material(shader);
-                lr.material.color = color;
+                // Single construction (the old .material getter-after-setter pattern read
+                // back the instance just to tint it); destroyed in OnDestroy below.
+                lr.material = new Material(shader) { color = color };
             }
         }
 
+        void OnDestroy()
+        {
+            // Runtime-created line materials are owned by this manager — destroy them
+            // with it instead of leaking instances until the next scene load's
```

</details>

### `97d743e9d` — docs(perf): ledger §3.4b — record the July verified-review batches

_Claude, 2026-07-02 16:06:26 +0000_

```text
 Docs/PERFORMANCE.md | 13 +++++++++++++
 1 file changed, 13 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/PERFORMANCE.md b/Docs/PERFORMANCE.md
index 7f31e67b9..7e6f9e038 100644
--- a/Docs/PERFORMANCE.md
+++ b/Docs/PERFORMANCE.md
@@ -169,6 +169,19 @@ A benchmark-driven redesign of "where is the enemy mass?" for fauna seeking. Pha
 | Audio optimization | `a7f426af` |
 | Uncap frame rate (`BootstrapConfig` targetFrameRate −1, vSync off) | `67fffa78` |
 
+### 3.4b July 2026 verified-review batches
+
+The July 2026 codebase-wide review (`Docs/PERFORMANCE_REFACTOR_REVIEW.md`) landed four
+fix batches plus a Wave-3 extraction sweep re-porting the still-open wins from nine
+unmerged optimization branches (`Docs/PERF_BRANCH_MERGE_PLAN.md` has the full audit).
+Highlights: `PrismActivationQueue` (spawn-coroutine thundering herd → bounded central
+queue), O(1) trail indexing, single turn-monitor driver, allocation-free `GameDataSO`
+lookups, Spindle/Crystal material-clone elimination + leak fixes, `DomainVolumeIndicator`
+sub-canvas isolation, `GameEventFeed` row pooling, spawnable cache-key correctness fix,
+shield-SFX frame coalescing, super-shield 24→8-face mesh, and a menu-UI sweep. This
+substantially addresses §5-E (per-frame managed allocations) for the audited paths —
+re-profile before chasing more.
+
 ### 3.5 Measurement infrastructure (meta-performance)
 
 The whole `Assets/_Scripts/Utility/PerformanceBenchmark/` suite is itself a large
```

</details>

### `497b9be90` — fix: review findings — duplicate OnDestroy in PrismTimerManager, asset-safe line-material cleanup

_Claude, 2026-07-02 16:22:58 +0000_

```text
- PrismTimerManager: the Wave-3 _disposing guard added a second OnDestroy
  (CS0111 — would have failed the whole default assembly); folded into the
  existing OnDestroy
- ShapeDrawingManager: track the runtime-created line materials explicitly and
  destroy only those — the previous cleanup destroyed sharedMaterial
  unconditionally, which would throw on an inspector-wired asset material
```

```text
 Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs | 13 ++++++++++---
 Assets/_Scripts/Controller/Managers/PrismTimerManager.cs                      |  4 ++--
 2 files changed, 12 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs b/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
index 0b3fc4586..f0a83c687 100644
--- a/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
+++ b/Assets/_Scripts/Controller/Environment/MiniGameObjects/ShapeDrawingManager.cs
@@ -696,17 +696,24 @@ namespace CosmicShore.Gameplay
             {
                 // Single construction (the old .material getter-after-setter pattern read
                 // back the instance just to tint it); destroyed in OnDestroy below.
-                lr.material = new Material(shader) { color = color };
+                var runtimeMaterial = new Material(shader) { color = color };
+                lr.material = runtimeMaterial;
+                s_runtimeLineMaterials.Add(runtimeMaterial);
             }
         }
 
+        // Only materials WE created — never an inspector-wired asset material, which
+        // Destroy() would refuse with "Destroying assets is not permitted".
+        static readonly List<Material> s_runtimeLineMaterials = new();
+
         void OnDestroy()
         {
             // Runtime-created line materials are owned by this manager — destroy them
             // with it instead of leaking instances until the next scene load's
             // UnloadUnusedAssets pass.
-            if (guideLine && guideLine.sharedMaterial) Destroy(guideLine.sharedMaterial);
-            if (ghostLine && ghostLine.sharedMaterial) Destroy(ghostLine.sharedMaterial);
+            for (int i = 0; i < s_runtimeLineMaterials.Count; i++)
+                if (s_runtimeLineMaterials[i]) Destroy(s_runtimeLineMaterials[i]);
+            s_runtimeLineMaterials.Clear();
         }
 
         // ── Camera Helpers ──────────────────────────────────────────────────
diff --git a/Assets/_Scripts/Controller/Managers/PrismTimerManager.cs b/Assets/_Scripts/Controller/Managers/PrismTimerManager.cs
index d996c3b9c..252e46280 100644
--- a/Assets/_Scripts/Controller/Managers/PrismTimerManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismTimerManager.cs
@@ -51,10 +51,9 @@ namespace CosmicShore.Gameplay
 
         // During teardown every dying prism calls CancelTimers — with N timers and M
         // prisms that's an O(N*M) scan storm over state that's being discarded anyway.
+        // Set in OnDestroy below.
         bool _disposing;
 
-        void OnDestroy() => _disposing = true;
-
         /// <summary>
         /// Schedule a shield deactivation for the given PrismStateManager after a delay.
         /// Cancels any existing timer for the same target first.
@@ -134,6 +133,7 @@ namespace CosmicShore.Gameplay
 
         private void OnDestroy()
         {
+            _disposing = true;
             activeTimers.Clear();
             completionTargets.Clear();
         }
```

</details>

### `96ce7042b` — perf(ecology): cache per-prism world-volume — kill the ~23ms lossyScale walk

_Claude, 2026-07-02 19:22:35 +0000_

```text
Cherry-picked from claude/zen-volta-t9su0i (1c2022288) and adapted: the
Checkpoint A/C hooks it referenced (SyncRenderTransform companion-entity
sync, PrismColliderLodManager.NotifyPrismActivated) don't exist on this
branch and were dropped — the LOD manager's periodic sweep still classifies
restored colliders; added the PrismScaleAnimator.OwnerPrism accessor the
batched apply needs.

Pulled forward because a live profiler capture on this branch showed
DomainVolumeIndicator.Update at 15.64ms self (29.5% of a 53ms frame) — that
cost is Cell.EnsureVolumeFresh's O(all-prisms) transform.lossyScale walk
firing through the gauge's 0.25s sample. Zen-volta measured the same walk at
22.96ms on a 9,234-prism scene. Cell volume aggregation now reads
Prism.CachedVolume, refreshed O(growing)/frame by PrismScaleManager and at
create/restore/destroy boundaries; volume-is-the-spine semantics preserved
(<=1 frame lag on actively growing prisms).

When zen-volta merges, these hunks are content-identical and should resolve
clean; its additional hooks then layer on top.
```

```text
 Assets/_Scripts/Controller/Environment/Cell.cs                      |  6 ++++-
 Assets/_Scripts/Controller/Environment/Prisms/PrismScaleAnimator.cs |  6 +++++
 Assets/_Scripts/Controller/Managers/PrismScaleManager.cs            |  6 +++++
 Assets/_Scripts/Controller/Vessel/Prism.cs                          | 44 +++++++++++++++++++++++++++++++++--
 4 files changed, 59 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 141 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Environment/Cell.cs b/Assets/_Scripts/Controller/Environment/Cell.cs
index e555f36e8..69e7b3061 100644
--- a/Assets/_Scripts/Controller/Environment/Cell.cs
+++ b/Assets/_Scripts/Controller/Environment/Cell.cs
@@ -225,7 +225,11 @@ namespace CosmicShore.Gameplay
                 // RemoveBlock) are collected here instead of leaking forever.
                 if (!prism) { s_deadMassScratch.Add(prism); continue; }
 
-                float v = prism.CurrentVolume;
+                // Cached world-volume (Prism.CachedVolume) — identical to CurrentVolume
+                // but refreshed only when the prism's scale changes, so this whole-
+                // population recompute no longer does a transform.lossyScale parent-walk
+                // per prism (the ~23ms/recompute hotspot at high prism counts).
+                float v = prism.CachedVolume;
                 if (v <= 0f) continue; // destroyed / not yet grown
 
                 var domain = prism.Domain; // LIVE domain — steals re-attribute next tick
diff --git a/Assets/_Scripts/Controller/Environment/Prisms/PrismScaleAnimator.cs b/Assets/_Scripts/Controller/Environment/Prisms/PrismScaleAnimator.cs
index fa573b207..ca55dd49a 100644
--- a/Assets/_Scripts/Controller/Environment/Prisms/PrismScaleAnimator.cs
+++ b/Assets/_Scripts/Controller/Environment/Prisms/PrismScaleAnimator.cs
@@ -25,6 +25,12 @@ namespace CosmicShore.Gameplay
         public Vector3 AuthoredTargetScale => authoredTargetScale;
         public float GrowthRate { get; set; } = 0.01f;
 
+        /// <summary>
+        /// The Prism this animator belongs to (cached in Awake). PrismScaleManager's
+        /// batched apply uses it to refresh the prism's volume cache on growth frames.
+        /// </summary>
+        public Prism OwnerPrism => prism;
+
         private Prism prism;
         private MeshRenderer meshRenderer;
         private bool isRegistered;
diff --git a/Assets/_Scripts/Controller/Managers/PrismScaleManager.cs b/Assets/_Scripts/Controller/Managers/PrismScaleManager.cs
index ba208b0ec..52aa38e0a 100644
--- a/Assets/_Scripts/Controller/Managers/PrismScaleManager.cs
+++ b/Assets/_Scripts/Controller/Managers/PrismScaleManager.cs
@@ -90,6 +90,10 @@ namespace CosmicShore.Gameplay
                 else
                 {
                     block.transform.localScale = data.currentScale;
+                    // Refresh the volume cache here (O(growing)/frame) so the
+                    // cell's per-domain aggregation never has to read lossyScale for
+                    // the whole population (O(all)/recompute — the old ~23ms hotspot).
+                    block.OwnerPrism?.RefreshVolumeCache();
                 }
             }
 
@@ -101,6 +105,8 @@ namespace CosmicShore.Gameplay
 
                 // Hit target exactly
                 block.transform.localScale = targetScale;
+                // Final settled volume — last refresh until this prism scales again.
+                block.OwnerPrism?.RefreshVolumeCache();
 
                 // Stop scaling (may call back into manager depending on your base class)
                 block.IsScaling = false;
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index 89d5c86af..3e84bda91 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -207,6 +207,7 @@ namespace CosmicShore.Gameplay
             devastated = false;
             _destroyedByCreature = false; // pool reuse: clear stale creature-kill flag
             _lodCulled = false; // pool reuse: Initialize owns the collider again
+            CachedVolume = 0f;  // stale from the previous life; reseeded at CreateBlock
             IsSmallest = false;
             IsLargest = false;
             
@@ -286,6 +287,11 @@ namespace CosmicShore.Gameplay
             // cell's density grids. The cell binding is what makes trail mass
             // visible to fauna anti-domain targeting and the cell's phase system;
             // fauna bodies are excluded from that view inside the index.
+            // Seed the volume cache before the cell starts aggregating this prism
+            // (it enters massTracked via the Register → BindCell path below), so the
+            // first volume recompute reads a real value, not the default 0.
+            RefreshVolumeCache();
+
             var spatialIndex = PrismSpatialIndex.EnsureInstance();
```

</details>

### `e0e3c8d73` — docs(perf): note the volume-cache pull-forward in the merge plan

_Claude, 2026-07-02 19:23:57 +0000_

```text
 Docs/PERF_BRANCH_MERGE_PLAN.md | 7 +++++++
 1 file changed, 7 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/PERF_BRANCH_MERGE_PLAN.md b/Docs/PERF_BRANCH_MERGE_PLAN.md
index d2852999c..958452d23 100644
--- a/Docs/PERF_BRANCH_MERGE_PLAN.md
+++ b/Docs/PERF_BRANCH_MERGE_PLAN.md
@@ -95,6 +95,13 @@ the source branches (`optimistic-planck`, `beautiful-feynman`, `tender-pasteur`,
 8. Delete `kind-turing` (contained).
 
 ### Wave 2 — this branch (rciwzl)
+> **Update (July 2):** zen-volta's volume-cache commit (`1c2022288`) was pulled forward
+> onto this branch after a live profiler capture showed its target
+> (`Cell.EnsureVolumeFresh`'s O(all-prisms) lossyScale walk, surfacing as 15.64ms under
+> `DomainVolumeIndicator.Update`) as the single largest main-thread cost. The Checkpoint
+> A/C hooks were dropped in adaptation; when zen-volta merges, those hunks are
+> content-identical and should auto-resolve, with its extra hooks layering on top.
+
 9. Rebase/merge `performance-refactoring-review-rciwzl` onto post-zen-volta
    bleeding-edge. Two hand-resolves:
    - **`Spindle.cs` union** (both wins): keep zen-volta's 8 shared phase-variant
```

</details>

### `b5cf0ca34` — fix(prism): restore Prism.Grow() dropped by the volume-cache cherry-pick

_Claude, 2026-07-02 22:52:25 +0000_

```text
The 1c2022288 cherry-pick silently removed 'public void Grow(float) =>
scaleAnimator.Grow(amount)' (its region auto-merged, no conflict marker), so
Boid.cs's otherPrism.Grow()/embeddedHealthPrism.Grow() calls no longer
resolved — CS1061, the whole default assembly. This is the exact regression
zen-volta fixed on its own lineage. Restored beside RefreshVolumeCache.
```

```text
 Assets/_Scripts/Controller/Vessel/Prism.cs | 6 ++++++
 1 file changed, 6 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/Prism.cs b/Assets/_Scripts/Controller/Vessel/Prism.cs
index 3e84bda91..d71a4f9f8 100644
--- a/Assets/_Scripts/Controller/Vessel/Prism.cs
+++ b/Assets/_Scripts/Controller/Vessel/Prism.cs
@@ -381,6 +381,12 @@ namespace CosmicShore.Gameplay
             CachedVolume = v > 0f ? v : Mathf.Max(prismProperties?.volume ?? 0f, 0f);
         }
 
+        // Growth Methods
+        // NOTE: restored here — the volume-cache cherry-pick (1c2022288) dropped this
+        // line, breaking Boid.cs's otherPrism.Grow()/embeddedHealthPrism.Grow() calls
+        // (CS1061). zen-volta hit and fixed the identical regression on its own lineage.
+        public void Grow(float amount = 1) => scaleAnimator.Grow(amount);
+
         // Collision Handling
         protected void OnTriggerEnter(Collider other)
         {
```

</details>

### `5de0cc276` — docs(perf): add zen-volta merge handoff — conflict recipe, sequence, decisions

_Claude, 2026-07-02 22:55:30 +0000_

```text
 Docs/ZEN_VOLTA_HANDOFF.md | 206 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 206 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 212 lines)</summary>

```diff
diff --git a/Docs/ZEN_VOLTA_HANDOFF.md b/Docs/ZEN_VOLTA_HANDOFF.md
new file mode 100644
index 000000000..7d075b190
--- /dev/null
+++ b/Docs/ZEN_VOLTA_HANDOFF.md
@@ -0,0 +1,206 @@
+# Handoff → zen-volta owner (instanced prism rendering)
+
+**From:** the `claude/performance-refactoring-review-rciwzl` workstream (codebase-wide perf
+review + extraction of every unmerged optimization branch).
+**To:** whoever is driving `claude/zen-volta-t9su0i` / PR #573 to merge.
+**Date:** 2026-07-02. **rciwzl HEAD at handoff:** `b5cf0ca34` (17 commits over bleeding-edge, compiles clean).
+
+**One line:** your branch is the biggest single win in the whole perf backlog and a live
+profiler capture proves it; this doc gives you the full picture of the *other* perf branch
+that will merge alongside yours, the **exact** conflict surface between the two (4 files,
+with a copy-pasteable resolution for each), the recommended merge order, and the two
+product decisions only you can make.
+
+---
+
+## 1. Why you should care about `rciwzl` at all
+
+Two performance branches are landing in the same window. Yours (`zen-volta`) is the
+**render/architecture** attack: instanced prisms via Entities Graphics, instanced shields,
+the collider-LOD sweep, spindle SRP-batching, volume cache, VFX cap. Mine (`rciwzl`) is the
+**allocation/hot-path/correctness** attack: ~30 verified fixes + an extraction sweep that
+re-ported the still-live wins from nine older optimization branches.
+
+They are **almost entirely disjoint** — I ran `git merge-tree` between the two tips. Out of
+~50 files each touches, they collide in exactly **4 files** (below). Everything else merges
+untouched. So this is a clean two-branch land, not a tangle.
+
+Full branch-by-branch audit of all 20 unmerged perf branches (what to merge / extract /
+delete) is in **`Docs/PERF_BRANCH_MERGE_PLAN.md`**. What rciwzl itself changed and why is in
+**`Docs/PERFORMANCE_REFACTOR_REVIEW.md`**. The living ledger is **`Docs/PERFORMANCE.md`**.
+
+---
+
+## 2. The profiler capture that validates your branch (use this)
+
+A live Unity Profiler capture on rciwzl (instrumented run, `DiagnosticsHUD` present, ~53 ms
+main-thread frame) showed the top three self-time costs, **all of which are things zen-volta
+fixes**:
+
+| Profiler row | Self | What it actually is | zen-volta fix |
+|---|---|---|---|
+| `DomainVolumeIndicator.Update` | **15.64 ms (29.5%)** | Misattributed — it's `Cell.EnsureVolumeFresh` walking **every prism's `transform.lossyScale`** (parent-hierarchy walk) on the 0.25 s volume recompute that the gauge's sample triggers. | **volume cache** (`1c2022288`) — measured 22.96 ms → O(growing)/frame |
+| `PrismColliderLodManager.Update` | **8.56 ms (16.1%)** | O(population) LOD sweep every tick | **Checkpoint C** population-independent sweep + `QueryUnionOfSpheres` |
+| `PrismOctahedronShield.Update` | 8,740 calls | per-shield no-op `Update`s | **central shield ticker** + instanced settled shields |
+
+**~26 ms of a 53 ms frame is your branch + the one commit I pulled forward.** If you need a
+number to justify the merge, this capture is it. (I already pulled the volume cache onto
+rciwzl — see §4 — so if you re-profile that branch, `DomainVolumeIndicator.Update` should
+have collapsed and `PrismColliderLodManager.Update` should now be the #1 row, i.e. the
+direct empirical case for landing zen-volta next.)
+
+---
+
+## 3. THE CONFLICT SURFACE — 4 files, exact resolutions
+
+Verified with `git merge-tree --write-tree rciwzl@b5cf0ca34 zen-volta`. When the second of
+the two branches rebases/merges onto the first, these are the only files that need hands.
+**rciwzl has unique, non-conflicting changes in `Prism.cs` and `PrismScaleManager.cs`
+(PrismActivationQueue wiring, `scaleAnimator.GrowthRate` re-sync, cached layer id) that live
+in separate regions and MUST survive the merge — don't blanket "take theirs" on whole files.**
+
+### 3.1 `BoidSimulationController.cs` — modify/delete → **accept rciwzl's deletion**
+rciwzl deletes it (verified dead: zero code refs, zero scene/prefab GUID refs, and it did a
+**synchronous GPU readback every `Update()`** — a per-frame pipeline stall). zen-volta only
+renamed its `struct Entity` → `BoidEntity` to dodge the `Unity.Entities.Entity` name clash.
+Deleting the file satisfies that motive even better (the clash can't recur). **Resolution:
+`git rm` it / keep deleted.**
+
+### 3.2 `Spindle.cs` — content (3 hunks) → **union, both wins**
+The two branches pull the same code opposite ways and both are right about their half:
+- **zen-volta** removes the per-spindle `_Phase` MaterialPropertyBlock (MPB excludes the
+  renderer from the SRP Batcher → ~600 un-batched spindle draws) in favor of **8 shared
+  phase-variant materials** bucketed by world-position hash.
+- **rciwzl** deletes the `originalMaterial`/`temporaryMaterial` **clone** path (one `Material`
```

</details>
