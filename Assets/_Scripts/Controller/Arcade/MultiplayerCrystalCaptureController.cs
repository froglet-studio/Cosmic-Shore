
namespace CosmicShore.Gameplay
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

        // Party mode activate/deactivate use base class defaults — 
        // Crystal Capture has no custom state to reset.
    }
}
