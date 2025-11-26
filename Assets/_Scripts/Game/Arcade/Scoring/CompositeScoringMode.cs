using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    [System.Serializable]
    public class CompositeScoringMode : BaseScoringMode
    {
        [SerializeField] private List<BaseScoringMode> scoringModes;

        public CompositeScoringMode(IEnumerable<BaseScoringMode> modes, float scoreNormalizationQuotient = 145.65f)
            : base(scoreNormalizationQuotient)
        {
            scoringModes = new List<BaseScoringMode>(modes);
        }

        public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (scoringModes == null || scoringModes.Count == 0)
                return currentScore;

            float totalScore = currentScore;
            foreach (var mode in scoringModes)
            {
                if (mode != null)
                {
                    totalScore += mode.CalculateScore(playerName, 0, turnStartTime);
                }
            }

            return totalScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            if (scoringModes == null || scoringModes.Count == 0)
                return currentScore;

            float totalScore = currentScore;
            foreach (var mode in scoringModes)
            {
                if (mode != null)
                {
                    totalScore += mode.EndTurnScore(playerName, 0, turnStartTime);
                }
            }

            return totalScore;
        }

        public void AddScoringMode(BaseScoringMode mode, bool useGolfRules = false)
        {
            if (scoringModes == null)
                scoringModes = new List<BaseScoringMode>();

            if (mode != null && !scoringModes.Contains(mode))
            {
                scoringModes.Add(mode);
            }
        }

        public void RemoveScoringMode(BaseScoringMode mode)
        {
            if (scoringModes != null && mode != null)
            {
                scoringModes.Remove(mode);
            }
        }

        public int GetScoringModeCount()
        {
            return scoringModes?.Count ?? 0;
        }

        public bool ContainsScoringMode(BaseScoringMode mode)
        {
            return scoringModes != null && mode != null && scoringModes.Contains(mode);
        }
    }
}