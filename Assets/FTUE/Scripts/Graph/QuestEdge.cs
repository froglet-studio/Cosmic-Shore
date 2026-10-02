using System;

namespace CosmicShore.Core
{
    /// <summary>
    /// A directed connection from one node's named output port to another node.
    ///
    /// Edges are stored on the source node (<see cref="QuestNodeSO.Outputs"/>) and
    /// reference the target by its stable <see cref="QuestNodeSO.nodeId"/> — never by
    /// list index — so reordering or deleting sibling nodes never re-wires the graph.
    /// </summary>
    [Serializable]
    public class QuestEdge
    {
        /// <summary>Which output port of the source node this edge leaves from (see <see cref="QuestPorts"/>).</summary>
        public string portName = QuestPorts.Next;

        /// <summary>The <see cref="QuestNodeSO.nodeId"/> of the destination node (empty = dead end / end of flow).</summary>
        public string targetNodeId;

        /// <summary>Real-time seconds to wait before the target node runs (pacing between beats).</summary>
        [UnityEngine.Min(0f)] public float delaySeconds;

        public QuestEdge() { }

        public QuestEdge(string portName, string targetNodeId)
        {
            this.portName = portName;
            this.targetNodeId = targetNodeId;
        }
    }
}
