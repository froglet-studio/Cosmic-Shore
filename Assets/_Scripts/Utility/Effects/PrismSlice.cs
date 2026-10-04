using System.Collections.Generic;
using CosmicShore.ECS;
using CosmicShore.ScriptableObjects;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The Rhino sword's SLICE death (Docs/PRISM_ANIMATION.md §4.10,
    /// R_VesselActions/RHINO_ENERGY_SWORD.md § "The slice"). A prism the blade destroys is cut
    /// along the plane the blade swept through it: two halves, each a pure render entity drawing
    /// the shared <see cref="HighPolyPrismMesh"/>, part along the cut, open like a book, and
    /// dissolve away from the cut face — the whole course run on the GPU off one stamp
    /// (<c>PrismSlice.hlsl</c>).
    ///
    /// It is the explosion debris' shape (<see cref="PrismDebris"/>) with a different visual:
    /// requests queue during the frame, ONE prototype-instantiate batch per frame spawns them,
    /// and ONE batched destroy retires each expired batch. Nothing here writes to a live half —
    /// the clock-material law's stamp-and-retire, with no §1 exception.
    ///
    /// It never costs a death its visual. Every refusal — the config off or missing, the budget
    /// full, a degenerate cut, the render service down — returns false, and the caller
    /// (<c>PrismFactory.SpawnExplosion</c>) plays the ordinary explosion instead. The budget is
    /// the whole cost story: at most <see cref="PrismSliceConfigSO.MaxLiveSlices"/> slices alive,
    /// two halves each, so the triangle bill is bounded by
    /// <see cref="PrismSliceConfigSO.WorstCaseTriangles"/> however fast the blade kills. ZERO
    /// colliders, ever: the prism's own collider went with its destruction, and the halves are
    /// photons.
    /// </summary>
    public static class PrismSlice
    {
        public const string ConfigResourcePath = "PrismSliceConfig";

        static PrismSliceConfigSO s_config;
        static bool s_configResolved;

        /// <summary>The shared tuning (<c>Resources/PrismSliceConfig</c>), resolved once and cached.</summary>
        public static PrismSliceConfigSO Config
        {
            get
            {
                if (!s_configResolved)
                {
                    s_config = Resources.Load<PrismSliceConfigSO>(ConfigResourcePath);
                    s_configResolved = true;
                }
                return s_config;
            }
        }

        /// <summary>Editor tooling: forget the cached config and material so the next request re-reads them.</summary>
        public static void InvalidateConfig()
        {
            s_configResolved = false;
            s_config = null;
            DropRuntimeMaterial();
        }

        static readonly int MotionTimesId = Shader.PropertyToID("_SliceMotionTimes");
        static readonly int DissolveWindowId = Shader.PropertyToID("_SliceDissolveWindow");

        // A runtime CLONE of the config's material carrying the config's clock constants — the
        // shared asset is never written, and the clock has one source of truth (the config).
        static Material s_runtimeMaterial;
        static PrismSliceConfigSO s_runtimeMaterialFor;

        struct Record
        {
            public Entity Entity;
            public float EndTime;
        }

        static readonly List<PrismRenderService.SliceDebrisSpawn> s_pending = new(64);
        static readonly List<Entity> s_scratch = new(64);
        // Append order is expiry order: every slice lives the same configured life.
        static readonly List<Record> s_live = new(256);
        static int s_liveHead;
        static int s_liveEpoch = -1;
        static int s_pendingSlices;

        static TickHost s_host;
        static bool s_quitting;
        static float s_suspendedUntil;
        const float SuspendSeconds = 5f;

        /// <summary>Slices alive now (two render entities each) — diagnostics and the budget.</summary>
        public static int LiveSliceCount => (s_live.Count - s_liveHead) / 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_pending.Clear();
            s_scratch.Clear();
            s_live.Clear();
            s_liveHead = 0;
            s_liveEpoch = -1;
            s_pendingSlices = 0;
            s_host = null;
            s_quitting = false;
            s_suspendedUntil = 0f;
            s_config = null;
            s_configResolved = false;
            // The clone died with the previous play session's objects (or will be collected);
            // drop the reference rather than touching it.
            s_runtimeMaterial = null;
            s_runtimeMaterialFor = null;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
        }

        static void HandleQuitting() => s_quitting = true;

        /// <summary>
        /// Queues a slice of the prism that was at (<paramref name="position"/>,
        /// <paramref name="rotation"/>, <paramref name="scale"/>) along the plane through
        /// <paramref name="cutPoint"/> with world normal <paramref name="cutNormal"/>.
        /// <paramref name="velocity"/> is the impact's TRUE velocity (the blade's contact velocity
        /// times its restitution): it orients the opening and carries the halves. Colours are the
        /// tier's pair, already resolved by the caller from the same palette the explosion uses.
        /// Returns false — the caller explodes the prism instead — on any refusal.
        /// </summary>
        public static bool TryRequest(Vector3 position, Quaternion rotation, Vector3 scale,
            Color bright, Color dark, Vector3 velocity, Vector3 cutPoint, Vector3 cutNormal)
        {
            if (s_quitting || !PrismRenderService.Enabled) return false;
            if (Time.unscaledTime < s_suspendedUntil) return false;
            // The halves render on the explosion debris' layer, so a slice is only as available
            // as the explosion it replaces (PrismFactory configures that first).
            if (!PrismDebris.TryGetExplosionLayer(out _)) return false;

            var cfg = Config;
            if (cfg == null || !cfg.Enabled || !cfg.IsSane || cfg.Material == null) return false;
            if (LiveSliceCount + s_pendingSlices >= cfg.MaxLiveSlices) return false;

            if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
                velocity = Vector3.zero;

            var settings = new PrismSliceGeometry.Settings
            {
                MaxCutOffsetFraction = cfg.MaxCutOffsetFraction,
                SeparationFraction = cfg.SeparationFraction,
                MinSeparation = cfg.MinSeparation,
                OpenAngleMin = cfg.OpenAngleMinRadians,
                OpenAngleMax = cfg.OpenAngleMaxRadians,
                OpenAngleFullSpeed = cfg.OpenAngleFullSpeed,
                DriftFraction = cfg.DriftFraction,
                MaxDriftSpeed = cfg.MaxDriftSpeed,
                DriftDragSeconds = cfg.DriftDragSeconds,
            };
            if (!PrismSliceGeometry.TryBuild(position, rotation, scale, cutPoint, cutNormal, velocity,
                    in settings, out var a, out var b))
                return false;

            // One seed per slice, shared by both halves so the dissolve's raggedness lines up
            // across the cut. Hashed from the position: deterministic, and never two alike.
            float seed = math.frac(math.dot(new float3(position.x, position.y, position.z),
                new float3(0.1031f, 0.11369f, 0.13787f)) * 43.7585f);

            var model = Matrix4x4.TRS(position, rotation, scale);
            var brightF = PrismRenderService.ToFloat4(bright);
            var darkF = PrismRenderService.ToFloat4(dark);
            s_pending.Add(ToSpawn(in a, model, brightF, darkF, cfg.LifeSeconds, seed));
            s_pending.Add(ToSpawn(in b, model, brightF, darkF, cfg.LifeSeconds, seed));
            s_pendingSlices++;

            EnsureHost();
            return true;
        }

        static PrismRenderService.SliceDebrisSpawn ToSpawn(in PrismSliceGeometry.Half h, Matrix4x4 model,
            float4 bright, float4 dark, float life, float seed) => new()
        {
            LocalToWorld = model,
            BrightColor = bright,
            DarkColor = dark,
            Timing = new float4(0f, life, seed, 0f),
            Plane = h.Plane,
            Centre = h.Centre,
            Pivot = h.Pivot,
            Axis = h.Axis,
            Drift = h.Drift,
            Bounds = new AABB
            {
                Center = (float3)h.BoundsCenter,
                Extents = (float3)h.BoundsExtents,
            },
        };

        static Material ResolveRuntimeMaterial(PrismSliceConfigSO cfg)
        {
            if (s_runtimeMaterial != null && s_runtimeMaterialFor == cfg) return s_runtimeMaterial;
            DropRuntimeMaterial();
            if (cfg == null || cfg.Material == null) return null;

            s_runtimeMaterial = new Material(cfg.Material)
            {
                name = cfg.Material.name + " (runtime)",
                hideFlags = HideFlags.DontSave,
            };
            s_runtimeMaterial.SetVector(MotionTimesId,
                new Vector4(cfg.SeparateSeconds, cfg.OpenSeconds, cfg.DriftDragSeconds, 0f));
            s_runtimeMaterial.SetVector(DissolveWindowId,
                new Vector4(cfg.DissolveStart, cfg.DissolveEndMargin, 0f, 0f));
            s_runtimeMaterialFor = cfg;
            return s_runtimeMaterial;
        }

        static void DropRuntimeMaterial()
        {
            // InvalidateConfig is editor tooling and may run outside play mode, where Destroy is
            // refused; the clone is DontSave, so DestroyImmediate there is the right tool.
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
            var go = new GameObject("[PrismSlice]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            s_host = go.AddComponent<TickHost>();
        }

        // Just after PrismDebris (29000) and before the render service's visibility flush (30000):
        // a prism hidden by SetupDestruction this frame has its halves drawing the SAME frame.
        [DefaultExecutionOrder(29001)]
        sealed class TickHost : MonoBehaviour
        {
            void LateUpdate()
            {
                Drain();
                Sweep();
            }
        }

        static readonly Unity.Profiling.ProfilerMarker s_drainMarker = new("PrismSlice.Drain");
        static readonly Unity.Profiling.ProfilerMarker s_sweepMarker = new("PrismSlice.Sweep");

        static void Drain()
        {
            if (s_pending.Count == 0) return;

            using (s_drainMarker.Auto())
            {
                var cfg = Config;
                var material = ResolveRuntimeMaterial(cfg);
                bool spawned = material != null
                    && PrismDebris.TryGetExplosionLayer(out int layer)
                    && PrismRenderService.SpawnSliceDebrisBatch(
                        HighPolyPrismMesh.Get(cfg.Subdivision), material, layer,
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
                    // Accepted while the service looked usable, lost before the drain. Hold new
                    // requests so the factory goes back to exploding them, time-based so a rebuilt
                    // world re-enables the slice on its own. One log per suspension.
                    s_suspendedUntil = Time.unscaledTime + SuspendSeconds;
                    Debug.LogWarning($"[PrismSlice] Batch spawn failed for {s_pendingSlices} queued slices " +
                                     $"(render service: {PrismRenderService.StatusLine()}). These deaths have no " +
                                     $"visual; blade kills fall back to the explosion for {SuspendSeconds:F0}s.");
                }

                s_pending.Clear();
                s_scratch.Clear();
                s_pendingSlices = 0;
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

                if (s_liveHead >= 512 && s_liveHead * 2 >= s_live.Count)
                {
                    s_live.RemoveRange(0, s_liveHead);
                    s_liveHead = 0;
                }
            }
        }
    }
}
