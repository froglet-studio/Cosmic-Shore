#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Thresher's ANALOG TRIGGERS (Thresher Plant Lab round 2, <c>Docs/Studios/THRESHER_PLANT_LAB.md</c>):
    /// LT's depth is a brake that stops the ball only at a full pull; RT's depth is the chain's rigidity
    /// (pulled = rigid, released = a slack bungee). Flown at the numbers <c>ThresherConfig.asset</c> ships,
    /// which <c>Tools/Build/thresher_plant_lab/parity.cjs</c> holds equal to the lab's preset; the same gate
    /// holds this solver's analog path to the lab page frame by frame. The digital path keeps its own suite
    /// (<see cref="ThresherChainSolverTests"/>, at the class defaults, where analog triggers are off).
    /// Also compiled and RUN offline by <c>Tools/Build/thresher_chain_harness</c>.
    /// </summary>
    public class ThresherAnalogTriggerTests
    {
        const float Dt = 1f / 60f;
        const float FullPull = 0.97f, LetGo = 0.9f;

        /// <summary>The shipped asset's analog dials over the class defaults (game units).</summary>
        static ThresherChainSettings Analog()
        {
            var s = new ThresherDials().ToSettings();
            s.Analog = true;
            s.BrakeMax = 4f; s.BrakeCurve = 1.5f; s.AnchorCurve = 2f; s.StopDecel = 450f;
            s.ChainSlackRate = 1.2f; s.ChainRigidRate = 30f; s.ChainDamping = 0.8f; s.RigidityCurve = 1f;
            s.RigiditySlew = 2f; s.AnalogHookSeconds = 0.5f; s.SpinEase = 0.4f; s.Yank = 0f;
            return s;
        }

        /// <summary>Yaw <paramref name="v"/> by <paramref name="deg"/> about world up (positive = right).</summary>
        static Vector3 Yaw(Vector3 v, float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            return new Vector3(v.x * c + v.z * sn, v.y, -v.x * sn + v.z * c);
        }

        /// <summary>Unsigned angle between two directions (degrees).</summary>
        static float AngleDeg(Vector3 a, Vector3 b)
            => Mathf.Acos(Mathf.Clamp(Vector3.Dot(a.normalized, b.normalized), -1f, 1f)) / Mathf.Deg2Rad;

        /// <summary>The chain harness's stand-in pilot: speed eases toward cruise like the vector model's nose
        /// step, the stick yaws the nose, and while planted the nose is the solver's velocity.</summary>
        sealed class Flight
        {
            public readonly ThresherChainSolver S;
            public Vector3 P, F = Vector3.forward, Last;
            public float V, Cruise;
            public ThresherStepResult R;
            public Flight(ThresherChainSettings s)
            {
                S = new ThresherChainSolver(s);
                Cruise = V = s.CruiseSpeed;
                Last = F * V;
                S.Reset(P, F, Last);
            }
            /// <summary>One frame: triggers (letting go when the solver asks), then the move.</summary>
            public ThresherTriggerEdge Frame(float lt, float rt, float yawDegPerSec, float dt = Dt)
            {
                var edge = S.AnalogTriggers(lt, rt, FullPull, LetGo, P, Last, Vector3.up, dt);
                if (edge == ThresherTriggerEdge.LetGo) S.Release(Last);
                F = Yaw(F, yawDegPerSec * dt);
                float th = V + (Cruise - V) * 1.5f * dt;
                R = S.Step(P, F * th, false, Cruise, dt);
                if (S.Mode == ThresherMode.Pivot || R.Launched) F = R.ShipVelocity.normalized;
                V = R.ShipVelocity.magnitude;
                P += R.ShipVelocity * dt;
                Last = R.ShipVelocity;
                return edge;
            }
            public void Fly(float seconds, float lt, float rt, float yaw = 0f, float dt = Dt)
            {
                for (int i = 0; i < Mathf.RoundToInt(seconds / dt); i++) Frame(lt, rt, yaw, dt);
            }
        }

        [Test]
        public void PartialPull_BrakesTheBallButNeverPlantsIt()
        {
            var f = new Flight(Analog());
            f.Fly(1f, 0f, 1f, 40f);
            for (int i = 0; i < 180; i++)
            {
                f.Frame(0.85f, 1f, 0f);
                Assert.AreEqual(ThresherMode.Free, f.S.Mode, "a pull short of fullPull must never stop the ball");
            }
        }

        [Test]
        public void FullPull_StopsTheBallAlongACurve_WithNoSingleFrameStop()
        {
            var s = Analog();
            var f = new Flight(s);
            f.Fly(1f, 0f, 1f, 50f);
            Vector3 before = f.S.BallVelocity;
            float worst = 0f;
            bool planted = false;
            for (int i = 0; i < 60 && !planted; i++)
            {
                f.Frame(1f, 1f, 0f);
                worst = Mathf.Max(worst, (f.S.BallVelocity - before).magnitude);
                before = f.S.BallVelocity;
                planted = f.R.Planted;
            }
            Assert.IsTrue(planted, "a full pull must plant the ball");
            Assert.AreEqual(0f, f.S.BallSpeed, 1e-3f, "the ball is at rest when it becomes the pivot");
            // The S-curve peaks at StopDecel; the shipped skid pinned ~63 u/s in ONE frame.
            Assert.LessOrEqual(worst, s.StopDecel * Dt * 1.1f, "no frame may take more than the curve's peak off the ball");
        }

        [Test]
        public void LettingGoMidStop_TheBallKeepsItsOwnSpeed()
        {
            var f = new Flight(Analog());
            f.Fly(1f, 0f, 1f, 40f);
            f.Fly(3 * Dt, 1f, 1f);
            Assert.IsTrue(f.S.IsBraking, "three frames into a full pull the ball is still stopping");
            var edge = f.Frame(0f, 1f, 0f);
            Assert.AreEqual(ThresherTriggerEdge.LetGo, edge);
            Assert.AreEqual(ThresherMode.Free, f.S.Mode);
            Assert.Greater(f.S.BallSpeed, 20f, "a ball let go while stopping is not yanked to yank x ship speed (0 here)");
        }

        [Test]
        public void SlackChain_LetsTheTowedBallOut_RigidChainHoldsItAtRestLength()
        {
            float Reach(float rt)
            {
                var f = new Flight(Analog());
                float max = 0f;
                for (int i = 0; i < 240; i++) { f.Frame(0f, rt, 90f); max = Mathf.Max(max, f.S.Extent); }
                return max;
            }
            var s = Analog();
            Assert.Greater(Reach(0f), s.RestLength + 5f, "released, the chain is a bungee the turn swings the ball out on");
            Assert.Less(Reach(1f), s.RestLength + 2f, "fully pulled, the chain is rigid");
        }

        [Test]
        public void PullingRigid_ReelsAPlantedShipInAndSpinsItUp()
        {
            var f = new Flight(Analog());
            f.Fly(1f, 0f, 0f, 40f);
            f.Fly(2.5f, 1f, 0f);
            Assert.AreEqual(ThresherMode.Pivot, f.S.Mode);
            float outReach = f.S.Extent, outSpeed = f.S.LockSpeed;
            Assert.Greater(outReach, f.S.Settings.RestLength + 10f, "a slack planted chain lets the spin carry the ship out");
            f.Fly(1.5f, 1f, 1f);
            Assert.Less(f.S.Extent, outReach - 10f, "pulling rigid reels the ship in");
            Assert.Greater(f.S.LockSpeed, outSpeed * 1.2f, "and spin conservation spins it up");
        }

        [Test]
        public void RigiditySlew_LimitsHowFastTheChainStiffens()
        {
            var f = new Flight(Analog());
            for (int i = 0; i < 15; i++) f.Frame(0f, 1f, 0f);   // 0.25 s at 2/s
            Assert.AreEqual(0.5f, f.S.Rigidity, 0.02f);
        }

        [Test]
        public void AmbiguousPlant_OrbitsInTheFlightPlane_TheWayTheShipIsTurning()
        {
            // A plant within 30 degrees of flying straight away from the ball reads its orbit's direction from a
            // near-zero sideways velocity, which can disagree with the way the ship is turning. Sweep turn
            // patterns (one way, then back the other) and require, on every ambiguous plant: the orbit stays in
            // the flight plane, and the ship keeps turning the way it was turning when LT came down.
            int ambiguous = 0;
            foreach (float first in new[] { -90f, -45f, 0f, 45f, 90f })
            foreach (float second in new[] { -60f, -20f, 20f, 60f })
            foreach (int frames in new[] { 2, 6, 12, 20 })
            {
                var f = new Flight(Analog());
                f.Fly(1.5f, 0f, 1f);
                f.Fly(0.4f, 0f, 1f, first);
                for (int i = 0; i < frames; i++) f.Frame(0f, 1f, second);
                Vector3 rh = (f.P - f.S.BallPosition).normalized, v = f.Last.normalized;
                bool isAmbiguous = Mathf.Abs(Vector3.Dot(v, rh)) > Mathf.Cos(30f * Mathf.Deg2Rad);
                Vector3 before = f.Last;
                f.Frame(1f, 1f, 0f);                    // slam LT: the hook starts this frame
                f.Fly(0.25f, 1f, 1f);
                if (!isAmbiguous) continue;
                ambiguous++;
                Assert.Greater(Mathf.Abs(f.S.OrbitNormal(f.P).y), 0.99f, $"orbit left the flight plane ({first},{second},{frames})");
                float turned = Mathf.Atan2(Vector3.Dot(Vector3.Cross(before, f.Last), Vector3.up), Vector3.Dot(before, f.Last));
                Assert.AreEqual(Mathf.Sign(second), Mathf.Sign(turned), $"orbited against the turn ({first},{second},{frames})");
            }
            Assert.Greater(ambiguous, 10, "the sweep must actually produce ambiguous plants");
        }

        [Test]
        public void TheChainIsStableAtThirtyFramesASecond()
        {
            (float reach, float spin) Run(float dt)
            {
                var f = new Flight(Analog());
                f.Fly(1f, 0f, 0f, 40f, dt);
                f.Fly(2f, 1f, 0f, 0f, dt);
                f.Fly(1.5f, 1f, 1f, 0f, dt);
                return (f.S.Extent, f.S.LockSpeed);
            }
            var a = Run(1f / 60f);
            var b = Run(1f / 30f);
            Assert.AreEqual(a.reach, b.reach, 1.5f, "the rigid chain (30 rad/s) must be sub-stepped, or 30 fps diverges");
            Assert.AreEqual(a.spin, b.spin, a.spin * 0.08f);
        }

        [Test]
        public void ArmedLaunchWithALead_LeavesAlongTheOrbit_WithoutALead_AlongTheAim()
        {
            Vector3 Launch(float lead, Vector3 aim)
            {
                var f = new Flight(Analog());
                f.Fly(1f, 0f, 1f, 60f);
                f.Fly(1.5f, 1f, 1f);
                f.S.ArmLaunch(aim);
                f.S.LaunchLeadRadians = lead;
                for (int i = 0; i < 600; i++) { f.Frame(0.95f, 1f, 0f); if (f.R.Launched) return f.R.ShipVelocity; }
                Assert.IsTrue(false, "the armed launch never left");
                return Vector3.zero;
            }
            Vector3 aimDir = Vector3.right;
            Assert.Less(AngleDeg(Launch(0f, aimDir), aimDir), 0.5f, "shipped: the launch leaves along the aim");
            float off = AngleDeg(Launch(0.3f, aimDir), aimDir);
            Assert.Greater(off, 5f, "with a lead it leaves early, along the orbit, for the wind-down to turn onto the aim");
            Assert.Less(off, 0.3f / Mathf.Deg2Rad + 8f);
        }

        [Test]
        public void AnalogOff_TheTriggerEntryPointDoesNothing()
        {
            var s = new ThresherDials().ToSettings();
            Assert.IsFalse(s.Analog, "the class defaults keep the digital controls; the asset turns analog on");
            var solver = new ThresherChainSolver(s);
            solver.Reset(Vector3.zero, Vector3.forward, Vector3.forward * s.CruiseSpeed);
            Assert.AreEqual(ThresherTriggerEdge.None,
                solver.AnalogTriggers(1f, 1f, FullPull, LetGo, Vector3.zero, Vector3.forward * s.CruiseSpeed, Vector3.up, Dt));
            Assert.AreEqual(ThresherMode.Free, solver.Mode);
        }
    }
}
#endif
