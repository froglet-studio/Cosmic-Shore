using System.Collections.Generic;
using CosmicShore.ECS;
using Unity.Entities;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Batched PURE-ENTITY debris for the SHIELD DISENGAGE overlay — the shards of a
    /// dropped octahedron / stellated super-shield flying out along their own face
    /// normals while shrinking to points (Docs/PRISM_ANIMATION.md §5 B4), now DRIFTING
    /// and TUMBLING along the impulse that broke the shield: the prism explosion's own
    /// initial condition, applied per face (§4.8.1). A shield does not fall apart on its
    /// own, so the effect takes the same input the explosion takes — a velocity — and a
    /// zero one degrades exactly to the symmetric puff it always was.
    ///
    /// The prism itself snaps back to its box mesh and its own companion entity the
    /// instant the shield drops (gameplay and rendering both final at t = 0), so the
    /// shards are necessarily a SEPARATE, short-lived visual — and a separate visual
    /// with one pose, one clock stamp and one retirement is exactly what an entity
    /// serves for free. What this replaces: a lazily-created child GameObject per
    /// prism, carrying a MeshFilter + MeshRenderer, whose mesh was REBUILT ON THE CPU
    /// every frame for the whole 0.6–0.7 s overlay, driven by the last sanctioned
    /// per-frame prism ticker (PrismOctahedronShieldManager, now deleted).
    ///
    /// Batching: shards render with the prism's own BlockGraph material on the
    /// cache-SHARED settled shield mesh, so a frame's worth of same-size, same-domain
    /// disengages is ONE draw — and it shares its (mesh × material) pair with every
    /// settled shielded prism of that size and domain, which is why the pending queue
    /// is grouped by (mesh, material, layer) rather than assuming one global pair the
    /// way <see cref="PrismDebris"/> can.
    ///
    /// Continuity law: a shatter is never cancelled. Re-engaging a shield while its
    /// predecessor's shards are still in the air lets them finish — instantly deleting
    /// visible mass-shaped geometry is precisely what "nothing pops out of existence"
    /// forbids, and the old code's StopShatter() did exactly that.
    ///
    /// Clock-material law: one stamp at spawn, ZERO further writes (there is no moving
    /// target here — unlike the suction, this effect is write-once), one scheduled
    /// retirement via a flat time-ordered sweep, never per-entity progress polling.
    ///
    /// There is no fallback carrier. Strict mode has no CPU animation tier: with the
    /// render service off, a disengage simply has no overlay (the shield still drops
    /// correctly), and <see cref="PrismClockDiagnostics"/> says so once.
    /// </summary>
    public static class PrismShieldShatter
    {
        struct Record
        {
            public Entity Entity;
            public float EndTime;
        }

        /// <summary>One frame's queued shatters for a single (mesh, material, layer)
        /// triple — the granularity a batch spawn accepts.</summary>
        sealed class PendingGroup
        {
            public Mesh Mesh;
            public Material Material;
            public int Layer;
            public readonly List<PrismRenderService.ShieldShatterSpawn> Spawns = new(32);
        }

        // Distinct groups in a frame are few (shield sizes × domains), so a linear scan
        // beats hashing two UnityEngine.Object references. Groups are recycled, never
        // reallocated, so a steady state costs no GC.
        static readonly List<PendingGroup> s_pending = new(8);
        static readonly Stack<PendingGroup> s_groupPool = new(8);
        static int s_pendingCount;

        static readonly List<Entity> s_scratchEntities = new(256);

        // Live records in append order. Shield durations are per-component constants
        // (0.6 s octahedron / 0.7 s stellation), so append order is expiry order up to
        // that spread; a shorter-lived entry queued behind a longer one is destroyed at
        // most 0.1 s late, by which point its faces have already collapsed to points.
        static readonly List<Record> s_live = new(256);
        static int s_liveHead;
        static int s_liveEpoch = -1;

        static TickHost s_host;

        // After a failed batch spawn (world vanished between request and drain),
        // requests are refused for a few seconds instead of being accepted and silently
        // dropped again. Time-based so a rebuilt world re-enables the path on its own.
        static float s_suspendedUntil;
        const float SuspendSeconds = 5f;

        /// <summary>Shard entities currently flying (diagnostics/readouts).</summary>
        public static int LiveShatterCount => s_live.Count - s_liveHead;

        /// <summary>Disengages queued for this frame's batches (diagnostics).</summary>
        public static int PendingSpawnCount => s_pendingCount;

        // Enter-play-mode-without-domain-reload: statics survive, the old world does
        // not. Epoch/world guards make stale records inert, but start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_pending.Clear();
            s_groupPool.Clear();
            s_pendingCount = 0;
            s_scratchEntities.Clear();
            s_live.Clear();
            s_liveHead = 0;
            s_liveEpoch = -1;
            s_host = null;
            s_suspendedUntil = 0f;
        }

        /// <summary>
        /// Queues one shield disengage's shards for this frame's batch.
        /// <paramref name="sharedShieldMesh"/> MUST be the cache-shared settled shield
        /// mesh (Octahedron/StellatedOctahedronMeshGenerator.GetSharedShieldMesh) — it
        /// carries the per-face centroids in TEXCOORD1 that the GPU morph needs, and
        /// sharing it is what keeps the shards batched. Returns false when the render
        /// service is off or unusable, in which case the disengage has no overlay
        /// (strict mode: no CPU fallback tier).
        /// </summary>
        /// <param name="velocity">
        /// WORLD-space velocity of the force that BROKE the shield — the prism explosion's
        /// own initial condition (Docs/PRISM_ANIMATION.md §4.8.1). Already clamped by the
        /// caller: the shield components own the speed cap, so the GPU stays a pure
        /// function of what it is handed. Zero (a timer expiring, an arena teardown, a
        /// herbivore stripping armour) is the identity — the symmetric puff.
        /// </param>
        /// <param name="objectDrift">
        /// The same velocity × duration mapped into the prism's object space, for the
        /// culling envelope. Passed in rather than derived because the caller already holds
        /// the Transform, and inverting <paramref name="localToWorld"/> per spawn would pay
        /// for that Transform twice.
        /// </param>
        public static bool TryRequest(Mesh sharedShieldMesh, Material material, int layer,
            in Matrix4x4 localToWorld, float duration, float offset,
            Vector3 velocity = default, Vector3 objectDrift = default)
        {
            if (sharedShieldMesh == null || material == null) return false;
            if (duration <= 0f) return false;
            if (!PrismRenderService.Enabled) return false;
            if (Time.unscaledTime < s_suspendedUntil) return false;

            GetOrCreateGroup(sharedShieldMesh, material, layer).Spawns.Add(
                new PrismRenderService.ShieldShatterSpawn
                {
                    LocalToWorld = localToWorld,
                    Duration = duration,
                    Offset = Mathf.Max(0f, offset),
                    Velocity = new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z),
                    ObjectDrift = new Unity.Mathematics.float3(objectDrift.x, objectDrift.y, objectDrift.z),
                });
            s_pendingCount++;

            EnsureHost();
            return true;
        }

        static PendingGroup GetOrCreateGroup(Mesh mesh, Material material, int layer)
        {
            for (int i = 0; i < s_pending.Count; i++)
            {
                var g = s_pending[i];
                if (ReferenceEquals(g.Mesh, mesh) && ReferenceEquals(g.Material, material) && g.Layer == layer)
                    return g;
            }

            var group = s_groupPool.Count > 0 ? s_groupPool.Pop() : new PendingGroup();
            group.Mesh = mesh;
            group.Material = material;
            group.Layer = layer;
            group.Spawns.Clear();
            s_pending.Add(group);
            return group;
        }

        // ── Per-frame drive ──────────────────────────────────────────────────

        static void EnsureHost()
        {
            if (s_host != null) return;
            // HideInHierarchy, NOT HideAndDontSave — same reasoning as the render
            // service's visibility flush host (play-mode-exit cleanup applies).
            var go = new GameObject("[PrismShieldShatter]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            s_host = go.AddComponent<TickHost>();
        }

        // Order 29000: after every gameplay LateUpdate has queued its disengages,
        // before the render service's visibility flush (30000) and rendering — so a
        // shield dropped in Update has its shards drawing the SAME frame, with no gap
        // between the octahedron vanishing and the shards appearing.
        [DefaultExecutionOrder(29000)]
        sealed class TickHost : MonoBehaviour
        {
            void LateUpdate()
            {
                Drain();
                Sweep();
            }
        }

        static readonly Unity.Profiling.ProfilerMarker s_drainMarker = new("PrismShieldShatter.Drain");
        static readonly Unity.Profiling.ProfilerMarker s_sweepMarker = new("PrismShieldShatter.Sweep");

        /// <summary>Spawns this frame's queued disengages — one batch per group.</summary>
        static void Drain()
        {
            if (s_pendingCount == 0) return;

            using (s_drainMarker.Auto())
            {
                float now = PrismClock.Now;
                bool anyFailed = false;

                for (int gi = 0; gi < s_pending.Count; gi++)
                {
                    var group = s_pending[gi];
                    if (group.Spawns.Count == 0) continue;

                    s_scratchEntities.Clear();
                    bool spawned = PrismRenderService.SpawnShieldShatterBatch(
                        group.Mesh, group.Material, group.Layer, group.Spawns, now, s_scratchEntities);

                    if (spawned)
                    {
                        // Epoch AFTER the spawn — TryEnsure may have just rebuilt the world.
                        int epoch = PrismRenderService.CurrentEpoch;
                        if (s_liveEpoch != epoch)
                        {
                            // Records from a previous world: those entities died with it.
                            s_live.Clear();
                            s_liveHead = 0;
                            s_liveEpoch = epoch;
                        }

                        for (int i = 0; i < s_scratchEntities.Count; i++)
                        {
                            s_live.Add(new Record
                            {
                                Entity = s_scratchEntities[i],
                                EndTime = now + group.Spawns[i].Duration,
                            });
                        }
                    }
                    else
                    {
                        anyFailed = true;
                    }

                    s_scratchEntities.Clear();
                }

                if (anyFailed)
                {
                    // Requests were accepted while the service looked usable but the
                    // world vanished before the drain — those overlays are lost.
                    // Suspend so new requests are refused outright instead of being
                    // accepted and dropped again; time-based so a rebuilt world
                    // re-enables the path. One log per suspension.
                    s_suspendedUntil = Time.unscaledTime + SuspendSeconds;
                    Debug.LogWarning($"[PrismShieldShatter] Batch spawn failed for queued shield " +
                                     $"disengages (render service: {PrismRenderService.StatusLine()}). " +
                                     $"Suppressing shatter overlays for {SuspendSeconds:F0}s.");
                }

                for (int gi = 0; gi < s_pending.Count; gi++)
                {
                    var group = s_pending[gi];
                    group.Spawns.Clear();
                    // Drop the material/mesh references so a retired group cannot root
                    // a domain material or a shared mesh in a static pool.
                    group.Mesh = null;
                    group.Material = null;
                    s_groupPool.Push(group);
                }
                s_pending.Clear();
                s_pendingCount = 0;
            }
        }

        /// <summary>Retires every record whose clock ran out — one batched destroy.</summary>
        static void Sweep()
        {
            if (LiveShatterCount == 0)
            {
                if (s_live.Count > 0) { s_live.Clear(); s_liveHead = 0; }
                return;
            }

            using (s_sweepMarker.Auto())
            {
                if (s_liveEpoch != PrismRenderService.CurrentEpoch)
                {
                    // World reset since these spawned — the entities are already gone.
                    s_live.Clear();
                    s_liveHead = 0;
                    return;
                }

                float now = PrismClock.Now;
                int end = s_liveHead;
                while (end < s_live.Count && s_live[end].EndTime <= now) end++;
                if (end == s_liveHead) return;

                s_scratchEntities.Clear();
                for (int i = s_liveHead; i < end; i++)
                    s_scratchEntities.Add(s_live[i].Entity);
                PrismRenderService.DestroyDebrisBatch(s_scratchEntities, s_liveEpoch);
                s_scratchEntities.Clear();
                s_liveHead = end;

                // Compact once the dead prefix dominates, so the list can't grow
                // without bound across a long session.
                if (s_liveHead >= 512 && s_liveHead * 2 >= s_live.Count)
                {
                    s_live.RemoveRange(0, s_liveHead);
                    s_liveHead = 0;
                }
            }
        }
    }
}
