using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The claim book for gyroid octagon centres: which octagons already have (or are being
    /// grown toward) a crystal. One entry per octagon, claimed by the colony that owns it and
    /// released when that plant is destroyed - so a grazed-out octagon becomes plantable again,
    /// which is what lets neighbours recolonise a hole in the surface.
    ///
    /// <para>This is BOOKKEEPING, not a spatial store of prisms - the canonical spatial index of
    /// mass stays <c>PrismSpatialIndex</c> (Docs/SPATIAL_INDEX.md). Octagon centres are a
    /// handful of points spaced ≥36u apart; the registry exists so (a) two full parents cannot
    /// plant the same octagon in the same frame, and (b) the ownership gate can ask "whose
    /// territory is this site in" without a physics query (the 2026-04 attempt used
    /// <c>Physics.OverlapSphere</c> for exactly this and was structurally blind - prism
    /// colliders are disabled for 0.6s after spawn).</para>
    ///
    /// <para>Spatial hash with bin size = half the centre spacing; every query scans the 3³
    /// neighbouring bins, so lookups stay O(1) however large a colony grows.</para>
    ///
    /// <para>DELIBERATELY NOT SCALED by <c>AssembledFlora.LatticeScale</c>, unlike every other
    /// GyroidOctagonData distance (Docs/ECOSYSTEM.md §34.7). This is one book shared by every
    /// colony in the scene, so it cannot hold a per-plant scale - and it does not need one:
    /// <c>CenterDedupeRadius</c> (12u) only has to sit between the drift that makes two
    /// computations of the SAME centre disagree (~0.3u per 100u of lattice) and half the
    /// spacing between DISTINCT centres (17.9u at scale 1, 29.9u at the Space element's
    /// 1.667). It does, with room at both ends. A future element would break that only by
    /// shrinking its lattice below ~0.67× (distinct octagons falsely deduping) or widening it
    /// past ~40× (drift outgrowing the radius); scale the radius per query then, and raise
    /// <see cref="BinSize"/> with it, since the 3³ scan only guarantees a search radius of
    /// one bin.</para>
    /// </summary>
    public static class GyroidOctagonRegistry
    {
        const float BinSize = GyroidOctagonData.CenterSpacing * 0.5f;

        struct Claim
        {
            public Vector3 Center;
            public AssembledFlora Owner;
        }

        static readonly Dictionary<Vector3Int, List<Claim>> bins = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForDomainReload() => bins.Clear();

        static Vector3Int Key(Vector3 p) => new(
            Mathf.FloorToInt(p.x / BinSize),
            Mathf.FloorToInt(p.y / BinSize),
            Mathf.FloorToInt(p.z / BinSize));

        /// <summary>
        /// Claims an octagon centre for <paramref name="owner"/>. False when another claim
        /// already sits within <see cref="GyroidOctagonData.CenterDedupeRadius"/> - that octagon
        /// is taken (or two colonies' lattices disagree by less than the dedupe radius, which
        /// the same answer handles).
        /// </summary>
        public static bool TryClaim(Vector3 center, AssembledFlora owner)
        {
            if (IsClaimed(center)) return false;
            var key = Key(center);
            if (!bins.TryGetValue(key, out var list))
                bins[key] = list = new List<Claim>(2);
            list.Add(new Claim { Center = center, Owner = owner });
            return true;
        }

        /// <summary>
        /// Re-homes a claim onto the plant that now owns it (a parent claims its daughter's
        /// centre BEFORE spawning her, so a sibling can't race it; the daughter then adopts).
        /// </summary>
        public static void TransferClaim(Vector3 center, AssembledFlora newOwner)
        {
            var (list, index) = Find(center);
            if (list == null) return;
            var claim = list[index];
            claim.Owner = newOwner;
            list[index] = claim;
        }

        /// <summary>Releases a claim, but only for the plant that holds it.</summary>
        public static void Release(Vector3 center, AssembledFlora owner)
        {
            var (list, index) = Find(center);
            if (list == null) return;
            // `is null or ==` so a destroyed-but-non-null owner can still release during teardown.
            if (list[index].Owner is null || !list[index].Owner || list[index].Owner == owner)
                list.RemoveAt(index);
        }

        public static bool IsClaimed(Vector3 center) => Find(center).list != null;

        /// <summary>Live octagon claims - the colony's plant count, for diagnostics.</summary>
        public static int ClaimCount
        {
            get
            {
                int n = 0;
                foreach (var list in bins.Values) n += list.Count;
                return n;
            }
        }

        /// <summary>
        /// Squared distance from <paramref name="position"/> to the nearest claimed centre,
        /// excluding <paramref name="except"/>'s own. float.MaxValue when none is in range.
        /// The ownership gate's other half - see <c>AssembledFlora.OwnsLatticeSite</c>.
        /// </summary>
        public static float NearestForeignClaimSqr(Vector3 position, AssembledFlora except)
        {
            float best = float.MaxValue;
            var k = Key(position);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (!bins.TryGetValue(new Vector3Int(k.x + x, k.y + y, k.z + z), out var list))
                    continue;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Owner == except) continue;
                    float d = (list[i].Center - position).sqrMagnitude;
                    if (d < best) best = d;
                }
            }
            return best;
        }

        static (List<Claim> list, int index) Find(Vector3 center)
        {
            float dedupeSqr = GyroidOctagonData.CenterDedupeRadius * GyroidOctagonData.CenterDedupeRadius;
            var k = Key(center);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                if (!bins.TryGetValue(new Vector3Int(k.x + x, k.y + y, k.z + z), out var list))
                    continue;
                for (int i = 0; i < list.Count; i++)
                    if ((list[i].Center - center).sqrMagnitude < dedupeSqr)
                        return (list, i);
            }
            return (null, -1);
        }
    }
}
