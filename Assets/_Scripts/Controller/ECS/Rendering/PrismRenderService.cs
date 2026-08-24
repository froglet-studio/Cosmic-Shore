using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.ECS
{
    /// <summary>
    /// Opaque handle to a prism's companion render entity. Stored on the Prism
    /// MonoBehaviour and only ever dereferenced through PrismRenderService, which
    /// validates the epoch (ECS world generation) before touching the entity.
    /// </summary>
    public struct PrismRenderHandle
    {
        internal Entity Entity;
        internal int Epoch;

        public static readonly PrismRenderHandle Invalid = default;
    }

    /// <summary>
    /// Which per-instance override components a companion entity carries.
    /// Prism: the color trio. Explosion/Implosion: color trio + the effect
    /// shader's animated parameters.
    /// </summary>
    public enum PrismRenderOverrideSet
    {
        Prism,
        Explosion,
        Implosion,
    }

    /// <summary>
    /// Bridge between the GameObject prism pipeline and Entities Graphics
    /// (BatchRendererGroup) instanced rendering.
    ///
    /// WHY: on the legacy path every prism is its own MeshRenderer with a
    /// per-renderer MaterialPropertyBlock, which excludes it from the SRP
    /// Batcher — N live prisms ≈ N draw calls + N SetPass per frame, the root
    /// of the end-of-round frame collapse (see Docs/PRISM_ECS_MIGRATION.md).
    /// This service gives each prism a companion entity carrying
    /// LocalToWorld + MaterialMeshInfo + per-instance color overrides; all
    /// prisms sharing a mesh+material collapse into a handful of instanced
    /// batches with persistent GPU buffers.
    ///
    /// Scope (Checkpoint A): rendering ONLY. Gameplay, physics, triggers,
    /// trails, and the spatial index are untouched — the MeshRenderer is simply
    /// kept disabled while the entity draws in its place. A visual state that
    /// genuinely needs per-prism-unique geometry hands rendering back to the
    /// GameObject via Prism.SetExoticVisualActive — though as of the shield
    /// morph's GPU migration (Docs/PRISM_ANIMATION.md §5 B4) nothing in the
    /// project does: the shields animate their per-face bloom/shatter in the
    /// vertex stage on the cache-SHARED settled mesh, so they stay batched.
    ///
    /// ⚠ TRAP — a bare MeshFilter/material swap on a prism renders NOTHING.
    /// The GameObject's MeshRenderer is disabled while the companion entity
    /// draws, so any component that restyles a prism by swapping its
    /// MeshFilter mesh (or MeshRenderer materials) without the handoff shows
    /// no change on screen — the entity keeps drawing the plain box. This is
    /// exactly how the stellated super-shield first shipped invisible
    /// (PrismStellatedOctahedronShield predated the handoff). The contract for
    /// any new prism visual state: SetRenderMeshOverride(cachedSharedMesh) +
    /// SetExoticVisualActive(false) for anything shareable (so same-size
    /// prisms batch), SetExoticVisualActive(true) ONLY while genuinely showing
    /// per-prism-unique geometry — prefer a GPU morph over a shared mesh
    /// instead, which is what the shields now do — and ClearRenderMeshOverride
    /// + SetExoticVisualActive(false) on the way back AND on pool-return
    /// OnDisable. Reference implementations: PrismOctahedronShield /
    /// PrismStellatedOctahedronShield. Also listed in CLAUDE.md ▸ Anti-Patterns.
    ///
    /// All methods are main-thread only and no-op safely when the ECS world or
    /// EntitiesGraphicsSystem is unavailable (tool scenes, headless, teardown),
    /// so the legacy MeshRenderer path remains a complete fallback at runtime
    /// via the master toggle (PrismRenderConfigSO, runtime override, or the
    /// PRISM_RENDER_TOGGLE in the benchmark workflow).
    /// </summary>
    public static class PrismRenderService
    {
        // ------------------------------------------------------------------
        // Master toggle
        // ------------------------------------------------------------------

        static bool? _runtimeOverride;

        // Diagnostic overrides with no restore path of their own (the rest of this class's
        // statics self-heal through TryEnsure's dead-world branch).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOverrides()
        {
            _runtimeOverride = null;
            _linearizeOverride = null;
        }
        static bool _configLoaded;
        // OPT-IN: defaults OFF. The instanced path only renders correctly once the
        // prism ShaderGraphs expose their animated properties as Hybrid Per Instance
        // (see Docs/PRISM_ECS_MIGRATION.md §7); until that is verified in-editor the
        // legacy MeshRenderer path stays the baseline so a build is never broken by
        // default. Enable via the PrismRenderConfig asset or SetRuntimeOverride(true).
        static bool _configEnabled;
        static bool _configAssetFound;
        static bool _loggedActive;
        static bool _loggedWorldBootstrap;

        /// <summary>
        /// Master switch for instanced prism rendering. Resolution order:
        /// runtime override (benchmark A/B) → PrismRenderConfig asset in
        /// Resources → default OFF (opt-in; legacy path is the baseline).
        /// </summary>
        public static bool Enabled
        {
            get
            {
                bool enabled;
                if (_runtimeOverride.HasValue)
                {
                    enabled = _runtimeOverride.Value;
                }
                else
                {
                    if (!_configLoaded)
                    {
                        _configLoaded = true;
                        var config = Resources.Load<CosmicShore.ScriptableObjects.PrismRenderConfigSO>("PrismRenderConfig");
                        if (config != null) { _configAssetFound = true; _configEnabled = config.UseInstancedRendering; }
                    }
                    enabled = _configEnabled;
                }

                if (enabled && !_loggedActive)
                {
                    _loggedActive = true;
                    Debug.Log("[PrismRenderService] Instanced prism rendering is ACTIVE (Entities Graphics). " +
                              "If colors look uniform/mixed or explosions are frozen, the prism ShaderGraphs need " +
                              "'Hybrid Per Instance' on their animated properties — see Docs/PRISM_ECS_MIGRATION.md §7.");
                }
                return enabled;
            }
        }

        /// <summary>Runtime A/B override (null = fall back to config). New prisms follow the new value; existing ones keep their current path until reuse.</summary>
        public static void SetRuntimeOverride(bool? enabled) => _runtimeOverride = enabled;

        // ------------------------------------------------------------------
        // Clock-material animation (Docs/PRISM_ANIMATION.md, LOCKED law)
        // ------------------------------------------------------------------

        /// <summary>
        /// ALWAYS TRUE — clock-material animation is the ONLY prism animation path
        /// (STRICT MODE, locked by the prompter 2026-08-01: no legacy fallback).
        /// One initial-conditions stamp, the GPU runs the course, one scheduled end
        /// swap. A material whose graph is not wired (§4.4) does not fall back —
        /// its visual snaps to the end state and the stamp site logs a loud error
        /// naming the graph to wire. Kept as a property so call sites read as
        /// law-references, not as a toggle.
        /// </summary>
        public static bool ClockAnimationEnabled => true;

        /// <summary>
        /// Read-only, allocation-light diagnosis of whether the instanced path is
        /// actually engaging — and if not, exactly which link in the chain is broken
        /// (master toggle off / no config asset / no ECS world / no EntitiesGraphicsSystem).
        /// Surfaced on the DiagnosticsHUD so a "still N draw calls" symptom self-explains
        /// instead of failing silently. Does NOT create or cache the world.
        /// </summary>
        public static string StatusLine()
        {
            // Resolving Enabled also primes the config cache (_configAssetFound).
            if (!Enabled)
            {
                if (_runtimeOverride.HasValue) return "OFF (runtime override = false)";
                if (!_configAssetFound) return "OFF (no PrismRenderConfig asset in Resources)";
                return "OFF (config: Use Instanced Rendering unchecked)";
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return "OFF (no ECS world at runtime)";
            if (world.GetExistingSystemManaged<EntitiesGraphicsSystem>() == null)
                return "OFF (EntitiesGraphicsSystem missing — SRP/platform unsupported?)";

            // meshes/mats = distinct mesh & material assets the entities are registered under.
            // Entities Graphics batches by (mesh × material), so this is the batching ceiling:
            // if meshes/mats are tiny but draw calls stay ~= ents, the shader isn't instancing
            // (needs the Hybrid-Per-Instance variant); if meshes is huge, prisms don't share a mesh.
            return $"ON · ents={LiveEntityCount} meshes={_meshIds.Count} mats={_materialIds.Count}";
        }

        // ------------------------------------------------------------------
        // World / registration caches
        // ------------------------------------------------------------------

        static World _world;
        static EntitiesGraphicsSystem _graphics;
        static int _epoch; // bumped whenever the cached world goes away — invalidates all outstanding handles
        static readonly Dictionary<Mesh, BatchMeshID> _meshIds = new();
        static readonly Dictionary<Material, BatchMaterialID> _materialIds = new();

        // Prototype entities, one per (override set, layer). Creating a fresh entity
        // used to be ~8 structural changes (CreateEntity + RenderMeshUtility bundle +
        // one AddComponentData per override + DisableRendering) and EVERY structural
        // change is an archetype move that syncs running jobs — the measured
        // Prism.Create.Visibility cost (CompleteAllJobs under it), which grows with
        // the live entity count. Instantiating a prebuilt prototype is ONE structural
        // op; everything per-prism after that is SetComponentData (non-structural).
        // The Prefab tag keeps the prototype itself out of every query (it never
        // renders) and is stripped from clones automatically by Instantiate.
        static readonly Dictionary<(int layer, PrismRenderOverrideSet set), Entity> _prototypes = new();
        static int _prototypesEpoch = -1;

        /// <summary>Live companion entities (telemetry for the benchmark overlay).</summary>
        public static int LiveEntityCount { get; private set; }

        static bool TryEnsure()
        {
            if (_world != null && _world.IsCreated && _graphics != null)
                return true;

            // Cached world died (playmode exit / domain reload) — invalidate.
            if (_world != null)
            {
                _world = null;
                _graphics = null;
                _meshIds.Clear();
                _materialIds.Clear();
                // Pending keys are raw Entity ids with no epoch — a fresh world
                // restarts version counters, so stale keys could alias entities in
                // the new world and force-toggle an unrelated prism.
                s_pendingVisibility.Clear();
                LiveEntityCount = 0;
                _epoch++;
            }

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                // This render path is (currently) the project's only runtime Entities
                // usage, so if Unity's automatic world bootstrap is disabled the default
                // world may never be created — every prism would then silently fall back
                // to its MeshRenderer. Create it on demand instead of assuming it exists.
                // Guarded on null so it can never double-create; Initialize() also appends
                // the world's system groups (incl. EntitiesGraphicsSystem) to the player
                // loop so they actually update. No subscene / scene authoring required.
                try
                {
                    world = DefaultWorldInitialization.Initialize("Default World", false);
                    if (!_loggedWorldBootstrap)
                    {
                        _loggedWorldBootstrap = true;
                        Debug.Log("[PrismRenderService] No default ECS world found — bootstrapped one on demand for instanced prism rendering.");
                    }
                }
                catch (System.Exception e)
                {
                    if (!_loggedWorldBootstrap)
                    {
                        _loggedWorldBootstrap = true;
                        Debug.LogWarning("[PrismRenderService] Could not bootstrap a default ECS world; staying on the legacy MeshRenderer path. " + e.Message);
                    }
                    return false;
                }
            }
            if (world == null || !world.IsCreated)
                return false;

            var graphics = world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null)
                return false;

            _world = world;
            _graphics = graphics;
            _epoch++;
            return true;
        }

        static bool IsUsable(in PrismRenderHandle handle) =>
            handle.Epoch == _epoch &&
            handle.Entity != Entity.Null &&
            _world != null && _world.IsCreated &&
            _world.EntityManager.Exists(handle.Entity);

        /// <summary>True when the handle points at a live entity in the current world.</summary>
        public static bool IsHandleUsable(in PrismRenderHandle handle) => IsUsable(in handle);

        /// <summary>
        /// One-line diagnosis of WHY a grow stamp found no usable target — the
        /// strict-mode diagnostics quote this so a single repro run names the broken
        /// gate instead of a generic "no entity". Distinguishes: never created
        /// (EnsureRenderEntity declined), service off (with the StatusLine reason),
        /// stale epoch (service reset invalidated the handle), destroyed entity, and
        /// the masquerading case — an entity that EXISTS but lacks the grow clock
        /// overrides, which StampGrow also reports as failure.
        /// </summary>
        public static string DescribeGrowStampTarget(in PrismRenderHandle handle)
        {
            if (handle.Entity == Entity.Null)
                return "no companion entity was ever created — EnsureRenderEntity declined " +
                       "(null mesh/material/renderer, inactive hierarchy, or exotic visual active) " +
                       $"or the service was off at creation [service: {StatusLine()}]";
            if (_world == null || !_world.IsCreated)
                return $"ECS world gone [service: {StatusLine()}]";
            if (handle.Epoch != _epoch)
                return $"stale handle (created in service epoch {handle.Epoch}, current {_epoch} — " +
                       "a service/world reset invalidated it and the prism never re-created its entity)";
            if (!_world.EntityManager.Exists(handle.Entity))
                return "entity destroyed while the prism still holds the handle";
            if (!_world.EntityManager.HasComponent<PrismGrowStartTimeOverride>(handle.Entity))
                return "entity EXISTS but lacks the grow clock overrides (created with a non-Prism override set?)";
            return "target looks usable — if the stamp failed, re-check the call path";
        }

        static BatchMeshID GetMeshID(Mesh mesh)
        {
            if (!_meshIds.TryGetValue(mesh, out var id))
            {
                id = _graphics.RegisterMesh(mesh);
                _meshIds.Add(mesh, id);
            }
            return id;
        }

        static BatchMaterialID GetMaterialID(Material material)
        {
            if (!_materialIds.TryGetValue(material, out var id))
            {
                id = _graphics.RegisterMaterial(material);
                _materialIds.Add(material, id);
            }
            return id;
        }

        // ------------------------------------------------------------------
        // Shader property IDs (legacy-path parity reads off materials)
        // ------------------------------------------------------------------

        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int SpreadId = Shader.PropertyToID("_Spread");

        // ------------------------------------------------------------------
        // Color space
        // ------------------------------------------------------------------
        // DOTS-instanced per-instance data uploads to the GPU verbatim, while the legacy
        // path's colors went through Unity's color-space handling (per-renderer
        // MaterialPropertyBlock / material property upload). In a Linear color-space
        // project the legacy prisms therefore rendered the sRGB→linear CONVERTED values;
        // writing the same authored numbers raw reads brighter — most visibly on the dark
        // 'outside' color. Convert at this single write boundary so both paths render
        // identically. Runtime-overridable (the 'prismcolors' console command) for
        // in-editor A/B verification; affects colors written after the call.

        static bool? _linearizeOverride;
        static readonly bool IsLinearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;

        static bool LinearizeColors => _linearizeOverride ?? IsLinearColorSpace;

        /// <summary>Debug A/B hook: true = convert authored colors sRGB→linear at the
        /// entity write boundary (the default in Linear projects), false = write raw,
        /// null = automatic.</summary>
        public static void SetColorConversionOverride(bool? linearize) => _linearizeOverride = linearize;

        /// <summary>
        /// Applies the same color-space transform the legacy render path's property
        /// upload applied. Used internally by every color-writing API; call it yourself
        /// only when writing override components directly (stress-harness direct mode).
        /// </summary>
        public static float4 ApplyColorSpace(in float4 c) =>
            LinearizeColors
                ? new float4(Mathf.GammaToLinearSpace(c.x), Mathf.GammaToLinearSpace(c.y), Mathf.GammaToLinearSpace(c.z), c.w)
                : c;

        // ------------------------------------------------------------------
        // API
        // ------------------------------------------------------------------

        /// <summary>
        /// Prebuilt archetype donor for <see cref="Create"/> — see the _prototypes
        /// comment. Built lazily per (layer, override set); rebuilt when the world
        /// epoch changes.
        /// </summary>
        static Entity GetPrototype(int layer, PrismRenderOverrideSet overrideSet, Mesh mesh, Material material)
        {
            if (_prototypesEpoch != _epoch)
            {
                _prototypes.Clear();
                _prototypesEpoch = _epoch;
            }

            var em = _world.EntityManager;
            var key = (layer, overrideSet);
            if (_prototypes.TryGetValue(key, out var prototype) && em.Exists(prototype))
                return prototype;

            prototype = em.CreateEntity();

            var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
            var filter = desc.FilterSettings;
            filter.Layer = layer;
            desc.FilterSettings = filter;

            // Mesh/material here are only archetype placeholders — every clone gets
            // its real MaterialMeshInfo via SetComponentData in Create(). This is
            // load-bearing for prototype sharing: nothing else RenderMeshUtility
            // derives may vary per prism (shadows/probes are fixed by desc, layer is
            // part of the prototype key) — if a per-prism render flag is ever added,
            // it must join the key or the prototype pattern breaks.
            RenderMeshUtility.AddComponents(
                prototype, em, in desc,
                new MaterialMeshInfo(GetMaterialID(material), GetMeshID(mesh)));

            em.AddComponentData(prototype, new PrismBrightColorOverride { Value = new float4(1f) });
            em.AddComponentData(prototype, new PrismDarkColorOverride { Value = new float4(1f) });
            em.AddComponentData(prototype, new PrismSpreadOverride { Value = float3.zero });

            switch (overrideSet)
            {
                case PrismRenderOverrideSet.Explosion:
                    em.AddComponentData(prototype, new PrismVelocityOverride { Value = float3.zero });
                    em.AddComponentData(prototype, new PrismExplosionAmountOverride { Value = 0f });
                    em.AddComponentData(prototype, new PrismOpacityOverride { Value = 1f });
                    break;
                case PrismRenderOverrideSet.Implosion:
                    em.AddComponentData(prototype, new PrismImplosionStateOverride { Value = 0f });
                    em.AddComponentData(prototype, new PrismImplosionLocationOverride { Value = float3.zero });
                    break;
            }

            // Clock-material animation stamps (Docs/PRISM_ANIMATION.md §4). Added on
            // the PROTOTYPE so every per-prism stamp stays non-structural
            // SetComponentData — AddComponentData on a live entity is a per-prism
            // archetype move, the exact cost this prototype pattern exists to kill.
            // Defaults are the settled state (rate/duration 0 → PrismClockAnimation.hlsl
            // renders the end state), so entities render unchanged until a Stamp*
            // call arrives. Unconditional: the clock path is the only animation path.
            {
                switch (overrideSet)
                {
                    case PrismRenderOverrideSet.Prism:
                        em.AddComponentData(prototype, new PrismGrowStartTimeOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismGrowRateOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismGrowStartFracOverride { Value = new float3(1f) });
                        em.AddComponentData(prototype, new PrismColorStartTimeOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismColorDurationOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismStartBrightColorOverride { Value = new float4(1f) });
                        em.AddComponentData(prototype, new PrismStartDarkColorOverride { Value = new float4(1f) });
                        em.AddComponentData(prototype, new PrismStartSpreadOverride { Value = float3.zero });
                        em.AddComponentData(prototype, new PrismFlightStartTimeOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismFlightDurationOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismFlightVelocityOverride { Value = float3.zero });
                        em.AddComponentData(prototype, new PrismShieldMorphStartTimeOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismShieldMorphDurationOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismShieldMorphDirectionOverride { Value = ShieldMorphBloom });
                        em.AddComponentData(prototype, new PrismShieldMorphOffsetOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismJiggleStartTimeOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismJiggleDurationOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismJiggleParamsOverride { Value = float3.zero });
                        break;
                    case PrismRenderOverrideSet.Explosion:
                        em.AddComponentData(prototype, new PrismExplodeStartTimeOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismExplodeSpeedOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismExplodeDurationOverride { Value = 0f });
                        break;
                    case PrismRenderOverrideSet.Implosion:
                        em.AddComponentData(prototype, new PrismSuctionStartTimeOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismSuctionDurationOverride { Value = 0f });
                        em.AddComponentData(prototype, new PrismSuctionDirectionOverride { Value = 1f });
                        em.AddComponentData(prototype, new PrismSuctionGrowDelayOverride { Value = 0f });
                        break;
                }
            }

            // Clones are born hidden (existing contract); the Prefab tag keeps the
            // prototype itself invisible to every query and is stripped on Instantiate.
            em.AddComponent<DisableRendering>(prototype);
            em.AddComponent<Prefab>(prototype);

            _prototypes[key] = prototype;
            return prototype;
        }

        /// <summary>
        /// Creates a hidden companion render entity for a prism. Returns an
        /// invalid handle (and the caller stays on the MeshRenderer path) when
        /// the toggle is off or no ECS world / graphics system exists.
        /// One structural change (Instantiate from prototype); all per-prism
        /// state lands via non-structural SetComponentData.
        /// </summary>
        public static PrismRenderHandle Create(Mesh mesh, Material material, in Matrix4x4 localToWorld, int layer,
            PrismRenderOverrideSet overrideSet = PrismRenderOverrideSet.Prism)
        {
            if (!Enabled || mesh == null || material == null || !TryEnsure())
                return PrismRenderHandle.Invalid;

            var em = _world.EntityManager;
            var entity = em.Instantiate(GetPrototype(layer, overrideSet, mesh, material));

            em.SetComponentData(entity, new MaterialMeshInfo(GetMaterialID(material), GetMeshID(mesh)));
            em.SetComponentData(entity, new LocalToWorld { Value = ToFloat4x4(in localToWorld) });
            em.SetComponentData(entity, new RenderBounds
            {
                Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents }
            });

            em.SetComponentData(entity, new PrismBrightColorOverride { Value = ReadColor(material, BrightColorId) });
            em.SetComponentData(entity, new PrismDarkColorOverride { Value = ReadColor(material, DarkColorId) });
            em.SetComponentData(entity, new PrismSpreadOverride { Value = ReadVector3(material, SpreadId) });

            LiveEntityCount++;
            return new PrismRenderHandle { Entity = entity, Epoch = _epoch };
        }

        /// <summary>Shows/hides the entity immediately (DisableRendering tag add/remove —
        /// a structural change per call). Use for pooled VFX bursts and hand-offs to the
        /// GameObject renderer, where same-instant application matters. High-churn
        /// callers (the prism lifecycle) should use <see cref="QueueVisible"/>.</summary>
        public static void SetVisible(in PrismRenderHandle handle, bool visible)
        {
            if (!IsUsable(in handle)) return;
            // The immediate path is AUTHORITATIVE: drop any same-frame queued toggle
            // for this entity, or a stale queued SHOW could flush after an exotic
            // hand-off hid the entity and resurrect it alongside the MeshRenderer
            // (a ghost box drawing through the shield morph).
            s_pendingVisibility.Remove(handle.Entity);
            var em = _world.EntityManager;
            bool hidden = em.HasComponent<DisableRendering>(handle.Entity);
            if (visible && hidden)
                em.RemoveComponent<DisableRendering>(handle.Entity);
            else if (!visible && !hidden)
                em.AddComponent<DisableRendering>(handle.Entity);
        }

        // ------------------------------------------------------------------
        // Batched visibility — one structural change per direction per frame
        // ------------------------------------------------------------------

        // Desired-state map (last write this frame wins) flushed once in LateUpdate,
        // before rendering, by a hidden host. Per-entity DisableRendering toggles were
        // per-prism structural changes; the batch APIs move N entities in two ops.
        static readonly Dictionary<Entity, bool> s_pendingVisibility = new(64);
        static VisibilityFlushHost s_flushHost;
        static readonly Unity.Profiling.ProfilerMarker s_flushMarker = new("PrismRender.VisibilityFlush");

        /// <summary>
        /// Deferred SetVisible: applied in one batched structural change per direction
        /// at LateUpdate — same frame, before rendering, so nothing is ever visibly
        /// late. The prism lifecycle routes its show/hide churn through this.
        /// </summary>
        public static void QueueVisible(in PrismRenderHandle handle, bool visible)
        {
            if (!IsUsable(in handle)) return;
            s_pendingVisibility[handle.Entity] = visible;
            EnsureFlushHost();
        }

        static void EnsureFlushHost()
        {
            if (s_flushHost != null) return;
            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from
            // play-mode-exit cleanup and leaks one host into edit mode per session).
            var go = new GameObject("[PrismRenderVisibilityFlush]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            s_flushHost = go.AddComponent<VisibilityFlushHost>();
        }

        // Runs after every gameplay LateUpdate so same-frame toggles queued from
        // other LateUpdates still make this frame's flush (and render).
        [DefaultExecutionOrder(30000)]
        sealed class VisibilityFlushHost : MonoBehaviour
        {
            void LateUpdate() => FlushVisibility();
        }

        internal static void FlushVisibility()
        {
            if (s_pendingVisibility.Count == 0) return;
            if (_world == null || !_world.IsCreated)
            {
                s_pendingVisibility.Clear();
                return;
            }

            using (s_flushMarker.Auto())
            {
                var em = _world.EntityManager;
                var show = new Unity.Collections.NativeList<Entity>(s_pendingVisibility.Count, Unity.Collections.Allocator.Temp);
                var hide = new Unity.Collections.NativeList<Entity>(s_pendingVisibility.Count, Unity.Collections.Allocator.Temp);

                foreach (var kv in s_pendingVisibility)
                {
                    var entity = kv.Key;
                    if (!em.Exists(entity)) continue;
                    bool hidden = em.HasComponent<DisableRendering>(entity);
                    if (kv.Value && hidden) show.Add(entity);
                    else if (!kv.Value && !hidden) hide.Add(entity);
                }

                if (show.Length > 0)
                    em.RemoveComponent(show.AsArray(), ComponentType.ReadWrite<DisableRendering>());
                if (hide.Length > 0)
                    em.AddComponent(hide.AsArray(), ComponentType.ReadWrite<DisableRendering>());

                show.Dispose();
                hide.Dispose();
                s_pendingVisibility.Clear();
            }
        }

        /// <summary>Pushes the prism's current localToWorld matrix to the entity.</summary>
        public static void SetTransform(in PrismRenderHandle handle, in Matrix4x4 localToWorld)
        {
            if (!IsUsable(in handle)) return;
            _world.EntityManager.SetComponentData(handle.Entity, new LocalToWorld { Value = ToFloat4x4(in localToWorld) });
        }

        /// <summary>
        /// Swaps the entity's base material (domain / state / transparency
        /// changes). When refreshColors is true (not mid-animation) the
        /// per-instance overrides snap to the new material's authored values,
        /// matching what the legacy sharedMaterial swap displays.
        /// </summary>
        public static void SetMaterial(in PrismRenderHandle handle, Material material, bool refreshColors)
        {
            if (material == null || !IsUsable(in handle)) return;
            var em = _world.EntityManager;
            var mmi = em.GetComponentData<MaterialMeshInfo>(handle.Entity);
            mmi.MaterialID = GetMaterialID(material);
            em.SetComponentData(handle.Entity, mmi);

            if (refreshColors)
            {
                em.SetComponentData(handle.Entity, new PrismBrightColorOverride { Value = ReadColor(material, BrightColorId) });
                em.SetComponentData(handle.Entity, new PrismDarkColorOverride { Value = ReadColor(material, DarkColorId) });
                em.SetComponentData(handle.Entity, new PrismSpreadOverride { Value = ReadVector3(material, SpreadId) });
            }
        }

        /// <summary>
        /// Swaps the entity's mesh (settled octahedron shield ↔ prism box) and refreshes
        /// RenderBounds. The mesh registers once and is shared across entities, so
        /// same-geometry shielded prisms keep batching.
        /// </summary>
        public static void SetMesh(in PrismRenderHandle handle, Mesh mesh)
        {
            if (mesh == null || !IsUsable(in handle)) return;
            var em = _world.EntityManager;
            var mmi = em.GetComponentData<MaterialMeshInfo>(handle.Entity);
            mmi.MeshID = GetMeshID(mesh);
            em.SetComponentData(handle.Entity, mmi);
            em.SetComponentData(handle.Entity, new RenderBounds
            {
                Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents }
            });
        }

        /// <summary>Direct color write (stress test / tooling — live prisms use the
        /// clock stamps instead). Inputs are authored-space values; the color-space
        /// transform is applied here.</summary>
        public static void SetColors(in PrismRenderHandle handle, in float4 bright, in float4 dark, in float3 spread)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            em.SetComponentData(handle.Entity, new PrismBrightColorOverride { Value = ApplyColorSpace(in bright) });
            em.SetComponentData(handle.Entity, new PrismDarkColorOverride { Value = ApplyColorSpace(in dark) });
            em.SetComponentData(handle.Entity, new PrismSpreadOverride { Value = spread });
        }

        /// <summary>Team colors for effect entities (PrismFactory.ConfigureForTeam).
        /// Leaves _Spread at the material's authored value, matching the legacy MPB.</summary>
        public static void SetTeamColors(in PrismRenderHandle handle, in float4 bright, in float4 dark)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            em.SetComponentData(handle.Entity, new PrismBrightColorOverride { Value = ApplyColorSpace(in bright) });
            em.SetComponentData(handle.Entity, new PrismDarkColorOverride { Value = ApplyColorSpace(in dark) });
        }

        /// <summary>Initial implosion shader params (one-shot at effect start —
        /// progress itself rides the clock stamp).</summary>
        public static void SetImplosionParams(in PrismRenderHandle handle, float state, in float3 location)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            em.SetComponentData(handle.Entity, new PrismImplosionStateOverride { Value = state });
            em.SetComponentData(handle.Entity, new PrismImplosionLocationOverride { Value = location });
        }

        // ------------------------------------------------------------------
        // Clock-material stamps (Docs/PRISM_ANIMATION.md §4 — the law's
        // touchpoint 1). Each writes an animation's INITIAL CONDITIONS once;
        // PrismClockAnimation.hlsl evaluates the visual from _Time.y with zero
        // further CPU writes. All return false when the clock path is off or
        // the entity lacks the clock components (prototype built before an
        // override flip) so callers fall back to the legacy CPU managers.
        // Start times come from PrismClock.Now (same epoch as _Time.y).
        // ------------------------------------------------------------------

        /// <summary>Stamps a grow-in bloom: visual scales from startFrac (per axis, may
        /// exceed 1 for shrink-retargets) to 1 about the entity's FINAL transform
        /// (LocalToWorld must already hold final scale — gameplay-final-at-start).
        /// rate is the per-second exponential-approach k.</summary>
        public static bool StampGrow(in PrismRenderHandle handle, float startTime, float rate, in float3 startFrac)
        {
            if (!ClockAnimationEnabled || !IsUsable(in handle)) return false;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismGrowStartTimeOverride>(handle.Entity)) return false;
            em.SetComponentData(handle.Entity, new PrismGrowStartTimeOverride { Value = startTime });
            em.SetComponentData(handle.Entity, new PrismGrowRateOverride { Value = rate });
            em.SetComponentData(handle.Entity, new PrismGrowStartFracOverride { Value = startFrac });
            return true;
        }

        /// <summary>Settles just the grow stamp (visual snaps to final — call only when
        /// settled or covered). Safe no-op when the clock components are absent.</summary>
        public static void ClearGrowStamp(in PrismRenderHandle handle)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismGrowStartTimeOverride>(handle.Entity)) return;
            em.SetComponentData(handle.Entity, new PrismGrowStartTimeOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismGrowRateOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismGrowStartFracOverride { Value = new float3(1f) });
        }

        /// <summary>Settles just the color-transition stamp (visual snaps to the bound
        /// material's colors — the scheduled settle, or an interruption reset).</summary>
        public static void ClearColorTransitionStamp(in PrismRenderHandle handle)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismColorStartTimeOverride>(handle.Entity)) return;
            em.SetComponentData(handle.Entity, new PrismColorStartTimeOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismColorDurationOverride { Value = 0f });
        }

        /// <summary>Stamps a color/state transition: shader lerps from the given start
        /// colors (authored-space; color-space transform applied here, matching SetColors)
        /// to the bound material's target colors over duration. The settle swap to the
        /// end-state material is scheduled separately (touchpoint 3).</summary>
        public static bool StampColorTransition(in PrismRenderHandle handle, float startTime, float duration,
            in float4 startBright, in float4 startDark, in float3 startSpread)
        {
            if (!ClockAnimationEnabled || !IsUsable(in handle)) return false;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismColorStartTimeOverride>(handle.Entity)) return false;
            em.SetComponentData(handle.Entity, new PrismColorStartTimeOverride { Value = startTime });
            em.SetComponentData(handle.Entity, new PrismColorDurationOverride { Value = duration });
            em.SetComponentData(handle.Entity, new PrismStartBrightColorOverride { Value = ApplyColorSpace(in startBright) });
            em.SetComponentData(handle.Entity, new PrismStartDarkColorOverride { Value = ApplyColorSpace(in startDark) });
            em.SetComponentData(handle.Entity, new PrismStartSpreadOverride { Value = startSpread });
            return true;
        }

        /// <summary>
        /// Stamps a ballistic FLIGHT on a live prism (Docs/PRISM_ANIMATION.md §5 C5 —
        /// the Sparrow's turret-fired prisms). The entity transform must already hold
        /// the flight's END POINT: the shader walks the visual in from the muzzle and
        /// reaches the transform exactly at <paramref name="duration"/>, so collider,
        /// volume and spatial registration are free to be final from the stamp.
        ///
        /// <paramref name="velocity"/> is the WORLD-space muzzle velocity (units/s);
        /// velocity·2·duration/π is the full flight vector. The world→object conversion
        /// happens on the GPU inside PrismFlightClock (raw inverse-model multiply, NOT
        /// the normalizing Direction-mode Transform node).
        ///
        /// Expand RenderBounds by the object-space MUZZLE offset after stamping, or the
        /// prism frustum-culls against its anchor-point box while the visual is still
        /// out at the barrel.
        /// </summary>
        public static bool StampFlight(in PrismRenderHandle handle, float startTime, float duration,
            in float3 velocity)
        {
            if (!ClockAnimationEnabled || !IsUsable(in handle)) return false;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismFlightStartTimeOverride>(handle.Entity)) return false;
            em.SetComponentData(handle.Entity, new PrismFlightStartTimeOverride { Value = startTime });
            em.SetComponentData(handle.Entity, new PrismFlightDurationOverride { Value = duration });
            em.SetComponentData(handle.Entity, new PrismFlightVelocityOverride { Value = velocity });
            return true;
        }

        /// <summary>Settles the flight stamp — the visual snaps to the entity transform,
        /// which is the anchor point. Call at the scheduled arrival, or early when a hit
        /// cuts the flight short (after re-posing the transform to the impact point).</summary>
        public static void ClearFlightStamp(in PrismRenderHandle handle)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismFlightStartTimeOverride>(handle.Entity)) return;
            em.SetComponentData(handle.Entity, new PrismFlightStartTimeOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismFlightDurationOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismFlightVelocityOverride { Value = float3.zero });
        }

        /// <summary>Shield-morph direction: faces bloom outward from their centroids.</summary>
        public const float ShieldMorphBloom = 1f;

        /// <summary>Shield-morph direction: faces shrink to their centroids while flying
        /// out along their normals (the disengage overlay).</summary>
        public const float ShieldMorphShatter = -1f;

        /// <summary>
        /// Stamps a SHIELD MORPH on a live prism (Docs/PRISM_ANIMATION.md §5 B4): the
        /// engage bloom (<paramref name="direction"/> &gt;= 0) or the shatter
        /// (&lt; 0, faces flying out <paramref name="offset"/> local units along their
        /// normals). The entity must already hold the SETTLED shield mesh — the vertex
        /// stage collapses/expands its faces about the per-face centroids baked into
        /// TEXCOORD1, so gameplay state, collider, mass and render mesh are all final
        /// from the stamp and the shield never leaves the instanced path.
        /// </summary>
        public static bool StampShieldMorph(in PrismRenderHandle handle, float startTime,
            float duration, float direction, float offset)
        {
            if (!ClockAnimationEnabled || !IsUsable(in handle)) return false;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismShieldMorphStartTimeOverride>(handle.Entity)) return false;
            em.SetComponentData(handle.Entity, new PrismShieldMorphStartTimeOverride { Value = startTime });
            em.SetComponentData(handle.Entity, new PrismShieldMorphDurationOverride { Value = duration });
            em.SetComponentData(handle.Entity, new PrismShieldMorphDirectionOverride { Value = direction });
            em.SetComponentData(handle.Entity, new PrismShieldMorphOffsetOverride { Value = offset });
            return true;
        }

        /// <summary>Settles the shield-morph stamp — the mesh renders exactly as authored.
        /// REQUIRED on disengage and on pool reuse: a stale stamp left on an entity that has
        /// gone back to the prism's own box mesh would morph that box against face centroids
        /// it does not carry.</summary>
        public static void ClearShieldMorphStamp(in PrismRenderHandle handle)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismShieldMorphStartTimeOverride>(handle.Entity)) return;
            em.SetComponentData(handle.Entity, new PrismShieldMorphStartTimeOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismShieldMorphDurationOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismShieldMorphDirectionOverride { Value = ShieldMorphBloom });
            em.SetComponentData(handle.Entity, new PrismShieldMorphOffsetOverride { Value = 0f });
        }

        /// <summary>
        /// Stamps a super-shield DEFLECTION: a super-shielded prism absorbed a hit without
        /// being destroyed, and every face wobbles about the prism's object origin on a
        /// precessing, nutating axis before settling (Docs/PRISM_ANIMATION.md §5 C14).
        ///
        /// params_ is (peak tilt RADIANS, precession rad/s, nutation rad/s). Nothing about
        /// gameplay changes — super-shielded mass stays invulnerable; this is photons only.
        ///
        /// Re-stamping supersedes an in-flight wobble (interruption = re-stamp, §1): the
        /// visual is analytic, so a second hit simply restarts the envelope.
        ///
        /// Expand RenderBounds by the rotation's envelope after stamping, or a wobbling
        /// prism frustum-culls against its resting box.
        /// </summary>
        public static bool StampJiggle(in PrismRenderHandle handle, float startTime, float duration,
            in float3 params_)
        {
            if (!ClockAnimationEnabled || !IsUsable(in handle)) return false;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismJiggleStartTimeOverride>(handle.Entity)) return false;
            em.SetComponentData(handle.Entity, new PrismJiggleStartTimeOverride { Value = startTime });
            em.SetComponentData(handle.Entity, new PrismJiggleDurationOverride { Value = duration });
            em.SetComponentData(handle.Entity, new PrismJiggleParamsOverride { Value = params_ });
            return true;
        }

        /// <summary>Settles the jiggle stamp. Invisible when called at the scheduled end —
        /// the shader's envelope is already exactly zero there (verified against the shipped
        /// HLSL), so this only stops the GPU evaluating a finished wobble and keeps a pooled
        /// reuse from inheriting one.</summary>
        public static void ClearJiggleStamp(in PrismRenderHandle handle)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismJiggleStartTimeOverride>(handle.Entity)) return;
            em.SetComponentData(handle.Entity, new PrismJiggleStartTimeOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismJiggleDurationOverride { Value = 0f });
            em.SetComponentData(handle.Entity, new PrismJiggleParamsOverride { Value = float3.zero });
        }

        /// <summary>Clears a prism's animation stamps back to the settled state (pool
        /// reuse). Safe no-op when the clock components are absent.</summary>
        public static void ClearPrismStamps(in PrismRenderHandle handle)
        {
            ClearGrowStamp(in handle);
            ClearColorTransitionStamp(in handle);
            ClearFlightStamp(in handle);
            ClearShieldMorphStamp(in handle);
            ClearJiggleStamp(in handle);
        }

        /// <summary>Stamps an explosion's flight: offset/amount/opacity become pure
        /// functions of the clock. The entity transform must already hold the debris'
        /// initial pose — it never moves again. velocity is the ONE stamped vector,
        /// WORLD-space, feeding both the flight offset and the shatter-spin axis;
        /// the world→object conversion happens on the GPU inside PrismExplosionClock
        /// (raw inverse-model multiply — no CPU-side matrix math, and NOT the
        /// normalizing Direction-mode Transform node).</summary>
        public static bool StampExplosionClock(in PrismRenderHandle handle, float startTime, float speed, float duration,
            in float3 velocity)
        {
            if (!ClockAnimationEnabled || !IsUsable(in handle)) return false;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismExplodeStartTimeOverride>(handle.Entity)) return false;
            em.SetComponentData(handle.Entity, new PrismExplodeStartTimeOverride { Value = startTime });
            em.SetComponentData(handle.Entity, new PrismExplodeSpeedOverride { Value = speed });
            em.SetComponentData(handle.Entity, new PrismExplodeDurationOverride { Value = duration });
            em.SetComponentData(handle.Entity, new PrismVelocityOverride { Value = velocity });
            return true;
        }

        /// <summary>Restores RenderBounds to the mesh's authored bounds — call before
        /// re-expanding on a pooled reuse, or the envelopes compound run over run.</summary>
        public static void ResetBoundsToMesh(in PrismRenderHandle handle, Mesh mesh)
        {
            if (mesh == null || !IsUsable(in handle)) return;
            _world.EntityManager.SetComponentData(handle.Entity, new RenderBounds
            {
                Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents }
            });
        }

        /// <summary>
        /// One-shot RenderBounds expansion at stamp time so frustum culling covers a
        /// vertex-shader animation's WHOLE deterministic envelope (the entity matrix
        /// never moves; without this, debris culls against the unexploded box —
        /// visible faces vanish when the spawn point leaves the frustum and vice
        /// versa). objectDisplacement is the local-space end-of-flight offset;
        /// padding inflates for shatter spread. Conservative overdraw is the
        /// accepted cost — bounds are gameplay-free.
        /// </summary>
        public static void ExpandBoundsForClockAnimation(in PrismRenderHandle handle,
            in float3 objectDisplacement, float padding)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            var rb = em.GetComponentData<RenderBounds>(handle.Entity);
            float3 c0 = rb.Value.Center, e0 = rb.Value.Extents;
            float3 half = objectDisplacement * 0.5f;
            rb.Value = new AABB
            {
                Center = c0 + half,
                Extents = e0 + math.abs(half) + new float3(math.max(0f, padding)),
            };
            em.SetComponentData(handle.Entity, rb);
        }

        /// <summary>Grows RenderBounds minimally to contain an OBJECT-space point
        /// (+ padding) — the suction envelope: vertices lerp toward `_Location`, so
        /// the bounds must cover mesh ∪ convergence point or the collapsing geometry
        /// frustum-culls against the resting box. No-op while the point is already
        /// inside, so the moving-target refresh can call this each frame at
        /// read-mostly cost.</summary>
        public static void EncapsulateBoundsPoint(in PrismRenderHandle handle,
            in float3 objectPoint, float padding)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            var rb = em.GetComponentData<RenderBounds>(handle.Entity);
            float3 min = rb.Value.Center - rb.Value.Extents;
            float3 max = rb.Value.Center + rb.Value.Extents;
            float3 pad = new float3(math.max(0f, padding));
            float3 pmin = objectPoint - pad, pmax = objectPoint + pad;
            if (math.all(pmin >= min) && math.all(pmax <= max)) return;
            float3 nmin = math.min(min, pmin), nmax = math.max(max, pmax);
            rb.Value = new AABB { Center = (nmin + nmax) * 0.5f, Extents = (nmax - nmin) * 0.5f };
            em.SetComponentData(handle.Entity, rb);
        }

        /// <summary>Stamps a suction/implosion (direction >= 0, progress 0→1) or reverse
        /// grow (direction &lt; 0, progress 1→0) with an optional start delay. location is
        /// the convergence point, snapshotted at stamp time (moving-target exception:
        /// see PRISM_ANIMATION.md §1 — if retained, refresh via SetImplosionParams'
        /// location only, never the progress).</summary>
        public static bool StampSuctionClock(in PrismRenderHandle handle, float startTime, float duration,
            float direction, float growDelay, in float3 location)
        {
            if (!ClockAnimationEnabled || !IsUsable(in handle)) return false;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismSuctionStartTimeOverride>(handle.Entity)) return false;
            em.SetComponentData(handle.Entity, new PrismSuctionStartTimeOverride { Value = startTime });
            em.SetComponentData(handle.Entity, new PrismSuctionDurationOverride { Value = duration });
            em.SetComponentData(handle.Entity, new PrismSuctionDirectionOverride { Value = direction });
            em.SetComponentData(handle.Entity, new PrismSuctionGrowDelayOverride { Value = growDelay });
            em.SetComponentData(handle.Entity, new PrismImplosionLocationOverride { Value = location });
            return true;
        }

        /// <summary>Location-only refresh for a clock-stamped implosion tracking a MOVING
        /// convergence target — the documented exception (PRISM_ANIMATION.md §1): live
        /// gameplay data, one float3 per frame per implosion, nothing else.</summary>
        public static void SetImplosionLocation(in PrismRenderHandle handle, in float3 location)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismImplosionLocationOverride>(handle.Entity)) return;
            em.SetComponentData(handle.Entity, new PrismImplosionLocationOverride { Value = location });
        }

        /// <summary>Retires an explosion's clock stamp (pool return). Duration 0 restores
        /// the legacy CPU-fed fallback branch, so a later legacy-path reuse of this entity
        /// can't replay a stale clock animation.</summary>
        public static void ClearExplosionClockStamp(in PrismRenderHandle handle)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismExplodeDurationOverride>(handle.Entity)) return;
            em.SetComponentData(handle.Entity, new PrismExplodeDurationOverride { Value = 0f });
        }

        /// <summary>Retires a suction/grow clock stamp (pool return) — same contract as
        /// <see cref="ClearExplosionClockStamp"/>.</summary>
        public static void ClearSuctionClockStamp(in PrismRenderHandle handle)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            if (!em.HasComponent<PrismSuctionDurationOverride>(handle.Entity)) return;
            em.SetComponentData(handle.Entity, new PrismSuctionDurationOverride { Value = 0f });
        }

        // ------------------------------------------------------------------
        // Batched pure-entity debris (Docs/PRISM_ANIMATION.md B3 — the
        // mass-death path). A prism death's explosion VFX needs exactly what an
        // entity provides: one pose, one clock stamp, one retirement. The
        // pooled-GameObject carrier (PrismExplosion) charges Instantiate +
        // OnEnable/OnDisable registry churn + a transform + a per-effect timer
        // entry per death — profiled at 1.9s of a single frame when a lifted-
        // throttle blast killed 2,408 prisms at once (every death a pool miss).
        // These APIs spawn a whole frame's deaths as ONE prototype-instantiate
        // batch plus one batched visibility op; PrismDebris owns the entity
        // lifetimes and retires whole batches below. No handles: the registry
        // exists for objects whose state changes over life — debris state is
        // written once and never touched again.
        // ------------------------------------------------------------------

        /// <summary>Epoch of the cached ECS world. PrismDebris tags its batches with
        /// this; a mismatch at sweep time means the world (and every entity in it)
        /// is already gone, so records are dropped without a destroy.</summary>
        public static int CurrentEpoch => _epoch;

        /// <summary>One debris entity's complete initial conditions — everything the
        /// clock animation needs, stamped once at spawn (the entity never changes
        /// again until its batch is destroyed).</summary>
        public struct ExplosionDebrisSpawn
        {
            /// <summary>Initial pose. The entity matrix never moves — the GPU flies
            /// the debris off the clock stamp.</summary>
            public Matrix4x4 LocalToWorld;
            /// <summary>Raw team colors — color-space conversion happens at stamp,
            /// same as <see cref="SetTeamColors"/>.</summary>
            public float4 BrightColor;
            public float4 DarkColor;
            /// <summary>World-space debris velocity, already clamped by the caller
            /// (the shader does the world→object conversion).</summary>
            public float3 Velocity;
            /// <summary>Shatter-rate channel (may exceed |Velocity| on the legacy
            /// gain — see PrismExplosion.TriggerExplosion's ClampMagnitude note).</summary>
            public float Speed;
            public float Duration;
            /// <summary>Object-space end-of-flight offset — the culling envelope
            /// (mesh AABB extended along the flight, exactly like
            /// <see cref="ExpandBoundsForClockAnimation"/>).</summary>
            public float3 ObjectDisplacement;
            public float BoundsPadding;
        }

        /// <summary>
        /// Spawns every entry of <paramref name="spawns"/> as debris entities in ONE
        /// prototype-instantiate + ONE batched visibility change; per-entity state
        /// lands via non-structural SetComponentData. Spawned entities are appended
        /// to <paramref name="appendEntitiesTo"/> in spawn order (index-aligned with
        /// <paramref name="spawns"/>). Returns false — spawning nothing — when the
        /// service is off or no world exists; the caller falls back to the pooled
        /// GameObject path.
        /// </summary>
        public static bool SpawnExplosionDebrisBatch(Mesh mesh, Material material, int layer,
            System.Collections.Generic.List<ExplosionDebrisSpawn> spawns, float startTime,
            System.Collections.Generic.List<Entity> appendEntitiesTo)
        {
            if (!Enabled || mesh == null || material == null ||
                spawns == null || spawns.Count == 0 || !TryEnsure())
                return false;

            var em = _world.EntityManager;
            var prototype = GetPrototype(layer, PrismRenderOverrideSet.Explosion, mesh, material);

            var entities = new Unity.Collections.NativeArray<Entity>(
                spawns.Count, Unity.Collections.Allocator.Temp);
            em.Instantiate(prototype, entities);
            // Clones are born hidden (prototype ships DisableRendering); strip the
            // whole batch in one structural op. The stamp below IS the correct
            // initial state (amount 0, opacity 1 at t = startTime) — nothing to hide.
            em.RemoveComponent(entities, ComponentType.ReadWrite<DisableRendering>());

            var mmi = new MaterialMeshInfo(GetMaterialID(material), GetMeshID(mesh));
            float3 spread = ReadVector3(material, SpreadId);
            float3 meshCenter = mesh.bounds.center;
            float3 meshExtents = mesh.bounds.extents;

            for (int i = 0; i < spawns.Count; i++)
            {
                var s = spawns[i];
                var entity = entities[i];
                em.SetComponentData(entity, mmi);
                em.SetComponentData(entity, new LocalToWorld { Value = ToFloat4x4(in s.LocalToWorld) });
                em.SetComponentData(entity, new PrismBrightColorOverride { Value = ApplyColorSpace(in s.BrightColor) });
                em.SetComponentData(entity, new PrismDarkColorOverride { Value = ApplyColorSpace(in s.DarkColor) });
                em.SetComponentData(entity, new PrismSpreadOverride { Value = spread });
                em.SetComponentData(entity, new PrismExplodeStartTimeOverride { Value = startTime });
                em.SetComponentData(entity, new PrismExplodeSpeedOverride { Value = s.Speed });
                em.SetComponentData(entity, new PrismExplodeDurationOverride { Value = s.Duration });
                em.SetComponentData(entity, new PrismVelocityOverride { Value = s.Velocity });

                float3 half = s.ObjectDisplacement * 0.5f;
                em.SetComponentData(entity, new RenderBounds
                {
                    Value = new AABB
                    {
                        Center = meshCenter + half,
                        Extents = meshExtents + math.abs(half) + new float3(math.max(0f, s.BoundsPadding)),
                    }
                });

                appendEntitiesTo.Add(entity);
            }

            LiveEntityCount += spawns.Count;
            entities.Dispose();
            return true;
        }

        /// <summary>Retires debris entities in ONE batched DestroyEntity. Safe against
        /// world resets: a stale epoch or missing world is a no-op (the entities died
        /// with the world), and individual already-destroyed entities are skipped.</summary>
        public static void DestroyDebrisBatch(
            System.Collections.Generic.List<Entity> entities, int epoch)
        {
            if (entities == null || entities.Count == 0) return;
            if (epoch != _epoch || _world == null || !_world.IsCreated) return;

            var em = _world.EntityManager;
            var arr = new Unity.Collections.NativeArray<Entity>(
                entities.Count, Unity.Collections.Allocator.Temp);
            int n = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                var e = entities[i];
                if (e != Entity.Null && em.Exists(e)) arr[n++] = e;
            }
            if (n > 0)
                em.DestroyEntity(arr.GetSubArray(0, n));
            LiveEntityCount = Mathf.Max(0, LiveEntityCount - n);
            arr.Dispose();
        }

        // ------------------------------------------------------------------
        // Batched pure-entity SUCTION debris — the implosion half of the same
        // pattern. An implosion carries one extra piece of live state the
        // explosion does not: the convergence point tracks a MOVING target (a
        // fauna swims a long way during the ~2s suction), so the caller keeps a
        // record per entity and refreshes _Location while the target lives.
        // That is the documented §1 exception (Docs/PRISM_ANIMATION.md) — ONE
        // float3 per live effect per frame and nothing else; the progress
        // itself never touches the CPU.
        // ------------------------------------------------------------------

        /// <summary>One suction entity's complete initial conditions. Everything
        /// except <see cref="PrismImplosionLocationOverride"/> is stamped once and
        /// never written again.</summary>
        public struct ImplosionDebrisSpawn
        {
            /// <summary>Initial pose. The entity matrix never moves.</summary>
            public Matrix4x4 LocalToWorld;
            public float4 BrightColor;
            public float4 DarkColor;
            /// <summary>Suction length in seconds (excludes <see cref="GrowDelay"/>).</summary>
            public float Duration;
            /// <summary>&gt;= 0 implode (progress 0→1); &lt; 0 reverse grow (1→0).</summary>
            public float Direction;
            /// <summary>Start delay baked into the stamp (the grow path's 0.25s).</summary>
            public float GrowDelay;
            /// <summary>WORLD-space convergence point (the shader lerps vertices toward it).</summary>
            public float3 Location;
            /// <summary>Object-space AABB that already covers mesh ∪ convergence point —
            /// the suction culling envelope (see <see cref="EncapsulateBoundsPoint"/>).</summary>
            public AABB Bounds;
        }

        /// <summary>
        /// Spawns every entry as a suction entity in ONE prototype-instantiate + ONE
        /// batched visibility strip, exactly like
        /// <see cref="SpawnExplosionDebrisBatch"/>. Entities are appended to
        /// <paramref name="appendEntitiesTo"/> index-aligned with
        /// <paramref name="spawns"/>. Returns false — spawning nothing — when the
        /// service is off, so the caller can fall back to the pooled path.
        /// </summary>
        public static bool SpawnImplosionDebrisBatch(Mesh mesh, Material material, int layer,
            System.Collections.Generic.List<ImplosionDebrisSpawn> spawns, float startTime,
            System.Collections.Generic.List<Entity> appendEntitiesTo)
        {
            if (!Enabled || mesh == null || material == null ||
                spawns == null || spawns.Count == 0 || !TryEnsure())
                return false;

            var em = _world.EntityManager;
            var prototype = GetPrototype(layer, PrismRenderOverrideSet.Implosion, mesh, material);

            var entities = new Unity.Collections.NativeArray<Entity>(
                spawns.Count, Unity.Collections.Allocator.Temp);
            em.Instantiate(prototype, entities);
            // Clones are born hidden (prototype ships DisableRendering); strip the
            // whole batch in one structural op. An implosion is visible from frame
            // zero — progress 0 IS the whole, unconsumed block.
            em.RemoveComponent(entities, ComponentType.ReadWrite<DisableRendering>());

            var mmi = new MaterialMeshInfo(GetMaterialID(material), GetMeshID(mesh));
            float3 spread = ReadVector3(material, SpreadId);

            for (int i = 0; i < spawns.Count; i++)
            {
                var s = spawns[i];
                var entity = entities[i];
                em.SetComponentData(entity, mmi);
                em.SetComponentData(entity, new LocalToWorld { Value = ToFloat4x4(in s.LocalToWorld) });
                em.SetComponentData(entity, new PrismBrightColorOverride { Value = ApplyColorSpace(in s.BrightColor) });
                em.SetComponentData(entity, new PrismDarkColorOverride { Value = ApplyColorSpace(in s.DarkColor) });
                em.SetComponentData(entity, new PrismSpreadOverride { Value = spread });
                // Legacy _State fallback value. Only read when Duration <= 0 (see
                // PrismSuctionClock_float) — which a stamped entity never is — but a
                // correct static frame costs one write.
                em.SetComponentData(entity, new PrismImplosionStateOverride
                {
                    Value = s.Direction < 0f ? 1f : 0f
                });
                em.SetComponentData(entity, new PrismImplosionLocationOverride { Value = s.Location });
                em.SetComponentData(entity, new PrismSuctionStartTimeOverride { Value = startTime });
                em.SetComponentData(entity, new PrismSuctionDurationOverride { Value = s.Duration });
                em.SetComponentData(entity, new PrismSuctionDirectionOverride { Value = s.Direction });
                em.SetComponentData(entity, new PrismSuctionGrowDelayOverride { Value = s.GrowDelay });
                em.SetComponentData(entity, new RenderBounds { Value = s.Bounds });

                appendEntitiesTo.Add(entity);
            }

            LiveEntityCount += spawns.Count;
            entities.Dispose();
            return true;
        }

        /// <summary>One live suction entity's moving-target update.</summary>
        public struct ImplosionDebrisRefresh
        {
            public Entity Entity;
            /// <summary>New WORLD-space convergence point.</summary>
            public float3 Location;
            /// <summary>True when the point wandered outside the stamped envelope and
            /// <see cref="Bounds"/> must replace it. False on the common path (the
            /// creature approaches its meal), which then costs exactly one float3 write.</summary>
            public bool GrowBounds;
            /// <summary>Object-space replacement envelope. Only read when
            /// <see cref="GrowBounds"/>. The caller mirrors the AABB CPU-side so this
            /// path never has to read a component back per entity per frame.</summary>
            public AABB Bounds;
        }

        /// <summary>
        /// Applies a whole frame's moving-target refreshes in one pass — one epoch
        /// check and one EntityManager fetch for the batch instead of per entity.
        /// A stale epoch is a no-op (those entities died with their world). Entries
        /// are assumed live: the caller owns these lifetimes and destroys them only
        /// through <see cref="DestroyDebrisBatch"/>.
        /// </summary>
        public static void RefreshImplosionDebrisBatch(
            System.Collections.Generic.List<ImplosionDebrisRefresh> refreshes, int epoch)
        {
            if (refreshes == null || refreshes.Count == 0) return;
            if (epoch != _epoch || _world == null || !_world.IsCreated) return;

            var em = _world.EntityManager;
            for (int i = 0; i < refreshes.Count; i++)
            {
                var r = refreshes[i];
                if (r.Entity == Entity.Null || !em.Exists(r.Entity)) continue;
                em.SetComponentData(r.Entity, new PrismImplosionLocationOverride { Value = r.Location });
                if (r.GrowBounds)
                    em.SetComponentData(r.Entity, new RenderBounds { Value = r.Bounds });
            }
        }

        // The former SHIELD SHATTER batch spawner lived here. It is GONE on purpose
        // (Docs/PRISM_ANIMATION.md §4.8.1): a shield's shards are ordinary explosion
        // debris now — PrismShieldShatter groups a frame's disengages per shield mesh and
        // spawns them through SpawnExplosionDebrisBatch above, so the two death visuals
        // cannot drift apart. Do not reintroduce a shield-specific spawner: any look
        // difference between a shield coming apart and a prism coming apart is a MESH
        // AUTHORING question (the shield generators bake the debris attribute set), never
        // a pipeline fork.

        /// <summary>Destroys the companion entity (prism GameObject destruction / scene teardown).</summary>
        public static void Destroy(ref PrismRenderHandle handle)
        {
            if (IsUsable(in handle))
            {
                // Entity indices recycle — drop any queued toggle so the flush can
                // never apply a dead prism's wish to a future entity reusing the id.
                s_pendingVisibility.Remove(handle.Entity);
                _world.EntityManager.DestroyEntity(handle.Entity);
                LiveEntityCount = Mathf.Max(0, LiveEntityCount - 1);
            }
            handle = PrismRenderHandle.Invalid;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>Color → shader float4. Shared by every color sink so the
        /// conversion lives in exactly one place.</summary>
        public static float4 ToFloat4(Color c) => new float4(c.r, c.g, c.b, c.a);

        /// <summary>Vector3 → shader float3 (spread / location overrides).</summary>
        public static float3 ToFloat3(Vector3 v) => new float3(v.x, v.y, v.z);

        static float4x4 ToFloat4x4(in Matrix4x4 m) =>
            new float4x4(m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3));

        static float4 ReadColor(Material material, int propertyId)
        {
            if (material.HasProperty(propertyId))
            {
                Color c = material.GetColor(propertyId);
                return ApplyColorSpace(new float4(c.r, c.g, c.b, c.a));
            }
            return new float4(1f, 1f, 1f, 1f);
        }

        static float3 ReadVector3(Material material, int propertyId)
        {
            if (material.HasProperty(propertyId))
            {
                Vector4 v = material.GetVector(propertyId);
                return new float3(v.x, v.y, v.z);
            }
            return float3.zero;
        }
    }
}
