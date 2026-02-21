using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Game controller for Astro League - a Rocket League-inspired mini-game.
    /// Ships fly in a 3D arena and hit a ball into goals to score.
    ///
    /// Scene setup requires:
    /// - AstroLeagueArena with walls
    /// - AstroLeagueBall at arena center
    /// - Two AstroLeagueGoal triggers at each end
    /// - AstroLeagueScoreManager wired to the ball
    /// - TurnMonitorController with TimeBasedTurnMonitor and/or GoalScoredTurnMonitor
    /// - CountdownTimer for match start
    /// - GameDataSO reference (shared asset)
    /// </summary>
    public class SinglePlayerAstroLeagueController : SinglePlayerMiniGameControllerBase
    {
        [Header("Astro League")]
        [SerializeField] AstroLeagueBall ball;
        [SerializeField] AstroLeagueArena arena;
        [SerializeField] AstroLeagueScoreManager scoreManager;

        [Header("Match Settings")]
        [SerializeField] int roundsToWin = 1;

        protected override bool HasEndGame => true;
        protected override bool ShowEndGameSequence => true;
        protected override bool ShouldResetPlayersOnTurnEnd => true;

        protected override void Start()
        {
            numberOfRounds = roundsToWin;
            base.Start();
        }

        protected override void OnCountdownTimerEnded()
        {
            if (ball != null)
                ball.FullReset();

            if (scoreManager != null)
                scoreManager.ResetScores();

            base.OnCountdownTimerEnded();
        }

        protected override void SetupNewTurn()
        {
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewTurn();
        }

        protected override void SetupNewRound()
        {
            if (ball != null)
                ball.FullReset();

            if (scoreManager != null)
                scoreManager.ResetScores();

            base.SetupNewRound();
        }

        protected override void OnTurnEndedCustom()
        {
            base.OnTurnEndedCustom();
        }

        protected override void OnResetForReplay()
        {
            if (ball != null)
                ball.FullReset();

            if (scoreManager != null)
                scoreManager.ResetScores();

            base.OnResetForReplay();
        }

        protected override void ResetEnvironmentForReplay()
        {
            if (ball != null)
                ball.FullReset();

            if (scoreManager != null)
                scoreManager.ResetScores();
        }
    }
}
