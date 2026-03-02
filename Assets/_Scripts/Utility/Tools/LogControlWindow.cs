#if UNITY_EDITOR

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
        bool _sceneFoldout = true;
        bool _loggingFoldout = true;
        bool _questFoldout = true;
        bool _createFoldout;
        bool _multiplayerFoldout;
        bool _utilitiesFoldout;

        // ── Pastel Palette ───────────────────────────────────────────────────
        static readonly Color BannerBg       = new(0.22f, 0.20f, 0.30f, 1f);
        static readonly Color AccentLavender = new(0.68f, 0.62f, 0.85f, 1f);
        static readonly Color AccentMint     = new(0.60f, 0.85f, 0.75f, 1f);
        static readonly Color SectionHeader  = new(0.20f, 0.19f, 0.26f, 1f);
        static readonly Color DividerColor   = new(0.38f, 0.34f, 0.48f, 0.4f);
        static readonly Color BadgeOn        = new(0.45f, 0.72f, 0.58f, 1f);
        static readonly Color BadgeOff       = new(0.72f, 0.45f, 0.48f, 1f);
        static readonly Color TextMuted      = new(0.58f, 0.56f, 0.65f, 1f);
        static readonly Color FooterBg       = new(0.14f, 0.13f, 0.18f, 1f);

        // ── Styles (non-interactive only — buttons/toggles use defaults) ─────
        [System.NonSerialized] GUIStyle _bannerStyle;
        [System.NonSerialized] GUIStyle _sectionLabelStyle;
        [System.NonSerialized] GUIStyle _badgeStyle;
        [System.NonSerialized] GUIStyle _infoStyle;
        [System.NonSerialized] GUIStyle _mutedLabel;
        [System.NonSerialized] bool _stylesBuilt;

        [MenuItem("FrogletTools/Toolbox", false, 0)]
        static void Open()
        {
            var window = GetWindow<LogControlWindow>("Froglet Toolbox");
            window.minSize = new Vector2(340, 520);
        }

        void OnEnable() => LoadPrefs();
        void OnFocus() => Repaint();

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

            _sectionLabelStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            _sectionLabelStyle.normal.textColor = new Color(0.82f, 0.80f, 0.88f);
            _sectionLabelStyle.onNormal.textColor = new Color(0.82f, 0.80f, 0.88f);
            _sectionLabelStyle.focused.textColor = AccentLavender;
            _sectionLabelStyle.onFocused.textColor = AccentLavender;
            _sectionLabelStyle.active.textColor = AccentLavender;
            _sectionLabelStyle.onActive.textColor = AccentLavender;

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

            _stylesBuilt = true;
        }

        void OnGUI()
        {
            BuildStyles();

            // ── Banner ───────────────────────────────────────────────────────
            var bannerRect = GUILayoutUtility.GetRect(0, 38, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bannerRect, BannerBg);
            GUI.Label(bannerRect, "Froglet Toolbox", _bannerStyle);

            var lineRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, AccentLavender * 0.6f);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Space(6);

            // ═════════════════════════════════════════════════════════════════
            //  SCENES
            // ═════════════════════════════════════════════════════════════════
            DrawSectionHeader("Scenes", ref _sceneFoldout);
            if (_sceneFoldout)
            {
                BeginSection();
                DrawSceneButton("Main Menu",              "Assets/_Scenes/Menu_Main.unity");
                DrawSceneButton("Photo Booth",            "Assets/_Scenes/Tools/PhotoBooth.unity");
                DrawSceneButton("Recording Studio (WIP)", "Assets/_Scenes/Tools/Recording Studio.unity");
                DrawSceneButton("PlayFab Sandbox",        "Assets/_Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity");
                EndSection();
            }

            GUILayout.Space(2);

            // ═════════════════════════════════════════════════════════════════
            //  CREATE
            // ═════════════════════════════════════════════════════════════════
            DrawSectionHeader("Create", ref _createFoldout);
            if (_createFoldout)
            {
                BeginSection();
                DrawMenuItemButton("New MiniGame", "FrogletTools/Legacy/Create/MiniGame");
                DrawMenuItemButton("New Class",    "FrogletTools/Legacy/Create/Class");
                EndSection();
            }

            GUILayout.Space(2);

            // ═════════════════════════════════════════════════════════════════
            //  TESTING MULTIPLAYER
            // ═════════════════════════════════════════════════════════════════
            DrawSectionHeader("Testing Multiplayer", ref _multiplayerFoldout);
            if (_multiplayerFoldout)
            {
                BeginSection();

                bool bootstrapEnabled = EditorPrefs.GetBool(PrefBootstrapScene, true);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Pad);
                bool newBootstrap = GUILayout.Toggle(bootstrapEnabled, "Load Bootstrap on Play");
                if (newBootstrap != bootstrapEnabled)
                    EditorPrefs.SetBool(PrefBootstrapScene, newBootstrap);
                GUILayout.FlexibleSpace();
                DrawBadge(bootstrapEnabled ? "ON" : "OFF", bootstrapEnabled ? BadgeOn : BadgeOff);
                EditorGUILayout.EndHorizontal();

                EndSection();
            }

            GUILayout.Space(2);

            // ═════════════════════════════════════════════════════════════════
            //  UTILITIES
            // ═════════════════════════════════════════════════════════════════
            DrawSectionHeader("Utilities", ref _utilitiesFoldout);
            if (_utilitiesFoldout)
            {
                BeginSection();
                DrawMenuItemButton("Component Copier",           "FrogletTools/Legacy/Component Copier");
                DrawMenuItemButton("Dialogue Editor",            "FrogletTools/Legacy/Dialogue Editor");
                DrawMenuItemButton("ElementalFloat Editor",      "FrogletTools/Legacy/ElementalFloat Editor");
                DrawMenuItemButton("Find Asset by GUID",         "FrogletTools/Legacy/Find Asset by GUID");
                DrawMenuItemButton("Force Re-Serialize All SOs", "FrogletTools/Legacy/Force Re-Serialize All ScriptableObjects");
                EndSection();
            }

            GUILayout.Space(2);

            // ═════════════════════════════════════════════════════════════════
            //  LOGGING
            // ═════════════════════════════════════════════════════════════════
            DrawSectionHeader("Logging", ref _loggingFoldout);
            if (_loggingFoldout)
            {
                BeginSection();

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

                EndSection();
            }

            GUILayout.Space(2);

            // ═════════════════════════════════════════════════════════════════
            //  QUEST DEBUG
            // ═════════════════════════════════════════════════════════════════
            DrawSectionHeader("Quest Debug", ref _questFoldout);
            if (_questFoldout)
            {
                BeginSection();

                bool available = Application.isPlaying && GameModeProgressionService.Instance != null;

                if (!available)
                {
                    GUILayout.Space(Pad);
                    EditorGUILayout.LabelField("Enter Play Mode to use quest tools.", _mutedLabel);
                }
                else
                {
                    var svc = GameModeProgressionService.Instance;
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
                }

                EndSection();
            }

            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();

            // ── Footer ───────────────────────────────────────────────────────
            var footerRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(footerRect, FooterBg);
            GUI.Label(footerRect, "Froglet Inc. — Cosmic Shore", _mutedLabel);
        }

        // ── Drawing helpers ──────────────────────────────────────────────────

        void DrawSectionHeader(string title, ref bool foldout)
        {
            var rect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SectionHeader);

            var accent = new Rect(rect.x, rect.y, 3, rect.height);
            EditorGUI.DrawRect(accent, AccentMint * 0.8f);

            var foldRect = new Rect(rect.x + 10, rect.y, rect.width - 10, rect.height);
            foldout = EditorGUI.Foldout(foldRect, foldout, title, true, _sectionLabelStyle);
        }

        void BeginSection()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(4);
        }

        void EndSection()
        {
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();

            var div = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(div, DividerColor);
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

        void DrawLogToggle(string label, bool current, System.Action<bool> setter)
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
