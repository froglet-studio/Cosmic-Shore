using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using CosmicShore.Gameplay;
using CosmicShore.Utility.PerformanceBenchmark;

namespace CosmicShore.ECS
{
    /// <summary>
    /// Max-prism rendering stress harness. Spawns N render entities (no GameObjects,
    /// no colliders, no MonoBehaviours) sharing one mesh+material with per-instance
    /// color overrides — the end-state representation of bulk prism mass.
    ///
    /// Two spawn modes:
    ///  • useRenderService = true (default): every entity goes through PrismRenderService
    ///    (Create / SetVisible / SetColors / SetTransform) — the SAME bridge the game's
    ///    prisms use. The DiagnosticsHUD "Prism Path" line (ents/meshes/mats) then reflects
    ///    the population, and the service's own per-call overhead is part of the measurement.
    ///  • useRenderService = false: raw EntityManager spawn (prototype + mass Instantiate) —
    ///    the theoretical ECS ceiling with zero bridge overhead, for A/B comparison.
    ///
    /// Live numbers are published to the DiagnosticsHUD (F7 toggle / F6 advanced) via
    /// DiagnosticsHUD.SetStat — this component draws no overlay of its own.
    ///
    /// In-editor verification: Draw Calls / SetPass on the HUD stay in the tens regardless
    /// of Count; Entities Hierarchy shows Count entities; raise Count until fps breaks to
    /// find the rendering ceiling on target hardware.
    /// </summary>
    public class PrismRenderStressTest : MonoBehaviour
    {
        const string StatsSection = "Stress Test";

        [Header("Population")]
        [Tooltip("Number of render entities to spawn.")]
        [SerializeField] private int count = 100_000;
        [Tooltip("Mesh to instance — assign the prism mesh from a prism prefab.")]
        [SerializeField] private Mesh mesh;
        [Tooltip("Material to instance — assign a prism material (BlockGraph-based).")]
        [SerializeField] private Material material;
        [Tooltip("Half-extent of the spawn cube around this transform.")]
        [SerializeField] private float spawnRadius = 600f;
        [Tooltip("Uniform scale range for spawned instances.")]
        [SerializeField] private Vector2 scaleRange = new Vector2(0.5f, 3f);

        [Header("Path")]
        [Tooltip("Spawn through PrismRenderService — the game's actual render bridge — so the " +
                 "HUD's Prism Path ents/meshes/mats reflect the population. Off = raw " +
                 "EntityManager spawn for the zero-overhead theoretical ceiling.")]
        [SerializeField] private bool useRenderService = true;

        [Header("Churn (models growth + theme animation at scale)")]
        [Tooltip("Fraction of entities whose transform is rewritten every frame (growth/movers).")]
        [Range(0f, 1f)][SerializeField] private float movingFraction = 0.02f;
        [Tooltip("Fraction of entities whose colors are rewritten every frame (theme/state animation).")]
        [Range(0f, 1f)][SerializeField] private float colorChurnFraction = 0.02f;

        // Direct mode
        private NativeArray<Entity> _entities;
        // Service mode
        private PrismRenderHandle[] _handles;

        private NativeArray<float3> _basePositions;
        private NativeArray<float> _baseScales;
        private World _world;
        private bool _viaService;

        // Per-domain (bright, dark) prism colors resolved from the PROJECT's ColorSet —
        // never hardcoded. Primary source: ThemeManagerDataContainerSO.ColorSet (loaded
        // whenever the game booted through Bootstrap — menu, benchmark, races), the same
        // single source ThemeManager bakes into every domain block material
        // (_BrightColor = InsideBlockColor, _DarkColor = OutsideBlockColor). Fallback for
        // tool scenes with no theme loaded: the assigned material's authored colors,
        // which are themselves project-authored.
        private (float4 bright, float4 dark)[] _palette;

        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static float4 ToF4(Color c) => new float4(c.r, c.g, c.b, c.a);

        void ResolvePalette()
        {
            foreach (var container in Resources.FindObjectsOfTypeAll<ThemeManagerDataContainerSO>())
            {
                if (container == null || container.ColorSet == null) continue;
                var cs = container.ColorSet;
                _palette = new[]
                {
                    (ToF4(cs.JadeColors.InsideBlockColor), ToF4(cs.JadeColors.OutsideBlockColor)),
                    (ToF4(cs.RubyColors.InsideBlockColor), ToF4(cs.RubyColors.OutsideBlockColor)),
                    (ToF4(cs.GoldColors.InsideBlockColor), ToF4(cs.GoldColors.OutsideBlockColor)),
                };
                return;
            }

            Color bright = material.HasProperty(BrightColorId) ? material.GetColor(BrightColorId) : Color.white;
            Color dark = material.HasProperty(DarkColorId) ? material.GetColor(DarkColorId) : Color.black;
            _palette = new[] { (ToF4(bright), ToF4(dark)) };
            Debug.Log("[PrismRenderStressTest] No theme ColorSet loaded — cloud uses the assigned material's authored colors.");
        }

        /// <summary>
        /// Set the spawn parameters before Start runs — used by the runtime injector
        /// (AddComponent + Configure in the same frame; Start executes later that frame).
        /// </summary>
        public void Configure(Mesh sharedMesh, Material sharedMaterial, int entityCount, float radius)
        {
            mesh = sharedMesh;
            material = sharedMaterial;
            count = entityCount;
            spawnRadius = radius;
        }

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

            var graphics = _world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null)
            {
                Debug.LogError("[PrismRenderStressTest] EntitiesGraphicsSystem missing — is com.unity.entities.graphics installed and SRP active?");
                enabled = false;
                return;
            }

            _basePositions = new NativeArray<float3>(count, Allocator.Persistent);
            _baseScales = new NativeArray<float>(count, Allocator.Persistent);

            ResolvePalette();
            _viaService = useRenderService && SpawnViaService();
            if (!_viaService)
                SpawnDirect(graphics);

            DiagnosticsHUD.SetStat(StatsSection, "Entities", count.ToString("N0"));
            DiagnosticsHUD.SetStat(StatsSection, "Mode", _viaService ? "render service" : "direct ECS");
            DiagnosticsHUD.SetStat(StatsSection, "Moving / f", ((int)(count * movingFraction)).ToString("N0"));
            DiagnosticsHUD.SetStat(StatsSection, "Recolor / f", ((int)(count * colorChurnFraction)).ToString("N0"));

            Debug.Log($"[PrismRenderStressTest] Spawned {count} render entities via " +
                      $"{(_viaService ? "PrismRenderService (game bridge)" : "raw EntityManager")} — 1 mesh, 1 material, per-instance colors.");
        }

        /// <summary>Spawns through the game's render bridge. False when the service can't create
        /// (master toggle off / no graphics system) — caller falls back to the direct path.</summary>
        bool SpawnViaService()
        {
            _handles = new PrismRenderHandle[count];

            var random = new Unity.Mathematics.Random(0x5EED5EED);
            float3 origin = transform.position;
            for (int i = 0; i < count; i++)
            {
                float3 pos = origin + random.NextFloat3(-spawnRadius, spawnRadius);
                float scale = random.NextFloat(scaleRange.x, scaleRange.y);
                quaternion rot = random.NextQuaternionRotation();
                _basePositions[i] = pos;
                _baseScales[i] = scale;

                var matrix = Matrix4x4.TRS(pos, rot, new Vector3(scale, scale, scale));
                _handles[i] = PrismRenderService.Create(mesh, material, in matrix, gameObject.layer);

                if (i == 0 && !PrismRenderService.IsHandleUsable(in _handles[0]))
                {
                    Debug.LogWarning("[PrismRenderStressTest] PrismRenderService unavailable (toggle off or no ECS graphics) — falling back to direct EntityManager spawn.");
                    _handles = null;
                    return false;
                }

                var pair = _palette[i % _palette.Length];
                PrismRenderService.SetColors(in _handles[i], in pair.bright, in pair.dark, new float3(1f, 1f, 1f));
                PrismRenderService.SetVisible(in _handles[i], true);
            }
            return true;
        }

        /// <summary>Raw prototype + mass Instantiate — the zero-bridge-overhead ceiling.</summary>
        void SpawnDirect(EntitiesGraphicsSystem graphics)
        {
            var em = _world.EntityManager;
            var meshId = graphics.RegisterMesh(mesh);
            var materialId = graphics.RegisterMaterial(material);

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

                var pair = _palette[i % _palette.Length];
                // Direct mode bypasses the service, so apply the same color-space
                // transform its color APIs apply (legacy-path parity).
                em.SetComponentData(_entities[i], new PrismBrightColorOverride { Value = PrismRenderService.ApplyColorSpace(in pair.bright) });
                em.SetComponentData(_entities[i], new PrismDarkColorOverride { Value = PrismRenderService.ApplyColorSpace(in pair.dark) });
            }
        }

        void Update()
        {
            if (_world == null || !_world.IsCreated) return;
            if (!_viaService && !_entities.IsCreated) return;
            if (_viaService && _handles == null) return;

            float t = Time.time;

            int movingCount = (int)(count * movingFraction);
            for (int i = 0; i < movingCount; i++)
            {
                float phase = t + i * 0.61f;
                float3 pos = _basePositions[i] + new float3(0f, math.sin(phase) * 5f, 0f);
                float scale = _baseScales[i] * (1f + 0.25f * math.sin(phase * 2f));

                if (_viaService)
                {
                    var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, scale, scale));
                    PrismRenderService.SetTransform(in _handles[i], in matrix);
                }
                else
                {
                    _world.EntityManager.SetComponentData(_entities[i], new LocalToWorld
                    {
                        Value = float4x4.TRS(pos, quaternion.identity, new float3(scale))
                    });
                }
            }

            int churnStart = movingCount;
            int churnCount = (int)(count * colorChurnFraction);
            for (int i = 0; i < churnCount; i++)
            {
                int idx = churnStart + i;
                if (idx >= count) break;
                float pulse = 0.5f + 0.5f * math.sin(t * 3f + idx * 0.37f);
                var pair = _palette[idx % _palette.Length];
                var color = new float4(pair.bright.x * pulse, pair.bright.y * pulse, pair.bright.z * pulse, pair.bright.w);

                if (_viaService)
                    PrismRenderService.SetColors(in _handles[idx], in color, in pair.dark, new float3(1f, 1f, 1f));
                else
                    _world.EntityManager.SetComponentData(_entities[idx],
                        new PrismBrightColorOverride { Value = PrismRenderService.ApplyColorSpace(in color) });
            }
        }

        void OnDestroy()
        {
            DiagnosticsHUD.ClearStats(StatsSection);

            if (_handles != null)
            {
                for (int i = 0; i < _handles.Length; i++)
                    PrismRenderService.Destroy(ref _handles[i]);
                _handles = null;
            }
            if (_world != null && _world.IsCreated && _entities.IsCreated)
                _world.EntityManager.DestroyEntity(_entities);
            if (_entities.IsCreated) _entities.Dispose();
            if (_basePositions.IsCreated) _basePositions.Dispose();
            if (_baseScales.IsCreated) _baseScales.Dispose();
        }
    }
}
