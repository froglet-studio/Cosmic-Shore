#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The pure parts of the boot poster (Rev 94 C): the leaning quad's geometry and picture crop, the motion's
    /// travel, the colour-set paint and the stage's fit. Each is what a person would otherwise have to judge by
    /// eye on a splash that is on screen for a few seconds.
    /// </summary>
    [TestFixture]
    public class BootPosterTests
    {
        [Test]
        public void Slant_LeansItsTopToTheRight_AndStaysInsideItsRect()
        {
            var rect = new Rect(-100f, -50f, 200f, 100f);
            var c = new Vector2[4];
            KitSlant.Corners(rect, 14f, c);
            float reach = KitSlant.Reach(14f, rect.height);
            Assert.AreEqual(Mathf.Tan(14f * Mathf.Deg2Rad) * 100f, reach, 1e-4f);

            Assert.AreEqual(rect.xMin, c[0].x, 1e-4f, "bottom-left starts at the rect's left");
            Assert.AreEqual(rect.xMin + reach, c[1].x, 1e-4f, "top-left sits the lean's reach to the right");
            Assert.AreEqual(rect.xMax, c[2].x, 1e-4f, "top-right ends at the rect's right");
            Assert.AreEqual(rect.xMax - reach, c[3].x, 1e-4f);
            Assert.AreEqual(c[2].x - c[1].x, c[3].x - c[0].x, 1e-4f, "top and bottom edges are the same length");
            foreach (var p in c)
            {
                Assert.GreaterOrEqual(p.x, rect.xMin - 1e-4f);
                Assert.LessOrEqual(p.x, rect.xMax + 1e-4f);
            }
        }

        [Test]
        public void Slant_AtNoLean_IsItsRect_AndANegativeLeanMirrors()
        {
            var rect = new Rect(0f, 0f, 26f, 70f);
            var c = new Vector2[4];
            KitSlant.Corners(rect, 0f, c);
            Assert.AreEqual(new Vector2(0f, 0f), c[0]);
            Assert.AreEqual(new Vector2(0f, 70f), c[1]);
            Assert.AreEqual(new Vector2(26f, 70f), c[2]);
            Assert.AreEqual(new Vector2(26f, 0f), c[3]);

            KitSlant.Corners(new Rect(0f, 0f, 200f, 100f), -14f, c);
            Assert.Greater(c[0].x, c[1].x, "a negative lean puts the top to the left");
        }

        [Test]
        public void CoverWindow_CropsWithoutStretching_AndKeepsTheFocus()
        {
            // A square picture in a wide rect: full width, a band of its height, taken near the top.
            Rect wide = KitSlant.CoverWindow(1f, 2f, new Vector2(0.5f, 0.82f));
            Assert.AreEqual(1f, wide.width, 1e-5f);
            Assert.AreEqual(0.5f, wide.height, 1e-5f);
            Assert.AreEqual(0.5f * 0.82f, wide.y, 1e-5f);
            Assert.LessOrEqual(wide.yMax, 1f + 1e-5f);

            // In a tall rect: full height, a band of its width, centred.
            Rect tall = KitSlant.CoverWindow(1f, 0.5f, new Vector2(0.5f, 0.5f));
            Assert.AreEqual(0.5f, tall.width, 1e-5f);
            Assert.AreEqual(1f, tall.height, 1e-5f);
            Assert.AreEqual(0.25f, tall.x, 1e-5f);

            // The same aspect: the whole picture.
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), KitSlant.CoverWindow(1.5f, 1.5f, new Vector2(0.2f, 0.9f)));
        }

        [Test]
        public void Drift_TravelsThereAndBack_SlowingIntoEachEnd()
        {
            Assert.AreEqual(0f, PosterDrift.Travel(0f), 1e-6f);
            Assert.AreEqual(1f, PosterDrift.Travel(1f), 1e-6f);
            Assert.AreEqual(0f, PosterDrift.Travel(2f), 1e-6f, "the way back ends where the travel began: no restart");
            Assert.AreEqual(0.5f, PosterDrift.Travel(0.5f), 1e-6f);
            Assert.AreEqual(PosterDrift.Travel(0.3f), PosterDrift.Travel(1.7f), 1e-5f, "the way back retraces the way out");

            // Eased both ways: the first and last tenth of a trip cover less than a tenth of the distance.
            Assert.Less(PosterDrift.Travel(0.1f), 0.1f);
            Assert.Greater(PosterDrift.Travel(0.9f), 0.9f);

            float last = 0f;
            for (int i = 1; i <= 100; i++)
            {
                float t = PosterDrift.Travel(i / 100f);
                Assert.GreaterOrEqual(t, last, "the way out went backwards");
                last = t;
            }
        }

        [Test]
        public void Paint_BringsAnHdrColourUnderOne_KeepingItsHue()
        {
            Color hdr = new(2f, 1f, 0.5f, 1f);
            Color shown = PosterDomainPaint.Display(hdr, 0.74f);
            Assert.AreEqual(1f, shown.r, 1e-6f);
            Assert.AreEqual(0.5f, shown.g, 1e-6f);
            Assert.AreEqual(0.25f, shown.b, 1e-6f);
            Assert.AreEqual(0.74f, shown.a, 1e-6f);

            // A colour already under 1 is the set's colour, untouched: a dark face stays dark.
            Color face = new(0.28f, 0.004f, 0.21f, 1f);
            Color kept = PosterDomainPaint.Display(face, 1f);
            Assert.AreEqual(face.r, kept.r);
            Assert.AreEqual(face.g, kept.g);
            Assert.AreEqual(face.b, kept.b);
        }

        [Test]
        public void Paint_BlendEasesBothWays()
        {
            Assert.AreEqual(0f, PosterDomainPaint.Ease(0f));
            Assert.AreEqual(1f, PosterDomainPaint.Ease(1f));
            Assert.AreEqual(1f, PosterDomainPaint.Ease(3f), "past the end it holds the new set's colour");
            Assert.AreEqual(0.5f, PosterDomainPaint.Ease(0.5f), 1e-6f);
            Assert.Less(PosterDomainPaint.Ease(0.1f), 0.1f);
            Assert.Greater(PosterDomainPaint.Ease(0.9f), 0.9f);
        }

        [Test]
        public void Fit_ShrinksOnANarrowScreen_AndNeverGrows()
        {
            Assert.AreEqual(1f, PosterFit.ScaleFor(1920f, 1920f));
            Assert.AreEqual(1f, PosterFit.ScaleFor(2400f, 1920f), "a wide screen shows more sea, not a larger crew");
            Assert.AreEqual(0.5f, PosterFit.ScaleFor(960f, 1920f), 1e-6f);
            Assert.AreEqual(1f, PosterFit.ScaleFor(0f, 1920f), "a frame with no size yet leaves the stage alone");
        }

        [Test]
        public void PosterExit_HoldsWhileTheSplashIsUpAndPushesThroughAsItGoes()
        {
            Assert.AreEqual(1f, PosterExit.ScaleFor(1f, 0.12f), 1e-6f, "an opaque splash leaves the poster alone");
            Assert.AreEqual(1.12f, PosterExit.ScaleFor(0f, 0.12f), 1e-6f, "a dissolved splash is the full push");
            float last = 1f;
            for (int i = 100; i >= 0; i--)
            {
                float s = PosterExit.ScaleFor(i / 100f, 0.12f);
                Assert.GreaterOrEqual(s, last, "the poster only grows as the splash fades");
                last = s;
            }
            Assert.AreEqual(1f, PosterExit.ScaleFor(1f, -1f), 1e-6f, "a negative push is no push");
        }
    }
}
#endif
