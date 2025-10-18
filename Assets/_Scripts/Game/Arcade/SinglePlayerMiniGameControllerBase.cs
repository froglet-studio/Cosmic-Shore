namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerMiniGameControllerBase : MiniGameControllerBase
    {
        void OnEnable()
        {
            gameData.OnMiniGameTurnEnd.OnRaised += EndTurn;
            gameData.OnResetForReplay.OnRaised += OnResetForReplay;
        }
        
        void Start()
        {
            InitializeGame();
            SetupNewRound();
        }
        
        void OnDisable() 
        {
            gameData.OnMiniGameTurnEnd.OnRaised -= EndTurn;
            gameData.OnResetForReplay.OnRaised -= OnResetForReplay;
        }

        protected override void InitializeGame()
        {
            roundsPlayed = 0;
            base.InitializeGame();
        }
        
        protected override void OnCountdownTimerEnded()
        {
            gameData.SetPlayersActive();
            gameData.StartTurn();
        }

        protected override void EndTurn()
        {
            gameData.ResetPlayers();
            base.EndTurn();
        }
        
        protected override void EndGame()
        {
            gameData.InvokeMiniGameEnd();
        }
        
        void OnResetForReplay()
        {
            roundsPlayed = 0;
            SetupNewRound();
        }
    }
}