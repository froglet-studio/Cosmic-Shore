using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The camera feel (<c>Docs/CAMERA_FEEL.md</c>): <see cref="CameraFeel"/>'s inertia, kicks, shake, rumble and
    /// drift lean, and <see cref="CameraFeelConfigSO"/>'s weights. The model is plain C#, so every behaviour is
    /// stepped directly at 60 fps with the view facing +z (camera-local == world). Breathing is switched off in
    /// every rig so a test reads only the layer it is about.
    /// </summary>
    [TestFixture]
    public class CameraFeelTests
    {
        const float Dt = 1f / 60f;
        const float Distance = 20f;

        CameraFeelConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CameraFeelConfigSO>();
            Set("breathAngle", 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config) Object.DestroyImmediate(_config);
        }

        // By reflection rather than SerializedObject, so the suite also runs on the engine's edit-mode runner.
        void Set(string field, float value)
        {
            var info = typeof(CameraFeelConfigSO).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, $"CameraFeelConfigSO has no field '{field}'.");
            info.SetValue(_config, value);
        }

        /// <summary>Fly the framed point for <paramref name="seconds"/> from <paramref name="start"/> at
        /// <paramref name="velocity"/>, accelerating at <paramref name="accel"/>; returns where it ended.</summary>
        Vector3 Fly(CameraFeel feel, float seconds, Vector3 start, Vector3 velocity, Vector3 accel,
            float gForce = 1f, float drift = 0f)
        {
            Vector3 p = start, v = velocity;
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < frames; i++)
            {
                v += accel * Dt;
                p += v * Dt;
                feel.Step(_config, Dt, p, Quaternion.identity, Distance, gForce, 1f, drift);
            }
            return p;
        }

        // ---------------------------------------------------------------- G-force

        [Test]
        public void ASteadyCruise_LeavesTheCameraExactlyWhereItWas()
        {
            var feel = new CameraFeel();
            Fly(feel, 3f, Vector3.zero, new Vector3(0f, 0f, 150f), Vector3.zero);
            Assert.Less(feel.LocalOffset.magnitude, 1e-4f, "G must lag ACCELERATION, never speed.");
            Assert.Less(feel.LocalAngles.magnitude, 1e-4f);
        }

        [Test]
        public void ABoost_LetsTheHullPullAhead_InsideTheMaxLag()
        {
            var feel = new CameraFeel();
            // One full lag's worth of acceleration: FullLagAcceleration follow distances per second squared.
            Fly(feel, 2f, Vector3.zero, new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, _config.FullLagAcceleration * Distance));
            Assert.Less(feel.LocalOffset.z, -0.3f * _config.MaxLag * _config.ForwardLagShare,
                "Accelerating forward must drop the camera back, so the hull pulls ahead in the frame.");
            Assert.LessOrEqual(feel.LocalOffset.magnitude, _config.MaxLag + 1e-5f);
        }

        [Test]
        public void ATurn_SwingsTheCameraOutside_SoTheHullLeadsIntoIt()
        {
            var feel = new CameraFeel();
            Fly(feel, 2f, Vector3.zero, new Vector3(0f, 0f, 100f), new Vector3(0.5f * _config.FullLagAcceleration * Distance, 0f, 0f));
            Assert.Less(feel.LocalOffset.x, -0.2f * _config.MaxLag, "Accelerating right must swing the camera left.");
        }

        [Test]
        public void MoreG_MovesTheCameraFurther()
        {
            Vector3 accel = new(0.4f * _config.FullLagAcceleration * Distance, 0f, 0f);
            var less = new CameraFeel();
            var more = new CameraFeel();
            Fly(less, 3f, Vector3.zero, Vector3.forward * 100f, accel, gForce: 0.8f);
            Fly(more, 3f, Vector3.zero, Vector3.forward * 100f, accel, gForce: 1.4f);
            Assert.Greater(Mathf.Abs(more.LocalOffset.x), Mathf.Abs(less.LocalOffset.x) * 1.3f);
        }

        [Test]
        public void ZeroG_HasNoLag()
        {
            var feel = new CameraFeel();
            Fly(feel, 1f, Vector3.zero, Vector3.forward * 100f, new Vector3(0f, 0f, 500f), gForce: 0f);
            Assert.Less(feel.LocalOffset.magnitude, 1e-6f);
        }

        [Test]
        public void APortalCarry_IsNotMotion()
        {
            var feel = new CameraFeel();
            Vector3 v = new(0f, 0f, 120f);
            Vector3 p = Fly(feel, 1f, Vector3.zero, v, Vector3.zero);
            Vector3 shift = new(4000f, -300f, 900f);
            feel.Shift(shift);
            p += shift;
            for (int i = 0; i < 30; i++)
            {
                p += v * Dt;
                feel.Step(_config, Dt, p, Quaternion.identity, Distance, 1f, 1f, 0f);
                // Float noise at 4000 u is ~1e-4 of lag; an unshifted jump saturates it (0.06 and above).
                Assert.Less(feel.LocalOffset.magnitude, 2e-3f, "A portal jump read as acceleration.");
            }
        }

        [Test]
        public void APause_HoldsThePose()
        {
            var feel = new CameraFeel();
            Fly(feel, 0.5f, Vector3.zero, Vector3.forward * 100f, new Vector3(300f, 0f, 0f));
            Vector3 before = feel.LocalOffset;
            feel.Step(_config, 0f, new Vector3(1e5f, 0f, 0f), Quaternion.identity, Distance, 1f, 1f, 0f);
            Assert.AreEqual(before, feel.LocalOffset);
        }

        // ---------------------------------------------------------------- kicks

        [Test]
        public void AKick_PeaksAtTheAuthoredFraction_AndSettles()
        {
            var feel = new CameraFeel();
            Fly(feel, 0.1f, Vector3.zero, Vector3.zero, Vector3.zero);
            const float peak = 0.03f;
            feel.Kick(_config, Vector3.right, peak, Vector3.zero);
            float max = 0f;
            for (int i = 0; i < 180; i++)
            {
                feel.Step(_config, Dt, Vector3.zero, Quaternion.identity, Distance, 1f, 1f, 0f);
                max = Mathf.Max(max, feel.LocalOffset.x);
            }
            Assert.AreEqual(peak, max, peak * 0.1f, "The authored number must be the kick's true peak.");
            Assert.Less(feel.LocalOffset.magnitude, peak * 0.01f, "A kick must settle back to rest within 3 s.");
        }

        [Test]
        public void ASkim_NudgesAwayFromThePrism_AndRumbles()
        {
            var feel = new CameraFeel();
            Fly(feel, 0.1f, Vector3.zero, Vector3.zero, Vector3.zero);
            // The prism passed on the camera's left, so "away" is +x.
            feel.Skim(_config, Vector3.right, 1f, 1f);
            Assert.Greater(feel.SkimRumble, 0f);
            float max = 0f;
            for (int i = 0; i < 20; i++)
            {
                feel.Step(_config, Dt, Vector3.zero, Quaternion.identity, Distance, 1f, 1f, 0f);
                max = Mathf.Max(max, feel.LocalOffset.x);
            }
            Assert.Greater(max, 0.5f * _config.SkimNudge);
        }

        [Test]
        public void AHit_AddsTrauma_ThatDrainsAway()
        {
            var feel = new CameraFeel();
            Fly(feel, 0.1f, Vector3.zero, Vector3.zero, Vector3.zero);
            feel.Impact(_config, Vector3.left, 1f);
            Assert.AreEqual(_config.ImpactTrauma, feel.Trauma, 1e-6f);
            Fly(feel, 1f / _config.TraumaDecay + 0.1f, Vector3.zero, Vector3.zero, Vector3.zero);
            Assert.AreEqual(0f, feel.Trauma);
        }

        [Test]
        public void AHitReportedTwice_IsFeltOnce()
        {
            var feel = new CameraFeel();
            Fly(feel, 0.5f, Vector3.zero, Vector3.zero, Vector3.zero);
            feel.Impact(_config, Vector3.left, 1f);
            feel.Impact(_config, Vector3.left, 1f);   // a hull with two colliders, same frame
            Assert.AreEqual(_config.ImpactTrauma, feel.Trauma, 1e-6f);
        }

        [Test]
        public void AStreamOfRounds_RattlesWithoutPinningTheShake()
        {
            var feel = new CameraFeel();
            float max = 0f;
            for (int i = 0; i < 600; i++)   // a round every frame for 10 s
            {
                feel.Struck(_config, Vector3.left, 1f);
                feel.Step(_config, Dt, Vector3.zero, Quaternion.identity, Distance, 1f, 1f, 0f);
                max = Mathf.Max(max, feel.Trauma);
            }
            Assert.Greater(max, 0f);
            Assert.LessOrEqual(max, _config.StruckTrauma + 1e-4f,
                "Strikes must be spaced so the drain keeps up; sustained fire must not pin the shake at full.");
        }

        [Test]
        public void KicksNeverPassTheirClamp()
        {
            var feel = new CameraFeel();
            Set("traumaOffset", 0f);   // read the kicks alone; the positional shake rides on top of them
            for (int i = 0; i < 60; i++)
            {
                feel.Blast(_config, Vector3.up, 2f);
                feel.Step(_config, Dt, Vector3.zero, Quaternion.identity, Distance, 1f, 1f, 0f);
                Assert.LessOrEqual(feel.LocalOffset.magnitude, _config.MaxKick + 1e-5f);
                Assert.LessOrEqual(feel.LocalAngles.magnitude, _config.MaxKickAngle + 3f * _config.TraumaRoll + 1e-3f);
            }
        }

        [Test]
        public void AReset_ReturnsToRest()
        {
            var feel = new CameraFeel();
            Fly(feel, 0.5f, Vector3.zero, Vector3.forward * 50f, new Vector3(200f, 0f, 200f));
            feel.Impact(_config, Vector3.left, 2f);
            feel.Reset();
            feel.Step(_config, Dt, new Vector3(500f, 0f, 0f), Quaternion.identity, Distance, 1f, 1f, 0f);
            Assert.Less(feel.LocalOffset.magnitude, 1e-6f);
            Assert.Less(feel.LocalAngles.magnitude, 1e-6f);
            Assert.AreEqual(0f, feel.Trauma);
        }

        // ---------------------------------------------------------------- drift

        [Test]
        public void AFullDrift_SlidesTheHullOut_AndTheNeckLeans_ThenSettlesBack()
        {
            var feel = new CameraFeel();
            Set("driftRumble", 0f);
            Vector3 cruise = new(0f, 0f, 2f * _config.DriftFullSpeed * Distance);   // well above drift speed
            Vector3 p = Fly(feel, 3f, Vector3.zero, cruise, Vector3.zero, drift: 1f);
            float slide = CameraFeel.SoftClamp(new Vector3(_config.DriftSlide, 0f, 0f), _config.MaxGOffset).x;
            Assert.AreEqual(-slide, feel.LocalOffset.x, 1e-3f, "A full drift slides the hull off the centre by DriftSlide.");
            Assert.AreEqual(_config.GRoll * slide / _config.MaxGOffset, feel.LocalAngles.z, 0.02f,
                "The same G leans the camera like the pilot's neck.");
            Fly(feel, 8f, p, cruise, Vector3.zero, drift: 0f);
            Assert.Less(feel.LocalOffset.magnitude, 1e-3f);
            Assert.Less(feel.LocalAngles.magnitude, 1e-2f);
        }

        [Test]
        public void ADrift_BuildsWithAnAcceleratingStart()
        {
            // Not an ease along a straight line: the slide starts still and gathers speed.
            var feel = new CameraFeel();
            Vector3 cruise = new(0f, 0f, 2f * _config.DriftFullSpeed * Distance);
            Vector3 p = Fly(feel, 0.5f, Vector3.zero, cruise, Vector3.zero);
            float previous = 0f, previousStep = 0f;
            bool accelerated = false;
            for (int i = 0; i < 180; i++)
            {
                p += cruise * Dt;
                feel.Step(_config, Dt, p, Quaternion.identity, Distance, 1f, 1f, 1f);
                float x = -feel.LocalOffset.x;
                if (i > 0 && i < 4 && x - previous > previousStep + 1e-7f) accelerated = true;
                previousStep = x - previous;
                previous = x;
            }
            Assert.IsTrue(accelerated, "The slide must gather speed over its first frames, not start at full speed.");
            Assert.Greater(previous, 0f);
        }

        [Test]
        public void ADriftEnding_GlidesHome_WithoutSnappingOrSwingingPast()
        {
            // The Squirrel's flow (Garrett 2026-10-27): the return from a drift must not jerk. The slide's rest
            // eases home slower than it built, so the hull glides back and never crosses the centre.
            var feel = new CameraFeel();
            Set("driftRumble", 0f);
            Vector3 cruise = new(0f, 0f, 2f * _config.DriftFullSpeed * Distance);
            Vector3 p = Fly(feel, 3f, Vector3.zero, cruise, Vector3.zero, drift: 1f);
            float held = -feel.LocalOffset.x;
            float previous = held, fastest = 0f, lowest = held;
            for (int i = 0; i < 360; i++)
            {
                p += cruise * Dt;
                feel.Step(_config, Dt, p, Quaternion.identity, Distance, 1f, 1f, 0f);
                float x = -feel.LocalOffset.x;
                fastest = Mathf.Max(fastest, Mathf.Abs(x - previous) / Dt);
                lowest = Mathf.Min(lowest, x);
                previous = x;
            }
            Assert.Less(fastest, held * 1.5f, "The return is a glide (under 1.5 slide-widths a second), not a snap.");
            Assert.GreaterOrEqual(lowest, -1e-4f, "The hull never swings past the centre on the way home.");
            Assert.Less(previous, held * 0.1f, "And it does come home.");
        }

        [Test]
        public void ADriftAtACrawl_SlidesLess()
        {
            var slow = new CameraFeel();
            var fast = new CameraFeel();
            Fly(slow, 3f, Vector3.zero, new Vector3(0f, 0f, 0.3f * _config.DriftFullSpeed * Distance), Vector3.zero, drift: 1f);
            Fly(fast, 3f, Vector3.zero, new Vector3(0f, 0f, 2f * _config.DriftFullSpeed * Distance), Vector3.zero, drift: 1f);
            Assert.Less(Mathf.Abs(slow.LocalOffset.x), 0.5f * Mathf.Abs(fast.LocalOffset.x));
        }

        [Test]
        public void ABoost_ThrowsTheNeckBack()
        {
            var feel = new CameraFeel();
            Fly(feel, 2f, Vector3.zero, new Vector3(0f, 0f, 100f), new Vector3(0f, 0f, _config.FullLagAcceleration * Distance));
            Assert.Less(feel.LocalAngles.x, -0.1f * _config.GPitch, "A boost tips the view nose-up (a negative pitch).");
        }

        // ---------------------------------------------------------------- weights and profiles

        [Test]
        public void DifferentObjects_LandWithDifferentWeight()
        {
            Assert.AreEqual(1f, _config.PrismWeight(60f, false, false), 1e-5f, "The reference prism lands with weight 1.");
            Assert.AreEqual(2f, _config.PrismWeight(480f, false, false), 1e-4f, "8x the volume is 2x the size.");
            Assert.AreEqual(2f, _config.PrismWeight(1e6f, false, false), 1e-4f, "Held at the range's top.");
            Assert.AreEqual(0.6f, _config.PrismWeight(0.01f, false, false), 1e-4f, "Held at the range's bottom.");
            float plain = _config.PrismWeight(60f, false, false);
            float shielded = _config.PrismWeight(60f, true, false);
            float super = _config.PrismWeight(60f, true, true);
            Assert.Greater(shielded, plain);
            Assert.Greater(super, shielded);
        }

        [Test]
        public void EachHull_CarriesItsOwnCharacter()
        {
            var (rhinoG, rhinoWeight) = _config.ProfileFor(VesselClassType.Rhino);
            var (squirrelG, _) = _config.ProfileFor(VesselClassType.Squirrel);
            Assert.Less(rhinoG, 1f, "A lumbering hull's pilot feels less G than its size alone gives.");
            Assert.Greater(rhinoWeight, 1f);
            Assert.Greater(squirrelG, 1f, "An agile drifter's pilot feels more.");
            Assert.AreEqual((1f, 1f), _config.ProfileFor(VesselClassType.Any), "An unlisted hull is the fleet's.");
        }

        [Test]
        public void ABiggerHull_FeelsLessG_AndFeelsItSlower()
        {
            Set("referenceHullSize", 5f);
            Assert.AreEqual((1f, 1f), _config.SizeFeel(5f), "The reference hull feels the numbers as written.");
            Assert.AreEqual((1f, 1f), _config.SizeFeel(0f), "An unmeasured hull is the reference.");
            var (bigG, bigResponse) = _config.SizeFeel(20f);
            var (smallG, smallResponse) = _config.SizeFeel(2.5f);
            Assert.Less(bigG, 1f);
            Assert.Less(bigResponse, 1f);
            Assert.Greater(smallG, 1f);
            Assert.Greater(smallResponse, 1f);
            Assert.AreEqual(1.4f, _config.SizeFeel(0.01f).gForce, 1e-5f, "Held at the range's top.");
            Assert.AreEqual(0.55f, _config.SizeFeel(1e5f).gForce, 1e-5f, "Held at the range's bottom.");
        }

        [Test]
        public void TheHullIsMeasured_InItsOwnFrame_AtItsWorldSize()
        {
            var root = new GameObject("FeelHull");
            try
            {
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                var mesh = new Mesh { vertices = new[] { new Vector3(-5f, -1f, -2f), new Vector3(5f, 1f, 2f), new Vector3(0f, 0f, 0f) } };
                mesh.triangles = new[] { 0, 1, 2 };
                mesh.RecalculateBounds();
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>();

                // 10 x 2 x 4: a mean of 16 / 3.
                Assert.AreEqual(16f / 3f, CustomCameraController.MeasureHullSize(root.transform), 1e-3f);
                root.transform.rotation = Quaternion.Euler(30f, 70f, -20f);
                Assert.AreEqual(16f / 3f, CustomCameraController.MeasureHullSize(root.transform), 1e-3f,
                    "Pointing the hull elsewhere must not change its size.");
                root.transform.localScale = Vector3.one * 2f;
                Assert.AreEqual(32f / 3f, CustomCameraController.MeasureHullSize(root.transform), 1e-3f,
                    "A scaled prefab measures at its real size.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheShippedConfig_LoadsFromResources()
        {
            var shipped = Resources.Load<CameraFeelConfigSO>("CameraFeelConfig");
            Assert.IsNotNull(shipped, "Resources/CameraFeelConfig.asset is missing; the feel would run on code defaults.");
            Assert.IsTrue(shipped.Enabled);
            Assert.Greater(shipped.MaxLag, 0f);
            Assert.Greater(shipped.KickFrequency, 0f);
        }

        // ---------------------------------------------------------------- the controller's law

        const string CameraPath = "Assets/_Scripts/Controller/Camera/CustomCameraController.cs";

        static string Code(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            string text = File.ReadAllText(path);
            text = Regex.Replace(text, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(text, @"//.*?$", string.Empty, RegexOptions.Multiline);
        }

        [Test]
        public void TheFeelComesOffBeforeAnyPoseIsWorked()
        {
            // The dynamic and eased rigs smooth FROM the camera's current pose; a feel offset still on it
            // would be smoothed into the follow itself and outlive the frame it was meant for.
            Assert.IsTrue(Regex.IsMatch(Code(CameraPath), @"void UpdateCamera\(\)\s*\{\s*UndoFeel\(\);"),
                "UpdateCamera must take last frame's feel off before anything else reads the pose.");
        }

        [Test]
        public void EveryPathThatShakes_AlsoFeels()
        {
            string code = Code(CameraPath);
            int shakes = Regex.Matches(code, @"\bApplyShake\(\);").Count;
            int feels = Regex.Matches(code, @"\bApplyShake\(\);\s*ApplyFeel\(followPoint, warp\);").Count;
            Assert.Greater(shakes, 0);
            Assert.AreEqual(shakes, feels, "A pose path applies the shake but not the feel after it.");
        }

        [Test]
        public void TheSpringStep_IsTheSameAtEveryFrameRate(
            [Values(0.42f, 0.85f, 1f, 1.6f)] float zeta)
        {
            // Exact means one 1/30 s step lands where two 1/60 s steps do, in every damping regime.
            const float omega = 20f;
            Vector3 xa = new(0.3f, -0.1f, 0.05f), va = new(-2f, 1f, 0.5f);
            Vector3 xb = xa, vb = va;
            CameraFeel.Spring.Step(omega, zeta, 1f / 30f).Apply(ref xa, ref va);
            var half = CameraFeel.Spring.Step(omega, zeta, 1f / 60f);
            half.Apply(ref xb, ref vb);
            half.Apply(ref xb, ref vb);
            Assert.Less((xa - xb).magnitude, 1e-5f);
            Assert.Less((va - vb).magnitude, 1e-4f);
        }

        [Test]
        public void PeakFactor_IsTheFirstPeakOfTheSpring()
        {
            // Lightly damped springs keep most of their kick; heavily damped ones a little over 1/e.
            Assert.Greater(CameraFeel.PeakFactor(0.1f), 0.8f);
            Assert.AreEqual(1f / Mathf.Exp(1f), CameraFeel.PeakFactor(1f), 0.03f);
        }
    }
}
