using CosmicShore.App.Systems.Quests;
using CosmicShore.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.UserJourney
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
                Debug.Log($"quest.OnQuestCompleted.length: {quest.GetInvocationCount()}");
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