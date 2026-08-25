using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Batched PURE-ENTITY debris for prism-death VFX — the mass-death path
    /// (Docs/PRISM_ANIMATION.md B3, follow-up Prompt 9). Serves BOTH death
    /// visuals: the explosion (a vessel/AOE kill) and the suction implosion
    /// (fauna consumption).
    ///
    /// The pooled-GameObject carriers (<see cref="PrismExplosion"/> /
    /// <see cref="PrismImplosion"/>) charge Instantiate + OnEnable/OnDisable
    /// registry churn + a transform + a per-effect timer entry per death — and
    /// the implosion carrier additionally charges a per-instance MonoBehaviour
    /// <c>Update</c> (its watchdog) — all to do what an entity does for free:
    /// hold one pose and one clock stamp. Profiled on a 30³ lattice with the
    /// safety throttles lifted: 2,408 deaths in one frame were 2,408 pool
    /// misses, and PrismExplosion.OnDisable alone cost 1,863 ms of that frame.
    /// This path spawns a whole frame's deaths as ONE prototype-instantiate
    /// batch per family (PrismRenderService.Spawn*DebrisBatch), lets the GPU
    /// fly/shatter/fade or suck every piece in off the shader clock at FULL
    /// duration (no pressure shortening — nothing here costs per-frame CPU),
    /// and retires expired batches with ONE batched DestroyEntity per frame.
    ///
    /// The ONE piece of live state that survives the migration is the suction's
    /// MOVING convergence target: a fauna swims a long way during the ~2s
    /// implosion, so a snapshotted point would suck the mass toward where the
    /// creature WAS. That refresh is the documented §1 exception — one float3
    /// per live implosion per frame, the progress itself never touching the
    /// CPU — and it is why implosions carry a record with the target Transform
    /// while explosions carry only an entity and an end time.
    ///
    /// Continuity law: every death still animates out — this changes the
    /// carrier of the animation, never the animation. Clock-material law: one
    /// stamp at spawn, zero further writes (bar the §1 location), one scheduled
    /// retirement (the sweep — a flat time-ordered walk, never per-entity
    /// progress polling).
    ///
    /// The pooled path still exists as the route taken when this one declines a
    /// request, but do NOT read it as a visual fallback: with the render service
    /// off, a pooled explosion draws nothing and a pooled implosion draws a
    /// static block, both loudly (strict clock mode has no CPU animation tier by
    /// design). The pool prefabs' real remaining job is being the CONFIG source
    /// this class reads — mesh, material, layer, clamp band, duration — which is
    /// why retiring the pooled spawn path is a refactor rather than a deletion.
    /// Docs/PRISM_ANIMATION.md §4.6.
    /// </summary>
    public static class PrismDebris
    {
        // ── Explosion config (resolved once from the pooled effect prefab, so
        //    both paths ship IDENTICAL debris: same mesh, material, clamp band,
        //    duration) ─────────────────────────────────────────────────────────

        static Mesh s_mesh;
        static Material s_material;
        static int s_layer;
        static float s_minSpeed = 10f;
        static float s_maxSpeed = 33.33f;
        static float s_dangerDetonation = 1.6f;
        static bool s_configured;
        static PrismExplosion s_sourcePrefab;

        // ── Implosion config (same contract, read off the implosion prefab) ──

        static Mesh s_impMesh;
        static Material s_impMaterial;
        static int s_impLayer;
        static float s_impDuration = 2f;
        static bool s_impConfigured;
        static PrismImplosion s_impSourcePrefab;

        /// <summary>Object-space slack around the convergence point in the culling
        /// envelope — generous enough to absorb small per-frame target drift without
        /// re-growing bounds every frame. Mirrors
        /// PrismImplosion.ConvergenceBoundsPadding so both paths cull identically.</summary>
        const float ConvergenceBoundsPadding = 2f;

        // ── Pending spawns (this frame's deaths) and live records ────────────

        struct Record
        {
            public Entity Entity;
            public float EndTime;
        }

        /// <summary>A live suction entity. Carries what the moving-target refresh
        /// needs so the refresh never has to read a component back: the target
        /// transform, the (fixed) world→object matrix, and a CPU mirror of the
        /// object-space culling envelope.</summary>
        struct ImplosionRecord
        {
            public Entity Entity;
            public float EndTime;
            /// <summary>Live convergence target. Set to real null once it dies or the
            /// effect settles — the suction then freezes at the last known point,
            /// exactly as PrismImplosion.RefreshConvergenceForClock does.</summary>
            public Transform Target;
            public Matrix4x4 WorldToObject;
            public float3 BoundsMin;
            public float3 BoundsMax;
        }

        static readonly List<PrismRenderService.ExplosionDebrisSpawn> s_pending = new(256);

        /// <summary>Shared spawn/retire scratch. Safe ONLY because TickHost.LateUpdate
        /// runs Drain → DrainImplosions → Sweep → SweepImplosions strictly
        /// sequentially and each clears it before and after use. Reordering them,
        /// making any of them async, or calling one from outside the tick aliases the
        /// two families' batches — give the new caller its own list instead.</summary>
        static readonly List<Entity> s_scratchEntities = new(256);

        static readonly List<PrismRenderService.ImplosionDebrisSpawn> s_pendingImplosions = new(256);
        static readonly List<Transform> s_pendingImplosionTargets = new(256);
        static readonly List<PrismRenderService.ImplosionDebrisRefresh> s_refreshScratch = new(256);

        // Live records in append order. Durations are uniform (DefaultDuration /
        // the authored implosion duration), so append order IS expiry order and the
        // sweep only ever inspects the head. If per-spawn durations ever vary, a
        // shorter-lived entry behind a longer one is destroyed late — harmless (its
        // opacity is already 0), bounded by the duration spread.
        static readonly List<Record> s_live = new(1024);
        static int s_liveHead;
        static int s_liveEpoch = -1;

        static readonly List<ImplosionRecord> s_liveImplosions = new(1024);
        static int s_liveImplosionHead;
        static int s_liveImplosionEpoch = -1;

        static TickHost s_host;

        // After a failed batch spawn (world vanished between request and drain),
        // requests route to the pooled fallback for a few seconds instead of
        // being accepted and silently dropped again. Time-based so a rebuilt
        // world (playmode transition) re-enables the path on its own.
        static float s_suspendedUntil;
        static float s_implosionSuspendedUntil;
        const float SuspendSeconds = 5f;

        /// <summary>Explosion debris entities currently flying (diagnostics/readouts).</summary>
        public static int LiveDebrisCount => s_live.Count - s_liveHead;

        /// <summary>Suction debris entities currently converging (diagnostics/readouts).</summary>
        public static int LiveImplosionDebrisCount => s_liveImplosions.Count - s_liveImplosionHead;

        /// <summary>Deaths queued for this frame's batches (diagnostics).</summary>
        public static int PendingSpawnCount => s_pending.Count + s_pendingImplosions.Count;

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
            s_dangerDetonation = 1.6f;
            s_sourcePrefab = null;
            s_host = null;
            s_suspendedUntil = 0f;

            s_pendingImplosions.Clear();
            s_pendingImplosionTargets.Clear();
            s_refreshScratch.Clear();
            s_liveImplosions.Clear();
            s_liveImplosionHead = 0;
            s_liveImplosionEpoch = -1;
            s_impConfigured = false;
            s_impDuration = 2f;
            s_impMesh = null;
            s_impMaterial = null;
            s_impSourcePrefab = null;
            s_implosionSuspendedUntil = 0f;
        }

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
            s_dangerDetonation = prefab.DangerDetonationMultiplier;
            s_sourcePrefab = prefab;
            s_configured = true;
            return true;
        }

        /// <summary>
        /// The explosion config, for the ONE other producer of explosion-debris entities:
        /// the shield shatter (<see cref="PrismShieldShatter"/>), which spawns the SAME
        /// effect on the shield's own mesh — same material, same clamp band, same
        /// duration — so a shield coming apart IS a prism explosion, not an imitation of
        /// one. False while unconfigured (no pool yet): the caller refuses rather than
        /// inventing its own numbers.
        /// </summary>
        internal static bool TryGetExplosionConfig(out Material material,
            out float minSpeed, out float maxSpeed)
        {
            material = s_material;
            minSpeed = s_minSpeed;
            maxSpeed = s_maxSpeed;
            return s_configured;
        }

        /// <summary>Implosion counterpart of <see cref="Configure"/> — same contract,
        /// reading the mesh/material/timings off the pooled implosion prefab so the
        /// batched suction is visually identical to the pooled one.</summary>
        public static bool ConfigureImplosion(PrismImplosion prefab)
        {
            if (prefab == null) return s_impConfigured;
            if (s_impConfigured && prefab == s_impSourcePrefab) return true;

            // Renderer, not MeshRenderer: PrismImplosion is only
            // [RequireComponent(typeof(Renderer))] and serializes its renderer as a
            // Renderer, so this matches the component's own contract. The mesh still
            // comes from the MeshFilter — a prefab whose Renderer were NOT the
            // MeshFilter's own MeshRenderer would pair a mesh with a foreign material,
            // so both must resolve or the whole config is refused.
            var meshFilter = prefab.GetComponent<MeshFilter>();
            var renderer = prefab.GetComponent<Renderer>();
            if (meshFilter == null || meshFilter.sharedMesh == null ||
                renderer == null || renderer.sharedMaterial == null)
                return false;

            s_impMesh = meshFilter.sharedMesh;
            s_impMaterial = renderer.sharedMaterial;
            s_impLayer = prefab.gameObject.layer;
            // NOTE: the prefab's growDelay is deliberately NOT read for batched
            // implosion debris. It belongs to StartGrow (reverse suction) — used by
            // Sparrow turret ReverseSuction (`PrismType.Grow`, 2026-08-09). Every
            // batched suction is an implosion, which starts immediately (delay 0),
            // exactly like StartImplosion. The stamp still carries GrowDelay so the
            // shader contract stays complete for StartGrow on the pooled gameplay path.
            s_impDuration = prefab.ImplosionDuration;
            s_impSourcePrefab = prefab;
            s_impConfigured = true;
            return true;
        }

        /// <summary>
        /// Queues one death's debris for this frame's batch. Velocity semantics are
        /// EXACTLY PrismExplosion.TriggerExplosion's: apply the tier detonation gain, clamp
        /// to the (likewise scaled) [min, ceiling] where a positive
        /// <paramref name="speedLimitOverride"/> replaces the authored max (true-velocity
        /// impacts), and the shatter-rate channel keeps the pre-clamp magnitude on the legacy
        /// gain (load-bearing tuning). <paramref name="kind"/> is the tier the dying prism was
        /// wearing — the caller has already resolved its PALETTE from the same tier, this only
        /// drives the dynamics. Returns false when unconfigured or the render service is off —
        /// caller uses the pooled path.
        /// </summary>
        public static bool TryRequestExplosion(Vector3 position, Quaternion rotation, Vector3 scale,
            Color bright, Color dark, Vector3 velocity, float speedLimitOverride,
            PrismKind kind = PrismKind.Plain)
        {
            if (!s_configured || !PrismRenderService.Enabled) return false;
            if (Time.unscaledTime < s_suspendedUntil) return false;

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.up * s_minSpeed;

            float gain = PrismExplosion.DetonationGain(kind, s_dangerDetonation);
            velocity *= gain;

            bool hasOverride = speedLimitOverride > 0f;
            float ceiling = (hasOverride ? speedLimitOverride : s_maxSpeed) * gain;
            velocity = GeometryUtils.ClampMagnitude(velocity, s_minSpeed * gain, ceiling, out float speed);
            if (hasOverride) speed = velocity.magnitude;

            // Full length, always: on the entity path a live effect costs zero
            // per-frame CPU, so the pooled path's pressure model (which bounds
            // pool size and per-instance churn) has nothing to protect here.
            float duration = PrismExplosion.DefaultDuration;

            // Culling envelope: object-space end-of-flight offset (the entity
            // matrix never moves). Equivalent to InverseTransformVector for the
            // positive scales prisms use: inverse-rotate, divide per-axis scale.
            Vector3 flight = Quaternion.Inverse(rotation) * (velocity * duration);
            var objDisp = new float3(
                flight.x / Mathf.Max(1e-4f, Mathf.Abs(scale.x)),
                flight.y / Mathf.Max(1e-4f, Mathf.Abs(scale.y)),
                flight.z / Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
            float pad = 4f + 0.25f * math.length(objDisp);

            s_pending.Add(new PrismRenderService.ExplosionDebrisSpawn
            {
                LocalToWorld = Matrix4x4.TRS(position, rotation, scale),
                BrightColor = PrismRenderService.ToFloat4(bright),
                DarkColor = PrismRenderService.ToFloat4(dark),
                Velocity = new float3(velocity.x, velocity.y, velocity.z),
                Speed = speed,
                Duration = duration,
                ObjectDisplacement = objDisp,
                BoundsPadding = pad,
            });

            EnsureHost();
            return true;
        }

        /// <summary>
        /// Queues one consumed prism's suction for this frame's batch. Semantics are
        /// EXACTLY PrismImplosion.StartImplosion's: progress 0→1 over the authored
        /// duration, converging on <paramref name="target"/> — which is RETAINED (not
        /// snapshotted) so the sink tracks the creature as it swims, the §1 exception.
        /// Returns false when unconfigured, targetless, or the render service is off —
        /// caller uses the pooled path.
        /// </summary>
        public static bool TryRequestImplosion(Vector3 position, Quaternion rotation, Vector3 scale,
            Color bright, Color dark, Transform target)
        {
            if (!s_impConfigured || !PrismRenderService.Enabled) return false;
            if (Time.unscaledTime < s_implosionSuspendedUntil) return false;
            // A suction with nothing to converge on is not an implosion. The pooled
            // path guards the same way (PrismFactory's deferred-implosion drain drops
            // entries whose consumer died), so this defers rather than inventing a point.
            if (target == null) return false;

            var localToWorld = Matrix4x4.TRS(position, rotation, scale);
            Vector3 location = target.position;

            ComputeSuctionBounds(in localToWorld, location, out var bounds, out _, out _);

            s_pendingImplosions.Add(new PrismRenderService.ImplosionDebrisSpawn
            {
                LocalToWorld = localToWorld,
                BrightColor = PrismRenderService.ToFloat4(bright),
                DarkColor = PrismRenderService.ToFloat4(dark),
                Duration = s_impDuration,
                Direction = 1f,
                GrowDelay = 0f,
                Location = new float3(location.x, location.y, location.z),
                Bounds = bounds,
            });
            s_pendingImplosionTargets.Add(target);

            EnsureHost();
            return true;
        }

        /// <summary>
        /// The suction culling envelope: vertices lerp toward the convergence point,
        /// so RenderBounds must cover mesh ∪ that point or the collapsing geometry
        /// frustum-culls against the resting box (same class of bug as the explosion
        /// flight envelope). Computed in OBJECT space, since RenderBounds is.
        /// </summary>
        static void ComputeSuctionBounds(in Matrix4x4 localToWorld, Vector3 worldPoint,
            out Unity.Mathematics.AABB bounds, out float3 min, out float3 max)
        {
            var meshBounds = s_impMesh != null ? s_impMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            float3 mMin = (float3)(Vector3)(meshBounds.center - meshBounds.extents);
            float3 mMax = (float3)(Vector3)(meshBounds.center + meshBounds.extents);

            Vector3 obj = localToWorld.inverse.MultiplyPoint3x4(worldPoint);
            float3 p = new float3(obj.x, obj.y, obj.z);
            float3 pad = new float3(ConvergenceBoundsPadding);

            min = math.min(mMin, p - pad);
            max = math.max(mMax, p + pad);
            bounds = new Unity.Mathematics.AABB
            {
                Center = (min + max) * 0.5f,
                Extents = (max - min) * 0.5f,
            };
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
                DrainImplosions();
                Sweep();
                SweepImplosions();
                // After the sweep, so a record retired this frame is not written to.
                RefreshImplosions();
            }
        }

        static readonly Unity.Profiling.ProfilerMarker s_drainMarker = new("PrismDebris.Drain");
        static readonly Unity.Profiling.ProfilerMarker s_sweepMarker = new("PrismDebris.Sweep");
        static readonly Unity.Profiling.ProfilerMarker s_drainImplosionMarker = new("PrismDebris.DrainImplosions");
        static readonly Unity.Profiling.ProfilerMarker s_sweepImplosionMarker = new("PrismDebris.SweepImplosions");
        static readonly Unity.Profiling.ProfilerMarker s_refreshMarker = new("PrismDebris.RefreshConvergence");

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

        /// <summary>Spawns this frame's queued consumptions as one suction batch.</summary>
        static void DrainImplosions()
        {
            if (s_pendingImplosions.Count == 0) return;

            using (s_drainImplosionMarker.Auto())
            {
                s_scratchEntities.Clear();
                bool spawned = PrismRenderService.SpawnImplosionDebrisBatch(
                    s_impMesh, s_impMaterial, s_impLayer, s_pendingImplosions,
                    PrismClock.Now, s_scratchEntities);

                if (spawned)
                {
                    int epoch = PrismRenderService.CurrentEpoch;
                    if (s_liveImplosionEpoch != epoch)
                    {
                        s_liveImplosions.Clear();
                        s_liveImplosionHead = 0;
                        s_liveImplosionEpoch = epoch;
                    }

                    float now = PrismClock.Now;
                    for (int i = 0; i < s_scratchEntities.Count; i++)
                    {
                        var spawn = s_pendingImplosions[i];
                        float3 min = spawn.Bounds.Center - spawn.Bounds.Extents;
                        float3 max = spawn.Bounds.Center + spawn.Bounds.Extents;
                        s_liveImplosions.Add(new ImplosionRecord
                        {
                            Entity = s_scratchEntities[i],
                            EndTime = now + spawn.GrowDelay + spawn.Duration,
                            Target = s_pendingImplosionTargets[i],
                            WorldToObject = spawn.LocalToWorld.inverse,
                            BoundsMin = min,
                            BoundsMax = max,
                        });
                    }
                }
                else
                {
                    s_implosionSuspendedUntil = Time.unscaledTime + SuspendSeconds;
                    Debug.LogWarning($"[PrismDebris] Suction batch spawn failed for " +
                                     $"{s_pendingImplosions.Count} queued consumptions (render service: " +
                                     $"{PrismRenderService.StatusLine()}). " +
                                     $"Routing to the pooled path for {SuspendSeconds:F0}s.");
                }

                s_pendingImplosions.Clear();
                s_pendingImplosionTargets.Clear();
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

        /// <summary>Suction counterpart of <see cref="Sweep"/>. Clears each retired
        /// record's target reference so a finished effect can never keep a creature's
        /// Transform reachable from a static list.</summary>
        static void SweepImplosions()
        {
            if (LiveImplosionDebrisCount == 0)
            {
                if (s_liveImplosions.Count > 0) { s_liveImplosions.Clear(); s_liveImplosionHead = 0; }
                return;
            }

            using (s_sweepImplosionMarker.Auto())
            {
                if (s_liveImplosionEpoch != PrismRenderService.CurrentEpoch)
                {
                    s_liveImplosions.Clear();
                    s_liveImplosionHead = 0;
                    return;
                }

                float now = PrismClock.Now;
                int end = s_liveImplosionHead;
                while (end < s_liveImplosions.Count && s_liveImplosions[end].EndTime <= now) end++;
                if (end == s_liveImplosionHead) return;

                s_scratchEntities.Clear();
                for (int i = s_liveImplosionHead; i < end; i++)
                {
                    s_scratchEntities.Add(s_liveImplosions[i].Entity);
                    // Drop the managed reference immediately — a retired record must
                    // not root a fauna Transform until the next compaction.
                    var rec = s_liveImplosions[i];
                    rec.Target = null;
                    s_liveImplosions[i] = rec;
                }
                PrismRenderService.DestroyDebrisBatch(s_scratchEntities, s_liveImplosionEpoch);
                s_scratchEntities.Clear();
                s_liveImplosionHead = end;

                if (s_liveImplosionHead >= 1024 && s_liveImplosionHead * 2 >= s_liveImplosions.Count)
                {
                    s_liveImplosions.RemoveRange(0, s_liveImplosionHead);
                    s_liveImplosionHead = 0;
                }
            }
        }

        /// <summary>
        /// The §1 documented exception: refresh each live suction's convergence point
        /// to its target's CURRENT position — one float3 per live implosion per frame,
        /// and a bounds write only when the point wanders outside the stamped envelope
        /// Nothing here touches the animation's progress; the GPU owns that.
        ///
        /// Do NOT read the bounds write as rare. A grazing eater brakes to a hover for
        /// the feed hold, but it decays from maxSpeed — and LightFaunaDataSO's 6f is a
        /// stale initializer both shipped assets override (BrittleStar 25, Shark 35),
        /// so residual drift over the 2s suction is ~v0/k ≈ 6.25 world units, about
        /// half the 12-unit feeding cluster radius. The predation path does not brake
        /// at all (the predator's mouth keeps swimming), so it drifts further still.
        /// The envelope therefore grows a few times per effect, not never — and this
        /// is exactly why the convergence point may never be snapshotted at stamp time
        /// as an "optimization": the mass would converge on where the creature WAS.
        ///
        /// A target that dies mid-suction drops out (real-null'd) and the sink freezes
        /// at the last known point — the same degradation PrismImplosion has always had,
        /// because starvation and predation outlive this VFX.
        /// </summary>
        static void RefreshImplosions()
        {
            if (LiveImplosionDebrisCount == 0) return;
            if (s_liveImplosionEpoch != PrismRenderService.CurrentEpoch) return;

            using (s_refreshMarker.Auto())
            {
                s_refreshScratch.Clear();
                for (int i = s_liveImplosionHead; i < s_liveImplosions.Count; i++)
                {
                    var rec = s_liveImplosions[i];
                    // `is null` skips the Unity fake-null operator: once we have
                    // observed the target gone we real-null it, so the per-frame cost
                    // of a dead-target record collapses to a reference compare.
                    if (rec.Target is null) continue;
                    if (rec.Target == null)
                    {
                        rec.Target = null;
                        s_liveImplosions[i] = rec;
                        continue;
                    }

                    Vector3 world = rec.Target.position;
                    Vector3 objP = rec.WorldToObject.MultiplyPoint3x4(world);
                    float3 p = new float3(objP.x, objP.y, objP.z);
                    float3 pad = new float3(ConvergenceBoundsPadding);

                    bool grow = math.any(p - pad < rec.BoundsMin) || math.any(p + pad > rec.BoundsMax);
                    Unity.Mathematics.AABB bounds = default;
                    if (grow)
                    {
                        rec.BoundsMin = math.min(rec.BoundsMin, p - pad);
                        rec.BoundsMax = math.max(rec.BoundsMax, p + pad);
                        s_liveImplosions[i] = rec;
                        bounds = new Unity.Mathematics.AABB
                        {
                            Center = (rec.BoundsMin + rec.BoundsMax) * 0.5f,
                            Extents = (rec.BoundsMax - rec.BoundsMin) * 0.5f,
                        };
                    }

                    s_refreshScratch.Add(new PrismRenderService.ImplosionDebrisRefresh
                    {
                        Entity = rec.Entity,
                        Location = new float3(world.x, world.y, world.z),
                        GrowBounds = grow,
                        Bounds = bounds,
                    });
                }

                PrismRenderService.RefreshImplosionDebrisBatch(s_refreshScratch, s_liveImplosionEpoch);
                s_refreshScratch.Clear();
            }
        }
    }
}
