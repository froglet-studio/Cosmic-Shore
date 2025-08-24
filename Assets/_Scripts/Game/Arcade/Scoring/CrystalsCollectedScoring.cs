using CosmicShore.Core;
using CosmicShore.SOAP;
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

        public CrystalsCollectedScoring(MiniGameDataSO scoreData, float scoreMultiplier = 145.65f, CrystalType type = CrystalType.All, bool ScaleWithSize = false) 
            : base(scoreData, scoreMultiplier)
        {
            crystalType = type;
            scaleWithSize = ScaleWithSize;
        }

        public override void CalculateScore()
        {
            foreach (var playerScore in miniGameData.RoundStatsList)
            {
                if (!TryGetRoundStats(playerScore.Name, out IRoundStats roundStats))
                    return;
                
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
                playerScore.Score += scoreIncrement * scoreMultiplier;
            }
        }
        
        /*public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
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
                return currentScore + scoreIncrement * scoreMultiplier;
            }
            return currentScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            // return CalculateScore(playerName, currentScore, turnStartTime);
            return CalculateScore(playerName, currentScore);
        }*/
    }
}
