using System;
using System.Collections.Generic;

public class LeaderboardDataAccessor
{
    static readonly string SaveFileName = "Leaderboard.data";

    public static Dictionary<MiniGames, List<LeaderboardEntry>> Leaderboard;

    public static void Save(MiniGames mode, List<LeaderboardEntry> leaderboard)
    {
        if (Leaderboard == null)
            Load();

        if (Leaderboard.ContainsKey(mode))
            Leaderboard[mode] = leaderboard;
        else
            Leaderboard.Add(mode, leaderboard);

        DataAccessor dataAccessor = new DataAccessor(SaveFileName);
        dataAccessor.Save(Leaderboard);
    }

    public static Dictionary<MiniGames, List<LeaderboardEntry>> Load()
    {
        DataAccessor dataAccessor = new DataAccessor(SaveFileName);
        Leaderboard = dataAccessor.Load<Dictionary<MiniGames, List<LeaderboardEntry>>>();
        return Leaderboard;
    }
}

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