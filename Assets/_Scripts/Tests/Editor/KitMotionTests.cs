using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The §7 motion vocabulary the kit's motion components read (Docs/MENU_REDESIGN_B.md §13.4).</summary>
    public class KitMotionTests
    {
        [TestCase(KitEase.OutQuint)]
        [TestCase(KitEase.OutCubic)]
        [TestCase(KitEase.OutBack)]
        [TestCase(KitEase.Linear)]
        [TestCase(KitEase.InOutSine)]
        public void EveryCurve_StartsAtZero_AndEndsAtOne(KitEase ease)
        {
            Assert.That(KitMotion.Ease(ease, 0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(KitMotion.Ease(ease, 1f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void Ease_ClampsOutOfRangeProgress()
        {
            Assert.That(KitMotion.Ease(KitEase.OutQuint, -2f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(KitMotion.Ease(KitEase.OutQuint, 3f), Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void OutBack_Overshoots_ThenSettles()
        {
            float peak = 0f;
            for (int i = 1; i < 100; i++) peak = UnityEngine.Mathf.Max(peak, KitMotion.Ease(KitEase.OutBack, i / 100f));
            Assert.That(peak, Is.GreaterThan(1f), "a pop overshoots");
        }

        [Test]
        public void OutCurves_AreAheadOfLinear_AtTheMidpoint()
        {
            Assert.That(KitMotion.Ease(KitEase.OutQuint, 0.5f), Is.GreaterThan(0.5f));
            Assert.That(KitMotion.Ease(KitEase.OutCubic, 0.5f), Is.GreaterThan(0.5f));
        }

        [Test]
        public void Stagger_Is40ms_PerItem_CappedAt8()
        {
            Assert.That(KitMotion.StaggerDelay(0), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(KitMotion.StaggerDelay(3), Is.EqualTo(0.12f).Within(1e-6f));
            Assert.That(KitMotion.StaggerDelay(8), Is.EqualTo(0.32f).Within(1e-6f));
            Assert.That(KitMotion.StaggerDelay(40), Is.EqualTo(KitMotion.StaggerDelay(8)));
            Assert.That(KitMotion.StaggerDelay(-1), Is.EqualTo(0f));
        }

        [Test]
        public void Tokens_MatchStyleFoundationSection7()
        {
            Assert.That(KitMotion.Micro, Is.EqualTo(0.12f));
            Assert.That(KitMotion.Std, Is.EqualTo(0.2f));
            Assert.That(KitMotion.Panel, Is.EqualTo(0.32f));
            Assert.That(KitMotion.Ceremony, Is.GreaterThanOrEqualTo(0.5f));
        }

        /// <summary>
        /// A row enabled before its layout group ran starts its entrance from the wrong place; the
        /// layout then moves it. The entrance must adopt that position, not write the stale one back.
        /// </summary>
        [Test]
        public void Entrance_AdoptsAPositionALayoutWroteMidEntrance()
        {
            var rest = new Vector2(0f, 0f);
            var lastWritten = new Vector2(0f, -14f);      // the entrance's own last frame
            var laidOut = new Vector2(120f, -300f);       // where the layout group put the row
            Assert.AreEqual(laidOut, KitEntrance.ReconcileRest(laidOut, lastWritten, rest));
        }

        [Test]
        public void Entrance_KeepsItsRest_WhenNothingElseMovedIt()
        {
            var rest = new Vector2(40f, 10f);
            var lastWritten = new Vector2(40f, 2f);
            Assert.AreEqual(rest, KitEntrance.ReconcileRest(lastWritten, lastWritten, rest));
        }
    }
}
