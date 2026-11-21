using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Decides which loose prisms a Serpent seed wall steals in one sweep, and into which lattice
    /// sites (design: R_VesselActions/SERPENT_SEED_WALL.md, "Growth"). Pure: no Unity objects, no
    /// prisms, so it is tested headless.
    ///
    /// <para><b>The wall steals from its EDGE.</b> Only an open site next to a standing (or
    /// in-flight) brick can take a prism, and only a prism within the steal radius of that site.
    /// So the wall grows toward the mass around it, along a trail it is eating, instead of as a
    /// uniform disc (the first lattice cut ranked sites by distance from the seed and searched the
    /// whole neighbourhood for each one; Garrett, 2026-10-13, asked for the old wall's edge-steal
    /// back).</para>
    ///
    /// <para><b>Many thefts, few updates.</b> The wall calls this a few times a second, not every
    /// frame. One call runs up to <c>maxWaves</c> waves: each wave pairs every edge site with its
    /// nearest unclaimed prism, grants the pairs nearest-first (a prism goes to one site, a site
    /// takes one prism; a site that lost its prism to a nearer site asks for its next nearest),
    /// and the sites it fills open their own neighbours as the next wave's edge. So one sweep can creep several rings along a trail and claim dozens of prisms.</para>
    ///
    /// <para><b>Cost.</b> Candidates are bucketed once per call by the lattice cell they sit over
    /// (a sort, no allocation after warm-up). A wave then visits only the edge sites and, for
    /// each, the cells within the radius: <c>edge x (2 ceil(r / pitch) + 1)^2</c> bucket lookups,
    /// independent of how many prisms are far from the edge.</para>
    ///
    /// Candidate positions are wall-local: x along the seed's right, y along its up, z along its
    /// forward (off the wall's plane), in world units, with the seed at the origin.
    /// </summary>
    public sealed class SerpentWallStealPlanner
    {
        public struct Claim
        {
            public Vector2Int Site;
            /// <summary>Index into the candidate list passed to <see cref="Plan"/>.</summary>
            public int Candidate;
        }

        struct Proposal
        {
            public float Sqr;
            public Vector2Int Site;
            public int Candidate;
        }

        struct Bucket
        {
            public int Start;
            public int Count;
        }

        static readonly Vector2Int[] Neighbours =
            { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };

        static readonly Comparison<Proposal> NearestFirst = (a, b) => a.Sqr.CompareTo(b.Sqr);

        readonly HashSet<Vector2Int> _edge = new();
        readonly HashSet<Vector2Int> _added = new();
        readonly List<Vector2Int> _pending = new();
        readonly List<Vector2Int> _opened = new();
        readonly List<Proposal> _proposals = new();
        readonly List<int> _sorted = new();
        readonly List<Vector2Int> _cellOf = new();
        readonly Dictionary<Vector2Int, Bucket> _buckets = new();
        readonly List<bool> _used = new();
        Comparison<int> _byCell;

        /// <summary>
        /// Plan one sweep. <paramref name="filled"/> is every site that already holds a brick,
        /// standing or in flight, the seed's (0, 0) included; it is read, never changed. Returns
        /// the number of claims written to <paramref name="into"/> (cleared first), at most
        /// <paramref name="maxClaims"/>.
        /// </summary>
        public int Plan(HashSet<Vector2Int> filled, List<Vector3> candidates, float pitch, float radius,
            int maxClaims, int maxWaves, List<Claim> into)
        {
            into.Clear();
            _edge.Clear();
            _added.Clear();
            if (filled == null || candidates == null || candidates.Count == 0) return 0;
            if (pitch <= 0f || radius <= 0f || maxClaims <= 0 || maxWaves <= 0) return 0;

            float r2 = radius * radius;
            int reach = Mathf.CeilToInt(radius / pitch);
            BucketCandidates(candidates, pitch, r2);

            foreach (var site in filled)
                foreach (var d in Neighbours)
                {
                    var n = site + d;
                    if (!filled.Contains(n)) _edge.Add(n);
                }

            for (int wave = 0; wave < maxWaves && _edge.Count > 0 && into.Count < maxClaims; wave++)
            {
                _opened.Clear();
                _pending.Clear();
                _pending.AddRange(_edge);

                // Every pending edge site proposes its nearest free prism in reach; the nearest
                // pairs are granted first, and a site that lost its prism to a nearer site asks
                // again for its next nearest, until no site can be served this wave.
                while (_pending.Count > 0 && into.Count < maxClaims)
                {
                    _proposals.Clear();
                    foreach (var site in _pending)
                    {
                        if (TryNearest(site, candidates, pitch, reach, r2, out int c, out float sqr))
                            _proposals.Add(new Proposal { Sqr = sqr, Site = site, Candidate = c });
                    }
                    if (_proposals.Count == 0) break;

                    _proposals.Sort(NearestFirst);
                    _pending.Clear();
                    foreach (var p in _proposals)
                    {
                        if (into.Count >= maxClaims) break;
                        if (_used[p.Candidate])
                        {
                            _pending.Add(p.Site);
                            continue;
                        }
                        _used[p.Candidate] = true;
                        _edge.Remove(p.Site);
                        _added.Add(p.Site);
                        into.Add(new Claim { Site = p.Site, Candidate = p.Candidate });
                        foreach (var d in Neighbours) _opened.Add(p.Site + d);
                    }
                }
                if (_opened.Count == 0) break;

                // The bricks this wave placed are the next wave's edge.
                foreach (var n in _opened)
                    if (!filled.Contains(n) && !_added.Contains(n)) _edge.Add(n);
            }

            return into.Count;
        }

        /// <summary>Sorts candidate indices by the lattice cell each sits over and records each
        /// cell's run, so a site finds its neighbourhood's prisms without scanning them all.
        /// Prisms further off the wall's plane than the radius can never be in reach and are left
        /// out.</summary>
        void BucketCandidates(List<Vector3> candidates, float pitch, float r2)
        {
            _used.Clear();
            _cellOf.Clear();
            _sorted.Clear();
            _buckets.Clear();
            for (int c = 0; c < candidates.Count; c++)
            {
                var p = candidates[c];
                _used.Add(false);
                _cellOf.Add(new Vector2Int(Mathf.RoundToInt(p.x / pitch), Mathf.RoundToInt(p.y / pitch)));
                if (p.z * p.z <= r2) _sorted.Add(c);
            }

            _byCell ??= (a, b) =>
            {
                var ca = _cellOf[a];
                var cb = _cellOf[b];
                return ca.x != cb.x ? ca.x.CompareTo(cb.x) : ca.y.CompareTo(cb.y);
            };
            _sorted.Sort(_byCell);

            for (int k = 0; k < _sorted.Count;)
            {
                var cell = _cellOf[_sorted[k]];
                int start = k;
                while (k < _sorted.Count && _cellOf[_sorted[k]] == cell) k++;
                _buckets[cell] = new Bucket { Start = start, Count = k - start };
            }
        }

        bool TryNearest(Vector2Int site, List<Vector3> candidates, float pitch, int reach, float r2,
            out int best, out float bestSqr)
        {
            best = -1;
            bestSqr = r2;
            float sx = site.x * pitch, sy = site.y * pitch;
            for (int dx = -reach; dx <= reach; dx++)
            for (int dy = -reach; dy <= reach; dy++)
            {
                if (!_buckets.TryGetValue(new Vector2Int(site.x + dx, site.y + dy), out var bucket)) continue;
                for (int k = bucket.Start; k < bucket.Start + bucket.Count; k++)
                {
                    int c = _sorted[k];
                    if (_used[c]) continue;
                    var p = candidates[c];
                    float ex = p.x - sx, ey = p.y - sy;
                    float sqr = ex * ex + ey * ey + p.z * p.z;
                    if (sqr > bestSqr) continue;
                    bestSqr = sqr;
                    best = c;
                }
            }
            return best >= 0;
        }
    }
}
