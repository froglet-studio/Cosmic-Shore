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
        /// WHICH crystal was collected and the pose it had at the collect — so an effect that needs
        /// the crystal itself (a vessel's bespoke retirement drawing the crystal's own body) can
        /// find it on EVERY peer and start where it actually was. The vessel's crystal effects are
        /// broadcast, and on a remote peer the crystal has usually respawned elsewhere by the time
        /// they arrive; on a host the respawn can even land in the same physics step
        /// (<see cref="Crystal.CollectPose"/>). The same struct the Scarab's forged ball replicates.
        /// </summary>
        public CrystalForgeOrigin Origin;

        // 🔥 The factory method
        public static CrystalImpactData FromCrystal(Crystal crystal)
        {
            var pose = crystal.CollectPose;
            return new CrystalImpactData
            {
                Element = crystal.crystalProperties.Element,
                SpeedBuffAmount = crystal.crystalProperties.speedBuffAmount,
                Origin = new CrystalForgeOrigin
                {
                    CrystalId = crystal.Id,
                    Position = pose.position,
                    Rotation = pose.rotation,
                    Scale = crystal.CollectScale,
                    Valid = true,
                },
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
                Origin.NetworkSerialize(serializer);
            }
        }
    }
}