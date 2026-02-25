using CosmicShore.Utilities;
using System.Collections.Generic;
using CosmicShore.Systems.Quests;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Systems.UserJourney
{
    public class UserJourneySystem : SingletonPersistent<UserJourneySystem>
    {
        [SerializeField] SO_QuestChain questChain;
        List<Quest> quests;
        int activeQuestIndex = 0;

        void Start()
        {
            quests = questChain.Quests;

            foreach (var quest in quests)
            {
                CSDebug.Log($"quest.OnQuestCompleted.length: {quest.GetInvocationCount()}");
                quest.OnQuestCompleted += CompleteQuest;
            }
        }

        void CompleteQuest()
        {
            activeQuestIndex++;

            if (activeQuestIndex < quests.Count)
                QuestSystem.Instance.AddQuest(quests[activeQuestIndex]);
        }
    }
}