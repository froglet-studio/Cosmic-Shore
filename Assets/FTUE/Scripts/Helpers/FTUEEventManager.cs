using System;
using CosmicShore.Core;
using CosmicShore.Data;

namespace CosmicShore.Core
{
    /// <summary>
    /// Central hub for FTUE-related events.
    /// </summary>
    public static class FTUEEventManager
    {
        /// <summary>
        /// Fired when a Call-To-Action card is selected in the arcade menu.
        /// Carries the target ID so subscribers can react accordingly.
        /// </summary>
        public static event Action<CallToActionTargetType> OnCTAClicked;
        public static void RaiseCTAClicked(CallToActionTargetType id)
            => OnCTAClicked?.Invoke(id);

        /// <summary>
        /// Fired two times. Once when a user enters the game for the first time.
        /// Second, when the user starts Phase 3 of the FTUE.
        /// We can add more here in the future.
        /// </summary>
        public static event Action InitializeFTUE;
        public static void OnInitializeFTUECalled()
            => InitializeFTUE?.Invoke();

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
