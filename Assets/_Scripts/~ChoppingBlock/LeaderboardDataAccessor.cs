using System.Collections.Generic;

namespace _Scripts._Core.Game.DataPersistence
{
    public class LeaderboardDataAccessor
    {
        static readonly string SaveFileName = "Leaderboard.data";

        static Dictionary<MiniGames, List<LeaderboardEntry>> Leaderboard;

        public static void Save(MiniGames mode, List<LeaderboardEntry> leaderboard)
        {
            Leaderboard ??= Load();

            Leaderboard[mode] = leaderboard;

            DataAccessor dataAccessor = new(SaveFileName);
            dataAccessor.Save(Leaderboard);
        }

        static Dictionary<MiniGames, List<LeaderboardEntry>> Load()
        {
            DataAccessor dataAccessor = new(SaveFileName);
            return dataAccessor.Load<Dictionary<MiniGames, List<LeaderboardEntry>>>();
            // This is not used anywhere else, comment out for now
            // return Leaderboard;
        }

        public static Dictionary<MiniGames, List<LeaderboardEntry>> LeaderboardEntriesDefault = new()
        {
            {
                MiniGames.Darts, new List<LeaderboardEntry>()
                {
                    new LeaderboardEntry("Siren",   10, ShipTypes.Manta),
                    new LeaderboardEntry("Fenrys",  20, ShipTypes.Dolphin),
                    new LeaderboardEntry("Gradies", 30, ShipTypes.Dolphin),
                    new LeaderboardEntry("Igarus",  40, ShipTypes.Rhino),
                    new LeaderboardEntry("Spades",  50, ShipTypes.Rhino)
                }
            },
            {
                MiniGames.Rampage, new List<LeaderboardEntry>()
                {
                    new LeaderboardEntry("Gradies", 10, ShipTypes.Dolphin),
                    new LeaderboardEntry("Spades",  20, ShipTypes.Rhino),
                    new LeaderboardEntry("Fenrys",  30, ShipTypes.Rhino),
                    new LeaderboardEntry("Igarus",  40, ShipTypes.Manta),
                    new LeaderboardEntry("Siren",   50, ShipTypes.Manta)
                }
            },
            {
                MiniGames.Elimination, new List<LeaderboardEntry>()
                {
                    new LeaderboardEntry("Spades",  10, ShipTypes.Dolphin),
                    new LeaderboardEntry("Siren",   20, ShipTypes.Urchin),
                    new LeaderboardEntry("Gradies", 30, ShipTypes.Manta),
                    new LeaderboardEntry("Fenrys",  40, ShipTypes.Rhino),
                    new LeaderboardEntry("Igarus",  50, ShipTypes.Rhino),
                }
            },
            {
                MiniGames.ShootingGallery, new List<LeaderboardEntry>()
                {
                    new LeaderboardEntry("Spades",  10, ShipTypes.Dolphin),
                    new LeaderboardEntry("Siren",   20, ShipTypes.Urchin),
                    new LeaderboardEntry("Igarus",  30, ShipTypes.Manta),
                    new LeaderboardEntry("Fenrys",  40, ShipTypes.Rhino),
                    new LeaderboardEntry("Gradies", 50, ShipTypes.Rhino),
                }
            },
            {
                MiniGames.DriftCourse, new List<LeaderboardEntry>()
                {
                    new LeaderboardEntry("Igarus",  10, ShipTypes.Dolphin),
                    new LeaderboardEntry("Siren",   20, ShipTypes.Urchin),
                    new LeaderboardEntry("Spades",  30, ShipTypes.Manta),
                    new LeaderboardEntry("Gradies", 40, ShipTypes.Rhino),
                    new LeaderboardEntry("Fenrys",  50, ShipTypes.Rhino),
                }
            },
            {
                MiniGames.BlockBandit, new List<LeaderboardEntry>()
                {
                    new LeaderboardEntry("Igarus",  10, ShipTypes.Dolphin),
                    new LeaderboardEntry("Gradies", 20, ShipTypes.Urchin),
                    new LeaderboardEntry("Spades",  30, ShipTypes.Manta),
                    new LeaderboardEntry("Siren",   40, ShipTypes.Rhino),
                    new LeaderboardEntry("Fenrys",  50, ShipTypes.Rhino),
                }
            },
            {
                MiniGames.CellularDuel, new List<LeaderboardEntry>()
                {
                    new LeaderboardEntry("Igarus",  10, ShipTypes.Dolphin),
                    new LeaderboardEntry("Gradies", 20, ShipTypes.Urchin),
                    new LeaderboardEntry("Spades",  30, ShipTypes.Manta),
                    new LeaderboardEntry("Siren",   40, ShipTypes.Rhino),
                    new LeaderboardEntry("Fenrys",  50, ShipTypes.Rhino),
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
}