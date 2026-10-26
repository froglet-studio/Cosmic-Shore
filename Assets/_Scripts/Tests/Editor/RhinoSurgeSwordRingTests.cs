#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's omni-crystal Surge (RHINO_ENERGY_SWORD.md § "The omni-crystal Surge"): the ring's
    /// fan-out angles and the camera's surround framing, the two pieces of math the state's look rests on.
    /// </summary>
    [TestFixture]
    public class RhinoSurgeSwordRingTests
    {
        [Test]
        public void FullyOpen_SpreadsTheSwordsEvenlyRoundTheNose()
        {
            const int count = 8;
            for (int k = 1; k < count; k++)
                Assert.AreEqual(k * 45f, RhinoSurgeSwordRing.CopyAngleDegrees(k, count, 1f), 1e-4f);
        }

        [Test]
        public void Folded_PutsEveryCopyInsideTheBlade()
        {
            for (int k = 1; k < 8; k++)
                Assert.AreEqual(0f, RhinoSurgeSwordRing.CopyAngleDegrees(k, 8, 0f), 1e-6f);
        }

        [Test]
        public void FanOut_IsMonotonicAndEased()
        {
            float prev = -1f;
            for (int i = 0; i <= 20; i++)
            {
                float a = RhinoSurgeSwordRing.CopyAngleDegrees(3, 8, i / 20f);
                Assert.GreaterOrEqual(a, prev);
                prev = a;
            }
            // Eased: the first tenth of the time covers well under a tenth of the sweep.
            Assert.Less(RhinoSurgeSwordRing.CopyAngleDegrees(3, 8, 0.1f), 0.1f * 135f);
        }

        [Test]
        public void SurroundOffset_AtZero_IsTheAuthoredOffsetExactly()
        {
            var offset = new Vector3(3f, 12f, -70f);
            Assert.AreEqual(offset, CustomCameraController.SurroundOffset(offset, 0f, 2f));
        }

        [Test]
        public void SurroundOffset_AtOne_SitsStraightBehindAtTheScaledDistance()
        {
            var offset = new Vector3(0f, 0f, -70f);   // the Rhino's camera
            var framed = CustomCameraController.SurroundOffset(offset, 1f, 2f);
            Assert.AreEqual(0f, framed.x, 1e-4f);
            Assert.AreEqual(0f, framed.y, 1e-4f);
            Assert.AreEqual(-140f, framed.z, 1e-3f);
        }

        [Test]
        public void SurroundOffset_CentresARaisedCamera_AndKeepsItsDistance()
        {
            var offset = new Vector3(0f, 30f, -40f);   // 50 away, raised
            var framed = CustomCameraController.SurroundOffset(offset, 1f, 1f);
            Assert.AreEqual(0f, framed.y, 1e-4f);
            Assert.AreEqual(-50f, framed.z, 1e-3f);
        }

        [Test]
        public void SurroundOffset_UnderTheRearViewMirror_StaysAhead()
        {
            var framed = CustomCameraController.SurroundOffset(new Vector3(0f, 10f, 70f), 1f, 2f);
            Assert.Greater(framed.z, 0f);
        }
    }
}
#endif
