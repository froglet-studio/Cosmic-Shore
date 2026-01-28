namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// WildlifeBlitz game mode.
    /// Simple mode with: Start → Play → End → Cinematics → Scoreboard
    /// </summary>
    public class WildlifeBlitzMiniGame : SinglePlayerMiniGameControllerBase
    {
        protected override void SetupNewTurn()
        {
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewTurn();
        }
    }
}