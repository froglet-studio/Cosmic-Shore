using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Swordfish's sword is its bill (Garrett, 2026-11-08): at rest it points ahead and the camera has no offset;
    /// one trigger lays it out to that side and the camera pans along x to centre it; both triggers stand it straight
    /// up and the camera pans along y only. <see cref="ShieldSwipeActionExecutor.BillChannels"/>,
    /// <see cref="SwordfishSwordCamera.PanFor"/> and <see cref="CustomCameraController.FramingPan"/>.
    /// </summary>
    [TestFixture]
    public class SwordfishBillPoseTests
    {
        const float Side = 90f, Roll = 0f, Raise = 90f;

        // The prefab's rest: the blade's local +Y (its length) along the nose, its wide local Z level to the right.
        static readonly Quaternion Rest = new(0.5f, 0.5f, 0.5f, 0.5f);

        static Vector3 BladeDirection(SwordPoseChannels c)
        {
            var sweep = Quaternion.AngleAxis(c.yaw, Vector3.up)
                      * Quaternion.AngleAxis(c.roll, Vector3.forward)
                      * Quaternion.AngleAxis(c.pitch, Vector3.right);
            return sweep * Rest * Vector3.up;
        }

        static SwordPoseChannels Pose(float lt, float rt) =>
            ShieldSwipeActionExecutor.BillChannels(rt - lt, rt + lt, Side, Roll, Raise);

        [Test]
        public void Rest_PointsStraightAhead_FlatSideLevel()
        {
            Assert.Greater(Vector3.Dot(Rest * Vector3.up, Vector3.forward), 0.9999f, "the bill points along the nose");
            Assert.Greater(Vector3.Dot(Rest * Vector3.forward, Vector3.right), 0.9999f, "the bill's wide side lies level");
            var c = Pose(0f, 0f);
            Assert.AreEqual(0f, c.yaw, 1e-5f);
            Assert.AreEqual(0f, c.pitch, 1e-5f);
        }

        [Test]
        public void OneTrigger_LaysTheSwordOutToThatSide_WithoutRaisingIt()
        {
            Assert.Greater(Vector3.Dot(BladeDirection(Pose(0f, 1f)), Vector3.right), 0.9999f, "right trigger: out to the right");
            Assert.Greater(Vector3.Dot(BladeDirection(Pose(1f, 0f)), Vector3.left), 0.9999f, "left trigger: out to the left");
            Assert.AreEqual(0f, Pose(0f, 1f).pitch, 1e-5f, "a single trigger never lifts the sword");
        }

        [Test]
        public void BothTriggers_StandTheSwordStraightUp()
        {
            Assert.Greater(Vector3.Dot(BladeDirection(Pose(1f, 1f)), Vector3.up), 0.9999f);
        }

        [Test]
        public void TheRaise_IsTheSharedPartOfThePull()
        {
            // right full, left half: half the difference sweeps, the shared half raises
            var c = Pose(0.5f, 1f);
            Assert.AreEqual(0.5f * Side, c.yaw, 1e-4f);
            Assert.AreEqual(-0.5f * Raise, c.pitch, 1e-4f);
        }

        [Test]
        public void CameraPan_CentresTheSword_XForTheSide_YForUp_NoneAtRest()
        {
            const float half = 24f;   // the blade's half length at rest (scale.y 24, hilt anchored)
            Vector3 restMid = Vector3.forward * half;
            Vector3 sideMid = Vector3.right * half, upMid = Vector3.up * half;

            Assert.AreEqual(Vector3.zero, SwordfishSwordCamera.PanFor(restMid - restMid, 1f), "home: no x, no y offset");

            var side = SwordfishSwordCamera.PanFor(sideMid - restMid, 1f);
            Assert.AreEqual(half, side.x, 1e-4f, "one trigger: an x pan of the sword's midpoint");
            Assert.AreEqual(0f, side.y, 1e-4f);
            Assert.AreEqual(0f, side.z, 1e-4f, "never a z: the camera already pulls back with the blade");

            var up = SwordfishSwordCamera.PanFor(upMid - restMid, 1f);
            Assert.AreEqual(0f, up.x, 1e-4f, "both triggers: no x pan");
            Assert.AreEqual(half, up.y, 1e-4f, "both triggers: a y pan only");
        }

        GameObject _cameraGo, _shipGo;

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_shipGo != null) Object.DestroyImmediate(_shipGo);
        }

        [Test]
        public void FramingPan_MovesTheCameraAndItsLookPointTogether_InTheHullsFrame()
        {
            _cameraGo = new GameObject("SwordfishPanCamera", typeof(Camera));
            _shipGo = new GameObject("SwordfishPanShip");
            var cam = _cameraGo.AddComponent<CustomCameraController>();
            var settings = ScriptableObject.CreateInstance<CameraSettingsSO>();
            settings.mode = CameraMode.FixedCamera;
            settings.followOffset = new Vector3(0f, 0f, -70f);
            cam.ApplySettings(settings);
            cam.SetFollowTarget(_shipGo.transform);
            var ship = _shipGo.transform;
            ship.SetPositionAndRotation(new Vector3(10f, 4f, -3f), Quaternion.Euler(0f, 90f, 0f));

            cam.FramingPan = new Vector3(24f, 0f, 0f);
            cam.SnapToTarget();
            Vector3 panned = ship.position + ship.right * 24f;
            Assert.Less(Vector3.Distance(cam.transform.position, panned + ship.forward * -70f), 1e-3f, "the camera moves with the pan");
            Assert.Greater(Vector3.Dot(cam.transform.forward, ship.forward), 0.9999f, "and keeps looking the way the hull does");

            cam.FramingPan = Vector3.zero;
            cam.SnapToTarget();
            Assert.Less(Vector3.Distance(cam.transform.position, ship.position + ship.forward * -70f), 1e-3f,
                "zero pan leaves the camera exactly where it always was");
        }
    }
}
