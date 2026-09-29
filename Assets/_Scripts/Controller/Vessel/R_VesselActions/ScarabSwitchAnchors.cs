using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where a Scarab's switch may be planted: <b>on a living plant's heart</b>.
    ///
    /// <para>This is a VESSEL capability, not a mode rule. A ring used to land wherever the nose
    /// pointed, which made "plant a ring in front of my own ball and nudge it through" one move —
    /// so Tollway first grew a set of mode-owned sockets to snap onto. Those were the wrong owner:
    /// they existed in one arena, taught a shape nothing else used, and had to be built, seeded,
    /// replicated and drawn by a controller. A flora crystal is the same affordance the ecology
    /// already produces everywhere — placed by the food web rather than by a designer, visible from
    /// a long way off, already a thing a pilot flies at, and grazeable, so the set of anchors is
    /// ALIVE. The Scarab now snaps onto one in every arena, and an arena that wants to be built on
    /// seeds flora (<c>Docs/ECOSYSTEM.md</c>) instead of shipping a bespoke socket system.</para>
    ///
    /// <para><b>Gate the PATH, never the projected point.</b> The ring is placed
    /// <c>placementDistance</c> AHEAD of the nose (150u), so a proximity test on that projected
    /// point is an ANNULUS, not a radius: it admits a press from 80–220u out and refuses every
    /// press inside 80, which is point-blank — exactly where a pilot following the objective arrow
    /// ends up. That shipped once and read as a dead button. The test here is the distance from a
    /// heart to the SEGMENT [ship, requested centre], so flying at a plant admits the press at
    /// every range from touching it to the far edge of reach.</para>
    ///
    /// <para><b>Determinism.</b> A press re-executes on every peer through the action handler's
    /// ClientRpc and nothing about a placed switch is replicated, so the anchor set has to agree
    /// across machines. Snapping to a DISCRETE set is more forgiving than the continuous placement
    /// it replaces — two peers whose vessel transforms differ by interpolation still pick the same
    /// plant unless the ship is near-equidistant between two of them — but only if the plants
    /// themselves agree. Flora are per-peer by default (every machine runs its own spawner off its
    /// own <c>UnityEngine.Random</c>), so <b>a mode that RELIES on the anchor must set
    /// <c>FloraConfigurationSO.NetworkSynced</c> on the species it seeds</b>, which replicates the
    /// planting decision — species, root pose, domain and element — and therefore the heart's world
    /// position too. Freestyle and Scramble do not rely on it: there the snap is an assist and a
    /// miss simply places free.</para>
    /// </summary>
    public static class ScarabSwitchAnchors
    {
        /// <summary>
        /// Two switch centres this close are on the same heart. Placement SNAPS exactly onto a
        /// heart position, so this is an equality test with slack for float drift — not a
        /// proximity radius. Using the reach here instead would let one planted ring silently
        /// close every neighbouring plant that happened to fall inside it.
        /// </summary>
        const float OccupiedEpsilon = 1f;

        /// <summary>
        /// The nearest FREE heart to the segment [<paramref name="from"/>, <paramref name="to"/>],
        /// if one lies within <paramref name="reach"/> of it.
        /// </summary>
        /// <param name="reach">How far off the flight path a heart may sit and still be claimable.
        /// 0 (or less) switches anchoring off entirely and always returns false.</param>
        /// <param name="position">The heart's world position — the ring's centre.</param>
        /// <param name="anchor">The plant the ring would be grafted onto.</param>
        public static bool TryResolve(Vector3 from, Vector3 to, float reach,
                                      out Vector3 position, out Flora anchor)
        {
            position = to;
            anchor = null;
            if (reach <= 0f) return false;

            anchor = FloraHeartRegistry.NearestToSegment(from, to, IsOccupied, out float distance);
            if (!anchor || distance > reach) { anchor = null; return false; }

            position = anchor.HeartTransform.position;
            return true;
        }

        /// <summary>Convenience overload for callers that only want the position.</summary>
        public static bool TryResolve(Vector3 from, Vector3 to, float reach, out Vector3 position)
            => TryResolve(from, to, reach, out position, out _);

        /// <summary>
        /// The nearest free heart to a POINT — for the HUD arrow, which has a pilot and no
        /// placement path. Returns the heart's TRANSFORM because <c>IObjectiveProvider</c>'s
        /// contract is a transform to point at, and because a plant that is grazed away should
        /// take the arrow with it rather than leave it aimed at a remembered position.
        /// </summary>
        public static Transform NearestFreeHeart(Vector3 from)
        {
            var flora = FloraHeartRegistry.NearestToPoint(from, IsOccupied);
            return flora ? flora.HeartTransform : null;
        }

        /// <summary>The nearest free heart's position, or <paramref name="fallback"/> when the
        /// arena holds none — an AI steering target, so it must always answer something.</summary>
        public static Vector3 NearestFreePosition(Vector3 from, Vector3 fallback)
        {
            var heart = NearestFreeHeart(from);
            return heart ? heart.position : fallback;
        }

        /// <summary>True when a live switch already stands on this plant's heart.</summary>
        public static bool IsOccupied(Flora flora)
        {
            if (!flora) return true;
            var heart = flora.HeartTransform;
            if (!heart) return true;

            Vector3 p = heart.position;
            var live = ScarabSwitch.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var sw = live[i];
                if (sw == null) continue;
                if ((sw.transform.position - p).sqrMagnitude <= OccupiedEpsilon * OccupiedEpsilon)
                    return true;
            }
            return false;
        }
    }
}
