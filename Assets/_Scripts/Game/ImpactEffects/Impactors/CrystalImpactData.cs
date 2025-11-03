using UnityEngine;
using Unity.Netcode;

namespace CosmicShore.Game
{
    public struct CrystalImpactData : INetworkSerializable
    {
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
        }
    }

}