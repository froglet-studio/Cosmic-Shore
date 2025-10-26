using CosmicShore.Core;
using CosmicShore.SOAP;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class HostileVolumeDestroyedScoring : BaseScoring
    {
        public HostileVolumeDestroyedScoring(GameDataSO data, float scoreMultiplier) : base(data, scoreMultiplier) { }

        float lastVolumeDestroyed;

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnVolumeDestroyedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnVolumeDestroyedChanged -= UpdateScore;
                lastVolumeDestroyed = 0;
            }
        }

        void UpdateScore(IRoundStats roundStats)
        {
            var newVolumeDestroyed = roundStats.VolumeDestroyed - lastVolumeDestroyed;
            roundStats.Score = newVolumeDestroyed * scoreMultiplier;
        }
    }
}
