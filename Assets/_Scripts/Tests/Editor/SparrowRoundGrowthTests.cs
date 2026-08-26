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

        // The missile's own geometry, measured from Sparrow Missile.fbx and expressed in the
        // projectile's ROOT-LOCAL space — the frame the sphere collider lives in. The mesh is
        // rotated +90° about X under MissileVisual, so the model's long axis is root +z (flight)
        // and its cross-section is root x/y. Half-extents at growth 1, child scale 2 included.
        static readonly Vector3 ModelCentre  = new Vector3(-0.000149f, 0.001277f, 0.002190f);
        static readonly Vector3 ModelExtents = new Vector3(0.019053f, 0.019053f, 0.082950f);
        const float RootScale = 10f;        // ProjectileScale, applied to the projectile root

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
        public void TheMissileHitSphereIsTheModelAtItsWidest()
        {
            // The skyburst satisfies "the size you see is the size that hits" the other way
            // round from the bullets: the MODEL grows and the collider is fitted to it, rather
            // than the model standing still while a shell draws the hit volume. So the sphere's
            // radius must be the model's widest cross-section at every growth, never the box
            // DIAGONAL (which would overstate a round missile by √2).
            foreach (int level in new[] { -5, 0, 5, 10, 15 })
            {
                float g = M(level);
                float widest = Mathf.Max(ModelExtents.x, ModelExtents.y) * g;
                Assert.AreEqual(widest, Projectile.ModelHitRadius(ModelExtents, g), 1e-6f,
                    $"level {level}");
            }
        }

        [Test]
        public void TheMissileNoseSitsExactlyOnTheHitSphereSurface()
        {
            // The contract, and the reason the tail is allowed to trail: a model may stick out
            // the BACK of its collider — a tail that has already passed you cannot cause a false
            // read — but never out the FRONT, where the nose would visibly reach a target before
            // the hit registered. Front surface == model tip, at every growth.
            foreach (int level in new[] { -5, 0, 5, 10, 15 })
            {
                float g = M(level);
                float radius = Projectile.ModelHitRadius(ModelExtents, g);
                Vector3 centre = Projectile.ModelHitCentre(ModelCentre, ModelExtents, g);

                float nose = (ModelCentre.z + ModelExtents.z) * g;
                Assert.AreEqual(nose, centre.z + radius, 1e-5f, $"nose, level {level}");

                // ...and the tail is behind the sphere's back, which is the permitted overhang.
                float tail = (ModelCentre.z - ModelExtents.z) * g;
                Assert.That(tail, Is.LessThan(centre.z - radius), $"tail, level {level}");
            }
        }

        [Test]
        public void TheMissileHitSphereShrankFromTheOldEmergentOne()
        {
            // The 8.5 u sphere it replaced was `0.85 × ProjectileScale 10` arithmetic rather
            // than an authored size, and it dwarfed the model it belonged to. Pinned so a future
            // change has to argue with the number: at resting Mass the fitted sphere is 3.81 u,
            // 45% of what the missile used to hit with. That is a Dog Fight reach change and is
            // the point of the fit, not a side effect of it.
            float rest = Projectile.ModelHitRadius(ModelExtents, M(0)) * RootScale;
            Assert.AreEqual(3.811f, rest, 0.01f);
            Assert.That(rest, Is.LessThan(8.5f));
        }

        // -------------------------------------------------------------------- the TAIL
        //
        // The missile is the one round that carries a TAIL — the long streak that lets other
        // pilots see it coming (Docs/VESSEL_TAIL_AND_JETS.md). Both of its numbers fall out of
        // the SAME measurement the hit sphere is fitted to, so nothing about it is authored per
        // MASS level and nothing hardcodes this missile.

        const float TailWidthFraction = 0.4f;   // the shipped SkyBurstProjectile.prefab value

        [Test]
        public void TheTailHangsOffTheModelsRearFace()
        {
            // Exactly the mirror of the nose contract above: the emitter sits on the back of
            // the body at every growth, so it stays pinned to the exhaust end while the missile
            // swells from 1.7 u to 33 u instead of drifting into the middle of it.
            foreach (int level in new[] { -5, 0, 5, 10, 15 })
            {
                float g = M(level);
                float rear = (ModelCentre.z - ModelExtents.z) * g;
                Assert.AreEqual(rear, Projectile.TailMount(ModelCentre, ModelExtents, g).z, 1e-6f,
                    $"level {level}");

                // ...and it is BEHIND the hit sphere, which is the permitted overhang. A tail
                // emitting from in front of the collider would draw the round's path starting
                // ahead of where it can hit.
                Vector3 centre = Projectile.ModelHitCentre(ModelCentre, ModelExtents, g);
                float radius = Projectile.ModelHitRadius(ModelExtents, g);
                Assert.That(Projectile.TailMount(ModelCentre, ModelExtents, g).z,
                            Is.LessThan(centre.z - radius), $"behind the sphere, level {level}");
            }
        }

        [Test]
        public void TheTailIsAlwaysBehindTheRoundOrigin()
        {
            // The emitter slides backwards as the model grows, while the round flies forwards
            // far faster (~70 u over the 0.6 s swell against ~15 u of slide at resting Mass), so
            // the ribbon can never double back on itself. Sign is the invariant worth pinning.
            foreach (int level in new[] { -5, 0, 5, 10, 15 })
                Assert.That(Projectile.TailMount(ModelCentre, ModelExtents, M(level)).z,
                            Is.LessThan(0f), $"level {level}");
        }

        [Test]
        public void TheTailWidthTracksTheBodyItStreamsOff()
        {
            // A TrailRenderer's width is WORLD-space and ignores transform scale, so a round
            // that swells 14x-38x would otherwise fly a constant thread. Width is a fraction of
            // the body's own diameter, which is twice the radius the collider is fitted to — so
            // the tail and the hit sphere can never disagree about how big the round is.
            foreach (int level in new[] { -5, 0, 5, 10, 15 })
            {
                float g = M(level);
                float expected = TailWidthFraction * 2f * Projectile.ModelHitRadius(ModelExtents, g) * RootScale;
                Assert.AreEqual(expected,
                    Projectile.TailWidth(ModelExtents, g, RootScale, TailWidthFraction), 1e-5f,
                    $"level {level}");
            }
        }

        [Test]
        public void TheTailReadsLikeTheShipsOwnTail()
        {
            // 0.4 is not a taste call: it is the ratio the one hull the fleet actually tuned a
            // tail against already flies at — the Sparrow's widthScale 2.5 on a ~6.4 u hull
            // (Docs/VESSEL_TAIL_AND_JETS.md 3.1). At resting Mass the missile's tail lands at
            // 3.05 u, a little wider than the ship's because the missile is a little bigger
            // than the ship. Pinned so a retune has to argue with the number.
            float rest = Projectile.TailWidth(ModelExtents, M(0), RootScale, TailWidthFraction);
            Assert.AreEqual(3.05f, rest, 0.02f);
            Assert.AreEqual(2.5f / 6.4f, TailWidthFraction, 0.02f);
        }

        [Test]
        public void TheTailWidthIsMonotonicInMass()
        {
            // MASS owns the substance of what you fire; the streak it leaves has to say so.
            for (int level = -5; level < 15; level++)
                Assert.That(Projectile.TailWidth(ModelExtents, M(level + 1), RootScale, TailWidthFraction),
                            Is.GreaterThan(Projectile.TailWidth(ModelExtents, M(level), RootScale, TailWidthFraction)),
                            $"level {level}");
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

        // ------------------------------------------------------- the flight-growth curve
        //
        // The ramp moved to RoundGrowthRamp when the skyburst missile needed a second SHAPE
        // (all its growth in the first fifth of the flight, then held). These tests cover the
        // full-flight shape the bullets use, composed with the real Mass curve above;
        // RoundGrowthRampTests covers the ramp itself, including the early-and-hold window.

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
                Assert.AreEqual(1f, RoundGrowthRamp.At(0f, g), 1e-4f, $"muzzle, level {level}");
                Assert.AreEqual(g, RoundGrowthRamp.At(1f, g), 1e-4f, $"end, level {level}");
                Assert.AreEqual(1f + (g - 1f) * 0.5f, RoundGrowthRamp.At(0.5f, g), 1e-4f,
                    $"midpoint, level {level}");
            }
        }

        [Test]
        public void GrowthIsClampedToTheFlightAndNeverOvershoots()
        {
            // The mover feeds elapsed/projectileTime, which can tick past 1 on a long frame.
            // Past the end the round must sit at its full factor, not keep swelling.
            float g = G(10);
            Assert.AreEqual(1f, RoundGrowthRamp.At(-3f, g), 1e-4f);
            Assert.AreEqual(g, RoundGrowthRamp.At(4f, g), 1e-4f);
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
                    float hitRadius = LaunchHitRadius * RoundGrowthRamp.At(progress, g);
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
