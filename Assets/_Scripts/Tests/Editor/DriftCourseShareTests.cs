using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The drift-course blend keeps <see cref="VesselTransformer.DriftCourseShare"/> of the drifted course each frame.
    /// It compounds per frame, so a frame is run as the 60 fps frames it stands for: a 30 fps device must pull the
    /// course onto the nose as hard as a 60 fps one (2026-11-22: it pulled half as hard, and an AI Dolphin's commit
    /// drift held its locked course 2-5x longer at 30 fps).
    /// </summary>
    public class DriftCourseShareTests
    {
        const float Frame60 = 1f / 60f;

        [Test]
        public void A60FpsFrameKeepsTheDriftAmount()
        {
            foreach (float a in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
                Assert.AreEqual(a, VesselTransformer.DriftCourseShare(0.3f, a, Frame60), 1e-4f, $"amount {a}");
        }

        [Test]
        public void A30FpsFrameMatchesTwo60FpsFramesAlongTheRamp()
        {
            // The trigger ramp at 60 fps: 0.2, 0.4, 0.6, 0.8, 1. At 30 fps the same ramp reaches 0.4 and 0.8 a frame.
            float at60 = VesselTransformer.DriftCourseShare(0f, 0.2f, Frame60) * VesselTransformer.DriftCourseShare(0.2f, 0.4f, Frame60)
                       * VesselTransformer.DriftCourseShare(0.4f, 0.6f, Frame60) * VesselTransformer.DriftCourseShare(0.6f, 0.8f, Frame60);
            float at30 = VesselTransformer.DriftCourseShare(0f, 0.4f, 2f * Frame60) * VesselTransformer.DriftCourseShare(0.4f, 0.8f, 2f * Frame60);
            Assert.AreEqual(at60, at30, 1e-4f);
        }

        [Test]
        public void ASteadyPartialDriftCompoundsAtTheSameRatePerSecond()
        {
            float at60 = Mathf.Pow(VesselTransformer.DriftCourseShare(0.5f, 0.5f, Frame60), 60f);
            float at30 = Mathf.Pow(VesselTransformer.DriftCourseShare(0.5f, 0.5f, 2f * Frame60), 30f);
            float at45 = Mathf.Pow(VesselTransformer.DriftCourseShare(0.5f, 0.5f, 1f / 45f), 45f);
            Assert.AreEqual(at60, at30, 1e-5f);
            Assert.AreEqual(at60, at45, 1e-5f);
        }

        [Test]
        public void AFullDriftKeepsTheWholeCourseAndNoDriftNone()
        {
            Assert.AreEqual(1f, VesselTransformer.DriftCourseShare(1f, 1f, 0.05f), 1e-6f);
            Assert.AreEqual(0f, VesselTransformer.DriftCourseShare(0f, 0f, 0.05f), 1e-6f);
        }
    }
}
