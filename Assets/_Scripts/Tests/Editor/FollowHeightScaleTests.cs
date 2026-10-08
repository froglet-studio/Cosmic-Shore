using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <c>CustomCameraController.FollowHeightScale</c> — the Butterfly's Mass mode drops the camera
    /// to directly behind the hull by scaling the follow offset's HEIGHT at the point of use
    /// (<c>R_VesselActions/BUTTERFLY.md</c> §2.0). Exercised against a live controller, the same
    /// rig <see cref="RearViewLawTests"/> uses.
    /// </summary>
    [TestFixture]
    public class FollowHeightScaleTests
    {
        static readonly Vector3 ButterflyOffset = new(0f, 37.4f, -204f);

        GameObject _cameraGo;
        GameObject _shipGo;
        GameObject _otherShipGo;

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_shipGo != null) Object.DestroyImmediate(_shipGo);
            if (_otherShipGo != null) Object.DestroyImmediate(_otherShipGo);
        }

        (CustomCameraController cam, Transform ship, CameraSettingsSO settings) Rig(Vector3 followOffset)
        {
            _cameraGo = new GameObject("FollowHeightTestCamera", typeof(Camera));
            _shipGo = new GameObject("FollowHeightTestShip");

            var settings = ScriptableObject.CreateInstance<CameraSettingsSO>();
            settings.mode = CameraMode.FixedCamera;
            settings.followOffset = followOffset;

            var cam = _cameraGo.AddComponent<CustomCameraController>();
            cam.ApplySettings(settings);
            cam.SetFollowTarget(_shipGo.transform);
            return (cam, _shipGo.transform, settings);
        }

        [Test]
        public void ZeroHeightSitsDirectlyBehindAtTheSameDistance()
        {
            var (cam, ship, _) = Rig(ButterflyOffset);
            ship.SetPositionAndRotation(new Vector3(40f, -12f, 7f), Quaternion.Euler(20f, 75f, -10f));

            cam.FollowHeightScale = 0f;
            cam.SnapToTarget();

            Vector3 expected = ship.position + ship.rotation * new Vector3(0f, 0f, ButterflyOffset.z);
            Assert.Less(Vector3.Distance(cam.transform.position, expected), 1e-3f,
                "Height 0 must put the camera on the ship's own back axis, the authored distance away.");
            Assert.Greater(Vector3.Dot(cam.transform.forward, ship.forward), 0.999f,
                "Directly behind, the camera must look straight down the ship's forward axis.");
        }

        [Test]
        public void DefaultIsTheAuthoredOffset()
        {
            var (cam, ship, _) = Rig(ButterflyOffset);
            ship.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            cam.SnapToTarget();

            Assert.AreEqual(1f, cam.FollowHeightScale, "A fresh rig keeps its full height.");
            Assert.Less(Vector3.Distance(cam.transform.position, ButterflyOffset), 1e-3f,
                "With the scale untouched the camera must pose exactly where it always did.");
        }

        [Test]
        public void ScaleIsAppliedAtThePointOfUseAndSurvivesReappliedSettings()
        {
            var (cam, ship, settings) = Rig(ButterflyOffset);
            ship.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            cam.FollowHeightScale = 0f;
            cam.ApplySettings(settings);   // a vessel swap / settings re-apply mid-Mass-mode
            cam.SnapToTarget();

            Assert.AreEqual(0f, cam.transform.position.y, 1e-3f,
                "Re-applying settings must not put the height back; the scale lives at the point of use.");
            Assert.AreEqual(ButterflyOffset, cam.GetFollowOffset(),
                "The authored offset itself must be untouched by the height scale.");
        }

        [Test]
        public void ComposesWithTheRearView()
        {
            var (cam, ship, _) = Rig(ButterflyOffset);
            ship.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            cam.FollowHeightScale = 0f;
            cam.RearView = true;
            cam.SnapToTarget();

            Assert.AreEqual(0f, cam.transform.position.y, 1e-3f, "The look-back keeps the scaled height.");
            Assert.AreEqual(-ButterflyOffset.z, cam.transform.position.z, 1e-3f, "The look-back still mirrors z.");
        }

        [Test]
        public void ANewFollowTargetResetsTheHeight()
        {
            var (cam, _, _) = Rig(ButterflyOffset);
            _otherShipGo = new GameObject("FollowHeightTestOtherShip");

            cam.FollowHeightScale = 0f;
            cam.SetFollowTarget(_shipGo.transform);
            Assert.AreEqual(0f, cam.FollowHeightScale, "Re-handing the SAME target must keep the scale.");

            cam.SetFollowTarget(_otherShipGo.transform);
            Assert.AreEqual(1f, cam.FollowHeightScale,
                "A height belongs to the ship it was set for; a different target starts at full height.");
        }

        [Test]
        public void ScaleIsClampedToUnitRange()
        {
            var (cam, _, _) = Rig(ButterflyOffset);

            cam.FollowHeightScale = -2f;
            Assert.AreEqual(0f, cam.FollowHeightScale);
            cam.FollowHeightScale = 5f;
            Assert.AreEqual(1f, cam.FollowHeightScale);
        }
    }
}
