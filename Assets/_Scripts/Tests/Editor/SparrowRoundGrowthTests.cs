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

        // ------------------------------------------------------- the flight-growth curve

        // The tracer's authored launch hit radius: SparrowProjectile's SphereCollider radius
        // 0.04125 against its largest lossy-scale component, the z-stretch of 20.
        const float LaunchHitRadius = 0.825f;

        // SparrowProjectile's own transform — deliberately NON-UNIFORM, which is the whole
        // reason the shell needs a per-axis divide rather than a uniform scale. The z-stretch
        // is also the largest lossy component, which is why halving the model's cross-section
        // (1.5 → 0.75) left the collider's world radius at 0.825 untouched.
        static readonly Vector3 DartScale = new Vector3(0.75f, 0.75f, 20f);

        [Test]
        public void GrowthRunsFromOneAtTheMuzzleToTheFullFactorAtTheEnd()
        {
            foreach (int level in new[] { -5, 0, 5, 10, 15 })
            {
                float g = G(level);
                Assert.AreEqual(1f, Projectile.GrowthAtProgress(g, 0f), 1e-4f, $"muzzle, level {level}");
                Assert.AreEqual(g, Projectile.GrowthAtProgress(g, 1f), 1e-4f, $"end, level {level}");
                Assert.AreEqual(1f + (g - 1f) * 0.5f, Projectile.GrowthAtProgress(g, 0.5f), 1e-4f,
                    $"midpoint, level {level}");
            }
        }

        [Test]
        public void GrowthIsClampedToTheFlightAndNeverOvershoots()
        {
            // The mover feeds elapsed/projectileTime, which can tick past 1 on a long frame.
            // Past the end the round must sit at its full factor, not keep swelling.
            float g = G(10);
            Assert.AreEqual(1f, Projectile.GrowthAtProgress(g, -3f), 1e-4f);
            Assert.AreEqual(g, Projectile.GrowthAtProgress(g, 4f), 1e-4f);
        }

        // ------------------------------------------------------------------ honesty

        [Test]
        public void TheChargeShellIsExactlyTheHitVolume()
        {
            // The honesty claim, now that the MODEL no longer grows: the shell the player reads
            // the growth off is not an impression of the hit volume, it IS the hit volume. Its
            // world radius must equal the swept hit radius at every instant of every flight.
            //
            // (The test this replaced compared hitRadius*s / visibleRadius*s against
            // hitRadius / visibleRadius — the same factor top and bottom, so it was true for
            // any code at all. It asserted nothing.)
            foreach (int level in new[] { -5, 0, 5, 10, 15 })
            {
                float g = G(level);
                for (float progress = 0f; progress <= 1f; progress += 0.25f)
                {
                    float hitRadius = LaunchHitRadius * Projectile.GrowthAtProgress(g, progress);
                    Vector3 local = Projectile.ChargeFieldLocalScale(hitRadius, DartScale);

                    // The shell's world scale, and from it the world radius of a built-in
                    // sphere mesh (object-space radius 0.5).
                    Vector3 world = Vector3.Scale(local, DartScale);
                    Assert.AreEqual(hitRadius, 0.5f * world.x, 1e-4f, $"level {level}, progress {progress}");
                    Assert.AreEqual(hitRadius, 0.5f * world.y, 1e-4f, $"level {level}, progress {progress}");
                    Assert.AreEqual(hitRadius, 0.5f * world.z, 1e-4f, $"level {level}, progress {progress}");
                }
            }
        }

        [Test]
        public void TheChargeShellCancelsTheDartsNonUniformTransform()
        {
            // A uniform world sphere under a (0.75, 0.75, 20) parent needs a per-axis divide.
            // If this ever collapses to a single divisor the shell renders as a lens, not a
            // ball — and a lens is a lie about a sphere-swept hit volume.
            Vector3 local = Projectile.ChargeFieldLocalScale(LaunchHitRadius, DartScale);
            Assert.AreNotEqual(local.x, local.z, "a non-uniform parent needs different divisors");

            Vector3 world = Vector3.Scale(local, DartScale);
            Assert.AreEqual(world.x, world.y, 1e-4f);
            Assert.AreEqual(world.x, world.z, 1e-4f);
        }

        [Test]
        public void TheChargeShellSurvivesADegenerateParentScale()
        {
            // A round can be sized while its parent chain is still mid-setup (the turret's
            // carried collider detaches from a prism that may still be at scale zero), so the
            // divide must never produce an infinity that poisons the transform.
            Vector3 local = Projectile.ChargeFieldLocalScale(LaunchHitRadius, Vector3.zero);
            Assert.That(float.IsFinite(local.x) && float.IsFinite(local.y) && float.IsFinite(local.z));

            // A negative parent scale is a mirror, not a reason to invert the shell.
            Vector3 mirrored = Projectile.ChargeFieldLocalScale(LaunchHitRadius, new Vector3(-1.5f, 1.5f, 20f));
            Assert.That(mirrored.x, Is.GreaterThan(0f));
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
