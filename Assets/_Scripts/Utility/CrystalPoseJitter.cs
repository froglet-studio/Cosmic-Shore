using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// A crystal's own random pose, so a field of depth-prism crystals (rows of pickups, a colony's hearts) does
    /// not read as one shared animation (Omni Shepard Lab round 10, <c>Docs/Studios/OMNI_SHEPARD_LAB.md</c>).
    /// <see cref="DepthRatchet"/> reads depth in the crystal's OWN space, so crystals that share an orientation
    /// and sit far from the camera see it from the same direction and show the same columns; turning each one
    /// to its own pose keeps the depth rule exact and breaks the sameness (measured: neighbours' rendered images
    /// correlate 0.60 at 250 u with every crystal alike, 0.12 with a random pose each).
    ///
    /// Seeded from the SPAWN POSITION, so every client turns a crystal spawned at the same place the same way
    /// without anything on the network (a heart riding a lifeform is seeded wherever its host stands that frame,
    /// so clients may disagree on it - cosmetic), and a pooled crystal respawned elsewhere takes a new pose.
    /// Pure maths, no Unity state.
    ///
    /// JS twin: <c>instRandom()</c> / <c>jitterOf()</c> in <c>Docs/Studios/OmniShepardLab.html</c> (the lab
    /// seeds from the crystal's index instead; the stream and the rotation it draws are the same). Change one,
    /// change both.
    /// </summary>
    public static class CrystalPoseJitter
    {
        /// <summary>
        /// A seed from a spawn position, quantized to a centimetre so the same spawn gives the same pose on every
        /// machine (float noise below 5 mm never changes it).
        /// </summary>
        public static uint SeedOf(Vector3 position)
        {
            unchecked
            {
                uint x = (uint)Mathf.RoundToInt(position.x * 100f);
                uint y = (uint)Mathf.RoundToInt(position.y * 100f);
                uint z = (uint)Mathf.RoundToInt(position.z * 100f);
                uint h = x * 0x8DA6B343u ^ y * 0xD8163841u ^ z * 0xCB1AB31Fu;
                // murmur3's finalizer: nearby spawns land far apart.
                h ^= h >> 16; h *= 0x85EBCA6Bu; h ^= h >> 13; h *= 0xC2B2AE35u; h ^= h >> 16;
                return h;
            }
        }

        /// <summary>
        /// The pose for <paramref name="seed"/>: a uniformly random rotation (Shoemake's quaternion from the
        /// first three draws of <see cref="Mulberry32"/>), turned <paramref name="amount"/> of the way from
        /// identity about its own axis (0 = the authored pose, 1 = uniformly random).
        /// </summary>
        public static Quaternion Pose(uint seed, float amount)
        {
            uint state = seed;
            double u1 = Mulberry32(ref state), u2 = Mulberry32(ref state), u3 = Mulberry32(ref state);
            double a = System.Math.Sqrt(1.0 - u1), b = System.Math.Sqrt(u1);
            double qx = a * System.Math.Sin(2.0 * System.Math.PI * u2), qy = a * System.Math.Cos(2.0 * System.Math.PI * u2);
            double qz = b * System.Math.Sin(2.0 * System.Math.PI * u3), qw = b * System.Math.Cos(2.0 * System.Math.PI * u3);
            double sign = qw < 0.0 ? -1.0 : 1.0;
            double angle = 2.0 * System.Math.Acos(System.Math.Min(1.0, System.Math.Abs(qw))) * Mathf.Clamp01(amount);
            double len = System.Math.Sqrt(qx * qx + qy * qy + qz * qz);
            if (angle <= 0.0 || len < 1e-12) return Quaternion.identity;
            double s = System.Math.Sin(angle * 0.5) * sign / len;
            return new Quaternion((float)(qx * s), (float)(qy * s), (float)(qz * s), (float)System.Math.Cos(angle * 0.5));
        }

        /// <summary>One draw in [0, 1) - Tommy Ettinger's mulberry32, bit-for-bit the lab's <c>mulberry32</c>.</summary>
        public static double Mulberry32(ref uint state)
        {
            unchecked
            {
                state += 0x6D2B79F5u;
                uint t = (state ^ (state >> 15)) * (1u | state);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }
}
