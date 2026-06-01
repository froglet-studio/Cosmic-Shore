using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Minimal always-on FPS counter, top-left, toggled with F7.
    ///
    /// Auto-spawns in the Editor and Development builds only (compiled out of Release), so you can
    /// read the real frame rate in a standalone Development Build with zero setup. Near-zero
    /// overhead: a smoothed frame-time average plus one IMGUI label. Independent of the benchmark
    /// recorder and the Profiler — this is the honest in-build smoothness readout.
    /// </summary>
    public class FpsCounterHUD : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static FpsCounterHUD _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (_instance != null) return;
            var go = new GameObject("[FpsCounterHUD]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FpsCounterHUD>();
        }

        [SerializeField] Key toggleKey = Key.F7;
        [SerializeField] bool visibleOnStart = true;

        bool _visible;
        float _smoothedMs;          // exponential moving average of frame time
        float _displayFps, _displayMs;
        float _refreshTimer;

        GUIStyle _style;
        Texture2D _bg;

        void Awake() => _visible = visibleOnStart;

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_bg != null) Destroy(_bg);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame)
                _visible = !_visible;

            float ms = Time.unscaledDeltaTime * 1000f;
            _smoothedMs = _smoothedMs <= 0f ? ms : Mathf.Lerp(_smoothedMs, ms, 0.1f);

            // Refresh the shown numbers ~4x/sec so they're readable, not flickering.
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.25f)
            {
                _refreshTimer = 0f;
                _displayMs = _smoothedMs;
                _displayFps = _displayMs > 0.0001f ? 1000f / _displayMs : 0f;
            }
        }

        void OnGUI()
        {
            if (!_visible) return;

            if (_style == null)
            {
                _style = new GUIStyle
                {
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(8, 8, 4, 4)
                };
                _bg = new Texture2D(1, 1);
                _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
                _bg.Apply();
            }

            _style.normal.textColor =
                _displayFps >= 50f ? new Color(0.45f, 0.95f, 0.55f) :
                _displayFps >= 30f ? new Color(0.98f, 0.85f, 0.40f) :
                                     new Color(0.97f, 0.45f, 0.45f);

            var rect = new Rect(8, 8, 170, 28);
            GUI.DrawTexture(rect, _bg, ScaleMode.StretchToFill);
            GUI.Label(rect, $"{_displayFps:F1} FPS   {_displayMs:F1} ms", _style);
        }
#endif
    }
}
