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
            /// <summary>The nose turns with the chain's tug while towing too, as <c>ThresherVesselTransformer</c>
            /// turns the hull (off: the stick alone flies the nose, so a tug is erased on the next frame).</summary>
            public bool Grip;
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
                Move(yawDegPerSec, dt);
                return edge;
            }
            /// <summary>The move half of a frame (after the triggers).</summary>
            public void Move(float yawDegPerSec, float dt = Dt)
            {
                F = Yaw(F, yawDegPerSec * dt);
                float th = V + (Cruise - V) * 1.5f * dt;
                R = S.Step(P, F * th, false, Cruise, dt);
                if (Grip || S.Mode == ThresherMode.Pivot || R.Launched) F = R.ShipVelocity.normalized;
                V = R.ShipVelocity.magnitude;
                P += R.ShipVelocity * dt;
                Last = R.ShipVelocity;
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

        /// <summary>Plant, spin up, arm a launch along +X and fly it to its point, holding LT at
        /// <paramref name="ltAfter"/> and RT at <paramref name="rtAfter"/> from the arm on; the nose turns with the tug.</summary>
        static Flight AimedLaunch(float ltAfter, float rtAfter, float timeSpin = 1f)
        {
            var s = Analog();
            s.LockSpinRate *= timeSpin;   // the Time element's spin multiplier, as ThresherExecutor.ApplyElementScaling folds it
            s.LockMaxSpeed *= timeSpin;
            var f = new Flight(s) { Grip = true };
            f.Fly(1f, 0f, 1f, 60f);
            f.Fly(2.5f, 1f, 1f);
            f.S.ArmLaunch(Vector3.right, 4.8f);
            for (int i = 0; i < 600; i++) { f.Frame(ltAfter, rtAfter, 0f); if (f.R.Launched) return f; }
            Assert.IsTrue(false, "the armed launch never left");
            return f;
        }

        [Test]
        public void AimedLaunch_FliesTheAimLine_WhateverTheTriggersStillHold()
        {
            // RT held rigid with LT just past the let-go point used to swing the ship 177 degrees round the ball
            // left at the old pivot (Garrett, 2026-10-21: "it doesn't always shoot me at the cross hair").
            foreach (var (lt, rt) in new[] { (0.85f, 1f), (0.85f, 0f), (0.5f, 1f), (0f, 1f), (0f, 0f) })
            {
                var f = AimedLaunch(lt, rt);
                Vector3 p0 = f.P;
                float worstDeg = 0f, worstOff = 0f;
                for (int k = 0; k < 90; k++)
                {
                    f.Frame(lt, rt, 0f);
                    Vector3 d = f.P - p0;
                    worstOff = Mathf.Max(worstOff, (d - Vector3.right * Vector3.Dot(d, Vector3.right)).magnitude);
                    worstDeg = Mathf.Max(worstDeg, AngleDeg(f.Last, Vector3.right));
                }
                Assert.Less(worstDeg, 3f, $"LT {lt} RT {rt}: the heading stays on the aim");
                Assert.Less(worstOff, 10f, $"LT {lt} RT {rt}: the ship stays on the crosshair's line");
            }
        }

        [Test]
        public void AimedLaunch_OffATimeRaisedSpin_StaysOnTheLine_WhenTheShipOutrunsTheBall()
        {
            // Time maxed (x1.75): the launch leaves at ~1 300 u/s against the ball's 479 u/s cap. The rigid chain then
            // brakes the ship below the ball's speed and the ball overtakes it; letting the line go as soon as the ball
            // first trailed pulled the ship 55 degrees off the aim (30 with the along-line rule but an early end).
            var f = AimedLaunch(0f, 1f, 1.75f);
            Assert.Greater(f.R.ShipVelocity.magnitude, f.S.Settings.MaxBallSpeed * 2f, "the case under test: the launch outruns the ball");
            float worstDeg = 0f;
            for (int k = 0; k < 120; k++) { f.Frame(0f, 1f, 0f); worstDeg = Mathf.Max(worstDeg, AngleDeg(f.Last, Vector3.right)); }
            Assert.Less(worstDeg, 3f, "the heading stays on the aim through the brake and the ball's overtake");
            Assert.Greater(Vector3.Dot(f.Last, Vector3.right), 0f, "never reversed");
        }

        [Test]
        public void AimedLaunch_TheLineAlwaysEnds_AndTheHeadingHoldsAfterIt()
        {
            // RT held, the rigid chain brakes the ship below the ball's speed and the ball overtakes and leads it; RT
            // released, the slack chain swings the ball round like a pendulum. Either way the line lets go by its cap,
            // the chain then tows normally, and the ship is still on the aim a second later.
            foreach (float rt in new[] { 1f, 0f })
            {
                var f = AimedLaunch(0f, rt);
                Assert.IsTrue(f.S.OnLaunchLine, $"RT {rt}: the launch frame starts the line");
                int frames = 0;
                float worstDeg = 0f;
                for (; frames < 400 && f.S.OnLaunchLine; frames++) { f.Frame(0f, rt, 0f); worstDeg = Mathf.Max(worstDeg, AngleDeg(f.Last, Vector3.right)); }
                Assert.IsFalse(f.S.OnLaunchLine, $"RT {rt}: the line ends");
                Assert.LessOrEqual(frames * Dt, ThresherChainSolver.LaunchLineMaxSeconds + 2f * Dt, $"RT {rt}: by its cap");
                for (int k = 0; k < 60; k++) { f.Frame(0f, rt, 0f); worstDeg = Mathf.Max(worstDeg, AngleDeg(f.Last, Vector3.right)); }
                Assert.Less(worstDeg, 3f, $"RT {rt}: on the aim through the line and a second after it");
            }
        }

        [Test]
        public void AimedLaunch_LtAnchorsNothingUntilBackAtRest_AndAFullPullStillPlants()
        {
            var f = AimedLaunch(0.85f, 1f);
            Assert.IsTrue(f.S.LeftTriggerLetGo, "LT is still most of the way down when the launch fires");
            f.Fly(0.5f, 0.85f, 1f);
            Assert.IsTrue(f.S.LeftTriggerLetGo, "held short of rest, it stays let go");
            f.Frame(0.1f, 1f, 0f);
            Assert.IsFalse(f.S.LeftTriggerLetGo, "back at rest, the next pull brakes again");

            var g = AimedLaunch(0.85f, 1f);
            g.Fly(0.3f, 0.85f, 1f);
            g.Frame(1f, 1f, 0f);
            Assert.AreEqual(ThresherMode.Pivot, g.S.Mode, "a full pull plants again without waiting for rest");
            Assert.IsFalse(g.S.LeftTriggerLetGo);
            Assert.IsFalse(g.S.OnLaunchLine, "a new plant ends the line");
        }

        // ---------------------------------------------------------------- on a phone (2026-10-21)
        // Garrett: "the thresher's aimed launch should be both thumbs off and aimed with gyro". A phone's
        // triggers are thumb LIFTS (right lift = Plant, left = Winch) and read as a full pull or none; both thumbs
        // off a planted spin reads as both triggers full (ThresherTouchLaunch).

        /// <summary>The shipped <c>spectateSpinRate</c> (rad/s): the spin the launch view needs.</summary>
        const float SpectateSpinRate = 0.9f;
        /// <summary>The shipped <c>launchBoost</c>.</summary>
        const float LaunchBoost = 4.8f;

        /// <summary>One frame of a phone pilot, as <c>ThresherExecutor.PollTriggers</c> reads it: the Plant lift as a
        /// full pull or none, no winch, both through the touch hold. On the let-go edge an <paramref name="aim"/>
        /// (the launch view up) ARMS the boosted launch, as <c>ThresherExecutor.LetGo</c> does; none lets go.</summary>
        static ThresherTriggerEdge TouchFrame(Flight f, ref ThresherTouchLaunch touch, bool plantHeld,
                                              out bool grip, Vector3 aim = default)
        {
            float lt = plantHeld ? 1f : 0f, rt = 0f;
            touch.Apply(f.S.Mode, ref lt, ref rt);
            var edge = f.S.AnalogTriggers(lt, rt, FullPull, LetGo, f.P, f.Last, Vector3.up, Dt);
            if (edge == ThresherTriggerEdge.LetGo)
            {
                if (aim.sqrMagnitude > 0.5f) f.S.ArmLaunch(aim, LaunchBoost);
                else f.S.Release(f.Last);
            }
            f.Move(0f);
            grip = ThresherCameraFraming.LaunchViewGrip(true, f.S.IsBraking, lt, f.S.Rigidity, FullPull)
                   && f.S.Mode == ThresherMode.Pivot
                   && ThresherCameraFraming.SpinRate(f.S.LockSpeed, f.S.Extent) >= SpectateSpinRate;
            return edge;
        }

        /// <summary>Tow, plant on the right-thumb lift (RT released: a slack chain) and spin for 2.5 s.</summary>
        static Flight TouchPlanted(out ThresherTouchLaunch touch)
        {
            var f = new Flight(Analog()) { Grip = true };
            touch = new ThresherTouchLaunch();
            f.Fly(1f, 0f, 0f, 40f);
            for (int i = 0; i < 150; i++) TouchFrame(f, ref touch, true, out _);
            Assert.AreEqual(ThresherMode.Pivot, f.S.Mode, "the right-thumb lift plants");
            return f;
        }

        /// <summary><see cref="TouchPlanted"/>, then the last thumb leaves the glass: the Plant release and then the
        /// IdleAction press, back to back on one frame - the order TouchInputStrategy raises them in and every peer
        /// replays them in. Flown until the launch view grips (at most 1 s).</summary>
        static Flight TouchLaunchView(out ThresherTouchLaunch touch)
        {
            var f = TouchPlanted(out touch);
            touch.PlantReleased();
            touch.IdlePressed(f.S.Mode, f.S.LaunchArmed);
            Assert.IsTrue(touch.Held, "both thumbs off a planted spin hold the plant");
            for (int i = 0; i < 60; i++)
            {
                TouchFrame(f, ref touch, false, out bool grip);
                Assert.AreEqual(ThresherMode.Pivot, f.S.Mode, $"frame {i}: the Plant release ahead of the idle press never lets the ball go");
                if (grip) return f;
            }
            Assert.IsTrue(false, "both thumbs off never gripped the launch view within 1 s");
            return f;
        }

        [Test]
        public void Touch_APlantAlone_NeverGripsTheLaunchView()
        {
            // Why the aimed launch was unreachable on a phone: the Winch is the OTHER thumb's lift, so a plant is
            // always held with the chain slack.
            var f = TouchPlanted(out var touch);
            for (int i = 0; i < 120; i++)
            {
                TouchFrame(f, ref touch, true, out bool grip);
                Assert.IsFalse(grip, "a phone's plant alone holds RT released, so the chain is never rigid");
            }
        }

        [Test]
        public void Touch_BothThumbsOffAPlantedSpin_GripsTheLaunchView_AsBothTriggersFull()
        {
            var f = TouchLaunchView(out var touch);
            Assert.GreaterOrEqual(f.S.Rigidity, FullPull, "RT reads a full pull: the chain went rigid at its slew");
            Assert.IsFalse(f.S.IsBraking);
            for (int i = 0; i < 120; i++)
            {
                TouchFrame(f, ref touch, false, out bool grip);
                Assert.IsTrue(grip, $"frame {i}: the view holds while the thumbs stay off");
            }
        }

        [Test]
        public void Touch_AThumbBackDown_ArmsTheBoostedLaunch_WhichFliesTheCrosshair()
        {
            var f = TouchLaunchView(out var touch);
            float orbit = f.S.LockSpeed;
            touch.IdleReleased();
            Vector3 aim = Vector3.right;
            var edge = TouchFrame(f, ref touch, false, out _, aim);
            Assert.AreEqual(ThresherTriggerEdge.LetGo, edge, "a thumb back on the glass is the pad's LT let-go");
            Assert.IsTrue(f.S.LaunchArmed || f.R.Launched, "in the launch view the let-go arms the aimed launch");
            for (int i = 0; i < 600 && !f.R.Launched; i++) TouchFrame(f, ref touch, false, out _, aim);
            Assert.IsTrue(f.R.Launched, "the armed launch leaves at its point");
            Assert.Less(AngleDeg(f.R.ShipVelocity, aim), 0.5f, "along the crosshair");
            Assert.Greater(f.R.ShipVelocity.magnitude, orbit * 3f, "boosted (launchBoost x the orbit), as every aimed launch");
            // One thumb flies on (no lift, so no trigger): the launch line holds the heading on the aim.
            float worstDeg = 0f;
            for (int k = 0; k < 90; k++) { TouchFrame(f, ref touch, false, out _); worstDeg = Mathf.Max(worstDeg, AngleDeg(f.Last, aim)); }
            Assert.Less(worstDeg, 3f, "the heading stays on the aim");
        }

        [Test]
        public void Touch_AThumbLandingBackWithNoIdle_LetsGoAfterOnePoll()
        {
            // The right thumb back on the glass (1 -> 2 thumbs) releases the Plant with no idle press: the one-poll
            // grace waits for an idle press that never comes, then lets go on the orbit's tangent.
            var f = TouchPlanted(out var touch);
            touch.PlantReleased();
            Assert.AreEqual(ThresherTriggerEdge.None, TouchFrame(f, ref touch, false, out _));
            Assert.AreEqual(ThresherMode.Pivot, f.S.Mode, "one poll of grace");
            Assert.AreEqual(ThresherTriggerEdge.LetGo, TouchFrame(f, ref touch, false, out _));
            Assert.AreEqual(ThresherMode.Free, f.S.Mode);

            // The grace never starts a stop: it holds only a ball that is already planted.
            var g = new Flight(Analog());
            var towing = new ThresherTouchLaunch();
            g.Fly(1f, 0f, 0f, 40f);
            towing.PlantReleased();
            TouchFrame(g, ref towing, false, out _);
            Assert.AreEqual(ThresherMode.Free, g.S.Mode);
        }

        [Test]
        public void Touch_IdleOffTheTowOrAnArmedLaunch_HoldsNothing_AndAWindmillEndsTheHold()
        {
            // Both thumbs off while towing (or after the Winch lift alone) is tilt flight, not a plant.
            var f = new Flight(Analog());
            var touch = new ThresherTouchLaunch();
            f.Fly(1f, 0f, 0f, 40f);
            touch.IdlePressed(f.S.Mode, f.S.LaunchArmed);
            Assert.IsFalse(touch.Held);
            for (int i = 0; i < 60; i++) TouchFrame(f, ref touch, false, out _);
            Assert.AreEqual(ThresherMode.Free, f.S.Mode, "an idle press never starts a stop");

            // Lifting off again while an armed launch waits for its point changes nothing.
            var g = TouchLaunchView(out var armed);
            armed.IdleReleased();
            TouchFrame(g, ref armed, false, out _, Vector3.right);
            Assert.IsTrue(g.S.LaunchArmed);
            armed.IdlePressed(g.S.Mode, g.S.LaunchArmed);
            Assert.IsFalse(armed.Held, "an armed launch is not re-held");

            // A plant that ends some other way (an omni crystal's windmill) ends the hold.
            var h = TouchLaunchView(out var held);
            h.S.StartWindmill(h.P, 2f);
            float lt = 0f, rt = 0f;
            held.Apply(h.S.Mode, ref lt, ref rt);
            Assert.IsFalse(held.Held);
            Assert.AreEqual(0f, lt);
            Assert.AreEqual(0f, rt);
        }

        [Test]
        public void Touch_WithNoPhoneEvents_TheDepthsPassThroughUntouched()
        {
            // Every other device never raises the touch map's IdleAction or a phone's PlantReleased: the hold is inert.
            var touch = new ThresherTouchLaunch();
            foreach (ThresherMode mode in System.Enum.GetValues(typeof(ThresherMode)))
            foreach (float depth in new[] { 0f, 0.3f, 0.95f, 1f })
            {
                float lt = depth, rt = 1f - depth;
                touch.Apply(mode, ref lt, ref rt);
                Assert.AreEqual(depth, lt);
                Assert.AreEqual(1f - depth, rt);
            }
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
