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
    /// Node/flowchart editor for authoring <see cref="FTUEGraphSO"/> FTUE flows.
    ///
    /// Built on IMGUI to match the rest of the project's editor tooling
    /// (DialogueEditorWindow, EndConditionOverridesWindow) — no GraphView/UIToolkit, which
    /// the repo does not use anywhere. Three panels:
    ///   • Left   — pick / create / delete FTUE graph assets.
    ///   • Center — draggable node canvas; connect ports; set the entry node.
    ///   • Right  — the selected node's typed inspector, its per-port connections, and validation.
    /// </summary>
    public class FTUEGraphEditorWindow : EditorWindow
    {
        FTUEGraphSO _graph;
        FTUENodeSO _selectedNode;
        UnityEditor.Editor _nodeEditor;

        Vector2 _leftScroll, _rightScroll;

        const float ToolbarHeight = 22f;
        const float LeftWidth = 210f;
        const float RightWidth = 330f;

        const float NodeWidth = 176f;
        const float NodeHeight = 66f;
        const float HeaderHeight = 22f;
        const float PortSize = 14f;

        // Linking state (click an output port, then click a target node).
        FTUENodeSO _linkFrom;
        string _linkFromPort;

        // Node drag state.
        FTUENodeSO _dragNode;

        static readonly string[] EmptyPortTargetLabels = { "(end)" };

        [MenuItem("FrogletTools/FTUE/Graph Editor")]
        public static void Open() => GetWindow<FTUEGraphEditorWindow>("FTUE Graph");

        void OnDisable()
        {
            if (_nodeEditor != null)
                DestroyImmediate(_nodeEditor);
        }

        void OnGUI()
        {
            DrawToolbar();

            float bodyTop = ToolbarHeight;
            var leftRect = new Rect(0, bodyTop, LeftWidth, position.height - bodyTop);
            var rightRect = new Rect(position.width - RightWidth, bodyTop, RightWidth, position.height - bodyTop);
            var canvasRect = new Rect(LeftWidth, bodyTop, position.width - LeftWidth - RightWidth, position.height - bodyTop);

            DrawLeftPanel(leftRect);
            DrawCanvas(canvasRect);
            DrawRightPanel(rightRect);
        }

        // ── Toolbar ────────────────────────────────────────────────────

        void DrawToolbar()
        {
            var rect = new Rect(0, 0, position.width, ToolbarHeight);
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.20f, 0.27f, 1f));

            GUILayout.BeginArea(rect);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6);

            EditorGUI.BeginChangeCheck();
            var picked = (FTUEGraphSO)EditorGUILayout.ObjectField(_graph, typeof(FTUEGraphSO), false, GUILayout.Width(240));
            if (EditorGUI.EndChangeCheck())
                SelectGraph(picked);

            using (new EditorGUI.DisabledScope(_graph == null))
            {
                if (GUILayout.Button("+ Add Node", EditorStyles.toolbarButton, GUILayout.Width(90)))
                    ShowAddNodeMenu();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(60)))
                SaveGraph();

            GUILayout.Space(6);
            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ── Left Panel: graph list ─────────────────────────────────────

        void DrawLeftPanel(Rect rect)
        {
            GUILayout.BeginArea(rect, GUI.skin.box);

            EditorGUILayout.LabelField("FTUE Graphs", EditorStyles.boldLabel);
            GUILayout.Space(4);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            foreach (var guid in AssetDatabase.FindAssets("t:FTUEGraphSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var g = AssetDatabase.LoadAssetAtPath<FTUEGraphSO>(path);
                if (g == null) continue;

                bool isSel = g == _graph;
                var prevBg = GUI.backgroundColor;
                if (isSel) GUI.backgroundColor = new Color(0.36f, 0.42f, 0.75f);

                if (GUILayout.Button(g.name, isSel ? EditorStyles.boldLabel : EditorStyles.label))
                    SelectGraph(g);

                GUI.backgroundColor = prevBg;
            }

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ New Graph"))
                CreateGraph();

            GUILayout.EndArea();
        }

        // ── Center Panel: node canvas ──────────────────────────────────

        void DrawCanvas(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.14f, 0.17f, 1f));

            if (_graph == null)
            {
                GUI.Label(rect, "\n  Select or create an FTUE graph.", EditorStyles.wordWrappedLabel);
                return;
            }

            GUI.BeginClip(rect);
            var localCanvas = new Rect(0, 0, rect.width, rect.height);
            DrawGrid(localCanvas);

            var e = Event.current;

            // Edges (drawn under nodes).
            foreach (var node in _graph.nodes.Where(n => n != null))
            {
                var ports = node.OutputPorts;
                for (int p = 0; p < ports.Count; p++)
                {
                    var edge = node.EdgeForPort(ports[p]);
                    if (edge == null) continue;
                    var target = _graph.FindNode(edge.targetNodeId);
                    if (target == null) continue;
                    DrawEdge(OutputPortCenter(node, p, ports.Count), InputPortCenter(target),
                             new Color(0.55f, 0.75f, 1f, 0.9f));
                }
            }

            // In-progress link line.
            if (_linkFrom != null)
            {
                int idx = Mathf.Max(0, _linkFrom.OutputPorts.ToList().IndexOf(_linkFromPort));
                DrawEdge(OutputPortCenter(_linkFrom, idx, _linkFrom.OutputPorts.Count), e.mousePosition,
                         new Color(1f, 0.85f, 0.3f, 0.9f));
                Repaint();
            }

            // Nodes.
            foreach (var node in _graph.nodes.Where(n => n != null))
                DrawNode(node, e);

            // Canvas-level interactions (pan / cancel link / deselect).
            HandleCanvasEvents(localCanvas, e);

            GUI.EndClip();
        }

        void DrawNode(FTUENodeSO node, Event e)
        {
            var r = NodeRect(node);

            // Body + header.
            EditorGUI.DrawRect(r, new Color(0.19f, 0.20f, 0.25f, 0.97f));
            var header = new Rect(r.x, r.y, r.width, HeaderHeight);
            EditorGUI.DrawRect(header, NodeColor(node));

            // Selection / entry borders.
            if (_graph.entryNode == node)
                DrawBorder(r, new Color(0.3f, 1f, 0.5f, 1f), 2f);
            if (_selectedNode == node)
                DrawBorder(r, new Color(1f, 0.85f, 0.3f, 1f), 1f);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
            GUI.Label(new Rect(r.x + 6, r.y + 3, r.width - 40, HeaderHeight), node.NodeTypeLabel, titleStyle);

            if (_graph.entryNode == node)
                GUI.Label(new Rect(r.x + r.width - 44, r.y + 4, 40, HeaderHeight), "ENTRY", EditorStyles.miniBoldLabel);

            GUI.Label(new Rect(r.x + 6, r.y + HeaderHeight + 2, r.width - 12, r.height - HeaderHeight - 4),
                      string.IsNullOrEmpty(node.displayName) ? "<i>(unnamed)</i>" : node.displayName,
                      new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true });

            // Delete button.
            if (GUI.Button(new Rect(r.x + r.width - 20, r.y + 2, 18, 18), "✖"))
            {
                DeleteNode(node);
                GUIUtility.ExitGUI();
            }

            // Input port (left).
            var inRect = InputPortRect(node);
            EditorGUI.DrawRect(inRect, new Color(0.7f, 0.8f, 1f, 1f));

            // Output ports (right).
            var ports = node.OutputPorts;
            for (int p = 0; p < ports.Count; p++)
            {
                var pr = OutputPortRect(node, p, ports.Count);
                bool active = _linkFrom == node && _linkFromPort == ports[p];
                EditorGUI.DrawRect(pr, active ? new Color(1f, 0.85f, 0.3f) : new Color(0.55f, 0.75f, 1f));
                if (ports.Count > 1)
                    GUI.Label(new Rect(pr.x - 54, pr.y - 3, 52, 16), ports[p], EditorStyles.miniLabel);

                if (e.type == EventType.MouseDown && pr.Contains(e.mousePosition) && e.button == 0)
                {
                    _linkFrom = node;
                    _linkFromPort = ports[p];
                    e.Use();
                }
            }

            // Header drag / select / link completion.
            if (e.type == EventType.MouseDown && header.Contains(e.mousePosition) && e.button == 0)
            {
                if (_linkFrom != null && _linkFrom != node)
                {
                    SetEdge(_linkFrom, _linkFromPort, node.nodeId);
                    _linkFrom = null;
                }
                else
                {
                    SelectNode(node);
                    _dragNode = node;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseDown && r.Contains(e.mousePosition) && e.button == 1)
            {
                ShowNodeContextMenu(node);
                e.Use();
            }

            if (e.type == EventType.MouseDrag && _dragNode == node && e.button == 0)
            {
                node.graphPosition += e.delta;
                EditorUtility.SetDirty(node);
                e.Use();
                Repaint();
            }

            if (e.type == EventType.MouseUp)
                _dragNode = null;
        }

        void HandleCanvasEvents(Rect canvas, Event e)
        {
            if (e.type == EventType.MouseDown && canvas.Contains(e.mousePosition))
            {
                if (_linkFrom != null)
                {
                    // Clicked empty canvas while linking → cancel.
                    _linkFrom = null;
                    e.Use();
                }
                else if (e.button == 0)
                {
                    SelectNode(null);
                    Repaint();
                }
            }

            // Pan with middle mouse or right-drag on empty space.
            if (e.type == EventType.MouseDrag && (e.button == 2))
            {
                _graph.canvasScroll += e.delta;
                EditorUtility.SetDirty(_graph);
                e.Use();
                Repaint();
            }
        }

        // ── Right Panel: inspector + connections + validation ──────────

        void DrawRightPanel(Rect rect)
        {
            GUILayout.BeginArea(rect, GUI.skin.box);
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_selectedNode == null)
            {
                EditorGUILayout.HelpBox("Select a node to edit it.\n\n• Drag a node header to move it.\n• Click an output port then a node to connect.\n• Right-click a node for options.", MessageType.Info);
                DrawValidation();
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            EditorGUILayout.LabelField(_selectedNode.NodeTypeLabel, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newName = EditorGUILayout.TextField("Display Name", _selectedNode.displayName);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedNode, "Edit FTUE node name");
                _selectedNode.displayName = newName;
                EditorUtility.SetDirty(_selectedNode);
            }

            using (new EditorGUI.DisabledScope(_graph.entryNode == _selectedNode))
            {
                if (GUILayout.Button(_graph.entryNode == _selectedNode ? "Entry Node" : "Set As Entry Node"))
                {
                    Undo.RecordObject(_graph, "Set FTUE entry node");
                    _graph.entryNode = _selectedNode;
                    EditorUtility.SetDirty(_graph);
                }
            }

            GUILayout.Space(6);
            EditorGUILayout.LabelField("Fields", EditorStyles.boldLabel);
            if (_nodeEditor == null || _nodeEditor.target != _selectedNode)
            {
                if (_nodeEditor != null) DestroyImmediate(_nodeEditor);
                _nodeEditor = UnityEditor.Editor.CreateEditor(_selectedNode);
            }
            _nodeEditor.OnInspectorGUI();

            GUILayout.Space(6);
            DrawConnections();

            GUILayout.Space(8);
            DrawValidation();

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawConnections()
        {
            EditorGUILayout.LabelField("Connections", EditorStyles.boldLabel);

            var nodeList = _graph.nodes.Where(n => n != null && n != _selectedNode).ToList();
            var labels = new List<string> { "(end)" };
            labels.AddRange(nodeList.Select(NodeLabel));
            var labelArray = labels.ToArray();

            foreach (var port in _selectedNode.OutputPorts)
            {
                var edge = _selectedNode.EdgeForPort(port);
                int current = 0;
                if (edge != null && !string.IsNullOrEmpty(edge.targetNodeId))
                {
                    int idx = nodeList.FindIndex(n => n.nodeId == edge.targetNodeId);
                    current = idx >= 0 ? idx + 1 : 0;
                }

                EditorGUI.BeginChangeCheck();
                int picked = EditorGUILayout.Popup(port, current, labelArray);
                if (EditorGUI.EndChangeCheck())
                {
                    string targetId = picked == 0 ? null : nodeList[picked - 1].nodeId;
                    SetEdge(_selectedNode, port, targetId);
                }
            }
        }

        void DrawValidation()
        {
            if (_graph == null) return;

            var errors = ValidateGraph();
            GUILayout.Space(6);
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (errors.Count == 0)
                EditorGUILayout.HelpBox("No problems found.", MessageType.Info);
            else
                foreach (var err in errors)
                    EditorGUILayout.HelpBox(err, MessageType.Warning);
        }

        List<string> ValidateGraph()
        {
            var errors = new List<string>();
            if (_graph == null) return errors;

            if (_graph.entryNode == null)
                errors.Add("No entry node set.");

            var nodes = _graph.nodes.Where(n => n != null).ToList();

            // Reachability from entry.
            var reachable = new HashSet<FTUENodeSO>();
            if (_graph.entryNode != null)
            {
                var stack = new Stack<FTUENodeSO>();
                stack.Push(_graph.entryNode);
                while (stack.Count > 0)
                {
                    var n = stack.Pop();
                    if (!reachable.Add(n)) continue;
                    foreach (var edge in n.Outputs)
                    {
                        var t = _graph.FindNode(edge.targetNodeId);
                        if (t != null) stack.Push(t);
                    }
                }
            }

            foreach (var n in nodes)
            {
                if (_graph.entryNode != null && !reachable.Contains(n))
                    errors.Add($"'{NodeLabel(n)}' is unreachable from the entry node.");

                foreach (var edge in n.Outputs)
                    if (!string.IsNullOrEmpty(edge.targetNodeId) && _graph.FindNode(edge.targetNodeId) == null)
                        errors.Add($"'{NodeLabel(n)}' has an edge to a missing node.");

                n.Validate(_graph, errors);
            }

            return errors;
        }

        // ── Graph / node mutations ─────────────────────────────────────

        void SelectGraph(FTUEGraphSO g)
        {
            _graph = g;
            _selectedNode = null;
            _linkFrom = null;
            if (_nodeEditor != null) { DestroyImmediate(_nodeEditor); _nodeEditor = null; }
            Repaint();
        }

        void SelectNode(FTUENodeSO node)
        {
            _selectedNode = node;
        }

        void CreateGraph()
        {
            var path = EditorUtility.SaveFilePanelInProject("New FTUE Graph", "FTUEGraph", "asset",
                "Choose where to save the FTUE graph.");
            if (string.IsNullOrEmpty(path)) return;

            var g = ScriptableObject.CreateInstance<FTUEGraphSO>();
            g.graphId = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(g, path);
            AssetDatabase.SaveAssets();
            SelectGraph(g);
        }

        void ShowAddNodeMenu()
        {
            var menu = new GenericMenu();
            var types = TypeCache.GetTypesDerivedFrom<FTUENodeSO>()
                .Where(t => !t.IsAbstract)
                .OrderBy(t => t.Name);

            foreach (var type in types)
            {
                string label = type.Name.Replace("FTUE", string.Empty).Replace("Node", string.Empty);
                menu.AddItem(new GUIContent(label), false, () => CreateNode(type));
            }
            menu.ShowAsContext();
        }

        void CreateNode(Type type)
        {
            var node = (FTUENodeSO)ScriptableObject.CreateInstance(type);
            node.nodeId = Guid.NewGuid().ToString("N");
            node.name = type.Name.Replace("FTUE", string.Empty).Replace("Node", string.Empty);
            node.displayName = node.name;
            node.graphPosition = -_graph.canvasScroll + new Vector2(60, 60) + new Vector2(_graph.nodes.Count % 5 * 24, _graph.nodes.Count % 5 * 24);

            Undo.RegisterCreatedObjectUndo(node, "Create FTUE node");
            AssetDatabase.AddObjectToAsset(node, _graph);
            _graph.nodes.Add(node);
            if (_graph.entryNode == null)
                _graph.entryNode = node;

            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
            SelectNode(node);
        }

        void DeleteNode(FTUENodeSO node)
        {
            if (!EditorUtility.DisplayDialog("Delete Node", $"Delete '{NodeLabel(node)}'?", "Delete", "Cancel"))
                return;

            // Remove inbound edges.
            foreach (var n in _graph.nodes.Where(n => n != null))
                n.Outputs.RemoveAll(edge => edge.targetNodeId == node.nodeId);

            _graph.nodes.Remove(node);
            if (_graph.entryNode == node)
                _graph.entryNode = _graph.nodes.FirstOrDefault(n => n != null);
            if (_selectedNode == node)
                SelectNode(null);

            AssetDatabase.RemoveObjectFromAsset(node);
            DestroyImmediate(node, true);

            EditorUtility.SetDirty(_graph);
            AssetDatabase.SaveAssets();
        }

        void SetEdge(FTUENodeSO from, string port, string targetNodeId)
        {
            Undo.RecordObject(from, "Set FTUE edge");
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
                from.Outputs.Add(new FTUEEdge(port, targetNodeId));
            }
            EditorUtility.SetDirty(from);
        }

        void ShowNodeContextMenu(FTUENodeSO node)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Set As Entry Node"), _graph.entryNode == node, () =>
            {
                _graph.entryNode = node;
                EditorUtility.SetDirty(_graph);
            });
            menu.AddItem(new GUIContent("Delete Node"), false, () => DeleteNode(node));
            menu.ShowAsContext();
        }

        void SaveGraph()
        {
            if (_graph == null) return;
            EditorUtility.SetDirty(_graph);
            foreach (var n in _graph.nodes.Where(n => n != null))
                EditorUtility.SetDirty(n);
            AssetDatabase.SaveAssets();
        }

        // ── Drawing helpers ────────────────────────────────────────────

        Rect NodeRect(FTUENodeSO node) =>
            new Rect(node.graphPosition.x + _graph.canvasScroll.x, node.graphPosition.y + _graph.canvasScroll.y, NodeWidth, NodeHeight);

        Rect InputPortRect(FTUENodeSO node)
        {
            var r = NodeRect(node);
            return new Rect(r.x - PortSize * 0.5f, r.y + HeaderHeight * 0.5f - PortSize * 0.5f, PortSize, PortSize);
        }

        Vector2 InputPortCenter(FTUENodeSO node)
        {
            var r = InputPortRect(node);
            return new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
        }

        Rect OutputPortRect(FTUENodeSO node, int portIndex, int portCount)
        {
            var r = NodeRect(node);
            float span = r.height - HeaderHeight;
            float step = span / (portCount + 1);
            float y = r.y + HeaderHeight + step * (portIndex + 1) - PortSize * 0.5f;
            return new Rect(r.x + r.width - PortSize * 0.5f, y, PortSize, PortSize);
        }

        Vector2 OutputPortCenter(FTUENodeSO node, int portIndex, int portCount)
        {
            var r = OutputPortRect(node, portIndex, portCount);
            return new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
        }

        static void DrawEdge(Vector2 from, Vector2 to, Color color)
        {
            Handles.DrawBezier(from, to,
                from + Vector2.right * 50f, to + Vector2.left * 50f,
                color, null, 3f);
        }

        static void DrawBorder(Rect r, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }

        static void DrawGrid(Rect rect)
        {
            const float spacing = 20f;
            var col = new Color(1f, 1f, 1f, 0.03f);
            Handles.color = col;
            for (float x = 0; x < rect.width; x += spacing)
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, rect.height));
            for (float y = 0; y < rect.height; y += spacing)
                Handles.DrawLine(new Vector3(0, y), new Vector3(rect.width, y));
            Handles.color = Color.white;
        }

        static Color NodeColor(FTUENodeSO node)
        {
            // Stable hue from the type name.
            int h = node.GetType().Name.Aggregate(17, (acc, c) => acc * 31 + c);
            float hue = (Mathf.Abs(h) % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.5f);
        }

        static string NodeLabel(FTUENodeSO node) =>
            string.IsNullOrEmpty(node.displayName) ? node.NodeTypeLabel : $"{node.displayName} ({node.NodeTypeLabel})";
    }
}
#endif
