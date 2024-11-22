using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    public class CrystalsCollectedScoring : BaseScoringMode
    {
        private readonly CrystalType crystalType;

        public enum CrystalType
        {
            All,
            Omni,
            Elemental
        }

        public CrystalsCollectedScoring(CrystalType type = CrystalType.All, float scoreNormalizationQuotient = 145.65f) 
            : base(scoreNormalizationQuotient)
        {
            crystalType = type;
        }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (StatsManager.Instance.PlayerStats.TryGetValue(playerName, out var roundStats))
            {
                Debug.Log($"Calculating score for {playerName} with {roundStats.ElementalCrystalsCollected} elemental crystals collected.");
                float scoreIncrement = crystalType switch
                {
                    CrystalType.All => roundStats.CrystalsCollected,
                    CrystalType.Omni => roundStats.OmniCrystalsCollected,
                    CrystalType.Elemental => roundStats.ElementalCrystalsCollected,
                    _ => 0
                };
                return currentScore + ApplyGolfRules(scoreIncrement);
            }
            return currentScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            return CalculateScore(playerName, currentScore, turnStartTime);
        }
    }
}
