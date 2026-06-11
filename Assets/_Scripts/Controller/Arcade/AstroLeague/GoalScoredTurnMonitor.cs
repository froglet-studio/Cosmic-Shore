using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Turn monitor that ends the turn when a team reaches the goal limit.
    /// Works alongside TimeBasedTurnMonitor for timed matches with goal caps.
    /// </summary>
    public class GoalScoredTurnMonitor : TurnMonitor
    {
        [Header("Goal Settings")]
        [SerializeField] int goalLimit = 5;
        [SerializeField] AstroLeagueBall ball;

        int jadeGoals;
        int rubyGoals;
        bool limitReached;

        public int JadeGoals => jadeGoals;
        public int RubyGoals => rubyGoals;

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
            if (scoringTeam == Domains.Jade)
                jadeGoals++;
            else if (scoringTeam == Domains.Ruby)
                rubyGoals++;

            UpdateDisplay();

            if (jadeGoals >= goalLimit || rubyGoals >= goalLimit)
                limitReached = true;
        }

        public override bool CheckForEndOfTurn() => limitReached;

        public override void StartMonitor()
        {
            jadeGoals = 0;
            rubyGoals = 0;
            limitReached = false;
            UpdateDisplay();
            base.StartMonitor();
        }

        protected override void ResetState()
        {
            jadeGoals = 0;
            rubyGoals = 0;
            limitReached = false;
        }

        void UpdateDisplay()
        {
            onUpdateTurnMonitorDisplay?.Raise($"{jadeGoals} - {rubyGoals}");
        }
    }
}
