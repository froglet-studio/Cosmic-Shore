using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The swept rounds' broad phase, batched: once a frame, ONE PhysX <see cref="OverlapCapsuleCommand"/> batch
    /// and ONE Burst job over the prism index answer, for every round in flight, "could this frame's step touch a
    /// vessel / a prism at all?". A round told "no" skips that per-round query; a round told "maybe" (or not told,
    /// or told under changed conditions) runs it exactly as before. Hits, their order and their effects are
    /// therefore unchanged: the batch only removes queries that would have found nothing.
    ///
    /// <para><b>Why.</b> <see cref="Projectile.MoveProjectileAsync"/> sweeps each round's step for vessels
    /// (<c>Physics.OverlapCapsuleNonAlloc</c>) and prisms (<see cref="PrismSpatialIndex.QuerySegment"/>). Measured in
    /// the editor (Breakwater captures, ~110 Sparrow rounds in flight): ~1.7 us and ~3.6 us per round per frame. A
    /// Brittle Star holding its five guns keeps up to ~2000 rounds in the air (pool prewarm 1,400), so ~10 ms a frame,
    /// nearly all of it rounds in open space finding nothing.</para>
    ///
    /// <para><b>Why a "no" is exact.</b> Each round is batched with its frame ENVELOPE: from its position before the
    /// step to <c>position + Velocity * deltaTime</c>. The real step is <c>Velocity * deltaTime * factor</c> with the
    /// flight's cosine factor in (0, 1], so it is a prefix of the envelope, and the batched radius is the most the
    /// round's sweep radius can grow to (<see cref="Projectile.MaxSweepRadius"/>). Nothing a real query could find
    /// lies outside the envelope's capsule. The answer is used only when the round is still where it was batched, at
    /// the same velocity, in the same frame; for prisms only while <see cref="PrismSpatialIndex.WriteStamp"/> is
    /// unchanged (no prism added or moved since); for vessels the physics scene cannot change in between (no
    /// simulation or transform sync runs inside a frame's Update/LateUpdate here: autoSyncTransforms is off and
    /// nothing calls <c>Physics.SyncTransforms</c>).</para>
    ///
    /// <para><b>When.</b> The first swept round to step in a frame builds the batch for every registered round
    /// (<see cref="EnsureBuilt"/>), before any of them has moved. A round fired later that frame was not in it and
    /// queries as before. Below <see cref="MinRounds"/> rounds the batch is not built: scheduling costs more than a
    /// handful of queries.</para>
    /// </summary>
    public static class ProjectileSweepBatch
    {
        /// <summary>Fewer rounds than this in flight: no batch, every round queries as before.</summary>
        public const int MinRounds = 16;

        static readonly List<Projectile> s_live = new(256);
        static int s_builtFrame = -1;
        static int s_shipsMask;

        static readonly ProfilerMarker s_buildMarker = new("Projectile.SweepBatch");

        /// <summary>Rounds registered now (telemetry and tests).</summary>
        public static int LiveCount => s_live.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_live.Clear();
            s_builtFrame = -1;
            s_shipsMask = 0;
            s_warned = false;
        }

        /// <summary>Joins the batch. Idempotent; O(1).</summary>
        public static void Add(Projectile round)
        {
            if (!round || round.SweepBatchSlot >= 0) return;
            round.SweepBatchSlot = s_live.Count;
            s_live.Add(round);
        }

        /// <summary>Leaves the batch. Idempotent; O(1) (swap-remove).</summary>
        public static void Remove(Projectile round)
        {
            if (ReferenceEquals(round, null)) return;
            int slot = round.SweepBatchSlot;
            if (slot < 0) return;
            round.SweepBatchSlot = -1;
            if (slot >= s_live.Count || !ReferenceEquals(s_live[slot], round)) return;

            int last = s_live.Count - 1;
            if (slot != last)
            {
                var moved = s_live[last];
                s_live[slot] = moved;
                if (moved) moved.SweepBatchSlot = slot;
            }
            s_live.RemoveAt(last);
        }

        /// <summary>Builds this frame's batch if no round has yet. Call before the round moves.</summary>
        public static void EnsureBuilt()
        {
            int frame = Time.frameCount;
            if (s_builtFrame == frame) return;
            s_builtFrame = frame;
            try { Build(frame); }
            catch (System.Exception e)
            {
                // A batch that cannot be built leaves no stamps, so every round queries as before. Fail loud once,
                // never into the round that happened to ask (its flight would end on someone else's error).
                if (!s_warned) { s_warned = true; CosmicShore.Utility.CSDebug.LogError($"[ProjectileSweepBatch] build failed, rounds fall back to per-round queries: {e}"); }
            }
        }

        static bool s_warned;

        static void Build(int frame)
        {
            // A destroyed round (pool overflow) left without OnDisable reaching us: drop it.
            for (int i = s_live.Count - 1; i >= 0; i--)
                if (!s_live[i]) { s_live[i] = s_live[s_live.Count - 1]; if (s_live[i]) s_live[i].SweepBatchSlot = i; s_live.RemoveAt(s_live.Count - 1); }

            int n = s_live.Count;
            if (n < MinRounds) return;

            using var _ = s_buildMarker.Auto();

            if (s_shipsMask == 0) s_shipsMask = LayerMask.GetMask("Ships");
            var query = new QueryParameters(s_shipsMask, false, QueryTriggerInteraction.Collide, false);
            float dt = Time.deltaTime;

            var a = new NativeArray<float3>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var ab = new NativeArray<float3>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var prismRadius = new NativeArray<float>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var occupied = new NativeArray<byte>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var commands = new NativeArray<OverlapCapsuleCommand>(n, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var hits = new NativeArray<ColliderHit>(n, Allocator.TempJob);
            JobHandle physics = default;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    var round = s_live[i];
                    round.SweepEnvelope(dt, out Vector3 from, out Vector3 to, out float vesselRadius, out float prismR);
                    a[i] = from;
                    ab[i] = to - from;
                    prismRadius[i] = prismR;
                    commands[i] = new OverlapCapsuleCommand(from, to, vesselRadius, query);
                }

                // PhysX on the workers while the index job runs; maxHits 1 - the answer is "any", not "which".
                physics = s_shipsMask != 0
                    ? OverlapCapsuleCommand.ScheduleBatch(commands, hits, 32, 1)
                    : default;
                JobHandle.ScheduleBatchedJobs();

                var index = PrismSpatialIndex.Instance;
                bool indexLive = index && index.IsAvailable;
                if (indexLive) index.SegmentsOccupied(a, ab, prismRadius, occupied, n);
                int stamp = indexLive ? index.WriteStamp : 0;

                physics.Complete();

                for (int i = 0; i < n; i++)
                {
                    bool vesselsClear = s_shipsMask != 0 && hits[i].instanceID == 0;
                    bool prismsClear = indexLive && occupied[i] == 0;
                    s_live[i].StampSweepBatch(frame, stamp, vesselsClear, prismsClear);
                }
            }
            finally
            {
                physics.Complete();   // never dispose a buffer a PhysX worker may still be writing
                a.Dispose();
                ab.Dispose();
                prismRadius.Dispose();
                occupied.Dispose();
                commands.Dispose();
                hits.Dispose();
            }
        }
    }
}
