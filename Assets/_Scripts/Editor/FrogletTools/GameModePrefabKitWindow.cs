using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// FrogletTools &gt; Game Modes &gt; Game Mode Prefab Kit.
    ///
    /// The checklist for standing up a new game-mode scene: every prefab the mode needs, with
    /// "Add to Scene" / "Open Prefab" per row, plus a Validate pass that answers the question that
    /// actually bites - <b>is some scene running its own unsaved version of this prefab?</b>
    ///
    /// Validation never writes. Fixes are separate, explicit buttons, and each says exactly which
    /// scene it will touch.
    /// </summary>
    public sealed class GameModePrefabKitWindow : EditorWindow
    {
        const string AssetPath = "Assets/Resources/" + GameModePrefabKitSO.ResourcePath + ".asset";

        GameModePrefabKitSO _kit;
        Vector2 _scroll;
        string _search = "";
        readonly Dictionary<int, KitEntryReport> _reports = new();
        readonly HashSet<int> _expanded = new();
        bool _showConfig;

        [MenuItem("FrogletTools/Game Modes/Game Mode Prefab Kit", false, 10)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 5,
            Description = "Add and validate every prefab a game-mode scene needs.",
            DocPath = "Docs/GAMECANVAS.md")]
        public static void Open()
        {
            var w = GetWindow<GameModePrefabKitWindow>("Prefab Kit");
            w.minSize = new Vector2(700f, 460f);
            w.Show();
        }

        void OnEnable() => _kit = LoadOrCreate();

        // ── Config asset ─────────────────────────────────────────────────────────

        static GameModePrefabKitSO LoadOrCreate()
        {
            var kit = AssetDatabase.LoadAssetAtPath<GameModePrefabKitSO>(AssetPath);
            if (kit != null) return kit;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            kit = CreateInstance<GameModePrefabKitSO>();
            foreach (var (path, role, required, note) in GameModePrefabKitSO.DefaultSeedPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;      // seed is best-effort, never a hard dependency
                kit.Entries.Add(new GameModePrefabEntry
                {
                    Prefab = prefab,
                    Role = role,
                    Required = required,
                    Notes = note,
                });
            }

            AssetDatabase.CreateAsset(kit, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PrefabKit] Created kit config at {AssetPath} with {kit.Entries.Count} seeded entrie(s).");
            return kit;
        }

        // ── GUI ──────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "Game Mode Prefab Kit",
                "The prefabs every game-mode scene needs. Add them, open them, and check no scene is " +
                "quietly running its own unsaved copy.",
                FrogletEditorPalette.Jade);

            if (_kit == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox("Kit config missing.", MessageType.Warning);
                if (GUILayout.Button("Create kit config")) _kit = LoadOrCreate();
                return;
            }

            DrawToolbar();
            DrawSceneContextStrip();

            var entries = _kit.Entries
                .Select((e, i) => (entry: e, index: i))
                .Where(t => Matches(t.entry))
                .ToList();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                GUILayout.Space(4);
                foreach (var role in entries.Select(t => t.entry.Role).Distinct().OrderBy(r => r))
                {
                    DrawRoleHeader(role, entries.Count(t => t.entry.Role == role));
                    foreach (var (entry, index) in entries.Where(t => t.entry.Role == role))
                        DrawEntry(entry, index);
                    GUILayout.Space(8);
                }

                if (entries.Count == 0)
                    EditorGUILayout.HelpBox("No entries match. Add prefabs in the config below.", MessageType.Info);

                DrawConfigSection();
                GUILayout.Space(12);
            }
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.Width(44));
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select config", EditorStyles.toolbarButton, GUILayout.Width(90)))
                {
                    Selection.activeObject = _kit;
                    EditorGUIUtility.PingObject(_kit);
                }
                _showConfig = GUILayout.Toggle(_showConfig, "Edit list", EditorStyles.toolbarButton, GUILayout.Width(64));
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Space(6);
                if (FrogletEditorPalette.ColorButton("＋  ADD ALL TO SCENE", FrogletEditorPalette.Jade, 168f, 30f,
                        "Instantiate every missing required prefab into the active scene."))
                    AddAllToScene();

                GUILayout.Space(6);
                if (FrogletEditorPalette.ColorButton("✓  VALIDATE ALL", FrogletEditorPalette.Azure, 140f, 30f,
                        "Check every entry: asset health, presence in the open scene, and cross-scene drift."))
                    ValidateAll();

                GUILayout.Space(6);
                if (FrogletEditorPalette.ColorButton("CLEAR RESULTS", FrogletEditorPalette.Muted, 116f, 30f,
                        "Forget the current validation report.", outline: true))
                    _reports.Clear();

                GUILayout.FlexibleSpace();
                DrawOverallStatus();
                GUILayout.Space(6);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        void DrawOverallStatus()
        {
            if (_reports.Count == 0) return;
            int errors = _reports.Values.Sum(r => r.Issues.Count(i => i.Severity == KitSeverity.Error));
            int warns = _reports.Values.Sum(r => r.Issues.Count(i => i.Severity == KitSeverity.Warning));

            var r = GUILayoutUtility.GetRect(190f, 30f, GUILayout.Width(190f), GUILayout.Height(30f));
            var half = new Rect(r.x, r.y + 3f, 90f, 24f);
            FrogletEditorPalette.StatusPill(half, errors == 0 ? "0 ERRORS" : $"{errors} ERRORS",
                errors == 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Error);
            var half2 = new Rect(r.x + 96f, r.y + 3f, 90f, 24f);
            FrogletEditorPalette.StatusPill(half2, warns == 0 ? "0 WARNINGS" : $"{warns} WARNINGS",
                warns == 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Warn);
        }

        void DrawSceneContextStrip()
        {
            var active = SceneManager.GetActiveScene();
            var r = GUILayoutUtility.GetRect(0, 20f, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawRect(r, FrogletEditorPalette.Surface);
            FrogletEditorPalette.DrawAccentStripe(r, FrogletEditorPalette.Adapt(FrogletEditorPalette.Violet), 3f);
            var label = string.IsNullOrEmpty(active.path)
                ? "Active scene: (unsaved)   -   \"Add to Scene\" targets whatever is open."
                : $"Active scene: {active.path}";
            GUI.Label(new Rect(r.x + 10f, r.y, r.width - 14f, r.height), label, FrogletEditorPalette.CardBody);
            GUILayout.Space(4);
        }

        static void DrawRoleHeader(GameModePrefabRole role, int count)
        {
            var accent = FrogletEditorPalette.Adapt(AccentFor(role));
            var r = GUILayoutUtility.GetRect(0, 20f, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawRect(r, accent.WithAlpha(0.12f));
            FrogletEditorPalette.DrawAccentStripe(r, accent, 4f);
            GUI.Label(new Rect(r.x + 12f, r.y, r.width - 60f, r.height),
                role.ToString().ToUpperInvariant(),
                new GUIStyle(FrogletEditorPalette.SectionLabel) { normal = { textColor = accent } });
            FrogletEditorPalette.StatusPill(new Rect(r.xMax - 44f, r.y + 2f, 36f, r.height - 4f),
                count.ToString(), AccentFor(role));
            GUILayout.Space(3);
        }

        void DrawEntry(GameModePrefabEntry entry, int index)
        {
            var accent = FrogletEditorPalette.Adapt(AccentFor(entry.Role));
            bool hasPrefab = entry.Prefab != null;
            _reports.TryGetValue(index, out var report);

            var row = GUILayoutUtility.GetRect(0, 46f, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawCard(row, FrogletEditorPalette.Surface, accent.WithAlpha(0.35f));
            FrogletEditorPalette.DrawAccentStripe(row, accent, 3f);

            // Name + notes
            var nameRect = new Rect(row.x + 12f, row.y + 5f, row.width - 380f, 18f);
            GUI.Label(nameRect, entry.ResolvedName, FrogletEditorPalette.CardTitle);

            var noteRect = new Rect(row.x + 12f, row.y + 23f, row.width - 380f, 18f);
            GUI.Label(noteRect, hasPrefab ? (entry.Notes ?? AssetDatabase.GetAssetPath(entry.Prefab))
                                          : "No prefab assigned - set one in Edit list.",
                FrogletEditorPalette.CardBody);

            // Status pill
            var pill = new Rect(row.xMax - 356f, row.y + 13f, 78f, 20f);
            DrawEntryStatus(pill, entry, report);

            // Buttons
            float bx = row.xMax - 268f;
            var open = new Rect(bx, row.y + 11f, 84f, 24f);
            if (FrogletEditorPalette.ColorButton(open, "Open Prefab", FrogletEditorPalette.Violet,
                    hasPrefab ? AssetDatabase.GetAssetPath(entry.Prefab) : "No prefab assigned", hasPrefab))
                OpenPrefab(entry);

            var add = new Rect(bx + 88f, row.y + 11f, 84f, 24f);
            if (FrogletEditorPalette.ColorButton(add, "Add to Scene", FrogletEditorPalette.Jade,
                    "Instantiate into the active scene", hasPrefab))
                AddToScene(entry);

            var val = new Rect(bx + 176f, row.y + 11f, 84f, 24f);
            if (FrogletEditorPalette.ColorButton(val, "Validate", FrogletEditorPalette.Azure,
                    "Check asset health, scene presence and cross-scene drift", hasPrefab))
                _reports[index] = KitValidator.Validate(entry, _kit);

            // Expand toggle over the text area
            var expandZone = new Rect(row.x, row.y, row.width - 372f, row.height);
            if (report != null && report.Issues.Count > 0)
            {
                EditorGUIUtility.AddCursorRect(expandZone, MouseCursor.Link);
                if (GUI.Button(expandZone, GUIContent.none, GUIStyle.none))
                {
                    if (!_expanded.Add(index)) _expanded.Remove(index);
                }
            }

            GUILayout.Space(2);

            if (report != null && _expanded.Contains(index))
                DrawIssues(entry, index, report);

            GUILayout.Space(3);
        }

        static void DrawEntryStatus(Rect r, GameModePrefabEntry entry, KitEntryReport report)
        {
            if (entry.Prefab == null)
            {
                FrogletEditorPalette.StatusPill(r, "NO PREFAB", FrogletEditorPalette.Error);
                return;
            }
            if (report == null)
            {
                FrogletEditorPalette.StatusPill(r, "UNCHECKED", FrogletEditorPalette.Muted);
                return;
            }
            int errors = report.Issues.Count(i => i.Severity == KitSeverity.Error);
            int warns = report.Issues.Count(i => i.Severity == KitSeverity.Warning);
            if (errors > 0) FrogletEditorPalette.StatusPill(r, $"{errors} ERROR", FrogletEditorPalette.Error);
            else if (warns > 0) FrogletEditorPalette.StatusPill(r, $"{warns} DRIFT", FrogletEditorPalette.Warn);
            else FrogletEditorPalette.StatusPill(r, "CLEAN", FrogletEditorPalette.Ok);
        }

        void DrawIssues(GameModePrefabEntry entry, int index, KitEntryReport report)
        {
            foreach (var issue in report.Issues)
            {
                var accent = issue.Severity switch
                {
                    KitSeverity.Error => FrogletEditorPalette.Error,
                    KitSeverity.Warning => FrogletEditorPalette.Warn,
                    _ => FrogletEditorPalette.Info,
                };

                var r = GUILayoutUtility.GetRect(0, 26f, GUILayout.ExpandWidth(true));
                var inner = new Rect(r.x + 22f, r.y, r.width - 26f, r.height);
                FrogletEditorPalette.DrawCard(inner, FrogletEditorPalette.Adapt(accent).WithAlpha(0.08f),
                    FrogletEditorPalette.Adapt(accent).WithAlpha(0.4f));
                FrogletEditorPalette.DrawAccentStripe(inner, FrogletEditorPalette.Adapt(accent), 2f);

                bool hasFix = issue.Fix != null;
                var textRect = new Rect(inner.x + 10f, inner.y, inner.width - (hasFix ? 200f : 20f), inner.height);
                GUI.Label(textRect, issue.Message, FrogletEditorPalette.CardBody);

                if (issue.Ping != null)
                {
                    var pingRect = new Rect(inner.xMax - (hasFix ? 190f : 90f), inner.y + 3f, 82f, 20f);
                    if (FrogletEditorPalette.ColorButton(pingRect, "Show", FrogletEditorPalette.Info,
                            "Reveal in the project or hierarchy", outline: true))
                        issue.Ping();
                }

                if (hasFix)
                {
                    var fixRect = new Rect(inner.xMax - 96f, inner.y + 3f, 88f, 20f);
                    if (FrogletEditorPalette.ColorButton(fixRect, issue.FixLabel ?? "Fix", accent,
                            issue.FixTooltip ?? "Apply the suggested fix"))
                    {
                        if (!issue.NeedsConfirm ||
                            EditorUtility.DisplayDialog("Froglet Prefab Kit",
                                issue.FixTooltip ?? issue.Message, "Do it", "Cancel"))
                        {
                            issue.Fix();
                            _reports[index] = KitValidator.Validate(entry, _kit);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
                GUILayout.Space(2);
            }
        }

        void DrawConfigSection()
        {
            if (!_showConfig) return;
            FrogletEditorPalette.HorizontalRule();
            EditorGUILayout.LabelField("Kit contents", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.LabelField(
                "The list below IS the checklist. Add the prefabs a new game-mode scene must carry.",
                FrogletEditorPalette.Subtitle);
            GUILayout.Space(4);

            var so = new SerializedObject(_kit);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty(nameof(GameModePrefabKitSO.Entries)), true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(GameModePrefabKitSO.SceneSearchFolders)), true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(GameModePrefabKitSO.GloballyExcludedScenes)), true);
            EditorGUILayout.PropertyField(so.FindProperty(nameof(GameModePrefabKitSO.IgnoredPropertyPaths)), true);
            if (so.ApplyModifiedProperties())
                EditorUtility.SetDirty(_kit);
        }

        void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label($"{_kit.Entries.Count} entrie(s)   -   drift scan covers {string.Join(", ", _kit.SceneSearchFolders)}",
                EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ── Actions ──────────────────────────────────────────────────────────────

        void ValidateAll()
        {
            _reports.Clear();
            try
            {
                for (int i = 0; i < _kit.Entries.Count; i++)
                {
                    var e = _kit.Entries[i];
                    EditorUtility.DisplayProgressBar("Validating prefab kit", e.ResolvedName,
                        (float)i / Mathf.Max(1, _kit.Entries.Count));
                    if (e.Prefab != null) _reports[i] = KitValidator.Validate(e, _kit);
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            foreach (var kv in _reports.Where(kv => kv.Value.Issues.Count > 0))
                _expanded.Add(kv.Key);
        }

        void AddAllToScene()
        {
            int added = 0, skipped = 0;
            foreach (var e in _kit.Entries)
            {
                if (e.Prefab == null) continue;
                if (!e.Required && !EditorUtility.DisplayDialog("Optional prefab",
                        $"'{e.ResolvedName}' is optional. Add it too?", "Add", "Skip"))
                { skipped++; continue; }

                if (e.Singleton && FindInActiveScene(e.Prefab).Count > 0) { skipped++; continue; }
                if (AddToScene(e)) added++;
            }
            Debug.Log($"[PrefabKit] Added {added} prefab(s) to '{SceneManager.GetActiveScene().name}', skipped {skipped}.");
        }

        static bool AddToScene(GameModePrefabEntry entry)
        {
            if (entry.Prefab == null) return false;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[PrefabKit] No valid active scene to add to.");
                return false;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, scene);
            if (go == null) return false;

            Undo.RegisterCreatedObjectUndo(go, $"Add {entry.ResolvedName}");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        static void OpenPrefab(GameModePrefabEntry entry)
        {
            if (entry.Prefab == null) return;
            var path = AssetDatabase.GetAssetPath(entry.Prefab);
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.OpenAsset(entry.Prefab);
            EditorGUIUtility.PingObject(entry.Prefab);
        }

        internal static List<GameObject> FindInActiveScene(GameObject prefab)
        {
            var hits = new List<GameObject>();
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || prefab == null) return hits;
            var assetPath = AssetDatabase.GetAssetPath(prefab);

            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == assetPath) hits.Add(go);
            }
            return hits;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        bool Matches(GameModePrefabEntry e)
        {
            if (string.IsNullOrWhiteSpace(_search)) return true;
            var q = _search.Trim();
            return e.ResolvedName.Contains(q, StringComparison.OrdinalIgnoreCase)
                   || (e.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                   || e.Role.ToString().Contains(q, StringComparison.OrdinalIgnoreCase);
        }

        static Color AccentFor(GameModePrefabRole role) => role switch
        {
            GameModePrefabRole.Essential => FrogletEditorPalette.Ruby,
            GameModePrefabRole.Interface => FrogletEditorPalette.Cyan,
            GameModePrefabRole.Spawning => FrogletEditorPalette.Jade,
            GameModePrefabRole.Environment => FrogletEditorPalette.Lime,
            GameModePrefabRole.Networking => FrogletEditorPalette.Magenta,
            _ => FrogletEditorPalette.Slate,
        };
    }
}
