using CosmicShore.Utility;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using Reflex.Attributes;
using UnityEngine;
using System.Linq;

namespace CosmicShore.Core
{
    public class QuestSystem : SingletonPersistent<QuestSystem>
    {
        [SerializeField] Quest TestQuest;
        [SerializeField] Quest TestQuest2;
        [Inject] AnalyticsServiceFacade _analytics;
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
            CSDebug.LogVerbose(CSLogChannel.CloudData, $"[QuestSystem] Quest Completed - Shards to issue: {quest.ShardValue}");

            // Grant Reward
            // TODO: no reward backend. PlayerDataController (PlayFab) was deleted; the live
            // profile owner is PlayerDataService and CatalogManager no longer grants anything.
            // CatalogManager.Instance.GrantCaptainXP(quest.ShardValue, ShipTypes.Manta, Element.Space);

            // Mark Granted
            quest.RewardGranted = true;

            quest.CompleteQuest();
            _analytics?.RecordQuestCompleted(quest.Title, quest.ShardValue);

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
        }

        /// <summary>
        /// Advances every active quest whose completion action matches, and retires the ones
        /// that complete. (This summary previously described the call-to-action dismissal
        /// steps, copied from a system this no longer talks to - retired 2026-09-08, F7.)
        /// </summary>
        /// <param name="action"></param>
        void UpdateQuestProgressOnUserActionCompleted(UserAction action)
        {
            if (ActiveQuests.Count <= 0) return;
            if (!ActiveQuests.ContainsKey(action.Label)) return;

            foreach (var quest in ActiveQuests[action.Label])
            {
                if (action.ActionType == UserActionType.PlayGame)
                {
                    
                    // Analyze label and see if it matches


                    if (TestQuest.CompletionAction.Value <= action.Value)
                        CompleteQuest(TestQuest);

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