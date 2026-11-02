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

        /// <summary>
        /// Only an entrance that offsets the position may write it. A Pop / Grow / Fade that wrote its
        /// captured pre-layout position back on Finish() snapped the in-game score column under its new
        /// layout anchors, centred on the bar's top-left corner and half off screen (2026-10-31).
        /// Enumerates the enum so a mode added later is covered the day it lands.
        /// </summary>
        [Test]
        public void Entrance_WritesPosition_OnlyForTheModesThatMoveIt()
        {
            foreach (KitEntranceMode mode in System.Enum.GetValues(typeof(KitEntranceMode)))
            {
                bool moves = mode == KitEntranceMode.Rise || mode == KitEntranceMode.Drop;
                Assert.AreEqual(moves, KitEntrance.MovesPosition(mode), mode.ToString());
            }
        }

        [Test]
        public void Entrance_KeepsAnOwnersDim_ThroughAReplay()
        {
            // A configure tile dims itself to 0.4 through the same CanvasGroup its entrance fades.
            // The window replays the entrance on open; it must come back to 0.4, not to 1.
            const float dimmed = 0.4f;
            Assert.AreEqual(dimmed, KitEntrance.ReconcileAlpha(dimmed * 0.5f, dimmed * 0.5f, dimmed));
        }

        [Test]
        public void Entrance_AdoptsAHide_ThatLandsMidEntrance()
        {
            // KitVisibility hides the part while the entrance is still fading it up.
            Assert.AreEqual(0f, KitEntrance.ReconcileAlpha(0f, 0.6f, 1f));
        }

        // ---- The ambient layer sleeps while nobody can see it (section 24) ----

        static (GameObject window, GameObject part) Window(float windowAlpha)
        {
            var window = new GameObject("Window", typeof(RectTransform), typeof(CanvasGroup));
            window.GetComponent<CanvasGroup>().alpha = windowAlpha;
            var part = new GameObject("Part", typeof(RectTransform));
            part.transform.SetParent(window.transform, false);
            return (window, part);
        }

        [Test]
        public void Sightline_SeesAPartInAnOpenWindow()
        {
            var (window, part) = Window(1f);
            try { Assert.IsTrue(new KitSightline(part.transform, null, null).Visible); }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void Sightline_DoesNotSeeAPartInAClosedWindow()
        {
            // A closed modal stays active at alpha 0 - the case every ambient loop used to run through.
            var (window, part) = Window(0f);
            try { Assert.IsFalse(new KitSightline(part.transform, null, null).Visible); }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void Sightline_StopsAtAGroupThatIgnoresItsParents()
        {
            var (window, part) = Window(0f);
            try
            {
                var own = part.AddComponent<CanvasGroup>();
                own.ignoreParentGroups = true;
                Assert.IsTrue(new KitSightline(part.transform, null, null).Visible);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void Sightline_LeavesOutTheGroupABreatheDrives()
        {
            // A Breathe writes its own group; the low point of the pulse must not put it to sleep.
            var (window, part) = Window(1f);
            try
            {
                var own = part.AddComponent<CanvasGroup>();
                own.alpha = 0f;
                Assert.IsTrue(new KitSightline(part.transform, own, null).Visible);
                Assert.IsFalse(new KitSightline(part.transform, null, null).Visible);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test]
        public void Breathe_KeepsAPartItsOwnerHid_Hidden()
        {
            // The configure footer's "Waiting for others..." is hidden by KitVisibility (alpha 0) and
            // breathes. The owner's 0 is adopted as the rest, so the pulse cannot show it.
            Assert.AreEqual(0f, KitAmbient.OwnerAlpha(0f, 0.75f, 1f));
        }

        [Test]
        public void Breathe_KeepsItsRest_WhenOnlyItWroteTheGroup()
        {
            Assert.AreEqual(1f, KitAmbient.OwnerAlpha(0.75f, 0.75f, 1f));
        }
    }
}
