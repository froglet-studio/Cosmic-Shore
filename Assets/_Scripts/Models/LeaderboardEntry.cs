[System.Serializable]
public struct LeaderboardEntry
{
    public string PlayerName;
    public int Score;
    public ShipClassType ShipType;

    public LeaderboardEntry(string playerName, int score, ShipClassType shipType)
    {
        PlayerName = playerName;
        Score = score;
        ShipType = shipType;
    }
}