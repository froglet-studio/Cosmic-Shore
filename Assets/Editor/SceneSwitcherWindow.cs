using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

public class SceneSwitcherWindow : EditorWindow
{
    private struct SceneAsset
    {
        public string GUID;
        public string Path => AssetDatabase.GUIDToAssetPath(GUID);
        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
    }

    private Vector2 _scrollPos;
    private string _filter = string.Empty;
    private bool _showFavorites = true;
    private bool _showAllScenes = true;

    private List<string> _favoriteGUIDs;
    private SceneAsset[] _allScenes;
    private ReorderableList _favoriteList;

    private const string k_FavoritesKey = "SceneSwitcherWindow.FavoriteScenes";

    [MenuItem("Window/Scene Switcher")]
    private static void OpenWindow() => GetWindow<SceneSwitcherWindow>("Scene Switcher");

    private void OnEnable()
    {
        LoadFavorites();
        RefreshScenes();
        SetupFavoriteList();
    }

    private void OnGUI()
    {
        GUILayout.Space(4);
        DrawSearchBar();

        if (GUILayout.Button("Refresh Scenes"))
            RefreshScenes();

        GUILayout.Space(8);
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // Favourites
        _showFavorites = EditorGUILayout.Foldout(_showFavorites, "Favourites", true);
        if (_showFavorites)
            _favoriteList.DoLayoutList();

        // All Scenes
        _showAllScenes = EditorGUILayout.Foldout(_showAllScenes, "All Scenes", true);
        if (_showAllScenes)
        {
            var others = _allScenes
                .Where(s => !_favoriteGUIDs.Contains(s.GUID)
                    && (string.IsNullOrEmpty(_filter)
                        || s.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToArray();
            foreach (var scene in others)
                DrawSceneRow(scene);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSearchBar()
    {
        EditorGUI.BeginChangeCheck();
        _filter = EditorGUILayout.TextField("Search", _filter);
        if (EditorGUI.EndChangeCheck())
            RefreshScenes();
    }

    private void DrawSceneRow(SceneAsset scene)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("S", GUILayout.Width(20)))
            OpenScene(scene.Path, OpenSceneMode.Single);
        if (GUILayout.Button("A", GUILayout.Width(20)))
            OpenScene(scene.Path, OpenSceneMode.Additive);

        EditorGUILayout.LabelField(scene.Name, GUILayout.Width(200));

        bool isFav = _favoriteGUIDs.Contains(scene.GUID);
        GUIStyle style = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = isFav ? Color.yellow : Color.gray }
        };
        if (GUILayout.Button(isFav ? "●" : "○", style, GUILayout.Width(20)))
            ToggleFavorite(scene.GUID, !isFav);

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void SetupFavoriteList()
    {
        _favoriteList = new ReorderableList(_favoriteGUIDs, typeof(string), true, false, false, false)
        {
            elementHeight = EditorGUIUtility.singleLineHeight + 4f,
            drawHeaderCallback = rect => GUI.Label(rect, "Favourites", EditorStyles.boldLabel),
            drawElementCallback = (rect, index, active, focused) =>
            {
                var guid = _favoriteGUIDs[index];
                var scene = _allScenes.FirstOrDefault(s => s.GUID == guid);
                if (scene.GUID == null) return;

                Rect rowRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                float x = rowRect.x;

                if (GUI.Button(new Rect(x, rowRect.y, 20, rowRect.height), "S"))
                    OpenScene(scene.Path, OpenSceneMode.Single);
                x += 24;
                if (GUI.Button(new Rect(x, rowRect.y, 20, rowRect.height), "A"))
                    OpenScene(scene.Path, OpenSceneMode.Additive);
                x += 24;

                GUI.Label(new Rect(x, rowRect.y, 200, rowRect.height), scene.Name);
                x += 204;

                GUIStyle styleFav = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Color.yellow }
                };
                if (GUI.Button(new Rect(x, rowRect.y, 20, rowRect.height), "●", styleFav))
                    ToggleFavorite(guid, false);
            },
            onReorderCallback = list => SaveFavorites()
        };
    }

    private void RefreshScenes()
    {
        var guids = AssetDatabase.FindAssets("t:Scene " + _filter);
        _allScenes = guids.Select(g => new SceneAsset { GUID = g }).ToArray();
    }

    private void OpenScene(string path, OpenSceneMode mode) =>
        EditorSceneManager.OpenScene(path, mode);

    private void LoadFavorites()
    {
        string data = EditorPrefs.GetString(k_FavoritesKey, string.Empty);
        _favoriteGUIDs = string.IsNullOrEmpty(data)
            ? new List<string>()
            : new List<string>(data.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private void SaveFavorites() =>
        EditorPrefs.SetString(k_FavoritesKey, string.Join(",", _favoriteGUIDs));

    private void ToggleFavorite(string guid, bool add)
    {
        if (add)
        {
            if (!_favoriteGUIDs.Contains(guid))
                _favoriteGUIDs.Add(guid);
        }
        else
        {
            _favoriteGUIDs.Remove(guid);
        }

        SaveFavorites();
        SetupFavoriteList();
        Repaint();
    }
}
