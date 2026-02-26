using UnityEngine;
using Unity.Netcode;
using CosmicShore.Models.Enums;
using CosmicShore.Game.Environment;
namespace CosmicShore.Game.ImpactEffects
{
    public struct CrystalImpactData : INetworkSerializable
    {
        public Element Element;
        public float SpeedBuffAmount;
        public bool IsAlive;

        // 🔥 The factory method
        public static CrystalImpactData FromCrystal(Crystal crystal)
        {
            return new CrystalImpactData
            {
                Element = crystal.crystalProperties.Element,
                SpeedBuffAmount = crystal.crystalProperties.speedBuffAmount,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            serializer.SerializeValue(ref Element);
            serializer.SerializeValue(ref SpeedBuffAmount);
        }
    }
}