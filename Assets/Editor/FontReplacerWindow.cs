using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor-only tool that swaps the TextMeshPro font asset on text components.
    ///
    /// <para>Two modes: replace on the current hierarchy <b>Selection</b> (optionally
    /// including children / inactive objects), or <b>Find All</b> TMP text in the loaded
    /// scene(s) and replace them together. Nothing runs at play time.</para>
    ///
    /// <para>Correctness: changing a TMP font asset without also updating the font material
    /// leaves the component pointing at the <i>old</i> atlas material, which renders as
    /// blank/tofu. This tool always applies a matching material — the supplied preset, or the
    /// target font's default material — right after the font swap. Every change goes through
    /// <see cref="Undo"/>, registers prefab-instance overrides, and dirties the owning
    /// scene(s) so it survives a save.</para>
    ///
    /// Open via <b>Tools ▸ Cosmic Shore ▸ Font Replacer</b>.
    /// </summary>
    public sealed class FontReplacerWindow : EditorWindow
    {
        // ── Config ────────────────────────────────────────────────────────────
        private TMP_FontAsset _targetFont;
        private Material _materialPreset;

        private bool _useSourceFilter;
        private TMP_FontAsset _sourceFilter;

        private bool _includeChildren = true;
        private bool _includeInactive = true;

        // ── Cached scan results ───────────────────────────────────────────────
        private readonly List<TMP_Text> _selectionTargets = new();
        private readonly List<TMP_Text> _sceneTargets = new();

        private Vector2 _scroll;
        private bool _showPreview = true;

        [MenuItem("Tools/Cosmic Shore/Font Replacer")]
        public static void Open()
        {
            var window = GetWindow<FontReplacerWindow>("Font Replacer");
            window.minSize = new Vector2(380f, 460f);
            window.RefreshSelection();
            window.Show();
        }

        /// <summary>Context-menu entry on any TMP component: opens the tool primed from it.</summary>
        [MenuItem("CONTEXT/TMP_Text/Replace Font…")]
        private static void OpenFromContext(MenuCommand command)
        {
            var window = GetWindow<FontReplacerWindow>("Font Replacer");
            if (command.context is TMP_Text tmp && tmp.font != null)
            {
                window._useSourceFilter = true;
                window._sourceFilter = tmp.font;
            }
            window.RefreshSelection();
            window.Show();
        }

        private void OnEnable() => RefreshSelection();

        private void OnSelectionChange()
        {
            RefreshSelection();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

            _targetFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
                new GUIContent("Font Asset", "The TMP font asset to apply to matching text components."),
                _targetFont, typeof(TMP_FontAsset), false);

            _materialPreset = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Material Preset",
                    "Optional. Font material preset to apply alongside the font (e.g. an outline/glow variant). " +
                    "Leave empty to use the target font's default material."),
                _materialPreset, typeof(Material), false);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Filter", EditorStyles.boldLabel);

            _useSourceFilter = EditorGUILayout.ToggleLeft(
                new GUIContent("Only replace a specific source font",
                    "When on, only components currently using the Source Font are changed. " +
                    "When off, every text in scope is changed regardless of its current font."),
                _useSourceFilter);

            using (new EditorGUI.DisabledScope(!_useSourceFilter))
            {
                EditorGUI.indentLevel++;
                _sourceFilter = (TMP_FontAsset)EditorGUILayout.ObjectField(
                    "Source Font", _sourceFilter, typeof(TMP_FontAsset), false);
                if (GUILayout.Button("Set Source From Selection"))
                    SetSourceFromSelection();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _includeChildren = EditorGUILayout.ToggleLeft(
                new GUIContent("Include children of selection",
                    "Also affect TMP text on descendants of the selected objects."), _includeChildren);
            _includeInactive = EditorGUILayout.ToggleLeft(
                new GUIContent("Include inactive objects",
                    "Also affect TMP text on disabled objects."), _includeInactive);
            if (EditorGUI.EndChangeCheck())
                RefreshSelection();

            if (_targetFont == null)
                EditorGUILayout.HelpBox("Assign a Target Font Asset to enable replacement.", MessageType.Info);

            DrawSelectionSection();
            DrawSceneSection();
            DrawPreview();
        }

        // ── Selection ─────────────────────────────────────────────────────────
        private void DrawSelectionSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Replace on Selection", EditorStyles.boldLabel);

            int matching = Effective(_selectionTargets).Count();
            EditorGUILayout.LabelField(
                $"Selection: {_selectionTargets.Count} TMP text(s)   ·   matching filter: {matching}");

            using (new EditorGUI.DisabledScope(_targetFont == null || matching == 0))
            {
                if (GUILayout.Button($"Replace on Selection  ({matching})", GUILayout.Height(28f)))
                    Replace(_selectionTargets, "Selection");
            }
        }

        // ── Whole scene ───────────────────────────────────────────────────────
        private void DrawSceneSection()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Replace in Whole Scene", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find All TMP Text in Scene", GUILayout.Height(22f)))
                    FindSceneTargets();
                using (new EditorGUI.DisabledScope(_sceneTargets.Count == 0))
                {
                    if (GUILayout.Button("Clear", GUILayout.Width(60f), GUILayout.Height(22f)))
                        _sceneTargets.Clear();
                }
            }

            int matching = Effective(_sceneTargets).Count();
            EditorGUILayout.LabelField(
                $"Found in scene: {_sceneTargets.Count}   ·   matching filter: {matching}");

            using (new EditorGUI.DisabledScope(_targetFont == null || matching == 0))
            {
                if (GUILayout.Button($"Replace All in Scene  ({matching})", GUILayout.Height(28f)))
                {
                    bool ok = EditorUtility.DisplayDialog(
                        "Replace fonts in scene",
                        $"Replace the font on {matching} TMP text component(s) in the loaded scene(s) " +
                        $"with '{_targetFont.name}'?\n\nThis can be undone (Ctrl/Cmd+Z).",
                        "Replace", "Cancel");
                    if (ok) Replace(_sceneTargets, "Scene");
                }
            }
        }

        // ── Preview ───────────────────────────────────────────────────────────
        private void DrawPreview()
        {
            EditorGUILayout.Space(8f);
            _showPreview = EditorGUILayout.Foldout(_showPreview, "Preview (targets matching filter)", true);
            if (!_showPreview) return;

            var pool = _selectionTargets.Count > 0 ? _selectionTargets : _sceneTargets;
            var list = Effective(pool).ToList();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(150f));
            const int cap = 300;
            int shown = 0;
            foreach (var t in list)
            {
                if (t == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("→", GUILayout.Width(24f)))
                    {
                        Selection.activeObject = t.gameObject;
                        EditorGUIUtility.PingObject(t.gameObject);
                    }
                    EditorGUILayout.LabelField(
                        new GUIContent(t.gameObject.name, t.gameObject.name),
                        new GUIContent(t.font != null ? t.font.name : "(no font)"));
                }
                if (++shown >= cap)
                {
                    EditorGUILayout.LabelField($"… (+{list.Count - shown} more not shown)");
                    break;
                }
            }
            if (shown == 0)
                EditorGUILayout.LabelField("No matching TMP text. Select objects or use Find All.");
            EditorGUILayout.EndScrollView();
        }

        // ── Core ──────────────────────────────────────────────────────────────

        /// <summary>Rebuild the cached list of TMP components under the current selection.</summary>
        private void RefreshSelection()
        {
            _selectionTargets.Clear();
            var seen = new HashSet<int>();

            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;

                var comps = _includeChildren
                    ? go.GetComponentsInChildren<TMP_Text>(true)
                    : go.GetComponents<TMP_Text>();

                foreach (var c in comps)
                {
                    if (c == null) continue;
                    if (!_includeInactive && !c.gameObject.activeInHierarchy) continue;
                    if (seen.Add(c.GetInstanceID()))
                        _selectionTargets.Add(c);
                }
            }
        }

        /// <summary>Scan every loaded scene for TMP text (includes the prefab stage when open).</summary>
        private void FindSceneTargets()
        {
            _sceneTargets.Clear();
            var mode = _includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
            var all = FindObjectsByType<TMP_Text>(mode, FindObjectsSortMode.None);
            _sceneTargets.AddRange(all.Where(t => t != null));
        }

        private IEnumerable<TMP_Text> Effective(IEnumerable<TMP_Text> source)
        {
            var live = source.Where(t => t != null);
            return (_useSourceFilter && _sourceFilter != null)
                ? live.Where(t => t.font == _sourceFilter)
                : live;
        }

        private void SetSourceFromSelection()
        {
            foreach (var t in _selectionTargets)
            {
                if (t != null && t.font != null)
                {
                    _sourceFilter = t.font;
                    return;
                }
            }
            ShowNotification(new GUIContent("Selection has no TMP text with a font."));
        }

        private void Replace(List<TMP_Text> pool, string label)
        {
            if (_targetFont == null) return;

            var targets = Effective(pool).ToList();
            if (targets.Count == 0) return;

            var material = _materialPreset != null ? _materialPreset : _targetFont.material;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName($"Replace TMP Font ({label})");
            int group = Undo.GetCurrentGroup();

            var dirtyScenes = new HashSet<Scene>();
            int changed = 0;

            foreach (var tmp in targets)
            {
                if (tmp == null) continue;

                Undo.RecordObject(tmp, "Replace TMP Font");

                tmp.font = _targetFont;              // swap the atlas/glyph source
                tmp.fontSharedMaterial = material;   // keep the material in lock-step (prevents tofu)

                EditorUtility.SetDirty(tmp);
                PrefabUtility.RecordPrefabInstancePropertyModifications(tmp);

                var scene = tmp.gameObject.scene;
                if (scene.IsValid())
                    dirtyScenes.Add(scene);

                changed++;
            }

            Undo.CollapseUndoOperations(group);

            foreach (var scene in dirtyScenes)
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[FontReplacer] {label}: applied '{_targetFont.name}' to {changed} TMP text component(s).");
            Repaint();
        }
    }
}
