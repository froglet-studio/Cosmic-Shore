using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Core
{
    [TestFixture]
    public class BootstrapConfigSOTests
    {
        BootstrapConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<BootstrapConfigSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void DefaultFirstSceneName_IsAuthentication()
        {
            Assert.AreEqual("Authentication", _config.FirstSceneName);
        }

        [Test]
        public void DefaultMainMenuSceneName_IsMenuMain()
        {
            Assert.AreEqual("Menu_Main", _config.MainMenuSceneName);
        }

        [Test]
        public void DefaultServiceInitTimeout_Is15Seconds()
        {
            Assert.AreEqual(15f, _config.ServiceInitTimeoutSeconds);
        }

        [Test]
        public void DefaultMinimumSplashDuration_Is1Second()
        {
            Assert.AreEqual(1f, _config.MinimumSplashDuration);
        }

        [Test]
        public void DefaultTargetFrameRate_Is60()
        {
            Assert.AreEqual(60, _config.TargetFrameRate);
        }

        [Test]
        public void DefaultPreventScreenSleep_IsTrue()
        {
            Assert.IsTrue(_config.PreventScreenSleep);
        }

        [Test]
        public void DefaultVSyncCount_IsZero()
        {
            Assert.AreEqual(0, _config.VSyncCount);
        }

        [Test]
        public void DefaultVerboseLogging_IsFalse()
        {
            Assert.IsFalse(_config.VerboseLogging);
        }

        [Test]
        public void CreateInstance_ReturnsNonNull()
        {
            Assert.IsNotNull(_config);
        }

        [Test]
        public void CreateInstance_IsBootstrapConfigSO()
        {
            Assert.IsInstanceOf<BootstrapConfigSO>(_config);
        }

        [Test]
        public void SetFieldsViaSerializedObject_ReturnsUpdatedValues()
        {
            var so = new UnityEditor.SerializedObject(_config);

            so.FindProperty("_firstSceneName").stringValue = "TestScene";
            so.FindProperty("_mainMenuSceneName").stringValue = "TestMenu";
            so.FindProperty("_serviceInitTimeoutSeconds").floatValue = 30f;
            so.FindProperty("_minimumSplashDuration").floatValue = 2.5f;
            so.FindProperty("_targetFrameRate").intValue = 120;
            so.FindProperty("_preventScreenSleep").boolValue = false;
            so.FindProperty("_vSyncCount").intValue = 1;
            so.FindProperty("_verboseLogging").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("TestScene", _config.FirstSceneName);
            Assert.AreEqual("TestMenu", _config.MainMenuSceneName);
            Assert.AreEqual(30f, _config.ServiceInitTimeoutSeconds);
            Assert.AreEqual(2.5f, _config.MinimumSplashDuration);
            Assert.AreEqual(120, _config.TargetFrameRate);
            Assert.IsFalse(_config.PreventScreenSleep);
            Assert.AreEqual(1, _config.VSyncCount);
            Assert.IsTrue(_config.VerboseLogging);
        }
    }
}
