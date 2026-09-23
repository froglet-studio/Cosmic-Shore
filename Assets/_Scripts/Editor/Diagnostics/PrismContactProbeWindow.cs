using System;
using System.Collections.Generic;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// READER + A/B harness for the prism contact tier. Writes no assets.
    ///
    /// The question it exists to answer: what does it cost to stop resolving
    /// vessel/skimmer↔prism contact through PhysX box triggers and resolve it
    /// through the spatial index instead, for EVERY prism rather than only the
    /// shielded ones?
    ///
    /// Today <see cref="ShellContactQueryJob"/> escapes on a flag byte for the
    /// unshielded majority, so the tier is nearly free. Extended coverage deletes
    /// that escape: the scan pays a bounding test per slot per probe, with no
    /// broadphase under it. Everything else about the change — sampling at render
    /// rate instead of the 25 Hz physics tick, contact order that is deterministic
    /// across peers, and the ability to own a prism whose collider is stale because
    /// its pose is driven from the GPU — is only worth having if that scan is
    /// affordable. So measure it, on a real scene, on a real machine.
    ///
    /// Run A/B samples both modes for the same wall time and prints one block.
    /// Nothing here persists: both switches reset on the next play session.
    /// </summary>
    public class PrismContactProbeWindow : EditorWindow
    {
        const float SampleSeconds = 8f;

        enum Phase { Idle, WarmA, RunA, WarmB, RunB, Done }

        Phase _phase = Phase.Idle;
        double _phaseEndsAt;
        readonly List<double> _queryA = new(512), _queryB = new(512);
        readonly List<int> _hitsA = new(512), _hitsB = new(512);
        readonly List<int> _pairsA = new(512), _pairsB = new(512);
        int _entersA, _entersB, _framesA, _framesB;
        int _popA, _popB, _probesA, _probesB;
        string _report = "";
        Vector2 _scroll;

        [MenuItem("FrogletTools/Diagnostics/Prism Contact Probe", false, 12)]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 4,
            Description = "Measure what it costs to resolve every prism contact through the spatial index instead of PhysX triggers. Play mode; reader only.")]
        public static void Open()
        {
            var w = GetWindow<PrismContactProbeWindow>("Contact Probe");
            w.minSize = new Vector2(430f, 460f);
            w.Show();
        }

        void OnEnable()
        {
            EditorApplication.update += Tick;
            PrismShellContactManager.CollectDiagnostics = true;
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            PrismShellContactManager.CollectDiagnostics = false;
            if (_phase != Phase.Idle) Abort();
        }

        void Abort()
        {
            _phase = Phase.Idle;
            PrismShellContactManager.SetExtendToUnshieldedPrisms(false);
        }

        // ------------------------------------------------------------------

        void Tick()
        {
            if (!Application.isPlaying)
            {
                if (_phase != Phase.Idle) _phase = Phase.Idle;
                return;
            }
            if (_phase == Phase.Idle || _phase == Phase.Done) { Repaint(); return; }

            // Sampling frames, not the editor's update rate: one row per render frame.
            if (_phase == Phase.RunA)
            {
                _queryA.Add(PrismShellContactManager.LastQueryMs);
                _hitsA.Add(PrismShellContactManager.LastHitCount);
                _pairsA.Add(PrismShellContactManager.LastActivePairs);
                _entersA += PrismShellContactManager.LastEnterDispatches;
                _framesA++;
                _probesA = Mathf.Max(_probesA, PrismShellContactManager.LastProbeCount);
                _popA = Mathf.Max(_popA, PrismSpatialIndex.Instance != null ? PrismSpatialIndex.Instance.HighWaterMark : 0);
            }
            else if (_phase == Phase.RunB)
            {
                _queryB.Add(PrismShellContactManager.LastQueryMs);
                _hitsB.Add(PrismShellContactManager.LastHitCount);
                _pairsB.Add(PrismShellContactManager.LastActivePairs);
                _entersB += PrismShellContactManager.LastEnterDispatches;
                _framesB++;
                _probesB = Mathf.Max(_probesB, PrismShellContactManager.LastProbeCount);
                _popB = Mathf.Max(_popB, PrismSpatialIndex.Instance != null ? PrismSpatialIndex.Instance.HighWaterMark : 0);
            }

            if (EditorApplication.timeSinceStartup < _phaseEndsAt) { Repaint(); return; }

            switch (_phase)
            {
                case Phase.WarmA: Begin(Phase.RunA, SampleSeconds); break;
                case Phase.RunA:
                    PrismShellContactManager.SetExtendToUnshieldedPrisms(true);
                    Begin(Phase.WarmB, 1.5f);
                    break;
                case Phase.WarmB: Begin(Phase.RunB, SampleSeconds); break;
                case Phase.RunB:
                    PrismShellContactManager.SetExtendToUnshieldedPrisms(false);
                    _phase = Phase.Done;
                    _report = BuildReport();
                    Debug.Log(_report);
                    break;
            }
            Repaint();
        }

        void Begin(Phase p, float seconds)
        {
            _phase = p;
            _phaseEndsAt = EditorApplication.timeSinceStartup + seconds;
        }

        void StartRun()
        {
            _queryA.Clear(); _queryB.Clear(); _hitsA.Clear(); _hitsB.Clear();
            _pairsA.Clear(); _pairsB.Clear();
            _entersA = _entersB = _framesA = _framesB = 0;
            _popA = _popB = _probesA = _probesB = 0;
            _report = "";
            PrismShellContactManager.CollectDiagnostics = true;
            PrismShellContactManager.SetExtendToUnshieldedPrisms(false);
            Begin(Phase.WarmA, 1.5f);
        }

        // ------------------------------------------------------------------

        static double Median(List<double> v)
        {
            if (v.Count == 0) return 0d;
            var c = new List<double>(v); c.Sort();
            return c[c.Count / 2];
        }
        static double Pct(List<double> v, float p)
        {
            if (v.Count == 0) return 0d;
            var c = new List<double>(v); c.Sort();
            return c[Mathf.Clamp(Mathf.FloorToInt(c.Count * p), 0, c.Count - 1)];
        }
        static int MedianI(List<int> v)
        {
            if (v.Count == 0) return 0;
            var c = new List<int>(v); c.Sort();
            return c[c.Count / 2];
        }
        static int MaxI(List<int> v)
        {
            int m = 0;
            for (int i = 0; i < v.Count; i++) if (v[i] > m) m = v[i];
            return m;
        }

        string BuildReport()
        {
            var idx = PrismSpatialIndex.Instance;
            int box = 0, octa = 0, stella = 0;
            idx?.CountShells(out box, out octa, out stella);

            var sb = new StringBuilder();
            sb.AppendLine("=== PRISM CONTACT PROBE ===");
            sb.AppendLine($"scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}   "
                        + $"sample {SampleSeconds:0}s per mode   unity {Application.unityVersion}");
            sb.AppendLine($"registry high-water {Mathf.Max(_popA, _popB)}   probes {Mathf.Max(_probesA, _probesB)}   "
                        + $"fixedDelta {Time.fixedDeltaTime * 1000f:0.0} ms ({1f / Mathf.Max(Time.fixedDeltaTime, 1e-4f):0} Hz)");
            sb.AppendLine($"collider LOD: near {PrismColliderLodManager.LastNearCount} of {PrismColliderLodManager.LastLiveCount} live"
                        + "   <- the collider count extended coverage could actually remove");
            sb.AppendLine($"shell census now: box {box}  octa {octa}  stella {stella}");
            sb.AppendLine();
            sb.AppendLine("                         A: shielded only      B: every prism");
            sb.AppendLine($"  frames sampled         {_framesA,16}   {_framesB,17}");
            sb.AppendLine($"  query ms  median       {Median(_queryA),16:0.000}   {Median(_queryB),17:0.000}");
            sb.AppendLine($"  query ms  p95          {Pct(_queryA, 0.95f),16:0.000}   {Pct(_queryB, 0.95f),17:0.000}");
            sb.AppendLine($"  query ms  max          {Pct(_queryA, 0.999f),16:0.000}   {Pct(_queryB, 0.999f),17:0.000}");
            sb.AppendLine($"  hits/frame median      {MedianI(_hitsA),16}   {MedianI(_hitsB),17}");
            sb.AppendLine($"  hits/frame max         {MaxI(_hitsA),16}   {MaxI(_hitsB),17}");
            sb.AppendLine($"  active pairs median    {MedianI(_pairsA),16}   {MedianI(_pairsB),17}");
            sb.AppendLine($"  active pairs max       {MaxI(_pairsA),16}   {MaxI(_pairsB),17}");
            sb.AppendLine($"  enter dispatches/s     {(_framesA > 0 ? _entersA / SampleSeconds : 0),16:0.0}   {(_framesB > 0 ? _entersB / SampleSeconds : 0),17:0.0}");
            sb.AppendLine();

            double mA = Median(_queryA), mB = Median(_queryB);
            sb.AppendLine($"  query cost multiple    {(mA > 1e-6 ? (mB / mA).ToString("0.0") + "x" : "n/a (A too cheap to divide)")}");
            sb.AppendLine($"  added main-thread cost {(mB - mA):0.000} ms/frame median, {(Pct(_queryB, 0.95f) - Pct(_queryA, 0.95f)):0.000} ms at p95");
            int cap = Mathf.Min(262144, Mathf.Max(4096, Mathf.Max(_popA, _popB)));
            sb.AppendLine($"  AddNoResize headroom   peak hits {MaxI(_hitsB)} against capacity {cap}"
                        + (MaxI(_hitsB) > cap * 0.5f ? "   <-- WITHIN 2x OF A THROW" : "   (comfortable)"));
            sb.AppendLine();
            sb.AppendLine("Read it like this: B is the whole cost of the change. The scan has no");
            sb.AppendLine("broadphase, so B should grow with the registry, not with what is near the");
            sb.AppendLine("vessel - if B is affordable in the heaviest cell you have, the bucket-grid");
            sb.AppendLine("work is optional; if it is not, that work is the prerequisite.");
            sb.AppendLine("A large jump in enter dispatches/s is contacts the 25 Hz trigger path was");
            sb.AppendLine("missing, not a bug - unless it is enormous, which would be double-firing.");
            return sb.ToString();
        }

        // ------------------------------------------------------------------

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Prism Contact Probe",
                "PhysX triggers vs the spatial index, measured", FrogletEditorPalette.Info);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter play mode in the scene you want measured (the heaviest one you have), "
                    + "fly for a few seconds so prisms exist and the vessel is among them, then Run A/B.",
                    MessageType.Info);
                return;
            }

            var idx = PrismSpatialIndex.Instance;
            if (idx == null || !idx.IsAvailable)
            {
                EditorGUILayout.HelpBox("No live PrismSpatialIndex in this scene.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Live", EditorStyles.boldLabel);
                Row("registry high-water", idx.HighWaterMark.ToString());
                Row("probes this frame", PrismShellContactManager.LastProbeCount.ToString());
                Row("hits / active pairs",
                    $"{PrismShellContactManager.LastHitCount} / {PrismShellContactManager.LastActivePairs}");
                Row("query ms (last / peak)",
                    $"{PrismShellContactManager.LastQueryMs:0.000} / {PrismShellContactManager.PeakQueryMs:0.000}");
                Row("collider LOD near / live",
                    $"{PrismColliderLodManager.LastNearCount} / {PrismColliderLodManager.LastLiveCount}");
                Row("extended coverage",
                    PrismShellContactManager.ExtendToUnshieldedPrisms ? "ON" : "off");
            }

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(_phase != Phase.Idle && _phase != Phase.Done))
            {
                if (GUILayout.Button($"Run A/B  ({SampleSeconds:0}s per mode — keep flying)", GUILayout.Height(30f)))
                    StartRun();
            }

            if (_phase != Phase.Idle && _phase != Phase.Done)
            {
                float left = (float)Math.Max(0d, _phaseEndsAt - EditorApplication.timeSinceStartup);
                EditorGUILayout.HelpBox($"{_phase}  —  {left:0.0}s left. Keep flying through mass.", MessageType.None);
                if (GUILayout.Button("Abort")) Abort();
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool ext = PrismShellContactManager.ExtendToUnshieldedPrisms;
                if (GUILayout.Button(ext ? "Extended: ON (click to disable)" : "Extended: off (click to enable)"))
                    PrismShellContactManager.SetExtendToUnshieldedPrisms(!ext);
                if (GUILayout.Button("Reset peaks", GUILayout.Width(100f)))
                    PrismShellContactManager.ResetPeaks();
            }
            if (GUILayout.Button(PrismShellContactManager.ForceLegacyBoxInteraction
                    ? "Force legacy PhysX: ON (shell tier inert)"
                    : "Force legacy PhysX: off"))
                PrismShellContactManager.ForceLegacyBoxInteraction = !PrismShellContactManager.ForceLegacyBoxInteraction;

            if (!string.IsNullOrEmpty(_report))
            {
                EditorGUILayout.Space(6f);
                if (GUILayout.Button("Re-print report to console")) Debug.Log(_report);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(160f));
                EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(190f));
                EditorGUILayout.LabelField(value, EditorStyles.miniBoldLabel);
            }
        }
    }
}
