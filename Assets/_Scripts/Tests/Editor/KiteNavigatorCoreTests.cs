#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The navigator's kite (Crew Vessel Lab round 3, signed off 2026-10-17; <c>Docs/Studios/CREW_VESSEL_LAB.md</c>): the
    /// thumb drives the kite, the tail closes loops, a loop's motes resolve to one effect, and every effect grows as
    /// <c>base × growth^(n − 1)</c>. The same core is held to the lab page frame by frame by
    /// <c>Tools/Build/kite_navigator_lab/parity.cjs</c>; these tests pin the rules a reader can state in a sentence.
    /// </summary>
    public class KiteNavigatorCoreTests
    {
        const float Dt = 1f / 60f;

        // round 3's signed-off kite (the shipped tail is 2.4 since 2026-10-18; these rules hold at any length)
        static KiteSettings Candidate() => new() { BoardRadius = 160f, Anchor = 0.3f, KiteSpeed = 320f, KiteAccel = 12f, TailLength = 1.6f };

        static KiteNavigatorCore Kite(KiteSettings s)
        {
            var k = new KiteNavigatorCore();
            k.Reset(new KiteVec(0f, s.Anchor * s.BoardRadius * 0.5f));
            return k;
        }

        [Test]
        public void Move_ThumbDown_ReachesTheAimPointAndStops()
        {
            var s = Candidate(); var k = Kite(s);
            for (int i = 0; i < 240; i++) k.Move(s, 50f, 90f, true, Dt);
            Assert.AreEqual(50f, k.Position.X, 0.5f);
            Assert.AreEqual(90f, k.Position.Z, 0.5f);
            Assert.Less(System.MathF.Sqrt(k.Velocity.X * k.Velocity.X + k.Velocity.Z * k.Velocity.Z), 2f, "the kite slows into its aim point");
        }

        [Test]
        public void Move_ThumbUp_CoastsToAStop()
        {
            var s = Candidate(); var k = Kite(s);
            for (int i = 0; i < 30; i++) k.Move(s, 120f, 48f, true, Dt);
            float moving = k.Velocity.X;
            Assert.Greater(moving, 50f);
            for (int i = 0; i < 120; i++) k.Move(s, 0f, 0f, false, Dt);
            Assert.Less(System.MathF.Abs(k.Velocity.X), moving * 0.01f, "with the thumb up the kite coasts at CoastRate and stops");
        }

        [Test]
        public void Move_AimPastTheRim_IsClampedOntoTheBoard_SoTheKiteNeverFreezes()
        {
            // L-STU-83: a target past the rim could never be reached and the kite froze there for the rest of the run
            var s = Candidate(); var k = Kite(s);
            for (int i = 0; i < 300; i++) k.Move(s, 1000f, 48f, true, Dt);
            var c = KiteNavigatorCore.Centre(s);
            float r = System.MathF.Sqrt((k.Position.X - c.X) * (k.Position.X - c.X) + (k.Position.Z - c.Z) * (k.Position.Z - c.Z));
            Assert.AreEqual(s.BoardRadius * KiteNavigatorCore.RimFraction, r, 0.5f);
            Assert.Less(System.MathF.Abs(k.Velocity.X), 1f, "it arrives at the clamped target and settles");
        }

        [Test]
        public void Tail_CircleTheThumb_ClosesALoopAroundTheCentre()
        {
            var s = Candidate(); var k = Kite(s);
            var loop = new List<KiteVec>();
            bool closed = false; KiteVec centre = default;
            for (int i = 0; i < 600 && !closed; i++)
            {
                float t = i * Dt;
                // a 35 u circle: ~220 u round, inside the 256 u tail (a 45 u one, ~257 u after the kite's lag, never reaches it)
                k.Move(s, System.MathF.Cos(t * 3f) * 35f, 48f + System.MathF.Sin(t * 3f) * 35f, true, Dt);
                if (k.AdvanceTail(s, loop)) { closed = true; centre = KiteNavigatorCore.Centroid(loop); }
            }
            Assert.IsTrue(closed, "circling the thumb crosses the tail");
            Assert.IsTrue(KiteNavigatorCore.PointInPolygon(0f, 48f, loop), "the loop encloses the circle's centre");
            Assert.AreEqual(1, k.Tail.Count, "a closed loop restarts the tail at the kite");
            Assert.AreEqual(0f, centre.X, 15f);
        }

        [Test]
        public void Tail_AStraightLine_NeverClosesALoop_AndIsTrimmedToItsLength()
        {
            var s = Candidate(); var k = Kite(s);
            var loop = new List<KiteVec>();
            for (int i = 0; i < 600; i++)
            {
                float t = i * Dt;
                k.Move(s, -120f + t * 20f, 48f - 40f + t * 8f, true, Dt);
                Assert.IsFalse(k.AdvanceTail(s, loop));
            }
            float length = 0f;
            for (int i = 1; i < k.Tail.Count; i++) { float dx = k.Tail[i].X - k.Tail[i - 1].X, dz = k.Tail[i].Z - k.Tail[i - 1].Z; length += System.MathF.Sqrt(dx * dx + dz * dz); }
            Assert.LessOrEqual(length, s.TailLength * s.BoardRadius + KiteNavigatorCore.TailStep * 2f);
        }

        [Test]
        public void Tail_ShortTail_CannotCloseALoopTooBigForIt()
        {
            // a 1.0-radius tail is 160 u: a 45 u circle (283 u round) can never reach its own start (round 3, Found 5)
            var s = Candidate(); s.TailLength = 1.0f;
            var k = Kite(s); var loop = new List<KiteVec>();
            int loops = 0;
            for (int i = 0; i < 600; i++)
            {
                float t = i * Dt;
                k.Move(s, System.MathF.Cos(t * 2f) * 45f, 48f + System.MathF.Sin(t * 2f) * 45f, true, Dt);
                if (k.AdvanceTail(s, loop)) loops++;
            }
            Assert.AreEqual(0, loops);
        }

        [Test]
        public void ResolveLoop_OneElement_FiresItAtItsCount()
        {
            Assert.IsTrue(KiteNavigatorCore.ResolveLoop(0, 0, 3, 0, KiteMixedLoopRule.Fizzle, out int e, out int n));
            Assert.AreEqual(2, e, "index 2 is Space (Element.Space - 1)");
            Assert.AreEqual(3, n);
        }

        [Test]
        public void ResolveLoop_Mixed_FizzlesUnderTheSignedOffRule()
        {
            Assert.IsFalse(KiteNavigatorCore.ResolveLoop(1, 0, 4, 0, KiteMixedLoopRule.Fizzle, out _, out _));
            Assert.IsFalse(KiteNavigatorCore.ResolveLoop(0, 0, 0, 0, KiteMixedLoopRule.Fizzle, out _, out _), "an empty loop fires nothing");
        }

        [Test]
        public void ResolveLoop_Majority_FiresTheBiggestElement_OneWeakerPerStray()
        {
            Assert.IsTrue(KiteNavigatorCore.ResolveLoop(1, 0, 4, 0, KiteMixedLoopRule.Majority, out int e, out int n));
            Assert.AreEqual(2, e);
            Assert.AreEqual(3, n);
            Assert.IsFalse(KiteNavigatorCore.ResolveLoop(2, 0, 2, 0, KiteMixedLoopRule.Majority, out _, out _), "a tie cancels to nothing");
        }

        [Test]
        public void Grow_DoublesPerMote_AndStopsAtTheCap()
        {
            Assert.AreEqual(60f, KiteNavigatorCore.Grow(60f, 1, 2f, 640f), 1e-4f);
            Assert.AreEqual(480f, KiteNavigatorCore.Grow(60f, 4, 2f, 640f), 1e-3f, "4 Space motes: MaxScale 480, a 240 u blast");
            Assert.AreEqual(640f, KiteNavigatorCore.Grow(60f, 5, 2f, 640f), 1e-4f);
            Assert.AreEqual(13.5f, KiteNavigatorCore.Grow(6f, 3, 1.5f, 240f), 1e-4f);
        }

        [Test]
        public void BlastRadius_FollowsAOEExplosion_DelayThenSinEaseToHalfMaxScale()
        {
            Assert.AreEqual(0f, KiteNavigatorCore.BlastRadius(120f, 0.1f, 0.2f, 3f));
            Assert.AreEqual(60f * System.MathF.Sin(0.5f * System.MathF.PI / 2f), KiteNavigatorCore.BlastRadius(120f, 1.7f, 0.2f, 3f), 1e-3f);
            Assert.AreEqual(60f, KiteNavigatorCore.BlastRadius(120f, 5f, 0.2f, 3f), 1e-4f);
        }

        [Test]
        public void AI_Hard_ClosesPureLoopsAroundAOneElementCluster()
        {
            var s = Candidate(); var k = Kite(s); var ai = new KiteNavigatorAI(7) { Level = KiteNavigatorAI.Skill.Hard };
            ai.Reset(k.Position);
            var motes = new List<KiteMote>();
            for (int i = 0; i < 5; i++) motes.Add(new KiteMote { X = 40f + (i % 3) * 6f, Z = 110f + (i / 3) * 6f, Element = 1, Touchable = true });
            var none = new List<KiteTarget>();
            var loop = new List<KiteVec>();
            int enclosed = 0;
            for (int i = 0; i < 600 && enclosed == 0; i++)
            {
                ai.Step(s, 2f, k, 0f, motes, none, none, false, Dt);
                k.Move(s, ai.Aim.X, ai.Aim.Z, ai.Down, Dt);
                if (k.AdvanceTail(s, loop)) foreach (var m in motes) if (KiteNavigatorCore.PointInPolygon(m.X, m.Z, loop)) enclosed++;
            }
            Assert.Greater(enclosed, 1, "the AI's thumb circles the cluster and the kite's tail closes round it");
        }
    }
}
#endif
