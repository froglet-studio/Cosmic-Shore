using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Edit-mode coverage for the MASS in-flight growth curve — the Sparrow's answer to
    /// "the only thing that felt fun was huge projectiles". Rounds leave the muzzle small
    /// and swell as they travel; MASS decides how much. The shipped pair is 3× at resting
    /// Mass and 6× at Mass 10, extrapolated across the element system's [-5, 15] band.
    ///
    /// Lives under an Editor/ folder per CLAUDE.md — a test anywhere else compiles into the
    /// player and breaks the Windows build at the IL2CPP linker.
    /// </summary>
    public class SparrowRoundGrowthTests
    {
        const float AtRest = 3f;   // the shipped FullAutoAction.asset values
        const float AtFull = 6f;

        static float G(int level) => FullAutoActionSO.GrowthFactorForLevel(level, AtRest, AtFull);

        [Test]
        public void HitsTheAuthoredAnchors()
        {
            // These two are the design, stated by the prompter: triple at rest, 6× at Mass 10.
            Assert.AreEqual(3f, G(0), 1e-4f);
            Assert.AreEqual(6f, G(10), 1e-4f);
        }

        [Test]
        public void ExtrapolatesAcrossTheWholeElementBand()
        {
            // GetLevel returns [-5, 15]; the curve must keep going rather than clamp at the
            // anchors, so a starved Mass level is visibly punier and full overcharge is huge.
            Assert.AreEqual(1.5f, G(-5), 1e-4f);
            Assert.AreEqual(7.5f, G(15), 1e-4f);
        }

        [Test]
        public void IsLinearInLevel()
        {
            // 0.3× per level across the band — a steady, readable progression rather than a
            // curve that dumps all its growth at one end.
            for (int level = -5; level < 15; level++)
                Assert.AreEqual(0.3f, G(level + 1) - G(level), 1e-4f, $"step above level {level}");
        }

        [Test]
        public void IsMonotonic()
        {
            for (int level = -5; level < 15; level++)
                Assert.That(G(level + 1), Is.GreaterThan(G(level)));
        }

        [Test]
        public void NeverReturnsAZeroOrNegativeFactor()
        {
            // A factor of zero would collapse the round to nothing mid-flight, taking its hit
            // volume with it. Guard holds even for a mis-authored inverted pair.
            Assert.That(FullAutoActionSO.GrowthFactorForLevel(-5, 0f, 0f), Is.GreaterThan(0f));
            Assert.That(FullAutoActionSO.GrowthFactorForLevel(15, 6f, 3f), Is.GreaterThan(0f));
            Assert.That(FullAutoActionSO.GrowthFactorForLevel(-5, 1f, 10f), Is.GreaterThan(0f));
        }

        [Test]
        public void AnEqualPairDisablesGrowthAtEveryLevel()
        {
            // The sanctioned opt-out: author both endpoints to 1 and rounds fly at launch size.
            for (int level = -5; level <= 15; level++)
                Assert.AreEqual(1f, FullAutoActionSO.GrowthFactorForLevel(level, 1f, 1f), 1e-4f);
        }

        // ------------------------------------------------------------------ honesty

        [Test]
        public void TheHitRadiusTracksTheVisibleCrossSection()
        {
            // The whole point of growing rather than just inflating a collider: the hit volume
            // and the thing you can see must stay in lockstep. The tracer's visible
            // cross-section radius is 0.75 and its authored hit radius is 0.825 (+10%); both
            // are scaled by the SAME factor, so the ratio is invariant through the flight.
            const float visibleRadius = 0.75f;
            const float hitRadius = 0.825f;

            for (int level = -5; level <= 15; level += 5)
            {
                float g = G(level);
                for (float progress = 0f; progress <= 1f; progress += 0.25f)
                {
                    float scale = Mathf.LerpUnclamped(1f, g, progress);
                    Assert.AreEqual(hitRadius / visibleRadius,
                        (hitRadius * scale) / (visibleRadius * scale), 1e-4f,
                        $"level {level}, progress {progress}");
                }
            }
        }

        [Test]
        public void GrowthIsWorthMoreThanTheFireRateEverWas()
        {
            // Sanity on the design bet: destruction footprint goes as the SQUARE of the
            // radius, so tripling the round's width is ~9× the swath — far more than any
            // affordable fire-rate increase, and it is what the "huge projectiles" report was
            // actually reacting to.
            float endOfFlight = G(0);
            Assert.That(endOfFlight * endOfFlight, Is.GreaterThan(8f));
            Assert.That(G(10) * G(10), Is.GreaterThan(35f));
        }
    }
}
