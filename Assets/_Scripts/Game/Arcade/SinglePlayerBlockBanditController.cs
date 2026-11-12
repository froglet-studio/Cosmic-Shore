namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Block Bandit game mode — timed single-player mode where the player
    /// steals blocks to score points before time runs out.
    /// EndGameCinematicController handles the post-game cinematic and scoreboard trigger.
    /// </summary>
    public class SinglePlayerBlockBanditController : SinglePlayerMiniGameControllerBase
    {
        protected override void SetupNewRound()
        {
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewRound();
        }
    }
}
