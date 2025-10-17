using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Tools.MiniGameMaker
{
    public sealed class MiniGameMakerView : IToolView
    {
        // Session UI state
        string _sceneName = "NewMiniGame";
        MiniGamePrefabLibrarySO _library;
        bool _createAsDraft;
        string _lastScenePath;
        string _lastControllerClass;
        int _spawnPointCount = 2;

        // Cached “Config” targets (lazy)
        GameObject _gameRoot;
        Object _scoreTracker;
        Object _turnMonitorController;
        Object _timeBasedTurnMonitor;
        Object _localVolumeUIController;
        Object _playerSpawner;
        Object _shipSpawner;

        public void DrawGUI(object subTab, ColorThemeSO theme)
        {
            switch (subTab.ToString())
            {
                case "Overview": DrawOverview(); break;
                case "Config": DrawConfig(); break;
                case "Validate": DrawValidate(); break;
                case "Utilities": DrawUtilities(); break;
            }
        }

        void DrawOverview()
        {
            EditorGUILayout.LabelField("Mini-Game Wizard", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Layout Style", EditorStyles.miniBoldLabel, GUILayout.Width(90));
                // (keep your style row above if present)
            }

            _sceneName = EditorGUILayout.TextField("Scene / Mode Name", _sceneName);
            _library = (MiniGamePrefabLibrarySO)EditorGUILayout.ObjectField("Prefab Library", _library, typeof(MiniGamePrefabLibrarySO), false);

            var libPath = _library ? AssetDatabase.GetAssetPath(_library) : null;
            if (_library && !string.IsNullOrEmpty(libPath))
            {
                EditorPrefs.SetString("CS_MGM_LastLibPath", libPath);
                if (!libPath.Contains("/Resources/"))
                {
                    if (GUILayout.Button("Move Library to Resources", EditorStyles.miniButton))
                    {
                        var window = EditorWindow.GetWindow<CosmicShoreMiniGameMakerWindow>();
     
                        var method = typeof(CosmicShoreMiniGameMakerWindow).GetMethod("MoveAssetToResources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        method?.Invoke(window, new object[] { libPath, "MiniGamePrefabLibrary.asset" });
                    }
                }
            }

            _spawnPointCount =
                EditorGUILayout.IntPopup("Spawn Points", _spawnPointCount, new[] { "1", "2" }, new[] { 1, 2 });
            _createAsDraft = EditorGUILayout.ToggleLeft("Create as Draft (don’t save yet)", _createAsDraft);

            EditorGUILayout.Space();

            using (new EditorGUILayout.VerticalScope("helpbox"))
            {
                EditorGUILayout.LabelField("Checklist (locked items are created automatically):",
                    EditorStyles.miniBoldLabel);
                DrawLock("DependencySpawner");
                DrawLock("MiniGameCamera");
                DrawLock("Environment");
                DrawLock("GameCanvas");
                DrawDot("Game root + NetworkObject + XController");
                DrawDot("SpawnPoints /1 /2");
                DrawDot("PlayerSpawner (configurable)");
                DrawDot("ShipSpawner (configurable)");
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_sceneName) || !_library))
            {
                if (GUILayout.Button("Create Scene"))
                {
                    string path;
                    var res = MiniGameSceneAssembler.CreateAndSave(_sceneName, _library, _createAsDraft, out path,
                        _spawnPointCount);
                    _lastScenePath = SceneManager.GetActiveScene().path;
                    _lastControllerClass = res.controllerClassName;
                    _gameRoot = GameObject.Find("Game");
                    EditorApplication.delayCall += () => EditorWindow.focusedWindow?.Repaint();
                }
            }

            // Quick save button
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save Scene")) EditorSceneManager.SaveOpenScenes();

                // NEW: Delete Scene
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(SceneManager.GetActiveScene().path)))
                {
                    if (GUILayout.Button("Delete Scene"))
                    {
                        var activePath = SceneManager.GetActiveScene().path;
                        if (!string.IsNullOrEmpty(activePath) &&
                            EditorUtility.DisplayDialog("Delete Mini-Game Scene",
                                $"Delete scene asset?\n\n{activePath}\n\nThis cannot be undone.", "Delete", "Cancel"))
                        {
                            // Close and delete
                            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                            AssetDatabase.DeleteAsset(activePath);
                            AssetDatabase.Refresh();
                            _lastScenePath = "";
                            _lastControllerClass = "";
                        }
                    }
                }

                if (GUILayout.Button("Run Validate")) EditorWindow.focusedWindow?.Repaint();
            }
        }

        void DrawConfig()
        {
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            if (!EnsureTargets())
            {
                EditorGUILayout.HelpBox("Open or create a mini-game scene to configure.", MessageType.Info);
                return;
            }

            RefreshSerializedObjects();
            if (GUILayout.Button("Auto-Assign From Game"))
            {
                AutoAssignFromGame();
                RefreshSerializedObjects();
            }

            var controller = FindControllerOnGame(); // component deriving from SinglePlayerMiniGameControllerBase
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Mini-Game Controller", EditorStyles.miniBoldLabel);
                if (controller)
                {
                    var so = new SerializedObject(controller);
                    so.Update();

                    var it = so.GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.name == "m_Script") continue; // skip script field
                        EditorGUILayout.PropertyField(it, true);
                    }

                    so.ApplyModifiedProperties();
                }
                else
                {
                    EditorGUILayout.HelpBox("Controller not found on 'Game'.", MessageType.Warning);
                }
            }
            
            // ScoreTracker
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("ScoreTracker", EditorStyles.miniBoldLabel);
                if (_soScore != null)
                {
                    _soScore.Update();
                    EditorGUILayout.PropertyField(_soScore.FindProperty("miniGameData"));
                    EditorGUILayout.PropertyField(_soScore.FindProperty("golfRules"));
                    EditorGUILayout.PropertyField(_soScore.FindProperty("scoringConfigs"), true); // array drawer
                    _soScore.ApplyModifiedProperties();
                }
                else EditorGUILayout.HelpBox("ScoreTracker not found on Game.", MessageType.Warning);
            }

            // Turn Monitor Controller
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("TurnMonitorController", EditorStyles.miniBoldLabel);
                if (_soTurnCtrl != null)
                {
                    _soTurnCtrl.Update();
                    EditorGUILayout.PropertyField(_soTurnCtrl.FindProperty("miniGameData"));
                    EditorGUILayout.PropertyField(_soTurnCtrl.FindProperty("monitors"), true);
                    _soTurnCtrl.ApplyModifiedProperties();
                }
                else EditorGUILayout.HelpBox("TurnMonitorController not found on Game.", MessageType.Warning);
            }

            // Time Based Turn Monitor
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("TimeBasedTurnMonitor", EditorStyles.miniBoldLabel);
                if (_soTimeMon != null)
                {
                    _soTimeMon.Update();
                    EditorGUILayout.PropertyField(_soTimeMon.FindProperty("duration"));
                    _soTimeMon.ApplyModifiedProperties();
                }
                else EditorGUILayout.HelpBox("TimeBasedTurnMonitor not found.", MessageType.Warning);
            }

            // Local Volume UI Controller
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("LocalVolumeUIController", EditorStyles.miniBoldLabel);
                if (_soLocalUI != null)
                {
                    _soLocalUI.Update();
                    EditorGUILayout.PropertyField(_soLocalUI.FindProperty("miniGameData"));
                    EditorGUILayout.PropertyField(_soLocalUI.FindProperty("volumeUI"));
                    _soLocalUI.ApplyModifiedProperties();
                }
                else EditorGUILayout.HelpBox("LocalVolumeUIController not found on Game.", MessageType.Warning);
            }

            if (GUILayout.Button("Save Scene")) EditorSceneManager.SaveOpenScenes();
        }


        void DrawValidate()
        {
            var passStyle = new GUIStyle(EditorStyles.label)
                { normal = { textColor = new Color(0.35f, 0.85f, 0.45f) } };
            var warnStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.7f, 0.25f) } };
            var failStyle = new GUIStyle(EditorStyles.label)
                { normal = { textColor = new Color(0.95f, 0.35f, 0.35f) } };

            foreach (var v in MiniGameValidatorRunner.GetAll())
            {
                var (sev, msg) = v.Check();
                using (new EditorGUILayout.HorizontalScope())
                {
                    var style = sev == Severity.Pass ? passStyle : sev == Severity.Warning ? warnStyle : failStyle;
                    GUILayout.Label(sev.ToString().ToUpper(), style, GUILayout.Width(50));
                    GUILayout.Label($"{v.Name}: {msg}");
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(sev == Severity.Pass))
                        if (GUILayout.Button("Fix", GUILayout.Width(60)))
                        {
                            v.Fix();
                        }
                }
            }
        }

        void DrawUtilities()
        {
            if (GUILayout.Button("Open Scene Folder"))
            {
                var active = SceneManager.GetActiveScene().path;
                if (!string.IsNullOrEmpty(active))
                {
                    var folder = System.IO.Path.GetDirectoryName(active);
                    EditorUtility.RevealInFinder(folder);
                }
            }
        }

        // helpers
        void DrawLock(string label) => EditorGUILayout.LabelField($"🔒 {label}");
        void DrawDot(string label) => EditorGUILayout.LabelField($"• {label}");

        string Badge(Severity s) => s switch
        {
            Severity.Pass => "PASS",
            Severity.Warning => "WARN",
            _ => "FAIL"
        };

        bool EnsureTargets()
        {
            _gameRoot = GameObject.Find("Game");
            if (!_gameRoot) return false;

            // fetch by type name to avoid direct compile deps in the tool
            _scoreTracker = _scoreTracker ? _scoreTracker : _gameRoot.GetComponents<Component>().FirstOrDefault(c => c && c.GetType().Name == "ScoreTracker");
            _turnMonitorController = _turnMonitorController ? _turnMonitorController : _gameRoot.GetComponents<Component>().FirstOrDefault(c => c && c.GetType().Name == "TurnMonitorController");
            _timeBasedTurnMonitor = _timeBasedTurnMonitor ? _timeBasedTurnMonitor : _gameRoot.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c && c.GetType().Name == "TimeBasedTurnMonitor");
            _localVolumeUIController = _localVolumeUIController ? _localVolumeUIController : _gameRoot.GetComponents<Component>().FirstOrDefault(c => c && c.GetType().Name == "LocalVolumeUIController");

            return true;
        }


        // MiniGameMakerView helpers

        SerializedObject _soScore;
        SerializedObject _soTurnCtrl;
        SerializedObject _soTimeMon;
        SerializedObject _soLocalUI;

        public void TryLoadLibraryFromResources(string resName, string prefKey)
        {
            if (!_library) _library = Resources.Load<MiniGamePrefabLibrarySO>(resName);
            if (!_library)
            {
                var last = EditorPrefs.GetString(prefKey, "");
                if (!string.IsNullOrEmpty(last))
                    _library = AssetDatabase.LoadAssetAtPath<MiniGamePrefabLibrarySO>(last);
            }
        }

        public void SetLibrary(MiniGamePrefabLibrarySO library) { _library = library; }

        void RefreshSerializedObjects()
        {
            if (_scoreTracker) _soScore = new SerializedObject(_scoreTracker);
            if (_turnMonitorController) _soTurnCtrl = new SerializedObject(_turnMonitorController);
            if (_timeBasedTurnMonitor) _soTimeMon = new SerializedObject(_timeBasedTurnMonitor);
            if (_localVolumeUIController)
                _soLocalUI = new SerializedObject(_localVolumeUIController);
        }

        void AutoAssignFromGame()
        {
            if (!_gameRoot) return;

            // ScoreTracker: assign miniGameData from any provider in scene, else leave
            if (_soScore != null)
            {
                TryAssignObject(_soScore, "miniGameData",
                    FindFromProvidersScriptable("MiniGameDataProvider", "miniGameData"));
                TryAssignBool(_soScore, "golfRules", false); // default false
                _soScore.ApplyModifiedPropertiesWithoutUndo();
            }

            // TurnMonitorController: miniGameData + monitors list includes TimeBasedTurnMonitor under Game
            if (_soTurnCtrl != null)
            {
                TryAssignObject(_soTurnCtrl, "miniGameData",
                    FindFromProvidersScriptable("MiniGameDataProvider", "miniGameData"));
                var monList = _soTurnCtrl.FindProperty("monitors");
                if (monList != null && monList.isArray)
                {
                    var tbm = (_timeBasedTurnMonitor as Component);
                    if (tbm)
                    {
                        bool present = false;
                        for (int i = 0; i < monList.arraySize; i++)
                            present |= monList.GetArrayElementAtIndex(i).objectReferenceValue == tbm;
                        if (!present)
                        {
                            monList.arraySize++;
                            monList.GetArrayElementAtIndex(monList.arraySize - 1).objectReferenceValue = tbm;
                        }
                    }
                }

                _soTurnCtrl.ApplyModifiedPropertiesWithoutUndo();
            }

            // TimeBasedTurnMonitor: set default duration
            if (_soTimeMon != null)
            {
                TryAssignFloat(_soTimeMon, "duration", 60f);
                _soTimeMon.ApplyModifiedPropertiesWithoutUndo();
            }

            // LocalVolumeUIController: miniGameData + volumeUI from provider on GameCanvas
            if (_soLocalUI != null)
            {
                TryAssignObject(_soLocalUI, "miniGameData",
                    FindFromProvidersScriptable("MiniGameDataProvider", "miniGameData"));
                TryAssignObject(_soLocalUI, "volumeUI", FindFromProvidersComponent("VolumeUIProvider", "volumeUI"));
                _soLocalUI.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        Object FindFromProvidersScriptable(string providerTypeName, string fieldName)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var provider = go.GetComponent(providerTypeName);
                if (!provider) continue;
                var so = new SerializedObject(provider);
                var sp = so.FindProperty(fieldName);
                if (sp != null && sp.objectReferenceValue) return sp.objectReferenceValue;
            }

            return null;
        }

        Object FindFromProvidersComponent(string providerTypeName, string fieldName)
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var provider = go.GetComponent(providerTypeName);
                if (!provider) continue;
                var so = new SerializedObject(provider);
                var sp = so.FindProperty(fieldName);
                if (sp != null && sp.objectReferenceValue) return sp.objectReferenceValue;
            }

            return null;
        }

        static void TryAssignObject(SerializedObject so, string propName, UnityEngine.Object value)
        {
            var p = so.FindProperty(propName);
            if (p != null && value && p.objectReferenceValue == null) p.objectReferenceValue = value;
        }

        static void TryAssignBool(SerializedObject so, string propName, bool value)
        {
            var p = so.FindProperty(propName);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value;
        }

        static void TryAssignFloat(SerializedObject so, string propName, float value)
        {
            var p = so.FindProperty(propName);
            if (p != null && p.propertyType == SerializedPropertyType.Float && Mathf.Approximately(p.floatValue, 0f))
                p.floatValue = value;
        }
        
        Component FindControllerOnGame()
        {
            var game = GameObject.Find("Game");
            if (!game) return null;
            var baseType = System.Type.GetType("CosmicShore.Game.Arcade.SinglePlayerMiniGameControllerBase, Assembly-CSharp", false);
            if (baseType == null) return null;
            return game.GetComponents<Component>().FirstOrDefault(c => c && baseType.IsAssignableFrom(c.GetType()));
        }

    }
}