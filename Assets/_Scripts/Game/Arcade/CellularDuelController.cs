namespace CosmicShore.Game.Arcade
{
    public class CellularDuelController : SinglePlayerMiniGameControllerBase 
    {
        protected override void SetupNewRound()
        {
            ToggleReadyButton(true);
            base.SetupNewRound();
        }
    }
}