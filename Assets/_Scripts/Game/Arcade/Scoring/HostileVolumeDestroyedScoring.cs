using CosmicShore.Core;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class HostileVolumeDestroyedScoring : BaseScoring
    {
        float lastVolumeDestroyed;
        
        public HostileVolumeDestroyedScoring(GameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnHostileVolumeDestroyedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnHostileVolumeDestroyedChanged -= UpdateScore;
                lastVolumeDestroyed = 0;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            var volumeDelta = roundStats.HostileVolumeDestroyed - lastVolumeDestroyed;
            roundStats.Score += volumeDelta * scoreMultiplier;
            lastVolumeDestroyed = roundStats.HostileVolumeDestroyed;
            
        }
    }
}