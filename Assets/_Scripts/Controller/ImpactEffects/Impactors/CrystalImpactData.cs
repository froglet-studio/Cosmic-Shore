using UnityEngine;
using Unity.Netcode;
using CosmicShore.Data;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    public struct CrystalImpactData : INetworkSerializable
    {
        public Element Element;
        public float SpeedBuffAmount;
        public bool IsAlive;

        /// <summary>
        /// WHICH crystal was collected (<see cref="CellItem.Id"/>), so an effect that needs the
        /// crystal itself can find it on EVERY peer. The vessel's crystal effects are broadcast
        /// (NetworkVesselImpactor), and a remote peer never latched the crystal locally, so an
        /// impactor-side "last crystal I touched" field would be empty exactly where it matters.
        /// </summary>
        public int CrystalId;

        /// <summary>
        /// Where the crystal was WHEN IT WAS COLLECTED. Carried rather than read back off the
        /// crystal because collection and respawn are two independent RPC chains: on a remote
        /// peer the crystal may already have been moved to its next home by the time these
        /// effects arrive, and a retirement animation that starts there starts in the wrong
        /// place.
        /// </summary>
        public Vector3 Position;

        // 🔥 The factory method
        public static CrystalImpactData FromCrystal(Crystal crystal)
        {
            return new CrystalImpactData
            {
                Element = crystal.crystalProperties.Element,
                SpeedBuffAmount = crystal.crystalProperties.speedBuffAmount,
                CrystalId = crystal.Id,
                Position = crystal.transform.position,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer)
            where T : IReaderWriter
        {
            using (serializer.IsReader
                ? CosmicShore.Utility.PerformanceBenchmark.NetMarkers.Deserialize.Auto()
                : CosmicShore.Utility.PerformanceBenchmark.NetMarkers.Serialize.Auto())
            {
                serializer.SerializeValue(ref Element);
                serializer.SerializeValue(ref SpeedBuffAmount);
                serializer.SerializeValue(ref CrystalId);
                serializer.SerializeValue(ref Position);
            }
        }
    }
}