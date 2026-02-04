using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerWildlifeBlitzController : SinglePlayerMiniGameControllerBase
    {
        [Header("Blitz Config")]
        [SerializeField] SinglePlayerWildlifeBlitzScoreTracker scoreTracker;
        [SerializeField] SingleplayerWildlifeBlitzTurnMonitor blitzTurnMonitor;
        [SerializeField] TimeBasedTurnMonitor timeTurnMonitor;
        [SerializeField] CellDataSO cellData;

        protected override void Start()
        {
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
            //timeTurnMonitor?.StartMonitor();
            if (scoreTracker) scoreTracker.StartTracking();
            base.SetupNewTurn();
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

        protected override void OnCountdownTimerEnded()
        {
            base.OnCountdownTimerEnded();
        }
    }
}