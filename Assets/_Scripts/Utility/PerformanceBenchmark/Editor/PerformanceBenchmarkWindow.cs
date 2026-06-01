#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Utility; // GameDataSO
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    public class PerformanceBenchmarkWindow : EditorWindow
    {
        enum Tab { Collect, Sweep, History, Compare }
        [SerializeField] Tab activeTab = Tab.Collect;

        // ── Shared assets (serialized so they survive the play-mode domain reload) ──
        [SerializeField] BenchmarkConfigSO config;
        [SerializeField] BenchmarkHintRulesSO hintRules;
        [SerializeField] GameDataSO gameData;

        // Default config shipped in the repo; auto-loaded when nothing is assigned.
        const string DefaultConfigPath = "Assets/_SO_Assets/Benchmark/BenchmarkConfig.asset";

        // ── Collect tab ─────────────────────────────────
        [SerializeField] int sceneIndex;
        [SerializeField] bool bootFromBootstrap;
        [SerializeField] string collectTag = "";
        string[] sceneDisplay = System.Array.Empty<string>();
        string[] scenePaths = System.Array.Empty<string>();
        PerformanceBenchmarkRunner collectRunner;
        Vector2 collectScroll;
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
        public static void Open()
        {
            var window = GetWindow<PerformanceBenchmarkWindow>("Performance Benchmark");
            window.minSize = new Vector2(620, 520);
            window.Show();
        }

        void OnEnable()
        {
            RefreshSceneList();
            RefreshHistory();
            // Auto-load the repo's default config so the tool works out of the box.
            if (config == null)
                config = AssetDatabase.LoadAssetAtPath<BenchmarkConfigSO>(DefaultConfigPath);
            // Restore the last Collect run after a domain reload (e.g. leaving Play Mode).
            if (collectReport == null && File.Exists(CollectCachePath))
                collectReport = BenchmarkReport.LoadFromFile(CollectCachePath);
        }

        void Update()
        {
            // Re-acquire the live runner after the play-mode domain reload wiped our reference.
            if (Application.isPlaying && collectRunner == null)
                collectRunner = FindFirstObjectByType<PerformanceBenchmarkRunner>();

            if ((collectRunner != null && collectRunner.IsRunning) ||
                (activeSweep != null && activeSweep.IsSweeping))
                Repaint();
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
            }
        }

        void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawTabButton(Tab.Collect, "Runtime Capture");
            DrawTabButton(Tab.Sweep, "Sweep");
            DrawTabButton(Tab.History, $"History ({historyEntries.Count})");
            DrawTabButton(Tab.Compare, "Compare");
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

            EditorUIStyles.SectionHeader("Setup", EditorUIStyles.Sky);

            config = (BenchmarkConfigSO)EditorGUILayout.ObjectField("Config", config, typeof(BenchmarkConfigSO), false);
            if (config == null)
            {
                EditorGUILayout.HelpBox("No benchmark config assigned.", MessageType.Info);
                if (GUILayout.Button("Create Default Config"))
                    CreateDefaultConfig();
                EditorGUILayout.EndScrollView();
                return;
            }

            hintRules = (BenchmarkHintRulesSO)EditorGUILayout.ObjectField(
                new GUIContent("Hint Rules (optional)", "Customizable rule set for actionable hints. Defaults used when empty."),
                hintRules, typeof(BenchmarkHintRulesSO), false);
            gameData = (GameDataSO)EditorGUILayout.ObjectField(
                new GUIContent("Game Data (optional)", "When assigned, vessel/player counts are recorded."),
                gameData, typeof(GameDataSO), false);

            // Scene picker
            if (sceneDisplay.Length == 0)
            {
                EditorGUILayout.HelpBox("No scenes in Build Settings. Add scenes via File > Build Settings.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                sceneIndex = EditorGUILayout.Popup(new GUIContent("Scene to capture"), Mathf.Clamp(sceneIndex, 0, sceneDisplay.Length - 1), sceneDisplay);
                if (GUILayout.Button("↻", GUILayout.Width(26))) RefreshSceneList();
                EditorGUILayout.EndHorizontal();
                bootFromBootstrap = EditorGUILayout.ToggleLeft(
                    new GUIContent("Boot from Bootstrap first",
                        "On: the game boots through Bootstrap so networked scenes initialize (capture starts at boot). " +
                        "Off: the chosen scene is loaded directly — best for self-contained scenes."),
                    bootFromBootstrap);
            }

            DrawConfigSummary();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Spawn Live HUD Overlay (toggle in Game view with F9)"))
                    SpawnLiveHud();
            }

            EditorGUILayout.Space(8);
            EditorUIStyles.SectionHeader("Capture", EditorUIStyles.Mint);

            bool running = collectRunner != null && collectRunner.IsRunning;
            if (running)
            {
                DrawRecordingStatus();
            }
            else if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Free-play recording happens in Play Mode: press Enter Play Mode, then ● Start Recording, play, " +
                    "and ■ Stop & Analyze. Or run a fixed-length capture of the chosen scene (the old Collect behaviour).",
                    MessageType.Info);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = EditorUIStyles.Mint;
                if (GUILayout.Button("▶  Enter Play Mode", GUILayout.Height(30)))
                    EditorApplication.isPlaying = true;
                GUI.backgroundColor = prev;
                if (GUILayout.Button($"Run Fixed {config.SampleDuration:F0}s Capture (enter Play)"))
                    StartCaptureViaPlay();
            }
            else
            {
                EditorGUILayout.HelpBox("In Play Mode — record free play (start/stop yourself), or run a fixed-length capture of the current scene.", MessageType.None);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = EditorUIStyles.Mint;
                if (GUILayout.Button("●  Start Recording (free play)", GUILayout.Height(30)))
                    StartFreeFormInCurrentPlay();
                GUI.backgroundColor = prev;
                if (GUILayout.Button($"Run Fixed {config.SampleDuration:F0}s Capture of Current Scene"))
                    StartCaptureInCurrentPlay();
            }

            // When the live runner finishes a run, adopt it and cache to disk so it survives
            // leaving Play Mode.
            if (collectRunner != null && collectRunner.LastReport != null &&
                collectRunner.LastReport.reportId != cachedReportId)
            {
                collectReport = collectRunner.LastReport;
                cachedReportId = collectReport.reportId;
                collectSavedPath = collectRunner.LastReportPath ?? "";
                CacheCollectReport(collectReport);
            }

            // Results (persisted — shows even after Play Mode is stopped)
            var report = collectReport;
            if (report?.statistics != null && report.statistics.totalFrames > 0)
            {
                DrawCopyForClaude(report);
                DrawResults(report);
            }
            else if (report?.statistics != null)
                EditorGUILayout.HelpBox("No data captured (the run was too short or interrupted).", MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        // ── Runtime Capture: live recording + Copy-for-Claude ───────────────

        void DrawRecordingStatus()
        {
            int spikeCount = collectRunner.Spikes?.Count ?? 0;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (collectRunner.IsFreeForm)
            {
                EditorGUILayout.LabelField(
                    $"● Recording (free play) — {collectRunner.FramesCaptured} frames · {spikeCount} spikes",
                    EditorStyles.boldLabel);
            }
            else
            {
                string phase = collectRunner.IsWarmingUp ? "Warming up" : "Sampling";
                var rect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(rect, collectRunner.Progress,
                    $"{phase}…  {collectRunner.Progress * 100:F0}%   ({collectRunner.FramesCaptured} frames · {spikeCount} spikes)");
            }
            EditorGUILayout.EndVertical();

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = EditorUIStyles.Rose;
            if (GUILayout.Button(collectRunner.IsFreeForm ? "■  Stop & Analyze" : "Stop Early", GUILayout.Height(26)))
                collectRunner.StopBenchmark();
            GUI.backgroundColor = prev;

            DrawLiveSpikes(collectRunner.Spikes);
        }

        void DrawLiveSpikes(IReadOnlyList<SpikeEntry> liveSpikes)
        {
            int count = liveSpikes?.Count ?? 0;
            if (count == 0)
            {
                EditorGUILayout.HelpBox("No spikes yet — keep playing. Any frame over the spike threshold gets its script breakdown captured here.", MessageType.None);
                return;
            }

            EditorGUILayout.Space(4);
            EditorUIStyles.SectionHeader($"Live Spikes ({count})", EditorUIStyles.Rose);

            if (GUILayout.Button("📋  Copy spikes for Claude"))
            {
                EditorGUIUtility.systemCopyBuffer = BuildClaudeSpikesText(
                    collectReport?.sceneName ?? "(recording)", collectRunner != null ? collectRunner.FramesCaptured : 0, liveSpikes);
                ShowNotification(new GUIContent("Spikes copied — paste to Claude"));
            }

            // Newest first, worst-first within the most recent few.
            int show = Mathf.Min(8, count);
            for (int i = count - 1; i >= 0 && i >= count - show; i--)
                DrawSpikeRow(liveSpikes[i]);
        }

        void DrawSpikeRow(SpikeEntry spike)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            string cpuGpu = (spike.cpuFrameTimeMs > 0.001f || spike.gpuFrameTimeMs > 0.001f)
                ? $"  (CPU {spike.cpuFrameTimeMs:F1} / GPU {spike.gpuFrameTimeMs:F1})" : "";
            EditorGUILayout.LabelField($"Frame {spike.frameIndex}: {spike.frameTimeMs:F1} ms{cpuGpu}", EditorStyles.boldLabel);
            if (spike.topMarkers != null && spike.topMarkers.Count > 0)
            {
                foreach (var m in spike.topMarkers)
                    EditorGUILayout.LabelField($"   {(m.isScript ? "▸" : "·")} {m.name} — {m.ms:F2} ms", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField("   (no marker data — is the Profiler enabled?)", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        void DrawCopyForClaude(BenchmarkReport report)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = EditorUIStyles.Lavender;
            if (GUILayout.Button("📋  Copy report for Claude (stats + script spikes)", GUILayout.Height(24)))
            {
                EditorGUIUtility.systemCopyBuffer = BuildClaudeReportText(report);
                ShowNotification(new GUIContent("Report copied — paste to Claude"));
            }
            GUI.backgroundColor = prev;
        }

        static string BuildClaudeSpikesText(string scene, int frames, IReadOnlyList<SpikeEntry> spikes)
        {
            var sb = new System.Text.StringBuilder(1024);
            sb.AppendLine($"Cosmic Shore spike capture — {scene}  ({frames} frames)");
            sb.AppendLine("Top spikes (self-time; editor noise filtered, ▸ = script):");
            foreach (var s in spikes.OrderByDescending(x => x.frameTimeMs).Take(12))
                AppendSpike(sb, s);
            return sb.ToString();
        }

        static string BuildClaudeReportText(BenchmarkReport report)
        {
            var s = report.statistics;
            var sb = new System.Text.StringBuilder(2048);
            sb.AppendLine($"Cosmic Shore perf capture — {report.sceneName}");
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
                    sb.AppendLine($"  [{h.severity}] {h.title} — {h.finding}");
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
                    sb.AppendLine($"      {(m.isScript ? "▸" : "·")} {m.name}  {m.ms:F2} ms");
        }

        void StartFreeFormInCurrentPlay()
        {
            collectRunner = FindFirstObjectByType<PerformanceBenchmarkRunner>();
            if (collectRunner == null)
                collectRunner = new GameObject("[PerformanceBenchmarkRunner]").AddComponent<PerformanceBenchmarkRunner>();

            SpikeAnalyzer.SetProfilerEnabled(true);
            ResetCollectDisplay();
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

        void DrawConfigSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Warmup {config.WarmupDuration:F0}s · Sample {config.SampleDuration:F0}s · " +
                $"Capturing: {(config.CaptureRenderingStats ? "render " : "")}{(config.CaptureMemoryStats ? "memory " : "")}" +
                $"{(config.CapturePhysicsStats ? "physics " : "")}{(config.CaptureGameLoadStats ? "load" : "")}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        void DrawResults(BenchmarkReport report)
        {
            var s = report.statistics;
            var analysis = report.analysis;

            EditorGUILayout.Space(8);
            EditorUIStyles.SectionHeader("Results", EditorUIStyles.Lavender);

            int score = analysis?.score ?? BenchmarkAnalysis.ComputeScore(s);
            string grade = BenchmarkGrade.Evaluate(s, out string explanation);
            EditorUIStyles.ScoreBar(score, $"Score {score}/100   ·   Grade {grade} — {explanation}");

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            EditorUIStyles.Badge($"Grade {grade}", EditorUIStyles.ForGrade(grade), 80);
            if (analysis != null) EditorUIStyles.Badge(analysis.boundVerdict, EditorUIStyles.Slate, 110);
            if (analysis != null && analysis.isBlocked) EditorUIStyles.Badge("BLOCKERS", EditorUIStyles.Rose, 90);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Collector self-check — should read ~0 B/frame in steady state.
            EditorGUILayout.LabelField(
                $"Collector overhead: {s.collectorAllocBytesPerFrame:F1} B/frame" +
                (s.collectorAllocBytesPerFrame > 64f ? "  ⚠ above ~0 — investigate" : "  ✓"),
                EditorStyles.miniLabel);

            // Core stats
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorUIStyles.StatRow("Avg FPS", $"{s.avgFps:F1}", "Higher is better. Target 60.");
            EditorUIStyles.StatRow("Worst 1% FPS", $"{s.p1Fps:F1}", "FPS during the worst spikes.");
            EditorUIStyles.StatRow("Avg Frame Time", $"{s.avgFrameTimeMs:F2} ms", "16.7ms = 60fps, 33.3ms = 30fps.");
            EditorUIStyles.StatRow("P99 Frame Time", $"{s.p99FrameTimeMs:F2} ms", "Worst-1% frame time.");
            EditorUIStyles.StatRow("Stability (StdDev)", $"{s.stdDevFrameTimeMs:F2} ms", "Lower = smoother. >6ms = hitching.");
            EditorGUILayout.EndVertical();

            // CPU / GPU
            if (s.avgCpuFrameTimeMs > 0.001f || s.avgGpuFrameTimeMs > 0.001f)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorUIStyles.StatRow("CPU Frame Time", $"{s.avgCpuFrameTimeMs:F2} ms  (max {s.maxCpuFrameTimeMs:F1})", "Main + render thread CPU time.");
                EditorUIStyles.StatRow("GPU Frame Time", $"{s.avgGpuFrameTimeMs:F2} ms  (max {s.maxGpuFrameTimeMs:F1})", "GPU time (0 if platform can't report it).");
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("CPU/GPU split unavailable — enable 'Frame Timing Stats' in Player Settings (Start does this for you next run).", MessageType.None);
            }

            // Rendering + memory
            if (s.avgDrawCalls > 0 || s.peakAllocatedMemory > 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (s.avgDrawCalls > 0)
                {
                    EditorUIStyles.StatRow("Draw Calls", $"{s.avgDrawCalls:F0}", "Lower is better.");
                    EditorUIStyles.StatRow("Batches / Tris", $"{s.avgBatches:F0} / {s.avgTriangles:F0}");
                }
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
            EditorUIStyles.SectionHeader("Netcode", EditorUIStyles.Sky);
            if (hasNetcode)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorUIStyles.StatRow("Netcode Share", $"{s.netcodeSharePercent:F0}%  ({s.avgNetcodeTimeMs:F2} ms/frame, max {s.maxNetcodeTimeMs:F1})", "CSM.Net.* marker time as a share of frame time.");
                EditorUIStyles.StatRow("RPCs / frame", $"{s.avgRpcsSent:F1}");
                EditorUIStyles.StatRow("NetVars dirty / frame", $"{s.avgNetVarsDirty:F1}");
                if (s.totalNetBytesSent > 0)
                    EditorUIStyles.StatRow("Bytes sent (total)", $"{s.totalNetBytesSent / 1024f:F1} KB");
                if (report.networkTickRate > 0)
                    EditorUIStyles.StatRow("Network tick rate", $"{report.networkTickRate} Hz  (~{(s.avgFps > 0 ? s.avgFps / report.networkTickRate : 0):F1} render frames/tick)");
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.HelpBox("No netcode activity recorded — non-networked scene, or the NGO hot paths aren't instrumented with NetMarkers here.", MessageType.None);
            }

            DrawHints(analysis);
            DrawSpikes(report.spikes);
            DrawSaveTag(report);
        }

        void DrawHints(BenchmarkAnalysisResult analysis)
        {
            if (analysis == null || analysis.hints == null || analysis.hints.Count == 0)
            {
                EditorGUILayout.HelpBox("No issues flagged. 🎉", MessageType.None);
                return;
            }

            EditorGUILayout.Space(4);
            EditorUIStyles.SectionHeader($"Hints ({analysis.hints.Count})", EditorUIStyles.Peach);

            foreach (var h in analysis.hints.OrderByDescending(h => (int)h.severity))
            {
                var rect = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorUIStyles.TintLastRect(rect, EditorUIStyles.ForSeverity(h.severity), 0.14f);

                EditorGUILayout.BeginHorizontal();
                EditorUIStyles.Badge(h.severity.ToString(), EditorUIStyles.ForSeverity(h.severity), 70);
                EditorGUILayout.LabelField(h.title, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(h.finding))
                    EditorGUILayout.LabelField(h.finding, EditorUIStyles.Wrap);
                if (!string.IsNullOrEmpty(h.matchedMarker))
                    EditorGUILayout.LabelField($"Marker: {h.matchedMarker}", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(h.fixAdvice))
                    EditorGUILayout.LabelField($"Fix: {h.fixAdvice}", EditorUIStyles.Wrap);

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        void DrawSpikes(List<SpikeEntry> spikes)
        {
            if (spikes == null || spikes.Count == 0) return;

            EditorGUILayout.Space(4);
            EditorUIStyles.SectionHeader($"Top Spikes ({spikes.Count})", EditorUIStyles.Rose);

            foreach (var spike in spikes.OrderByDescending(sp => sp.frameTimeMs).Take(6))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                string cpuGpu = (spike.cpuFrameTimeMs > 0.001f || spike.gpuFrameTimeMs > 0.001f)
                    ? $"  (CPU {spike.cpuFrameTimeMs:F1} / GPU {spike.gpuFrameTimeMs:F1})" : "";
                EditorGUILayout.LabelField($"Frame {spike.frameIndex}: {spike.frameTimeMs:F1} ms{cpuGpu}", EditorStyles.boldLabel);
                if (spike.topMarkers != null && spike.topMarkers.Count > 0)
                {
                    foreach (var m in spike.topMarkers)
                        EditorGUILayout.LabelField($"   • {m.name} — {m.ms:F2} ms", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("   (no marker data — was the Profiler enabled?)", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }
        }

        void DrawSaveTag(BenchmarkReport report)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            // Window-driven save so it works during AND after Play Mode (no runner needed).
            bool saved = !string.IsNullOrEmpty(collectSavedPath);
            using (new EditorGUI.DisabledScope(saved))
            {
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = EditorUIStyles.Mint;
                if (GUILayout.Button(saved ? "Saved ✓" : "Save to History", GUILayout.Height(24)))
                {
                    string path = report.SaveToFile(GetOutputFolder());
                    BenchmarkHistory.AddToHistory(report, path, GetOutputFolder());
                    collectSavedPath = path;
                    RefreshHistory();
                }
                GUI.backgroundColor = prev;
            }

            using (new EditorGUI.DisabledScope(!saved))
            {
                collectTag = EditorGUILayout.TextField(collectTag, GUILayout.Width(120));
                if (GUILayout.Button("Tag", GUILayout.Width(50)))
                {
                    BenchmarkHistory.TagReport(report.reportId, collectTag, GetOutputFolder());
                    RefreshHistory();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(saved
                ? "Saved to History (and kept on disk)."
                : "This run is cached — it stays here after you stop Play. Save to add it to History/Compare.",
                EditorStyles.miniLabel);
        }

        void StartCaptureViaPlay()
        {
            string scenePath = (!bootFromBootstrap && scenePaths.Length > 0)
                ? scenePaths[Mathf.Clamp(sceneIndex, 0, scenePaths.Length - 1)]
                : null;
            collectRunner = null;
            ResetCollectDisplay();
            BenchmarkAutoStart.RequestCaptureOnPlay(config, hintRules, gameData, scenePath, bootFromBootstrap);
        }

        void StartCaptureInCurrentPlay()
        {
            collectRunner = FindFirstObjectByType<PerformanceBenchmarkRunner>();
            if (collectRunner == null)
                collectRunner = new GameObject("[PerformanceBenchmarkRunner]").AddComponent<PerformanceBenchmarkRunner>();

            SpikeAnalyzer.SetProfilerEnabled(true);
            ResetCollectDisplay();
            collectRunner.Configure(config, null, gameData, hintRules);
            collectRunner.AutoSave = false;
            collectRunner.StartBenchmark();
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
                Debug.Log("[Benchmark] Live HUD overlay already present — press F9 in the Game view to toggle.");
                return;
            }
            new GameObject("[BenchmarkHUDOverlay]").AddComponent<BenchmarkHUDOverlay>();
            Debug.Log("[Benchmark] Live HUD overlay spawned — press F9 in the Game view to show/hide it.");
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
            EditorGUILayout.Space(6);
            EditorUIStyles.SectionHeader("Multi-Scene Sweep", EditorUIStyles.Sky);

            config = (BenchmarkConfigSO)EditorGUILayout.ObjectField("Config", config, typeof(BenchmarkConfigSO), false);
            gameData = (GameDataSO)EditorGUILayout.ObjectField("Game Data (optional)", gameData, typeof(GameDataSO), false);
            sweepTag = EditorGUILayout.TextField("Sweep Tag", sweepTag);

            EditorGUILayout.BeginHorizontal();
            sweepCaptureErrors = EditorGUILayout.ToggleLeft("Capture errors", sweepCaptureErrors, GUILayout.Width(140));
            sweepErrorsOnly = EditorGUILayout.ToggleLeft(new GUIContent("Errors only (fast scan)", "Skip the full benchmark; just load each scene briefly and catch errors."), sweepErrorsOnly);
            EditorGUILayout.EndHorizontal();

            if (config == null && !sweepErrorsOnly)
            {
                EditorGUILayout.HelpBox("Assign a Benchmark Config (or use 'Errors only').", MessageType.Info);
            }

            EnsureSweepScenesPopulated();

            EditorGUILayout.Space(4);
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

            EditorGUILayout.Space(6);
            bool sweepRunning = activeSweep != null && activeSweep.IsSweeping;
            if (sweepRunning)
            {
                float p = activeSweep.TotalScenes > 0 ? (float)activeSweep.CurrentIndex / activeSweep.TotalScenes : 0f;
                var rect = EditorGUILayout.GetControlRect(false, 22);
                EditorGUI.ProgressBar(rect, p, $"Sweeping {activeSweep.CurrentIndex + 1}/{activeSweep.TotalScenes}: {activeSweep.CurrentScene}");
            }
            else
            {
                if (!Application.isPlaying)
                    EditorGUILayout.HelpBox("Enter Play Mode (ideally from Bootstrap) to run a sweep.", MessageType.Warning);
                using (new EditorGUI.DisabledScope(!Application.isPlaying || (config == null && !sweepErrorsOnly)))
                {
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = EditorUIStyles.Mint;
                    if (GUILayout.Button(sweepErrorsOnly ? "Run Error Scan" : "Start Sweep", GUILayout.Height(28)))
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

            // Cross-source guard — absolute numbers aren't comparable across Editor vs DevBuild or
            // different platforms/devices; only same-source deltas are meaningful.
            var bSrc = comparisonResult.baseline?.source;
            var cSrc = comparisonResult.current?.source;
            if (bSrc != null && cSrc != null && (bSrc.origin != cSrc.origin || bSrc.platform != cSrc.platform))
            {
                EditorGUILayout.HelpBox(
                    $"Cross-source comparison: {bSrc.origin}/{bSrc.platform} vs {cSrc.origin}/{cSrc.platform}. " +
                    "Absolute numbers aren't comparable across sources — only same-source before/after deltas are meaningful.",
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

        void RefreshSceneList()
        {
            var scenes = EditorBuildSettings.scenes;
            sceneDisplay = scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
            scenePaths = scenes.Select(s => s.path).ToArray();
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
