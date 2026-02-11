namespace CosmicShore.Game.Arcade
{
    public class MultiplayerCrystalCaptureController : MultiplayerDomainGamesController
    {
        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            this.numberOfRounds = 1;
            this.numberOfTurnsPerRound = 1;
        }

        protected override bool UseGolfRules => false;
    }
}