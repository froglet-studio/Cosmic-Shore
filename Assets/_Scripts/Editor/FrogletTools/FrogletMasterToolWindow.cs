using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// FrogletTools &gt; Froglet Master Tool - the single front door to every editor tool in the
    /// project, laid out as a Gantt-style board: one colour-coded swimlane per category, one bar
    /// per tool, bar length = importance. Click a bar to launch the tool.
    ///
    /// Tools are DISCOVERED, never registered: see <see cref="FrogletToolRegistry"/>. Any
    /// <c>[MenuItem("FrogletTools/...")]</c> in the project's editor assembly shows up here the
    /// moment it compiles. Tools still parked under a legacy root (<c>Tools/Cosmic Shore/...</c>,
    /// <c>Cosmic Shore/...</c>) are surfaced in the "Needs migration" strip so the convention
    /// enforces itself.
    /// </summary>
    public sealed class FrogletMasterToolWindow : EditorWindow
    {
        const float LaneLabelWidth = 132f;
        const float RowHeight = 26f;
        const float RowGap = 3f;
        const float LaneHeaderHeight = 22f;
        const float TrackPadding = 10f;

        string _search = "";
        Vector2 _scroll;
        bool _showMigration = true;
        bool _groupByCategory = true;
        readonly HashSet<FrogletToolCategory> _collapsed = new();

        [MenuItem("FrogletTools/Froglet Master Tool", false, -100)]
        [FrogletTool(FrogletToolCategory.Misc, Importance = 5,
            Description = "This board. Every Froglet editor tool in one place.")]
        public static void Open()
        {
            var w = GetWindow<FrogletMasterToolWindow>("Froglet Tools");
            w.minSize = new Vector2(720f, 420f);
            w.Show();
        }

        void OnEnable() => FrogletToolRegistry.Refresh();

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "Froglet Master Tool",
                "Every editor tool in the project, ranked by importance. Bars are sized by how load-bearing a tool is - " +
                "click one to launch it.",
                FrogletEditorPalette.Jade);

            DrawToolbar();

            var tools = Filter(FrogletToolRegistry.All);

            if (tools.Count == 0)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(_search)
                        ? "No tools discovered. Check that at least one [MenuItem(\"FrogletTools/...\")] exists in the editor assembly."
                        : $"No tool matches \"{_search}\".",
                    MessageType.Info);
                return;
            }

            DrawRuler();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                if (_groupByCategory)
                {
                    foreach (var lane in tools.GroupBy(t => t.Category).OrderBy(g => g.Key))
                        DrawLane(lane.Key, lane.OrderByDescending(t => t.Importance)
                                               .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                                               .ToList());
                }
                else
                {
                    DrawRows(tools.OrderByDescending(t => t.Importance)
                                  .ThenBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
                                  .ToList(), showLaneColor: true);
                }

                DrawMigrationStrip();
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
                if (!string.IsNullOrEmpty(_search) && GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    _search = "";
                    GUI.FocusControl(null);
                }

                GUILayout.FlexibleSpace();

                _groupByCategory = GUILayout.Toggle(_groupByCategory, "Swimlanes",
                    EditorStyles.toolbarButton, GUILayout.Width(78));
                _showMigration = GUILayout.Toggle(_showMigration, "Migration",
                    EditorStyles.toolbarButton, GUILayout.Width(74));

                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(56)))
                {
                    FrogletToolRegistry.Refresh();
                    Repaint();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Importance axis, drawn once so the bar lengths read as a scale, not decoration.</summary>
        void DrawRuler()
        {
            var r = GUILayoutUtility.GetRect(0, 18f, GUILayout.ExpandWidth(true));
            var track = TrackRect(r);

            FrogletEditorPalette.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f),
                FrogletEditorPalette.Muted.WithAlpha(0.25f));

            var label = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                normal = { textColor = FrogletEditorPalette.Muted },
            };
            GUI.Label(new Rect(r.x + 4f, r.y, LaneLabelWidth, 16f), "IMPORTANCE", label);

            for (int i = 1; i <= 5; i++)
            {
                float x = track.x + track.width * (i / 5f);
                FrogletEditorPalette.DrawRect(new Rect(x - 1f, r.y + 4f, 1f, 10f),
                    FrogletEditorPalette.Muted.WithAlpha(0.22f));
                var lr = new Rect(x - 26f, r.y, 24f, 16f);
                GUI.Label(lr, i.ToString(), new GUIStyle(label) { alignment = TextAnchor.MiddleRight });
            }
        }

        void DrawFooter(int shown)
        {
            var all = FrogletToolRegistry.All;
            int legacy = all.Count(t => !t.IsConforming);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label($"{shown} shown / {all.Count} discovered", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (legacy > 0)
                {
                    var s = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = FrogletEditorPalette.Adapt(FrogletEditorPalette.Warn) } };
                    GUILayout.Label($"{legacy} outside FrogletTools/", s);
                }
                else
                {
                    var s = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = FrogletEditorPalette.Adapt(FrogletEditorPalette.Ok) } };
                    GUILayout.Label("all tools conform", s);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        // ── Board ────────────────────────────────────────────────────────────────

        void DrawLane(FrogletToolCategory category, List<FrogletToolEntry> tools)
        {
            var accent = FrogletEditorPalette.ColorFor(category);
            bool collapsed = _collapsed.Contains(category);

            var header = GUILayoutUtility.GetRect(0, LaneHeaderHeight, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawRect(header, FrogletEditorPalette.Adapt(accent).WithAlpha(0.13f));
            FrogletEditorPalette.DrawAccentStripe(header, FrogletEditorPalette.Adapt(accent), 4f);

            var titleRect = new Rect(header.x + 12f, header.y, header.width - 80f, header.height);
            var style = new GUIStyle(FrogletEditorPalette.LaneLabel)
            { normal = { textColor = FrogletEditorPalette.Adapt(accent) } };
            GUI.Label(titleRect, $"{(collapsed ? "▸" : "▾")}  {FrogletEditorPalette.LabelFor(category).ToUpperInvariant()}", style);

            var countRect = new Rect(header.xMax - 46f, header.y + 3f, 38f, header.height - 6f);
            FrogletEditorPalette.StatusPill(countRect, tools.Count.ToString(), accent);

            if (GUI.Button(header, GUIContent.none, GUIStyle.none))
            {
                if (collapsed) _collapsed.Remove(category);
                else _collapsed.Add(category);
            }
            EditorGUIUtility.AddCursorRect(header, MouseCursor.Link);

            if (collapsed) { GUILayout.Space(RowGap); return; }

            GUILayout.Space(RowGap);
            DrawRows(tools, showLaneColor: false);
            GUILayout.Space(6f);
        }

        void DrawRows(List<FrogletToolEntry> tools, bool showLaneColor)
        {
            foreach (var tool in tools)
                DrawToolRow(tool, showLaneColor);
        }

        void DrawToolRow(FrogletToolEntry tool, bool showCategoryTag)
        {
            var row = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
            var accent = FrogletEditorPalette.Adapt(FrogletEditorPalette.ColorFor(tool.Category));
            var track = TrackRect(row);

            bool hoverRow = row.Contains(Event.current.mousePosition);
            if (hoverRow)
                FrogletEditorPalette.DrawRect(row, FrogletEditorPalette.SurfaceRaised.WithAlpha(0.55f));

            // Lane gutter: the tool's own label, so the board is readable with lanes collapsed too.
            var gutter = new Rect(row.x + 10f, row.y, LaneLabelWidth - 14f, row.height);
            var gutterStyle = new GUIStyle(FrogletEditorPalette.CardBody)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = FrogletEditorPalette.Muted },
            };
            GUI.Label(gutter, showCategoryTag ? FrogletEditorPalette.LabelFor(tool.Category) : tool.Group ?? "", gutterStyle);

            // Track baseline so short bars still read as "on a scale".
            FrogletEditorPalette.DrawRect(new Rect(track.x, row.y + row.height * 0.5f - 0.5f, track.width, 1f),
                FrogletEditorPalette.Muted.WithAlpha(0.10f));

            // The Gantt bar: length is the tool's importance.
            float barW = Mathf.Max(96f, track.width * (tool.Importance / 5f));
            var bar = new Rect(track.x, row.y + 2f, barW, row.height - 4f);
            bool hoverBar = bar.Contains(Event.current.mousePosition);

            var fill = hoverBar ? Color.Lerp(accent, Color.white, 0.16f) : accent.WithAlpha(0.88f);
            FrogletEditorPalette.DrawCard(bar, fill, Color.Lerp(accent, Color.black, 0.3f));

            var nameRect = new Rect(bar.x + 9f, bar.y, bar.width - 18f, bar.height);
            GUI.Label(nameRect, tool.DisplayName,
                new GUIStyle(FrogletEditorPalette.CardTitle) { normal = { textColor = Color.white } });

            // Description trails the bar so long text never squashes the bar itself.
            if (!string.IsNullOrEmpty(tool.Description))
            {
                var descRect = new Rect(bar.xMax + 8f, row.y, Mathf.Max(0f, track.xMax - bar.xMax - 78f), row.height);
                if (descRect.width > 40f)
                    GUI.Label(descRect, tool.Description, FrogletEditorPalette.CardBody);
            }

            if (!tool.IsConforming)
            {
                var warn = new Rect(track.xMax - 68f, row.y + 5f, 62f, row.height - 10f);
                FrogletEditorPalette.StatusPill(warn, "LEGACY", FrogletEditorPalette.Warn);
            }

            if (GUI.Button(bar, new GUIContent("", $"{tool.MenuPath}\n{tool.DeclaringType}"), GUIStyle.none))
                tool.Invoke();
            if (hoverBar) EditorGUIUtility.AddCursorRect(bar, MouseCursor.Link);

            HandleRowContextMenu(row, tool);
            GUILayout.Space(RowGap);
        }

        static void HandleRowContextMenu(Rect row, FrogletToolEntry tool)
        {
            var e = Event.current;
            if (e.type != EventType.ContextClick || !row.Contains(e.mousePosition)) return;

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Launch"), false, tool.Invoke);
            menu.AddSeparator("");
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
                    () => AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(tool.SourceFile)));
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Ping script"));
            }

            menu.ShowAsContext();
            e.Use();
        }

        void DrawMigrationStrip()
        {
            if (!_showMigration) return;
            var legacy = FrogletToolRegistry.NonConforming().ToList();
            if (legacy.Count == 0) return;

            GUILayout.Space(10);
            FrogletEditorPalette.HorizontalRule();

            EditorGUILayout.LabelField("Needs migration", FrogletEditorPalette.SectionHeader);
            EditorGUILayout.LabelField(
                $"{legacy.Count} tool(s) still declare a [MenuItem] outside \"FrogletTools/\". " +
                "Move the path under FrogletTools/<Category>/ so the menu bar stays single-rooted.",
                FrogletEditorPalette.Subtitle);
            GUILayout.Space(4);

            foreach (var t in legacy.OrderBy(t => t.MenuPath, StringComparer.OrdinalIgnoreCase))
            {
                var row = GUILayoutUtility.GetRect(0, 22f, GUILayout.ExpandWidth(true));
                FrogletEditorPalette.DrawAccentStripe(row, FrogletEditorPalette.Adapt(FrogletEditorPalette.Warn), 3f);
                GUI.Label(new Rect(row.x + 10f, row.y, row.width - 190f, row.height),
                    t.MenuPath, FrogletEditorPalette.CardBody);

                var btn = new Rect(row.xMax - 172f, row.y + 1f, 82f, row.height - 2f);
                if (FrogletEditorPalette.ColorButton(btn, "Launch", FrogletEditorPalette.Info, t.MenuPath, outline: true))
                    t.Invoke();

                var btn2 = new Rect(row.xMax - 86f, row.y + 1f, 82f, row.height - 2f);
                bool hasScript = !string.IsNullOrEmpty(t.SourceFile);
                if (FrogletEditorPalette.ColorButton(btn2, "Script", FrogletEditorPalette.Muted,
                        hasScript ? t.SourceFile : "Script not found", hasScript, outline: true) && hasScript)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(t.SourceFile);
                    if (asset != null) { EditorGUIUtility.PingObject(asset); Selection.activeObject = asset; }
                }

                GUILayout.Space(2);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        static Rect TrackRect(Rect row) => new(
            row.x + LaneLabelWidth,
            row.y,
            Mathf.Max(120f, row.width - LaneLabelWidth - TrackPadding),
            row.height);

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
