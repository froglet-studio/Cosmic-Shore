using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerWildlifeBlitzController : SinglePlayerMiniGameControllerBase
    {
        [Header("Blitz Config")]
        [SerializeField] WildlifeBlitzScoreTracker scoreTracker;
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
            
            base.SetupNewTurn();
        }

        protected override void OnResetForReplay()
        {
            ResetEnvironmentForReplay();
            base.OnResetForReplay();
        }

        protected override void ResetEnvironmentForReplay()
        {
            blitzTurnMonitor?.StopMonitor();
            timeTurnMonitor?.StopMonitor();

            if (gameData.LocalRoundStats != null) 
                gameData.LocalRoundStats.Score = 0;
    
            if (scoreTracker) 
                scoreTracker.ResetScores();

            if (cellData?.OnResetForReplay)
                cellData.OnResetForReplay.Raise();
    
            base.ResetEnvironmentForReplay();
        }

        protected override void OnCountdownTimerEnded()
        {
            base.OnCountdownTimerEnded();
        }
    }
}