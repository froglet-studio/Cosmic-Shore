using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every LIVING plant's heart, in one flat list.
    ///
    /// <para><b>Why this exists.</b> A flora crystal is the platform's one naturally-occurring
    /// "point of interest you can build on" — it is placed by the ecology rather than by a mode,
    /// it is visible from a long way off, it is already a thing a pilot flies at, and it is
    /// grazeable, so the set of them is alive rather than authored. The Scarab's switch anchors on
    /// one (<see cref="ScarabSwitchAnchors"/>), which is what turns "plant a ring wherever the nose
    /// points" into a read of the world. The <see cref="Cell"/> already counts live plants
    /// per-species (<c>liveFloraCounts</c>) but holds no positions, and a per-species dictionary
    /// cannot answer "the nearest heart to this line" without walking every species; this is the
    /// flat list that can.</para>
    ///
    /// <para><b>Live, not cached.</b> Entries are <see cref="Flora"/> references and a query reads
    /// <c>flora.HeartTransform.position</c> at the moment it is asked — an
    /// <c>AssembledFlora</c> moves its crystal onto its lattice site AFTER seating it, and a plant
    /// on a moving container would drag its heart along, so a cached position would be wrong for
    /// two independent reasons.</para>
    ///
    /// <para><b>Membership is the plant's LIFE, not its crystal's existence.</b> A plant registers
    /// when it is initialized and leaves on death (the heart is released to be collected — it is no
    /// longer a heart) and on destruction. Nothing here spawns, moves, ages or removes a prism or a
    /// crystal: it is an index over what the ecology already made.</para>
    ///
    /// <para>Static because there is at most one simulation running and because the query has to be
    /// answerable from a vessel that holds no reference to any cell. Cleared defensively on the
    /// first registration of a new scene is NOT attempted — entries are unregistered by their own
    /// <c>OnDestroy</c>, and a stale destroyed entry is pruned lazily by every query, so a scene
    /// change cannot leak.</para>
    /// </summary>
    public static class FloraHeartRegistry
    {
        static readonly List<Flora> s_live = new();

        /// <summary>Every living plant with a heart. May contain destroyed entries between a
        /// scene teardown and the next query; every accessor here prunes them.</summary>
        public static IReadOnlyList<Flora> Live => s_live;

        public static void Register(Flora flora)
        {
            if (!flora || s_live.Contains(flora)) return;
            s_live.Add(flora);
        }

        public static void Unregister(Flora flora)
        {
            // `is null` rather than the Unity truth test: a destroyed plant must still be removed.
            if (flora is null) return;
            s_live.Remove(flora);
        }

        /// <summary>
        /// The heart nearest to the SEGMENT <paramref name="a"/>→<paramref name="b"/>, with its
        /// distance to that segment. Segment rather than point because the callers ask about a
        /// FLIGHT PATH, not a location (see <see cref="ScarabSwitchAnchors"/> for why that
        /// distinction is the difference between a radius and an annulus).
        ///
        /// <para><paramref name="reject"/> lets a caller skip hearts it considers unavailable
        /// without this class learning what "unavailable" means to it.</para>
        /// </summary>
        public static Flora NearestToSegment(Vector3 a, Vector3 b, System.Predicate<Flora> reject,
                                             out float distance)
        {
            Prune();

            Flora best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < s_live.Count; i++)
            {
                var flora = s_live[i];
                var heart = flora.HeartTransform;
                if (!heart) continue;
                if (reject != null && reject(flora)) continue;

                float sqr = SqrDistanceToSegment(heart.position, a, b);
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = flora;
            }

            distance = best ? Mathf.Sqrt(bestSqr) : float.MaxValue;
            return best;
        }

        /// <summary>
        /// Squared distance from <paramref name="p"/> to the SEGMENT [a, b]. Pure, and public so
        /// the rule it encodes can be tested without standing up a plant: measuring to the segment
        /// rather than to its far ENDPOINT is the whole difference between a reach and an annulus
        /// with point-blank in the hole (<see cref="ScarabSwitchAnchors"/>), and that difference
        /// shipped once as a placement rule nobody could satisfy at close range.
        /// </summary>
        public static float SqrDistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            float t = lengthSqr > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / lengthSqr) : 0f;
            return (p - (a + ab * t)).sqrMagnitude;
        }

        /// <summary>Distance from <paramref name="p"/> to the segment [a, b].</summary>
        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
            => Mathf.Sqrt(SqrDistanceToSegment(p, a, b));

        /// <summary>The heart nearest to a POINT, for a caller that has no path (the HUD arrow).</summary>
        public static Flora NearestToPoint(Vector3 from, System.Predicate<Flora> reject)
            => NearestToSegment(from, from, reject, out _);

        static void Prune()
        {
            for (int i = s_live.Count - 1; i >= 0; i--)
                if (!s_live[i]) s_live.RemoveAt(i);
        }
    }
}
