# Branch archive: `ftue-implementation`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2025-07-18 by Yash Sadhukhan
- **Unmerged commits:** 6
- **Forked from:** `d9164baba` (2025-07-18, Initialize LifeForms from Cell)
- **Tip:** `98c17ef8a`
- **Files touched (168):**
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/L1 Active.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/L1 Active.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/L1.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/L1.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/L2 Active.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/L2 Active.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/L2.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/L2.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/Rectangle 1356.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/Rectangle 1356.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/o button Active.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/o button Active.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/o button.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/o button.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/square button Active.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/square button Active.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/square button.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/square button.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/triangle button Active.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/triangle button Active.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/triangle button.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/PS/triangle button.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/A button active-1.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/A button active-1.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/A button active.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/A button active.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/A button.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/A button.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/B button active.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/B button active.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/B button.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/B button.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/R1 Active.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/R1 Active.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/R1.png`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/R1.png.meta`
  - `Assets/_Graphics/Design Assests/HUD UI/Buttons/XBOX/R2 Active.png`
  - … and 128 more

### `1776ea730` — Revert "Initialize LifeForms from Cell"

_Yash Sadhukhan, 2025-07-18 23:09:46 +0530_

```text
This reverts commit d9164babafb59258164ab92162b6ba6400d2d987.
```

```text
 Assets/_Scenes/Menu_Main.unity                                     |   2 +-
 Assets/_Scenes/MinigameFreestyle.unity                             | 201 ++++++++++++++++++-----------------
 Assets/_Scripts/Game/Environment/Cell.cs                           |  22 +++-
 Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs   |   2 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/BodySegmentFauna.cs |   6 ++
 Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs             |   4 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs   |  13 ++-
 Assets/_Scripts/Game/Environment/FloraAndFauna/Flora.cs            |   5 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs         |  13 +--
 Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs       |   4 +-
 10 files changed, 147 insertions(+), 125 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 267 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/Cell.cs b/Assets/_Scripts/Game/Environment/Cell.cs
index ece2db41b..c988f026e 100644
--- a/Assets/_Scripts/Game/Environment/Cell.cs
+++ b/Assets/_Scripts/Game/Environment/Cell.cs
@@ -1,10 +1,8 @@
-﻿using System;
-using System.Collections;
+﻿using System.Collections;
 using System.Collections.Generic;
 using UnityEngine;
 using CosmicShore.Core;
 using Obvious.Soap;
-using Random = UnityEngine.Random;
 
 
 namespace CosmicShore.Game
@@ -71,6 +69,15 @@ namespace CosmicShore.Game
                 //volumeGrids.Add(t, new BlockVolumeDensityGrid(t));
             }
         }
+        void OnEnable()
+        {
+            Crystal.OnCrystalMove += UpdateItem;
+        }
+
+        void OnDisable()
+        {
+            Crystal.OnCrystalMove -= UpdateItem;
+        }
         
         void Start()
         {
@@ -210,6 +217,9 @@ namespace CosmicShore.Game
 
         public bool TryAddItem(CellItem item)
         {
+            if (!ContainsPosition(item.transform.position))
+                return false;
+
             if (item.GetID() != 0)
                 return false;
             
@@ -222,6 +232,9 @@ namespace CosmicShore.Game
 
         public bool TryRemoveItem(CellItem item)
         {
+            if (!ContainsPosition(item.transform.position))
+                return false;
+
             if (!CellItems.Contains(item))
                 return false;
             
@@ -315,7 +328,6 @@ namespace CosmicShore.Game
             {
                 var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                 newFlora.Team = spawnJade ? (Teams)Random.Range(1, 5): (Teams)Random.Range(2, 5);
-                newFlora.Initialize(this);
             }
             while (true)
             {
@@ -324,7 +336,6 @@ namespace CosmicShore.Game
                 {
                     var newFlora = Instantiate(floraConfiguration.Flora, transform.position, Quaternion.identity);
                     newFlora.Team = spawnJade ? (Teams)Random.Range(1, 5) : (Teams)Random.Range(2, 5);
-                    newFlora.Initialize(this);
                 }
                 if (floraConfiguration.OverrideDefaultPlantPeriod) yield return new WaitForSeconds(floraConfiguration.NewPlantPeriod);
                 else yield return new WaitForSeconds(floraConfiguration.Flora.PlantPeriod);
@@ -340,6 +351,7 @@ namespace CosmicShore.Game
                 var period = baseFaunaSpawnTime * faunaSpawnVolumeThreshold / controllingVolume; //TODO: use this to adjust spawn rate
                 if (controllingVolume > faunaSpawnVolumeThreshold)
                 {
+                    
                     var newPopulation = Instantiate(population, transform.position, Quaternion.identity);
                     newPopulation.Team = ControllingTeam;
                     newPopulation.Goal = Crystal.transform.position;
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
index 2f7c9c902..14b9f0dab 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/AssembledFlora.cs
@@ -164,7 +164,7 @@ namespace CosmicShore
         public override void Plant()
         {
             assembler = CreateNewAssembler();
-            transform.position = cell.GetCrystal().transform.position + 200 * Random.onUnitSphere; // TODO: replace magic number with nucleus radius 
+            transform.position = node.GetCrystal().transform.position + 200 * Random.onUnitSphere; // TODO: replace magic number with nucleus radius 
         }
 
         public Assembler CreateNewAssembler()
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BodySegmentFauna.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BodySegmentFauna.cs
index a2f31edcd..1d949dea5 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BodySegmentFauna.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BodySegmentFauna.cs
@@ -8,8 +8,14 @@ public class BodySegmentFauna : Fauna
     public bool IsHead;
     public bool IsTail;
 
+    protected override void Start()
+    {
+        base.Start();
+    }
+
     protected override void Die()
     {
+        
         if (!IsHead && !IsTail)
         {
             ParentWorm.SplitWorm(this);
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
index 5fa41fee1..2f02fd674 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
@@ -3,7 +3,6 @@ using System.Collections;
 using System.Collections.Generic;
 using CosmicShore.Core;
 using CosmicShore;
-using CosmicShore.Game;
 
 public enum BoidCollisionEffects
 {
@@ -53,8 +52,9 @@ public class Boid : Fauna
     List<Collider> separatedBoids = new List<Collider>();
 
 
-    public override void Initialize(Cell cell)
+    protected override void Start()
     {
+        base.Start();
         AddSpindle(spindle);
         BlockCollider = healthBlock.GetComponent<BoxCollider>();
         currentVelocity = transform.forward * Random.Range(minSpeed, maxSpeed);
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
index 9c20a4ecc..fb85b736b 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/BranchingFlora.cs
@@ -1,5 +1,4 @@
 using System.Collections.Generic;
-using CosmicShore.Game;
 using UnityEngine;
 
 namespace CosmicShore
@@ -42,16 +41,16 @@ namespace CosmicShore
 
         private int spawnedItemCount = 0;
 
-        public override void Initialize(Cell cell)
+        protected override void Start()
         {
-            base.Initialize(cell);
+            base.Start();
```

</details>

### `b27bf55be` — Revert "Fix Merge Conflict from FTUE branch"

_Yash Sadhukhan, 2025-07-18 23:09:51 +0530_

```text
This reverts commit 8b8ad43a5e8ee01c1673d29217a6f9d2ee4816af.
```

```text
 Assets/_Scripts/Game/Managers/CameraManager.cs           | 8 ++------
 Assets/_Scripts/Game/Player/Player.cs                    | 1 -
 Assets/_Scripts/ShipHUD/Controller/ShipHUDController.cs  | 2 +-
 Assets/_Scripts/ShipHUD/Model/ShipHUDControllerEditor.cs | 6 +++---
 Assets/_Scripts/ShipHUD/View/ShipHUDView.cs              | 4 ++--
 5 files changed, 8 insertions(+), 13 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Managers/CameraManager.cs b/Assets/_Scripts/Game/Managers/CameraManager.cs
index 9692cb2ad..943f63bbe 100644
--- a/Assets/_Scripts/Game/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Game/Managers/CameraManager.cs
@@ -4,7 +4,6 @@ using CosmicShore.Game.CameraSystem;
 using CosmicShore.Utilities;
 using CosmicShore.Utility;
 using System.Collections;
-using CosmicShore.Game;
 using Unity.Cinemachine;
 using UnityEngine;
 using UnityEngine.Rendering.Universal;
@@ -47,7 +46,6 @@ public class CameraManager : SingletonPersistent<CameraManager>
     Coroutine zoomOutCoroutine;
     Coroutine returnToNeutralCoroutine;
     Coroutine lerper;
-    private IShip selectedShip;
 
     public override void Awake()
     {
@@ -102,8 +100,6 @@ public class CameraManager : SingletonPersistent<CameraManager>
         }
     }
 
-    public void Initialize(IShip ship) => selectedShip = ship;
-
     private void ApplyRuntimeOffset()
     {
         playerCamera.SetFollowOffset(runtimeFollowOffset);
@@ -148,7 +144,7 @@ public class CameraManager : SingletonPersistent<CameraManager>
 
     public void SetupGamePlayCameras()
     {
-        playerFollowTarget = FollowOverride ? selectedShip.ShipStatus.ShipCameraCustomizer.FollowTarget : selectedShip.Transform;
+        playerFollowTarget = FollowOverride ? Hangar.Instance.SelectedShip.ShipStatus.ShipCameraCustomizer.FollowTarget : Hangar.Instance.SelectedShip.Transform;
         SetupGamePlayCameras(playerFollowTarget);
     }
 
@@ -170,7 +166,7 @@ public class CameraManager : SingletonPersistent<CameraManager>
 
     void LookAtCrystal()
     {
-        mainMenuCamera.LookAt = CellControlManager.Instance.GetNearestCell(Vector3.zero).GetCrystal().transform;
+        mainMenuCamera.LookAt = NodeControlManager.Instance.GetNearestNode(Vector3.zero).GetCrystal().transform;
     }
 
     public void SetCloseCameraActive()
diff --git a/Assets/_Scripts/Game/Player/Player.cs b/Assets/_Scripts/Game/Player/Player.cs
index 2020cb314..575fe18a1 100644
--- a/Assets/_Scripts/Game/Player/Player.cs
+++ b/Assets/_Scripts/Game/Player/Player.cs
@@ -68,7 +68,6 @@ namespace CosmicShore.Game
             if (!_isAI)
             {
                 InputController.Initialize(Ship);
-                CameraManager.Instance.Initialize(Ship);
             }
         }
 
diff --git a/Assets/_Scripts/ShipHUD/Controller/ShipHUDController.cs b/Assets/_Scripts/ShipHUD/Controller/ShipHUDController.cs
index f3b16a52c..51200260c 100644
--- a/Assets/_Scripts/ShipHUD/Controller/ShipHUDController.cs
+++ b/Assets/_Scripts/ShipHUD/Controller/ShipHUDController.cs
@@ -7,7 +7,7 @@ namespace CosmicShore.Game
     [RequireComponent(typeof(IShipStatus))]
     public class ShipHUDController : MonoBehaviour, IShipHUDController
     {
-        [SerializeField] private ShipClassType shipType;
+        [SerializeField] private ShipTypes shipType;
         
         [Header("Event Channels")]
         [SerializeField] 
diff --git a/Assets/_Scripts/ShipHUD/Model/ShipHUDControllerEditor.cs b/Assets/_Scripts/ShipHUD/Model/ShipHUDControllerEditor.cs
index 4164475a1..4c80d6f56 100644
--- a/Assets/_Scripts/ShipHUD/Model/ShipHUDControllerEditor.cs
+++ b/Assets/_Scripts/ShipHUD/Model/ShipHUDControllerEditor.cs
@@ -37,18 +37,18 @@ public class ShipHUDControllerEditor : Editor
         EditorGUILayout.PropertyField(shipTypeProp, new GUIContent("Ship Type"));
 
         // Only show the relevant action fields
-        ShipClassType type = (ShipClassType)shipTypeProp.intValue;
+        ShipTypes type = (ShipTypes)shipTypeProp.intValue;
         GUILayout.Space(6);
         DrawSection(type + " Actions", sectionColor, () =>
         {
             switch (type)
             {
-                case ShipClassType.Serpent:
+                case ShipTypes.Serpent:
                     EditorGUILayout.PropertyField(boostProp, new GUIContent("Boost Action"));
                     EditorGUILayout.PropertyField(seedProp,  new GUIContent("Seed Assembler"));
                     break;
 
-                case ShipClassType.Sparrow:
+                case ShipTypes.Sparrow:
                     EditorGUILayout.PropertyField(overheatProp, new GUIContent("Overheating Action"));
                     EditorGUILayout.PropertyField(fullAutoProp, new GUIContent("Full-Auto Action"));
                     EditorGUILayout.PropertyField(fireGunProp,  new GUIContent("Fire Gun Action"));
diff --git a/Assets/_Scripts/ShipHUD/View/ShipHUDView.cs b/Assets/_Scripts/ShipHUD/View/ShipHUDView.cs
index 903fa06fc..f13f825ad 100644
--- a/Assets/_Scripts/ShipHUD/View/ShipHUDView.cs
+++ b/Assets/_Scripts/ShipHUD/View/ShipHUDView.cs
@@ -31,9 +31,9 @@ namespace CosmicShore.Game
 
     public class ShipHUDView : MonoBehaviour,IShipHUDView
     {
-        public ShipClassType ShipHUDType => hudType;
+        public ShipTypes ShipHUDType => hudType;
 
-        [SerializeField] private ShipClassType hudType;
+        [SerializeField] private ShipTypes hudType;
         [SerializeField] private ResourceDisplayRef[] resourceDisplays;
         [SerializeField] private Transform silhouetteContainer;
         [SerializeField] private Transform trailContainer;
```

</details>

### `cadccace9` — Revert "Merge branch 'ftue-implementation' into Multiplayer-Duel-Cell"

_Yash Sadhukhan, 2025-07-18 23:09:59 +0530_

```text
This reverts commit 461ae921b9d941cab5b0f00e5a61e4e44223d613, reversing
changes made to 713038c3d855a75d460b813644c31ea60bd77095.
```

```text
 .../_Graphics/Design Assests/HUD UI/New_Sparrow/swap weapon  icon.png |  Bin 785 -> 0 bytes
 .../Design Assests/HUD UI/New_Sparrow/swap weapon  icon.png.meta      |  143 --
 Assets/_Prefabs/CORE/GameCanvas.prefab                                |    2 +-
 Assets/_Prefabs/Spaceships/Serpent.prefab                             |  128 +-
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             |   45 -
 Assets/_Prefabs/UI Elements/ShipHUD/DolphinHUDVariant.prefab          |  424 +-----
 Assets/_Prefabs/UI Elements/ShipHUD/SerpentHUDVariant.prefab          |  438 +-----
 Assets/_Prefabs/UI Elements/ShipHUD/SparrowHUDVariant.prefab          | 2212 ++++++++-----------------------
 Assets/_Scenes/Menu_Main.unity                                        |  413 +-----
 Assets/_Scripts/Game/Camera.meta                                      |    8 -
 Assets/_Scripts/Game/Camera/CustomCameraController.cs                 |  105 --
 Assets/_Scripts/Game/Camera/CustomCameraController.cs.meta            |   11 -
 Assets/_Scripts/Game/Managers/CameraManager.cs                        |  140 +-
 Assets/_Scripts/Game/Ship/R_ShipController.cs                         |    1 -
 Assets/_Scripts/Game/Ship/ShipActions/FireGunAction.cs                |    4 +-
 Assets/_Scripts/Game/Ship/ShipActions/FullAutoAction.cs               |    6 -
 Assets/_Scripts/Game/Ship/ShipActions/OverheatingAction.cs            |   11 +-
 Assets/_Scripts/Game/Ship/ShipActions/SeedAssemblerAction.cs          |    7 -
 Assets/_Scripts/Game/Ship/ShipActions/ToggleStationaryModeAction.cs   |   13 +-
 Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs                     |    8 -
 Assets/_Scripts/Game/UI/ResourceDisplay.cs                            |  324 +----
 Assets/_Scripts/ShipHUD/Controller/ShipHUDController.cs               |   96 +-
 Assets/_Scripts/ShipHUD/Interfaces/IShipHUDView.cs                    |   20 -
 Assets/_Scripts/ShipHUD/Model/ShipHUDControllerEditor.cs              |   97 --
 Assets/_Scripts/ShipHUD/Model/ShipHUDControllerEditor.cs.meta         |    2 -
 Assets/_Scripts/ShipHUD/Model/ShipHUDInspector.cs                     |    8 -
 Assets/_Scripts/ShipHUD/View/ControllerButtonIconReferences.cs        |   51 -
 Assets/_Scripts/ShipHUD/View/ControllerButtonIconReferences.cs.meta   |    2 -
 Assets/_Scripts/ShipHUD/View/ShipHUDView.cs                           |  204 +--
 119 files changed, 885 insertions(+), 10092 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1633 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Camera/CustomCameraController.cs b/Assets/_Scripts/Game/Camera/CustomCameraController.cs
deleted file mode 100644
index e0a0ced82..000000000
--- a/Assets/_Scripts/Game/Camera/CustomCameraController.cs
+++ /dev/null
@@ -1,105 +0,0 @@
-using UnityEngine;
-
-namespace CosmicShore.Game.CameraSystem
-{
-    [RequireComponent(typeof(Camera))]
-    public class CustomCameraController : MonoBehaviour
-    {
-        [SerializeField] Transform followTarget;
-        [SerializeField] Vector3 followOffset = new(0f, 10f, -20f);
-        [SerializeField] float followSmoothTime = 0.2f;
-        [SerializeField] float rotationSmoothTime = 5f;
-        [SerializeField] bool useFixedUpdate = false;
-        [SerializeField] float farClipPlane = 10000f;
-        [SerializeField] float fieldOfView = 60f;
-
-        Camera cachedCamera;
-        Vector3 velocity;
-
-        public Camera Camera => cachedCamera;
-
-        public Transform FollowTarget
-        {
-            get => followTarget;
-            set => followTarget = value;
-        }
-
-        public float FollowSmoothTime
-        {
-            get => followSmoothTime;
-            set => followSmoothTime = Mathf.Max(0f, value);
-        }
-
-        public float RotationSmoothTime
-        {
-            get => rotationSmoothTime;
-            set => rotationSmoothTime = Mathf.Max(0f, value);
-        }
-
-
-        void Awake()
-        {
-            cachedCamera = GetComponent<Camera>();
-            cachedCamera.fieldOfView = fieldOfView;
-            cachedCamera.useOcclusionCulling = false;
-            cachedCamera.farClipPlane = farClipPlane;
-        }
-
-        void LateUpdate()
-        {
-            if (!useFixedUpdate)
-                UpdateCamera();
-        }
-
-        void FixedUpdate()
-        {
-            if (useFixedUpdate)
-                UpdateCamera();
-        }
-
-        void UpdateCamera()
-        {
-            if (followTarget == null)
-                return;
-
-            Debug.Log($"<color=green>We did enter here{followOffset}</color>");
-
-            Quaternion offsetRot = followTarget.rotation;
-            Vector3 desiredPos = followTarget.position + offsetRot * followOffset;
-
-            Vector3 currentLocal = followTarget.InverseTransformPoint(transform.position);
-            Vector3 desiredLocal = followTarget.InverseTransformPoint(desiredPos);
-
-            currentLocal.z = Mathf.SmoothDamp(currentLocal.z, desiredLocal.z, ref velocity.z, followSmoothTime);
-            currentLocal.x = desiredLocal.x;
-            currentLocal.y = desiredLocal.y;
-
-            transform.position = followTarget.TransformPoint(currentLocal);
-            
-            Vector3 toTarget = followTarget.position - transform.position;
-            Quaternion targetRot = Quaternion.LookRotation(toTarget, followTarget.up);
-            float t = 1f - Mathf.Exp(-rotationSmoothTime * Time.deltaTime);
-            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
-        }
-
-        public void SetFollowTarget(Transform target) => followTarget = target;
-        public void SetFollowOffset(Vector3 offset)
-        {
-            followOffset = offset;
-        }
-        public Vector3 GetFollowOffset() => followOffset;
-
-        public void SetFieldOfView(float fov) => cachedCamera.fieldOfView = fov;
-        public void SetClipPlanes(float near, float far)
-        {
-            cachedCamera.nearClipPlane = near;
-            cachedCamera.farClipPlane = far;
-        }
-        public void SetOrthographic(bool ortho, float size)
-        {
-            cachedCamera.orthographic = ortho;
-            if (ortho)
-                cachedCamera.orthographicSize = size;
-        }
-    }
-}
diff --git a/Assets/_Scripts/Game/Managers/CameraManager.cs b/Assets/_Scripts/Game/Managers/CameraManager.cs
index 943f63bbe..990ae43f5 100644
--- a/Assets/_Scripts/Game/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Game/Managers/CameraManager.cs
@@ -1,12 +1,11 @@
-using CosmicShore;
+using Unity.Cinemachine;
 using CosmicShore.Core;
-using CosmicShore.Game.CameraSystem;
-using CosmicShore.Utilities;
 using CosmicShore.Utility;
 using System.Collections;
-using Unity.Cinemachine;
 using UnityEngine;
-using UnityEngine.Rendering.Universal;
+using CosmicShore;
+using CosmicShore.Game;
+using CosmicShore.Utilities;
 
 public class CameraManager : SingletonPersistent<CameraManager>
 {
@@ -14,9 +13,9 @@ public class CameraManager : SingletonPersistent<CameraManager>
     ThemeManagerDataContainerSO _themeManagerData;
 
     [SerializeField] CinemachineCamera mainMenuCamera;
-    [SerializeField] CustomCameraController playerCamera;
-    [SerializeField] CustomCameraController deathCamera;
-    [SerializeField] CustomCameraController endCamera;
+    [SerializeField] CinemachineVirtualCameraBase playerCamera;
+    [SerializeField] CinemachineVirtualCameraBase deathCamera;
+    [SerializeField] CinemachineVirtualCameraBase endCamera;
 
     [SerializeField] Transform endCameraFollowTarget;
     [SerializeField] Transform endCameraLookAtTarget;
@@ -39,37 +38,14 @@ public class CameraManager : SingletonPersistent<CameraManager>
 
     public float CloseCamDistance;
     public float FarCamDistance;
-    [SerializeField] float startTransitionDistance = 40f;
```

</details>

### `7d67b0a1d` — Revert "Merge remote-tracking branch 'upstream/development' into Multiplayer-Duel-Cell"

_Yash Sadhukhan, 2025-07-18 23:10:08 +0530_

```text
This reverts commit 713038c3d855a75d460b813644c31ea60bd77095, reversing
changes made to cf7017acd5a171865773d26cbd835602edd4d753.
```

```text
 Assets/_Scenes/Menu_Main.unity                  | 45 --------------------------
 Assets/_Scripts/Utility/GamepadDebugger.cs      | 95 -------------------------------------------------------
 Assets/_Scripts/Utility/GamepadDebugger.cs.meta |  2 --
 3 files changed, 142 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/GamepadDebugger.cs b/Assets/_Scripts/Utility/GamepadDebugger.cs
deleted file mode 100644
index e9ba994db..000000000
--- a/Assets/_Scripts/Utility/GamepadDebugger.cs
+++ /dev/null
@@ -1,95 +0,0 @@
-using UnityEngine;
-using UnityEngine.InputSystem;
-using System.Linq;
-
-public class GamepadDebugger : MonoBehaviour
-{
-    void Start()
-    {
-        LogConnectedGamepads();
-        InputSystem.onDeviceChange += OnDeviceChange;
-    }
-
-    void OnDestroy()
-    {
-        InputSystem.onDeviceChange -= OnDeviceChange;
-    }
-
-    private void LogConnectedGamepads()
-    {
-        var gamepads = Gamepad.all;
-        if (gamepads.Count == 0)
-        {
-            Debug.LogWarning("<color=yellow>No gamepads detected.</color>");
-            return;
-        }
-
-        foreach (var pad in gamepads)
-        {
-            string type = GetControllerType(pad);
-            string color = GetColorForType(type);
-
-            string info = $"<color={color}><b>Gamepad Detected:</b> '{pad.displayName}' ({pad.device.description.product})";
-            info += $"\n<b>Controller Type:</b> {type}";
-
-            if (type == "Other")
-            {
-                info += $"\n<b>[Details]</b>";
-                info += $"\nName: {pad.name}";
-                info += $"\nProduct: {pad.device.description.product}";
-                info += $"\nManufacturer: {pad.device.description.manufacturer}";
-                info += $"\nInterface: {pad.device.description.interfaceName}";
-                info += $"\nUsages: {string.Join(",", pad.usages.Select(u => u.ToString()))}";
-                info += $"\n<b>Supported Controls:</b>";
-                foreach (var c in pad.allControls)
-                {
-                    info += $"\n   - {c.name}: {c.path} [{c.layout}]";
-                }
-            }
-
-            info += "</color>";
-            Debug.Log(info);
-        }
-    }
-
-    private string GetControllerType(Gamepad pad)
-    {
-        var product = (pad.device.description.product ?? "").ToLower();
-        if (product.Contains("xbox")) return "XBOX";
-        if (product.Contains("dualshock") || product.Contains("dualsense") || product.Contains("ps4") || product.Contains("ps5") || product.Contains("playstation"))
-            return "PlayStation";
-        if (product.Contains("logitech")) return "Logitech (Other)";
-        return "Other";
-    }
-
-    private string GetColorForType(string type)
-    {
-        switch (type)
-        {
-            case "XBOX": return "green";
-            case "PlayStation": return "blue";
-            case "Logitech (Other)": return "orange";
-            default: return "red";
-        }
-    }
-
-    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
-    {
-        if (device is Gamepad)
-        {
-            switch (change)
-            {
-                case InputDeviceChange.Added:
-                case InputDeviceChange.Reconnected:
-                    Debug.Log("<color=cyan>Gamepad connected/reconnected: " + device.displayName + "</color>");
-                    LogConnectedGamepads();
-                    break;
-                case InputDeviceChange.Removed:
-                case InputDeviceChange.Disconnected:
-                    Debug.Log("<color=magenta>Gamepad disconnected: " + device.displayName + "</color>");
-                    LogConnectedGamepads();
-                    break;
-            }
-        }
-    }
-}
```

</details>

### `2c1cb543d` — Revert "Add Vessel Collider to remove Rigidbody dependency"

_Yash Sadhukhan, 2025-07-18 23:10:19 +0530_

```text
This reverts commit cf7017acd5a171865773d26cbd835602edd4d753.
```

```text
 Assets/_Prefabs/Spaceships/Dolphin.prefab             | 296 ------------------------------------------------
 Assets/_Scenes/Menu_Main.unity                        |   4 +-
 Assets/_Scripts/Game/Environment/FlowField/Crystal.cs |   4 +-
 Assets/_Scripts/Game/Ship/TrailBlock.cs               |  29 +++--
 Assets/_Scripts/Game/Ship/VesselCollider.cs           |  17 ---
 Assets/_Scripts/Game/Ship/VesselCollider.cs.meta      |   3 -
 6 files changed, 22 insertions(+), 331 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
index 5ae28c963..d8428cd2e 100644
--- a/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
@@ -99,11 +99,9 @@ namespace CosmicShore.Game
 
         protected virtual void Collide(Collider other)
         {
-            if (!other.TryGetComponent(out IVesselCollider shipCollider))
+            if (!other.TryGetComponent(out IShip ship))
                 return;
 
-            var ship = shipCollider.Ship;
-            
             if (OwnTeam != Teams.None && OwnTeam != ship.ShipStatus.Team)
                 return;
 
diff --git a/Assets/_Scripts/Game/Ship/TrailBlock.cs b/Assets/_Scripts/Game/Ship/TrailBlock.cs
index b07757e4d..3403c9c08 100644
--- a/Assets/_Scripts/Game/Ship/TrailBlock.cs
+++ b/Assets/_Scripts/Game/Ship/TrailBlock.cs
@@ -79,7 +79,7 @@ namespace CosmicShore.Core
             get => scaleAnimator?.TargetScale ?? transform.localScale;
             set
             {
-                scaleAnimator?.SetTargetScale(value);
+                if (scaleAnimator != null) scaleAnimator.SetTargetScale(value);
             }
         }
 
@@ -90,13 +90,14 @@ namespace CosmicShore.Core
             get => scaleAnimator?.MaxScale ?? Vector3.one * 10f;  // Default max scale as fallback
             set
             {
-                if (scaleAnimator is not null) scaleAnimator.MaxScale = value;
+                if (scaleAnimator != null)
+                    scaleAnimator.MaxScale = value;
             }
         }
 
         public void ChangeSize()
         {
-            if (scaleAnimator is not null)
+            if (scaleAnimator != null)
             {
                 scaleAnimator.SetTargetScale(TargetScale);
             }
@@ -178,15 +179,15 @@ namespace CosmicShore.Core
                 TrailBlockProperties = TrailBlockProperties
             });
 
-            if (CellControlManager.Instance is not null)
+            if (CellControlManager.Instance != null)
             {
                 CellControlManager.Instance.AddBlock(Team, TrailBlockProperties);
                 
                 // Setup team node tracking after block is fully initialized
-                Cell targetCell = CellControlManager.Instance.GetNearestCell(TrailBlockProperties.position);
+                Cell targetNode = CellControlManager.Instance.GetNearestCell(TrailBlockProperties.position);
                 System.Array.ForEach(new[] { Teams.Jade, Teams.Ruby, Teams.Gold }, t =>
                 {
-                    if (t != Team) targetCell.countGrids[t].AddBlock(this);
+                    if (t != Team) targetNode.countGrids[t].AddBlock(this);
                 });
             }
         }
@@ -198,24 +199,32 @@ namespace CosmicShore.Core
         // Collision Handling
         protected void OnTriggerEnter(Collider other)
         {
-            if (other.TryGetComponent(out IVesselCollider vesselCollider))
+            if (other.gameObject.IsLayer("Ships"))
             {
-                var ship = vesselCollider.Ship;
+                if (!other.TryGetComponent(out IShip ship))
+                    return;
+
                 if (!ship.ShipStatus.Attached)
+                {
                     ship.PerformTrailBlockImpactEffects(TrailBlockProperties);
+                }
             }
-            
-            else if (other.TryGetComponent(out CellItem cellItem))
+
+            if (other.gameObject.IsLayer("Crystals"))
             {
                 if (!TrailBlockProperties.IsShielded)
+                {
                     ActivateShield();
+                }
             }
         }
 
         protected void OnTriggerExit(Collider other)
         {
             if (other.gameObject.IsLayer("Crystals"))
+            {
                 ActivateShield(2.0f);
+            }
         }
 
         // Destruction Methods
diff --git a/Assets/_Scripts/Game/Ship/VesselCollider.cs b/Assets/_Scripts/Game/Ship/VesselCollider.cs
deleted file mode 100644
index 04cabcb03..000000000
--- a/Assets/_Scripts/Game/Ship/VesselCollider.cs
+++ /dev/null
@@ -1,17 +0,0 @@
-using UnityEngine;
-
-namespace CosmicShore.Game
-{
-    public interface IVesselCollider
-    {
-        IShip Ship { get; }
-    }
-    
-    public class VesselCollider : MonoBehaviour, IVesselCollider
-    {
-        [SerializeField, RequireInterface(typeof(IShip))]
-        private Object shipObject;
-        
-        public IShip Ship => shipObject as IShip;
-    }
-}
\ No newline at end of file
```

</details>

### `98c17ef8a` — Revert "AI Pilot of Manta Working"

_Yash Sadhukhan, 2025-07-18 23:10:24 +0530_

```text
This reverts commit d42f17259b3eade9d697a821bbb8bb90f7b9ad76.
```

```text
 .../{CellControlTurnMonitor.cs => NodeControlTurnMonitor.cs}          |   3 +-
 ...{CellControlTurnMonitor.cs.meta => NodeControlTurnMonitor.cs.meta} |   0
 Assets/_Scripts/Game/Arcade/WildlifeBlitzMiniGame.cs                  |   1 +
 Assets/_Scripts/Game/Environment/Cell.cs                              | 544 ++++++++++++++++----------------
 Assets/_Scripts/Game/Environment/CellModifiers/CellModifier.cs        |  14 +-
 Assets/_Scripts/Game/Environment/CellModifiers/ExtraOmniCrystals.cs   |  49 ++-
 Assets/_Scripts/Game/Environment/CrystalProperties.cs                 |  28 +-
 Assets/_Scripts/Game/Environment/Cytoplasm/SnowChanger.cs             | 172 +++++-----
 Assets/_Scripts/Game/Environment/FloraAndFauna/BodySegmentFauna.cs    |   4 +
 .../Game/Environment/FloraAndFauna/BoidSimulationController.cs        |   6 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs            |   2 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/Population.cs          |   1 -
 Assets/_Scripts/Game/Environment/FlowField/Crystal.cs                 |  33 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/CellItem.cs          |  77 +++--
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableCrystal.cs  |  26 +-
 Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableWall.cs     |  76 +++--
 Assets/_Scripts/Game/Managers/CameraManager.cs                        |   1 -
 Assets/_Scripts/Game/Managers/CellControlManager.cs                   | 181 ++++++-----
 Assets/_Scripts/Game/Projectiles/AOERadialBlocks.cs                   |   1 -
 Assets/_Scripts/Game/Projectiles/FakeCrystal.cs                       |  21 +-
 Assets/_Scripts/Game/Ship/IShipStatus.cs                              |   2 +-
 Assets/_Scripts/Game/Ship/R_ShipController.cs                         |   1 -
 Assets/_Scripts/Game/Ship/ShipActions/MoundDronesShipAction.cs        |   4 +-
 Assets/_Scripts/Game/Ship/ShipStatus.cs                               |   2 +-
 Assets/_Scripts/Game/Ship/ShipTransformer.cs                          |   1 +
 Assets/_Scripts/Game/Ship/SingleStickShipTransformer.cs               | 124 ++++----
 Assets/_Scripts/Game/Ship/Skimmer.cs                                  |  20 +-
 Assets/_Scripts/Models/ScriptableObjects/SO_CellType.cs               |  41 ++-
 Assets/_Scripts/~ChoppingBlock/TestHarnessOctreeDensitySearch.cs      |   3 +-
 39 files changed, 871 insertions(+), 840 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 2289 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index ca28b61d0..ffbe99a06 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -3,6 +3,7 @@ using System.Collections.Generic;
 using System.Collections;
 using System;
 using Obvious.Soap;
+using CosmicShore.Core;
 
 namespace CosmicShore.Game.AI
 {
@@ -36,6 +37,9 @@ namespace CosmicShore.Game.AI
         float throttle;
         float aggressiveness;
 
+        public float X;
+        public float Y;
+
         public float defaultThrottle => Mathf.Lerp(defaultThrottleLow, defaultThrottleHigh, SkillLevel);
         public float defaultAggressiveness => Mathf.Lerp(defaultAggressivenessLow, defaultAggressivenessHigh, SkillLevel);
         float throttleIncrease => Mathf.Lerp(throttleIncreaseLow, throttleIncreaseHigh, SkillLevel);
@@ -47,6 +51,7 @@ namespace CosmicShore.Game.AI
         [SerializeField] float raycastHeight;
         [SerializeField] float raycastWidth;
 
+        [SerializeField] bool LookingAtCrystal;
         [SerializeField] bool ram;
         [SerializeField] bool drift;
 
@@ -78,7 +83,8 @@ namespace CosmicShore.Game.AI
 
         Vector3 _targetPosition;
         Vector3 _distance;
-        bool LookingAtCrystal;
+
+        [HideInInspector] public FlowFieldData flowFieldData;
 
         Dictionary<Corner, AvoidanceBehavior> CornerBehaviors;
 
@@ -122,11 +128,11 @@ namespace CosmicShore.Game.AI
             if (activeCell == null)
                 activeCell = CellControlManager.Instance.GetNearestCell(transform.position);
 
-            var cellItems = activeCell.CellItems;
+            var cellItems = activeCell.GetItems();
             float MinDistance = Mathf.Infinity;
             CellItem closestItem = null;
 
-            foreach (var item in cellItems)
+            foreach (var item in cellItems.Values)
             {
                 // Debuffs are disguised as desireable to the other team
                 // So, if it's good, or if it's bad but made by another team, go for it
@@ -143,6 +149,12 @@ namespace CosmicShore.Game.AI
             _targetPosition = closestItem == null ? activeCell.transform.position : closestItem.transform.position;
         }
 
+
+        /*public void AssignShip(IShip ship)
+        {
+            _ship = ship;
+        }*/
+
         public void Initialize(bool enableAutoPilot, IShip ship)
         {
             AutoPilotEnabled = enableAutoPilot;
@@ -165,7 +177,8 @@ namespace CosmicShore.Game.AI
                 { Corner.TopLeft, new AvoidanceBehavior (-raycastWidth, raycastHeight, CounterClockwise, Vector3.zero ) }
             };
 
-            foreach (var ability in abilities)
+            foreach
+                (var ability in abilities)
             {
                 StartCoroutine(UseAbilityCoroutine(ability));
             }
@@ -231,6 +244,9 @@ namespace CosmicShore.Game.AI
             return transform.forward * _maxDistance - (transform.position + position);
         }
 
+        
+
+
         IEnumerator UseAbilityCoroutine(AIAbility action) 
         {
             yield return new WaitForSeconds(3);
@@ -242,50 +258,29 @@ namespace CosmicShore.Game.AI
                 yield return new WaitForSeconds(action.Cooldown);
             }
         }
-        
-        #region Unused Methods
 
-        float CalculateRollAdjustment(Dictionary<Corner, Vector3> obstacleDirections)
-        {
-            float rollAdjustment = 0f;
-
-            // Example logic: If top right and bottom left corners detect obstacles, induce a roll.
-            if (obstacleDirections[Corner.TopRight].magnitude > 0 && obstacleDirections[Corner.BottomLeft].magnitude > 0)
-                rollAdjustment -= 1; // Roll left
-            if (obstacleDirections[Corner.TopLeft].magnitude > 0 && obstacleDirections[Corner.BottomRight].magnitude > 0)
-                rollAdjustment += 1; // Roll right
 
-            return rollAdjustment;
-        }
-
-        float SigmoidResponse(float input)
-        {
-            float output = 2 * (1 / (1 + Mathf.Exp(-0.1f * input)) - 0.5f);
-            return output;
-        }
-        
-        
         /*IEnumerator SetTargetCoroutine()
         {
             // TODO - these lists if needed, should be specified separate.
-            List<ShipClassType> aggressiveShips = new List<ShipClassType>
-            {
-                ShipClassType.Rhino,
+            List<ShipClassType> aggressiveShips = new List<ShipClassType> 
+            { 
+                ShipClassType.Rhino, 
                 ShipClassType.Sparrow,
             };
 
             var rand = new System.Random();
 
             // Assume activeNode can't change.
-            var activeCell = CellControlManager.Instance.GetCellByPosition(transform.position);
+            var activeCell = CellControlManager.Instance.GetCellByPosition(transform.position);  
             if (activeCell == null)
                 activeCell = CellControlManager.Instance.GetNearestCell(transform.position);
 
             while (true)
             {
-                if (activeCell != null &&
-                    // TODO - Commented out as aggressive
-                    // aggressiveShips.Contains(_ship.ShipStatus.ShipType) &&
+                if (activeCell != null && 
+                    // TODO - Commented out as aggressive 
+                    // aggressiveShips.Contains(_ship.ShipStatus.ShipType) && 
                     activeCell.ControllingTeam != Teams.None)
                 {
                     if ((_shipStatus.Team == activeCell.ControllingTeam) || (rand.NextDouble() < 0.5))  // Your team is winning.
@@ -305,6 +300,26 @@ namespace CosmicShore.Game.AI
             }
         }*/
 
+        #region Unused Methods
```

</details>
