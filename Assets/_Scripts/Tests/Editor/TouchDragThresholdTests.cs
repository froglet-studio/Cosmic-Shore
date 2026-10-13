using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The drag threshold scales with pixel density so a phone tap stays a tap, and leaves a
    /// desktop exactly as authored.
    /// </summary>
    public class TouchDragThresholdTests
    {
        [Test]
        public void Phone_ScalesUnityDefault()
        {
            // 460 ppi iPhone: 10 * 460 / 160 = 28.75 -> 29 px, about 1.6 mm.
            Assert.AreEqual(29, TouchDragThreshold.Scaled(10, 460f));
        }

        [Test]
        public void Desktop_KeepsAuthoredValue()
        {
            Assert.AreEqual(10, TouchDragThreshold.Scaled(10, 96f));
            Assert.AreEqual(10, TouchDragThreshold.Scaled(10, 160f));
        }

        [Test]
        public void UnknownDensity_LeavesValueAlone()
        {
            Assert.AreEqual(10, TouchDragThreshold.Scaled(10, 0f));
            Assert.AreEqual(10, TouchDragThreshold.Scaled(10, -1f));
        }

        [Test]
        public void LargerAuthoredValue_Wins()
        {
            Assert.AreEqual(40, TouchDragThreshold.Scaled(40, 460f));
        }

        [Test]
        public void ReapplyingIsIdempotent()
        {
            // An EventSystem that survives a scene load is scaled again on the next load.
            int once = TouchDragThreshold.Scaled(10, 460f);
            Assert.AreEqual(once, TouchDragThreshold.Scaled(once, 460f));
        }
    }
}
