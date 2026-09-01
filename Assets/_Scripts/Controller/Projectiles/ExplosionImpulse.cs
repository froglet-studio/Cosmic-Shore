using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a blast hands the mass it destroys: a magnitude (<see cref="Speed"/> x
    /// <see cref="Inertia"/>) applied along the unit direction the query already
    /// computed, plus the ceiling the resulting debris is clamped to.
    ///
    /// It travels as one value because the three are not independent. Debris speed is
    /// <c>min(Speed * Inertia, ceiling)</c>, and the AOE paths sit far above the
    /// explosion prefab's authored ceiling (33.33 u/s) - the Dolphin cone alone hands
    /// over ~222 u/s - so on the legacy contract EVERY blast saturates and
    /// <see cref="Inertia"/> is dead tuning: turn it up, nothing moves. Only a blast
    /// that supplies its OWN <see cref="DebrisSpeedLimit"/> in the same units (the
    /// true-velocity contract of <see cref="PrismEffectHelper.DamageProportional"/>)
    /// lets that dial reach the screen. Splitting them across separate parameters is
    /// what let them drift apart in the first place.
    ///
    /// <see cref="DebrisSpeedLimit"/> of 0 means "legacy": fall back to the explosion
    /// prefab's clamp, and accept that it flattens the magnitude.
    /// </summary>
    public readonly struct ExplosionImpulse
    {
        /// <summary>Blast-wave speed the impact vector is built from (u/s).</summary>
        public readonly float Speed;

        /// <summary>Designer gain on <see cref="Speed"/>. 1 = the wavefront's own speed.</summary>
        public readonly float Inertia;

        /// <summary>
        /// Per-impact ceiling in real speed units, replacing the explosion prefab's
        /// authored max. 0 keeps the prefab clamp (legacy, saturating).
        /// </summary>
        public readonly float DebrisSpeedLimit;

        public ExplosionImpulse(float speed, float inertia, float debrisSpeedLimit = 0f)
        {
            Speed = speed;
            Inertia = inertia;
            DebrisSpeedLimit = debrisSpeedLimit;
        }

        /// <summary>Impact vector along an already-normalized direction (Burst job output).</summary>
        public Vector3 Along(float3 unitDirection) => (Vector3)(unitDirection * (Speed * Inertia));

        /// <summary>Impact vector along an already-normalized direction.</summary>
        public Vector3 Along(Vector3 unitDirection) => unitDirection * (Speed * Inertia);
    }
}
