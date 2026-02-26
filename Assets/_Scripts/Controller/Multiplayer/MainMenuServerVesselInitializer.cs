using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main variant of ServerPlayerVesselInitializer.
    /// - Spawns only the host player's vessel (no AI opponents).
    /// - Does NOT shutdown the NetworkManager on despawn so the
    ///   host persists across scene transitions.
    /// </summary>
    public class MainMenuServerVesselInitializer : ServerPlayerVesselInitializer
    {
        // Only spawn the host player's vessel — no AI in the menu.
        protected override void OnClientConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            DelayedSpawnVesselForPlayer(clientId).Forget();
        }

        // Clean up subscriptions but do NOT shutdown the NetworkManager.
        // The host stays running when transitioning to a game scene.
        protected override void OnNetworkDespawn()
        {
            if (NetworkManager.Singleton)
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;

            NetworkVesselClientCache.OnNewInstanceAdded -= OnNewVesselClientAdded;
        }
    }
}
