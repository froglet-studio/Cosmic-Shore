#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Gibbon tether's invariants, guarded inside Unity.
    ///
    /// The exhaustive version — swings, frame-rate identity, hitch frames, the feel ladder — lives
    /// in <c>Tools/Build/gibbon_tether_harness</c>, which compiles this same solver from its real
    /// path and runs it. These are the handful that are cheap enough to keep in the edit-mode
    /// suite and that guard the rules most likely to be "simplified" away by a later pass.
    /// </summary>
    public class TetherSolverTests
    {
        const float Dt = 1f / 60f;
        const float K = 9f, C = 3.2f, MaxStretch = 26f, Break = 70f;

        [Test]
        public void SlackLine_AppliesNoForce()
        {
            var s = TetherSolver.Step(Vector3.zero, Vector3.forward * 70f,
                                      new Vector3(100f, 0f, 0f), 200f, K, C, MaxStretch, Break, Dt);
            Assert.AreEqual(0f, s.DeltaV.magnitude, 1e-6f, "A rope must be silent while slack.");
            Assert.AreEqual(0f, s.Tension01, 1e-6f);
        }

        [Test]
        public void TautLine_DoesNoTangentialWork()
        {
            // THE load-bearing invariant: a rope preserves swing speed and conserves angular
            // momentum. Damping the full velocity instead of the radial component alone breaks
            // this, brakes every swing, and is the single easiest way to make the vessel mud.
            Vector3 pos = Vector3.zero, anchor = new Vector3(100f, 0f, 0f), v = Vector3.forward * 70f;
            float before = TetherSolver.TangentialSpeed(pos, v, anchor);
            var s = TetherSolver.Step(pos, v, anchor, 40f, K, C, MaxStretch, Break, Dt);
            float after = TetherSolver.TangentialSpeed(pos, v + s.DeltaV, anchor);
            Assert.AreEqual(before, after, 1e-3f, "A taut line must do no tangential work.");
        }

        [Test]
        public void SpringForce_IsBoundedByMaxStretch()
        {
            // What makes an explicit spring safe across a hitch frame: the stretch fed to it is
            // clamped, so peak acceleration is a number that can be stated.
            var near = TetherSolver.Step(Vector3.zero, Vector3.zero, new Vector3(-100f, 0f, 0f),
                                         100f - MaxStretch, K, C, MaxStretch, 0f, Dt);
            var far = TetherSolver.Step(Vector3.zero, Vector3.zero, new Vector3(-100f, 0f, 0f),
                                        1f, K, C, MaxStretch, 0f, Dt);
            Assert.AreEqual(near.DeltaV.magnitude, far.DeltaV.magnitude, 1e-4f,
                "Stretch past MaxStretch must not increase the force.");
        }

        [Test]
        public void OverStretchedLine_Breaks()
        {
            var s = TetherSolver.Step(Vector3.zero, Vector3.zero, new Vector3(-500f, 0f, 0f),
                                      100f, K, C, MaxStretch, Break, Dt);
            Assert.IsTrue(s.Break, "Past BreakStretch a line must release itself.");
        }

        [Test]
        public void ApplyReel_NeverPaysALineOut()
        {
            // The chatter bug, as a standing negative control. The obvious one-liner
            // `Max(floor, rest - step)` LENGTHENS the line whenever the floor has risen above it —
            // and the floor rises with orbital speed, which the reel itself is raising.
            Assert.AreEqual(50f, TetherSolver.ApplyReel(50f, 5f, 80f), 1e-4f,
                "A line already below the floor must HOLD, never grow.");
            Assert.AreEqual(60f, TetherSolver.ApplyReel(70f, 30f, 60f), 1e-4f,
                "The floor must stop a reel at the floor.");
            Assert.AreEqual(65f, TetherSolver.ApplyReel(70f, 5f, 10f), 1e-4f,
                "An unobstructed reel must travel its full step.");
        }

        [Test]
        public void ReelEfficiency_ReachesZeroAtTheCap()
        {
            Assert.AreEqual(0f, TetherSolver.ReelStep(260f, 260f, 34f, Dt), 1e-5f,
                "The winch must stop doing work AT the cap rather than braking the pilot.");
            Assert.Greater(TetherSolver.ReelStep(0f, 260f, 34f, Dt), 0f);
        }

        [Test]
        public void RestLengthFloor_TakesTheLargerOfGeometryAndOrbitalRate()
        {
            // Slow: geometry binds. Fast: the orbital-rate term binds, so the end of a reel stays
            // readable instead of becoming a blur.
            Assert.AreEqual(13f, TetherSolver.RestLengthFloor(6f, 3f, 4f, 10f, 3.2f), 1e-3f);
            Assert.AreEqual(62.5f, TetherSolver.RestLengthFloor(6f, 3f, 4f, 200f, 3.2f), 1e-3f);
        }

        [Test]
        public void BeamLength_IsLinearInTheTrigger()
        {
            // The drawn preview is the contract; a curve here makes it disagree with the control.
            Assert.AreEqual(45f, TetherSolver.BeamLength(0f, 45f, 320f), 1e-4f);
            Assert.AreEqual(182.5f, TetherSolver.BeamLength(0.5f, 45f, 320f), 1e-3f);
            Assert.AreEqual(320f, TetherSolver.BeamLength(1f, 45f, 320f), 1e-4f);
        }
    }
}
#endif
