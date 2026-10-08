# Branch archive: `codex/smooth-camera-follow-for-sparrow.prefab`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2025-07-16 by Shombith03
- **Unmerged commits:** 1
- **Forked from:** `d09516eed` (2025-07-15, Update Player V Cam)
- **Tip:** `ac62c977e`
- **Files touched (2):**
  - `Assets/_Scripts/Game/Camera/CustomCameraController.cs`
  - `Assets/_Scripts/Game/Managers/CameraManager.cs`

### `ac62c977e` — feat: adjust camera follow smoothing

_Shombith03, 2025-07-16 01:10:56 +0530_

```text
 Assets/_Scripts/Game/Camera/CustomCameraController.cs | 21 ++++++++++++++++++---
 Assets/_Scripts/Game/Managers/CameraManager.cs        |  2 ++
 2 files changed, 20 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Camera/CustomCameraController.cs b/Assets/_Scripts/Game/Camera/CustomCameraController.cs
index 4bfdc618d..6ebe8d0fa 100644
--- a/Assets/_Scripts/Game/Camera/CustomCameraController.cs
+++ b/Assets/_Scripts/Game/Camera/CustomCameraController.cs
@@ -9,6 +9,8 @@ namespace CosmicShore.Game.CameraSystem
         [SerializeField] Vector3 followOffset = new(0f, 10f, -50f);
         [SerializeField] float followSmoothTime = 0.2f;
         [SerializeField] float rotationSmoothTime = 5f;
+        [SerializeField] bool smoothPosition = true;
+        [SerializeField] bool smoothRotation = true;
         [SerializeField] bool useFixedUpdate = false;
         [SerializeField] float farClipPlane = 10000f;
         [SerializeField] float fieldOfView = 60f;
@@ -67,15 +69,25 @@ namespace CosmicShore.Game.CameraSystem
 
             // 2) Move the camera to ship.position + that rotated offset
             Vector3 desiredPos = followTarget.position + offsetRot * followOffset;
-            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, followSmoothTime);
+            if (smoothPosition && followSmoothTime > 0f)
+                transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, followSmoothTime);
+            else
+                transform.position = desiredPos;
 
             // 3) Look at the ship using its up vector so roll is preserved
             Vector3 toTarget = followTarget.position - transform.position;
             Quaternion targetRot = Quaternion.LookRotation(toTarget, followTarget.up);
 
             // 4) Smooth?damp into that rotation
-            float t = 1f - Mathf.Exp(-rotationSmoothTime * Time.deltaTime);
-            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
+            if (smoothRotation)
+            {
+                float t = 1f - Mathf.Exp(-rotationSmoothTime * Time.deltaTime);
+                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
+            }
+            else
+            {
+                transform.rotation = targetRot;
+            }
         }
 
         public void SetFollowTarget(Transform target) => followTarget = target;
@@ -85,6 +97,9 @@ namespace CosmicShore.Game.CameraSystem
         }
         public Vector3 GetFollowOffset() => followOffset;
 
+        public void EnableSmoothFollow(bool enable) => smoothPosition = enable;
+        public void EnableSmoothRotation(bool enable) => smoothRotation = enable;
+
         public void SetFieldOfView(float fov) => cachedCamera.fieldOfView = fov;
         public void SetClipPlanes(float near, float far)
         {
diff --git a/Assets/_Scripts/Game/Managers/CameraManager.cs b/Assets/_Scripts/Game/Managers/CameraManager.cs
index 0ca03f319..b20849ed3 100644
--- a/Assets/_Scripts/Game/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Game/Managers/CameraManager.cs
@@ -137,6 +137,7 @@ public class CameraManager : SingletonPersistent<CameraManager>
     public void OnMainMenu()
     {
         SetMainMenuCameraActive();
+        playerCamera.EnableSmoothFollow(true);
         _themeManagerData.SetBackgroundColor(Camera.main);
     }
 
@@ -150,6 +151,7 @@ public class CameraManager : SingletonPersistent<CameraManager>
     {
         playerFollowTarget = _transform;
         playerCamera.SetFollowTarget(playerFollowTarget);
+        playerCamera.EnableSmoothFollow(!FollowOverride);
         deathCamera.SetFollowTarget(playerFollowTarget);
         _themeManagerData.SetBackgroundColor(Camera.main);
 
```

</details>
