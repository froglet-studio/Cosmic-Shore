# Branch archive: `claude/audit-flora-pooling-f5vmD`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Flora prism pooling performance pass**

A performance pass on plants (flora): reuse prisms and spindles from object pools instead of creating/destroying them, remove per-frame memory allocations, fix a 51ms/frame spike in prism effects, defer colliders until growth completes, and speed up scene teardown. The final commit ported these optimizations onto the restructured codebase.

- **Status:** Partly landed
- **Areas:** flora, prism performance, object pooling, physics
- **Already in bleeding-edge:** bleeding-edge has per-vessel prism pools (Assets/_Prefabs/Pools/*PrismPool.prefab) and pooling fixes (504557466 'AOEBlockCreation ... never destroys pooled prisms'), but SpindlePoolManager and PrismActivationQueue do not exist there.
- **Risk if deleted:** medium
- **Suggestion (2026-10-08):** keep — Specific flora/spindle pooling and frame-spike fixes (SpindlePoolManager, PrismActivationQueue) were not found in bleeding-edge and may still be useful performance work.

## Evidence

- **Last commit:** 2026-06-12 by Claude
- **Unmerged commits:** 24
- **Forked from:** `be4afb009` (2026-06-11, docs(ecosystem): lock in universality — one HyperSea rule set, no exempt con)
- **Tip:** `3c458d192`
- **Files touched (103):**
  - `Assets/Unity Assests/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset`
  - `Assets/_Prefabs/HealthPrismsPoolManager.prefab`
  - `Assets/_Prefabs/HealthPrismsPoolManager.prefab.meta`
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Flora.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/SpawnableCord.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs`
  - `Assets/_Scripts/Game/Environment/HealthPrism.cs`
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
  - … and 63 more

### `898b31973` — Replace all prism Instantiate calls with object pool retrieval

_Claude, 2026-03-04 04:10:04 +0000_

```text
Audited the entire codebase for direct Instantiate() calls on Prism and
HealthPrism objects. Every site now pulls from a pool when one is
configured, with fallback to Instantiate for backward compatibility.

Key changes:
- New HealthPrismPoolManager (GenericPoolManager<HealthPrism>) for
  flora/fauna health prism pooling
- HealthPrism.Initialize() clears stale spindle ref for clean pool reuse
- HealthPrism.Explode/Implode now call ReturnToPool() after cleanup
- LifeForm gains GetHealthPrism() helper + pool field; HandleTurnEnded
  returns pooled prisms before Destroy
- AssembledFlora and BranchingFlora use GetHealthPrism() instead of
  Instantiate
- Boid and BoidSimulationController accept optional prism pool
- SpawnableBase adds prismPool field + GetPrismFromPool() helper used
  by SpawnPrismTrail and all subclass overrides (DartBoard, ShapeBase,
  Gyroid, SchwarzP, Rings, Flower)
- ShootingGalleryMiniGame and FireTrailBlockActionExecutor use pool;
  FireTrailBlockActionExecutor replaces Destroy with ReturnToPool
```

```text
 Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs                |  8 ++++--
 Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs      |  4 +--
 Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs                |  9 +++++-
 .../Game/Environment/FloraAndFauna/BoidSimulationController.cs        | 17 ++++++++---
 Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs      |  9 ++----
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs            | 26 ++++++++++++++++-
 Assets/_Scripts/Game/Environment/HealthPrism.cs                       | 18 ++++++++++--
 .../_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs   |  2 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableGyroid.cs   |  2 +-
 .../Game/Environment/MiniGameObjects/SpawnableSchwarzPSurface.cs      |  2 +-
 .../_Scripts/Game/Environment/MiniGameObjects/SpawnableShapeBase.cs   |  2 +-
 Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs            | 20 ++++++++++++-
 Assets/_Scripts/Game/Projectiles/SpawnableFlower.cs                   |  2 +-
 Assets/_Scripts/Game/Projectiles/SpawnableRings.cs                    |  6 ++--
 Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs             | 50 +++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs               | 36 +++++++++---------------
 16 files changed, 161 insertions(+), 52 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 521 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs b/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
index 20716aa51..7ccc4497e 100644
--- a/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
@@ -37,6 +37,7 @@ namespace CosmicShore.Game.Arcade
         }
 
         [FormerlySerializedAs("trailBlock")] [SerializeField] protected Prism prism;
+        [SerializeField] InteractivePrismPoolManager prismPool;
         [SerializeField] float blockCount = 20;
         [SerializeField] float radius = 60f;
         [SerializeField] protected Vector3 blockScale = new Vector3(20f, 10f, 5f);
@@ -68,9 +69,12 @@ namespace CosmicShore.Game.Arcade
 
         virtual protected Prism CreateBlock(Vector3 position, Vector3 lookPosition, Trail trail)
         {
-            var Block = Instantiate(prism);
+            Prism Block;
+            if (prismPool)
+                Block = prismPool.Get(position, Quaternion.identity);
+            else
+                Block = Instantiate(prism);
             Block.ChangeTeam(ActivePlayer.Domain);
-            // Block.ownerID = LocalPlayer.PlayerUUID;
             if (SafeLookRotation.TryGet(lookPosition - transform.position, transform.forward, out var rotation, Block))
                 Block.transform.SetPositionAndRotation(position, rotation);
             else
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
index 47bfd3780..f1a1df51b 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
@@ -108,7 +108,7 @@ namespace CosmicShore
                     continue;
                 }
 
-                HealthPrism newHealthPrism = Instantiate(healthPrism, growthInfo.Position, growthInfo.Rotation);
+                HealthPrism newHealthPrism = GetHealthPrism(growthInfo.Position, growthInfo.Rotation);
                 AddHealthBlock(newHealthPrism);
                 Branch newBranch = new Branch(newHealthPrism);
 
@@ -178,7 +178,7 @@ namespace CosmicShore
             CSDebug.Log("New Assembler");
             var newSpindle = AddSpindle();
 
-            HealthPrism newHealthPrism = Instantiate(healthPrism, transform.position, transform.rotation);
+            HealthPrism newHealthPrism = GetHealthPrism(transform.position, transform.rotation);
             AddHealthBlock(newHealthPrism);
             newHealthPrism.transform.SetParent(newSpindle.transform, false);
             newHealthPrism.LifeForm = this;
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
index d1baf7950..cb9929c61 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
@@ -42,6 +42,9 @@ public class Boid : Fauna
     [SerializeField]
     Prism healthPrism;
 
+    [SerializeField]
+    InteractivePrismPoolManager prismPool;
+
     Vector3 currentVelocity;
     Vector3 desiredDirection;
     Quaternion desiredRotation;
@@ -260,7 +263,11 @@ public class Boid : Fauna
 
     private (Prism, GyroidAssembler) NewBlock()
     {
-        var newBlock = Instantiate(healthPrism, transform.position, transform.rotation, transform);
+        Prism newBlock;
+        if (prismPool)
+            newBlock = prismPool.Get(transform.position, transform.rotation, transform);
+        else
+            newBlock = Instantiate(healthPrism, transform.position, transform.rotation, transform);
         newBlock.ChangeTeam(domain);
         newBlock.gameObject.layer = LayerMask.NameToLayer("Mound");
         newBlock.prismProperties = new() { prism = newBlock };
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
index ec1a441d1..6ddc4b314 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
```

</details>

### `5b9b7aed7` — Enforce strict prism pooling — error on raw Instantiate or Destroy

_Claude, 2026-03-04 04:54:46 +0000_

```text
Prism.cs now enforces that all prisms are created by a pool and returned
via ReturnToPool(). Raw Instantiate() or Destroy() on prisms produces
a clear error message telling the developer exactly what to fix.

Enforcement mechanism:
- Static BeginPoolCreation/EndPoolCreation guard on Prism wraps pool
  CreateFunc — Awake() records CreatedByPool flag
- Prism.Initialize() errors if prism is neither pool-created nor
  embedded (IsEmbedded flag set by owning systems like LifeForm)
- Prism.OnDestroy() errors if a pool-managed prism is destroyed while
  still active (should have been ReturnToPool()'d)
- Guards compiled out in release builds via UNITY_EDITOR || DEVELOPMENT_BUILD

All pool managers (Interactive, HealthPrism, BlockProjectile) override
CreateFunc to set the guard.

All Instantiate fallbacks replaced with LogError + null return:
- LifeForm.GetHealthPrism(), SpawnableBase.GetPrismFromPool()
- Boid.NewBlock(), BoidSimulationController init/create
- ShootingGalleryMiniGame.CreateBlock()
- FireTrailBlockActionExecutor.FireBlock()

Additional fixes caught during audit:
- SpawnableWaypointTrack, SpawnableRaceTrack — were still using raw
  Instantiate; now use GetPrismFromPool()
- AOEBlockCreation.PerformResetCleanup() — was Destroy()'ing pooled
  prisms; now uses ReturnToPool()
- Boid.Initialize() marks embeddedHealthPrism.IsEmbedded = true
```

```text
 Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs                | 13 +++++----
 Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs                | 15 +++++++----
 .../Game/Environment/FloraAndFauna/BoidSimulationController.cs        | 29 +++++++++++---------
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs            | 15 ++++++++---
 .../_Scripts/Game/Environment/MiniGameObjects/SpawnableRaceTrack.cs   |  2 +-
 .../Game/Environment/MiniGameObjects/SpawnableWaypointTrack.cs        |  2 +-
 Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs            | 11 +++++---
 Assets/_Scripts/Game/Projectiles/AOEBlockCreation.cs                  |  4 +--
 Assets/_Scripts/Game/Projectiles/BlockProjectilePoolManager.cs        |  7 +++++
 Assets/_Scripts/Game/Ship/Prism.cs                                    | 48 +++++++++++++++++++++++++++++++--
 Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs             |  7 +++++
 Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs        |  7 +++++
 Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs               | 13 +++++----
 13 files changed, 132 insertions(+), 41 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 365 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs b/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
index 7ccc4497e..f4846f53b 100644
--- a/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
@@ -69,11 +69,14 @@ namespace CosmicShore.Game.Arcade
 
         virtual protected Prism CreateBlock(Vector3 position, Vector3 lookPosition, Trail trail)
         {
-            Prism Block;
-            if (prismPool)
-                Block = prismPool.Get(position, Quaternion.identity);
-            else
-                Block = Instantiate(prism);
+            if (!prismPool)
+            {
+                Debug.LogError(
+                    $"[ShootingGalleryMiniGame] '{gameObject.name}' has no InteractivePrismPoolManager assigned. " +
+                    "All prisms must come from a pool. Assign the 'prismPool' field.", this);
+                return null;
+            }
+            var Block = prismPool.Get(position, Quaternion.identity);
             Block.ChangeTeam(ActivePlayer.Domain);
             if (SafeLookRotation.TryGet(lookPosition - transform.position, transform.forward, out var rotation, Block))
                 Block.transform.SetPositionAndRotation(position, rotation);
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
index cb9929c61..5e459d900 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
@@ -71,6 +71,7 @@ public class Boid : Fauna
             CSDebug.LogError($"{nameof(Boid)} on {name} has no embedded HealthPrism in children. Scaling cannot work.");
             return;
         }
+        embeddedHealthPrism.IsEmbedded = true;
 
         blockCollider = embeddedHealthPrism.GetComponent<BoxCollider>();
         if (!blockCollider)
@@ -263,11 +264,15 @@ public class Boid : Fauna
 
     private (Prism, GyroidAssembler) NewBlock()
     {
-        Prism newBlock;
-        if (prismPool)
-            newBlock = prismPool.Get(transform.position, transform.rotation, transform);
-        else
-            newBlock = Instantiate(healthPrism, transform.position, transform.rotation, transform);
+        if (!prismPool)
+        {
+            Debug.LogError(
+                $"[Boid] '{gameObject.name}' has no InteractivePrismPoolManager assigned. " +
+                "All prisms must come from a pool. Add an InteractivePrismPoolManager to the scene " +
+                "and assign it to the 'prismPool' field on this Boid.", this);
+            return (null, null);
+        }
+        var newBlock = prismPool.Get(transform.position, transform.rotation, transform);
         newBlock.ChangeTeam(domain);
         newBlock.gameObject.layer = LayerMask.NameToLayer("Mound");
         newBlock.prismProperties = new() { prism = newBlock };
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
index 6ddc4b314..0d58856f1 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
@@ -95,12 +95,15 @@ public class BoidSImulationController : MonoBehaviour
         {
             Vector3 spawnPosition = transform.position + Random.insideUnitSphere * spawnRadius;
             CSDebug.Log("Instantiating boid number: " + i);
-            Prism newBoid;
-            if (boidPrismPool)
-                newBoid = boidPrismPool.Get(spawnPosition, Quaternion.identity, transform);
-            else
-                newBoid = Instantiate(boidPrefab, spawnPosition, Quaternion.identity);
-            if (!boidPrismPool) newBoid.transform.SetParent(transform);
+            if (!boidPrismPool)
+            {
+                CSDebug.LogError(
+                    $"[BoidSimulationController] '{gameObject.name}' has no InteractivePrismPoolManager assigned. " +
+                    "All prisms must come from a pool. Add an InteractivePrismPoolManager to the scene " +
+                    "and assign it to the 'boidPrismPool' field.");
+                return;
+            }
+            Prism newBoid = boidPrismPool.Get(spawnPosition, Quaternion.identity, transform);
```

</details>

### `faacc6d43` — Auto-wire pool manager references for prefab-spawned objects

_Claude, 2026-03-04 09:03:02 +0000_

```text
Prefab instances can't hold scene-object references, so pool fields
are null when LifeForms/Spawnables are instantiated by CellLifeSpawnerBase
or SegmentSpawner. Each consumer now discovers its pool manager via
FindAnyObjectByType<T>() during initialization if the serialized field
is unset.
```

```text
 Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs                     | 4 ++++
 Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs                     | 4 ++++
 Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs | 4 ++++
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs                 | 4 ++++
 Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs                 | 4 ++++
 Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs                    | 4 ++++
 6 files changed, 24 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 80 of 90 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs b/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
index f4846f53b..74c45e922 100644
--- a/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
@@ -18,6 +18,10 @@ namespace CosmicShore.Game.Arcade
         {
             base.Start();
 
+            // Auto-wire pool when not set in inspector
+            if (!prismPool)
+                prismPool = FindAnyObjectByType<InteractivePrismPoolManager>();
+
             SegmentSpawner.Seed = new System.Random().Next();
         }
 
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
index 5e459d900..5224d5867 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
@@ -65,6 +65,10 @@ public class Boid : Fauna
 
     public override void Initialize(Cell cell)
     {
+        // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
+        if (!prismPool)
+            prismPool = FindAnyObjectByType<InteractivePrismPoolManager>();
+
         embeddedHealthPrism = GetComponentInChildren<HealthPrism>(true);
         if (!embeddedHealthPrism)
         {
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
index 0d58856f1..91be8f00a 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
@@ -49,6 +49,10 @@ public class BoidSImulationController : MonoBehaviour
 
     private void Start()
     {
+        // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
+        if (!boidPrismPool)
+            boidPrismPool = FindAnyObjectByType<InteractivePrismPoolManager>();
+
         kernel = boidSimulationShader.FindKernel("CSMain");
 
         InitializeEntities();
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
index e7e885adb..1bf55cb44 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
@@ -69,6 +69,10 @@ namespace CosmicShore
             if (initialized) return;
             initialized = true;
 
+            // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
+            if (!healthPrismPool)
+                healthPrismPool = FindAnyObjectByType<HealthPrismPoolManager>();
+
             if (shieldPeriod > 0)
                 StartCoroutine(ShieldRegen());
 
diff --git a/Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs b/Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs
index 971468bd1..f13707a8d 100644
--- a/Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs
+++ b/Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs
@@ -140,6 +140,10 @@ namespace CosmicShore.Game.Spawning
         /// </summary>
         public virtual GameObject Spawn(int intensity = 1)
         {
+            // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
+            if (!prismPool)
+                prismPool = FindAnyObjectByType<InteractivePrismPoolManager>();
+
             intensityLevel = intensity;
             trails.Clear();
 
diff --git a/Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs b/Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs
index 595ddd1d2..e73c60297 100644
--- a/Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs
+++ b/Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs
@@ -18,6 +18,10 @@ public sealed class FireTrailBlockActionExecutor : ShipActionExecutorBase
```

</details>

### `2edaf4d89` — Add null guards after GetHealthPrism() calls in flora classes

_Claude, 2026-03-04 17:58:08 +0000_

```text
GetHealthPrism() returns null when no HealthPrismPoolManager exists
in the scene. AssembledFlora and BranchingFlora were not guarding
against this, causing NullReferenceExceptions in AddHealthBlock()
and downstream .gameObject accesses.
```

```text
 Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs | 5 ++++-
 Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs | 4 +++-
 2 files changed, 7 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
index f1a1df51b..5dfa61d8d 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
@@ -109,6 +109,7 @@ namespace CosmicShore
                 }
 
                 HealthPrism newHealthPrism = GetHealthPrism(growthInfo.Position, growthInfo.Rotation);
+                if (!newHealthPrism) continue;
                 AddHealthBlock(newHealthPrism);
                 Branch newBranch = new Branch(newHealthPrism);
 
@@ -170,7 +171,8 @@ namespace CosmicShore
         public override void Plant()
         {
             assembler = CreateNewAssembler();
-            transform.position = cellData.CrystalTransform.position + 200 * Random.onUnitSphere; // TODO: replace magic number with nucleus radius 
+            if (!assembler) return;
+            transform.position = cellData.CrystalTransform.position + 200 * Random.onUnitSphere; // TODO: replace magic number with nucleus radius
         }
 
         public Assembler CreateNewAssembler()
@@ -179,6 +181,7 @@ namespace CosmicShore
             var newSpindle = AddSpindle();
 
             HealthPrism newHealthPrism = GetHealthPrism(transform.position, transform.rotation);
+            if (!newHealthPrism) return null;
             AddHealthBlock(newHealthPrism);
             newHealthPrism.transform.SetParent(newSpindle.transform, false);
             newHealthPrism.LifeForm = this;
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
index a6639f8f4..a0f66c5c3 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
@@ -125,7 +125,9 @@ namespace CosmicShore
                     Branch newBranch = new Branch();
                     if (Random.value < leafChance)
                     {
-                        newBranch.gameObject = GetHealthPrism(branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward), branch.gameObject.transform.rotation).gameObject;
+                        var leafPrism = GetHealthPrism(branch.gameObject.transform.position + (branchingScaleFactor * branch.gameObject.transform.forward), branch.gameObject.transform.rotation);
+                        if (!leafPrism) continue;
+                        newBranch.gameObject = leafPrism.gameObject;
                         ScaleAndPositionBranch(ref newBranch, branch);
                         var newHealthblock = newBranch.gameObject.GetComponent<HealthPrism>();
                         AddHealthBlock(newHealthblock);
```

</details>

### `654469937` — Fix NRE: pooled HealthPrisms lack Assembler components

_Claude, 2026-03-04 19:16:45 +0000_

```text
The original code Instantiated from the flora's healthPrism prefab
which had Assembler/GyroidAssembler/WallAssembler baked in. Pooled
HealthPrisms are generic instances without these components.

- Add EnsureAssemblerComponent() that copies the Assembler type from
  the healthPrism prefab template onto pooled instances
- Fix AssemblerFactory.ProgramAssembler to use TryGetComponent +
  AddComponent instead of bare GetComponent
- Add null guard in Flora.AddHealthBlock override
```

```text
 Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs | 42 ++++++++++++++++++++++++++++++--------
 Assets/_Scripts/Game/Environment/FloraAndFauna/Flora.cs          |  1 +
 2 files changed, 34 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 92 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
index 5dfa61d8d..a31d7bc77 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
@@ -55,22 +55,22 @@ namespace CosmicShore
         {
             public static Assembler ProgramAssembler(GameObject gameObject, GrowthInfo growthInfo)
             {
-                if (growthInfo is GyroidGrowthInfo)
+                if (growthInfo is GyroidGrowthInfo gyroidInfo)
                 {
-                    var newAssembler = gameObject.GetComponent<GyroidAssembler>();
-                    // Copy properties from growthInfo.assembler to newAssembler
-                    newAssembler.BlockType = ((GyroidGrowthInfo)growthInfo).BlockType;
+                    if (!gameObject.TryGetComponent<GyroidAssembler>(out var newAssembler))
+                        newAssembler = gameObject.AddComponent<GyroidAssembler>();
+                    newAssembler.BlockType = gyroidInfo.BlockType;
                     newAssembler.Depth = growthInfo.Depth;
-                    // Copy other properties as needed
                     return newAssembler;
                 }
-                // Add other assembler types here as needed
                 else
                 {
-                    var newAssembler = gameObject.GetComponent<Assembler>();
-                    // Copy properties from growthInfo.assembler to newAssembler
+                    if (!gameObject.TryGetComponent<Assembler>(out var newAssembler))
+                    {
+                        // Fallback: add WallAssembler as default concrete type
+                        newAssembler = gameObject.AddComponent<WallAssembler>();
+                    }
                     newAssembler.Depth = growthInfo.Depth;
-                    // Copy other properties as needed
                     return newAssembler;
                 }
             }
@@ -175,6 +175,23 @@ namespace CosmicShore
             transform.position = cellData.CrystalTransform.position + 200 * Random.onUnitSphere; // TODO: replace magic number with nucleus radius
         }
 
+        /// <summary>
+        /// Pooled HealthPrisms don't carry Assembler components — the original prefab did.
+        /// Copy the Assembler type from the healthPrism prefab template onto the pooled instance.
+        /// </summary>
+        void EnsureAssemblerComponent(GameObject go)
+        {
+            if (go.GetComponent<Assembler>()) return;
+
+            // Use the healthPrism prefab (still on LifeForm) as the template for which Assembler type to add
+            if (healthPrism && healthPrism.TryGetComponent<GyroidAssembler>(out _))
+                go.AddComponent<GyroidAssembler>();
+            else if (healthPrism && healthPrism.TryGetComponent<WallAssembler>(out _))
+                go.AddComponent<WallAssembler>();
+            else
+                go.AddComponent<WallAssembler>(); // default concrete type
+        }
+
         public Assembler CreateNewAssembler()
         {
             CSDebug.Log("New Assembler");
@@ -182,12 +199,19 @@ namespace CosmicShore
 
             HealthPrism newHealthPrism = GetHealthPrism(transform.position, transform.rotation);
             if (!newHealthPrism) return null;
+            EnsureAssemblerComponent(newHealthPrism.gameObject);
             AddHealthBlock(newHealthPrism);
             newHealthPrism.transform.SetParent(newSpindle.transform, false);
             newHealthPrism.LifeForm = this;
             newHealthPrism.Initialize();
 
             Assembler newAssembler = newHealthPrism.GetComponent<Assembler>();
+            if (!newAssembler)
+            {
+                CSDebug.LogError($"[AssembledFlora] Failed to add Assembler to pooled HealthPrism. " +
+                    $"Check that the healthPrism prefab on '{name}' has an Assembler component.", this);
+                return null;
+            }
             newAssembler.Prism = newHealthPrism;
             newAssembler.Spindle = newSpindle;
             newAssembler.Depth = depth;
```

</details>

### `75e2e8de0` — Eliminate FindAnyObjectByType spikes in flora spawn loop

_Claude, 2026-03-04 19:33:36 +0000_

```text
Profiler showed FindObjectsOfType taking 3.28ms (3 calls) every
spawn cycle inside SpawnFloraTypeLoop_Random.

- Add static cached Instance property on InteractivePrismPoolManager
  and HealthPrismPoolManager — single FindAnyObjectByType, then cached
- All consumers now use PoolType.Instance instead of per-call scene scan
- Remove CSDebug.Log("New Assembler") from hot path (3.32ms LogStringToConsole)
```

```text
 Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs                     |  2 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs           |  1 -
 Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs                     |  2 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs |  2 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs                 |  2 +-
 Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs                 |  2 +-
 Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs                  | 10 ++++++++++
 Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs             | 10 ++++++++++
 Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs                    |  2 +-
 9 files changed, 26 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 132 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs b/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
index 74c45e922..92329e827 100644
--- a/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/ShootingGalleryMiniGame.cs
@@ -20,7 +20,7 @@ namespace CosmicShore.Game.Arcade
 
             // Auto-wire pool when not set in inspector
             if (!prismPool)
-                prismPool = FindAnyObjectByType<InteractivePrismPoolManager>();
+                prismPool = InteractivePrismPoolManager.Instance;
 
             SegmentSpawner.Seed = new System.Random().Next();
         }
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
index a31d7bc77..b84829e37 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
@@ -194,7 +194,6 @@ namespace CosmicShore
 
         public Assembler CreateNewAssembler()
         {
-            CSDebug.Log("New Assembler");
             var newSpindle = AddSpindle();
 
             HealthPrism newHealthPrism = GetHealthPrism(transform.position, transform.rotation);
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
index 5224d5867..605cef365 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
@@ -67,7 +67,7 @@ public class Boid : Fauna
     {
         // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
         if (!prismPool)
-            prismPool = FindAnyObjectByType<InteractivePrismPoolManager>();
+            prismPool = InteractivePrismPoolManager.Instance;
 
         embeddedHealthPrism = GetComponentInChildren<HealthPrism>(true);
         if (!embeddedHealthPrism)
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
index 91be8f00a..e42e22d52 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs
@@ -51,7 +51,7 @@ public class BoidSImulationController : MonoBehaviour
     {
         // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
         if (!boidPrismPool)
-            boidPrismPool = FindAnyObjectByType<InteractivePrismPoolManager>();
+            boidPrismPool = InteractivePrismPoolManager.Instance;
 
         kernel = boidSimulationShader.FindKernel("CSMain");
 
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
index 1bf55cb44..8d0bdc925 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
@@ -71,7 +71,7 @@ namespace CosmicShore
 
             // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
             if (!healthPrismPool)
-                healthPrismPool = FindAnyObjectByType<HealthPrismPoolManager>();
+                healthPrismPool = HealthPrismPoolManager.Instance;
 
             if (shieldPeriod > 0)
                 StartCoroutine(ShieldRegen());
diff --git a/Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs b/Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs
index f13707a8d..2ce4bd6aa 100644
--- a/Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs
+++ b/Assets/_Scripts/Game/Environment/Spawning/SpawnableBase.cs
@@ -142,7 +142,7 @@ namespace CosmicShore.Game.Spawning
         {
             // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
             if (!prismPool)
-                prismPool = FindAnyObjectByType<InteractivePrismPoolManager>();
+                prismPool = InteractivePrismPoolManager.Instance;
 
             intensityLevel = intensity;
             trails.Clear();
diff --git a/Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs b/Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs
index 8f8183c33..eac0628dc 100644
--- a/Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs
```

</details>

### `9e73b2096` — Fix PrismEffectsManager 51ms/frame spike

_Claude, 2026-03-04 19:35:52 +0000_

```text
Profiler showed PrismEffectsManager.Update() at 49.1% self (51ms):

1. Remove FindObjectsByType<PrismExplosion> audit that ran every 60
   frames (~1s) — caused the periodic spikes. Redundant now that
   Prism class enforcement catches lifecycle violations at creation/
   destruction time.

2. Replace O(n) List.Remove with swap-and-pop in completion queues
   and Unregister methods to avoid O(n²) cost with many active effects.

3. Add ProfilerMarkers on ProcessExplosions/ProcessImplosions so
   next profile session shows where time is actually spent.
```

```text
 Assets/_Scripts/Game/Managers/PrismEffectsManager.cs | 58 +++++++++++++++++++++++++++++++++-----------------
 1 file changed, 38 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 115 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs b/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
index 957c3da50..2278f4aaf 100644
--- a/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
+++ b/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
@@ -2,6 +2,7 @@ using UnityEngine;
 using Unity.Collections;
 using Unity.Jobs;
 using Unity.Mathematics;
+using Unity.Profiling;
 using System.Collections.Generic;
 using CosmicShore.Utilities;
 
@@ -77,7 +78,11 @@ namespace CosmicShore.Game
 
         public void UnregisterExplosion(PrismExplosion explosion)
         {
-            activeExplosions.Remove(explosion);
+            int idx = activeExplosions.IndexOf(explosion);
+            if (idx < 0) return;
+            int last = activeExplosions.Count - 1;
+            activeExplosions[idx] = activeExplosions[last];
+            activeExplosions.RemoveAt(last);
         }
 
         public void RegisterImplosion(PrismImplosion implosion)
@@ -89,7 +94,11 @@ namespace CosmicShore.Game
 
         public void UnregisterImplosion(PrismImplosion implosion)
         {
-            activeImplosions.Remove(implosion);
+            int idx = activeImplosions.IndexOf(implosion);
+            if (idx < 0) return;
+            int last = activeImplosions.Count - 1;
+            activeImplosions[idx] = activeImplosions[last];
+            activeImplosions.RemoveAt(last);
         }
 
         #endregion
@@ -116,25 +125,22 @@ namespace CosmicShore.Game
 
         #endregion
 
+        static readonly ProfilerMarker s_explosionMarker = new("PrismEffects.Explosions");
+        static readonly ProfilerMarker s_implosionMarker = new("PrismEffects.Implosions");
+
         private void Update()
         {
             float dt = Time.deltaTime;
-            if (activeExplosions.Count > 0) ProcessExplosions(dt);
-            if (activeImplosions.Count > 0) ProcessImplosions(dt);
-
-#if UNITY_EDITOR || DEVELOPMENT_BUILD
-            // Safety audit: detect explosions with enabled renderers that aren't actively managed.
-            // This catches "zombie" objects that escaped the pool lifecycle.
-            if (Time.frameCount % 60 == 0) // Check once per ~second at 60fps
+            if (activeExplosions.Count > 0)
             {
-                var allExplosions = FindObjectsByType<PrismExplosion>(FindObjectsSortMode.None);
-                foreach (var exp in allExplosions)
-                {
-                    if (exp.Renderer != null && exp.Renderer.enabled && !exp.IsActive)
-                        exp.Renderer.enabled = false;
-                }
+                using (s_explosionMarker.Auto())
+                    ProcessExplosions(dt);
+            }
+            if (activeImplosions.Count > 0)
+            {
+                using (s_implosionMarker.Auto())
+                    ProcessImplosions(dt);
             }
-#endif
         }
 
         #region Explosion Processing
@@ -208,11 +214,17 @@ namespace CosmicShore.Game
                 }
             }
 
-            // Process completions after iteration to avoid list mutation during traversal
```

</details>

### `4499c7e78` — Optimize AdaptiveAnimationManager — eliminate per-frame allocations and redundant passes

_Claude, 2026-03-04 20:10:47 +0000_

```text
Profiler showed AdaptiveAnimationManager.Update() at 39.1% self (53ms)
with 37K+ colliders:

1. MaterialStateManager: Remove ToArray() allocation every frame in
   the cleanup validation loop. Use scratch list instead.

2. Both managers: Add dirty flag for activeAnimatorsList — only
   rebuild from HashSet when animators are added/removed, not every
   frame. Saves O(n) copy on frames where the set is unchanged.

3. PrismScaleManager: Remove redundant cleanup pass that iterated
   all animators checking IsScaling. Completions already remove from
   activeAnimators directly, marking dirty for next rebuild.
```

```text
 Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs | 21 +++++++++++++++++----
 Assets/_Scripts/Game/Managers/MaterialStateManager.cs     | 27 +++++++++++++--------------
 Assets/_Scripts/Game/Managers/PrismScaleManager.cs        | 21 +++------------------
 3 files changed, 33 insertions(+), 36 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 176 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
index 2391b9167..9a3e68c2b 100644
--- a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
+++ b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
@@ -25,6 +25,7 @@ namespace CosmicShore.Core
         protected readonly HashSet<TAnimator> registeredAnimators = new HashSet<TAnimator>();
         protected readonly HashSet<TAnimator> activeAnimators = new HashSet<TAnimator>();
         protected readonly List<TAnimator> activeAnimatorsList = new List<TAnimator>();
+        protected bool activeListDirty = true;
         protected NativeArray<TAnimationData> animationData;
 
         // Performance monitoring
@@ -51,7 +52,8 @@ namespace CosmicShore.Core
 
             if (IsAnimatorActive(animator))
             {
-                activeAnimators.Add(animator);
+                if (activeAnimators.Add(animator))
+                    activeListDirty = true;
                 EnsureCapacity();
             }
         }
@@ -60,20 +62,23 @@ namespace CosmicShore.Core
         {
             if (animator == null) return;
             registeredAnimators.Remove(animator);
-            activeAnimators.Remove(animator);
+            if (activeAnimators.Remove(animator))
+                activeListDirty = true;
         }
 
         protected virtual void OnAnimatorStart(TAnimator animator)
         {
             if (animator == null || !IsAnimatorValid(animator) || !registeredAnimators.Contains(animator)) return;
-            activeAnimators.Add(animator);
+            if (activeAnimators.Add(animator))
+                activeListDirty = true;
             EnsureCapacity();
         }
 
         protected virtual void OnAnimatorStop(TAnimator animator)
         {
             if (animator == null) return;
-            activeAnimators.Remove(animator);
+            if (activeAnimators.Remove(animator))
+                activeListDirty = true;
 
             // If this was the last active animator, reset our monitoring state
             if (activeAnimators.Count == 0)
@@ -189,6 +194,14 @@ namespace CosmicShore.Core
             accumulatedTime -= updateInterval * updateSteps;
         }
 
+        protected void RefreshActiveListIfDirty()
+        {
+            if (!activeListDirty) return;
+            activeAnimatorsList.Clear();
+            activeAnimatorsList.AddRange(activeAnimators);
+            activeListDirty = false;
+        }
+
         protected abstract void ProcessAnimationFrame(float deltaTime);
         protected abstract bool IsAnimatorActive(TAnimator animator);
         protected abstract bool IsAnimatorValid(TAnimator animator);
diff --git a/Assets/_Scripts/Game/Managers/MaterialStateManager.cs b/Assets/_Scripts/Game/Managers/MaterialStateManager.cs
index e790c38d9..250323a2d 100644
--- a/Assets/_Scripts/Game/Managers/MaterialStateManager.cs
+++ b/Assets/_Scripts/Game/Managers/MaterialStateManager.cs
@@ -39,9 +39,7 @@ namespace CosmicShore.Core
 
         protected override void ProcessAnimationFrame(float deltaTime)
         {
-            // Update our stable index list
-            activeAnimatorsList.Clear();
-            activeAnimatorsList.AddRange(activeAnimators);
+            RefreshActiveListIfDirty();
 
             int animatingCount = 0;
             foreach (var animator in activeAnimatorsList)
@@ -95,13 +93,8 @@ namespace CosmicShore.Core
```

</details>

### `f71289a44` — Register pool manager Instance in Awake instead of FindAnyObjectByType

_Claude, 2026-03-04 22:14:01 +0000_

```text
The static Instance getter was using FindAnyObjectByType which returned
null when called before the pool manager's Awake had run, or when the
pool manager didn't exist in the scene. Now registers s_instance = this
in Awake() — zero-cost access, no scene scan, available immediately.
```

```text
 Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs      | 11 +++++------
 Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs | 11 +++++------
 2 files changed, 10 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs b/Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs
index eac0628dc..3c3669df7 100644
--- a/Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs
+++ b/Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs
@@ -8,13 +8,12 @@ namespace CosmicShore.Game
     public class HealthPrismPoolManager : GenericPoolManager<HealthPrism>
     {
         static HealthPrismPoolManager s_instance;
-        public static HealthPrismPoolManager Instance
+        public static HealthPrismPoolManager Instance => s_instance;
+
+        protected override void Awake()
         {
-            get
-            {
-                if (!s_instance) s_instance = FindAnyObjectByType<HealthPrismPoolManager>();
-                return s_instance;
-            }
+            base.Awake();
+            s_instance = this;
         }
 
         [Header("Cleanup Events")]
diff --git a/Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs b/Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs
index a81cc0a8e..b51e62952 100644
--- a/Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs
+++ b/Assets/_Scripts/Utility/Effects/InteractivePrismPoolManager.cs
@@ -8,13 +8,12 @@ namespace CosmicShore.Game
     public class InteractivePrismPoolManager : GenericPoolManager<Prism>
     {
         static InteractivePrismPoolManager s_instance;
-        public static InteractivePrismPoolManager Instance
+        public static InteractivePrismPoolManager Instance => s_instance;
+
+        protected override void Awake()
         {
-            get
-            {
-                if (!s_instance) s_instance = FindAnyObjectByType<InteractivePrismPoolManager>();
-                return s_instance;
-            }
+            base.Awake();
+            s_instance = this;
         }
 
         [Header("Cleanup Events")]
```

</details>

### `2ab050dfb` — wiring up healthprism pool in main menu

_Garrett Milliron, 2026-03-04 18:16:42 -0500_

```text
 .../Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset   | 13560 +++++++++++++++++++++++++++++-
 Assets/_Scenes/Menu_Main.unity                                        |    55 +-
 Assets/_Scripts/Utility/Effects/HealthPrismPoolManager.cs.meta        |     2 +
 3 files changed, 13607 insertions(+), 10 deletions(-)
```

### `2e16ce796` — Optimize Spindle: replace temp materials with MaterialPropertyBlock, eliminate allocations

_Claude, 2026-03-04 23:24:48 +0000_

```text
- Replace UseTemporaryMaterial()/RestoreOriginalMaterial() (new Material() per spindle)
  with MaterialPropertyBlock.SetFloat for _DeathAnimation — zero allocations
- Cache Shader.PropertyToID("_DeathAnimation") as static readonly
- Replace spindles.ToArray() in ForceWither with static scratch list
- Remove originalMaterial/temporaryMaterial fields and System.Linq import
```

```text
 Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs | 55 ++++++++++++++++++++++++---------------------
 1 file changed, 29 insertions(+), 26 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 147 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
index 6e888e1dc..f7d9e2f36 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
@@ -1,6 +1,5 @@
 using System.Collections;
 using System.Collections.Generic;
-using System.Linq;
 using UnityEngine;
 using CosmicShore.Utility;
 
@@ -9,6 +8,7 @@ namespace CosmicShore
     public class Spindle : MonoBehaviour
     {
         private static readonly int PhaseOffsetID = Shader.PropertyToID("_Phase");
+        private static readonly int DeathAnimationID = Shader.PropertyToID("_DeathAnimation");
         private MaterialPropertyBlock propertyBlock;
 
         public Renderer RenderedObject;
@@ -19,8 +19,6 @@ namespace CosmicShore
         HashSet<HealthPrism> healthBlocks = new HashSet<HealthPrism>();
         HashSet<Spindle> spindles = new HashSet<Spindle>();
 
-        Material originalMaterial;
-        Material temporaryMaterial;
         Coroutine condenseCoroutine;
 
         bool deregistered;
@@ -65,7 +63,6 @@ namespace CosmicShore
             propertyBlock.SetFloat(PhaseOffsetID, randomOffset);
             RenderedObject.SetPropertyBlock(propertyBlock);
 
-            originalMaterial = RenderedObject.sharedMaterial;
             condenseCoroutine = StartCoroutine(CondenseCoroutine());
 
             if (LifeForm) LifeForm.AddSpindle(this);
@@ -124,19 +121,22 @@ namespace CosmicShore
                 StartCoroutine(EvaporateCoroutine());
         }
 
-        void RestoreOriginalMaterial()
+        void SetDeathAnimation(float value)
         {
-            if (RenderedObject) RenderedObject.material = originalMaterial;
-
-            if (!temporaryMaterial) return;
-            Destroy(temporaryMaterial);
-            temporaryMaterial = null;
+            if (!RenderedObject) return;
+            propertyBlock ??= new MaterialPropertyBlock();
+            RenderedObject.GetPropertyBlock(propertyBlock);
+            propertyBlock.SetFloat(DeathAnimationID, value);
+            RenderedObject.SetPropertyBlock(propertyBlock);
         }
 
-        void UseTemporaryMaterial()
+        void ClearDeathAnimation()
         {
-            temporaryMaterial = new Material(originalMaterial);
-            if (RenderedObject) RenderedObject.material = temporaryMaterial;
+            if (!RenderedObject) return;
+            propertyBlock ??= new MaterialPropertyBlock();
+            RenderedObject.GetPropertyBlock(propertyBlock);
+            propertyBlock.SetFloat(DeathAnimationID, 0f);
+            RenderedObject.SetPropertyBlock(propertyBlock);
         }
 
         IEnumerator EvaporateCoroutine()
@@ -147,21 +147,19 @@ namespace CosmicShore
                 condenseCoroutine = null;
             }
 
-            UseTemporaryMaterial();
-
             float deathAnimation = 0f;
             float animationSpeed = 1f;
             while (deathAnimation < 1f)
             {
                 yield return null;
 
```

</details>

### `ef76eae4a` — Disable Explosions vs TrailBlocks physics collision (redundant with PrismAOERegistry)

_Claude, 2026-03-04 23:40:58 +0000_

```text
ExplosionImpactor.OnTriggerEnter already skips TrailBlocks during batch
processing — all prism damage goes through PrismAOERegistry's Burst spatial
query. PhysX was doing broadphase work testing every explosion against 37K+
TrailBlock colliders, only for the callback to immediately return.
```

```text
 ProjectSettings/DynamicsManager.asset | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `1206c61bf` — Eliminate GC allocations in flora death cascade and growth loop

_Claude, 2026-03-04 23:59:32 +0000_

```text
Spindle/LifeForm CleanupDeadRefs: cache RemoveWhere predicates as
static delegates instead of allocating lambda closures on every call.

LifeForm.Die: replace healthBlocks.ToArray() and
GetComponentsInChildren<Spindle> with static scratch lists (zero alloc).

LifeForm.ShieldRegen: cache WaitForSeconds, reuse list instead of
allocating new List<HealthPrism>(healthBlocks) every cycle.

AssembledFlora.Grow: convert activeBranches from HashSet to List for
O(1) indexed access (was O(n) ElementAt per iteration = O(n²) total).
Replace per-call List<Branch> allocations with static scratch lists.
Replace LINQ FirstOrDefault in RemoveSpindle with manual loop.

Flora.GrowCoroutine: cache WaitForSeconds instances.
```

```text
 Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs | 40 ++++++++++++++++-----------
 Assets/_Scripts/Game/Environment/FloraAndFauna/Flora.cs          |  6 ++--
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs       | 56 ++++++++++++++++++++++++--------------
 Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs        |  7 +++--
 4 files changed, 69 insertions(+), 40 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 269 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
index b84829e37..e22696fb5 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
@@ -1,6 +1,5 @@
 
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Game;
 using CosmicShore.Soap;
 using UnityEngine;
@@ -46,7 +45,9 @@ namespace CosmicShore
         [SerializeField] int randomItems = 2;
         [SerializeField] float crystalGrowth = 1.01f;
 
-        HashSet<Branch> activeBranches = new HashSet<Branch>();
+        List<Branch> activeBranches = new List<Branch>();
+        static readonly List<Branch> s_newBranches = new List<Branch>(16);
+        static readonly List<int> s_removeIndices = new List<int>(16);
 
         int spawnedItemCount = 0;
         Assembler assembler;
@@ -80,14 +81,14 @@ namespace CosmicShore
         {
             if (spawnedItemCount >= maxTotalSpawnedObjects) return;
 
-            List<Branch> newBranches = new List<Branch>();
-            List<Branch> branchesToRemove = new List<Branch>();
+            s_newBranches.Clear();
+            s_removeIndices.Clear();
 
             float itemsSpawned = 0;
             int skippedItems = 0;
             for (int i = 0; i < activeBranches.Count && itemsSpawned < itemsPerGrow; i++)
             {
-                Branch branch = activeBranches.ElementAt(i);
+                Branch branch = activeBranches[i];
 
                 if (!branch.assembler || branch.depth >= maxDepth)
                 {
@@ -97,7 +98,7 @@ namespace CosmicShore
                 var growthInfo = branch.assembler.GetGrowthInfo();
                 if (!growthInfo.CanGrow)
                 {
-                    branchesToRemove.Add(branch);
+                    s_removeIndices.Add(i);
                     continue;
                 }
 
@@ -130,26 +131,27 @@ namespace CosmicShore
                 newHealthPrism.transform.localRotation = Quaternion.identity;
                 if (growthInfo.IsDangerous) newHealthPrism.MakeDangerous();
                 newHealthPrism.Initialize();
-                
+
                 newBranch.gameObject = newSpindle.gameObject;
                 newBranch.assembler = newAssembler;
                 newBranch.depth = branch.depth + 1;
 
-                newBranches.Add(newBranch);
+                s_newBranches.Add(newBranch);
                 itemsSpawned++;
 
                 if (branch.depth >= maxDepth - 1 || branch.assembler.IsFullyBonded())
                 {
-                    branchesToRemove.Add(branch);
+                    s_removeIndices.Add(i);
                 }
             }
 
-            foreach (Branch branch in branchesToRemove)
-            {
-                activeBranches.Remove(branch);               
-            }
+            // Remove in reverse order to preserve indices
+            for (int i = s_removeIndices.Count - 1; i >= 0; i--)
+                activeBranches.RemoveAt(s_removeIndices[i]);
 
-            activeBranches.UnionWith(newBranches);
+            activeBranches.AddRange(s_newBranches);
```

</details>

### `4f536d9af` — Add SpindlePoolManager to eliminate Instantiate/Destroy in flora growth

_Claude, 2026-03-05 00:08:22 +0000_

```text
New SpindlePoolManager (singleton, GenericPoolManager<Spindle>) pools
spindle GameObjects instead of Instantiate/Destroy per growth cycle.

Spindle changes:
- Add InitializeFromPool() for re-initialization on pool Get
- Add ResetForPool() to clear all state on pool Release
- Add ReturnToPool() replacing Destroy(gameObject) in EvaporateCoroutine
- Graceful fallback to Instantiate/Destroy when no pool is present

AssembledFlora/BranchingFlora/LifeForm:
- All Instantiate(spindle, ...) calls now go through SpindlePoolManager
- BranchingFlora: convert HashSet<Branch> to List, eliminate per-Grow
  List allocations with static scratch lists, remove LINQ First()
```

```text
 Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs | 17 +++++++--
 Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs | 60 +++++++++++++++++------------
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs       | 10 ++++-
 Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs        | 77 +++++++++++++++++++++++++++++++++++++-
 Assets/_Scripts/Utility/Effects/SpindlePoolManager.cs            | 55 +++++++++++++++++++++++++++
 5 files changed, 189 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 371 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
index e22696fb5..9e1dac7f3 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
@@ -121,10 +121,21 @@ namespace CosmicShore
                     continue;
                 }
 
-                Spindle newSpindle = Instantiate(spindle, branch.gameObject.transform);
+                Spindle newSpindle;
+                if (SpindlePoolManager.Instance)
+                {
+                    newSpindle = SpindlePoolManager.Instance.Get(
+                        newHealthPrism.transform.position,
+                        newHealthPrism.transform.rotation,
+                        branch.gameObject.transform);
+                }
+                else
+                {
+                    newSpindle = Instantiate(spindle, branch.gameObject.transform);
+                    newSpindle.transform.position = newHealthPrism.transform.position;
+                    newSpindle.transform.rotation = newHealthPrism.transform.rotation;
+                }
                 newSpindle.LifeForm = this;
-                newSpindle.transform.position = newHealthPrism.transform.position;
-                newSpindle.transform.rotation = newHealthPrism.transform.rotation;
 
                 newHealthPrism.transform.SetParent(newSpindle.transform, false);
                 newHealthPrism.transform.localPosition = Vector3.zero;
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
index a0f66c5c3..5d68929de 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
@@ -1,6 +1,5 @@
 using System.Collections;
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Game;
 using CosmicShore.Utility;
 using UnityEngine;
@@ -33,7 +32,9 @@ namespace CosmicShore
         [SerializeField] float branchingScaleFactor = 14f;
         public Vector3 goal;
 
-        HashSet<Branch> activeBranches = new HashSet<Branch>();
+        List<Branch> activeBranches = new List<Branch>();
+        static readonly List<Branch> s_newBranches = new List<Branch>(16);
+        static readonly List<int> s_removeIndices = new List<int>(16);
 
         [SerializeField] float plantRadius = 75f;
         [SerializeField] float noLeafFailsafeSeconds = 8f;
@@ -64,13 +65,13 @@ namespace CosmicShore
             if (noLeafFailsafeRoutine != null) StopCoroutine(noLeafFailsafeRoutine);
             noLeafFailsafeRoutine = StartCoroutine(KillIfStillNoLeaves(noLeafFailsafeSeconds));
         }
-        
+
         void SpawnOneLeafOnAnyTrunk()
         {
             if (activeBranches.Count == 0) return;
 
             // pick any trunk
-            var trunk = activeBranches.First();
+            var trunk = activeBranches[0];
 
             var hp = GetHealthPrism(
                 trunk.gameObject.transform.position + (branchingScaleFactor * trunk.gameObject.transform.forward),
@@ -98,17 +99,25 @@ namespace CosmicShore
             CSDebug.LogWarning($"{name}: BranchingFlora had no HealthPrisms after {seconds}s. Auto-dying.");
             Die();
         }
+
+        Spindle GetOrCreateSpindle(Vector3 position, Quaternion rotation, Transform parent = null)
+        {
+            if (SpindlePoolManager.Instance)
+                return SpindlePoolManager.Instance.Get(position, rotation, parent);
+            return Instantiate(spindle, position, rotation, parent);
+        }
+
         void SeedBranches()
```

</details>

### `81fb1b0b6` — Fix ambiguous Random reference in Spindle

_Claude, 2026-03-05 00:14:54 +0000_

```text
Use UnityEngine.Random.Range() to disambiguate from System.Random
```

```text
 Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
index fdeef319f..e89737858 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
@@ -69,7 +69,7 @@ namespace CosmicShore
             startedOnce = true;
             propertyBlock = new MaterialPropertyBlock();
 
-            float randomOffset = Random.Range(0f, Mathf.PI * 2f);
+            float randomOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
             RenderedObject.GetPropertyBlock(propertyBlock);
             propertyBlock.SetFloat(PhaseOffsetID, randomOffset);
             RenderedObject.SetPropertyBlock(propertyBlock);
@@ -96,7 +96,7 @@ namespace CosmicShore
 
             propertyBlock ??= new MaterialPropertyBlock();
 
-            float randomOffset = Random.Range(0f, Mathf.PI * 2f);
+            float randomOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
             RenderedObject.GetPropertyBlock(propertyBlock);
             propertyBlock.SetFloat(PhaseOffsetID, randomOffset);
             RenderedObject.SetPropertyBlock(propertyBlock);
```

</details>

### `96204ab79` — Eliminate Spindle coroutine overhead and death cascade spikes

_Claude, 2026-03-05 01:20:12 +0000_

```text
Three major optimizations targeting the 367ms frame spike:

1. Replace all per-spindle coroutines with a single batched Update loop
   - EvaporateCoroutine (501 calls, 116ms) → one static TickAllAnimations()
   - CondenseCoroutine → same batched treatment
   - SpindleAnimDriver auto-created via [RuntimeInitializeOnLoadMethod]

2. Defer CheckForLife cascade to LateUpdate
   - OnDisable cascade (163 calls, 53ms) → deferred to FlushPendingLifeChecks
   - Deduplicated via HashSet to avoid re-checking same spindle
   - Capped at 4 cascade iterations per frame

3. Cap simultaneous animations and skip off-screen updates
   - MaxAnimatedEvaporations = 48: excess deaths skip animation entirely
   - MaxAnimatedCondensations = 64: excess spawns skip grow-in animation
   - Visibility check: only touch MaterialPropertyBlock for on-screen spindles
```

```text
 Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs | 300 ++++++++++++++++++++++++++++++++------------
 1 file changed, 217 insertions(+), 83 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 481 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
index e89737858..8f85bbc35 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs
@@ -1,5 +1,4 @@
 using System;
-using System.Collections;
 using System.Collections.Generic;
 using UnityEngine;
 using CosmicShore.Game;
@@ -21,8 +20,6 @@ namespace CosmicShore
         HashSet<HealthPrism> healthBlocks = new HashSet<HealthPrism>();
         HashSet<Spindle> spindles = new HashSet<Spindle>();
 
-        Coroutine condenseCoroutine;
-
         bool deregistered;
         bool dying = false;
 
@@ -32,10 +29,131 @@ namespace CosmicShore
         bool isPooled;
         bool startedOnce;
 
+        // Per-instance animation state (driven by static tick)
+        float animProgress;
+        bool isEvaporating;
+        bool isCondensing;
+
         public event Action<Spindle> OnReturnToPool;
 
-        static readonly System.Predicate<HealthPrism> s_deadHealthPrism = h => !h;
-        static readonly System.Predicate<Spindle> s_deadSpindle = s => !s;
+        static readonly Predicate<HealthPrism> s_deadHealthPrism = h => !h;
+        static readonly Predicate<Spindle> s_deadSpindle = s => !s;
+
+        // ──────────────── Batched animation system ────────────────
+        // Replaces per-spindle coroutines with a single Update pass.
+
+        // When more than this many spindles are evaporating, new deaths
+        // skip animation and clean up immediately. Players can't track
+        // 50+ individual fade-outs anyway.
+        const int MaxAnimatedEvaporations = 48;
+        const int MaxAnimatedCondensations = 64;
+
+        static readonly List<Spindle> s_evaporating = new List<Spindle>(256);
+        static readonly List<Spindle> s_condensing = new List<Spindle>(256);
+        static readonly List<Spindle> s_pendingLifeCheck = new List<Spindle>(64);
+        static readonly HashSet<Spindle> s_pendingLifeCheckSet = new HashSet<Spindle>();
+        static bool s_driverInstalled;
+
+        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
+        static void InstallDriver()
+        {
+            if (s_driverInstalled) return;
+            s_driverInstalled = true;
+            var go = new GameObject("[SpindleAnimDriver]");
+            go.hideFlags = HideFlags.HideAndDontSave;
+            UnityEngine.Object.DontDestroyOnLoad(go);
+            go.AddComponent<SpindleAnimDriver>();
+        }
+
+        /// <summary>
+        /// Single-pass update for all evaporating and condensing spindles.
+        /// Called once per frame by SpindleAnimDriver.
+        /// </summary>
+        internal static void TickAllAnimations()
+        {
+            float dt = Time.deltaTime;
+
+            // Tick evaporating (iterate backwards for safe removal)
+            for (int i = s_evaporating.Count - 1; i >= 0; i--)
+            {
+                var s = s_evaporating[i];
+                if (!s || !s.isEvaporating)
+                {
+                    SwapRemove(s_evaporating, i);
+                    continue;
+                }
+
+                s.animProgress += dt;
```

</details>

### `833910049` — Fix AdaptiveAnimationManager throttle never activating + add visibility culling

_Claude, 2026-03-05 01:55:07 +0000_

```text
Bug: UpdateFrameInterval was only called from EnsureCapacity (during
capacity growth), never from Update(). So currentFrameInterval stayed
stuck at 1 forever — no adaptive throttling ever kicked in. This caused
both PrismScaleManager and MaterialStateManager to process ALL animators
EVERY frame, producing the 90ms spikes visible in the profiler.

Fixes:
- Call UpdateFrameInterval from Update() every 0.25s so the throttle
  actually adapts to frame pressure
- Ramp up aggressively when under pressure (halve the gap each tick)
  instead of ±1 per 0.5s check — reaches target interval in <1s
- Ease down slowly (±1) to avoid oscillation

PrismScaleManager visibility culling:
- Skip off-screen prisms (renderer.isVisible check) to avoid
  unnecessary Transform reads/writes in ProcessAnimationFrame
- Expose MeshRenderer on PrismScaleAnimator for the check
```

```text
 Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs |  2 ++
 Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs     | 32 +++++++++++++++-----------------
 Assets/_Scripts/Game/Managers/PrismScaleManager.cs            |  5 +++++
 3 files changed, 22 insertions(+), 17 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 112 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
index 86ee2f4d5..7a505ea58 100644
--- a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
+++ b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
@@ -28,6 +28,8 @@ namespace CosmicShore.Core
         private MeshRenderer meshRenderer;
         private bool isRegistered;
 
+        public MeshRenderer MeshRenderer => meshRenderer;
+
         private Vector3 prefabAuthoredScale;
 
         private bool isScaling;
diff --git a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
index 9a3e68c2b..3cf1d7107 100644
--- a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
+++ b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
@@ -122,7 +122,7 @@ namespace CosmicShore.Core
                 return;
             }
 
-            if (Time.realtimeSinceStartup - lastIntervalUpdateTime < 0.5f)
+            if (Time.realtimeSinceStartup - lastIntervalUpdateTime < 0.25f)
                 return;
 
             lastIntervalUpdateTime = Time.realtimeSinceStartup;
@@ -132,38 +132,34 @@ namespace CosmicShore.Core
                 frameTimeHistory.Dequeue();
 
             float avgFrameTime = 0f;
-            float maxFrameTime = 0f;
             foreach (float frameTime in frameTimeHistory)
-            {
                 avgFrameTime += frameTime;
-                maxFrameTime = Mathf.Max(maxFrameTime, frameTime);
-            }
             avgFrameTime /= frameTimeHistory.Count;
 
-            // More aggressive capacity scaling
-            float capacityFactor = capacity / 50f; // Start scaling earlier and more aggressively
-
             // Scale based on both capacity and frame time pressure
+            float capacityFactor = capacity / 50f;
             float performancePressure = avgFrameTime / TARGET_FRAME_TIME;
-            performancePressure = Mathf.Pow(performancePressure, 1.5f); // Exponential scaling for performance pressure
+            performancePressure = Mathf.Pow(performancePressure, 1.5f);
 
-            // Higher baseline interval for large numbers of objects
             float baseInterval = Mathf.Max(BASE_FRAME_INTERVAL, capacityFactor);
-
-            // Combine factors multiplicatively instead of weighted average
             float scaleFactor = baseInterval * (1f + performancePressure);
 
-            // Calculate new interval with smoother clamping
             int newInterval = Mathf.Clamp(
                 Mathf.RoundToInt(scaleFactor),
                 Mathf.Max(BASE_FRAME_INTERVAL, Mathf.FloorToInt(capacityFactor)),
                 MAX_FRAME_INTERVAL
             );
 
-            // Smooth transition to new interval
-            if (newInterval != currentFrameInterval)
+            // Respond quickly when under pressure (jump up fast, ease down slowly)
+            if (newInterval > currentFrameInterval)
             {
-                currentFrameInterval += (newInterval > currentFrameInterval) ? 1 : -1;
+                // Under pressure — jump up aggressively (halve the gap each tick)
+                currentFrameInterval += Mathf.Max(1, (newInterval - currentFrameInterval + 1) / 2);
+                currentFrameInterval = Mathf.Min(currentFrameInterval, MAX_FRAME_INTERVAL);
+            }
+            else if (newInterval < currentFrameInterval)
+            {
+                currentFrameInterval -= 1; // ease down slowly
             }
         }
 
@@ -172,11 +168,13 @@ namespace CosmicShore.Core
             // Early exit if nothing is animating
             if (activeAnimators.Count == 0)
             {
```

</details>

### `00333f725` — wire up spindle pool manager

_Garrett Milliron, 2026-03-04 22:40:49 -0500_

```text
 Assets/_Prefabs/HealthPrismsPoolManager.prefab             |  55 ++++++++++++++
 Assets/_Prefabs/HealthPrismsPoolManager.prefab.meta        |   7 ++
 Assets/_Scenes/Menu_Main.unity                             | 177 ++++++++++++++++++++++++++++++-------------
 Assets/_Scripts/Utility/Effects/SpindlePoolManager.cs.meta |   2 +
 4 files changed, 187 insertions(+), 54 deletions(-)
```

### `6990a01c3` — Fix O(N²) scene teardown and stale singleton refs (cherry-pick from optimize-scene-load-times)

_Claude, 2026-03-05 04:58:00 +0000_

```text
Cherry-picked from claude/optimize-scene-load-times-5sopf (323bf09):
- Singleton<T>.OnDestroy clears Instance to prevent stale refs across scene transitions
- PrismTimerManager: swap-remove O(1) + _disposing flag prevents O(N²) teardown
- GenericPoolManager: cap sync prewarm to 8 objects, defer rest to maintenance loop
- GameManager: use LoadSceneAsync for non-blocking transitions
- Remove noisy print() calls from Singleton Awake methods
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

<details><summary>Patch (code/doc/text files, first 80 of 458 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
index 3cf1d7107..3e51f941d 100644
--- a/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
+++ b/Assets/_Scripts/Game/Managers/AdaptiveAnimationManager.cs
@@ -209,9 +209,10 @@ namespace CosmicShore.Core
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
index 2278f4aaf..241a31ce4 100644
--- a/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
+++ b/Assets/_Scripts/Game/Managers/PrismEffectsManager.cs
@@ -321,7 +321,7 @@ namespace CosmicShore.Game
             activeImplosions.Clear();
         }
 
-        private void OnDestroy()
+        protected override void OnDestroy()
         {
             if (explosionJobData.IsCreated) explosionJobData.Dispose();
             if (implosionJobData.IsCreated) implosionJobData.Dispose();
@@ -329,6 +329,7 @@ namespace CosmicShore.Game
             activeImplosions.Clear();
             tempExplosionList.Clear();
             tempImplosionList.Clear();
+            base.OnDestroy();
         }
 
         #endregion
diff --git a/Assets/_Scripts/Game/Managers/PrismTimerManager.cs b/Assets/_Scripts/Game/Managers/PrismTimerManager.cs
```

</details>

### `384f83589` — Replace per-prism coroutine with centralized PrismActivationQueue (cherry-pick from optimize-scene-load-times)

_Claude, 2026-03-05 04:58:00 +0000_

```text
Cherry-picked from claude/optimize-scene-load-times-5sopf (59aefd3):

Profiler showed 49,836 CreateBlockCoroutine coroutines all expiring on the same
frame (thundering herd from WaitForSeconds(0.6)). This caused a multi-second
stall and 10+ MB GC alloc.

Fix: PrismActivationQueue singleton replaces per-prism coroutines.
- Prism.Initialize() calls Enqueue(prism, scale, delay) instead of StartCoroutine
- Queue processes up to 200 prisms per Update, spreading 50K activations across
  ~250 frames instead of 1
- Eliminates 50K coroutine heap allocations and WaitForSeconds objects
- Prism.ResetState() cancels pending activations via swap-remove
```

```text
 Assets/_Scripts/Game/Managers/PrismActivationQueue.cs | 116 ++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Ship/Prism.cs                    |  39 +++++++---------
 2 files changed, 133 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 214 lines)</summary>

```diff
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
+        // Sorted by ActivateAtTime so we always process the earliest first
+        private readonly List<PendingActivation> _queue = new(256);
+
+        /// <summary>
+        /// Queue a prism for deferred activation. Replaces StartCoroutine(CreateBlockCoroutine).
+        /// </summary>
+        public void Enqueue(Prism prism, Vector3 authoredTargetScale, float delay)
+        {
+            if (prism == null) return;
+
+            _queue.Add(new PendingActivation
+            {
+                Prism = prism,
+                AuthoredTargetScale = authoredTargetScale,
+                ActivateAtTime = Time.time + delay
+            });
+        }
+
+        /// <summary>
+        /// Remove all pending activations for a specific prism (e.g. when returned to pool).
+        /// Uses swap-remove for O(1) per removal.
+        /// </summary>
+        public void Cancel(Prism prism)
+        {
+            for (int i = _queue.Count - 1; i >= 0; i--)
+            {
+                if (_queue[i].Prism == prism)
+                {
+                    int last = _queue.Count - 1;
+                    if (i != last) _queue[i] = _queue[last];
+                    _queue.RemoveAt(last);
+                }
+            }
+        }
+
+        private void Update()
+        {
+            if (_queue.Count == 0) return;
+
+            float now = Time.time;
+            int activated = 0;
+
+            for (int i = _queue.Count - 1; i >= 0 && activated < maxActivationsPerFrame; i--)
+            {
+                var entry = _queue[i];
```

</details>

### `cd9de82e1` — Include intensity/domain/seed in spawnable cache key (cherry-pick from add-spawnable-caching)

_Claude, 2026-03-05 04:58:00 +0000_

```text
Cherry-picked from claude/add-spawnable-caching-wg2zr (0b4b69e):

SpawnableBase.GetTrailData() now uses HashCode.Combine(subclassHash,
intensityLevel, domain, seed) so the cache correctly invalidates when
intensity, domain, or seed changes. Subclass GetParameterHash() no
longer needs to include these base-class fields.

All ~60 spawnable subclasses updated to remove redundant seed/intensity
from their hash calculations.
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

<details><summary>Patch (code/doc/text files, first 80 of 781 lines)</summary>

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
 
```

</details>

### `d0248949b` — Defer prism colliders until growth completes + optimize physics settings

_Claude, 2026-03-05 06:55:08 +0000_

```text
Root cause: ExecuteDeferredActivation enabled BoxColliders immediately,
then PrismScaleAnimator grew prisms over ~1s. Every frame, each growing
prism's Transform.localScale change forced PhysX to recompute its AABB.
With 200 prisms activating/frame and growth taking ~60 frames, thousands
of colliders were simultaneously churning the broadphase — causing
PxScene.simulate to spike to 164ms.

Fix: collider stays disabled during growth. PrismScaleAnimator calls
Prism.EnableCollider() once in ExecuteOnScaleComplete, inserting each
collider into the broadphase exactly once at its final size.

Physics settings tuned for large trigger-heavy scenes:
- Broadphase: SAP → MBP (better for many static/sleeping objects)
- Sleep threshold: 0.005 → 0.05 (rigidbodies sleep sooner)
- Solver iterations: 6 → 4 (triggers don't need high fidelity)
- World bounds: 256 → 512 (match actual play volume)
```

```text
 Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs |  3 +++
 Assets/_Scripts/Game/Ship/Prism.cs                            | 14 +++++++++++++-
 ProjectSettings/DynamicsManager.asset                         |  8 ++++----
 3 files changed, 20 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
index 7a505ea58..41536438c 100644
--- a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
+++ b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
@@ -125,6 +125,9 @@ namespace CosmicShore.Core
 
         public void ExecuteOnScaleComplete()
         {
+            // Enable the collider now that scaling is done — avoids AABB churn during growth
+            prism.EnableCollider();
+
             var deltaVolume = UpdateVolume();
             onPrismVolumeModified.Raise(new PrismStats
             {
diff --git a/Assets/_Scripts/Game/Ship/Prism.cs b/Assets/_Scripts/Game/Ship/Prism.cs
index 06580c6fc..3f3bc7c4b 100644
--- a/Assets/_Scripts/Game/Ship/Prism.cs
+++ b/Assets/_Scripts/Game/Ship/Prism.cs
@@ -208,6 +208,15 @@ namespace CosmicShore.Core
             if (meshRenderer) meshRenderer.enabled = false;
         }
 
+        /// <summary>
+        /// Enables the BoxCollider. Called by PrismScaleAnimator.ExecuteOnScaleComplete()
+        /// after growth finishes, so PhysX doesn't pay AABB-rebuild costs during growth.
+        /// </summary>
+        internal void EnableCollider()
+        {
+            if (blockCollider) blockCollider.enabled = true;
+        }
+
         /// <summary>
         /// Public method to immediately return this instance to the pool.
         /// </summary>
@@ -236,7 +245,10 @@ namespace CosmicShore.Core
         internal void ExecuteDeferredActivation(Vector3 authoredTargetScale)
         {
             meshRenderer.enabled = true;
-            blockCollider.enabled = true;
+            // Collider is NOT enabled here — it stays off while the prism grows.
+            // PrismScaleAnimator.ExecuteOnScaleComplete() calls EnableCollider()
+            // once growth finishes. This prevents thousands of simultaneously-growing
+            // prisms from forcing PhysX to recompute AABBs every fixed step.
 
             if (scaleAnimator.TargetScale == Vector3.zero)
                 scaleAnimator.SetTargetScale(authoredTargetScale);
```

</details>

### `31dffc045` — Fix ClearPrisms moving static collider + material instance leak

_Claude, 2026-03-05 06:57:31 +0000_

```text
ClearPrisms creates a CapsuleCollider and moves it every frame (tracking
camera-to-ship line). Without a Rigidbody, PhysX treats this as a static
collider — moving it forces a full static-tree rebuild each frame,
contributing to the sustained 100ms+ PhysX.FinalizeUpdateTask cost even
after all prisms finish growing.

Fix: add kinematic Rigidbody so PhysX handles it as a dynamic object
with efficient broadphase updates.

Also fix OnTriggerStay which called renderer.material.SetFloat() — this
creates a unique material instance per prism, destroying batching and
leaking materials. Replaced with MaterialPropertyBlock which modifies
per-renderer properties without instancing the material.
```

```text
 Assets/_Scripts/Game/Ship/ClearPrisms.cs | 29 +++++++++++++++++++++++------
 1 file changed, 23 insertions(+), 6 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/ClearPrisms.cs b/Assets/_Scripts/Game/Ship/ClearPrisms.cs
index 366dd21dd..8faf5579b 100644
--- a/Assets/_Scripts/Game/Ship/ClearPrisms.cs
+++ b/Assets/_Scripts/Game/Ship/ClearPrisms.cs
@@ -24,9 +24,12 @@ namespace CosmicShore
 
         CameraManager cameraManager;
         GeometryUtils.LineData lineData;
-        
+
         bool isInitialized;
 
+        // Reusable MaterialPropertyBlock to avoid creating material instances
+        private static readonly int AlphaID = Shader.PropertyToID("_Alpha");
+        private MaterialPropertyBlock _mpb;
 
         private void OnEnable()
         {
@@ -59,13 +62,22 @@ namespace CosmicShore
                 CSDebug.LogError("Close main camera not found! This should not happen!");
                 return;
             }
-            
+
             visibilityCapsuleTransform = new GameObject("Visibility Capsule").transform;
             transform.SetParent(visibilityCapsuleTransform);
+
+            // Kinematic Rigidbody so PhysX treats this as a dynamic object.
+            // Without it, moving the capsule every frame forces a full static-tree rebuild.
+            var rb = gameObject.AddComponent<Rigidbody>();
+            rb.isKinematic = true;
+            rb.useGravity = false;
+
             visibilityCapsule = gameObject.AddComponent<CapsuleCollider>();
             visibilityCapsule.isTrigger = true;
             visibilityCapsule.radius = capsuleRadius;
 
+            _mpb = new MaterialPropertyBlock();
+
             isInitialized = true;
         }
 
@@ -101,9 +113,14 @@ namespace CosmicShore
 
         private void OnTriggerStay(Collider other)
         {
-            Renderer renderer = other.GetComponent<Renderer>();
-            if (renderer != null)
-                renderer.material.SetFloat("_Alpha", scaleCurve.Evaluate(GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData)/ capsuleRadius));
+            if (!other.TryGetComponent<Renderer>(out var renderer)) return;
+
+            // Use MaterialPropertyBlock to set alpha without creating per-prism material instances.
+            // renderer.material creates a copy, breaking static batching and leaking materials.
+            renderer.GetPropertyBlock(_mpb);
+            _mpb.SetFloat(AlphaID, scaleCurve.Evaluate(
+                GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData) / capsuleRadius));
+            renderer.SetPropertyBlock(_mpb);
         }
 
         void OnTriggerExit(Collider other)
@@ -113,4 +130,4 @@ namespace CosmicShore
                 prism.SetTransparency(false);
         }
     }
-}
\ No newline at end of file
+}
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
