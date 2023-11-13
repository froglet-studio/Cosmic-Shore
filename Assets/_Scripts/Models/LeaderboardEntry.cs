[System.Serializable]
public struct LeaderboardEntry
{
    public string PlayerName;
    public int Score;
    public ShipTypes ShipType;

    public LeaderboardEntry(string playerName, int score, ShipTypes shipType)
    {
        PlayerName = playerName;
        Score = score;
        ShipType = shipType;
    }
}