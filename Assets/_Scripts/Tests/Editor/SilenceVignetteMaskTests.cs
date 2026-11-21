using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The silence vignette's shape (Docs/ElementalAbilitySystem/SILENCE.md §5). The lab's JavaScript
    /// twin is held to the same function by Tools/Build/silence_vignette_parity; these pin what the
    /// shape must DO, whatever the tuning.
    /// </summary>
    public class SilenceVignetteMaskTests
    {
        const float Wide = 16f / 9f;

        static SilenceVignetteMask.Shape Soft(float roundness = 0f) => new()
        {
            Style = SilenceVignetteMask.StyleSoft, Facets = 10, Inner = 0.55f, Feather = 0.5f,
            Roundness = roundness, Fill = 1f, RimStrength = 0f, RimWidth = 0.04f,
        };

        [Test]
        public void TheCentreIsClearAndTheCornersCarryTheEdge()
        {
            var s = Soft();
            Assert.AreEqual(0f, SilenceVignetteMask.Alpha(0.5f, 0.5f, Wide, s));
            Assert.Greater(SilenceVignetteMask.Alpha(0f, 0f, Wide, s), 0.95f);
            Assert.Greater(SilenceVignetteMask.Alpha(1f, 0.5f, Wide, s), 0.5f, "the side midpoint");
        }

        [Test]
        public void RoundnessZeroFollowsTheScreenAndOneIsACircleInPixels()
        {
            Assert.AreEqual(1f, SilenceVignetteMask.Distance(1f, 0.5f, Wide, Soft(0f)), 1e-5f);
            Assert.AreEqual(Wide, SilenceVignetteMask.Distance(1f, 0.5f, Wide, Soft(1f)), 1e-5f);
            Assert.AreEqual(1f, SilenceVignetteMask.Distance(0.5f, 1f, Wide, Soft(1f)), 1e-5f);
        }

        [Test]
        public void TheFacetedWindowIsAPolygonWithAFlatTop()
        {
            var s = Soft();
            s.Style = SilenceVignetteMask.StyleFaceted;
            s.Facets = 6;
            // Straight up meets the middle of the top edge: the polygon's distance equals the radius.
            Assert.AreEqual(1f, SilenceVignetteMask.Distance(0.5f, 1f, 1f, s), 1e-5f);
            // Toward a vertex the projection is shorter than the radius.
            float a = 30f * UnityEngine.Mathf.Deg2Rad + UnityEngine.Mathf.PI * 0.5f;
            float u = 0.5f + 0.5f * UnityEngine.Mathf.Cos(a), v = 0.5f + 0.5f * UnityEngine.Mathf.Sin(a);
            Assert.AreEqual(UnityEngine.Mathf.Cos(30f * UnityEngine.Mathf.Deg2Rad),
                            SilenceVignetteMask.Distance(u, v, 1f, s), 1e-4f);
            s.Facets = 2;
            Assert.AreEqual(SilenceVignetteMask.Distance(u, v, 1f, Soft()), SilenceVignetteMask.Distance(u, v, 1f, s),
                            1e-6f, "fewer than three facets falls back to the soft ellipse");
        }

        [Test]
        public void TheRimIsALineAtTheInnerEdgeEvenWithNoFill()
        {
            var s = Soft();
            s.Fill = 0f;
            s.RimStrength = 0.8f;
            s.RimWidth = 0.05f;
            Assert.AreEqual(0.8f, SilenceVignetteMask.Alpha(0.5f, 0.5f + 0.55f * 0.5f, 1f, s), 1e-4f, "on the line");
            Assert.AreEqual(0f, SilenceVignetteMask.Alpha(0.5f, 0.5f, 1f, s), "the centre");
            Assert.AreEqual(0f, SilenceVignetteMask.Alpha(0.5f, 1f, 1f, s), 1e-6f, "the outer edge, no fill");
        }

        [Test]
        public void BreathingDipsByItsDepthAndSitsAtOneWhenOff()
        {
            Assert.AreEqual(1f, SilenceVignetteMask.Breathe(0f, 0.3f, 1f), 1e-6f, "the peak");
            Assert.AreEqual(0.7f, SilenceVignetteMask.Breathe(0.5f, 0.3f, 1f), 1e-5f, "the trough");
            Assert.AreEqual(1f, SilenceVignetteMask.Breathe(0.5f, 0f, 1f));
            Assert.AreEqual(1f, SilenceVignetteMask.Breathe(0.5f, 0.3f, 0f));
        }

        [Test]
        public void OpacityIsWeightTimesStrengthPlusTheHitFlareClamped()
        {
            Assert.AreEqual(0.75f, SilenceVignetteMask.Opacity(1f, 1f, 0.75f, 1f, 0f), 1e-6f);
            Assert.AreEqual(0f, SilenceVignetteMask.Opacity(1f, 0f, 0.75f, 1f, 0f), "no silence, no edge");
            Assert.AreEqual(0.6f, SilenceVignetteMask.Opacity(1f, 0.5f, 0.5f, 1f, 0.35f), 1e-6f, "a flare adds");
            Assert.AreEqual(1f, SilenceVignetteMask.Opacity(1f, 1f, 1f, 1f, 0.9f), "clamped");
            Assert.AreEqual(0.5f, SilenceVignetteMask.Opacity(0.5f, 1f, 1f, 1f, 0f), 1e-6f, "the mask scales it");
        }
    }
}
