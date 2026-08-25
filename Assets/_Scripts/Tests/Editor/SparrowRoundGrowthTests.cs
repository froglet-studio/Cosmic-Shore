using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Edit-mode coverage for the MASS in-flight growth curve — the Sparrow's answer to
    /// "the only thing that felt fun was huge projectiles". Rounds leave the muzzle small
    /// and swell as they travel; MASS decides how much. The bullets' shipped pair is 3× at
    /// resting Mass and 6× at Mass 10, extrapolated across the element system's [-5, 15]
    /// band; the skyburst missile points at the SAME curve with its own 20×/32× pair.
    ///
    /// Lives under an Editor/ folder per CLAUDE.md — a test anywhere else compiles into the
    /// player and breaks the Windows build at the IL2CPP linker.
    /// </summary>
    public class SparrowRoundGrowthTests
    {
        const float AtRest = 3f;   // the shipped FullAutoAction.asset values
        const float AtFull = 6f;

        static float G(int level) => ElementalScaling.RoundGrowthFactorForLevel(level, AtRest, AtFull);

        // ---- the skyburst missile (SkyBurstGunAction.asset) ----------------------
        // The same ONE curve with its own authored pair: the missile leaves the bay at the
        // size of the one the bay animation just ejected and swells into the warhead that
        // detonates.
        const float MissileAtRest = 20f;
        const float MissileAtFull = 32f;

        static float M(int level) => ElementalScaling.RoundGrowthFactorForLevel(level, MissileAtRest, MissileAtFull);

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
            Assert.That(ElementalScaling.RoundGrowthFactorForLevel(-5, 0f, 0f), Is.GreaterThan(0f));
            Assert.That(ElementalScaling.RoundGrowthFactorForLevel(15, 6f, 3f), Is.GreaterThan(0f));
            Assert.That(ElementalScaling.RoundGrowthFactorForLevel(-5, 1f, 10f), Is.GreaterThan(0f));
        }

        [Test]
        public void AnEqualPairDisablesGrowthAtEveryLevel()
        {
            // The sanctioned opt-out: author both endpoints to 1 and rounds fly at launch size.
            for (int level = -5; level <= 15; level++)
                Assert.AreEqual(1f, ElementalScaling.RoundGrowthFactorForLevel(level, 1f, 1f), 1e-4f);
        }

        // ------------------------------------------------------- the skyburst missile

        // The missile's own geometry, measured rather than assumed: Sparrow Missile.fbx vertex
        // bounds span 8.2951 mesh units along the nose axis and 1.9054 across it, at
        // UnitScaleFactor 1 (Unity import factor 0.01), and the prefab flies the mesh at
        // MissileVisual 2 × root ProjectileScale 10.
        const float LaunchLength = 0.0829514f * 2f * 10f;   // ≈ 1.659 u
        const float LaunchGirth  = 0.0190535f * 2f * 10f;   // ≈ 0.381 u
        const float HitDiameter  = 0.85f * 10f * 2f;        // = 17 u (SphereCollider 0.85 × 10)

        [Test]
        public void TheMissileHitsItsOwnAuthoredAnchors()
        {
            // A different weapon with its own endpoints, but NOT its own curve — one home
            // (ElementalScaling.RoundGrowthFactorForLevel) for every round that grows.
            Assert.AreEqual(20f, M(0), 1e-4f);
            Assert.AreEqual(32f, M(10), 1e-4f);
            Assert.AreEqual(14f, M(-5), 1e-4f);
            Assert.AreEqual(38f, M(15), 1e-4f);
        }

        [Test]
        public void TheMissileIsBroadsideContainedByItsHitSphere()
        {
            // The skyburst grows its MODEL, not its collider (Projectile.flightGrowthTarget →
            // MissileVisual), so no amount of growth changes what it hits. What growth CAN do
            // is make the model disagree with the hit volume, and at the shipped 20× it does —
            // deliberately, and asymmetrically, which is the part worth pinning:
            //
            //   GIRTH is contained. Even at full overcharge the missile is 14.5 u across
            //   against a 17 u hit diameter, so a round crossing your view never looks wider
            //   than the volume that would have caught you. This is the read that matters for
            //   a near miss, and it is the one held here.
            //
            //   LENGTH is not, and cannot be: a 20× missile is 33 u nose to tail. The nose
            //   therefore reaches ~8 u past the hit sphere (~23 u at Mass 15), so it visually
            //   arrives a fraction of a second before the hit registers. Accepted as the cost
            //   of the authored size — see SPARROW_SKYBURST_BAY.md.
            for (int level = -5; level <= 15; level++)
                Assert.That(LaunchGirth * M(level), Is.LessThan(HitDiameter),
                    $"grown missile girth at Mass level {level}");

            // The headroom left in the girth is what bounds a future retune: past ~44.6× the
            // round would look wider than it hits in every direction, which is where the
            // mismatch stops being a nose-overhang and starts being a lie.
            Assert.That(M(15), Is.LessThan(HitDiameter / LaunchGirth),
                "the top of the Mass band must stay inside the broadside budget");
        }

        [Test]
        public void TheMissileNoseOverhangIsWhatWeThinkItIs()
        {
            // Pinned because it is the one thing the size buys that a player can be surprised
            // by. Nose-to-hit-sphere overhang, per end, in world units.
            Assert.AreEqual(8.09f, (LaunchLength * M(0) - HitDiameter) / 2f, 0.05f);
            Assert.AreEqual(23.02f, (LaunchLength * M(15) - HitDiameter) / 2f, 0.05f);
        }

        [Test]
        public void TheMissileGrowsMoreThanABulletDoes()
        {
            // Deliberate: a bullet launches at a size you can already see; the missile
            // launches at bay size (~1.7 u) inside a 17 u hit sphere and has further to go —
            // and unlike a bullet it does all of it in the first fifth of the flight.
            for (int level = -5; level <= 15; level++)
                Assert.That(M(level), Is.GreaterThan(G(level)), $"level {level}");
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
