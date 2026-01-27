namespace CosmicShore.Game.Arcade
{
    public abstract class SinglePlayerMiniGameControllerBase : MiniGameControllerBase
    {
        void OnEnable()
        {
            gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
        }
        
        protected virtual void Start()
        {
            gameData.InitializeGame();
            gameData.InvokeClientReady();
            SetupNewRound();
        }
        
        void OnDisable() 
        {
            gameData.OnMiniGameTurnEnd.OnRaised -= EndTurn;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
        }
        
        protected override void SetupNewRound()
        {
            gameData.InvokeMiniGameRoundStarted();
            base.SetupNewRound();
        }
        
        protected override void OnCountdownTimerEnded()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        protected override void EndTurn()
        {
           // gameData.ResetPlayers();
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