using UnityEngine;
using System;

namespace CosmicShore.Core
{
    public enum RewardType { Item = 0, Currency = 1, Unlock = 2 }
    public enum RewardRarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }

    [System.Serializable]
    public class RewardData
    {
        public RewardType rewardType;
        public string rewardValue;
        public Sprite rewardImage;
        public string description;
        public RewardRarity rarity;
        public string condition;
        public string unlockTrigger; // or a Unity Object reference
        public string customScript; // callback or script name
    }
}
