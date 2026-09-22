#if !LINUX_BUILD
using System.Collections.Generic;
using CosmicShore.Core;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Canonical canvas arrangement for quest phase graphs: the flow reads left→right along a
    /// ROW, and a new row starts every time the player moves between the app shell (menus,
    /// arcade, profile) and gameplay (freestyle flight, a launched match).
    ///
    /// So each row is one place the player is standing, and each row break is a real context
    /// switch — the flight-school row, the "back in the menu" row, the away-trip row for a
    /// match, and so on. Boundaries come from <see cref="QuestNodeSO.Venue"/> /
    /// <see cref="QuestNodeSO.VenueAfter"/>, which only the transition nodes declare; every
    /// other node inherits, so no row ever breaks on a beat that didn't move the player.
    ///
    /// Used by the editor window's <c>Layout Rows</c> button and by
    /// <see cref="QuestDefaultContentBuilder"/> so generated graphs land already arranged.
    /// </summary>
    public static class QuestGraphLayout
    {
        /// <summary>Left edge of every row.</summary>
        public const float OriginX = 60f;

        /// <summary>Top edge of the first row.</summary>
        public const float OriginY = 40f;

        /// <summary>Horizontal step between consecutive nodes in a row (widest card + arrow gap).</summary>
        public const float ColumnPitch = 380f;

        /// <summary>Vertical step between rows (tallest card + room for the wrap edge).</summary>
        public const float RowPitch = 220f;

        /// <summary>
        /// Re-arrange every phase graph in the project and write it to disk — the one-shot for
        /// "make all the quest tracks read as rows" after hand-editing or adding beats.
        /// </summary>
        [MenuItem("FrogletTools/Quest Graph/Layout All Phases (Rows)")]
        public static void LayoutAllPhases()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(QuestPhaseGraphSO)}");
            int graphs = 0, rows = 0;
            foreach (var guid in guids)
            {
                var graph = AssetDatabase.LoadAssetAtPath<QuestPhaseGraphSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (graph == null)
                    continue;
                rows += LayoutRows(graph);
                graphs++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Quest] Laid out {graphs} phase graph{(graphs == 1 ? "" : "s")} into {rows} venue rows.");
        }

        /// <summary>
        /// Arrange <paramref name="graph"/> into venue rows. Returns the number of rows used.
        /// Records an undo step and marks the assets dirty; the caller saves.
        /// </summary>
        public static int LayoutRows(QuestPhaseGraphSO graph)
        {
            if (graph == null || graph.nodes == null || graph.nodes.Count == 0)
                return 0;

            var order = FlowOrder(graph);

            foreach (var n in order)
                if (n != null)
                    Undo.RecordObject(n, "Layout Quest Graph Rows");

            // Two cursors: where the previous beat left the player, and the venue of the row
            // being filled. They diverge on "away trip" nodes (a match, a tier grind) — the
            // node itself is gameplay but hands the player back to the shell, so the row it
            // opens closes again behind it.
            var venue = QuestVenue.AppShell; // the player starts every phase in the shell
            var rowVenue = QuestVenue.AppShell;
            int row = 0, column = 0;

            foreach (var node in order)
            {
                if (node == null)
                    continue;

                var runsIn = node.Venue == QuestVenue.Inherit ? venue : node.Venue;
                if (column > 0 && runsIn != rowVenue)
                {
                    row++;
                    column = 0;
                }
                if (column == 0)
                    rowVenue = runsIn;

                node.graphPosition = new Vector2(OriginX + column * ColumnPitch, OriginY + row * RowPitch);
                column++;
                EditorUtility.SetDirty(node);

                venue = node.VenueAfter == QuestVenue.Inherit ? runsIn : node.VenueAfter;
            }

            EditorUtility.SetDirty(graph);
            return row + 1;
        }

        /// <summary>
        /// Execution order: depth-first from the entry node following each node's output ports
        /// in their declared order, then any nodes the entry can't reach (appended so a
        /// half-wired graph still lays out instead of leaving orphans stacked at the origin).
        /// </summary>
        public static List<QuestNodeSO> FlowOrder(QuestPhaseGraphSO graph)
        {
            var order = new List<QuestNodeSO>();
            var seen = new HashSet<QuestNodeSO>();

            var byId = new Dictionary<string, QuestNodeSO>();
            foreach (var n in graph.nodes)
                if (n != null && !string.IsNullOrEmpty(n.nodeId))
                    byId[n.nodeId] = n;

            var entry = graph.entryNode != null ? graph.entryNode
                      : graph.nodes.Count > 0 ? graph.nodes[0] : null;
            if (entry != null)
                Walk(entry, byId, order, seen);

            foreach (var n in graph.nodes)
                if (n != null && seen.Add(n))
                    order.Add(n);

            return order;
        }

        static void Walk(QuestNodeSO node, Dictionary<string, QuestNodeSO> byId,
                         List<QuestNodeSO> order, HashSet<QuestNodeSO> seen)
        {
            if (node == null || !seen.Add(node))
                return;
            order.Add(node);

            foreach (var port in node.OutputPorts)
            {
                var edge = node.EdgeForPort(port);
                if (edge != null && !string.IsNullOrEmpty(edge.targetNodeId)
                    && byId.TryGetValue(edge.targetNodeId, out var next))
                    Walk(next, byId, order, seen);
            }
        }
    }
}
#endif
