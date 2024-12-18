using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class TeamVolumeDifferenceScoring : BaseScoringMode
    {
        public TeamVolumeDifferenceScoring(float scoreMultiplier) : base(scoreMultiplier) { }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            var teamStats = StatsManager.Instance.TeamStats;
            var greenVolume = teamStats.TryGetValue(Teams.Jade, out var greenStats) ? greenStats.VolumeRemaining : 0f;
            var redVolume = teamStats.TryGetValue(Teams.Ruby, out var redStats) ? redStats.VolumeRemaining : 0f;

            return (greenVolume - redVolume) * ScoreMultiplier;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            var score = CalculateScore(playerName, currentScore, turnStartTime);
            StatsManager.Instance.ResetStats();
            return score;
        }
    }
}
