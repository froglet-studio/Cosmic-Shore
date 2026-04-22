using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    [TestFixture]
    public class BenchmarkConfigSOTests
    {
        BenchmarkConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<BenchmarkConfigSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        #region Default Values

        [Test]
        public void CreateInstance_ReturnsValidInstance()
        {
            Assert.IsNotNull(_config);
            Assert.IsInstanceOf<BenchmarkConfigSO>(_config);
        }

        [Test]
        public void DefaultWarmupDuration_IsPositive()
        {
            Assert.Greater(_config.WarmupDuration, 0f);
        }

        [Test]
        public void DefaultSampleDuration_IsPositive()
        {
            Assert.Greater(_config.SampleDuration, 0f);
        }

        [Test]
        public void DefaultCaptureRenderingStats_IsTrue()
        {
            Assert.IsTrue(_config.CaptureRenderingStats);
        }

        [Test]
        public void DefaultCaptureMemoryStats_IsTrue()
        {
            Assert.IsTrue(_config.CaptureMemoryStats);
        }

        [Test]
        public void DefaultCapturePhysicsStats_IsTrue()
        {
            Assert.IsTrue(_config.CapturePhysicsStats);
        }

        [Test]
        public void DefaultOutputFolder_IsNotEmpty()
        {
            Assert.IsNotNull(_config.OutputFolder);
            Assert.IsNotEmpty(_config.OutputFolder);
        }

        #endregion

        #region Property Access Via SerializedObject

        [Test]
        public void WarmupDuration_ReflectsSerializedField()
        {
            var so = new UnityEditor.SerializedObject(_config);
            so.FindProperty("warmupDuration").floatValue = 5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(5f, _config.WarmupDuration, 0.001f);
        }

        [Test]
        public void SampleDuration_ReflectsSerializedField()
        {
            var so = new UnityEditor.SerializedObject(_config);
            so.FindProperty("sampleDuration").floatValue = 20f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(20f, _config.SampleDuration, 0.001f);
        }

        [Test]
        public void OutputFolder_ReflectsSerializedField()
        {
            var so = new UnityEditor.SerializedObject(_config);
            so.FindProperty("outputFolder").stringValue = "CustomFolder";
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("CustomFolder", _config.OutputFolder);
        }

        [Test]
        public void BenchmarkLabel_ReflectsSerializedField()
        {
            var so = new UnityEditor.SerializedObject(_config);
            so.FindProperty("benchmarkLabel").stringValue = "GDC_Demo";
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual("GDC_Demo", _config.BenchmarkLabel);
        }

        #endregion
    }
}
