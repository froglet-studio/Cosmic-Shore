#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The geometry behind <c>PrismSpatialIndex.CountInCone</c> — "how much prism mass stands
    /// between a lens and the thing it is looking at".
    ///
    /// <para>The predicate is tested rather than the query because the bucket walk is copied
    /// verbatim from <c>QuerySegment</c> (already in service) while the cone test is new, and
    /// because a cone that is quietly a capsule produces plausible-looking counts forever: it
    /// would still rank an open vantage above a buried one, just with the wrong prisms, and
    /// nothing on screen would say so.</para>
    /// </summary>
    public class PrismConeQueryTests
    {
        const float Radius = 10f;

        static readonly float3 Apex = new(0f, 0f, 0f);
        static readonly float3 Axis = new(0f, 0f, 100f);   // apex → base
        static readonly float AxisLenSq = 100f * 100f;

        static bool Inside(float3 p) =>
            PrismSpatialIndex.IsInsideCone(p, Apex, Axis, AxisLenSq, Radius);

        [Test]
        public void APointOnTheAxisBetweenTheEndsIsInside()
        {
            Assert.IsTrue(Inside(new float3(0f, 0f, 50f)));
            Assert.IsTrue(Inside(new float3(0f, 0f, 1f)));
            Assert.IsTrue(Inside(new float3(0f, 0f, 99f)));
        }

        /// <summary>
        /// The cone WIDENS: the allowance is nothing at the lens and the full subject radius at
        /// the subject, because a prism a metre off the axis at the far end barely clips the
        /// silhouette while the same prism at the lens fills the frame.
        /// </summary>
        [Test]
        public void TheAllowanceGrowsLinearlyFromNothingAtTheLens()
        {
            // Halfway along, the cone is half as wide as it is at the base.
            Assert.IsTrue(Inside(new float3(Radius * 0.49f, 0f, 50f)), "inside the half-width");
            Assert.IsFalse(Inside(new float3(Radius * 0.51f, 0f, 50f)), "outside the half-width");

            // The same offset near the base is comfortably inside — which is the whole difference
            // between this and a constant-radius capsule.
            Assert.IsTrue(Inside(new float3(Radius * 0.51f, 0f, 95f)));
        }

        /// <summary>
        /// The NEGATIVE CONTROL for the trap the implementation names: a clamped axial parameter
        /// rounds a point behind the apex onto the apex and a point past the base onto the base
        /// disc, turning the cone into a capsule with rounded caps — so mass BEHIND the camera and
        /// mass BEYOND the ship would both be counted as standing in the way.
        /// </summary>
        [Test]
        public void MassOnTheWrongSideOfEitherEndIsNotInTheWay()
        {
            // Behind the lens, well within the base radius: a capsule would take it.
            Assert.IsFalse(Inside(new float3(0f, 0f, -1f)));
            Assert.IsFalse(Inside(new float3(Radius * 0.5f, 0f, -5f)));

            // Past the subject, on the axis: a capsule would take this one too.
            Assert.IsFalse(Inside(new float3(0f, 0f, 101f)));
            Assert.IsFalse(Inside(new float3(0f, 0f, 150f)));

            // Exactly at either end is excluded rather than ambiguous.
            Assert.IsFalse(Inside(Apex));
            Assert.IsFalse(Inside(new float3(0f, 0f, 100f)));
        }

        [Test]
        public void AWideOffsetIsOutsideAtEveryDepth()
        {
            for (float t = 0.05f; t < 1f; t += 0.05f)
                Assert.IsFalse(
                    Inside(new float3(Radius * 1.01f, 0f, 100f * t)),
                    $"an offset past the base radius must be outside at t={t:0.00}");
        }

        /// <summary>
        /// Orientation-independent: the predicate is pure vector algebra, so an arbitrarily posed
        /// camera must get the same answer as the axis-aligned case above.
        /// </summary>
        [Test]
        public void TheAnswerDoesNotDependOnHowTheConeIsPosed()
        {
            var rotation = Quaternion.Euler(37f, -114f, 61f);
            float3 apex = new(123f, -45f, 678f);
            float3 axis = rotation * new Vector3(0f, 0f, 100f);
            float axisLenSq = math.lengthsq(axis);

            float3 Posed(float3 local) => apex + (float3)(rotation * (Vector3)local);

            Assert.IsTrue(PrismSpatialIndex.IsInsideCone(
                Posed(new float3(Radius * 0.49f, 0f, 50f)), apex, axis, axisLenSq, Radius));
            Assert.IsFalse(PrismSpatialIndex.IsInsideCone(
                Posed(new float3(Radius * 0.51f, 0f, 50f)), apex, axis, axisLenSq, Radius));
            Assert.IsFalse(PrismSpatialIndex.IsInsideCone(
                Posed(new float3(0f, 0f, -5f)), apex, axis, axisLenSq, Radius));
        }
    }
}
#endif
