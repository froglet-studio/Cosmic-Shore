#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The mouse camera's three claims, held against its own pure math (<see cref="MouseOrbitCamera"/>):
    /// a pan moves the world exactly with the cursor at the pivot's depth, a zoom toward the cursor
    /// keeps the point under it fixed on screen, and a wheel reading is a notch on every platform.
    /// Plus the shipped bindings are reachable.
    /// </summary>
    public class MouseOrbitCameraTests
    {
        const float Fov = 60f;
        const float Aspect = 16f / 9f;

        /// <summary>A perspective projection to viewport (0..1), written independently of the camera's own math.</summary>
        static Vector2 Project(Vector3 point, Vector3 eye, Quaternion rotation)
        {
            var local = Quaternion.Inverse(rotation) * (point - eye);
            float tanHalf = Mathf.Tan(0.5f * Fov * Mathf.Deg2Rad);
            return new Vector2(
                0.5f + 0.5f * local.x / (local.z * tanHalf * Aspect),
                0.5f + 0.5f * local.y / (local.z * tanHalf));
        }

        [Test]
        public void PositionFor_SitsBehindThePivotAlongTheViewAxis()
        {
            var rot = Quaternion.Euler(30f, 45f, 0f);
            var eye = MouseOrbitCamera.PositionFor(new Vector3(1f, 2f, 3f), rot, 100f);
            Assert.AreEqual(100f, Vector3.Distance(eye, new Vector3(1f, 2f, 3f)), 1e-3f);
            Assert.Greater(Vector3.Dot((new Vector3(1f, 2f, 3f) - eye).normalized, rot * Vector3.forward), 0.9999f);
        }

        [Test]
        public void PointAtPivotDepth_ProjectsBackToTheViewportPointItCameFrom()
        {
            var pivot = new Vector3(10f, -5f, 40f);
            var rot = Quaternion.Euler(-20f, 130f, 0f);
            const float dist = 250f;
            var eye = MouseOrbitCamera.PositionFor(pivot, rot, dist);
            foreach (var uv in new[] { new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.9f), new Vector2(0.83f, 0.27f) })
            {
                var p = MouseOrbitCamera.PointAtPivotDepth(pivot, rot, dist, Fov, Aspect, uv.x, uv.y);
                var back = Project(p, eye, rot);
                Assert.AreEqual(uv.x, back.x, 1e-4f);
                Assert.AreEqual(uv.y, back.y, 1e-4f);
                // ...and it lies on the plane through the pivot facing the camera.
                Assert.AreEqual(0f, Vector3.Dot(p - pivot, rot * Vector3.forward), 1e-2f);
            }
        }

        [Test]
        public void ZoomTowardCursor_KeepsThePointUnderTheCursorFixedOnScreen()
        {
            var pivot = new Vector3(0f, 0f, 0f);
            var rot = Quaternion.Euler(15f, -40f, 0f);
            const float oldDist = 400f;
            var uv = new Vector2(0.78f, 0.31f);
            var point = MouseOrbitCamera.PointAtPivotDepth(pivot, rot, oldDist, Fov, Aspect, uv.x, uv.y);

            foreach (float newDist in new[] { 340f, 120f, 900f })
            {
                var newPivot = MouseOrbitCamera.ZoomPivotTowardPoint(pivot, point, oldDist, newDist);
                var eye = MouseOrbitCamera.PositionFor(newPivot, rot, newDist);
                var screen = Project(point, eye, rot);
                Assert.AreEqual(uv.x, screen.x, 1e-4f, $"zoom to {newDist} moved the cursor point horizontally");
                Assert.AreEqual(uv.y, screen.y, 1e-4f, $"zoom to {newDist} moved the cursor point vertically");
            }
            // Zooming toward the CENTRE moves nothing sideways.
            var centre = MouseOrbitCamera.PointAtPivotDepth(pivot, rot, oldDist, Fov, Aspect, 0.5f, 0.5f);
            Assert.AreEqual(0f, Vector3.Distance(pivot, MouseOrbitCamera.ZoomPivotTowardPoint(pivot, centre, oldDist, 100f)), 1e-3f);
        }

        [Test]
        public void Pan_MovesTheWorldExactlyWithTheCursorAtThePivotDepth()
        {
            const float screenHeight = 1080f;
            const float dist = 300f;
            var rot = Quaternion.Euler(10f, 25f, 0f);
            var pivot = Vector3.zero;
            float wpp = MouseOrbitCamera.WorldPerPixel(dist, Fov, screenHeight);

            // A point at the pivot, then the cursor drags 120 px right: the pivot moves left by
            // 120 * wpp, and the point must now project 120 px right of where it was.
            var point = pivot;
            var newPivot = pivot - rot * Vector3.right * (120f * wpp);
            var before = Project(point, MouseOrbitCamera.PositionFor(pivot, rot, dist), rot);
            var after = Project(point, MouseOrbitCamera.PositionFor(newPivot, rot, dist), rot);
            float movedPixels = (after.x - before.x) * screenHeight * Aspect;
            Assert.AreEqual(120f, movedPixels, 0.05f);
        }

        [Test]
        public void WheelNotches_NormalisesBothInputSystemScales()
        {
            Assert.AreEqual(1f, MouseOrbitCamera.WheelNotches(120f), 1e-6f);
            Assert.AreEqual(-2f, MouseOrbitCamera.WheelNotches(-240f), 1e-6f);
            Assert.AreEqual(1f, MouseOrbitCamera.WheelNotches(1f), 1e-6f);
            Assert.AreEqual(-1f, MouseOrbitCamera.WheelNotches(-1f), 1e-6f);
            Assert.AreEqual(0f, MouseOrbitCamera.WheelNotches(0f));
        }

        [Test]
        public void ShippedConfig_IsSaneAndBindsTheRequestedButtons()
        {
            var config = AssetDatabase.LoadAssetAtPath<MouseOrbitCameraConfigSO>(
                "Assets/Resources/" + MouseOrbitCamera.ConfigResourcePath + ".asset");
            Assert.IsNotNull(config, "Assets/Resources/MouseOrbitCameraConfig.asset is missing.");
            Assert.IsTrue(config.IsSane, "two gestures share a button — one of them is unreachable.");
            // The bindings the camera was asked for: right-drag pans, the middle button zooms.
            Assert.AreEqual(MouseOrbitCameraConfigSO.MouseButtonBinding.Right, config.PanButton);
            Assert.AreEqual(MouseOrbitCameraConfigSO.MouseButtonBinding.Middle, config.ZoomDragButton);
        }
    }
}
#endif
