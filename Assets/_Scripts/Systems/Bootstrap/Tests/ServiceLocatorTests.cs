using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CosmicShore.Systems.Bootstrap
{
    [TestFixture]
    public class ServiceLocatorTests
    {
        // Dummy service types for testing.
        class ServiceA { }
        class ServiceB { }

        interface IMyService { }
        class MyServiceImpl : IMyService { }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.ClearAll();
        }

        #region Register & Get

        [Test]
        public void Register_ThenGet_ReturnsSameInstance()
        {
            var service = new ServiceA();
            ServiceLocator.Register(service);

            var retrieved = ServiceLocator.Get<ServiceA>();

            Assert.AreSame(service, retrieved);
        }

        [Test]
        public void Get_Unregistered_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, "[ServiceLocator] Service ServiceA not registered.");

            var result = ServiceLocator.Get<ServiceA>();

            Assert.IsNull(result);
        }

        [Test]
        public void Register_OverwriteExisting_ReturnsNewInstance()
        {
            var first = new ServiceA();
            var second = new ServiceA();

            ServiceLocator.Register(first);
            ServiceLocator.Register(second);

            var retrieved = ServiceLocator.Get<ServiceA>();

            Assert.AreSame(second, retrieved);
        }

        [Test]
        public void Register_ByInterface_ReturnsSameInstance()
        {
            var impl = new MyServiceImpl();
            ServiceLocator.Register<IMyService>(impl);

            var retrieved = ServiceLocator.Get<IMyService>();

            Assert.AreSame(impl, retrieved);
        }

        #endregion

        #region TryGet

        [Test]
        public void TryGet_Registered_ReturnsTrueAndService()
        {
            var service = new ServiceA();
            ServiceLocator.Register(service);

            bool found = ServiceLocator.TryGet<ServiceA>(out var retrieved);

            Assert.IsTrue(found);
            Assert.AreSame(service, retrieved);
        }

        [Test]
        public void TryGet_Unregistered_ReturnsFalseAndNull()
        {
            bool found = ServiceLocator.TryGet<ServiceA>(out var retrieved);

            Assert.IsFalse(found);
            Assert.IsNull(retrieved);
        }

        #endregion

        #region IsRegistered

        [Test]
        public void IsRegistered_Registered_ReturnsTrue()
        {
            ServiceLocator.Register(new ServiceA());

            Assert.IsTrue(ServiceLocator.IsRegistered<ServiceA>());
        }

        [Test]
        public void IsRegistered_Unregistered_ReturnsFalse()
        {
            Assert.IsFalse(ServiceLocator.IsRegistered<ServiceA>());
        }

        [Test]
        public void IsRegistered_SceneService_ReturnsTrue()
        {
            ServiceLocator.RegisterSceneService(new ServiceA());

            Assert.IsTrue(ServiceLocator.IsRegistered<ServiceA>());
        }

        #endregion

        #region Unregister

        [Test]
        public void Unregister_RemovesGlobalService()
        {
            ServiceLocator.Register(new ServiceA());

            ServiceLocator.Unregister<ServiceA>();

            Assert.IsFalse(ServiceLocator.IsRegistered<ServiceA>());
        }

        [Test]
        public void Unregister_RemovesSceneService()
        {
            ServiceLocator.RegisterSceneService(new ServiceA());

            ServiceLocator.Unregister<ServiceA>();

            Assert.IsFalse(ServiceLocator.IsRegistered<ServiceA>());
        }

        [Test]
        public void Unregister_NonExistent_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ServiceLocator.Unregister<ServiceA>());
        }

        #endregion

        #region Scene Services

        [Test]
        public void RegisterSceneService_ThenGet_ReturnsSameInstance()
        {
            var service = new ServiceA();
            ServiceLocator.RegisterSceneService(service);

            var retrieved = ServiceLocator.Get<ServiceA>();

            Assert.AreSame(service, retrieved);
        }

        [Test]
        public void SceneService_TakesPriorityOverGlobal()
        {
            var global = new ServiceA();
            var sceneScoped = new ServiceA();

            ServiceLocator.Register(global);
            ServiceLocator.RegisterSceneService(sceneScoped);

            var retrieved = ServiceLocator.Get<ServiceA>();

            Assert.AreSame(sceneScoped, retrieved);
        }

        [Test]
        public void TryGet_SceneServicePriority_ReturnsSceneService()
        {
            var global = new ServiceA();
            var sceneScoped = new ServiceA();

            ServiceLocator.Register(global);
            ServiceLocator.RegisterSceneService(sceneScoped);

            ServiceLocator.TryGet<ServiceA>(out var retrieved);

            Assert.AreSame(sceneScoped, retrieved);
        }

        [Test]
        public void ClearSceneServices_RemovesOnlySceneServices()
        {
            var global = new ServiceA();
            var sceneScoped = new ServiceB();

            ServiceLocator.Register(global);
            ServiceLocator.RegisterSceneService(sceneScoped);

            ServiceLocator.ClearSceneServices();

            Assert.IsTrue(ServiceLocator.IsRegistered<ServiceA>());
            Assert.IsFalse(ServiceLocator.IsRegistered<ServiceB>());
        }

        [Test]
        public void ClearSceneServices_FallsBackToGlobal()
        {
            var global = new ServiceA();
            var sceneScoped = new ServiceA();

            ServiceLocator.Register(global);
            ServiceLocator.RegisterSceneService(sceneScoped);

            ServiceLocator.ClearSceneServices();

            var retrieved = ServiceLocator.Get<ServiceA>();

            Assert.AreSame(global, retrieved);
        }

        #endregion

        #region ClearAll

        [Test]
        public void ClearAll_RemovesGlobalAndSceneServices()
        {
            ServiceLocator.Register(new ServiceA());
            ServiceLocator.RegisterSceneService(new ServiceB());

            ServiceLocator.ClearAll();

            Assert.IsFalse(ServiceLocator.IsRegistered<ServiceA>());
            Assert.IsFalse(ServiceLocator.IsRegistered<ServiceB>());
        }

        #endregion

        #region Multiple Service Types

        [Test]
        public void Register_MultipleDifferentTypes_AllRetrievable()
        {
            var a = new ServiceA();
            var b = new ServiceB();

            ServiceLocator.Register(a);
            ServiceLocator.Register(b);

            Assert.AreSame(a, ServiceLocator.Get<ServiceA>());
            Assert.AreSame(b, ServiceLocator.Get<ServiceB>());
        }

        [Test]
        public void Unregister_OneType_DoesNotAffectOther()
        {
            ServiceLocator.Register(new ServiceA());
            ServiceLocator.Register(new ServiceB());

            ServiceLocator.Unregister<ServiceA>();

            Assert.IsFalse(ServiceLocator.IsRegistered<ServiceA>());
            Assert.IsTrue(ServiceLocator.IsRegistered<ServiceB>());
        }

        #endregion
    }
}
