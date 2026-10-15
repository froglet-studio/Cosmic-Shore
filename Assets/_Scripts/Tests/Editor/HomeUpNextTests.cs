#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>Home's weekly card best line (HomeUpNext.BestText): a race time, else a score, else "None yet".</summary>
    [TestFixture]
    public class HomeUpNextTests
    {
        [Test]
        public void BestTextPrefersTheRaceTime() => Assert.AreEqual("1:08.42", HomeUpNext.BestText(68420, 12));

        [Test]
        public void BestTextFallsBackToTheScore() => Assert.AreEqual("12", HomeUpNext.BestText(0, 12));

        [Test]
        public void BestTextWithNothingPlayed() => Assert.AreEqual("None yet", HomeUpNext.BestText(0, 0));
    }
}
#endif
