#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The one piece of arithmetic the Scarab's switch anchoring turns on: a plant's heart is
    /// measured against the SEGMENT from the ship to the projected ring centre, never against that
    /// projected point.
    ///
    /// <para>This is a regression suite before it is a maths suite. The rule shipped once as a
    /// proximity test on the point <c>placementDistance</c> (150u) ahead of the nose, which is an
    /// ANNULUS rather than a reach: a press was admitted only from roughly 80u to 220u out and
    /// refused at every range inside 80 — i.e. point-blank, which is exactly where the objective
    /// arrow puts a pilot. It read on screen as a dead button, and the AI was unaffected because it
    /// presses on a pacing timer while still approaching, which is why the first report was "the AI
    /// placed rings at the right points, I could not place any at all".</para>
    ///
    /// <para>Tested through <see cref="FloraHeartRegistry.DistanceToSegment"/> rather than through
    /// <c>ScarabSwitchAnchors.TryResolve</c>: the resolver needs live <c>Flora</c> components and a
    /// live switch roster, while the property that actually failed is pure geometry.</para>
    /// </summary>
    public class ScarabSwitchAnchorGeometryTests
    {
        // PlaceSwitchActionSO's shipped values.
        const float PlacementDistance = 150f;
        const float AnchorReach = 70f;

        static float ToSegment(Vector3 heart, Vector3 ship, Vector3 course) =>
            FloraHeartRegistry.DistanceToSegment(heart, ship, ship + course * PlacementDistance);

        /// <summary>
        /// A heart dead ahead is claimable at EVERY range inside reach — including 5u, the case the
        /// endpoint-only rule refused.
        /// </summary>
        [TestCase(5f)]
        [TestCase(40f)]
        [TestCase(79f)]
        [TestCase(150f)]
        [TestCase(210f)]
        public void AimedAtAHeart_IsAdmittedAtEveryRangeInReach(float range)
        {
            var ship = new Vector3(-range, 0f, 0f);
            var course = Vector3.right;
            var heart = Vector3.zero;

            Assert.LessOrEqual(ToSegment(heart, ship, course), AnchorReach,
                $"a heart {range}u dead ahead must be claimable");
        }

        /// <summary>
        /// The negative control that names the bug: at point-blank the projected centre is a long
        /// way PAST the heart, so an endpoint-only test refuses a press aimed straight at it.
        /// </summary>
        [Test]
        public void PointBlank_WasRefusedByTheOldEndpointOnlyRule()
        {
            var ship = new Vector3(-5f, 0f, 0f);
            var projected = ship + Vector3.right * PlacementDistance;
            float endpointDistance = Vector3.Distance(Vector3.zero, projected);

            Assert.Greater(endpointDistance, AnchorReach,
                "the endpoint-only rule is supposed to refuse this - if it does not, the test no "
                + "longer reproduces the bug it guards");
            Assert.LessOrEqual(ToSegment(Vector3.zero, ship, Vector3.right), AnchorReach,
                "...while the segment rule admits it");
        }

        /// <summary>
        /// Flying AWAY from a heart is a refusal — the gate has to constrain something. Measured
        /// from 100u out, because a heart a few units behind the nose is genuinely within reach and
        /// admitting it is correct: the rule is "is this plant near my flight path", not "am I
        /// pointing at it".
        /// </summary>
        [Test]
        public void AimedAwayFromAHeart_IsRefused()
        {
            var ship = new Vector3(-100f, 0f, 0f);
            Assert.Greater(ToSegment(Vector3.zero, ship, Vector3.left), AnchorReach);
        }

        /// <summary>A heart well off the flight path is a refusal however close the ship passes it.</summary>
        [Test]
        public void OffThePath_IsRefused()
        {
            var ship = Vector3.zero;
            var heart = new Vector3(75f, 200f, 0f);
            Assert.Greater(ToSegment(heart, ship, Vector3.right), AnchorReach);
        }

        /// <summary>
        /// The segment is a SEGMENT, not a ray: a heart 400u beyond the projected centre is out of
        /// reach even though it is perfectly on the line. Without the clamp the whole course ahead
        /// of the ship would be claimable, which is the free placement the anchor rule removes.
        /// </summary>
        [Test]
        public void FarBeyondTheProjectedCentre_IsRefused()
        {
            var ship = Vector3.zero;
            var heart = new Vector3(PlacementDistance + 400f, 0f, 0f);
            Assert.Greater(ToSegment(heart, ship, Vector3.right), AnchorReach);
        }

        /// <summary>A degenerate segment (ship and centre coincident) collapses to a point test.</summary>
        [Test]
        public void DegenerateSegment_IsAPointTest()
        {
            var p = new Vector3(3f, 4f, 0f);
            Assert.AreEqual(5f, FloraHeartRegistry.DistanceToSegment(p, Vector3.zero, Vector3.zero),
                            1e-4f);
        }
    }
}
#endif
