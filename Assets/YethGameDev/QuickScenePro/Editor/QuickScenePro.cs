using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace YethGameDev.QuickScenePro
{
    public class QuickScenePro : EditorWindow
    {
        private List<string> _favoriteGUIDs;
        private SceneAssetEntry[] _allScenes;

        private Vector2 _scrollPos;
        private string _filter = string.Empty;
        private bool _showFavorites = true;
        private bool _showAllScenes = true;

        private const string _additiveColorCode = "#6DEBE1";
        private const string _openColorCode = "#BBEB6D";

        private Color _addtiveColor;
        private Color _openColor;

        private ReorderableList _favoriteList;

        private const string k_FavoritesKey = "QuickScene.Pro.Favorites";
        private const string k_Version = "1.0.0";

        [MenuItem("Window/Quick Scene Pro %&m")]
        private static void OpenWindow() => GetWindow<QuickScenePro>("Quick Scene Pro");

        private void OnEnable()
        {
            SetColorCodes();
            LoadFavorites();
            RefreshScenes();
            SetupFavoriteList();
        }

        private void OnGUI()
        {
            GUILayout.Space(6);
            DrawSearchBar();

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUI.enabled = _favoriteGUIDs.Count > 1 && _favoriteList != null && _favoriteList.index > 0;
            if (GUILayout.Button("◀", GUILayout.Width(24)))
                OpenFavoriteAt(_favoriteList.index - 1);

            GUI.enabled = true;
            if (GUILayout.Button("Refresh Scenes"))
                RefreshScenes();

            GUI.enabled = _favoriteGUIDs.Count > 1 && _favoriteList != null && _favoriteList.index < _favoriteGUIDs.Count - 1;
            if (GUILayout.Button("▶", GUILayout.Width(24)))
                OpenFavoriteAt(_favoriteList.index + 1);
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // Favorites Section
            _showFavorites = EditorGUILayout.Foldout(_showFavorites, "Favorites", true);
            if (_showFavorites)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (_favoriteList != null && _favoriteGUIDs.Count > 0)
                {
                    _favoriteList.DoLayoutList();
                }
                else
                {
                    EditorGUILayout.HelpBox("No favorites added.", MessageType.Info);
                }
                EditorGUILayout.EndVertical();
            }

            if (_favoriteGUIDs.Count > 0)
            {
                if (GUILayout.Button("Clear All Favorites"))
                {
                    _favoriteGUIDs.Clear();
                    SaveFavorites();
                    SetupFavoriteList();
                }
            }


            // All Scenes Section
            _showAllScenes = EditorGUILayout.Foldout(_showAllScenes, "All Scenes", true);
            if (_showAllScenes)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var others = _allScenes
                    .Where(s => !_favoriteGUIDs.Contains(s.GUID)
                        && (string.IsNullOrEmpty(_filter)
                            || s.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToArray();
                foreach (var scene in others)
                    DrawSceneRow(scene);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();

            // Footer: version label
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUIStyle versionStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.LowerRight,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) }
            };
            GUILayout.Label($"Version: {k_Version}", versionStyle, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
        }

        private void SetColorCodes()
        {
            ColorUtility.TryParseHtmlString(_openColorCode, out _openColor);
            ColorUtility.TryParseHtmlString(_additiveColorCode, out _addtiveColor);
        }

        private void DrawSearchBar()
        {
            EditorGUI.BeginChangeCheck();
            _filter = EditorGUILayout.TextField("Search", _filter);
            if (EditorGUI.EndChangeCheck())
                RefreshScenes();
        }

        private void DrawSceneRow(SceneAssetEntry scene)
        {
            EditorGUILayout.BeginHorizontal();

            var prevBg = GUI.backgroundColor;

            GUI.backgroundColor = _openColor;
            if (GUILayout.Button("O", GUILayout.Width(20)))
                OpenScene(scene.Path, OpenSceneMode.Single);
            GUI.backgroundColor = prevBg;

            GUI.backgroundColor = _addtiveColor;
            if (GUILayout.Button("A", GUILayout.Width(20)))
                OpenScene(scene.Path, OpenSceneMode.Additive);
            GUI.backgroundColor = prevBg;

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
            if (_favoriteGUIDs.Count == 0)
            {
                _favoriteList = null;
                return;
            }

            _favoriteList = new ReorderableList(_favoriteGUIDs, typeof(string), true, true, false, false)
            {
                elementHeight = EditorGUIUtility.singleLineHeight + 4f,
                drawHeaderCallback = rect => GUI.Label(rect, "Favorites", EditorStyles.boldLabel),
                drawElementCallback = (rect, index, active, focused) =>
                {
                    if (index >= _favoriteGUIDs.Count) return;

                    var guid = _favoriteGUIDs[index];
                    var scene = _allScenes.FirstOrDefault(s => s.GUID == guid);
                    if (scene.GUID == null) return;

                    float x = rect.x;
                    var prev = GUI.backgroundColor;

                    GUI.backgroundColor = _openColor;
                    if (GUI.Button(new Rect(x, rect.y, 20, rect.height), "O"))
                        OpenScene(scene.Path, OpenSceneMode.Single);
                    x += 24;

                    GUI.backgroundColor = _addtiveColor;
                    if (GUI.Button(new Rect(x, rect.y, 20, rect.height), "A"))
                        OpenScene(scene.Path, OpenSceneMode.Additive);
                    GUI.backgroundColor = prev;
                    x += 24;

                    GUI.Label(new Rect(x, rect.y, 200, rect.height), scene.Name);
                    x += 204;

                    GUIStyle styleFav = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = Color.yellow }
                    };
                    if (GUI.Button(new Rect(x, rect.y, 20, rect.height), "●", styleFav))
                        ToggleFavorite(guid, false);
                },
                onReorderCallback = list => SaveFavorites()
            };
        }

        private void RefreshScenes()
        {
            var guids = AssetDatabase.FindAssets("t:Scene " + _filter);
            _allScenes = guids.Select(g => new SceneAssetEntry { GUID = g }).ToArray();
        }

        private void OpenScene(string path, OpenSceneMode mode)
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
            }

            EditorSceneManager.OpenScene(path, mode);
        }

        private void OpenFavoriteAt(int index)
        {
            if (index < 0 || index >= _favoriteGUIDs.Count)
                return;
            var guid = _favoriteGUIDs[index];
            var scene = _allScenes.FirstOrDefault(s => s.GUID == guid);
            if (scene.GUID == null) return;
            OpenScene(scene.Path, OpenSceneMode.Single);
            _favoriteList.index = index;
        }

        private void LoadFavorites()
        {
            string data = EditorPrefs.GetString(k_FavoritesKey, string.Empty);
            _favoriteGUIDs = string.IsNullOrEmpty(data)
                ? new List<string>()
                : new List<string>(data.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private void SaveFavorites() => EditorPrefs.SetString(k_FavoritesKey, string.Join(",", _favoriteGUIDs));

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

        private struct SceneAssetEntry
        {
            public string GUID;
            public string Path => AssetDatabase.GUIDToAssetPath(GUID);
            public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);
        }
    }
}
