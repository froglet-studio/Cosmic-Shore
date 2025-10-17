// Assets/_Tools/CosmicShore/MiniGameMaker/Editor/CosmicShoreMiniGameMakerWindow.cs

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Tools.MiniGameMaker
{
    public sealed class CosmicShoreMiniGameMakerWindow : EditorWindow
    {
        private enum MakerMode
        {
            Vessel,
            MiniGame
        }

        private enum SubTab
        {
            Overview,
            Config,
            Validate,
            Utilities
        }

        private enum Density
        {
            Compact,
            Normal,
            Relaxed
        }

        private enum ThemeMode
        {
            Auto,
            Light,
            Dark
        }

        [SerializeField] private ColorThemeSO theme;

        private MakerMode _mode = MakerMode.Vessel;
        private SubTab _subTab = SubTab.Overview;
        private Density _density = Density.Normal;
        private ThemeMode _themeMode = ThemeMode.Auto;

        private IToolView _vesselView;
        private IToolView _miniGameView;

        private Vector2 _scroll;

        // Cached styles & spacing
        private GUIStyle _headerStyle;
        private GUIStyle _sectionTitle;
        private float _vSpace = 6f;
        private float _hSpace = 8f;

        // “Layout style” placeholder
        private readonly string[] _styleOptions = { "Default", "Compact", "Spacious" };
        private int _styleIndex = 0;
        
        const string kResBase = "CosmicShore/MiniGameMaker/";
        const string kThemeResName = kResBase + "ColorTheme";
        const string kLibResName   = kResBase + "MiniGamePrefabLibrary";
        const string kPrefTheme    = "CS_MGM_LastThemePath";
        const string kPrefLib      = "CS_MGM_LastLibPath";

        [MenuItem("FrogletTools/Cosmic Shore Mini Game Maker %#m")]
        public static void Open()
        {
            var window = GetWindow<CosmicShoreMiniGameMakerWindow>("Cosmic Shore Mini Game Maker");
            window.minSize = new Vector2(720, 420);
            window.Show();
        }

        private void OnEnable()
        {
            _vesselView   = new VesselMakerView();
            _miniGameView = new MiniGameMakerView();

            // after view exists, try Resources auto-load
            if (_miniGameView is MiniGameMakerView mgv)
            {
                var lib = Resources.Load<MiniGamePrefabLibrarySO>(kLibResName);
                if (lib) mgv.SetLibrary(lib);  
            }

            if (!theme) theme = Resources.Load<ColorThemeSO>("CosmicShore/MiniGameMaker/ColorTheme");

            BuildStyles();
        }

        private void OnValidate()
        {
            EditorApplication.delayCall += () =>
            {
                if (this) BuildStyles();
            };
        }

        private void BuildStyles()
        {
            if (EditorStyles.boldLabel == null) return;
            
            // Theme resolve
            var editorDark = EditorGUIUtility.isProSkin;
            var useDark =
                _themeMode == ThemeMode.Auto ? editorDark :
                _themeMode == ThemeMode.Dark ? true : false;

            Color titleColor = theme
                ? theme.TitleText
                : (useDark ? new Color(0.85f, 0.9f, 1f) : new Color(0.12f, 0.2f, 0.45f));
            Color subtitle = theme
                ? theme.SubtitleText
                : (useDark ? new Color(0.75f, 0.8f, 0.9f) : new Color(0.18f, 0.18f, 0.18f));

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = _density == Density.Compact ? 14 : _density == Density.Normal ? 16 : 18,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = titleColor }
            };

            _sectionTitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = _density == Density.Compact ? 11 : 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = subtitle }
            };

            // Density spacing
            switch (_density)
            {
                case Density.Compact:
                    _vSpace = 4f;
                    _hSpace = 6f;
                    break;
                case Density.Normal:
                    _vSpace = 6f;
                    _hSpace = 8f;
                    break;
                case Density.Relaxed:
                    _vSpace = 10f;
                    _hSpace = 12f;
                    break;
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            GUILayout.Space(_vSpace);
            DrawTopBars();
            GUILayout.Space(_vSpace);
            DrawStyleRow();
            GUILayout.Space(_vSpace);

            // Body now uses pure layout + a scroll view (no BeginArea → no overlap)
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(260, position.width * 0.30f))))
                {
                    if (_mode == MakerMode.Vessel)
                    {
                        EditorGUILayout.LabelField("Vessel Overview", _sectionTitle);
                        EditorGUILayout.HelpBox("Define core identity: class, role, size, FX profile.",
                            MessageType.Info);
                    }
                    else // Mini-Game
                    {
                        var snap = SnapshotActiveMiniGameScene();

                        EditorGUILayout.LabelField("Mini-Game Overview", _sectionTitle);

                        if (!snap.isMiniGameScene)
                        {
                            // BEFORE creation: show only the short helper text
                            EditorGUILayout.HelpBox("Choose template, map goals, pacing, and rewards.",
                                MessageType.Info);
                        }
                        else
                        {
                            // AFTER creation: show live scene details
                            using (new EditorGUILayout.VerticalScope("helpbox"))
                            {
                                EditorGUILayout.LabelField("Current Scene", EditorStyles.miniBoldLabel);
                                EditorGUILayout.LabelField("Name:",
                                    string.IsNullOrEmpty(snap.sceneName) ? "—" : snap.sceneName);
                                EditorGUILayout.LabelField("Path:",
                                    string.IsNullOrEmpty(snap.scenePath) ? "—" : snap.scenePath);
                                EditorGUILayout.Space(4);
                                EditorGUILayout.LabelField("Controller:",
                                    string.IsNullOrEmpty(snap.controllerTypeName) ? "—" : snap.controllerTypeName);
                                EditorGUILayout.LabelField("Spawn Points:", snap.spawnPointCount.ToString());
                                EditorGUILayout.Space(4);

                                DrawOverviewFlag("DependencySpawner", snap.hasDependencySpawner);
                                DrawOverviewFlag("MiniGameMainCamera", snap.hasMiniGameCamera);
                                DrawOverviewFlag("Environment", snap.hasEnvironment);
                                DrawOverviewFlag("GameCanvas", snap.hasGameCanvas);
                                DrawOverviewFlag("PlayerAndShipSpawner", snap.hasPlayerSpawner);
                                // DrawOverviewFlag("ShipSpawner", snap.hasShipSpawner);

                                EditorGUILayout.Space(6);
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    if (GUILayout.Button("Open Scene Folder"))
                                    {
                                        if (!string.IsNullOrEmpty(snap.scenePath))
                                            EditorUtility.RevealInFinder(
                                                System.IO.Path.GetDirectoryName(snap.scenePath));
                                    }

                                    if (GUILayout.Button("Delete Scene"))
                                    {
                                        if (!string.IsNullOrEmpty(snap.scenePath) &&
                                            EditorUtility.DisplayDialog("Delete Mini-Game Scene",
                                                $"Delete scene asset?\n\n{snap.scenePath}\n\nThis cannot be undone.",
                                                "Delete", "Cancel"))
                                        {
                                            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                                            AssetDatabase.DeleteAsset(snap.scenePath);
                                            AssetDatabase.Refresh();
                                        }
                                    }
                                }
                            }
                        }
                    }

                    GUILayout.FlexibleSpace();
                }


                GUILayout.Space(_hSpace);

                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    switch (_mode)
                    {
                        case MakerMode.Vessel: _vesselView?.DrawGUI(_subTab, theme); break;
                        case MakerMode.MiniGame: _miniGameView?.DrawGUI(_subTab, theme); break;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Cosmic Shore Mini Game Maker", _headerStyle);

                GUILayout.FlexibleSpace();

                var newTheme = (ColorThemeSO)EditorGUILayout.ObjectField(theme, typeof(ColorThemeSO), false, GUILayout.MaxWidth(240));
                if (newTheme != theme)
                {
                    theme = newTheme;
                    // persist last used
                    var path = AssetDatabase.GetAssetPath(theme);
                    if (!string.IsNullOrEmpty(path)) EditorPrefs.SetString(kPrefTheme, path);
                    BuildStyles();

                    // Offer one-click move into Resources for auto-load next time
                    if (!string.IsNullOrEmpty(path) && !path.Contains("/Resources/"))
                    {
                        if (GUILayout.Button("Move to Resources", EditorStyles.miniButton, GUILayout.Width(130)))
                            MoveAssetToResources(path, "ColorTheme.asset");
                    }
                }

            }
        }

        private void DrawTopBars()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Left: mode tabs
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawToolbarToggle(ref _mode, MakerMode.Vessel, "Vessel Maker");
                    DrawToolbarToggle(ref _mode, MakerMode.MiniGame, "Mini-Game Maker");
                }

                GUILayout.FlexibleSpace();

                // Right: sub tabs
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSubTabButton(SubTab.Overview, "Overview");
                    DrawSubTabButton(SubTab.Config, "Config");
                    DrawSubTabButton(SubTab.Validate, "Validate");
                    DrawSubTabButton(SubTab.Utilities, "Utilities");
                }
            }
        }

        private void DrawToolbarToggle<T>(ref T state, T value, string label) where T : System.Enum
        {
            bool on = Equals(state, value);
            if (GUILayout.Toggle(on, label, EditorStyles.toolbarButton) != on)
            {
                state = value;
                GUI.FocusControl(null);
                Repaint();
            }
        }

        private void DrawSubTabButton(SubTab tab, string label)
        {
            bool on = _subTab == tab;
            if (GUILayout.Toggle(on, label, EditorStyles.toolbarButton) != on)
            {
                _subTab = tab;
                GUI.FocusControl(null);
                Repaint();
            }
        }

        private void DrawStyleRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Layout Style", _sectionTitle, GUILayout.Width(90));
                _styleIndex = EditorGUILayout.Popup(_styleIndex, _styleOptions, GUILayout.MaxWidth(160));

                GUILayout.Space(_hSpace);

                GUILayout.Label("UI Density", _sectionTitle, GUILayout.Width(80));
                var newDensity = (Density)EditorGUILayout.Popup((int)_density, new[] { "Compact", "Normal", "Relaxed" },
                    GUILayout.MaxWidth(120));
                if (newDensity != _density)
                {
                    _density = newDensity;
                    BuildStyles();
                }

                GUILayout.Space(_hSpace);

                GUILayout.Label("Theme", _sectionTitle, GUILayout.Width(50));
                var newThemeMode = (ThemeMode)EditorGUILayout.Popup((int)_themeMode, new[] { "Auto", "Light", "Dark" },
                    GUILayout.MaxWidth(100));
                if (newThemeMode != _themeMode)
                {
                    _themeMode = newThemeMode;
                    BuildStyles();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Docs", EditorStyles.miniButton, GUILayout.Width(60)))
                    EditorUtility.DisplayDialog("Coming Soon", "Documentation panel will be added in Utilities.", "OK");
            }
        }

        private void DrawOverviewFlag(string label, bool present)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = present ? new Color(0.35f, 0.85f, 0.45f) : new Color(0.95f, 0.35f, 0.35f) }
            };
            EditorGUILayout.LabelField($"{(present ? "✓" : "✗")} {label}", style);
        }

        private void MoveAssetToResources(string assetPath, string targetFileName)
        {
            var targetDir = "Assets/Resources/" + kResBase.TrimEnd('/');
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            var targetPath = Path.Combine(targetDir, targetFileName).Replace("\\","/");
            var result = AssetDatabase.MoveAsset(assetPath, targetPath);
            if (!string.IsNullOrEmpty(result))
                Debug.LogError("Move failed: " + result);
            else
                AssetDatabase.SaveAssets();
        }


        private struct MiniGameSceneInfo
        {
            public bool isMiniGameScene;
            public string sceneName;
            public string scenePath;
            public string controllerTypeName;
            public int spawnPointCount;
            public bool hasDependencySpawner;
            public bool hasMiniGameCamera;
            public bool hasEnvironment;
            public bool hasGameCanvas;
            public bool hasPlayerSpawner;
            public bool hasShipSpawner;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }

            return null;
        }

        private static MiniGameSceneInfo SnapshotActiveMiniGameScene()
        {
            var info = new MiniGameSceneInfo();
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                info.isMiniGameScene = false;
                return info;
            }

            info.sceneName = scene.name;
            info.scenePath = scene.path;

            // Detect controller that derives from SinglePlayerMiniGameControllerBase
            var baseType = FindType("CosmicShore.Game.Arcade.SinglePlayerMiniGameControllerBase");
            var roots = scene.GetRootGameObjects();

            var game = roots.FirstOrDefault(r => r.name == "Game");
            if (game != null && baseType != null)
            {
                var ctrl = game.GetComponents<Component>()
                    .FirstOrDefault(c => c && baseType.IsAssignableFrom(c.GetType()));
                if (ctrl)
                {
                    info.isMiniGameScene = true;
                    info.controllerTypeName = ctrl.GetType().FullName;
                }
            }

            // Commons / spawners
            info.hasDependencySpawner = roots.Any(r => r.name == "DependencySpawner");
            info.hasMiniGameCamera = roots.Any(r => r.name == "MiniGameMainCamera");
            info.hasEnvironment = roots.Any(r => r.name == "Environment");
            info.hasGameCanvas = roots.Any(r => r.name == "GameCanvas");
            info.hasPlayerSpawner = roots.Any(r => r.name == "PlayerandShipSpawner");
            // info.hasShipSpawner = roots.Any(r => r.name == "ShipSpawner");

            // Spawn points
            var spRoot = game ? game.transform.Find("SpawnPoints") : null;
            int spCount = 0;
            if (spRoot)
            {
                if (spRoot.Find("1")) spCount++;
                if (spRoot.Find("2")) spCount++;
                if (spCount == 0) spCount = spRoot.childCount; // fallback
            }

            info.spawnPointCount = spCount;

            return info;
        }
    }
}