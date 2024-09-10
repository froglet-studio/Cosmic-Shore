using System;

[Serializable]
public struct GameplayReward
{
    public int ScoreRequirement;
    public int Value;
    public Element Element;
    public MiniGames GameMode;
}