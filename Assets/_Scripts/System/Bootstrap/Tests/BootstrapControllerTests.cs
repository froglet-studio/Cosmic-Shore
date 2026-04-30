using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

namespace CosmicShore.Core
{
    [TestFixture]
    public class AppManagerBootstrapTests
    {
        [SetUp]
        public void SetUp()
        {
            // Reset the static _hasBootstrapped flag before each test.
            ResetHasBootstrapped();
        }

        [TearDown]
        public void TearDown()
        {
            ResetHasBootstrapped();
        }

        static void ResetHasBootstrapped()
        {
            var field = typeof(AppManager)
                .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, false);
        }

        static void SetHasBootstrapped(bool value)
        {
            var field = typeof(AppManager)
                .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, value);
        }

        #region HasBootstrapped

        [Test]
        public void HasBootstrapped_InitiallyFalse()
        {
            Assert.IsFalse(AppManager.HasBootstrapped);
        }

        [Test]
        public void HasBootstrapped_ReturnsTrueWhenSet()
        {
            SetHasBootstrapped(true);

            Assert.IsTrue(AppManager.HasBootstrapped);
        }

        #endregion

        #region EnsureBootstrapOnStartup

        [Test]
        public void EnsureBootstrapOnStartup_ResetsHasBootstrapped()
        {
            SetHasBootstrapped(true);

            // Invoke the static initializer method.
            var method = typeof(AppManager)
                .GetMethod("EnsureBootstrapOnStartup", BindingFlags.Static | BindingFlags.NonPublic);
            method?.Invoke(null, null);

            Assert.IsFalse(AppManager.HasBootstrapped);
        }

        #endregion

        #region Awake — Persistent Root

        [Test]
        public void Awake_NoPersistentRoot_UsesSelf()
        {
            var go = new GameObject("TestAppManager");
            // Don't set _persistentRoot — Awake will use transform as fallback.
            var manager = go.AddComponent<AppManager>();

            // Verify the persistent root field was set to the manager's own transform.
            var field = typeof(AppManager)
                .GetField("_persistentRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            var persistentRoot = field?.GetValue(manager) as Transform;

            Assert.AreSame(go.transform, persistentRoot);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Awake_WithPersistentRoot_UsesAssigned()
        {
            var rootGo = new GameObject("PersistentRoot");
            var go = new GameObject("TestAppManager");

            // Set the persistent root via serialized field before Awake.
            // We need to set it before AddComponent, but AddComponent calls Awake immediately.
            // Instead, we create the component on a disabled GO, set the field, then enable.
            go.SetActive(false);
            var manager = go.AddComponent<AppManager>();

            var field = typeof(AppManager)
                .GetField("_persistentRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(manager, rootGo.transform);

            go.SetActive(true); // Triggers Awake.

            var persistentRoot = field?.GetValue(manager) as Transform;
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

            var go = new GameObject("TestAppManagerDuplicate");
            go.AddComponent<AppManager>();

            // The Awake should have scheduled destruction. In edit mode, we need DestroyImmediate
            // but the manager uses Destroy (deferred). Check the object is still valid but
            // verify the guard path was taken by confirming _persistentRoot was NOT set up.
            var field = typeof(AppManager)
                .GetField("_persistentRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            var manager = go.GetComponent<AppManager>();

            // In the duplicate path, _persistentRoot is never assigned because
            // SetupPersistentRoot() is skipped. The field stays at its default (null)
            // because Destroy(gameObject) is called before SetupPersistentRoot.
            var persistentRoot = field?.GetValue(manager) as Transform;
            Assert.IsNull(persistentRoot);

            Object.DestroyImmediate(go);
        }

        #endregion

        #region ConfigurePlatform

        [Test]
        public void ConfigurePlatform_NullConfig_SetsDefaultFrameRate()
        {
            var go = new GameObject("TestAppManager");
            go.SetActive(false);

            var manager = go.AddComponent<AppManager>();

            // Ensure _bootstrapConfig is null (default).
            var configField = typeof(AppManager)
                .GetField("_bootstrapConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            configField?.SetValue(manager, null);

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

            var go = new GameObject("TestAppManager");
            go.SetActive(false);

            var manager = go.AddComponent<AppManager>();

            var configField = typeof(AppManager)
                .GetField("_bootstrapConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            configField?.SetValue(manager, config);

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

            var go = new GameObject("TestAppManager");
            go.SetActive(false);

            var manager = go.AddComponent<AppManager>();

            var configField = typeof(AppManager)
                .GetField("_bootstrapConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            configField?.SetValue(manager, config);

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
            AppManager.OnBootstrapComplete += () => fired = true;

            // Just verifying subscription works — the event won't fire without the full async flow.
            Assert.IsFalse(fired);

            // Clean up.
            AppManager.OnBootstrapComplete -= () => fired = true;
        }

        [Test]
        public void OnBootstrapFailed_CanSubscribeWithoutError()
        {
            string errorMsg = null;
            AppManager.OnBootstrapFailed += msg => errorMsg = msg;

            Assert.IsNull(errorMsg);

            AppManager.OnBootstrapFailed -= msg => errorMsg = msg;
        }

        #endregion

        #region Bootstrap Services List

        [Test]
        public void BootstrapServices_DefaultsToEmptyList()
        {
            var go = new GameObject("TestAppManager");
            go.SetActive(false);
            var manager = go.AddComponent<AppManager>();

            var field = typeof(AppManager)
                .GetField("_bootstrapServices", BindingFlags.Instance | BindingFlags.NonPublic);
            var services = field?.GetValue(manager) as System.Collections.Generic.List<MonoBehaviour>;

            Assert.IsNotNull(services);
            Assert.AreEqual(0, services.Count);

            Object.DestroyImmediate(go);
        }

        #endregion
    }
}
