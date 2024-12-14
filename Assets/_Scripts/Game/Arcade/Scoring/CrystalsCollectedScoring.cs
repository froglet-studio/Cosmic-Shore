using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class CrystalsCollectedScoring : BaseScoringMode
    {

        readonly CrystalType crystalType;
        bool scaleWithSize;

        public enum CrystalType
        {
            All,
            Omni,
            Elemental
        }

        public CrystalsCollectedScoring(CrystalType type = CrystalType.All, float scoreNormalizationQuotient = 145.65f, bool ScaleWithSize = false) 
            : base(scoreNormalizationQuotient)
        {
            crystalType = type;
            scaleWithSize = ScaleWithSize;
        }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (StatsManager.Instance.PlayerStats.TryGetValue(playerName, out var roundStats))
            {
                float scoreIncrement = crystalType switch
                {
                    CrystalType.All => roundStats.CrystalsCollected,
                    CrystalType.Omni => roundStats.OmniCrystalsCollected,
                    CrystalType.Elemental => scaleWithSize ? roundStats.MassCrystalValue +
                                                             roundStats.ChargeCrystalValue +
                                                             roundStats.TimeCrystalValue + 
                                                             roundStats.SpaceCrystalValue: roundStats.ElementalCrystalsCollected,
                    _ => 0
                };
                return currentScore + scoreIncrement * ScoreMultiplier;
            }
            return currentScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            return CalculateScore(playerName, currentScore, turnStartTime);
        }
    }
}
