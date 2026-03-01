using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor window that displays all <see cref="CanvasGroup"/> GameObjects in the
    /// active scene as a hierarchy tree. Each node has an active toggle and an alpha
    /// field to configure initial state at game start. State is stored in an
    /// <see cref="InitialPanelStateApplier"/> component in the scene.
    /// </summary>
    public class CanvasGroupEditorWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private InitialPanelStateApplier _applier;
        private SerializedObject _serializedApplier;

        private readonly List<TreeNode> _rootNodes = new();
        private string _searchFilter = "";
        private readonly HashSet<int> _expanded = new();
        private readonly Dictionary<GameObject, PanelState> _managed = new();
        private int _totalCanvasGroups;
        private int _managedCount;
        private bool _dirty = true;

        private struct PanelState
        {
            public bool active;
            public float alpha;
        }

        private class TreeNode
        {
            public GameObject go;
            public bool hasCanvasGroup;
            public readonly List<TreeNode> children = new();
        }

        // ─────────────────────────── Lifecycle ──────────────────────

        [MenuItem("Tools/Cosmic Shore/Canvas Group Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<CanvasGroupEditorWindow>("Canvas Group Editor");
            window.minSize = new Vector2(450, 300);
        }

        void OnEnable()
        {
            EditorApplication.hierarchyChanged += MarkDirty;
            Undo.undoRedoPerformed += MarkDirty;
            _dirty = true;
        }

        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= MarkDirty;
            Undo.undoRedoPerformed -= MarkDirty;
        }

        void OnFocus() => _dirty = true;
        void MarkDirty() => _dirty = true;

        // ─────────────────────────── Data ───────────────────────────

        void Rebuild()
        {
            _dirty = false;
            if (_applier == null)
                _applier = FindAnyObjectByType<InitialPanelStateApplier>(FindObjectsInactive.Include);
            BuildTree();
            RebuildManagedCache();
        }

        void RebuildManagedCache()
        {
            _managed.Clear();
            _managedCount = 0;
            if (_applier == null) return;

            _serializedApplier = new SerializedObject(_applier);
            var list = _serializedApplier.FindProperty("panelStates");

            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                var panel = entry.FindPropertyRelative("panel").objectReferenceValue as GameObject;
                if (panel == null) continue;
                _managed[panel] = new PanelState
                {
                    active = entry.FindPropertyRelative("startActive").boolValue,
                    alpha = entry.FindPropertyRelative("startAlpha").floatValue
                };
                _managedCount++;
            }
        }

        void BuildTree()
        {
            _rootNodes.Clear();
            _totalCanvasGroups = 0;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return;

            // Recursively collect all CanvasGroups (handles inactive GameObjects)
            var cgSet = new HashSet<GameObject>();
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
                CollectCanvasGroups(r.transform, cgSet);

            _totalCanvasGroups = cgSet.Count;

            // Collect ancestor GameObjects for hierarchy context
            var ancestors = new HashSet<GameObject>();
            foreach (var go in cgSet)
            {
                var t = go.transform.parent;
                while (t != null)
                {
                    if (!ancestors.Add(t.gameObject)) break;
                    t = t.parent;
                }
            }

            // Build tree matching scene hierarchy order
            foreach (var r in roots.OrderBy(g => g.transform.GetSiblingIndex()))
            {
                if (!cgSet.Contains(r) && !ancestors.Contains(r)) continue;
                var node = BuildNode(r, cgSet, ancestors);
                if (node == null) continue;
                _rootNodes.Add(node);
                _expanded.Add(node.go.GetInstanceID());
            }
        }

        static void CollectCanvasGroups(Transform t, HashSet<GameObject> results)
        {
            if (t.TryGetComponent<CanvasGroup>(out _))
                results.Add(t.gameObject);
            for (int i = 0; i < t.childCount; i++)
                CollectCanvasGroups(t.GetChild(i), results);
        }

        TreeNode BuildNode(GameObject go, HashSet<GameObject> cgSet, HashSet<GameObject> ancestors)
        {
            if (!cgSet.Contains(go) && !ancestors.Contains(go)) return null;

            var node = new TreeNode
            {
                go = go,
                hasCanvasGroup = cgSet.Contains(go)
            };

            for (int i = 0; i < go.transform.childCount; i++)
            {
                var child = BuildNode(go.transform.GetChild(i).gameObject, cgSet, ancestors);
                if (child != null) node.children.Add(child);
            }

            return node;
        }

        // ─────────────────────────── GUI ────────────────────────────

        void OnGUI()
        {
            if (_dirty) Rebuild();

            if (_serializedApplier is { targetObject: not null })
                _serializedApplier.Update();

            DrawToolbar();
            DrawApplierField();

            if (_applier == null)
            {
                DrawNoApplierMessage();
                return;
            }

            DrawActionButtons();
            DrawColumnHeaders();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            foreach (var root in _rootNodes)
                DrawNode(root, 0);
            EditorGUILayout.EndScrollView();

            DrawStatusBar();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(55)))
                _dirty = true;

            GUILayout.Space(4);
            _searchFilter = EditorGUILayout.TextField(
                _searchFilter, EditorStyles.toolbarSearchField);

            if (GUILayout.Button("\u00d7", EditorStyles.toolbarButton, GUILayout.Width(18)))
            {
                _searchFilter = "";
                GUI.FocusControl(null);
            }

            GUILayout.Space(6);
            if (GUILayout.Button("Expand", EditorStyles.toolbarButton, GUILayout.Width(50)))
                ExpandAll(_rootNodes);
            if (GUILayout.Button("Collapse", EditorStyles.toolbarButton, GUILayout.Width(55)))
                _expanded.Clear();

            EditorGUILayout.EndHorizontal();
        }

        void ExpandAll(List<TreeNode> nodes)
        {
            foreach (var n in nodes)
            {
                _expanded.Add(n.go.GetInstanceID());
                ExpandAll(n.children);
            }
        }

        void DrawApplierField()
        {
            EditorGUILayout.BeginHorizontal();
            var prev = _applier;
            _applier = (InitialPanelStateApplier)EditorGUILayout.ObjectField(
                "State Applier", _applier, typeof(InitialPanelStateApplier), true);
            if (_applier != prev)
                _dirty = true;
            EditorGUILayout.EndHorizontal();
        }

        void DrawNoApplierMessage()
        {
            EditorGUILayout.HelpBox(
                "No InitialPanelStateApplier found in the scene.\n" +
                "Create one to define initial panel states at game start.",
                MessageType.Info);

            if (GUILayout.Button("Create InitialPanelStateApplier"))
            {
                var go = new GameObject("InitialPanelStateApplier");
                _applier = go.AddComponent<InitialPanelStateApplier>();
                Undo.RegisterCreatedObjectUndo(go, "Create InitialPanelStateApplier");
                Selection.activeGameObject = go;
                _dirty = true;
            }
        }

        void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add All (Current State)"))
                AddAllCanvasGroups();
            if (GUILayout.Button("Activate All"))
                SetAllActive(true);
            if (GUILayout.Button("Deactivate All"))
                SetAllActive(false);
            if (GUILayout.Button("Clean Nulls"))
                CleanNullEntries();
            EditorGUILayout.EndHorizontal();
        }

        void DrawColumnHeaders()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(22);
            GUILayout.Label("On", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(18));
            GUILayout.Label("Alpha", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(40));
            GUILayout.Label("Panel", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawNode(TreeNode node, int depth)
        {
            if (node.go == null) return;
            if (!string.IsNullOrEmpty(_searchFilter) && !MatchesFilter(node)) return;

            bool isManaged = node.hasCanvasGroup && _managed.ContainsKey(node.go);
            bool hasChildren = node.children.Count > 0;
            int id = node.go.GetInstanceID();
            bool isExpanded = _expanded.Contains(id);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(depth * 18 + 4);

            // Foldout arrow
            if (hasChildren)
            {
                var foldRect = GUILayoutUtility.GetRect(14, 18, GUILayout.Width(14));
                bool newExpanded = EditorGUI.Foldout(foldRect, isExpanded, GUIContent.none, true);
                if (newExpanded != isExpanded)
                {
                    if (newExpanded) _expanded.Add(id);
                    else _expanded.Remove(id);
                }
            }
            else
            {
                GUILayout.Space(18);
            }

            var prevColor = GUI.color;
            if (node.hasCanvasGroup)
            {
                PanelState current = isManaged
                    ? _managed[node.go]
                    : DefaultState(node.go);

                if (!isManaged) GUI.color = new Color(1f, 1f, 1f, 0.5f);

                // Active toggle (SetActive)
                bool nextActive = EditorGUILayout.Toggle(current.active, GUILayout.Width(16));
                if (nextActive != current.active)
                    SetEntry(node.go, nextActive, current.alpha);

                // Alpha field (CanvasGroup.alpha)
                float nextAlpha = EditorGUILayout.FloatField(current.alpha, GUILayout.Width(40));
                nextAlpha = Mathf.Clamp01(nextAlpha);
                if (!Mathf.Approximately(nextAlpha, current.alpha))
                    SetEntry(node.go, current.active, nextAlpha);

                GUI.color = prevColor;
            }
            else
            {
                GUILayout.Space(60);
            }

            // Name label — click to select in hierarchy
            GUIStyle style;
            if (!node.hasCanvasGroup)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.45f);
                style = EditorStyles.miniLabel;
            }
            else if (isManaged)
            {
                style = EditorStyles.boldLabel;
            }
            else
            {
                style = EditorStyles.label;
            }

            if (GUILayout.Button(node.go.name, style))
            {
                EditorGUIUtility.PingObject(node.go);
                Selection.activeGameObject = node.go;
            }

            GUI.color = prevColor;

            // Status indicator / remove button
            if (node.hasCanvasGroup)
            {
                if (isManaged)
                {
                    if (GUILayout.Button("\u00d7", EditorStyles.miniButton, GUILayout.Width(20)))
                        RemoveEntry(node.go);
                }
                else
                {
                    var c = GUI.color;
                    GUI.color = new Color(1f, 0.8f, 0.4f, 0.7f);
                    GUILayout.Label("untracked", EditorStyles.miniLabel, GUILayout.Width(58));
                    GUI.color = c;
                }
            }

            EditorGUILayout.EndHorizontal();

            // Children
            if (hasChildren && isExpanded)
            {
                foreach (var child in node.children)
                    DrawNode(child, depth + 1);
            }
        }

        static PanelState DefaultState(GameObject go) => new()
        {
            active = go.activeSelf,
            alpha = go.TryGetComponent<CanvasGroup>(out var cg) ? cg.alpha : 1f
        };

        bool MatchesFilter(TreeNode node)
        {
            if (node.go.name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return node.children.Any(MatchesFilter);
        }

        void DrawStatusBar()
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(
                $"{_totalCanvasGroups} CanvasGroups  |  {_managedCount} tracked",
                EditorStyles.centeredGreyMiniLabel);
        }

        // ─────────────────────── Mutations ──────────────────────────

        void SetEntry(GameObject go, bool active, float alpha)
        {
            if (_applier == null) return;
            _serializedApplier.Update();
            var list = _serializedApplier.FindProperty("panelStates");

            // Update existing entry
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("panel").objectReferenceValue == go)
                {
                    entry.FindPropertyRelative("startActive").boolValue = active;
                    entry.FindPropertyRelative("startAlpha").floatValue = alpha;
                    _serializedApplier.ApplyModifiedProperties();
                    _managed[go] = new PanelState { active = active, alpha = alpha };
                    return;
                }
            }

            // Add new entry
            list.InsertArrayElementAtIndex(list.arraySize);
            var newEntry = list.GetArrayElementAtIndex(list.arraySize - 1);
            newEntry.FindPropertyRelative("panel").objectReferenceValue = go;
            newEntry.FindPropertyRelative("startActive").boolValue = active;
            newEntry.FindPropertyRelative("startAlpha").floatValue = alpha;
            _serializedApplier.ApplyModifiedProperties();
            _managed[go] = new PanelState { active = active, alpha = alpha };
            _managedCount++;
        }

        void RemoveEntry(GameObject go)
        {
            if (_applier == null) return;
            _serializedApplier.Update();
            var list = _serializedApplier.FindProperty("panelStates");

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("panel").objectReferenceValue == go)
                {
                    list.DeleteArrayElementAtIndex(i);
                    _serializedApplier.ApplyModifiedProperties();
                    _managed.Remove(go);
                    _managedCount--;
                    return;
                }
            }
        }

        void AddAllCanvasGroups()
        {
            if (_applier == null) return;
            _serializedApplier.Update();
            var list = _serializedApplier.FindProperty("panelStates");

            foreach (var node in Flatten(_rootNodes))
            {
                if (!node.hasCanvasGroup || _managed.ContainsKey(node.go)) continue;
                var state = DefaultState(node.go);
                list.InsertArrayElementAtIndex(list.arraySize);
                var entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("panel").objectReferenceValue = node.go;
                entry.FindPropertyRelative("startActive").boolValue = state.active;
                entry.FindPropertyRelative("startAlpha").floatValue = state.alpha;
                _managed[node.go] = state;
                _managedCount++;
            }

            _serializedApplier.ApplyModifiedProperties();
        }

        void SetAllActive(bool active)
        {
            if (_applier == null) return;
            _serializedApplier.Update();
            var list = _serializedApplier.FindProperty("panelStates");

            for (int i = 0; i < list.arraySize; i++)
                list.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("startActive").boolValue = active;

            _serializedApplier.ApplyModifiedProperties();
            foreach (var key in _managed.Keys.ToList())
            {
                var s = _managed[key];
                s.active = active;
                _managed[key] = s;
            }
        }

        void CleanNullEntries()
        {
            if (_applier == null) return;
            _serializedApplier.Update();
            var list = _serializedApplier.FindProperty("panelStates");

            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("panel").objectReferenceValue == null)
                    list.DeleteArrayElementAtIndex(i);
            }

            _serializedApplier.ApplyModifiedProperties();
            RebuildManagedCache();
        }

        static IEnumerable<TreeNode> Flatten(List<TreeNode> nodes)
        {
            foreach (var n in nodes)
            {
                yield return n;
                foreach (var c in Flatten(n.children))
                    yield return c;
            }
        }
    }
}
