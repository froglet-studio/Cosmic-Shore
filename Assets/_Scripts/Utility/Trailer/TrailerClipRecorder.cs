using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;

namespace CosmicShore.Utility.Trailer
{
    /// <summary>
    /// Captures high-quality PNG frame sequences from every trailer camera.
    /// Each capture records for <see cref="TrailerCameraConfigSO.clipDurationSeconds"/>
    /// seconds. File writes happen on a background thread to avoid stalling gameplay.
    ///
    /// UI is hidden from trailer cameras via culling mask on the cameras themselves
    /// (set up by TrailerCameraRig), so no Canvas objects are touched and game
    /// systems remain unaffected.
    /// </summary>
    public class TrailerClipRecorder : MonoBehaviour
    {
        [SerializeField] private TrailerCameraConfigSO config;
        [SerializeField] private TrailerCameraRig cameraRig;

        private bool _isRecording;
        private string _sessionFolder;
        private int _frameIndex;
        private float _captureInterval;
        private float _captureTimer;
        private float _elapsedRecordTime;
        private int _clipNumber;
        private int _pendingWrites;

        public bool IsRecording => _isRecording;
        public int ClipNumber => _clipNumber;
        public float RecordingProgress => config != null && config.clipDurationSeconds > 0
            ? Mathf.Clamp01(_elapsedRecordTime / config.clipDurationSeconds)
            : 0f;

        public event Action<string> OnClipFinished;

        public void Setup(TrailerCameraConfigSO configSO, TrailerCameraRig rig)
        {
            config = configSO;
            cameraRig = rig;
            _clipNumber = 0;
        }

        private void OnDisable()
        {
            if (_isRecording)
                FinishClip();
        }

        /// <summary>
        /// Begin capturing a single clip from all active trailer cameras.
        /// </summary>
        public void StartClip()
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
            _clipNumber++;

            // Create per-clip output folders
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string rootPath = Path.Combine(Application.dataPath, "..", config.outputFolder);
            _sessionFolder = Path.Combine(rootPath, $"Clip_{_clipNumber:D2}_{timestamp}");

            foreach (var cam in cameraRig.Cameras)
            {
                string camFolder = Path.Combine(_sessionFolder, cam.Setup.label);
                Directory.CreateDirectory(camFolder);
            }

            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} started — " +
                        $"{cameraRig.Cameras.Count} cameras, {config.clipDurationSeconds}s @ " +
                        $"{config.captureWidth}x{config.captureHeight}");
        }

        private void FinishClip()
        {
            if (!_isRecording) return;
            _isRecording = false;

            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} done — {_frameIndex} frames/camera → {_sessionFolder}");
            OnClipFinished?.Invoke(_sessionFolder);
        }

        private void LateUpdate()
        {
            if (!_isRecording) return;

            _elapsedRecordTime += Time.deltaTime;

            if (_elapsedRecordTime >= config.clipDurationSeconds)
            {
                FinishClip();
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
            int frameIdx = _frameIndex;
            _frameIndex++;

            for (int i = 0; i < cameraRig.Cameras.Count; i++)
            {
                var instance = cameraRig.Cameras[i];

                instance.Camera.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = instance.RenderTexture;

                var tex = new Texture2D(config.captureWidth, config.captureHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, config.captureWidth, config.captureHeight), 0, 0);
                tex.Apply();

                RenderTexture.active = prev;

                // Encode on main thread (required), but write on background thread
                byte[] pngData = tex.EncodeToPNG();
                Destroy(tex);

                string filePath = Path.Combine(
                    _sessionFolder,
                    instance.Setup.label,
                    $"frame_{frameIdx:D6}.png"
                );

                Interlocked.Increment(ref _pendingWrites);
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        File.WriteAllBytes(filePath, pngData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[TrailerClipRecorder] Failed to write {filePath}: {e.Message}");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingWrites);
                    }
                });
            }
        }
    }
}
