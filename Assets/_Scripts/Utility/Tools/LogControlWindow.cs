#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility
{
    public class LogControlWindow : EditorWindow
    {
        // ── Prefs keys ───────────────────────────────────────────────────────
        const string PrefLogEnabled = "CSDebug_LogEnabled";
        const string PrefWarningsEnabled = "CSDebug_WarningsEnabled";
        const string PrefErrorsEnabled = "CSDebug_ErrorsEnabled";
        const string PrefUnityLoggerEnabled = "CSDebug_UnityLoggerEnabled";
        const string PrefVerboseChannels = "CSDebug_VerboseChannels";
        const string PrefStackTracePrefix = "CSDebug_StackTrace_";
        const string PrefBootstrapScene = "Load Main_Menu Scene";
        const int Pad = 12;

        // ── State ────────────────────────────────────────────────────────────
        Vector2 _scrollPos;
        string _questIndexInput = "1";
        string _crystalAmountInput = "100";
        SO_VesselList _vesselList;

        // ── Tab system ───────────────────────────────────────────────────────
        int _selectedTab;

        struct TabDef
        {
            public string Label;
            public Color Color;
            public Action<LogControlWindow> Draw;
        }

        static readonly TabDef[] Tabs =
        {
            new() { Label = "Scenes",     Color = new Color(0.68f, 0.62f, 0.85f, 1f), Draw = w => w.DrawScenesTab() },
            new() { Label = "Tools",      Color = new Color(0.60f, 0.85f, 0.75f, 1f), Draw = w => w.DrawToolsTab() },
            new() { Label = "Logging",    Color = new Color(0.85f, 0.72f, 0.60f, 1f), Draw = w => w.DrawLoggingTab() },
            new() { Label = "Density",    Color = new Color(0.85f, 0.78f, 0.55f, 1f), Draw = w => w.DrawDensityTab() },
            new() { Label = "Quest",      Color = new Color(0.72f, 0.60f, 0.85f, 1f), Draw = w => w.DrawQuestTab() },
            new() { Label = "Vessels",    Color = new Color(0.60f, 0.78f, 0.85f, 1f), Draw = w => w.DrawVesselsTab() },
            new() { Label = "Crystals",   Color = new Color(0.85f, 0.60f, 0.72f, 1f), Draw = w => w.DrawCrystalsTab() },
            new() { Label = "UGS Data",   Color = new Color(0.75f, 0.85f, 0.60f, 1f), Draw = w => w.DrawUGSDataTab() },
        };

        // ── UGS Data sub-foldouts ────────────────────────────────────────────
        bool _ugsProfileFoldout;
        bool _ugsStatsFoldout;
        bool _ugsProgressionFoldout;
        bool _ugsHangarFoldout;
        bool _ugsEpisodesFoldout;
        bool _ugsSettingsFoldout;

        // EditorPrefs key for pending debug crystals (edit-mode awards applied on next play)
        const string PrefPendingCrystals = "FrogletDebug_PendingCrystals";

        // ── Pastel Palette ───────────────────────────────────────────────────
        static readonly Color BannerBg       = new(0.22f, 0.20f, 0.30f, 1f);
        static readonly Color AccentLavender = new(0.68f, 0.62f, 0.85f, 1f);
        static readonly Color SectionHeader  = new(0.20f, 0.19f, 0.26f, 1f);
        static readonly Color DividerColor   = new(0.38f, 0.34f, 0.48f, 0.4f);
        static readonly Color BadgeOn        = new(0.45f, 0.72f, 0.58f, 1f);
        static readonly Color BadgeOff       = new(0.72f, 0.45f, 0.48f, 1f);
        static readonly Color TextMuted      = new(0.58f, 0.56f, 0.65f, 1f);
        static readonly Color FooterBg       = new(0.14f, 0.13f, 0.18f, 1f);
        static readonly Color TabInactive    = new(0.18f, 0.17f, 0.22f, 1f);
        static readonly Color TabHover       = new(0.26f, 0.24f, 0.32f, 1f);

        // ── Styles ───────────────────────────────────────────────────────────
        [NonSerialized] GUIStyle _bannerStyle;
        [NonSerialized] GUIStyle _badgeStyle;
        [NonSerialized] GUIStyle _infoStyle;
        [NonSerialized] GUIStyle _mutedLabel;
        [NonSerialized] GUIStyle _tabLabelStyle;
        [NonSerialized] GUIStyle _sectionTitleStyle;
        [NonSerialized] GUIStyle _contentLabelStyle;
        [NonSerialized] GUIStyle _contentBoldStyle;
        [NonSerialized] Texture2D _whiteTexture;
        [NonSerialized] bool _stylesBuilt;

        [MenuItem("FrogletTools/Toolbox", false, 0)]
        static void Open()
        {
            var window = GetWindow<LogControlWindow>("Froglet Toolbox");
            window.minSize = new Vector2(340, 520);
        }

        bool _subscribedToUGS;

        void OnEnable()
        {
            LoadPrefs();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            UnsubscribeFromUGS();
        }

        void OnFocus() => Repaint();

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.update += TrySubscribeToUGS;
            else if (state == PlayModeStateChange.ExitingPlayMode)
                UnsubscribeFromUGS();

            Repaint();
        }

        void TrySubscribeToUGS()
        {
            var ds = UGSDataService.Instance;
            if (ds == null) return;

            EditorApplication.update -= TrySubscribeToUGS;

            if (ds.IsInitialized)
            {
                Repaint();
                return;
            }

            ds.OnInitialized += HandleUGSInitialized;
            _subscribedToUGS = true;
        }

        void HandleUGSInitialized()
        {
            _subscribedToUGS = false;
            Repaint();
        }

        void UnsubscribeFromUGS()
        {
            EditorApplication.update -= TrySubscribeToUGS;
            if (_subscribedToUGS)
            {
                var ds = UGSDataService.Instance;
                if (ds != null)
                    ds.OnInitialized -= HandleUGSInitialized;
                _subscribedToUGS = false;
            }
        }

        Texture2D MakeColorTexture(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }

        void BuildStyles()
        {
            if (_stylesBuilt) return;

            _whiteTexture = MakeColorTexture(Color.white);

            _bannerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 6, 6)
            };
            _bannerStyle.normal.textColor = AccentLavender;

            _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(5, 5, 2, 2)
            };
            _badgeStyle.normal.textColor = Color.white;

            _infoStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                richText = true,
                padding = new RectOffset(8, 8, 6, 6)
            };

            _mutedLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 10 };
            _mutedLabel.normal.textColor = TextMuted;

            _tabLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                padding = new RectOffset(8, 0, 4, 4)
            };

            _contentLabelStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };
            _contentBoldStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };

            _stylesBuilt = true;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MAIN GUI
        // ═════════════════════════════════════════════════════════════════════

        void OnGUI()
        {
            BuildStyles();

            // ── Banner ───────────────────────────────────────────────────────
            var bannerRect = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bannerRect, BannerBg);
            GUI.Label(bannerRect, "Froglet Toolbox", _bannerStyle);

            var lineRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, AccentLavender * 0.6f);

            // ── Tab Bar ──────────────────────────────────────────────────────
            DrawTabBar();

            // ── Tab Content ──────────────────────────────────────────────────
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Draw a subtle tinted background for the entire content area
            if (_selectedTab >= 0 && _selectedTab < Tabs.Length)
            {
                Color tint = Color.Lerp(new Color(0.18f, 0.17f, 0.22f), Tabs[_selectedTab].Color, 0.06f);
                var contentBgRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true));
                // We paint a large rect behind everything
                var bgRect = new Rect(contentBgRect.x, contentBgRect.y, position.width, position.height);
                EditorGUI.DrawRect(bgRect, tint);
            }

            GUILayout.Space(6);

            if (_selectedTab >= 0 && _selectedTab < Tabs.Length)
                Tabs[_selectedTab].Draw(this);

            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();

            // ── Footer ───────────────────────────────────────────────────────
            var footerRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(footerRect, FooterBg);
            GUI.Label(footerRect, "Froglet Inc. - Cosmic Shore", _mutedLabel);
        }

        void DrawTabBar()
        {
            // Two rows: row 1 = first 4 tabs, row 2 = remaining 3 tabs
            const float tabHeight = 32;
            const float gap = 2;
            const float padding = 4;
            int row1Count = 4;
            int row2Count = Tabs.Length - row1Count;
            float totalHeight = tabHeight * 2 + gap + padding * 2;

            var barRect = GUILayoutUtility.GetRect(0, totalHeight, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, new Color(0.16f, 0.15f, 0.20f, 1f));

            float usableWidth = barRect.width - padding * 2;

            // Row 1
            DrawTabRow(barRect.x + padding, barRect.y + padding, usableWidth, tabHeight, 0, row1Count);
            // Row 2
            DrawTabRow(barRect.x + padding, barRect.y + padding + tabHeight + gap, usableWidth, tabHeight, row1Count, row2Count);

            // Repaint on hover for highlight effect
            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        void DrawTabRow(float startX, float startY, float totalWidth, float height, int startIdx, int count)
        {
            float gap = 2;
            float tabWidth = (totalWidth - gap * (count - 1)) / count;

            for (int i = 0; i < count; i++)
            {
                int tabIdx = startIdx + i;
                var tab = Tabs[tabIdx];
                bool isSelected = tabIdx == _selectedTab;

                var tabRect = new Rect(startX + i * (tabWidth + gap), startY, tabWidth, height);
                bool isHover = tabRect.Contains(Event.current.mousePosition);

                // Background: selected = full color, hover = dimmed color, inactive = dark with color tint
                Color bgColor;
                if (isSelected)
                    bgColor = tab.Color;
                else if (isHover)
                    bgColor = Color.Lerp(TabInactive, tab.Color, 0.45f);
                else
                    bgColor = Color.Lerp(TabInactive, tab.Color, 0.15f);

                EditorGUI.DrawRect(tabRect, bgColor);

                // Selected indicator: bright bottom bar
                if (isSelected)
                {
                    var indicatorRect = new Rect(tabRect.x, tabRect.yMax - 3, tabRect.width, 3);
                    EditorGUI.DrawRect(indicatorRect, Color.white * 0.9f);
                }

                // Label
                _tabLabelStyle.normal.textColor = isSelected ? Color.white : new Color(0.88f, 0.86f, 0.94f);
                GUI.Label(tabRect, tab.Label, _tabLabelStyle);

                // Click
                if (Event.current.type == EventType.MouseDown && tabRect.Contains(Event.current.mousePosition))
                {
                    _selectedTab = tabIdx;
                    _scrollPos = Vector2.zero;
                    Event.current.Use();
                    Repaint();
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: SCENES
        // ═════════════════════════════════════════════════════════════════════
        void DrawScenesTab()
        {
            DrawTabTitle("Scenes", Tabs[0].Color);
            DrawSceneButton("Main Menu",              "Assets/_Scenes/Menu_Main.unity");
            DrawSceneButton("Photo Booth",            "Assets/_Scenes/Tools/PhotoBooth.unity");
            DrawSceneButton("Recording Studio (WIP)", "Assets/_Scenes/Tools/Recording Studio.unity");
            DrawSceneButton("PlayFab Sandbox",        "Assets/_Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: TOOLS (Create, Multiplayer, Utilities)
        // ═════════════════════════════════════════════════════════════════════
        void DrawToolsTab()
        {
            DrawTabTitle("Tools", Tabs[1].Color);

            DrawSubSectionLabel("Create");
            DrawMenuItemButton("New MiniGame", "FrogletTools/Legacy/Create/MiniGame");
            DrawMenuItemButton("New Class",    "FrogletTools/Legacy/Create/Class");

            GUILayout.Space(8);
            DrawSubSectionLabel("Testing Multiplayer");

            bool bootstrapEnabled = EditorPrefs.GetBool(PrefBootstrapScene, true);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            bool newBootstrap = GUILayout.Toggle(bootstrapEnabled, "Load Bootstrap on Play");
            if (newBootstrap != bootstrapEnabled)
                EditorPrefs.SetBool(PrefBootstrapScene, newBootstrap);
            GUILayout.FlexibleSpace();
            DrawBadge(bootstrapEnabled ? "ON" : "OFF", bootstrapEnabled ? BadgeOn : BadgeOff);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            DrawSubSectionLabel("Utilities");
            DrawMenuItemButton("Component Copier",           "FrogletTools/Legacy/Component Copier");
            DrawMenuItemButton("Dialogue Editor",            "FrogletTools/Legacy/Dialogue Editor");
            DrawMenuItemButton("ElementalFloat Editor",      "FrogletTools/Legacy/ElementalFloat Editor");
            DrawMenuItemButton("Find Asset by GUID",         "FrogletTools/Legacy/Find Asset by GUID");
            DrawMenuItemButton("Force Re-Serialize All SOs", "FrogletTools/Legacy/Force Re-Serialize All ScriptableObjects");
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: LOGGING
        // ═════════════════════════════════════════════════════════════════════
        void DrawLoggingTab()
        {
            DrawTabTitle("Logging", Tabs[2].Color);

            DrawLogToggle("Unity Logger", Debug.unityLogger.logEnabled, v =>
            {
                Debug.unityLogger.logEnabled = v;
                EditorPrefs.SetBool(PrefUnityLoggerEnabled, v);
            });

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            if (GUILayout.Button("All"))            { CSDebug.LogLevel = CSLogLevel.All; SavePrefs(); }
            if (GUILayout.Button("Warn + Err"))     { CSDebug.LogLevel = CSLogLevel.WarningsAndErrors; SavePrefs(); }
            if (GUILayout.Button("Silent"))         { CSDebug.LogLevel = CSLogLevel.Off; SavePrefs(); }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            DrawLogToggle("Logs",     CSDebug.LogEnabled,      v => { CSDebug.LogEnabled = v; SavePrefs(); });
            DrawLogToggle("Warnings", CSDebug.WarningsEnabled, v => { CSDebug.WarningsEnabled = v; SavePrefs(); });
            DrawLogToggle("Errors",   CSDebug.ErrorsEnabled,   v => { CSDebug.ErrorsEnabled = v; SavePrefs(); });

            GUILayout.Space(10);
            DrawSubSectionLabel("Diagnostic Channels");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            EditorGUILayout.LabelField(
                "Bring-up telemetry for a system that already works - off by default so a past " +
                "development cycle's trace is neither console spam nor deleted knowledge. " +
                "Requires \"Logs\" above to be on.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            foreach (var channel in ChannelRows)
                DrawLogToggle(
                    channel.Label,
                    (CSDebug.VerboseChannels & channel.Flag) != 0,
                    v =>
                    {
                        if (v) CSDebug.VerboseChannels |= channel.Flag;
                        else CSDebug.VerboseChannels &= ~channel.Flag;
                        SavePrefs();
                    });

            GUILayout.Space(10);
            DrawSubSectionLabel("Console Stack Traces");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            EditorGUILayout.LabelField(
                "Full traces on an info log bury the message under ~40 lines of native Unity " +
                "frames. ScriptOnly keeps the managed frames (and double-click-to-source); None " +
                "drops the trace entirely. This is a live override - the project default lives " +
                "in ProjectSettings and applies on the next Editor launch.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            DrawStackTraceRow("Log", LogType.Log);
            DrawStackTraceRow("Warning", LogType.Warning);
            DrawStackTraceRow("Error", LogType.Error);
            DrawStackTraceRow("Exception", LogType.Exception);
        }

        // Channels are listed here rather than reflected off the enum so each one carries a
        // human label; adding a CSLogChannel member without a row here simply leaves it
        // un-toggleable from the toolbox (and CSLogChannel's own doc comment says not to add
        // one until real call sites use it).
        static readonly (CSLogChannel Flag, string Label)[] ChannelRows =
        {
            (CSLogChannel.NetworkFlow,  "[FLOW-n] spawn / session flow"),
            (CSLogChannel.GyroidColony, "[GyroidColony] lattice telemetry"),
        };

        void DrawStackTraceRow(string label, LogType type)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            EditorGUILayout.LabelField(label, _contentLabelStyle, GUILayout.Width(90));
            var current = Application.GetStackTraceLogType(type);
            var next = (StackTraceLogType)EditorGUILayout.EnumPopup(current);
            if (next != current)
            {
                Application.SetStackTraceLogType(type, next);
                EditorPrefs.SetInt(PrefStackTracePrefix + type, (int)next);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: DENSITY (Density Partition Benchmark Runner)
        //
        //  Decoupled from the runner type at compile time via reflection so this
        //  toolbox stays compilable even if the DensityPartitionBenchmark folder
        //  is missing, deleted, or hasn't been re-imported yet. The reflection
        //  surface is tiny (one Type lookup + one method invoke + one field read)
        //  and the runner's API is part of the audit's locked-in contract.
        // ═════════════════════════════════════════════════════════════════════
        const string DensityBenchmarkScenePath = "Assets/_Scenes/Game_TestDesign/DensityPartitionBenchmark.unity";
        const string DensityRunnerTypeName = "CosmicShore.Utility.Tools.DensityPartitionBenchmark.DensityPartitionBenchmarkRunner";
        const string DensityTemporalSimTypeName = "CosmicShore.Utility.Tools.DensityPartitionBenchmark.DensityPartitionTemporalSimRunner";

        void DrawDensityTab()
        {
            DrawTabTitle("Density Partition Benchmark", Tabs[3].Color);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            EditorGUILayout.LabelField(
                "Edit-Mode harness that grades density-search algorithms against a deterministic " +
                "ground truth. See Docs/DENSITY_PARTITIONING_AUDIT.md for the design audit.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            DrawSubSectionLabel("Scene");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            string sceneButtonLabel = System.IO.File.Exists(DensityBenchmarkScenePath)
                ? "Open Benchmark Scene"
                : "Create && Open Benchmark Scene";
            if (GUILayout.Button(sceneButtonLabel))
            {
                EnsureAndOpenBenchmarkScene();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);

            DrawSubSectionLabel("Runner");

            var runnerType = ResolveRunnerType();
            if (runnerType == null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Pad);
                EditorGUILayout.LabelField(
                    "DensityPartitionBenchmarkRunner type not found. The Density benchmark folder may not " +
                    "have been re-imported. Try Assets > Refresh, or right-click the folder and Reimport.",
                    _mutedLabel);
                EditorGUILayout.EndHorizontal();
                return;
            }

            var runner = FindRunnerInOpenScenes(runnerType);
            if (runner == null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Pad);
                EditorGUILayout.LabelField(
                    "No DensityPartitionBenchmarkRunner found in the open scenes. Open the benchmark " +
                    "scene above, or add the component to any GameObject in the active scene.",
                    _mutedLabel);
                EditorGUILayout.EndHorizontal();
                return;
            }

            var go = runner is Component c ? c.gameObject : null;
            int scenarioCount = ReadScenarioCount(runner);
            string lastReport = ReadLastReport(runner);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            EditorGUILayout.LabelField($"Runner: {(go ? go.name : "(unknown)")}   Scenarios: {scenarioCount}");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.65f, 0.85f, 0.7f);
            if (GUILayout.Button("Run All && Dump Report", GUILayout.Height(28)))
            {
                Undo.RecordObject(runner, "Run Density Benchmark");
                InvokeRunAllAndDump(runner);
                EditorUtility.SetDirty(runner);
                if (go) Selection.activeGameObject = go;
            }
            GUI.backgroundColor = prev;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastReport)))
            {
                if (GUILayout.Button("Copy Last Report"))
                {
                    EditorGUIUtility.systemCopyBuffer = lastReport ?? "";
                    CSDebug.Log("[FrogletToolbox] Last density-partition report copied to clipboard.");
                }
                if (GUILayout.Button("Select Runner GameObject") && go)
                {
                    Selection.activeGameObject = go;
                    EditorGUIUtility.PingObject(go);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            // ── Temporal ecology sim ──────────────────────────────────────
            DrawSubSectionLabel("Temporal Ecology Sim");
            var simType = ResolveTemporalSimType();
            var sim = simType != null ? FindRunnerInOpenScenes(simType) : null;
            if (sim == null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Pad);
                EditorGUILayout.LabelField(
                    "No DensityPartitionTemporalSimRunner in the open scenes. Scenes created " +
                    "before this component existed only have the benchmark runner. Click " +
                    "'Add Temporal Sim to Scene' below to attach it to the existing GameObject.",
                    _mutedLabel);
                EditorGUILayout.EndHorizontal();

                // Offer a one-click heal: find the benchmark runner's GameObject in the
                // open scenes and add the temporal sim component to it. Avoids the
                // delete-and-recreate-the-scene path for users who already have a scene.
                if (simType != null && runnerType != null)
                {
                    var benchmarkRunnerForHeal = FindRunnerInOpenScenes(runnerType);
                    GameObject hostGo = benchmarkRunnerForHeal is Component bc ? bc.gameObject : null;
                    using (new EditorGUI.DisabledScope(hostGo == null))
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(Pad);
                        if (GUILayout.Button(hostGo != null
                            ? $"Add Temporal Sim to '{hostGo.name}'"
                            : "Add Temporal Sim to Scene (no benchmark runner found)"))
                        {
                            Undo.AddComponent(hostGo, simType);
                            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hostGo.scene);
                            CSDebug.Log($"[FrogletToolbox] Added DensityPartitionTemporalSimRunner to '{hostGo.name}'. Save the scene to persist.");
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
            else
            {
                var simGo = sim is Component sc ? sc.gameObject : null;
                string simReport = ReadLastReport(sim);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Pad);
                var prevSim = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.65f, 0.80f, 0.90f);
                if (GUILayout.Button("Run Temporal Sim (old grid vs new grid)", GUILayout.Height(28)))
                {
                    Undo.RecordObject(sim, "Run Temporal Sim");
                    InvokeMethod(sim, "RunComparison");
                    EditorUtility.SetDirty(sim);
                    if (simGo) Selection.activeGameObject = simGo;
                }
                GUI.backgroundColor = prevSim;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Pad);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(simReport)))
                {
                    if (GUILayout.Button("Copy Sim Report"))
                    {
                        EditorGUIUtility.systemCopyBuffer = simReport ?? "";
                        CSDebug.Log("[FrogletToolbox] Last temporal-sim report copied to clipboard.");
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(8);

            DrawSubSectionLabel("Notes");
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            EditorGUILayout.LabelField(
                "1. Paste the report back to the prompter - it's the falsifiable correctness contract.\n" +
                "2. The geometric benchmark grades single-query accuracy. The temporal sim runs the " +
                "flora/fauna/phase loop over time and checks whether outer-shell mass stays bounded " +
                "(fauna reach it) or accumulates forever (the shipped ±500m grid is blind to it).\n" +
                "3. Runs are deterministic per seed - a textual diff surfaces real changes.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        static void InvokeMethod(UnityEngine.Object target, string methodName)
        {
            if (target == null) return;
            var method = target.GetType().GetMethod(methodName);
            method?.Invoke(target, null);
        }

        // ── Scene creation (Unity generates a valid .unity file) ──

        static void EnsureAndOpenBenchmarkScene()
        {
            // If the scene exists on disk, just open it. Wrap in try/catch so a
            // corrupt scene file doesn't leave the IMGUI layout state mid-call
            // (the parent OnGUI's Begin/End pairs would otherwise mismatch).
            if (System.IO.File.Exists(DensityBenchmarkScenePath))
            {
                try
                {
                    EditorSceneManager.OpenScene(DensityBenchmarkScenePath, OpenSceneMode.Single);
                    CSDebug.Log($"[FrogletToolbox] Opened {DensityBenchmarkScenePath}.");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[FrogletToolbox] Failed to open {DensityBenchmarkScenePath}: {ex.Message}. " +
                                   $"Delete the file and click again to regenerate.");
                }
                return;
            }

            // Otherwise, generate it. Unity's NewScene + SaveScene produces a valid
            // .unity asset; hand-crafted YAML can omit required SceneRoots metadata
            // in Unity 6 and throw ArgumentException on OpenScene.
            var runnerType = ResolveRunnerType();
            if (runnerType == null)
            {
                DumpDensityPartitionDiagnostic();
                return;
            }

            // Ensure the target folder exists.
            string folder = System.IO.Path.GetDirectoryName(DensityBenchmarkScenePath)
                ?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            // Save the current scene state so we don't lose unsaved edits.
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    CSDebug.Log("[FrogletToolbox] Scene creation cancelled by user.");
                    return;
                }
            }

            var newScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("DensityPartitionBenchmarkRunner");
            go.AddComponent(runnerType);
            // Also add the temporal ecology sim runner if it compiled - same
            // GameObject, so the Density tab finds both in one scene.
            var simType = ResolveTemporalSimType();
            if (simType != null) go.AddComponent(simType);

            bool saved = EditorSceneManager.SaveScene(newScene, DensityBenchmarkScenePath);
            if (!saved)
            {
                Debug.LogError($"[FrogletToolbox] Failed to save benchmark scene to {DensityBenchmarkScenePath}.");
                return;
            }

            AssetDatabase.Refresh();
            CSDebug.Log($"[FrogletToolbox] Created {DensityBenchmarkScenePath}.");
        }

        // ── Reflection helpers (decouple from runner type at compile time) ──

        /// <summary>
        /// Called when ResolveRunnerType() returns null. Prints a diagnostic showing
        /// which assemblies are loaded and which (if any) DensityPartition-named
        /// types are present. Lets us distinguish "files didn't compile" from "files
        /// compiled but namespace lookup is wrong".
        /// </summary>
        static void DumpDensityPartitionDiagnostic()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[FrogletToolbox] Cannot find '{DensityRunnerTypeName}'.");
            sb.AppendLine("Diagnostic - searching every loaded assembly for *DensityPartition* types:");

            int hits = 0;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex)
                {
                    sb.AppendLine($"  {asm.GetName().Name}: ReflectionTypeLoadException ({ex.LoaderExceptions?.Length ?? 0} loader errors)");
                    types = ex.Types;
                }
                if (types == null) continue;
                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (t.FullName == null) continue;
                    if (!t.FullName.Contains("DensityPartition")) continue;
                    sb.AppendLine($"  {asm.GetName().Name} :: {t.FullName}");
                    hits++;
                }
            }
            if (hits == 0)
            {
                sb.AppendLine("  (no DensityPartition* types in any loaded assembly)");
                sb.AppendLine();
                sb.AppendLine("Most likely cause: the .cs files in Assets/_Scripts/Utility/Tools/DensityPartitionBenchmark/");
                sb.AppendLine("failed to compile. Check the Console for any other compile errors (red ⊘ icon, not yellow ⚠).");
                sb.AppendLine("If the Console is clean, try:");
                sb.AppendLine("  1. Right-click Assets/_Scripts/Utility/Tools/DensityPartitionBenchmark → Reimport.");
                sb.AppendLine("  2. Assets > Refresh (Ctrl-R / Cmd-R).");
                sb.AppendLine("  3. Close Unity and delete the Library/ScriptAssemblies folder, then reopen.");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine($"Found {hits} DensityPartition* type(s) but expected name '{DensityRunnerTypeName}' wasn't among them.");
                sb.AppendLine("This means the runner is loaded under a different namespace - paste the diagnostic above and I'll fix it.");
            }

            Debug.LogError(sb.ToString());
        }

        static System.Type _cachedRunnerType;
        static System.Type _cachedTemporalSimType;

        static System.Type ResolveRunnerType()
        {
            if (_cachedRunnerType != null) return _cachedRunnerType;
            _cachedRunnerType = ResolveTypeByName(DensityRunnerTypeName);
            return _cachedRunnerType;
        }

        static System.Type ResolveTemporalSimType()
        {
            if (_cachedTemporalSimType != null) return _cachedTemporalSimType;
            _cachedTemporalSimType = ResolveTypeByName(DensityTemporalSimTypeName);
            return _cachedTemporalSimType;
        }

        static System.Type ResolveTypeByName(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        static UnityEngine.Object FindRunnerInOpenScenes(System.Type runnerType)
        {
            return UnityEngine.Object.FindFirstObjectByType(runnerType, FindObjectsInactive.Include) as UnityEngine.Object;
        }

        static int ReadScenarioCount(UnityEngine.Object runner)
        {
            if (runner == null) return 0;
            var field = runner.GetType().GetField("scenarios");
            if (field?.GetValue(runner) is System.Collections.ICollection list) return list.Count;
            return 0;
        }

        static string ReadLastReport(UnityEngine.Object runner)
        {
            if (runner == null) return "";
            var field = runner.GetType().GetField("lastReport");
            return field?.GetValue(runner) as string ?? "";
        }

        static void InvokeRunAllAndDump(UnityEngine.Object runner)
        {
            if (runner == null) return;
            var method = runner.GetType().GetMethod("RunAllAndDump");
            method?.Invoke(runner, null);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: QUEST (Quest Debug, Non-Quest Game Modes, Intensity)
        // ═════════════════════════════════════════════════════════════════════
        void DrawQuestTab()
        {
            DrawTabTitle("Quest Debug", Tabs[4].Color);

            bool available = Application.isPlaying && GameModeProgressionService.Instance != null;

            if (!available)
            {
                GUILayout.Space(Pad);
                EditorGUILayout.LabelField("Enter Play Mode to use quest tools.", _mutedLabel);
                return;
            }

            var svc = GameModeProgressionService.Instance;

            // ── Quest progression ──
            DrawSubSectionLabel("Quest Progression");
            int maxQuests = svc.QuestList?.Quests.Count ?? 1;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            GUILayout.Label("Unlock to index", GUILayout.Width(100));
            _questIndexInput = EditorGUILayout.TextField(_questIndexInput, GUILayout.Width(36));
            GUILayout.Label($"/ {maxQuests}", GUILayout.Width(32));
            if (GUILayout.Button("Apply", GUILayout.Width(56)))
            {
                if (int.TryParse(_questIndexInput, out int idx))
                    svc.DebugSetProgressToIndex(idx);
                else
                    Debug.LogWarning("[FrogletToolbox] Enter a valid number.");
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            if (GUILayout.Button("Reset All Quests"))
                svc.ResetAllProgress();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            string info = $"<b>Unlocked:</b> {svc.ProgressionData.UnlockedModes.Count}   " +
                          $"<b>Completed:</b> {svc.ProgressionData.CompletedQuests.Count}   " +
                          $"<b>Claimed:</b> {svc.GetClaimedQuestCount()}";
            GUILayout.Label(info, _infoStyle);

            GUILayout.Space(8);

            // ── Non-Quest Game Modes ──
            DrawSubSectionLabel("Non-Quest Game Modes");
            var nonQuestModes = GetNonQuestModes(svc);
            foreach (var mode in nonQuestModes)
            {
                bool isUnlocked = svc.IsGameModeUnlocked(mode);
                DrawLogToggle(mode.ToString(), isUnlocked, v =>
                {
                    svc.DebugSetModeUnlocked(mode, v);
                });
            }

            GUILayout.Space(8);

            // ── Intensity Debug ──
            DrawSubSectionLabel("Intensity Debug");
            var questList = svc.QuestList;

            if (questList == null || questList.Quests.Count == 0)
            {
                GUILayout.Space(Pad);
                EditorGUILayout.LabelField("No quest list configured.", _mutedLabel);
            }
            else
            {
                foreach (var quest in questList.Quests)
                {
                    if (quest == null || quest.IsPlaceholder) continue;

                    var mode = quest.GameMode;
                    int maxUnlocked = svc.GetMaxUnlockedIntensity(mode);

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(Pad);
                    GUILayout.Label(mode.ToString(), GUILayout.Width(120));
                    GUILayout.Label($"Max: {maxUnlocked}", GUILayout.Width(48));

                    if (GUILayout.Button("2", GUILayout.Width(28)))
                        svc.DebugSetMaxIntensity(mode, 2);
                    if (GUILayout.Button("3", GUILayout.Width(28)))
                        svc.DebugSetMaxIntensity(mode, 3);
                    if (GUILayout.Button("4", GUILayout.Width(28)))
                        svc.DebugSetMaxIntensity(mode, 4);

                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: VESSELS
        // ═════════════════════════════════════════════════════════════════════
        void DrawVesselsTab()
        {
            DrawTabTitle("Vessel Unlock", Tabs[5].Color);

            if (!_vesselList)
            {
                var guids = AssetDatabase.FindAssets("t:SO_VesselList");
                if (guids.Length > 0)
                    _vesselList = AssetDatabase.LoadAssetAtPath<SO_VesselList>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (!_vesselList)
            {
                GUILayout.Space(Pad);
                EditorGUILayout.LabelField("No SO_VesselList asset found.", _mutedLabel);
                return;
            }

            foreach (var vessel in _vesselList.VesselList)
            {
                if (vessel == null) continue;

                bool isUnlocked = !vessel.IsLocked;
                DrawLogToggle(vessel.Name, isUnlocked, v =>
                {
                    if (v)
                        VesselUnlockSystem.UnlockVessel(vessel);
                    else
                        VesselUnlockSystem.LockVessel(vessel);
                });
            }

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            if (GUILayout.Button("Unlock All"))
            {
                foreach (var vessel in _vesselList.VesselList)
                {
                    if (vessel != null)
                        VesselUnlockSystem.UnlockVessel(vessel);
                }
            }
            if (GUILayout.Button("Lock All"))
                VesselUnlockSystem.ResetAllUnlocks(_vesselList);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            int balance = VesselUnlockSystem.GetCurrencyBalance();
            string balanceInfo = $"<b>Currency Balance:</b> {balance}";
            GUILayout.Label(balanceInfo, _infoStyle);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: CRYSTALS
        // ═════════════════════════════════════════════════════════════════════
        void DrawCrystalsTab()
        {
            DrawTabTitle("Crystal Currency", Tabs[6].Color);

            bool isPlayMode = Application.isPlaying;
            var service = isPlayMode ? PlayerDataService.Instance : null;

            int currentBalance;
            if (service != null)
                currentBalance = service.GetCrystalBalance();
            else
                currentBalance = EditorPrefs.GetInt(PrefPendingCrystals, 0);

            string balanceLabel = isPlayMode ? "Live Balance" : "Pending (edit-mode)";
            string balanceInfo = $"<b>{balanceLabel}:</b> {currentBalance}";
            GUILayout.Label(balanceInfo, _infoStyle);

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            GUILayout.Label("Amount", GUILayout.Width(52));
            _crystalAmountInput = EditorGUILayout.TextField(_crystalAmountInput, GUILayout.Width(60));
            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                if (int.TryParse(_crystalAmountInput, out int customAmount) && customAmount > 0)
                    AwardDebugCrystals(customAmount);
                else
                    Debug.LogWarning("[FrogletToolbox] Enter a valid positive number.");
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            if (GUILayout.Button("+10"))   AwardDebugCrystals(10);
            if (GUILayout.Button("+50"))   AwardDebugCrystals(50);
            if (GUILayout.Button("+100"))  AwardDebugCrystals(100);
            if (GUILayout.Button("+500"))  AwardDebugCrystals(500);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            if (GUILayout.Button("Set Balance"))
            {
                if (int.TryParse(_crystalAmountInput, out int setAmount) && setAmount >= 0)
                    SetDebugCrystalBalance(setAmount);
                else
                    Debug.LogWarning("[FrogletToolbox] Enter a valid non-negative number.");
            }
            if (GUILayout.Button("Reset to 0"))
                SetDebugCrystalBalance(0);
            EditorGUILayout.EndHorizontal();

            if (!isPlayMode)
            {
                GUILayout.Space(4);
                GUILayout.Space(Pad);
                EditorGUILayout.LabelField("Edit-mode crystals are applied on next Play.", _mutedLabel);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: UGS DATA
        // ═════════════════════════════════════════════════════════════════════
        void DrawUGSDataTab()
        {
            DrawTabTitle("UGS Data View", Tabs[7].Color);

            bool available = Application.isPlaying && UGSDataService.Instance != null && UGSDataService.Instance.IsInitialized;

            if (!available)
            {
                GUILayout.Space(Pad);
                EditorGUILayout.LabelField("Enter Play Mode and sign in to view cloud data.", _mutedLabel);
                return;
            }

            var ds = UGSDataService.Instance;

            DrawUGSSubSection("Player Profile", ref _ugsProfileFoldout, () =>
            {
                var d = ds.Profile?.Data;
                if (d == null) { DrawNoData(); return; }
                DrawFieldHeader("Identity");
                DrawSubField("User ID", d.Identity.UserId);
                DrawSubField("Display Name", d.Identity.DisplayName);
                DrawSubField("Avatar ID", d.Identity.AvatarId.ToString());

                DrawFieldHeader("Economy");
                DrawSubField("Crystal Balance", d.Economy.CrystalBalance.ToString());
                DrawSubField("Lifetime Earned", d.Economy.LifetimeCrystalsEarned.ToString());
                DrawSubField("Lifetime Spent", d.Economy.LifetimeCrystalsSpent.ToString());
                DrawSubField("Unlocked Rewards", d.Economy.UnlockedRewardIds != null && d.Economy.UnlockedRewardIds.Count > 0
                    ? string.Join(", ", d.Economy.UnlockedRewardIds)
                    : "(none)");

                DrawFieldHeader("Lifecycle");
                DrawSubField("First Seen", FormatUtcMs(d.Lifecycle.FirstSeenUtcMs));
                DrawSubField("Last Seen", FormatUtcMs(d.Lifecycle.LastSeenUtcMs));
                DrawSubField("Sessions", d.Lifecycle.SessionCount.ToString());
                DrawSubField("Games Completed", d.Lifecycle.GamesCompleted.ToString());
                DrawSubField("Total Flight Time", $"{d.Lifecycle.TotalFlightTimeSeconds:F1}s");
                DrawSubField("App Version", string.IsNullOrEmpty(d.Lifecycle.LastAppVersion) ? "(unknown)" : d.Lifecycle.LastAppVersion);
                DrawSubField("Platform", string.IsNullOrEmpty(d.Lifecycle.LastPlatform) ? "(unknown)" : d.Lifecycle.LastPlatform);
            });

            DrawUGSSubSection("Mode Stats", ref _ugsStatsFoldout, () =>
            {
                var d = ds.ModeStats?.Data;
                if (d == null || d.Modes == null || d.Modes.Count == 0) { DrawNoData(); return; }

                foreach (var kv in d.Modes)
                {
                    var r = kv.Value;
                    if (r == null) continue;
                    DrawFieldHeader(kv.Key);
                    DrawSubField("Played / Won", $"{r.GamesPlayed} / {r.GamesWon}");
                    DrawSubField("Best Score", r.HasScore ? $"{r.BestScore:F2}" : "(none)");
                    DrawSubField("Flight Time", $"{r.FlightTimeSeconds:F1}s");
                    DrawSubField("Last Played", FormatUtcMs(r.LastPlayedUtcMs));
                }
            });

            DrawUGSSubSection("Game Mode Progression", ref _ugsProgressionFoldout, () =>
            {
                var d = ds.Progression?.Data;
                if (d == null) { DrawNoData(); return; }
                DrawField("Unlocked Modes", d.UnlockedModes != null && d.UnlockedModes.Count > 0
                    ? string.Join(", ", d.UnlockedModes) : "(none)");
                DrawField("Completed Quests", d.CompletedQuests != null && d.CompletedQuests.Count > 0
                    ? string.Join(", ", d.CompletedQuests) : "(none)");
                if (d.BestStats != null && d.BestStats.Count > 0)
                {
                    DrawFieldHeader("Best Stats");
                    foreach (var kv in d.BestStats)
                        DrawSubField(kv.Key, $"{kv.Value:F2}");
                }
            });

            DrawUGSSubSection("Hangar (ownership + vessel stats)", ref _ugsHangarFoldout, () =>
            {
                var d = ds.Hangar?.Data;
                if (d == null) { DrawNoData(); return; }
                DrawField("Selected Vessel", string.IsNullOrEmpty(d.SelectedVessel) ? "(none)" : d.SelectedVessel);
                DrawField("Preferred Vessel", string.IsNullOrEmpty(d.PreferredVessel) ? "(none)" : d.PreferredVessel);

                if (d.Vessels == null || d.Vessels.Count == 0) { DrawNoData(); return; }

                foreach (var kv in d.Vessels)
                {
                    var v = kv.Value;
                    if (v == null) continue;
                    DrawFieldHeader($"{kv.Key}{(v.Unlocked ? "" : "  (locked)")}");
                    DrawSubField("Games Played", v.GamesPlayed.ToString());
                    DrawSubField("Flight Time", $"{v.FlightTimeSeconds:F1}s");
                    DrawSubField("Best Drift", $"{v.BestDriftTimeSeconds:F2}s");
                    DrawSubField("Best Boost", $"{v.BestBoostTimeSeconds:F2}s");
                    DrawSubField("Prisms Damaged", v.TotalPrismsDamaged.ToString());
                    DrawSubField("Last Used", FormatUtcMs(v.LastUsedUtcMs));
                    if (v.Counters != null)
                        foreach (var c in v.Counters)
                            DrawSubField(c.Key, c.Value.ToString());
                }
            });

            DrawUGSSubSection("Episode Progress", ref _ugsEpisodesFoldout, () =>
            {
                var d = ds.Episodes?.Data;
                if (d == null) { DrawNoData(); return; }
                DrawField("Unlocked Episodes", d.UnlockedEpisodes != null && d.UnlockedEpisodes.Count > 0
                    ? string.Join(", ", d.UnlockedEpisodes) : "(none)");
                DrawField("Completed Episodes", d.CompletedEpisodes != null && d.CompletedEpisodes.Count > 0
                    ? string.Join(", ", d.CompletedEpisodes) : "(none)");
                if (d.EpisodeProgress != null && d.EpisodeProgress.Count > 0)
                {
                    DrawFieldHeader("Per-Episode State");
                    foreach (var kv in d.EpisodeProgress)
                    {
                        var s = kv.Value;
                        DrawSubField(kv.Key, $"missions={s.MissionsCompleted}/{s.TotalMissions}, best={s.BestScore}, stars={s.StarsEarned}");
                    }
                }
            });

            DrawUGSSubSection("Player Settings", ref _ugsSettingsFoldout, () =>
            {
                var d = ds.Settings?.Data;
                if (d == null) { DrawNoData(); return; }
                DrawField("Music", $"{(d.MusicEnabled ? "ON" : "OFF")} (level: {d.MusicLevel:F2})");
                DrawField("SFX", $"{(d.SFXEnabled ? "ON" : "OFF")} (level: {d.SFXLevel:F2})");
                DrawField("Haptics", $"{(d.HapticsEnabled ? "ON" : "OFF")} (level: {d.HapticsLevel:F2})");
                DrawField("Invert Y", d.InvertYEnabled ? "ON" : "OFF");
                DrawField("Invert Throttle", d.InvertThrottleEnabled ? "ON" : "OFF");
                DrawField("Joystick Visuals", d.JoystickVisualsEnabled ? "ON" : "OFF");
            });
        }

        /// <summary>Formats a Unix epoch-millisecond UTC timestamp - the project-wide standard.</summary>
        static string FormatUtcMs(long utcMs) => utcMs > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(utcMs).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"
            : "(never)";

        // ── Drawing helpers ──────────────────────────────────────────────────

        void DrawTabTitle(string title, Color accentColor)
        {
            var rect = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
            // Tinted header background
            Color headerBg = Color.Lerp(SectionHeader, accentColor, 0.18f);
            EditorGUI.DrawRect(rect, headerBg);
            // Left accent bar
            var accent = new Rect(rect.x, rect.y, 4, rect.height);
            EditorGUI.DrawRect(accent, accentColor);
            // Title text in accent color
            _sectionTitleStyle.normal.textColor = accentColor;
            var labelRect = new Rect(rect.x + 12, rect.y, rect.width - 12, rect.height);
            GUI.Label(labelRect, title, _sectionTitleStyle);
            GUILayout.Space(6);
        }

        void DrawSubSectionLabel(string title)
        {
            GUILayout.Space(2);
            var rect = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            // Subtle darker strip behind sub-section
            EditorGUI.DrawRect(rect, new Color(0.16f, 0.15f, 0.20f, 0.5f));
            var labelRect = new Rect(rect.x + Pad, rect.y, rect.width - Pad, rect.height);
            // Use active tab color for sub-section text
            Color subColor = (_selectedTab >= 0 && _selectedTab < Tabs.Length)
                ? Color.Lerp(new Color(0.85f, 0.83f, 0.92f), Tabs[_selectedTab].Color, 0.5f)
                : new Color(0.85f, 0.83f, 0.92f);
            _contentBoldStyle.normal.textColor = subColor;
            _contentBoldStyle.fontSize = 12;
            GUI.Label(labelRect, "- " + title, _contentBoldStyle);
            GUILayout.Space(4);
        }

        void DrawSceneButton(string label, string scenePath)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            if (GUILayout.Button(label))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                CSDebug.Log($"[FrogletToolbox] Opening {label}.");
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawMenuItemButton(string label, string menuPath)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            if (GUILayout.Button(label))
                EditorApplication.ExecuteMenuItem(menuPath);
            EditorGUILayout.EndHorizontal();
        }

        void DrawLogToggle(string label, bool current, Action<bool> setter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            bool next = GUILayout.Toggle(current, label);
            if (next != current)
            {
                setter(next);
                Repaint();
            }
            GUILayout.FlexibleSpace();
            DrawBadge(current ? "ON" : "OFF", current ? BadgeOn : BadgeOff);
            EditorGUILayout.EndHorizontal();
        }

        void DrawBadge(string text, Color bg)
        {
            var content = new GUIContent(text);
            var size = _badgeStyle.CalcSize(content);
            var rect = GUILayoutUtility.GetRect(size.x + 8, 18, GUILayout.Width(size.x + 8));
            // Round the badge color slightly toward the active tab color
            Color badgeBg = bg;
            if (_selectedTab >= 0 && _selectedTab < Tabs.Length)
                badgeBg = Color.Lerp(bg, Tabs[_selectedTab].Color, 0.2f);
            EditorGUI.DrawRect(rect, badgeBg);
            GUI.Label(rect, content, _badgeStyle);
        }

        static List<GameModes> GetNonQuestModes(GameModeProgressionService svc)
        {
            var all = (GameModes[])Enum.GetValues(typeof(GameModes));
            return all
                .Where(m => m != GameModes.Random && !svc.IsGameModeInQuestChain(m))
                .OrderBy(m => m.ToString())
                .ToList();
        }

        // ── UGS Data View helpers ─────────────────────────────────────────────

        void DrawUGSSubSection(string title, ref bool foldout, Action drawContent)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            foldout = EditorGUILayout.Foldout(foldout, title, true, EditorStyles.foldoutHeader);
            EditorGUILayout.EndHorizontal();

            if (!foldout) return;

            EditorGUILayout.BeginVertical();
            GUILayout.Space(2);
            drawContent();
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        void DrawField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad + 8);
            EditorGUILayout.LabelField(label, value);
            EditorGUILayout.EndHorizontal();
        }

        void DrawFieldHeader(string label)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad + 8);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawSubField(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad + 20);
            EditorGUILayout.LabelField(label, value);
            EditorGUILayout.EndHorizontal();
        }

        void DrawNoData()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad + 8);
            EditorGUILayout.LabelField("(no data)", _mutedLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ── Crystal debug helpers ──────────────────────────────────────────────

        void AwardDebugCrystals(int amount)
        {
            if (Application.isPlaying)
            {
                var service = PlayerDataService.Instance;
                if (service != null)
                {
                    service.AddCrystals(amount);
                    CSDebug.Log($"[FrogletToolbox] Awarded {amount} crystals via debug.");
                }
                else
                    Debug.LogWarning("[FrogletToolbox] PlayerDataService not available.");
            }
            else
            {
                int pending = EditorPrefs.GetInt(PrefPendingCrystals, 0);
                pending += amount;
                EditorPrefs.SetInt(PrefPendingCrystals, pending);
                CSDebug.Log($"[FrogletToolbox] Queued +{amount} crystals (pending: {pending}).");
            }
            Repaint();
        }

        void SetDebugCrystalBalance(int balance)
        {
            if (Application.isPlaying)
            {
                var service = PlayerDataService.Instance;
                if (service != null)
                {
                    int current = service.GetCrystalBalance();
                    int diff = balance - current;
                    if (diff > 0)
                        service.AddCrystals(diff);
                    else if (diff < 0)
                        service.TrySpendCrystals(-diff);
                    CSDebug.Log($"[FrogletToolbox] Set crystal balance to {balance}.");
                }
                else
                    Debug.LogWarning("[FrogletToolbox] PlayerDataService not available.");
            }
            else
            {
                EditorPrefs.SetInt(PrefPendingCrystals, balance);
                CSDebug.Log($"[FrogletToolbox] Set pending crystals to {balance}.");
            }
            Repaint();
        }

        /// <summary>
        /// Called by PlayerDataService on init to consume any pending debug crystals
        /// that were queued in edit mode.
        /// </summary>
        internal static int ConsumePendingDebugCrystals()
        {
            int pending = EditorPrefs.GetInt(PrefPendingCrystals, 0);
            if (pending > 0)
                EditorPrefs.SetInt(PrefPendingCrystals, 0);
            return pending;
        }

        // ── Prefs persistence ────────────────────────────────────────────────

        static void SavePrefs()
        {
            EditorPrefs.SetBool(PrefLogEnabled, CSDebug.LogEnabled);
            EditorPrefs.SetBool(PrefWarningsEnabled, CSDebug.WarningsEnabled);
            EditorPrefs.SetBool(PrefErrorsEnabled, CSDebug.ErrorsEnabled);
            EditorPrefs.SetInt(PrefVerboseChannels, (int)CSDebug.VerboseChannels);
        }

        internal static void LoadPrefs()
        {
            CSDebug.LogEnabled = EditorPrefs.GetBool(PrefLogEnabled, true);
            CSDebug.WarningsEnabled = EditorPrefs.GetBool(PrefWarningsEnabled, true);
            CSDebug.ErrorsEnabled = EditorPrefs.GetBool(PrefErrorsEnabled, true);

            // Channels default to None so a fresh clone is quiet; the CSDebug static resets on
            // every domain reload, which is why this runs from FrogletTools' [InitializeOnLoad]
            // rather than only when the toolbox window is open.
            CSDebug.VerboseChannels = (CSLogChannel)EditorPrefs.GetInt(PrefVerboseChannels, (int)CSLogChannel.None);

            if (EditorPrefs.HasKey(PrefUnityLoggerEnabled))
                Debug.unityLogger.logEnabled = EditorPrefs.GetBool(PrefUnityLoggerEnabled, true);

            // Stack-trace overrides are per-developer; the project default is in ProjectSettings.
            foreach (LogType type in Enum.GetValues(typeof(LogType)))
            {
                string key = PrefStackTracePrefix + type;
                if (EditorPrefs.HasKey(key))
                    Application.SetStackTraceLogType(type, (StackTraceLogType)EditorPrefs.GetInt(key));
            }
        }
    }
}

#endif
