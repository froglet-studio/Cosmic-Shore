using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure arithmetic of the Stoat's annihilation ripple (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1c), held by
    /// <c>StoatRippleTests</c>: how big a ripple a pair throws, where its front is, and who it is allowed to strip.
    /// </summary>
    public static class StoatRippleMath
    {
        /// <summary>
        /// The ripple's full radius: <paramref name="separation"/> × <paramref name="perSeparation"/>, clamped to
        /// [<paramref name="min"/>, <paramref name="max"/>], × <paramref name="charge"/> (≥ 0; the config's floor holds it at half in the deficit band).
        /// </summary>
        public static float Radius(float separation, float perSeparation, float min, float max, float charge) =>
            Mathf.Clamp(Mathf.Max(0f, separation) * Mathf.Max(0f, perSeparation), Mathf.Max(0f, min), Mathf.Max(min, max))
            * Mathf.Max(0f, charge);

        /// <summary>
        /// Where the front is <paramref name="age"/> seconds after birth: an ease-out (1 − (1 − t)³) to
        /// <paramref name="radius"/> over <paramref name="expandSeconds"/> — fast at the meeting point, settling at its
        /// reach — then held there.
        /// </summary>
        public static float Front(float age, float expandSeconds, float radius)
        {
            float t = Mathf.Clamp01(age / Mathf.Max(1e-3f, expandSeconds));
            float u = 1f - t;
            return Mathf.Max(0f, radius) * (1f - u * u * u);
        }

        /// <summary>
        /// The drawn wave's strength 0..1: full while it expands, then fading to nothing over
        /// <paramref name="fadeSeconds"/>.
        /// </summary>
        public static float Strength(float age, float expandSeconds, float fadeSeconds)
        {
            if (age <= expandSeconds) return 1f;
            if (fadeSeconds <= 0f) return 0f;
            return Mathf.Clamp01(1f - (age - expandSeconds) / fadeSeconds);
        }

        /// <summary>
        /// The drawn wave's shape across its front: <paramref name="u"/> is how many wavelengths a point is BEHIND the
        /// front (0 on it, negative ahead of it). A sine that rises sharply at the front and rings down over about three
        /// trailing crests; nothing ahead of the front, nothing past four wavelengths. Mirrors <c>StoatRippleProfile</c> in
        /// <c>StoatRipple.shader</c> - change both together.
        /// </summary>
        public static float Profile(float u)
        {
            if (u < -1.5f || u > 4f) return 0f;
            float envelope = u < 0f ? Mathf.Exp(-6f * u * u) : Mathf.Exp(-0.45f * u * u);
            return Mathf.Sin(2f * Mathf.PI * u) * envelope;
        }

        /// <summary>
        /// How far the wave bends light at its front, radians: <paramref name="bend"/> while the front is inside
        /// <paramref name="nearRadius"/>, then falling as nearRadius / front (a spreading wave thins as 1/r), × the
        /// wave's <paramref name="strength"/> (<see cref="Strength"/>).
        /// </summary>
        public static float Amplitude(float bend, float front, float nearRadius, float strength) =>
            Mathf.Max(0f, bend) * Mathf.Clamp01(strength) * Mathf.Min(1f, Mathf.Max(1e-3f, nearRadius) / Mathf.Max(1e-3f, front));

        /// <summary>True once the wave has nothing left to draw or do.</summary>
        public static bool Done(float age, float expandSeconds, float fadeSeconds) => age >= expandSeconds + Mathf.Max(0f, fadeSeconds);

        /// <summary>
        /// Whether the ripple may strip a vessel: never its owner, never a teammate (same domain), never a pilot whose
        /// domain is not known yet.
        /// </summary>
        public static bool Strips(bool isOwner, bool domainKnown, Domains vesselDomain, Domains ownerDomain) =>
            !isOwner && domainKnown && vesselDomain != ownerDomain;
    }
}
