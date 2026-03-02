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

        // ── State ────────────────────────────────────────────────────────────
        Vector2 _scrollPos;
        string _questIndexInput = "1";
        bool _sceneFoldout = true;
        bool _loggingFoldout = true;
        bool _questFoldout = true;

        // ── Palette ──────────────────────────────────────────────────────────
        static readonly Color HeaderPurple   = new(0.24f, 0.12f, 0.43f, 1f);   // #3D1F6E
        static readonly Color AccentGreen    = new(0f, 0.90f, 0.46f, 1f);      // #00E676
        static readonly Color SectionBg      = new(0.16f, 0.16f, 0.20f, 1f);
        static readonly Color DividerColor   = new(0.35f, 0.20f, 0.55f, 0.6f);
        static readonly Color BtnNormal      = new(0.22f, 0.22f, 0.28f, 1f);
        static readonly Color BtnHover       = new(0.30f, 0.18f, 0.50f, 1f);
        static readonly Color BadgeOn        = new(0.15f, 0.68f, 0.38f, 1f);
        static readonly Color BadgeOff       = new(0.62f, 0.22f, 0.22f, 1f);
        static readonly Color TextDim        = new(0.65f, 0.65f, 0.70f, 1f);

        // ── Cached styles (built once) ───────────────────────────────────────
        GUIStyle _headerStyle;
        GUIStyle _sectionLabelStyle;
        GUIStyle _styledButton;
        GUIStyle _smallButton;
        GUIStyle _badgeStyle;
        GUIStyle _infoBoxStyle;
        GUIStyle _inputFieldStyle;
        bool _stylesBuilt;

        [MenuItem("FrogletTools/Toolbox", false, 0)]
        static void Open()
        {
            var window = GetWindow<LogControlWindow>("Froglet Toolbox");
            window.minSize = new Vector2(340, 460);
        }

        void OnEnable() => LoadPrefs();
        void OnFocus() => Repaint();

        void BuildStyles()
        {
            if (_stylesBuilt) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = AccentGreen },
                padding = new RectOffset(0, 0, 8, 8)
            };

            _sectionLabelStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                onNormal = { textColor = Color.white },
                focused = { textColor = AccentGreen },
                onFocused = { textColor = AccentGreen },
                active = { textColor = AccentGreen },
                onActive = { textColor = AccentGreen }
            };

            _styledButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                fixedHeight = 28,
                margin = new RectOffset(4, 4, 2, 2),
                normal = { textColor = Color.white },
                hover = { textColor = AccentGreen }
            };

            _smallButton = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fixedHeight = 24,
                margin = new RectOffset(4, 4, 2, 2),
                normal = { textColor = Color.white }
            };

            _badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(6, 6, 2, 2)
            };

            _infoBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                richText = true,
                padding = new RectOffset(8, 8, 6, 6)
            };

            _inputFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 24
            };

            _stylesBuilt = true;
        }

        void OnGUI()
        {
            BuildStyles();

            // ── Banner ───────────────────────────────────────────────────────
            var bannerRect = GUILayoutUtility.GetRect(0, 44, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bannerRect, HeaderPurple);
            GUI.Label(bannerRect, "FROGLET TOOLBOX", _headerStyle);

            // Accent line below banner
            var lineRect = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, AccentGreen);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Space(6);

            // ── Scenes ───────────────────────────────────────────────────────
            DrawSectionHeader("SCENES", ref _sceneFoldout);
            if (_sceneFoldout)
            {
                BeginSection();
                DrawSceneButton("Main Menu",              "Assets/_Scenes/Menu_Main.unity");
                DrawSceneButton("Photo Booth",            "Assets/_Scenes/Tools/PhotoBooth.unity");
                DrawSceneButton("Recording Studio (WIP)", "Assets/_Scenes/Tools/Recording Studio.unity");
                DrawSceneButton("PlayFab Sandbox",        "Assets/_Scenes/TestScenes/Playfab Sandbox Test/Playfab Sandbox.unity");
                EndSection();
            }

            GUILayout.Space(4);

            // ── Logging ──────────────────────────────────────────────────────
            DrawSectionHeader("LOGGING", ref _loggingFoldout);
            if (_loggingFoldout)
            {
                BeginSection();

                // Unity Logger master
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Unity Logger", EditorStyles.label, GUILayout.Width(100));
                bool unityLogger = Debug.unityLogger.logEnabled;
                DrawBadge(unityLogger ? "ON" : "OFF", unityLogger ? BadgeOn : BadgeOff);
                bool newUnity = EditorGUILayout.Toggle(unityLogger, GUILayout.Width(16));
                if (newUnity != unityLogger)
                {
                    Debug.unityLogger.logEnabled = newUnity;
                    EditorPrefs.SetBool(PrefUnityLoggerEnabled, newUnity);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Preset row
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("All", _smallButton))            { CSDebug.LogLevel = CSLogLevel.All; SavePrefs(); }
                if (GUILayout.Button("Warn + Err", _smallButton))     { CSDebug.LogLevel = CSLogLevel.WarningsAndErrors; SavePrefs(); }
                if (GUILayout.Button("Silent", _smallButton))         { CSDebug.LogLevel = CSLogLevel.Off; SavePrefs(); }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);

                // Per-type toggles with inline badges
                DrawLogToggle("Logs",     CSDebug.LogEnabled,      v => { CSDebug.LogEnabled = v; SavePrefs(); });
                DrawLogToggle("Warnings", CSDebug.WarningsEnabled, v => { CSDebug.WarningsEnabled = v; SavePrefs(); });
                DrawLogToggle("Errors",   CSDebug.ErrorsEnabled,   v => { CSDebug.ErrorsEnabled = v; SavePrefs(); });

                EndSection();
            }

            GUILayout.Space(4);

            // ── Quest Debug ──────────────────────────────────────────────────
            DrawSectionHeader("QUEST DEBUG", ref _questFoldout);
            if (_questFoldout)
            {
                BeginSection();

                bool available = Application.isPlaying && GameModeProgressionService.Instance != null;

                if (!available)
                {
                    EditorGUILayout.LabelField("Enter Play Mode to use quest tools.",
                        new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11, normal = { textColor = TextDim } });
                }
                else
                {
                    var svc = GameModeProgressionService.Instance;
                    int maxQuests = svc.QuestList?.Quests.Count ?? 1;

                    // Index input row
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Unlock to index", EditorStyles.label, GUILayout.Width(100));
                    _questIndexInput = EditorGUILayout.TextField(_questIndexInput, _inputFieldStyle, GUILayout.Width(36));
                    GUILayout.Label($"/ {maxQuests}", new GUIStyle(EditorStyles.label) { normal = { textColor = TextDim } }, GUILayout.Width(32));
                    if (GUILayout.Button("Apply", _smallButton, GUILayout.Width(60)))
                    {
                        if (int.TryParse(_questIndexInput, out int idx))
                            svc.DebugSetProgressToIndex(idx);
                        else
                            Debug.LogWarning("[FrogletToolbox] Enter a valid number.");
                    }
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(4);

                    if (GUILayout.Button("Reset All Quests", _styledButton))
                        svc.ResetAllProgress();

                    GUILayout.Space(4);

                    // Status display
                    string info = $"<b>Unlocked:</b> {svc.ProgressionData.UnlockedModes.Count}   " +
                                  $"<b>Completed:</b> {svc.ProgressionData.CompletedQuests.Count}   " +
                                  $"<b>Claimed:</b> {svc.GetClaimedQuestCount()}";
                    GUILayout.Label(info, _infoBoxStyle);
                }

                EndSection();
            }

            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();

            // ── Footer ───────────────────────────────────────────────────────
            var footerRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(footerRect, new Color(0.10f, 0.10f, 0.13f, 1f));
            GUI.Label(footerRect, "Froglet Inc. — Cosmic Shore",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 10, normal = { textColor = TextDim } });
        }

        // ── Drawing helpers ──────────────────────────────────────────────────

        void DrawSectionHeader(string title, ref bool foldout)
        {
            var rect = GUILayoutUtility.GetRect(0, 26, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, HeaderPurple * 0.7f);

            // Left accent bar
            var accentRect = new Rect(rect.x, rect.y, 3, rect.height);
            EditorGUI.DrawRect(accentRect, AccentGreen);

            // Foldout
            var foldRect = new Rect(rect.x + 10, rect.y, rect.width - 10, rect.height);
            foldout = EditorGUI.Foldout(foldRect, foldout, title, true, _sectionLabelStyle);
        }

        void BeginSection()
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Space(4);
            EditorGUI.indentLevel++;
        }

        void EndSection()
        {
            EditorGUI.indentLevel--;
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();

            // Divider
            var div = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(div, DividerColor);
        }

        void DrawSceneButton(string label, string scenePath)
        {
            if (GUILayout.Button(label, _styledButton))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                CSDebug.Log($"[FrogletToolbox] Opening {label}.");
            }
        }

        void DrawLogToggle(string label, bool current, System.Action<bool> setter)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, EditorStyles.label, GUILayout.Width(80));
            DrawBadge(current ? "ON" : "OFF", current ? BadgeOn : BadgeOff);
            bool next = EditorGUILayout.Toggle(current, GUILayout.Width(16));
            if (next != current) setter(next);
            EditorGUILayout.EndHorizontal();
        }

        void DrawBadge(string text, Color bg)
        {
            var content = new GUIContent(text);
            var size = _badgeStyle.CalcSize(content);
            var rect = GUILayoutUtility.GetRect(size.x + 4, 18, GUILayout.Width(size.x + 4));
            EditorGUI.DrawRect(rect, bg);
            GUI.Label(rect, content, _badgeStyle);
        }

        static string OnOff(bool value) => value ? "ON" : "OFF";

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
