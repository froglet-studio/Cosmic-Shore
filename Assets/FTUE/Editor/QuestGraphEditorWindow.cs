#if !LINUX_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Quest Graph Editor — node/flowchart authoring for <see cref="QuestSO"/> quests and
    /// their <see cref="QuestPhaseGraphSO"/> phases (FrogletTools ▸ Quest Graph Editor).
    ///
    /// Built on IMGUI to match the project's editor tooling, tuned for a Shader-Graph feel:
    /// wheel zoom anchored at the cursor, middle/alt drag panning, content-sized node cards,
    /// category-colored headers with a legend, hover tooltips, drag-to-connect ports (release
    /// on empty canvas to spawn-and-connect), and cached asset/validation state so the window
    /// never hits the AssetDatabase or re-validates per frame.
    /// </summary>
    public class QuestGraphEditorWindow : EditorWindow
    {
        // ── Selection / assets ─────────────────────────────────────────
        QuestSO _quest;
        QuestPhaseGraphSO _graph;
        QuestNodeSO _selectedNode;
        UnityEditor.Editor _nodeEditor;

        readonly List<QuestSO> _quests = new();
        string _questRename;

        // ── Canvas state ───────────────────────────────────────────────
        QuestNodeSO _dragNode;
        Vector2 _dragGrab;
        QuestNodeSO _linkFrom;
        string _linkPort;
        bool _linkDrag;
        Vector2 _linkMouse;

        QuestNodeSO _hoverNode;
        string _tooltip;
        Vector2 _tooltipWinPos;
        Rect _canvasRect;

        // ── Caches ─────────────────────────────────────────────────────
        struct NodeCard { public int hash; public Vector2 size; public string header; public string title; public string summary; }
        readonly Dictionary<QuestNodeSO, NodeCard> _cards = new();
        readonly List<string> _validation = new();
        bool _validationDirty = true;

        Vector2 _leftScroll, _rightScroll;
        Vector2 _questNotesScroll, _phaseNotesScroll;
        bool _showLegend = true;

        // ── Player progress (checkpoint view — reads the local PlayerPrefs mirror) ──
        HashSet<string> _doneNodeIds = new();
        string _cursorNodeId = string.Empty;
        int _cursorPhaseIndex = -1;
        bool _questCompletedLocal;
        double _nextProgressPoll;

        // ── Panels (resizable + hideable, persisted per user) ──────────
        const string PrefLeftW = "QuestGraph.LeftW";
        const string PrefRightW = "QuestGraph.RightW";
        const string PrefShowLeft = "QuestGraph.ShowLeft";
        const string PrefShowRight = "QuestGraph.ShowRight";
        const string PrefShowLegend = "QuestGraph.ShowLegend";
        const float MinPanelW = 180f;
        const float MaxPanelW = 560f;
        const float MinCanvasW = 220f;
        const float SplitterGrabW = 6f;

        float _leftW = 234f;
        float _rightW = 340f;
        bool _showLeft = true;
        bool _showRight = true;
        int _panelDrag; // 0 = none, 1 = left splitter, 2 = right splitter

        float EffectiveLeftW => _showLeft ? _leftW : 0f;
        float EffectiveRightW => _showRight ? _rightW : 0f;

        // ── Constants ──────────────────────────────────────────────────
        const float ToolbarH = 24f;
        const float HeaderH = 24f;
        const float PortR = 6f;
        const float MinZoom = 0.35f;
        const float MaxZoom = 1.75f;

        const string RootFolder = "Assets/FTUE/DataContainer";
        const string QuestsFolder = RootFolder + "/Quests";
        const string PhasesFolder = RootFolder + "/Phases";

        float Zoom
        {
            get => _graph != null ? Mathf.Clamp(_graph.canvasZoom <= 0f ? 1f : _graph.canvasZoom, MinZoom, MaxZoom) : 1f;
            set { if (_graph != null) _graph.canvasZoom = Mathf.Clamp(value, MinZoom, MaxZoom); }
        }

        [MenuItem("FrogletTools/Quest Graph Editor")]
        public static void Open() => GetWindow<QuestGraphEditorWindow>("Quest Graph Editor");

        void OnEnable()
        {
            wantsMouseMove = true;
            _leftW = Mathf.Clamp(EditorPrefs.GetFloat(PrefLeftW, 234f), MinPanelW, MaxPanelW);
            _rightW = Mathf.Clamp(EditorPrefs.GetFloat(PrefRightW, 340f), MinPanelW, MaxPanelW);
            _showLeft = EditorPrefs.GetBool(PrefShowLeft, true);
            _showRight = EditorPrefs.GetBool(PrefShowRight, true);
            _showLegend = EditorPrefs.GetBool(PrefShowLegend, true);
            RefreshAssets();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_nodeEditor != null) DestroyImmediate(_nodeEditor);
        }

        void OnFocus() { RefreshAssets(); RefreshProgress(); MarkValidationDirty(); }
        void OnProjectChange() { RefreshAssets(); MarkValidationDirty(); Repaint(); }
        void OnUndoRedo() { _cards.Clear(); MarkValidationDirty(); Repaint(); }

        void MarkValidationDirty() => _validationDirty = true;

        /// <summary>Re-read the player's local checkpoint (cursor + done-set) for the selected quest.</summary>
        void RefreshProgress()
        {
            if (_quest == null)
            {
                _doneNodeIds = new HashSet<string>();
                _cursorNodeId = string.Empty;
                _cursorPhaseIndex = -1;
                _questCompletedLocal = false;
                return;
            }

            _doneNodeIds = QuestProgressStore.GetCompletedNodeIds(_quest.QuestId);
            _cursorNodeId = QuestProgressStore.GetCurrentNodeId(_quest.QuestId) ?? string.Empty;
            _cursorPhaseIndex = QuestProgressStore.GetPhaseIndex(_quest.QuestId);
            _questCompletedLocal = QuestProgressStore.IsCompleted(_quest.QuestId);
        }

        /// <summary>Live checkpoint updates while testing: poll the local mirror and repaint on change.</summary>
        void OnInspectorUpdate()
        {
            if (_quest == null || EditorApplication.timeSinceStartup < _nextProgressPoll) return;
            _nextProgressPoll = EditorApplication.timeSinceStartup + (Application.isPlaying ? 0.5 : 2.0);

            string prevCursor = _cursorNodeId;
            int prevDone = _doneNodeIds.Count;
            int prevPhase = _cursorPhaseIndex;
            bool prevCompleted = _questCompletedLocal;

            RefreshProgress();

            if (prevCursor != _cursorNodeId || prevDone != _doneNodeIds.Count
                || prevPhase != _cursorPhaseIndex || prevCompleted != _questCompletedLocal)
                Repaint();
        }

        // ════════════════════════════════════════════════════════════════
        // OnGUI
        // ════════════════════════════════════════════════════════════════

        void OnGUI()
        {
            QuestGraphStyles.Ensure();
            var e = Event.current;

            float leftW = EffectiveLeftW;
            float rightW = EffectiveRightW;

            // Keep the canvas usable at any window size: shrink the panels proportionally
            // instead of letting them swallow the window (a negative-width canvas rect would
            // silently stop responding to all mouse input).
            float maxPanels = Mathf.Max(0f, position.width - MinCanvasW);
            if (leftW + rightW > maxPanels && leftW + rightW > 0f)
            {
                float scale = maxPanels / (leftW + rightW);
                leftW *= scale;
                rightW *= scale;
            }

            var toolbarRect = new Rect(0, 0, position.width, ToolbarH);
            var leftRect = new Rect(0, ToolbarH, leftW, position.height - ToolbarH);
            var rightRect = new Rect(position.width - rightW, ToolbarH, rightW, position.height - ToolbarH);
            _canvasRect = new Rect(leftW, ToolbarH, position.width - leftW - rightW, position.height - ToolbarH);

            HandleKeyboard(e);
            HandlePanelResize(e, leftRect, rightRect); // before the canvas so splitter drags win

            // Canvas first (its zoom group can over-clip at low zoom; panels drawn after cover any spill).
            DrawCanvas(_canvasRect, e);

            DrawToolbar(toolbarRect);
            if (_showLeft) DrawLeftPanel(leftRect);
            if (_showRight) DrawRightPanel(rightRect);
            DrawPanelSplitters(leftRect, rightRect);
            DrawOverlays();
        }

        // ── Panel resize (draggable splitters) ─────────────────────────

        Rect LeftSplitterRect(Rect leftRect) =>
            new(leftRect.xMax - SplitterGrabW * 0.5f, leftRect.y, SplitterGrabW, leftRect.height);

        Rect RightSplitterRect(Rect rightRect) =>
            new(rightRect.x - SplitterGrabW * 0.5f, rightRect.y, SplitterGrabW, rightRect.height);

        void HandlePanelResize(Event e, Rect leftRect, Rect rightRect)
        {
            var leftSplit = LeftSplitterRect(leftRect);
            var rightSplit = RightSplitterRect(rightRect);

            if (_showLeft) EditorGUIUtility.AddCursorRect(leftSplit, MouseCursor.ResizeHorizontal);
            if (_showRight) EditorGUIUtility.AddCursorRect(rightSplit, MouseCursor.ResizeHorizontal);

            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && _showLeft && leftSplit.Contains(e.mousePosition):
                    _panelDrag = 1;
                    e.Use();
                    break;

                case EventType.MouseDown when e.button == 0 && _showRight && rightSplit.Contains(e.mousePosition):
                    _panelDrag = 2;
                    e.Use();
                    break;

                case EventType.MouseDrag when _panelDrag == 1:
                {
                    // Guard the upper bound against dropping below the lower one on narrow
                    // windows — an inverted Mathf.Clamp would snap the width negative and put
                    // the splitter (and panel) unrecoverably offscreen.
                    float max = Mathf.Max(MinPanelW, Mathf.Min(MaxPanelW, position.width - EffectiveRightW - MinCanvasW));
                    _leftW = Mathf.Clamp(e.mousePosition.x, MinPanelW, max);
                    EditorPrefs.SetFloat(PrefLeftW, _leftW);
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseDrag when _panelDrag == 2:
                {
                    float max = Mathf.Max(MinPanelW, Mathf.Min(MaxPanelW, position.width - EffectiveLeftW - MinCanvasW));
                    _rightW = Mathf.Clamp(position.width - e.mousePosition.x, MinPanelW, max);
                    EditorPrefs.SetFloat(PrefRightW, _rightW);
                    e.Use();
                    Repaint();
                    break;
                }

                case EventType.MouseUp when _panelDrag != 0:
                    _panelDrag = 0;
                    e.Use();
                    break;
            }
        }

        void DrawPanelSplitters(Rect leftRect, Rect rightRect)
        {
            if (_showLeft)
                EditorGUI.DrawRect(new Rect(leftRect.xMax - 1f, leftRect.y, _panelDrag == 1 ? 2f : 1f, leftRect.height),
                    _panelDrag == 1 ? QuestGraphStyles.SelectionBorder : QuestGraphStyles.SplitterLine);
            if (_showRight)
                EditorGUI.DrawRect(new Rect(rightRect.x, rightRect.y, _panelDrag == 2 ? 2f : 1f, rightRect.height),
                    _panelDrag == 2 ? QuestGraphStyles.SelectionBorder : QuestGraphStyles.SplitterLine);
        }

        void HandleKeyboard(Event e)
        {
            if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;

            if (e.keyCode == KeyCode.F && _graph != null)
            {
                FrameContent();
                e.Use();
            }
            else if ((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && _selectedNode != null)
            {
                DeleteNode(_selectedNode);
                e.Use();
                GUIUtility.ExitGUI();
            }
            else if (e.keyCode == KeyCode.Escape && _linkFrom != null)
            {
                _linkFrom = null;
                _linkDrag = false;
                e.Use();
                Repaint();
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Toolbar
        // ════════════════════════════════════════════════════════════════

        void DrawToolbar(Rect rect)
        {
            EditorGUI.DrawRect(rect, QuestGraphStyles.ToolbarBg);

            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6);

            string crumb = _quest != null && _graph != null
                ? $"{_quest.name}  ▸  Phase {_quest.phases.IndexOf(_graph)} · {_graph.PhaseName}"
                : _graph != null ? _graph.PhaseName
                : _quest != null ? _quest.name
                : "Select or create a quest";
            GUILayout.Label(crumb, QuestGraphStyles.Breadcrumb, GUILayout.MinWidth(120));

            GUILayout.Space(10);
            using (new EditorGUI.DisabledScope(_graph == null))
            {
                if (GUILayout.Button("+ Add Node", EditorStyles.toolbarButton, GUILayout.Width(84)))
                    ShowCreateNodeMenu(ScreenCenterWorld(), null, null);
                if (GUILayout.Button("Frame (F)", EditorStyles.toolbarButton, GUILayout.Width(72)))
                    FrameContent();
                if (GUILayout.Button(new GUIContent("Layout Rows",
                        "Re-arrange this phase into rows: the flow reads left→right, and a new row starts "
                        + "wherever the player moves between the app shell and gameplay."),
                        EditorStyles.toolbarButton, GUILayout.Width(84)))
                    LayoutRows();
            }

            bool legend = GUILayout.Toggle(_showLegend,
                new GUIContent("Node Colors", "Show/hide the node-color legend on the canvas."),
                EditorStyles.toolbarButton, GUILayout.Width(82));
            if (legend != _showLegend)
            {
                _showLegend = legend;
                EditorPrefs.SetBool(PrefShowLegend, _showLegend);
            }

            GUILayout.FlexibleSpace();

            bool showLeft = GUILayout.Toggle(_showLeft,
                new GUIContent("◧ Quests", "Show/hide the quest/phase sidebar. Drag its inner edge to resize."),
                EditorStyles.toolbarButton, GUILayout.Width(70));
            if (showLeft != _showLeft)
            {
                _showLeft = showLeft;
                EditorPrefs.SetBool(PrefShowLeft, _showLeft);
            }

            bool showRight = GUILayout.Toggle(_showRight,
                new GUIContent("Inspector ◨", "Show/hide the inspector sidebar. Drag its inner edge to resize."),
                EditorStyles.toolbarButton, GUILayout.Width(80));
            if (showRight != _showRight)
            {
                _showRight = showRight;
                EditorPrefs.SetBool(PrefShowRight, _showRight);
            }

            GUILayout.Space(8);

            using (new EditorGUI.DisabledScope(_graph == null))
            {
                if (GUILayout.Button($"{Mathf.RoundToInt(Zoom * 100)}%", EditorStyles.toolbarButton, GUILayout.Width(52)))
                {
                    Zoom = 1f;
                    Repaint();
                }
            }

            bool unsaved = HasUnsavedEdits();
            if (GUILayout.Button(new GUIContent(unsaved ? "Save*" : "Save",
                    unsaved ? "Unsaved graph edits (text, positions, wiring) — write them to disk so git sees them."
                            : "All graph edits are on disk."),
                    EditorStyles.toolbarButton, GUILayout.Width(56)))
                SaveAll();

            GUILayout.Space(6);
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ════════════════════════════════════════════════════════════════
        // Left panel — quests, phases, standalone graphs
        // ════════════════════════════════════════════════════════════════

        void DrawLeftPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, QuestGraphStyles.PanelBg);
            GUILayout.BeginArea(rect);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
            GUILayout.Space(6);

            GUILayout.Label("QUESTS", QuestGraphStyles.PanelHeader);
            foreach (var quest in _quests)
            {
                if (quest == null) continue;
                bool isSel = quest == _quest;

                var row = GUILayoutUtility.GetRect(1, 22, GUILayout.ExpandWidth(true));
                if (isSel) EditorGUI.DrawRect(row, QuestGraphStyles.RowSelected);

                bool qEnabled = GUI.Toggle(new Rect(row.x + 4, row.y + 3, 16, 16), quest.questEnabled,
                    new GUIContent(string.Empty, "Enable/disable this quest — the runner never starts a disabled quest."));
                if (qEnabled != quest.questEnabled)
                {
                    Undo.RecordObject(quest, "Toggle Quest Enabled");
                    quest.questEnabled = qEnabled;
                    SaveToggleToDisk(quest);
                }

                var prevQuestCol = GUI.color;
                if (!quest.questEnabled) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                if (GUI.Button(new Rect(row.x + 24, row.y, row.width - 24, row.height),
                        quest.questEnabled ? quest.name : $"{quest.name}  (off)",
                        isSel ? QuestGraphStyles.RowLabelSelected : QuestGraphStyles.RowLabel))
                    SelectQuest(quest);
                GUI.color = prevQuestCol;

                if (!isSel) continue;

                // Phases of the selected quest
                for (int i = 0; i < quest.phases.Count; i++)
                {
                    var phase = quest.phases[i];
                    var prow = GUILayoutUtility.GetRect(1, 20, GUILayout.ExpandWidth(true));
                    bool phaseSel = phase != null && phase == _graph;
                    if (phaseSel) EditorGUI.DrawRect(prow, QuestGraphStyles.RowSelectedFaint);

                    if (phase != null)
                    {
                        bool pEnabled = GUI.Toggle(new Rect(prow.x + 20, prow.y + 2, 16, 16), phase.phaseEnabled,
                            new GUIContent(string.Empty, "Enable/disable this phase — the runner skips disabled phases."));
                        if (pEnabled != phase.phaseEnabled)
                        {
                            Undo.RecordObject(phase, "Toggle Phase Enabled");
                            phase.phaseEnabled = pEnabled;
                            SaveToggleToDisk(phase);
                        }
                    }

                    bool isCursorPhase = !_questCompletedLocal && i == _cursorPhaseIndex;
                    string label = phase != null ? $"{i} · {phase.PhaseName}" : $"{i} · (missing)";
                    if (isCursorPhase) label = "▶ " + label;
                    var prevPhaseCol = GUI.color;
                    if (phase != null && !phase.phaseEnabled) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                    if (GUI.Button(new Rect(prow.x + 38, prow.y, prow.width - 104, prow.height), label,
                            phaseSel ? QuestGraphStyles.RowLabelSelected : QuestGraphStyles.RowLabelSmall)
                        && phase != null)
                        SelectGraph(phase, quest);
                    GUI.color = prevPhaseCol;

                    // Reorder / remove controls
                    var upR = new Rect(prow.xMax - 62, prow.y + 1, 18, 18);
                    var dnR = new Rect(prow.xMax - 43, prow.y + 1, 18, 18);
                    var rmR = new Rect(prow.xMax - 22, prow.y + 1, 18, 18);
                    using (new EditorGUI.DisabledScope(i == 0))
                        if (GUI.Button(upR, "▲", QuestGraphStyles.MiniButton)) { MovePhase(quest, i, -1); GUIUtility.ExitGUI(); }
                    using (new EditorGUI.DisabledScope(i == quest.phases.Count - 1))
                        if (GUI.Button(dnR, "▼", QuestGraphStyles.MiniButton)) { MovePhase(quest, i, +1); GUIUtility.ExitGUI(); }
                    if (GUI.Button(rmR, "✕", QuestGraphStyles.MiniButtonDanger)) { RemovePhase(quest, i); GUIUtility.ExitGUI(); }
                }

                GUILayout.Space(2);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);
                if (GUILayout.Button("+ Add Phase", QuestGraphStyles.MiniWide, GUILayout.Width(110)))
                    AddPhase(quest);
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();
            if (GUILayout.Button("+ New Quest"))
                CreateQuestAsset();
            GUILayout.Space(6);
            EditorGUILayout.EndVertical();
            GUILayout.EndArea();
        }

        // ════════════════════════════════════════════════════════════════
        // Canvas
        // ════════════════════════════════════════════════════════════════

        void DrawCanvas(Rect rect, Event e)
        {
            EditorGUI.DrawRect(rect, QuestGraphStyles.CanvasBg);

            if (_graph == null)
            {
                GUI.Label(new Rect(rect.x + 24, rect.y + 20, rect.width - 48, 60),
                    "Select a quest phase on the left — or create a quest and add its first phase.",
                    QuestGraphStyles.CanvasHint);
                return;
            }

            bool mouseInCanvas = rect.Contains(e.mousePosition);

            // Cursor-anchored wheel zoom (handled in window space, before the zoom group).
            if (e.type == EventType.ScrollWheel && mouseInCanvas)
            {
                float oldZoom = Zoom;
                float newZoom = Mathf.Clamp(oldZoom * (e.delta.y > 0 ? 0.92f : 1.087f), MinZoom, MaxZoom);
                if (!Mathf.Approximately(oldZoom, newZoom))
                {
                    Vector2 worldUnderMouse = (e.mousePosition - rect.position) / oldZoom - _graph.canvasScroll;
                    Zoom = newZoom;
                    _graph.canvasScroll = (e.mousePosition - rect.position) / newZoom - worldUnderMouse;
                    EditorUtility.SetDirty(_graph);
                }
                e.Use();
                Repaint();
                return;
            }

            _hoverNode = null;
            _tooltip = null;

            QuestZoomArea.Begin(Zoom, rect);
            try
            {
                Vector2 viewSize = rect.size / Zoom;
                DrawGrid(viewSize);
                DrawEdges();
                DrawLinkPreview(e);
                DrawNodes(e, mouseInCanvas);
                HandleCanvasEvents(viewSize, e, mouseInCanvas);
            }
            finally
            {
                QuestZoomArea.End(position);
            }

            if (e.type == EventType.MouseMove && mouseInCanvas)
                Repaint();
        }

        void DrawGrid(Vector2 viewSize)
        {
            var scroll = _graph.canvasScroll;
            DrawGridLines(viewSize, scroll, 20f, QuestGraphStyles.GridMinor);
            DrawGridLines(viewSize, scroll, 100f, QuestGraphStyles.GridMajor);
        }

        static void DrawGridLines(Vector2 viewSize, Vector2 scroll, float spacing, Color color)
        {
            Handles.color = color;
            float offX = scroll.x % spacing;
            float offY = scroll.y % spacing;
            for (float x = offX; x < viewSize.x; x += spacing)
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, viewSize.y));
            for (float y = offY; y < viewSize.y; y += spacing)
                Handles.DrawLine(new Vector3(0, y), new Vector3(viewSize.x, y));
            Handles.color = Color.white;
        }

        // ── Node cards ─────────────────────────────────────────────────

        NodeCard GetCard(QuestNodeSO n)
        {
            string header = n.NodeTypeLabel;
            string title = n.displayName ?? string.Empty;
            string summary = n.EditorSummary ?? string.Empty;
            int hash = header.GetHashCode();
            unchecked
            {
                hash = hash * 31 + title.GetHashCode();
                hash = hash * 31 + summary.GetHashCode();
                hash = hash * 31 + n.OutputPorts.Count;
            }

            if (_cards.TryGetValue(n, out var card) && card.hash == hash)
                return card;

            float width = Mathf.Clamp(QuestGraphStyles.NodeHeader.CalcSize(new GUIContent(header)).x + 62f, 200f, 320f);
            float bodyW = width - 22f;
            float h = HeaderH + 7f;
            bool titleShown = !string.IsNullOrEmpty(title) && title != header;
            if (titleShown)
                h += QuestGraphStyles.NodeTitle.CalcHeight(new GUIContent(title), bodyW);
            if (!string.IsNullOrEmpty(summary))
                h += QuestGraphStyles.NodeSummary.CalcHeight(new GUIContent(summary), bodyW) + 2f;

            int ports = n.OutputPorts.Count;
            h += ports > 1 ? ports * 17f + 5f : 9f;
            h = Mathf.Max(h, 54f);

            card = new NodeCard { hash = hash, size = new Vector2(width, h), header = header, title = titleShown ? title : null, summary = summary };
            _cards[n] = card;
            return card;
        }

        Rect NodeRect(QuestNodeSO n) => new(n.graphPosition + _graph.canvasScroll, GetCard(n).size);

        Vector2 InPortCenter(QuestNodeSO n)
        {
            var r = NodeRect(n);
            return new Vector2(r.x, r.y + HeaderH * 0.5f);
        }

        Vector2 OutPortCenter(QuestNodeSO n, int portIndex)
        {
            var r = NodeRect(n);
            int count = n.OutputPorts.Count;
            if (count <= 1)
                return new Vector2(r.xMax, r.y + r.height * 0.55f);
            float bottom = r.yMax - 8f;
            float step = 17f;
            float first = bottom - (count - 1) * step;
            return new Vector2(r.xMax, first + portIndex * step);
        }

        // ── Edges ──────────────────────────────────────────────────────

        void DrawEdges()
        {
            foreach (var n in _graph.nodes)
            {
                if (n == null) continue;
                var ports = n.OutputPorts;
                for (int p = 0; p < ports.Count; p++)
                {
                    var edge = n.EdgeForPort(ports[p]);
                    if (edge == null) continue;
                    var target = _graph.FindNode(edge.targetNodeId);
                    if (target == null) continue;

                    var col = QuestGraphStyles.CategoryColor(n.Category);
                    col.a = n == _selectedNode || target == _selectedNode ? 1f : 0.75f;
                    Vector2 edgeFrom = OutPortCenter(n, p);
                    Vector2 edgeTo = InPortCenter(target);
                    DrawBezier(edgeFrom, edgeTo, col, n == _selectedNode || target == _selectedNode ? 4f : 3f);

                    if (edge.delaySeconds > 0f)
                    {
                        Vector2 mid = (edgeFrom + edgeTo) * 0.5f;
                        GUI.Label(new Rect(mid.x - 26, mid.y - 16, 52, 14),
                            $"⏱ {edge.delaySeconds:0.#}s", QuestGraphStyles.EdgeLabel);
                    }
                }
            }
        }

        void DrawLinkPreview(Event e)
        {
            if (_linkFrom == null) return;

            int idx = IndexOfPort(_linkFrom, _linkPort);
            Vector2 from = OutPortCenter(_linkFrom, idx);
            Vector2 to = _linkDrag || e.isMouse ? e.mousePosition : _linkMouse;
            _linkMouse = to;
            DrawBezier(from, to, QuestGraphStyles.LinkPreview, 3f);
            Repaint();
        }

        static int IndexOfPort(QuestNodeSO n, string port)
        {
            var ports = n.OutputPorts;
            for (int i = 0; i < ports.Count; i++)
                if (ports[i] == port) return i;
            return 0;
        }

        static void DrawBezier(Vector2 from, Vector2 to, Color color, float width)
        {
            float tangent = Mathf.Clamp(Mathf.Abs(to.x - from.x) * 0.5f, 30f, 90f);
            Handles.DrawBezier(from, to,
                from + Vector2.right * tangent, to + Vector2.left * tangent,
                color, null, width);
        }

        // ── Nodes ──────────────────────────────────────────────────────

        void DrawNodes(Event e, bool mouseInCanvas)
        {
            // The resume marker only applies to the phase the player's cursor is in.
            bool cursorPhase = !_questCompletedLocal && _quest != null
                               && _quest.phases.IndexOf(_graph) == _cursorPhaseIndex;

            foreach (var n in _graph.nodes)
            {
                if (n == null) continue;
                DrawNode(n, e, mouseInCanvas, cursorPhase);
            }
        }

        void DrawNode(QuestNodeSO n, Event e, bool mouseInCanvas, bool cursorPhase)
        {
            var card = GetCard(n);
            var r = NodeRect(n);
            bool hovered = mouseInCanvas && r.Contains(e.mousePosition);
            if (hovered) _hoverNode = n;

            // Shadow + body + header
            EditorGUI.DrawRect(new Rect(r.x + 3, r.y + 3, r.width, r.height), QuestGraphStyles.NodeShadow);
            EditorGUI.DrawRect(r, hovered ? QuestGraphStyles.NodeBodyHover : QuestGraphStyles.NodeBody);
            var headerRect = new Rect(r.x, r.y, r.width, HeaderH);
            var catColor = QuestGraphStyles.CategoryColor(n.Category);
            EditorGUI.DrawRect(headerRect, catColor);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3f, r.height), catColor); // accent rail

            bool isEntry = _graph.entryNode == n;
            if (isEntry) DrawBorder(r, QuestGraphStyles.EntryBorder, 2f);
            if (_selectedNode == n) DrawBorder(r, QuestGraphStyles.SelectionBorder, 2f);

            bool nEnabled = GUI.Toggle(new Rect(r.x + 6, r.y + 5, 14, 14), n.nodeEnabled,
                new GUIContent(string.Empty, "Enable/disable this node — the runner passes straight through disabled nodes."));
            if (nEnabled != n.nodeEnabled)
            {
                Undo.RecordObject(n, "Toggle Node Enabled");
                n.nodeEnabled = nEnabled;
                SaveToggleToDisk(n);
                MarkValidationDirty();
            }
            GUI.Label(new Rect(r.x + 24, r.y + 3, r.width - 68, HeaderH - 4), card.header, QuestGraphStyles.NodeHeader);
            if (isEntry)
                GUI.Label(new Rect(r.xMax - 66, r.y + 5, 44, 14), "ENTRY", QuestGraphStyles.EntryBadge);

            // Body text
            float y = r.y + HeaderH + 4f;
            float bodyW = r.width - 22f;
            if (card.title != null)
            {
                float th = QuestGraphStyles.NodeTitle.CalcHeight(new GUIContent(card.title), bodyW);
                GUI.Label(new Rect(r.x + 12, y, bodyW, th), card.title, QuestGraphStyles.NodeTitle);
                y += th;
            }
            if (!string.IsNullOrEmpty(card.summary))
            {
                float sh = QuestGraphStyles.NodeSummary.CalcHeight(new GUIContent(card.summary), bodyW);
                GUI.Label(new Rect(r.x + 12, y + 1, bodyW, sh), card.summary, QuestGraphStyles.NodeSummary);
            }

            // Delete button
            var delRect = new Rect(r.xMax - 20, r.y + 3, 17, 17);
            if (GUI.Button(delRect, "✕", QuestGraphStyles.NodeDelete))
            {
                DeleteNode(n);
                GUIUtility.ExitGUI();
            }

            // Ports
            var inCenter = InPortCenter(n);
            bool inHover = mouseInCanvas && Vector2.Distance(e.mousePosition, inCenter) <= PortR + 4f;
            DrawPort(inCenter, QuestGraphStyles.PortIn, inHover || (_linkFrom != null && hovered));

            var ports = n.OutputPorts;
            for (int p = 0; p < ports.Count; p++)
            {
                var c = OutPortCenter(n, p);
                bool armed = _linkFrom == n && _linkPort == ports[p];
                bool pHover = mouseInCanvas && Vector2.Distance(e.mousePosition, c) <= PortR + 4f;
                DrawPort(c, armed ? QuestGraphStyles.PortArmed : catColor, pHover || armed);

                if (ports.Count > 1)
                    GUI.Label(new Rect(c.x - 72, c.y - 8, 64, 16), ports[p], QuestGraphStyles.PortLabel);

                if (pHover)
                    SetTooltip(c + new Vector2(10, 8), ports.Count > 1 ? $"Port '{ports[p]}' — drag to connect" : "Drag to connect");

                if (!mouseInCanvas) continue;

                if (e.type == EventType.MouseDown && e.button == 0 && pHover)
                {
                    _linkFrom = n;
                    _linkPort = ports[p];
                    _linkDrag = true;
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 1 && pHover)
                {
                    ShowPortMenu(n, ports[p]);
                    e.Use();
                }
            }

            if (!n.nodeEnabled)
            {
                EditorGUI.DrawRect(r, new Color(0.05f, 0.05f, 0.07f, 0.45f));
                GUI.Label(new Rect(r.xMax - 38, r.yMax - 18, 34, 16), "OFF", QuestGraphStyles.EntryBadge);
            }

            // ── Player checkpoint overlay (reads the local progress mirror) ──
            // ▶ NEXT = the node the quest resumes at; ✓ = the player already completed it.
            bool isResume = cursorPhase && !string.IsNullOrEmpty(_cursorNodeId) && n.nodeId == _cursorNodeId;
            if (isResume)
            {
                DrawBorder(r, QuestGraphStyles.ResumeBorder, 2f);
                var badge = new Rect(r.x - 2, r.y - 18, 64, 16);
                EditorGUI.DrawRect(badge, QuestGraphStyles.ResumeBorder);
                GUI.Label(badge, "▶ NEXT", QuestGraphStyles.ResumeBadge);
            }
            else if (_doneNodeIds.Contains(n.nodeId))
            {
                GUI.Label(new Rect(r.x + 6, r.yMax - 19, 16, 16), "✓", QuestGraphStyles.DoneBadge);
            }

            // Hover tooltip on the header
            if (hovered && headerRect.Contains(e.mousePosition) && !string.IsNullOrEmpty(n.TypeTooltip))
                SetTooltip(new Vector2(r.x, r.yMax + 6), n.TypeTooltip);

            if (!mouseInCanvas) return;

            // Node-body interactions
            if (e.type == EventType.MouseDown && e.button == 0 && hovered)
            {
                if (_linkFrom != null && _linkFrom != n)
                {
                    ConnectLink(n);
                    e.Use();
                }
                else if (!delRect.Contains(e.mousePosition))
                {
                    SelectNode(n);
                    _dragNode = n;
                    _dragGrab = e.mousePosition - (Vector2)r.position;
                    Undo.RecordObject(n, "Move Quest Node");
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDown && e.button == 1 && hovered)
            {
                ShowNodeMenu(n);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _dragNode == n && e.button == 0)
            {
                n.graphPosition = e.mousePosition - _dragGrab - _graph.canvasScroll;
                EditorUtility.SetDirty(n);
                e.Use();
                Repaint();
            }
        }

        static void DrawPort(Vector2 center, Color color, bool emphasized)
        {
            float radius = emphasized ? PortR + 2f : PortR;
            Handles.color = QuestGraphStyles.PortRim;
            Handles.DrawSolidDisc(center, Vector3.forward, radius + 1.5f);
            Handles.color = color;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = Color.white;
        }

        static void DrawBorder(Rect r, Color color, float t)
        {
            EditorGUI.DrawRect(new Rect(r.x - t, r.y - t, r.width + t * 2, t), color);
            EditorGUI.DrawRect(new Rect(r.x - t, r.yMax, r.width + t * 2, t), color);
            EditorGUI.DrawRect(new Rect(r.x - t, r.y, t, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax, r.y, t, r.height), color);
        }

        void SetTooltip(Vector2 canvasLocalPos, string text)
        {
            _tooltip = text;
            _tooltipWinPos = _canvasRect.position + canvasLocalPos * Zoom;
        }

        // ── Canvas-level events ────────────────────────────────────────

        void HandleCanvasEvents(Vector2 viewSize, Event e, bool mouseInCanvas)
        {
            if (!mouseInCanvas) return;

            switch (e.type)
            {
                case EventType.MouseUp when e.button == 0 && _linkFrom != null && _linkDrag:
                    // Released over a node? DrawNode's MouseDown path handles click-connect;
                    // for drag-release we resolve here.
                    var overNode = NodeUnderMouse(e.mousePosition);
                    if (overNode != null && overNode != _linkFrom)
                    {
                        ConnectLink(overNode);
                    }
                    else if (overNode == null)
                    {
                        float dragDist = Vector2.Distance(OutPortCenter(_linkFrom, IndexOfPort(_linkFrom, _linkPort)), e.mousePosition);
                        if (dragDist > 24f)
                            ShowCreateNodeMenu(e.mousePosition - _graph.canvasScroll, _linkFrom, _linkPort); // spawn-and-connect
                        else
                            _linkDrag = false; // small release: stay armed for click-click connecting
                    }
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseDown when e.button == 0:
                    if (_linkFrom != null) { _linkFrom = null; _linkDrag = false; }
                    else SelectNode(null);
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseDown when e.button == 1:
                    ShowCreateNodeMenu(e.mousePosition - _graph.canvasScroll, null, null);
                    e.Use();
                    break;

                case EventType.MouseDrag when e.button == 2 || (e.button == 0 && e.alt):
                    _graph.canvasScroll += e.delta;
                    EditorUtility.SetDirty(_graph);
                    e.Use();
                    Repaint();
                    break;

                case EventType.MouseUp:
                    _dragNode = null;
                    break;
            }
        }

        QuestNodeSO NodeUnderMouse(Vector2 mouse)
        {
            for (int i = _graph.nodes.Count - 1; i >= 0; i--)
            {
                var n = _graph.nodes[i];
                if (n != null && NodeRect(n).Contains(mouse))
                    return n;
            }
            return null;
        }

        void ConnectLink(QuestNodeSO target)
        {
            SetEdge(_linkFrom, _linkPort, target.nodeId);
            _linkFrom = null;
            _linkDrag = false;
        }

        Vector2 ScreenCenterWorld() =>
            _graph != null ? _canvasRect.size * 0.5f / Zoom - _graph.canvasScroll : Vector2.zero;

        // ════════════════════════════════════════════════════════════════
        // Right panel — quest / phase / node inspectors + validation
        // ════════════════════════════════════════════════════════════════

        void DrawRightPanel(Rect rect)
        {
            EditorGUI.DrawRect(rect, QuestGraphStyles.PanelBg);
            GUILayout.BeginArea(new Rect(rect.x + 6, rect.y + 4, rect.width - 12, rect.height - 8));
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_quest != null)
            {
                GUILayout.Label($"QUEST — {_quest.name}", QuestGraphStyles.PanelHeader);
                EditorGUI.BeginChangeCheck();
                bool questOn = EditorGUILayout.ToggleLeft(
                    new GUIContent("Quest Enabled (runner)", "Master test-harness switch — the runner never starts a disabled quest."),
                    _quest.questEnabled);
                string id = EditorGUILayout.TextField("Quest Id", _quest.questId);
                string notes = ScrollableNotesArea(_quest.designerNotes, ref _questNotesScroll, 76f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_quest, "Edit Quest");
                    _quest.questEnabled = questOn;
                    _quest.questId = id;
                    _quest.designerNotes = notes;
                    EditorUtility.SetDirty(_quest);
                }

                EditorGUILayout.BeginHorizontal();
                _questRename = EditorGUILayout.TextField(_questRename ?? _quest.name);
                if (GUILayout.Button("Rename Asset", GUILayout.Width(94))
                    && !string.IsNullOrWhiteSpace(_questRename) && _questRename.Trim() != _quest.name)
                {
                    AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_quest), _questRename.Trim());
                    AssetDatabase.SaveAssets();
                    RefreshAssets();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(6);
                GUILayout.Label("PLAYER PROGRESS (local checkpoint)", QuestGraphStyles.PanelHeader);
                if (_questCompletedLocal)
                {
                    EditorGUILayout.HelpBox("Quest COMPLETED on this machine — the runner will not start it again (reset below to replay).", MessageType.Info);
                }
                else if (_doneNodeIds.Count == 0 && string.IsNullOrEmpty(_cursorNodeId))
                {
                    EditorGUILayout.HelpBox("No progress yet — the quest starts from Phase 0's entry node.", MessageType.None);
                }
                else
                {
                    string phaseLabel = _cursorPhaseIndex >= 0 && _cursorPhaseIndex < _quest.phases.Count
                                        && _quest.phases[_cursorPhaseIndex] != null
                        ? $"{_cursorPhaseIndex} · {_quest.phases[_cursorPhaseIndex].PhaseName}"
                        : _cursorPhaseIndex.ToString();
                    var cursorNode = _cursorPhaseIndex >= 0 && _cursorPhaseIndex < _quest.phases.Count
                                     && _quest.phases[_cursorPhaseIndex] != null
                        ? _quest.phases[_cursorPhaseIndex].FindNode(_cursorNodeId)
                        : null;
                    string nextLabel = cursorNode != null ? NodeLabel(cursorNode)
                        : string.IsNullOrEmpty(_cursorNodeId) ? "(phase entry)"
                        : "(node not in this graph — regenerated quest? Reset below)";

                    string gateDetail = DescribeGateRequirement(cursorNode);

                    EditorGUILayout.HelpBox(
                        $"Phase: {phaseLabel}\nResumes at: {nextLabel}\nNodes completed: {_doneNodeIds.Count}\n" +
                        "Canvas: ✓ = completed, ▶ NEXT = resume point (updates live in Play mode)." +
                        (string.IsNullOrEmpty(gateDetail) ? string.Empty : $"\n\n{gateDetail}"),
                        MessageType.Info);
                }

                GUILayout.Space(4);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (CenteredButton(new GUIContent(
                            Application.isPlaying ? "▶ Force-Advance Current Node" : "▶ Force-Advance (Play mode only)",
                            "TESTING: complete the node the quest is currently waiting on (the ▶ NEXT node) as if the player did it — skip a game, a gate, a dialogue. Progress is persisted exactly like a real advance.")))
                    {
                        var runner = FindFirstObjectByType<QuestGraphRunner>(FindObjectsInactive.Include);
                        if (runner != null)
                        {
                            runner.DebugForceAdvance();
                            _nextProgressPoll = 0; // refresh the checkpoint view immediately
                        }
                        else
                        {
                            Debug.LogWarning("[Quest] Force-advance: no QuestGraphRunner found in the loaded scenes.");
                        }
                    }
                }

                if (Application.isPlaying)
                    DrawLiveState();

                GUILayout.Space(4);
                if (CenteredButton(new GUIContent("Reset ALL Player Progress",
                        "Clears this quest's PlayerPrefs mirror always. In PLAY MODE it also resets game-mode progression (unlocks + intensity tiers + play counts), all vessel unlocks, and any quest arcade constraints — plus the UGS cloud records when the backend gate is open. The Froglet Toolbox reads this state live and can still manually re-unlock anything.")))
                {
                    if (Application.isPlaying)
                    {
                        bool cloud = QuestProgressStore.ResetAllGameplayProgress(_quest.QuestId);
                        string cloudMsg = !ProgressionBackendGate.CloudEnabled
                            ? "skipped (backend gate closed — local-only mode)"
                            : cloud ? "✓" : "✗ (repos not loaded / not signed in)";
                        Debug.Log($"[Quest] '{_quest.QuestId}' FULL reset — local ✓, progression+vessels ✓, cloud {cloudMsg}.");
                        RefreshProgress();
                    }
                    else
                    {
                        QuestProgressStore.ResetLocal(_quest.QuestId);
                        RefreshProgress();
                        Debug.Log(ProgressionBackendGate.CloudEnabled
                            ? $"[Quest] '{_quest.QuestId}' local mirror cleared. Enter PLAY MODE (signed in) and press again for the full backend reset (progression, intensities, vessels)."
                            : $"[Quest] '{_quest.QuestId}' local mirror cleared — with the backend gate closed this IS the full reset (progression is session-local and starts fresh every play).");
                    }
                }

                GUILayout.Space(8);
            }

            if (_graph != null)
            {
                GUILayout.Label($"PHASE — {_graph.PhaseName}", QuestGraphStyles.PanelHeader);
                EditorGUI.BeginChangeCheck();
                bool phaseOn = EditorGUILayout.ToggleLeft(
                    new GUIContent("Phase Enabled (runner)", "The runner skips disabled phases."),
                    _graph.phaseEnabled);
                string pname = EditorGUILayout.TextField("Phase Name", _graph.phaseName);
                GUILayout.Label("Designer Notes", EditorStyles.miniLabel);
                string pnotes = ScrollableNotesArea(_graph.designerNotes, ref _phaseNotesScroll, 92f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_graph, "Edit Phase");
                    _graph.phaseEnabled = phaseOn;
                    _graph.phaseName = pname;
                    _graph.designerNotes = pnotes;
                    EditorUtility.SetDirty(_graph);
                }
                GUILayout.Space(8);
            }

            if (_selectedNode != null)
            {
                GUILayout.Label($"NODE — {_selectedNode.NodeTypeLabel}", QuestGraphStyles.PanelHeader);
                if (!string.IsNullOrEmpty(_selectedNode.TypeTooltip))
                    EditorGUILayout.HelpBox(_selectedNode.TypeTooltip, MessageType.None);

                EditorGUI.BeginChangeCheck();
                bool nodeOn = EditorGUILayout.ToggleLeft(
                    new GUIContent("Node Enabled (runner)", "The runner passes straight through disabled nodes."),
                    _selectedNode.nodeEnabled);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedNode, "Toggle Node Enabled");
                    _selectedNode.nodeEnabled = nodeOn;
                    SaveToggleToDisk(_selectedNode);
                    MarkValidationDirty();
                }

                EditorGUI.BeginChangeCheck();
                string dn = EditorGUILayout.TextField("Display Name", _selectedNode.displayName);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedNode, "Edit Node Name");
                    _selectedNode.displayName = dn;
                    EditorUtility.SetDirty(_selectedNode);
                }

                using (new EditorGUI.DisabledScope(_graph == null || _graph.entryNode == _selectedNode))
                {
                    if (GUILayout.Button(_graph != null && _graph.entryNode == _selectedNode ? "Entry Node ✓" : "Set As Entry Node"))
                    {
                        Undo.RecordObject(_graph, "Set Entry Node");
                        _graph.entryNode = _selectedNode;
                        EditorUtility.SetDirty(_graph);
                        MarkValidationDirty();
                    }
                }

                GUILayout.Space(4);
                GUILayout.Label("Fields", EditorStyles.boldLabel);
                if (_nodeEditor == null || _nodeEditor.target != _selectedNode)
                {
                    if (_nodeEditor != null) DestroyImmediate(_nodeEditor);
                    _nodeEditor = UnityEditor.Editor.CreateEditor(_selectedNode);
                }
                EditorGUI.BeginChangeCheck();
                _nodeEditor.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                    MarkValidationDirty();

                GUILayout.Space(6);
                DrawConnections();
                GUILayout.Space(8);
            }
            else if (_graph != null)
            {
                EditorGUILayout.HelpBox(
                    "Canvas controls:\n• Drag a node to move it\n• Drag from an output port to connect (release on empty canvas to spawn-and-connect)\n• Wheel = zoom · Middle/Alt-drag = pan · F = frame\n• Right-click canvas to add a node",
                    MessageType.Info);
                GUILayout.Space(8);
            }

            DrawValidation();
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>
        /// Play-mode testing readout: the live arcade-funnel constraints and progression
        /// unlocks — so "why is this card locked / this intensity blocked?" is answered at a
        /// glance instead of by replaying the quest. Includes a Clear Funnel escape hatch.
        /// </summary>
        void DrawLiveState()
        {
            GUILayout.Space(4);
            GUILayout.Label("LIVE STATE (play mode)", QuestGraphStyles.PanelHeader);

            string funnel = QuestArcadeConstraints.Active
                ? $"ACTIVE — only {QuestArcadeConstraints.AllowedMode}"
                  + (QuestArcadeConstraints.ForcedIntensity > 0 ? $" @ intensity {QuestArcadeConstraints.ForcedIntensity}" : ", intensities open")
                  + (QuestArcadeConstraints.ForcedPlayerCount > 0 ? $" · {QuestArcadeConstraints.ForcedPlayerCount}p" : string.Empty)
                  + (QuestArcadeConstraints.ForcedDomainCount > 0 ? $" · {QuestArcadeConstraints.ForcedDomainCount} domains" : string.Empty)
                : "inactive (arcade unrestricted)";

            string unlocks = "(progression service not alive)";
            var svc = GameModeProgressionService.Instance;
            if (svc != null)
            {
                var parts = new List<string>();
                foreach (var modeName in svc.ProgressionData.UnlockedModes)
                {
                    string tier = System.Enum.TryParse(modeName, out CosmicShore.Data.GameModes m)
                        ? $" (tier ≤{svc.GetMaxUnlockedIntensity(m)})"
                        : string.Empty;
                    parts.Add(modeName + tier);
                }
                unlocks = parts.Count > 0 ? string.Join(", ", parts) : "(none)";
            }

            EditorGUILayout.HelpBox($"Arcade funnel: {funnel}\nUnlocked modes: {unlocks}", MessageType.None);

            if (QuestArcadeConstraints.Active
                && CenteredButton(new GUIContent("Clear Arcade Funnel Now",
                    "TESTING: drop all funnel constraints immediately (cards + intensities revert to pure progression gating).")))
            {
                QuestArcadeConstraints.Clear();
                Debug.Log("[Quest] Arcade funnel cleared from the editor.");
            }
        }

        /// <summary>
        /// When the ▶ NEXT node is a tier gate, spell out WHY it hasn't advanced: the authored
        /// unlock goal (stat threshold or play count), the mode's current tier, and — in Play
        /// mode — the recorded plays. Answers "I played it once, why no progress?" inside the
        /// tool instead of requiring console archaeology.
        /// </summary>
        static string DescribeGateRequirement(QuestNodeSO node)
        {
            if (node is not QuestWaitForIntensityNode gate) return null;

            var svc = GameModeProgressionService.Instance;
            var questList = svc != null ? svc.QuestList
                : QuestRunnerSetup.FindAsset<CosmicShore.ScriptableObjects.SO_UnlockList>();

            CosmicShore.ScriptableObjects.SO_UnlockData quest = null;
            if (questList != null)
            {
                foreach (var q in questList.Quests)
                {
                    if (q != null && q.GameMode == gate.mode) { quest = q; break; }
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append($"GATE — {gate.mode} must reach tier {gate.intensityTier}");
            if (svc != null && Application.isPlaying)
                sb.Append($" (currently ≤{svc.GetMaxUnlockedIntensity(gate.mode)})");
            sb.Append('.');

            if (quest == null) return sb.ToString();

            int playIntensity = Mathf.Max(1, gate.intensityTier - 1);
            bool statBased = quest.IntensityUnlockStatType !=
                             CosmicShore.ScriptableObjects.QuestTargetType.Placeholder;
            if (statBased)
            {
                float target = gate.intensityTier >= 4 ? quest.Intensity4StatTarget : quest.Intensity3StatTarget;
                string requirement = quest.IntensityUnlockStatType switch
                {
                    CosmicShore.ScriptableObjects.QuestTargetType.RaceTimeUnder =>
                        $"finish time ≤ {target}s (must finish on the WINNING domain — a loss scores 0)",
                    CosmicShore.ScriptableObjects.QuestTargetType.WinMatch =>
                        "WIN the match (rank first — your domain takes the game)",
                    _ => $"{quest.IntensityUnlockStatType} ≥ {target}",
                };
                sb.Append($"\nUnlocks by: {requirement} in ONE intensity-{playIntensity} game.");
                string desc = gate.intensityTier >= 4 ? quest.Intensity4GoalDescription : quest.Intensity3GoalDescription;
                if (!string.IsNullOrEmpty(desc))
                    sb.Append($"\nGoal text: \"{desc}\"");
            }
            else
            {
                int needed = gate.intensityTier >= 4 ? quest.PlaysToUnlockIntensity4 : quest.PlaysToUnlockIntensity3;
                sb.Append($"\nUnlocks by: playing {needed} game(s) at intensity {playIntensity}");
                if (svc != null && Application.isPlaying)
                    sb.Append($" — {svc.ProgressionData.GetIntensityPlayCount(gate.mode.ToString(), playIntensity)}/{needed} so far");
                sb.Append('.');
            }

            return sb.ToString();
        }

        /// <summary>
        /// Action button that fits its label instead of stretching to the panel width, centered
        /// on its own row — full-width buttons read as section bars and made the inspector clumsy.
        /// </summary>
        static bool CenteredButton(GUIContent content)
        {
            var size = GUI.skin.button.CalcSize(content);
            float width = Mathf.Max(170f, size.x + 18f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool pressed = GUILayout.Button(content, GUILayout.Width(width), GUILayout.Height(22f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            return pressed;
        }

        /// <summary>
        /// Fixed-height notes editor with a WORKING scrollbar: a raw TextArea only auto-scrolls
        /// with the caret (its "scrollbar" isn't draggable), so long notes were unreachable by
        /// mouse. The TextArea expands to its content inside a real ScrollView instead.
        /// </summary>
        static string ScrollableNotesArea(string text, ref Vector2 scroll, float height)
        {
            text ??= string.Empty;

            // The scrollbar sets GUI.changed on every drag, which would trip the caller's
            // BeginChangeCheck and dirty the asset from mere scrolling — isolate it so only
            // a real text edit leaks out.
            bool outerChanged = GUI.changed;
            GUI.changed = false;

            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(height));
            string result = EditorGUILayout.TextArea(text, QuestGraphStyles.NotesArea, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            GUI.changed = outerChanged || !string.Equals(result, text, StringComparison.Ordinal);
            return result;
        }

        void DrawConnections()
        {
            GUILayout.Label("Connections", EditorStyles.boldLabel);

            var others = _graph.nodes.Where(n => n != null && n != _selectedNode).ToList();
            var labels = new string[others.Count + 1];
            labels[0] = "(end of flow)";
            for (int i = 0; i < others.Count; i++)
                labels[i + 1] = NodeLabel(others[i]);

            foreach (var port in _selectedNode.OutputPorts)
            {
                var edge = _selectedNode.EdgeForPort(port);
                int current = 0;
                if (edge != null && !string.IsNullOrEmpty(edge.targetNodeId))
                {
                    int idx = others.FindIndex(n => n.nodeId == edge.targetNodeId);
                    current = idx >= 0 ? idx + 1 : 0;
                }

                EditorGUI.BeginChangeCheck();
                int picked = EditorGUILayout.Popup(port, current, labels);
                if (EditorGUI.EndChangeCheck())
                    SetEdge(_selectedNode, port, picked == 0 ? null : others[picked - 1].nodeId);

                if (edge != null && !string.IsNullOrEmpty(edge.targetNodeId))
                {
                    EditorGUI.BeginChangeCheck();
                    float delay = EditorGUILayout.FloatField(
                        new GUIContent("    ↳ Delay (s)", "Real-time pause before the next node runs — pacing between beats."),
                        edge.delaySeconds);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_selectedNode, "Edit Edge Delay");
                        edge.delaySeconds = Mathf.Max(0f, delay);
                        EditorUtility.SetDirty(_selectedNode);
                    }
                }
            }

            if (_selectedNode.OutputPorts.Count == 0)
                GUILayout.Label("Terminal node — no outputs.", EditorStyles.miniLabel);
        }

        void DrawValidation()
        {
            if (_graph == null && _quest == null) return;

            if (_validationDirty)
            {
                _validation.Clear();
                ValidateQuest(_quest, _validation);
                ValidateGraph(_graph, _validation);
                _validationDirty = false;
            }

            GUILayout.Label("Validation", QuestGraphStyles.PanelHeader);
            if (_validation.Count == 0)
                EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
            else
                foreach (var v in _validation)
                    EditorGUILayout.HelpBox(v, MessageType.Warning);
        }

        static void ValidateQuest(QuestSO quest, List<string> sink)
        {
            if (quest == null) return;
            if (quest.phases.Count == 0)
                sink.Add("Quest has no phases.");
            for (int i = 0; i < quest.phases.Count; i++)
                if (quest.phases[i] == null)
                    sink.Add($"Quest phase slot {i} is empty.");

            // The arcade funnel PERSISTS (PlayerPrefs) — a quest that applies constraints but
            // never clears them leaves the arcade locked to the tutorial mode forever, so a
            // newly claimed mode shows its CTA but its card stays locked.
            bool applies = false, clears = false;
            foreach (var phase in quest.phases)
            {
                if (phase == null) continue;
                foreach (var n in phase.nodes)
                    if (n is QuestSetArcadeConstraintsNode c && n.nodeEnabled)
                    {
                        if (c.clearConstraints) clears = true;
                        else applies = true;
                    }
            }
            if (applies && !clears)
                sink.Add("Arcade constraints are applied but NEVER cleared in any phase — add a Set Arcade " +
                         "Constraints node with 'Clear Constraints' ticked (else newly unlocked modes stay locked " +
                         "in the arcade forever).");
        }

        void ValidateGraph(QuestPhaseGraphSO graph, List<string> sink)
        {
            if (graph == null) return;

            if (graph.entryNode == null)
                sink.Add("Phase has no entry node.");
            else if (!graph.entryNode.nodeEnabled)
                sink.Add("Entry node is disabled — the runner will pass straight through it.");

            var reachable = new HashSet<QuestNodeSO>();
            if (graph.entryNode != null)
            {
                var stack = new Stack<QuestNodeSO>();
                stack.Push(graph.entryNode);
                while (stack.Count > 0)
                {
                    var n = stack.Pop();
                    if (n == null || !reachable.Add(n)) continue;
                    foreach (var edge in n.Outputs)
                    {
                        var t = graph.FindNode(edge.targetNodeId);
                        if (t != null) stack.Push(t);
                    }
                }
            }

            bool hasTerminal = false;
            foreach (var n in graph.nodes)
            {
                if (n == null) continue;
                if (graph.entryNode != null && !reachable.Contains(n))
                    sink.Add($"'{NodeLabel(n)}' is unreachable from the entry node.");
                foreach (var edge in n.Outputs)
                    if (!string.IsNullOrEmpty(edge.targetNodeId) && graph.FindNode(edge.targetNodeId) == null)
                        sink.Add($"'{NodeLabel(n)}' has an edge to a missing node.");
                if (n is QuestPhaseEndNode || n is QuestEndNode)
                    hasTerminal = true;
                n.Validate(graph, sink);
            }

            if (!hasTerminal && graph.nodes.Count > 0)
                sink.Add("Phase has no Phase End (or Quest End) node — it will auto-advance on a dead end.");
        }

        // ════════════════════════════════════════════════════════════════
        // Overlays — legend + tooltip (window space, crisp at any zoom)
        // ════════════════════════════════════════════════════════════════

        void DrawOverlays()
        {
            if (_showLegend && _graph != null)
                DrawLegend();

            if (!string.IsNullOrEmpty(_tooltip))
            {
                var content = new GUIContent(_tooltip);
                float w = Mathf.Min(300f, QuestGraphStyles.Tooltip.CalcSize(content).x + 4f);
                float h = QuestGraphStyles.Tooltip.CalcHeight(content, w);
                var pos = _tooltipWinPos;
                pos.x = Mathf.Min(pos.x, position.width - EffectiveRightW - w - 8f);
                pos.y = Mathf.Min(pos.y, position.height - h - 8f);
                var r = new Rect(pos, new Vector2(w, h));
                EditorGUI.DrawRect(new Rect(r.x - 5, r.y - 4, r.width + 10, r.height + 8), QuestGraphStyles.TooltipBg);
                GUI.Label(r, content, QuestGraphStyles.Tooltip);
            }
        }

        static readonly (QuestNodeCategory cat, string label)[] LegendEntries =
        {
            (QuestNodeCategory.Flow, "Flow — pacing & cinematics"),
            (QuestNodeCategory.Presentation, "Presentation — instructions & dialogue"),
            (QuestNodeCategory.Gameplay, "Gameplay — control, navigation, locking"),
            (QuestNodeCategory.Gate, "Gate — waits for player / game"),
            (QuestNodeCategory.Guidance, "Guidance — CTA breadcrumbs"),
            (QuestNodeCategory.Progression, "Progression — unlock writes"),
            (QuestNodeCategory.Terminal, "Terminal — phase / quest end"),
        };

        void DrawLegend()
        {
            const float lineH = 17f;
            float w = 250f;
            float h = LegendEntries.Length * lineH + 26f;
            var r = new Rect(_canvasRect.x + 10, _canvasRect.yMax - h - 10, w, h);
            EditorGUI.DrawRect(r, QuestGraphStyles.LegendBg);
            GUI.Label(new Rect(r.x + 8, r.y + 4, w - 16, 16), "NODE COLORS", QuestGraphStyles.LegendHeader);
            for (int i = 0; i < LegendEntries.Length; i++)
            {
                float y = r.y + 22f + i * lineH;
                EditorGUI.DrawRect(new Rect(r.x + 8, y + 3, 10, 10), QuestGraphStyles.CategoryColor(LegendEntries[i].cat));
                GUI.Label(new Rect(r.x + 24, y, w - 30, lineH), LegendEntries[i].label, QuestGraphStyles.LegendLabel);
            }
        }

        // ════════════════════════════════════════════════════════════════
        // Selection / asset ops
        // ════════════════════════════════════════════════════════════════

        void RefreshAssets()
        {
            _quests.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:QuestSO"))
            {
                var q = AssetDatabase.LoadAssetAtPath<QuestSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (q != null) _quests.Add(q);
            }

        }

        void SelectQuest(QuestSO quest)
        {
            _quest = quest;
            _questRename = quest != null ? quest.name : null;
            RefreshProgress();
            if (quest.phases.Count > 0 && quest.phases[0] != null && (_graph == null || !quest.phases.Contains(_graph)))
                SelectGraph(quest.phases[0], quest);
            MarkValidationDirty();
            Repaint();
        }

        void SelectGraph(QuestPhaseGraphSO graph, QuestSO owner)
        {
            _graph = graph;
            if (owner != null) _quest = owner;
            SelectNode(null);
            _linkFrom = null;
            _cards.Clear();
            MarkValidationDirty();
            Repaint();
        }

        void SelectNode(QuestNodeSO node) => _selectedNode = node;

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }

        static void EnsureDefaultFolders()
        {
            EnsureFolder("Assets/FTUE", "DataContainer");
            EnsureFolder(RootFolder, "Quests");
            EnsureFolder(RootFolder, "Phases");
        }

        void CreateQuestAsset()
        {
            EnsureDefaultFolders();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{QuestsFolder}/Quest_New.asset");
            var quest = CreateInstance<QuestSO>();
            quest.questId = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(quest, path);
            AssetDatabase.SaveAssets();
            RefreshAssets();
            SelectQuest(quest);
        }

        QuestPhaseGraphSO CreateGraphAsset(string baseName)
        {
            EnsureDefaultFolders();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{PhasesFolder}/{baseName ?? "QuestPhase_New"}.asset");
            var graph = CreateInstance<QuestPhaseGraphSO>();
            graph.graphId = System.IO.Path.GetFileNameWithoutExtension(path);
            graph.phaseName = graph.graphId;
            AssetDatabase.CreateAsset(graph, path);
            AssetDatabase.SaveAssets();
            RefreshAssets();
            SelectGraph(graph, null);
            return graph;
        }

        void AddPhase(QuestSO quest)
        {
            var graph = CreateGraphAsset($"{quest.name}_Phase_{quest.phases.Count}");
            Undo.RecordObject(quest, "Add Quest Phase");
            quest.phases.Add(graph);
            EditorUtility.SetDirty(quest);
            AssetDatabase.SaveAssets();
            RefreshAssets();
            SelectGraph(graph, quest);
        }

        void MovePhase(QuestSO quest, int index, int dir)
        {
            int to = index + dir;
            if (to < 0 || to >= quest.phases.Count) return;
            Undo.RecordObject(quest, "Reorder Quest Phases");
            (quest.phases[index], quest.phases[to]) = (quest.phases[to], quest.phases[index]);
            EditorUtility.SetDirty(quest);
            MarkValidationDirty();
        }

        void RemovePhase(QuestSO quest, int index)
        {
            var phase = quest.phases[index];
            string phaseName = phase != null ? phase.PhaseName : "(missing)";
            int choice = EditorUtility.DisplayDialogComplex("Remove Phase",
                $"Remove phase {index} ('{phaseName}') from '{quest.name}'?",
                "Remove From Quest", "Cancel", "Remove + Delete Asset");
            if (choice == 1) return;

            Undo.RecordObject(quest, "Remove Quest Phase");
            quest.phases.RemoveAt(index);
            EditorUtility.SetDirty(quest);

            if (choice == 2 && phase != null)
            {
                if (_graph == phase) SelectGraph(null, quest);
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(phase));
            }

            AssetDatabase.SaveAssets();
            RefreshAssets();
            MarkValidationDirty();
        }

        // ── Node ops ───────────────────────────────────────────────────

        void ShowCreateNodeMenu(Vector2 worldPos, QuestNodeSO connectFrom, string connectPort)
        {
            var menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<QuestNodeSO>()
                .Where(t => !t.IsAbstract)
                .Select(t => (type: t, label: MenuLabelFor(t)))
                .OrderBy(x => x.label);

            foreach (var (type, label) in types)
                menu.AddItem(new GUIContent(label), false, () => CreateNode(type, worldPos, connectFrom, connectPort));
            menu.ShowAsContext();

            if (connectFrom != null)
            {
                // Consume the armed link either way; the callback rewires it if a type is picked.
                _linkFrom = null;
                _linkDrag = false;
            }
        }

        static string MenuLabelFor(Type t)
        {
            var probe = (QuestNodeSO)CreateInstance(t);
            string label = $"{probe.Category}/{SpaceCamelCase(probe.NodeTypeLabel)}";
            DestroyImmediate(probe);
            return label;
        }

        static string SpaceCamelCase(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length + 6);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1]))
                    sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        void CreateNode(Type type, Vector2 worldPos, QuestNodeSO connectFrom, string connectPort)
        {
            if (_graph == null) return;

            var node = (QuestNodeSO)CreateInstance(type);
            node.nodeId = Guid.NewGuid().ToString("N");
            node.name = type.Name;
            node.displayName = SpaceCamelCase(node.NodeTypeLabel);
            node.graphPosition = worldPos;

            Undo.RegisterCreatedObjectUndo(node, "Create Quest Node");
            AssetDatabase.AddObjectToAsset(node, _graph);
            Undo.RecordObject(_graph, "Create Quest Node");
            _graph.nodes.Add(node);
            if (_graph.entryNode == null)
                _graph.entryNode = node;

            if (connectFrom != null)
                SetEdge(connectFrom, connectPort, node.nodeId);

            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            SelectNode(node);
            MarkValidationDirty();
            Repaint();
        }

        void DeleteNode(QuestNodeSO node)
        {
            Undo.RecordObject(_graph, "Delete Quest Node");
            foreach (var n in _graph.nodes)
                if (n != null)
                    n.Outputs.RemoveAll(edge => edge.targetNodeId == node.nodeId);

            _graph.nodes.Remove(node);
            if (_graph.entryNode == node)
                _graph.entryNode = _graph.nodes.FirstOrDefault(n => n != null);
            if (_selectedNode == node)
                SelectNode(null);
            _cards.Remove(node);

            AssetDatabase.RemoveObjectFromAsset(node);
            DestroyImmediate(node, true);

            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            MarkValidationDirty();
            Repaint();
        }

        void SetEdge(QuestNodeSO from, string port, string targetNodeId)
        {
            Undo.RecordObject(from, "Connect Quest Nodes");
            var edge = from.EdgeForPort(port);
            if (string.IsNullOrEmpty(targetNodeId))
            {
                if (edge != null) from.Outputs.Remove(edge);
            }
            else if (edge != null)
            {
                edge.targetNodeId = targetNodeId;
            }
            else
            {
                from.Outputs.Add(new QuestEdge(port, targetNodeId));
            }
            EditorUtility.SetDirty(from);
            MarkValidationDirty();
        }

        void ShowNodeMenu(QuestNodeSO node)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Enabled"), node.nodeEnabled, () =>
            {
                Undo.RecordObject(node, "Toggle Node Enabled");
                node.nodeEnabled = !node.nodeEnabled;
                SaveToggleToDisk(node);
                MarkValidationDirty();
            });
            menu.AddItem(new GUIContent("Set As Entry Node"), _graph.entryNode == node, () =>
            {
                Undo.RecordObject(_graph, "Set Entry Node");
                _graph.entryNode = node;
                EditorUtility.SetDirty(_graph);
                MarkValidationDirty();
            });
            menu.AddItem(new GUIContent("Disconnect All Outputs"), false, () =>
            {
                Undo.RecordObject(node, "Disconnect Node");
                node.Outputs.Clear();
                EditorUtility.SetDirty(node);
                MarkValidationDirty();
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete Node"), false, () => DeleteNode(node));
            menu.ShowAsContext();
        }

        void ShowPortMenu(QuestNodeSO node, string port)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent($"Disconnect '{port}'"), false, () => SetEdge(node, port, null));
            menu.ShowAsContext();
        }

        /// <summary>
        /// Re-arrange the open phase with <see cref="QuestGraphLayout"/> — one row per venue
        /// (app shell / gameplay), broken wherever the flow moves the player between them —
        /// then frame the result. Undoable; press Save to write it to disk.
        /// </summary>
        void LayoutRows()
        {
            if (_graph == null) return;

            int rows = QuestGraphLayout.LayoutRows(_graph);
            _cards.Clear();
            FrameContent();
            Debug.Log($"[Quest] Laid out '{_graph.PhaseName}' into {rows} row{(rows == 1 ? "" : "s")}. Save to keep it.");
        }

        void FrameContent()
        {
            if (_graph == null || _graph.nodes.Count == 0) return;

            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            foreach (var n in _graph.nodes)
            {
                if (n == null) continue;
                var size = GetCard(n).size;
                min = Vector2.Min(min, n.graphPosition);
                max = Vector2.Max(max, n.graphPosition + size);
            }

            Vector2 bounds = max - min;
            float fitZoom = Mathf.Min((_canvasRect.width - 60f) / Mathf.Max(bounds.x, 1f),
                                      (_canvasRect.height - 60f) / Mathf.Max(bounds.y, 1f));
            Zoom = Mathf.Clamp(fitZoom, MinZoom, 1f);
            _graph.canvasScroll = (_canvasRect.size / Zoom - bounds) * 0.5f - min;
            EditorUtility.SetDirty(_graph);
            Repaint();
        }

        /// <summary>
        /// Enable/disable toggles are the tool's TEST-HARNESS state and must land in git the
        /// moment they're flipped — SetDirty alone only changes the in-memory asset, and a
        /// toggle that never hits disk silently vanishes from version control.
        /// </summary>
        static void SaveToggleToDisk(UnityEngine.Object obj)
        {
            EditorUtility.SetDirty(obj);
            AssetDatabase.SaveAssets();
        }

        /// <summary>True when the selected quest/phase/nodes have unsaved (in-memory) edits.</summary>
        bool HasUnsavedEdits()
        {
            if (_quest != null && EditorUtility.IsDirty(_quest)) return true;
            if (_graph != null)
            {
                if (EditorUtility.IsDirty(_graph)) return true;
                foreach (var n in _graph.nodes)
                    if (n != null && EditorUtility.IsDirty(n))
                        return true;
            }
            return false;
        }

        void SaveAll()
        {
            if (_quest != null) EditorUtility.SetDirty(_quest);
            if (_graph != null)
            {
                EditorUtility.SetDirty(_graph);
                foreach (var n in _graph.nodes)
                    if (n != null) EditorUtility.SetDirty(n);
            }
            AssetDatabase.SaveAssets();
        }

        static string NodeLabel(QuestNodeSO n) =>
            string.IsNullOrEmpty(n.displayName) ? n.NodeTypeLabel : $"{n.displayName} ({n.NodeTypeLabel})";
    }

    /// <summary>
    /// The classic IMGUI zoom-area: suspends the window's implicit group, applies a scale
    /// matrix pivoted at the canvas origin, and restores everything on End. Mouse positions
    /// read inside the area are automatically in canvas-local (unzoomed) coordinates.
    /// </summary>
    static class QuestZoomArea
    {
        const float TabHeight = 21f;
        static Matrix4x4 _prevMatrix;

        public static void Begin(float zoom, Rect screenRect)
        {
            GUI.EndGroup();

            var clipped = new Rect(screenRect.x, screenRect.y + TabHeight,
                screenRect.width / zoom, screenRect.height / zoom);
            GUI.BeginGroup(clipped);

            _prevMatrix = GUI.matrix;
            var pivot = new Vector2(clipped.x, clipped.y);
            var translation = Matrix4x4.TRS(pivot, Quaternion.identity, Vector3.one);
            var scale = Matrix4x4.Scale(new Vector3(zoom, zoom, 1f));
            GUI.matrix = translation * scale * translation.inverse * GUI.matrix;
        }

        public static void End(Rect windowPosition)
        {
            GUI.matrix = _prevMatrix;
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(0f, TabHeight, windowPosition.width, windowPosition.height));
        }
    }

    /// <summary>Cached styles + palette for the Quest Graph editor (no per-frame GUIStyle allocation).</summary>
    static class QuestGraphStyles
    {
        static bool _ready;

        public static Color ToolbarBg, PanelBg, CanvasBg, GridMinor, GridMajor;
        public static Color NodeBody, NodeBodyHover, NodeShadow, PortIn, PortRim, PortArmed;
        public static Color EntryBorder, SelectionBorder, LinkPreview, LegendBg, TooltipBg;
        public static Color RowSelected, RowSelectedFaint, SplitterLine, ResumeBorder;

        public static GUIStyle Breadcrumb, PanelHeader, RowLabel, RowLabelSelected, RowLabelSmall;
        public static GUIStyle MiniButton, MiniButtonDanger, MiniWide;
        public static GUIStyle NodeHeader, NodeTitle, NodeSummary, NodeDelete, EntryBadge, PortLabel;
        public static GUIStyle CanvasHint, LegendHeader, LegendLabel, Tooltip, NotesArea, EdgeLabel;
        public static GUIStyle DoneBadge, ResumeBadge;

        public static void Ensure()
        {
            if (_ready) return;
            _ready = true;

            ToolbarBg = new Color(0.13f, 0.14f, 0.18f);
            PanelBg = new Color(0.16f, 0.17f, 0.21f);
            CanvasBg = new Color(0.082f, 0.09f, 0.11f);
            GridMinor = new Color(1f, 1f, 1f, 0.028f);
            GridMajor = new Color(1f, 1f, 1f, 0.055f);
            NodeBody = new Color(0.155f, 0.165f, 0.205f, 0.98f);
            NodeBodyHover = new Color(0.195f, 0.21f, 0.26f, 0.99f);
            NodeShadow = new Color(0f, 0f, 0f, 0.28f);
            PortIn = new Color(0.75f, 0.82f, 0.95f);
            PortRim = new Color(0.06f, 0.065f, 0.08f);
            PortArmed = new Color(1f, 0.84f, 0.35f);
            EntryBorder = new Color(0.28f, 0.85f, 0.45f);
            SelectionBorder = new Color(1f, 0.8f, 0.3f);
            LinkPreview = new Color(1f, 0.84f, 0.35f, 0.95f);
            LegendBg = new Color(0.1f, 0.11f, 0.14f, 0.94f);
            TooltipBg = new Color(0.08f, 0.09f, 0.115f, 0.97f);
            RowSelected = new Color(0.25f, 0.32f, 0.52f, 0.85f);
            RowSelectedFaint = new Color(0.25f, 0.32f, 0.52f, 0.45f);
            SplitterLine = new Color(0f, 0f, 0f, 0.55f);
            ResumeBorder = new Color(1f, 0.62f, 0.18f);

            Breadcrumb = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            PanelHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.62f, 0.67f, 0.78f) },
            };
            RowLabel = new GUIStyle(EditorStyles.label) { fontSize = 12 };
            RowLabelSelected = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            RowLabelSmall = new GUIStyle(EditorStyles.label) { fontSize = 11 };
            MiniButton = new GUIStyle(EditorStyles.miniButton) { fontSize = 8, padding = new RectOffset(1, 1, 1, 1) };
            MiniButtonDanger = new GUIStyle(MiniButton) { normal = { textColor = new Color(0.95f, 0.45f, 0.45f) } };
            MiniWide = new GUIStyle(EditorStyles.miniButton) { fontSize = 10 };

            NodeHeader = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = Color.white },
                clipping = TextClipping.Clip,
            };
            NodeTitle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.9f, 0.92f, 0.96f) },
            };
            NodeSummary = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.66f, 0.76f) },
            };
            NodeDelete = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.55f) },
                hover = { textColor = new Color(1f, 0.5f, 0.5f) },
            };
            EntryBadge = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.85f, 1f, 0.9f) },
            };
            PortLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.65f, 0.72f, 0.85f) },
            };
            CanvasHint = new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.55f, 0.6f, 0.7f) },
            };
            LegendHeader = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.6f, 0.66f, 0.78f) },
            };
            LegendLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.78f, 0.82f, 0.9f) },
            };
            Tooltip = new GUIStyle(EditorStyles.wordWrappedMiniLabel)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.88f, 0.9f, 0.95f) },
            };
            NotesArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 11 };
            EdgeLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.55f, 0.75f, 1f) },
            };
            DoneBadge = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 0.95f, 0.5f) },
            };
            ResumeBadge = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.1f, 0.07f, 0.02f) },
            };
        }

        public static Color CategoryColor(QuestNodeCategory cat) => cat switch
        {
            QuestNodeCategory.Flow => new Color(0.35f, 0.41f, 0.78f),
            QuestNodeCategory.Presentation => new Color(0.16f, 0.62f, 0.56f),
            QuestNodeCategory.Gameplay => new Color(0.56f, 0.34f, 0.72f),
            QuestNodeCategory.Gate => new Color(0.24f, 0.49f, 0.8f),
            QuestNodeCategory.Guidance => new Color(0.76f, 0.62f, 0.19f),
            QuestNodeCategory.Progression => new Color(0.82f, 0.49f, 0.18f),
            QuestNodeCategory.Terminal => new Color(0.24f, 0.62f, 0.32f),
            _ => new Color(0.4f, 0.4f, 0.45f),
        };
    }
}
#endif
