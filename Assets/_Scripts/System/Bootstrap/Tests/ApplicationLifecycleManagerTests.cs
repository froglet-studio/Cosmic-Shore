using System;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Core
{
    [TestFixture]
    public class ApplicationLifecycleManagerTests
    {
        GameObject _go;
        ApplicationLifecycleManager _manager;

        [SetUp]
        public void SetUp()
        {
            // Reset static state before each test.
            ResetStatics();

            _go = new GameObject("TestLifecycleManager");
            _manager = _go.AddComponent<ApplicationLifecycleManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);

            ResetStatics();
        }

        /// <summary>
        /// Invokes the private static ResetStatics method via reflection.
        /// </summary>
        static void ResetStatics()
        {
            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("ResetStatics",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(null, null);
        }

        /// <summary>
        /// Creates a fully wired <see cref="ApplicationLifecycleEventsContainerSO"/>
        /// and injects it into the manager via reflection.
        /// </summary>
        static ApplicationLifecycleEventsContainerSO CreateAndInjectContainer(ApplicationLifecycleManager manager)
        {
            var container = ScriptableObject.CreateInstance<ApplicationLifecycleEventsContainerSO>();
            container.OnAppPaused = ScriptableObject.CreateInstance<ScriptableEventBool>();
            container.OnAppFocusChanged = ScriptableObject.CreateInstance<ScriptableEventBool>();
            container.OnAppQuitting = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            container.OnSceneLoaded = ScriptableObject.CreateInstance<ScriptableEventString>();
            container.OnSceneUnloading = ScriptableObject.CreateInstance<ScriptableEventString>();

            var field = typeof(ApplicationLifecycleManager)
                .GetField("_lifecycleEvents",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(manager, container);

            return container;
        }

        #region IsQuitting

        [Test]
        public void IsQuitting_InitiallyFalse()
        {
            Assert.IsFalse(ApplicationLifecycleManager.IsQuitting);
        }

        #endregion

        #region Event Subscription

        [Test]
        public void OnAppPaused_CanSubscribeAndReceiveCallback()
        {
            bool received = false;
            bool pauseValue = false;

            ApplicationLifecycleManager.OnAppPaused += paused =>
            {
                received = true;
                pauseValue = paused;
            };

            // Simulate pause via reflection (OnApplicationPause is called by Unity).
            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationPause",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, new object[] { true });

            Assert.IsTrue(received);
            Assert.IsTrue(pauseValue);
        }

        [Test]
        public void OnAppFocusChanged_CanSubscribeAndReceiveCallback()
        {
            bool received = false;
            bool focusValue = false;

            ApplicationLifecycleManager.OnAppFocusChanged += focus =>
            {
                received = true;
                focusValue = focus;
            };

            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationFocus",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, new object[] { true });

            Assert.IsTrue(received);
            Assert.IsTrue(focusValue);
        }

        [Test]
        public void OnAppQuitting_SetsIsQuittingAndFiresEvent()
        {
            bool received = false;
            ApplicationLifecycleManager.OnAppQuitting += () => received = true;

            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationQuit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, null);

            Assert.IsTrue(received);
            Assert.IsTrue(ApplicationLifecycleManager.IsQuitting);
        }

        #endregion

        #region SOAP Container Wiring

        [Test]
        public void LifecycleEvents_ContainerCanBeInjected()
        {
            var container = CreateAndInjectContainer(_manager);

            var field = typeof(ApplicationLifecycleManager)
                .GetField("_lifecycleEvents",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var value = field?.GetValue(_manager) as ApplicationLifecycleEventsContainerSO;

            Assert.IsNotNull(value);
            Assert.AreEqual(container, value);
        }

        [Test]
        public void WithoutContainer_StaticEventsStillFire()
        {
            // Verify that when no SOAP container is injected, static events still work.
            bool pauseReceived = false;
            ApplicationLifecycleManager.OnAppPaused += _ => pauseReceived = true;

            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationPause",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, new object[] { true });

            Assert.IsTrue(pauseReceived);
        }

        [Test]
        public void WithContainer_StaticEventsStillFire()
        {
            // Verify static events fire even when SOAP container is present.
            CreateAndInjectContainer(_manager);

            bool pauseReceived = false;
            ApplicationLifecycleManager.OnAppPaused += _ => pauseReceived = true;

            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationPause",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, new object[] { true });

            Assert.IsTrue(pauseReceived);
        }

        #endregion

        #region ResetStatics

        [Test]
        public void ResetStatics_ClearsIsQuitting()
        {
            // Trigger quit to set IsQuitting = true.
            var quitMethod = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationQuit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            quitMethod?.Invoke(_manager, null);

            Assert.IsTrue(ApplicationLifecycleManager.IsQuitting);

            ResetStatics();

            Assert.IsFalse(ApplicationLifecycleManager.IsQuitting);
        }

        [Test]
        public void ResetStatics_ClearsAllEvents()
        {
            bool pauseFired = false;
            bool focusFired = false;
            bool quitFired = false;

            ApplicationLifecycleManager.OnAppPaused += _ => pauseFired = true;
            ApplicationLifecycleManager.OnAppFocusChanged += _ => focusFired = true;
            ApplicationLifecycleManager.OnAppQuitting += () => quitFired = true;

            ResetStatics();

            // After reset, triggering events should not fire the old handlers.
            var pauseMethod = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationPause",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pauseMethod?.Invoke(_manager, new object[] { true });

            var focusMethod = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationFocus",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            focusMethod?.Invoke(_manager, new object[] { true });

            Assert.IsFalse(pauseFired);
            Assert.IsFalse(focusFired);
            Assert.IsFalse(quitFired);
        }

        #endregion

        #region Scene Events

        [Test]
        public void HandleSceneLoaded_FiresOnSceneLoadedEvent()
        {
            bool received = false;
            string loadedSceneName = null;

            ApplicationLifecycleManager.OnSceneLoaded += (scene, mode) =>
            {
                received = true;
                loadedSceneName = scene.name;
            };

            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("HandleSceneLoaded",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });

            Assert.IsTrue(received);
            Assert.IsNotNull(loadedSceneName);
        }

        [Test]
        public void HandleSceneUnloaded_FiresOnSceneUnloadingEvent()
        {
            bool received = false;

            ApplicationLifecycleManager.OnSceneUnloading += scene => received = true;

            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("HandleSceneUnloaded",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, new object[] { SceneManager.GetActiveScene() });

            Assert.IsTrue(received);
        }

        #endregion
    }
}
