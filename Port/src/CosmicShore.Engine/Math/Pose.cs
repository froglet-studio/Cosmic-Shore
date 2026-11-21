using System;

namespace CosmicShore.Engine
{
    /// <summary>Position + rotation pair (spawn points, teleport targets).</summary>
    [Serializable]
    public struct Pose : IEquatable<Pose>
    {
        public Vector3 position;
        public Quaternion rotation;

        public Pose(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }

        public static Pose identity => new(Vector3.zero, Quaternion.identity);

        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 right => rotation * Vector3.right;

        public bool Equals(Pose other) => position.Equals(other.position) && rotation.Equals(other.rotation);
        public override bool Equals(object obj) => obj is Pose other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(position, rotation);
        public override string ToString() => $"({position}, {rotation})";

        public static bool operator ==(Pose a, Pose b) => a.position == b.position && a.rotation == b.rotation;
        public static bool operator !=(Pose a, Pose b) => !(a == b);
    }
}
