namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerMiniGameControllerBase : MiniGameControllerBase
    {
        void OnEnable()
        {
            gameData.OnMiniGameTurnEnd += EndTurn;
        }
        
        void Start()
        {
            ToggleReadyButton(true);
            InitializeGame();
        }
        
        void OnDisable() 
        {
            gameData.OnMiniGameTurnEnd -= EndTurn;
        }
        
        protected override void OnCountdownTimerEnded()
        {
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            gameData.SetPlayersActive();
            gameData.StartNewGame();
        }
        
        protected override void EndGame()
        {
            gameData.InvokeMiniGameEnd();
            gameData.ResetPlayers();
        }
    }
}