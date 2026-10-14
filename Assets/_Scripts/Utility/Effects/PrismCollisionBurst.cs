using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.ScriptableObjects;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The COLLISION BURST (Docs/PRISM_ANIMATION.md §4.11, Docs/PRISM_MOTION.md § "The look"): how a
    /// prism destroyed by prism movement's collision rule comes apart at LEVEL 1 of the ladder — while
    /// few prisms move and the budget has room. One pure render entity per destroyed prism, drawing the
    /// shared UNWELDED high-poly prism (<see cref="HighPolyPrismMesh.GetUnwelded"/>), in one of four
    /// kinds — shatter, bloom, unravel, glitter — run entirely on the GPU off one stamp
    /// (<c>PrismCollisionBurst.hlsl</c>).
    ///
    /// <para><b>Spectacular when looked at, the explosion when squinted at.</b> The burst's FLIGHT is
    /// <see cref="PrismDebris.ResolveExplosionFlight"/> (the explosion's own clamp and gain), its LIFE
    /// is the explosion's, its PALETTE is the tier colours the factory resolved for the explosion, and
    /// its pieces stay within a prism-sized envelope of the travelling centre. Only the fine detail
    /// differs.</para>
    ///
    /// <para><b>It rides the slice's carrier unchanged</b> — <see cref="PrismRenderService.SpawnSliceDebrisBatch"/>
    /// and the slice's stamp channels (timing and drift), with its own mesh and material — so it added
    /// no ECS component, no prototype and no render-service code. Same shape as <see cref="PrismSlice"/>:
    /// requests queue during the frame, ONE batch spawns them, ONE batched destroy retires each expired
    /// batch, nothing writes to a live burst.</para>
    ///
    /// <para><b>It never costs a death its visual.</b> Every refusal — the config off or missing, the
    /// budget full, the render service down — returns false, and the caller
    /// (<c>PrismFactory.SpawnExplosion</c>) plays the ordinary explosion. Budget:
    /// <see cref="PrismMotionConfigSO.MaxLiveBursts"/> entities of 12·s² triangles. ZERO colliders.</para>
    /// </summary>
    public static class PrismCollisionBurst
    {
        /// <summary>The kinds, in <c>PrismCollisionBurst.hlsl</c>'s order.</summary>
        public const int Shatter = 0, Bloom = 1, Unravel = 2, Glitter = 3, KindCount = 4;

        /// <summary>
        /// How far from the travelling centre any piece of any kind can be, in prism radii — the SAME
        /// formula as <c>PrismBurstEnvelope</c> in the HLSL, whose harness proves every pose stays inside
        /// it. The culling bounds are sized by it.
        /// </summary>
        public static float EnvelopeRadii(float spread) => 3.5f + 1.5f * Mathf.Max(0f, spread);

        static readonly int ShapeId = Shader.PropertyToID("_BurstShape");
        static readonly int CurlId = Shader.PropertyToID("_BurstCurl");
        static readonly int FadeId = Shader.PropertyToID("_BurstFade");

        static Material s_runtimeMaterial;
        static PrismMotionConfigSO s_runtimeMaterialFor;

        struct Record
        {
            public Entity Entity;
            public float EndTime;
        }

        static readonly List<PrismRenderService.SliceDebrisSpawn> s_pending = new(64);
        static readonly List<Entity> s_scratch = new(64);
        // Append order is expiry order: every burst lives the explosion's one life.
        static readonly List<Record> s_live = new(128);
        static int s_liveHead;
        static int s_liveEpoch = -1;

        static TickHost s_host;
        static bool s_quitting;
        static float s_suspendedUntil;
        const float SuspendSeconds = 5f;

        /// <summary>Bursts alive now — diagnostics and the budget.</summary>
        public static int LiveCount => s_live.Count - s_liveHead;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_pending.Clear();
            s_scratch.Clear();
            s_live.Clear();
            s_liveHead = 0;
            s_liveEpoch = -1;
            s_host = null;
            s_quitting = false;
            s_suspendedUntil = 0f;
            s_runtimeMaterial = null;
            s_runtimeMaterialFor = null;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
        }

        static void HandleQuitting() => s_quitting = true;

        /// <summary>
        /// Whether a burst could be accepted right now (config on and sane, budget room, service up). The
        /// factory asks <see cref="TryRequest"/> directly; this is for diagnostics and tests.
        /// </summary>
        public static bool CanAfford(PrismMotionConfigSO cfg) =>
            cfg != null && cfg.BurstEnabled && cfg.IsSane && cfg.BurstMaterial != null &&
            LiveCount + s_pending.Count < cfg.MaxLiveBursts;

        /// <summary>
        /// Queues a collision burst of the prism that was at (<paramref name="position"/>,
        /// <paramref name="rotation"/>, <paramref name="scale"/>). <paramref name="impactVelocity"/>,
        /// <paramref name="speedLimitOverride"/> and <paramref name="kind"/> are exactly what the explosion
        /// would have been handed, so the burst leaves exactly like it. Returns false — the caller explodes
        /// the prism instead — on any refusal.
        /// </summary>
        public static bool TryRequest(Vector3 position, Quaternion rotation, Vector3 scale,
            Color bright, Color dark, Vector3 impactVelocity, float speedLimitOverride, PrismKind kind)
        {
            if (s_quitting || !PrismRenderService.Enabled) return false;
            if (Time.unscaledTime < s_suspendedUntil) return false;
            if (!PrismDebris.TryGetExplosionLayer(out _)) return false;

            var cfg = PrismMotionConfigSO.Instance;
            if (!CanAfford(cfg)) return false;

            Vector3 flight = PrismDebris.ResolveExplosionFlight(impactVelocity, speedLimitOverride, kind, out float speed);
            float life = PrismExplosion.DefaultDuration;

            // One seed per burst, hashed from the pose: deterministic on every peer, and never two alike.
            float seed = math.frac(math.dot(new float3(position.x, position.y, position.z),
                new float3(0.1031f, 0.11369f, 0.13787f)) * 43.7585f);
            int burstKind = PickKind(math.frac(seed * 7.919f + 0.371f),
                cfg.WeightOf(Shatter), cfg.WeightOf(Bloom), cfg.WeightOf(Unravel), cfg.WeightOf(Glitter));

            BuildBounds(rotation, scale, flight * life, cfg.Spread, out var centre, out var extents);

            s_pending.Add(new PrismRenderService.SliceDebrisSpawn
            {
                LocalToWorld = Matrix4x4.TRS(position, rotation, scale),
                BrightColor = PrismRenderService.ToFloat4(bright),
                DarkColor = PrismRenderService.ToFloat4(dark),
                Timing = new float4(0f, life, seed, burstKind),
                // The slice's unused channels at their unstamped values.
                Plane = float4.zero,
                Centre = new float4(0f, 0f, 0f, 1f),
                Pivot = float4.zero,
                Axis = float4.zero,
                Drift = new float4(flight.x, flight.y, flight.z, speed),
                Bounds = new AABB { Center = centre, Extents = extents },
            });

            EnsureHost();
            return true;
        }

        /// <summary>
        /// A weighted pick of a kind from <paramref name="roll01"/> in [0, 1). Zero-weight kinds are never
        /// picked; all weights zero picks the shatter.
        /// </summary>
        public static int PickKind(float roll01, float shatter, float bloom, float unravel, float glitter)
        {
            float total = Mathf.Max(0f, shatter) + Mathf.Max(0f, bloom) + Mathf.Max(0f, unravel) + Mathf.Max(0f, glitter);
            if (total <= 0f) return Shatter;
            float r = Mathf.Clamp01(roll01) * total;
            if ((r -= Mathf.Max(0f, shatter)) < 0f) return Shatter;
            if ((r -= Mathf.Max(0f, bloom)) < 0f) return Bloom;
            if ((r -= Mathf.Max(0f, unravel)) < 0f) return Unravel;
            if (glitter > 0f) return Glitter;
            // roll01 == 1 lands past the end: the last kind with weight.
            return unravel > 0f ? Unravel : bloom > 0f ? Bloom : Shatter;
        }

        /// <summary>
        /// The OBJECT-space culling box (the entity matrix never moves): every pose stays within
        /// <see cref="EnvelopeRadii"/> prism radii of the centre in WORLD space — an ellipsoid of semi-axes
        /// R / |scale_i| in object space — swept along the whole flight.
        /// </summary>
        public static void BuildBounds(Quaternion rotation, Vector3 scale, Vector3 worldFlight, float spread,
            out float3 centre, out float3 extents)
        {
            var s = new Vector3(Mathf.Max(1e-4f, Mathf.Abs(scale.x)), Mathf.Max(1e-4f, Mathf.Abs(scale.y)),
                Mathf.Max(1e-4f, Mathf.Abs(scale.z)));
            float radius = 0.5f * s.magnitude;
            float reach = EnvelopeRadii(spread) * radius;
            Vector3 local = Quaternion.Inverse(rotation) * worldFlight;
            var flightOS = new float3(local.x / s.x, local.y / s.y, local.z / s.z);
            centre = flightOS * 0.5f;
            extents = new float3(reach / s.x, reach / s.y, reach / s.z) + math.abs(flightOS) * 0.5f;
        }

        static Material ResolveRuntimeMaterial(PrismMotionConfigSO cfg)
        {
            if (s_runtimeMaterial != null && s_runtimeMaterialFor == cfg) return s_runtimeMaterial;
            DropRuntimeMaterial();
            if (cfg == null || cfg.BurstMaterial == null) return null;

            s_runtimeMaterial = new Material(cfg.BurstMaterial)
            {
                name = cfg.BurstMaterial.name + " (runtime)",
                hideFlags = HideFlags.DontSave,
            };
            s_runtimeMaterial.SetVector(ShapeId,
                new Vector4(cfg.Spread, cfg.OpenSeconds, cfg.SpinPerSpeed, cfg.HeatSeconds));
            s_runtimeMaterial.SetVector(CurlId,
                new Vector4(cfg.BloomTurns, cfg.UnravelTurns, cfg.RibbonsPerFace, cfg.ShardsPerFaceEdge));
            s_runtimeMaterial.SetVector(FadeId,
                new Vector4(cfg.DissolveGrain, cfg.EmberBand, cfg.CrackWidth, cfg.Glint));
            s_runtimeMaterialFor = cfg;
            return s_runtimeMaterial;
        }

        /// <summary>Editor tooling: forget the runtime clone so the next burst re-reads the config.</summary>
        public static void InvalidateConfig()
        {
            PrismMotionConfigSO.InvalidateInstance();
            DropRuntimeMaterial();
        }

        static void DropRuntimeMaterial()
        {
            if (s_runtimeMaterial != null)
            {
                if (Application.isPlaying) Object.Destroy(s_runtimeMaterial);
                else Object.DestroyImmediate(s_runtimeMaterial);
            }
            s_runtimeMaterial = null;
            s_runtimeMaterialFor = null;
        }

        static void EnsureHost()
        {
            if (s_host != null) return;
            if (s_quitting || !Application.isPlaying) return;
            var go = new GameObject("[PrismCollisionBurst]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            s_host = go.AddComponent<TickHost>();
        }

        // Beside the slice (29001), before the render service's visibility flush (30000): a prism hidden
        // by SetupDestruction this frame has its burst drawing the SAME frame.
        [DefaultExecutionOrder(29002)]
        sealed class TickHost : MonoBehaviour
        {
            void LateUpdate()
            {
                Drain();
                Sweep();
            }
        }

        static readonly Unity.Profiling.ProfilerMarker s_drainMarker = new("PrismCollisionBurst.Drain");
        static readonly Unity.Profiling.ProfilerMarker s_sweepMarker = new("PrismCollisionBurst.Sweep");

        static void Drain()
        {
            if (s_pending.Count == 0) return;

            using (s_drainMarker.Auto())
            {
                var cfg = PrismMotionConfigSO.Instance;
                var material = ResolveRuntimeMaterial(cfg);
                bool spawned = material != null
                    && PrismDebris.TryGetExplosionLayer(out int layer)
                    && PrismRenderService.SpawnSliceDebrisBatch(
                        HighPolyPrismMesh.GetUnwelded(cfg.BurstSubdivision), material, layer,
                        s_pending, PrismClock.Now, s_scratch);

                if (spawned)
                {
                    int epoch = PrismRenderService.CurrentEpoch;
                    if (s_liveEpoch != epoch)
                    {
                        s_live.Clear();
                        s_liveHead = 0;
                        s_liveEpoch = epoch;
                    }

                    float now = PrismClock.Now;
                    for (int i = 0; i < s_scratch.Count; i++)
                        s_live.Add(new Record { Entity = s_scratch[i], EndTime = now + s_pending[i].Timing.y });
                }
                else
                {
                    // Accepted while the service looked usable, lost before the drain. Hold new requests so
                    // collisions go back to the explosion, time-based so a rebuilt world re-enables it.
                    s_suspendedUntil = Time.unscaledTime + SuspendSeconds;
                    CSDebug.LogWarning($"[PrismCollisionBurst] Batch spawn failed for {s_pending.Count} queued bursts " +
                                     $"(render service: {PrismRenderService.StatusLine()}). These deaths have no visual; " +
                                     $"collisions fall back to the explosion for {SuspendSeconds:F0}s.");
                }

                s_pending.Clear();
                s_scratch.Clear();
            }
        }

        static void Sweep()
        {
            if (s_live.Count - s_liveHead == 0)
            {
                if (s_live.Count > 0) { s_live.Clear(); s_liveHead = 0; }
                return;
            }

            using (s_sweepMarker.Auto())
            {
                if (s_liveEpoch != PrismRenderService.CurrentEpoch)
                {
                    s_live.Clear();
                    s_liveHead = 0;
                    return;
                }

                float now = PrismClock.Now;
                int end = s_liveHead;
                while (end < s_live.Count && s_live[end].EndTime <= now) end++;
                if (end == s_liveHead) return;

                s_scratch.Clear();
                for (int i = s_liveHead; i < end; i++)
                    s_scratch.Add(s_live[i].Entity);
                PrismRenderService.DestroyDebrisBatch(s_scratch, s_liveEpoch);
                s_scratch.Clear();
                s_liveHead = end;

                if (s_liveHead >= 256 && s_liveHead * 2 >= s_live.Count)
                {
                    s_live.RemoveRange(0, s_liveHead);
                    s_liveHead = 0;
                }
            }
        }
    }
}
