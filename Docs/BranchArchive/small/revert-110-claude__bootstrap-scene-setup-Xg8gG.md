# Branch archive: `revert-110-claude/bootstrap-scene-setup-Xg8gG`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-25 by Yash Sadhukhan
- **Unmerged commits:** 1
- **Forked from:** `88328f360` (2026-02-25, Add Unity .meta files for Bootstrap scripts)
- **Tip:** `fdc5a4311`
- **Files touched (5):**
  - `Assets/_Scripts/Systems/Bootstrap/ApplicationLifecycleManager.cs`
  - `Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs`
  - `Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs`
  - `Assets/_Scripts/Systems/Bootstrap/IBootstrapService.cs`
  - `Assets/_Scripts/Systems/Bootstrap/ServiceLocator.cs`

### `fdc5a4311` — Revert "Add bootstrap system with service initialization and lifecycle management"

_Yash Sadhukhan, 2026-02-25 13:11:14 +0530_

```text
 Assets/_Scripts/Systems/Bootstrap/ApplicationLifecycleManager.cs | 101 -------------
 Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs           |  43 ------
 Assets/_Scripts/Systems/Bootstrap/BootstrapController.cs         | 290 -------------------------------------
 Assets/_Scripts/Systems/Bootstrap/IBootstrapService.cs           |  29 ----
 Assets/_Scripts/Systems/Bootstrap/ServiceLocator.cs              | 103 -------------
 5 files changed, 566 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 596 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Systems/Bootstrap/ApplicationLifecycleManager.cs b/Assets/_Scripts/Systems/Bootstrap/ApplicationLifecycleManager.cs
deleted file mode 100644
index 155f7002d..000000000
--- a/Assets/_Scripts/Systems/Bootstrap/ApplicationLifecycleManager.cs
+++ /dev/null
@@ -1,101 +0,0 @@
-using System;
-using UnityEngine;
-using UnityEngine.SceneManagement;
-
-namespace CosmicShore.Systems.Bootstrap
-{
-    /// <summary>
-    /// Centralized application lifecycle event dispatcher.
-    /// Place on the persistent Bootstrap root so it survives scene loads.
-    ///
-    /// Other systems subscribe to the static events instead of implementing
-    /// OnApplicationPause/Focus/Quit in every MonoBehaviour.
-    /// </summary>
-    public class ApplicationLifecycleManager : MonoBehaviour
-    {
-        /// <summary>
-        /// Fired when the app is paused (true) or resumed (false).
-        /// Mobile: triggered by backgrounding/foregrounding.
-        /// </summary>
-        public static event Action<bool> OnAppPaused;
-
-        /// <summary>
-        /// Fired when the app gains (true) or loses (false) focus.
-        /// Desktop: alt-tab, overlay windows, etc.
-        /// </summary>
-        public static event Action<bool> OnAppFocusChanged;
-
-        /// <summary>
-        /// Fired once when the application is about to quit.
-        /// Use for save operations and cleanup.
-        /// </summary>
-        public static event Action OnAppQuitting;
-
-        /// <summary>
-        /// Fired when a new scene finishes loading. Passes the loaded scene and load mode.
-        /// </summary>
-        public static event Action<Scene, LoadSceneMode> OnSceneLoaded;
-
-        /// <summary>
-        /// Fired just before a scene is unloaded.
-        /// </summary>
-        public static event Action<Scene> OnSceneUnloading;
-
-        static bool _isQuitting;
-
-        /// <summary>
-        /// Check this to guard against late operations during shutdown
-        /// (e.g., avoiding Instantiate calls during OnDestroy).
-        /// </summary>
-        public static bool IsQuitting => _isQuitting;
-
-        void OnEnable()
-        {
-            SceneManager.sceneLoaded += HandleSceneLoaded;
-            SceneManager.sceneUnloaded += HandleSceneUnloaded;
-        }
-
-        void OnDisable()
-        {
-            SceneManager.sceneLoaded -= HandleSceneLoaded;
-            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
-        }
-
-        void OnApplicationPause(bool pauseStatus) => OnAppPaused?.Invoke(pauseStatus);
-
-        void OnApplicationFocus(bool hasFocus) => OnAppFocusChanged?.Invoke(hasFocus);
-
-        void OnApplicationQuit()
-        {
-            _isQuitting = true;
-            OnAppQuitting?.Invoke();
-            ServiceLocator.ClearAll();
-        }
-
-        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
-        {
-            OnSceneLoaded?.Invoke(scene, mode);
-        }
-
-        void HandleSceneUnloaded(Scene scene)
-        {
-            // Clear scene-scoped services when any scene unloads.
-            ServiceLocator.ClearSceneServices();
-            OnSceneUnloading?.Invoke(scene);
-        }
-
-        /// <summary>
-        /// Reset static state on domain reload (editor play mode toggling).
-        /// </summary>
-        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
-        static void ResetStatics()
-        {
-            _isQuitting = false;
-            OnAppPaused = null;
-            OnAppFocusChanged = null;
-            OnAppQuitting = null;
-            OnSceneLoaded = null;
-            OnSceneUnloading = null;
-        }
-    }
-}
diff --git a/Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs b/Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs
deleted file mode 100644
index f66088c01..000000000
--- a/Assets/_Scripts/Systems/Bootstrap/BootstrapConfigSO.cs
+++ /dev/null
@@ -1,43 +0,0 @@
-using UnityEngine;
-
-namespace CosmicShore.Systems.Bootstrap
-{
-    [CreateAssetMenu(
-        fileName = "BootstrapConfig",
-        menuName = "ScriptableObjects/Core/BootstrapConfig")]
-    public class BootstrapConfigSO : ScriptableObject
-    {
-        [Header("Scene Transition")]
-        [SerializeField, Tooltip("Scene to load after bootstrap completes successfully.")]
-        string _firstSceneName = "Menu_Main";
-
-        [Header("Timeouts")]
-        [SerializeField, Tooltip("Max seconds to wait for all services to initialize before giving up.")]
-        float _serviceInitTimeoutSeconds = 15f;
-
-        [SerializeField, Tooltip("Minimum seconds to show the splash/loading screen.")]
-        float _minimumSplashDuration = 1f;
-
-        [Header("Platform Settings")]
-        [SerializeField, Tooltip("Target framerate. 0 = platform default.")]
-        int _targetFrameRate = 60;
-
-        [SerializeField, Tooltip("Prevent the screen from dimming during gameplay.")]
-        bool _preventScreenSleep = true;
-
-        [SerializeField, Tooltip("VSync count. 0 = off, 1 = every VBlank, 2 = every other VBlank.")]
-        int _vSyncCount = 0;
-
-        [Header("Debug")]
-        [SerializeField, Tooltip("Log detailed bootstrap timing to the console.")]
-        bool _verboseLogging;
-
-        public string FirstSceneName => _firstSceneName;
-        public float ServiceInitTimeoutSeconds => _serviceInitTimeoutSeconds;
-        public float MinimumSplashDuration => _minimumSplashDuration;
```

</details>
