using System;
using System.IO;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Utility.Trailer
{
    /// <summary>
    /// Records a single clip from ONE randomly-selected trailer camera.
    ///
    /// Performance approach:
    ///   - Only one camera renders per clip (not all 6)
    ///   - AsyncGPUReadback avoids ReadPixels GPU stall
    ///   - PNG encode happens on the main thread (callback), but with only
    ///     1 camera at 30fps the cost is negligible
    ///   - File writes go to the thread pool
    /// </summary>
    public class TrailerClipRecorder : MonoBehaviour
    {
        [SerializeField] private TrailerCameraConfigSO config;
        [SerializeField] private TrailerCameraRig cameraRig;

        private bool _isRecording;
        private string _clipFolder;
        private int _frameIndex;
        private float _captureInterval;
        private float _captureTimer;
        private float _elapsedRecordTime;
        private int _clipNumber;
        private int _activeCameraIndex;

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
            if (_isRecording) FinishClip();
        }

        /// <summary>
        /// Begin recording a clip from one randomly-selected camera.
        /// </summary>
        public void StartClip()
        {
            if (_isRecording) return;
            if (cameraRig == null || !cameraRig.IsInitialized || cameraRig.Cameras.Count == 0)
            {
                CSDebug.LogWarning("[TrailerClipRecorder] Camera rig not ready.");
                return;
            }

            // Pick a random camera for this clip
            _activeCameraIndex = UnityEngine.Random.Range(0, cameraRig.Cameras.Count);
            var chosen = cameraRig.Cameras[_activeCameraIndex];

            _isRecording = true;
            _frameIndex = 0;
            _elapsedRecordTime = 0f;
            _captureInterval = 1f / config.targetFPS;
            _captureTimer = 0f;
            _clipNumber++;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string rootPath = Path.Combine(Application.dataPath, "..", config.outputFolder);
            _clipFolder = Path.Combine(rootPath, $"Clip_{_clipNumber:D2}_{chosen.Setup.label}_{timestamp}");
            Directory.CreateDirectory(_clipFolder);

            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} recording — " +
                        $"camera: {chosen.Setup.label}, {config.clipDurationSeconds}s @ {config.targetFPS}fps");
        }

        private void FinishClip()
        {
            if (!_isRecording) return;
            _isRecording = false;

            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} saved — {_frameIndex} frames → {_clipFolder}");
            OnClipFinished?.Invoke(_clipFolder);
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
            if (_activeCameraIndex < 0 || _activeCameraIndex >= cameraRig.Cameras.Count) return;

            var instance = cameraRig.Cameras[_activeCameraIndex];

            // Render just this one camera
            instance.Camera.Render();

            int frameIdx = _frameIndex;
            string folder = _clipFolder;
            int w = config.captureWidth;
            int h = config.captureHeight;

            // Async readback — no GPU stall
            AsyncGPUReadback.Request(instance.RenderTexture, 0, TextureFormat.RGB24, request =>
            {
                if (request.hasError) return;

                // Callback runs on main thread — safe to use Texture2D
                NativeArray<byte> data = request.GetData<byte>();

                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.LoadRawTextureData(data);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                Destroy(tex);

                // File write on background thread
                string filePath = Path.Combine(folder, $"frame_{frameIdx:D6}.png");
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { File.WriteAllBytes(filePath, png); }
                    catch (Exception e) { Debug.LogError($"[TrailerClipRecorder] Write failed: {e.Message}"); }
                });
            });

            _frameIndex++;
        }
    }
}
