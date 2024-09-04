using System;

[Serializable]
public struct DailyChallengeRewardState
{
    public bool RewardTierOneSatisfied;
    public bool RewardTierTwoSatisfied;
    public bool RewardTierThreeSatisfied;
    public bool RewardTierOneClaimed;
    public bool RewardTierTwoClaimed;
    public bool RewardTierThreeClaimed;
    public int HighScore;
}