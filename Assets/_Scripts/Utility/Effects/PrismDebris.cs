using System.Collections.Generic;
using CosmicShore.ECS;
using Unity.Entities;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Batched PURE-ENTITY debris for prism-death explosion VFX — the mass-death
    /// path (Docs/PRISM_ANIMATION.md B3, follow-up Prompt 9).
    ///
    /// The pooled-GameObject carrier (<see cref="PrismExplosion"/>) charges
    /// Instantiate + OnEnable/OnDisable registry churn + a transform + a
    /// per-effect timer entry per death, all to do two things an entity does
    /// for free: hold one pose and one clock stamp. Profiled on a 30³ lattice
    /// with the safety throttles lifted: 2,408 deaths in one frame were 2,408
    /// pool misses, and PrismExplosion.OnDisable alone cost 1,863 ms of that
    /// frame. This path spawns a whole frame's deaths as ONE prototype-
    /// instantiate batch (PrismRenderService.SpawnExplosionDebrisBatch), lets
    /// the GPU fly/shatter/fade every piece off the shader clock at FULL
    /// duration (no pressure shortening — nothing here costs per-frame CPU),
    /// and retires expired batches with ONE batched DestroyEntity per frame.
    ///
    /// Continuity law: every death still animates out — this changes the
    /// carrier of the animation, never the animation. Clock-material law: one
    /// stamp at spawn, zero further writes, one scheduled retirement (the
    /// sweep — a flat time-ordered walk, never per-entity progress polling).
    ///
    /// The pooled path remains as the fallback for when the render service is
    /// off (strict-mode diagnostics already cover that world) and for callers
    /// needing GameObject semantics.
    /// </summary>
    public static class PrismDebris
    {
        // ── Config (resolved once from the pooled effect prefab, so both paths
        //    ship IDENTICAL debris: same mesh, material, clamp band, duration) ──

        static Mesh s_mesh;
        static Material s_material;
        static int s_layer;
        static float s_minSpeed = 10f;
        static float s_maxSpeed = 33.33f;
        static bool s_configured;

        // ── Pending spawns (this frame's deaths) and live records ────────────

        struct Record
        {
            public Entity Entity;
            public float EndTime;
        }

        static readonly List<PrismRenderService.ExplosionDebrisSpawn> s_pending = new(256);
        static readonly List<Entity> s_scratchEntities = new(256);

        // Live records in append order. Durations are uniform (DefaultDuration),
        // so append order IS expiry order and the sweep only ever inspects the
        // head. If per-spawn durations ever vary, a shorter-lived entry behind a
        // longer one is destroyed late — harmless (its opacity is already 0),
        // bounded by the duration spread.
        static readonly List<Record> s_live = new(1024);
        static int s_liveHead;
        static int s_liveEpoch = -1;
        static TickHost s_host;

        // After a failed batch spawn (world vanished between request and drain),
        // requests route to the pooled fallback for a few seconds instead of
        // being accepted and silently dropped again. Time-based so a rebuilt
        // world (playmode transition) re-enables the path on its own.
        static float s_suspendedUntil;
        const float SuspendSeconds = 5f;

        /// <summary>Debris entities currently flying (diagnostics/readouts).</summary>
        public static int LiveDebrisCount => s_live.Count - s_liveHead;

        /// <summary>Deaths queued for this frame's batch (diagnostics).</summary>
        public static int PendingSpawnCount => s_pending.Count;

        // Enter-play-mode-without-domain-reload: statics survive, the old world
        // does not. Epoch/world guards make stale records inert, but start clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_pending.Clear();
            s_scratchEntities.Clear();
            s_live.Clear();
            s_liveHead = 0;
            s_liveEpoch = -1;
            s_configured = false;
            s_mesh = null;
            s_material = null;
            s_sourcePrefab = null;
            s_host = null;
            s_suspendedUntil = 0f;
        }

        static PrismExplosion s_sourcePrefab;

        /// <summary>
        /// Points the debris system at the pooled effect prefab it must match.
        /// Idempotent and cheap once configured — PrismFactory calls it on every
        /// explosion request so the config always tracks the active pool (a
        /// DIFFERENT prefab re-resolves; a null keeps whatever is configured).
        /// </summary>
        public static bool Configure(PrismExplosion prefab)
        {
            if (prefab == null) return s_configured;
            if (s_configured && prefab == s_sourcePrefab) return true;

            var meshFilter = prefab.GetComponent<MeshFilter>();
            var meshRenderer = prefab.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshFilter.sharedMesh == null ||
                meshRenderer == null || meshRenderer.sharedMaterial == null)
                return false;

            s_mesh = meshFilter.sharedMesh;
            s_material = meshRenderer.sharedMaterial;
            s_layer = prefab.gameObject.layer;
            s_minSpeed = prefab.MinDebrisSpeed;
            s_maxSpeed = prefab.MaxDebrisSpeed;
            s_sourcePrefab = prefab;
            s_configured = true;
            return true;
        }

        /// <summary>
        /// Queues one death's debris for this frame's batch. Velocity semantics are
        /// EXACTLY PrismExplosion.TriggerExplosion's: clamp to [min, ceiling] where a
        /// positive <paramref name="speedLimitOverride"/> replaces the authored max
        /// (true-velocity impacts), and the shatter-rate channel keeps the pre-clamp
        /// magnitude on the legacy gain (load-bearing tuning). Returns false when
        /// unconfigured or the render service is off — caller uses the pooled path.
        /// </summary>
        public static bool TryRequestExplosion(Vector3 position, Quaternion rotation, Vector3 scale,
            Color bright, Color dark, Vector3 velocity, float speedLimitOverride)
        {
            if (!s_configured || !PrismRenderService.Enabled) return false;
            if (Time.unscaledTime < s_suspendedUntil) return false;

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * s_minSpeed;

            bool hasOverride = speedLimitOverride > 0f;
            float ceiling = hasOverride ? speedLimitOverride : s_maxSpeed;
            velocity = GeometryUtils.ClampMagnitude(velocity, s_minSpeed, ceiling, out float speed);
            if (hasOverride) speed = velocity.magnitude;

            // Full length, always: on the entity path a live effect costs zero
            // per-frame CPU, so the pooled path's pressure model (which bounds
            // pool size and per-instance churn) has nothing to protect here.
            float duration = PrismExplosion.DefaultDuration;

            // Culling envelope: object-space end-of-flight offset (the entity
            // matrix never moves). Equivalent to InverseTransformVector for the
            // positive scales prisms use: inverse-rotate, divide per-axis scale.
            Vector3 flight = Quaternion.Inverse(rotation) * (velocity * duration);
            var objDisp = new Unity.Mathematics.float3(
                flight.x / Mathf.Max(1e-4f, Mathf.Abs(scale.x)),
                flight.y / Mathf.Max(1e-4f, Mathf.Abs(scale.y)),
                flight.z / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
            float pad = 4f + 0.25f * Unity.Mathematics.math.length(objDisp);

            s_pending.Add(new PrismRenderService.ExplosionDebrisSpawn
            {
                LocalToWorld = Matrix4x4.TRS(position, rotation, scale),
                BrightColor = PrismRenderService.ToFloat4(bright),
                DarkColor = PrismRenderService.ToFloat4(dark),
                Velocity = new Unity.Mathematics.float3(velocity.x, velocity.y, velocity.z),
                Speed = speed,
                Duration = duration,
                ObjectDisplacement = objDisp,
                BoundsPadding = pad,
            });

            EnsureHost();
            return true;
        }

        // ── Per-frame drive ──────────────────────────────────────────────────

        static void EnsureHost()
        {
            if (s_host != null) return;
            // HideInHierarchy, NOT HideAndDontSave — same reasoning as the render
            // service's visibility flush host (play-mode-exit cleanup applies).
            var go = new GameObject("[PrismDebris]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            s_host = go.AddComponent<TickHost>();
        }

        // Order 29000: after every gameplay LateUpdate has queued its deaths
        // (PrismFactory's deferred drain runs at default order), before the render
        // service's visibility flush (30000) and rendering — so a prism hidden by
        // SetupDestruction in Update has its debris drawing the SAME frame.
        [DefaultExecutionOrder(29000)]
        sealed class TickHost : MonoBehaviour
        {
            void LateUpdate()
            {
                Drain();
                Sweep();
            }
        }

        static readonly Unity.Profiling.ProfilerMarker s_drainMarker = new("PrismDebris.Drain");
        static readonly Unity.Profiling.ProfilerMarker s_sweepMarker = new("PrismDebris.Sweep");

        /// <summary>Spawns this frame's queued deaths as one batch.</summary>
        static void Drain()
        {
            if (s_pending.Count == 0) return;

            using (s_drainMarker.Auto())
            {
                s_scratchEntities.Clear();
                bool spawned = PrismRenderService.SpawnExplosionDebrisBatch(
                    s_mesh, s_material, s_layer, s_pending, PrismClock.Now, s_scratchEntities);

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

                    float now = PrismClock.Now;
                    for (int i = 0; i < s_scratchEntities.Count; i++)
                    {
                        s_live.Add(new Record
                        {
                            Entity = s_scratchEntities[i],
                            EndTime = now + s_pending[i].Duration,
                        });
                    }
                }
                else
                {
                    // Requests were accepted while the service looked usable but the
                    // world vanished before the drain — this batch's visuals are lost.
                    // Suspend so new requests actually route to the pooled fallback
                    // instead of being accepted and dropped again; time-based so a
                    // rebuilt world re-enables the path. One log per suspension.
                    s_suspendedUntil = Time.unscaledTime + SuspendSeconds;
                    Debug.LogWarning($"[PrismDebris] Batch spawn failed for {s_pending.Count} queued " +
                                     $"deaths (render service: {PrismRenderService.StatusLine()}). " +
                                     $"Routing to the pooled path for {SuspendSeconds:F0}s.");
                }

                s_pending.Clear();
                s_scratchEntities.Clear();
            }
        }

        /// <summary>Retires every record whose clock ran out — one batched destroy.</summary>
        static void Sweep()
        {
            if (LiveDebrisCount == 0)
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
                if (s_liveHead >= 1024 && s_liveHead * 2 >= s_live.Count)
                {
                    s_live.RemoveRange(0, s_liveHead);
                    s_liveHead = 0;
                }
            }
        }
    }
}
