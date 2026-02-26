using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    [TestFixture]
    public class BootstrapControllerTests
    {
        [SetUp]
        public void SetUp()
        {
            // Reset the static _hasBootstrapped flag before each test.
            ResetHasBootstrapped();
            ServiceLocator.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            ResetHasBootstrapped();
            ServiceLocator.ClearAll();
        }

        static void ResetHasBootstrapped()
        {
            var field = typeof(BootstrapController)
                .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, false);
        }

        static void SetHasBootstrapped(bool value)
        {
            var field = typeof(BootstrapController)
                .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, value);
        }

        #region HasBootstrapped

        [Test]
        public void HasBootstrapped_InitiallyFalse()
        {
            Assert.IsFalse(BootstrapController.HasBootstrapped);
        }

        [Test]
        public void HasBootstrapped_ReturnsTrueWhenSet()
        {
            SetHasBootstrapped(true);

            Assert.IsTrue(BootstrapController.HasBootstrapped);
        }

        #endregion

        #region EnsureBootstrapOnStartup

        [Test]
        public void EnsureBootstrapOnStartup_ResetsHasBootstrapped()
        {
            SetHasBootstrapped(true);

            // Invoke the static initializer method.
            var method = typeof(BootstrapController)
                .GetMethod("EnsureBootstrapOnStartup", BindingFlags.Static | BindingFlags.NonPublic);
            method?.Invoke(null, null);

            Assert.IsFalse(BootstrapController.HasBootstrapped);
        }

        #endregion

        #region Awake — Persistent Root

        [Test]
        public void Awake_NoPersistentRoot_UsesSelf()
        {
            var go = new GameObject("TestBootstrap");
            // Don't set _persistentRoot — Awake will use transform as fallback.
            var controller = go.AddComponent<BootstrapController>();

            // Verify the persistent root field was set to the controller's own transform.
            var field = typeof(BootstrapController)
                .GetField("_persistentRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            var persistentRoot = field?.GetValue(controller) as Transform;

            Assert.AreSame(go.transform, persistentRoot);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Awake_WithPersistentRoot_UsesAssigned()
        {
            var rootGo = new GameObject("PersistentRoot");
            var go = new GameObject("TestBootstrap");

            // Set the persistent root via serialized field before Awake.
            // We need to set it before AddComponent, but AddComponent calls Awake immediately.
            // Instead, we create the component on a disabled GO, set the field, then enable.
            go.SetActive(false);
            var controller = go.AddComponent<BootstrapController>();

            var field = typeof(BootstrapController)
                .GetField("_persistentRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(controller, rootGo.transform);

            go.SetActive(true); // Triggers Awake.

            var persistentRoot = field?.GetValue(controller) as Transform;
            Assert.AreSame(rootGo.transform, persistentRoot);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(rootGo);
        }

        #endregion

        #region Awake — Re-entry Guard

        [Test]
        public void Awake_WhenAlreadyBootstrapped_DestroysGameObject()
        {
            SetHasBootstrapped(true);

            var go = new GameObject("TestBootstrapDuplicate");
            go.AddComponent<BootstrapController>();

            // The Awake should have scheduled destruction. In edit mode, we need DestroyImmediate
            // but the controller uses Destroy (deferred). Check the object is still valid but
            // verify the guard path was taken by confirming _persistentRoot was NOT set up.
            var field = typeof(BootstrapController)
                .GetField("_persistentRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            var controller = go.GetComponent<BootstrapController>();

            // In the duplicate path, _persistentRoot is never assigned because
            // SetupPersistentRoot() is skipped. The field stays at its default (null)
            // because Destroy(gameObject) is called before SetupPersistentRoot.
            var persistentRoot = field?.GetValue(controller) as Transform;
            Assert.IsNull(persistentRoot);

            Object.DestroyImmediate(go);
        }

        #endregion

        #region ConfigurePlatform

        [Test]
        public void ConfigurePlatform_NullConfig_SetsDefaultFrameRate()
        {
            var go = new GameObject("TestBootstrap");
            go.SetActive(false);

            var controller = go.AddComponent<BootstrapController>();

            // Ensure _config is null (default).
            var configField = typeof(BootstrapController)
                .GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
            configField?.SetValue(controller, null);

            go.SetActive(true); // Triggers Awake -> ConfigurePlatform.

            Assert.AreEqual(60, Application.targetFrameRate);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ConfigurePlatform_WithConfig_AppliesTargetFrameRate()
        {
            var config = ScriptableObject.CreateInstance<BootstrapConfigSO>();
            var so = new UnityEditor.SerializedObject(config);
            so.FindProperty("_targetFrameRate").intValue = 120;
            so.ApplyModifiedPropertiesWithoutUndo();

            var go = new GameObject("TestBootstrap");
            go.SetActive(false);

            var controller = go.AddComponent<BootstrapController>();

            var configField = typeof(BootstrapController)
                .GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
            configField?.SetValue(controller, config);

            go.SetActive(true);

            Assert.AreEqual(120, Application.targetFrameRate);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void ConfigurePlatform_WithConfig_AppliesVSyncCount()
        {
            var config = ScriptableObject.CreateInstance<BootstrapConfigSO>();
            var so = new UnityEditor.SerializedObject(config);
            so.FindProperty("_vSyncCount").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();

            var go = new GameObject("TestBootstrap");
            go.SetActive(false);

            var controller = go.AddComponent<BootstrapController>();

            var configField = typeof(BootstrapController)
                .GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);
            configField?.SetValue(controller, config);

            go.SetActive(true);

            Assert.AreEqual(1, QualitySettings.vSyncCount);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(config);
        }

        #endregion

        #region Static Events

        [Test]
        public void OnBootstrapComplete_CanSubscribeWithoutError()
        {
            bool fired = false;
            BootstrapController.OnBootstrapComplete += () => fired = true;

            // Just verifying subscription works — the event won't fire without the full async flow.
            Assert.IsFalse(fired);

            // Clean up.
            BootstrapController.OnBootstrapComplete -= () => fired = true;
        }

        [Test]
        public void OnBootstrapFailed_CanSubscribeWithoutError()
        {
            string errorMsg = null;
            BootstrapController.OnBootstrapFailed += msg => errorMsg = msg;

            Assert.IsNull(errorMsg);

            BootstrapController.OnBootstrapFailed -= msg => errorMsg = msg;
        }

        #endregion

        #region Bootstrap Services List

        [Test]
        public void BootstrapServices_DefaultsToEmptyList()
        {
            var go = new GameObject("TestBootstrap");
            go.SetActive(false);
            var controller = go.AddComponent<BootstrapController>();

            var field = typeof(BootstrapController)
                .GetField("_bootstrapServices", BindingFlags.Instance | BindingFlags.NonPublic);
            var services = field?.GetValue(controller) as System.Collections.Generic.List<MonoBehaviour>;

            Assert.IsNotNull(services);
            Assert.AreEqual(0, services.Count);

            Object.DestroyImmediate(go);
        }

        #endregion
    }
}
