namespace CosmicShore.Game.Arcade
{
    public abstract class SinglePlayerMiniGameControllerBase : MiniGameControllerBase
    {
        void OnEnable()
        {
            gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
        }
        
        void Start()
        {
            gameData.InitializeGame();
            gameData.InvokeClientReady();
            RaiseToggleReadyButtonEvent(true);
        }
        
        void OnDisable() 
        {
            gameData.OnMiniGameTurnEnd.OnRaised -= EndTurn;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
        }
        
        protected override void OnCountdownTimerEnded()
        {
            SetupNewRound();
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        protected override void SetupNewRound()
        {
            if (gameData.RoundsPlayed >= 1)
                RaiseToggleReadyButtonEvent(true);

            base.SetupNewRound();
        }

        protected override void EndTurn()
        {
            gameData.ResetPlayers();
            base.EndTurn();
        }

        protected override void EndRound()
        {
            gameData.RoundsPlayed++;
            gameData.InvokeMiniGameRoundEnd();
            base.EndRound();
        }
        
        protected override void EndGame() =>
            gameData.InvokeMiniGameEnd();
    }
}