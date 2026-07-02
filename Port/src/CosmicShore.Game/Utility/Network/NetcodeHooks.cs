// Ported verbatim from Assets/_Scripts/Utility/Network/NetcodeHooks.cs
// (vessel-initializer / menu-swap arc). Mechanical substitutions only (README).
using System;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Utility
{
    // useful for classes that can't be NetworkBehaviours themselves (for example, with dedicated servers, you can't have a NetworkBehaviour that exists
    // on clients but gets stripped on the server, this will mess with your NetworkBehaviour indexing.
    public class NetcodeHooks : NetworkBehaviour
    {
        public event Action OnNetworkSpawnHook;

        public event Action OnNetworkDespawnHook;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            OnNetworkSpawnHook?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            OnNetworkDespawnHook?.Invoke();
        }
    }
}
