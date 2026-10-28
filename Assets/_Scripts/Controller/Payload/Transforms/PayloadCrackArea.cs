using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A payload transform's crack over a sphere: every rival shield in it runs <see cref="SuperShieldCrack.TryCrack"/>
    /// (so a silenced carrier's crack deflects and its own domain's mass is spared). One query on the spatial index.
    /// Hydra's heads and Aftershock's shocks crack through here.
    /// </summary>
    public static class PayloadCrackArea
    {
        static readonly List<Prism> s_scratch = new(256);

        /// <summary>Crack every rival shield within <paramref name="radius"/> of <paramref name="at"/>; returns how many
        /// super-shields came down. <paramref name="leaveRegularShield"/> false shatters: no shield is left behind.</summary>
        public static int Crack(Vector3 at, float radius, string carrierName, Domains carrierDomain,
            bool leaveRegularShield = true)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || radius <= 0f) return 0;
            index.QuerySphere(at, radius, s_scratch);
            int cracked = 0;
            for (int i = 0; i < s_scratch.Count; i++)
            {
                var prism = s_scratch[i];
                if (!prism || prism.destroyed || prism.IsCarried) continue;
                if (SuperShieldCrack.TryCrack(prism, carrierName, carrierDomain, alsoShielded: true,
                        leaveRegularShield) == SuperShieldCrack.Outcome.CrackedSuperShield)
                    cracked++;
            }
            s_scratch.Clear();
            return cracked;
        }

        /// <summary>
        /// The rival super-shielded prism within <paramref name="range"/> of <paramref name="from"/> that lies nearest to
        /// <paramref name="heading"/> (inside <paramref name="maxDegrees"/> of it), or null. A seeking head's target.
        /// </summary>
        public static Prism NearestAhead(Vector3 from, Vector3 heading, float range, float maxDegrees, Domains spare)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null) return null;
            index.QuerySphere(from, range, s_scratch);
            Prism best = null;
            float bestAngle = maxDegrees;
            for (int i = 0; i < s_scratch.Count; i++)
            {
                var prism = s_scratch[i];
                if (!prism || prism.destroyed || prism.IsCarried || prism.Domain == spare) continue;
                if (prism.prismProperties is not { IsSuperShielded: true }) continue;
                float angle = Vector3.Angle(heading, prism.transform.position - from);
                if (angle > bestAngle) continue;
                bestAngle = angle;
                best = prism;
            }
            s_scratch.Clear();
            return best;
        }
    }
}
