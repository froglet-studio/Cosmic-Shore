using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>One released payload: where it burst, how far it reaches, and whose it was.</summary>
    public readonly struct PayloadRelease
    {
        public readonly Vector3 Center;
        public readonly Vector3 Forward;
        public readonly float Radius;
        public readonly string CarrierName;
        public readonly Domains CarrierDomain;

        public PayloadRelease(Vector3 center, Vector3 forward, float radius, string carrierName, Domains carrierDomain)
        {
            Center = center;
            Forward = forward;
            Radius = radius;
            CarrierName = carrierName;
            CarrierDomain = carrierDomain;
        }
    }

    /// <summary>
    /// The burst: every live prism within the release's radius gets each effect in turn. Pure
    /// dispatch over the spatial index (no colliders, no physics query), so a burst costs one
    /// sphere query however much mass it reaches.
    /// </summary>
    public static class PayloadBurst
    {
        static readonly List<Prism> s_scratch = new(256);

        /// <summary>Run <paramref name="effects"/> on every prism in the burst; returns how many
        /// prisms the burst reached (whatever the effects then did to them).</summary>
        public static int Detonate(in PayloadRelease release, IReadOnlyList<PayloadPrismEffectSO> effects)
        {
            if (effects == null || effects.Count == 0) return 0;
            var index = PrismSpatialIndex.Instance;
            if (index == null) return 0;

            index.QuerySphere(release.Center, release.Radius, s_scratch);
            int reached = 0;
            for (int i = 0; i < s_scratch.Count; i++)
            {
                var prism = s_scratch[i];
                // The snapshot can go stale under the effects' own side effects; and a parcel still
                // riding another carrier is cargo, not the arena.
                if (!prism || prism.destroyed || prism.IsCarried) continue;
                reached++;
                for (int e = 0; e < effects.Count; e++)
                    if (effects[e]) effects[e].Execute(release, prism);
            }
            s_scratch.Clear();
            return reached;
        }
    }
}
