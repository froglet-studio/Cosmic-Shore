using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// FrogletTools &gt; Froglet Master Tool - the single front door to every editor tool in the
    /// project.
    ///
    /// Layout is a card grid grouped into collapsible, colour-coded sections: one card per tool
    /// with its name, what it does, and a five-dot importance rating. Click a card to launch it.
    /// Within a section the most important tools come first.
    ///
    /// Tools are DISCOVERED, never registered: see <see cref="FrogletToolRegistry"/>. Any
    /// <c>[MenuItem("FrogletTools/...")]</c> in the project's assemblies shows up here the moment
    /// it compiles - nothing else to wire up.
    /// </summary>
    public sealed class FrogletMasterToolWindow : EditorWindow
    {
        const float CardMinWidth = 256f;
        const float CardHeight = 74f;
        const float CardGap = 8f;
        const float SectionHeaderHeight = 24f;
        const float SidePadding = 10f;
        const float ScrollbarAllowance = 16f;

        string _search = "";
        Vector2 _scroll;
        readonly HashSet<FrogletToolCategory> _collapsed = new();

        [MenuItem("FrogletTools/Froglet Master Tool", false, -100)]
        [FrogletTool(FrogletToolCategory.Misc, Importance = 5,
            Description = "This board. Every Froglet editor tool in one place.",
            DocPath = "Docs/TOOLING.md")]
        public static void Open()
        {
            var w = GetWindow<FrogletMasterToolWindow>("Froglet Tools");
            w.minSize = new Vector2(620f, 420f);
            w.Show();
        }

        void OnEnable() => FrogletToolRegistry.Refresh();

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "Froglet Master Tool",
                "Every editor tool in the project, grouped by what it touches. Click a card to open it.",
                FrogletEditorPalette.Jade);

            DrawToolbar();

            var tools = Filter(FrogletToolRegistry.All);

            if (tools.Count == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(_search)
                        ? "No tools discovered. A tool appears here as soon as its [MenuItem] path starts with \"FrogletTools/\"."
                        : $"No tool matches \"{_search}\".",
                    MessageType.Info);
                DrawFooter(0);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                GUILayout.Space(6);
                foreach (var section in tools.GroupBy(t => t.Category).OrderBy(g => g.Key))
                {
                    var ordered = section
                        .OrderByDescending(t => t.Importance)
                        .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    DrawSection(section.Key, ordered);
                }
                GUILayout.Space(10);
            }
            EditorGUILayout.EndScrollView();

            DrawFooter(tools.Count);
        }

        // ── Chrome ───────────────────────────────────────────────────────────────

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label("Search", EditorStyles.miniLabel, GUILayout.Width(44));
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(140));
                if (!string.IsNullOrEmpty(_search) &&
                    GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    _search = "";
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Expand all", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    _collapsed.Clear();

                if (GUILayout.Button("Collapse all", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    foreach (var c in FrogletToolRegistry.All.Select(t => t.Category).Distinct())
                        _collapsed.Add(c);

                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(56)))
                {
                    FrogletToolRegistry.Refresh();
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        static void DrawFooter(int shown)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label($"{shown} shown / {FrogletToolRegistry.All.Count} tools", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Add a [MenuItem(\"FrogletTools/…\")] and it appears here automatically",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Sections ─────────────────────────────────────────────────────────────

        void DrawSection(FrogletToolCategory category, List<FrogletToolEntry> tools)
        {
            var accent = FrogletEditorPalette.Adapt(FrogletEditorPalette.ColorFor(category));
            bool collapsed = _collapsed.Contains(category);

            var header = GUILayoutUtility.GetRect(0, SectionHeaderHeight, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawRect(header, accent.WithAlpha(0.14f));
            FrogletEditorPalette.DrawAccentStripe(header, accent, 4f);

            GUI.Label(new Rect(header.x + 13f, header.y, header.width - 70f, header.height),
                $"{(collapsed ? "▸" : "▾")}  {FrogletEditorPalette.LabelFor(category).ToUpperInvariant()}",
                new GUIStyle(FrogletEditorPalette.SectionLabel) { normal = { textColor = accent } });

            FrogletEditorPalette.StatusPill(
                new Rect(header.xMax - 46f, header.y + 4f, 38f, header.height - 8f),
                tools.Count.ToString(), FrogletEditorPalette.ColorFor(category));

            if (GUI.Button(header, GUIContent.none, GUIStyle.none))
            {
                if (collapsed) _collapsed.Remove(category);
                else _collapsed.Add(category);
            }
            EditorGUIUtility.AddCursorRect(header, MouseCursor.Link);

            GUILayout.Space(CardGap);
            if (!collapsed)
            {
                DrawCardGrid(tools);
                GUILayout.Space(6f);
            }
        }

        /// <summary>
        /// Flows the cards into as many columns as the window is wide enough for.
        ///
        /// Column count is derived from <see cref="EditorGUIUtility.currentViewWidth"/> rather than
        /// from a laid-out rect, because a rect's width is meaningless during the Layout event -
        /// deriving it from the rect would emit a different number of controls in Layout than in
        /// Repaint and trip IMGUI's control-count check.
        /// </summary>
        void DrawCardGrid(List<FrogletToolEntry> tools)
        {
            float avail = Mathf.Max(CardMinWidth,
                EditorGUIUtility.currentViewWidth - SidePadding * 2f - ScrollbarAllowance);

            int columns = Mathf.Max(1, Mathf.FloorToInt((avail + CardGap) / (CardMinWidth + CardGap)));
            float cardWidth = (avail - CardGap * (columns - 1)) / columns;

            for (int i = 0; i < tools.Count; i += columns)
            {
                var row = GUILayoutUtility.GetRect(0, CardHeight, GUILayout.ExpandWidth(true));
                for (int c = 0; c < columns && i + c < tools.Count; c++)
                {
                    var rect = new Rect(row.x + SidePadding + c * (cardWidth + CardGap),
                                        row.y, cardWidth, row.height);
                    DrawToolCard(rect, tools[i + c]);
                }
                GUILayout.Space(CardGap);
            }
        }

        // ── The card ─────────────────────────────────────────────────────────────

        void DrawToolCard(Rect r, FrogletToolEntry tool)
        {
            var accent = FrogletEditorPalette.Adapt(FrogletEditorPalette.ColorFor(tool.Category));
            bool hover = r.Contains(Event.current.mousePosition);

            FrogletEditorPalette.DrawCard(
                r,
                hover ? FrogletEditorPalette.SurfaceRaised : FrogletEditorPalette.Surface,
                accent.WithAlpha(hover ? 0.95f : 0.4f));
            FrogletEditorPalette.DrawAccentStripe(r, accent, 3f);

            const float pad = 11f;
            float dotsWidth = 5 * 7f;

            var titleRect = new Rect(r.x + pad, r.y + 8f, r.width - pad * 2f - dotsWidth - 6f, 17f);
            GUI.Label(titleRect, tool.DisplayName, new GUIStyle(FrogletEditorPalette.CardTitle)
            {
                normal = { textColor = hover ? accent : FrogletEditorPalette.HeaderText },
            });

            DrawImportanceDots(new Rect(r.xMax - pad - dotsWidth, r.y + 13f, dotsWidth, 6f),
                tool.Importance, accent);

            var body = tool.Description;
            if (string.IsNullOrEmpty(body))
                body = tool.Group != null ? $"FrogletTools ▸ {tool.Group}" : "FrogletTools";

            var descRect = new Rect(r.x + pad, r.y + 27f, r.width - pad * 2f, r.height - 34f);
            GUI.Label(descRect, body, new GUIStyle(FrogletEditorPalette.CardBody) { wordWrap = true });

            // DOCS chip — only on documented tools. Drawn (and its button emitted) BEFORE the
            // whole-card button so a click on the chip opens the doc instead of launching.
            if (!string.IsNullOrEmpty(tool.DocPath))
            {
                var docRect = new Rect(r.xMax - pad - 44f, r.yMax - 21f, 44f, 15f);
                FrogletEditorPalette.StatusPill(docRect, "DOCS", accent);
                if (GUI.Button(docRect, new GUIContent("", $"Open the documentation on GitHub\n{tool.DocPath}"), GUIStyle.none))
                {
                    FrogletDocLinks.Open(tool.DocPath);
                    GUIUtility.ExitGUI();
                }
            }

            if (GUI.Button(r, new GUIContent("", $"{tool.MenuPath}\n{tool.DeclaringType}"), GUIStyle.none))
                tool.Invoke();
            if (hover) EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);

            HandleCardContextMenu(r, tool);
        }

        /// <summary>Five dots, filled up to the tool's importance - the ranking without a chart.</summary>
        static void DrawImportanceDots(Rect r, int importance, Color accent)
        {
            const float dot = 5f;
            const float step = 7f;
            for (int i = 0; i < 5; i++)
            {
                var d = new Rect(r.x + i * step, r.y, dot, dot);
                FrogletEditorPalette.DrawRect(d, i < importance
                    ? accent
                    : FrogletEditorPalette.Muted.WithAlpha(0.28f));
            }
        }

        static void HandleCardContextMenu(Rect r, FrogletToolEntry tool)
        {
            var e = Event.current;
            if (e.type != EventType.ContextClick || !r.Contains(e.mousePosition)) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Launch"), false, tool.Invoke);
            menu.AddSeparator("");
            if (!string.IsNullOrEmpty(tool.DocPath))
                menu.AddItem(new GUIContent("Open documentation"), false,
                    () => FrogletDocLinks.Open(tool.DocPath));
            else
                menu.AddDisabledItem(new GUIContent("Open documentation"));
            menu.AddItem(new GUIContent("Copy menu path"), false,
                () => EditorGUIUtility.systemCopyBuffer = tool.MenuPath);

            if (!string.IsNullOrEmpty(tool.SourceFile))
            {
                menu.AddItem(new GUIContent("Ping script"), false, () =>
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(tool.SourceFile);
                    if (asset != null) { EditorGUIUtility.PingObject(asset); Selection.activeObject = asset; }
                });
                menu.AddItem(new GUIContent("Open script"), false,
                    () => AssetDatabase.OpenAsset(
                        AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(tool.SourceFile)));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Ping script"));
            }

            menu.ShowAsContext();
            e.Use();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        List<FrogletToolEntry> Filter(IReadOnlyList<FrogletToolEntry> src)
        {
            if (string.IsNullOrWhiteSpace(_search)) return src.ToList();
            var q = _search.Trim();
            return src.Where(t =>
                t.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || t.MenuPath.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (t.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || FrogletEditorPalette.LabelFor(t.Category).Contains(q, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }
}
