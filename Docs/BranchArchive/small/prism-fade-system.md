# Branch archive: `prism-fade-system`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2025-10-24 by Emmanuel
- **Unmerged commits:** 1
- **Forked from:** `83b01d4a9` (2025-10-23, Sending new (hopefully fixed) expanded triangles. Origin point should be fixed)
- **Tip:** `9b2133401`
- **Files touched (5):**
  - `Assets/_Scripts/Game/Environment/FlowField/Crystal.cs`
  - `Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs`
  - `Assets/_Scripts/Game/Ship/ClearPrisms.cs`
  - `Assets/_Scripts/Game/Ship/OccludingPrismFadeSystem.cs`
  - `Assets/_Scripts/Game/Ship/OccludingPrismFadeSystem.cs.meta`

### `9b2133401` — The capsule shows and is at the right place. Weird collision bug.

_Emmanuel, 2025-10-24 13:45:01 -0700_

```text
 Assets/_Scripts/Game/Environment/FlowField/Crystal.cs                 |   4 +-
 Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs       |   5 +-
 Assets/_Scripts/Game/Ship/ClearPrisms.cs                              | 124 ---------------------------
 Assets/_Scripts/Game/Ship/OccludingPrismFadeSystem.cs                 | 147 ++++++++++++++++++++++++++++++++
 .../Ship/{ClearPrisms.cs.meta => OccludingPrismFadeSystem.cs.meta}    |   0
 5 files changed, 152 insertions(+), 128 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 325 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs b/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
index 001527092..622c63fdd 100644
--- a/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
+++ b/Assets/_Scripts/Game/Environment/FlowField/Crystal.cs
@@ -111,12 +111,12 @@ namespace CosmicShore.Game
            //  _lastSpawnPosition = newPos;
         }
 
-        public void Vacuum(Vector3 newPosition, float vaccumAmount)
+        public void Vacuum(Vector3 newPosition, float vacuumAmount)
         {
             transform.position = Vector3.MoveTowards(
                 transform.position,
                 newPosition,
-                vaccumAmount * Time.deltaTime / transform.lossyScale.x);
+                vacuumAmount * Time.deltaTime / transform.lossyScale.x);
         }
 
         //the following is a public method that can be called to grow the crystal
diff --git a/Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs b/Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs
index 0c954e1a9..ad549f7b2 100644
--- a/Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs
+++ b/Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs
@@ -39,8 +39,8 @@ namespace CosmicShore.Game
             
             if (skimmer.AllowVaccumCrystal && other.TryGetComponent<Crystal>(out var crystal))
             {
-                // NEW -> Vaccum logic transferred from skimmer to crystal, to reduce crystal dependency
-                crystal.Vacuum(transform.position, skimmer.VaccumAmount);
+                // NEW -> Vacuum logic transferred from skimmer to crystal, to reduce crystal dependency
+                // crystal.Vacuum(transform.position, skimmer.VacuumAmount);
                 // skimmer.TryVacuumCrystal(crystal);
                 // no return; a Crystal may also have a TrailBlock? (unlikely, safe to continue)
             }
@@ -59,6 +59,7 @@ namespace CosmicShore.Game
             
             float sqrDistance = (skimmer.transform.position - other.transform.position).sqrMagnitude;
 
+            return; 
             float scale = skimmer.transform.localScale.x;
             float sqrSweetSpot = scale * scale / 16f;
             float sigma = sqrSweetSpot / 2.355f; 
diff --git a/Assets/_Scripts/Game/Ship/ClearPrisms.cs b/Assets/_Scripts/Game/Ship/ClearPrisms.cs
deleted file mode 100644
index 1b7f58215..000000000
--- a/Assets/_Scripts/Game/Ship/ClearPrisms.cs
+++ /dev/null
@@ -1,124 +0,0 @@
-using UnityEngine;
-using CosmicShore.Core;
-using CosmicShore.Game;
-
-namespace CosmicShore
-{
-    public class ClearPrisms : MonoBehaviour
-    {
-        Transform mainCamera;
-
-        [SerializeField, RequireInterface(typeof(IVessel))]
-        Object _shipMono;
-        IVessel Vessel => _shipMono as IVessel;
-
-        [SerializeField] AnimationCurve scaleCurve = AnimationCurve.Linear(0, 0, 1, 1);
-        [SerializeField] float capsuleRadius = 5f;
-
-        Transform visibilityCapsuleTransform;
-
-        private CapsuleCollider visibilityCapsule;
-
-        Vector3 capsuleDirection;
-
-        CameraManager cameraManager;
-        GeometryUtils.LineData lineData;
-        
-        bool isInitialized;
-
-
-        private void OnEnable()
-        {
-            if (Vessel == null)
-            {
-                Debug.LogError("Vessel instance is not set or does not implement IVessel interface.");
-                enabled = false;
-                return;
-            }
-
-            Vessel.OnInitialized += VesselInitialized;
-            Vessel.OnBeforeDestroyed += OnBeforeVesselDestroyed;
-        }
-
-        private void OnDisable()
-        {
-            Vessel.OnInitialized -= VesselInitialized;
-            Vessel.OnBeforeDestroyed -= OnBeforeVesselDestroyed;
-        }
-
-        private void OnBeforeVesselDestroyed() => isInitialized = false; // Destroy(gameObject);
-
-
-        private void VesselInitialized()
-        {
-            if (!Vessel.IsOwnerClient && Vessel.VesselStatus.IsInitializedAsAI) 
-                return;
-            
-            cameraManager = CameraManager.Instance;
-            mainCamera = cameraManager.GetCloseCamera();
-            if (mainCamera == null)
-            {
-                Debug.LogError("Close main camera not found! This should not happen!");
-                return;
-            }
-            
-            visibilityCapsuleTransform = new GameObject("Visibility Capsule").transform;
-            transform.SetParent(visibilityCapsuleTransform);
-            visibilityCapsule = gameObject.AddComponent<CapsuleCollider>();
-            visibilityCapsule.isTrigger = true;
-            visibilityCapsule.radius = capsuleRadius;
-
-            isInitialized = true;
-        }
-
-        void Update()
-        {
-            if (!isInitialized)
-                return;
-
-            Vector3 cameraPosition = mainCamera.position;
-            Vector3 shipPosition = Vessel.Transform.position;
-
-            // Position the capsule between the camera and the vessel
-            transform.position = (cameraPosition + shipPosition) / 2f;
-            transform.LookAt(shipPosition);
-
-            // Scale the capsule to fit between the camera and vessel
-            float distance = Vector3.Distance(cameraPosition, shipPosition);
-            visibilityCapsule.height = distance;
-
-            // Update the capsule's end positions
-            capsuleDirection = (shipPosition - cameraPosition).normalized;
-            visibilityCapsule.center = Vector3.zero;
-            transform.up = capsuleDirection;
-            lineData = GeometryUtils.PrecomputeLineData(cameraPosition, shipPosition);
-        }
-
-        void OnTriggerEnter(Collider other)
-        {
-            Prism prism = other.GetComponent<Prism>();
-            if (prism != null)
-            {
-                prism.SetTransparency(true);
```

</details>
