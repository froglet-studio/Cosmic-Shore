using System;
using System.Linq;
using CosmicShore.Engine;
using CosmicShore.Engine.Rendering;
using Unity.Entities;
using Unity.Rendering;

// The Entities shim (src/CosmicShore.Compat/Entities) is a real in-memory component store:
// these pin the original semantics the game's ECS render path relies on — writes read back,
// SetComponentData on a missing component throws, Instantiate copies by value and strips
// Prefab, destroyed handles go stale, and the default world carries no Entities Graphics system
// (so PrismRenderService keeps its MonoBehaviour path in the port).
public class EntitiesShimTests : IDisposable
{
    struct Health : IComponentData { public float Value; }
    struct Speed : IComponentData { public int Value; }
    struct Tag : IComponentData { }
    struct Toggle : IComponentData, IEnableableComponent { public int Value; }
    struct Item : IBufferElementData { public int Value; }
    struct Shared : ISharedComponentData { public int Group; }
    sealed class Managed : IComponentData { public string Label; }

    readonly World _world = new World("EntitiesShimTests");
    EntityManager Em => _world.EntityManager;

    public void Dispose() => _world.Dispose();

    [Fact]
    public void CreateEntity_ExistsWithFreshHandles()
    {
        var a = Em.CreateEntity();
        var b = Em.CreateEntity();
        Assert.True(Em.Exists(a));
        Assert.True(Em.Exists(b));
        Assert.NotEqual(a, b);
        Assert.NotEqual(Entity.Null, a);
        Assert.False(Em.Exists(Entity.Null));
        Assert.Equal(2, Em.Debug.EntityCount);
    }

    [Fact]
    public void AddGetSetComponentData_RoundTrips()
    {
        var e = Em.CreateEntity();
        Assert.False(Em.HasComponent<Health>(e));

        Assert.True(Em.AddComponentData(e, new Health { Value = 3f }));
        Assert.True(Em.HasComponent<Health>(e));
        Assert.Equal(3f, Em.GetComponentData<Health>(e).Value);

        Em.SetComponentData(e, new Health { Value = 7f });
        Assert.Equal(7f, Em.GetComponentData<Health>(e).Value);

        // AddComponentData on a present component overwrites and reports "not added".
        Assert.False(Em.AddComponentData(e, new Health { Value = 9f }));
        Assert.Equal(9f, Em.GetComponentData<Health>(e).Value);
    }

    [Fact]
    public void SetOrGetMissingComponent_Throws()
    {
        var e = Em.CreateEntity();
        Assert.Throws<ArgumentException>(() => Em.SetComponentData(e, new Speed { Value = 1 }));
        Assert.Throws<ArgumentException>(() => Em.GetComponentData<Speed>(e));
    }

    [Fact]
    public void AddComponent_DefaultValue_ThenRemove()
    {
        var e = Em.CreateEntity(ComponentType.ReadWrite<Tag>());
        Assert.True(Em.HasComponent<Tag>(e));
        Assert.True(Em.AddComponent<Speed>(e));
        Assert.False(Em.AddComponent<Speed>(e)); // already present
        Assert.Equal(0, Em.GetComponentData<Speed>(e).Value);

        Assert.True(Em.RemoveComponent<Speed>(e));
        Assert.False(Em.HasComponent<Speed>(e));
        Assert.False(Em.RemoveComponent<Speed>(e));
    }

    [Fact]
    public void DestroyEntity_StaleHandleNeverAliasesReusedSlot()
    {
        var e = Em.CreateEntity();
        Em.AddComponentData(e, new Health { Value = 1f });
        Em.DestroyEntity(e);
        Assert.False(Em.Exists(e));
        Assert.Throws<ArgumentException>(() => Em.GetComponentData<Health>(e));
        Em.DestroyEntity(e); // no-op on a dead handle

        var reused = Em.CreateEntity();
        Assert.Equal(e.Index, reused.Index);
        Assert.NotEqual(e.Version, reused.Version);
        Assert.False(Em.Exists(e));
        Assert.False(Em.HasComponent<Health>(reused));
    }

    [Fact]
    public void Instantiate_CopiesByValue_AndStripsPrefab()
    {
        var prototype = Em.CreateEntity();
        Em.AddComponentData(prototype, new Health { Value = 5f });
        Em.AddComponent<Prefab>(prototype);
        Em.AddComponent<DisableRendering>(prototype);

        var clone = Em.Instantiate(prototype);
        Assert.NotEqual(prototype, clone);
        Assert.Equal(5f, Em.GetComponentData<Health>(clone).Value);
        Assert.True(Em.HasComponent<DisableRendering>(clone));
        Assert.False(Em.HasComponent<Prefab>(clone));
        Assert.True(Em.HasComponent<Prefab>(prototype));

        Em.SetComponentData(clone, new Health { Value = 42f });
        Assert.Equal(5f, Em.GetComponentData<Health>(prototype).Value);

        var batch = Em.InstantiateManaged(prototype, 4);
        Assert.Equal(4, batch.Distinct().Count());
        Assert.All(batch, c => Assert.Equal(5f, Em.GetComponentData<Health>(c).Value));
    }

    [Fact]
    public void Buffers_AliasPerEntity_AndDeepCopyOnInstantiate()
    {
        var e = Em.CreateEntity();
        var buffer = Em.AddBuffer<Item>(e);
        buffer.Add(new Item { Value = 1 });
        Em.GetBuffer<Item>(e).Add(new Item { Value = 2 });
        Assert.Equal(2, buffer.Length);

        var clone = Em.Instantiate(e);
        Em.GetBuffer<Item>(clone).Add(new Item { Value = 3 });
        Assert.Equal(2, Em.GetBuffer<Item>(e).Length);
        Assert.Equal(3, Em.GetBuffer<Item>(clone).Length);
        Assert.Equal(2, Em.GetBuffer<Item>(clone)[1].Value);
    }

    [Fact]
    public void EnableableComponent_StaysPresent_ButLeavesQueries()
    {
        var e = Em.CreateEntity();
        Em.AddComponentData(e, new Toggle { Value = 1 });
        var query = Em.CreateEntityQuery(ComponentType.ReadOnly<Toggle>());
        Assert.Equal(1, query.CalculateEntityCount());

        Em.SetComponentEnabled<Toggle>(e, false);
        Assert.True(Em.HasComponent<Toggle>(e));
        Assert.False(Em.IsComponentEnabled<Toggle>(e));
        Assert.Equal(0, query.CalculateEntityCount());

        var clone = Em.Instantiate(e);
        Assert.False(Em.IsComponentEnabled<Toggle>(clone));

        Em.SetComponentEnabled<Toggle>(e, true);
        Assert.Equal(1, query.CalculateEntityCount());
    }

    [Fact]
    public void Query_ExcludesPrefabsAndDisabled_AndHonorsExclude()
    {
        var plain = Em.CreateEntity();
        Em.AddComponentData(plain, new Health());
        var prefab = Em.CreateEntity();
        Em.AddComponentData(prefab, new Health());
        Em.AddComponent<Prefab>(prefab);
        var disabled = Em.CreateEntity();
        Em.AddComponentData(disabled, new Health());
        Em.SetEnabled(disabled, false);
        var tagged = Em.CreateEntity();
        Em.AddComponentData(tagged, new Health());
        Em.AddComponent<Tag>(tagged);

        var all = Em.CreateEntityQuery(ComponentType.ReadOnly<Health>()).ToEntityArrayManaged();
        Assert.Equal(new[] { plain, tagged }.OrderBy(x => x.Index), all.OrderBy(x => x.Index));

        var untagged = Em.CreateEntityQuery(ComponentType.ReadOnly<Health>(), ComponentType.Exclude<Tag>()).ToEntityArrayManaged();
        Assert.Equal(new[] { plain }, untagged);

        Em.SetEnabled(disabled, true);
        Assert.True(Em.IsEnabled(disabled));
        Assert.Equal(3, Em.CreateEntityQuery(ComponentType.ReadOnly<Health>()).CalculateEntityCount());
    }

    [Fact]
    public void SharedAndManagedComponents_RoundTrip()
    {
        var e = Em.CreateEntity();
        Em.AddSharedComponentManaged(e, new Shared { Group = 4 });
        Assert.Equal(4, Em.GetSharedComponentManaged<Shared>(e).Group);
        Em.SetSharedComponentManaged(e, new Shared { Group = 6 });
        Assert.Equal(6, Em.GetSharedComponentManaged<Shared>(e).Group);

        Em.AddComponentData(e, new Managed { Label = "x" });
        Assert.Equal("x", Em.GetComponentData<Managed>(e).Label);
    }

    [Fact]
    public void LinkedEntityGroup_InstantiatesAndDestroysTogether()
    {
        var root = Em.CreateEntity();
        var child = Em.CreateEntity();
        Em.AddComponentData(child, new Speed { Value = 8 });
        var group = Em.AddBuffer<LinkedEntityGroup>(root);
        group.Add(root);
        group.Add(child);

        var cloneRoot = Em.Instantiate(root);
        var cloneGroup = Em.GetBuffer<LinkedEntityGroup>(cloneRoot);
        Assert.Equal(2, cloneGroup.Length);
        Assert.Equal(cloneRoot, cloneGroup[0].Value);
        var cloneChild = cloneGroup[1].Value;
        Assert.NotEqual(child, cloneChild);
        Assert.Equal(8, Em.GetComponentData<Speed>(cloneChild).Value);

        Em.DestroyEntity(cloneRoot);
        Assert.False(Em.Exists(cloneChild));
        Assert.True(Em.Exists(child));
    }

    [Fact]
    public void WorldDispose_InvalidatesEverything()
    {
        var w = new World("disposable");
        var em = w.EntityManager;
        var e = em.CreateEntity();
        Assert.Contains(w, World.All);
        w.Dispose();
        Assert.False(w.IsCreated);
        Assert.False(em.IsCreated);
        Assert.False(em.Exists(e));
        Assert.DoesNotContain(w, World.All);
    }

    [Fact]
    public void DefaultWorld_HasTheEntitiesGraphicsSystem_WithStableRegistrations()
    {
        var previous = World.DefaultGameObjectInjectionWorld;
        var previousFlag = DefaultWorldInitialization.UseCustomBootstrap;
        DefaultWorldInitialization.UseCustomBootstrap = false;
        try
        {
            var world = DefaultWorldInitialization.Initialize("Default World");
            try
            {
                Assert.Same(world, World.DefaultGameObjectInjectionWorld);
                Assert.True(world.IsCreated);
                var graphics = world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                Assert.NotNull(graphics); // the original's default bootstrap creates it
                Assert.NotNull(world.GetExistingSystemManaged<SimulationSystemGroup>());
                Assert.Same(graphics, world.GetOrCreateSystemManaged<EntitiesGraphicsSystem>());

                var mesh = new Mesh();
                var material = new Material(Shader.Find("Hidden/EntitiesShimTest"));
                BatchMeshID meshId = graphics.RegisterMesh(mesh);
                Assert.NotEqual(BatchMeshID.Null, meshId);
                Assert.Equal(meshId, graphics.RegisterMesh(mesh)); // same mesh, same id (ref-counted)
                Assert.Same(mesh, graphics.GetMesh(meshId));
                BatchMaterialID materialId = graphics.RegisterMaterial(material);
                Assert.Same(material, graphics.GetMaterial(materialId));

                var info = new MaterialMeshInfo(materialId, meshId);
                Assert.Equal(meshId, info.MeshID);
                Assert.Equal(materialId, info.MaterialID);

                var e = world.EntityManager.CreateEntity();
                world.EntityManager.AddComponentData(e, info);
                Assert.Equal(meshId, world.EntityManager.GetComponentData<MaterialMeshInfo>(e).MeshID);
            }
            finally
            {
                world.Dispose();
            }
            Assert.Null(World.DefaultGameObjectInjectionWorld);
        }
        finally
        {
            DefaultWorldInitialization.UseCustomBootstrap = previousFlag;
            World.DefaultGameObjectInjectionWorld = previous;
        }
    }

    [MaterialProperty("_ShimTestTint")]
    struct ShimTintOverride : IComponentData { public Unity.Mathematics.float4 Value; }

    [Fact]
    public void DrawCollection_CachedPlanFollowsStructuralChanges_NotValueWrites()
    {
        var previous = World.DefaultGameObjectInjectionWorld;
        var previousFlag = DefaultWorldInitialization.UseCustomBootstrap;
        DefaultWorldInitialization.UseCustomBootstrap = false;
        try
        {
            var world = DefaultWorldInitialization.Initialize("Default World");
            try
            {
                var graphics = world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                var mesh = new Mesh();
                var material = new Material(Shader.Find("Hidden/EntitiesShimTest"));
                var em = world.EntityManager;
                var e = em.CreateEntity();
                em.AddComponentData(e, new MaterialMeshInfo(graphics.RegisterMaterial(material), graphics.RegisterMesh(mesh)));
                em.AddComponentData(e, new Unity.Transforms.LocalToWorld { Value = Matrix4x4.identity });
                int tint = EntityDrawList.Slot("_ShimTestTint");

                int Count(out bool tinted, out Vector4 value)
                {
                    var list = new EntityDrawList();
                    EntityDraws.Collect(list);
                    value = default;
                    tinted = list.Count > 0 && list.TryGet(0, tint, out value);
                    return list.Count;
                }

                Assert.Equal(1, Count(out var t0, out _));
                Assert.False(t0);

                em.AddComponentData(e, new ShimTintOverride { Value = new Unity.Mathematics.float4(1, 0, 0, 1) });
                Assert.Equal(1, Count(out var t1, out var v1));   // a newly added binding is picked up
                Assert.True(t1);
                Assert.Equal(new Vector4(1, 0, 0, 1), v1);

                em.SetComponentData(e, new ShimTintOverride { Value = new Unity.Mathematics.float4(0, 1, 0, 1) });
                Count(out _, out var v2);                            // a value write reaches the cached plan
                Assert.Equal(new Vector4(0, 1, 0, 1), v2);

                em.AddComponentData(e, new DisableRendering());
                Assert.Equal(0, Count(out _, out _));                // hidden
                em.RemoveComponent<DisableRendering>(e);
                Assert.Equal(1, Count(out _, out _));                // shown again
                em.DestroyEntity(e);
                Assert.Equal(0, Count(out _, out _));
            }
            finally { world.Dispose(); }
        }
        finally
        {
            World.DefaultGameObjectInjectionWorld = previous;
            DefaultWorldInitialization.UseCustomBootstrap = previousFlag;
        }
    }

    [Fact]
    public void DrawCollection_HandsVisibleEntitiesAndTheirOverridesToTheRenderer()
    {
        var previous = World.DefaultGameObjectInjectionWorld;
        var previousFlag = DefaultWorldInitialization.UseCustomBootstrap;
        DefaultWorldInitialization.UseCustomBootstrap = false;
        try
        {
            var world = DefaultWorldInitialization.Initialize("Default World");
            try
            {
                var graphics = world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                var mesh = new Mesh();
                var material = new Material(Shader.Find("Hidden/EntitiesShimTest"));
                var info = new MaterialMeshInfo(graphics.RegisterMaterial(material), graphics.RegisterMesh(mesh));
                var em = world.EntityManager;

                Entity Make(bool hidden)
                {
                    var e = em.CreateEntity();
                    em.AddComponentData(e, info);
                    em.AddComponentData(e, new Unity.Transforms.LocalToWorld { Value = Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.identity, Vector3.one) });
                    em.AddComponentData(e, new ShimTintOverride { Value = new Unity.Mathematics.float4(0.5f, 0.25f, 1f, 1f) });
                    if (hidden) em.AddComponentData(e, new DisableRendering());
                    return e;
                }
                Make(hidden: false);
                Make(hidden: true);
                var prefab = Make(hidden: false);
                em.AddComponentData(prefab, new Prefab());

                var list = new EntityDrawList();
                EntityDraws.Collect(list);
                Assert.Equal(1, list.Count);
                Assert.Same(mesh, list.Meshes[0]);
                Assert.Same(material, list.Materials[0]);
                Assert.Equal(3f, list.Matrices[0].m23);
                Assert.True(list.TryGet(0, EntityDrawList.Slot("_ShimTestTint"), out var tint));
                Assert.Equal(new Vector4(0.5f, 0.25f, 1f, 1f), tint);
            }
            finally
            {
                world.Dispose();
            }
            var empty = new EntityDrawList();
            EntityDraws.Collect(empty);
            Assert.Equal(0, empty.Count); // a disposed world's system stops drawing
        }
        finally
        {
            DefaultWorldInitialization.UseCustomBootstrap = previousFlag;
            World.DefaultGameObjectInjectionWorld = previous;
        }
    }
}
