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
    /// both call <see cref="PrismMotion.EntersContact"/>, keep one rule.
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
    }
}
