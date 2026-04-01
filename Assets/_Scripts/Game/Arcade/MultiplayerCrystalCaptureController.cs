namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCrystalCaptureController : MultiplayerDomainGamesController
    {
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            numberOfRounds = 1;
            numberOfTurnsPerRound = 1;
        }

        protected override bool UseGolfRules => false;
    }
}