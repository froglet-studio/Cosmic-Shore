#if DEVELOPMENT_BUILD || UNITY_EDITOR
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using CosmicShore.Gameplay;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Runtime IMGUI overlay displaying AOE performance metrics.
    /// Toggle with F9 or FrogletTools menu. Reads ProfilerMarker timings
    /// via ProfilerRecorder - zero manual stopwatch code needed.
    ///
    /// The Physics/Burst buttons drive ExplosionImpactor.ForceLegacyPhysics for
    /// live A/B comparison: Physics mode forces every explosion through the
    /// OnTriggerEnter fallback, Burst mode uses the PrismSpatialIndex batch path.
    /// </summary>
    public class AOEBenchmarkOverlay : MonoBehaviour
    {
        private static AOEBenchmarkOverlay _instance;
        private static bool _enabled;

        // The menu toggle flips _enabled but nothing re-reads it at play start — a stale true
        // makes the first toggle of the next session read as "turn off" with no overlay shown.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _enabled = false;
            _instance = null;
        }

        private const int ROLLING_WINDOW = 60;

        // ProfilerRecorders - read back the markers we added to the hot paths
        private ProfilerRecorder _recOnTrigger;
        private ProfilerRecorder _recBurstJob;
        private ProfilerRecorder _recResolveDamage;
        private ProfilerRecorder _recProcessExplosion;
        private ProfilerRecorder _recExplodeFrame;
        private ProfilerRecorder _recProcessBatch;
        private ProfilerRecorder _recPrismExplosions;
        private ProfilerRecorder _recPrismImplosions;

        // Rolling averages
        private readonly double[] _avgOnTrigger = new double[ROLLING_WINDOW];
        private readonly double[] _avgBurstJob = new double[ROLLING_WINDOW];
        private readonly double[] _avgResolveDmg = new double[ROLLING_WINDOW];
        private readonly double[] _avgTotal = new double[ROLLING_WINDOW];
        private int _frameIndex;

        // GUI
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _btnStyle;
        private bool _stylesInit;
        private Rect _windowRect = new(10, 10, 340, 400);

        private enum AOEMode { Burst, PhysicsOnly }
        private AOEMode _currentMode = AOEMode.Burst;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (_enabled) EnsureInstance();
                else if (_instance) _instance.gameObject.SetActive(false);
            }
        }

        public static void ToggleOverlay() => Enabled = !Enabled;

        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                _instance.gameObject.SetActive(true);
                return;
            }

            var go = new GameObject("[AOEBenchmarkOverlay]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AOEBenchmarkOverlay>();
        }

        private void OnEnable()
        {
            _recOnTrigger = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AOE.OnTriggerEnter");
            _recBurstJob = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AOE.BurstJob.Schedule");
            _recResolveDamage = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AOE.ResolveDamage");
            _recProcessExplosion = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AOE.ProcessExplosion");
            _recExplodeFrame = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AOE.ExplodeAsync.Frame");
            _recProcessBatch = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AOE.ProcessBatchFrame");
            _recPrismExplosions = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Prism.ProcessExplosions");
            _recPrismImplosions = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Prism.ProcessImplosions");
        }

        private void OnDisable()
        {
            // Never leave the A/B toggle forcing the slow path after the overlay closes
            ExplosionImpactor.ForceLegacyPhysics = false;
            _recOnTrigger.Dispose();
            _recBurstJob.Dispose();
            _recResolveDamage.Dispose();
            _recProcessExplosion.Dispose();
            _recExplodeFrame.Dispose();
            _recProcessBatch.Dispose();
            _recPrismExplosions.Dispose();
            _recPrismImplosions.Dispose();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
                Enabled = !Enabled;

            if (!_enabled) return;

            // Store rolling samples (nanoseconds → milliseconds)
            int idx = _frameIndex % ROLLING_WINDOW;
            _avgOnTrigger[idx] = NsToMs(_recOnTrigger.LastValue);
            _avgBurstJob[idx] = NsToMs(_recBurstJob.LastValue);
            _avgResolveDmg[idx] = NsToMs(_recResolveDamage.LastValue);
            _avgTotal[idx] = NsToMs(_recProcessExplosion.LastValue + _recOnTrigger.LastValue);
            _frameIndex++;

            // Sync mode toggle
            ExplosionImpactor.ForceLegacyPhysics = _currentMode == AOEMode.PhysicsOnly;
        }

        private void OnGUI()
        {
            if (!_enabled) return;
            InitStyles();

            _windowRect = GUILayout.Window(9427, _windowRect, DrawWindow, "", _boxStyle);
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label("AOE Benchmark", _headerStyle);
            GUILayout.Space(4);

            // --- Mode ---
            string modeLabel = _currentMode switch
            {
                AOEMode.PhysicsOnly => "PHYSICS (baseline)",
                AOEMode.Burst => "BURST",
                _ => "?"
            };
            GUILayout.Label($"Mode: {modeLabel}", _labelStyle);

            // --- Spatial index status ---
            var index = PrismSpatialIndex.Instance;
            if (index != null && index.IsAvailable)
                GUILayout.Label($"Spatial index: Available ({index.HighWaterMark} slots)", _labelStyle);
            else
                GUILayout.Label("Spatial index: UNAVAILABLE", _labelStyle);

            GUILayout.Space(8);

            // --- Last Frame ---
            GUILayout.Label("Last Frame:", _headerStyle);
            DrawTimingRow("OnTriggerEnter", _recOnTrigger.LastValue);
            DrawTimingRow("BurstJob", _recBurstJob.LastValue);
            DrawTimingRow("ResolveDamage", _recResolveDamage.LastValue);
            DrawTimingRow("ProcessExplosion", _recProcessExplosion.LastValue);
            DrawTimingRow("ExplodeFrame", _recExplodeFrame.LastValue);
            DrawTimingRow("ProcessBatch", _recProcessBatch.LastValue);
            DrawTimingRow("PrismExplosionVFX", _recPrismExplosions.LastValue);
            DrawTimingRow("PrismImplosionVFX", _recPrismImplosions.LastValue);

            GUILayout.Space(8);

            // --- Rolling Average ---
            int samples = Mathf.Min(_frameIndex, ROLLING_WINDOW);
            GUILayout.Label($"Rolling Avg ({samples}f):", _headerStyle);
            DrawTimingRow("OnTriggerEnter", RollingAvg(_avgOnTrigger, samples), true);
            DrawTimingRow("BurstJob", RollingAvg(_avgBurstJob, samples), true);
            DrawTimingRow("ResolveDamage", RollingAvg(_avgResolveDmg, samples), true);
            DrawTimingRow("Total AOE", RollingAvg(_avgTotal, samples), true);

            GUILayout.Space(8);

            // --- Toggle Buttons ---
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Physics", _btnStyle))
                _currentMode = AOEMode.PhysicsOnly;
            if (GUILayout.Button("Burst", _btnStyle))
                _currentMode = AOEMode.Burst;
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Reset Stats", _btnStyle))
            {
                _frameIndex = 0;
                System.Array.Clear(_avgOnTrigger, 0, ROLLING_WINDOW);
                System.Array.Clear(_avgBurstJob, 0, ROLLING_WINDOW);
                System.Array.Clear(_avgResolveDmg, 0, ROLLING_WINDOW);
                System.Array.Clear(_avgTotal, 0, ROLLING_WINDOW);
            }

            GUI.DragWindow();
        }

        private void DrawTimingRow(string label, long nanoSeconds)
        {
            DrawTimingRow(label, NsToMs(nanoSeconds), true);
        }

        private void DrawTimingRow(string label, double ms, bool isAvg)
        {
            Color color = ms > 5.0 ? Color.red : ms > 1.0 ? Color.yellow : Color.green;
            var prev = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label($"  {label,-20} {ms,8:F3} ms", _labelStyle);
            GUI.contentColor = prev;
        }

        private static double NsToMs(long ns) => ns / 1_000_000.0;

        private static double RollingAvg(double[] buffer, int count)
        {
            if (count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < count; i++) sum += buffer[i];
            return sum / count;
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _stylesInit = true;

            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0, 0, 0, 0.85f));
            bgTex.Apply();

            _boxStyle = new GUIStyle(GUI.skin.window)
            {
                normal = { background = bgTex },
                padding = new RectOffset(8, 8, 8, 8)
            };

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                normal = { textColor = Color.white }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                font = Font.CreateDynamicFontFromOSFont("Consolas", 11),
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };

            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 11 };
        }
    }
}
#endif
