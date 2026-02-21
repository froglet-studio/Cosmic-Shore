using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class TeamVolumeDifferenceScoring : BaseScoring
    {
        public TeamVolumeDifferenceScoring(IScoreTracker tracker, GameDataSO scoreData, float scoreMultiplier) : base(tracker, scoreData, scoreMultiplier) { }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnVolumeRemainingChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnVolumeRemainingChanged -= UpdateScore;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            var sorted = GameData.GetSortedListInDecendingOrderBasedOnVolumeRemaining();
            if (sorted == null || sorted.Count == 0) return;

            float minVol = sorted[^1].VolumeRemaining;
            float rel = Mathf.Max(0f, roundStats.VolumeRemaining - minVol);
            Score = rel * scoreMultiplier;
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}
