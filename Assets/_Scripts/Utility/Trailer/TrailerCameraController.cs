using System.Collections;
using CosmicShore.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.Trailer
{
    /// <summary>
    /// Top-level runtime controller for the trailer camera system.
    ///
    /// Persists across scene loads via DontDestroyOnLoad. Only activates
    /// in game mode scenes (scene names starting with "Minigame").
    /// Automatically tears down when returning to the main menu or any
    /// non-game scene, and re-initializes when entering a new game scene.
    /// </summary>
    public class TrailerCameraController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private TrailerCameraConfigSO config;
        [SerializeField] private GameDataSO gameData;

        private TrailerCameraRig _rig;
        private TrailerClipRecorder _recorder;
        private Coroutine _randomCaptureRoutine;
        private Coroutine _customClipRoutine;
        private bool _initialized;
        private int _clipsRecorded;
        private bool _inGameScene;

        public TrailerCameraConfigSO Config => config;
        public TrailerCameraRig Rig => _rig;
        public TrailerClipRecorder Recorder => _recorder;
        public bool IsActive => _initialized;
        public int ClipsRecorded => _clipsRecorded;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Cleanup();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Tear down any existing rig from the previous scene
            Cleanup();

            _inGameScene = IsGameScene(scene.name);

            if (_inGameScene)
                CSDebug.Log($"[TrailerCamera] Entered game scene: {scene.name}");
        }

        /// <summary>
        /// Game mode scenes start with "Minigame". Everything else
        /// (Menu_Main, Authentication, SplashScreen, tools) is ignored.
        /// </summary>
        private static bool IsGameScene(string sceneName)
        {
            return sceneName.StartsWith("Minigame", System.StringComparison.OrdinalIgnoreCase);
        }

        private void Update()
        {
            // Only poll for vessel in game scenes
            if (_initialized || !_inGameScene) return;
            if (config == null || !config.toolEnabled || gameData == null) return;

            var vessel = gameData.LocalPlayer?.Vessel;
            if (vessel?.Transform == null) return;

            InitializeRig(vessel.Transform);

            if (config.numberOfRandomClips > 0)
                _randomCaptureRoutine = StartCoroutine(RandomCaptureScheduler());
        }

        /// <summary>
        /// Initialize the rig with a vessel transform.
        /// </summary>
        public void InitializeRig(Transform vesselTransform)
        {
            if (_initialized) Cleanup();

            var rigGO = new GameObject("TrailerCameraRig");
            rigGO.transform.SetParent(transform);
            _rig = rigGO.AddComponent<TrailerCameraRig>();
            _rig.Initialize(vesselTransform, config);

            var recGO = new GameObject("TrailerClipRecorder");
            recGO.transform.SetParent(transform);
            _recorder = recGO.AddComponent<TrailerClipRecorder>();
            _recorder.Setup(config, _rig);
            _recorder.OnClipFinished += OnClipFinished;

            _initialized = true;
            _clipsRecorded = 0;

            CSDebug.Log($"[TrailerCamera] Ready — {_rig.Cameras.Count} cameras tracking {vesselTransform.name}");
        }

        /// <summary>
        /// Record next clip with the configured delay (for the editor button).
        /// </summary>
        public void RecordNextClipWithDelay()
        {
            if (_recorder == null || !_initialized)
            {
                CSDebug.LogWarning("[TrailerCamera] Not initialized.");
                return;
            }
            if (_recorder.IsRecording)
            {
                CSDebug.LogWarning("[TrailerCamera] Already recording.");
                return;
            }

            if (_customClipRoutine != null)
                StopCoroutine(_customClipRoutine);

            _customClipRoutine = StartCoroutine(DelayedClip(config.delayBeforeCustomClip));
        }

        private IEnumerator DelayedClip(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (_recorder != null && !_recorder.IsRecording)
                _recorder.StartClip();

            _customClipRoutine = null;
        }

        private void OnClipFinished(string outputPath)
        {
            _clipsRecorded++;
            CSDebug.Log($"[TrailerCamera] Clip {_clipsRecorded} saved → {outputPath}");
        }

        private IEnumerator RandomCaptureScheduler()
        {
            yield return new WaitForSeconds(config.initialDelay);

            int captured = 0;
            while (captured < config.numberOfRandomClips)
            {
                // Wait if a clip is in progress
                while (_recorder != null && _recorder.IsRecording)
                    yield return null;

                // Random interval
                float wait = Random.Range(config.minimumTimeBetweenClips, config.minimumTimeBetweenClips * 2f);
                yield return new WaitForSeconds(wait);

                if (_recorder == null || !_initialized)
                    yield break;

                if (!_recorder.IsRecording)
                {
                    _recorder.StartClip();
                    captured++;
                }

                // Wait for this clip to finish
                while (_recorder != null && _recorder.IsRecording)
                    yield return null;
            }

            CSDebug.Log($"[TrailerCamera] All {captured} random clips captured.");
        }

        private void Cleanup()
        {
            if (_randomCaptureRoutine != null)
            {
                StopCoroutine(_randomCaptureRoutine);
                _randomCaptureRoutine = null;
            }
            if (_customClipRoutine != null)
            {
                StopCoroutine(_customClipRoutine);
                _customClipRoutine = null;
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
        }
    }
}
