using System;
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

            // Decouple OnClientReady (which clears the loading screen) from
            // InitializeGame. InitializeGame fires OnInitializeGame synchronously
            // to many subscribers (Cell, MiniGamePlayerSpawnerAdapter, ScoreTracker,
            // etc); a single subscriber throwing would otherwise prevent
            // InvokeClientReady from ever being called and leave the loading screen
            // stuck. Fail loud, but always raise OnClientReady so the fade clears.
            try
            {
                gameData.InitializeGame();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SinglePlayerMiniGameBase] InitializeGame threw: {ex}", this);
            }

            gameData.InvokeClientReady();

            // Match MultiplayerMiniGameControllerBase.InitializeAfterDelay so the
            // ApplicationStateMachine transitions LoadingGame → InGame for solo
            // games too. Without this, AppState would stay at LoadingGame for the
            // duration of the singleplayer session.
            gameData.InvokeSessionStarted();

            try
            {
                SetupNewRound();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SinglePlayerMiniGameBase] SetupNewRound threw: {ex}", this);
            }
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