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
        /// The crystal's full POSE when it was collected — position, orientation and world scale.
        ///
        /// Carried rather than read back off the crystal for two independent reasons, and both
        /// have bitten:
        ///   • <b>Same frame.</b> A collect is serviced by two trigger callbacks in one physics
        ///     step and Unity does not order them; the crystal's own ends in a respawn that
        ///     re-poses it synchronously on a host. See <see cref="Crystal.CollectPose"/>.
        ///   • <b>Across the wire.</b> Collection and respawn are independent RPC chains, so on
        ///     a remote peer the crystal has usually already moved on by the time these effects
        ///     arrive.
        /// A retirement animation that reads the live transform therefore starts at the
        /// crystal's NEXT home, wearing the respawn's identity rotation.
        /// </summary>
        public Vector3 Position;
        /// <summary>Orientation to match <see cref="Position"/>. A respawn resets this to
        /// identity, so it cannot be recovered afterwards.</summary>
        public Quaternion Rotation;
        /// <summary>World scale to match <see cref="Position"/>.</summary>
        public Vector3 Scale;

        // 🔥 The factory method
        public static CrystalImpactData FromCrystal(Crystal crystal)
        {
            var pose = crystal.CollectPose;
            return new CrystalImpactData
            {
                Element = crystal.crystalProperties.Element,
                SpeedBuffAmount = crystal.crystalProperties.speedBuffAmount,
                CrystalId = crystal.Id,
                Position = pose.position,
                Rotation = pose.rotation,
                Scale = crystal.CollectScale,
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
                serializer.SerializeValue(ref Rotation);
                serializer.SerializeValue(ref Scale);
            }
        }
    }
}