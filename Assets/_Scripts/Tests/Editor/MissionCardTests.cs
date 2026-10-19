#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The mission card's threat count (KitMissionCard.ThreatsCaption): singular for one, plural
    /// otherwise, and nothing at all for a mission that lists no threats.
    /// </summary>
    [TestFixture]
    public class MissionCardTests
    {
        [TestCase(0, "")]
        [TestCase(1, "1 threat")]
        [TestCase(8, "8 threats")]
        public void ThreatsCaptionCountsTheMissionsThreats(int count, string expected)
        {
            Assert.AreEqual(expected, KitMissionCard.ThreatsCaption(count));
        }
    }
}
#endif
