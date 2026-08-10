#if UNITY_EDITOR
using System.IO;
using System.Linq;
using CosmicShore.Soap;
using CosmicShore.Utility.Trailer;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor window at <b>Tools > Cosmic Shore > Trailer Camera Tool</b>.
    ///
    /// When enabled, auto-injects a TrailerCameraController into the scene on
    /// play mode entry. The controller auto-discovers the vessel and begins
    /// recording random clips. No manual setup required.
    /// </summary>
    public class TrailerCameraToolWindow : EditorWindow
    {
        private TrailerCameraConfigSO _config;
        private TrailerCameraController _runtimeController;
        private Vector2 _scrollPos;
        private bool _showCameraFoldout = true;
        private bool _showQualityFoldout;

        [MenuItem("Tools/Cosmic Shore/Trailer Camera Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<TrailerCameraToolWindow>("Trailer Camera");
            window.minSize = new Vector2(360, 400);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    InjectControllerIfNeeded();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    _runtimeController = null;
                    break;
            }
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        /// <summary>
        /// Auto-inject the TrailerCameraController on play mode entry.
        /// The controller uses DontDestroyOnLoad and listens for scene
        /// changes itself — it only activates in game scenes (Minigame*).
        /// </summary>
        private void InjectControllerIfNeeded()
        {
            if (_config == null || !_config.toolEnabled) return;

            // Controller persists via DontDestroyOnLoad, don't duplicate
            _runtimeController = FindAnyObjectByType<TrailerCameraController>();
            if (_runtimeController != null) return;

            var go = new GameObject("TrailerCameraController");
            _runtimeController = go.AddComponent<TrailerCameraController>();

            var so = new SerializedObject(_runtimeController);

            var configProp = so.FindProperty("config");
            if (configProp != null)
                configProp.objectReferenceValue = _config;

            var gameDataProp = so.FindProperty("gameData");
            if (gameDataProp != null)
            {
                var guids = AssetDatabase.FindAssets("t:GameDataSO");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    gameDataProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameDataSO>(path);
                }
            }

            so.ApplyModifiedProperties();

            Debug.Log("[TrailerTool] Controller injected (persists across scenes, active only in game modes).");
        }

        // ────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Trailer Camera Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawConfigSection();

            if (_config != null)
            {
                EditorGUILayout.Space(8);
                DrawToolToggle();
                EditorGUILayout.Space(8);
                DrawClipSettings();
                EditorGUILayout.Space(8);
                DrawCameraSection();
                EditorGUILayout.Space(8);
                DrawQualitySection();
                EditorGUILayout.Space(8);
                DrawOutputSection();
            }

            EditorGUILayout.Space(12);

            if (Application.isPlaying)
                DrawRuntimeControls();
            else
                EditorGUILayout.HelpBox(
                    "Enter Play Mode in Hex Race, Crystal Capture, or Joust.\n" +
                    "The tool auto-creates cameras and records clips.",
                    MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        // ── Config ──────────────────────────────────────────────────────

        private void DrawConfigSection()
        {
            EditorGUI.BeginChangeCheck();
            _config = (TrailerCameraConfigSO)EditorGUILayout.ObjectField(
                "Config Asset", _config, typeof(TrailerCameraConfigSO), false);
            if (EditorGUI.EndChangeCheck() && _config != null)
                EditorUtility.SetDirty(_config);

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Assign or create a config asset.", MessageType.Warning);
                if (GUILayout.Button("Create Default Config"))
                    CreateDefaultConfig();
            }
        }

        // ── Tool toggle ─────────────────────────────────────────────────

        private void DrawToolToggle()
        {
            EditorGUI.BeginChangeCheck();
            _config.toolEnabled = EditorGUILayout.Toggle("Tool Enabled", _config.toolEnabled);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);

            if (!_config.toolEnabled)
                EditorGUILayout.HelpBox("Tool is disabled. Nothing will run.", MessageType.Info);
        }

        // ── Clip settings ───────────────────────────────────────────────

        private void DrawClipSettings()
        {
            EditorGUILayout.LabelField("Clip Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _config.clipDurationSeconds = EditorGUILayout.Slider("Clip Duration (s)", _config.clipDurationSeconds, 1f, 30f);
            _config.numberOfRandomClips = EditorGUILayout.IntSlider("Random Clips Per Match", _config.numberOfRandomClips, 0, 20);
            _config.minimumTimeBetweenClips = EditorGUILayout.Slider("Min Time Between Clips (s)", _config.minimumTimeBetweenClips, 5f, 60f);
            _config.initialDelay = EditorGUILayout.Slider("Initial Delay (s)", _config.initialDelay, 3f, 30f);
            _config.delayBeforeCustomClip = EditorGUILayout.Slider("Custom Clip Delay (s)", _config.delayBeforeCustomClip, 0f, 10f);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);
        }

        // ── Cameras ─────────────────────────────────────────────────────

        private void DrawCameraSection()
        {
            _showCameraFoldout = EditorGUILayout.Foldout(_showCameraFoldout, "Cameras", true, EditorStyles.foldoutHeader);
            if (!_showCameraFoldout) return;

            EditorGUI.indentLevel++;
            int enabled = _config.cameraSetups.Count(c => c.enabled);
            EditorGUILayout.LabelField($"{enabled} / {_config.cameraSetups.Count} enabled");

            EditorGUI.BeginChangeCheck();
            foreach (var s in _config.cameraSetups)
            {
                EditorGUILayout.BeginHorizontal();
                s.enabled = EditorGUILayout.Toggle(s.enabled, GUILayout.Width(20));
                EditorGUILayout.LabelField($"{s.label} ({s.cameraType})", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);
            _config.hideUILayer = EditorGUILayout.Toggle("Hide UI From Cameras", _config.hideUILayer);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);
            EditorGUI.indentLevel--;
        }

        // ── Quality ─────────────────────────────────────────────────────

        private void DrawQualitySection()
        {
            _showQualityFoldout = EditorGUILayout.Foldout(_showQualityFoldout, "Capture Quality", true, EditorStyles.foldoutHeader);
            if (!_showQualityFoldout) return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            _config.captureWidth = EditorGUILayout.IntField("Width", _config.captureWidth);
            _config.captureHeight = EditorGUILayout.IntField("Height", _config.captureHeight);
            _config.targetFPS = EditorGUILayout.IntSlider("FPS", _config.targetFPS, 24, 120);
            _config.antiAliasing = EditorGUILayout.IntPopup("Anti-Aliasing",
                _config.antiAliasing, new[] { "1x", "2x", "4x", "8x" }, new[] { 1, 2, 4, 8 });

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1080p", EditorStyles.miniButton)) { _config.captureWidth = 1920; _config.captureHeight = 1080; }
            if (GUILayout.Button("1440p", EditorStyles.miniButton)) { _config.captureWidth = 2560; _config.captureHeight = 1440; }
            if (GUILayout.Button("4K", EditorStyles.miniButton)) { _config.captureWidth = 3840; _config.captureHeight = 2160; }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);
            EditorGUI.indentLevel--;
        }

        // ── Output ──────────────────────────────────────────────────────

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _config.outputFolder = EditorGUILayout.TextField("Folder", _config.outputFolder);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);

            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", _config.outputFolder));
            EditorGUILayout.LabelField(fullPath, EditorStyles.wordWrappedMiniLabel);

            if (GUILayout.Button("Open Output Folder"))
            {
                if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
                EditorUtility.RevealInFinder(fullPath);
            }
        }

        // ── Runtime (play mode only) ────────────────────────────────────

        private void DrawRuntimeControls()
        {
            // Find the controller (may have been injected automatically)
            if (_runtimeController == null)
                _runtimeController = FindAnyObjectByType<TrailerCameraController>();

            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            if (_runtimeController == null)
            {
                EditorGUILayout.HelpBox("Controller not found. Is the tool enabled?", MessageType.Warning);
                return;
            }

            // ── Status box ──
            EditorGUILayout.BeginVertical("box");

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool isGameScene = sceneName.StartsWith("Minigame", System.StringComparison.OrdinalIgnoreCase);
            string status = _runtimeController.IsActive
                ? "Active — tracking vessel"
                : isGameScene ? "In game scene — waiting for vessel..." : $"Non-game scene ({sceneName}) — idle";
            EditorGUILayout.LabelField("Status", status);

            if (_runtimeController.Rig != null)
                EditorGUILayout.LabelField("Cameras", $"{_runtimeController.Rig.Cameras.Count}");

            EditorGUILayout.LabelField("Clips Recorded", _runtimeController.ClipsRecorded.ToString());

            if (_runtimeController.Recorder != null && _runtimeController.Recorder.IsRecording)
            {
                float p = _runtimeController.Recorder.RecordingProgress;
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 18),
                    p, $"Recording clip... {p * 100f:F0}%");
            }

            EditorGUILayout.EndVertical();

            // ── Record Next Clip button ──
            EditorGUILayout.Space(4);
            bool canRecord = _runtimeController.IsActive &&
                             (_runtimeController.Recorder == null || !_runtimeController.Recorder.IsRecording);

            GUI.enabled = canRecord;
            float dur = _config != null ? _config.clipDurationSeconds : 5f;
            float delay = _config != null ? _config.delayBeforeCustomClip : 3f;
            string label = delay > 0
                ? $"Record Next {dur:F0}s (starts in {delay:F0}s)"
                : $"Record Next {dur:F0}s";

            if (GUILayout.Button(label, GUILayout.Height(28)))
                _runtimeController.RecordNextClipWithDelay();
            GUI.enabled = true;

            // ── Camera previews ──
            if (_runtimeController.Rig != null && _runtimeController.Rig.Cameras.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Camera Previews", EditorStyles.miniLabel);

                foreach (var cam in _runtimeController.Rig.Cameras)
                {
                    if (cam.RenderTexture == null) continue;
                    EditorGUILayout.LabelField(cam.Setup.label, EditorStyles.miniBoldLabel);
                    Rect r = EditorGUILayout.GetControlRect(false, 100);
                    EditorGUI.DrawPreviewTexture(r, cam.RenderTexture, null, ScaleMode.ScaleToFit);
                }
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private void CreateDefaultConfig()
        {
            string folder = "Assets/_SO_Assets/Trailer";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/_SO_Assets"))
                    AssetDatabase.CreateFolder("Assets", "_SO_Assets");
                AssetDatabase.CreateFolder("Assets/_SO_Assets", "Trailer");
            }

            var asset = CreateInstance<TrailerCameraConfigSO>();
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/TrailerCameraConfig.asset");
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _config = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
#endif
