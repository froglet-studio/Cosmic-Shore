using System;

namespace CosmicShore.Core
{
    /// <summary>
    /// A directed connection from one node's named output port to another node.
    ///
    /// Edges are stored on the source node (<see cref="FTUENodeSO.Outputs"/>) and
    /// reference the target by its stable <see cref="FTUENodeSO.nodeId"/> — never by
    /// list index — so reordering or deleting sibling nodes never re-wires the graph.
    /// </summary>
    [Serializable]
    public class FTUEEdge
    {
        /// <summary>Which output port of the source node this edge leaves from (see <see cref="FTUEPorts"/>).</summary>
        public string portName = FTUEPorts.Next;

        /// <summary>The <see cref="FTUENodeSO.nodeId"/> of the destination node (empty = dead end / end of flow).</summary>
        public string targetNodeId;

        public FTUEEdge() { }

        public FTUEEdge(string portName, string targetNodeId)
        {
            this.portName = portName;
            this.targetNodeId = targetNodeId;
        }
    }
}
