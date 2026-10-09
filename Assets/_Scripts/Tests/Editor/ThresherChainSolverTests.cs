#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Thresher's chain physics, flown at the SHIPPED defaults (a fresh <see cref="ThresherDials"/>,
    /// whose field initializers are the same numbers the config asset embeds).
    ///
    /// The four the vessel was specified against come first: a lazy turn stays under smash speed,
    /// a snap-then-reel crack exceeds it, the ship is never dragged below <c>minShip</c>, and the
    /// lock never stalls. The rest pin the rules a later pass is most likely to "simplify" away.
    /// The same file is also compiled and RUN offline by <c>Tools/Build/thresher_chain_harness</c>,
    /// which prints the feel table these assertions are drawn from.
    /// </summary>
    public class ThresherChainSolverTests
    {
        const float Dt = 1f / 60f;

        static ThresherChainSettings Defaults() => new ThresherDials().ToSettings();

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

            public readonly ThresherChainSolver Solver;
            readonly float _cruise;

            public Pilot(ThresherChainSettings s, float cruise)
            {
                _cruise = cruise;
                Speed = cruise;
                Solver = new ThresherChainSolver(s);
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
                    if (Solver.Mode == ThresherMode.Pivot)
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
        // The window is real: Tools/Build/thresher_chain_harness prints the whole turn x snap grid,
        // and long snaps (0.3 s+) on a short turn land well under smash. That is recorded in
        // THRESHER.md as the timing a pilot has to learn, not hidden by a friendlier test case.
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
            var solver = new ThresherChainSolver(s);
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
            Assert.AreEqual(ThresherMode.Pivot, p.Solver.Mode, "The skid must end in a pivot.");

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
            var solver = new ThresherChainSolver(s);
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
            Assert.AreEqual(ThresherMode.Pivot, solver.Mode);
            Assert.Greater(vel.magnitude, s.MinShipFraction * s.CruiseSpeed * 0.9f, "Radial entry must still orbit.");
            Assert.LessOrEqual((pos - solver.Pivot).magnitude, solver.Length + 1e-2f, "The ship stays on the chain.");
        }

        // ------------------------------------------------------------------ the rules underneath

        [Test]
        public void ReelIn_ConservesTangentialSpeedTimesRadius()
        {
            var s = Defaults();
            s.ReelSpinCap = 100f;   // isolate the conservation law from the per-change cap
            var solver = new ThresherChainSolver(s);
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
            var solver = new ThresherChainSolver(s);
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
            var solver = new ThresherChainSolver(s);
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
        public void Plough_HotterBallKeepsMore_AndASlowBallStillDestroysAtCrushKeep()
        {
            var s = Defaults();
            float atSmash = ThresherChainSolver.KeepAfterHit(s.SmashSpeed, s);
            float whiteHot = ThresherChainSolver.KeepAfterHit(s.WhiteHotSpeed, s);
            float slow = ThresherChainSolver.KeepAfterHit(s.SmashSpeed * 0.5f, s);
            Assert.AreEqual(s.PloughKeep, atSmash, 1e-5f);
            Assert.AreEqual(s.PloughKeepHot, whiteHot, 1e-5f);
            Assert.Greater(whiteHot, atSmash);
            Assert.AreEqual(s.CrushKeep, slow, 1e-5f);
            Assert.IsFalse(ThresherChainSolver.IsSmash(s.SmashSpeed * 0.99f, s));
            Assert.IsTrue(ThresherChainSolver.IsSmash(s.SmashSpeed, s));
        }

        [Test]
        public void Wrecker_SmashCostsNothing_ButACrushStillDoes()
        {
            var s = Defaults();
            Assert.AreEqual(1f, ThresherChainSolver.KeepAfterHit(s.SmashSpeed, s, wrecker: true), 1e-6f);
            Assert.AreEqual(s.CrushKeep, ThresherChainSolver.KeepAfterHit(s.SmashSpeed * 0.5f, s, wrecker: true), 1e-6f,
                "Mass 5 is about SMASHING; below smash speed the ball pays as usual.");
        }

        [Test]
        public void Bounce_ReflectsOnlyTheInwardComponent()
        {
            var s = Defaults();
            var solver = new ThresherChainSolver(s);
            solver.Reset(Vector3.zero, Vector3.forward, Vector3.zero);
            solver.BallVelocity = new Vector3(10f, 0f, -20f);          // driving into a wall facing +z
            Assert.IsTrue(solver.Bounce(new Vector3(1f, 2f, 3f), Vector3.forward));
            Assert.AreEqual(10f, solver.BallVelocity.x, 1e-4f, "The sliding component is kept.");
            Assert.AreEqual(20f * s.BounceRestitution, solver.BallVelocity.z, 1e-4f, "The inward component reflects at the restitution.");
            Assert.AreEqual(3f, solver.BallPosition.z, 1e-6f, "The ball is put back at the contact point.");
        }

        [Test]
        public void Bounce_IgnoresABallAlreadyLeaving()
        {
            var s = Defaults();
            var solver = new ThresherChainSolver(s);
            solver.Reset(Vector3.zero, Vector3.forward, Vector3.zero);
            solver.BallVelocity = new Vector3(0f, 0f, 5f);
            Assert.IsFalse(solver.Bounce(Vector3.zero, Vector3.forward),
                "A contact that lasts several frames must bounce once, not every frame.");
            Assert.AreEqual(5f, solver.BallVelocity.z, 1e-6f);
        }

        [Test]
        public void Slingshot_ReleasesTheBallAtLeastAtSmashSpeed()
        {
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Solver.Plant();
            p.Fly(0.5f, 0f, payOut: false);
            Assert.AreEqual(ThresherMode.Pivot, p.Solver.Mode);
            Vector3 slow = p.Forward * (s.CruiseSpeed * 0.5f);   // yank alone would be far under smash
            p.Solver.Release(slow, slingshot: true);
            Assert.AreEqual(s.SmashSpeed, p.Solver.BallSpeed, 1e-3f);
        }

        [Test]
        public void ChainLinks_TautChainIsStraightAndNeverLongerThanTheRope()
        {
            var links = new ThresherChainLinks(12);
            Vector3 hull = Vector3.zero, ball = new Vector3(0f, 0f, -20f);
            links.Reset(hull, ball);
            for (int f = 0; f < 30; f++) links.Step(hull, ball, 20f);
            for (int i = 0; i < links.Count; i++)
                Assert.AreEqual(0f, new Vector3(links.Points[i].x, links.Points[i].y, 0f).magnitude, 1e-3f,
                    "A taut chain lies on the hull-ball line.");

            // Slack: the hull and ball close in, the chain must not stretch past its rope length.
            Vector3 nearBall = new Vector3(0f, 0f, -8f);
            for (int f = 0; f < 30; f++) links.Step(hull, nearBall, 20f);
            float total = 0f;
            for (int i = 0; i < links.Count - 1; i++) total += (links.Points[i + 1] - links.Points[i]).magnitude;
            Assert.LessOrEqual(total, 20f + 1e-2f);
        }

        [Test]
        public void Unlock_YanksTheBallAfterTheShip()
        {
            var s = Defaults();
            var p = new Pilot(s, s.CruiseSpeed);
            p.Solver.Plant();
            p.Fly(1f, 0f, payOut: false);
            Assert.AreEqual(ThresherMode.Pivot, p.Solver.Mode);
            Vector3 shipVelocity = p.Forward * p.Speed;
            p.Solver.Release(shipVelocity);
            Assert.AreEqual(ThresherMode.Free, p.Solver.Mode);
            Assert.AreEqual(s.Yank * p.Speed, p.Solver.BallSpeed, 1e-3f);
        }

        [Test]
        public void Dials_ScaleSpeedsAndLengthsButNotRates()
        {
            var s = Defaults();
            float k = new ThresherDials().Scale;
            Assert.AreEqual(120f * k, s.RestLength, 1e-4f);
            Assert.AreEqual(700f * k, s.SmashSpeed, 1e-4f);
            Assert.AreEqual(0.35f, s.BallDrag, 1e-6f, "Drag is a rate (1/s) and must not be scaled.");
            Assert.AreEqual(2.2f * s.CruiseSpeed, s.LockMaxSpeed, 1e-4f);
        }

        // ------------------------------------------------------------ camera framing

        // The Thresher camera asset's follow offset (0, 8, -55) and a 60 deg vertical FOV at 16:9.
        const float CamHeight = 8f, CamNeutral = 55f, Margin = 1.15f, MinAhead = 6f;
        static readonly float HalfV = 30f * Mathf.Deg2Rad;
        static readonly float HalfH = Mathf.Atan(Mathf.Tan(30f * Mathf.Deg2Rad) * 16f / 9f);

        /// <summary>Does a ball of <paramref name="r"/> at <paramref name="b"/> (hull-local) sit
        /// inside the margin-shrunk view of a camera <paramref name="d"/> behind the hull and
        /// looking at it? Built from vectors (camera position, look-at basis), independently of the
        /// closed-form basis <see cref="ThresherCameraFraming.Frames"/> uses.</summary>
        static bool Framed(Vector3 b, float r, float d, float slack = 1e-3f)
        {
            Vector3 cam = new Vector3(0f, CamHeight, -d);
            Vector3 fwd = (-cam).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            Vector3 up = Vector3.Cross(fwd, right);
            Vector3 rel = b - cam;
            float depth = Vector3.Dot(rel, fwd);
            if (depth < MinAhead + r - slack) return false;
            bool side = (Mathf.Abs(Vector3.Dot(rel, right)) + r) * Margin <= Mathf.Tan(HalfH) * depth + slack;
            bool vert = (Mathf.Abs(Vector3.Dot(rel, up)) + r) * Margin <= Mathf.Tan(HalfV) * depth + slack;
            return side && vert;
        }

        [TestCase(140f, 0f, 0f)]     // let out, swung hard to the side
        [TestCase(0f, 0f, -140f)]    // let out, trailing straight behind
        [TestCase(0f, 0f, 140f)]     // let out, flung out ahead
        [TestCase(0f, 120f, -60f)]   // high and behind
        [TestCase(-90f, -60f, 30f)]  // low, left, ahead
        public void Framing_RequiredDistanceIsTheSmallestThatKeepsTheBallInFrame(float x, float y, float z)
        {
            var b = new Vector3(x, y, z);
            float r = 5.7f;
            float d = ThresherCameraFraming.RequiredDistance(b, r, CamHeight, HalfV, HalfH, Margin, MinAhead);
            Assert.IsTrue(Framed(b, r, d), $"ball {b} not framed at the required distance {d}");
            Assert.IsFalse(Framed(b, r, d - 0.5f), $"ball {b} still framed nearer than {d}: the zoom is too generous");
        }

        [Test]
        public void Framing_AReeledInBallBarelyZoomsTheCamera()
        {
            var s = Defaults();
            float r = s.BallRadius * 1.25f * 1.45f;   // rendered ball x gauge ring, at the shipped look
            for (int deg = 0; deg < 360; deg += 15)
            {
                float a = deg * Mathf.Deg2Rad;
                // A YAW turn swings the ball in the hull's plane: no zoom at all.
                float flat = ThresherCameraFraming.RequiredDistance(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * s.RestLength,
                    r, CamHeight, HalfV, HalfH, Margin, MinAhead);
                Assert.LessOrEqual(flat, CamNeutral, $"reeled-in ball at yaw {deg} deg needs {flat}: the camera would breathe on every turn");
                // A PITCH loop lifts it toward the camera's own height: a breath at most (the worst,
                // just above-behind the hull, measures +9%).
                float loop = ThresherCameraFraming.RequiredDistance(new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * s.RestLength,
                    r, CamHeight, HalfV, HalfH, Margin, MinAhead);
                Assert.LessOrEqual(loop, CamNeutral * 1.12f, $"reeled-in ball at pitch {deg} deg needs {loop}");
            }
            float full = ThresherCameraFraming.RequiredDistance(Vector3.right * s.MaxLength, r, CamHeight, HalfV, HalfH, Margin, MinAhead);
            Assert.Greater(full, CamNeutral, "control: a let-out ball swung abeam must pull the camera back");
        }

        [Test]
        public void Framing_ZoomsOutFastAndBackInSlowly_NeverOvershooting()
        {
            const float outRate = 12f, inRate = 0.7f;
            float d = CamNeutral;
            for (int i = 0; i < 15; i++) d = ThresherCameraFraming.Ease(d, 200f, outRate, inRate, Dt);   // 0.25 s
            Assert.Greater(d, CamNeutral + 0.9f * (200f - CamNeutral), "zoom-out must cover 90% in a quarter second");
            Assert.LessOrEqual(d, 200f);

            float back = 200f;
            for (int i = 0; i < 15; i++) back = ThresherCameraFraming.Ease(back, CamNeutral, outRate, inRate, Dt);
            Assert.Greater(back, 200f - 0.2f * (200f - CamNeutral), "zoom-in must take its time: under 20% in a quarter second");
            Assert.GreaterOrEqual(back, CamNeutral);

            Assert.AreEqual(80f, ThresherCameraFraming.Ease(80f, 300f, outRate, inRate, 0f), 1e-6f, "dt 0 (a paused frame) must not move it");
        }

        [Test]
        public void Spectate_VantageFramesTheOrbitFromTheTiltedAxis()
        {
            Vector3 pivot = new Vector3(10f, -4f, 30f);
            Vector3 normal = Vector3.up, outward = Vector3.right;
            float radius = 40f, tilt = 25f;
            Vector3 p = ThresherCameraFraming.SpectatePosition(pivot, normal, outward, radius, HalfV, tilt, Margin);
            Vector3 off = p - pivot;
            Assert.AreEqual(radius * Margin / Mathf.Tan(HalfV), off.magnitude, 1e-3f, "the whole orbit circle fits the narrower FOV");
            float fromAxis = Mathf.Acos(Mathf.Clamp(Vector3.Dot(off.normalized, normal), -1f, 1f)) / Mathf.Deg2Rad;
            Assert.AreEqual(tilt, fromAxis, 1e-2f);
            Assert.Greater(Vector3.Dot(off, outward), 0f, "it leans toward the side the ship detached on");
        }

        [Test]
        public void Spectate_ShippedThresholdCatchesATightPlantButNotALongCruisingOne()
        {
            // spectateSpinRate ships at 1.8 rad/s (ThresherConfigSO); this pins what that means.
            const float threshold = 1.8f;
            var s = Defaults();
            Assert.Greater(ThresherCameraFraming.SpinRate(s.CruiseSpeed, s.RestLength), threshold,
                "planting reeled in at cruise is already a dizzying spin");
            Assert.Less(ThresherCameraFraming.SpinRate(s.LockMaxSpeed, s.MaxLength), threshold,
                "a fully let-out orbit at its cap is slow enough to ride");
            Assert.AreEqual(0f, ThresherCameraFraming.SpinRate(50f, 0f), 0f);
        }
    }
}
#endif
