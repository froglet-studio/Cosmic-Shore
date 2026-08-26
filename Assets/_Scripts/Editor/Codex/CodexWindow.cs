using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Codex
{
    /// <summary>
    /// <b>FrogletTools &gt; Interface &gt; Ethirion &amp; Ecology Codex</b> — the one place the
    /// in-game encyclopedia is authored.
    ///
    /// <para>It does three things. <b>Scan &amp; Merge</b> walks the project and folds every
    /// ethirion (crystal) and every ecology species (flora and fauna) into
    /// <c>Assets/Resources/Codex.asset</c>, harvesting the facts it can re-derive and leaving
    /// authored prose alone. <b>Bake</b> renders each entry's hero image to a transparent PNG.
    /// And the panel on the right edits any entry by hand — add, modify, delete — because a
    /// generated encyclopedia with no room for a writer is a spreadsheet.</para>
    ///
    /// <para>The runtime UI reads the same asset through <c>CodexSO.Load()</c>; there is no second
    /// data path and nothing to wire per scene.</para>
    /// </summary>
    public partial class CodexWindow : EditorWindow
    {
        public const string ToolName = "Ethirion & Ecology Codex";
        const string AssetPath = "Assets/Resources/" + CodexSO.ResourcePath + ".asset";

        CodexSO _codex;
        string _selectedId;
        string _search = string.Empty;
        int _kingdomFilter;                 // 0 = all, else (CodexKingdom)(_kingdomFilter - 1)
        int _bakeSize = 512;

        Vector2 _listScroll;
        Vector2 _detailScroll;
        CodexHarvestReport _lastReport;
        string _status = string.Empty;
        bool _statusIsError;

        /// <summary>
        /// Queued mutation. IMGUI is mid-layout while the buttons are drawn, so adding to or
        /// removing from the list the loop is walking throws; every action runs after the pass.
        /// </summary>
        Action _deferred;

        static readonly string[] KingdomFilters = { "All", "Ethirions", "Flora", "Fauna" };
        static readonly int[] BakeSizes = { 256, 512, 1024 };

        [MenuItem("FrogletTools/Interface/Ethirion & Ecology Codex")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 4,
            Description = "Harvest, edit and illustrate every crystal and lifeform for the in-game encyclopedia.",
            DocPath = "Docs/CODEX.md")]
        static void Open()
        {
            var window = GetWindow<CodexWindow>("Codex");
            window.minSize = new Vector2(920f, 560f);
            window.Show();
        }

        void OnEnable() => _codex = LoadOrCreate();

        static CodexSO LoadOrCreate()
        {
            var codex = AssetDatabase.LoadAssetAtPath<CodexSO>(AssetPath);
            if (codex) return codex;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            codex = CreateInstance<CodexSO>();
            AssetDatabase.CreateAsset(codex, AssetPath);
            AssetDatabase.SaveAssets();
            FrogletToolChangeLedger.Record(ToolName, AssetPath);
            Debug.Log($"[Codex] Created {AssetPath}. Run Scan & Merge to populate it.");
            return codex;
        }

        // ── Frame ────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (!_codex)
            {
                EditorGUILayout.HelpBox("Codex asset missing.", MessageType.Warning);
                if (GUILayout.Button("Create Codex asset")) _codex = LoadOrCreate();
                return;
            }

            FrogletEditorPalette.Banner(
                "Ethirion & Ecology Codex",
                "Every crystal and every lifeform, as the in-game encyclopedia reads them.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Interface));

            DrawToolbar();
            DrawStatus();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawList();
                DrawDetail();
            }

            if (_deferred == null) return;
            var action = _deferred;
            _deferred = null;
            action();
            Repaint();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("Scan & Merge",
                        "Re-read the project. Adds new entries, refreshes harvested facts, and " +
                        "never touches authored prose, ordering, discovery or the preview pose."),
                        EditorStyles.toolbarButton, GUILayout.Width(110f)))
                    _deferred = ScanAndMerge;

                if (GUILayout.Button(new GUIContent("Bake Missing",
                        "Render an image for every entry that has none."),
                        EditorStyles.toolbarButton, GUILayout.Width(95f)))
                    _deferred = () => BakeImages(onlyMissing: true);

                if (GUILayout.Button(new GUIContent("Bake All",
                        "Re-render every entry's image. Overwrites existing PNGs."),
                        EditorStyles.toolbarButton, GUILayout.Width(75f)))
                    _deferred = () => BakeImages(onlyMissing: false);

                GUILayout.Space(8f);
                GUILayout.Label("Size", EditorStyles.miniLabel, GUILayout.Width(28f));
                int sizeIndex = Mathf.Max(0, Array.IndexOf(BakeSizes, _bakeSize));
                _bakeSize = BakeSizes[EditorGUILayout.Popup(sizeIndex,
                    BakeSizes.Select(s => $"{s}px").ToArray(),
                    EditorStyles.toolbarPopup, GUILayout.Width(70f))];

                if (GUILayout.Button(new GUIContent("Validate",
                        "Check ids, names and images. Reports only — nothing is committed."),
                        EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    _deferred = RunValidation;

                GUILayout.FlexibleSpace();

                _kingdomFilter = EditorGUILayout.Popup(_kingdomFilter, KingdomFilters,
                    EditorStyles.toolbarPopup, GUILayout.Width(90f));
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(180f));

                if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton, GUILayout.Width(90f)))
                    Selection.activeObject = _codex;
            }
        }

        void DrawStatus()
        {
            if (string.IsNullOrEmpty(_status)) return;
            EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Error : MessageType.Info);

            if (_lastReport == null || _lastReport.Warnings.Count == 0) return;
            EditorGUILayout.HelpBox(
                "Warnings:\n• " + string.Join("\n• ", _lastReport.Warnings), MessageType.Warning);
        }

        // ── List ─────────────────────────────────────────────────────────────────

        void DrawList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(300f)))
            {
                var entries = FilteredEntries();

                using (var scroll = new EditorGUILayout.ScrollViewScope(_listScroll,
                           GUILayout.ExpandHeight(true)))
                {
                    _listScroll = scroll.scrollPosition;

                    CodexKingdom? heading = null;
                    foreach (var entry in entries)
                    {
                        if (heading != entry.Kingdom)
                        {
                            heading = entry.Kingdom;
                            GUILayout.Space(4f);
                            GUILayout.Label(HeadingFor(entry.Kingdom), FrogletEditorPalette.SectionLabel);
                        }
                        DrawListRow(entry);
                    }

                    if (entries.Count == 0)
                        EditorGUILayout.HelpBox(
                            _codex.AllEntries().Count == 0
                                ? "Empty. Run Scan & Merge to harvest the project."
                                : "Nothing matches the current filter.",
                            MessageType.Info);
                }

                DrawListActions();
            }
        }

        void DrawListRow(CodexEntry entry)
        {
            bool selected = entry.Id == _selectedId;
            var accent = entry.ResolveAccent(AccentFor(entry.Kingdom));

            var row = GUILayoutUtility.GetRect(0f, 40f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                FrogletEditorPalette.DrawRect(row, selected
                    ? accent.WithAlpha(0.22f)
                    : FrogletEditorPalette.Surface.WithAlpha(0.35f));
                FrogletEditorPalette.DrawAccentStripe(row, accent);
            }

            var thumb = new Rect(row.x + 8f, row.y + 4f, 32f, 32f);
            if (entry.Image && entry.Image.texture)
                GUI.DrawTexture(thumb, entry.Image.texture, ScaleMode.ScaleToFit);
            else if (Event.current.type == EventType.Repaint)
                FrogletEditorPalette.DrawRect(thumb, FrogletEditorPalette.Muted.WithAlpha(0.18f));

            var text = new Rect(thumb.xMax + 8f, row.y + 3f, row.width - thumb.width - 26f, 18f);
            GUI.Label(text, entry.DisplayName, FrogletEditorPalette.CardTitle);

            var sub = new Rect(text.x, text.yMax - 1f, text.width, 16f);
            GUI.Label(sub, Subtitle(entry), FrogletEditorPalette.CardBody);

            if (GUI.Button(row, GUIContent.none, GUIStyle.none)) _selectedId = entry.Id;
        }

        string Subtitle(CodexEntry entry)
        {
            var parts = new List<string>();
            if (entry.Variants.Count > 0) parts.Add($"{entry.Variants.Count} variants");
            if (!entry.Image) parts.Add("no image");
            if (entry.LockAutoHarvest) parts.Add("locked");
            if (!entry.SourcePrefab) parts.Add("orphan");
            return parts.Count == 0 ? entry.Id : string.Join(" · ", parts);
        }

        void DrawListActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // Shown immediately, not deferred: ShowAsContext reads the CURRENT mouse
                // position, and by the end of the pass the event is a repaint. Its own callbacks
                // fire after the menu closes, so the list mutation is still safely outside layout.
                if (GUILayout.Button(new GUIContent("+ Entry", "Add a hand-authored entry."),
                        GUILayout.Height(22f)))
                    AddEntryMenu();

                using (new EditorGUI.DisabledScope(Selected == null))
                {
                    if (GUILayout.Button("Duplicate", GUILayout.Height(22f)))
                        _deferred = DuplicateSelected;
                    if (GUILayout.Button("Delete", GUILayout.Height(22f)))
                        _deferred = DeleteSelected;
                }
            }

            using (new EditorGUI.DisabledScope(Selected == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▲ Move Up", GUILayout.Height(20f))) _deferred = () => Move(-1);
                if (GUILayout.Button("▼ Move Down", GUILayout.Height(20f))) _deferred = () => Move(1);
            }
        }

        // ── Selection helpers ────────────────────────────────────────────────────

        CodexEntry Selected => _codex ? _codex.Find(_selectedId) : null;

        List<CodexEntry> FilteredEntries()
        {
            var all = _codex.AllEntries().Where(e => e != null);

            if (_kingdomFilter > 0)
            {
                var kingdom = (CodexKingdom)(_kingdomFilter - 1);
                all = all.Where(e => e.Kingdom == kingdom);
            }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                var needle = _search.Trim();
                all = all.Where(e =>
                    Contains(e.DisplayName, needle) || Contains(e.Id, needle) ||
                    Contains(e.Tagline, needle) || Contains(e.Description, needle));
            }

            return all
                .OrderBy(e => e.Kingdom)
                .ThenBy(e => e.SortOrder)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            static bool Contains(string haystack, string needle) =>
                !string.IsNullOrEmpty(haystack) &&
                haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string HeadingFor(CodexKingdom kingdom) => kingdom switch
        {
            CodexKingdom.Ethirion => "ETHIRIONS",
            CodexKingdom.Flora => "ECOLOGY · FLORA",
            CodexKingdom.Fauna => "ECOLOGY · FAUNA",
            _ => kingdom.ToString().ToUpperInvariant(),
        };

        static Color AccentFor(CodexKingdom kingdom) => kingdom switch
        {
            CodexKingdom.Ethirion => FrogletEditorPalette.Cyan,
            CodexKingdom.Flora => FrogletEditorPalette.Lime,
            CodexKingdom.Fauna => FrogletEditorPalette.Coral,
            _ => FrogletEditorPalette.Slate,
        };

        // ── Actions ──────────────────────────────────────────────────────────────

        void ScanAndMerge()
        {
            Undo.RecordObject(_codex, "Scan codex");
            _lastReport = CodexHarvester.ScanAndMerge(_codex);

            AssetDatabase.SaveAssets();
            FrogletToolChangeLedger.Record(ToolName, AssetPath);

            SetStatus(_lastReport.Summary +
                      (_lastReport.Orphans.Count > 0
                          ? "\nOrphaned (kept, never auto-deleted): " + string.Join(", ", _lastReport.Orphans)
                          : string.Empty),
                error: false);
        }

        void BakeImages(bool onlyMissing)
        {
            var targets = _codex.AllEntries()
                .Where(e => e != null && e.SourcePrefab && (!onlyMissing || !e.Image))
                .ToList();

            if (targets.Count == 0)
            {
                SetStatus(onlyMissing
                    ? "Every entry with a source prefab already has an image."
                    : "Nothing to bake — no entry has a source prefab yet. Run Scan & Merge first.",
                    error: false);
                return;
            }

            Undo.RecordObject(_codex, "Bake codex images");

            var written = new List<string>();
            var failures = new List<string>();
            int fellBack = 0;

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var entry = targets[i];
                    EditorUtility.DisplayProgressBar("Baking codex images",
                        $"{entry.DisplayName} ({i + 1}/{targets.Count})", (i + 1f) / targets.Count);

                    var result = CodexImageBaker.Bake(entry, _bakeSize);
                    if (result.Success)
                    {
                        written.Add(result.AssetPath);
                        if (result.FellBackToFlat) fellBack++;
                    }
                    else
                    {
                        failures.Add(result.Error);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.SetDirty(_codex);
            AssetDatabase.SaveAssets();

            FrogletToolChangeLedger.Record(ToolName, AssetPath);
            if (written.Count > 0) FrogletToolChangeLedger.Record(ToolName, written);

            var message = $"Baked {written.Count} image(s) at {_bakeSize}px.";
            if (fellBack > 0)
                message += $"\n{fellBack} rendered empty with their own materials and fell back to a " +
                           "flat silhouette — gameplay shaders that read per-frame globals do this. " +
                           "Tick 'Flat silhouette' on those entries to make the choice explicit.";
            if (failures.Count > 0)
                message += "\nFailed:\n• " + string.Join("\n• ", failures);

            SetStatus(message, failures.Count > 0);
        }

        void AddEntryMenu()
        {
            var menu = new GenericMenu();
            foreach (CodexKingdom kingdom in Enum.GetValues(typeof(CodexKingdom)))
                menu.AddItem(new GUIContent(kingdom.ToString()), false, () => AddEntry(kingdom));
            menu.ShowAsContext();
        }

        void AddEntry(CodexKingdom kingdom)
        {
            Undo.RecordObject(_codex, "Add codex entry");

            var entry = new CodexEntry
            {
                Kingdom = kingdom,
                DisplayName = "New " + kingdom,
                // Hand-authored by definition: a scan must not overwrite what someone just typed,
                // and it has no source asset to re-derive from anyway.
                LockAutoHarvest = true,
            };
            entry.Id = UniqueId(kingdom, "new-" + kingdom.ToString().ToLowerInvariant());

            _codex.ListFor(kingdom).Add(entry);
            _selectedId = entry.Id;
            Persist("Added an entry. It is locked against Scan & Merge — untick that once it has a " +
                    "source prefab you want facts harvested from.");
        }

        void DuplicateSelected()
        {
            var source = Selected;
            if (source == null) return;

            Undo.RecordObject(_codex, "Duplicate codex entry");

            var copy = Clone(source);
            copy.Id = UniqueId(source.Kingdom, source.Id);
            copy.DisplayName = source.DisplayName + " (copy)";
            copy.LockAutoHarvest = true;

            var list = _codex.ListFor(copy.Kingdom);
            list.Insert(Mathf.Clamp(list.IndexOf(source) + 1, 0, list.Count), copy);
            _selectedId = copy.Id;
            Persist($"Duplicated '{source.DisplayName}'. The copy is locked against Scan & Merge.");
        }

        /// <summary>
        /// A deep copy. Written out rather than round-tripped through JsonUtility: that helper
        /// encodes UnityEngine.Object references as instance IDs, which happen to resolve inside
        /// one editor session and are not something to build a duplicate button on.
        /// </summary>
        static CodexEntry Clone(CodexEntry source) => new()
        {
            Id = source.Id,
            Kingdom = source.Kingdom,
            DisplayName = source.DisplayName,
            Tagline = source.Tagline,
            Description = source.Description,
            Image = source.Image,
            SourcePrefab = source.SourcePrefab,
            AccentColor = source.AccentColor,
            UnlockedByDefault = source.UnlockedByDefault,
            DiscoveryKey = source.DiscoveryKey,
            PreviewYaw = source.PreviewYaw,
            PreviewPitch = source.PreviewPitch,
            PreviewPadding = source.PreviewPadding,
            FlatSilhouette = source.FlatSilhouette,
            SortOrder = source.SortOrder,
            LockAutoHarvest = source.LockAutoHarvest,
            Stats = new List<CodexStat>(source.Stats),
            Variants = source.Variants.Select(v => new CodexVariant
            {
                Label = v.Label,
                Element = v.Element,
                SourceConfig = v.SourceConfig,
                SourcePrefab = v.SourcePrefab,
                Image = v.Image,
                Stats = new List<CodexStat>(v.Stats),
            }).ToList(),
        };

        void DeleteSelected()
        {
            var entry = Selected;
            if (entry == null) return;

            if (!EditorUtility.DisplayDialog("Delete codex entry",
                    $"Delete '{entry.DisplayName}'?\n\nThe baked PNG is left on disk. If this entry " +
                    "has a source asset in the project, the next Scan & Merge will bring it back.",
                    "Delete", "Cancel"))
                return;

            Undo.RecordObject(_codex, "Delete codex entry");
            _codex.ListFor(entry.Kingdom).Remove(entry);
            _selectedId = null;
            Persist($"Deleted '{entry.DisplayName}'.");
        }

        /// <summary>
        /// Reordering writes <see cref="CodexEntry.SortOrder"/> rather than shuffling the list,
        /// because the list draws sorted and a positional move would appear to do nothing.
        /// </summary>
        void Move(int direction)
        {
            var entry = Selected;
            if (entry == null) return;

            var siblings = _codex.AllEntries()
                .Where(e => e != null && e.Kingdom == entry.Kingdom)
                .OrderBy(e => e.SortOrder)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            int index = siblings.IndexOf(entry);
            int target = index + direction;
            if (index < 0 || target < 0 || target >= siblings.Count) return;

            Undo.RecordObject(_codex, "Reorder codex");
            siblings.RemoveAt(index);
            siblings.Insert(target, entry);
            for (int i = 0; i < siblings.Count; i++) siblings[i].SortOrder = i;

            Persist(null);
        }

        string UniqueId(CodexKingdom kingdom, string desired)
        {
            var baseId = desired.Contains('.')
                ? desired
                : $"{kingdom.ToString().ToLowerInvariant()}.{CodexHarvester.Slug(desired)}";

            var id = baseId;
            int suffix = 2;
            while (_codex.Find(id) != null) id = $"{baseId}-{suffix++}";
            return id;
        }

        void Persist(string message)
        {
            EditorUtility.SetDirty(_codex);
            AssetDatabase.SaveAssets();
            FrogletToolChangeLedger.Record(ToolName, AssetPath);
            if (message != null) SetStatus(message, error: false);
        }

        void SetStatus(string message, bool error)
        {
            _status = message;
            _statusIsError = error;
        }

        // ── Validation ───────────────────────────────────────────────────────────

        void RunValidation()
        {
            AssetDatabase.SaveAssets();
            var result = Validate();
            SetStatus(result.Passed
                    ? "Validation passed — " + result.Summary
                    : result.Summary + "\n• " + string.Join("\n• ", result.Problems),
                !result.Passed);
        }

        /// <summary>
        /// Reports only. The codex asset and its PNGs are recorded on the tool ledger as they are
        /// written, so <b>FrogletTools &gt; Build &gt; Pending Tool Changes</b> lists them and they
        /// are committed by hand like any other change — this window does not push.
        /// </summary>
        FrogletToolValidation Validate()
        {
            var problems = new List<string>();
            var entries = _codex.AllEntries().Where(e => e != null).ToList();

            if (entries.Count == 0)
                problems.Add("The codex is empty — run Scan & Merge before shipping it.");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Id))
                    problems.Add($"'{entry.DisplayName}' has no id — the UI and any save file key on it.");
                else if (!seen.Add(entry.Id))
                    problems.Add($"Duplicate id '{entry.Id}' — CodexSO.Find would return only one of them.");

                if (string.IsNullOrWhiteSpace(entry.DisplayName))
                    problems.Add($"'{entry.Id}' has no display name.");
            }

            int missingImages = entries.Count(e => !e.Image);
            if (missingImages > 0)
                problems.Add($"{missingImages} entr{(missingImages == 1 ? "y has" : "ies have")} no " +
                             "image — run Bake Missing.");

            return problems.Count == 0
                ? FrogletToolValidation.Pass($"{entries.Count} entries, all keyed and illustrated.")
                : FrogletToolValidation.Fail($"{problems.Count} problem(s) in the codex.", problems);
        }
    }
}
