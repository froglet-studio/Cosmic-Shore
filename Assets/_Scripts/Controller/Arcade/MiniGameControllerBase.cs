using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Stateless top-level game-flow controller.
    /// Template Method Pattern: Defines skeleton of game flow with hooks for customization.
    /// </summary>
    public abstract class MiniGameControllerBase : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] protected int numberOfRounds = int.MaxValue;
        [SerializeField] protected int numberOfTurnsPerRound = 1;
        
        [Header("Scene References")]
        [SerializeField] protected CountdownTimer countdownTimer;
        [Inject] protected GameDataSO gameData;
        [SerializeField] protected ScriptableEventBool _onToggleReadyButton;

        protected virtual bool HasEndGame => true;
        protected virtual bool ShouldResetPlayersOnTurnEnd => false;
        protected virtual bool ShowEndGameSequence => true;
        protected virtual bool UseGolfRules => false;
        
        public void OnReadyClicked()
        {
            OnReadyClicked_();
        }
        
        protected virtual void OnReadyClicked_()
        {
            RaiseToggleReadyButtonEvent(false);
            StartCountdownTimer();
        }

        protected void StartCountdownTimer()
        {
            if (countdownTimer != null)
                countdownTimer.BeginCountdown(OnCountdownTimerEnded);
        }
        
        protected void RaiseToggleReadyButtonEvent(bool enable)
        {
            _onToggleReadyButton?.Raise(enable);
        }
        
        protected abstract void OnCountdownTimerEnded();

        /// <summary>
        /// Per-peer latch for <see cref="ZeroStatsForGameStartOnce"/>. Not networked - every peer
        /// zeroes its own copy of the stats.
        /// </summary>
        bool _statsZeroedForGame;

        /// <summary>
        /// Zero every player's stats exactly once per GAME, at the moment the first turn begins.
        ///
        /// <see cref="StatsManager"/> has no turn gate - it records from the moment it network-
        /// spawns - and the window before the first turn is long: the arena builds (Ribcage lays
        /// 10-20k prisms), vessels spawn, and the countdown runs. Anything destroyed in there used
        /// to count, so a match could visibly start with a player above zero.
        ///
        /// Once per GAME rather than per turn, because modes with several turns per round
        /// accumulate across them deliberately; wiping at every countdown would erase earlier
        /// turns. Call <see cref="RearmGameStartStatsReset"/> when a new game begins (replay).
        /// </summary>
        protected void ZeroStatsForGameStartOnce()
        {
            if (_statsZeroedForGame) return;

            _statsZeroedForGame = true;
            gameData.ResetStatsForGameStart();
        }

        /// <summary>Re-arms <see cref="ZeroStatsForGameStartOnce"/> for the next game (replay).</summary>
        protected void RearmGameStartStatsReset() => _statsZeroedForGame = false;
        
        protected virtual void SetupNewTurn()
        {
        }

        protected void EndTurn()
        {
            OnTurnEndedCustom();
            
            if (ShouldResetPlayersOnTurnEnd)
                gameData.ResetPlayers();
            
            gameData.TurnsTakenThisRound++;

            if (gameData.TurnsTakenThisRound >= numberOfTurnsPerRound)
                EndRound();
            else 
                SetupNewTurn();
        }
        
        protected virtual void OnTurnEndedCustom()
        {
        }
        
        protected virtual void SetupNewRound()
        {
            gameData.TurnsTakenThisRound = 0;
            gameData.InvokeMiniGameRoundStarted();
            SetupNewTurn();
        }
        
        protected void EndRound()
        {
            OnRoundEndedCustom();
            
            gameData.RoundsPlayed++;
            gameData.InvokeMiniGameRoundEnd();
            
            if (HasEndGame && gameData.RoundsPlayed >= numberOfRounds)
                EndGame();
            else
                SetupNewRound();
        }
        
        protected virtual void OnRoundEndedCustom()
        {
        }

        protected virtual void EndGame()
        {
            if (!ShowEndGameSequence) return;
            gameData.SortRoundStats(UseGolfRules);
            gameData.InvokeWinnerCalculated();
            gameData.InvokeMiniGameEnd();
        }
        
        protected virtual void OnResetForReplay()
        {
            SetupNewRound();
        }

        /// <summary>
        /// Entry point for Play Again / Restart. Unified across singleplayer and
        /// multiplayer: the scoreboard and pause menu both call this. Subclasses
        /// decide how to execute the reset (local vs. network-authoritative).
        /// </summary>
        public abstract void RequestReplay();

        protected virtual void ResetEnvironmentForReplay()
        {
            CSDebug.Log("[MiniGameControllerBase] ResetEnvironmentForReplay - Override in subclass");
        }
    }
}