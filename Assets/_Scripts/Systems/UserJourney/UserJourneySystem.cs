using CosmicShore.Models.ScriptableObjects;
using CosmicShore.Utility;
using System.Collections.Generic;
using CosmicShore.Systems.Quest;
using UnityEngine;
using CosmicShore.Utility.Recording;

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