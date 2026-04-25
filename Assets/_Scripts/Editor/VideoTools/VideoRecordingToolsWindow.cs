using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Utility.Recording;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Editor.VideoTools
{
    /// <summary>
    /// FrogletTools tab for video recording utilities. Currently exposes the
    /// "Macros" tab — a modular keyboard-triggered action runner intended to
    /// be used while capturing gameplay footage.
    /// </summary>
    public class VideoRecordingToolsWindow : EditorWindow
    {
        enum Tab { Macros = 0 }

        static readonly string[] TabLabels = { "Macros" };

        Tab _activeTab = Tab.Macros;
        VideoMacroLibrarySO _library;
        Vector2 _scroll;

        // Cached list of action types that subclass VideoMacroAction.
        Type[] _actionTypes;
        string[] _actionTypeNames;

        [MenuItem("FrogletTools/Video Recording Tools")]
        public static void ShowWindow()
        {
            var window = GetWindow<VideoRecordingToolsWindow>("Video Recording Tools");
            window.minSize = new Vector2(420, 320);
        }

        void OnEnable()
        {
            CacheActionTypes();
            TryAutoLoadLibrary();
        }

        void CacheActionTypes()
        {
            _actionTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t != null
                            && !t.IsAbstract
                            && typeof(VideoMacroAction).IsAssignableFrom(t)
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToArray();

            _actionTypeNames = _actionTypes.Select(t =>
            {
                var instance = (VideoMacroAction)Activator.CreateInstance(t);
                return instance.DisplayName;
            }).ToArray();
        }

        void TryAutoLoadLibrary()
        {
            if (_library != null) return;
            var guids = AssetDatabase.FindAssets("t:VideoMacroLibrarySO");
            if (guids.Length == 0) return;
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _library = AssetDatabase.LoadAssetAtPath<VideoMacroLibrarySO>(path);
        }

        void OnGUI()
        {
            DrawTabBar();
            EditorGUILayout.Space(6);

            switch (_activeTab)
            {
                case Tab.Macros:
                    DrawMacrosTab();
                    break;
            }
        }

        void DrawTabBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                for (int i = 0; i < TabLabels.Length; i++)
                {
                    var isActive = (int)_activeTab == i;
                    if (GUILayout.Toggle(isActive, TabLabels[i], EditorStyles.toolbarButton) && !isActive)
                        _activeTab = (Tab)i;
                }
                GUILayout.FlexibleSpace();
            }
        }

        void DrawMacrosTab()
        {
            DrawLibraryHeader();

            if (_library == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign or create a Video Macro Library to start adding macros.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);
            DrawSceneSetupHelpers();
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawMacroList();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Add Macro", GUILayout.Height(24)))
            {
                Undo.RecordObject(_library, "Add Macro");
                _library.Macros.Add(new VideoMacro());
                EditorUtility.SetDirty(_library);
            }
        }

        void DrawLibraryHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _library = (VideoMacroLibrarySO)EditorGUILayout.ObjectField(
                    "Library", _library, typeof(VideoMacroLibrarySO), false);

                if (GUILayout.Button("New", GUILayout.Width(60)))
                    CreateNewLibrary();
            }
        }

        void CreateNewLibrary()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Video Macro Library",
                "VideoMacroLibrary",
                "asset",
                "Choose a location for the new library asset.");
            if (string.IsNullOrEmpty(path)) return;

            var asset = ScriptableObject.CreateInstance<VideoMacroLibrarySO>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _library = asset;
            EditorGUIUtility.PingObject(asset);
        }

        void DrawSceneSetupHelpers()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Spawn Runner in Active Scene"))
                    SpawnRunnerInScene();

                using (new EditorGUI.DisabledScope(_library == null))
                {
                    if (GUILayout.Button("Ping Library Asset"))
                        EditorGUIUtility.PingObject(_library);
                }
            }
        }

        void SpawnRunnerInScene()
        {
            var existing = UnityEngine.Object.FindFirstObjectByType<VideoMacroRunner>();
            if (existing != null)
            {
                existing.Library = _library;
                EditorUtility.SetDirty(existing);
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var go = new GameObject("VideoMacroRunner");
            var runner = go.AddComponent<VideoMacroRunner>();
            runner.Library = _library;
            Undo.RegisterCreatedObjectUndo(go, "Spawn Video Macro Runner");
            Selection.activeGameObject = go;
        }

        void DrawMacroList()
        {
            for (int i = 0; i < _library.Macros.Count; i++)
            {
                var macro = _library.Macros[i];
                if (macro == null)
                {
                    _library.Macros[i] = new VideoMacro();
                    macro = _library.Macros[i];
                }

                DrawMacro(i, macro);
                EditorGUILayout.Space(2);
            }
        }

        void DrawMacro(int index, VideoMacro macro)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Macro {index}", EditorStyles.boldLabel, GUILayout.Width(80));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                    {
                        Undo.RecordObject(_library, "Remove Macro");
                        _library.Macros.RemoveAt(index);
                        EditorUtility.SetDirty(_library);
                        return;
                    }
                }

                EditorGUI.BeginChangeCheck();
                macro.Name = EditorGUILayout.TextField("Name", macro.Name);
                macro.TriggerKey = (Key)EditorGUILayout.EnumPopup("Trigger Key", macro.TriggerKey);
                if (EditorGUI.EndChangeCheck())
                    EditorUtility.SetDirty(_library);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

                DrawActionList(macro);
                DrawAddActionRow(macro);
            }
        }

        void DrawActionList(VideoMacro macro)
        {
            for (int i = 0; i < macro.Actions.Count; i++)
            {
                var action = macro.Actions[i];

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var label = action != null ? action.DisplayName : "<null>";
                        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("X", GUILayout.Width(22)))
                        {
                            Undo.RecordObject(_library, "Remove Action");
                            macro.Actions.RemoveAt(i);
                            EditorUtility.SetDirty(_library);
                            return;
                        }
                    }

                    if (action != null)
                    {
                        EditorGUI.indentLevel++;
                        action.DrawEditor(_library);
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }

        int _newActionTypeIndex;

        void DrawAddActionRow(VideoMacro macro)
        {
            if (_actionTypes == null || _actionTypes.Length == 0)
            {
                EditorGUILayout.HelpBox("No VideoMacroAction subclasses found.", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _newActionTypeIndex = EditorGUILayout.Popup(_newActionTypeIndex, _actionTypeNames);
                if (GUILayout.Button("Add Action", GUILayout.Width(110)))
                {
                    Undo.RecordObject(_library, "Add Action");
                    var newAction = (VideoMacroAction)Activator.CreateInstance(_actionTypes[_newActionTypeIndex]);
                    macro.Actions.Add(newAction);
                    EditorUtility.SetDirty(_library);
                }
            }
        }
    }
}
