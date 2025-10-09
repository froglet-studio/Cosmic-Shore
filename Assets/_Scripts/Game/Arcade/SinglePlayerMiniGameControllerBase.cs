namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerMiniGameControllerBase : MiniGameControllerBase
    {
        void OnEnable()
        {
            miniGameData.OnMiniGameTurnEnd += EndTurn;
        }
        
        void Start()
        {
            ToggleReadyButton(true);
            Initialize();
        }
        
        void OnDisable() 
        {
            miniGameData.OnMiniGameTurnEnd -= EndTurn;
        }
        
        protected override void OnCountdownTimerEnded()
        {
            roundsPlayed = 0;
            turnsTakenThisRound = 0;
            miniGameData.SetPlayersActive();
            miniGameData.StartNewGame();
        }
        
        protected override void EndGame()
        {
            miniGameData.InvokeMiniGameEnd();
            miniGameData.ResetPlayers();
        }
    }
}