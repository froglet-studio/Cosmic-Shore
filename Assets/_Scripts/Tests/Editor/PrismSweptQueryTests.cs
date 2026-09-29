using CosmicShore.Gameplay;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Edit-mode coverage for the point-to-segment metric behind
    /// <see cref="PrismSpatialIndex.QuerySegment"/> — the swept prism query that closes the
    /// projectile tunneling gap. The query itself needs a live index (NativeArrays + the
    /// singleton), so what is unit-testable is its correctness core: the distance function
    /// that decides whether a prism sat on the path a projectile teleported across.
    ///
    /// Lives under an Editor/ folder per CLAUDE.md — a test anywhere else compiles into the
    /// player and breaks the Windows build at the IL2CPP linker.
    /// </summary>
    public class PrismSweptQueryTests
    {
        static float Dist(Vector3 p, Vector3 a, Vector3 b)
        {
            float3 ab = (float3)(Vector3)(b - a);
            return Mathf.Sqrt(PrismSpatialIndex.DistanceToSegmentSq(
                (float3)(Vector3)p, (float3)(Vector3)a, ab, math.lengthsq(ab)));
        }

        [Test]
        public void PointOnTheSegment_IsZeroAway()
        {
            Assert.AreEqual(0f, Dist(new Vector3(5f, 0f, 0f), Vector3.zero, new Vector3(10f, 0f, 0f)), 1e-4f);
        }

        [Test]
        public void PerpendicularOffset_IsTheOffset()
        {
            // The whole point of the swept test: a prism sitting BESIDE the middle of the
            // step is measured from the path, not from either endpoint. Under the old
            // point-sampled trigger this prism was invisible.
            Assert.AreEqual(3f, Dist(new Vector3(5f, 3f, 0f), Vector3.zero, new Vector3(10f, 0f, 0f)), 1e-4f);
        }

        [Test]
        public void PastTheEnd_ClampsToTheEndpoint()
        {
            // Clamped, not infinite-line: a prism beyond where the projectile stopped this
            // frame must NOT be hit — it gets its chance next frame.
            Assert.AreEqual(5f, Dist(new Vector3(15f, 0f, 0f), Vector3.zero, new Vector3(10f, 0f, 0f)), 1e-4f);
            Assert.AreEqual(5f, Dist(new Vector3(-5f, 0f, 0f), Vector3.zero, new Vector3(10f, 0f, 0f)), 1e-4f);
        }

        [Test]
        public void DegenerateSegment_ReducesToAPointDistance()
        {
            // A stationary projectile (or a zero-length step) must behave exactly like the
            // sphere query it generalizes.
            Assert.AreEqual(7f, Dist(new Vector3(0f, 7f, 0f), Vector3.zero, Vector3.zero), 1e-4f);
        }

        [Test]
        public void MidStepPrism_IsFound_WhereThePointSampleMissedIt()
        {
            // The shipped geometry, as a regression: a Sparrow round at its base 375 u/s
            // crosses 6.25 u in a 60 fps frame behind a 0.825 hit radius. A prism centred a
            // third of the way along that step, 1 u off-axis, is well inside contact range —
            // and PhysX, which only ever samples the endpoints, never saw it.
            var from = Vector3.zero;
            var to = new Vector3(0f, 0f, 6.25f);
            var prism = new Vector3(1f, 0f, 2.1f);

            const float bulletRadius = 0.825f;
            const float prismBoundingRadius = 1.5f;

            Assert.That(Dist(prism, from, to), Is.LessThan(bulletRadius + prismBoundingRadius),
                "the swept test must find a prism sitting mid-step");
            Assert.That(Vector3.Distance(prism, from), Is.GreaterThan(bulletRadius + prismBoundingRadius * 0.5f));
            Assert.That(Vector3.Distance(prism, to), Is.GreaterThan(bulletRadius + prismBoundingRadius * 0.5f),
                "and it must genuinely be out of reach of both sampled endpoints");
        }

        [Test]
        public void OffPathPrism_IsStillRejected()
        {
            // Restoring path coverage must not turn the gun into an area-of-effect weapon:
            // a prism well off the line is still a miss.
            var from = Vector3.zero;
            var to = new Vector3(0f, 0f, 6.25f);
            Assert.That(Dist(new Vector3(9f, 0f, 3f), from, to), Is.GreaterThan(8f));
        }
    }
}
