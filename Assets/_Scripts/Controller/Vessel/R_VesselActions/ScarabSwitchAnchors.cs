using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Where a Scarab's switch may be planted: <b>around an ELEMENTAL crystal</b> (Charge, Mass,
    /// Space or Time - never an omni), in ANY state.
    ///
    /// <para><b>A rule of the Scarab's kit, in every game (Garrett, 2026-10-27).</b> "Have the
    /// scarab's ability to make a switch always (in all games, and locked to its kit) tied to
    /// elemental crystals regardless of state." So a ring has no free placement: a press with no
    /// elemental crystal on its path is refused, the same as a press with no charge banked. No
    /// mode installs this and no mode can switch it off - <see cref="TryResolve"/> is called by the
    /// vessel's own executor, before any mode's <c>PlacementResolver</c> sees the press.</para>
    ///
    /// <para><b>"Regardless of state" means every elemental crystal in the scene</b>
    /// (<see cref="Crystal.Active"/>): a free-floating pickup, a flora heart, and a heart still
    /// embedded INSIDE a living fauna (<see cref="Crystal.IsEmbedded"/>), which is inactive as a
    /// pickup but is still a crystal. The one exclusion is a crystal mid-burst
    /// (<see cref="Crystal.IsExploding"/>) - it is already leaving the world. This supersedes the
    /// 2026-09 flora-heart rule; every flora heart is an elemental crystal, so every anchor the old
    /// rule offered is still offered.</para>
    ///
    /// <para><b>The ring is placed where the crystal IS at the press.</b> It does not follow a
    /// fauna that carries its crystal away; a switch is a fixed structure (its dais is laid in
    /// place), and following would need a replicated transform a switch does not have.</para>
    ///
    /// <para><b>Gate the PATH, never the projected point.</b> The ring is projected
    /// <c>placementDistance</c> AHEAD of the nose (150u), so a proximity test on that point is an
    /// ANNULUS that refuses every point-blank press. The test is the distance from a crystal to the
    /// SEGMENT [ship, requested centre], so flying at a crystal admits the press at every range from
    /// touching it to the far edge of reach.</para>
    ///
    /// <para><b>Determinism.</b> A press re-executes on every peer through the action handler's
    /// ClientRpc and nothing about a placed switch is replicated, so the anchor set has to agree
    /// across machines. Snapping to a DISCRETE set tolerates interpolation differences, but only if
    /// the crystals themselves agree: network-synced crystals and flora (<c>FloraConfigurationSO.
    /// NetworkSynced</c>) do; per-peer fauna hearts can differ by a few units, and a press right on
    /// the edge of reach can then build on one machine and not another. A mode that RELIES on the
    /// anchor (Tollway) seeds network-synced flora.</para>
    /// </summary>
    public static class ScarabSwitchAnchors
    {
        /// <summary>
        /// Two switch centres this close are on the same heart. Placement SNAPS exactly onto a
        /// crystal's position, so this is an equality test with slack for float drift — not a
        /// proximity radius. Using the reach here instead would let one planted ring silently
        /// close every neighbouring plant that happened to fall inside it.
        /// </summary>
        const float OccupiedEpsilon = 1f;

        /// <summary>
        /// The nearest FREE elemental crystal to the segment [<paramref name="from"/>,
        /// <paramref name="to"/>], if one lies within <paramref name="reach"/> of it.
        /// </summary>
        /// <param name="reach">How far off the flight path a crystal may sit and still be
        /// claimable. Clamped to at least <see cref="MinimumReach"/>: anchoring is part of the kit
        /// and cannot be switched off by authoring a 0.</param>
        /// <param name="position">The crystal's world position - the ring's centre.</param>
        /// <param name="anchor">The crystal the ring is placed around.</param>
        public static bool TryResolve(Vector3 from, Vector3 to, float reach,
                                      out Vector3 position, out Crystal anchor)
        {
            position = to;
            anchor = NearestToSegment(from, to, out float distance);
            if (!anchor || distance > Mathf.Max(MinimumReach, reach)) { anchor = null; return false; }

            position = anchor.transform.position;
            return true;
        }

        /// <summary>Convenience overload for callers that only want the position.</summary>
        public static bool TryResolve(Vector3 from, Vector3 to, float reach, out Vector3 position)
            => TryResolve(from, to, reach, out position, out _);

        /// <summary>The smallest reach a placement is ever tested at.</summary>
        public const float MinimumReach = 10f;

        /// <summary>
        /// The nearest free anchor to a POINT - for the HUD arrow, which has a pilot and no
        /// placement path. Returns the crystal's TRANSFORM because <c>IObjectiveProvider</c>'s
        /// contract is a transform to point at, and a crystal that is collected or carried off
        /// takes the arrow with it.
        /// </summary>
        public static Transform NearestFreeHeart(Vector3 from)
        {
            var crystal = NearestToSegment(from, from, out _);
            return crystal ? crystal.transform : null;
        }

        /// <summary>The nearest free anchor's position, or <paramref name="fallback"/> when the
        /// arena holds none - an AI steering target, so it must always answer something.</summary>
        public static Vector3 NearestFreePosition(Vector3 from, Vector3 fallback)
        {
            var heart = NearestFreeHeart(from);
            return heart ? heart.position : fallback;
        }

        /// <summary>True for a crystal a switch may be placed around: elemental (not omni, not
        /// unset), not mid-burst. Its state - free, a flora heart, or embedded in a fauna - does not
        /// matter.</summary>
        public static bool IsAnchorable(Crystal crystal) =>
            crystal && crystal.isActiveAndEnabled && !crystal.IsExploding &&
            crystal.crystalProperties.IsElemental;

        static Crystal NearestToSegment(Vector3 a, Vector3 b, out float distance)
        {
            Crystal best = null;
            float bestSqr = float.MaxValue;
            var live = Crystal.Active;
            for (int i = 0; i < live.Count; i++)
            {
                var crystal = live[i];
                if (!IsAnchorable(crystal)) continue;
                Vector3 p = crystal.transform.position;
                float sqr = FloraHeartRegistry.SqrDistanceToSegment(p, a, b);
                if (sqr >= bestSqr || IsOccupied(p)) continue;
                bestSqr = sqr;
                best = crystal;
            }
            distance = best ? Mathf.Sqrt(bestSqr) : float.MaxValue;
            return best;
        }

        /// <summary>True when a live switch already stands on this point.</summary>
        public static bool IsOccupied(Vector3 p)
        {
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
