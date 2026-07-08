using System;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
// Same alias upstream AppManager carries: the engine also has a screen Resolution type.
using Resolution = CosmicShore.Engine.Injection.Resolution;

namespace CosmicShore.Tests;

public class InjectionTests
{
    interface IService { string Name { get; } }

    class ServiceImpl : IService
    {
        public string Name => "impl";
        public static int ConstructionCount;
        public ServiceImpl() => ConstructionCount++;
    }

    class ConsumerBase
    {
        [Inject] protected IService baseService;
        public IService BaseService => baseService;
    }

    class Consumer : ConsumerBase
    {
        [Inject] IService _service;
        [Inject] public ServiceImpl Concrete { get; set; }
        public IService Service => _service;
    }

    [Fact]
    public void RegisterValue_ResolvesByContractType()
    {
        var container = new Container();
        var impl = new ServiceImpl();
        container.RegisterValue<IService>(impl);

        Assert.Same(impl, container.Resolve<IService>());
        Assert.False(container.IsRegistered<ServiceImpl>()); // contract-exact, no assignable scanning
    }

    [Fact]
    public void RegisterFactory_IsLazySingleton()
    {
        ServiceImpl.ConstructionCount = 0;
        var container = new Container();
        container.RegisterFactory(() => new ServiceImpl());

        Assert.Equal(0, ServiceImpl.ConstructionCount); // lazy

        var first = container.Resolve<ServiceImpl>();
        var second = container.Resolve<ServiceImpl>();

        Assert.Equal(1, ServiceImpl.ConstructionCount); // singleton
        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_Missing_FailsLoud()
    {
        var container = new Container();
        var ex = Assert.Throws<InvalidOperationException>(() => container.Resolve<IService>());
        Assert.Contains("IService", ex.Message);
    }

    [Fact]
    public void Inject_PopulatesPrivate_Inherited_AndPropertyMembers()
    {
        var container = new Container();
        var impl = new ServiceImpl();
        container.RegisterValue<IService>(impl);
        container.RegisterValue(impl);

        var consumer = new Consumer();
        container.Inject(consumer);

        Assert.Same(impl, consumer.Service);       // private field
        Assert.Same(impl, consumer.BaseService);   // inherited protected field
        Assert.Same(impl, consumer.Concrete);      // property
    }

    [Fact]
    public void ChildContainer_ResolvesThroughParent_AndOverrides()
    {
        var parent = new Container();
        var parentImpl = new ServiceImpl();
        parent.RegisterValue<IService>(parentImpl);

        var child = parent.CreateChild();
        Assert.Same(parentImpl, child.Resolve<IService>()); // falls through

        var childImpl = new ServiceImpl();
        child.RegisterValue<IService>(childImpl);
        Assert.Same(childImpl, child.Resolve<IService>());  // override wins
        Assert.Same(parentImpl, parent.Resolve<IService>()); // parent untouched
    }

    [Fact]
    public void Factory_CanResolveDependenciesFromContainer()
    {
        var container = new Container();
        container.RegisterValue<IService>(new ServiceImpl());
        container.RegisterFactory(c => new Holder(c.Resolve<IService>()));

        Assert.NotNull(container.Resolve<Holder>().Service);
    }

    class Holder
    {
        public readonly IService Service;
        public Holder(IService service) { Service = service; }
    }

    class InjectedBehaviour : MonoBehaviour
    {
        [Inject] public IService Service;
    }

    [Fact]
    public void InjectGameObject_PopulatesComponentsRecursively()
    {
        using var loop = new GameLoop();
        var container = new Container();
        var impl = new ServiceImpl();
        container.RegisterValue<IService>(impl);

        var root = new GameObject("root");
        var child = new GameObject("child");
        child.transform.SetParent(root.transform);
        var rootComponent = root.AddComponent<InjectedBehaviour>();
        var childComponent = child.AddComponent<InjectedBehaviour>();

        container.InjectGameObject(root);

        Assert.Same(impl, rootComponent.Service);
        Assert.Same(impl, childComponent.Service);
    }

    [Fact]
    public void Installer_Pattern_Works()
    {
        // IInstaller carries the Reflex builder contract: install into a
        // ContainerBuilder, then Build() the scope (the AppManager flow).
        var builder = new ContainerBuilder();
        IInstaller installer = new TestInstaller();
        installer.InstallBindings(builder);
        var container = builder.Build();

        Assert.Equal("impl", container.Resolve<IService>().Name);
    }

    class TestInstaller : IInstaller
    {
        public void InstallBindings(ContainerBuilder builder) => builder.RegisterValue<IService>(new ServiceImpl());
    }

    // ── ContainerBuilder (the Reflex builder surface AppManager drives) ──

    [Fact]
    public void Builder_FactoriesSeeSiblingBindings_RegardlessOfRegistrationOrder()
    {
        var builder = new ContainerBuilder();
        // The dependent factory registers FIRST — at Build it must still resolve
        // the sibling registered after it (the Reflex contract the party-service
        // registrations in AppManager rely on).
        builder.RegisterFactory(c => new Consumer { Concrete = c.Resolve<ServiceImpl>() },
            lifetime: Lifetime.Singleton, resolution: Resolution.Lazy);
        var impl = new ServiceImpl();
        builder.RegisterValue(impl);

        var container = builder.Build();

        Assert.Same(impl, container.Resolve<Consumer>().Concrete);
    }

    [Fact]
    public void Builder_LazySingleton_RunsTheFactoryOnceOnFirstResolve()
    {
        int constructions = 0;
        var builder = new ContainerBuilder();
        builder.RegisterFactory(_ => { constructions++; return new ServiceImpl(); },
            lifetime: Lifetime.Singleton, resolution: Resolution.Lazy);

        var container = builder.Build();
        Assert.Equal(0, constructions); // lazy — nothing ran at Build

        var first = container.Resolve<ServiceImpl>();
        var second = container.Resolve<ServiceImpl>();
        Assert.Equal(1, constructions);
        Assert.Same(first, second);
    }

    [Fact]
    public void Builder_EagerResolution_RunsTheFactoryAtBuild()
    {
        int constructions = 0;
        var builder = new ContainerBuilder();
        builder.RegisterFactory(_ => { constructions++; return new ServiceImpl(); },
            lifetime: Lifetime.Singleton, resolution: Resolution.Eager);

        builder.Build();

        Assert.Equal(1, constructions);
    }

    [Fact]
    public void Builder_NonSingletonLifetimes_FailLoudAtRegistration()
    {
        var builder = new ContainerBuilder();
        Assert.Throws<NotSupportedException>(() =>
            builder.RegisterFactory(_ => new ServiceImpl(), lifetime: Lifetime.Transient));
    }

    [Fact]
    public void Builder_BuildWithParent_ChainsScopes()
    {
        var rootBuilder = new ContainerBuilder();
        var impl = new ServiceImpl();
        rootBuilder.RegisterValue<IService>(impl);
        var root = rootBuilder.Build();

        var child = new ContainerBuilder().Build(parent: root);

        Assert.Same(impl, child.Resolve<IService>());
    }

    [Fact]
    public void Builder_RegistersTheManagerShellFamily_AsLazySingletons()
    {
        // Mirrors AppManager.RegisterManagerSingleton for the six shelled managers:
        // serialized reference preferred, lazy factory otherwise. All six must be
        // registrable + resolvable so the AppManager port compiles against them.
        using var loop = new GameLoop(nameof(Builder_RegistersTheManagerShellFamily_AsLazySingletons));
        var go = new GameObject("managers");
        var builder = new ContainerBuilder();
        builder.RegisterFactory(_ => go.AddComponent<CosmicShore.Core.UGSStatsManager>());
        builder.RegisterFactory(_ => go.AddComponent<CosmicShore.Core.CaptainManager>());
        builder.RegisterFactory(_ => go.AddComponent<CosmicShore.Core.IAPManager>());
        builder.RegisterFactory(_ => go.AddComponent<CosmicShore.Gameplay.PostProcessingManager>());
        builder.RegisterFactory(_ => go.AddComponent<CosmicShore.Gameplay.StatsManager>());
        builder.RegisterFactory(_ => go.AddComponent<CosmicShore.Core.UGSDataService>());
        var container = builder.Build();

        Assert.NotNull(container.Resolve<CosmicShore.Core.UGSStatsManager>());
        Assert.NotNull(container.Resolve<CosmicShore.Core.CaptainManager>());
        Assert.NotNull(container.Resolve<CosmicShore.Core.IAPManager>());
        Assert.NotNull(container.Resolve<CosmicShore.Gameplay.PostProcessingManager>());
        Assert.NotNull(container.Resolve<CosmicShore.Gameplay.StatsManager>());
        Assert.NotNull(container.Resolve<CosmicShore.Core.UGSDataService>());
    }
}
