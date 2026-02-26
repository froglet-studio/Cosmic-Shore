using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Systems.Bootstrap
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
            ServiceLocator.ClearAll();

            _go = new GameObject("TestLifecycleManager");
            _manager = _go.AddComponent<ApplicationLifecycleManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);

            ResetStatics();
            ServiceLocator.ClearAll();
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

        [Test]
        public void OnAppQuitting_ClearsServiceLocator()
        {
            ServiceLocator.Register(new object());

            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("OnApplicationQuit",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, null);

            Assert.IsFalse(ServiceLocator.IsRegistered<object>());
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
        public void HandleSceneUnloaded_ClearsSceneServices()
        {
            ServiceLocator.RegisterSceneService(new object());

            var method = typeof(ApplicationLifecycleManager)
                .GetMethod("HandleSceneUnloaded",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_manager, new object[] { SceneManager.GetActiveScene() });

            Assert.IsFalse(ServiceLocator.IsRegistered<object>());
        }

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
