# Branch archive: `claude/game-trailer-camera-tool-f9zK9`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-03 by Claude
- **Unmerged commits:** 7
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/347
- **Forked from:** `98fdd8823` (2026-03-04, Update MinigameJoust_Gameplay.unity)
- **Tip:** `b2475ae60`
- **Files touched (5):**
  - `Assets/_Scripts/Editor/TrailerCameraToolWindow.cs`
  - `Assets/_Scripts/Utility/Trailer/TrailerCameraConfigSO.cs`
  - `Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs`
  - `Assets/_Scripts/Utility/Trailer/TrailerCameraRig.cs`
  - `Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs`

### `44a11fca5` — Add game trailer camera tool for cinematic capture

_Claude, 2026-03-03 21:59:21 +0000_

```text
Multi-camera recording system for Hex Race, Crystal Capture, and Joust
game modes. Creates 6 configurable cinematic cameras (chase, side, front,
high orbit, low hero, slow orbit) that track the vessel and capture
high-resolution PNG frame sequences at game end.

- TrailerCameraConfigSO: ScriptableObject for all camera, recording, and
  output settings (resolution presets 1080p/1440p/4K, FPS, AA, UI toggle)
- TrailerCameraRig: Runtime multi-camera system rendering to off-screen
  RenderTextures with per-camera smoothing and orbit behaviors
- TrailerClipRecorder: Frame capture engine with auto-trigger on game end,
  timestamped session folders, and per-camera PNG output
- TrailerCameraController: Top-level orchestrator that auto-discovers the
  local vessel via GameDataSO events
- TrailerCameraToolWindow: Editor window (Tools > Cosmic Shore > Trailer
  Camera Tool) with live camera previews, recording controls, config
  editing, and output folder management
```

```text
 Assets/_Scripts/Editor/TrailerCameraToolWindow.cs          | 443 +++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Utility/Trailer/TrailerCameraConfigSO.cs   | 146 ++++++++++++++
 Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs | 157 +++++++++++++++
 Assets/_Scripts/Utility/Trailer/TrailerCameraRig.cs        | 237 +++++++++++++++++++++++
 Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs     | 231 ++++++++++++++++++++++
 5 files changed, 1214 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 1244 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs b/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
new file mode 100644
index 000000000..db78ed994
--- /dev/null
+++ b/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
@@ -0,0 +1,443 @@
+#if UNITY_EDITOR
+using System.IO;
+using System.Linq;
+using CosmicShore.Game;
+using CosmicShore.Utility.Trailer;
+using UnityEditor;
+using UnityEngine;
+
+namespace CosmicShore.Editor
+{
+    /// <summary>
+    /// Editor window for controlling the Trailer Camera system.
+    /// Provides controls to:
+    ///   - Assign/create a TrailerCameraConfigSO
+    ///   - Toggle UI visibility during recording
+    ///   - Toggle recording on/off
+    ///   - Monitor camera count and recording progress
+    ///   - Open the output folder
+    ///
+    /// Works at runtime in the editor — play the game in a supported mode
+    /// (Hex Race, Crystal Capture, Joust), then use this window to control capture.
+    /// </summary>
+    public class TrailerCameraToolWindow : EditorWindow
+    {
+        private TrailerCameraConfigSO _config;
+        private TrailerCameraController _runtimeController;
+        private Vector2 _scrollPos;
+        private bool _showCameraFoldout = true;
+        private bool _showRecordingFoldout = true;
+        private bool _showOutputFoldout = true;
+        private string _lastOutputPath;
+
+        [MenuItem("Tools/Cosmic Shore/Trailer Camera Tool")]
+        public static void ShowWindow()
+        {
+            var window = GetWindow<TrailerCameraToolWindow>("Trailer Camera");
+            window.minSize = new Vector2(380, 500);
+        }
+
+        private void OnEnable()
+        {
+            EditorApplication.playModeStateChanged += OnPlayModeChanged;
+        }
+
+        private void OnDisable()
+        {
+            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
+        }
+
+        private void OnPlayModeChanged(PlayModeStateChange change)
+        {
+            if (change == PlayModeStateChange.ExitingPlayMode)
+                _runtimeController = null;
+
+            Repaint();
+        }
+
+        private void OnInspectorUpdate()
+        {
+            // Repaint periodically to update progress bars during recording
+            if (Application.isPlaying)
+                Repaint();
+        }
+
+        private void OnGUI()
+        {
+            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
+
+            DrawHeader();
+            EditorGUILayout.Space(8);
+            DrawConfigSection();
+            EditorGUILayout.Space(8);
+
+            if (_config != null)
+            {
+                DrawCameraSection();
+                EditorGUILayout.Space(8);
+                DrawRecordingSection();
+                EditorGUILayout.Space(8);
+                DrawOutputSection();
+                EditorGUILayout.Space(8);
+            }
+
+            if (Application.isPlaying)
+            {
+                DrawRuntimeControls();
+            }
+            else
+            {
+                EditorGUILayout.HelpBox(
+                    "Enter Play Mode in a supported game scene (Hex Race, Crystal Capture, or Joust) " +
+                    "to use runtime controls.",
+                    MessageType.Info);
+            }
+
+            EditorGUILayout.EndScrollView();
+        }
+
+        private void DrawHeader()
+        {
+            EditorGUILayout.LabelField("Trailer Camera Tool", EditorStyles.boldLabel);
+            EditorGUILayout.LabelField("Capture cinematic footage from multiple angles during gameplay.",
+                EditorStyles.wordWrappedMiniLabel);
+        }
+
+        private void DrawConfigSection()
+        {
+            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
+
+            EditorGUI.BeginChangeCheck();
+            _config = (TrailerCameraConfigSO)EditorGUILayout.ObjectField(
+                "Config Asset", _config, typeof(TrailerCameraConfigSO), false);
+            if (EditorGUI.EndChangeCheck() && _config != null)
+                EditorUtility.SetDirty(_config);
+
+            if (_config == null)
+            {
+                EditorGUILayout.HelpBox(
+                    "Assign or create a TrailerCameraConfigSO to configure cameras and recording.",
+                    MessageType.Warning);
+
+                if (GUILayout.Button("Create Default Config Asset"))
+                    CreateDefaultConfig();
+            }
+        }
+
+        private void DrawCameraSection()
+        {
+            _showCameraFoldout = EditorGUILayout.Foldout(_showCameraFoldout, "Camera Setups", true, EditorStyles.foldoutHeader);
+            if (!_showCameraFoldout) return;
+
+            EditorGUI.indentLevel++;
+
+            int enabledCount = _config.cameraSetups.Count(c => c.enabled);
+            EditorGUILayout.LabelField($"{enabledCount} / {_config.cameraSetups.Count} cameras enabled");
+
+            for (int i = 0; i < _config.cameraSetups.Count; i++)
+            {
+                var setup = _config.cameraSetups[i];
+                EditorGUILayout.BeginHorizontal();
+
+                setup.enabled = EditorGUILayout.Toggle(setup.enabled, GUILayout.Width(20));
+                EditorGUILayout.LabelField($"{setup.label} ({setup.cameraType})", EditorStyles.miniLabel);
+
```

</details>

### `e53bdf8ef` — Simplify trailer tool: auto random clips, fix crash, remove start/stop

_Claude, 2026-03-03 22:29:31 +0000_

```text
Reworked based on feedback:
- Auto-creates camera rig when game turn starts (if tool enabled)
- Schedules random 5-second clip captures throughout the match
  (configurable count via numberOfRandomClips)
- Replaced Start/Stop Recording with a single "Record Next 5s" button
  for one-off custom captures
- Fixed crash: removed Canvas disable/enable logic that broke game
  systems — UI is now hidden from trailer cameras via culling mask only
- File writes moved to background threads to avoid frame stalls
- Simplified editor window layout with clear sections
```

```text
 Assets/_Scripts/Editor/TrailerCameraToolWindow.cs          | 321 ++++++++++++++++++-------------------------
 Assets/_Scripts/Utility/Trailer/TrailerCameraConfigSO.cs   |  29 ++--
 Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs | 140 +++++++++++--------
 Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs     | 167 +++++++---------------
 4 files changed, 293 insertions(+), 364 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1096 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs b/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
index db78ed994..28d8b08d2 100644
--- a/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
+++ b/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
@@ -9,16 +9,18 @@ using UnityEngine;
 namespace CosmicShore.Editor
 {
     /// <summary>
-    /// Editor window for controlling the Trailer Camera system.
-    /// Provides controls to:
-    ///   - Assign/create a TrailerCameraConfigSO
-    ///   - Toggle UI visibility during recording
-    ///   - Toggle recording on/off
-    ///   - Monitor camera count and recording progress
-    ///   - Open the output folder
+    /// Editor window at <b>Tools > Cosmic Shore > Trailer Camera Tool</b>.
     ///
-    /// Works at runtime in the editor — play the game in a supported mode
-    /// (Hex Race, Crystal Capture, Joust), then use this window to control capture.
+    /// When the tool is enabled and a supported game mode starts (Hex Race,
+    /// Crystal Capture, Joust), the system auto-creates a multi-camera rig
+    /// around the vessel and captures random 5-second clips throughout the match.
+    ///
+    /// The window lets you configure:
+    ///   - Enable/disable the tool
+    ///   - Number of random clips per match
+    ///   - Camera setups and UI visibility
+    ///   - Resolution and quality presets
+    ///   - A one-shot "Record Next 5s" button for custom captures
     /// </summary>
     public class TrailerCameraToolWindow : EditorWindow
     {
@@ -26,15 +28,13 @@ namespace CosmicShore.Editor
         private TrailerCameraController _runtimeController;
         private Vector2 _scrollPos;
         private bool _showCameraFoldout = true;
-        private bool _showRecordingFoldout = true;
-        private bool _showOutputFoldout = true;
-        private string _lastOutputPath;
+        private bool _showQualityFoldout;
 
         [MenuItem("Tools/Cosmic Shore/Trailer Camera Tool")]
         public static void ShowWindow()
         {
             var window = GetWindow<TrailerCameraToolWindow>("Trailer Camera");
-            window.minSize = new Vector2(380, 500);
+            window.minSize = new Vector2(360, 420);
         }
 
         private void OnEnable()
@@ -51,62 +51,56 @@ namespace CosmicShore.Editor
         {
             if (change == PlayModeStateChange.ExitingPlayMode)
                 _runtimeController = null;
-
             Repaint();
         }
 
         private void OnInspectorUpdate()
         {
-            // Repaint periodically to update progress bars during recording
-            if (Application.isPlaying)
-                Repaint();
+            if (Application.isPlaying) Repaint();
         }
 
         private void OnGUI()
         {
             _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
 
-            DrawHeader();
-            EditorGUILayout.Space(8);
+            EditorGUILayout.LabelField("Trailer Camera Tool", EditorStyles.boldLabel);
+            EditorGUILayout.Space(4);
+
+            // ── Config asset ──
             DrawConfigSection();
-            EditorGUILayout.Space(8);
 
             if (_config != null)
             {
+                EditorGUILayout.Space(8);
+                DrawToolToggle();
+                EditorGUILayout.Space(8);
+                DrawClipSettings();
+                EditorGUILayout.Space(8);
                 DrawCameraSection();
                 EditorGUILayout.Space(8);
-                DrawRecordingSection();
+                DrawQualitySection();
                 EditorGUILayout.Space(8);
                 DrawOutputSection();
-                EditorGUILayout.Space(8);
             }
 
+            EditorGUILayout.Space(12);
+
             if (Application.isPlaying)
-            {
                 DrawRuntimeControls();
-            }
             else
-            {
                 EditorGUILayout.HelpBox(
-                    "Enter Play Mode in a supported game scene (Hex Race, Crystal Capture, or Joust) " +
-                    "to use runtime controls.",
+                    "Enter Play Mode in Hex Race, Crystal Capture, or Joust to see runtime controls.",
                     MessageType.Info);
-            }
 
             EditorGUILayout.EndScrollView();
         }
 
-        private void DrawHeader()
-        {
-            EditorGUILayout.LabelField("Trailer Camera Tool", EditorStyles.boldLabel);
-            EditorGUILayout.LabelField("Capture cinematic footage from multiple angles during gameplay.",
-                EditorStyles.wordWrappedMiniLabel);
-        }
+        // ────────────────────────────────────────────────────────────────
+        //  Config
+        // ────────────────────────────────────────────────────────────────
 
         private void DrawConfigSection()
         {
-            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
-
             EditorGUI.BeginChangeCheck();
             _config = (TrailerCameraConfigSO)EditorGUILayout.ObjectField(
                 "Config Asset", _config, typeof(TrailerCameraConfigSO), false);
@@ -115,212 +109,185 @@ namespace CosmicShore.Editor
 
             if (_config == null)
             {
-                EditorGUILayout.HelpBox(
-                    "Assign or create a TrailerCameraConfigSO to configure cameras and recording.",
-                    MessageType.Warning);
-
-                if (GUILayout.Button("Create Default Config Asset"))
+                EditorGUILayout.HelpBox("Assign or create a config asset.", MessageType.Warning);
+                if (GUILayout.Button("Create Default Config"))
                     CreateDefaultConfig();
             }
         }
 
+        // ────────────────────────────────────────────────────────────────
+        //  Tool toggle
+        // ────────────────────────────────────────────────────────────────
+
+        private void DrawToolToggle()
```

</details>

### `60ad06646` — Fix auto-setup, lag, and random clip recording

_Claude, 2026-03-03 22:42:29 +0000_

```text
Root causes:
- Controller wasn't in scene at game start → missed event → no random clips
- Recording all 6 cameras + ReadPixels + EncodeToPNG on main thread → lag
- Canvas disable/enable on stop → crash

Fixes:
- Editor window auto-injects controller on play mode entry (no manual
  Create/Force Initialize needed)
- Controller polls for vessel in Update() instead of relying on a
  one-shot event it might miss
- Records from ONE randomly-selected camera per clip, not all 6
- AsyncGPUReadback replaces ReadPixels (no GPU stall)
- PNG encode on main thread (callback), file write on thread pool
- Default FPS lowered to 30
- "Record Next 5s" button now has configurable delay (default 3s)
  before recording starts
- Removed all Canvas manipulation — UI hidden via camera culling mask
```

```text
 Assets/_Scripts/Editor/TrailerCameraToolWindow.cs          | 222 +++++++++++++++++--------------------------
 Assets/_Scripts/Utility/Trailer/TrailerCameraConfigSO.cs   |   7 +-
 Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs | 121 +++++++++++------------
 Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs     | 110 ++++++++++-----------
 4 files changed, 197 insertions(+), 263 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 838 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs b/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
index 28d8b08d2..f299b9200 100644
--- a/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
+++ b/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
@@ -1,7 +1,7 @@
 #if UNITY_EDITOR
 using System.IO;
 using System.Linq;
-using CosmicShore.Game;
+using CosmicShore.Soap;
 using CosmicShore.Utility.Trailer;
 using UnityEditor;
 using UnityEngine;
@@ -11,16 +11,9 @@ namespace CosmicShore.Editor
     /// <summary>
     /// Editor window at <b>Tools > Cosmic Shore > Trailer Camera Tool</b>.
     ///
-    /// When the tool is enabled and a supported game mode starts (Hex Race,
-    /// Crystal Capture, Joust), the system auto-creates a multi-camera rig
-    /// around the vessel and captures random 5-second clips throughout the match.
-    ///
-    /// The window lets you configure:
-    ///   - Enable/disable the tool
-    ///   - Number of random clips per match
-    ///   - Camera setups and UI visibility
-    ///   - Resolution and quality presets
-    ///   - A one-shot "Record Next 5s" button for custom captures
+    /// When enabled, auto-injects a TrailerCameraController into the scene on
+    /// play mode entry. The controller auto-discovers the vessel and begins
+    /// recording random clips. No manual setup required.
     /// </summary>
     public class TrailerCameraToolWindow : EditorWindow
     {
@@ -34,7 +27,7 @@ namespace CosmicShore.Editor
         public static void ShowWindow()
         {
             var window = GetWindow<TrailerCameraToolWindow>("Trailer Camera");
-            window.minSize = new Vector2(360, 420);
+            window.minSize = new Vector2(360, 400);
         }
 
         private void OnEnable()
@@ -49,8 +42,15 @@ namespace CosmicShore.Editor
 
         private void OnPlayModeChanged(PlayModeStateChange change)
         {
-            if (change == PlayModeStateChange.ExitingPlayMode)
-                _runtimeController = null;
+            switch (change)
+            {
+                case PlayModeStateChange.EnteredPlayMode:
+                    InjectControllerIfNeeded();
+                    break;
+                case PlayModeStateChange.ExitingPlayMode:
+                    _runtimeController = null;
+                    break;
+            }
             Repaint();
         }
 
@@ -59,6 +59,46 @@ namespace CosmicShore.Editor
             if (Application.isPlaying) Repaint();
         }
 
+        /// <summary>
+        /// Auto-inject the TrailerCameraController into the scene when
+        /// entering play mode, if the tool is enabled and config is assigned.
+        /// </summary>
+        private void InjectControllerIfNeeded()
+        {
+            if (_config == null || !_config.toolEnabled) return;
+
+            // Check if one already exists
+            _runtimeController = FindAnyObjectByType<TrailerCameraController>();
+            if (_runtimeController != null) return;
+
+            var go = new GameObject("TrailerCameraController");
+            _runtimeController = go.AddComponent<TrailerCameraController>();
+
+            // Wire up serialized references
+            var so = new SerializedObject(_runtimeController);
+
+            var configProp = so.FindProperty("config");
+            if (configProp != null)
+                configProp.objectReferenceValue = _config;
+
+            var gameDataProp = so.FindProperty("gameData");
+            if (gameDataProp != null)
+            {
+                var guids = AssetDatabase.FindAssets("t:GameDataSO");
+                if (guids.Length > 0)
+                {
+                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
+                    gameDataProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameDataSO>(path);
+                }
+            }
+
+            so.ApplyModifiedProperties();
+
+            Debug.Log("[TrailerTool] Controller auto-injected into scene.");
+        }
+
+        // ────────────────────────────────────────────────────────────────
+
         private void OnGUI()
         {
             _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
@@ -66,7 +106,6 @@ namespace CosmicShore.Editor
             EditorGUILayout.LabelField("Trailer Camera Tool", EditorStyles.boldLabel);
             EditorGUILayout.Space(4);
 
-            // ── Config asset ──
             DrawConfigSection();
 
             if (_config != null)
@@ -89,15 +128,14 @@ namespace CosmicShore.Editor
                 DrawRuntimeControls();
             else
                 EditorGUILayout.HelpBox(
-                    "Enter Play Mode in Hex Race, Crystal Capture, or Joust to see runtime controls.",
+                    "Enter Play Mode in Hex Race, Crystal Capture, or Joust.\n" +
+                    "The tool auto-creates cameras and records clips.",
                     MessageType.Info);
 
             EditorGUILayout.EndScrollView();
         }
 
-        // ────────────────────────────────────────────────────────────────
-        //  Config
-        // ────────────────────────────────────────────────────────────────
+        // ── Config ──────────────────────────────────────────────────────
 
         private void DrawConfigSection()
         {
@@ -115,9 +153,7 @@ namespace CosmicShore.Editor
             }
         }
 
-        // ────────────────────────────────────────────────────────────────
-        //  Tool toggle
-        // ────────────────────────────────────────────────────────────────
+        // ── Tool toggle ─────────────────────────────────────────────────
 
         private void DrawToolToggle()
         {
@@ -126,12 +162,10 @@ namespace CosmicShore.Editor
             if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);
 
             if (!_config.toolEnabled)
-                EditorGUILayout.HelpBox("Tool is disabled. Nothing will run in play mode.", MessageType.Info);
```

</details>

### `5ce99c5d0` — Only record in game scenes, not main menu

_Claude, 2026-03-03 22:48:00 +0000_

```text
Controller now persists across scene loads (DontDestroyOnLoad) and
listens for sceneLoaded events. It only activates in game mode scenes
(names starting with "Minigame") and tears down the rig + stops
recording when leaving a game scene. Main menu, splash screen, auth,
and tool scenes are all ignored.

Editor window status now shows the current scene and whether it's a
game scene or idle.
```

```text
 Assets/_Scripts/Editor/TrailerCameraToolWindow.cs          | 18 ++++++++------
 Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs | 56 +++++++++++++++++++++++++++++++++++---------
 2 files changed, 56 insertions(+), 18 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs b/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
index f299b9200..48fb46b5e 100644
--- a/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
+++ b/Assets/_Scripts/Editor/TrailerCameraToolWindow.cs
@@ -60,21 +60,21 @@ namespace CosmicShore.Editor
         }
 
         /// <summary>
-        /// Auto-inject the TrailerCameraController into the scene when
-        /// entering play mode, if the tool is enabled and config is assigned.
+        /// Auto-inject the TrailerCameraController on play mode entry.
+        /// The controller uses DontDestroyOnLoad and listens for scene
+        /// changes itself — it only activates in game scenes (Minigame*).
         /// </summary>
         private void InjectControllerIfNeeded()
         {
             if (_config == null || !_config.toolEnabled) return;
 
-            // Check if one already exists
+            // Controller persists via DontDestroyOnLoad, don't duplicate
             _runtimeController = FindAnyObjectByType<TrailerCameraController>();
             if (_runtimeController != null) return;
 
             var go = new GameObject("TrailerCameraController");
             _runtimeController = go.AddComponent<TrailerCameraController>();
 
-            // Wire up serialized references
             var so = new SerializedObject(_runtimeController);
 
             var configProp = so.FindProperty("config");
@@ -94,7 +94,7 @@ namespace CosmicShore.Editor
 
             so.ApplyModifiedProperties();
 
-            Debug.Log("[TrailerTool] Controller auto-injected into scene.");
+            Debug.Log("[TrailerTool] Controller injected (persists across scenes, active only in game modes).");
         }
 
         // ────────────────────────────────────────────────────────────────
@@ -270,8 +270,12 @@ namespace CosmicShore.Editor
             // ── Status box ──
             EditorGUILayout.BeginVertical("box");
 
-            EditorGUILayout.LabelField("Status",
-                _runtimeController.IsActive ? "Active — tracking vessel" : "Waiting for vessel...");
+            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
+            bool isGameScene = sceneName.StartsWith("Minigame", System.StringComparison.OrdinalIgnoreCase);
+            string status = _runtimeController.IsActive
+                ? "Active — tracking vessel"
+                : isGameScene ? "In game scene — waiting for vessel..." : $"Non-game scene ({sceneName}) — idle";
+            EditorGUILayout.LabelField("Status", status);
 
             if (_runtimeController.Rig != null)
                 EditorGUILayout.LabelField("Cameras", $"{_runtimeController.Rig.Cameras.Count}");
diff --git a/Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs b/Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs
index 2eb7da4e4..851ebe416 100644
--- a/Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs
+++ b/Assets/_Scripts/Utility/Trailer/TrailerCameraController.cs
@@ -1,16 +1,17 @@
 using System.Collections;
 using CosmicShore.Soap;
 using UnityEngine;
+using UnityEngine.SceneManagement;
 
 namespace CosmicShore.Utility.Trailer
 {
     /// <summary>
     /// Top-level runtime controller for the trailer camera system.
     ///
-    /// Auto-discovers the local vessel (polls until found), creates the
-    /// camera rig, and schedules random clip captures throughout the match.
-    /// No manual setup required — the editor window injects this on play
-    /// mode entry when the tool is enabled.
+    /// Persists across scene loads via DontDestroyOnLoad. Only activates
+    /// in game mode scenes (scene names starting with "Minigame").
+    /// Automatically tears down when returning to the main menu or any
+    /// non-game scene, and re-initializes when entering a new game scene.
     /// </summary>
     public class TrailerCameraController : MonoBehaviour
     {
@@ -24,6 +25,7 @@ namespace CosmicShore.Utility.Trailer
         private Coroutine _customClipRoutine;
         private bool _initialized;
         private int _clipsRecorded;
+        private bool _inGameScene;
 
         public TrailerCameraConfigSO Config => config;
         public TrailerCameraRig Rig => _rig;
@@ -31,10 +33,47 @@ namespace CosmicShore.Utility.Trailer
         public bool IsActive => _initialized;
         public int ClipsRecorded => _clipsRecorded;
 
+        private void Awake()
+        {
+            DontDestroyOnLoad(gameObject);
+        }
+
+        private void OnEnable()
+        {
+            SceneManager.sceneLoaded += OnSceneLoaded;
+        }
+
+        private void OnDisable()
+        {
+            SceneManager.sceneLoaded -= OnSceneLoaded;
+            Cleanup();
+        }
+
+        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
+        {
+            // Tear down any existing rig from the previous scene
+            Cleanup();
+
+            _inGameScene = IsGameScene(scene.name);
+
+            if (_inGameScene)
+                CSDebug.Log($"[TrailerCamera] Entered game scene: {scene.name}");
+        }
+
+        /// <summary>
+        /// Game mode scenes start with "Minigame". Everything else
+        /// (Menu_Main, Authentication, SplashScreen, tools) is ignored.
+        /// </summary>
+        private static bool IsGameScene(string sceneName)
+        {
+            return sceneName.StartsWith("Minigame", System.StringComparison.OrdinalIgnoreCase);
+        }
+
         private void Update()
         {
-            // Poll for vessel until we find it and initialize
-            if (_initialized || config == null || !config.toolEnabled || gameData == null) return;
+            // Only poll for vessel in game scenes
+            if (_initialized || !_inGameScene) return;
+            if (config == null || !config.toolEnabled || gameData == null) return;
 
             var vessel = gameData.LocalPlayer?.Vessel;
             if (vessel?.Transform == null) return;
@@ -45,11 +84,6 @@ namespace CosmicShore.Utility.Trailer
                 _randomCaptureRoutine = StartCoroutine(RandomCaptureScheduler());
         }
 
-        private void OnDisable()
-        {
-            Cleanup();
-        }
-
         /// <summary>
         /// Initialize the rig with a vessel transform.
         /// </summary>
```

</details>

### `5bc2bf025` — Output MP4 video, render skybox, match player camera, no lag

_Claude, 2026-03-03 22:59:08 +0000_

```text
Recorder:
- Replaced PNG frame dumps with Unity MediaEncoder for direct MP4 output
- Uses AsyncGPUReadback (not synchronous ReadPixels) so recording never
  stalls the GPU or causes gameplay lag
- Readback callbacks fire in request order, so frames arrive at the
  encoder sequentially
- Pending readbacks drain before the encoder is finalized

Camera rig:
- ClearFlags.Skybox instead of SolidColor — skybox now renders
- farClipPlane 10000 to match the player camera (was 5000)
- Occlusion culling disabled to match the player camera
- Culling mask explicitly set to Everything, only strips UI layer
```

```text
 Assets/_Scripts/Utility/Trailer/TrailerCameraRig.cs    |   9 ++--
 Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs | 109 +++++++++++++++++++++++++++++++----------------
 2 files changed, 77 insertions(+), 41 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 202 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Trailer/TrailerCameraRig.cs b/Assets/_Scripts/Utility/Trailer/TrailerCameraRig.cs
index 8225fd3d3..2069ae049 100644
--- a/Assets/_Scripts/Utility/Trailer/TrailerCameraRig.cs
+++ b/Assets/_Scripts/Utility/Trailer/TrailerCameraRig.cs
@@ -50,13 +50,14 @@ namespace CosmicShore.Utility.Trailer
 
             var cam = cameraGO.AddComponent<Camera>();
             cam.enabled = false; // We render manually via RenderTexture
-            cam.clearFlags = CameraClearFlags.SolidColor;
-            cam.backgroundColor = Color.black;
+            cam.clearFlags = CameraClearFlags.Skybox;
             cam.nearClipPlane = 0.3f;
-            cam.farClipPlane = 5000f;
+            cam.farClipPlane = 10000f;
             cam.fieldOfView = 60f;
+            cam.useOcclusionCulling = false;
 
-            // Hide UI layer from trailer cameras
+            // Match the player camera: render everything, only strip UI
+            cam.cullingMask = -1; // Everything
             if (config.hideUILayer)
             {
                 int uiLayer = LayerMask.NameToLayer("UI");
diff --git a/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs b/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
index faa299f16..cd62f09fb 100644
--- a/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
+++ b/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
@@ -1,21 +1,21 @@
 using System;
 using System.IO;
-using System.Threading;
 using Unity.Collections;
 using UnityEngine;
 using UnityEngine.Rendering;
+#if UNITY_EDITOR
+using UnityEditor.Media;
+#endif
 
 namespace CosmicShore.Utility.Trailer
 {
     /// <summary>
-    /// Records a single clip from ONE randomly-selected trailer camera.
+    /// Records a single clip from ONE randomly-selected trailer camera,
+    /// encoding directly to an MP4 file via Unity's MediaEncoder.
     ///
-    /// Performance approach:
-    ///   - Only one camera renders per clip (not all 6)
-    ///   - AsyncGPUReadback avoids ReadPixels GPU stall
-    ///   - PNG encode happens on the main thread (callback), but with only
-    ///     1 camera at 30fps the cost is negligible
-    ///   - File writes go to the thread pool
+    /// Uses AsyncGPUReadback so recording never stalls the GPU or gameplay.
+    /// Readback callbacks fire in request order on the main thread, so
+    /// frames arrive at the encoder sequentially.
     /// </summary>
     public class TrailerClipRecorder : MonoBehaviour
     {
@@ -23,13 +23,19 @@ namespace CosmicShore.Utility.Trailer
         [SerializeField] private TrailerCameraRig cameraRig;
 
         private bool _isRecording;
-        private string _clipFolder;
+        private string _clipFilePath;
         private int _frameIndex;
         private float _captureInterval;
         private float _captureTimer;
         private float _elapsedRecordTime;
         private int _clipNumber;
         private int _activeCameraIndex;
+        private int _pendingReadbacks;
+        private bool _finishing;
+
+#if UNITY_EDITOR
+        private MediaEncoder _encoder;
+#endif
 
         public bool IsRecording => _isRecording;
         public int ClipNumber => _clipNumber;
@@ -63,7 +69,6 @@ namespace CosmicShore.Utility.Trailer
                 return;
             }
 
-            // Pick a random camera for this clip
             _activeCameraIndex = UnityEngine.Random.Range(0, cameraRig.Cameras.Count);
             var chosen = cameraRig.Cameras[_activeCameraIndex];
 
@@ -72,24 +77,55 @@ namespace CosmicShore.Utility.Trailer
             _elapsedRecordTime = 0f;
             _captureInterval = 1f / config.targetFPS;
             _captureTimer = 0f;
+            _pendingReadbacks = 0;
+            _finishing = false;
             _clipNumber++;
 
+            int w = config.captureWidth;
+            int h = config.captureHeight;
+
             string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
             string rootPath = Path.Combine(Application.dataPath, "..", config.outputFolder);
-            _clipFolder = Path.Combine(rootPath, $"Clip_{_clipNumber:D2}_{chosen.Setup.label}_{timestamp}");
-            Directory.CreateDirectory(_clipFolder);
+            Directory.CreateDirectory(rootPath);
+            _clipFilePath = Path.Combine(rootPath, $"Clip_{_clipNumber:D2}_{chosen.Setup.label}_{timestamp}.mp4");
 
-            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} recording — " +
-                        $"camera: {chosen.Setup.label}, {config.clipDurationSeconds}s @ {config.targetFPS}fps");
+#if UNITY_EDITOR
+            var videoAttr = new VideoTrackAttributes
+            {
+                frameRate = new MediaRational(config.targetFPS),
+                width = (uint)w,
+                height = (uint)h,
+                includeAlpha = false,
+                bitRateMode = VideoBitrateMode.High
+            };
+            _encoder = new MediaEncoder(_clipFilePath, videoAttr);
+#endif
+
+            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} recording → " +
+                        $"camera: {chosen.Setup.label}, {config.clipDurationSeconds}s @ {config.targetFPS}fps → {_clipFilePath}");
         }
 
         private void FinishClip()
         {
             if (!_isRecording) return;
             _isRecording = false;
+            _finishing = true;
 
-            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} saved — {_frameIndex} frames → {_clipFolder}");
-            OnClipFinished?.Invoke(_clipFolder);
+            // If no readbacks are in flight, finalize immediately
+            if (_pendingReadbacks == 0)
+                FinalizeEncoder();
+        }
+
+        private void FinalizeEncoder()
+        {
+#if UNITY_EDITOR
+            _encoder?.Dispose();
+            _encoder = null;
+#endif
+            _finishing = false;
+
+            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} saved — {_frameIndex} frames → {_clipFilePath}");
+            OnClipFinished?.Invoke(_clipFilePath);
         }
 
         private void LateUpdate()
@@ -121,33 +157,32 @@ namespace CosmicShore.Utility.Trailer
             // Render just this one camera
```

</details>

### `17ff78f9f` — Remove VideoBitrateMode — not in Unity 6 MediaEncoder API

_Claude, 2026-03-03 23:01:31 +0000_

```text
VideoTrackAttributes in UnityEditor.Media only has frameRate, width,
height, and includeAlpha. The bitRateMode field doesn't exist.
```

```text
 Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs | 3 +--
 1 file changed, 1 insertion(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs b/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
index cd62f09fb..ef8e668da 100644
--- a/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
+++ b/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
@@ -95,8 +95,7 @@ namespace CosmicShore.Utility.Trailer
                 frameRate = new MediaRational(config.targetFPS),
                 width = (uint)w,
                 height = (uint)h,
-                includeAlpha = false,
-                bitRateMode = VideoBitrateMode.High
+                includeAlpha = false
             };
             _encoder = new MediaEncoder(_clipFilePath, videoAttr);
 #endif
```

</details>

### `b2475ae60` — Fix trailer recorder: wrong playback speed and flipped video

_Claude, 2026-03-03 23:09:31 +0000_

```text
Two bugs causing unusable output:

1. Timer-based capture with Time.deltaTime produced variable frame
   counts depending on actual game FPS. When encoded at the declared
   targetFPS, the video played back fast-forwarded. Fix: use
   Time.captureFramerate to lock the game clock so every frame is
   exactly 1/targetFPS — capture every frame, no timer needed.

2. AsyncGPUReadback from RenderTextures returns pixel data bottom-to-top.
   Without flipping rows, the encoded video was upside-down/garbled.
   Fix: flip NativeArray rows before loading into the Texture2D.
```

```text
 Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs | 40 +++++++++++++++++++++++++++++-----------
 1 file changed, 29 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs b/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
index ef8e668da..36dd8ef35 100644
--- a/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
+++ b/Assets/_Scripts/Utility/Trailer/TrailerClipRecorder.cs
@@ -13,6 +13,10 @@ namespace CosmicShore.Utility.Trailer
     /// Records a single clip from ONE randomly-selected trailer camera,
     /// encoding directly to an MP4 file via Unity's MediaEncoder.
     ///
+    /// Uses Time.captureFramerate for deterministic frame timing — the game
+    /// clock advances by exactly 1/targetFPS per frame regardless of wall-clock
+    /// time, guaranteeing the output video plays back at real-time speed.
+    ///
     /// Uses AsyncGPUReadback so recording never stalls the GPU or gameplay.
     /// Readback callbacks fire in request order on the main thread, so
     /// frames arrive at the encoder sequentially.
@@ -25,13 +29,12 @@ namespace CosmicShore.Utility.Trailer
         private bool _isRecording;
         private string _clipFilePath;
         private int _frameIndex;
-        private float _captureInterval;
-        private float _captureTimer;
         private float _elapsedRecordTime;
         private int _clipNumber;
         private int _activeCameraIndex;
         private int _pendingReadbacks;
         private bool _finishing;
+        private int _previousCaptureFramerate;
 
 #if UNITY_EDITOR
         private MediaEncoder _encoder;
@@ -75,12 +78,16 @@ namespace CosmicShore.Utility.Trailer
             _isRecording = true;
             _frameIndex = 0;
             _elapsedRecordTime = 0f;
-            _captureInterval = 1f / config.targetFPS;
-            _captureTimer = 0f;
             _pendingReadbacks = 0;
             _finishing = false;
             _clipNumber++;
 
+            // Lock the game clock so every frame = exactly 1/targetFPS.
+            // This makes the recorded video play back at real-time speed
+            // regardless of actual GPU/CPU performance.
+            _previousCaptureFramerate = Time.captureFramerate;
+            Time.captureFramerate = config.targetFPS;
+
             int w = config.captureWidth;
             int h = config.captureHeight;
 
@@ -110,6 +117,9 @@ namespace CosmicShore.Utility.Trailer
             _isRecording = false;
             _finishing = true;
 
+            // Restore previous frame rate mode
+            Time.captureFramerate = _previousCaptureFramerate;
+
             // If no readbacks are in flight, finalize immediately
             if (_pendingReadbacks == 0)
                 FinalizeEncoder();
@@ -139,12 +149,9 @@ namespace CosmicShore.Utility.Trailer
                 return;
             }
 
-            _captureTimer += Time.deltaTime;
-            if (_captureTimer >= _captureInterval)
-            {
-                _captureTimer -= _captureInterval;
-                CaptureFrame();
-            }
+            // With Time.captureFramerate set, every frame is exactly 1/targetFPS.
+            // Capture every frame — no timer needed.
+            CaptureFrame();
         }
 
         private void CaptureFrame()
@@ -169,9 +176,20 @@ namespace CosmicShore.Utility.Trailer
                 if (!request.hasError)
                 {
                     NativeArray<byte> data = request.GetData<byte>();
+
+                    // GPU readback from RenderTextures is vertically flipped
+                    // (bottom-to-top). Flip rows so the encoded video is right-side up.
+                    int rowBytes = w * 4; // RGBA32 = 4 bytes per pixel
+                    var flipped = new NativeArray<byte>(data.Length, Allocator.Temp);
+                    for (int y = 0; y < h; y++)
+                    {
+                        NativeArray<byte>.Copy(data, y * rowBytes, flipped, (h - 1 - y) * rowBytes, rowBytes);
+                    }
+
                     var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
-                    tex.LoadRawTextureData(data);
+                    tex.LoadRawTextureData(flipped);
                     tex.Apply();
+                    flipped.Dispose();
 
 #if UNITY_EDITOR
                     _encoder?.AddFrame(tex);
```

</details>
