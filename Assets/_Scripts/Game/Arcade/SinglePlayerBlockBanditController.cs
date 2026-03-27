namespace CosmicShore.Game.Arcade
{
    /// <summary>
    /// Block Bandit game mode — timed single-player mode where the player
    /// steals blocks to score points before time runs out.
    /// </summary>
    public class SinglePlayerBlockBanditController : SinglePlayerMiniGameControllerBase
    {
        protected override void SetupNewRound()
        {
            RaiseToggleReadyButtonEvent(true);
            base.SetupNewRound();
        }

        protected override void EndGame()
        {
            base.EndGame();
            // No EndGameCinematicController in this scene, so trigger
            // the scoreboard directly after the base EndGame flow.
            gameData.InvokeShowGameEndScreen();
        }
    }
}
