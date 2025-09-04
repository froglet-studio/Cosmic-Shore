using CosmicShore.Core;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class HostileVolumeDestroyedScoring : BaseScoring
    {
        public HostileVolumeDestroyedScoring(MiniGameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        public override void CalculateScore()
        {
            foreach (var playerScore in miniGameData.RoundStatsList)
            {
                if (!miniGameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                {
                    Debug.LogError($"Didn't find RoundStats for player: {playerScore.Name}");
                    return;
                }
                
                playerScore.Score += roundStats.HostileVolumeDestroyed * scoreMultiplier;
            }
        }
        
        /*public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (StatsManager.Instance.PlayerStats.TryGetValue(playerName, out var roundStats))
            {
                float score = roundStats.HostileVolumeDestroyed * scoreMultiplier;
                return currentScore + score;
            }
            return currentScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            var score = CalculateScore(playerName, currentScore, turnStartTime);
            StatsManager.Instance.ResetStats();
            return score;
        }*/
    }
}
