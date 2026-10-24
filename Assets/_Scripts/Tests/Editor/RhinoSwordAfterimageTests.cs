using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's Mass afterimage cuts the blade into segments (R_VesselActions/RHINO_SWORD_AFTERIMAGE.md).
    /// The tiling is the one piece of the ability that is pure, and the one that sets how much mass an
    /// afterimage is - so it is pinned here.
    /// </summary>
    public class RhinoSwordAfterimageTests
    {
        [TestCase(27f, 10f, 6, 3)]    // resting blade (30 x 0.9): three segments of 9
        [TestCase(45f, 10f, 6, 5)]    // Space-10 blade (50 x 0.9)
        [TestCase(108f, 10f, 6, 6)]   // full-energy blade (120 x 0.9) - capped
        [TestCase(10f, 10f, 6, 1)]    // exactly one segment long
        [TestCase(0.2f, 10f, 6, 1)]   // a stub still lays one
        public void SegmentCount_IsTheFewestSegmentsNoLongerThanTheLimit(float covered, float segment, int max,
                                                                         int expected)
        {
            Assert.AreEqual(expected, RhinoSwordAfterimageConfigSO.SegmentCount(covered, segment, max));
        }

        [Test]
        public void SegmentCount_NeverReturnsZero_EvenForADegenerateConfig()
        {
            Assert.AreEqual(1, RhinoSwordAfterimageConfigSO.SegmentCount(50f, 0f, 0));
        }
    }
}
