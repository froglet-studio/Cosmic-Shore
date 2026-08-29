#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Utility; // GameDataSO
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    public class PerformanceBenchmarkWindow : EditorWindow
    {
        enum Tab { Collect, Sweep, History, Compare, LoadInsights }
        [SerializeField] Tab activeTab = Tab.Collect;

        // ── Shared assets (serialized so they survive the play-mode domain reload) ──
        [SerializeField] BenchmarkConfigSO config;
        [SerializeField] BenchmarkHintRulesSO hintRules;
        [SerializeField] GameDataSO gameData;

        // Default config shipped in the repo; auto-loaded when nothing is assigned.
        const string DefaultConfigPath = "Assets/_SO_Assets/Benchmark/BenchmarkConfig.asset";

        // ── Runtime Capture tab ─────────────────────────
        PerformanceBenchmarkRunner collectRunner;
        Vector2 collectScroll;

        // Foldout + spike-filter state.
        [SerializeField] bool foldSetup;
        [SerializeField] bool foldTiming = true;
        [SerializeField] bool foldCpuGpu;
        [SerializeField] bool foldRender;
        [SerializeField] bool foldNetcode;
        [SerializeField] bool foldHints = true;
        [SerializeField] bool foldSpikes = true;
        [SerializeField] bool spikeScriptsOnly = true;
        [SerializeField] int spikeShowCount = 10;
        string spikeSearch = "";
        // Report currently shown in Collect. Cached to disk so it survives leaving Play Mode
        // (the domain reload wipes in-memory state). collectSavedPath is set once committed to History.
        [System.NonSerialized] BenchmarkReport collectReport;
        [SerializeField] string collectSavedPath = "";
        string cachedReportId = "";
        static string CollectCachePath =>
            Path.Combine(Application.persistentDataPath, "Benchmarks", "_collect_lastrun.json");

        // ── Sweep tab ───────────────────────────────────
        Vector2 sweepScroll;
        List<(string name, bool include)> sweepScenes;
        [SerializeField] string sweepTag = "sweep";
        [SerializeField] bool sweepCaptureErrors = true;
        [SerializeField] bool sweepErrorsOnly;
        BenchmarkSweepRunner activeSweep;
        readonly HashSet<int> sweepExpanded = new();

        // ── Manual session (primary Sweep mode) ─────────
        Vector2 sweepOuterScroll;
        PerformanceBenchmarkRunner sweepRunner;
        ManualSweepSession sweepSession;
        [System.NonSerialized] BenchmarkReport sweepReport;
        string sweepCachedReportId = "";
        [SerializeField] string sweepSavedPath = "";
        [SerializeField] string sweepMarkLabel = "";
        [SerializeField] bool foldAutomatic;
        [SerializeField] bool foldErrors = true;
        [SerializeField] bool foldMarks = true;
        static string SweepCachePath =>
            Path.Combine(Application.persistentDataPath, "Benchmarks", "_sweep_lastrun.json");

        // ── History tab ─────────────────────────────────
        Vector2 historyScroll;
        List<BenchmarkHistory.IndexEntry> historyEntries = new();
        string tagEditId;
        string tagEditValue = "";

        // ── Compare tab ─────────────────────────────────
        Vector2 compareScroll;
        int baselineIndex = -1;
        int currentIndex = -1;
        BenchmarkComparer.ComparisonResult comparisonResult;
        string comparisonText;

        [MenuItem("FrogletTools/Performance Benchmark", false, 20)]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 5,
            Description = "Frame-cost benchmark: score, hints, sweeps, load-time insights.")]
        public static void Open()
        {
            var window = GetWindow<PerformanceBenchmarkWindow>("Performance Benchmark");
            window.minSize = new Vector2(620, 520);
            window.Show();
        }

        void OnEnable()
        {
            RefreshHistory();
            // Auto-load the repo's default config so the tool works out of the box.
            if (config == null)
                config = AssetDatabase.LoadAssetAtPath<BenchmarkConfigSO>(DefaultConfigPath);
            // Restore the last Collect run after a domain reload (e.g. leaving Play Mode).
            if (collectReport == null && File.Exists(CollectCachePath))
                collectReport = BenchmarkReport.LoadFromFile(CollectCachePath);
            if (sweepReport == null && File.Exists(SweepCachePath))
                sweepReport = BenchmarkReport.LoadFromFile(SweepCachePath);
            LoadInsightsTab.OnWindowEnable();
        }

        // Spike enrichment (editor-side, off the game thread).
        readonly List<MarkerSample> _enrichScratch = new(16);
        double _nextEnrichTime;
        const double EnrichInterval = 0.35;   // seconds between hierarchy walks (keeps the editor responsive)
        const int TopMarkersToCapture = 8;

        // When off, the tool only records frame time / fps / stability - no Profiler enable,
        // no per-spike hierarchy walks - so it barely perturbs what it measures. Turn this off
        // for a true smoothness read; turn it on when you need the script breakdown of a spike.
        [SerializeField] bool captureSpikeBreakdowns = true;

        // Repaint throttle: redrawing this window every game frame is itself editor overhead
        // that bleeds into measured frame time. 10 Hz is plenty for a live readout.
        double _nextRepaint;
        const double RepaintInterval = 0.1;

        void Update()
        {
            // Re-acquire the live runner after the play-mode domain reload wiped our reference.
            if (Application.isPlaying && collectRunner == null)
                collectRunner = FindFirstObjectByType<PerformanceBenchmarkRunner>();

            if (captureSpikeBreakdowns)
                EnrichPendingSpikes();

            bool busy = (collectRunner != null && collectRunner.IsRunning) ||
                        (sweepRunner != null && sweepRunner.IsRunning) ||
                        (activeSweep != null && activeSweep.IsSweeping) ||
                        (activeTab == Tab.LoadInsights && LoadInsightsTab.IsBusy);
            if (busy && EditorApplication.timeSinceStartup >= _nextRepaint)
            {
                _nextRepaint = EditorApplication.timeSinceStartup + RepaintInterval;
                Repaint();
            }
        }

        /// <summary>
        /// Fills the script breakdown for captured spikes here, on the editor loop, instead of
        /// inside the game's capture frame. Rate-limited and worst-spike-first so a heavy
        /// hierarchy walk can't cascade into a spike storm or hang the editor. Runs only while a
        /// profiler frame for the spike is still in the buffer.
        /// </summary>
        void EnrichPendingSpikes()
        {
            if (collectRunner == null) return;
            if (EditorApplication.timeSinceStartup < _nextEnrichTime) return;

            var spikes = collectRunner.Spikes;
            if (spikes == null || spikes.Count == 0) return;

            int firstFrame = SpikeAnalyzer.FirstFrameIndex;
            SpikeEntry target = null;
            for (int i = 0; i < spikes.Count; i++)
            {
                var sp = spikes[i];
                if (sp == null) continue;
                if (sp.topMarkers != null && sp.topMarkers.Count > 0) continue;   // already done
                if (sp.profilerFrameIndex < 0 || sp.profilerFrameIndex < firstFrame) continue; // unavailable / scrolled out
                if (target == null || sp.frameTimeMs > target.frameTimeMs) target = sp;        // worst first
            }
            if (target == null) return;

            if (SpikeAnalyzer.TryGetTopMarkers(target.profilerFrameIndex, TopMarkersToCapture, _enrichScratch)
                && target.topMarkers != null)
            {
                for (int m = 0; m < _enrichScratch.Count; m++)
                    target.topMarkers.Add(_enrichScratch[m]);
                Repaint();
            }
            _nextEnrichTime = EditorApplication.timeSinceStartup + EnrichInterval;
        }

        void OnGUI()
        {
            DrawTabs();
            switch (activeTab)
            {
                case Tab.Collect: DrawCollectTab(); break;
                case Tab.Sweep: DrawSweepTab(); break;
                case Tab.History: DrawHistoryTab(); break;
                case Tab.Compare: DrawCompareTab(); break;
                case Tab.LoadInsights: LoadInsightsTab.Draw(this); break;
            }
        }

        void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawTabButton(Tab.Collect, "Runtime Capture");
            DrawTabButton(Tab.Sweep, "Sweep");
            DrawTabButton(Tab.History, $"History ({historyEntries.Count})");
            DrawTabButton(Tab.Compare, "Compare");
            DrawTabButton(Tab.LoadInsights, LoadInsights.IsRecording ? "● Load Time Insights" : "Load Time Insights");
            EditorGUILayout.EndHorizontal();
        }

        void DrawTabButton(Tab tab, string label)
        {
            bool on = activeTab == tab;
            var prev = GUI.backgroundColor;
            if (on) GUI.backgroundColor = EditorUIStyles.Sky;
            if (GUILayout.Toggle(on, label, EditorStyles.toolbarButton) && !on)
                activeTab = tab;
            GUI.backgroundColor = prev;
        }

        // ════════════════════════════════════════════════
        // ── Collect Tab ─────────────────────────────────
        // ════════════════════════════════════════════════

        void DrawCollectTab()
        {
            collectScroll = EditorGUILayout.BeginScrollView(collectScroll);
            EditorGUILayout.Space(6);

            if (config == null)
            {
                EditorUIStyles.SectionHeader("Setup", EditorUIStyles.Sky);
                EditorGUILayout.HelpBox("No benchmark config assigned.", MessageType.Info);
                config = (BenchmarkConfigSO)EditorGUILayout.ObjectField("Config", config, typeof(BenchmarkConfigSO), false);
                if (GUILayout.Button("Create Default Config")) CreateDefaultConfig();
                EditorGUILayout.EndScrollView();
                return;
            }

            // Collapsible setup keeps the tab uncluttered.
            foldSetup = EditorGUILayout.Foldout(foldSetup, "Setup", true, EditorStyles.foldoutHeader);
            if (foldSetup)
            {
                EditorGUI.indentLevel++;
                config = (BenchmarkConfigSO)EditorGUILayout.ObjectField("Config", config, typeof(BenchmarkConfigSO), false);
                hintRules = (BenchmarkHintRulesSO)EditorGUILayout.ObjectField("Hint Rules", hintRules, typeof(BenchmarkHintRulesSO), false);
                gameData = (GameDataSO)EditorGUILayout.ObjectField("Game Data", gameData, typeof(GameDataSO), false);
                captureSpikeBreakdowns = EditorGUILayout.ToggleLeft(
                    new GUIContent("Capture spike breakdowns (Profiler - adds overhead)",
                        "On: each spike gets its script breakdown via the Profiler (what you need to find a culprit). " +
                        "Off: records frame time / fps / stability only, near-zero overhead - use this for a TRUE smoothness read."),
                    captureSpikeBreakdowns);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button("Spawn Live HUD Overlay (F9 in Game view)"))
                        SpawnLiveHud();
                EditorGUI.indentLevel--;
            }

            if (!captureSpikeBreakdowns)
                EditorGUILayout.HelpBox(
                    "Low-overhead mode: frame time / fps / stability only (no script breakdown). " +
                    "Also CLOSE the Profiler window and turn OFF Deep Profile for a true read - they dominate editor frame time. " +
                    "The real ground truth is a Development Build run standalone, not the editor.",
                    MessageType.Info);

            // Adopt a finished run before drawing so results appear the moment recording stops.
            if (collectRunner != null && collectRunner.LastReport != null &&
                collectRunner.LastReport.reportId != cachedReportId)
            {
                collectReport = collectRunner.LastReport;
                cachedReportId = collectReport.reportId;
                collectSavedPath = collectRunner.LastReportPath ?? "";
                CacheCollectReport(collectReport);
            }

            var report = collectReport;
            bool hasReport = report?.statistics != null && report.statistics.totalFrames > 0;
            bool running = collectRunner != null && collectRunner.IsRunning;

            EditorGUILayout.Space(6);
            EditorUIStyles.SectionHeader("Record", EditorUIStyles.Mint);

            // While recording: only the live status + spikes, nothing else.
            if (running)
            {
                DrawRecordingStatus();
                EditorGUILayout.EndScrollView();
                return;
            }

            // Idle: one state-appropriate primary button.
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode, get to the scene you want to profile, then press ● Start Recording.", MessageType.Info);
                DrawAccentButton("▶  Enter Play Mode", EditorUIStyles.Sky, 30, () => EditorApplication.isPlaying = true);
            }
            else
            {
                DrawAccentButton("●  Start Recording", EditorUIStyles.Mint, 32, StartFreeFormInCurrentPlay);
            }

            // Copy / Save / Clear - disabled until there's a recorded run.
            DrawActionRow(report, hasReport);

            // Results (collapsible).
            if (hasReport)
                DrawResults(report);
            else if (report?.statistics != null)
                EditorGUILayout.HelpBox("No data captured (the run was too short or interrupted).", MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        void DrawAccentButton(string label, Color color, float height, System.Action onClick)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            if (GUILayout.Button(label, GUILayout.Height(height))) onClick();
            GUI.backgroundColor = prev;
        }

        void DrawActionRow(BenchmarkReport report, bool hasReport)
        {
            EditorGUILayout.Space(4);
            bool saved = !string.IsNullOrEmpty(collectSavedPath);

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!hasReport))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = EditorUIStyles.Lavender;
                if (GUILayout.Button("📋  Copy error log", GUILayout.Height(24)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildClaudeReportText(report);
                    CacheCollectReport(report);   // persist any spikes enriched after the run
                    ShowNotification(new GUIContent("Error log copied + cached"));
                }
                GUI.backgroundColor = prev;
            }

            using (new EditorGUI.DisabledScope(!hasReport || saved))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = EditorUIStyles.Mint;
                if (GUILayout.Button(saved ? "Saved ✓" : "Save", GUILayout.Height(24)))
                {
                    string path = report.SaveToFile(GetOutputFolder());
                    BenchmarkHistory.AddToHistory(report, path, GetOutputFolder());
                    collectSavedPath = path;
                    RefreshHistory();
                }
                GUI.backgroundColor = prev;
            }

            using (new EditorGUI.DisabledScope(!hasReport))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = EditorUIStyles.Rose;
                if (GUILayout.Button("Clear Recent", GUILayout.Height(24)))
                    ClearRecent();
                GUI.backgroundColor = prev;
            }

            EditorGUILayout.EndHorizontal();

            if (hasReport && !saved)
                EditorGUILayout.LabelField("Unsaved - Save keeps it in History; Clear discards it. Re-recording also discards it.", EditorStyles.miniLabel);
        }

        void ClearRecent()
        {
            ResetCollectDisplay();
            try { if (File.Exists(CollectCachePath)) File.Delete(CollectCachePath); }
            catch { /* best-effort */ }
        }

        // ── Runtime Capture: live recording + Copy error log ───────────────

        void DrawRecordingStatus()
        {
            int spikeCount = collectRunner.Spikes?.Count ?? 0;
            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorUIStyles.TintLastRect(rect, EditorUIStyles.Rose, 0.10f);
            EditorGUILayout.LabelField($"● Recording - {collectRunner.FramesCaptured} frames · {spikeCount} spikes",
                EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            DrawAccentButton("■  Stop & Analyze", EditorUIStyles.Rose, 28, () => collectRunner.StopBenchmark());

            EditorGUILayout.Space(4);
            EditorUIStyles.SectionHeader($"Live Spikes ({spikeCount})", EditorUIStyles.Rose);
            DrawSpikeFilters();

            var spikes = collectRunner.Spikes;
            if (spikeCount == 0)
            {
                EditorGUILayout.HelpBox("No spikes yet - keep playing. Frames over the threshold get their script breakdown captured here.", MessageType.None);
                return;
            }
            int shown = 0;                                   // newest first
            for (int i = spikeCount - 1; i >= 0 && shown < spikeShowCount; i--, shown++)
                DrawSpikeRow(spikes[i]);
        }

        void DrawSpikeFilters()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            spikeScriptsOnly = GUILayout.Toggle(spikeScriptsOnly, "Scripts only", EditorStyles.toolbarButton, GUILayout.Width(92));
            GUILayout.Label("Show", EditorStyles.miniLabel, GUILayout.Width(34));
            spikeShowCount = EditorGUILayout.IntPopup(spikeShowCount,
                new[] { "5", "10", "20", "All" }, new[] { 5, 10, 20, 9999 }, GUILayout.Width(58));
            GUILayout.Space(6);
            spikeSearch = EditorGUILayout.TextField(spikeSearch, EditorStyles.toolbarSearchField);
            EditorGUILayout.EndHorizontal();
        }

        void DrawSpikes(List<SpikeEntry> spikes)
        {
            if (spikes == null || spikes.Count == 0)
            {
                EditorGUILayout.HelpBox("No spikes captured. 🎉", MessageType.None);
                return;
            }
            foreach (var spike in spikes.OrderByDescending(sp => sp.frameTimeMs).Take(spikeShowCount))
                DrawSpikeRow(spike);
        }

        void DrawSpikeRow(SpikeEntry spike)
        {
            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Color tint = spike.frameTimeMs >= 100f ? EditorUIStyles.Rose
                       : spike.frameTimeMs >= 50f ? EditorUIStyles.Peach
                       : EditorUIStyles.Slate;
            EditorUIStyles.TintLastRect(rect, tint, 0.12f);

            string cpuGpu = (spike.cpuFrameTimeMs > 0.001f || spike.gpuFrameTimeMs > 0.001f)
                ? $"    CPU {spike.cpuFrameTimeMs:F0} / GPU {spike.gpuFrameTimeMs:F0}" : "";
            EditorGUILayout.LabelField($"⚡ Frame {spike.frameIndex}  -  {spike.frameTimeMs:F1} ms{cpuGpu}", EditorStyles.boldLabel);

            if (spike.topMarkers == null || spike.topMarkers.Count == 0)
            {
                EditorGUILayout.LabelField("   breakdown pending (or scrolled out of the profiler buffer)", EditorStyles.miniLabel);
            }
            else
            {
                bool any = false;
                foreach (var m in spike.topMarkers)
                {
                    if (spikeScriptsOnly && !m.isScript) continue;
                    if (!string.IsNullOrEmpty(spikeSearch) &&
                        m.name.IndexOf(spikeSearch, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    DrawMarkerBar(m);
                    any = true;
                }
                if (!any) EditorGUILayout.LabelField("   (no markers match the filter)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        void DrawMarkerBar(MarkerSample m)
        {
            EditorGUILayout.BeginHorizontal();
            var prevC = GUI.contentColor;
            GUI.contentColor = m.isScript ? new Color(0.60f, 0.90f, 0.68f) : new Color(0.64f, 0.66f, 0.72f);
            GUILayout.Label((m.isScript ? "▸ " : "· ") + ShortName(m.name), EditorStyles.miniLabel);
            GUI.contentColor = prevC;
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{m.ms:F2} ms", EditorStyles.miniLabel, GUILayout.Width(58));
            EditorGUILayout.EndHorizontal();
        }

        // Trims "Assembly-CSharp.dll!Namespace::" prefixes and " [Invoke]" so names read cleanly.
        static string ShortName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int ns = name.LastIndexOf("::", System.StringComparison.Ordinal);
            if (ns >= 0) name = name.Substring(ns + 2);
            return name.Replace(" [Invoke]", "");
        }

        static string BuildClaudeReportText(BenchmarkReport report)
        {
            var s = report.statistics;
            var sb = new System.Text.StringBuilder(2048);
            sb.AppendLine($"Cosmic Shore perf capture - {report.sceneName}");
            if (s != null)
            {
                sb.AppendLine($"frames {s.totalFrames} · {s.durationSeconds:F0}s · avg {s.avgFps:F1} fps (worst1% {s.p1Fps:F1})");
                sb.AppendLine($"frame ms: avg {s.avgFrameTimeMs:F1} · p95 {s.p95FrameTimeMs:F1} · p99 {s.p99FrameTimeMs:F1} · max {s.maxFrameTimeMs:F1} · stddev {s.stdDevFrameTimeMs:F1}");
                if (s.avgCpuFrameTimeMs > 0.001f || s.avgGpuFrameTimeMs > 0.001f)
                    sb.AppendLine($"CPU {s.avgCpuFrameTimeMs:F1} / GPU {s.avgGpuFrameTimeMs:F1} ms ({report.analysis?.boundVerdict})");
                sb.AppendLine($"draws {s.avgDrawCalls:F0} · tris {s.avgTriangles:F0} · GC {(s.totalFrames > 0 ? (s.totalGcAllocated / (float)s.totalFrames) / 1024f : 0):F1} KB/frame");
            }
            var spikes = report.spikes;
            if (spikes != null && spikes.Count > 0)
            {
                sb.AppendLine("Top spikes (self-time; editor noise filtered, ▸ = script):");
                foreach (var sp in spikes.OrderByDescending(x => x.frameTimeMs).Take(12))
                    AppendSpike(sb, sp);
            }
            var hints = report.analysis?.hints;
            if (hints != null && hints.Count > 0)
            {
                sb.AppendLine("Hints:");
                foreach (var h in hints.OrderByDescending(x => (int)x.severity))
                    sb.AppendLine($"  [{h.severity}] {h.title} - {h.finding}");
            }
            return sb.ToString();
        }

        static void AppendSpike(System.Text.StringBuilder sb, SpikeEntry s)
        {
            string cpuGpu = (s.cpuFrameTimeMs > 0.001f || s.gpuFrameTimeMs > 0.001f)
                ? $" (CPU {s.cpuFrameTimeMs:F1}/GPU {s.gpuFrameTimeMs:F1})" : "";
            sb.AppendLine($"  frame {s.frameIndex}: {s.frameTimeMs:F1} ms{cpuGpu}");
            if (s.topMarkers != null)
                foreach (var m in s.topMarkers)
                    sb.AppendLine($"      {(m.isScript ? "▸" : "·")} {ShortName(m.name)}  {m.ms:F2} ms");
        }

        void StartFreeFormInCurrentPlay()
        {
            collectRunner = FindFirstObjectByType<PerformanceBenchmarkRunner>();
            if (collectRunner == null)
                collectRunner = new GameObject("[PerformanceBenchmarkRunner]").AddComponent<PerformanceBenchmarkRunner>();

            // Only force the Profiler on when we actually want spike breakdowns - it adds
            // overhead. In low-overhead mode we record frame time / fps / stability only.
            if (captureSpikeBreakdowns)
                SpikeAnalyzer.SetProfilerEnabled(true);
            ClearRecent();                 // discard any previous unsaved run + its cache
            collectRunner.Configure(config, null, gameData, hintRules);
            collectRunner.AutoSave = false;
            collectRunner.StartBenchmark(true); // free-form: record until Stop
        }

        void CacheCollectReport(BenchmarkReport report)
        {
            try
            {
                var dir = Path.GetDirectoryName(CollectCachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(CollectCachePath, JsonUtility.ToJson(report, false));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Benchmark] Could not cache last run for display: {e.Message}");
            }
        }

        void DrawResults(BenchmarkReport report)
        {
            var s = report.statistics;
            var analysis = report.analysis;

            EditorGUILayout.Space(8);
            EditorUIStyles.SectionHeader("Results", EditorUIStyles.Lavender);

            int score = analysis?.score ?? BenchmarkAnalysis.ComputeScore(s);
            string grade = BenchmarkGrade.Evaluate(s, out string explanation);
            EditorUIStyles.ScoreBar(score, $"Score {score}/100   ·   Grade {grade} - {explanation}");

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            EditorUIStyles.Badge($"Grade {grade}", EditorUIStyles.ForGrade(grade), 80);
            if (analysis != null) EditorUIStyles.Badge(analysis.boundVerdict, EditorUIStyles.Slate, 110);
            if (analysis != null && analysis.isBlocked) EditorUIStyles.Badge("BLOCKERS", EditorUIStyles.Rose, 90);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // One-line summary, always visible.
            EditorGUILayout.BeginHorizontal();
            EditorUIStyles.Badge($"{s.avgFps:F0} fps", EditorUIStyles.ForScore(score), 64);
            EditorUIStyles.Badge($"p99 {s.p99FrameTimeMs:F0} ms", EditorUIStyles.Slate, 92);
            if (analysis != null) EditorUIStyles.Badge(analysis.boundVerdict, EditorUIStyles.Sky, 100);
            if (analysis != null && analysis.isBlocked) EditorUIStyles.Badge("BLOCKERS", EditorUIStyles.Rose, 84);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Frame timing
            if (Section(ref foldTiming, "Frame timing"))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorUIStyles.StatRow("Avg FPS", $"{s.avgFps:F1}", "Higher is better. Target 60.");
                EditorUIStyles.StatRow("Worst 1% FPS", $"{s.p1Fps:F1}", "FPS during the worst spikes.");
                EditorUIStyles.StatRow("Avg Frame Time", $"{s.avgFrameTimeMs:F2} ms", "16.7ms = 60fps, 33.3ms = 30fps.");
                EditorUIStyles.StatRow("P99 Frame Time", $"{s.p99FrameTimeMs:F2} ms", "Worst-1% frame time.");
                EditorUIStyles.StatRow("Stability (StdDev)", $"{s.stdDevFrameTimeMs:F2} ms", "Lower = smoother. >6ms = hitching.");
                EditorGUILayout.LabelField($"Collector overhead: {s.collectorAllocBytesPerFrame:F1} B/frame" +
                    (s.collectorAllocBytesPerFrame > 64f ? "  ⚠" : "  ✓"), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }

            // CPU / GPU
            if (Section(ref foldCpuGpu, "CPU / GPU"))
            {
                if (s.avgCpuFrameTimeMs > 0.001f || s.avgGpuFrameTimeMs > 0.001f)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorUIStyles.StatRow("CPU Frame Time", $"{s.avgCpuFrameTimeMs:F2} ms  (max {s.maxCpuFrameTimeMs:F1})", "Main + render thread CPU time.");
                    EditorUIStyles.StatRow("GPU Frame Time", $"{s.avgGpuFrameTimeMs:F2} ms  (max {s.maxGpuFrameTimeMs:F1})", "GPU time (0 if platform can't report it).");
                    EditorGUILayout.EndVertical();
                }
                else EditorGUILayout.HelpBox("CPU/GPU split unavailable - enable 'Frame Timing Stats' in Player Settings.", MessageType.None);
            }

            // Rendering & memory
            if (Section(ref foldRender, "Rendering & memory"))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorUIStyles.StatRow("Draw Calls", $"{s.avgDrawCalls:F0}", "Lower is better.");
                EditorUIStyles.StatRow("Batches / Tris", $"{s.avgBatches:F0} / {s.avgTriangles:F0}");
                if (s.peakAllocatedMemory > 0)
                {
                    EditorUIStyles.StatRow("Peak Memory", $"{s.peakAllocatedMemory / (1024f * 1024f):F1} MB");
                    EditorUIStyles.StatRow("GC Total", $"{s.totalGcAllocated / (1024f * 1024f):F2} MB", "Total garbage created during the run.");
                }
                if (s.avgActivePrisms > 0 || s.peakActiveExplosions > 0)
                    EditorUIStyles.StatRow("Load", $"prisms {s.avgActivePrisms:F0} (peak {s.peakActivePrisms}), VFX peak {s.peakActiveExplosions}/{s.peakActiveImplosions}");
                EditorGUILayout.EndVertical();
            }

            // Netcode
            bool hasNetcode = s.avgNetcodeTimeMs > 0.0001f || s.avgRpcsSent > 0f || s.avgNetVarsDirty > 0f;
            if (Section(ref foldNetcode, "Netcode"))
            {
                if (hasNetcode)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorUIStyles.StatRow("Netcode Share", $"{s.netcodeSharePercent:F0}%  ({s.avgNetcodeTimeMs:F2} ms/frame, max {s.maxNetcodeTimeMs:F1})", "CSM.Net.* marker time as a share of frame time.");
                    EditorUIStyles.StatRow("RPCs / frame", $"{s.avgRpcsSent:F1}");
                    EditorUIStyles.StatRow("NetVars dirty / frame", $"{s.avgNetVarsDirty:F1}");
                    if (s.totalNetBytesSent > 0) EditorUIStyles.StatRow("Bytes sent (total)", $"{s.totalNetBytesSent / 1024f:F1} KB");
                    if (report.networkTickRate > 0) EditorUIStyles.StatRow("Network tick rate", $"{report.networkTickRate} Hz");
                    EditorGUILayout.EndVertical();
                }
                else EditorGUILayout.HelpBox("No netcode activity recorded.", MessageType.None);
            }

            // Hints
            int hintCount = analysis?.hints?.Count ?? 0;
            if (Section(ref foldHints, $"Hints ({hintCount})"))
                DrawHints(analysis);

            // Spikes (filtered + colored)
            int spikeCount = report.spikes?.Count ?? 0;
            if (Section(ref foldSpikes, $"Top Spikes ({spikeCount})"))
            {
                DrawSpikeFilters();
                DrawSpikes(report.spikes);
            }
        }

        // Styled foldout header; returns the updated open state.
        bool Section(ref bool state, string title)
        {
            EditorGUILayout.Space(2);
            state = EditorGUILayout.Foldout(state, title, true, EditorStyles.foldoutHeader);
            return state;
        }

        void DrawHints(BenchmarkAnalysisResult analysis)
        {
            if (analysis == null || analysis.hints == null || analysis.hints.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues flagged. 🎉", MessageType.None);
                return;
            }

            foreach (var h in analysis.hints.OrderByDescending(h => (int)h.severity))
            {
                var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorUIStyles.TintLastRect(rect, EditorUIStyles.ForSeverity(h.severity), 0.14f);

                EditorGUILayout.BeginHorizontal();
                EditorUIStyles.Badge(h.severity.ToString(), EditorUIStyles.ForSeverity(h.severity), 70);
                EditorGUILayout.LabelField(h.title, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(h.finding)) EditorGUILayout.LabelField(h.finding, EditorUIStyles.Wrap);
                if (!string.IsNullOrEmpty(h.fixAdvice)) EditorGUILayout.LabelField($"Fix: {h.fixAdvice}", EditorUIStyles.Wrap);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        void ResetCollectDisplay()
        {
            collectReport = null;
            cachedReportId = "";
            collectSavedPath = "";
        }

        void SpawnLiveHud()
        {
            if (FindFirstObjectByType<BenchmarkHUDOverlay>() != null)
            {
                Debug.Log("[Benchmark] Live HUD overlay already present - press F9 in the Game view to toggle.");
                return;
            }
            new GameObject("[BenchmarkHUDOverlay]").AddComponent<BenchmarkHUDOverlay>();
            Debug.Log("[Benchmark] Live HUD overlay spawned - press F9 in the Game view to show/hide it.");
        }

        void CreateDefaultConfig()
        {
            const string root = "Assets/_SO_Assets";
            const string dir = root + "/Benchmark";
            const string path = dir + "/BenchmarkConfig.asset";

            config = AssetDatabase.LoadAssetAtPath<BenchmarkConfigSO>(path);
            if (config != null) return;

            if (!AssetDatabase.IsValidFolder(root)) AssetDatabase.CreateFolder("Assets", "_SO_Assets");
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(root, "Benchmark");

            config = CreateInstance<BenchmarkConfigSO>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Benchmark] Created default config at {path}");
        }

        // ════════════════════════════════════════════════
        // ── Sweep Tab ───────────────────────────────────
        // ════════════════════════════════════════════════

        void DrawSweepTab()
        {
            sweepOuterScroll = EditorGUILayout.BeginScrollView(sweepOuterScroll);
            EditorGUILayout.Space(6);
            EditorUIStyles.SectionHeader("Manual Session", EditorUIStyles.Mint);
            EditorGUILayout.HelpBox(
                "Play the game and this records a data set with minimal FPS hit: frame stats, a " +
                "timestamped error/exception log, and the moments you mark (F8). Stop & Save to keep it.",
                MessageType.None);

            // Adopt a finished session so results show the moment it stops.
            if (sweepRunner != null && sweepRunner.LastReport != null &&
                sweepRunner.LastReport.reportId != sweepCachedReportId)
            {
                sweepReport = sweepRunner.LastReport;
                sweepCachedReportId = sweepReport.reportId;
                sweepSavedPath = sweepRunner.LastReportPath ?? "";
                CacheSweepReport(sweepReport);
            }

            bool running = sweepRunner != null && sweepRunner.IsRunning;
            var report = sweepReport;
            bool hasReport = report?.statistics != null && report.statistics.totalFrames > 0;

            if (running)
            {
                DrawSweepRecording();
            }
            else if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode, get into the game, then Start Session.", MessageType.Info);
                DrawAccentButton("▶  Enter Play Mode", EditorUIStyles.Sky, 28, () => EditorApplication.isPlaying = true);
            }
            else
            {
                bool captureBusy = collectRunner != null && collectRunner.IsRunning;
                using (new EditorGUI.DisabledScope(captureBusy || config == null))
                    DrawAccentButton("●  Start Session", EditorUIStyles.Mint, 30, StartManualSweep);
                if (captureBusy)
                    EditorGUILayout.LabelField("Stop the Runtime Capture recording first.", EditorStyles.miniLabel);
                else if (config == null)
                    EditorGUILayout.LabelField("Assign a Config in the Runtime Capture tab first.", EditorStyles.miniLabel);
            }

            if (!running)
            {
                DrawSweepActionRow(report, hasReport);
                if (hasReport) DrawSweepResultsManual(report);
            }

            // ── Automatic multi-scene sweep (secondary / experimental) ──
            EditorGUILayout.Space(10);
            foldAutomatic = EditorGUILayout.Foldout(foldAutomatic, "Automatic (multi-scene) - experimental", true, EditorStyles.foldoutHeader);
            if (foldAutomatic) DrawAutomaticSweep();

            EditorGUILayout.EndScrollView();
        }

        // ── Manual session ──────────────────────────────────────────────────

        void DrawSweepRecording()
        {
            int errs = sweepSession != null ? sweepSession.ErrorCount : 0;
            int marks = sweepSession != null ? sweepSession.Marks.Count : 0;

            var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorUIStyles.TintLastRect(rect, errs > 0 ? EditorUIStyles.Rose : EditorUIStyles.Mint, 0.10f);
            EditorGUILayout.LabelField($"● Session - {sweepRunner.FramesCaptured} frames · {errs} errors · {marks} marks",
                EditorStyles.boldLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            DrawAccentButton("■  Stop & Save", EditorUIStyles.Rose, 26, StopManualSweep);
            sweepMarkLabel = EditorGUILayout.TextField(sweepMarkLabel, GUILayout.Width(150));
            using (new EditorGUI.DisabledScope(sweepSession == null))
                if (GUILayout.Button("Mark (F8)", GUILayout.Width(90)))
                {
                    sweepSession.AddMark(sweepMarkLabel);
                    sweepMarkLabel = "";
                }
            EditorGUILayout.EndHorizontal();

            if (sweepSession != null && sweepSession.Errors.Count > 0)
            {
                EditorUIStyles.SectionHeader($"Errors ({sweepSession.ErrorCount})", EditorUIStyles.Rose);
                DrawErrorList(sweepSession.Errors, 6);
            }
            if (sweepSession != null && sweepSession.Marks.Count > 0)
            {
                EditorUIStyles.SectionHeader($"Marks ({sweepSession.Marks.Count})", EditorUIStyles.Amber);
                DrawMarkList(sweepSession.Marks, 6);
            }
        }

        void DrawSweepActionRow(BenchmarkReport report, bool hasReport)
        {
            EditorGUILayout.Space(4);
            bool saved = !string.IsNullOrEmpty(sweepSavedPath);
            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(!hasReport))
            {
                var prev = GUI.backgroundColor; GUI.backgroundColor = EditorUIStyles.Lavender;
                if (GUILayout.Button("📋  Copy error log", GUILayout.Height(24)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildSweepLogText(report);
                    CacheSweepReport(report);
                    ShowNotification(new GUIContent("Error log copied + cached"));
                }
                GUI.backgroundColor = prev;
            }
            using (new EditorGUI.DisabledScope(!hasReport || saved))
            {
                var prev = GUI.backgroundColor; GUI.backgroundColor = EditorUIStyles.Mint;
                if (GUILayout.Button(saved ? "Saved ✓" : "Save", GUILayout.Height(24)))
                {
                    string path = report.SaveToFile(GetOutputFolder());
                    BenchmarkHistory.AddToHistory(report, path, GetOutputFolder());
                    sweepSavedPath = path; RefreshHistory();
                }
                GUI.backgroundColor = prev;
            }
            using (new EditorGUI.DisabledScope(!hasReport))
            {
                var prev = GUI.backgroundColor; GUI.backgroundColor = EditorUIStyles.Rose;
                if (GUILayout.Button("Clear Recent", GUILayout.Height(24))) ClearSweepRecent();
                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawSweepResultsManual(BenchmarkReport report)
        {
            DrawResults(report);   // reuse the stats foldouts (low-overhead: spike breakdowns are off)

            int errCount = report.errors?.Count ?? 0;
            if (Section(ref foldErrors, $"Errors ({errCount})"))
            {
                if (errCount == 0) EditorGUILayout.HelpBox("No errors captured. 🎉", MessageType.None);
                else DrawErrorList(report.errors, 25);
            }
            int markCount = report.marks?.Count ?? 0;
            if (Section(ref foldMarks, $"Marks ({markCount})"))
            {
                if (markCount == 0) EditorGUILayout.HelpBox("No marks. Press F8 while playing to drop one.", MessageType.None);
                else DrawMarkList(report.marks, 25);
            }
        }

        void DrawErrorList(IReadOnlyList<SweepError> errors, int max)
        {
            int n = errors.Count;
            int start = Mathf.Max(0, n - max);
            for (int i = n - 1; i >= start; i--)   // newest first
            {
                var e = errors[i];
                var prevC = GUI.contentColor;
                GUI.contentColor = e.type == "Exception"
                    ? new Color(0.97f, 0.45f, 0.45f) : new Color(0.98f, 0.80f, 0.52f);
                EditorGUILayout.LabelField($"[{e.timeSeconds:F1}s] {e.type}: {e.message}", EditorUIStyles.Wrap);
                GUI.contentColor = prevC;
            }
            if (start > 0) EditorGUILayout.LabelField($"   …and {start} earlier", EditorStyles.miniLabel);
        }

        void DrawMarkList(IReadOnlyList<SweepMark> marks, int max)
        {
            int n = marks.Count;
            int start = Mathf.Max(0, n - max);
            for (int i = n - 1; i >= start; i--)
            {
                var m = marks[i];
                EditorGUILayout.LabelField($"[{m.timeSeconds:F1}s] {m.label} - {m.fps:F0} fps", EditorStyles.miniLabel);
            }
            if (start > 0) EditorGUILayout.LabelField($"   …and {start} earlier", EditorStyles.miniLabel);
        }

        static string BuildSweepLogText(BenchmarkReport report)
        {
            var sb = new System.Text.StringBuilder(2048);
            sb.AppendLine($"Cosmic Shore manual session - {report.sceneName}");
            var s = report.statistics;
            if (s != null)
                sb.AppendLine($"frames {s.totalFrames} · {s.durationSeconds:F0}s · avg {s.avgFps:F1} fps · " +
                              $"p99 {s.p99FrameTimeMs:F1} ms · stddev {s.stdDevFrameTimeMs:F1} ms · GC {(s.totalFrames > 0 ? (s.totalGcAllocated / (float)s.totalFrames) / 1024f : 0):F1} KB/f");

            var errs = report.errors;
            sb.AppendLine($"Errors ({errs?.Count ?? 0}):");
            if (errs != null) foreach (var e in errs) sb.AppendLine($"  [{e.timeSeconds:F1}s] {e.type}: {e.message}");

            var marks = report.marks;
            if (marks != null && marks.Count > 0)
            {
                sb.AppendLine($"Marks ({marks.Count}):");
                foreach (var m in marks) sb.AppendLine($"  [{m.timeSeconds:F1}s] {m.label} ({m.fps:F0} fps)");
            }
            return sb.ToString();
        }

        void StartManualSweep()
        {
            if (config == null) { Debug.LogWarning("[Benchmark] No config for the manual session."); return; }
            ClearSweepRecent();
            sweepRunner = new GameObject("[PerformanceBenchmarkRunner]").AddComponent<PerformanceBenchmarkRunner>();
            sweepRunner.Configure(config, null, gameData, hintRules);
            sweepRunner.AutoSave = false;
            sweepRunner.StartBenchmark(true);            // free-form, low overhead (no profiler walks)
            sweepSession = ManualSweepSession.StartSession();
        }

        void StopManualSweep()
        {
            if (sweepRunner == null) return;
            sweepRunner.StopBenchmark();                 // FinishRun is synchronous → LastReport set
            if (sweepSession != null && sweepRunner.LastReport != null)
                sweepSession.FillReport(sweepRunner.LastReport);
            ManualSweepSession.Stop();
            sweepSession = null;
        }

        void ClearSweepRecent()
        {
            sweepReport = null;
            sweepCachedReportId = "";
            sweepSavedPath = "";
            try { if (File.Exists(SweepCachePath)) File.Delete(SweepCachePath); } catch { /* best-effort */ }
        }

        void CacheSweepReport(BenchmarkReport report)
        {
            try
            {
                var dir = Path.GetDirectoryName(SweepCachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(SweepCachePath, JsonUtility.ToJson(report, false));
            }
            catch (System.Exception e) { Debug.LogWarning($"[Benchmark] Could not cache sweep run: {e.Message}"); }
        }

        // ── Automatic multi-scene sweep (experimental, parked) ──────────────

        void DrawAutomaticSweep()
        {
            EditorGUILayout.HelpBox("Loads each selected scene directly and benchmarks it. Networked game scenes that need the Bootstrap → host pipeline sweep in an uninitialized state.", MessageType.None);

            sweepTag = EditorGUILayout.TextField("Sweep Tag", sweepTag);

            EditorGUILayout.BeginHorizontal();
            sweepCaptureErrors = EditorGUILayout.ToggleLeft("Capture errors", sweepCaptureErrors, GUILayout.Width(140));
            sweepErrorsOnly = EditorGUILayout.ToggleLeft(new GUIContent("Errors only (fast scan)", "Skip the full benchmark; just load each scene briefly and catch errors."), sweepErrorsOnly);
            EditorGUILayout.EndHorizontal();

            EnsureSweepScenesPopulated();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Scenes (from Build Settings)", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(80))) PopulateSweepScenes();
            if (GUILayout.Button("All", GUILayout.Width(44))) SetAllSweep(true);
            if (GUILayout.Button("None", GUILayout.Width(50))) SetAllSweep(false);
            EditorGUILayout.EndHorizontal();

            sweepScroll = EditorGUILayout.BeginScrollView(sweepScroll, GUILayout.MaxHeight(150));
            for (int i = 0; i < sweepScenes.Count; i++)
            {
                bool inc = EditorGUILayout.ToggleLeft(sweepScenes[i].name, sweepScenes[i].include);
                if (inc != sweepScenes[i].include) sweepScenes[i] = (sweepScenes[i].name, inc);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            bool sweepRunning = activeSweep != null && activeSweep.IsSweeping;
            if (sweepRunning)
            {
                float p = activeSweep.TotalScenes > 0 ? (float)activeSweep.CurrentIndex / activeSweep.TotalScenes : 0f;
                var rect = EditorGUILayout.GetControlRect(false, 22);
                EditorGUI.ProgressBar(rect, p, $"Sweeping {activeSweep.CurrentIndex + 1}/{activeSweep.TotalScenes}: {activeSweep.CurrentScene}");
            }
            else
            {
                using (new EditorGUI.DisabledScope(!Application.isPlaying || (config == null && !sweepErrorsOnly)))
                {
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = EditorUIStyles.Mint;
                    if (GUILayout.Button(sweepErrorsOnly ? "Run Error Scan" : "Start Sweep", GUILayout.Height(26)))
                        StartSweep();
                    GUI.backgroundColor = prev;
                }
            }

            DrawSweepResults();
        }

        void DrawSweepResults()
        {
            if (activeSweep == null || activeSweep.Results.Count == 0) return;

            EditorGUILayout.Space(6);
            EditorUIStyles.SectionHeader(activeSweep.IsComplete ? "Sweep Results" : "Sweep Progress", EditorUIStyles.Lavender);

            for (int i = 0; i < activeSweep.Results.Count; i++)
            {
                var r = activeSweep.Results[i];
                var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (r.errorCount > 0) EditorUIStyles.TintLastRect(rect, EditorUIStyles.Rose, 0.14f);

                EditorGUILayout.BeginHorizontal();
                if (!r.loaded) EditorUIStyles.Badge("SKIP", EditorUIStyles.Slate, 54);
                else if (!sweepErrorsOnly) EditorUIStyles.Badge(r.grade, EditorUIStyles.ForGrade(r.grade), 40);
                else EditorUIStyles.Badge(r.errorCount > 0 ? "ERR" : "OK", r.errorCount > 0 ? EditorUIStyles.Rose : EditorUIStyles.Mint, 54);

                string stat = (!sweepErrorsOnly && r.loaded) ? $"{r.avgFps:F1} fps (p99 {r.p99FrameTimeMs:F1}ms)" : "";
                EditorGUILayout.LabelField($"{r.sceneName}   {stat}");
                if (r.errorCount > 0)
                {
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = EditorUIStyles.Rose;
                    if (GUILayout.Button($"⚠ {r.errorCount}", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        if (!sweepExpanded.Add(i)) sweepExpanded.Remove(i);
                    }
                    GUI.backgroundColor = prev;
                }
                EditorGUILayout.EndHorizontal();

                if (r.errorCount > 0 && sweepExpanded.Contains(i) && r.errorMessages != null)
                {
                    foreach (var msg in r.errorMessages)
                        EditorGUILayout.LabelField(msg, EditorUIStyles.Wrap);
                }
                EditorGUILayout.EndVertical();
            }

            if (activeSweep.IsComplete)
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy Summary")) GUIUtility.systemCopyBuffer = activeSweep.CombinedSummary;
                if (GUILayout.Button("View in History")) { RefreshHistory(); activeTab = Tab.History; }
                EditorGUILayout.EndHorizontal();
            }
        }

        void EnsureSweepScenesPopulated()
        {
            if (sweepScenes == null) PopulateSweepScenes();
        }

        void PopulateSweepScenes()
        {
            sweepScenes = new List<(string, bool)>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                sweepScenes.Add((Path.GetFileNameWithoutExtension(scene.path), true));
            }
        }

        void SetAllSweep(bool include)
        {
            for (int i = 0; i < sweepScenes.Count; i++)
                sweepScenes[i] = (sweepScenes[i].name, include);
        }

        void StartSweep()
        {
            var selected = sweepScenes.Where(s => s.include).Select(s => s.name).ToList();
            if (selected.Count == 0) { Debug.LogWarning("[Benchmark] No scenes selected for the sweep."); return; }

            if (sweepCaptureErrors || sweepErrorsOnly) SpikeAnalyzer.SetProfilerEnabled(true);
            sweepExpanded.Clear();
            activeSweep = BenchmarkSweepRunner.StartSweep(selected, config, null, gameData, sweepTag, sweepCaptureErrors, sweepErrorsOnly);
        }

        // ════════════════════════════════════════════════
        // ── History Tab ─────────────────────────────────
        // ════════════════════════════════════════════════

        void DrawHistoryTab()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            EditorUIStyles.SectionHeader("Benchmark History", EditorUIStyles.Sky);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(70))) RefreshHistory();
            if (GUILayout.Button("Rebuild Index", GUILayout.Width(110)))
            {
                BenchmarkHistory.RebuildIndex(GetOutputFolder());
                RefreshHistory();
            }
            if (GUILayout.Button("Import External Run", GUILayout.Width(150)))
                ImportExternalRun();
            EditorGUILayout.EndHorizontal();

            if (historyEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No saved runs yet. Capture a run in the Collect tab and press Save, " +
                    "or Import a dev-build run pulled off a device.", MessageType.Info);
                return;
            }

            historyScroll = EditorGUILayout.BeginScrollView(historyScroll);
            for (int i = 0; i < historyEntries.Count; i++)
                DrawHistoryEntry(historyEntries[i], i);
            EditorGUILayout.EndScrollView();
        }

        void DrawHistoryEntry(BenchmarkHistory.IndexEntry e, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorUIStyles.Badge(e.score.ToString(), EditorUIStyles.ForScore(e.score), 40);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(e.label) ? "(untitled)" : e.label, EditorStyles.boldLabel, GUILayout.Width(140));
            if (!string.IsNullOrEmpty(e.tag)) EditorUIStyles.Badge(e.tag, EditorUIStyles.Lavender, 80);
            if (!string.IsNullOrEmpty(e.origin))
            {
                var oc = e.origin == "DevBuild" ? EditorUIStyles.Peach : e.origin == "Legacy" ? EditorUIStyles.Slate : EditorUIStyles.Sky;
                EditorUIStyles.Badge(e.origin, oc, 70);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{e.gitBranch}/{e.gitCommitHash}", EditorStyles.miniLabel, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();

            string date = e.timestamp?.Length > 19 ? e.timestamp[..19].Replace("T", " ") : e.timestamp ?? "?";
            EditorGUILayout.LabelField(
                $"{date}  |  {e.sceneName}  |  {e.totalFrames} frames  |  FPS {e.avgFps:F1} (p1 {e.p1Fps:F1})  |  {e.avgFrameTimeMs:F1}ms (p99 {e.p99FrameTimeMs:F1})",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Baseline", GUILayout.Width(70))) { baselineIndex = index; TryCompare(); activeTab = Tab.Compare; }
            if (GUILayout.Button("Current", GUILayout.Width(64))) { currentIndex = index; TryCompare(); activeTab = Tab.Compare; }

            if (tagEditId == e.reportId)
            {
                tagEditValue = EditorGUILayout.TextField(tagEditValue, GUILayout.Width(80));
                if (GUILayout.Button("Save", GUILayout.Width(44)))
                {
                    BenchmarkHistory.TagReport(e.reportId, tagEditValue, GetOutputFolder());
                    tagEditId = null;
                    RefreshHistory();
                }
                if (GUILayout.Button("X", GUILayout.Width(22))) tagEditId = null;
            }
            else if (GUILayout.Button("Tag", GUILayout.Width(40)))
            {
                tagEditId = e.reportId;
                tagEditValue = e.tag ?? "";
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("JSON", GUILayout.Width(46)) && File.Exists(e.filePath))
                EditorUtility.RevealInFinder(e.filePath);
            if (GUILayout.Button("Del", GUILayout.Width(36)))
            {
                if (EditorUtility.DisplayDialog("Delete Snapshot", $"Delete \"{e.label}\" ({e.timestamp})?", "Delete", "Cancel"))
                {
                    BenchmarkHistory.RemoveEntry(e.reportId, GetOutputFolder());
                    RefreshHistory();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ════════════════════════════════════════════════
        // ── Compare Tab ─────────────────────────────────
        // ════════════════════════════════════════════════

        void DrawCompareTab()
        {
            EditorGUILayout.Space(6);
            EditorUIStyles.SectionHeader("Compare Two Runs (before / after)", EditorUIStyles.Sky);

            if (historyEntries.Count < 2)
            {
                EditorGUILayout.HelpBox(
                    "Save at least 2 runs to compare. Typical flow: capture → tag 'baseline' → make a change → " +
                    "capture again → compare to see what improved or regressed.", MessageType.Info);
                return;
            }

            string[] names = historyEntries.Select(e =>
            {
                string tag = string.IsNullOrEmpty(e.tag) ? "" : $" [{e.tag}]";
                string date = e.timestamp?.Length > 10 ? e.timestamp[..10] : "?";
                return $"{e.label}{tag} ({e.gitBranch} {date})";
            }).ToArray();

            int newBaseline = EditorGUILayout.Popup("Baseline (before)", baselineIndex, names);
            int newCurrent = EditorGUILayout.Popup("Current (after)", currentIndex, names);
            if (newBaseline != baselineIndex || newCurrent != currentIndex)
            {
                baselineIndex = newBaseline;
                currentIndex = newCurrent;
                TryCompare();
            }

            if (GUILayout.Button("Compare", GUILayout.Height(24))) TryCompare();

            if (comparisonResult == null) { EditorGUILayout.HelpBox("Pick a baseline and a current run.", MessageType.Info); return; }

            // Cross-source guard - absolute numbers aren't comparable across Editor vs DevBuild or
            // different platforms/devices; only same-source deltas are meaningful.
            var bSrc = comparisonResult.baseline?.source;
            var cSrc = comparisonResult.current?.source;
            if (bSrc != null && cSrc != null && (bSrc.origin != cSrc.origin || bSrc.platform != cSrc.platform))
            {
                EditorGUILayout.HelpBox(
                    $"Cross-source comparison: {bSrc.origin}/{bSrc.platform} vs {cSrc.origin}/{cSrc.platform}. " +
                    "Absolute numbers aren't comparable across sources - only same-source before/after deltas are meaningful.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            EditorUIStyles.Badge($"{comparisonResult.improvements} better", EditorUIStyles.Mint, 90);
            EditorUIStyles.Badge($"{comparisonResult.neutral} same", EditorUIStyles.Slate, 80);
            EditorUIStyles.Badge($"{comparisonResult.regressions} worse", EditorUIStyles.Rose, 90);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy Text", GUILayout.Width(90))) GUIUtility.systemCopyBuffer = comparisonText;
            EditorGUILayout.EndHorizontal();

            compareScroll = EditorGUILayout.BeginScrollView(compareScroll);
            foreach (var d in comparisonResult.deltas)
            {
                var rect = EditorGUILayout.BeginHorizontal();
                Color row = d.verdict switch
                {
                    MetricDelta.Verdict.Improved => new Color(EditorUIStyles.Mint.r, EditorUIStyles.Mint.g, EditorUIStyles.Mint.b, 0.14f),
                    MetricDelta.Verdict.Regressed => new Color(EditorUIStyles.Rose.r, EditorUIStyles.Rose.g, EditorUIStyles.Rose.b, 0.14f),
                    _ => Color.clear
                };
                if (row != Color.clear) EditorGUI.DrawRect(rect, row);

                EditorGUILayout.LabelField(d.metricName, EditorStyles.miniLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField(d.baselineValue.ToString("F2"), EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField(d.currentValue.ToString("F2"), EditorStyles.miniLabel, GUILayout.Width(80));
                string sign = d.absoluteDelta >= 0 ? "+" : "";
                EditorGUILayout.LabelField($"{sign}{d.percentDelta:F1}%", EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField(d.verdict == MetricDelta.Verdict.Improved ? "BETTER" : d.verdict == MetricDelta.Verdict.Regressed ? "WORSE" : "~", EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        void TryCompare()
        {
            if (baselineIndex < 0 || currentIndex < 0) return;
            if (baselineIndex >= historyEntries.Count || currentIndex >= historyEntries.Count) return;
            if (baselineIndex == currentIndex) return;

            var b = BenchmarkHistory.LoadReport(historyEntries[baselineIndex]);
            var c = BenchmarkHistory.LoadReport(historyEntries[currentIndex]);
            if (b == null || c == null) { comparisonResult = null; comparisonText = null; return; }

            comparisonResult = BenchmarkComparer.Compare(b, c);
            comparisonText = BenchmarkComparer.FormatAsText(comparisonResult);
        }

        // ── Data access ─────────────────────────────────

        string GetOutputFolder() =>
            config != null && !string.IsNullOrEmpty(config.OutputFolder) ? config.OutputFolder : "Benchmarks";

        void RefreshHistory()
        {
            historyEntries = BenchmarkHistory.GetAll(GetOutputFolder());
            if (baselineIndex >= historyEntries.Count) baselineIndex = -1;
            if (currentIndex >= historyEntries.Count) currentIndex = -1;
        }

        // Imports a BenchmarkReport JSON pulled off a device (dev-build run) into History.
        void ImportExternalRun()
        {
            string path = EditorUtility.OpenFilePanel("Import Benchmark Run", Application.persistentDataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            var report = BenchmarkReport.LoadFromFile(path);
            if (report == null || report.statistics == null)
            {
                EditorUtility.DisplayDialog("Import failed", "Could not read a valid benchmark report from that file.", "OK");
                return;
            }
            if (report.schemaVersion > BenchmarkReport.CurrentSchemaVersion)
                Debug.LogWarning($"[Benchmark] Imported run is schema v{report.schemaVersion}, newer than this tool " +
                                 $"(v{BenchmarkReport.CurrentSchemaVersion}); some fields may not display.");

            // Copy into the output folder so it persists alongside the index.
            try
            {
                string destDir = Path.Combine(Application.persistentDataPath, GetOutputFolder());
                Directory.CreateDirectory(destDir);
                string dest = Path.Combine(destDir, Path.GetFileName(path));
                if (Path.GetFullPath(dest) != Path.GetFullPath(path)) File.Copy(path, dest, true);
                BenchmarkHistory.AddToHistory(report, dest, GetOutputFolder());
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Benchmark] Import failed: {e.Message}");
            }
            RefreshHistory();
        }
    }
}

#endif
