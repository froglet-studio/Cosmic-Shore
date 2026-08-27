using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// One PHASE of a quest, authored as a node graph.
    ///
    /// Holds an ordered/branching set of <see cref="QuestNodeSO"/> sub-assets and the
    /// entry point the <c>QuestGraphRunner</c> starts from. Phases are sequenced by a
    /// <see cref="QuestSO"/>; a <c>QuestPhaseEndNode</c> inside the graph hands control
    /// to the next phase. Authored visually in FrogletTools ▸ Quest Graph Editor.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestPhase", menuName = "ScriptableObjects/Quests/Quest Phase Graph")]
    public class QuestPhaseGraphSO : ScriptableObject
    {
        [Tooltip("Stable id for this graph (analytics / persistence key). Defaults to the asset name.")]
        public string graphId;

        [Tooltip("Display name of the phase (e.g. 'Onboarding & Crystal Capture'). Shown in the editor's phase list.")]
        public string phaseName;

        [Tooltip("When off, the runner skips this phase entirely (test harness).")]
        public bool phaseEnabled = true;

        [Tooltip("Designer notes: what this phase teaches/unlocks, its entry gate, anything a teammate needs to know.")]
        [TextArea(3, 10)] public string designerNotes;

        [Tooltip("The first node executed when this phase starts.")]
        public QuestNodeSO entryNode;

        [Tooltip("Every node owned by this graph (stored as sub-assets).")]
        public List<QuestNodeSO> nodes = new();

        [Tooltip("Editor canvas scroll offset. Runtime-irrelevant.")]
        [HideInInspector] public Vector2 canvasScroll;

        [Tooltip("Editor canvas zoom. Runtime-irrelevant.")]
        [HideInInspector] public float canvasZoom = 1f;

        /// <summary>Display name with fallback to the asset name.</summary>
        public string PhaseName => string.IsNullOrEmpty(phaseName) ? name : phaseName;

        /// <summary>Find a node by its stable id (null-safe; returns null if not found).</summary>
        public QuestNodeSO FindNode(string id)
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
