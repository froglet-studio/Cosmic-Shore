using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class CrystalsCollectedScoring : BaseScoring
    {
        readonly CrystalType crystalType;
        bool scaleWithSize;

        // Stats this scoring actually subscribed to. Unsubscription must run off THIS
        // list, never GameData.RoundStatsList: on a mid-turn scene exit the roster is
        // cleared before the tracker tears down, so a list-based unsubscribe detaches
        // nothing and the dead UpdateScore keeps writing Score into the NEXT game's
        // persistent RoundStats (Docs/ScoringSystem/BUGS.md B15).
        readonly List<IRoundStats> _subscribedStats = new();

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
                // continue, not return - one unresolved player must not skip the rest.
                if (!GameData.TryGetRoundStats(playerScore.Name, out var roundStats))
                    continue;
                if (_subscribedStats.Contains(roundStats))
                    continue;

                roundStats.OnCrystalsCollectedChanged += UpdateScore;
                _subscribedStats.Add(roundStats);
            }
        }

        public override void Unsubscribe()
        {
            foreach (var roundStats in _subscribedStats)
            {
                if (roundStats == null) continue;
                roundStats.OnCrystalsCollectedChanged -= UpdateScore;
            }
            _subscribedStats.Clear();
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
