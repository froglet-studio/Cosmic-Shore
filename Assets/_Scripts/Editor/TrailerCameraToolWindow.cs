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
    /// Editor window for controlling the Trailer Camera system.
    /// Provides controls to:
    ///   - Assign/create a TrailerCameraConfigSO
    ///   - Toggle UI visibility during recording
    ///   - Toggle recording on/off
    ///   - Monitor camera count and recording progress
    ///   - Open the output folder
    ///
    /// Works at runtime in the editor — play the game in a supported mode
    /// (Hex Race, Crystal Capture, Joust), then use this window to control capture.
    /// </summary>
    public class TrailerCameraToolWindow : EditorWindow
    {
        private TrailerCameraConfigSO _config;
        private TrailerCameraController _runtimeController;
        private Vector2 _scrollPos;
        private bool _showCameraFoldout = true;
        private bool _showRecordingFoldout = true;
        private bool _showOutputFoldout = true;
        private string _lastOutputPath;

        [MenuItem("Tools/Cosmic Shore/Trailer Camera Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<TrailerCameraToolWindow>("Trailer Camera");
            window.minSize = new Vector2(380, 500);
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
            // Repaint periodically to update progress bars during recording
            if (Application.isPlaying)
                Repaint();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            EditorGUILayout.Space(8);
            DrawConfigSection();
            EditorGUILayout.Space(8);

            if (_config != null)
            {
                DrawCameraSection();
                EditorGUILayout.Space(8);
                DrawRecordingSection();
                EditorGUILayout.Space(8);
                DrawOutputSection();
                EditorGUILayout.Space(8);
            }

            if (Application.isPlaying)
            {
                DrawRuntimeControls();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode in a supported game scene (Hex Race, Crystal Capture, or Joust) " +
                    "to use runtime controls.",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Trailer Camera Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Capture cinematic footage from multiple angles during gameplay.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _config = (TrailerCameraConfigSO)EditorGUILayout.ObjectField(
                "Config Asset", _config, typeof(TrailerCameraConfigSO), false);
            if (EditorGUI.EndChangeCheck() && _config != null)
                EditorUtility.SetDirty(_config);

            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or create a TrailerCameraConfigSO to configure cameras and recording.",
                    MessageType.Warning);

                if (GUILayout.Button("Create Default Config Asset"))
                    CreateDefaultConfig();
            }
        }

        private void DrawCameraSection()
        {
            _showCameraFoldout = EditorGUILayout.Foldout(_showCameraFoldout, "Camera Setups", true, EditorStyles.foldoutHeader);
            if (!_showCameraFoldout) return;

            EditorGUI.indentLevel++;

            int enabledCount = _config.cameraSetups.Count(c => c.enabled);
            EditorGUILayout.LabelField($"{enabledCount} / {_config.cameraSetups.Count} cameras enabled");

            for (int i = 0; i < _config.cameraSetups.Count; i++)
            {
                var setup = _config.cameraSetups[i];
                EditorGUILayout.BeginHorizontal();

                setup.enabled = EditorGUILayout.Toggle(setup.enabled, GUILayout.Width(20));
                EditorGUILayout.LabelField($"{setup.label} ({setup.cameraType})", EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            _config.hideUILayer = EditorGUILayout.Toggle("Hide UI During Capture", _config.hideUILayer);
            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
                EditorUtility.SetDirty(_config);

            EditorGUI.indentLevel--;
        }

        private void DrawRecordingSection()
        {
            _showRecordingFoldout = EditorGUILayout.Foldout(_showRecordingFoldout, "Recording Settings", true, EditorStyles.foldoutHeader);
            if (!_showRecordingFoldout) return;

            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();

            _config.clipDurationSeconds = EditorGUILayout.Slider("Clip Duration (s)", _config.clipDurationSeconds, 1f, 30f);
            _config.captureWidth = EditorGUILayout.IntField("Width", _config.captureWidth);
            _config.captureHeight = EditorGUILayout.IntField("Height", _config.captureHeight);
            _config.targetFPS = EditorGUILayout.IntSlider("Target FPS", _config.targetFPS, 24, 120);
            _config.antiAliasing = EditorGUILayout.IntPopup("Anti-Aliasing",
                _config.antiAliasing, new[] { "1x", "2x", "4x", "8x" }, new[] { 1, 2, 4, 8 });

            EditorGUILayout.Space(4);
            _config.recordOnGameEnd = EditorGUILayout.Toggle("Record On Game End", _config.recordOnGameEnd);
            if (_config.recordOnGameEnd)
            {
                EditorGUI.indentLevel++;
                _config.recordingStartDelay = EditorGUILayout.Slider("Start Delay (s)", _config.recordingStartDelay, 0f, 5f);
                EditorGUI.indentLevel--;
            }

            // Resolution presets
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Quick Presets", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1080p", EditorStyles.miniButton)) { _config.captureWidth = 1920; _config.captureHeight = 1080; }
            if (GUILayout.Button("1440p", EditorStyles.miniButton)) { _config.captureWidth = 2560; _config.captureHeight = 1440; }
            if (GUILayout.Button("4K", EditorStyles.miniButton)) { _config.captureWidth = 3840; _config.captureHeight = 2160; }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_config);

            EditorGUI.indentLevel--;
        }

        private void DrawOutputSection()
        {
            _showOutputFoldout = EditorGUILayout.Foldout(_showOutputFoldout, "Output", true, EditorStyles.foldoutHeader);
            if (!_showOutputFoldout) return;

            EditorGUI.indentLevel++;

            EditorGUI.BeginChangeCheck();
            _config.outputFolder = EditorGUILayout.TextField("Output Folder", _config.outputFolder);
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(_config);

            string fullPath = Path.Combine(Application.dataPath, "..", _config.outputFolder);
            EditorGUILayout.LabelField("Full Path:", Path.GetFullPath(fullPath), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Output Folder"))
            {
                string absPath = Path.GetFullPath(fullPath);
                if (Directory.Exists(absPath))
                    EditorUtility.RevealInFinder(absPath);
                else
                {
                    Directory.CreateDirectory(absPath);
                    EditorUtility.RevealInFinder(absPath);
                }
            }

            if (!string.IsNullOrEmpty(_lastOutputPath) && GUILayout.Button("Open Last Session"))
            {
                if (Directory.Exists(_lastOutputPath))
                    EditorUtility.RevealInFinder(_lastOutputPath);
            }
            EditorGUILayout.EndHorizontal();

            // Show estimated file size
            int enabledCams = _config.cameraSetups.Count(c => c.enabled);
            int totalFrames = Mathf.CeilToInt(_config.clipDurationSeconds * _config.targetFPS);
            float estimatedMBPerFrame = (_config.captureWidth * _config.captureHeight * 3f) / (1024f * 1024f) * 0.5f; // rough PNG compression
            float totalEstMB = estimatedMBPerFrame * totalFrames * enabledCams;
            EditorGUILayout.LabelField($"Estimated: ~{totalFrames} frames/cam x {enabledCams} cams = ~{totalEstMB:F0} MB",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUI.indentLevel--;
        }

        private void DrawRuntimeControls()
        {
            EditorGUILayout.LabelField("Runtime Controls", EditorStyles.boldLabel);

            // Find or create runtime controller
            FindRuntimeController();

            if (_runtimeController == null)
            {
                EditorGUILayout.HelpBox(
                    "No TrailerCameraController found in scene. Click below to create one.",
                    MessageType.Info);

                if (GUILayout.Button("Create Trailer Camera Controller in Scene"))
                    CreateRuntimeController();

                return;
            }

            // Status
            EditorGUILayout.BeginVertical("box");
            string status = _runtimeController.IsActive ? "Active" : "Inactive";
            EditorGUILayout.LabelField("Status", status);

            if (_runtimeController.Rig != null)
                EditorGUILayout.LabelField("Cameras", $"{_runtimeController.Rig.Cameras.Count} active");
            else
                EditorGUILayout.LabelField("Cameras", "Not initialized");

            // Recording toggle
            EditorGUI.BeginChangeCheck();
            bool recEnabled = EditorGUILayout.Toggle("Recording Enabled", _runtimeController.RecordingEnabled);
            if (EditorGUI.EndChangeCheck())
                _runtimeController.RecordingEnabled = recEnabled;

            // Recording progress
            if (_runtimeController.Recorder != null && _runtimeController.Recorder.IsRecording)
            {
                float progress = _runtimeController.Recorder.RecordingProgress;
                EditorGUI.ProgressBar(
                    EditorGUILayout.GetControlRect(false, 20),
                    progress,
                    $"Recording... {progress * 100f:F0}%");
            }

            EditorGUILayout.EndVertical();

            // Manual controls
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = _runtimeController.IsActive &&
                          (_runtimeController.Recorder == null || !_runtimeController.Recorder.IsRecording);
            if (GUILayout.Button("Start Recording"))
            {
                _runtimeController.ManualStartRecording();
            }

            GUI.enabled = _runtimeController.Recorder != null && _runtimeController.Recorder.IsRecording;
            if (GUILayout.Button("Stop Recording"))
            {
                _runtimeController.ManualStopRecording();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            // Manual initialization (if vessel exists but rig isn't set up)
            if (!_runtimeController.IsActive)
            {
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Force Initialize (find vessel in scene)"))
                {
                    ForceInitialize();
                }
            }

            // Camera preview buttons
            if (_runtimeController.Rig != null && _runtimeController.Rig.Cameras.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Camera Previews", EditorStyles.miniLabel);

                foreach (var cam in _runtimeController.Rig.Cameras)
                {
                    if (cam.RenderTexture != null)
                    {
                        EditorGUILayout.LabelField(cam.Setup.label, EditorStyles.miniBoldLabel);
                        Rect previewRect = EditorGUILayout.GetControlRect(false, 120);
                        EditorGUI.DrawPreviewTexture(previewRect, cam.RenderTexture, null, ScaleMode.ScaleToFit);
                    }
                }
            }
        }

        private void FindRuntimeController()
        {
            if (_runtimeController != null) return;
            _runtimeController = FindAnyObjectByType<TrailerCameraController>();

            if (_runtimeController != null && _runtimeController.Recorder != null)
            {
                _runtimeController.Recorder.OnRecordingFinished -= OnRecordingFinished;
                _runtimeController.Recorder.OnRecordingFinished += OnRecordingFinished;
            }
        }

        private void OnRecordingFinished(string outputPath)
        {
            _lastOutputPath = outputPath;
            Debug.Log($"[TrailerTool] Recording saved to: {outputPath}");
        }

        private void CreateRuntimeController()
        {
            var go = new GameObject("TrailerCameraController");
            _runtimeController = go.AddComponent<TrailerCameraController>();

            // Wire the config via SerializedObject so it persists in the inspector
            var so = new SerializedObject(_runtimeController);
            var configProp = so.FindProperty("config");
            var gameDataProp = so.FindProperty("gameData");

            if (configProp != null && _config != null)
                configProp.objectReferenceValue = _config;

            // Try to find GameDataSO in the project
            if (gameDataProp != null)
            {
                var guids = AssetDatabase.FindAssets("t:GameDataSO");
                if (guids.Length > 0)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    var gameDataAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    gameDataProp.objectReferenceValue = gameDataAsset;
                }
            }

            so.ApplyModifiedProperties();
            Selection.activeGameObject = go;
        }

        private void ForceInitialize()
        {
            if (_runtimeController == null) return;

            // Try to find the local vessel via GameDataSO
            var gameDataGuids = AssetDatabase.FindAssets("t:GameDataSO");
            if (gameDataGuids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(gameDataGuids[0]);
                var gameData = AssetDatabase.LoadAssetAtPath<CosmicShore.Soap.GameDataSO>(path);
                if (gameData != null && gameData.LocalPlayer?.Vessel != null)
                {
                    _runtimeController.InitializeRig(gameData.LocalPlayer.Vessel.Transform);
                    return;
                }
            }

            // Fallback: find any vessel in scene
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
            Debug.Log($"[TrailerTool] Created config asset at: {assetPath}");
        }
    }
}
#endif
