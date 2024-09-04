using CosmicShore.App.Systems.CTA;
using CosmicShore.App.Systems.UserActions;
using CosmicShore.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.Quests
{
    public class QuestSystem : SingletonPersistent<QuestSystem>
    {
        [SerializeField] Quest TestQuest;
        [SerializeField] Quest TestQuest2;
        Dictionary<string, List<Quest>> ActiveQuests = new();
        List<Quest> CompletedQuests = new();

        void Start()
        {
            UserActionSystem.Instance.OnUserActionCompleted += UpdateQuestProgressOnUserActionCompleted;

            if (TestQuest != null)
                AddQuest(TestQuest);
            if (TestQuest2 != null)
                AddQuest(TestQuest2);
        }

        public void CompleteQuest(Quest quest)
        {
            Debug.Log($"{nameof(QuestSystem)}.{nameof(CompleteQuest)} - Quest Completed - Shards to issue: {quest.ShardValue}");

            // Grant Reward
            // TODO: Look for PlayerDataController
            // CatalogManager.Instance.GrantCaptainXP(quest.ShardValue, ShipTypes.Manta, Element.Space);

            // Mark Granted
            quest.RewardGranted = true;

            quest.CompleteQuest();

            RemoveQuest(quest);
            CompletedQuests.Add(quest);
        }

        public void RemoveQuest(Quest quest)
        {
            if (!ActiveQuests.ContainsKey(quest.CompletionAction.Label))
                return;

            ActiveQuests[quest.CompletionAction.Label].Remove(quest);

            if (ActiveQuests[quest.CompletionAction.Label].Count == 0)
                ActiveQuests.Remove(quest.CompletionAction.Label);
        }

        public void AddQuest(Quest quest)
        {
            if (ActiveQuests.ContainsKey(quest.CompletionAction.Label))
                ActiveQuests[quest.CompletionAction.Label].Add(quest);
            else
                ActiveQuests.Add(quest.CompletionAction.Label, new List<Quest>() { quest });

            CallToActionSystem.Instance.AddCallToAction(quest.CallToAction);
        }

        /// <summary>
        /// 1) Notify all CallToAction targets listening for this action to dismiss their indicators & remove from ActiveTargets list.
        /// 2) Decrement Dependency Counter if necessary and Notify DependencyTarget if counter reaches zero
        /// 3) Remove from ActiveCallsToAction
        /// </summary>
        /// <param name="action"></param>
        void UpdateQuestProgressOnUserActionCompleted(UserAction action)
        {
            Debug.Log($"{nameof(UpdateQuestProgressOnUserActionCompleted)}: {action.ActionType}");
            Debug.Log($"ActiveQuests.Count:{ActiveQuests.Count}, ActiveQuests.ContainsKey(action): {ActiveQuests.ContainsKey(action.Label)}");
            foreach (var quest in ActiveQuests)
                Debug.Log($"ActiveQuests.Key: {quest.Key}");

            if (ActiveQuests.Count <= 0) return;
            if (!ActiveQuests.ContainsKey(action.Label)) return;

            foreach (var quest in ActiveQuests[action.Label])
            {
                if (action.ActionType == UserActionType.PlayGame)
                {
                    
                    // Analyze label and see if it matches


                    if (TestQuest.CompletionAction.Value <= action.Value)
                        CompleteQuest(TestQuest);
                    else
                        Debug.Log($"Score not high enough: {action.Value}");

                }
                else
                {
                    quest.EventsCompleted++;
                    if (quest.EventsCompleted >= quest.ActionCount)
                    {
                        CompleteQuest(quest);
                    }
                }
            }
        }
    }
}