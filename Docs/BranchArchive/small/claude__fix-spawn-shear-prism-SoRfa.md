# Branch archive: `claude/fix-spawn-shear-prism-SoRfa`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-25 by Claude
- **Unmerged commits:** 3
- **Forked from:** `c68f11548` (2026-02-24, Update MinigameHexRace.unity)
- **Tip:** `8750e713d`
- **Files touched (14):**
  - `Assets/_Prefabs/Spawnables/SpawnablePumpkin 1.prefab`
  - `Assets/_Prefabs/Spawnables/SpawnablePumpkin 1.prefab.meta`
  - `Assets/_Prefabs/Spawnables/SpawnablePumpkin.prefab`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableEllipsoid.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnablePumpkin.cs`
  - `Assets/_Scripts/Game/Projectiles/AOEBlockCreation.cs`
  - `Assets/_Scripts/Game/Projectiles/AOERadialBlocks.cs`
  - `Assets/_Scripts/Game/Ship/Prism.cs`
  - `Assets/_Scripts/Game/Ship/VesselPrismController.cs`
  - `Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs`
  - `Assets/_Scripts/Utility/FireTrailBlockActionSO.cs`
  - `Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs`

### `7f9466170` — Fix prism shearing from hierarchy transform decomposition; add BlockState-based spawning

_Claude, 2026-02-25 05:16:58 +0000_

```text
Prevent non-rectilinear (sheared) prisms caused by non-uniform scale combined
with rotation at different levels of the parent-child hierarchy. Prisms now
always match their BoxColliders.

GenericPoolManager: reparent with worldPositionStays=false and reset localScale
to identity in Get_(), Release_(), and ReleaseAllActiveAsync() so transform
decomposition never introduces shear artifacts across pool cycles.

Prism.Initialize: reset localScale to zero on every pool reuse (matching Awake
behavior) and add IsSuperShielded state support.

All spawners (VesselPrismController, AOEBlockCreation, AOERadialBlocks,
FireTrailBlockActionSO/Executor) replace bool shielded with BlockState
initialBlockState so prisms can be spawned as Normal, Shielded, SuperShielded,
or Dangerous for any domain.
```

```text
 Assets/_Scripts/Game/Projectiles/AOEBlockCreation.cs          | 18 ++++++++++++++----
 Assets/_Scripts/Game/Projectiles/AOERadialBlocks.cs           | 16 +++++++++++++---
 Assets/_Scripts/Game/Ship/Prism.cs                            |  9 +++++++--
 Assets/_Scripts/Game/Ship/VesselPrismController.cs            | 28 +++++++++++++++++++++-------
 Assets/_Scripts/Utility/FireTrailBlockActionExecutor.cs       | 19 ++++++++++++++++---
 Assets/_Scripts/Utility/FireTrailBlockActionSO.cs             |  9 +++++----
 Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs | 27 ++++++++++++++++++++-------
 7 files changed, 96 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 277 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Projectiles/AOEBlockCreation.cs b/Assets/_Scripts/Game/Projectiles/AOEBlockCreation.cs
index 6ad9c1978..1c3deab1f 100644
--- a/Assets/_Scripts/Game/Projectiles/AOEBlockCreation.cs
+++ b/Assets/_Scripts/Game/Projectiles/AOEBlockCreation.cs
@@ -14,7 +14,7 @@ namespace CosmicShore.Game.Projectiles
     {
         [Header("Block Creation")]
         [SerializeField] protected Vector3 blockScale = new Vector3(20f, 10f, 5f);
-        [SerializeField] protected bool shielded = true;
+        [SerializeField] protected BlockState initialBlockState = BlockState.Shielded;
 
         [Header("Events")]
         [SerializeField] private PrismEventChannelWithReturnSO _prismSpawnEvent;
@@ -133,9 +133,19 @@ namespace CosmicShore.Game.Projectiles
             block.ownerID = OwnerIdBase + ownerSuffix + position;
             block.TargetScale = blockScale;
 
-            if (shielded)
-                block.prismProperties.IsShielded = true;
-            
+            switch (initialBlockState)
+            {
+                case BlockState.Shielded:
+                    block.prismProperties.IsShielded = true;
+                    break;
+                case BlockState.SuperShielded:
+                    block.prismProperties.IsSuperShielded = true;
+                    break;
+                case BlockState.Dangerous:
+                    block.prismProperties.IsDangerous = true;
+                    break;
+            }
+
             block.Initialize(Vessel?.VesselStatus?.PlayerName ?? "UnknownPlayer");
             trail.Add(block);
             return block;
diff --git a/Assets/_Scripts/Game/Projectiles/AOERadialBlocks.cs b/Assets/_Scripts/Game/Projectiles/AOERadialBlocks.cs
index bf9d85c19..3e7c158af 100644
--- a/Assets/_Scripts/Game/Projectiles/AOERadialBlocks.cs
+++ b/Assets/_Scripts/Game/Projectiles/AOERadialBlocks.cs
@@ -23,7 +23,7 @@ namespace CosmicShore.Game.Projectiles
         #region Block Creation
         [Header("Block Creation")]
         [SerializeField] private Vector3 baseBlockScale = new Vector3(10f, 5f, 5f);
-        [SerializeField] private bool shielded = true;
+        [SerializeField] private BlockState initialBlockState = BlockState.Shielded;
         #endregion
 
         #region Explosion Parameters
@@ -169,8 +169,18 @@ namespace CosmicShore.Game.Projectiles
             prism.ownerID = OwnerIdBase + blockId + position;
             prism.Domain = Domain;
 
-            if (shielded)
-                prism.prismProperties.IsShielded = true;
+            switch (initialBlockState)
+            {
+                case BlockState.Shielded:
+                    prism.prismProperties.IsShielded = true;
+                    break;
+                case BlockState.SuperShielded:
+                    prism.prismProperties.IsSuperShielded = true;
+                    break;
+                case BlockState.Dangerous:
+                    prism.prismProperties.IsDangerous = true;
+                    break;
+            }
 
             // Start at zero scale
             prism.transform.localScale = Vector3.zero;
diff --git a/Assets/_Scripts/Game/Ship/Prism.cs b/Assets/_Scripts/Game/Ship/Prism.cs
index fd1172ad4..8eee000d7 100644
--- a/Assets/_Scripts/Game/Ship/Prism.cs
+++ b/Assets/_Scripts/Game/Ship/Prism.cs
@@ -124,13 +124,18 @@ namespace CosmicShore.Core
 
             var authoredTargetScale = scaleAnimator ? scaleAnimator.TargetScale : transform.localScale;
             if (authoredTargetScale == Vector3.zero)
-                authoredTargetScale = transform.localScale;
+                authoredTargetScale = scaleAnimator ? scaleAnimator.AuthoredTargetScale : Vector3.one;
+
+            // Reset to zero so prisms grow from nothing — prevents any shearing
+            // artifacts that may have accumulated during pool reparenting.
+            transform.localScale = Vector3.zero;
 
             scaleAnimator.Initialize();
             scaleAnimator.SetTargetScale(authoredTargetScale);
             StartCoroutine(CreateBlockCoroutine(authoredTargetScale));
 
-            if (prismProperties.IsShielded) ActivateShield();
+            if (prismProperties.IsSuperShielded) ActivateSuperShield();
+            else if (prismProperties.IsShielded) ActivateShield();
             if (prismProperties.IsDangerous) MakeDangerous();
         }
 
diff --git a/Assets/_Scripts/Game/Ship/VesselPrismController.cs b/Assets/_Scripts/Game/Ship/VesselPrismController.cs
index 3dcd03c41..94349fbbc 100644
--- a/Assets/_Scripts/Game/Ship/VesselPrismController.cs
+++ b/Assets/_Scripts/Game/Ship/VesselPrismController.cs
@@ -37,7 +37,10 @@ namespace CosmicShore.Game
 
         [Header("Runtime Toggles")]
         [SerializeField] bool waitTillOutsideSkimmer = true;
-        [SerializeField] bool shielded = false;
+
+        [Header("Initial Prism State")]
+        [Tooltip("State applied to every spawned prism (Shielded, SuperShielded, Dangerous, or Normal). Danger mode override still applies on top.")]
+        [SerializeField] BlockState initialBlockState = BlockState.Normal;
 
         [Header("Gap Settings")]
         public float offset;
@@ -74,6 +77,7 @@ namespace CosmicShore.Game
         public float MinWaveLength => minWavelength;
         public ushort TrailLength => (ushort)Trail.TrailList.Count;
         public float TrailZScale => BaseScale.z; // <- from BaseScale now
+        public BlockState InitialBlockState { get => initialBlockState; set => initialBlockState = value; }
         public event Action<Prism> OnBlockSpawned;
         /// <summary>Static event: fired each time a danger block is created during overheat. Param = owner player name.</summary>
         public static event Action<string> OnDangerBlockCreated;
@@ -236,9 +240,24 @@ namespace CosmicShore.Game
                 ? (skimmer.transform.localScale.z + TrailZScale) / vesselStatus.Speed
                 : waitTime;
 
+            // Apply configured initial block state (shielded, super-shielded, dangerous)
+            switch (initialBlockState)
+            {
+                case BlockState.Shielded:
+                    prism.prismProperties.IsShielded = true;
+                    break;
+                case BlockState.SuperShielded:
+                    prism.prismProperties.IsSuperShielded = true;
+                    break;
+                case BlockState.Dangerous:
+                    prism.prismProperties.IsDangerous = true;
+                    break;
+            }
+
+            // Danger mode override (dynamic toggle takes priority)
             if (_dangerMode)
             {
-                try { prism.prismProperties.IsDangerous = true; } catch { /* ignore */ }
+                prism.prismProperties.IsDangerous = true;
 
                 if (_dangerMaterial && prism.TryGetComponent<Renderer>(out var rend))
                 {
@@ -251,11 +270,6 @@ namespace CosmicShore.Game
                 OnDangerBlockCreated?.Invoke(vesselStatus.PlayerName);
             }
 
-            
```

</details>

### `7bc539bf9` — spawnables children test

_Garrett Milliron, 2026-02-25 02:57:57 -0500_

```text
 Assets/_Prefabs/Spawnables/SpawnablePumpkin 1.prefab                  | 56 +++++++++++++++++++++++++++++++++
 Assets/_Prefabs/Spawnables/SpawnablePumpkin 1.prefab.meta             |  7 +++++
 Assets/_Prefabs/Spawnables/SpawnablePumpkin.prefab                    | 15 +++++----
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  |  2 +-
 4 files changed, 73 insertions(+), 7 deletions(-)
```

### `8750e713d` — Fix NullReferenceException when spawnable uses children instead of direct prism leaf

_Claude, 2026-02-25 08:08:38 +0000_

```text
SpawnablePumpkin 1 (with SpawnableComet as child) had prism = null because it
operates as an internal node. GenerateTrailData() in SpawnableEllipsoid,
SpawnablePumpkin, and SpawnableCardioidSmear all accessed
prism.transform.localScale unconditionally — NullReferenceException silently
killed trail generation, so nothing spawned.

Add PrismScale property to SpawnableEllipsoid that falls back to Vector3.one
when prism is null. This also produces uniform container scales for internal
nodes, avoiding shearing of nested child geometry.
```

```text
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs |  2 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableEllipsoid.cs     | 13 ++++++++++---
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnablePumpkin.cs       |  2 +-
 3 files changed, 12 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs
index 57b050948..740470ecf 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCardioidSmear.cs
@@ -32,7 +32,7 @@ public class SpawnableCardioidSmear : SpawnableEllipsoid
                 var lookPosition = block == 0 ? position : points[block - 1].Position;
                 var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
 
-                points[block] = new SpawnPoint(position, rotation, prism.transform.localScale);
+                points[block] = new SpawnPoint(position, rotation, PrismScale);
             }
 
             trailDataList.Add(new SpawnTrailData(points, true, domain));
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableEllipsoid.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableEllipsoid.cs
index 0498ca239..1e80da187 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableEllipsoid.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableEllipsoid.cs
@@ -18,6 +18,13 @@ public class SpawnableEllipsoid : SpawnableBase
     protected float width;
     protected float height;
 
+    /// <summary>
+    /// Safe accessor for the prism prefab's localScale.
+    /// Returns Vector3.one when prism is null (internal-node mode with children),
+    /// producing uniform point scales that avoid shearing nested child geometry.
+    /// </summary>
+    protected Vector3 PrismScale => prism ? prism.transform.localScale : Vector3.one;
+
     protected override SpawnTrailData[] GenerateTrailData()
     {
         length = (float)rng.Next(1, 100) / 100f * maxlength;
@@ -36,7 +43,7 @@ public class SpawnableEllipsoid : SpawnableBase
             var position = new Vector3(x, y, 0);
             var lookPosition = block == 0 ? position : points1[block - 1].Position;
             var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
-            points1[block] = new SpawnPoint(position, rotation, prism.transform.localScale);
+            points1[block] = new SpawnPoint(position, rotation, PrismScale);
         }
 
         // Ring 2: XZ plane (Ruby)
@@ -49,7 +56,7 @@ public class SpawnableEllipsoid : SpawnableBase
             var position = new Vector3(x, 0, z);
             var lookPosition = block == 0 ? position : points2[block - 1].Position;
             var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
-            points2[block] = new SpawnPoint(position, rotation, prism.transform.localScale);
+            points2[block] = new SpawnPoint(position, rotation, PrismScale);
         }
 
         // Ring 3: YZ plane (Gold)
@@ -62,7 +69,7 @@ public class SpawnableEllipsoid : SpawnableBase
             var position = new Vector3(0, y, z);
             var lookPosition = block == 0 ? position : points3[block - 1].Position;
             var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
-            points3[block] = new SpawnPoint(position, rotation, prism.transform.localScale);
+            points3[block] = new SpawnPoint(position, rotation, PrismScale);
         }
 
         return new[]
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnablePumpkin.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnablePumpkin.cs
index 101dbd220..849fd613b 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnablePumpkin.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnablePumpkin.cs
@@ -54,7 +54,7 @@ namespace CosmicShore.Environment.MiniGameObjects
                     var position = new Vector3(rotatedX, rotatedY, z);
                     var lookPosition = block == 0 ? position : points[block - 1].Position;
                     var rotation = SpawnPoint.LookRotation(lookPosition, position, Vector3.up);
-                    var blockScale = sizeMultiplier * pumpkinWidth * prism.transform.localScale * Mathf.Sin(t);
+                    var blockScale = sizeMultiplier * pumpkinWidth * PrismScale * Mathf.Sin(t);
 
                     points[block] = new SpawnPoint(position, rotation, blockScale);
                 }
```

</details>
