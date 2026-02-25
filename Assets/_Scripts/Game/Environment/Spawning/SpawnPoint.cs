using UnityEngine;

namespace CosmicShore.Game.Environment.Spawning
{
    /// <summary>
    /// Immutable spatial data for a single spawned object.
    /// Cached by SpawnableBase to avoid regeneration when parameters are unchanged.
    /// </summary>
    [System.Serializable]
    public struct SpawnPoint
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public SpawnPoint(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public SpawnPoint(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
            Scale = Vector3.one;
        }

        public SpawnPoint(Vector3 position)
        {
            Position = position;
            Rotation = Quaternion.identity;
            Scale = Vector3.one;
        }

        /// <summary>
        /// Compute a rotation that looks from this point toward a target point.
        /// Returns Quaternion.identity if the direction is degenerate.
        /// </summary>
        public static Quaternion LookRotation(Vector3 from, Vector3 to, Vector3 up)
        {
            Vector3 forward = to - from;
            if (forward.sqrMagnitude < 0.0001f)
                return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized, up);
        }

        /// <summary>
        /// Compute a rotation that looks along a forward direction.
        /// Returns Quaternion.identity if the direction is degenerate.
        /// </summary>
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            if (forward.sqrMagnitude < 0.0001f)
                return Quaternion.identity;
            return Quaternion.LookRotation(forward.normalized, up);
        }
    }
}
