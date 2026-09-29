using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// A quest: an ordered sequence of phase graphs (<see cref="QuestPhaseGraphSO"/>).
    ///
    /// The Main Quest (the FTUE) is one of these — phase 0 onboarding, then one phase per
    /// game-mode unlock, through Maelstrom, vessels, and the episodes finale. The
    /// <c>QuestGraphRunner</c> executes phases in order; a <c>QuestPhaseEndNode</c> inside
    /// a phase advances to the next, and a <c>QuestEndNode</c> completes the whole quest.
    /// Player progress through every node is persisted to UGS keyed by <see cref="QuestId"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Quest", menuName = "ScriptableObjects/Quests/Quest (Phase Sequence)")]
    public class QuestSO : ScriptableObject
    {
        [Tooltip("Stable id used as the persistence/analytics key. Defaults to the asset name. Never change after shipping.")]
        public string questId;

        [Tooltip("Master switch (test harness). When off, the runner never starts this quest.")]
        public bool questEnabled = true;

        [Tooltip("Designer notes for the whole quest — the progression plan, assumptions, links.")]
        [TextArea(3, 12)] public string designerNotes;

        [Tooltip("Ordered phase graphs. Index 0 runs first; a Phase End node advances to the next.")]
        public List<QuestPhaseGraphSO> phases = new();

        /// <summary>Persistence/analytics key with fallback to the asset name.</summary>
        public string QuestId => string.IsNullOrEmpty(questId) ? name : questId;
    }
}
