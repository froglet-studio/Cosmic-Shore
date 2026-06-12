using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Tracks goals per domain for Astro League, mirrors them into each player's
    /// RoundStats.Score (so the shared scoreboard/end-game flow works untouched),
    /// and raises a formatted "jade - ruby" score string for UI listeners.
    /// </summary>
    public class AstroLeagueScoreManager : MonoBehaviour
    {
        [SerializeField] AstroLeagueBall ball;
        [SerializeField] ScriptableEventString onScoreUpdated;

        [Inject] GameDataSO gameData;

        readonly Dictionary<Domains, int> goals = new();

        public int GetGoals(Domains domain) => goals.GetValueOrDefault(domain, 0);
        public int JadeGoals => GetGoals(Domains.Jade);
        public int RubyGoals => GetGoals(Domains.Ruby);

        /// <summary>Raised after a goal is recorded. Payload: scoring domain, jade total, ruby total.</summary>
        public event Action<Domains, int, int> OnGoalRecorded;

        void OnEnable()
        {
            if (ball != null)
                ball.OnGoalScored += RecordGoal;
        }

        void OnDisable()
        {
            if (ball != null)
                ball.OnGoalScored -= RecordGoal;
        }

        void RecordGoal(Domains scoringDomain)
        {
            goals[scoringDomain] = GetGoals(scoringDomain) + 1;

            foreach (var roundStats in gameData.RoundStatsList)
            {
                if (roundStats.Domain == scoringDomain)
                    roundStats.Score = goals[scoringDomain];
            }

            RaiseScore();
            OnGoalRecorded?.Invoke(scoringDomain, JadeGoals, RubyGoals);
        }

        public void ResetScores()
        {
            goals.Clear();
            RaiseScore();
        }

        void RaiseScore() => onScoreUpdated?.Raise($"{JadeGoals} - {RubyGoals}");
    }
}
