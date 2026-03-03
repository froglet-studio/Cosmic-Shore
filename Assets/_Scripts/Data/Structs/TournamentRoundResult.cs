using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    [Serializable]
    public struct TournamentPlayerScore
    {
        public string PlayerName;
        public Domains Domain;
        public float RawScore;
        public int Placement;
        public int PointsAwarded;
    }

    [Serializable]
    public struct TournamentRoundResult
    {
        public int RoundIndex;
        public GameModes GameMode;
        public string GameDisplayName;
        public List<TournamentPlayerScore> PlayerScores;
    }

    [Serializable]
    public struct TournamentStanding
    {
        public string PlayerName;
        public Domains Domain;
        public int TotalPoints;
        public List<int> PointsPerRound;
    }
}
