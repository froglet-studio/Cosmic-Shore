using CosmicShore.App.Systems.UserActions;
using CosmicShore.Integrations.PlayFab.Economy;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.Systems.Quests
{
    [Serializable]
    public class Quest
    {
        //public QuestKey Key;
        public string Title;
        public string Description;
        public int ShardValue;
        public CTA.CallToAction CallToAction;

        /* Satisfaction Requirements */
        public UserAction CompletionAction;
        public int ActionCount = 1;
        public float ActionQuantity = 0;
        public bool MultiSessionTracking;
        public string Scope;
        public DateTime Expiration;

        /* Rewards */
        [Tooltip("Playfab Item ID to be granted when the Quest is completed")]
        public string RewardItemID;
        public VirtualItem UnlockedItem;
        public List<(Element, int)> CrystalRewards;

        /* Progress Tracking */
        [HideInInspector] public int EventsCompleted = 0;
        [HideInInspector] public bool Completed;
        [HideInInspector] public bool RewardGranted;
        [HideInInspector] public DateTime TimeCompleted;

        public Action OnQuestCompleted;

        public int GetInvocationCount()
        {
            return (int) OnQuestCompleted?.GetInvocationList().Length;
        }
        public void CompleteQuest()
        {
            Completed = true;
            TimeCompleted = DateTime.Now;
            OnQuestCompleted?.Invoke();
        }

        //[NonSerialized] public Sprite Icon; //---- Sprite is not a serializable type
        /*public Quest(QuestScriptableObject questScriptableObject)
        {
            this.Title = questScriptableObject.Title;
            this.Description = questScriptableObject.Description;
            this.Icon = questScriptableObject.Icon;
            this.EarnedCoins = questScriptableObject.EarnedCoins;
            this.UnlockedItem = questScriptableObject.UnlockedItem;
            this.UnlockedLevel = questScriptableObject.UnlockedLevel;
            this.EventsNeeded = questScriptableObject.EventsNeeded;
            this.MultiSessionTracking = questScriptableObject.MultiSessionTracking;
            this.Key = questScriptableObject.Key;
            this.EventsCompleted = questScriptableObject.EventsCompleted;
            this.Completed = questScriptableObject.Completed;
            this.RewardGranted = questScriptableObject.RewardGranted;
            this.TimeCompleted = questScriptableObject.TimeCompleted;
        }*/
    }
}