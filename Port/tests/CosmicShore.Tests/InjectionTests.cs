using System;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;

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
        var container = new Container();
        IInstaller installer = new TestInstaller();
        installer.InstallBindings(container);

        Assert.Equal("impl", container.Resolve<IService>().Name);
    }

    class TestInstaller : IInstaller
    {
        public void InstallBindings(Container container) => container.RegisterValue<IService>(new ServiceImpl());
    }
}
