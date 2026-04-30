using NUnit.Framework;
using UnityEngine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// CameraSettingsSO Tests — Validates per-vessel camera configuration defaults.
    ///
    /// WHY THIS MATTERS:
    /// Each vessel type has its own CameraSettingsSO asset that controls camera distance,
    /// smoothing, clip planes, and zoom behavior. If default values drift (e.g., someone
    /// changes the class and forgets to update assets), the camera could clip through
    /// geometry, zoom to zero, or feel unresponsive during the GDC demo. These tests
    /// lock the defaults and verify the ScriptableObject instantiation works.
    /// </summary>
    [TestFixture]
    public class CameraSettingsSOTests
    {
        CameraSettingsSO _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<CameraSettingsSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        #region Default Values

        [Test]
        public void Default_Mode_IsFixedCamera()
        {
            Assert.AreEqual(CameraMode.FixedCamera, _settings.mode,
                "Default camera mode should be FixedCamera.");
        }

        [Test]
        public void Default_FollowOffset_HasReasonableValues()
        {
            // Default: (0, 10, -20) — camera behind and above the vessel.
            Assert.AreEqual(0f, _settings.followOffset.x, 0.001f);
            Assert.AreEqual(10f, _settings.followOffset.y, 0.001f);
            Assert.AreEqual(-20f, _settings.followOffset.z, 0.001f);
        }

        [Test]
        public void Default_DynamicMinDistance_IsReasonable()
        {
            Assert.AreEqual(10f, _settings.dynamicMinDistance,
                "Default min camera distance should be 10.");
        }

        [Test]
        public void Default_DynamicMaxDistance_IsGreaterThanMin()
        {
            Assert.Greater(_settings.dynamicMaxDistance, _settings.dynamicMinDistance,
                "Max camera distance must be greater than min distance.");
        }

        [Test]
        public void Default_FollowSmoothTime_IsPositive()
        {
            Assert.Greater(_settings.followSmoothTime, 0f,
                "Follow smooth time must be positive for smooth camera movement.");
        }

        [Test]
        public void Default_RotationSmoothTime_IsPositive()
        {
            Assert.Greater(_settings.rotationSmoothTime, 0f,
                "Rotation smooth time must be positive.");
        }

        [Test]
        public void Default_DisableSmoothing_IsFalse()
        {
            Assert.IsFalse(_settings.disableSmoothing,
                "Smoothing should be enabled by default.");
        }

        [Test]
        public void Default_NearClipPlane_IsPositive()
        {
            Assert.Greater(_settings.nearClipPlane, 0f,
                "Near clip plane must be positive to prevent rendering artifacts.");
        }

        [Test]
        public void Default_FarClipPlane_IsGreaterThanNear()
        {
            Assert.Greater(_settings.farClipPlane, _settings.nearClipPlane,
                "Far clip plane must be greater than near clip plane.");
        }

        [Test]
        public void Default_EnableAdaptiveZoom_IsFalse()
        {
            Assert.IsFalse(_settings.enableAdaptiveZoom,
                "Adaptive zoom should be off by default.");
        }

        [Test]
        public void Default_OrthographicSize_IsPositive()
        {
            Assert.Greater(_settings.orthographicSize, 0f,
                "Orthographic size must be positive.");
        }

        #endregion

        #region CameraMode Enum

        [Test]
        public void CameraMode_HasThreeModes()
        {
            var values = System.Enum.GetValues(typeof(CameraMode));
            Assert.AreEqual(3, values.Length,
                "CameraMode should have exactly 3 modes: Fixed, Dynamic, Orthographic.");
        }

        [Test]
        public void CameraMode_FixedCamera_IsFirst()
        {
            Assert.AreEqual(0, (int)CameraMode.FixedCamera);
        }

        [Test]
        public void CameraMode_DynamicCamera_IsSecond()
        {
            Assert.AreEqual(1, (int)CameraMode.DynamicCamera);
        }

        [Test]
        public void CameraMode_Orthographic_IsThird()
        {
            Assert.AreEqual(2, (int)CameraMode.Orthographic);
        }

        #endregion

        #region Field Modification

        [Test]
        public void Fields_CanBeModifiedAtRuntime()
        {
            _settings.mode = CameraMode.DynamicCamera;
            _settings.dynamicMinDistance = 5f;
            _settings.dynamicMaxDistance = 100f;
            _settings.followSmoothTime = 0.5f;

            Assert.AreEqual(CameraMode.DynamicCamera, _settings.mode);
            Assert.AreEqual(5f, _settings.dynamicMinDistance);
            Assert.AreEqual(100f, _settings.dynamicMaxDistance);
            Assert.AreEqual(0.5f, _settings.followSmoothTime);
        }

        [Test]
        public void ClipPlanes_Configuration_IsValid()
        {
            // Verify the default clip plane relationship
            _settings.nearClipPlane = 0.1f;
            _settings.farClipPlane = 5000f;

            Assert.Greater(_settings.farClipPlane, _settings.nearClipPlane);
            Assert.Greater(_settings.farClipPlane / _settings.nearClipPlane, 1f,
                "Far/near ratio determines depth buffer precision.");
        }

        #endregion

        #region ScriptableObject Lifecycle

        [Test]
        public void CreateInstance_ReturnsValidInstance()
        {
            Assert.IsNotNull(_settings);
            Assert.IsInstanceOf<CameraSettingsSO>(_settings);
            Assert.IsInstanceOf<ScriptableObject>(_settings);
        }

        #endregion
    }
}
