using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Arcade.Scoring
{
    // DEPRECATED - Unnecessary
    
    /*[System.Serializable]
    public class CompositeScoring : BaseScoring
    {
        List<BaseScoring> scoringList;

        public CompositeScoring(ScoreData scoreData, float scoreNormalizationQuotient = 145.65f) 
            : base(scoreData, scoreNormalizationQuotient)
        {
            scoringList = new List<BaseScoring>();
        }

        public override void CalculateScore()
        {
            
        }

        public void AddScoringMode(BaseScoring mode, bool useGolfRules = false)
        {
            if (scoringList == null)
                scoringList = new List<BaseScoring>();

            if (mode != null && !scoringList.Contains(mode))
            {
                scoringList.Add(mode);
            }
        }

        public void RemoveScoringMode(BaseScoring mode)
        {
            if (scoringList != null && mode != null)
            {
                scoringList.Remove(mode);
            }
        }

        public int GetScoringModeCount()
        {
            return scoringList?.Count ?? 0;
        }

        public bool ContainsScoringMode(BaseScoring mode)
        {
            return scoringList != null && mode != null && scoringList.Contains(mode);
        }*/
        
        /*public override float CalculateScore(string playerName, float currentScore, float turnStartTime)
        {
            if (scoringList == null || scoringList.Count == 0)
                return currentScore;

            float totalScore = currentScore;
            foreach (var mode in scoringList)
            {
                if (mode != null)
                {
                    // totalScore += mode.CalculateScore(playerName, 0, turnStartTime);
                    totalScore += mode.CalculateScore(playerName, 0);
                }
            }
            return totalScore;
        }

        public override float EndTurnScore(string playerName, float currentScore, float turnStartTime)
        {
            if (scoringList == null || scoringList.Count == 0)
                return currentScore;

            float totalScore = currentScore;
            foreach (var mode in scoringList)
            {
                if (mode != null)
                {
                    // totalScore += mode.EndTurnScore(playerName, 0, turnStartTime);
                    totalScore += mode.EndTurnScore(playerName, 0);
                }
            }
            return totalScore;
        }
    }*/
}
