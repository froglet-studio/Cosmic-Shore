using System.Collections;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Utility.Trailer
{
    /// <summary>
    /// Top-level runtime controller for the trailer camera system.
    ///
    /// When the tool is enabled and a game turn starts, it auto-creates a
    /// <see cref="TrailerCameraRig"/> around the local vessel and schedules
    /// random clip captures throughout the match. The number of random clips
    /// is controlled by <see cref="TrailerCameraConfigSO.numberOfRandomClips"/>.
    ///
    /// From the editor window users can also trigger a one-off
    /// "Record Next 5 Seconds" capture at any time.
    ///
    /// Supports: Hex Race, Crystal Capture, Joust — any scene using GameDataSO.
    /// </summary>
    public class TrailerCameraController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TrailerCameraConfigSO config;
        [SerializeField] private GameDataSO gameData;

        [Header("Runtime State")]
        [SerializeField] private bool isActive;

        private TrailerCameraRig _rig;
        private TrailerClipRecorder _recorder;
        private Coroutine _randomCaptureRoutine;
        private bool _initialized;
        private int _clipsRecorded;

        public TrailerCameraConfigSO Config => config;
        public TrailerCameraRig Rig => _rig;
        public TrailerClipRecorder Recorder => _recorder;
        public bool IsActive => isActive;
        public int ClipsRecorded => _clipsRecorded;

        private void OnEnable()
        {
            if (gameData != null)
                gameData.OnMiniGameTurnStarted.OnRaised += OnTurnStarted;
        }

        private void OnDisable()
        {
            if (gameData != null)
                gameData.OnMiniGameTurnStarted.OnRaised -= OnTurnStarted;

            Cleanup();
        }

        private void OnTurnStarted()
        {
            if (!config.toolEnabled || _initialized) return;

            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel == null)
            {
                CSDebug.LogWarning("[TrailerCameraController] No local vessel found at turn start.");
                return;
            }

            InitializeRig(vessel.Transform);

            // Start random clip scheduling
            if (config.numberOfRandomClips > 0)
                _randomCaptureRoutine = StartCoroutine(RandomCaptureScheduler());
        }

        /// <summary>
        /// Initialize rig manually (also used by ForceInitialize from editor tool).
        /// </summary>
        public void InitializeRig(Transform vesselTransform)
        {
            if (_initialized) Cleanup();

            // Camera rig
            var rigGO = new GameObject("TrailerCameraRig");
            rigGO.transform.SetParent(transform);
            _rig = rigGO.AddComponent<TrailerCameraRig>();
            _rig.Initialize(vesselTransform, config);

            // Clip recorder
            var recGO = new GameObject("TrailerClipRecorder");
            recGO.transform.SetParent(transform);
            _recorder = recGO.AddComponent<TrailerClipRecorder>();
            _recorder.Setup(config, _rig);
            _recorder.OnClipFinished += OnClipFinished;

            _initialized = true;
            isActive = true;
            _clipsRecorded = 0;

            CSDebug.Log($"[TrailerCameraController] Initialized — {_rig.Cameras.Count} cameras on {vesselTransform.name}");
        }

        /// <summary>
        /// One-shot: capture the next N seconds (uses config clipDurationSeconds).
        /// Wired to the "Record Next 5s" button in the editor window.
        /// </summary>
        public void RecordNextClip()
        {
            if (_recorder == null || !isActive)
            {
                CSDebug.LogWarning("[TrailerCameraController] Not initialized — cannot record.");
                return;
            }

            if (_recorder.IsRecording)
            {
                CSDebug.LogWarning("[TrailerCameraController] Already recording a clip.");
                return;
            }

            _recorder.StartClip();
        }

        private void OnClipFinished(string outputPath)
        {
            _clipsRecorded++;
            CSDebug.Log($"[TrailerCameraController] Clip {_clipsRecorded} saved → {outputPath}");
        }

        /// <summary>
        /// Coroutine that fires random clip captures at random intervals
        /// throughout the match, up to the configured limit.
        /// </summary>
        private IEnumerator RandomCaptureScheduler()
        {
            // Wait the initial delay before any random clips
            yield return new WaitForSeconds(config.initialDelay);

            int clipsCaptured = 0;

            while (clipsCaptured < config.numberOfRandomClips)
            {
                // Wait for any in-progress clip to finish
                while (_recorder != null && _recorder.IsRecording)
                    yield return null;

                // Random wait between minimumTimeBetweenClips and 2x that
                float wait = Random.Range(config.minimumTimeBetweenClips, config.minimumTimeBetweenClips * 2f);
                yield return new WaitForSeconds(wait);

                // Recorder may have been destroyed if game ended
                if (_recorder == null || !isActive)
                    yield break;

                _recorder.StartClip();
                clipsCaptured++;

                // Wait for this clip to finish before scheduling next
                while (_recorder != null && _recorder.IsRecording)
                    yield return null;
            }

            CSDebug.Log($"[TrailerCameraController] All {clipsCaptured} random clips captured.");
        }

        private void Cleanup()
        {
            if (_randomCaptureRoutine != null)
            {
                StopCoroutine(_randomCaptureRoutine);
                _randomCaptureRoutine = null;
            }

            if (_recorder != null)
            {
                _recorder.OnClipFinished -= OnClipFinished;
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
