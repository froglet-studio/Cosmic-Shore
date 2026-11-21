using Unity.Entities;

namespace CosmicShore.Tests;

/// <summary>
/// ComponentLookup: random access by entity, handed out by a system (the prism render service
/// writes mesh, transform and colour components through lookups captured in jobs).
/// </summary>
public class ComponentLookupTests
{
    struct Health : IComponentData { public int Value; }
    struct Shield : IComponentData { public int Value; }

    sealed partial class LookupSystem : SystemBase { protected override void OnUpdate() { } }

    [Fact]
    public void ALookupReadsAndWritesLiveComponentData()
    {
        using var world = new World("ComponentLookupTests");
        var em = world.EntityManager;
        var a = em.CreateEntity();
        em.AddComponentData(a, new Health { Value = 3 });
        var b = em.CreateEntity();

        var lookup = world.GetOrCreateSystemManaged<LookupSystem>().GetComponentLookup<Health>(isReadOnly: false);
        Assert.True(lookup.HasComponent(a));
        Assert.False(lookup.HasComponent(b));
        Assert.Equal(3, lookup[a].Value);

        lookup[a] = new Health { Value = 9 };                  // a job's write lands in the world
        Assert.Equal(9, em.GetComponentData<Health>(a).Value);

        Assert.True(lookup.TryGetComponent(a, out var h) && h.Value == 9);
        Assert.False(lookup.TryGetComponent(b, out _));
        Assert.False(world.GetOrCreateSystemManaged<LookupSystem>().GetComponentLookup<Shield>(true).HasComponent(a));

        em.DestroyEntity(a);
        Assert.False(lookup.EntityExists(a));
        Assert.False(lookup.HasComponent(a, out bool exists) || exists);
    }
}
