using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Lightweight on-screen performance overlay (FPS / frame time / draw calls / GC /
    /// gameplay load). Independent of a benchmark run — drop it on any GameObject (e.g. a
    /// DontDestroyOnLoad bootstrap object) to eyeball regressions live.
    ///
    /// The per-frame path is allocation-free: it only pushes a float into a ring buffer.
    /// The display string is rebuilt at most a few times per second. IMGUI drawing only
    /// runs while the overlay is visible.
    /// </summary>
    public class BenchmarkHUDOverlay : MonoBehaviour
    {
        [Header("Toggle")]
        [Tooltip("Keyboard key that toggles the overlay on/off (Unity Input System).")]
        [SerializeField] private Key toggleKey = Key.F9;
        [SerializeField] private bool visibleOnStart;

        [Header("Display")]
        [Tooltip("How often the on-screen text is rebuilt, in seconds.")]
        [SerializeField, Range(0.1f, 2f)] private float refreshInterval = 0.25f;

        [Tooltip("Optional GameDataSO — when assigned, vessel/player counts are shown.")]
        [SerializeField] private GameDataSO gameData;

        [SerializeField] private bool showGameLoad = true;

        ProfilerRecorder _drawCalls;
        ProfilerRecorder _setPass;
        ProfilerRecorder _triangles;
        ProfilerRecorder _gcAlloc;

        bool _visible;
        float _nextRefresh;

        readonly StringBuilder _sb = new(256);
        string _cached = "";

        // Rolling ~window of recent frame times (ms) for a smoothed avg + spike readout.
        const int WindowSize = 120;
        readonly float[] _frameTimes = new float[WindowSize];
        int _frameWriteIndex;
        int _frameCount;

        GUIStyle _boxStyle;

        void OnEnable()
        {
            _visible = visibleOnStart;
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        }

        void OnDisable()
        {
            _drawCalls.Dispose();
            _setPass.Dispose();
            _triangles.Dispose();
            _gcAlloc.Dispose();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
                _visible = !_visible;

            // Allocation-free hot path: just record the frame time into the ring buffer so
            // the window is already warm whenever the overlay is toggled on.
            _frameTimes[_frameWriteIndex] = Time.unscaledDeltaTime * 1000f;
            _frameWriteIndex = (_frameWriteIndex + 1) % WindowSize;
            if (_frameCount < WindowSize) _frameCount++;

            if (!_visible) return;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + refreshInterval;
            RebuildText();
        }

        void RebuildText()
        {
            float sum = 0f, max = 0f;
            for (int i = 0; i < _frameCount; i++)
            {
                float t = _frameTimes[i];
                sum += t;
                if (t > max) max = t;
            }
            float avg = _frameCount > 0 ? sum / _frameCount : 0f;
            float fps = avg > 0.0001f ? 1000f / avg : 0f;

            _sb.Clear();
            _sb.Append("PERF   ").Append(fps.ToString("F0")).Append(" fps    ")
               .Append(avg.ToString("F1")).Append(" ms   (max ").Append(max.ToString("F1")).Append(" ms)\n");
            _sb.Append("Draw ").Append(RecorderValue(_drawCalls))
               .Append("   SetPass ").Append(RecorderValue(_setPass))
               .Append("   Tris ").Append((RecorderValue(_triangles) / 1000)).Append("k")
               .Append("   GC ").Append((RecorderValueLong(_gcAlloc) / 1024f).ToString("F1")).Append(" KB/f");

            if (showGameLoad)
            {
                var load = GameLoadSampler.Sample(gameData);
                _sb.Append('\n')
                   .Append("Prisms ").Append(load.activePrisms)
                   .Append("   Expl ").Append(load.activeExplosions)
                   .Append("   Impl ").Append(load.activeImplosions)
                   .Append("   Vessels ").Append(load.activeVessels)
                   .Append("   Players ").Append(load.activePlayers);
            }

            _cached = _sb.ToString();
        }

        void OnGUI()
        {
            if (!_visible) return;

            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 14,
                    richText = false,
                    padding = new RectOffset(8, 8, 6, 6)
                };
                _boxStyle.normal.textColor = Color.white;
            }

            float height = showGameLoad ? 78f : 56f;
            GUI.Box(new Rect(10f, 10f, 460f, height), _cached, _boxStyle);
        }

        static int RecorderValue(ProfilerRecorder recorder) =>
            recorder.Valid && recorder.Count > 0 ? (int)recorder.LastValue : 0;

        static long RecorderValueLong(ProfilerRecorder recorder) =>
            recorder.Valid && recorder.Count > 0 ? recorder.LastValue : 0;
    }
}
