using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Base controller for single-player game modes.
    /// Handles event subscriptions and initial setup.
    /// </summary>
    public abstract class SinglePlayerMiniGameControllerBase : MiniGameControllerBase
    {
        protected virtual void Start()
        {
            if (gameData == null)
            {
                CSDebug.LogError("GameDataSO is not assigned!", this);
                return;
            }

            gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;

            gameData.InitializeGame();
            gameData.InvokeClientReady();
            SetupNewRound();
        }

        protected virtual void OnDisable()
        {
            if (gameData == null) return;
            gameData.OnMiniGameTurnEnd.OnRaised -= EndTurn;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
        }

        protected override void OnCountdownTimerEnded()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        public override void RequestReplay()
        {
            gameData.ResetStatsDataForReplay();
            gameData.ResetForReplay();

            if (CameraManager.Instance)
                CameraManager.Instance.SnapPlayerCameraToTarget();
        }
    }
}