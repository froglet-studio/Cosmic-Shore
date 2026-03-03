#if UNITY_EDITOR
using System.IO;
using System.Linq;
using CosmicShore.Game;
using CosmicShore.Utility.Trailer;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor window at <b>Tools > Cosmic Shore > Trailer Camera Tool</b>.
    ///
    /// When the tool is enabled and a supported game mode starts (Hex Race,
    /// Crystal Capture, Joust), the system auto-creates a multi-camera rig
    /// around the vessel and captures random 5-second clips throughout the match.
    ///
    /// The window lets you configure:
    ///   - Enable/disable the tool
    ///   - Number of random clips per match
    ///   - Camera setups and UI visibility
    ///   - Resolution and quality presets
    ///   - A one-shot "Record Next 5s" button for custom captures
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
            window.minSize = new Vector2(360, 420);
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
            if (change == PlayModeStateChange.ExitingPlayMode)
                _runtimeController = null;
            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying) Repaint();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("Trailer Camera Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // ── Config asset ──
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
                    "Enter Play Mode in Hex Race, Crystal Capture, or Joust to see runtime controls.",
                    MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        // ────────────────────────────────────────────────────────────────
        //  Config
        // ────────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────────
        //  Tool toggle
        // ────────────────────────────────────────────────────────────────

        private void DrawToolToggle()
        {
            EditorGUI.BeginChangeCheck();
            _config.toolEnabled = EditorGUILayout.Toggle("Tool Enabled", _config.toolEnabled);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);

            if (!_config.toolEnabled)
                EditorGUILayout.HelpBox("Tool is disabled. Nothing will run in play mode.", MessageType.Info);
        }

        // ────────────────────────────────────────────────────────────────
        //  Clip settings
        // ────────────────────────────────────────────────────────────────

        private void DrawClipSettings()
        {
            EditorGUILayout.LabelField("Clip Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _config.clipDurationSeconds = EditorGUILayout.Slider("Clip Duration (s)", _config.clipDurationSeconds, 1f, 30f);
            _config.numberOfRandomClips = EditorGUILayout.IntSlider("Random Clips Per Match", _config.numberOfRandomClips, 0, 20);
            _config.minimumTimeBetweenClips = EditorGUILayout.Slider("Min Time Between Clips (s)", _config.minimumTimeBetweenClips, 5f, 60f);
            _config.initialDelay = EditorGUILayout.Slider("Initial Delay (s)", _config.initialDelay, 3f, 30f);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_config);
        }

        // ────────────────────────────────────────────────────────────────
        //  Camera setups
        // ────────────────────────────────────────────────────────────────

        private void DrawCameraSection()
        {
            _showCameraFoldout = EditorGUILayout.Foldout(_showCameraFoldout, "Cameras", true, EditorStyles.foldoutHeader);
            if (!_showCameraFoldout) return;

            EditorGUI.indentLevel++;
            int enabledCount = _config.cameraSetups.Count(c => c.enabled);
            EditorGUILayout.LabelField($"{enabledCount} / {_config.cameraSetups.Count} cameras enabled");

            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < _config.cameraSetups.Count; i++)
            {
                var s = _config.cameraSetups[i];
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

        // ────────────────────────────────────────────────────────────────
        //  Capture quality
        // ────────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────────
        //  Output
        // ────────────────────────────────────────────────────────────────

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

        // ────────────────────────────────────────────────────────────────
        //  Runtime controls (play mode only)
        // ────────────────────────────────────────────────────────────────

        private void DrawRuntimeControls()
        {
            FindRuntimeController();

            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);

            if (_runtimeController == null)
            {
                EditorGUILayout.HelpBox(
                    "No TrailerCameraController in scene. Create one to use at runtime.",
                    MessageType.Info);

                if (GUILayout.Button("Create Controller in Scene"))
                    CreateRuntimeController();
                return;
            }

            // Status box
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Status", _runtimeController.IsActive ? "Active" : "Waiting for game start");

            if (_runtimeController.Rig != null)
                EditorGUILayout.LabelField("Cameras", $"{_runtimeController.Rig.Cameras.Count}");

            EditorGUILayout.LabelField("Clips Recorded", _runtimeController.ClipsRecorded.ToString());

            // Progress bar during recording
            if (_runtimeController.Recorder != null && _runtimeController.Recorder.IsRecording)
            {
                float p = _runtimeController.Recorder.RecordingProgress;
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 18),
                    p, $"Recording clip... {p * 100f:F0}%");
            }

            EditorGUILayout.EndVertical();

            // ── Record Next 5s button ──
            EditorGUILayout.Space(4);

            bool canRecord = _runtimeController.IsActive &&
                             (_runtimeController.Recorder == null || !_runtimeController.Recorder.IsRecording);

            GUI.enabled = canRecord;
            string btnLabel = $"Record Next {(_config != null ? _config.clipDurationSeconds : 5f):F0}s";
            if (GUILayout.Button(btnLabel, GUILayout.Height(30)))
                _runtimeController.RecordNextClip();
            GUI.enabled = true;

            // Force init button when not yet active
            if (!_runtimeController.IsActive)
            {
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Force Initialize (find vessel)"))
                    ForceInitialize();
            }

            // Camera previews
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

        // ────────────────────────────────────────────────────────────────
        //  Helpers
        // ────────────────────────────────────────────────────────────────

        private void FindRuntimeController()
        {
            if (_runtimeController != null) return;
            _runtimeController = FindAnyObjectByType<TrailerCameraController>();
        }

        private void CreateRuntimeController()
        {
            var go = new GameObject("TrailerCameraController");
            _runtimeController = go.AddComponent<TrailerCameraController>();

            var so = new SerializedObject(_runtimeController);
            var configProp = so.FindProperty("config");
            var gameDataProp = so.FindProperty("gameData");

            if (configProp != null && _config != null)
                configProp.objectReferenceValue = _config;

            if (gameDataProp != null)
            {
                var guids = AssetDatabase.FindAssets("t:GameDataSO");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    gameDataProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                }
            }

            so.ApplyModifiedProperties();
            Selection.activeGameObject = go;
        }

        private void ForceInitialize()
        {
            if (_runtimeController == null) return;

            // Try GameDataSO first
            var guids = AssetDatabase.FindAssets("t:GameDataSO");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var gd = AssetDatabase.LoadAssetAtPath<CosmicShore.Soap.GameDataSO>(path);
                if (gd?.LocalPlayer?.Vessel != null)
                {
                    _runtimeController.InitializeRig(gd.LocalPlayer.Vessel.Transform);
                    return;
                }
            }

            // Fallback: any IVessel MonoBehaviour in scene
            var vessels = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(mb => mb is IVessel)
                .ToArray();

            if (vessels.Length > 0)
            {
                _runtimeController.InitializeRig(vessels[0].transform);
                Debug.Log($"[TrailerTool] Initialized with vessel: {vessels[0].name}");
            }
            else
            {
                Debug.LogWarning("[TrailerTool] No vessel found in scene.");
            }
        }

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
