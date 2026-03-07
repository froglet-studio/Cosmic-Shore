#if UNITY_EDITOR

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game.Arcade;
using CosmicShore.Models.Enums;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.Tools.Benchmarking
{
    public class BenchmarkWindow : EditorWindow
    {
        // ── Config ──────────────────────────────────────────────────────────
        string _label = "Benchmark";
        float _warmupSeconds = 2f;
        float _durationSeconds = 10f;

        // ── Deterministic config ─────────────────────────────────────────
        bool _deterministicMode = true;
        int _deterministicSeed = 42;
        int _deterministicWarmupFrames = 120;
        float _deterministicFixedDt = 0.02f;

        // ── State ───────────────────────────────────────────────────────────
        PerformanceSampler _activeSampler;
        DeterministicBenchmarkController _deterministicController;
        BenchmarkReport _lastReport;
        BenchmarkReport _baselineReport;
        Vector2 _scrollPos;
        int _selectedTab;

        // ── Report browser ──────────────────────────────────────────────────
        string[] _savedPaths = Array.Empty<string>();
        string[] _savedNames = Array.Empty<string>();
        int _selectedBaselineIdx = -1;
        int _selectedCurrentIdx = -1;

        // ── Comparison cache ────────────────────────────────────────────────
        List<ComparisonLine> _comparisonLines;
        BenchmarkReport _comparedBaseline;
        BenchmarkReport _comparedCurrent;

        // ── Session config (persists across play mode via EditorPrefs) ──────
        [NonSerialized] BenchmarkSessionConfig _session;
        [NonSerialized] bool _sessionCallbackRegistered;
        [NonSerialized] bool _waitingForGameScene;
        [NonSerialized] bool _waitingForCountdown;
        [NonSerialized] double _countdownWaitEndTime;

        // Countdown is 4 sprites × 1s each; add 1s buffer for scene init before pressing Go
        const float CountdownDurationSeconds = 5f;

        // ── Session results (loaded after session completes) ────────────────
        [NonSerialized] List<BenchmarkReport> _sessionReports;
        [NonSerialized] BenchmarkSessionSummary _sessionSummary;

        // ── Arcade game list (loaded from SO at editor time) ─────────────────
        [NonSerialized] SO_GameList _arcadeGameList;
        [NonSerialized] SO_ArcadeGame[] _selectableGames;
        [NonSerialized] string[] _gameDisplayNames;

        static readonly VesselClassType[] SelectableVessels =
        {
            VesselClassType.Squirrel,
            VesselClassType.Sparrow,
            VesselClassType.Manta,
            VesselClassType.Dolphin,
            VesselClassType.Rhino,
            VesselClassType.Urchin,
            VesselClassType.Grizzly,
            VesselClassType.Serpent,
            VesselClassType.Termite,
            VesselClassType.Falcon,
            VesselClassType.Shrike,
        };
        static readonly string[] VesselNames = SelectableVessels.Select(v => v.ToString()).ToArray();

        // ── Palette (consistent with LogControlWindow) ──────────────────────
        static readonly Color BannerBg       = new(0.22f, 0.20f, 0.30f, 1f);
        static readonly Color AccentColor    = new(0.55f, 0.82f, 0.65f, 1f);
        static readonly Color SectionHeader  = new(0.20f, 0.19f, 0.26f, 1f);
        static readonly Color TextMuted      = new(0.58f, 0.56f, 0.65f, 1f);
        static readonly Color FooterBg       = new(0.14f, 0.13f, 0.18f, 1f);
        static readonly Color TabInactive    = new(0.18f, 0.17f, 0.22f, 1f);
        static readonly Color GoodColor      = new(0.35f, 0.78f, 0.48f, 1f);
        static readonly Color BadColor       = new(0.88f, 0.38f, 0.35f, 1f);
        static readonly Color NeutralColor   = new(0.72f, 0.72f, 0.72f, 1f);
        static readonly Color SessionColor   = new(0.82f, 0.55f, 0.55f, 1f);

        // ── Styles ──────────────────────────────────────────────────────────
        [NonSerialized] GUIStyle _bannerStyle;
        [NonSerialized] GUIStyle _sectionStyle;
        [NonSerialized] GUIStyle _mutedLabel;
        [NonSerialized] GUIStyle _tabStyle;
        [NonSerialized] GUIStyle _valueStyle;
        [NonSerialized] GUIStyle _metricLabel;
        [NonSerialized] bool _stylesBuilt;

        static readonly string[] TabLabels = { "Run", "Session", "Results", "Compare", "History" };
        static readonly Color[] TabColors =
        {
            new(0.55f, 0.82f, 0.65f, 1f),
            new(0.82f, 0.55f, 0.55f, 1f),
            new(0.65f, 0.65f, 0.88f, 1f),
            new(0.88f, 0.72f, 0.55f, 1f),
            new(0.72f, 0.55f, 0.82f, 1f),
        };

        [MenuItem("FrogletTools/Benchmark", false, 10)]
        static void Open()
        {
            var w = GetWindow<BenchmarkWindow>("Benchmark");
            w.minSize = new Vector2(440, 500);
        }

        void OnEnable()
        {
            RefreshSavedReports();
            RefreshArcadeGameList();
            _session = BenchmarkSessionConfig.Load();
            RegisterSessionCallback();
        }

        void OnDisable()
        {
            UnregisterSessionCallback();
        }

        void OnInspectorUpdate()
        {
            if (_activeSampler != null || (_session != null && _session.IsRunning))
                Repaint();
        }

        void BuildStyles()
        {
            if (_stylesBuilt) return;

            _bannerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 6, 6)
            };
            _bannerStyle.normal.textColor = AccentColor;

            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                padding = new RectOffset(8, 0, 4, 4)
            };
            _sectionStyle.normal.textColor = AccentColor;

            _mutedLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 10 };
            _mutedLabel.normal.textColor = TextMuted;

            _tabStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _valueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight
            };

            _metricLabel = new GUIStyle(EditorStyles.label) { fontSize = 11 };

            _stylesBuilt = true;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MAIN GUI
        // ═════════════════════════════════════════════════════════════════════

        void OnGUI()
        {
            BuildStyles();

            // Banner
            var bannerRect = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bannerRect, BannerBg);
            GUI.Label(bannerRect, "Performance Benchmark", _bannerStyle);

            var lineRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, AccentColor * 0.6f);

            // Tabs
            DrawTabBar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Space(6);

            switch (_selectedTab)
            {
                case 0: DrawRunTab(); break;
                case 1: DrawSessionTab(); break;
                case 2: DrawResultsTab(); break;
                case 3: DrawCompareTab(); break;
                case 4: DrawHistoryTab(); break;
            }

            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();

            // Footer
            var footerRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(footerRect, FooterBg);
            GUI.Label(footerRect, "Froglet Inc. — Cosmic Shore — Benchmark Tool", _mutedLabel);
        }

        void DrawTabBar()
        {
            const float tabHeight = 28;
            const float gap = 2;
            const float pad = 4;

            var barRect = GUILayoutUtility.GetRect(0, tabHeight + pad * 2, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, new Color(0.16f, 0.15f, 0.20f, 1f));

            float w = (barRect.width - pad * 2 - gap * (TabLabels.Length - 1)) / TabLabels.Length;

            for (int i = 0; i < TabLabels.Length; i++)
            {
                var rect = new Rect(barRect.x + pad + i * (w + gap), barRect.y + pad, w, tabHeight);
                bool selected = i == _selectedTab;
                bool hover = rect.Contains(Event.current.mousePosition);

                Color bg = selected ? TabColors[i] : hover ? Color.Lerp(TabInactive, TabColors[i], 0.45f) : Color.Lerp(TabInactive, TabColors[i], 0.15f);
                EditorGUI.DrawRect(rect, bg);

                if (selected)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3, rect.width, 3), Color.white * 0.9f);

                _tabStyle.normal.textColor = selected ? Color.white : new Color(0.88f, 0.86f, 0.94f);

                // Flash the Session tab if a session is running
                string label = TabLabels[i];
                if (i == 1 && _session is { IsRunning: true })
                {
                    float pulse = Mathf.PingPong((float)EditorApplication.timeSinceStartup * 2f, 1f);
                    _tabStyle.normal.textColor = Color.Lerp(Color.white, SessionColor, pulse);
                }

                GUI.Label(rect, label, _tabStyle);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _selectedTab = i;
                    _scrollPos = Vector2.zero;
                    Event.current.Use();
                    Repaint();
                }
            }

            if (Event.current.type == EventType.MouseMove) Repaint();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: RUN (manual single benchmark)
        // ═════════════════════════════════════════════════════════════════════

        void DrawRunTab()
        {
            DrawSection("Configuration");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Label", GUILayout.Width(80));
            _label = EditorGUILayout.TextField(_label);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Duration (s)", GUILayout.Width(80));
            _durationSeconds = EditorGUILayout.FloatField(_durationSeconds, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            // Deterministic mode
            DrawSection("Deterministic Mode");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            _deterministicMode = EditorGUILayout.Toggle(_deterministicMode, GUILayout.Width(16));
            GUILayout.Label("Enable deterministic mode (removes randomness for repeatable results)");
            EditorGUILayout.EndHorizontal();

            if (_deterministicMode)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                GUILayout.Label("Seed", GUILayout.Width(100));
                _deterministicSeed = EditorGUILayout.IntField(_deterministicSeed, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                GUILayout.Label("Warmup Frames", GUILayout.Width(100));
                _deterministicWarmupFrames = EditorGUILayout.IntField(_deterministicWarmupFrames, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                GUILayout.Label("Fixed Delta Time", GUILayout.Width(100));
                _deterministicFixedDt = EditorGUILayout.FloatField(_deterministicFixedDt, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox(
                    "Deterministic mode seeds Random.InitState(), locks physics timestep, " +
                    "disables VSync, and uses frame-counted warmup instead of wall-clock time. " +
                    "This makes benchmark results comparable across runs.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label("Warmup (s)", GUILayout.Width(80));
                _warmupSeconds = EditorGUILayout.FloatField(_warmupSeconds, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            bool isPlaying = Application.isPlaying;
            bool isSampling = _activeSampler != null &&
                (_activeSampler.IsSampling || _activeSampler.IsWarming || _activeSampler.IsWaitingForDeterministic);

            if (!isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to run a benchmark.", MessageType.Info);
                return;
            }

            // Status
            if (isSampling)
            {
                GUILayout.Space(4);
                DrawSection("Status");

                string status;
                float progress;
                if (_activeSampler.IsWaitingForDeterministic && _deterministicController != null)
                {
                    int remaining = _deterministicController.FramesRemaining;
                    status = $"Deterministic warmup... {remaining} frames remaining (seed={_deterministicController.Seed})";
                    progress = 1f - (remaining / (float)_deterministicWarmupFrames);
                }
                else if (_activeSampler.IsWarming)
                {
                    status = $"Warming up... {_activeSampler.WarmupRemaining:F1}s remaining";
                    progress = 1f - (_activeSampler.WarmupRemaining / _warmupSeconds);
                }
                else
                {
                    float elapsed = _activeSampler.ElapsedSampleTime;
                    status = _durationSeconds > 0
                        ? $"Sampling... {elapsed:F1}s / {_durationSeconds:F0}s  ({_activeSampler.FramesCaptured} frames)"
                        : $"Sampling... {elapsed:F1}s  ({_activeSampler.FramesCaptured} frames)";
                    progress = _durationSeconds > 0 ? elapsed / _durationSeconds : -1f;
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label(status);
                EditorGUILayout.EndHorizontal();

                if (progress >= 0f)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(12);
                    var rect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                    rect.width -= 12;
                    EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.2f));
                    var fill = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height);
                    EditorGUI.DrawRect(fill, AccentColor);
                    EditorGUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8);

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUI.enabled = !isSampling;
            if (GUILayout.Button("Start Benchmark", GUILayout.Height(30)))
            {
                StartBenchmark();
            }
            GUI.enabled = isSampling;
            if (GUILayout.Button("Stop", GUILayout.Height(30), GUILayout.Width(60)))
            {
                var report = _activeSampler.StopSampling();
                HandleReport(report);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: SESSION (automated multi-iteration benchmark via Arcade)
        // ═════════════════════════════════════════════════════════════════════

        void DrawSessionTab()
        {
            if (_session == null)
                _session = BenchmarkSessionConfig.Load();

            bool isRunning = _session.IsRunning;

            // ── Active session banner ────────────────────────────────────────
            if (isRunning)
            {
                DrawSection($"Session In Progress — Iteration {_session.CurrentIteration + 1} / {_session.Iterations}");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label($"Mode: {_session.GameMode}   |   Vessel: {_session.Vessel}   |   Duration: {_session.DurationSeconds}s");
                EditorGUILayout.EndHorizontal();

                // Overall progress bar
                float overallProgress = _session.CurrentIteration / (float)_session.Iterations;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                rect.width -= 12;
                EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.2f));
                var fill = new Rect(rect.x, rect.y, rect.width * overallProgress, rect.height);
                EditorGUI.DrawRect(fill, SessionColor);

                var labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 11
                };
                labelStyle.normal.textColor = Color.white;
                GUI.Label(rect, $"{_session.CompletedReportPaths.Count} / {_session.Iterations} iterations complete", labelStyle);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                if (Application.isPlaying)
                {
                    string phase;
                    if (_waitingForGameScene)
                        phase = "Waiting for game scene to load via Arcade...";
                    else if (_waitingForCountdown)
                    {
                        float remaining = Mathf.Max(0f, (float)(_countdownWaitEndTime - EditorApplication.timeSinceStartup));
                        phase = $"Go button pressed — countdown {remaining:F1}s remaining...";
                    }
                    else
                        phase = "Sampling gameplay...";

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(12);
                    GUILayout.Label(phase);
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(12);
                    GUILayout.Label("Entering play mode for next iteration...");
                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                if (GUILayout.Button("Abort Session", GUILayout.Height(28)))
                {
                    AbortSession();
                }
                EditorGUILayout.EndHorizontal();

                return;
            }

            // ── Configuration ────────────────────────────────────────────────
            DrawSection("Automated Session");

            EditorGUILayout.HelpBox(
                "Run a benchmark multiple times through the Arcade bootstrap flow:\n" +
                "1. Enter play mode (SceneBootstrapper loads Menu_Main)\n" +
                "2. Arcade launches the selected game from OrganicRematchGames\n" +
                "3. Once the game scene loads, the Go button is pressed automatically\n" +
                "4. After the countdown, benchmark sampling captures 20s of gameplay\n" +
                "5. After sampling, results are saved and play mode exits\n" +
                "6. Repeat for N iterations, then generate reproducibility report",
                MessageType.None);

            GUILayout.Space(4);

            // Game mode from ArcadeGames SO
            DrawSection("Game Configuration");

            if (_selectableGames == null || _selectableGames.Length == 0)
                RefreshArcadeGameList();

            if (_selectableGames != null && _selectableGames.Length > 0)
            {
                int gameModeIdx = Array.FindIndex(_selectableGames, g => g.Mode == _session.GameMode);
                if (gameModeIdx < 0) gameModeIdx = 0;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label("Game Mode", GUILayout.Width(100));
                int newGameModeIdx = EditorGUILayout.Popup(gameModeIdx, _gameDisplayNames);
                if (newGameModeIdx != gameModeIdx || _session.GameMode != _selectableGames[newGameModeIdx].Mode)
                    _session.GameMode = _selectableGames[newGameModeIdx].Mode;
                EditorGUILayout.EndHorizontal();

                // Show scene name for clarity
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                var sceneStyle = new GUIStyle(EditorStyles.miniLabel);
                sceneStyle.normal.textColor = TextMuted;
                GUILayout.Label($"Scene: {_selectableGames[Mathf.Clamp(newGameModeIdx, 0, _selectableGames.Length - 1)].SceneName}", sceneStyle);
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("Could not find ArcadeGames SO. Check Assets/_SO_Assets/Games/GameLists/.", MessageType.Warning);
            }

            // Vessel picker
            int vesselIdx = Array.IndexOf(SelectableVessels, _session.Vessel);
            if (vesselIdx < 0) vesselIdx = 0;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Vessel", GUILayout.Width(100));
            int newVesselIdx = EditorGUILayout.Popup(vesselIdx, VesselNames);
            if (newVesselIdx != vesselIdx)
                _session.Vessel = SelectableVessels[newVesselIdx];
            EditorGUILayout.EndHorizontal();

            // Intensity 1-4
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Intensity", GUILayout.Width(100));
            _session.Intensity = EditorGUILayout.IntSlider(_session.Intensity, 1, 4);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Session parameters
            DrawSection("Parameters");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Label", GUILayout.Width(100));
            _session.Label = EditorGUILayout.TextField(_session.Label);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Iterations", GUILayout.Width(100));
            _session.Iterations = EditorGUILayout.IntField(_session.Iterations, GUILayout.Width(60));
            _session.Iterations = Mathf.Clamp(_session.Iterations, 2, 50);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Duration (s)", GUILayout.Width(100));
            _session.DurationSeconds = EditorGUILayout.FloatField(_session.DurationSeconds, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Deterministic settings
            DrawSection("Deterministic Settings");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            _session.Deterministic = EditorGUILayout.Toggle(_session.Deterministic, GUILayout.Width(16));
            GUILayout.Label("Deterministic mode (strongly recommended for reproducibility)");
            EditorGUILayout.EndHorizontal();

            if (_session.Deterministic)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                GUILayout.Label("Seed", GUILayout.Width(100));
                _session.Seed = EditorGUILayout.IntField(_session.Seed, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                GUILayout.Label("Warmup Frames", GUILayout.Width(100));
                _session.WarmupFrames = EditorGUILayout.IntField(_session.WarmupFrames, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(28);
                GUILayout.Label("Fixed Delta Time", GUILayout.Width(100));
                _session.FixedDt = EditorGUILayout.FloatField(_session.FixedDt, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            // Launch button
            bool canLaunch = !Application.isPlaying;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUI.enabled = canLaunch;
            if (GUILayout.Button("Run Session", GUILayout.Height(34)))
            {
                LaunchSession();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Exit Play Mode before starting a session.", MessageType.Warning);
            }

            // ── Last session results ─────────────────────────────────────────
            if (_sessionSummary != null)
            {
                GUILayout.Space(12);
                DrawSessionSummary();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: RESULTS
        // ═════════════════════════════════════════════════════════════════════

        void DrawResultsTab()
        {
            if (_lastReport == null)
            {
                EditorGUILayout.HelpBox("No benchmark results yet. Run a benchmark first.", MessageType.Info);
                return;
            }

            DrawSection($"Results — {_lastReport.Label}");
            DrawReportDetails(_lastReport);

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            if (GUILayout.Button("Save Report"))
            {
                string path = _lastReport.Save();
                CSDebug.Log($"[Benchmark] Saved to {path}");
                EditorUtility.DisplayDialog("Benchmark Saved", $"Report saved to:\n{path}", "OK");
                RefreshSavedReports();
            }
            if (GUILayout.Button("Set as Baseline"))
            {
                _baselineReport = _lastReport;
                CSDebug.Log("[Benchmark] Current result set as baseline.");
            }
            EditorGUILayout.EndHorizontal();

            // Quick comparison with baseline
            if (_baselineReport != null && _baselineReport != _lastReport)
            {
                GUILayout.Space(12);
                DrawSection("Quick Comparison vs Baseline");
                DrawComparison(_baselineReport, _lastReport);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: COMPARE
        // ═════════════════════════════════════════════════════════════════════

        void DrawCompareTab()
        {
            DrawSection("Compare Two Reports");

            if (_savedPaths.Length == 0)
            {
                EditorGUILayout.HelpBox("No saved reports found. Run and save a benchmark first.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Baseline", GUILayout.Width(60));
            _selectedBaselineIdx = EditorGUILayout.Popup(_selectedBaselineIdx, _savedNames);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Current", GUILayout.Width(60));
            _selectedCurrentIdx = EditorGUILayout.Popup(_selectedCurrentIdx, _savedNames);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            if (GUILayout.Button("Compare", GUILayout.Height(28)))
            {
                if (_selectedBaselineIdx >= 0 && _selectedCurrentIdx >= 0)
                {
                    _comparedBaseline = BenchmarkReport.Load(_savedPaths[_selectedBaselineIdx]);
                    _comparedCurrent = BenchmarkReport.Load(_savedPaths[_selectedCurrentIdx]);
                    _comparisonLines = BenchmarkReport.Compare(_comparedBaseline, _comparedCurrent);
                }
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(60), GUILayout.Height(28)))
                RefreshSavedReports();
            EditorGUILayout.EndHorizontal();

            if (_comparisonLines != null && _comparedBaseline != null && _comparedCurrent != null)
            {
                GUILayout.Space(8);
                DrawSection($"{_comparedBaseline.Label} vs {_comparedCurrent.Label}");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label($"Baseline: {_comparedBaseline.Timestamp}  ({_comparedBaseline.GitCommit})", _mutedLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label($"Current:  {_comparedCurrent.Timestamp}  ({_comparedCurrent.GitCommit})", _mutedLabel);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);
                DrawComparisonLines(_comparisonLines);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: HISTORY
        // ═════════════════════════════════════════════════════════════════════

        void DrawHistoryTab()
        {
            DrawSection("Saved Reports");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            if (GUILayout.Button("Refresh"))
                RefreshSavedReports();
            if (GUILayout.Button("Open Folder") && Directory.Exists(ReportsDir))
                EditorUtility.RevealInFinder(ReportsDir);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (_savedPaths.Length == 0)
            {
                EditorGUILayout.HelpBox("No saved reports. Reports are saved to /BenchmarkReports/ in the project root.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _savedPaths.Length; i++)
            {
                var report = BenchmarkReport.Load(_savedPaths[i]);
                if (report == null) continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);

                GUILayout.Label($"{report.Label}", EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label($"{report.Timestamp}", GUILayout.Width(140));
                GUILayout.Label($"{report.SceneName}", GUILayout.Width(100));
                GUILayout.Label($"{report.AvgFps:F1} fps", GUILayout.Width(70));
                GUILayout.Label($"({report.GitCommit})", _mutedLabel, GUILayout.Width(70));

                if (GUILayout.Button("Load", GUILayout.Width(45)))
                {
                    _lastReport = report;
                    _selectedTab = 2;
                    _scrollPos = Vector2.zero;
                }
                if (GUILayout.Button("Base", GUILayout.Width(40)))
                {
                    _baselineReport = report;
                }
                if (GUILayout.Button("Del", GUILayout.Width(35)))
                {
                    if (EditorUtility.DisplayDialog("Delete Report", $"Delete {_savedNames[i]}?", "Delete", "Cancel"))
                    {
                        File.Delete(_savedPaths[i]);
                        RefreshSavedReports();
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SESSION SUMMARY DRAWING
        // ═════════════════════════════════════════════════════════════════════

        void DrawSessionSummary()
        {
            var s = _sessionSummary;

            DrawSection($"Session Results — {s.Label} ({s.IterationCount} iterations)");

            DrawMetric("Scene", s.SceneName);
            DrawMetric("Iterations", $"{s.IterationCount}");
            DrawMetric("Duration/iter", $"{s.DurationPerIteration:F1}s");
            if (s.Deterministic)
                DrawMetric("Deterministic", $"seed={s.Seed}");

            GUILayout.Space(6);
            DrawSubSection("Avg Frame Time (ms)");
            DrawMetric("Mean", $"{s.AvgFrameTimeMean:F2}");
            DrawMetric("Min / Max", $"{s.AvgFrameTimeMin:F2} / {s.AvgFrameTimeMax:F2}");
            DrawMetric("Spread", $"{s.AvgFrameTimeSpread:F2} ({s.AvgFrameTimeSpreadPercent:F1}%)");
            DrawReproducibilityMetric("CoV", s.AvgFrameTimeCoV);

            GUILayout.Space(6);
            DrawSubSection("Avg FPS");
            DrawMetric("Mean", $"{s.AvgFpsMean:F1}");
            DrawMetric("Min / Max", $"{s.AvgFpsMin:F1} / {s.AvgFpsMax:F1}");
            DrawMetric("Spread", $"{s.AvgFpsSpread:F1} ({s.AvgFpsSpreadPercent:F1}%)");
            DrawReproducibilityMetric("CoV", s.AvgFpsCoV);

            GUILayout.Space(6);
            DrawSubSection("P99 Frame Time (ms)");
            DrawMetric("Mean", $"{s.P99FrameTimeMean:F2}");
            DrawMetric("Min / Max", $"{s.P99FrameTimeMin:F2} / {s.P99FrameTimeMax:F2}");
            DrawReproducibilityMetric("CoV", s.P99FrameTimeCoV);

            GUILayout.Space(6);
            DrawSubSection("Jank %");
            DrawMetric("Mean", $"{s.JankPercentMean:F2}%");
            DrawMetric("Min / Max", $"{s.JankPercentMin:F2}% / {s.JankPercentMax:F2}%");

            GUILayout.Space(6);
            DrawSubSection("Reproducibility Verdict");

            Color verdictColor;
            string verdict;
            if (s.AvgFrameTimeCoV < 0.02f)
            {
                verdictColor = GoodColor;
                verdict = "EXCELLENT — CoV < 2%, results are highly reproducible";
            }
            else if (s.AvgFrameTimeCoV < 0.05f)
            {
                verdictColor = Color.Lerp(GoodColor, NeutralColor, 0.5f);
                verdict = "GOOD — CoV < 5%, results are reasonably reproducible";
            }
            else if (s.AvgFrameTimeCoV < 0.10f)
            {
                verdictColor = NeutralColor;
                verdict = "FAIR — CoV < 10%, some variance present. Consider longer duration or more warmup.";
            }
            else
            {
                verdictColor = BadColor;
                verdict = "POOR — CoV >= 10%, results are not reproducible. Check for background processes, thermal throttling, or non-deterministic scene behavior.";
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            var style = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11 };
            style.normal.textColor = verdictColor;
            GUILayout.Label(verdict, style);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            DrawSubSection("Per-Iteration Results");

            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("#", EditorStyles.boldLabel, GUILayout.Width(30));
            GUILayout.Label("Avg FPS", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Label("Avg ms", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Label("P99 ms", EditorStyles.boldLabel, GUILayout.Width(70));
            GUILayout.Label("Jank%", EditorStyles.boldLabel, GUILayout.Width(60));
            GUILayout.Label("Frames", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            if (_sessionReports != null)
            {
                for (int i = 0; i < _sessionReports.Count; i++)
                {
                    var r = _sessionReports[i];
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16);
                    GUILayout.Label($"{i + 1}", GUILayout.Width(30));
                    GUILayout.Label($"{r.AvgFps:F1}", GUILayout.Width(70));
                    GUILayout.Label($"{r.AvgFrameTimeMs:F2}", GUILayout.Width(70));
                    GUILayout.Label($"{r.P99FrameTimeMs:F2}", GUILayout.Width(70));
                    GUILayout.Label($"{r.JankPercent:F1}%", GUILayout.Width(60));
                    GUILayout.Label($"{r.TotalFrames}", GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        void DrawReproducibilityMetric(string label, float cov)
        {
            Color color = cov < 0.02f ? GoodColor : cov < 0.05f ? NeutralColor : BadColor;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label(label, _metricLabel, GUILayout.Width(160));
            var style = new GUIStyle(_valueStyle);
            style.normal.textColor = color;
            GUILayout.Label($"{cov * 100f:F2}%", style);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  DRAWING HELPERS
        // ═════════════════════════════════════════════════════════════════════

        void DrawSection(string title)
        {
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SectionHeader);
            var accent = new Rect(rect.x, rect.y, 4, rect.height);
            EditorGUI.DrawRect(accent, AccentColor);
            var labelRect = new Rect(rect.x + 12, rect.y, rect.width - 12, rect.height);
            _sectionStyle.normal.textColor = AccentColor;
            GUI.Label(labelRect, title, _sectionStyle);
            GUILayout.Space(4);
        }

        void DrawReportDetails(BenchmarkReport r)
        {
            DrawMetric("Scene", r.SceneName);
            DrawMetric("Duration", $"{r.DurationSeconds:F1}s ({r.TotalFrames} frames)");
            DrawMetric("Git Commit", r.GitCommit);
            if (r.Deterministic)
                DrawMetric("Deterministic", $"seed={r.DeterministicSeed}");

            GUILayout.Space(6);
            DrawSubSection("Frame Time");
            DrawMetric("Average", $"{r.AvgFrameTimeMs:F2} ms");
            DrawMetric("Median", $"{r.MedianFrameTimeMs:F2} ms");
            DrawMetric("Min / Max", $"{r.MinFrameTimeMs:F2} / {r.MaxFrameTimeMs:F2} ms");
            DrawMetric("P1 / P5", $"{r.P1FrameTimeMs:F2} / {r.P5FrameTimeMs:F2} ms");
            DrawMetric("P95 / P99", $"{r.P95FrameTimeMs:F2} / {r.P99FrameTimeMs:F2} ms");
            DrawMetric("Std Dev", $"{r.StdDevFrameTimeMs:F2} ms");

            GUILayout.Space(6);
            DrawSubSection("FPS");
            DrawMetric("Average", $"{r.AvgFps:F1}");
            DrawMetric("P1 (worst 1%)", $"{r.P1Fps:F1}");
            DrawMetric("P5 (worst 5%)", $"{r.P5Fps:F1}");
            DrawMetric("Jank %", $"{r.JankPercent:F1}%");

            if (r.AvgGpuTimeMs >= 0)
            {
                GUILayout.Space(6);
                DrawSubSection("GPU");
                DrawMetric("Avg GPU Time", $"{r.AvgGpuTimeMs:F2} ms");
            }

            GUILayout.Space(6);
            DrawSubSection("Memory / GC");
            DrawMetric("GC Alloc Total", $"{r.TotalGcAllocBytes / 1024f:F0} KB");
            DrawMetric("GC Collections", $"{r.GcCollectCount}");
            DrawMetric("Peak Used Memory", $"{r.PeakUsedMemoryBytes / (1024f * 1024f):F1} MB");

            if (r.AvgDrawCalls >= 0)
            {
                GUILayout.Space(6);
                DrawSubSection("Rendering");
                DrawMetric("Avg Draw Calls", $"{r.AvgDrawCalls}");
                DrawMetric("Avg SetPass Calls", $"{r.AvgSetPassCalls}");
                DrawMetric("Avg Triangles", $"{r.AvgTriangles / 1000f:F1}K");
                DrawMetric("Avg Vertices", $"{r.AvgVertices / 1000f:F1}K");
            }
        }

        void DrawMetric(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label(label, _metricLabel, GUILayout.Width(160));
            GUILayout.Label(value, _valueStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawSubSection(string title)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            style.normal.textColor = Color.Lerp(AccentColor, Color.white, 0.4f);
            GUILayout.Label("— " + title, style);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        void DrawComparison(BenchmarkReport baseline, BenchmarkReport current)
        {
            var lines = BenchmarkReport.Compare(baseline, current);
            DrawComparisonLines(lines);
        }

        void DrawComparisonLines(List<ComparisonLine> lines)
        {
            // Header row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("Metric", EditorStyles.boldLabel, GUILayout.Width(140));
            GUILayout.Label("Baseline", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Current", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Delta", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Change", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);

            foreach (var line in lines)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16);

                GUILayout.Label(line.Label, _metricLabel, GUILayout.Width(140));
                GUILayout.Label($"{line.Baseline:F2}{line.Unit}", GUILayout.Width(80));
                GUILayout.Label($"{line.Current:F2}{line.Unit}", GUILayout.Width(80));

                // Delta with color
                Color deltaColor = line.Improved ? GoodColor : line.Regressed ? BadColor : NeutralColor;
                var deltaStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };
                deltaStyle.normal.textColor = deltaColor;

                string sign = line.Delta > 0 ? "+" : "";
                GUILayout.Label($"{sign}{line.Delta:F2}", deltaStyle, GUILayout.Width(80));
                GUILayout.Label($"{sign}{line.DeltaPercent:F1}%", deltaStyle, GUILayout.Width(80));

                EditorGUILayout.EndHorizontal();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MANUAL BENCHMARK LOGIC
        // ═════════════════════════════════════════════════════════════════════

        void StartBenchmark()
        {
            // Clean up previous run
            if (_activeSampler != null)
                DestroyImmediate(_activeSampler.gameObject);
            if (_deterministicController != null)
                DestroyImmediate(_deterministicController.gameObject);

            // Set up deterministic controller if enabled
            if (_deterministicMode)
            {
                var detGo = new GameObject("[Benchmark Deterministic]");
                detGo.hideFlags = HideFlags.DontSave;
                _deterministicController = detGo.AddComponent<DeterministicBenchmarkController>();
                _deterministicController.Configure(_deterministicSeed, _deterministicWarmupFrames, _deterministicFixedDt);
            }
            else
            {
                _deterministicController = null;
            }

            var go = new GameObject("[Benchmark Sampler]");
            go.hideFlags = HideFlags.DontSave;
            _activeSampler = go.AddComponent<PerformanceSampler>();
            _activeSampler.Configure(_label, _warmupSeconds, _durationSeconds);

            if (_deterministicController != null)
                _activeSampler.SetDeterministicController(_deterministicController);

            _activeSampler.OnSamplingComplete += HandleReport;
            _activeSampler.StartSampling();
        }

        void HandleReport(BenchmarkReport report)
        {
            if (report == null) return;
            _lastReport = report;
            _selectedTab = 2;
            _scrollPos = Vector2.zero;

            // Clean up deterministic controller
            if (_deterministicController != null)
            {
                DestroyImmediate(_deterministicController.gameObject);
                _deterministicController = null;
            }

            Repaint();

            string detInfo = report.Deterministic ? $" [deterministic seed={report.DeterministicSeed}]" : "";
            CSDebug.Log($"[Benchmark] Complete — {report.AvgFps:F1} avg FPS, {report.P99FrameTimeMs:F2}ms P99, {report.TotalFrames} frames in {report.DurationSeconds:F1}s{detInfo}");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  SESSION ORCHESTRATION (Arcade bootstrap flow)
        // ═════════════════════════════════════════════════════════════════════

        void RegisterSessionCallback()
        {
            if (_sessionCallbackRegistered) return;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _sessionCallbackRegistered = true;
        }

        void UnregisterSessionCallback()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _sessionCallbackRegistered = false;
        }

        void LaunchSession()
        {
            _session.IsRunning = true;
            _session.CurrentIteration = 0;
            _session.GameLaunched = false;
            _session.CompletedReportPaths.Clear();
            _session.Save();

            _sessionReports = null;
            _sessionSummary = null;

            CSDebug.Log($"[Benchmark Session] Starting {_session.Iterations} iterations — {_session.GameMode} / {_session.Vessel}");

            // Just enter play mode. SceneBootstrapper will redirect to Menu_Main (bootstrap).
            // Our playModeStateChanged callback handles the rest.
            EditorApplication.EnterPlaymode();
        }

        void StartNextIteration()
        {
            _session = BenchmarkSessionConfig.Load();

            if (!_session.IsRunning || _session.CurrentIteration >= _session.Iterations)
            {
                FinishSession();
                return;
            }

            CSDebug.Log($"[Benchmark Session] Starting iteration {_session.CurrentIteration + 1} / {_session.Iterations}");

            // Reset game-launched flag for this iteration
            _session.GameLaunched = false;
            _session.Save();

            // Enter play mode — SceneBootstrapper handles loading Menu_Main
            EditorApplication.EnterPlaymode();
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            _session = BenchmarkSessionConfig.Load();

            if (!_session.IsRunning) return;

            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    // We're now in play mode (Menu_Main loaded by SceneBootstrapper).
                    // Wait a frame for Arcade singleton to initialize, then launch the game.
                    _waitingForGameScene = true;
                    EditorApplication.delayCall += LaunchGameViaArcade;
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    // Play mode exited — start next iteration or finish
                    EditorApplication.delayCall += StartNextIteration;
                    break;
            }
        }

        void LaunchGameViaArcade()
        {
            _session = BenchmarkSessionConfig.Load();
            if (!_session.IsRunning || !Application.isPlaying) return;

            // Check if Arcade singleton is ready
            if (Arcade.Instance == null)
            {
                // Arcade not ready yet — try again next frame
                EditorApplication.delayCall += LaunchGameViaArcade;
                return;
            }

            // Subscribe to scene load so we know when the game scene arrives
            SceneManager.sceneLoaded += OnGameSceneLoaded;

            CSDebug.Log($"[Benchmark Session] Launching {_session.GameMode} with {_session.Vessel} via Arcade...");

            // Launch through the Arcade — this populates GameDataSO and fires the event
            // that GameManager listens to, which loads the game scene after a 0.5s delay
            Arcade.Instance.LaunchArcadeGame(
                gameMode: _session.GameMode,
                vessel: _session.Vessel,
                shipResources: new ResourceCollection(0.5f, 0.5f, 0.5f, 0.5f),
                intensity: _session.Intensity,
                numberOfPlayers: 1,
                isMultiplayer: false,
                isDailyChallenge: false
            );

            _session.GameLaunched = true;
            _session.Save();
        }

        void OnGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Ignore the bootstrap scene itself
            string bootstrapName = "Menu_Main";
            if (scene.name == bootstrapName) return;

            SceneManager.sceneLoaded -= OnGameSceneLoaded;
            _waitingForGameScene = false;

            CSDebug.Log($"[Benchmark Session] Game scene '{scene.name}' loaded. Pressing Go button...");

            // The game scene is loaded — wait a frame for initialization, then press Go
            EditorApplication.delayCall += PressGoButton;
        }

        void PressGoButton()
        {
            if (!Application.isPlaying) return;

            // Find the MiniGameControllerBase in the scene and press Go
            var controller = UnityEngine.Object.FindAnyObjectByType<MiniGameControllerBase>();
            if (controller == null)
            {
                // Controller not ready yet — retry next frame
                EditorApplication.delayCall += PressGoButton;
                return;
            }

            controller.OnReadyClicked();
            CSDebug.Log("[Benchmark Session] Go button pressed. Waiting for countdown to finish...");

            // Wait for the countdown to complete before starting the benchmark sampler
            _waitingForCountdown = true;
            _countdownWaitEndTime = EditorApplication.timeSinceStartup + CountdownDurationSeconds;
            EditorApplication.update += OnCountdownWaitUpdate;
        }

        void OnCountdownWaitUpdate()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= OnCountdownWaitUpdate;
                _waitingForCountdown = false;
                return;
            }

            if (EditorApplication.timeSinceStartup < _countdownWaitEndTime)
                return;

            // Countdown is over — start the benchmark sampler
            EditorApplication.update -= OnCountdownWaitUpdate;
            _waitingForCountdown = false;

            CSDebug.Log("[Benchmark Session] Countdown complete. Starting benchmark sampling...");
            AutoStartSessionBenchmark();
        }

        void AutoStartSessionBenchmark()
        {
            _session = BenchmarkSessionConfig.Load();
            if (!_session.IsRunning || !Application.isPlaying) return;

            // Clean up any previous sampler
            if (_activeSampler != null)
                DestroyImmediate(_activeSampler.gameObject);
            if (_deterministicController != null)
                DestroyImmediate(_deterministicController.gameObject);

            // Set up deterministic controller
            if (_session.Deterministic)
            {
                var detGo = new GameObject("[Benchmark Session Deterministic]");
                detGo.hideFlags = HideFlags.DontSave;
                _deterministicController = detGo.AddComponent<DeterministicBenchmarkController>();
                _deterministicController.Configure(_session.Seed, _session.WarmupFrames, _session.FixedDt);
            }

            string iterLabel = $"{_session.Label}_iter{_session.CurrentIteration + 1}";
            var go = new GameObject("[Benchmark Session Sampler]");
            go.hideFlags = HideFlags.DontSave;
            _activeSampler = go.AddComponent<PerformanceSampler>();
            // Gameplay is already running (Go button pressed, countdown finished).
            // Use a short 1s warmup just for frame-timing stabilization.
            _activeSampler.Configure(iterLabel, 1f, _session.DurationSeconds);

            if (_deterministicController != null)
                _activeSampler.SetDeterministicController(_deterministicController);

            _activeSampler.OnSamplingComplete += HandleSessionIterationComplete;
            _activeSampler.StartSampling();

            CSDebug.Log($"[Benchmark Session] Iteration {_session.CurrentIteration + 1} benchmark started");
            Repaint();
        }

        void HandleSessionIterationComplete(BenchmarkReport report)
        {
            if (report == null) return;

            // Save the report
            string path = report.Save();
            CSDebug.Log($"[Benchmark Session] Iteration {_session.CurrentIteration + 1} complete — {report.AvgFps:F1} fps, saved to {path}");

            // Update session state
            _session = BenchmarkSessionConfig.Load();
            _session.CompletedReportPaths.Add(path);
            _session.CurrentIteration++;
            _session.Save();

            // Clean up
            if (_deterministicController != null)
            {
                DestroyImmediate(_deterministicController.gameObject);
                _deterministicController = null;
            }

            _lastReport = report;
            Repaint();

            // Exit play mode — the callback will handle starting the next iteration
            EditorApplication.ExitPlaymode();
        }

        void FinishSession()
        {
            _session = BenchmarkSessionConfig.Load();
            var paths = _session.CompletedReportPaths;

            _session.IsRunning = false;
            _session.Save();

            CSDebug.Log($"[Benchmark Session] All {paths.Count} iterations complete. Generating reproducibility report...");

            // Load all reports
            _sessionReports = new List<BenchmarkReport>();
            foreach (var path in paths)
            {
                var report = BenchmarkReport.Load(path);
                if (report != null)
                    _sessionReports.Add(report);
            }

            // Generate summary
            if (_sessionReports.Count >= 2)
            {
                _sessionSummary = BenchmarkSessionSummary.Build(_sessionReports, _session);
                CSDebug.Log($"[Benchmark Session] Reproducibility — Avg FPS CoV: {_sessionSummary.AvgFpsCoV * 100f:F2}%, Avg Frame Time CoV: {_sessionSummary.AvgFrameTimeCoV * 100f:F2}%");
            }

            _selectedTab = 1; // Switch to Session tab to show results
            RefreshSavedReports();
            Repaint();
        }

        void AbortSession()
        {
            CSDebug.Log("[Benchmark Session] Aborted by user.");

            // Unsubscribe from scene loads and countdown wait in case we're mid-wait
            SceneManager.sceneLoaded -= OnGameSceneLoaded;
            EditorApplication.update -= OnCountdownWaitUpdate;
            _waitingForGameScene = false;
            _waitingForCountdown = false;

            // Stop any active sampling
            if (_activeSampler != null)
            {
                _activeSampler.StopSampling();
                DestroyImmediate(_activeSampler.gameObject);
                _activeSampler = null;
            }
            if (_deterministicController != null)
            {
                DestroyImmediate(_deterministicController.gameObject);
                _deterministicController = null;
            }

            // Load whatever reports were completed so far
            _session = BenchmarkSessionConfig.Load();

            _session.IsRunning = false;
            _session.Save();

            // Build partial summary if we have enough data
            _sessionReports = new List<BenchmarkReport>();
            foreach (var path in _session.CompletedReportPaths)
            {
                var report = BenchmarkReport.Load(path);
                if (report != null)
                    _sessionReports.Add(report);
            }
            if (_sessionReports.Count >= 2)
                _sessionSummary = BenchmarkSessionSummary.Build(_sessionReports, _session);

            if (Application.isPlaying)
                EditorApplication.ExitPlaymode();

            RefreshSavedReports();
            Repaint();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ARCADE GAME LIST (loaded from SO at editor time)
        // ═════════════════════════════════════════════════════════════════════

        void RefreshArcadeGameList()
        {
            // Find the OrganicRematchGames SO asset (the active game list)
            string[] guids = AssetDatabase.FindAssets("t:SO_GameList", new[] { "Assets/_SO_Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var list = AssetDatabase.LoadAssetAtPath<SO_GameList>(path);
                if (list != null && list.name == "OrganicRematchGames")
                {
                    _arcadeGameList = list;
                    break;
                }
            }

            if (_arcadeGameList == null || _arcadeGameList.Games == null)
            {
                _selectableGames = Array.Empty<SO_ArcadeGame>();
                _gameDisplayNames = Array.Empty<string>();
                return;
            }

            // Build settings scene names for filtering
            var buildSceneNames = new HashSet<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                    buildSceneNames.Add(System.IO.Path.GetFileNameWithoutExtension(scene.path));
            }

            // Include games with scenes in build settings
            var valid = new List<SO_ArcadeGame>();
            foreach (var game in _arcadeGameList.Games)
            {
                if (game == null) continue;
                if (buildSceneNames.Contains(game.SceneName))
                    valid.Add(game);
            }

            _selectableGames = valid.ToArray();
            _gameDisplayNames = valid.Select(g => $"{g.DisplayName} ({g.Mode})").ToArray();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  REPORT MANAGEMENT
        // ═════════════════════════════════════════════════════════════════════

        static readonly string ReportsDir = Path.Combine(Application.dataPath, "..", "BenchmarkReports");

        void RefreshSavedReports()
        {
            _savedPaths = BenchmarkReport.GetSavedReportPaths();
            Array.Sort(_savedPaths);
            Array.Reverse(_savedPaths); // newest first
            _savedNames = _savedPaths.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();
            _selectedBaselineIdx = Mathf.Clamp(_selectedBaselineIdx, -1, _savedPaths.Length - 1);
            _selectedCurrentIdx = Mathf.Clamp(_selectedCurrentIdx, -1, _savedPaths.Length - 1);
        }
    }
}

#endif
