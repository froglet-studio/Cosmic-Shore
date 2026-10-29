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

        /// <summary>
        /// Spend a parcel in its carrier's name: each prism bursts outward from <paramref name="release"/>'s centre as
        /// ordinary destruction (a vessel ability is an active sink, Docs/ECOSYSTEM.md), which is also the burst's
        /// picture. Empties <paramref name="parcel"/>.
        ///
        /// <para>Every prism leaves the parcel's root for <paramref name="restParent"/> first: the root is destroyed
        /// after a spend, and a pooled prism destroyed with a hierarchy corrupts its pool. A prism the spend could not
        /// destroy (a silenced carrier's: the silence gate deflects even its own cargo) goes straight back to the pool.</para>
        /// </summary>
        public static void SpendParcel(List<Prism> parcel, in PayloadRelease release, Transform restParent)
        {
            for (int i = 0; i < parcel.Count; i++)
            {
                var prism = parcel[i];
                if (!prism) continue;
                prism.transform.SetParent(restParent, true);
                if (prism.destroyed) continue;
                prism.SetCarried(false);
                if (prism.prismProperties is { IsSuperShielded: true }) prism.DeactivateShields();
                var outward = prism.transform.position - release.Center;
                if (outward.sqrMagnitude < 0.01f) outward = release.Forward;
                prism.Damage(outward.normalized * 40f, release.CarrierDomain, release.CarrierName, devastate: true);
                if (!prism.destroyed && !EnvironmentPrismPool.TryRelease(prism)) Object.Destroy(prism.gameObject);
            }
            parcel.Clear();
        }
    }
}
