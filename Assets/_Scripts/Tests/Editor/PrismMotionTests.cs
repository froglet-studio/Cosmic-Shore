using CosmicShore.Gameplay;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Prism movement's collision rule (Docs/PRISM_MOTION.md): a moving prism that RUNS INTO another
    /// destroys both. These pin what "runs into" means — the contact spheres go from apart to touching
    /// over the step — so the gravity field's batch pass and the Rhino plough's one-shot sweep, which
    /// both call <c>PrismMotion.EntersContact</c>, keep one rule — as spheres when many prisms move, as
    /// oriented boxes when few do (the box half below).
    /// </summary>
    public class PrismMotionTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void TheContactRadiusIsTheSphereOfTheSameVolume()
        {
            float r = PrismMotion.ContactRadius(1f);
            Assert.AreEqual(1f, 4f / 3f * Mathf.PI * r * r * r, Eps);
            Assert.AreEqual(0f, PrismMotion.ContactRadius(0f));
            Assert.AreEqual(0f, PrismMotion.ContactRadius(-3f));
        }

        [Test]
        public void TheContactRadiusIsCapped()
        {
            Assert.AreEqual(PrismMotion.MaxContactRadius, PrismMotion.ContactRadius(1e9f), Eps);
        }

        [Test]
        public void AMoverThatReachesAStaticPrismCollides_AtTheFirstTouch()
        {
            var b = new float3(10f, 0f, 0f);
            Assert.IsTrue(PrismMotion.EntersContact(float3.zero, new float3(20f, 0f, 0f), b, b, 2f, out float t));
            // Centres 2 apart first at x = 8, 8/20 of the way.
            Assert.AreEqual(0.4f, t, Eps);
        }

        [Test]
        public void AMoverThatStopsShortDoesNotCollide()
        {
            var b = new float3(10f, 0f, 0f);
            Assert.IsFalse(PrismMotion.EntersContact(float3.zero, new float3(7.9f, 0f, 0f), b, b, 2f, out _));
        }

        [Test]
        public void AMoverThatPassesWideDoesNotCollide()
        {
            var b = new float3(10f, 3f, 0f);
            Assert.IsFalse(PrismMotion.EntersContact(float3.zero, new float3(20f, 0f, 0f), b, b, 2f, out _));
        }

        [Test]
        public void ATouchingPairIsAtRest_NotACollision()
        {
            // A trail's neighbours: already within reach when the step begins.
            var b = new float3(1.5f, 0f, 0f);
            Assert.IsFalse(PrismMotion.EntersContact(float3.zero, new float3(1f, 0f, 0f), b, b, 2f, out _));
        }

        [Test]
        public void ASeparatingPairDoesNotCollide()
        {
            var b = new float3(5f, 0f, 0f);
            Assert.IsFalse(PrismMotion.EntersContact(float3.zero, new float3(-5f, 0f, 0f), b, b, 2f, out _));
        }

        [Test]
        public void TwoMoversCollideByTheirRelativeMotion()
        {
            // Head-on, closing 9 over the step from 10 apart: they are 2 apart after 8 of it.
            Assert.IsTrue(PrismMotion.EntersContact(float3.zero, new float3(4f, 0f, 0f),
                new float3(10f, 0f, 0f), new float3(5f, 0f, 0f), 2f, out float t));
            Assert.AreEqual(8f / 9f, t, Eps);
            // Neither alone would have reached the other's start.
            var b = new float3(10f, 0f, 0f);
            Assert.IsFalse(PrismMotion.EntersContact(float3.zero, new float3(4f, 0f, 0f), b, b, 2f, out _));
        }

        [Test]
        public void TwoMoversInLockstepDoNotCollide()
        {
            var step = new float3(5f, 1f, 0f);
            Assert.IsFalse(PrismMotion.EntersContact(float3.zero, step,
                new float3(3f, 0f, 0f), new float3(3f, 0f, 0f) + step, 2f, out _));
        }

        [Test]
        public void TheRuleIsSymmetric()
        {
            float3 a0 = new(0f, 0f, 0f), a1 = new(9f, 2f, 0f), b0 = new(10f, 0f, 1f), b1 = new(8f, 1f, 0f);
            bool ab = PrismMotion.EntersContact(a0, a1, b0, b1, 2.5f, out float tab);
            bool ba = PrismMotion.EntersContact(b0, b1, a0, a1, 2.5f, out float tba);
            Assert.AreEqual(ab, ba);
            if (ab) Assert.AreEqual(tab, tba, Eps);
        }

        // ---------------------------------------------------------------- boxes

        static PrismMotion.Box Slab(float3 center, float3 half, quaternion rotation) => new()
        {
            Center = center,
            X = math.mul(rotation, new float3(1f, 0f, 0f)),
            Y = math.mul(rotation, new float3(0f, 1f, 0f)),
            Z = math.mul(rotation, new float3(0f, 0f, 1f)),
            Half = half,
        };

        static PrismMotion.Box Unit(float3 center) => Slab(center, new float3(0.5f), quaternion.identity);

        [Test]
        public void ABoxReachingAStaticBoxCollides_AtTheFirstTouch()
        {
            // Faces meet when the centres are 1 apart: x = 9 of the 20 travelled.
            Assert.IsTrue(PrismMotion.EntersContact(Unit(float3.zero), new float3(20f, 0f, 0f),
                Unit(new float3(10f, 0f, 0f)), float3.zero, out float t));
            Assert.AreEqual(9f / 20f, t, Eps);
        }

        [Test]
        public void ABoxThatStopsShortOrPassesWideDoesNotCollide()
        {
            var b = Unit(new float3(10f, 0f, 0f));
            Assert.IsFalse(PrismMotion.EntersContact(Unit(float3.zero), new float3(8.9f, 0f, 0f), b, float3.zero, out _));
            Assert.IsFalse(PrismMotion.EntersContact(Unit(new float3(0f, 1.1f, 0f)), new float3(20f, 0f, 0f), b, float3.zero, out _));
        }

        [Test]
        public void OverlappingBoxesAreAtRest_NotACollision()
        {
            Assert.IsFalse(PrismMotion.EntersContact(Unit(float3.zero), new float3(0.5f, 0f, 0f),
                Unit(new float3(0.8f, 0f, 0f)), float3.zero, out _));
        }

        [Test]
        public void ALongSlabIsMetAlongItsLength_WhereItsSphereWouldMiss()
        {
            // A 20-long slab lying along x; a unit box falls past its end, 8 out from its centre.
            var slab = Slab(float3.zero, new float3(10f, 0.25f, 0.25f), quaternion.identity);
            var faller = Unit(new float3(8f, 5f, 0f));
            Assert.IsTrue(PrismMotion.EntersContact(faller, new float3(0f, -10f, 0f), slab, float3.zero, out float t));
            Assert.AreEqual((5f - 0.75f) / 10f, t, Eps);

            // Its volume's sphere is ~1.06 across the radius: the same fall never comes within reach.
            float reach = PrismMotion.ContactRadius(20f * 0.5f * 0.5f) + PrismMotion.ContactRadius(1f);
            Assert.IsFalse(PrismMotion.EntersContact(new float3(8f, 5f, 0f), new float3(8f, -5f, 0f),
                float3.zero, float3.zero, reach, out _));
        }

        [Test]
        public void ABoxPassingDiagonallyBetweenRotatedBoxesUsesTheEdgeAxes()
        {
            // A unit box turned 45° about z points its corner at x = -0.1. A 0.1 cube falling straight down
            // x = 0 clears that corner by 0.05 (only the edge axes can see the gap); pushed sideways, it hits.
            var turned = quaternion.RotateZ(math.radians(45f));
            float corner = 0.5f * math.SQRT2;
            var left = Slab(new float3(-(corner + 0.1f), 0f, 0f), new float3(0.5f), turned);
            var faller = Slab(new float3(0f, 5f, 0f), new float3(0.05f), quaternion.identity);
            Assert.IsFalse(PrismMotion.EntersContact(faller, new float3(0f, -10f, 0f), left, float3.zero, out _));
            Assert.IsTrue(PrismMotion.EntersContact(faller, new float3(-0.5f, -10f, 0f), left, float3.zero, out _));
        }

        [Test]
        public void TwoMovingBoxesCollideByTheirRelativeMotion_AndNotInLockstep()
        {
            var a = Unit(float3.zero);
            var b = Unit(new float3(10f, 0f, 0f));
            // Closing 10 over the step with 9 of air between the faces: first touch at 0.9.
            Assert.IsTrue(PrismMotion.EntersContact(a, new float3(4f, 0f, 0f), b, new float3(-6f, 0f, 0f), out float t));
            Assert.AreEqual(0.9f, t, Eps);
            Assert.IsFalse(PrismMotion.EntersContact(a, new float3(5f, 1f, 0f), b, new float3(5f, 1f, 0f), out _));
        }

        [Test]
        public void TheBoxRuleIsSymmetric()
        {
            var a = Slab(float3.zero, new float3(2f, 0.3f, 0.6f), quaternion.RotateY(0.7f));
            var b = Slab(new float3(9f, 1f, 0.5f), new float3(0.4f, 1.5f, 0.4f), quaternion.RotateX(1.1f));
            float3 sa = new(8f, 0.5f, 0f), sb = new(-2f, 0f, 0.3f);
            bool ab = PrismMotion.EntersContact(a, sa, b, sb, out float tab);
            bool ba = PrismMotion.EntersContact(b, sb, a, sa, out float tba);
            Assert.IsTrue(ab);
            Assert.AreEqual(ab, ba);
            Assert.AreEqual(tab, tba, Eps);
        }
    }
}
