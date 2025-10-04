using Unity.Netcode;

namespace CosmicShore.Utility.ClassExtensions
{
    public static class NetcodeExtensions
    {
        public static bool IsLocalClient(this ulong clientId)
            => clientId == NetworkManager.Singleton.LocalClientId;
    }
}