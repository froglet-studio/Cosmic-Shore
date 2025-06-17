using UnityEngine;

public enum RewardType { Item, Currency, XP, Unlock }
public enum RewardRarity { Common, Rare, Epic, Legendary }

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
