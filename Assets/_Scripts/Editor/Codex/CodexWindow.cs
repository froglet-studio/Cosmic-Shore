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
    /// <b>FrogletTools &gt; Interface &gt; Codex</b> — the one place the in-game encyclopedia is
    /// authored, across all three of its kingdoms: <b>Ethirions</b> (every crystal),
    /// <b>Ecology</b> (every lifeform) and <b>Tools</b> (every freestyle toy, categorised by the
    /// fundamental it changes).
    ///
    /// <para><b>The layout is a drill-down, not a split view.</b> A glowing KINGDOM TAB strip
    /// sits on top; under it, the selected kingdom's entries as a browse grid of illustrated
    /// cards (grouped by <see cref="CodexEntry.Group"/> where the kingdom divides); click a card
    /// and the whole body becomes that entry's PAGE, with its own DETAILS / EDIT tabs — details
    /// is the encyclopedia page as a reader meets it (portrait, prose, facts, the variant grid),
    /// edit is everything a curator changes (identity, copy, pose and bake, fact editing,
    /// ordering). The old two-pane list-plus-inspector layout is retired.</para>
    ///
    /// <para>It still does three things. <b>Scan &amp; Merge</b> walks the project and folds
    /// every ethirion, every ecology species and every tool into
    /// <c>Assets/Resources/Codex.asset</c>, harvesting the facts it can re-derive and leaving
    /// authored prose alone. <b>Bake</b> renders each entry's hero image and each distinct
    /// variant's icon. And the EDIT tab changes any entry by hand — because a generated
    /// encyclopedia with no room for a writer is a spreadsheet.</para>
    ///
    /// <para>The runtime UI reads the same asset through <c>CodexSO.Load()</c>; there is no second
    /// data path and nothing to wire per scene. Nothing here pushes: every file written is
    /// recorded on the tool ledger, so <b>FrogletTools &gt; Build &gt; Pending Tool Changes</b>
    /// lists it and it is committed by hand.</para>
    /// </summary>
    public partial class CodexWindow : EditorWindow
    {
        public const string ToolName = "Codex";
        const string AssetPath = "Assets/Resources/" + CodexSO.ResourcePath + ".asset";

        const float SectionHeaderHeight = 24f;
        const float GroupHeaderHeight = 18f;

        const float KingdomTabWidth = 158f;
        const float KingdomTabHeight = 34f;

        const float BrowseCardWidth = 150f;
        const float BrowseCardHeight = 178f;
        const float BrowseThumbSize = 110f;

        CodexSO _codex;

        /// <summary>The kingdom whose tab is lit. Browse and search operate within it.</summary>
        CodexKingdom _tab = CodexKingdom.Ethirion;

        /// <summary>Open entry. Null = the kingdom's browse grid.</summary>
        string _selectedId;

        string _search = string.Empty;
        int _bakeSize = 512;

        Vector2 _browseScroll;
        Vector2 _detailScroll;
        CodexHarvestReport _lastReport;
        string _status = string.Empty;
        bool _statusIsError;

        /// <summary>
        /// Queued mutation. IMGUI is mid-layout while the buttons are drawn, so adding to or
        /// removing from the list the loop is walking throws; every action runs after the pass.
        /// </summary>
        Action _deferred;

        static readonly int[] BakeSizes = { 256, 512, 1024 };

        [MenuItem("FrogletTools/Interface/Codex")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 4,
            Description = "Harvest, edit and illustrate every ethirion, lifeform and tool for the " +
                          "in-game encyclopedia.",
            DocPath = "Docs/CODEX.md")]
        static void Open()
        {
            var window = GetWindow<CodexWindow>("Codex");
            window.minSize = new Vector2(980f, 620f);
            window.Show();
        }

        void OnEnable() => _codex = LoadOrCreate();

        static Color ToolAccent => FrogletEditorPalette.ColorFor(FrogletToolCategory.Interface);

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
                "Codex",
                "Every ethirion, every lifeform and every tool, as the in-game encyclopedia reads them.",
                ToolAccent);

            DrawKingdomTabs();
            DrawToolbar();
            DrawStatus();

            var entry = Selected;
            if (entry != null) DrawEntryPage(entry);
            else DrawBrowse();

            DrawFooter();

            if (_deferred == null) return;
            var action = _deferred;
            _deferred = null;
            action();
            Repaint();
        }

        // ── Kingdom tabs ─────────────────────────────────────────────────────────

        /// <summary>
        /// The top-level navigation: one glowing tab per kingdom, count pill on each. Clicking a
        /// tab always lands on that kingdom's BROWSE grid — an open entry is closed, because the
        /// tab strip is "where am I", and an entry page under the wrong lit tab is a lie.
        /// </summary>
        void DrawKingdomTabs()
        {
            var all = _codex.AllEntries();

            GUILayout.Space(9f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12f);
                foreach (CodexKingdom kingdom in Enum.GetValues(typeof(CodexKingdom)))
                {
                    int count = all.Count(e => e != null && e.Kingdom == kingdom);

                    var r = GUILayoutUtility.GetRect(KingdomTabWidth, KingdomTabHeight,
                        GUILayout.Width(KingdomTabWidth), GUILayout.Height(KingdomTabHeight));

                    if (FrogletEditorPalette.GlowTab(r, HeadingFor(kingdom), AccentFor(kingdom),
                            _tab == kingdom, count.ToString(),
                            $"Browse the {count} {kingdom} entries."))
                    {
                        var captured = kingdom;
                        _deferred = () =>
                        {
                            _tab = captured;
                            _selectedId = null;
                            _selectedVariant = null;
                            GUI.FocusControl(null);
                        };
                    }

                    GUILayout.Space(8f);
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(9f);
        }

        // ── Toolbar ──────────────────────────────────────────────────────────────

        /// <summary>Search (scoped to the lit tab) plus the four whole-codex actions.</summary>
        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(12f);
                GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.Width(44f));
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField,
                    GUILayout.Width(200f));
                if (!string.IsNullOrEmpty(_search) &&
                    GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20f)))
                {
                    _search = string.Empty;
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();

                if (FrogletEditorPalette.ColorButton("Scan & Merge", FrogletEditorPalette.Info, 116f, 24f,
                        "Re-read the project. Adds new entries and refreshes harvested facts; never " +
                        "touches authored prose, ordering, discovery or the preview pose."))
                    _deferred = ScanAndMerge;

                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Bake Missing", FrogletEditorPalette.Cyan, 104f, 24f,
                        "Render an image for every entry and variant that has none."))
                    _deferred = () => BakeImages(onlyMissing: true);

                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Bake All", FrogletEditorPalette.Violet, 82f, 24f,
                        "Re-render every image. Overwrites existing PNGs.", outline: true))
                    _deferred = () => BakeImages(onlyMissing: false);

                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Validate", FrogletEditorPalette.Ok, 82f, 24f,
                        "Check ids, names and images. Reports only — nothing is committed.",
                        outline: true))
                    _deferred = RunValidation;

                GUILayout.Space(8f);
                int sizeIndex = Mathf.Max(0, Array.IndexOf(BakeSizes, _bakeSize));
                _bakeSize = BakeSizes[EditorGUILayout.Popup(sizeIndex,
                    BakeSizes.Select(s => $"{s} px").ToArray(), GUILayout.Width(72f))];

                GUILayout.Space(8f);
                if (GUILayout.Button("Select Asset", EditorStyles.miniButton, GUILayout.Width(88f)))
                    Selection.activeObject = _codex;
                GUILayout.Space(12f);
            }
            GUILayout.Space(4f);
        }

        // ── Chrome shared with the entry page ────────────────────────────────────

        /// <summary>
        /// A section bar in the house style: tinted ground, accent stripe, accent-coloured label
        /// and an optional count pill. Returns true when it was clicked.
        /// </summary>
        static bool SectionBar(string label, Color accent, string pill = null, bool clickable = false)
        {
            var a = FrogletEditorPalette.Adapt(accent);
            var bar = GUILayoutUtility.GetRect(0f, SectionHeaderHeight, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                FrogletEditorPalette.DrawRect(bar, a.WithAlpha(0.14f));
                FrogletEditorPalette.DrawAccentStripe(bar, a, 4f);
            }

            GUI.Label(new Rect(bar.x + 13f, bar.y, bar.width - 70f, bar.height), label,
                new GUIStyle(FrogletEditorPalette.SectionLabel) { normal = { textColor = a } });

            if (!string.IsNullOrEmpty(pill))
                FrogletEditorPalette.StatusPill(
                    new Rect(bar.xMax - 54f, bar.y + 4f, 46f, bar.height - 8f), pill, accent);

            if (!clickable) return false;

            EditorGUIUtility.AddCursorRect(bar, MouseCursor.Link);
            return GUI.Button(bar, GUIContent.none, GUIStyle.none);
        }

        /// <summary>
        /// A sub-heading INSIDE a kingdom — the tool categories today. Deliberately quieter than
        /// <see cref="SectionBar"/> (no ground, a thinner stripe, muted text): it is a division
        /// within a place, not a place.
        /// </summary>
        static void GroupBar(string label, Color accent)
        {
            var a = FrogletEditorPalette.Adapt(accent);
            var bar = GUILayoutUtility.GetRect(0f, GroupHeaderHeight, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
                FrogletEditorPalette.DrawAccentStripe(bar, a.WithAlpha(0.55f), 2f);

            GUI.Label(new Rect(bar.x + 13f, bar.y, bar.width - 20f, bar.height), label,
                new GUIStyle(FrogletEditorPalette.CardBody)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleLeft,
                    normal = { textColor = a.WithAlpha(0.85f) },
                });
        }

        void DrawStatus()
        {
            if (string.IsNullOrEmpty(_status)) return;

            var accent = _statusIsError ? FrogletEditorPalette.Error : FrogletEditorPalette.Ok;

            var card = EditorGUILayout.BeginVertical(GUIStyle.none);
            if (Event.current.type == EventType.Repaint && card.height > 1f)
            {
                FrogletEditorPalette.DrawCard(card, FrogletEditorPalette.Adapt(accent).WithAlpha(0.10f),
                    FrogletEditorPalette.Adapt(accent).WithAlpha(0.55f));
                FrogletEditorPalette.DrawAccentStripe(card, accent, 4f);
            }

            GUILayout.Space(5f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(13f);
                var pill = GUILayoutUtility.GetRect(64f, 16f, GUILayout.Width(64f), GUILayout.Height(16f));
                FrogletEditorPalette.StatusPill(pill, _statusIsError ? "ISSUES" : "DONE", accent);

                GUILayout.Space(6f);
                GUILayout.Label(_status, FrogletEditorPalette.CardBodyWrapped);

                if (GUILayout.Button("✕", EditorStyles.miniLabel, GUILayout.Width(18f)))
                    _deferred = () => { _status = string.Empty; _lastReport = null; };
                GUILayout.Space(6f);
            }
            GUILayout.Space(5f);
            EditorGUILayout.EndVertical();

            if (_lastReport is { Warnings: { Count: > 0 } })
                EditorGUILayout.HelpBox(
                    "Warnings:\n• " + string.Join("\n• ", _lastReport.Warnings), MessageType.Warning);

            GUILayout.Space(4f);
        }

        void DrawFooter()
        {
            var all = _codex.AllEntries().Where(e => e != null).ToList();
            int illustrated = all.Count(e => e.Image);

            // Variant icons are counted only where one is actually WANTED. Counting every variant
            // would report a permanent shortfall for the ones that correctly have no art of their
            // own - an element borrows its ethirion's picture, a domain draws its colour - and a
            // number that can never reach its target is a number nobody reads twice.
            int wanted = 0, drawn = 0;
            foreach (var entry in all)
                foreach (var variant in entry.Variants)
                {
                    if (!CodexImageBaker.CanBake(entry, variant)) continue;
                    wanted++;
                    if (variant.Image) drawn++;
                }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(
                    $"{all.Count} entries  ·  {_codex.Ethirions.Count} ethirions  ·  " +
                    $"{all.Count(e => e.Kingdom == CodexKingdom.Flora)} flora  ·  " +
                    $"{all.Count(e => e.Kingdom == CodexKingdom.Fauna)} fauna  ·  " +
                    $"{all.Count(e => e.Kingdom == CodexKingdom.Tool)} tools  ·  " +
                    $"{illustrated}/{all.Count} illustrated" +
                    (wanted > 0 ? $"  ·  {drawn}/{wanted} variant icons" : string.Empty),
                    EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();
                GUILayout.Label("Written files are recorded for FrogletTools ▸ Build ▸ Pending Tool Changes",
                    EditorStyles.miniLabel);
            }
        }

        // ── Browse grid ──────────────────────────────────────────────────────────

        /// <summary>
        /// The lit kingdom as a grid of illustrated cards, grouped where the kingdom divides.
        /// This is the page the tab strip navigates between; clicking a card opens its entry.
        /// </summary>
        void DrawBrowse()
        {
            var entries = BrowseEntries();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_browseScroll,
                       GUILayout.ExpandHeight(true)))
            {
                _browseScroll = scroll.scrollPosition;

                if (entries.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        _codex.AllEntries().Count == 0
                            ? "Empty. Run Scan & Merge to harvest the project."
                            : string.IsNullOrWhiteSpace(_search)
                                ? $"No {_tab} entries yet. Run Scan & Merge, or add one below."
                                : "Nothing in this kingdom matches the search.",
                        MessageType.Info);
                }
                else
                {
                    int columns = Mathf.Max(1,
                        Mathf.FloorToInt((position.width - 28f) / (BrowseCardWidth + 8f)));
                    var accent = AccentFor(_tab);

                    // entries are ordered by group, so one pass with a running key draws every
                    // sub-heading in place — no second grouping that could disagree with the sort.
                    int i = 0;
                    while (i < entries.Count)
                    {
                        var group = entries[i].Group ?? string.Empty;
                        int end = i;
                        while (end < entries.Count && (entries[end].Group ?? string.Empty) == group)
                            end++;

                        if (group.Length > 0)
                        {
                            GUILayout.Space(4f);
                            GroupBar(GroupLabel(group), accent);
                            GUILayout.Space(3f);
                        }

                        for (int row = i; row < end; row += columns)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Space(12f);
                                for (int c = 0; c < columns && row + c < end; c++)
                                {
                                    DrawBrowseCard(entries[row + c]);
                                    GUILayout.Space(8f);
                                }
                                GUILayout.FlexibleSpace();
                            }
                            GUILayout.Space(8f);
                        }

                        i = end;
                    }
                }

                GUILayout.Space(6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    if (FrogletEditorPalette.ColorButton($"+ New {_tab} entry",
                            FrogletEditorPalette.Ok, 150f, 22f,
                            "Add a hand-authored page to this kingdom. It is locked against " +
                            "Scan & Merge until you say otherwise.", outline: true))
                        _deferred = () => AddEntry(_tab);
                }
                GUILayout.Space(8f);
            }
        }

        void DrawBrowseCard(CodexEntry entry)
        {
            var accent = FrogletEditorPalette.Adapt(entry.ResolveAccent(AccentFor(entry.Kingdom)));

            var card = GUILayoutUtility.GetRect(BrowseCardWidth, BrowseCardHeight,
                GUILayout.Width(BrowseCardWidth), GUILayout.Height(BrowseCardHeight));
            bool hover = card.Contains(Event.current.mousePosition);

            if (Event.current.type == EventType.Repaint)
            {
                FrogletEditorPalette.DrawCard(card,
                    hover ? FrogletEditorPalette.SurfaceRaised : FrogletEditorPalette.Surface,
                    hover ? accent.WithAlpha(0.85f) : FrogletEditorPalette.Muted.WithAlpha(0.25f));
                FrogletEditorPalette.DrawRect(
                    new Rect(card.x + 1f, card.y + 1f, card.width - 2f, 3f), accent.WithAlpha(0.9f));
            }

            var thumb = new Rect(card.x + (card.width - BrowseThumbSize) * 0.5f, card.y + 12f,
                BrowseThumbSize, BrowseThumbSize);
            if (entry.Image && entry.Image.texture)
                GUI.DrawTexture(thumb, entry.Image.texture, ScaleMode.ScaleToFit);
            else if (Event.current.type == EventType.Repaint)
            {
                FrogletEditorPalette.DrawRect(thumb, FrogletEditorPalette.Muted.WithAlpha(0.12f));
                GUI.Label(thumb, "not baked",
                    new GUIStyle(FrogletEditorPalette.CardBody) { alignment = TextAnchor.MiddleCenter });
            }

            GUI.Label(new Rect(card.x + 6f, thumb.yMax + 5f, card.width - 12f, 18f),
                entry.DisplayName,
                new GUIStyle(FrogletEditorPalette.CardTitle) { alignment = TextAnchor.MiddleCenter });
            GUI.Label(new Rect(card.x + 6f, thumb.yMax + 23f, card.width - 12f, 14f),
                Subtitle(entry),
                new GUIStyle(FrogletEditorPalette.CardBody) { alignment = TextAnchor.MiddleCenter });

            var flag = FlagFor(entry);
            if (flag.label != null)
                FrogletEditorPalette.StatusPill(
                    new Rect(card.x + (card.width - 66f) * 0.5f, card.yMax - 20f, 66f, 15f),
                    flag.label, flag.color);

            EditorGUIUtility.AddCursorRect(card, MouseCursor.Link);
            if (!GUI.Button(card, GUIContent.none, GUIStyle.none)) return;

            var id = entry.Id;
            _deferred = () => OpenEntry(id);
        }

        void OpenEntry(string id)
        {
            _selectedId = id;
            _selectedVariant = null;
            _entryTab = 0;
            _detailScroll = Vector2.zero;
            GUI.FocusControl(null);
        }

        void CloseEntry()
        {
            _selectedId = null;
            _selectedVariant = null;
            GUI.FocusControl(null);
        }

        // ── Selection helpers ────────────────────────────────────────────────────

        CodexEntry Selected => _codex ? _codex.Find(_selectedId) : null;

        /// <summary>The lit kingdom's entries, search-filtered, in draw order.</summary>
        List<CodexEntry> BrowseEntries()
        {
            var all = _codex.EntriesOf(_tab).Where(e => e != null);

            if (!string.IsNullOrWhiteSpace(_search))
            {
                var needle = _search.Trim();
                all = all.Where(e =>
                    Contains(e.DisplayName, needle) || Contains(e.Id, needle) ||
                    Contains(e.Tagline, needle) || Contains(e.Description, needle));
            }

            return all
                .OrderBy(e => e.Group ?? string.Empty, StringComparer.Ordinal)
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
            CodexKingdom.Tool => "TOOLS",
            _ => kingdom.ToString().ToUpperInvariant(),
        };

        static Color AccentFor(CodexKingdom kingdom) => kingdom switch
        {
            CodexKingdom.Ethirion => FrogletEditorPalette.Cyan,
            CodexKingdom.Flora => FrogletEditorPalette.Lime,
            CodexKingdom.Fauna => FrogletEditorPalette.Coral,
            CodexKingdom.Tool => FrogletEditorPalette.Violet,
            _ => FrogletEditorPalette.Slate,
        };

        /// <summary>
        /// A group key without the numeric prefix that orders it. The prefix ("1 · Pilot") is how
        /// the harvester states an order that is not alphabetical; it is not something a reader
        /// should have to look at.
        /// </summary>
        static string GroupLabel(string group)
        {
            int separator = group.IndexOf('·');
            var label = separator >= 0 ? group[(separator + 1)..].Trim() : group;
            return label.ToUpperInvariant();
        }

        /// <summary>The one thing about this entry worth saying at a glance, or nothing.</summary>
        static (string label, Color color) FlagFor(CodexEntry entry)
        {
            if (!entry.HasSource && !entry.LockAutoHarvest)
                return ("ORPHAN", FrogletEditorPalette.Error);
            if (!entry.Image) return ("NO IMAGE", FrogletEditorPalette.Warn);
            if (entry.LockAutoHarvest) return ("LOCKED", FrogletEditorPalette.Slate);
            return (null, default);
        }

        static string Subtitle(CodexEntry entry)
        {
            int variants = entry.Variants.Count;
            string prose = string.IsNullOrWhiteSpace(entry.Description) ? "no description" : "written";
            return variants > 0 ? $"{variants} variants · {prose}" : prose;
        }

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

        /// <summary>
        /// Bake every portrait AND every variant icon that can be drawn. One pass over both
        /// because they are one job to the person pressing the button — and because a run that
        /// did portraits only would leave the grids below them half-illustrated with no button
        /// that obviously fills them in.
        /// </summary>
        void BakeImages(bool onlyMissing)
        {
            var jobs = new List<(CodexEntry entry, CodexVariant variant)>();

            foreach (var entry in _codex.AllEntries())
            {
                if (entry == null) continue;

                if (CodexImageBaker.CanBake(entry) && (!onlyMissing || !entry.Image))
                    jobs.Add((entry, null));

                foreach (var variant in entry.Variants)
                    if (CodexImageBaker.CanBake(entry, variant) && (!onlyMissing || !variant.Image))
                        jobs.Add((entry, variant));
            }

            if (jobs.Count == 0)
            {
                SetStatus(onlyMissing
                    ? "Every entry and variant that can be illustrated already has an image."
                    : "Nothing to bake — no entry has a source asset yet. Run Scan & Merge first.",
                    error: false);
                return;
            }

            Undo.RecordObject(_codex, "Bake codex images");

            var written = new List<string>();
            var failures = new List<string>();
            int fellBack = 0;
            int icons = 0;

            try
            {
                for (int i = 0; i < jobs.Count; i++)
                {
                    var (entry, variant) = jobs[i];
                    var label = variant == null
                        ? entry.DisplayName
                        : $"{entry.DisplayName} · {variant.Label}";

                    EditorUtility.DisplayProgressBar("Baking codex images",
                        $"{label} ({i + 1}/{jobs.Count})", (i + 1f) / jobs.Count);

                    var result = variant == null
                        ? CodexImageBaker.Bake(entry, _bakeSize)
                        : CodexImageBaker.Bake(entry, variant, _bakeSize);

                    if (result.Success)
                    {
                        written.Add(result.AssetPath);
                        if (variant != null) icons++;
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

            var message = $"Baked {written.Count} image(s) at {_bakeSize}px" +
                          (icons > 0 ? $" — {written.Count - icons} portraits, {icons} variant icons." : ".");
            if (fellBack > 0)
                message += $"\n{fellBack} rendered empty with their own materials and fell back to a " +
                           "flat silhouette — gameplay shaders that read per-frame globals do this. " +
                           "Tick 'Flat silhouette' on those entries to make the choice explicit.";
            if (failures.Count > 0)
                message += "\nFailed:\n• " + string.Join("\n• ", failures);

            SetStatus(message, failures.Count > 0);
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
            OpenEntry(entry.Id);
            _entryTab = 1; // a brand-new page has nothing to look at yet — land on EDIT
            Persist("Added an entry. It is locked against Scan & Merge — untick that once it has a " +
                    "source prefab you want facts harvested from.");
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
            SourceConfig = source.SourceConfig,
            AccentColor = source.AccentColor,
            UnlockedByDefault = source.UnlockedByDefault,
            DiscoveryKey = source.DiscoveryKey,
            PreviewYaw = source.PreviewYaw,
            PreviewPitch = source.PreviewPitch,
            PreviewPadding = source.PreviewPadding,
            FlatSilhouette = source.FlatSilhouette,
            Group = source.Group,
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
                AccentColor = v.AccentColor,
                Stats = new List<CodexStat>(v.Stats),
            }).ToList(),
        };

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
            OpenEntry(copy.Id);
            Persist($"Duplicated '{source.DisplayName}'. The copy is locked against Scan & Merge.");
        }

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
            CloseEntry();
            Persist($"Deleted '{entry.DisplayName}'.");
        }

        /// <summary>
        /// Reordering writes <see cref="CodexEntry.SortOrder"/> rather than shuffling the list,
        /// because the grid draws sorted and a positional move would appear to do nothing.
        /// Siblings are the entries drawn under the SAME sub-heading — ordering across a group
        /// boundary would let a move appear to do nothing (the grid re-groups it straight back)
        /// while silently rewriting everyone's SortOrder.
        /// </summary>
        void Move(int direction)
        {
            var entry = Selected;
            if (entry == null) return;

            var siblings = _codex.AllEntries()
                .Where(e => e != null && e.Kingdom == entry.Kingdom &&
                            (e.Group ?? string.Empty) == (entry.Group ?? string.Empty))
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
        /// are committed by hand — this window does not push.
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
