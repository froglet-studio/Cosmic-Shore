using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Utility.Trailer
{
    /// <summary>
    /// Top-level runtime controller that wires together the camera rig and clip
    /// recorder. Drop this on a GameObject in any supported game mode scene,
    /// assign the config SO, and it auto-discovers the local vessel at game start.
    ///
    /// Supports: Hex Race, Crystal Capture, Joust (any scene that uses GameDataSO).
    /// </summary>
    public class TrailerCameraController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TrailerCameraConfigSO config;
        [SerializeField] private GameDataSO gameData;

        [Header("Runtime State (read-only)")]
        [SerializeField] private bool isActive;
        [SerializeField] private bool recordingEnabled = true;

        private TrailerCameraRig _rig;
        private TrailerClipRecorder _recorder;
        private bool _initialized;

        public TrailerCameraConfigSO Config => config;
        public TrailerCameraRig Rig => _rig;
        public TrailerClipRecorder Recorder => _recorder;
        public bool IsActive => isActive;

        public bool RecordingEnabled
        {
            get => recordingEnabled;
            set => recordingEnabled = value;
        }

        private void OnEnable()
        {
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
                gameData.OnMiniGameEnd += OnGameEnd;
            }
        }

        private void OnDisable()
        {
            if (gameData != null)
            {
                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;
                gameData.OnMiniGameEnd -= OnGameEnd;
            }

            Cleanup();
        }

        private void OnTurnStarted()
        {
            if (_initialized) return;

            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel == null)
            {
                CSDebug.LogWarning("[TrailerCameraController] No local vessel found at turn start.");
                return;
            }

            InitializeRig(vessel.Transform);
        }

        /// <summary>
        /// Manually initialize with a specific vessel transform.
        /// Useful when called from the editor tool.
        /// </summary>
        public void InitializeRig(Transform vesselTransform)
        {
            if (_initialized) Cleanup();

            // Create rig
            var rigGO = new GameObject("TrailerCameraRig");
            rigGO.transform.SetParent(transform);
            _rig = rigGO.AddComponent<TrailerCameraRig>();
            _rig.Initialize(vesselTransform, config);

            // Create recorder
            var recorderGO = new GameObject("TrailerClipRecorder");
            recorderGO.transform.SetParent(transform);
            _recorder = recorderGO.AddComponent<TrailerClipRecorder>();

            // Wire up recorder via reflection-free serialization workaround:
            // We set fields via a helper since they're SerializeField.
            SetupRecorder(_recorder);

            _initialized = true;
            isActive = true;

            CSDebug.Log($"[TrailerCameraController] Initialized with {_rig.Cameras.Count} cameras on vessel: {vesselTransform.name}");
        }

        private void SetupRecorder(TrailerClipRecorder recorder)
        {
            // Recorder needs config, rig, and gameData references.
            // Since these are [SerializeField] we use a setup method pattern.
            recorder.Setup(config, _rig, recordingEnabled ? gameData : null);
        }

        private void OnGameEnd()
        {
            if (!isActive || !recordingEnabled) return;

            CSDebug.Log("[TrailerCameraController] Game ended — recorder will handle capture if configured.");
        }

        /// <summary>
        /// Manually trigger recording from the editor tool.
        /// </summary>
        public void ManualStartRecording()
        {
            if (_recorder == null)
            {
                CSDebug.LogWarning("[TrailerCameraController] Recorder not initialized.");
                return;
            }

            _recorder.StartRecording();
        }

        /// <summary>
        /// Manually stop recording from the editor tool.
        /// </summary>
        public void ManualStopRecording()
        {
            _recorder?.StopRecording();
        }

        private void Cleanup()
        {
            if (_recorder != null)
            {
                _recorder.StopRecording();
                Destroy(_recorder.gameObject);
                _recorder = null;
            }

            if (_rig != null)
            {
                _rig.Teardown();
                Destroy(_rig.gameObject);
                _rig = null;
            }

            _initialized = false;
            isActive = false;
        }
    }
}
