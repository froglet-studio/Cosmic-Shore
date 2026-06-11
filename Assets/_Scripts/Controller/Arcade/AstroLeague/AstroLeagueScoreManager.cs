using System;
using System.Collections.Generic;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Tracks goal counts per team and updates round stats accordingly.
    /// Listens to the ball's OnGoalScored event and maps goals to player scores.
    /// </summary>
    public class AstroLeagueScoreManager : MonoBehaviour
    {
        [SerializeField] AstroLeagueBall ball;
        [SerializeField] GameDataSO gameData;
        [SerializeField] ScriptableEventString onScoreUpdated;

        readonly Dictionary<Domains, int> teamGoals = new();

        public event Action<Domains, int, int> OnGoalScoredWithTotals;

        public int GetGoals(Domains team) =>
            teamGoals.TryGetValue(team, out int goals) ? goals : 0;

        void OnEnable()
        {
            if (ball != null)
                ball.OnGoalScored += HandleGoalScored;
        }

        void OnDisable()
        {
            if (ball != null)
                ball.OnGoalScored -= HandleGoalScored;
        }

        void HandleGoalScored(Domains scoringTeam)
        {
            if (!teamGoals.ContainsKey(scoringTeam))
                teamGoals[scoringTeam] = 0;

            teamGoals[scoringTeam]++;

            // Update round stats for all players on the scoring team
            foreach (var roundStats in gameData.RoundStatsList)
            {
                if (roundStats.Domain == scoringTeam)
                    roundStats.Score = teamGoals[scoringTeam];
            }

            int jadeGoals = GetGoals(Domains.Jade);
            int rubyGoals = GetGoals(Domains.Ruby);

            onScoreUpdated?.Raise($"{jadeGoals} - {rubyGoals}");
            OnGoalScoredWithTotals?.Invoke(scoringTeam, jadeGoals, rubyGoals);
        }

        public void ResetScores()
        {
            teamGoals.Clear();
            onScoreUpdated?.Raise("0 - 0");
        }
    }
}
