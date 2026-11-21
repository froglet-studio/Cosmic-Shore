using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's 2026-11-19 pass (R_VesselActions/RHINO_RAMP_BOOST.md § "Inertia and Slingshot"): no camera shake, the
    /// old Space and Time level-5s in the base kit, an inertial ramp that oversteers, and the new level-5s Avalanche
    /// (Space) and Slingshot (Time).
    /// </summary>
    public class RhinoInertiaTests
    {
        [Test]
        public void ZeroGripSnapsAndGripClosesPartOfTheAngle()
        {
            Assert.AreEqual(1f, InertialGripMath.Convergence(0f, 1f / 60f), 1e-6f, "no grip authored = the fleet's snap");
            float c = InertialGripMath.Convergence(16f, 1f / 60f);
            Assert.That(c, Is.GreaterThan(0.2f).And.LessThan(0.3f), "16/s closes about a quarter of the angle a frame");
            // Frame-rate independent: two half frames close what one whole frame does.
            float half = InertialGripMath.Convergence(16f, 1f / 120f);
            Assert.AreEqual(c, 1f - (1f - half) * (1f - half), 1e-5f);
        }

        [Test]
        public void ASteadyTurnSlipsByTurnRateOverGrip()
        {
            // Cruise 50 u/s: 50 x 0.2 + 90 = 100 deg/s at 16/s grip -> about 6 degrees: "a little sharper".
            Assert.AreEqual(6.25f, InertialGripMath.SteadySlipDegrees(100f, 16f), 1e-4f);
            Assert.AreEqual(0f, InertialGripMath.SteadySlipDegrees(100f, 0f), 1e-6f);
        }

        [Test]
        public void TheSlingshotScalesWithTheSlideAndIsCapped()
        {
            Assert.AreEqual(0f, RhinoSlingshot.Kick(10f, 800f, 12f, 35f, 0.3f, 350f, 80f), 1e-5f, "a shallow turn is no slide");
            Assert.AreEqual(0f, RhinoSlingshot.Kick(30f, 50f, 12f, 35f, 0.3f, 350f, 80f), 1e-5f, "a crawl does not fling");
            Assert.AreEqual(120f, RhinoSlingshot.Kick(35f, 400f, 12f, 35f, 0.3f, 350f, 80f), 1e-4f, "a full slide: 30% of 400");
            Assert.AreEqual(350f, RhinoSlingshot.Kick(60f, 1400f, 12f, 35f, 0.3f, 350f, 80f), 1e-4f, "never more than the cap");
            Assert.AreEqual(60f, RhinoSlingshot.Kick(23.5f, 400f, 12f, 35f, 0.3f, 350f, 80f), 1e-4f, "half the depth, half the kick");
        }

        [Test]
        public void TheRhinoTakesNoCameraShake()
        {
            var feel = Resources.Load<CameraFeelConfigSO>("CameraFeelConfig");
            Assert.IsNotNull(feel);
            Assert.IsTrue(feel.NoShakeFor(VesselClassType.Rhino));
            Assert.AreEqual(0f, feel.ProfileFor(VesselClassType.Rhino).weight, 1e-6f, "no hit, shot or skim kicks");
            Assert.IsFalse(feel.NoShakeFor(VesselClassType.Squirrel), "every other hull keeps its feel");
        }

        [Test]
        public void ANoShakeFeelHoldsStillUnderTraumaAndDrift()
        {
            var config = ScriptableObject.CreateInstance<CameraFeelConfigSO>();
            var feel = new CameraFeel();
            feel.AddTrauma(1f);
            feel.Step(config, 1f / 60f, Vector3.zero, Quaternion.identity, 50f, 1f, 1f, drift: 1f, noShake: true);
            Assert.AreEqual(0f, feel.Trauma, 1e-6f);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void TheRampIsAFreightTrain()
        {
            var ramp = AssetDatabase.LoadAssetAtPath<RampBoostActionSO>(
                "Assets/_SO_Assets/VesselActions/Rhino/RhinoRampBoostAction.asset");
            Assert.IsNotNull(ramp);
            Assert.AreEqual(28f, ramp.MaxBoostMultiplier, 1e-4f, "top 50 x 28 = 1400 u/s");
            Assert.AreEqual(110f, ramp.AccelerationPerSecond, 1e-4f, "a slow build");
            Assert.That(ramp.BleedPerSecond, Is.LessThanOrEqualTo(100f), "and a slow loss");
            Assert.That(ramp.ReturnPerSecond, Is.LessThanOrEqualTo(60f));
        }
    }
}
