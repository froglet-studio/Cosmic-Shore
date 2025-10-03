using Unity.Netcode;

namespace CosmicShore.Game
{
    public class ClientMultiplayerFreestylePlayerSpawner : ClientPlayerSpawner
    {
        protected override void InitializeAndSetupPlayer()
        {
            base.InitializeAndSetupPlayer();
            gameData.InvokeClientReady();
        }
    }
}