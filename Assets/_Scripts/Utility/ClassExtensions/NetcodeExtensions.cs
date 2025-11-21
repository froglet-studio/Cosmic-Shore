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
    }
}