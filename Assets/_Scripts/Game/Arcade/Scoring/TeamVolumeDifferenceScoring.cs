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
            var goldVolume = teamStats.TryGetValue(Teams.Gold, out var goldStats) ? goldStats.VolumeRemaining : 0f;
            var difference = redVolume > goldVolume ? greenVolume - redVolume : greenVolume - goldVolume;

            return (difference) * ScoreMultiplier;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            var score = CalculateScore(playerName, currentScore, turnStartTime);
            StatsManager.Instance.ResetStats();
            return score;
        }
    }
}
