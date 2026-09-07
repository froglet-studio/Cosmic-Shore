using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The held camera's axis alignment (SCARAB.md §4.7). Every test here runs the candidate up
    /// vector through the SHIPPED <see cref="Quaternion.LookRotation"/> and asserts what the camera
    /// will actually do — the claim is about handedness, and a handedness bug looks like a correct
    /// implementation right up until you are sitting in the wrong place with the world upside down.
    /// </summary>
    public class AnchorAlignmentMathTests
    {
        const float Tol = 1e-3f;

        /// <summary>Where the camera's right ends up for a camera at <paramref name="dir"/> ×
        /// distance from the anchor, looking back at it with <paramref name="up"/>.</summary>
        static Vector3 RightOf(Vector3 dir, Vector3 up)
            => Quaternion.LookRotation((-dir).normalized, up) * Vector3.right;

        [Test]
        public void TheCamerasXAxisLandsOnTheAxis_FromEveryStartingVantage()
        {
            Vector3 axis = new Vector3(0.3f, 0.8f, -0.5f).normalized;

            // A spray of starting vantages, including ones nearly along the axis.
            Vector3[] starts =
            {
                Vector3.forward, Vector3.back, Vector3.right, Vector3.up,
                new Vector3(1f, 1f, 1f).normalized, new Vector3(-0.4f, 0.9f, 0.2f).normalized,
                (axis + new Vector3(0.02f, 0f, 0f)).normalized,
            };

            foreach (var start in starts)
            {
                Vector3 dir = AnchorAlignmentMath.InPlaneDirection(start, axis, Vector3.up);
                Vector3 up = AnchorAlignmentMath.UpFor(dir, axis, Vector3.up);
                Vector3 right = RightOf(dir, up);

                Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(right.normalized, axis)), Tol,
                    $"the camera's x axis must be PARALLEL to the orbit axis (start {start})");
            }
        }

        [Test]
        public void AlignmentIsTheSameStatementAsSittingInThePlane()
        {
            // The identity the whole design rests on: right ∥ axis ⟺ the camera lies in the plane
            // the anchor spins in, i.e. the view direction is perpendicular to the axis.
            Vector3 axis = new Vector3(-0.6f, 0.2f, 0.77f).normalized;
            Vector3 dir = AnchorAlignmentMath.InPlaneDirection(
                new Vector3(0.9f, -0.3f, 0.1f).normalized, axis, Vector3.up);

            Assert.AreEqual(0f, Vector3.Dot(dir, axis), Tol,
                "the aligned vantage lies in the orbit plane");
            Assert.AreEqual(1f, dir.magnitude, Tol, "and is a direction");
        }

        [Test]
        public void TheInPlaneDirectionIsTheNEARESTOne_SoEasingInIsTheShortestWay()
        {
            Vector3 axis = Vector3.up;
            Vector3 start = new Vector3(1f, 4f, 0f).normalized;   // steeply above the plane
            Vector3 dir = AnchorAlignmentMath.InPlaneDirection(start, axis, Vector3.up);

            Assert.Less((dir - Vector3.right).magnitude, Tol,
                "projecting straight down onto the plane is the nearest legal vantage");
        }

        [Test]
        public void ACameraSittingExactlyOnTheAxisStillGetsAVantage()
        {
            Vector3 axis = Vector3.up;
            Vector3 dir = AnchorAlignmentMath.InPlaneDirection(Vector3.up, axis, Vector3.forward);

            Assert.AreEqual(1f, dir.magnitude, Tol, "degenerate is a legal place to be, not an error");
            Assert.AreEqual(0f, Vector3.Dot(dir, axis), Tol, "and it still lands in the plane");
        }

        [Test]
        public void TheUpSignIsCHOSEN_SoEnteringTheHoldNeverFlipsTheWorld()
        {
            Vector3 axis = Vector3.right;
            Vector3 dir = Vector3.back;                       // camera behind the anchor

            Vector3 upA = AnchorAlignmentMath.UpFor(dir, axis, Vector3.up);
            Vector3 upB = AnchorAlignmentMath.UpFor(dir, axis, Vector3.down);

            Assert.Greater(Vector3.Dot(upA, Vector3.up), 0f, "keeps a right-way-up camera up");
            Assert.Greater(Vector3.Dot(upB, Vector3.down), 0f, "and an inverted one inverted");
            Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(RightOf(dir, upA).normalized, axis)), Tol,
                "both signs still align the x axis — that is why the sign is free to be chosen");
            Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(RightOf(dir, upB).normalized, axis)), Tol);
        }

        [Test]
        public void AROLLINGAxisRollsTheFrameContinuously_AndNeverSnapsOver()
        {
            // Yaw rolls the orbit axis about the camera's view direction. Stepping it a full turn
            // must roll the up vector smoothly all the way round: a sign choice made per frame
            // against the PREVIOUS up is what stops it flipping as it passes the halfway point.
            Vector3 view = Vector3.forward;
            Vector3 dir = -view;                              // camera looks along +z at the anchor
            Vector3 axis = Vector3.right;
            Vector3 up = AnchorAlignmentMath.UpFor(dir, axis, Vector3.up);
            Vector3 first = up;

            float maxStep = 0f;
            for (int i = 0; i < 360; i++)
            {
                axis = (Quaternion.AngleAxis(1f, view) * axis).normalized;
                Vector3 next = AnchorAlignmentMath.UpFor(dir, axis, up);
                maxStep = Mathf.Max(maxStep, Vector3.Angle(up, next));
                up = next;
                Assert.AreEqual(1f, Mathf.Abs(Vector3.Dot(RightOf(dir, up).normalized, axis)), Tol,
                    $"still aligned at step {i}");
            }

            Assert.Less(maxStep, 5f, "no frame may jump — a snap here is the world flipping over");
            Assert.Less(Vector3.Angle(up, first), 1f, "a full turn of the axis is a full roll home");
        }
    }
}
