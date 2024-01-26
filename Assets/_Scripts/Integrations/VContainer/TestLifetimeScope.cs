using System;
using CosmicShore.Integrations.Loxodon.MVVM;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CosmicShore.Integrations.VContainer
{
    public class TestLifetimeScope : LifetimeScope
    {
        [SerializeField] TestMenu testMenu;
        [SerializeField] private TestComponentA testComponentA;
        protected override void Configure(IContainerBuilder builder)
        {
            // builder.RegisterEntryPoint<TestPresenter>();
            // builder.Register<TestService>(Lifetime.Singleton).As<IService, IDisposable>();
            builder.Register<TestService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<IServiceA, TestServiceA>(Lifetime.Singleton);
            
            // builder.Register<IModelB>(container =>
            // {
            //     var serviceA = container.Resolve<TestServiceA>();
            //     return serviceA.ProvideModelB();
            // }, Lifetime.Singleton);
            
            builder.Register<IServiceB, TestServiceB>(Lifetime.Singleton);
            builder.RegisterComponent(testComponentA);
            builder.RegisterComponent(testMenu);

            builder.Register<IModel>(_ =>
            {
                return new TestModelA{Id = 11, Name = "Mario", StartDate = DateTime.Today};
            }, Lifetime.Scoped);
            builder.Register<IModelB>(_ =>
            {
                return new TestModelB();
            }, Lifetime.Scoped);


            // Use registered entry point with a group
            builder.UseEntryPoints(Lifetime.Singleton, entryPoints =>
            {
                entryPoints.Add<TestPresenter>().AsSelf();
                entryPoints.Add<ModuleA>();
                entryPoints.Add<TestMessenger>();
            });
            
            builder.RegisterEntryPointExceptionHandler(ex =>
            {
                Debug.LogException(ex);
                // Additional process for handling exceptions
            });
        }
    }
}