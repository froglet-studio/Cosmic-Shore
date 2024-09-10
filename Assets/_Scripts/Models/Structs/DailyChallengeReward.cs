using System;

[Serializable]
public struct DailyChallengeReward
{
    public int ScoreRequirement;
    public int Value;
    public Element Element;
    public MiniGames GameMode;
}