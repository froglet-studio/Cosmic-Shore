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
    /// kept disabled while the entity draws in its place. Exotic visual states
    /// (octahedron shield morph/shatter, which swap the MeshFilter mesh
    /// per-frame) hand rendering back to the GameObject via
    /// Prism.SetExoticVisualActive.
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
        static bool _configLoaded;
        static bool _configEnabled = true;

        /// <summary>
        /// Master switch for instanced prism rendering. Resolution order:
        /// runtime override (benchmark A/B) → PrismRenderConfig asset in
        /// Resources → default ON (this branch exists to prove the path).
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (_runtimeOverride.HasValue) return _runtimeOverride.Value;
                if (!_configLoaded)
                {
                    _configLoaded = true;
                    var config = Resources.Load<CosmicShore.ScriptableObjects.PrismRenderConfigSO>("PrismRenderConfig");
                    if (config != null) _configEnabled = config.UseInstancedRendering;
                }
                return _configEnabled;
            }
        }

        /// <summary>Runtime A/B override (null = fall back to config). New prisms follow the new value; existing ones keep their current path until reuse.</summary>
        public static void SetRuntimeOverride(bool? enabled) => _runtimeOverride = enabled;

        // ------------------------------------------------------------------
        // World / registration caches
        // ------------------------------------------------------------------

        static World _world;
        static EntitiesGraphicsSystem _graphics;
        static int _epoch; // bumped whenever the cached world goes away — invalidates all outstanding handles
        static readonly Dictionary<Mesh, BatchMeshID> _meshIds = new();
        static readonly Dictionary<Material, BatchMaterialID> _materialIds = new();

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
                LiveEntityCount = 0;
                _epoch++;
            }

            var world = World.DefaultGameObjectInjectionWorld;
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
        // API
        // ------------------------------------------------------------------

        /// <summary>
        /// Creates a hidden companion render entity for a prism. Returns an
        /// invalid handle (and the caller stays on the MeshRenderer path) when
        /// the toggle is off or no ECS world / graphics system exists.
        /// </summary>
        public static PrismRenderHandle Create(Mesh mesh, Material material, in Matrix4x4 localToWorld, int layer)
        {
            if (!Enabled || mesh == null || material == null || !TryEnsure())
                return PrismRenderHandle.Invalid;

            var em = _world.EntityManager;
            var entity = em.CreateEntity();

            var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
            var filter = desc.FilterSettings;
            filter.Layer = layer;
            desc.FilterSettings = filter;

            RenderMeshUtility.AddComponents(
                entity, em, in desc,
                new MaterialMeshInfo(GetMaterialID(material), GetMeshID(mesh)));

            em.SetComponentData(entity, new LocalToWorld { Value = ToFloat4x4(in localToWorld) });
            em.SetComponentData(entity, new RenderBounds
            {
                Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents }
            });

            em.AddComponentData(entity, new PrismBrightColorOverride { Value = ReadColor(material, BrightColorId) });
            em.AddComponentData(entity, new PrismDarkColorOverride { Value = ReadColor(material, DarkColorId) });
            em.AddComponentData(entity, new PrismSpreadOverride { Value = ReadVector3(material, SpreadId) });

            // Born hidden — Prism.ApplyRenderPath shows it when the spawn window opens.
            em.AddComponent<DisableRendering>(entity);

            LiveEntityCount++;
            return new PrismRenderHandle { Entity = entity, Epoch = _epoch };
        }

        /// <summary>Shows/hides the entity (DisableRendering tag add/remove).</summary>
        public static void SetVisible(in PrismRenderHandle handle, bool visible)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            bool hidden = em.HasComponent<DisableRendering>(handle.Entity);
            if (visible && hidden)
                em.RemoveComponent<DisableRendering>(handle.Entity);
            else if (!visible && !hidden)
                em.AddComponent<DisableRendering>(handle.Entity);
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

        /// <summary>Per-frame animated colors from MaterialStateManager's Burst job output.</summary>
        public static void SetColors(in PrismRenderHandle handle, in float4 bright, in float4 dark, in float3 spread)
        {
            if (!IsUsable(in handle)) return;
            var em = _world.EntityManager;
            em.SetComponentData(handle.Entity, new PrismBrightColorOverride { Value = bright });
            em.SetComponentData(handle.Entity, new PrismDarkColorOverride { Value = dark });
            em.SetComponentData(handle.Entity, new PrismSpreadOverride { Value = spread });
        }

        /// <summary>Destroys the companion entity (prism GameObject destruction / scene teardown).</summary>
        public static void Destroy(ref PrismRenderHandle handle)
        {
            if (IsUsable(in handle))
            {
                _world.EntityManager.DestroyEntity(handle.Entity);
                LiveEntityCount = Mathf.Max(0, LiveEntityCount - 1);
            }
            handle = PrismRenderHandle.Invalid;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        static float4x4 ToFloat4x4(in Matrix4x4 m) =>
            new float4x4(m.GetColumn(0), m.GetColumn(1), m.GetColumn(2), m.GetColumn(3));

        static float4 ReadColor(Material material, int propertyId)
        {
            if (material.HasProperty(propertyId))
            {
                Color c = material.GetColor(propertyId);
                return new float4(c.r, c.g, c.b, c.a);
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
