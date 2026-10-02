// Round 8 (Docs/SWARM_FAUNA.md §16): how a weapon, a predator or the cell FINDS a swarm member that is
// only data. Round 7 drew members from one GPU buffer and gave a GameObject only to the ones near a vessel,
// so anything that looked for a member through PrismSpatialIndex or PhysX found nothing past 160 u - a
// Dolphin cone, a rocket and the Serpent's rifle all passed straight through a whale.
//
// Two pieces, both free of UnityEngine so the SAME file compiles and RUNS in
// Tools/Build/swarm_core_harness (test R8a proves the volumes against the shipped Burst code):
//
//   * SwarmVolume - the five query shapes the platform already uses for PRISMS, transcribed term for term
//     from PrismSpatialIndex: QuerySphere / the AOE sphere, QuerySegment (a projectile's swept capsule),
//     QueryCone (the sniper), AOEConicSweepQueryJob (the Dolphin's cone) and AOECylinderSweepQueryJob (the
//     Scarab's plate). "A virtual member is treated exactly as its body prism would be" starts here: the
//     same CENTRE test, the same operations in the same order, so a member and a prism standing at one
//     point always get the same answer.
//   * SwarmMemberGrid - a spatial hash of the swarm's living members, built by the TICK JOB on its worker
//     thread (counting sort, no allocation after the first tick) and published with the rest of the frame.
//     A query walks only the hash cells under the volume's bounding box, so its cost is O(members near the
//     volume) - a rocket at the far side of the cell touches a few buckets and no member.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>The five shapes a weapon's query can take (Docs/SWARM_FAUNA.md §16.1).</summary>
    public enum SwarmVolumeKind
    {
        Sphere = 0,
        /// <summary>Points within a radius of a SEGMENT (PrismSpatialIndex.QuerySegment).</summary>
        Capsule = 1,
        /// <summary>A cone with a minimum radius near its apex (PrismSpatialIndex.QueryCone / ConeContains).</summary>
        Cone = 2,
        /// <summary>The conic explosion's capsule-section slab (AOEConicSweepQueryJob).</summary>
        ConeSlab = 3,
        /// <summary>The cylindrical explosion's plate slab, optionally mirrored (AOECylinderSweepQueryJob).</summary>
        CylinderSlab = 4,
    }

    /// <summary>
    /// One query volume. Built through the static constructors, which do the SAME pre-processing the
    /// shipped call does (QueryCone normalizes and clamps; ProcessExplosionConeFrame normalizes the axis
    /// and re-orthogonalises the gape axis), so the stored parameters are the ones the Burst job would see.
    /// </summary>
    public readonly struct SwarmVolume
    {
        public readonly SwarmVolumeKind Kind;
        public readonly Vector3 A, Axis, Gape, Ab;
        public readonly float RadiusSq, AbLenSq, Length, TanHalf, MinRadius, SliceMin, SliceMax, CoreTan, GapeTan;
        public readonly bool Mirrored;
        /// <summary>A conservative world AABB of the volume (the grid walk's extent).</summary>
        public readonly Vector3 Lo, Hi;

        SwarmVolume(SwarmVolumeKind kind, Vector3 a, Vector3 axis, Vector3 gape, Vector3 ab, float radiusSq, float abLenSq,
                    float length, float tanHalf, float minRadius, float sliceMin, float sliceMax, float coreTan, float gapeTan,
                    bool mirrored, Vector3 lo, Vector3 hi)
        {
            Kind = kind; A = a; Axis = axis; Gape = gape; Ab = ab; RadiusSq = radiusSq; AbLenSq = abLenSq; Length = length;
            TanHalf = tanHalf; MinRadius = minRadius; SliceMin = sliceMin; SliceMax = sliceMax; CoreTan = coreTan; GapeTan = gapeTan;
            Mirrored = mirrored; Lo = lo; Hi = hi;
        }

        public bool IsEmpty => Kind == SwarmVolumeKind.Sphere ? RadiusSq < 0f : !(Hi.X >= Lo.X);

        /// <summary>QuerySphere / AOESpatialQueryJob: centre within <paramref name="radius"/>.</summary>
        public static SwarmVolume Sphere(Vector3 centre, float radius)
        {
            var e = new Vector3(radius);
            return new SwarmVolume(SwarmVolumeKind.Sphere, centre, default, default, default, radius * radius, 0, 0, 0, 0, 0, 0, 0, 0,
                                   false, centre - e, centre + e);
        }

        /// <summary>QuerySegment: centre within <paramref name="radius"/> of segment a→b.</summary>
        public static SwarmVolume Capsule(Vector3 a, Vector3 b, float radius)
        {
            var ab = b - a;
            var e = new Vector3(radius);
            return new SwarmVolume(SwarmVolumeKind.Capsule, a, default, default, ab, radius * radius, ab.LengthSquared(), 0, 0, 0, 0, 0, 0, 0,
                                   false, Vector3.Min(a, b) - e, Vector3.Max(a, b) + e);
        }

        /// <summary>QueryCone's preprocessing + ConeContains. An invalid cone (zero direction, length &lt;= 0)
        /// contains nothing, exactly as QueryCone returns 0.</summary>
        public static SwarmVolume Cone(Vector3 apex, Vector3 direction, float length, float halfAngleDegrees, float minRadius)
        {
            float dirLenSq = direction.LengthSquared();
            if (length <= 0f || dirLenSq < 1e-8f) return Empty();
            var dir = direction * (1f / MathF.Sqrt(dirLenSq));
            float tanHalf = MathF.Tan(Math.Clamp(halfAngleDegrees, 0f, 89f) * (MathF.PI / 180f));
            minRadius = MathF.Max(minRadius, 0f);
            float endRadius = MathF.Max(minRadius, length * tanHalf);
            var end = apex + dir * length;
            var e = new Vector3(endRadius);
            return new SwarmVolume(SwarmVolumeKind.Cone, apex, dir, default, default, 0, 0, length, tanHalf, minRadius, 0, 0, 0, 0,
                                   false, Vector3.Min(apex, end) - e, Vector3.Max(apex, end) + e);
        }

        /// <summary>ProcessExplosionConeFrame's preprocessing + AOEConicSweepQueryJob. A degenerate slab
        /// (sliceMax &lt;= 0 or tanCore &lt;= 0) contains nothing, exactly as that frame queries nothing.</summary>
        public static SwarmVolume ConeSlab(Vector3 apex, Vector3 axis, Vector3 gapeAxis, float sliceMin, float sliceMax,
                                           float tanCoreHalfAngle, float tanGapePerUnit)
        {
            if (!(sliceMax > 0f) || !(tanCoreHalfAngle > 0f)) return Empty();
            var sweepAxis = NormalizeSafe(axis, new Vector3(0f, 0f, 1f));
            var gape = gapeAxis;
            gape -= sweepAxis * Vector3.Dot(gape, sweepAxis);
            gape = NormalizeSafe(gape, NormalizeSafe(Vector3.Cross(sweepAxis, new Vector3(0f, 1f, 0f)), new Vector3(1f, 0f, 0f)));
            float gapeTan = MathF.Max(tanGapePerUnit, 0f);
            float r = (tanCoreHalfAngle + gapeTan) * sliceMax;
            var end = apex + sweepAxis * sliceMax;
            var e = new Vector3(r);
            return new SwarmVolume(SwarmVolumeKind.ConeSlab, apex, sweepAxis, gape, default, 0, 0, 0, 0, 0, MathF.Max(sliceMin, 0f), sliceMax,
                                   tanCoreHalfAngle, gapeTan, false, Vector3.Min(apex, end) - e, Vector3.Max(apex, end) + e);
        }

        /// <summary>ProcessExplosionCylinderFrame's preprocessing + AOECylinderSweepQueryJob.</summary>
        public static SwarmVolume CylinderSlab(Vector3 origin, Vector3 axis, float sliceMin, float sliceMax, float radius, bool mirrored)
        {
            if (!(sliceMax > 0f) || !(radius > 0f)) return Empty();
            var ax = NormalizeSafe(axis, new Vector3(0f, 0f, 1f));
            var end = origin + ax * sliceMax;
            var start = mirrored ? origin - ax * sliceMax : origin;
            var e = new Vector3(radius);
            return new SwarmVolume(SwarmVolumeKind.CylinderSlab, origin, ax, default, default, radius * radius, 0, 0, 0, 0,
                                   MathF.Max(sliceMin, 0f), sliceMax, 0, 0, mirrored, Vector3.Min(start, end) - e, Vector3.Max(start, end) + e);
        }

        static SwarmVolume Empty() =>
            new(SwarmVolumeKind.Capsule, default, default, default, default, 0, 0, 0, 0, 0, 0, 0, 0, 0, false,
                new Vector3(1f), new Vector3(-1f));

        /// <summary>Unity.Mathematics.normalizesafe: x/|x| when |x|^2 is a normal float, else the default.</summary>
        static Vector3 NormalizeSafe(Vector3 x, Vector3 fallback)
        {
            float len = Vector3.Dot(x, x);
            return len > 1.175494351e-38f ? x * (1f / MathF.Sqrt(len)) : fallback;
        }

        /// <summary>The shipped test, term for term (see each shape's source in the summary above).</summary>
        public bool Contains(Vector3 p)
        {
            switch (Kind)
            {
                case SwarmVolumeKind.Sphere:
                    return Vector3.DistanceSquared(p, A) <= RadiusSq;

                case SwarmVolumeKind.Capsule:
                {
                    if (!(Hi.X >= Lo.X)) return false;
                    // PrismSpatialIndex.DistanceToSegmentSq
                    float t = AbLenSq > 1e-8f ? Math.Clamp(Vector3.Dot(p - A, Ab) / AbLenSq, 0f, 1f) : 0f;
                    return Vector3.DistanceSquared(p, A + Ab * t) <= RadiusSq;
                }

                case SwarmVolumeKind.Cone:
                {
                    if (!(Hi.X >= Lo.X)) return false;
                    // PrismSpatialIndex.ConeContains
                    var rel = p - A;
                    float t = Vector3.Dot(rel, Axis);
                    if (t < 0f || t > Length) return false;
                    float allowed = MathF.Max(MinRadius, t * TanHalf);
                    float perpSq = MathF.Max(0f, rel.LengthSquared() - t * t);
                    return perpSq <= allowed * allowed;
                }

                case SwarmVolumeKind.ConeSlab:
                {
                    if (!(Hi.X >= Lo.X)) return false;
                    // AOEConicSweepQueryJob.Execute
                    var rel = p - A;
                    float s = Vector3.Dot(rel, Axis);
                    if (s < SliceMin || s > SliceMax) return false;
                    var radial = rel - Axis * s;
                    float halfLength = GapeTan * s;
                    float along = Vector3.Dot(radial, Gape);
                    var offAxis = radial - Gape * Math.Clamp(along, -halfLength, halfLength);
                    float coreRadius = CoreTan * s;
                    return offAxis.LengthSquared() <= coreRadius * coreRadius;
                }

                case SwarmVolumeKind.CylinderSlab:
                {
                    if (!(Hi.X >= Lo.X)) return false;
                    // AOECylinderSweepQueryJob.Execute
                    var rel = p - A;
                    float s = Vector3.Dot(rel, Axis);
                    float axial = Mirrored ? MathF.Abs(s) : s;
                    if (axial < SliceMin || axial > SliceMax) return false;
                    var radial = rel - Axis * s;
                    return radial.LengthSquared() <= RadiusSq;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// A spatial hash of one swarm's living members, keyed on the published CURRENT heart position
    /// (Docs/SWARM_FAUNA.md §16.1). Built on the worker by <see cref="SwarmTickJob"/>; read on the main thread
    /// from the FRONT copy. A candidate is any member filed in a bucket under the query box - the caller then
    /// tests the member's displayed (interpolated) position against the exact volume, so <see cref="Pad"/>
    /// widens the box by the most any member can be from where it was filed (one tick of travel plus the body
    /// prism's seat behind the heart).
    /// </summary>
    public sealed class SwarmMemberGrid
    {
        public const float CellSize = 16f;
        readonly int _cap, _buckets;
        readonly int[] _start, _items, _bucketOf, _stamp, _written;
        int _stampValue, _count;

        /// <summary>World AABB of every filed member, already widened by <see cref="Pad"/>.</summary>
        public Vector3 Lo { get; private set; }
        public Vector3 Hi { get; private set; }
        /// <summary>Max distance between a member's filed position and any point the frame can draw for it.</summary>
        public float Pad { get; private set; }
        public int Count => _count;

        public SwarmMemberGrid(int cap)
        {
            _cap = Math.Max(1, cap);
            int b = 16; while (b < 2 * _cap) b <<= 1;
            _buckets = b;
            _start = new int[b + 1];
            _items = new int[_cap];
            _bucketOf = new int[_cap];
            _stamp = new int[_cap];
            _written = new int[b];
            Lo = new Vector3(1f); Hi = new Vector3(-1f);
        }

        static int Cell(float v) => (int)MathF.Floor(v / CellSize);

        int Bucket(int cx, int cy, int cz) =>
            (int)(((uint)(cx * 73856093) ^ (uint)(cy * 19349663) ^ (uint)(cz * 83492791)) & (uint)(_buckets - 1));

        /// <summary>Files every alive slot of <paramref name="inst"/> (counting sort). Worker thread.</summary>
        public void Build(SwarmInstance[] inst, float pad)
        {
            Array.Clear(_start, 0, _start.Length);
            var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
            int n = 0;
            for (int i = 0; i < _cap; i++)
            {
                if ((inst[i].Flags & 1u) == 0) { _bucketOf[i] = -1; continue; }
                var p = inst[i].CurPos;
                int bk = Bucket(Cell(p.X), Cell(p.Y), Cell(p.Z));
                _bucketOf[i] = bk;
                _start[bk + 1]++;
                lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
                n++;
            }
            for (int b = 0; b < _buckets; b++) _start[b + 1] += _start[b];
            Array.Clear(_written, 0, _written.Length);
            for (int i = 0; i < _cap; i++)
            {
                int bk = _bucketOf[i];
                if (bk < 0) continue;
                _items[_start[bk] + _written[bk]++] = i;
            }
            Array.Clear(_stamp, 0, _stamp.Length);
            _stampValue = 0;
            _count = n;
            Pad = pad;
            var e = new Vector3(pad);
            if (n > 0) { Lo = lo - e; Hi = hi + e; }
            else { Lo = new Vector3(1f); Hi = new Vector3(-1f); }
        }

        /// <summary>
        /// Every filed slot whose bucket lies under the box [lo, hi] widened by <see cref="Pad"/>, each once.
        /// Appends to <paramref name="slots"/>; returns how many were appended. Main thread (front grid).
        /// When the box covers more cells than there are buckets it walks the buckets directly instead.
        /// </summary>
        public int Candidates(Vector3 lo, Vector3 hi, List<int> slots)
        {
            if (_count == 0) return 0;
            lo = Vector3.Max(lo - new Vector3(Pad), Lo);
            hi = Vector3.Min(hi + new Vector3(Pad), Hi);
            if (hi.X < lo.X || hi.Y < lo.Y || hi.Z < lo.Z) return 0;
            int x0 = Cell(lo.X), y0 = Cell(lo.Y), z0 = Cell(lo.Z), x1 = Cell(hi.X), y1 = Cell(hi.Y), z1 = Cell(hi.Z);
            long cells = (long)(x1 - x0 + 1) * (y1 - y0 + 1) * (z1 - z0 + 1);
            int before = slots.Count;
            if (cells >= _buckets)
            {
                for (int k = 0; k < _start[_buckets]; k++) slots.Add(_items[k]);
                return slots.Count - before;
            }
            if (++_stampValue == int.MaxValue) { Array.Clear(_stamp, 0, _stamp.Length); _stampValue = 1; }
            for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
            for (int z = z0; z <= z1; z++)
            {
                int bk = Bucket(x, y, z);
                for (int k = _start[bk]; k < _start[bk + 1]; k++)
                {
                    int s = _items[k];
                    if (_stamp[s] == _stampValue) continue;   // two cells can share a bucket
                    _stamp[s] = _stampValue;
                    slots.Add(s);
                }
            }
            return slots.Count - before;
        }
    }

    /// <summary>
    /// Round 8 (Docs/SWARM_FAUNA.md §16.3): what a swarm states to its cell's volume ladder. The worker sums
    /// every DRAWN body per domain slot (<c>SwarmTickJob.VolumeBySlot</c>, recording which slot it counted each
    /// member in, <c>Counted</c>); this takes out exactly the members whose volume the cell already sees some
    /// other way - a proxy whose body prism has finished creation is a registered HealthPrism and counts itself,
    /// and a member killed since the frame was built is dead in the cell even though its frame still draws it.
    /// One rule, so "never double-count a member that has a proxy" cannot drift between the C# and its proof.
    /// </summary>
    public static class SwarmVolumeLedger
    {
        /// <summary>A body's volume, as the cell counts a prism's: |x·y·z| (a plan may author a negative extent).</summary>
        public static double BodyVolume(Vector3 scale) => Math.Abs((double)scale.X * scale.Y * scale.Z);

        /// <param name="volumeBySlot">The worker's per-domain-slot sums (3 used).</param>
        /// <param name="cellSlotOfDomainSlot">Cell volume slot (0..3) each swarm domain slot maps to.</param>
        /// <param name="counted">Per member: the domain slot the worker counted it in, or -1.</param>
        /// <param name="excluded">Members the cell already sees (finished proxies) or that are gone.</param>
        /// <param name="outByCellSlot">Receives the stated volume per cell slot (4 entries).</param>
        public static void State(double[] volumeBySlot, int[] cellSlotOfDomainSlot, sbyte[] counted,
                                 SwarmInstance[] instances, IList<int> excluded, double[] outByCellSlot)
        {
            Array.Clear(outByCellSlot, 0, 4);
            for (int ds = 0; ds < 3; ds++) outByCellSlot[cellSlotOfDomainSlot[ds]] += volumeBySlot[ds];
            for (int q = 0; q < excluded.Count; q++)
            {
                int i = excluded[q];
                if (i < 0 || i >= counted.Length) continue;
                int ds = counted[i];
                if (ds < 0) continue;   // not in the worker's sum - nothing to take out
                int slot = cellSlotOfDomainSlot[ds];
                outByCellSlot[slot] = Math.Max(0.0, outByCellSlot[slot] - BodyVolume(instances[i].Scale));
            }
        }
    }
}
