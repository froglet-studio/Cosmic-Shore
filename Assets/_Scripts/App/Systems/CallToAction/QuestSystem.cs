using StarWriter.Utility.Singleton;
using UnityEngine;

public class QuestSystem : SingletonPersistent<QuestSystem>
{
    [SerializeField] Quest TestQuest;

    void Start()
    {
        UserActionMonitor.Instance.OnUserActionCompleted += UpdateQuestProgressOnUserActionCompleted;
        if (TestQuest != null)
            CallToActionSystem.Instance.AddCallToAction(TestQuest.CallToAction);
    }

    public void CompleteQuest(Quest quest)
    {
        Debug.LogWarning($"{nameof(QuestSystem)}.{nameof(CompleteQuest)} - Quest Completed - Shards to issue: {quest.ShardValue}");
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
        if (TestQuest == null) return;

        if (action.ActionType == TestQuest.CompletionAction.ActionType)
        {
            if (action.ActionType == UserActionType.PlayGame)
            {
                if (TestQuest.CompletionAction.Value <= action.Value)
                    CompleteQuest(TestQuest);
                else
                    Debug.LogWarning($"Score not high enough: {action.Value}");

            }
            else
            {
                CompleteQuest(TestQuest);
            }
        }
    }
}