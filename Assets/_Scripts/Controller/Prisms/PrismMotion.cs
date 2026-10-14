using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What a mover can afford to spend on prism movement's collision rule, by how many prisms it moves
    /// (<see cref="PrismMotion.NextLevel"/>, <c>PrismMotionConfigSO</c>). The RULE is the same at every
    /// level; only its precision and its look change. Explicit values: a level is a tuning vocabulary.
    /// </summary>
    public enum PrismMotionLevel
    {
        /// <summary>Few movers: exact boxes, and each destroyed prism comes apart as a collision burst on
        /// the ultra-high-res prism while the burst budget allows.</summary>
        Showcase = 1,
        /// <summary>More movers: exact boxes, the ordinary explosion.</summary>
        Plain = 2,
        /// <summary>Many movers: the spheres of the prisms' volumes in Burst, the ordinary explosion.</summary>
        Bulk = 3,
    }

    /// <summary>
    /// PRISM MOVEMENT, the fundamental (Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md § "Prism movement",
    /// design + verification <c>Docs/PRISM_MOTION.md</c>): the one set of expectations every force that
    /// DISPLACES a prism keeps, whoever applies it.
    ///
    /// <para><b>The collision rule.</b> A moving prism that runs into another prism destroys BOTH. "Runs
    /// into" means: over the step, the two prisms' contact shapes go from apart to touching. Prisms that were already touching when the step began are at rest against each other —
    /// a trail's neighbours, a structure being carried whole — and are not a collision. Super-shielded
    /// mass is the one exception: it is indestructible, so it deflects (<see cref="Prism.AbsorbSuperShieldHit"/>)
    /// and only the mover is destroyed. An ordinary shield is no answer to a collision (devastating).</para>
    ///
    /// <para><b>The ladder: what a mover can afford, by how many prisms it moves</b>
    /// (<see cref="PrismMotionLevel"/>, <see cref="NextLevel"/>, <c>PrismMotionConfigSO</c>).
    /// LEVEL 1, Showcase (≤ 64 movers): the prism's own oriented BOX, swept over the step by the
    /// separating-axis test (<see cref="EntersContact(in Box, float3, in Box, float3, out float)"/>,
    /// <see cref="TryFirstContact"/>), and each destroyed prism comes apart as a COLLISION BURST on the
    /// ultra-high-res prism (<c>PrismCollisionBurst</c>, Docs/PRISM_ANIMATION.md §4.11) while its budget
    /// allows. LEVEL 2, Plain (≤ 256): boxes, the ordinary explosion. LEVEL 3, Bulk (above): the SPHERE of
    /// each prism's volume (<see cref="ContactRadius"/>, capped at <see cref="MaxContactRadius"/>), tested
    /// for thousands of bodies at once in the Burst <see cref="ContactJob"/>, the ordinary explosion. Each
    /// line is crossed back downward at three quarters of it. The Rhino plough moves one prism per ram,
    /// so it is always level 1 (<see cref="TrySweep"/>); the gravity field climbs with its body count.
    /// Every level answers the same question — apart at the start, touching during the step — so only the
    /// precision and the look change with the count, never the rule.</para>
    ///
    /// <para><b>Who it is scored to.</b> The force that moved the prism: the collision is the act of the
    /// vessel or field behind the motion, never a passive decay, so it stays inside "Mass is conserved"
    /// (only active forces remove mass).</para>
    ///
    /// <para><b>What is NOT a moving prism.</b> A prism POSED by its owner every frame — a creature's
    /// body, a builder setting a stone, a plant growing, a projectile carrying its payload — is the shape
    /// of that owner, not mass a force displaced. It is a valid TARGET (a moving prism that hits a
    /// creature's body prism destroys it) but it does not run this rule itself.</para>
    /// </summary>
    public static class PrismMotion
    {
        /// <summary>
        /// Ceiling on a contact radius, in world units. It bounds the broad phase (the bucket walk and
        /// the segment query reach at most this far past the mover's own sphere), so a prism larger
        /// than a sphere of this radius (~900 u³) meets the world as one of this radius.
        /// </summary>
        public const float MaxContactRadius = 6f;

        /// <summary>
        /// How far past its own half-diagonal the BOX path looks for a target's centre, in world units: a
        /// target whose half-diagonal is longer than this can be missed by the broad phase. Generous on
        /// purpose — the box path only runs while few prisms move.
        /// </summary>
        public const float MaxBoxReach = 12f;

        /// <summary>
        /// Debris speed as a multiple of the closing speed: the ram's own restitution
        /// (<c>VesselDamagePrismEffect.restitution</c>), so a prism thrown into a prism comes apart like
        /// one a hull hit at that speed.
        /// </summary>
        public const float DebrisRestitution = 1f / 3f;

        /// <summary>Ceiling on that debris speed — the ram's and the sword's.</summary>
        public const float DebrisSpeedLimit = 200f;

        const float FourThirdsPi = 4f / 3f * math.PI;

        /// <summary>The radius of the sphere of <paramref name="volume"/>, capped at
        /// <see cref="MaxContactRadius"/>. A non-positive volume has no extent (0).</summary>
        public static float ContactRadius(float volume) =>
            volume > 0f ? math.min(math.pow(volume / FourThirdsPi, 1f / 3f), MaxContactRadius) : 0f;

        /// <summary>
        /// Whether two prisms moving linearly over one step — <paramref name="aFrom"/>→<paramref name="aTo"/>
        /// and <paramref name="bFrom"/>→<paramref name="bTo"/> — come into contact during it: their centres
        /// start farther apart than <paramref name="reach"/> (the sum of the two contact radii) and close to
        /// within it. <paramref name="t"/> is the fraction of the step at first contact (0..1).
        ///
        /// <para>A pair already within reach at the start is NOT a collision (resting contact, or a pair
        /// moving together), and neither is a pair that only separates. A static target is
        /// <c>bFrom == bTo</c>.</para>
        /// </summary>
        public static bool EntersContact(float3 aFrom, float3 aTo, float3 bFrom, float3 bTo, float reach, out float t)
        {
            t = 0f;
            float3 d0 = bFrom - aFrom;
            float c = math.lengthsq(d0) - reach * reach;
            if (c <= 0f) return false;                          // already touching: at rest, not a collision
            float3 dv = (bTo - bFrom) - (aTo - aFrom);          // b's motion relative to a's
            float b = math.dot(d0, dv);
            if (b >= 0f) return false;                          // not closing
            float a = math.lengthsq(dv);
            float disc = b * b - a * c;
            if (disc < 0f) return false;                        // the closest approach stays out of reach
            // The smaller root of a t^2 + 2 b t + c = 0; positive because c > 0 and b < 0.
            t = (-b - math.sqrt(disc)) / a;
            return t <= 1f;
        }

        /// <summary>
        /// The collision itself: <paramref name="struck"/> and <paramref name="mover"/> are both destroyed,
        /// scored to <paramref name="playerName"/> in <paramref name="domain"/> (the force behind the
        /// motion). <paramref name="closingVelocity"/> is the mover's velocity relative to the struck
        /// prism; each one's debris leaves along it — the struck forward, the mover back.
        /// Super-shielded mass deflects and survives (<see cref="Prism.Damage"/>'s own gate); the mover
        /// is destroyed regardless.
        /// </summary>
        public static void Annihilate(Prism mover, Prism struck, Vector3 closingVelocity, Domains domain, string playerName,
            PrismMotionLevel level)
        {
            Vector3 impact = closingVelocity * DebrisRestitution;
            bool burst = level == PrismMotionLevel.Showcase;
            if (struck && !struck.destroyed)
                struck.DamageByCollision(impact, domain, playerName, burst, DebrisSpeedLimit);
            if (mover && !mover.destroyed)
                mover.DamageByCollision(-impact, domain, playerName, burst, DebrisSpeedLimit);
        }

        // ------------------------------------------------------------------ the ladder

        /// <summary>
        /// The level a mover moving <paramref name="movers"/> prisms is at, from the level it was at
        /// (<paramref name="current"/>): up at a ceiling, back down only at three quarters of it, so a count
        /// hovering at a line does not flip every frame. LEVEL 1 (Showcase) at or under
        /// <paramref name="showcaseMax"/>, LEVEL 2 (Plain) at or under <paramref name="boxMax"/>, LEVEL 3
        /// (Bulk) above. <c>PrismMotionConfigSO</c> holds both ceilings.
        /// </summary>
        public static PrismMotionLevel NextLevel(PrismMotionLevel current, int movers, int showcaseMax, int boxMax)
        {
            if (movers > boxMax) return PrismMotionLevel.Bulk;
            var level = current;
            if (level == PrismMotionLevel.Showcase && movers > showcaseMax) level = PrismMotionLevel.Plain;
            if (level == PrismMotionLevel.Bulk && movers <= boxMax * 3 / 4) level = PrismMotionLevel.Plain;
            if (level == PrismMotionLevel.Plain && movers <= showcaseMax * 3 / 4) level = PrismMotionLevel.Showcase;
            return level;
        }

        /// <summary>
        /// The level for a mover that moves ONE prism at a time (the Rhino plough): Showcase unless the
        /// config has turned level 1 off.
        /// </summary>
        public static PrismMotionLevel SingleMoverLevel()
        {
            var cfg = PrismMotionConfigSO.Instance;
            int showcaseMax = cfg != null ? cfg.ShowcaseMaxMovers : PrismMotionConfigSO.DefaultShowcaseMaxMovers;
            int boxMax = cfg != null ? cfg.BoxMaxMovers : PrismMotionConfigSO.DefaultBoxMaxMovers;
            return NextLevel(PrismMotionLevel.Showcase, 1, showcaseMax, boxMax);
        }

        // ------------------------------------------------------------------ boxes (few movers)

        /// <summary>A prism's oriented box: centre, unit axes, half-extents along them.</summary>
        public struct Box
        {
            public float3 Center, X, Y, Z, Half;

            public float BoundRadius => math.length(Half);

            /// <summary>An axis-aligned cube (a virtual swarm member, which has no transform to read).</summary>
            public static Box Cube(float3 center, float half) => new()
            {
                Center = center,
                X = new float3(1f, 0f, 0f),
                Y = new float3(0f, 1f, 0f),
                Z = new float3(0f, 0f, 1f),
                Half = new float3(half),
            };

            /// <summary>The box's half-width along <paramref name="axis"/> (its support distance).</summary>
            public float RadiusAlong(float3 axis) =>
                Half.x * math.abs(math.dot(X, axis)) + Half.y * math.abs(math.dot(Y, axis)) + Half.z * math.abs(math.dot(Z, axis));
        }

        /// <summary>
        /// The box twin of the sphere test: whether <paramref name="a"/> moving by <paramref name="aStep"/> and
        /// <paramref name="b"/> moving by <paramref name="bStep"/> (both translating, neither turning — every
        /// mover today) go from apart to overlapping during the step, <paramref name="t"/> the first touch
        /// (0..1). Exact: the 15 separating axes of two boxes depend only on their orientations, so each
        /// axis's overlap is one interval of t, and the boxes overlap exactly where all 15 do. A pair that
        /// overlaps at the start is at rest, not a collision.
        /// </summary>
        public static bool EntersContact(in Box a, float3 aStep, in Box b, float3 bStep, out float t)
        {
            t = 0f;
            float3 d0 = b.Center - a.Center;
            float3 dv = bStep - aStep;
            float enter = float.NegativeInfinity, exit = float.PositiveInfinity;
            bool apart = false;

            if (!Axis(a.X, in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(a.Y, in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(a.Z, in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(b.X, in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(b.Y, in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(b.Z, in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.X, b.X), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.X, b.Y), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.X, b.Z), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.Y, b.X), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.Y, b.Y), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.Y, b.Z), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.Z, b.X), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.Z, b.Y), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;
            if (!Axis(math.cross(a.Z, b.Z), in a, in b, d0, dv, ref enter, ref exit, ref apart)) return false;

            if (!apart) return false;                           // overlapping at the start: at rest
            float lo = math.max(enter, 0f), hi = math.min(exit, 1f);
            if (lo > hi) return false;
            t = lo;
            return true;
        }

        // One separating axis: narrows [enter, exit] to the times the projections overlap on it, and
        // notes whether they were apart at t = 0. False when they can never overlap. A degenerate axis
        // (parallel edges) carries no information; the face axes already cover that case.
        static bool Axis(float3 axis, in Box a, in Box b, float3 d0, float3 dv,
                         ref float enter, ref float exit, ref bool apart)
        {
            if (math.lengthsq(axis) < 1e-10f) return true;
            float r = a.RadiusAlong(axis) + b.RadiusAlong(axis);
            float p = math.dot(d0, axis);
            float s = math.dot(dv, axis);
            if (math.abs(p) > r) apart = true;
            if (math.abs(s) < 1e-12f) return math.abs(p) <= r;  // no relative motion along it
            float t1 = (-r - p) / s, t2 = (r - p) / s;
            if (t1 > t2) (t1, t2) = (t2, t1);
            enter = math.max(enter, t1);
            exit = math.min(exit, t2);
            return enter <= exit;
        }

        /// <summary>The prism's box at its CURRENT pose: its authored box collider through its live
        /// transform. False for a prism with no box geometry.</summary>
        public static bool TryGetBox(Prism prism, out Box box)
        {
            box = default;
            if (!prism || !prism.TryGetBoxGeometry(out var centerLocal, out var halfLocal)) return false;
            var t = prism.transform;
            Vector3 half = Vector3.Scale(halfLocal, t.lossyScale);
            Quaternion q = t.rotation;
            box = new Box
            {
                Center = t.TransformPoint(centerLocal),
                X = q * Vector3.right,
                Y = q * Vector3.up,
                Z = q * Vector3.forward,
                Half = new float3(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)),
            };
            return true;
        }

        /// <summary>
        /// Which other prisms are movers this step, and by how much — for the box path inside a batch
        /// mover, whose movers' transforms already stand at the END of their step. Default = none (every
        /// other prism static).
        /// </summary>
        public readonly struct MoverSteps
        {
            readonly NativeParallelHashMap<int, int> _moverOfSlot;
            readonly NativeArray<float3> _from, _to;
            readonly bool _live;

            public MoverSteps(NativeParallelHashMap<int, int> moverOfSlot, NativeArray<float3> from, NativeArray<float3> to)
            {
                _moverOfSlot = moverOfSlot;
                _from = from;
                _to = to;
                _live = moverOfSlot.IsCreated && from.IsCreated && to.IsCreated;
            }

            public bool TryGetStep(int slot, out float3 step)
            {
                step = float3.zero;
                if (!_live || !_moverOfSlot.TryGetValue(slot, out int j)) return false;
                step = _to[j] - _from[j];
                return true;
            }
        }

        // Scratch for the box path, kept off PrismMotion itself so the Burst-compiled jobs below never
        // share a class with managed statics.
        static class SweepScratch
        {
            internal static readonly List<Prism> Segment = new();
            internal static readonly List<int> Virtual = new();
        }

        /// <summary>
        /// The box path: the earliest prism that <paramref name="mover"/>, starting as <paramref name="start"/>
        /// and moving by <paramref name="step"/>, runs into — its index <paramref name="slot"/> and the fraction
        /// <paramref name="t"/> of the step at first touch. Targets are found by their stored centres within the
        /// mover's half-diagonal + <see cref="MaxBoxReach"/> of the path, then tested box against box; a target in
        /// <paramref name="movers"/> is met along its own step. A virtual swarm member (no transform) is a cube
        /// of its contact radius, and is not materialised here. Main thread.
        /// </summary>
        public static bool TryFirstContact(PrismSpatialIndex index, Prism mover, in Box start, float3 step,
            in MoverSteps movers, out int slot, out float t)
        {
            slot = -1;
            t = float.MaxValue;
            if (index == null || !index.IsAvailable) return false;

            float reach = start.BoundRadius + MaxBoxReach;
            Vector3 a = start.Center, b = start.Center + step;

            var segment = SweepScratch.Segment;
            index.QuerySegment(a, b, reach, segment);
            for (int i = 0; i < segment.Count; i++)
            {
                var other = segment[i];
                if (!other || other == mover || other.destroyed) continue;
                int id = other.SpatialIndexId;
                if (id < 0 || !TryGetBox(other, out var box)) continue;
                if (movers.TryGetStep(id, out float3 otherStep)) box.Center -= otherStep;
                if (EntersContact(in start, step, in box, otherStep, out float hit) && hit < t)
                {
                    t = hit;
                    slot = id;
                }
            }
            segment.Clear();

            var virtuals = SweepScratch.Virtual;
            index.QuerySegmentVirtualIds(a, b, reach, virtuals);
            for (int i = 0; i < virtuals.Count; i++)
            {
                int id = virtuals[i];
                var cube = Box.Cube(index.StoredPosition(id), ContactRadius(index.ContactVolume(id)));
                if (EntersContact(in start, step, in cube, float3.zero, out float hit) && hit < t)
                {
                    t = hit;
                    slot = id;
                }
            }
            virtuals.Clear();

            return slot >= 0;
        }

        /// <summary>
        /// The one-shot path, for a mover that relocates ONE prism in a single step
        /// (<see cref="RhinoPloughPrismEffectSO"/>) — always the box path: the first prism the move from
        /// <paramref name="from"/> to <paramref name="to"/> runs into, and where the mover's pivot is at that
        /// moment. False when the path is clear, so the caller completes its move; otherwise the caller calls
        /// <see cref="Annihilate"/>. Main thread; every other prism is taken as standing where it is.
        /// </summary>
        public static bool TrySweep(Prism mover, Vector3 from, Vector3 to, out Prism struck, out Vector3 contactPoint)
        {
            struck = null;
            contactPoint = to;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || !TryGetBox(mover, out var box)) return false;

            // The box at the start of the move, wherever the transform stands now.
            box.Center += (float3)(from - mover.transform.position);
            if (!TryFirstContact(index, mover, in box, (float3)(to - from), default, out int slot, out float t))
                return false;

            // A virtual member is materialised only now that it is the hit.
            struck = index.ResolvePrism(slot, materialise: true);
            if (!struck || struck == mover || struck.destroyed) return false;
            contactPoint = Vector3.Lerp(from, to, t);
            return true;
        }

        // ------------------------------------------------------------------ spheres (many movers)

        /// <summary>One collision the batch path found: mover <see cref="Mover"/> (an index into the
        /// caller's mover arrays) runs into index slot <see cref="Slot"/>, <see cref="T"/> of the way
        /// through the step.</summary>
        public struct Contact
        {
            public int Mover;
            public int Slot;
            public float T;
        }

        /// <summary>
        /// The SPHERE batch path, for a mover that integrates many prisms per frame (the gravity field past
        /// its box budget): one
        /// mover per index, its EARLIEST contact over the step against every live index entry — static
        /// prisms at their stored position, and other movers (found through <see cref="MoverOfSlot"/>)
        /// along their own step. At most one <see cref="Contact"/> per mover, so a hit list of capacity
        /// = mover count never overflows.
        ///
        /// <para>Reads the index's hot arrays and buckets (<see cref="PrismSpatialIndex.TryGetMotionView"/>):
        /// schedule it after the step, and complete it before ANY index write, the stored positions
        /// being last frame's (= every mover's <see cref="From"/>).</para>
        /// </summary>
        [BurstCompile]
        public struct ContactJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float3> From;
            [ReadOnly] public NativeArray<float3> To;
            [ReadOnly] public NativeArray<byte> Valid;
            [ReadOnly] public NativeArray<int> SelfSlot;
            [ReadOnly] public NativeParallelHashMap<int, int> MoverOfSlot;

            [ReadOnly] public NativeArray<PrismSpatialData> Spatial;
            [ReadOnly] public NativeArray<PrismCellData> CellData;
            [ReadOnly] public NativeParallelMultiHashMap<int3, int> Buckets;
            public int HighWaterMark;
            public float BucketSize;

            public NativeList<Contact>.ParallelWriter Hits;

            public void Execute(int i)
            {
                if (Valid[i] == 0) return;
                int self = SelfSlot[i];
                if (self < 0 || self >= HighWaterMark) return;

                float3 a0 = From[i], a1 = To[i];
                float rSelf = ContactRadius(CellData[self].Volume);
                float reach = rSelf + MaxContactRadius;
                int3 min = (int3)math.floor((math.min(a0, a1) - reach) / BucketSize);
                int3 max = (int3)math.floor((math.max(a0, a1) + reach) / BucketSize);

                float best = float.MaxValue;
                int bestSlot = -1;
                for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    if (!Buckets.TryGetFirstValue(new int3(x, y, z), out int slot, out var it)) continue;
                    do
                    {
                        if (slot == self || slot < 0 || slot >= HighWaterMark) continue;
                        var s = Spatial[slot];
                        if ((s.Flags & PrismFlags.JobSkipMask) != PrismFlags.JobPassValue) continue;

                        float3 b0 = s.Position, b1 = s.Position;
                        if (MoverOfSlot.TryGetValue(slot, out int j))
                        {
                            if (Valid[j] == 0) continue;
                            b0 = From[j];
                            b1 = To[j];
                        }

                        float r = rSelf + ContactRadius(CellData[slot].Volume);
                        if (EntersContact(a0, a1, b0, b1, r, out float t) && t < best)
                        {
                            best = t;
                            bestSlot = slot;
                        }
                    } while (Buckets.TryGetNextValue(out slot, ref it));
                }

                if (bestSlot >= 0)
                    Hits.AddNoResize(new Contact { Mover = i, Slot = bestSlot, T = best });
            }
        }

        /// <summary>Fills <see cref="Map"/> with slot → mover index for the first <see cref="Count"/>
        /// movers (the map is cleared first and must already hold the capacity).</summary>
        [BurstCompile]
        public struct MapSlotsJob : IJob
        {
            [ReadOnly] public NativeArray<int> SelfSlot;
            public int Count;
            public NativeParallelHashMap<int, int> Map;

            public void Execute()
            {
                Map.Clear();
                for (int i = 0; i < Count; i++)
                    if (SelfSlot[i] >= 0) Map.TryAdd(SelfSlot[i], i);
            }
        }
    }
}
