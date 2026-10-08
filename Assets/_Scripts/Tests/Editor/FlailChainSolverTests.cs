#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Flail's chain physics, flown at the SHIPPED defaults (a fresh <see cref="FlailDials"/>,
    /// whose field initializers are the same numbers the config asset embeds).
    ///
    /// The four the vessel was specified against come first: a lazy turn stays under smash speed,
    /// a snap-then-reel crack exceeds it, the ship is never dragged below <c>minShip</c>, and the
    /// lock never stalls. The rest pin the rules a later pass is most likely to "simplify" away.
    /// The same file is also compiled and RUN offline by <c>Tools/Build/flail_chain_harness</c>,
    /// which prints the feel table these assertions are drawn from.
    /// </summary>
    public class FlailChainSolverTests
    {
        const float Dt = 1f / 60f;

        static FlailChainSettings Defaults() => new FlailDials().ToSettings();

        /// <summary>A minimal stand-in for the flight model: the ship flies along its nose at a
        /// speed that eases toward <paramref name="cruise"/> exactly like the vector model's nose
        /// step (<c>LERP_AMOUNT</c> 1.5/s), so any speed the rope takes off it has to be earned
        /// back by thrust, as in the game.</summary>
        sealed class Pilot
        {
            public Vector3 Position;
            public Vector3 Forward = Vector3.forward;
            public float Speed;
            public float MaxBallSpeed;
            public float MinShipSpeedSeen = float.MaxValue;
            public int Cracks;

            public readonly FlailChainSolver Solver;
            readonly float _cruise;

            public Pilot(FlailChainSettings s, float cruise)
            {
                _cruise = cruise;
                Speed = cruise;
                Solver = new FlailChainSolver(s);
                Solver.Reset(Position, Forward, Forward * Speed);
            }

            /// <summary>Fly <paramref name="seconds"/> yawing at <paramref name="yawDegPerSec"/>
            /// (positive = right), right trigger <paramref name="payOut"/>.</summary>
            public void Fly(float seconds, float yawDegPerSec, bool payOut)
            {
                int steps = Mathf.Max(1, Mathf.RoundToInt(seconds / Dt));
                for (int i = 0; i < steps; i++)
                {
                    Forward = Rotate(Forward, yawDegPerSec * Dt);
                    float thrust = Speed + (_cruise - Speed) * (1.5f * Dt);
                    var r = Solver.Step(Position, Forward * thrust, payOut, _cruise, Dt);
                    if (Solver.Mode == FlailMode.Pivot)
                        Forward = r.ShipVelocity.normalized;   // the transformer faces the orbit tangent
                    Speed = r.ShipVelocity.magnitude;
                    Position += r.ShipVelocity * Dt;
                    if (r.Cracked) Cracks++;
                    MaxBallSpeed = Mathf.Max(MaxBallSpeed, Solver.BallSpeed);
                    MinShipSpeedSeen = Mathf.Min(MinShipSpeedSeen, Speed);
                }
            }

            static Vector3 Rotate(Vector3 v, float degrees)
            {
                float a = degrees * Mathf.Deg2Rad;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                return new Vector3(v.x * c + v.z * s, v.y, -v.x * s + v.z * c);
            }
        }

        // ------------------------------------------------------------------ the four specified

        [Test]
        public void LazyTurn_StaysUnderSmashSpeed()
        {
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Fly(1f, 0f, payOut: false);
            p.Fly(4f, 30f, payOut: false);
            Assert.Less(p.MaxBallSpeed, s.SmashSpeed,
                $"A lazy 30 deg/s turn on a reeled chain must not smash: peak {p.MaxBallSpeed:F1} vs smash {s.SmashSpeed:F1}.");
        }

        // Hard turn (120 deg/s, the Squirrel-derived hull's full-stick rate) for turnSeconds, then a
        // short snap back. The snap ALONE does not reach smash speed — the negative control below
        // pins that — so what these cases prove is that the RELEASE (the reel-in) is the crack.
        // The window is real: Tools/Build/flail_chain_harness prints the whole turn x snap grid,
        // and long snaps (0.3 s+) on a short turn land well under smash. That is recorded in
        // FLAIL.md as the timing a pilot has to learn, not hidden by a friendlier test case.
        [TestCase(0.8f, 0.1f)]
        [TestCase(1.2f, 0.1f)]
        [TestCase(1.2f, 0.2f)]
        public void SnapThenReel_CrackExceedsSmashSpeed(float turnSeconds, float snapSeconds)
        {
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Fly(1.0f, 0f, payOut: true);                 // wind up: chain out
            p.Fly(turnSeconds, 120f, payOut: true);        // hard turn one way...
            p.Fly(snapSeconds, -120f, payOut: true);       // ...snap back
            float beforeReel = p.MaxBallSpeed;
            p.Fly(0.6f, 0f, payOut: false);                // release: the reel-in is the crack
            Assert.Greater(p.MaxBallSpeed, s.SmashSpeed,
                $"A snap plus reel-in must crack past smash speed: peak {p.MaxBallSpeed:F1} vs smash {s.SmashSpeed:F1} " +
                $"(before the reel {beforeReel:F1}).");
        }

        [TestCase(0.8f, 0.1f)]
        [TestCase(1.2f, 0.1f)]
        [TestCase(1.2f, 0.2f)]
        public void SnapWithoutReel_StaysUnderSmashSpeed(float turnSeconds, float snapSeconds)
        {
            // Negative control for the case above: the same turn and snap with the trigger still
            // HELD must not smash, or the crack test would be passing on the turn alone.
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Fly(1.0f, 0f, payOut: true);
            p.Fly(turnSeconds, 120f, payOut: true);
            p.Fly(snapSeconds, -120f, payOut: true);
            Assert.Less(p.MaxBallSpeed, s.SmashSpeed, $"peak {p.MaxBallSpeed:F1} without a reel");
        }

        [Test]
        public void Ship_NeverDraggedBelowMinShip()
        {
            var s = Defaults();
            float floor = s.MinShipFraction * s.CruiseSpeed;

            // A deliberately violent sequence: a ball flung hard backwards, then whips both ways
            // with the chain going out and in.
            var p = new Pilot(s, s.CruiseSpeed);
            p.Solver.BallVelocity = Vector3.back * s.MaxBallSpeed;
            p.Fly(0.5f, 0f, payOut: true);
            p.Fly(0.5f, 120f, payOut: true);
            p.Fly(0.5f, -120f, payOut: false);
            p.Solver.BallVelocity = -p.Forward * s.MaxBallSpeed;
            p.Fly(1.0f, 60f, payOut: false);

            Assert.GreaterOrEqual(p.MinShipSpeedSeen, floor - 1e-3f,
                $"The rope dragged the ship to {p.MinShipSpeedSeen:F2} u/s, under the floor {floor:F2}.");
        }

        [Test]
        public void Ship_FloorNeverSpeedsUpAPilotWhoWasAlreadySlower()
        {
            var s = Defaults();
            var solver = new FlailChainSolver(s);
            Vector3 before = Vector3.forward * 5f;
            Vector3 after = solver.ApplyShipFloor(before, Vector3.forward * 2f, s.CruiseSpeed);
            Assert.AreEqual(5f, after.magnitude, 1e-4f,
                "The floor is min(own speed, minShip x cruise): a pilot crawling at 5 by choice is held at 5, not lifted to the floor.");
        }

        [TestCase(0f, false)]
        [TestCase(0f, true)]
        [TestCase(0.25f, false)]
        [TestCase(1f, false)]
        [TestCase(1f, true)]
        public void Lock_NeverStalls(float entrySpeedFraction, bool holdPayOut)
        {
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Fly(0.5f, 0f, payOut: false);
            p.Speed = entrySpeedFraction * s.CruiseSpeed;

            p.Solver.Plant();
            p.Fly(s.SkidSeconds + 0.05f, 0f, payOut: holdPayOut);
            Assert.AreEqual(FlailMode.Pivot, p.Solver.Mode, "The skid must end in a pivot.");

            float lastLock = p.Solver.LockSpeed;
            for (int second = 0; second < 4; second++)
            {
                p.Fly(1f, 0f, payOut: holdPayOut);
                Assert.Greater(p.Speed, 0.5f * s.MinShipFraction * s.CruiseSpeed,
                    $"Orbit speed collapsed to {p.Speed:F2} after {second + 1}s (entry {entrySpeedFraction}, payOut {holdPayOut}).");
                if (!holdPayOut)
                    Assert.GreaterOrEqual(p.Solver.LockSpeed, lastLock - 1e-3f,
                        "With the chain winding in, the orbit only ever speeds up.");
                lastLock = p.Solver.LockSpeed;
            }
        }

        [Test]
        public void Lock_FlyingStraightAtTheBall_StillOrbits()
        {
            // The stall case: no tangential speed at all at the moment of hooking on.
            var s = Defaults();
            var solver = new FlailChainSolver(s);
            solver.Reset(Vector3.zero, Vector3.forward, Vector3.zero);
            solver.BallPosition = new Vector3(0f, 0f, s.RestLength);   // dead ahead
            solver.BallVelocity = Vector3.zero;
            solver.Plant();

            Vector3 pos = Vector3.zero;
            Vector3 vel = Vector3.forward * s.CruiseSpeed;
            for (int i = 0; i < 120; i++)
            {
                var r = solver.Step(pos, vel, false, s.CruiseSpeed, Dt);
                vel = r.ShipVelocity;
                pos += vel * Dt;
            }
            Assert.AreEqual(FlailMode.Pivot, solver.Mode);
            Assert.Greater(vel.magnitude, s.MinShipFraction * s.CruiseSpeed * 0.9f, "Radial entry must still orbit.");
            Assert.LessOrEqual((pos - solver.Pivot).magnitude, solver.Length + 1e-2f, "The ship stays on the chain.");
        }

        // ------------------------------------------------------------------ the rules underneath

        [Test]
        public void ReelIn_ConservesTangentialSpeedTimesRadius()
        {
            var s = Defaults();
            s.ReelSpinCap = 100f;   // isolate the conservation law from the per-change cap
            var solver = new FlailChainSolver(s);
            solver.Reset(Vector3.zero, Vector3.forward, Vector3.zero);
            float r0 = s.MaxLength;
            solver.BallPosition = new Vector3(0f, 0f, -r0);
            solver.BallVelocity = new Vector3(20f, 0f, 0f);   // purely tangential
            solver.ApplyLengthChange(Vector3.zero, Vector3.zero, r0, s.RestLength);
            Assert.AreEqual(20f * r0, solver.BallVelocity.magnitude * s.RestLength, 1e-2f);
        }

        [Test]
        public void ReelIn_SpinMultiplierIsCappedPerChange()
        {
            var s = Defaults();
            var solver = new FlailChainSolver(s);
            solver.Reset(Vector3.zero, Vector3.forward, Vector3.zero);
            solver.BallPosition = new Vector3(0f, 0f, -s.MaxLength);
            solver.BallVelocity = new Vector3(20f, 0f, 0f);
            solver.ApplyLengthChange(Vector3.zero, Vector3.zero, s.MaxLength, s.RestLength);
            Assert.AreEqual(20f * s.ReelSpinCap, solver.BallVelocity.magnitude, 1e-3f);
        }

        [Test]
        public void PayOut_SpendsSpinBySqrtOfLengthRatio()
        {
            var s = Defaults();
            var solver = new FlailChainSolver(s);
            solver.Reset(Vector3.zero, Vector3.forward, Vector3.zero);
            float l0 = s.RestLength, l1 = s.RestLength * 2f;
            solver.BallPosition = new Vector3(0f, 0f, -l0);
            solver.BallVelocity = new Vector3(20f, 0f, 0f);
            solver.ApplyLengthChange(Vector3.zero, Vector3.zero, l0, l1);
            Assert.AreEqual(20f * Mathf.Sqrt(l0 / l1), solver.BallVelocity.magnitude, 1e-3f);
        }

        [Test]
        public void TautChain_DoesNotCrackEveryFrame()
        {
            // Towing straight, the chain rides taut. A crack must need slack first, or the ball
            // would gain a free kick every frame it is towed.
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Fly(3f, 0f, payOut: false);
            Assert.LessOrEqual(p.Cracks, 1, $"A straight tow cracked {p.Cracks} times.");
            Assert.Less(p.MaxBallSpeed, s.SmashSpeed);
        }

        [Test]
        public void BallSpeed_NeverExceedsTheCap()
        {
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Solver.BallVelocity = Vector3.right * s.MaxBallSpeed * 3f;
            p.Fly(0.1f, 0f, payOut: false);
            Assert.LessOrEqual(p.MaxBallSpeed, s.MaxBallSpeed + 1e-3f);
        }

        [Test]
        public void Plough_HotterBallKeepsMore_AndChipsKeepChipKeep()
        {
            var s = Defaults();
            float atSmash = FlailChainSolver.KeepAfterHit(s.SmashSpeed, s);
            float whiteHot = FlailChainSolver.KeepAfterHit(s.WhiteHotSpeed, s);
            float slow = FlailChainSolver.KeepAfterHit(s.SmashSpeed * 0.5f, s);
            Assert.AreEqual(s.PloughKeep, atSmash, 1e-5f);
            Assert.AreEqual(s.PloughKeepHot, whiteHot, 1e-5f);
            Assert.Greater(whiteHot, atSmash);
            Assert.AreEqual(s.ChipKeep, slow, 1e-5f);
            Assert.IsFalse(FlailChainSolver.IsSmash(s.SmashSpeed * 0.99f, s));
            Assert.IsTrue(FlailChainSolver.IsSmash(s.SmashSpeed, s));
        }

        [Test]
        public void Unlock_YanksTheBallAfterTheShip()
        {
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Solver.Plant();
            p.Fly(1f, 0f, payOut: false);
            Assert.AreEqual(FlailMode.Pivot, p.Solver.Mode);
            Vector3 shipVelocity = p.Forward * p.Speed;
            p.Solver.Release(shipVelocity);
            Assert.AreEqual(FlailMode.Free, p.Solver.Mode);
            Assert.AreEqual(s.Yank * p.Speed, p.Solver.BallSpeed, 1e-3f);
        }

        [Test]
        public void Dials_ScaleSpeedsAndLengthsButNotRates()
        {
            var s = Defaults();
            float k = new FlailDials().Scale;
            Assert.AreEqual(120f * k, s.RestLength, 1e-4f);
            Assert.AreEqual(700f * k, s.SmashSpeed, 1e-4f);
            Assert.AreEqual(0.35f, s.BallDrag, 1e-6f, "Drag is a rate (1/s) and must not be scaled.");
            Assert.AreEqual(2.2f * s.CruiseSpeed, s.LockMaxSpeed, 1e-4f);
        }
    }
}
#endif
