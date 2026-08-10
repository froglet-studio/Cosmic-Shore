using System;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor.Media;
#endif

namespace CosmicShore.Utility.Trailer
{
    /// <summary>
    /// Records a single clip from ONE randomly-selected trailer camera,
    /// encoding directly to an MP4 file via Unity's MediaEncoder.
    ///
    /// Uses Time.captureFramerate for deterministic frame timing — the game
    /// clock advances by exactly 1/targetFPS per frame regardless of wall-clock
    /// time, guaranteeing the output video plays back at real-time speed.
    ///
    /// Uses AsyncGPUReadback so recording never stalls the GPU or gameplay.
    /// Readback callbacks fire in request order on the main thread, so
    /// frames arrive at the encoder sequentially.
    /// </summary>
    public class TrailerClipRecorder : MonoBehaviour
    {
        [SerializeField] private TrailerCameraConfigSO config;
        [SerializeField] private TrailerCameraRig cameraRig;

        private bool _isRecording;
        private string _clipFilePath;
        private int _frameIndex;
        private float _elapsedRecordTime;
        private int _clipNumber;
        private int _activeCameraIndex;
        private int _pendingReadbacks;
        private bool _finishing;
        private int _previousCaptureFramerate;

#if UNITY_EDITOR
        private MediaEncoder _encoder;
#endif

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

            _activeCameraIndex = UnityEngine.Random.Range(0, cameraRig.Cameras.Count);
            var chosen = cameraRig.Cameras[_activeCameraIndex];

            _isRecording = true;
            _frameIndex = 0;
            _elapsedRecordTime = 0f;
            _pendingReadbacks = 0;
            _finishing = false;
            _clipNumber++;

            // Lock the game clock so every frame = exactly 1/targetFPS.
            // This makes the recorded video play back at real-time speed
            // regardless of actual GPU/CPU performance.
            _previousCaptureFramerate = Time.captureFramerate;
            Time.captureFramerate = config.targetFPS;

            int w = config.captureWidth;
            int h = config.captureHeight;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string rootPath = Path.Combine(Application.dataPath, "..", config.outputFolder);
            Directory.CreateDirectory(rootPath);
            _clipFilePath = Path.Combine(rootPath, $"Clip_{_clipNumber:D2}_{chosen.Setup.label}_{timestamp}.mp4");

#if UNITY_EDITOR
            var videoAttr = new VideoTrackAttributes
            {
                frameRate = new MediaRational(config.targetFPS),
                width = (uint)w,
                height = (uint)h,
                includeAlpha = false
            };
            _encoder = new MediaEncoder(_clipFilePath, videoAttr);
#endif

            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} recording → " +
                        $"camera: {chosen.Setup.label}, {config.clipDurationSeconds}s @ {config.targetFPS}fps → {_clipFilePath}");
        }

        private void FinishClip()
        {
            if (!_isRecording) return;
            _isRecording = false;
            _finishing = true;

            // Restore previous frame rate mode
            Time.captureFramerate = _previousCaptureFramerate;

            // If no readbacks are in flight, finalize immediately
            if (_pendingReadbacks == 0)
                FinalizeEncoder();
        }

        private void FinalizeEncoder()
        {
#if UNITY_EDITOR
            _encoder?.Dispose();
            _encoder = null;
#endif
            _finishing = false;

            CSDebug.Log($"[TrailerClipRecorder] Clip {_clipNumber} saved — {_frameIndex} frames → {_clipFilePath}");
            OnClipFinished?.Invoke(_clipFilePath);
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

            // With Time.captureFramerate set, every frame is exactly 1/targetFPS.
            // Capture every frame — no timer needed.
            CaptureFrame();
        }

        private void CaptureFrame()
        {
            if (_activeCameraIndex < 0 || _activeCameraIndex >= cameraRig.Cameras.Count) return;

            var instance = cameraRig.Cameras[_activeCameraIndex];

            // Render just this one camera
            instance.Camera.Render();

            int w = config.captureWidth;
            int h = config.captureHeight;
            _pendingReadbacks++;

            // Async readback — no GPU stall, no gameplay lag.
            // Callbacks fire in request order on the main thread.
            AsyncGPUReadback.Request(instance.RenderTexture, 0, TextureFormat.RGBA32, request =>
            {
                _pendingReadbacks--;

                if (!request.hasError)
                {
                    NativeArray<byte> data = request.GetData<byte>();

                    // GPU readback from RenderTextures is vertically flipped
                    // (bottom-to-top). Flip rows so the encoded video is right-side up.
                    int rowBytes = w * 4; // RGBA32 = 4 bytes per pixel
                    var flipped = new NativeArray<byte>(data.Length, Allocator.Temp);
                    for (int y = 0; y < h; y++)
                    {
                        NativeArray<byte>.Copy(data, y * rowBytes, flipped, (h - 1 - y) * rowBytes, rowBytes);
                    }

                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(flipped);
                    tex.Apply();
                    flipped.Dispose();

#if UNITY_EDITOR
                    _encoder?.AddFrame(tex);
#endif
                    Destroy(tex);
                }

                // If clip finished and this was the last pending readback, finalize
                if (_finishing && _pendingReadbacks == 0)
                    FinalizeEncoder();
            });

            _frameIndex++;
        }
    }
}
