using System;
using System.Collections;
using System.IO;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Utility.Trailer
{
    /// <summary>
    /// Captures high-quality PNG frame sequences from every trailer camera
    /// when triggered. Integrates with GameDataSO to auto-record at game end.
    /// Saves each camera's frames into a timestamped session folder.
    /// </summary>
    public class TrailerClipRecorder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TrailerCameraConfigSO config;
        [SerializeField] private TrailerCameraRig cameraRig;
        [SerializeField] private GameDataSO gameData;

        private bool _isRecording;
        private string _sessionFolder;
        private int _frameIndex;
        private float _captureInterval;
        private float _captureTimer;
        private float _elapsedRecordTime;
        private Canvas[] _cachedCanvases;
        private bool _uiWasHidden;
        private Coroutine _recordingCoroutine;

        public bool IsRecording => _isRecording;
        public float RecordingProgress => config != null && config.clipDurationSeconds > 0
            ? Mathf.Clamp01(_elapsedRecordTime / config.clipDurationSeconds)
            : 0f;

        /// <summary>
        /// Raised when recording finishes. Provides the output folder path.
        /// </summary>
        public event Action<string> OnRecordingFinished;

        /// <summary>
        /// Runtime setup for when the recorder is created via code
        /// rather than placed in a scene with serialized references.
        /// </summary>
        public void Setup(TrailerCameraConfigSO configSO, TrailerCameraRig rig, GameDataSO data)
        {
            // Unsubscribe from old gameData if any
            if (gameData != null)
                gameData.OnWinnerCalculated -= OnGameEnded;

            config = configSO;
            cameraRig = rig;
            gameData = data;

            // Resubscribe with new gameData
            if (config != null && config.recordOnGameEnd && gameData != null)
                gameData.OnWinnerCalculated += OnGameEnded;
        }

        private void OnEnable()
        {
            if (config != null && config.recordOnGameEnd && gameData != null)
                gameData.OnWinnerCalculated += OnGameEnded;
        }

        private void OnDisable()
        {
            if (gameData != null)
                gameData.OnWinnerCalculated -= OnGameEnded;

            StopRecording();
        }

        private void OnGameEnded()
        {
            if (!config.recordOnGameEnd || _isRecording) return;

            if (config.recordingStartDelay > 0f)
                _recordingCoroutine = StartCoroutine(DelayedStartRecording());
            else
                StartRecording();
        }

        private IEnumerator DelayedStartRecording()
        {
            yield return new WaitForSeconds(config.recordingStartDelay);
            StartRecording();
        }

        /// <summary>
        /// Begin recording clips from all active trailer cameras.
        /// Can be called manually from the editor tool or automatically.
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording) return;
            if (cameraRig == null || !cameraRig.IsInitialized)
            {
                CSDebug.LogWarning("[TrailerClipRecorder] Camera rig not initialized. Cannot record.");
                return;
            }

            _isRecording = true;
            _frameIndex = 0;
            _elapsedRecordTime = 0f;
            _captureInterval = 1f / config.targetFPS;
            _captureTimer = 0f;

            // Create session output folder
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string rootPath = Path.Combine(Application.dataPath, "..", config.outputFolder);
            _sessionFolder = Path.Combine(rootPath, $"Session_{timestamp}");

            // Create per-camera subfolders
            foreach (var cam in cameraRig.Cameras)
            {
                string camFolder = Path.Combine(_sessionFolder, cam.Setup.label);
                Directory.CreateDirectory(camFolder);
            }

            // Hide UI if configured
            if (config.hideUILayer)
                HideUI();

            CSDebug.Log($"[TrailerClipRecorder] Recording started — {cameraRig.Cameras.Count} cameras, " +
                        $"{config.clipDurationSeconds}s, {config.captureWidth}x{config.captureHeight} @ {config.targetFPS}fps");
            CSDebug.Log($"[TrailerClipRecorder] Output: {_sessionFolder}");
        }

        /// <summary>
        /// Stop recording and finalize output.
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording) return;

            _isRecording = false;

            if (_recordingCoroutine != null)
            {
                StopCoroutine(_recordingCoroutine);
                _recordingCoroutine = null;
            }

            // Restore UI
            if (_uiWasHidden)
                ShowUI();

            CSDebug.Log($"[TrailerClipRecorder] Recording stopped — {_frameIndex} frames captured per camera.");
            OnRecordingFinished?.Invoke(_sessionFolder);
        }

        private void LateUpdate()
        {
            if (!_isRecording) return;

            _elapsedRecordTime += Time.deltaTime;

            if (_elapsedRecordTime >= config.clipDurationSeconds)
            {
                StopRecording();
                return;
            }

            _captureTimer += Time.deltaTime;
            if (_captureTimer >= _captureInterval)
            {
                _captureTimer -= _captureInterval;
                CaptureFrame();
            }
        }

        private void CaptureFrame()
        {
            // Render all cameras and save their output
            for (int i = 0; i < cameraRig.Cameras.Count; i++)
            {
                var instance = cameraRig.Cameras[i];

                // Render camera to its RenderTexture
                instance.Camera.Render();

                // Read pixels from RenderTexture
                RenderTexture currentRT = RenderTexture.active;
                RenderTexture.active = instance.RenderTexture;

                var tex = new Texture2D(config.captureWidth, config.captureHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, config.captureWidth, config.captureHeight), 0, 0);
                tex.Apply();

                RenderTexture.active = currentRT;

                // Encode and save
                byte[] pngData = tex.EncodeToPNG();
                Destroy(tex);

                string filePath = Path.Combine(
                    _sessionFolder,
                    instance.Setup.label,
                    $"frame_{_frameIndex:D6}.png"
                );
                File.WriteAllBytes(filePath, pngData);
            }

            _frameIndex++;
        }

        private void HideUI()
        {
            _cachedCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in _cachedCanvases)
            {
                if (canvas != null && canvas.gameObject.activeInHierarchy)
                    canvas.gameObject.SetActive(false);
            }
            _uiWasHidden = true;
        }

        private void ShowUI()
        {
            if (_cachedCanvases == null) return;
            foreach (var canvas in _cachedCanvases)
            {
                if (canvas != null)
                    canvas.gameObject.SetActive(true);
            }
            _cachedCanvases = null;
            _uiWasHidden = false;
        }
    }
}
