using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The look-back view (Docs/REAR_VIEW.md): the gameplay camera flips to the mirror of its own
    /// follow offset — the same distance AHEAD of the vessel that it normally sits behind — on
    /// <c>C</c> or LB+RB, and flips back.
    ///
    /// <para>Two kinds of test, because the invariant has two halves. The GEOMETRY is real
    /// behaviour and is exercised against a live <c>CustomCameraController</c>. The rest are
    /// source-text laws in the shape <c>SpeedTunnelLawTests</c> and
    /// <c>InputDeviceUnificationTests</c> use, because "there is exactly one gesture", "it is
    /// bound where the other camera laws are bound" and "the toggle never writes the offset" are
    /// claims about structure — you cannot detect a second implementation by calling the first,
    /// and the camera and post stack this touches only exist in play mode.</para>
    /// </summary>
    [TestFixture]
    public class RearViewLawTests
    {
        const string GesturePath = "Assets/_Scripts/Controller/IO/RearViewGesture.cs";
        const string DriverPath = "Assets/_Scripts/Utility/VesselRearView.cs";
        const string ControllerPath = "Assets/_Scripts/Controller/IO/InputController.cs";
        const string VesselPath = "Assets/_Scripts/Controller/Vessel/VesselController.cs";
        const string CameraPath = "Assets/_Scripts/Controller/Camera/CustomCameraController.cs";
        const string PipPath = "Assets/_Scripts/Controller/Vessel/Pip.cs";
        const string GameHudPath = "Assets/_Scripts/UI/MiniGameHUD.cs";
        const string SelfPath = "Assets/_Scripts/Tests/Editor/RearViewLawTests.cs";

        /// <summary>Every first-party script except this file, which necessarily quotes the very
        /// symbols the sweeps below forbid.</summary>
        static System.Collections.Generic.IEnumerable<string> ScriptsExcept(params string[] exempt)
        {
            foreach (var raw in Directory.GetFiles("Assets/_Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string path = raw.Replace('\\', '/');
                bool skip = path.EndsWith(SelfPath);
                foreach (var e in exempt) skip |= path.EndsWith(e);
                if (!skip) yield return path;
            }
        }

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            return File.ReadAllText(path);
        }

        /// <summary>Source with // and /* */ comments stripped, so prose ABOUT a rule is never
        /// mistaken for a violation OF it.</summary>
        static string Code(string path)
        {
            string text = Read(path);
            text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            text = Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
            return text;
        }

        // ==================================================================
        // The geometry — the half that is real behaviour

        GameObject _cameraGo;
        GameObject _shipGo;

        [TearDown]
        public void TearDown()
        {
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_shipGo != null) Object.DestroyImmediate(_shipGo);
        }

        (CustomCameraController cam, Transform ship) Rig(Vector3 followOffset)
        {
            _cameraGo = new GameObject("RearViewTestCamera", typeof(Camera));
            _shipGo = new GameObject("RearViewTestShip");

            var settings = ScriptableObject.CreateInstance<CameraSettingsSO>();
            settings.mode = CameraMode.FixedCamera;
            settings.followOffset = followOffset;

            var cam = _cameraGo.AddComponent<CustomCameraController>();
            cam.ApplySettings(settings);
            cam.SetFollowTarget(_shipGo.transform);
            return (cam, _shipGo.transform);
        }

        [Test]
        public void RearViewSitsTheSameDistanceAheadThatTheCameraSatBehind()
        {
            var (cam, ship) = Rig(new Vector3(0f, 0f, -17f)); // the shipped Squirrel offset
            ship.SetPositionAndRotation(new Vector3(120f, -40f, 8f), Quaternion.Euler(15f, 62f, -9f));

            cam.SnapToTarget();
            float forwardDistance = Vector3.Distance(cam.transform.position, ship.position);
            Vector3 forwardPos = cam.transform.position;

            cam.RearView = true;
            cam.SnapToTarget();
            float rearDistance = Vector3.Distance(cam.transform.position, ship.position);

            Assert.AreEqual(forwardDistance, rearDistance, 1e-3f,
                "The rear vantage must be the SAME distance from the ship as the forward one - " +
                "that is the whole of what 'just in front, at the same distance' means.");

            // ...and on the OPPOSITE side, along the ship's own forward axis.
            Assert.Greater(Vector3.Dot(cam.transform.position - ship.position, ship.forward), 0f,
                "The rear-view camera must sit AHEAD of the ship.");
            Assert.Less(Vector3.Dot(forwardPos - ship.position, ship.forward), 0f,
                "The ordinary camera must sit BEHIND the ship (the test rig is wrong otherwise).");
        }

        [Test]
        public void RearViewLooksBackAtTheShip()
        {
            var (cam, ship) = Rig(new Vector3(0f, 0f, -17f));
            ship.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            cam.RearView = true;
            cam.SnapToTarget();

            // Looking back down the ship's forward axis: the pilot sees their own nose against
            // whatever is chasing them.
            Assert.Greater(Vector3.Dot(cam.transform.forward, -ship.forward), 0.999f,
                "The rear-view camera must look BACK along the ship's forward axis.");
        }

        [Test]
        public void RearViewMirrorsZOnlySoHeightAndOffsetAreKept()
        {
            // The Sparrow rides high and behind: (0, 10, -50). Mirroring the whole vector would
            // put the camera UNDER the ship - a vantage no CameraSettingsSO ever described.
            var (cam, ship) = Rig(new Vector3(3f, 10f, -50f));
            ship.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            cam.RearView = true;
            cam.SnapToTarget();

            Assert.AreEqual(3f, cam.transform.position.x, 1e-3f, "x must be preserved.");
            Assert.AreEqual(10f, cam.transform.position.y, 1e-3f, "y must be preserved.");
            Assert.AreEqual(50f, cam.transform.position.z, 1e-3f, "z must be mirrored.");
        }

        [Test]
        public void RearViewFollowsALiveZoomInsteadOfFightingIt()
        {
            // The reason the mirror is applied at the point of use and never written into the
            // offset: the zoom-out abilities and the skimmer's camera-scaling effect keep writing
            // the raw offset while the rear view is up, and the rear view must track them.
            var (cam, ship) = Rig(new Vector3(0f, 0f, -20f));
            ship.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            cam.RearView = true;
            cam.SetCameraDistance(-90f);   // an ability zooming out mid-look-back
            cam.SnapToTarget();

            Assert.AreEqual(90f, cam.transform.position.z, 1e-3f,
                "The rear view must track a live camera-distance change. If this reads 20, the " +
                "mirror has been baked into the offset instead of applied at the point of use.");
            Assert.AreEqual(-90f, cam.GetCameraDistance(), 1e-3f,
                "The authored offset itself must be untouched by the rear view.");
        }

        [Test]
        public void ForwardPoseIsUnchangedByTheFeatureExisting()
        {
            var (cam, ship) = Rig(new Vector3(0f, 0f, -17f));
            ship.SetPositionAndRotation(new Vector3(5f, 5f, 5f), Quaternion.Euler(0f, 90f, 0f));

            cam.SnapToTarget();
            Vector3 expected = ship.position + ship.rotation * new Vector3(0f, 0f, -17f);

            Assert.Less(Vector3.Distance(cam.transform.position, expected), 1e-3f,
                "With RearView off the camera must pose exactly where it always did.");
        }

        // ==================================================================
        // One gesture, in one place

        [Test]
        public void TheGestureIsCOrBothShoulders()
        {
            string code = Code(GesturePath);
            Assert.IsTrue(code.Contains("cKey"), "C must open the look-back view.");
            Assert.IsTrue(code.Contains("leftShoulder") && code.Contains("rightShoulder"),
                "Both pad shoulders must be part of the chord.");
        }

        [Test]
        public void ThePadChordIsARisingEdgeAndCarriesNoState()
        {
            string code = Code(GesturePath);
            Assert.IsTrue(code.Contains("wasPressedThisFrame"),
                "The chord must fire on an EDGE, or holding LB+RB flips the camera every frame.");
            Assert.IsFalse(Regex.IsMatch(code, @"\bstatic\s+(bool|int|float)\s+\w+\s*[;=]"),
                "RearViewGesture must stay stateless: a remembered was-both-held flag survives a " +
                "scene load and an editor play-mode exit, and desynchronises the toggle from " +
                "what the player is actually holding.");
        }

        [Test]
        public void OnlyInputControllerPollsTheGesture()
        {
            // One poller, on the one pump that is already local-pilot and pause gated. A second
            // caller is how the same press comes to toggle twice and appear to do nothing.
            Assert.IsTrue(Code(ControllerPath).Contains("RearViewGesture.RequestedThisFrame"),
                "InputController must poll the shared gesture.");

            foreach (var path in ScriptsExcept(GesturePath, ControllerPath))
            {
                Assert.IsFalse(Code(path).Contains("RearViewGesture."),
                    $"{Path.GetFileName(path)} polls the rear-view gesture. InputController is the " +
                    "only sanctioned poller - it is the one per-frame pump already gated on local " +
                    "human pilot and on both pause gates.");
            }
        }

        [Test]
        public void TheGestureIsPolledBelowTheLocalPilotAndPauseGates()
        {
            string code = Code(ControllerPath);
            int pilotGate = code.IndexOf("IsLocalPilot", System.StringComparison.Ordinal);
            int pauseGate = code.IndexOf("PauseSystem.Paused", System.StringComparison.Ordinal);
            int poll = code.IndexOf("RearViewGesture.RequestedThisFrame", System.StringComparison.Ordinal);

            Assert.Greater(pilotGate, -1);
            Assert.Greater(pauseGate, -1);
            Assert.Greater(poll, pilotGate,
                "An AI hull and a remote replica both carry an InputController; neither may move " +
                "the local player's camera.");
            Assert.Greater(poll, pauseGate,
                "The camera must not be flippable from the overview or a modal.");
        }

        // ==================================================================
        // Bound where the other camera laws are bound

        [Test]
        public void TheRearViewBindsAtBothVesselOwnershipSites()
        {
            string code = Code(VesselPath);

            // Initialize AND ChangePlayer - the latter hands a LIVE vessel to another player and
            // never reaches Initialize (the Cellular Duel round-boundary swap).
            Assert.AreEqual(2, Regex.Matches(code, @"VesselRearView\.SetTarget\(transform\)").Count,
                "VesselRearView must bind in BOTH Initialize and ChangePlayer, under IsLocalPilot - " +
                "the same two sites the occlusion corridor, the speed tunnel and the vision band use.");

            // ChangePlayer's non-local branch AND OnDestroy, both identity-guarded.
            Assert.AreEqual(2, Regex.Matches(code, @"VesselRearView\.ClearTarget\(transform\)").Count,
                "VesselRearView must release on the non-local branch and on destruction, keyed on " +
                "the transform so an outgoing vessel's late teardown cannot cancel the incoming " +
                "vessel's bind.");
        }

        [Test]
        public void TheDriverNeverWritesTheFollowOffset()
        {
            // The composition property, asserted rather than described: writing a mirrored offset
            // would mean the first zoom ability or vessel swap to fire silently drops the pilot
            // out of the rear view with nothing to explain it.
            string code = Code(DriverPath);
            foreach (var forbidden in new[] { "SetFollowOffset", "SetCameraDistance", "ApplySettings" })
                Assert.IsFalse(code.Contains(forbidden),
                    $"VesselRearView calls {forbidden}. The mirror belongs at the point of use " +
                    "(CustomCameraController.EffectiveOffset), never written into the offset.");
        }

        [Test]
        public void OnlyThePlayerCameraIsEverFlipped()
        {
            // The death camera and the end/replay camera are CustomCameraControllers too, so
            // "the active controller" is not a specific enough test: it would mirror a death cam
            // on the frame you die, and re-snap a hand-posed broadcast replay underneath its own
            // framing math.
            string code = Code(DriverPath);
            Assert.IsTrue(code.Contains("GetCloseCamera"),
                "VesselRearView must identify the PLAYER rig specifically, not merely take " +
                "whichever CustomCameraController happens to be active.");
        }

        [Test]
        public void TheDriverCutsToTheNewVantageRatherThanSweepingToIt()
        {
            Assert.IsTrue(Code(DriverPath).Contains("SnapToTarget"),
                "Flipping the vantage must SNAP. The two positions are 2x the follow distance " +
                "apart - 34 units on a Squirrel, 500 on a Serpent - and a dynamic rig would " +
                "SmoothDamp that gap straight through the ship.");
        }

        [Test]
        public void TheCameraPosesFromTheMirroredOffsetAtEveryPoseSite()
        {
            string code = Code(CameraPath);
            Assert.IsFalse(Regex.IsMatch(code, @"rotation\s*\*\s*_followOffset"),
                "A pose site still reads the RAW offset, so the rear view is silently inactive " +
                "there. Every pose must go through EffectiveOffset.");
            Assert.AreEqual(2, Regex.Matches(code, @"rotation\s*\*\s*EffectiveOffset").Count,
                "Both pose sites (UpdateCamera's desiredPos and SnapToTarget) must use it.");
        }

        // ==================================================================
        // The retired picture-in-picture

        [Test]
        public void NothingGrantsTheRetiredPipPanel()
        {
            foreach (var path in ScriptsExcept(PipPath))
            {
                Assert.IsFalse(Code(path).Contains("SetLocalPilot("),
                    $"{Path.GetFileName(path)} grants the Pip panel. The picture-in-picture rear " +
                    "view is retired in favour of VesselRearView; re-granting it puts a second " +
                    "camera pass and a dead panel back on screen.");
            }
        }

        [Test]
        public void PipIsKeptSoItsAwakeStillStandsTheSecondCameraDown()
        {
            // Deleting the component is the tempting tidy-up and is a REGRESSION: eight hulls
            // still instance PipCamera.prefab, which ships active and enabled, and this Awake is
            // now the only thing switching it off.
            string code = Code(PipPath);
            Assert.IsTrue(code.Contains("void Awake"), "Pip must keep its Awake default-off.");
            Assert.IsTrue(Regex.IsMatch(code, @"Awake\(\)\s*\{[\s\S]{0,200}SetCameraActive"),
                "Pip.Awake must still stand its camera down.");
        }

        [Test]
        public void TheHudNeverPutsThePipPanelBackOnScreen()
        {
            string code = Code(GameHudPath);
            Assert.IsFalse(Regex.IsMatch(code, @"Pip\.SetActive\(\s*(true|data\.IsActive)\s*\)"),
                "MiniGameHUD can still activate the retired Pip panel. Both GameCanvas.prefab and " +
                "MiniGameHUD.prefab carried an m_IsActive:1 override on it, so 'off' has to be " +
                "stated, not assumed.");
            Assert.IsTrue(code.Contains("HideRetiredPipPanel"),
                "MiniGameHUD must stand the retired panel down - fifteen scenes carry structural " +
                "forks of that canvas and a fork's own copy is out of the prefab's reach.");
        }
    }
}
