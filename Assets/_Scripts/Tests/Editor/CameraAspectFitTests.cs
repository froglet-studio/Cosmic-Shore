using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Screens narrower than 16:9 keep the authored 16:9 width of view and gain height; 16:9 and
    /// wider render exactly as authored; the authored field of view survives the switch to the
    /// physical gate.
    /// </summary>
    public class CameraAspectFitTests
    {
        const float Fov = 60f;

        [Test]
        public void SixteenByNineAndWider_KeepTheAuthoredAngle()
        {
            Assert.AreEqual(Fov, CameraAspectFit.EffectiveVerticalFov(Fov, 16f / 9f), 1e-3f);
            Assert.AreEqual(Fov, CameraAspectFit.EffectiveVerticalFov(Fov, 2556f / 1179f), 1e-3f);
        }

        [Test]
        public void FourByThree_ShowsMoreHeight()
        {
            // tan(30 deg) * (16/9) / (4/3) = 0.7698 -> 2 * atan = 75.19 deg.
            Assert.AreEqual(75.19f, CameraAspectFit.EffectiveVerticalFov(Fov, 4f / 3f), 0.05f);
        }

        [Test]
        public void Apply_KeepsTheFieldOfViewAndSetsTheGate()
        {
            var go = new GameObject("CameraAspectFitTest");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = Fov;

                CameraAspectFit.Apply(cam);

                Assert.IsTrue(cam.usePhysicalProperties);
                Assert.AreEqual(Camera.GateFitMode.Overscan, cam.gateFit);
                Assert.AreEqual(CameraAspectFit.SensorSize, cam.sensorSize);
                Assert.AreEqual(Fov, cam.fieldOfView, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Projection_MatchesTheFitAtFourByThreeAndSixteenByNine()
        {
            var go = new GameObject("CameraAspectFitProjectionTest");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.fieldOfView = Fov;
                CameraAspectFit.Apply(cam);

                foreach (float aspect in new[] { 4f / 3f, 16f / 9f, 21f / 9f })
                {
                    cam.aspect = aspect;
                    float shown = 2f * Mathf.Atan(1f / cam.projectionMatrix[1, 1]) * Mathf.Rad2Deg;
                    Assert.AreEqual(CameraAspectFit.EffectiveVerticalFov(Fov, aspect), shown, 0.5f,
                        $"vertical view at aspect {aspect:0.###}");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
