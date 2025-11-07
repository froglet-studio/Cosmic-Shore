namespace CosmicShore.Game.Arcade
{
    public class SinglePlayerCellularDuelController : SinglePlayerMiniGameControllerBase 
    {
        protected override void OnResetForReplay()
        {
            // swap to original vessels for each player
            gameData.SwapVessels();              
            base.OnResetForReplay();
        }

        protected override void SetupNewRound()
        {
            // Don't swap at first round
            if (gameData.RoundsPlayed > 0)       
                gameData.SwapVessels();
            
            ToggleReadyButton(true);
            base.SetupNewRound();
        }
    }
}