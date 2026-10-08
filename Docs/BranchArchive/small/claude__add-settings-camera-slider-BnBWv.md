# Branch archive: `claude/add-settings-camera-slider-BnBWv`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-01 by Claude
- **Unmerged commits:** 1
- **Forked from:** `dfe12d248` (2026-03-01, Merge pull request #303 from froglet-studio/claude/fix-camera-follow-vessel-u2)
- **Tip:** `a8df5f98a`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Camera/CustomCameraController.cs`
  - `Assets/_Scripts/Controller/Settings/GameSetting.cs`
  - `Assets/_Scripts/UI/Modals/SettingsModal.cs`

### `a8df5f98a` — feat(settings): add camera offset multiplier slider to game settings

_Claude, 2026-03-01 08:21:54 +0000_

```text
Add a camera follow-offset multiplier (-1 to 1, default 0) that scales
the camera's follow offset from the vessel. At 0 the offset matches the
CameraSettingsSO default; at 1 it doubles; at -1 it zeroes out.

- GameSetting: new CameraOffsetMultiplier property, PlayerPrefs
  persistence, and static OnChangeCameraOffsetMultiplier event
- CustomCameraController: tracks base offset separately, subscribes to
  the GameSetting event, and recalculates follow offset via multiplier
- SettingsModal: new AdjustCameraOffset(float) delegation method for
  UI slider binding
```

```text
 Assets/_Scripts/Controller/Camera/CustomCameraController.cs | 41 ++++++++++++++++++++++++++++++++++-------
 Assets/_Scripts/Controller/Settings/GameSetting.cs          | 15 +++++++++++++++
 Assets/_Scripts/UI/Modals/SettingsModal.cs                  |  4 ++++
 3 files changed, 53 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 163 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Camera/CustomCameraController.cs b/Assets/_Scripts/Controller/Camera/CustomCameraController.cs
index e8cf10aa5..dfa211935 100644
--- a/Assets/_Scripts/Controller/Camera/CustomCameraController.cs
+++ b/Assets/_Scripts/Controller/Camera/CustomCameraController.cs
@@ -1,4 +1,3 @@
-using System.Collections;
 using CosmicShore.Utility;
 using UnityEngine;
 using Camera = UnityEngine.Camera;
@@ -9,7 +8,9 @@ namespace CosmicShore.Gameplay
     public class CustomCameraController : MonoBehaviour, ICameraController
     {
         private Transform _followTarget;
-        private Vector3 _followOffset = new(0f, 10f, 0f); 
+        private Vector3 _followOffset = new(0f, 10f, 0f);
+        private Vector3 _baseFollowOffset = new(0f, 10f, 0f);
+        private float _offsetMultiplier;
 
         // --- Smoothing and Update Control ---
         private float _followSmoothTime = 0.2f;
@@ -28,6 +29,18 @@ namespace CosmicShore.Gameplay
         {
             Camera = GetComponent<Camera>();
             Camera.useOcclusionCulling = false;
+            _offsetMultiplier = PlayerPrefs.GetFloat(
+                nameof(GameSetting.PlayerPrefKeys.CameraOffsetMultiplier), 0f);
+        }
+
+        private void OnEnable()
+        {
+            GameSetting.OnChangeCameraOffsetMultiplier += SetOffsetMultiplier;
+        }
+
+        private void OnDisable()
+        {
+            GameSetting.OnChangeCameraOffsetMultiplier -= SetOffsetMultiplier;
         }
 
         private void LateUpdate()
@@ -88,20 +101,22 @@ namespace CosmicShore.Gameplay
 
             if (flags.HasFlag(CameraMode.DynamicCamera))
             {
-                _followOffset.x = settings.followOffset.x;
-                _followOffset.y = settings.followOffset.y;
+                _baseFollowOffset.x = settings.followOffset.x;
+                _baseFollowOffset.y = settings.followOffset.y;
+                _baseFollowOffset.z = settings.dynamicMinDistance;
 
                 _followSmoothTime = settings.followSmoothTime;
                 _rotationSmoothTime = settings.rotationSmoothTime;
                 _disableRotationLerp = settings.disableSmoothing;
 
-                SetCameraDistance(settings.dynamicMinDistance);
+                ApplyOffsetMultiplier();
             }
             else
             {
-                _followOffset = settings.followOffset;
+                _baseFollowOffset = settings.followOffset;
                 _disableRotationLerp = true;
                 adaptiveZoomEnabled = settings.enableAdaptiveZoom;
+                ApplyOffsetMultiplier();
                 _neutralOffsetZ = _followOffset.z;
             }
         }
@@ -172,7 +187,19 @@ namespace CosmicShore.Gameplay
         /// </summary>
         public void SetFollowOffset(Vector3 offset)
         {
-            _followOffset = offset;
+            _baseFollowOffset = offset;
+            ApplyOffsetMultiplier();
+        }
+
+        public void SetOffsetMultiplier(float multiplier)
+        {
+            _offsetMultiplier = Mathf.Clamp(multiplier, -1f, 1f);
+            ApplyOffsetMultiplier();
+        }
+
+        private void ApplyOffsetMultiplier()
+        {
+            _followOffset = _baseFollowOffset * (1f + _offsetMultiplier);
         }
 
         /// <summary>
diff --git a/Assets/_Scripts/Controller/Settings/GameSetting.cs b/Assets/_Scripts/Controller/Settings/GameSetting.cs
index 1b88c6c8b..174de364f 100644
--- a/Assets/_Scripts/Controller/Settings/GameSetting.cs
+++ b/Assets/_Scripts/Controller/Settings/GameSetting.cs
@@ -32,6 +32,9 @@ namespace CosmicShore.Gameplay
         public delegate void OnChangeHapticsLevelEvent(float level);
         public static event OnChangeHapticsLevelEvent OnChangeHapticsLevel;
 
+        public delegate void OnChangeCameraOffsetMultiplierEvent(float multiplier);
+        public static event OnChangeCameraOffsetMultiplierEvent OnChangeCameraOffsetMultiplier;
+
         public enum PlayerPrefKeys
         {
             IsInitialPlay = 1,
@@ -48,6 +51,7 @@ namespace CosmicShore.Gameplay
             SFXLevel = 12,
             HapticsLevel = 13,
             LastMissionDifficulty = 14,
+            CameraOffsetMultiplier = 15,
     }
 
         #region Settings
@@ -62,6 +66,7 @@ namespace CosmicShore.Gameplay
         float musicLevel = 1.0f;
         float sfxLevel = 1.0f;
         float hapticsLevel = 1.0f;
+        float cameraOffsetMultiplier;
 
         public bool MusicEnabled { get => musicEnabled; }
         public bool SFXEnabled { get => sfxEnabled; }
@@ -72,6 +77,7 @@ namespace CosmicShore.Gameplay
         public float MusicLevel { get => musicLevel; }
         public float SFXLevel { get => sfxLevel; }
         public float HapticsLevel { get => hapticsLevel; }
+        public float CameraOffsetMultiplier { get => cameraOffsetMultiplier; }
         #endregion
 
         void Awake()
@@ -101,6 +107,7 @@ namespace CosmicShore.Gameplay
             musicLevel = PlayerPrefs.GetFloat(nameof(PlayerPrefKeys.MusicLevel));
             sfxLevel = PlayerPrefs.GetFloat(nameof(PlayerPrefKeys.SFXLevel));
             hapticsLevel = PlayerPrefs.GetFloat(nameof(PlayerPrefKeys.HapticsLevel));
+            cameraOffsetMultiplier = PlayerPrefs.GetFloat(nameof(PlayerPrefKeys.CameraOffsetMultiplier), 0f);
         }
 
         /// <summary>
@@ -189,6 +196,14 @@ namespace CosmicShore.Gameplay
             OnChangeHapticsLevel?.Invoke(hapticsLevel);
         }
 
+        public void SetCameraOffsetMultiplier(float multiplier)
+        {
+            cameraOffsetMultiplier = Mathf.Clamp(multiplier, -1f, 1f);
+            PlayerPrefs.SetFloat(nameof(PlayerPrefKeys.CameraOffsetMultiplier), cameraOffsetMultiplier);
+            PlayerPrefs.Save();
+            OnChangeCameraOffsetMultiplier?.Invoke(cameraOffsetMultiplier);
+        }
+
         void SetPlayerPrefDefault(PlayerPrefKeys key, int value)
         {
             if (!PlayerPrefs.HasKey(key.ToString())) PlayerPrefs.SetInt(key.ToString(), value);
diff --git a/Assets/_Scripts/UI/Modals/SettingsModal.cs b/Assets/_Scripts/UI/Modals/SettingsModal.cs
index 7dc7361f6..618bb37de 100644
```

</details>
