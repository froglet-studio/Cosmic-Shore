using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The rules every placement prism is laid under, as a pure spatial test (no Unity objects, no physics), so a
    /// course genome can never seal the race whatever a search does to it:
    /// <list type="bullet">
    /// <item>nothing inside the <see cref="RegattaCourseGenome.LineClearance"/> tube round the racing line (the spine
    /// the rails are braided round), so the line through every ring stays open;</item>
    /// <item>nothing in a ring's mouth (course rings and alternate rings, a disc a little wider than the mouth and
    /// <see cref="RingSlab"/> deep either side of its plane);</item>
    /// <item>nothing touching a rail prism (the rails are the riders' terrain; a prism may never interpenetrate another,
    /// Docs/ECOSYSTEM.md 49.10);</item>
    /// <item>nothing inside the nucleus or out at the membrane;</item>
    /// <item>nothing touching a prism of an EARLIER placement (each placement's own layout is its spawner's).</item>
    /// </list>
    /// Prisms are tested as spheres of half their longest axis, which is conservative for every box.
    /// </summary>
    public sealed class RegattaPlacementCarve
    {
        public enum Verdict { Laid = 0, Line = 1, Ring = 2, Rail = 3, Shell = 4, Overlap = 5, Fine = 6 }

        /// <summary>Half the depth (u) of the slab either side of a ring's plane that is kept clear.</summary>
        public const float RingSlab = 20f;
        /// <summary>How much wider than its mouth (u) a ring's clear disc is: the ring's own body and a margin.</summary>
        public const float RingMargin = 12f;
        /// <summary>A rail prism's bounding radius: half the diagonal of a 6 x 6 x 8 box (5.83 u), rounded up.</summary>
        public const float RailRadius = 6f;

        const float Cell = 32f;

        readonly float _line;
        readonly Vector3[] _spine;
        readonly List<RaceGate> _rings = new();
        readonly Dictionary<long, List<int>> _spineGrid = new();
        readonly List<Vector3> _rails = new();
        readonly Dictionary<long, List<int>> _railGrid = new();
        readonly List<Vector3> _claimed = new();
        readonly List<float> _claimedRadius = new();
        readonly Dictionary<long, List<int>> _claimGrid = new();
        readonly Vector3 _shellCentre;
        readonly float _nucleus = RegattaCourseGenome.NucleusCarve;
        readonly float _membrane = RegattaCourseGenome.MembraneCarve;
        float _maxClaimRadius;
        float _spineReach;   // half the longest spine segment: how far a segment's nearest point can be from both ends

        /// <summary>A carve for a course with no rails (any gate race's own course: <see cref="TrainingCourseOverlay"/>):
        /// the line is the closed polyline through <paramref name="spine"/>, the shell is the sphere about
        /// <paramref name="shellCentre"/> between <paramref name="nucleus"/> and <paramref name="membrane"/>.</summary>
        public RegattaPlacementCarve(IReadOnlyList<Vector3> spine, IEnumerable<RaceGate> rings, float lineClearance,
                                     Vector3 shellCentre, float nucleus, float membrane)
            : this(null, rings, lineClearance)
        {
            _shellCentre = shellCentre;
            _nucleus = nucleus;
            _membrane = membrane;
            _spine = new Vector3[spine?.Count ?? 0];
            for (int i = 0; i < _spine.Length; i++)
            {
                _spine[i] = spine[i];
                Add(_spineGrid, _spine[i], i);
            }
            for (int i = 0; i < _spine.Length && _spine.Length > 1; i++)
                _spineReach = Mathf.Max(_spineReach, 0.5f * (_spine[(i + 1) % _spine.Length] - _spine[i]).magnitude);
        }

        public RegattaPlacementCarve(RegattaRailLayout layout, IEnumerable<RaceGate> rings, float lineClearance)
        {
            _line = lineClearance;
            _spine = layout?.SpinePositions ?? new Vector3[0];
            for (int i = 0; i < _spine.Length; i++)
            {
                Add(_spineGrid, _spine[i], i);
                if (_spine.Length > 1)
                    _spineReach = Mathf.Max(_spineReach, 0.5f * (_spine[(i + 1) % _spine.Length] - _spine[i]).magnitude);
            }
            if (layout != null)
                foreach (var lane in layout.LanePositions)
                    foreach (var p in lane)
                    {
                        Add(_railGrid, p, _rails.Count);
                        _rails.Add(p);
                    }
            if (rings != null) _rings.AddRange(rings);
        }

        static long Key(int x, int y, int z) => ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (long)(z & 0x1FFFFF);

        static void Add(Dictionary<long, List<int>> grid, Vector3 p, int index)
        {
            long k = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
            if (!grid.TryGetValue(k, out var list)) grid[k] = list = new List<int>(4);
            list.Add(index);
        }

        static IEnumerable<int> Near(Dictionary<long, List<int>> grid, Vector3 p, float r)
        {
            // A non-finite point or reach would floor to int.MaxValue and the cell loop below would never end.
            if (!(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z) && float.IsFinite(r))) yield break;
            int x0 = Mathf.FloorToInt((p.x - r) / Cell), x1 = Mathf.FloorToInt((p.x + r) / Cell);
            int y0 = Mathf.FloorToInt((p.y - r) / Cell), y1 = Mathf.FloorToInt((p.y + r) / Cell);
            int z0 = Mathf.FloorToInt((p.z - r) / Cell), z1 = Mathf.FloorToInt((p.z + r) / Cell);
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
                if (grid.TryGetValue(Key(x, y, z), out var list))
                    foreach (var i in list) yield return i;
        }

        /// <summary>Distance from <paramref name="p"/> to the closed spine polyline, searched within
        /// <paramref name="within"/> (returns at least that when nothing is nearer).</summary>
        public float SpineDistance(Vector3 p, float within)
        {
            int n = _spine.Length;
            float best = float.MaxValue;
            if (n < 2) return best;
            // A segment's nearest point can lie half its length from both of its ends: widen the search by that (and
            // never less than a cell, as before).
            foreach (int i in Near(_spineGrid, p, within + Mathf.Max(Cell, _spineReach) + 0.01f))
            {
                best = Mathf.Min(best, SegmentDistance(p, _spine[i], _spine[(i + 1) % n]));
                best = Mathf.Min(best, SegmentDistance(p, _spine[(i - 1 + n) % n], _spine[i]));
            }
            return best;
        }

        static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = Vector3.Dot(ab, ab);
            float t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Whether a prism (bounding radius <paramref name="r"/>) at <paramref name="p"/> may be laid. With
        /// <paramref name="crossLine"/> (<see cref="RegattaPlacement.CrossLine"/>) the line's tube is not carved; the
        /// ring mouths, the rails and the shell still are; with <paramref name="crossRail"/>
        /// (<see cref="RegattaPlacement.CrossRail"/>) the rails are not carved either.</summary>
        public Verdict Test(Vector3 p, float r, bool crossLine = false, bool crossRail = false)
        {
            float d = (p - _shellCentre).magnitude;
            if (d - r < _nucleus || d + r > _membrane) return Verdict.Shell;
            if (!crossLine && SpineDistance(p, _line + r) < _line + r) return Verdict.Line;
            for (int i = 0; i < _rings.Count; i++)
            {
                var g = _rings[i];
                Vector3 axis = RaceCourseGeometry.SafeNormalize(g.Axis, Vector3.forward);
                Vector3 v = p - g.Position;
                float along = Vector3.Dot(v, axis);
                if (Mathf.Abs(along) >= RingSlab + r) continue;
                float radial = (v - axis * along).magnitude;
                if (radial < g.Radius + RingMargin + r) return Verdict.Ring;
            }
            if (!crossRail)
                foreach (int i in Near(_railGrid, p, RailRadius + r))
                    if ((_rails[i] - p).sqrMagnitude < (RailRadius + r) * (RailRadius + r)) return Verdict.Rail;
            if (_claimed.Count > 0)
                foreach (int i in Near(_claimGrid, p, r + _maxClaimRadius))
                {
                    float sum = r + _claimedRadius[i];
                    if ((_claimed[i] - p).sqrMagnitude < sum * sum) return Verdict.Overlap;
                }
            return Verdict.Laid;
        }

        /// <summary>True when a sphere at <paramref name="p"/> of radius <paramref name="r"/> touches a laid placement
        /// prism (a planting site that would root a plant inside a placement is skipped).</summary>
        public bool TouchesClaimed(Vector3 p, float r)
        {
            if (_claimed.Count == 0) return false;
            foreach (int i in Near(_claimGrid, p, r + _maxClaimRadius))
            {
                float sum = r + _claimedRadius[i];
                if ((_claimed[i] - p).sqrMagnitude < sum * sum) return true;
            }
            return false;
        }

        /// <summary>Record a laid prism so later placements keep clear of it.</summary>
        public void Claim(Vector3 p, float r)
        {
            Add(_claimGrid, p, _claimed.Count);
            _claimed.Add(p);
            _claimedRadius.Add(r);
            _maxClaimRadius = Mathf.Max(_maxClaimRadius, r);
        }

        /// <summary>A prism's bounding radius: half its longest axis.</summary>
        public static float RadiusOf(Vector3 scale) =>
            0.5f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
    }
}
