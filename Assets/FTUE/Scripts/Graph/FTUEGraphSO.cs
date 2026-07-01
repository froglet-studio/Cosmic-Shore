using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Root asset for an authored First-Time-User-Experience flow.
    ///
    /// Holds an ordered/branching set of <see cref="FTUENodeSO"/> sub-assets and the
    /// entry point the <c>FTUEGraphRunner</c> starts from. Authored visually via the
    /// FTUE Graph editor window (Tools ▸ FrogletTools ▸ FTUE), executed at runtime by
    /// the runner. Replaces the old flat <c>TutorialSequenceSet</c> list, which could
    /// not express branches (input-vs-timeout, played-vs-backed-out, CTA jumps).
    /// </summary>
    [CreateAssetMenu(fileName = "FTUEGraph", menuName = "ScriptableObjects/FTUE/FTUE Graph")]
    public class FTUEGraphSO : ScriptableObject
    {
        [Tooltip("Stable id for this graph (analytics / persistence key). Defaults to the asset name.")]
        public string graphId;

        [Tooltip("The first node executed when the FTUE starts.")]
        public FTUENodeSO entryNode;

        [Tooltip("Every node owned by this graph (stored as sub-assets).")]
        public List<FTUENodeSO> nodes = new();

        [Tooltip("Editor canvas scroll offset. Runtime-irrelevant.")]
        [HideInInspector] public Vector2 canvasScroll;

        /// <summary>Find a node by its stable id (null-safe; returns null if not found).</summary>
        public FTUENodeSO FindNode(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var n in nodes)
                if (n != null && n.nodeId == id)
                    return n;

            return null;
        }
    }
}
