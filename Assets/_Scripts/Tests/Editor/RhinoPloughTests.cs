using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's hull plough (R_VesselActions/RHINO_PLOUGH.md): a rammed prism is shoved to its
    /// own side of the centreline until it clears the hull, never destroyed. These pin the pure
    /// geometry - side choice, idempotence (a multi-collider hull reports one ram twice) and the
    /// support distances the clearance is built from.
    /// </summary>
    public class RhinoPloughTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void APrismIsShovedToTheSideItAlreadySatOn()
        {
            Assert.Greater(RhinoPlough.LateralShove(2f, 10f, 0f), 0f);
            Assert.Less(RhinoPlough.LateralShove(-2f, 10f, 0f), 0f);
        }

        [Test]
        public void ItLandsAtClearancePlusThrow()
        {
            float lateral = 3f, clearance = 10f, throwDistance = 4f;
            float shove = RhinoPlough.LateralShove(lateral, clearance, throwDistance);
            Assert.AreEqual(clearance + throwDistance, lateral + shove, Eps);

            float left = RhinoPlough.LateralShove(-lateral, clearance, throwDistance);
            Assert.AreEqual(-(clearance + throwDistance), -lateral + left, Eps);
        }

        [Test]
        public void ADeadCentreHitStillMovesTheFullClearance()
        {
            float shove = RhinoPlough.LateralShove(0f, 10f, 0f);
            Assert.AreEqual(10f, Mathf.Abs(shove), Eps);
        }

        [Test]
        public void ASecondReportOfTheSameRamDoesNothing()
        {
            float lateral = 1.5f, clearance = 8f, throwDistance = 6f;
            float after = lateral + RhinoPlough.LateralShove(lateral, clearance, throwDistance);
            Assert.AreEqual(0f, RhinoPlough.LateralShove(after, clearance, throwDistance));
        }

        [Test]
        public void APrismAlreadyClearIsNotMoved()
        {
            Assert.AreEqual(0f, RhinoPlough.LateralShove(12f, 10f, 5f));
            Assert.AreEqual(0f, RhinoPlough.LateralShove(-10f, 10f, 5f));
        }

        [Test]
        public void ANegativeThrowIsTreatedAsNone()
        {
            Assert.AreEqual(10f, RhinoPlough.LateralShove(0f, 10f, -3f), Eps);
        }

        [Test]
        public void BoxSupportIsTheHalfSizeOnAnAxisAndTheSumOnADiagonal()
        {
            var half = new Vector3(1f, 2f, 3f);
            Assert.AreEqual(1f, RhinoPlough.BoxHalfExtentAlong(Vector3.right,
                Vector3.right, Vector3.up, Vector3.forward, half), Eps);

            var diag = new Vector3(1f, 1f, 0f).normalized;
            Assert.AreEqual((1f + 2f) * diag.x, RhinoPlough.BoxHalfExtentAlong(diag,
                Vector3.right, Vector3.up, Vector3.forward, half), Eps);
        }

        [Test]
        public void OctahedronSupportIsTheLargestVertexProjection()
        {
            // Vertices sit ON the axes, so a diagonal reaches LESS far than the box's corner would.
            var half = new Vector3(3f, 3f, 3f);
            var diag = new Vector3(1f, 1f, 0f).normalized;
            Assert.AreEqual(3f, RhinoPlough.OctahedronHalfExtentAlong(Vector3.right,
                Vector3.right, Vector3.up, Vector3.forward, half), Eps);
            Assert.AreEqual(3f * diag.x, RhinoPlough.OctahedronHalfExtentAlong(diag,
                Vector3.right, Vector3.up, Vector3.forward, half), Eps);
        }

        [Test]
        public void HullReachIsTheFurthestSupportPointOnThatSide()
        {
            var go = new GameObject("PloughHull");
            try
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 2f, 10f);
                box.center = new Vector3(1f, 0f, 0f);
                var hull = new Collider[] { box };

                // Right: centre +1, half-width 2 -> reaches 3. Left: centre -1 on that side -> 1.
                Assert.AreEqual(3f, RhinoPlough.HullReach(hull, Vector3.zero, Vector3.right, 1f), Eps);
                Assert.AreEqual(1f, RhinoPlough.HullReach(hull, Vector3.zero, Vector3.right, -1f), Eps);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ADisabledHullColliderDoesNotWidenTheFurrow()
        {
            var go = new GameObject("PloughHull");
            try
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(40f, 2f, 2f);
                box.enabled = false;
                Assert.AreEqual(0f, RhinoPlough.HullReach(new Collider[] { box }, Vector3.zero,
                    Vector3.right, 1f), Eps);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
