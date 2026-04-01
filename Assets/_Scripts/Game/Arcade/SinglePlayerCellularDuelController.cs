

namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Cellular Duel game mode.
    /// Features: 2-player duel with vessel swapping between rounds
    /// </summary>
    public class SinglePlayerCellularDuelController : SinglePlayerMiniGameControllerBase 
    {
        protected override bool ShouldResetPlayersOnTurnEnd => true;
        
        protected override void OnTurnEndedCustom()
        {
            gameData.SwapVessels();
            base.OnTurnEndedCustom();
        }
        
        protected override void SetupNewRound()
        {
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewRound();
        }

        protected override void OnResetForReplay()
        {
            gameData.SwapVessels();
            base.OnResetForReplay();
        }
    }
}