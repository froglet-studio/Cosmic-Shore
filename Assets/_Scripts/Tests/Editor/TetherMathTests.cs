using System;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Tether's four promised properties, flown against the SHIPPED pure maths
    /// (<see cref="TetherMath"/>, <see cref="AutoTetherRig"/>, <see cref="LongTetherRope"/>):
    ///
    /// <list type="number">
    /// <item><b>Hooking keeps speed</b> — the long tether redirects, it never brakes.</item>
    /// <item><b>Reeling stops at the spin limit</b> — one swing pays ~1x → ~2x cruise, not the cap.</item>
    /// <item><b>The release boost applies at a half turn</b> — and not a frame before.</item>
    /// <item><b>Alternating auto-tethers hold heading</b> within a few degrees over 5 s.</item>
    /// </list>
    ///
    /// Each property that could pass vacuously has a NEGATIVE CONTROL beside it — the spin limit
    /// switched off, the auto-tethers all planted on one side — so a green run is evidence that
    /// the property is held by the thing claimed to hold it.
    ///
    /// The numbers are the shipped defaults (<c>TetherConfig.asset</c>, cruise 80 u/s, sandbox ×
    /// 80/380). Runs offline too: <c>bash Tools/Build/tether_harness/run.sh</c> compiles this
    /// file and the three shipped sources against Unity-shaped stubs and runs every [Test].
    /// </summary>
    public class TetherMathTests
    {
        const float Dt = 1f / 60f;
        const float Cruise = 80f;

        // Long tether — sandbox values × 80/380.
        const float ReelRate = 40f;
        const float AutoReel = 7.4f;
        const float MinLength = 19f;
        const float Range = 93f;
        const float MaxSpeed = 358f;
        const float MaxSpinRevPerSec = 0.6f;
        const float ReleaseBoost = 0.2f;

        // Auto-tethers.
        const float AnchorLead = 0.8f;
        const float AnchorAngle = 20f;
        const float AnchorPeriod = 0.35f;
        const float AutoSpeedTarget = 1.15f;
        const float AutoStiffness = 0.8f;
        const float AutoDamping = 4f;
        const float AutoRestFraction = 0.5f;
        const float AutoNoseGrip = 6f;

        static readonly Vector3 Forward = new Vector3(0f, 0f, 1f);
        static readonly Vector3 Right = new Vector3(1f, 0f, 0f);
        static readonly Vector3 Up = new Vector3(0f, 1f, 0f);

        static LongTetherTuning Tuning(float maxSpinRevPerSec = MaxSpinRevPerSec, float autoReel = AutoReel) => new LongTetherTuning
        {
            ReelRate = ReelRate,
            AutoReel = autoReel,
            MinLength = MinLength,
            MaxLength = Range,
            MaxSpinRadPerSec = maxSpinRevPerSec * 2f * Mathf.PI,
            MaxSpeed = MaxSpeed,
        };

        /// <summary>One frame exactly as <c>TetherVesselTransformer</c> runs it while hooked:
        /// grip off, thrust off, the rope's velocity, integrate, put the hull back on the rope.</summary>
        static void SwingFrame(LongTetherRope rope, ref Vector3 pos, ref Vector3 vel, float reelInput, in LongTetherTuning t)
        {
            vel = rope.Step(pos, vel, reelInput, t, Dt);
            pos += vel * Dt;
            pos = TetherMath.ConstrainToRope(pos, rope.Anchor, rope.Length);
            rope.Track(pos);
        }

        static float AngleDegrees(Vector3 a, Vector3 b)
            => Mathf.Atan2(Vector3.Cross(a, b).magnitude, Vector3.Dot(a, b)) * Mathf.Rad2Deg;

        // ------------------------------------------------------------------ 1. hooking keeps speed

        [Test]
        public void Hook_KeepsSpeed_AndLandsOnTheTangent()
        {
            var rng = new System.Random(7);
            for (int i = 0; i < 200; i++)
            {
                Vector3 hull = RandomVector(rng, 200f);
                Vector3 anchor = hull + RandomVector(rng, 90f);
                Vector3 vel = RandomVector(rng, 300f);
                Vector3 hooked = TetherMath.HookVelocity(hull, vel, anchor, Forward);

                Assert.AreEqual(vel.magnitude, hooked.magnitude, vel.magnitude * 1e-4f,
                    "Hooking changed the hull's speed. It must only redirect it.");
                Vector3 radial = (hull - anchor).normalized;
                Assert.AreEqual(0f, Vector3.Dot(hooked.normalized, radial), 1e-3f,
                    "A hooked velocity must lie on the tangent of the rope's circle.");
            }
        }

        [Test]
        public void Swing_WithNoReel_KeepsSpeedAndRadius()
        {
            var rope = new LongTetherRope();
            Vector3 pos = Vector3.zero;
            Vector3 vel = Forward * Cruise;
            Vector3 anchor = Right * 60f + Forward * 20f;
            vel = rope.Hook(pos, vel, anchor, Forward, 1);
            float length = rope.Length;

            var t = Tuning(autoReel: 0f);
            for (int f = 0; f < 180; f++) SwingFrame(rope, ref pos, ref vel, 0f, t);

            Assert.AreEqual(Cruise, vel.magnitude, Cruise * 1e-3f, "A rigid rope with no reel did work on the hull.");
            Assert.AreEqual(length, (pos - anchor).magnitude, length * 1e-3f, "The hull left the rope's circle.");
            Assert.Greater(rope.Swept, Mathf.PI, "Three seconds at cruise on a 63 u line should sweep more than half a turn.");
        }

        // ------------------------------------------------------------------ 2. reeling stops at the spin limit

        [Test]
        public void Reel_StopsAtTheSpinLimit()
        {
            var rope = new LongTetherRope();
            Vector3 pos = Vector3.zero;
            Vector3 vel = Forward * Cruise;
            Vector3 anchor = Right * 90f;               // dead abeam, so nothing is lost to the redirect
            vel = rope.Hook(pos, vel, anchor, Forward, 1);

            var t = Tuning();
            float maxSpin = t.MaxSpinRadPerSec;
            for (int f = 0; f < 600; f++)
            {
                SwingFrame(rope, ref pos, ref vel, 1f, t);
                Assert.LessOrEqual(rope.SpinRate(vel.magnitude), maxSpin * 1.001f,
                    $"Frame {f}: spin {TetherMath.RevolutionsPerSecond(rope.SpinRate(vel.magnitude)):F3} rev/s passed the limit.");
            }

            Assert.GreaterOrEqual(rope.SpinRate(vel.magnitude), maxSpin * 0.98f,
                "Ten seconds of full reel never reached the spin limit, so nothing here proved it binds.");
            Assert.Greater(rope.Length, MinLength * 1.5f,
                "The reel ran to the minimum length; the SPIN limit was meant to be what stopped it.");

            float ratio = vel.magnitude / Cruise;
            Assert.GreaterOrEqual(ratio, 1.8f, "One full-reel swing should roughly double cruise.");
            Assert.LessOrEqual(ratio, 2.4f, "One swing should land near 2.2x cruise, not run away toward the cap.");
        }

        [Test]
        public void Reel_WithoutTheSpinLimit_RunsToTheCap_NegativeControl()
        {
            var rope = new LongTetherRope();
            Vector3 pos = Vector3.zero;
            Vector3 vel = Forward * Cruise;
            vel = rope.Hook(pos, vel, Right * 90f, Forward, 1);

            var t = Tuning(maxSpinRevPerSec: 0f);
            for (int f = 0; f < 600; f++) SwingFrame(rope, ref pos, ref vel, 1f, t);

            Assert.Greater(vel.magnitude / Cruise, 3.5f,
                "With the spin limit off the same reel should run far past 2.2x — if it does not, " +
                "the spin-limit test above is not measuring the spin limit.");
            Assert.LessOrEqual(vel.magnitude, MaxSpeed * 1.0001f, "The speed cap must hold even with the spin limit off.");
        }

        [Test]
        public void ReelFloor_NeverPaysTheLineOut()
        {
            // The floor rises with speed. A line below it must stay put, not be lengthened.
            float length = 30f, speed = 300f;
            float floor = TetherMath.SpinLimitedLength(length, speed, 1f);
            Assert.Greater(floor, length, "Setup: the floor should sit above the line.");
            float next = TetherMath.ReelLength(length, speed, 1f, ReelRate, AutoReel, MinLength, Range, 1f, MaxSpeed, Dt);
            Assert.AreEqual(length, next, 1e-5f, "A spin floor above the line paid rope OUT.");
        }

        // ------------------------------------------------------------------ 3. release boost at a half turn

        [Test]
        public void ReleaseBoost_AppliesAtAHalfTurn_AndNotBefore()
        {
            var rope = new LongTetherRope();
            Vector3 pos = Vector3.zero;
            Vector3 vel = Forward * Cruise;
            Vector3 anchor = Right * 50f;
            vel = rope.Hook(pos, vel, anchor, Forward, 1);

            var t = Tuning(autoReel: 0f);
            bool sawBefore = false, sawAfter = false;
            for (int f = 0; f < 360; f++)
            {
                SwingFrame(rope, ref pos, ref vel, 0f, t);
                float speed = vel.magnitude;
                float exit = TetherMath.ReleaseSpeed(speed, rope.Swept, ReleaseBoost, MaxSpeed);
                if (rope.Swept < Mathf.PI)
                {
                    sawBefore = true;
                    Assert.AreEqual(speed, exit, 1e-3f, $"A boost was paid at {rope.Swept * Mathf.Rad2Deg:F0}° of swing.");
                }
                else
                {
                    sawAfter = true;
                    float expected = speed * (1f + ReleaseBoost * (1f - (speed / MaxSpeed) * (speed / MaxSpeed)));
                    Assert.AreEqual(expected, exit, 1e-3f, "Past a half turn the release boost must apply.");
                    Assert.Greater(exit, speed * 1.15f, "At ~1x cruise the boost should be close to the full 20%.");
                }
            }
            Assert.IsTrue(sawBefore && sawAfter, "The swing must cross the half turn for this test to mean anything.");
        }

        [Test]
        public void ReleaseBoost_ShrinksNearTheCap_AndNeverPassesIt()
        {
            float slow = TetherMath.ReleaseSpeed(Cruise, Mathf.PI, ReleaseBoost, MaxSpeed) / Cruise - 1f;
            float fast = TetherMath.ReleaseSpeed(MaxSpeed * 0.9f, Mathf.PI, ReleaseBoost, MaxSpeed) / (MaxSpeed * 0.9f) - 1f;
            Assert.Greater(slow, fast * 3f, "The boost should shrink sharply as speed nears the cap.");
            Assert.LessOrEqual(TetherMath.ReleaseSpeed(MaxSpeed * 0.99f, 4f, ReleaseBoost, MaxSpeed), MaxSpeed);
        }

        // ------------------------------------------------------------------ 4. auto-tethers hold heading

        struct AutoRun
        {
            public float MaxHeadingDegrees;
            public float MeanSpeedRatio;
            public float MaxSpeedRatio;
            public float MinSpeedRatio;
            public int Planted;
        }

        /// <summary>
        /// Five seconds of hands-off straight flight on auto-tethers, in the order
        /// <c>TetherVesselTransformer</c> runs a frame: (executor) let go of passed anchors and
        /// plant one if due; (transformer) nose grip, thrust toward the throttle target, the
        /// tethers' pull bounded at <c>autoSpeedTarget</c>, integrate.
        /// </summary>
        static AutoRun FlyAuto(bool alternate, float noseGrip, float seconds = 5f)
        {
            var rig = new AutoTetherRig();
            Vector3 pos = Vector3.zero;
            Vector3 vel = Forward * Cruise;
            var run = new AutoRun { MinSpeedRatio = float.MaxValue };
            float speedSum = 0f;
            int speedSamples = 0;
            int frames = (int)(seconds / Dt);

            for (int f = 0; f < frames; f++)
            {
                rig.ReleasePassed(pos, Forward);
                if (rig.TickCadence(Dt, AnchorPeriod))
                {
                    int side = alternate ? rig.NextSide : 1;
                    Vector3 anchor = TetherMath.AnchorPoint(pos, Forward, Right, vel.magnitude, AnchorLead, AnchorAngle, side);
                    rig.Attach(pos, anchor, AutoRestFraction);
                    run.Planted++;
                }

                // Grip (NoseConvergence while taut), then thrust toward cruise (StepTowardTarget's lerp).
                float speed = vel.magnitude;
                float convergence = rig.AnyTaut ? 1f - Mathf.Exp(-noseGrip * Dt) : 1f;
                vel = SlerpDirection(vel / speed, Forward, convergence) * speed;
                float along = Vector3.Dot(vel, Forward);
                vel += Forward * (Mathf.Lerp(along, Cruise, 1.5f * Dt) - along);

                Vector3 pull = rig.Solve(pos, vel, AutoStiffness, AutoDamping, Dt);
                vel += TetherMath.LimitSpeedGain(vel, pull, AutoSpeedTarget * Cruise);
                pos += vel * Dt;

                run.MaxHeadingDegrees = Mathf.Max(run.MaxHeadingDegrees, AngleDegrees(vel, Forward));
                if (f * Dt >= 1f)   // past the first second's spin-up
                {
                    float r = vel.magnitude / Cruise;
                    speedSum += r;
                    speedSamples++;
                    run.MaxSpeedRatio = Mathf.Max(run.MaxSpeedRatio, r);
                    run.MinSpeedRatio = Mathf.Min(run.MinSpeedRatio, r);
                }
            }
            run.MeanSpeedRatio = speedSamples > 0 ? speedSum / speedSamples : 0f;
            return run;
        }

        /// <summary>
        /// Held twice: at the shipped nose grip, and with NO nose grip at all — the worst case,
        /// where nothing but the alternation can hold the line. The negative control below runs
        /// that same no-grip flight with every anchor on one side, and must wander off.
        /// </summary>
        [Test]
        public void AlternatingAutoTethers_HoldHeading_OverFiveSeconds()
        {
            var run = FlyAuto(alternate: true, noseGrip: AutoNoseGrip);
            Assert.Greater(run.Planted, 10, "Five seconds at a 0.35 s period should plant ~14 anchors.");
            Assert.LessOrEqual(run.MaxHeadingDegrees, 3f,
                $"Heading wandered {run.MaxHeadingDegrees:F2}° at the shipped nose grip.");

            var ungripped = FlyAuto(alternate: true, noseGrip: 0f);
            Assert.LessOrEqual(ungripped.MaxHeadingDegrees, 3f,
                $"Heading wandered {ungripped.MaxHeadingDegrees:F2}° with no nose grip — alternating " +
                "pulls should cancel sideways on their own.");
        }

        [Test]
        public void AutoTethers_HoldSpeedALittleAboveCruise_NotForever()
        {
            var run = FlyAuto(alternate: true, noseGrip: AutoNoseGrip);
            Assert.Greater(run.MeanSpeedRatio, 1.02f,
                $"Mean {run.MeanSpeedRatio:F3}x cruise — the auto-tethers should pull the vessel above cruise.");
            Assert.LessOrEqual(run.MaxSpeedRatio, AutoSpeedTarget + 1e-3f,
                $"Peak {run.MaxSpeedRatio:F3}x — the tethers must never pull past autoSpeedTarget.");
            Assert.Greater(run.MaxSpeedRatio - run.MinSpeedRatio, 0.005f,
                "No surge at all: the rhythm the alternation is for has flattened into a constant pull.");
        }

        [Test]
        public void OneSidedAutoTethers_DriftOffHeading_NegativeControl()
        {
            var run = FlyAuto(alternate: false, noseGrip: 0f);
            Assert.Greater(run.MaxHeadingDegrees, 3f,
                $"Every pull on one side still held heading ({run.MaxHeadingDegrees:F2}°) with no nose " +
                "grip, so the ungripped heading test above is not measuring the alternation.");
        }

        // ------------------------------------------------------------------ hook scoring + plane

        [Test]
        public void HookScoring_RespectsSideBehindPlaneAndWindow()
        {
            var w = new TetherMath.HookWindow
            {
                MinDistance = 27f, MaxDistance = Range, PlaneToleranceDegrees = 20f, IdealDistance01 = 0.5f,
                SideWeight = 1f, AheadWeight = 0.6f, DistanceWeight = 0.5f, PlaneWeight = 0.3f,
            };
            Vector3 hull = Vector3.zero;
            Assert.IsTrue(TetherMath.TryScoreHook(hull, Forward, Right, Up, 1, Right * 50f + Forward * 20f, w, out _), "right side, in plane");
            Assert.IsFalse(TetherMath.TryScoreHook(hull, Forward, Right, Up, -1, Right * 50f + Forward * 20f, w, out _), "wrong side");
            Assert.IsFalse(TetherMath.TryScoreHook(hull, Forward, Right, Up, 1, Right * 50f - Forward * 20f, w, out _), "behind");
            Assert.IsFalse(TetherMath.TryScoreHook(hull, Forward, Right, Up, 1, Right * 50f + Up * 40f, w, out _), "off the plane");
            Assert.IsFalse(TetherMath.TryScoreHook(hull, Forward, Right, Up, 1, Right * 10f, w, out _), "inside minHook");
            Assert.IsFalse(TetherMath.TryScoreHook(hull, Forward, Right, Up, 1, Right * 200f, w, out _), "out of range");

            // Rolling the hull 90° turns the right side into "up": the same prism is now off-plane,
            // and one overhead is in it. That is the whole aiming control.
            Vector3 rolledRight = Up, rolledUp = -Right;
            Assert.IsFalse(TetherMath.TryScoreHook(hull, Forward, rolledRight, rolledUp, 1, Right * 50f + Forward * 20f, w, out _));
            Assert.IsTrue(TetherMath.TryScoreHook(hull, Forward, rolledRight, rolledUp, 1, Up * 50f + Forward * 20f, w, out _));
        }

        [Test]
        public void HookWindow_MovesOutwardWithSpeed()
        {
            Assert.AreEqual(1f, TetherMath.WindowScale(Cruise, Cruise, 0.5f), 1e-6f);
            Assert.AreEqual(1f, TetherMath.WindowScale(Cruise * 0.5f, Cruise, 0.5f), 1e-6f);
            Assert.AreEqual(2.5f, TetherMath.WindowScale(Cruise * 4f, Cruise, 0.5f), 1e-5f);
        }

        // ------------------------------------------------------------------ helpers

        static Vector3 RandomVector(System.Random rng, float scale)
        {
            Vector3 v;
            do
            {
                v = new Vector3((float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1), (float)(rng.NextDouble() * 2 - 1));
            } while (v.sqrMagnitude < 0.05f || v.sqrMagnitude > 1f);
            return v.normalized * (scale * (0.2f + 0.8f * (float)rng.NextDouble()));
        }

        /// <summary>Unit-vector slerp, the same rotation <c>Vector3.Slerp</c> performs on the
        /// flight model's grip step, written out so this file needs nothing beyond Vector3 maths.</summary>
        static Vector3 SlerpDirection(Vector3 from, Vector3 to, float t)
        {
            float dot = Mathf.Clamp(Vector3.Dot(from, to), -1f, 1f);
            float theta = (float)Math.Acos(dot) * Mathf.Clamp01(t);
            Vector3 ortho = to - from * dot;
            if (ortho.sqrMagnitude < 1e-12f) return t >= 1f ? to : from;
            ortho = ortho.normalized;
            return from * Mathf.Cos(theta) + ortho * Mathf.Sin(theta);
        }
    }
}
