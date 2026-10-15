using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <c>CameraSettingsSO.lookAheadDistance</c> / <c>lookAheadLift</c> — the Stoat's chase camera, ported from
    /// its studio (above and behind, looking 40 u past the nose). Both are 0 on every
    /// other vessel, which must leave the camera exactly where it always was. Same live rig as
    /// <see cref="FollowHeightScaleTests"/>.
    /// </summary>
    [TestFixture]
    public class CameraLookAheadTests
    {
        static readonly Vector3 StoatOffset = new(0f, 6.5f, -21f);

        GameObject _cameraGo;
        GameObject _shipGo;

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_shipGo != null) Object.DestroyImmediate(_shipGo);
        }

        (CustomCameraController cam, Transform ship) Rig(CameraSettingsSO settings)
        {
            _cameraGo = new GameObject("LookAheadTestCamera", typeof(Camera));
            _shipGo = new GameObject("LookAheadTestShip");
            var cam = _cameraGo.AddComponent<CustomCameraController>();
            cam.ApplySettings(settings);
            cam.SetFollowTarget(_shipGo.transform);
            return (cam, _shipGo.transform);
        }

        static CameraSettingsSO Settings(float lookAhead, float lift)
        {
            var s = ScriptableObject.CreateInstance<CameraSettingsSO>();
            s.mode = CameraMode.FixedCamera;
            s.followOffset = StoatOffset;
            s.lookAheadDistance = lookAhead;
            s.lookAheadLift = lift;
            return s;
        }

        [Test]
        public void LooksPastTheNose_FromTheAuthoredOffset()
        {
            var (cam, ship) = Rig(Settings(40f, 3f));
            ship.SetPositionAndRotation(new Vector3(30f, -8f, 12f), Quaternion.Euler(15f, 60f, -20f));
            cam.SnapToTarget();

            Assert.Less(Vector3.Distance(cam.transform.position, ship.position + ship.rotation * StoatOffset), 1e-3f,
                "The look-ahead must not move the camera, only where it looks.");
            Vector3 look = ship.position + ship.forward * 40f + ship.up * 3f;
            Assert.Greater(Vector3.Dot(cam.transform.forward, (look - cam.transform.position).normalized), 0.9999f,
                "The camera must look at the point 40 u past the nose and 3 u above it.");
        }

        [Test]
        public void ZeroLookAhead_LooksAtTheHull()
        {
            var (cam, ship) = Rig(Settings(0f, 0f));
            ship.SetPositionAndRotation(new Vector3(-5f, 2f, 9f), Quaternion.Euler(-30f, 10f, 45f));
            cam.SnapToTarget();

            Assert.Greater(Vector3.Dot(cam.transform.forward, (ship.position - cam.transform.position).normalized), 0.9999f,
                "A vessel that sets no look-ahead keeps the camera aimed at the hull, as it always was.");
        }

        [Test]
        public void FramedLook_AtScaleOne_IsTheAuthoredLookPoint()
        {
            var look = new Vector3(0f, 3f, 40f);
            Assert.Less(Vector3.Distance(CustomCameraController.FramedLook(StoatOffset, look, 1f), look), 1e-3f);
        }

        /// <summary>The studio frames at 68 degrees; the game camera is the player's (90 by default). At the framed
        /// look the hull must sit where it sat on the studio's screen, and read the same size.</summary>
        [Test]
        public void FramedLook_KeepsTheHullsPlaceOnScreen_AtThePlayersFieldOfView()
        {
            var look = new Vector3(0f, 3f, 40f);
            float framing = 68f, player = 90f;
            float s = Mathf.Tan(framing * 0.5f * Mathf.Deg2Rad) / Mathf.Tan(player * 0.5f * Mathf.Deg2Rad);
            Vector3 cam = StoatOffset * s, framed = CustomCameraController.FramedLook(StoatOffset, look, s);

            float studioAngle = Vector3.Angle(-StoatOffset, look - StoatOffset) * Mathf.Deg2Rad;
            float gameAngle = Vector3.Angle(-cam, framed - cam) * Mathf.Deg2Rad;
            float studioScreen = Mathf.Tan(studioAngle) / Mathf.Tan(framing * 0.5f * Mathf.Deg2Rad);
            float gameScreen = Mathf.Tan(gameAngle) / Mathf.Tan(player * 0.5f * Mathf.Deg2Rad);
            Assert.AreEqual(studioScreen, gameScreen, 1e-3f, "the hull sits at the same height on screen");

            float studioSize = 1f / (StoatOffset.magnitude * Mathf.Tan(framing * 0.5f * Mathf.Deg2Rad));
            float gameSize = 1f / (cam.magnitude * Mathf.Tan(player * 0.5f * Mathf.Deg2Rad));
            Assert.AreEqual(studioSize, gameSize, 1e-4f, "the hull reads the same size on screen");
        }

        [Test]
        public void FramingFieldOfView_MovesTheCameraNearer_AtAWiderPlayerView()
        {
            var settings = Settings(40f, 3f);
            settings.framingFieldOfView = 68f;
            var (cam, ship) = Rig(settings);
            cam.Camera.fieldOfView = 90f;
            ship.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            cam.SnapToTarget();
            float s = Mathf.Tan(34f * Mathf.Deg2Rad) / Mathf.Tan(45f * Mathf.Deg2Rad);
            Assert.Less(Vector3.Distance(cam.transform.position, StoatOffset * s), 1e-3f);
        }
    }
}
