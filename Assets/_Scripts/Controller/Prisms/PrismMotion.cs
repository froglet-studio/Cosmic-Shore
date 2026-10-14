using System.Collections.Generic;
using CosmicShore.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// PRISM MOVEMENT, the fundamental (Docs/claude/DESIGN_PHILOSOPHY_EMERGENCE.md § "Prism movement",
    /// design + verification <c>Docs/PRISM_MOTION.md</c>): the one set of expectations every force that
    /// DISPLACES a prism keeps, whoever applies it.
    ///
    /// <para><b>The collision rule.</b> A moving prism that runs into another prism destroys BOTH. "Runs
    /// into" is <see cref="EntersContact"/>: over the step, the two contact spheres go from apart to
    /// touching. Prisms that were already touching when the step began are at rest against each other —
    /// a trail's neighbours, a structure being carried whole — and are not a collision. Super-shielded
    /// mass is the one exception: it is indestructible, so it deflects (<see cref="Prism.AbsorbSuperShieldHit"/>)
    /// and only the mover is destroyed. An ordinary shield is no answer to a collision (devastating).</para>
    ///
    /// <para><b>The contact sphere.</b> A prism's <see cref="ContactRadius"/> is the radius of the sphere
    /// with its volume — its MASS as a sphere, the quantity the cells already weigh — capped at
    /// <see cref="MaxContactRadius"/>. One definition, used by the batch path (the gravity field's Burst
    /// <see cref="ContactJob"/>) and the one-shot path (<see cref="TrySweep"/>, the Rhino plough), so a
    /// prism shoved and a prism pulled meet the world by the same rule.</para>
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
        public static void Annihilate(Prism mover, Prism struck, Vector3 closingVelocity, Domains domain, string playerName)
        {
            Vector3 impact = closingVelocity * DebrisRestitution;
            if (struck && !struck.destroyed)
                struck.Damage(impact, domain, playerName, devastate: true, debrisSpeedLimit: DebrisSpeedLimit);
            if (mover && !mover.destroyed)
                mover.Damage(-impact, domain, playerName, devastate: true, debrisSpeedLimit: DebrisSpeedLimit);
        }

        // Scratch for the one-shot sweep, kept off PrismMotion itself so the Burst-compiled jobs below
        // never share a class with managed statics.
        static class SweepScratch
        {
            internal static readonly List<Prism> Segment = new();
            internal static readonly List<int> Virtual = new();
        }

        /// <summary>
        /// The one-shot path, for a mover that relocates a prism in a single step
        /// (<see cref="RhinoPloughPrismEffectSO"/>): the first prism the move from <paramref name="from"/> to
        /// <paramref name="to"/> runs into, and where along it. False (and nothing hit) when the path is
        /// clear, so the caller completes its move. The caller then calls <see cref="Annihilate"/>.
        /// Main thread; reads the spatial index's stored positions, so every other prism is taken as
        /// static for the step.
        /// </summary>
        public static bool TrySweep(Prism mover, Vector3 from, Vector3 to, out Prism struck, out Vector3 contactPoint)
        {
            struck = null;
            contactPoint = to;
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable || !mover) return false;

            float self = ContactRadius(mover.CachedVolume);
            float broad = self + MaxContactRadius;
            float3 a0 = from, a1 = to;
            float best = float.MaxValue;

            var segment = SweepScratch.Segment;
            index.QuerySegment(from, to, broad, segment);
            for (int i = 0; i < segment.Count; i++)
            {
                var other = segment[i];
                if (!other || other == mover || other.destroyed || other.SpatialIndexId < 0) continue;
                float3 b = index.StoredPosition(other.SpatialIndexId);
                if (EntersContact(a0, a1, b, b, self + ContactRadius(index.ContactVolume(other.SpatialIndexId)), out float t)
                    && t < best)
                {
                    best = t;
                    struck = other;
                }
            }

            // Virtual entries (data-driven swarm members) are mass too; one is materialised only if it
            // is the hit, after the ranking, so a sweep past a swarm costs nothing.
            int bestVirtual = -1;
            var virtuals = SweepScratch.Virtual;
            index.QuerySegmentVirtualIds(from, to, broad, virtuals);
            for (int i = 0; i < virtuals.Count; i++)
            {
                int slot = virtuals[i];
                float3 b = index.StoredPosition(slot);
                if (EntersContact(a0, a1, b, b, self + ContactRadius(index.ContactVolume(slot)), out float t) && t < best)
                {
                    best = t;
                    bestVirtual = slot;
                }
            }
            if (bestVirtual >= 0) struck = index.ResolvePrism(bestVirtual, materialise: true);

            segment.Clear();
            virtuals.Clear();
            if (!struck) return false;
            contactPoint = Vector3.Lerp(from, to, best);
            return true;
        }

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
        /// The batch path, for a mover that integrates many prisms per frame (the gravity field): one
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
