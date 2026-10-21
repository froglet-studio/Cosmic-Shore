#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Sling (Wrasse Lasso Lab round 1; Garrett, 2026-10-20: "Sling is excellent. bring it to unity"): the thumb holds
    /// the kite on an invisible rope. <c>Tools/Build/kite_navigator_lab/sling_parity.cjs</c> holds
    /// <see cref="KiteNavigatorCore.Sling"/> to the lab page frame by frame; these pin the rules a reader can state.
    /// </summary>
    public class KiteNavigatorSlingTests
    {
        const float Dt = 1f / 60f;

        // the shipped asset's sling (KiteNavigatorConfig.asset), on a board centred at the origin
        static KiteSettings Shipped() => new()
        {
            BoardRadius = 160f, Anchor = 0f, KiteSpeed = 320f, KiteAccel = 12f, TailLength = 4.8f,
            SlingRope = 42f, SlingStiffness = 260f, SlingDamping = 5f, SlingDrag = 0.35f, SlingFreeDrag = 0.9f, SlingMaxSpeed = 364f,
        };

        static KiteNavigatorCore Kite() { var k = new KiteNavigatorCore(); k.Reset(new KiteVec(0f, 0f)); return k; }

        static float Speed(KiteNavigatorCore k) => MathF.Sqrt(k.Velocity.X * k.Velocity.X + k.Velocity.Z * k.Velocity.Z);

        [Test]
        public void Sling_SlackRope_DoesNotPull()
        {
            var s = Shipped(); var k = Kite();
            k.Sling(s, 20f, 0f, true, Dt);   // the hand 20 u away: inside the 42 u rope
            Assert.AreEqual(0f, Speed(k), 1e-5f, "a rope pulls only when stretched");
        }

        // The whirl the lab's AI spins (WrasseLassoLab.html aiInput 'sling'): the hand makes a small circle that LEADS
        // the kite by a quarter turn round the centre, so the rope pulls it forward round the circle.
        static void Whirl(KiteSettings s, KiteNavigatorCore k, int frames, List<KiteVec> loop, ref int loops, ref float radius)
        {
            for (int i = 0; i < frames; i++)
            {
                float d = MathF.Max(1e-3f, MathF.Sqrt(k.Position.X * k.Position.X + k.Position.Z * k.Position.Z));
                float hx = -k.Position.Z / d * 10f, hz = k.Position.X / d * 10f;   // 10 u from the centre, a quarter turn ahead
                k.Sling(s, hx, hz, true, Dt);
                if (loop != null && k.AdvanceTail(s, loop)) loops++;
                if (i > frames / 2) radius = MathF.Max(radius, d);
            }
        }

        [Test]
        public void Sling_TowedHand_PullsTheKiteAlong_NeverFartherThanTheRope()
        {
            var s = Shipped(); var k = Kite();
            for (int i = 0; i < 360; i++)
            {
                float hx = -100f + i * Dt * 40f;
                k.Sling(s, hx, 0f, true, Dt);
                // the first 1.5 s is the yank from the kite's start 100 u away (the 364 u/s cap settles it in ~90 frames)
                if (i > 90) Assert.LessOrEqual(MathF.Abs(k.Position.X - hx), s.SlingRope + 10f, $"frame {i}: the rope stretched far past its length");
            }
            Assert.Greater(k.Position.X, 60f, "the towed kite came along with the hand");
        }

        [Test]
        public void Sling_ALeadingHandCircle_WhirlsTheKiteWide_AndClosesLoops()
        {
            var s = Shipped(); var k = new KiteNavigatorCore(); k.Reset(new KiteVec(60f, 0f));
            var loop = new List<KiteVec>();
            int loops = 0; float radius = 0f;
            Whirl(s, k, 900, loop, ref loops, ref radius);
            Assert.Greater(radius, 30f, "a 10 u hand circle whirls the kite three times as wide and more");
            Assert.Greater(loops, 0, "the whirl closes lasso loops");
        }

        [Test]
        public void Sling_LetGo_FliesFreeAndSlows()
        {
            var s = Shipped(); var k = new KiteNavigatorCore(); k.Reset(new KiteVec(60f, 0f));
            int loops = 0; float radius = 0f;
            Whirl(s, k, 600, null, ref loops, ref radius);
            float v0 = Speed(k);
            Assert.Greater(v0, 50f, "the whirl had the kite moving");
            for (int i = 0; i < 6; i++) k.Sling(s, 999f, 999f, false, Dt);   // a lifted thumb: the hand's position is ignored
            Assert.AreEqual(v0 * MathF.Exp(-s.SlingFreeDrag * 6f * Dt), Speed(k), v0 * 0.02f, "free flight slows at slingFreeDrag");
        }

        [Test]
        public void Sling_NeverLeavesTheBoard_AndNeverExceedsItsTopSpeed()
        {
            var s = Shipped(); var k = Kite();
            for (int i = 0; i < 600; i++)
            {
                k.Sling(s, (i / 60) % 2 == 0 ? 400f : -400f, 0f, true, Dt);
                float r = MathF.Sqrt(k.Position.X * k.Position.X + k.Position.Z * k.Position.Z);
                Assert.LessOrEqual(r, s.BoardRadius * KiteNavigatorCore.RimFraction + 1e-3f, $"frame {i}: off the board");
                Assert.LessOrEqual(Speed(k), s.SlingMaxSpeed * (1f + KiteNavigatorCore.RimBounce) + 1e-3f, $"frame {i}: too fast");
            }
        }

        // ── The AI holds the kite the same way (Garrett, 2026-10-20: "the gamepad and AI should be consistent with mouse
        //    and touch"): its aim is the hand on the rope, and the kite is driven by Sling ──

        static KiteNavigatorAI SlingAI(KiteSettings s) =>
            new KiteNavigatorAI(7) { Level = KiteNavigatorAI.Skill.Hard, Control = KiteControl.Sling, SlingRope = s.SlingRope };

        static readonly List<KiteTarget> None = new();

        [Test]
        public void AI_OnTheSling_WhirlsALoopRoundAOneElementCluster()
        {
            var s = Shipped(); var k = Kite(); var ai = SlingAI(s);
            ai.Reset(k.Position);
            var motes = new List<KiteMote>();
            float[] ox = { 0f, 8f, -7f, 4f, -3f }, oz = { 0f, 5f, 3f, -8f, -6f };
            for (int i = 0; i < 5; i++) motes.Add(new KiteMote { X = 40f + ox[i], Z = 60f + oz[i], Element = 3, Touchable = true });
            var loop = new List<KiteVec>();
            bool caught = false;
            for (int i = 0; i < 900 && !caught; i++)
            {
                ai.Step(s, 2f, k, 0f, motes, None, None, false, Dt);
                k.Sling(s, ai.Aim.X, ai.Aim.Z, ai.Down, Dt);
                if (!k.AdvanceTail(s, loop)) continue;
                ai.OnLoopClosed();
                foreach (var m in motes) if (KiteNavigatorCore.PointInPolygon(m.X, m.Z, loop)) caught = true;
            }
            Assert.IsTrue(caught, "within 15 s the slinging AI closes a loop round the cluster");
        }

        [Test]
        public void AI_OnTheSling_DragsTheKiteThroughAGoal()
        {
            var s = Shipped(); var k = Kite(); var ai = SlingAI(s);
            ai.Reset(k.Position);
            ai.HasGoal = true;
            ai.Goal = new KiteVec(-60f, 90f);
            var motes = new List<KiteMote>();
            float nearest = float.MaxValue;
            for (int i = 0; i < 240; i++)
            {
                ai.Step(s, 2f, k, 0f, motes, None, None, false, Dt);
                k.Sling(s, ai.Aim.X, ai.Aim.Z, ai.Down, Dt);
                nearest = MathF.Min(nearest, MathF.Sqrt((k.Position.X + 60f) * (k.Position.X + 60f) + (k.Position.Z - 90f) * (k.Position.Z - 90f)));
            }
            Assert.Less(nearest, 12f, "within 4 s the rope drags the kite through the goal (a ring's mouth is 36-60 u)");
        }

        [Test]
        public void Sling_RegrabAfterLettingGo_DoesNotKickTheKite()
        {
            // the hand's velocity damps the stretch; a fresh grab must not read the jump from the last grab as hand speed
            var s = Shipped(); var k = Kite();
            k.Sling(s, 0f, 0f, true, Dt);
            for (int i = 0; i < 30; i++) k.Sling(s, 0f, 0f, false, Dt);
            k.Sling(s, 41f, 0f, true, Dt);   // grab again 41 u away, inside the rope
            Assert.AreEqual(0f, Speed(k), 1e-5f);
        }

        // --- the whole screen (round 7; Garrett, 2026-10-20: "Let the wrasse travel the whole screen space") ---------------

        // a 16:9 landscape screen at the shipped view scale: 1.352 x 160 u deep, that times the aspect wide, less the kite
        static KiteSettings Screen()
        {
            var s = Shipped();
            s.HalfDepth = 160f * 1.352f - 8f;
            s.HalfWidth = 160f * 1.352f * 16f / 9f - 8f;
            return s;
        }

        [Test]
        public void Screen_TheCornersAreReachable_PastTheBoardsDisc()
        {
            var s = Screen();
            Assert.IsTrue(s.Rectangular);
            var corner = KiteNavigatorCore.OnBoard(s, 1e4f, 1e4f);
            Assert.AreEqual(s.HalfWidth * KiteNavigatorCore.RimFraction, corner.X, 1e-3f);
            Assert.AreEqual(s.HalfDepth * KiteNavigatorCore.RimFraction, corner.Z, 1e-3f);
            Assert.Greater(MathF.Sqrt(corner.X * corner.X + corner.Z * corner.Z), s.BoardRadius, "the corner lies outside the old disc");
            Assert.IsTrue(KiteNavigatorCore.InBounds(s, corner.X, corner.Z));
            Assert.IsFalse(KiteNavigatorCore.InDisc(s, corner.X, corner.Z));
            Assert.IsFalse(KiteNavigatorCore.InBounds(s, s.HalfWidth + 1f, 0f));
            Assert.AreEqual(MathF.Sqrt(s.HalfWidth * s.HalfWidth + s.HalfDepth * s.HalfDepth), KiteNavigatorCore.BoundsReach(s), 1e-3f);
        }

        [Test]
        public void Screen_TheKitesFlightBounds_ReachPastEveryEdge()
        {
            // round 8b (KiteNavigator.FlightSettings, Garrett 2026-10-21: "the wrasse should only be limited by the tether"):
            // the kite flies the screen's rectangle plus kiteOffscreenReach on every side
            var s = Screen();
            const float reach = 240f;
            float edgeX = 160f * 1.352f * 16f / 9f, edgeZ = 160f * 1.352f;
            s.HalfWidth += reach;
            s.HalfDepth += reach;
            foreach (var (x, z) in new[] { (1e4f, 0f), (-1e4f, 0f), (0f, 1e4f), (0f, -1e4f) })
            {
                var p = KiteNavigatorCore.OnBoard(s, x, z);
                Assert.Greater(MathF.Max(MathF.Abs(p.X) - edgeX, MathF.Abs(p.Z) - edgeZ), 100f, $"toward ({x}, {z}) the kite stops on the screen");
            }

            // a kite slung at the side edge sails past it
            var k = Kite();
            k.Reset(new KiteVec(edgeX - 20f, 0f));
            k.Velocity = new KiteVec(s.SlingMaxSpeed, 0f);
            for (int i = 0; i < 20; i++) k.Sling(s, 0f, 0f, false, Dt);
            Assert.Greater(k.Position.X, edgeX + 50f, "the screen's edge walled the kite in");
        }

        [Test]
        public void Screen_NoRectangle_KeepsTheDisc()
        {
            var s = Shipped();
            Assert.IsFalse(s.Rectangular);
            var p = KiteNavigatorCore.OnBoard(s, 1e4f, 0f);
            Assert.AreEqual(s.BoardRadius * KiteNavigatorCore.RimFraction, p.X, 1e-3f);
            Assert.AreEqual(s.BoardRadius, KiteNavigatorCore.BoundsReach(s), 1e-3f);
        }

        [Test]
        public void Screen_AFlungKite_BouncesOffASideEdge_OnItsOwnAxis()
        {
            var s = Screen(); var k = Kite();
            k.Velocity = new KiteVec(s.SlingMaxSpeed, 50f);
            float hx = s.HalfWidth * KiteNavigatorCore.RimFraction, hz = s.HalfDepth * KiteNavigatorCore.RimFraction;
            bool bounced = false;
            for (int i = 0; i < 240; i++)
            {
                k.Sling(s, 0f, 0f, false, Dt);   // let go: it flies free
                Assert.LessOrEqual(MathF.Abs(k.Position.X), hx + 1e-3f, $"frame {i}: left the screen sideways");
                Assert.LessOrEqual(MathF.Abs(k.Position.Z), hz + 1e-3f, $"frame {i}: left the screen lengthwise");
                if (!bounced && k.Velocity.X < 0f)
                {
                    bounced = true;
                    Assert.Greater(k.Velocity.Z, 0f, "a side edge reverses x only");
                }
            }
            Assert.IsTrue(bounced, "the kite never reached the side edge");
        }
    }
}
#endif
