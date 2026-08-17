using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Is this vessel reference still usable?" — the one place that question is answered.
    ///
    /// <b>`vessel != null` IS NOT ENOUGH, and never was.</b> <see cref="IVessel"/> and
    /// <see cref="IVesselStatus"/> are INTERFACES, and `==` / `!=` on an interface reference is a
    /// plain C# comparison. It never reaches <c>UnityEngine.Object</c>'s overloaded operator — the
    /// thing that reports a destroyed object as null — so a destroyed <c>VesselController</c> or
    /// <c>VesselStatus</c> sails straight through the guard and throws
    /// <c>MissingReferenceException</c> on the first member access.
    ///
    /// This bites specifically around VESSEL SWAPS and despawns, where a reference captured before
    /// the swap is used after it: the Astro League ball sampling velocities from the roster, and
    /// the vessel-changer toy handing control back once a swap settles (which throws on every
    /// failed swap, because the outgoing hull is already gone).
    ///
    /// Route every such check through here rather than writing a fourth copy — the interface trap
    /// is exactly the kind of thing that gets fixed at one call site and forgotten at the next.
    /// </summary>
    public static class VesselLiveness
    {
        /// <summary>True while <paramref name="vessel"/> is non-null AND its underlying Unity
        /// object has not been destroyed.</summary>
        public static bool IsAlive(this IVessel vessel)
            => vessel != null && !(vessel is Object o && !o);

        /// <summary>True while <paramref name="status"/> is non-null AND its underlying Unity
        /// object has not been destroyed.</summary>
        public static bool IsAlive(this IVesselStatus status)
            => status != null && !(status is Object o && !o);

        /// <summary>The vessel's root transform, or null if it has been destroyed. Convenience for
        /// the roster walks that only want a position.</summary>
        public static Transform LiveTransform(this IVessel vessel)
        {
            if (!vessel.IsAlive()) return null;
            var root = vessel.Transform;
            return root ? root : null;
        }
    }
}
