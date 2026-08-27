#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Edit-Mode coverage for <see cref="AppManager"/>.
    ///
    /// NOTE ON LIFECYCLE: Unity does not call <c>Awake</c> outside Play Mode for a MonoBehaviour
    /// that isn't <c>[ExecuteAlways]</c>, so no test here may rely on <c>AddComponent</c> or
    /// <c>SetActive(true)</c> to run bootstrap code. Private methods are invoked explicitly via
    /// <see cref="EditModeLifecycle"/>. <c>AppManager.Awake</c> itself is only invoked in the
    /// duplicate-guard test, and only with the manager-resolution sweep pre-neutralised — the
    /// sweep would otherwise add <c>DontDestroyOnLoad</c> components to whatever scene the
    /// developer has open.
    /// </summary>
    [TestFixture]
    public class AppManagerBootstrapTests
    {
        // Deliberately not 60 (the no-config default) or 120 (the with-config value under test),
        // so a ConfigurePlatform assertion can never pass on ambient editor state.
        const int FrameRateSentinel = 33;

        int _savedTargetFrameRate;
        int _savedVSyncCount;
        int _savedSleepTimeout;

        [SetUp]
        public void SetUp()
        {
            // Reset the static _hasBootstrapped flag before each test.
            ResetHasBootstrapped();

            // These tests write real editor settings. Snapshot them so the suite cannot leave
            // the developer's editor retuned after a run.
            _savedTargetFrameRate = Application.targetFrameRate;
            _savedVSyncCount = QualitySettings.vSyncCount;
            _savedSleepTimeout = Screen.sleepTimeout;

            Application.targetFrameRate = FrameRateSentinel;
            QualitySettings.vSyncCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            ResetHasBootstrapped();

            Application.targetFrameRate = _savedTargetFrameRate;
            QualitySettings.vSyncCount = _savedVSyncCount;
            Screen.sleepTimeout = _savedSleepTimeout;
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

        #region Awake - Re-entry Guard

        [Test]
        public void Awake_WhenAlreadyBootstrapped_SkipsBootstrapWork()
        {
            SetHasBootstrapped(true);

            var go = new GameObject("TestAppManagerDuplicate");
            var manager = go.AddComponent<AppManager>();

            // Pre-set _resolved so that TryResolveManagersEarly is inert even if the guard ever
            // regresses. That sweep does FindAnyObjectByType across the open scene and adds a
            // DontDestroyOnLoad component to everything it finds, so a failing test must not be
            // able to dirty the developer's scene on its way to reporting the failure.
            EditModeLifecycle.SetPrivateField(manager, "_resolved", true);

            // Re-stamped here rather than relied on from SetUp: the editor drives
            // Application.targetFrameRate itself, so a frame boundary between SetUp and this body
            // could replace the sentinel. Written immediately before the invoke, nothing can.
            Application.targetFrameRate = FrameRateSentinel;

            // The guard path calls Destroy(gameObject), which throws in Edit Mode and surfaces
            // through reflection as TargetInvocationException. Swallowed rather than asserted so
            // the test still holds if a future Unity makes edit-mode Destroy a no-op — either
            // way, execution never reaches the work below the guard.
            try { EditModeLifecycle.InvokePrivate(manager, "Awake"); }
            catch (TargetInvocationException) { }

            // ConfigurePlatform is the first observable thing after the guard returns. If it had
            // run with a null config it would have written 60.
            Assert.AreEqual(FrameRateSentinel, Application.targetFrameRate,
                "A duplicate AppManager ran ConfigurePlatform - the _hasBootstrapped re-entry " +
                "guard in Awake did not return early. Two AppManagers bootstrapping is undefined " +
                "behaviour (see Docs BOOTSTRAP_AUDIT.md).");

            Object.DestroyImmediate(go);
        }

        #endregion

        #region ConfigurePlatform

        [Test]
        public void ConfigurePlatform_NullConfig_SetsDefaultFrameRate()
        {
            var go = new GameObject("TestAppManager");
            var manager = go.AddComponent<AppManager>();

            // Ensure _bootstrapConfig is null (default).
            EditModeLifecycle.SetPrivateField(manager, "_bootstrapConfig", null);

            EditModeLifecycle.InvokePrivate(manager, "ConfigurePlatform");

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
            var manager = go.AddComponent<AppManager>();

            EditModeLifecycle.SetPrivateField(manager, "_bootstrapConfig", config);

            EditModeLifecycle.InvokePrivate(manager, "ConfigurePlatform");

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
            var manager = go.AddComponent<AppManager>();

            EditModeLifecycle.SetPrivateField(manager, "_bootstrapConfig", config);

            EditModeLifecycle.InvokePrivate(manager, "ConfigurePlatform");

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

            // Just verifying subscription works - the event won't fire without the full async flow.
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

        #region Serialized Wiring Contract

        /// <summary>
        /// The Bootstrap scene wires these two by name. A rename silently unwires them - the
        /// serialized reference is dropped and nothing reports it, which costs a null config
        /// (60fps defaults, verbose logging) and a null scene list (hardcoded "Authentication"
        /// fallback) at runtime.
        /// </summary>
        [Test]
        public void AppManager_DeclaresBootstrapWiringFields()
        {
            AssertSerializedField("_bootstrapConfig", typeof(BootstrapConfigSO));
            AssertSerializedField("_sceneNames", typeof(CosmicShore.Utility.SceneNameListSO));
        }

        static void AssertSerializedField(string fieldName, System.Type expectedType)
        {
            var field = typeof(AppManager)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field,
                $"AppManager no longer declares '{fieldName}'. The Bootstrap scene wires it by " +
                "name, so a rename drops the serialized reference with nothing reporting it.");
            Assert.AreEqual(expectedType, field.FieldType,
                $"AppManager.{fieldName} changed type - the Bootstrap scene's serialized " +
                "reference will not survive.");
        }

        #endregion

        #region DI Installer Contract

        /// <summary>
        /// AppManager is the Reflex DI root. If it stops being an installer, every
        /// <c>[Inject]</c> in the game resolves against an empty container.
        /// </summary>
        [Test]
        public void AppManager_IsReflexInstaller()
        {
            var installer = typeof(AppManager).GetInterfaces()
                .FirstOrDefault(i => i.Name == "IInstaller");

            Assert.IsNotNull(installer,
                "AppManager must implement Reflex's IInstaller - it is the application's DI root.");

            var install = typeof(AppManager).GetMethod("InstallBindings",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(install,
                "AppManager.InstallBindings is missing - Reflex has nothing to call.");
        }

        #endregion
    }
}
#endif
