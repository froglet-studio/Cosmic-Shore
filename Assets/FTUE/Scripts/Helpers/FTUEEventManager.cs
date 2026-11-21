using System;
using CosmicShore.Core;

namespace CosmicShore.Core
{
    /// <summary>
    /// Central hub for FTUE-related events.
    /// </summary>
    public static class FTUEEventManager
    {
        /// <summary>
        /// Fired two times. Once when a user enters the game for the first time.
        /// Second, when the user starts Phase 3 of the FTUE.
        /// We can add more here in the future.
        /// </summary>
        public static event Action InitializeFTUE;
        public static void OnInitializeFTUECalled()
            => InitializeFTUE?.Invoke();

        /// <summary>
        /// Fired by the QuestGraphRunner each time a node of a persisted quest run completes -
        /// the same moment the node lands in <see cref="QuestProgressStore"/>. Carries the quest
        /// id, the phase index (0-based), the node's stable id and its authored display name.
        /// The analytics facade turns it into <c>ftue_step_completed</c>, the top of the
        /// activation funnel. A debug single-phase run raises nothing, as it persists nothing.
        /// </summary>
        public static event Action<string, int, string, string> OnQuestNodeCompleted;
        public static void RaiseQuestNodeCompleted(string questId, int phaseIndex, string nodeId, string nodeName)
            => OnQuestNodeCompleted?.Invoke(questId, phaseIndex, nodeId, nodeName);

        /// <summary>
        /// Fired by the QuestGraphRunner when a quest phase completes.
        /// Carries the quest id and the completed phase index (0-based).
        /// </summary>
        public static event Action<string, int> OnQuestPhaseCompleted;
        public static void RaiseQuestPhaseCompleted(string questId, int phaseIndex)
            => OnQuestPhaseCompleted?.Invoke(questId, phaseIndex);

        /// <summary>
        /// Fired by the QuestGraphRunner when a whole quest (e.g. the FTUE) completes.
        /// </summary>
        public static event Action<string> OnQuestCompleted;
        public static void RaiseQuestCompleted(string questId)
            => OnQuestCompleted?.Invoke(questId);

    }
}
