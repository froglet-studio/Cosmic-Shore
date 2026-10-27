using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Prism movement's SHIELD rule (Garrett, 2026-10-27; Docs/PRISM_MOTION.md § "Shielded prisms bounce"): a collision a
    /// shielded prism is part of is a BOUNCE, not an annihilation. Neither prism is destroyed, and whatever was moving
    /// rebounds off the contact. Super-shielded mass keeps its own rule: it never moves and is never destroyed, a SHIELDED
    /// mover bounces off it, and an unshielded mover still goes.
    ///
    /// <para>The pure arithmetic every mover shares (the gravity field, the Rhino plough, the Jacks net), so they keep one
    /// rule. <see cref="PrismMotion.Bounces(Prism, Prism)"/> reads the two prisms' shields into <see cref="Bounces"/>, and
    /// <c>PrismMotionConfigSO.ShieldBounceRestitution</c> is the one tuning number.</para>
    /// </summary>
    public static class PrismBounce
    {
        /// <summary>The restitution when no <c>PrismMotionConfig</c> asset is found.</summary>
        public const float DefaultRestitution = 0.6f;

        /// <summary>
        /// Whether a mover running into a prism bounces: yes when either is shielded, except that super-shielded mass only
        /// returns a SHIELDED mover (an unshielded one is destroyed against it, as before).
        /// </summary>
        public static bool Bounces(bool moverShielded, bool struckShielded, bool struckSuperShielded) =>
            struckSuperShielded ? moverShielded : moverShielded || struckShielded;

        /// <summary>
        /// The contact normal, pointing from the struck prism's centre to the mover's. When the two centres coincide it is
        /// the way the mover came from (minus <paramref name="closingVelocity"/>), else world up.
        /// </summary>
        public static Vector3 Normal(Vector3 moverCentre, Vector3 struckCentre, Vector3 closingVelocity)
        {
            var n = moverCentre - struckCentre;
            if (n.sqrMagnitude > 1e-8f) return n.normalized;
            return closingVelocity.sqrMagnitude > 1e-8f ? -closingVelocity.normalized : Vector3.up;
        }

        /// <summary>
        /// The mover's change of velocity in a bounce: <paramref name="relativeVelocity"/> is the mover's velocity relative
        /// to the struck prism, <paramref name="normal"/> points from the struck prism to the mover, and
        /// <paramref name="share"/> is the mover's part of the impulse: 1 against a prism that is not moving (it keeps still),
        /// ½ between two moving bodies (equal mass), which then take equal and opposite changes. Zero when the two are not
        /// closing along the normal.
        /// </summary>
        public static Vector3 Impulse(Vector3 relativeVelocity, Vector3 normal, float restitution, float share)
        {
            float closing = Vector3.Dot(relativeVelocity, normal);
            if (closing >= 0f) return Vector3.zero;
            return -(1f + Mathf.Clamp01(restitution)) * Mathf.Clamp01(share) * closing * normal;
        }

        /// <summary>
        /// A one-shot mover (the Rhino plough's throw, <paramref name="throwStep"/>) that met a prism at
        /// <paramref name="contactPoint"/>, a fraction <paramref name="t"/> of the way: where the rest of the throw ends,
        /// mirrored off the contact <paramref name="normal"/> and shortened by the restitution.
        /// </summary>
        public static Vector3 Rebound(Vector3 contactPoint, Vector3 throwStep, float t, Vector3 normal, float restitution)
        {
            var rest = throwStep * (1f - Mathf.Clamp01(t));
            var mirrored = rest - 2f * Vector3.Dot(rest, normal) * normal;
            return contactPoint + mirrored * Mathf.Clamp01(restitution);
        }
    }
}
