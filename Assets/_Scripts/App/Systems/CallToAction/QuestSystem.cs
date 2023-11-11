using CosmicShore._Core.Playfab_Models.Economy;
using CosmicShore.Utility.Singleton;
using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestSystem : SingletonPersistent<QuestSystem>
{
    [SerializeField] Quest TestQuest;
    [SerializeField] Quest TestQuest2;
    Dictionary<UserAction, List<Quest>> ActiveQuests = new();
    List<Quest> CompletedQuests;

    void Start()
    {
        UserActionMonitor.Instance.OnUserActionCompleted += UpdateQuestProgressOnUserActionCompleted;
        
        if (TestQuest != null)
            AddQuest(TestQuest);
        if (TestQuest2 != null)
            AddQuest(TestQuest2);
    }

    public void CompleteQuest(Quest quest)
    {
        Debug.LogWarning($"{nameof(QuestSystem)}.{nameof(CompleteQuest)} - Quest Completed - Shards to issue: {quest.ShardValue}");

        // Grant Reward
        CatalogManager.Instance.GrantShards(quest.ShardValue, ShipTypes.Manta, Element.Space);

        // Mark Granted
        quest.RewardGranted = true;
        quest.Completed = true;
        quest.TimeCompleted = DateTime.Now;

        RemoveQuest(quest);
        CompletedQuests.Add(quest);
    }

    public void RemoveQuest(Quest quest)
    {
        if (!ActiveQuests.ContainsKey(quest.CompletionAction))
            return;

        ActiveQuests[quest.CompletionAction].Remove(quest);

        if (ActiveQuests[quest.CompletionAction].Count == 0)
            ActiveQuests.Remove(quest.CompletionAction);
    }

    public void AddQuest(Quest quest)
    {
        if (ActiveQuests.ContainsKey(quest.CompletionAction))
            ActiveQuests[quest.CompletionAction].Add(quest);
        else
            ActiveQuests.Add(quest.CompletionAction, new List<Quest>() { quest });

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
        Debug.LogWarning($"{nameof(UpdateQuestProgressOnUserActionCompleted)}: {action.ActionType}");

        if (ActiveQuests.Count <= 0) return;
        if (!ActiveQuests.ContainsKey(action)) return;

        foreach (var quest in ActiveQuests[action])
        {
            if (action.ActionType == UserActionType.PlayGame)
            {
                // Analyze label and see if it matches


                if (TestQuest.CompletionAction.Value <= action.Value)
                    CompleteQuest(TestQuest);
                else
                    Debug.LogWarning($"Score not high enough: {action.Value}");


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