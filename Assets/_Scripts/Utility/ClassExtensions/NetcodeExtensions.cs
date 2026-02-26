using Unity.Netcode;

namespace CosmicShore.Utility.ClassExtensions
{
    public static class NetcodeExtensions
    {
        /// <summary>
        /// If the clientId is same as LocalClientId
        /// </summary>
        public static bool IsLocalClient(this ulong clientId)
            => clientId == NetworkManager.Singleton.LocalClientId;

        /// <summary>
        /// Returns true when this machine is the server/host.
        /// Falls back to <see cref="NetworkManager.Singleton"/> when the behaviour's
        /// <see cref="NetworkBehaviour.IsSpawned"/> flag is unreliable — which happens
        /// after a GameObject is toggled via SetActive(false) → SetActive(true) in
        /// party mode. The per-behaviour <see cref="NetworkBehaviour.IsServer"/>
        /// returns false in that case because IsSpawned becomes stale, even though
        /// the machine is still the host.
        /// </summary>
        public static bool IsServerSafe(this NetworkBehaviour behaviour)
        {
            if (behaviour.IsServer) return true;
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}