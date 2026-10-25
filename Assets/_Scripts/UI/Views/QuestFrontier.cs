using CosmicShore.Core;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// Where the player stands on the quest graph: each quest's state, and the FRONTIER (the first
    /// quest that is unlocked or ready to claim). One place for it, so the Profile screen's quest track
    /// and Home's "Current quest" card cannot disagree about which quest is current.
    /// </summary>
    public static class QuestFrontier
    {
        public static QuestItemState StateOf(SO_UnlockList list, int questIndex, GameModeProgressionService service)
        {
            var quest = list.Quests[questIndex];
            bool isUnlocked = questIndex == 0 || (service != null && service.IsGameModeUnlocked(quest.GameMode));
            if (!isUnlocked) return QuestItemState.Locked;

            if (questIndex + 1 < list.Quests.Count && service != null
                && service.IsGameModeUnlocked(list.Quests[questIndex + 1].GameMode))
                return QuestItemState.Claimed;

            if (quest.IsPlaceholder)
                return QuestItemState.ReadyToClaim;

            if (service != null && service.IsQuestCompleted(quest.GameMode))
                return QuestItemState.ReadyToClaim;

            return QuestItemState.Unlocked;
        }

        /// <summary>The first Unlocked or ReadyToClaim quest; the last one when all are claimed; -1 when empty.</summary>
        public static int FrontierIndex(SO_UnlockList list, GameModeProgressionService service)
        {
            if (list == null || list.Quests == null || list.Quests.Count == 0) return -1;
            for (int i = 0; i < list.Quests.Count; i++)
            {
                var state = StateOf(list, i, service);
                if (state == QuestItemState.Unlocked || state == QuestItemState.ReadyToClaim)
                    return i;
            }
            return list.Quests.Count - 1;
        }
    }
}
