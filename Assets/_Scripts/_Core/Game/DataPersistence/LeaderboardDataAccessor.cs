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

        DataAccessor dataAccessor = new(SaveFileName);
        dataAccessor.Save(Leaderboard);
    }

    public static Dictionary<MiniGames, List<LeaderboardEntry>> Load()
    {
        DataAccessor dataAccessor = new(SaveFileName);
        Leaderboard = dataAccessor.Load<Dictionary<MiniGames, List<LeaderboardEntry>>>();
        return Leaderboard;
    }

    public static Dictionary<MiniGames, List<LeaderboardEntry>> LeaderboardEntriesDefault = new()
    {
        {
            MiniGames.Darts, new List<LeaderboardEntry>()
            {
                new LeaderboardEntry("Siren",   10, ShipTypes.Manta),
                new LeaderboardEntry("Fenrys",  20, ShipTypes.Dolphin),
                new LeaderboardEntry("Gradies", 30, ShipTypes.Dolphin),
                new LeaderboardEntry("Igarus",  40, ShipTypes.Shark),
                new LeaderboardEntry("Spades",  50, ShipTypes.Shark)
            }
        },
        {
            MiniGames.Rampage, new List<LeaderboardEntry>()
            {
                new LeaderboardEntry("Gradies", 10, ShipTypes.Dolphin),
                new LeaderboardEntry("Spades",  20, ShipTypes.Shark),
                new LeaderboardEntry("Fenrys",  30, ShipTypes.Shark),
                new LeaderboardEntry("Igarus",  40, ShipTypes.Manta),
                new LeaderboardEntry("Siren",   50, ShipTypes.Manta)
            }
        },
        {
            MiniGames.Elimination, new List<LeaderboardEntry>()
            {
                new LeaderboardEntry("Spades",  10, ShipTypes.Dolphin),
                new LeaderboardEntry("Siren",   20, ShipTypes.GunManta),
                new LeaderboardEntry("Gradies", 30, ShipTypes.Manta),
                new LeaderboardEntry("Fenrys",  40, ShipTypes.Shark),
                new LeaderboardEntry("Igarus",  50, ShipTypes.Shark),
            }
        },
        {
            MiniGames.ShootingGallery, new List<LeaderboardEntry>()
            {
                new LeaderboardEntry("Spades",  10, ShipTypes.Dolphin),
                new LeaderboardEntry("Siren",   20, ShipTypes.GunManta),
                new LeaderboardEntry("Igarus",  30, ShipTypes.Manta),
                new LeaderboardEntry("Fenrys",  40, ShipTypes.Shark),
                new LeaderboardEntry("Gradies", 50, ShipTypes.Shark),
            }
        },
        {
            MiniGames.DriftCourse, new List<LeaderboardEntry>()
            {
                new LeaderboardEntry("Igarus",  10, ShipTypes.Dolphin),
                new LeaderboardEntry("Siren",   20, ShipTypes.GunManta),
                new LeaderboardEntry("Spades",  30, ShipTypes.Manta),
                new LeaderboardEntry("Gradies", 40, ShipTypes.Shark),
                new LeaderboardEntry("Fenrys",  50, ShipTypes.Shark),
            }
        },
        {
            MiniGames.BlockBandit, new List<LeaderboardEntry>()
            {
                new LeaderboardEntry("Igarus",  10, ShipTypes.Dolphin),
                new LeaderboardEntry("Gradies", 20, ShipTypes.GunManta),
                new LeaderboardEntry("Spades",  30, ShipTypes.Manta),
                new LeaderboardEntry("Siren",   40, ShipTypes.Shark),
                new LeaderboardEntry("Fenrys",  50, ShipTypes.Shark),
            }
        },
        {
            MiniGames.CellularDuel, new List<LeaderboardEntry>()
            {
                new LeaderboardEntry("Igarus",  10, ShipTypes.Dolphin),
                new LeaderboardEntry("Gradies", 20, ShipTypes.GunManta),
                new LeaderboardEntry("Spades",  30, ShipTypes.Manta),
                new LeaderboardEntry("Siren",   40, ShipTypes.Shark),
                new LeaderboardEntry("Fenrys",  50, ShipTypes.Shark),
            }
        },
    };
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