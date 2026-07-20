using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Blitz (single-player scene). Mid-turn scoring is owned by
    /// WildlifeBlitzScoreKeeper (blitz composite racing the cell's threshold); EndGame
    /// runs the rule-driven results tail via WildlifeBlitzScoringRuleSO - win means the
    /// blitz monitor tripped the threshold (Score = clear time), loss means the clock
    /// ran out (Score = DNF sentinel).
    /// </summary>
    public class SinglePlayerWildlifeBlitzController : SinglePlayerMiniGameControllerBase
    {
        [Header("Blitz Config")]
        [SerializeField] WildlifeBlitzScoreKeeper scoreTracker;
        [SerializeField] SingleplayerWildlifeBlitzTurnMonitor blitzTurnMonitor;
        [SerializeField] TimeBasedTurnMonitor timeTurnMonitor;
        [SerializeField] CellRuntimeDataSO cellData;

        [Header("Scoring")]
        [Tooltip("Drag WildlifeBlitzScoringRule.asset - formats the win-time/DNF outcome and results.")]
        [SerializeField] ScoringRuleSO rule;

        protected override bool UseGolfRules => true;

        protected override void Start()
        {
            gameData.ScoringRule = rule;
            base.Start();
            InitializeGame();
        }

        void InitializeGame()
        {
            if (gameData.LocalRoundStats != null)
                gameData.LocalRoundStats.Score = 0;

            if (scoreTracker)
                scoreTracker.ResetScores();
        }

        protected override void SetupNewTurn()
        {
            RaiseToggleReadyButtonEvent(true);

            blitzTurnMonitor?.StartMonitor();
            if (scoreTracker) scoreTracker.StartTracking();
            base.SetupNewTurn();
        }

        /// <summary>
        /// Rule-driven end-game: the SP twin of the domain base's SyncFinalResults tail.
        /// A tripped blitz monitor means the local player cleared the cell in
        /// timeTurnMonitor.ElapsedTime; otherwise the clock ran out (DNF).
        /// </summary>
        protected override void EndGame()
        {
            if (!ShowEndGameSequence) return;

            bool didWin = blitzTurnMonitor != null && blitzTurnMonitor.DidPlayerWin;
            float clearTime = timeTurnMonitor ? timeTurnMonitor.ElapsedTime : 0f;
            var localDomain = gameData.LocalRoundStats?.Domain ?? Domains.Blue;
            var winner = didWin ? localDomain : Domains.Blue;

            rule.AssignScores(gameData, winner, clearTime);
            gameData.SortRoundStats(UseGolfRules);
            gameData.CalculateDomainStats(UseGolfRules);
            gameData.SetResults(rule.BuildResults(gameData));

            // AFTER SetResults: on a DNF loss the winner is nobody (Blue), but SetResults
            // derives Winner* from Results[0] when unset - which would credit the DNF'd
            // local player and show VICTORY on a defeat. Explicit writes win either way.
            gameData.WinnerDomain = winner;
            gameData.WinnerName = didWin ? gameData.LocalRoundStats?.Name ?? "" : "";

            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }

        protected override void OnResetForReplay()
        {
            ResetEnvironmentForReplay();
            base.OnResetForReplay();
        }

        protected override void ResetEnvironmentForReplay()
        {
            if (scoreTracker) scoreTracker.StopTracking();
            blitzTurnMonitor?.StopMonitor();
            timeTurnMonitor?.StopMonitor();

            if (gameData.LocalRoundStats != null)
                gameData.LocalRoundStats.Score = 0;

            if (scoreTracker)
                scoreTracker.ResetScores();

            base.ResetEnvironmentForReplay();
        }
    }
}
