using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Base ScriptableObject for every node in an FTUE graph.
    ///
    /// Each concrete node is its own SO subclass carrying typed authoring fields and
    /// its own <see cref="Execute"/> behaviour — a polymorphic-SO design (mirrors the
    /// project's Effect / Scoring-rule SOs) that replaces the old flat
    /// <c>TutorialStepType</c> enum + payload switch. Nodes are stored as sub-assets of
    /// the owning <see cref="FTUEGraphSO"/>.
    ///
    /// A node is asset config only — it must hold NO per-run mutable state. All runtime
    /// state lives in the coroutine's locals and the passed-in <see cref="FTUERuntimeContext"/>,
    /// so the same asset can be re-run safely (e.g. FTUE replay from a debug menu).
    /// </summary>
    public abstract class FTUENodeSO : ScriptableObject
    {
        [Tooltip("Stable identifier used to wire edges. Assigned once on creation — never reuse or hand-edit.")]
        [HideInInspector] public string nodeId;

        [Tooltip("Human-readable label shown on the node in the graph editor (authoring aid only).")]
        public string displayName;

        [Tooltip("Node position on the editor canvas. Runtime-irrelevant.")]
        [HideInInspector] public Vector2 graphPosition;

        [Tooltip("Outgoing edges keyed by port name. Managed by the graph editor's port drag, not hand-edited.")]
        [HideInInspector] [SerializeField] private List<FTUEEdge> outputs = new();

        /// <summary>Outgoing edges of this node.</summary>
        public List<FTUEEdge> Outputs => outputs;

        /// <summary>Short type label for the editor header (e.g. "EnterFreestyle").</summary>
        public virtual string NodeTypeLabel => GetType().Name.Replace("FTUE", string.Empty).Replace("Node", string.Empty);

        /// <summary>
        /// The output ports this node can advance through. Linear nodes emit only
        /// <see cref="FTUEPorts.Next"/>; override to expose branch ports.
        /// </summary>
        public virtual IReadOnlyList<string> OutputPorts => FTUEPorts.NextOnly;

        /// <summary>
        /// Run this node. Do the node's work (drive a runtime system, wait for a signal),
        /// then call <paramref name="advance"/> exactly once with the chosen output port
        /// (usually <see cref="FTUEPorts.Next"/>). The coroutine ending does NOT advance —
        /// only calling <paramref name="advance"/> does — so event-driven nodes may subscribe,
        /// register cleanup via <see cref="FTUERuntimeContext.AddCleanup"/>, and yield break.
        /// </summary>
        public abstract IEnumerator Execute(FTUERuntimeContext ctx, System.Action<string> advance);

        /// <summary>
        /// Author-time validation. Append human-readable problems to <paramref name="errors"/>
        /// (missing references, unreachable targets). Called by the graph editor, never at runtime.
        /// </summary>
        public virtual void Validate(FTUEGraphSO graph, List<string> errors) { }

        /// <summary>Resolve the target node for a given output port, or null for a dead end.</summary>
        public FTUEEdge EdgeForPort(string port)
        {
            foreach (var e in outputs)
                if (e != null && e.portName == port)
                    return e;
            return null;
        }
    }
}
