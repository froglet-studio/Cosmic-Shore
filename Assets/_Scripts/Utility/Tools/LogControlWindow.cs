#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.App.Profile;
using CosmicShore.App.Systems.CloudData;
using CosmicShore.App.Systems.VesselUnlock;
using CosmicShore.Game.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CosmicShore.Utility.Tools
{
    public class LogControlWindow : EditorWindow
    {
        // ── Prefs keys ───────────────────────────────────────────────────────
        const string PrefLogEnabled = "CSDebug_LogEnabled";
        const string PrefWarningsEnabled = "CSDebug_WarningsEnabled";
        const string PrefErrorsEnabled = "CSDebug_ErrorsEnabled";
        const string PrefUnityLoggerEnabled = "CSDebug_UnityLoggerEnabled";
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
            new() { Label = "Quest",      Color = new Color(0.72f, 0.60f, 0.85f, 1f), Draw = w => w.DrawQuestTab() },
            new() { Label = "Vessels",    Color = new Color(0.60f, 0.78f, 0.85f, 1f), Draw = w => w.DrawVesselsTab() },
            new() { Label = "Crystals",   Color = new Color(0.85f, 0.60f, 0.72f, 1f), Draw = w => w.DrawCrystalsTab() },
            new() { Label = "UGS Data",   Color = new Color(0.75f, 0.85f, 0.60f, 1f), Draw = w => w.DrawUGSDataTab() },
        };

        // ── UGS Data sub-foldouts ────────────────────────────────────────────
        bool _ugsProfileFoldout;
        bool _ugsStatsFoldout;
        bool _ugsVesselStatsFoldout;
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
        [NonSerialized] GUIStyle _tabStyle;
        [NonSerialized] GUIStyle _tabActiveStyle;
        [NonSerialized] GUIStyle _sectionTitleStyle;
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

        void BuildStyles()
        {
            if (_stylesBuilt) return;

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

            _tabStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 4),
                margin = new RectOffset(1, 1, 0, 0),
                fixedHeight = 0
            };
            _tabStyle.normal.textColor = new Color(0.70f, 0.68f, 0.78f);

            _tabActiveStyle = new GUIStyle(_tabStyle)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold
            };
            _tabActiveStyle.normal.textColor = Color.white;

            _sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                padding = new RectOffset(4, 0, 2, 2)
            };
            _sectionTitleStyle.normal.textColor = new Color(0.85f, 0.83f, 0.92f);

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
            GUILayout.Space(6);

            if (_selectedTab >= 0 && _selectedTab < Tabs.Length)
                Tabs[_selectedTab].Draw(this);

            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();

            // ── Footer ───────────────────────────────────────────────────────
            var footerRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(footerRect, FooterBg);
            GUI.Label(footerRect, "Froglet Inc. — Cosmic Shore", _mutedLabel);
        }

        void DrawTabBar()
        {
            GUILayout.Space(2);

            // Use a flow layout: multiple rows of tabs that wrap based on window width
            float availableWidth = position.width - 8;
            float x = 4;
            float y = 0;
            float tabHeight = 24;
            float rowSpacing = 2;

            // First pass: measure to figure out total height needed
            float totalHeight = tabHeight;
            float tempX = 4;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var content = new GUIContent(Tabs[i].Label);
                float tabWidth = Mathf.Max(_tabStyle.CalcSize(content).x + 12, 58);
                if (tempX + tabWidth > availableWidth && tempX > 4)
                {
                    tempX = 4;
                    totalHeight += tabHeight + rowSpacing;
                }
                tempX += tabWidth + 2;
            }

            var barRect = GUILayoutUtility.GetRect(0, totalHeight + 6, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, new Color(0.16f, 0.15f, 0.20f, 1f));

            x = 4;
            y = barRect.y + 3;

            for (int i = 0; i < Tabs.Length; i++)
            {
                var tab = Tabs[i];
                var content = new GUIContent(tab.Label);
                float tabWidth = Mathf.Max(_tabStyle.CalcSize(content).x + 12, 58);

                // Wrap to next row if needed
                if (x + tabWidth > availableWidth && x > 4)
                {
                    x = 4;
                    y += tabHeight + rowSpacing;
                }

                var tabRect = new Rect(barRect.x + x, y, tabWidth, tabHeight);
                bool isSelected = (i == _selectedTab);
                bool isHover = tabRect.Contains(Event.current.mousePosition);

                // Draw tab background
                Color bgColor = isSelected ? tab.Color : (isHover ? TabHover : TabInactive);
                EditorGUI.DrawRect(tabRect, bgColor);

                // Draw bottom accent line for selected tab
                if (isSelected)
                {
                    var accentRect = new Rect(tabRect.x, tabRect.yMax - 3, tabRect.width, 3);
                    EditorGUI.DrawRect(accentRect, tab.Color * 1.2f);
                }

                // Draw label
                var style = isSelected ? _tabActiveStyle : _tabStyle;
                if (isSelected)
                    style.normal.textColor = Color.white;
                else
                    style.normal.textColor = isHover ? new Color(0.85f, 0.83f, 0.90f) : new Color(0.70f, 0.68f, 0.78f);

                GUI.Label(tabRect, content, style);

                // Handle click
                if (Event.current.type == EventType.MouseDown && tabRect.Contains(Event.current.mousePosition))
                {
                    _selectedTab = i;
                    _scrollPos = Vector2.zero;
                    Event.current.Use();
                    Repaint();
                }

                x += tabWidth + 2;
            }

            // Repaint on hover for highlight effect
            if (Event.current.type == EventType.MouseMove)
                Repaint();

            var divRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(divRect, DividerColor);
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
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: QUEST (Quest Debug, Non-Quest Game Modes, Intensity)
        // ═════════════════════════════════════════════════════════════════════
        void DrawQuestTab()
        {
            DrawTabTitle("Quest Debug", Tabs[3].Color);

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
            DrawTabTitle("Vessel Unlock", Tabs[4].Color);

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
            DrawTabTitle("Crystal Currency", Tabs[5].Color);

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
            DrawTabTitle("UGS Data View", Tabs[6].Color);

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
                DrawField("User ID", d.userId);
                DrawField("Display Name", d.displayName);
                DrawField("Avatar ID", d.avatarId.ToString());
                DrawField("Crystal Balance", d.crystalBalance.ToString());
                DrawField("Unlocked Rewards", d.unlockedRewardIds != null && d.unlockedRewardIds.Count > 0
                    ? string.Join(", ", d.unlockedRewardIds)
                    : "(none)");
            });

            DrawUGSSubSection("Player Stats", ref _ugsStatsFoldout, () =>
            {
                var d = ds.Stats?.Data;
                if (d == null) { DrawNoData(); return; }
                DrawField("Last Login", d.LastLoginTick > 0
                    ? new DateTime(d.LastLoginTick, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm:ss UTC")
                    : "(never)");

                if (d.BlitzStats?.HighScores != null && d.BlitzStats.HighScores.Count > 0)
                {
                    DrawFieldHeader("Blitz High Scores");
                    foreach (var kv in d.BlitzStats.HighScores)
                        DrawSubField(kv.Key, kv.Value.ToString());
                }
                if (d.MultiHexStats?.BestMultiplayerRaceTimes != null && d.MultiHexStats.BestMultiplayerRaceTimes.Count > 0)
                {
                    DrawFieldHeader("HexRace Best Times");
                    foreach (var kv in d.MultiHexStats.BestMultiplayerRaceTimes)
                        DrawSubField(kv.Key, $"{kv.Value:F2}s");
                }
                if (d.JoustStats?.BestRaceTimes != null && d.JoustStats.BestRaceTimes.Count > 0)
                {
                    DrawFieldHeader("Joust Best Times");
                    foreach (var kv in d.JoustStats.BestRaceTimes)
                        DrawSubField(kv.Key, $"{kv.Value:F2}s");
                }
                if (d.CrystalCaptureStats?.HighScores != null && d.CrystalCaptureStats.HighScores.Count > 0)
                {
                    DrawFieldHeader("Crystal Capture High Scores");
                    foreach (var kv in d.CrystalCaptureStats.HighScores)
                        DrawSubField(kv.Key, kv.Value.ToString());
                }
            });

            DrawUGSSubSection("Vessel Stats", ref _ugsVesselStatsFoldout, () =>
            {
                var d = ds.VesselStats?.Data;
                if (d == null || d.Vessels == null || d.Vessels.Count == 0) { DrawNoData(); return; }

                foreach (var kv in d.Vessels)
                {
                    DrawFieldHeader(kv.Key);
                    var v = kv.Value;
                    DrawSubField("Games Played", v.GamesPlayed.ToString());
                    DrawSubField("Best Drift", $"{v.BestDriftTime:F2}s");
                    DrawSubField("Best Boost", $"{v.BestBoostTime:F2}s");
                    DrawSubField("Prisms Damaged", v.TotalPrismsDamaged.ToString());
                    if (v.Counters != null && v.Counters.Count > 0)
                        foreach (var c in v.Counters)
                            DrawSubField(c.Key, c.Value.ToString());
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

            DrawUGSSubSection("Hangar", ref _ugsHangarFoldout, () =>
            {
                var d = ds.Hangar?.Data;
                if (d == null) { DrawNoData(); return; }
                DrawField("Selected Vessel", string.IsNullOrEmpty(d.SelectedVessel) ? "(none)" : d.SelectedVessel);
                DrawField("Unlocked Vessels", d.UnlockedVessels != null && d.UnlockedVessels.Count > 0
                    ? string.Join(", ", d.UnlockedVessels) : "(none)");
                if (d.VesselPreferences != null && d.VesselPreferences.Count > 0)
                {
                    DrawFieldHeader("Vessel Preferences");
                    foreach (var kv in d.VesselPreferences)
                    {
                        var p = kv.Value;
                        string lastUsed = p.LastUsedTicks > 0
                            ? new DateTime(p.LastUsedTicks, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm")
                            : "never";
                        DrawSubField(kv.Key, $"fav={p.Favorited}, last={lastUsed}");
                    }
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

        // ── Drawing helpers ──────────────────────────────────────────────────

        void DrawTabTitle(string title, Color accentColor)
        {
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SectionHeader);
            var accent = new Rect(rect.x, rect.y, 3, rect.height);
            EditorGUI.DrawRect(accent, accentColor);
            var labelRect = new Rect(rect.x + 10, rect.y, rect.width - 10, rect.height);
            GUI.Label(labelRect, title, _sectionTitleStyle);
            GUILayout.Space(4);
        }

        void DrawSubSectionLabel(string title)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Pad);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
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
            var rect = GUILayoutUtility.GetRect(size.x + 4, 16, GUILayout.Width(size.x + 4));
            EditorGUI.DrawRect(rect, bg);
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
        }

        internal static void LoadPrefs()
        {
            CSDebug.LogEnabled = EditorPrefs.GetBool(PrefLogEnabled, true);
            CSDebug.WarningsEnabled = EditorPrefs.GetBool(PrefWarningsEnabled, true);
            CSDebug.ErrorsEnabled = EditorPrefs.GetBool(PrefErrorsEnabled, true);

            if (EditorPrefs.HasKey(PrefUnityLoggerEnabled))
                Debug.unityLogger.logEnabled = EditorPrefs.GetBool(PrefUnityLoggerEnabled, true);
        }
    }
}

#endif
