using System;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where a forged object came from: WHICH crystal was spent on it, and the pose that crystal
    /// was standing in when it was spent. Replicated by <see cref="AstroLeagueBall"/> so a vessel's
    /// bespoke omni-crystal retirement plays on every peer.
    ///
    /// The POSE travels rather than being read back off the crystal, for two independent reasons —
    /// both of which have shipped as bugs:
    ///   • <b>Same frame.</b> A collect is serviced by two trigger callbacks in one physics step and
    ///     Unity does not order them; the crystal's own ends in a respawn that re-poses it
    ///     synchronously on a host. See <see cref="Crystal.CollectPose"/>.
    ///   • <b>Across the wire.</b> Collection and respawn are independent chains, so on a remote
    ///     peer the crystal has usually already moved on by the time the forged object arrives.
    /// A retirement that reads the live transform therefore starts at the crystal's NEXT home,
    /// wearing the respawn's identity rotation.
    ///
    /// <see cref="CrystalId"/> is <c>CellItem.Id</c> — the handle every peer can resolve
    /// the crystal through, which is what lets a client copy the crystal's own live renderers rather
    /// than rebuilding a look-alike. Zero is a legitimate id, so emptiness is <see cref="Valid"/>,
    /// never a sentinel id.
    /// </summary>
    public struct CrystalForgeOrigin : INetworkSerializable, IEquatable<CrystalForgeOrigin>
    {
        public int CrystalId;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public bool Valid;

        public Pose Pose => new(Position, Rotation);

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CrystalId);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Scale);
            serializer.SerializeValue(ref Valid);
        }

        public bool Equals(CrystalForgeOrigin other) =>
            Valid == other.Valid && CrystalId == other.CrystalId &&
            Position == other.Position && Rotation == other.Rotation && Scale == other.Scale;
    }
}
