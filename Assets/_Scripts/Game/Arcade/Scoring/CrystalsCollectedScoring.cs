using CosmicShore.Core;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class CrystalsCollectedScoring : BaseScoring
    {
        readonly CrystalType crystalType;
        bool scaleWithSize;

        public enum CrystalType
        {
            All,
            Omni,
            Elemental
        }

        public CrystalsCollectedScoring(IScoreTracker tracker, GameDataSO scoreData, float scoreMultiplier = 145.65f, CrystalType type = CrystalType.All, bool ScaleWithSize = false) 
            : base(tracker, scoreData, scoreMultiplier)
        {
            crystalType = type;
            scaleWithSize = ScaleWithSize;
        }

        public override void Subscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnCrystalsCollectedChanged += UpdateScore;
            }
        }

        public override void Unsubscribe()
        {
            foreach (var playerScore in GameData.RoundStatsList)
            {
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    return;

                roundStats.OnCrystalsCollectedChanged -= UpdateScore;
            }
        }
        
        void UpdateScore(IRoundStats roundStats)
        {
            /*float scoreIncrement = crystalType switch
            {
                CrystalType.All => roundStats.CrystalsCollected,
                CrystalType.Omni => roundStats.OmniCrystalsCollected,
                CrystalType.Elemental => scaleWithSize ? roundStats.MassCrystalValue +
                                                         roundStats.ChargeCrystalValue +
                                                         roundStats.TimeCrystalValue + 
                                                         roundStats.SpaceCrystalValue: roundStats.ElementalCrystalsCollected,
                _ => 0
            };*/
            
            Score = roundStats.CrystalsCollected * scoreMultiplier;
            ScoreTracker.CalculateTotalScore(roundStats.Name);
        }
    }
}
