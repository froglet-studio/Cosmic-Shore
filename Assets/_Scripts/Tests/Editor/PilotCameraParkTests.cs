#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pilot's camera put to sleep under a full-screen seat view on the lowest preset (<see cref="CrewSingleView"/>,
    /// <see cref="PilotCameraPark"/>): it draws nothing while parked, gives everything back on release, and a seat that
    /// copies the pilot's look still reads the pilot's own values while it is parked.
    /// </summary>
    public class PilotCameraParkTests
    {
        Camera _cam;

        [SetUp]
        public void SetUp()
        {
            _cam = new GameObject("PilotCameraParkTests").AddComponent<Camera>();
            _cam.cullingMask = 0b1011;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.backgroundColor = Color.cyan;
            var data = _cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.renderShadows = true;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cam.gameObject);

        [Test]
        public void Park_DrawsNothing_AndRelease_GivesItAllBack()
        {
            var park = new PilotCameraPark();
            park.Park(_cam, Color.black);
            var data = _cam.GetUniversalAdditionalCameraData();
            Assert.IsTrue(park.Parked);
            Assert.AreEqual(0, _cam.cullingMask, "no layer drawn");
            Assert.AreEqual(CameraClearFlags.SolidColor, _cam.clearFlags);
            Assert.AreEqual(Color.black, _cam.backgroundColor);
            Assert.IsFalse(data.renderPostProcessing, "no post-processing");
            Assert.IsFalse(data.renderShadows, "no shadows");
            Assert.IsTrue(_cam.enabled, "stays enabled, so Camera.main keeps working");

            park.Release();
            Assert.IsFalse(park.Parked);
            Assert.AreEqual(0b1011, _cam.cullingMask);
            Assert.AreEqual(CameraClearFlags.Skybox, _cam.clearFlags);
            Assert.AreEqual(Color.cyan, _cam.backgroundColor);
            Assert.IsTrue(data.renderPostProcessing);
            Assert.IsTrue(data.renderShadows);
        }

        [Test]
        public void WhileParked_TheSeatReadsThePilotsOwnLook()
        {
            var park = new PilotCameraPark();
            park.Park(_cam, Color.black);
            var data = _cam.GetUniversalAdditionalCameraData();
            Assert.AreEqual(0b1011, park.CullingMask(_cam));
            Assert.AreEqual(CameraClearFlags.Skybox, park.ClearFlags(_cam));
            Assert.AreEqual(Color.cyan, park.Background(_cam));
            Assert.IsTrue(park.PostProcessing(_cam, data));
            Assert.IsTrue(park.Shadows(_cam, data));
            park.Release();
        }

        [Test]
        public void ParkingTwice_KeepsTheFirstSavedLook()
        {
            var park = new PilotCameraPark();
            park.Park(_cam, Color.black);
            park.Park(_cam, Color.red);   // every frame parks again: the parked values must not be saved over the real ones
            park.Release();
            Assert.AreEqual(0b1011, _cam.cullingMask);
            Assert.AreEqual(CameraClearFlags.Skybox, _cam.clearFlags);
        }
    }
}
#endif
