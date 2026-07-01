using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.ECS
{
    /// <summary>
    /// Max-prism rendering stress harness. Spawns N pure render entities (no
    /// GameObjects, no colliders, no MonoBehaviours) sharing one mesh+material
    /// with per-instance color overrides — the end-state representation of bulk
    /// prism mass. Use it in any scene with a camera to measure the instanced
    /// rendering ceiling on target hardware (drop on an empty GameObject, set
    /// the mesh/material from a prism prefab, hit play).
    ///
    /// In-editor verification:
    ///  1. Game view + Stats panel (or the Performance Benchmark tool):
    ///     SetPass/draw calls stay in the tens regardless of Count.
    ///  2. Entities Hierarchy window shows Count entities.
    ///  3. With 100k static entities a mid PC should hold well above 60 fps;
    ///     raise Count until it doesn't — that is the rendering ceiling.
    ///  4. movingFraction / colorChurnFraction exercise the hybrid write path
    ///     (main-thread SetComponentData), modelling growth animation and
    ///     theme-change load at scale.
    /// </summary>
    public class PrismRenderStressTest : MonoBehaviour
    {
        [Header("Population")]
        [Tooltip("Number of pure render entities to spawn.")]
        [SerializeField] private int count = 100_000;
        [Tooltip("Mesh to instance — assign the prism mesh from a prism prefab.")]
        [SerializeField] private Mesh mesh;
        [Tooltip("Material to instance — assign a prism material (UnstablePrismGraph-based).")]
        [SerializeField] private Material material;
        [Tooltip("Half-extent of the spawn cube around this transform.")]
        [SerializeField] private float spawnRadius = 600f;
        [Tooltip("Uniform scale range for spawned instances.")]
        [SerializeField] private Vector2 scaleRange = new Vector2(0.5f, 3f);

        [Header("Churn (models growth + theme animation at scale)")]
        [Tooltip("Fraction of entities whose transform is rewritten every frame (growth/movers).")]
        [Range(0f, 1f)][SerializeField] private float movingFraction = 0.02f;
        [Tooltip("Fraction of entities whose colors are rewritten every frame (theme/state animation).")]
        [Range(0f, 1f)][SerializeField] private float colorChurnFraction = 0.02f;

        [Header("Overlay")]
        [SerializeField] private bool showOverlay = true;

        private NativeArray<Entity> _entities;
        private NativeArray<float3> _basePositions;
        private NativeArray<float> _baseScales;
        private World _world;
        private float _smoothedDt;

        private static readonly Color[] DomainPalette =
        {
            new Color(0.1f, 0.9f, 0.5f), // jade-ish
            new Color(0.9f, 0.2f, 0.3f), // ruby-ish
            new Color(0.95f, 0.8f, 0.2f), // gold-ish
        };

        void Start()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
                _world = DefaultWorldInitialization.Initialize("Default World", false);
            if (_world == null || !_world.IsCreated)
            {
                Debug.LogError("[PrismRenderStressTest] No default ECS world and could not bootstrap one.");
                enabled = false;
                return;
            }
            if (mesh == null || material == null)
            {
                Debug.LogError("[PrismRenderStressTest] Assign a mesh and material (take them from a prism prefab).");
                enabled = false;
                return;
            }

            var em = _world.EntityManager;
            var graphics = _world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null)
            {
                Debug.LogError("[PrismRenderStressTest] EntitiesGraphicsSystem missing — is com.unity.entities.graphics installed and SRP active?");
                enabled = false;
                return;
            }

            var meshId = graphics.RegisterMesh(mesh);
            var materialId = graphics.RegisterMaterial(material);

            // Prototype entity carrying the full render archetype, then mass-instantiate.
            var desc = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
            var prototype = em.CreateEntity();
            RenderMeshUtility.AddComponents(prototype, em, in desc, new MaterialMeshInfo(materialId, meshId));
            em.SetComponentData(prototype, new RenderBounds
            {
                Value = new AABB { Center = mesh.bounds.center, Extents = mesh.bounds.extents }
            });
            em.AddComponentData(prototype, new PrismBrightColorOverride { Value = new float4(1f) });
            em.AddComponentData(prototype, new PrismDarkColorOverride { Value = new float4(0f, 0f, 0f, 1f) });
            em.AddComponentData(prototype, new PrismSpreadOverride { Value = new float3(1f) });

            _entities = new NativeArray<Entity>(count, Allocator.Persistent);
            em.Instantiate(prototype, _entities);
            em.DestroyEntity(prototype);

            _basePositions = new NativeArray<float3>(count, Allocator.Persistent);
            _baseScales = new NativeArray<float>(count, Allocator.Persistent);

            var random = new Unity.Mathematics.Random(0x5EED5EED);
            float3 origin = transform.position;
            for (int i = 0; i < count; i++)
            {
                float3 pos = origin + random.NextFloat3(-spawnRadius, spawnRadius);
                float scale = random.NextFloat(scaleRange.x, scaleRange.y);
                quaternion rot = random.NextQuaternionRotation();
                _basePositions[i] = pos;
                _baseScales[i] = scale;

                em.SetComponentData(_entities[i], new LocalToWorld
                {
                    Value = float4x4.TRS(pos, rot, new float3(scale))
                });

                var bright = DomainPalette[i % DomainPalette.Length];
                em.SetComponentData(_entities[i], new PrismBrightColorOverride
                {
                    Value = new float4(bright.r, bright.g, bright.b, 1f)
                });
            }

            Debug.Log($"[PrismRenderStressTest] Spawned {count} render entities (1 mesh, 1 material, per-instance colors).");
        }

        void Update()
        {
            _smoothedDt = Mathf.Lerp(_smoothedDt <= 0f ? Time.unscaledDeltaTime : _smoothedDt, Time.unscaledDeltaTime, 0.05f);
            if (_world == null || !_world.IsCreated || !_entities.IsCreated) return;

            var em = _world.EntityManager;
            float t = Time.time;

            int movingCount = (int)(count * movingFraction);
            for (int i = 0; i < movingCount; i++)
            {
                float phase = t + i * 0.61f;
                float3 pos = _basePositions[i] + new float3(0f, math.sin(phase) * 5f, 0f);
                float scale = _baseScales[i] * (1f + 0.25f * math.sin(phase * 2f));
                em.SetComponentData(_entities[i], new LocalToWorld
                {
                    Value = float4x4.TRS(pos, quaternion.identity, new float3(scale))
                });
            }

            int churnStart = movingCount;
            int churnCount = (int)(count * colorChurnFraction);
            for (int i = 0; i < churnCount; i++)
            {
                int idx = churnStart + i;
                if (idx >= count) break;
                float pulse = 0.5f + 0.5f * math.sin(t * 3f + idx * 0.37f);
                var baseColor = DomainPalette[idx % DomainPalette.Length];
                em.SetComponentData(_entities[idx], new PrismBrightColorOverride
                {
                    Value = new float4(baseColor.r * pulse, baseColor.g * pulse, baseColor.b * pulse, 1f)
                });
            }
        }

        void OnGUI()
        {
            if (!showOverlay) return;
            float fps = _smoothedDt > 0f ? 1f / _smoothedDt : 0f;
            GUI.Label(new Rect(10, 10, 640, 22),
                $"[PrismRenderStressTest] entities={count}  moving={(int)(count * movingFraction)}  colorChurn={(int)(count * colorChurnFraction)}  fps={fps:F1}");
        }

        void OnDestroy()
        {
            if (_world != null && _world.IsCreated && _entities.IsCreated)
                _world.EntityManager.DestroyEntity(_entities);
            if (_entities.IsCreated) _entities.Dispose();
            if (_basePositions.IsCreated) _basePositions.Dispose();
            if (_baseScales.IsCreated) _baseScales.Dispose();
        }
    }
}
