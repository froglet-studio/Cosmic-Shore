using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The pure geometry behind the spherical-blast omni-crystal morph (Butterfly, Rhino, Manta:
    /// <c>R_VesselActions/SPHERE_BLAST_CRYSTAL_MORPH.md</c>). Kept free of any scene object so the
    /// headless harness (<c>Tools/Build/crystal_morph_harness</c>) runs it against the shipped
    /// builder.
    ///
    /// Two questions, each with one answer the blast already owns:
    /// <list type="bullet">
    /// <item><b>How big is the sphere at the hand-off?</b> <see cref="EaseAt"/> is the same curve
    /// <c>AOEExplosion.ExplodeAsync</c> runs — nothing for <c>ExplosionDelay</c>, then
    /// <c>sin(t·π/2)</c> of full size over <c>ExplosionDuration</c>.</item>
    /// <item><b>Where is the cage cast from?</b> <see cref="CastCentre"/>. The blast is centred on
    /// the HULL (plus the effect's spawn offset), not on the crystal, and a large sphere seen from
    /// its own centre puts every point of a small crystal in nearly the same direction — the plain
    /// cast would pile the whole cage onto one patch. Casting from the crystal's own middle fans
    /// the cage over the whole sphere, the way it faces; any point strictly inside a convex hull
    /// is a valid origin for the ray cast.</item>
    /// </list>
    /// </summary>
    public static class SphereBlastMorphGeometry
    {
        /// <summary>
        /// How far inside the sphere the cast origin may sit, as a fraction of its radius. Below 1
        /// so the origin is strictly interior (the ray cast needs it), and well below so the cage's
        /// far side still has room to open: at 0.5 the nearest facet is half a radius away.
        /// </summary>
        public const float MaxCastOffsetFraction = 0.5f;

        /// <summary>The blast's growth at <paramref name="secondsSinceInitialize"/>: 0 through the
        /// delay, then <c>sin(t·π/2)</c> to 1 over the duration, held at 1 after.</summary>
        public static float EaseAt(float secondsSinceInitialize, float delay, float duration)
        {
            float grown = Mathf.Max(0f, secondsSinceInitialize - Mathf.Max(0f, delay));
            float t = duration > 0f ? Mathf.Min(grown / duration, 1f) : 1f;
            return Mathf.Sin(t * Mathf.PI * 0.5f);
        }

        /// <summary>
        /// The ray-cast origin: the crystal's centre when it is well inside the sphere, otherwise
        /// the point on the line from the sphere's centre toward the crystal at
        /// <see cref="MaxCastOffsetFraction"/> of the radius. Degrades to the sphere's centre for a
        /// sphere with no size.
        /// </summary>
        public static Vector3 CastCentre(Vector3 sphereCentre, float sphereRadius, Vector3 crystalCentre)
        {
            if (!(sphereRadius > 0f)) return sphereCentre;
            Vector3 offset = crystalCentre - sphereCentre;
            float max = sphereRadius * MaxCastOffsetFraction;
            float len = offset.magnitude;
            if (len <= max) return crystalCentre;
            return sphereCentre + offset * (max / len);
        }
    }
}
