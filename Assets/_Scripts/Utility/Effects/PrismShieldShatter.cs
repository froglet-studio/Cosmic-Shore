using System.Collections.Generic;
using CosmicShore.ECS;
using Unity.Entities;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The shield disengage overlay IS the prism explosion, applied to the shield's own
    /// mesh (Docs/PRISM_ANIMATION.md §4.8.1). A dropped octahedron / stellated
    /// super-shield spawns ONE explosion-debris entity per disengage — the same entity
    /// family, material (ExplodingBlockMaterial → ExplodingBlockGraph), clock stamps,
    /// per-face rotation (RotateFacesAlongAxis), erosion wipe and fade that a dying
    /// prism's debris gets — with the shield mesh in place of the cube: 1 triangle per
    /// octahedral face where the cube carries 4 per side. The shield meshes author the
    /// same attribute set the cube has (UV0 + per-face normals and tangents), which is
    /// the WHOLE port: the pipeline is never forked, the mesh conforms to it.
    ///
    /// This class therefore owns only what the debris path does not: grouping a frame's
    /// disengages into one batch PER SHIELD MESH (shields vary in size where death debris
    /// share one cube — PrismDebris batches on its single mesh), carrying the dying
    /// shield's own team palette, and the flat time-ordered retirement sweep. The
    /// velocity/clamp semantics are PrismDebris.TryRequestExplosion's, byte for byte,
    /// against the SAME pool-prefab band (PrismDebris.TryGetExplosionConfig) — including
    /// the zero-vector → up·minSpeed fallback, so a shield timer expiring sheds exactly
    /// the way an arena teardown explodes a prism.
    ///
    /// History, so nobody re-walks it: this effect shipped three bespoke shapes first — a
    /// shield-morph shatter branch re-expressing the rotation in HLSL, a mirrored
    /// back-face mesh bake (z-fought under Cull Off), and a BlockGraph erosion splice.
    /// All reverted. When the base effect already looks right, port the MESH into the
    /// pipeline, never the pipeline into the mesh.
    ///
    /// Continuity law: a shatter is never cancelled — re-engaging a shield while its
    /// predecessor's shards fly lets them finish. Clock-material law: one stamp at spawn,
    /// zero further writes, one scheduled retirement via the sweep. Strict mode: with the
    /// render service off a disengage has no overlay (the shield still drops), said once
    /// by PrismClockDiagnostics.
    /// </summary>
    public static class PrismShieldShatter
    {
        struct Record
        {
            public Entity Entity;
            public float EndTime;
        }

        /// <summary>One frame's queued shatters for a single (mesh, layer) pair — the
        /// granularity a batch spawn accepts. The material is always the debris
        /// pipeline's own (PrismDebris.TryGetExplosionConfig), so it is not part of the
        /// key; the team palette rides per-entity color overrides exactly as it does on
        /// death debris.</summary>
        sealed class PendingGroup
        {
            public Mesh Mesh;
            public int Layer;
            public readonly List<PrismRenderService.ExplosionDebrisSpawn> Spawns = new(32);
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
        /// Queues one shield disengage as a prism-explosion debris entity on the shield's
        /// own mesh. <paramref name="sharedShieldMesh"/> MUST be the cache-shared settled
        /// shield mesh (Octahedron/StellatedOctahedronMeshGenerator.GetSharedShieldMesh) —
        /// it carries the debris attribute set (UV0, per-face normals + tangents), and
        /// sharing it is what keeps same-size shields batched. Velocity/clamp semantics
        /// are EXACTLY PrismDebris.TryRequestExplosion's, against the same pool-prefab
        /// band; <paramref name="breakVelocity"/> is the RAW impact vector of whatever
        /// broke the shield (zero/NaN degrade exactly as the base does), and a positive
        /// <paramref name="speedLimitOverride"/> replaces the authored ceiling for
        /// true-velocity impacts, as on the death path. Returns false when the render
        /// service or the debris config is unavailable (strict mode: no fallback tier).
        /// </summary>
        public static bool TryRequest(Mesh sharedShieldMesh, int layer, Transform host,
            Color bright, Color dark, Vector3 breakVelocity, float speedLimitOverride)
        {
            if (sharedShieldMesh == null || host == null) return false;
            if (!PrismRenderService.Enabled) return false;
            if (Time.unscaledTime < s_suspendedUntil) return false;
            if (!PrismDebris.TryGetExplosionConfig(out _, out float minSpeed, out float maxSpeed))
                return false;

            // From here, PrismDebris.TryRequestExplosion's computation verbatim (shields
            // are never the danger tier — PrismStateManager keeps danger and the shield
            // tiers mutually exclusive — so the detonation gain is structurally 1).
            Vector3 velocity = breakVelocity;
            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * minSpeed;

            bool hasOverride = speedLimitOverride > 0f;
            float ceiling = hasOverride ? speedLimitOverride : maxSpeed;
            velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed, ceiling, out float speed);
            if (hasOverride) speed = velocity.magnitude;

            float duration = PrismExplosion.DefaultDuration;

            Vector3 position = host.position;
            Quaternion rotation = host.rotation;
            Vector3 scale = host.lossyScale;
            Vector3 flight = Quaternion.Inverse(rotation) * (velocity * duration);
            var objDisp = new Unity.Mathematics.float3(
                flight.x / Mathf.Max(1e-4f, Mathf.Abs(scale.x)),
                flight.y / Mathf.Max(1e-4f, Mathf.Abs(scale.y)),
                flight.z / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
            float pad = 4f + 0.25f * Unity.Mathematics.math.length(objDisp);

            GetOrCreateGroup(sharedShieldMesh, layer).Spawns.Add(
                new PrismRenderService.ExplosionDebrisSpawn
                {
                    LocalToWorld = Matrix4x4.TRS(position, rotation, scale),
                    BrightColor = PrismRenderService.ToFloat4(bright),
                    DarkColor = PrismRenderService.ToFloat4(dark),
                    Velocity = new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z),
                    Speed = speed,
                    Duration = duration,
                    ObjectDisplacement = objDisp,
                    BoundsPadding = pad,
                    // A shield face is ONE triangle, not a wedge of a four-wedge face, so
                    // the cube's derived pivot is wrong for it — off centre on the
                    // octahedron and outside the triangle entirely on the stellation,
                    // whose three lateral spike faces share one tetrahedron-face plane.
                    // Both generators bake the true per-face centroid into TEXCOORD1 for
                    // the engage bloom; this is the shatter reading the same channel.
                    // Docs/PRISM_ANIMATION.md §4.8.2.
                    FacePivotFromCentroid = 1f,
                });
            s_pendingCount++;

            if (CSDebug.IsVerbose(CSLogChannel.PrismShieldShatter))
                CSDebug.LogVerbose(CSLogChannel.PrismShieldShatter,
                    $"[ShieldShatter] {host.name}: queued debris |v|={speed:F2} u/s " +
                    $"dur={duration:F2}s mesh={sharedShieldMesh.vertexCount}v layer={layer}", host);

            EnsureHost();
            return true;
        }

        static PendingGroup GetOrCreateGroup(Mesh mesh, int layer)
        {
            for (int i = 0; i < s_pending.Count; i++)
            {
                var g = s_pending[i];
                if (ReferenceEquals(g.Mesh, mesh) && g.Layer == layer)
                    return g;
            }

            var group = s_groupPool.Count > 0 ? s_groupPool.Pop() : new PendingGroup();
            group.Mesh = mesh;
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
                    // The shards ARE explosion debris: same batch spawner, same override
                    // set, same material as a dying prism's pieces — only the mesh is the
                    // shield's. The config re-resolves at drain (not cached from the
                    // request) so a pool prefab swap between the two is honoured.
                    bool spawned = PrismDebris.TryGetExplosionConfig(
                            out Material debrisMaterial, out _, out _) &&
                        PrismRenderService.SpawnExplosionDebrisBatch(
                            group.Mesh, debrisMaterial, group.Layer, group.Spawns, now, s_scratchEntities);

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
                    // Drop the mesh reference so a retired group cannot root a shared
                    // shield mesh in a static pool.
                    group.Mesh = null;
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
