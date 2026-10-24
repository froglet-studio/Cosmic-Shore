using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Holds <see cref="FleaMath"/> to the Vessel Studio page it was ported from. Every golden number below is the page's
    /// OWN Flea code (studio-kinds.js <c>K.ricochet</c>, on Ys-bleeding-edge) run in node on the same inputs:
    /// <c>node Tools/Build/flea_harness/goldens.cjs &lt;studio-kinds.js&gt; &lt;three.min.js&gt;</c>. Runs headless with
    /// <c>bash Tools/Build/flea_harness/run.sh</c> (<c>--self-test</c> plants two defects these tests must catch).
    /// </summary>
    [TestFixture]
    public class FleaMathTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector3 Nose = new Vector3(0f, 0f, -1f);
        static readonly FleaMath.Params P = FleaMath.Params.Studio;

        static void Near(Vector3 expected, Vector3 actual, float tol, string what)
        {
            Assert.AreEqual(expected.x, actual.x, tol, what + ".x");
            Assert.AreEqual(expected.y, actual.y, tol, what + ".y");
            Assert.AreEqual(expected.z, actual.z, tol, what + ".z");
        }

        [Test]
        public void Crawl_FromRest_EasesUpToCrawlSpeed_AndTheVelocityGripsTowardTheNose()
        {
            var s = FleaMath.State.Start(new Vector3(1f, 0f, 0f), 0f);
            for (int i = 1; i <= 60; i++)
            {
                FleaMath.Step(ref s, P, Nose, 0f, 0f, i * Dt, 1f, Dt);
                if (i == 30) { Assert.AreEqual(31.606028f, s.speed, 1e-3f, "speed at frame 30"); Near(new Vector3(0.594153f, 0f, -0.804352f), s.course, 1e-4f, "course at frame 30"); }
            }
            Assert.AreEqual(43.233236f, s.speed, 1e-3f, "speed at frame 60");
            Near(new Vector3(0.216266f, 0f, -0.976334f), s.course, 1e-4f, "course at frame 60");
        }

        [Test]
        public void Crouch_ChargesOverHopCharge_AndTheReleaseHopsAtTheChargedSpeed()
        {
            var s = FleaMath.State.Start(Nose, 80f);
            for (int i = 1; i <= 24; i++) FleaMath.Step(ref s, P, Nose, 1f, 0f, 10f + i * Dt, 1f, Dt);
            Assert.AreEqual(0.666667f, s.charge, 1e-4f, "charge after 0.4 s");
            Assert.AreEqual(28.123274f, s.speed, 1e-3f, "crouching speed");
            var ev = FleaMath.Step(ref s, P, Nose, 0f, 0f, 10f + 25 * Dt, 1f, Dt);
            Assert.AreEqual(FleaMath.Event.Hop, ev);
            Assert.AreEqual(126.666667f, s.speed, 1e-3f, "hop speed");
            Assert.AreEqual(0f, s.charge, 1e-6f, "charge spent");
        }

        [Test]
        public void Bounce_PressedInsideHalfTheWindow_IsPerfect_GainsSpeed_AndGrowsTheChain()
        {
            var s = FleaMath.State.Start(Nose, 100f);
            FleaMath.Step(ref s, P, Nose, 0f, 1f, 20f, 1f, Dt);
            Assert.AreEqual(99.7921f, s.speed, 1e-3f, "speed after the press frame");
            var n = FleaMath.BounceNormal(Vector3.zero, new Vector3(0f, 0f, -3f), new Vector3(0f, -1f, 0f), Vector3.up);
            Assert.IsTrue(FleaMath.Bounce(ref s, P, n, 20.05f), "a press 0.05 s before the contact is perfect");
            Assert.AreEqual(134.719335f, s.speed, 1e-3f, "perfect speed");
            Near(new Vector3(0f, 0f, 1f), s.course, 1e-5f, "reflected course");
            Assert.AreEqual(1, s.chain); Assert.AreEqual(1, s.perfects);
        }

        [Test]
        public void Bounce_WithNoPress_IsDull_AndALatePressInsideHalfTheWindowSavesIt()
        {
            var s = FleaMath.State.Start(Nose, 100f);
            Assert.IsFalse(FleaMath.Bounce(ref s, P, new Vector3(0f, 0f, 1f), 30f));
            Assert.AreEqual(85f, s.speed, 1e-3f, "dull speed"); Assert.AreEqual(0, s.chain); Assert.AreEqual(30f, s.dullAt, 1e-6f);
            var ev = FleaMath.Step(ref s, P, Nose, 0f, 1f, 30.05f, 1f, Dt);
            Assert.AreEqual(FleaMath.Event.LatePerfect, ev);
            Assert.AreEqual(134.64657f, s.speed, 1e-3f, "saved by the late press");
            Assert.AreEqual(1, s.chain);
        }

        [Test]
        public void Bounce_PressedJustOutsideHalfTheWindow_IsDull()
        {
            var s = FleaMath.State.Start(Nose, 100f);
            FleaMath.Step(ref s, P, Nose, 0f, 1f, 50f, 1f, Dt);
            Assert.IsFalse(FleaMath.Bounce(ref s, P, new Vector3(0f, 0f, 1f), 50f + P.window / 2f + 0.01f), "too early is dull");
            var t = FleaMath.State.Start(Nose, 100f);
            Assert.IsFalse(FleaMath.Bounce(ref t, P, new Vector3(0f, 0f, 1f), 60f));
            FleaMath.Step(ref t, P, Nose, 0f, 1f, 60f + P.window / 2f + 0.01f, 1f, Dt);
            Assert.AreEqual(0, t.chain, "too late a press saves nothing");
        }

        [Test]
        public void BounceNormal_IsTheOffsetFromThePrismsAxis_AndTheHullsUpOnTheAxis()
        {
            Near(new Vector3(0.948683f, 0.316228f, 0f), FleaMath.BounceNormal(new Vector3(3f, 1f, 5f), Vector3.zero, new Vector3(0f, 0f, 1f), Vector3.up), 1e-5f, "off the axis");
            var up = Quaternion.AngleAxis(0.5f * Mathf.Rad2Deg, new Vector3(0f, 0f, 1f)) * Vector3.up;
            Near(new Vector3(-0.479426f, 0.877583f, 0f), FleaMath.BounceNormal(new Vector3(0.2f, 0.1f, 7f), Vector3.zero, new Vector3(0f, 0f, 1f), up), 1e-5f, "on the axis");
        }

        [Test]
        public void Bounce_AtAGlancingAngle_SkipsAlongTheRibbon_KeepingItsAlongTrackSpeed()
        {
            var cin = new Vector3(0.866025f, -0.5f, 0f);
            var s = FleaMath.State.Start(cin, 120f);
            var n = FleaMath.BounceNormal(new Vector3(0f, 2f, 0f), Vector3.zero, new Vector3(1f, 0f, 0f), Vector3.up);
            FleaMath.Bounce(ref s, P, n, 40f);
            Assert.AreEqual(102f, s.speed, 1e-3f, "dull skip speed");
            Near(new Vector3(0.866025f, 0.5f, 0f), s.course, 1e-5f, "skipped course");
        }

        [Test]
        public void AtLevels_ScalesEachElementsParameterByItsRecipeRange()
        {
            var full = FleaMath.AtLevels(P, 10f, 10f, 10f, 10f);
            Assert.AreEqual(1.35f * 1.15f, full.perfectGain, 1e-5f, "Charge: Spring");
            Assert.AreEqual(260f * 1.2f, full.topSpeed, 1e-3f, "Mass: Momentum");
            Assert.AreEqual(160f * 1.5f, full.hopMax, 1e-3f, "Space: Big Hop");
            Assert.AreEqual(0.18f * 1.7f, full.window, 1e-5f, "Time: Wide Window");
            var half = FleaMath.AtLevels(P, 0f, 0f, 0f, 5f);
            Assert.AreEqual(0.18f * 1.35f, half.window, 1e-5f, "level 5 is halfway");
            Assert.AreEqual(P.perfectGain, half.perfectGain, 1e-6f, "level 0 leaves it");
            Assert.AreEqual(P.window * 1.7f, FleaMath.AtLevels(P, 0f, 0f, 0f, 14f).window, 1e-5f, "past 10 is clamped");
        }
    }
}
