namespace CosmicShore.Game.Arcade
{
    public class CellularDuelController : SinglePlayerMiniGameControllerBase 
    {
        protected override void OnResetForReplay()
        {
            // swap to original vessels for each player
            gameData.SwapVessels();              
            base.OnResetForReplay();
        }

        protected override void SetupNewRound()
        {
            ToggleReadyButton(true);
            
            // Don't swap at first round
            if (roundsPlayed > 0)       
                gameData.SwapVessels();
            
            base.SetupNewRound();
        }
    }
}