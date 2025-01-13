using System;
using Unity.Netcode;

namespace CosmicShore.Utilities.Network
{
    /// <summary>
    /// This struct is used to represent a globally unique identifier for networked objects.
    /// </summary>
    public struct NetworkGuid : INetworkSerializeByMemcpy
    {
        public ulong FirstHalf;
        public ulong SecondHalf;
    }

    /// <summary>
    /// This class provides extension methods for converting between Guid and NetworkGuid.
    /// </summary>
    public static class NetworkGuidExtensions
    {
        /// <summary>
        /// Converts a Guid to a NetworkGuid.
        /// </summary>
        public static NetworkGuid ToNetworkGuid(this Guid id)
        {
            NetworkGuid networkGuid = new NetworkGuid();
            networkGuid.FirstHalf = BitConverter.ToUInt64(id.ToByteArray(), 0);
            networkGuid.SecondHalf = BitConverter.ToUInt64(id.ToByteArray(), 8);
            return networkGuid;
        }

        /// <summary>
        /// Converts a NetworkGuid to a Guid.
        /// </summary>
        public static Guid ToGuid(this NetworkGuid networkGuid)
        {
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(networkGuid.FirstHalf), 0, bytes, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(networkGuid.SecondHalf), 0, bytes, 8, 8);
            return new Guid(bytes);
        }
    }
}