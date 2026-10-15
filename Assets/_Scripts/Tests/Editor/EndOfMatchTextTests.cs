using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The style-B end of match (Docs/MENU_REDESIGN_B.md section 22.4) writes its own lines: the
    /// scoreboard banner and the reveal's detail label.
    /// </summary>
    [TestFixture]
    public class EndOfMatchTextTests
    {
        [TestCase(Domains.Jade, "Jade victory")]
        [TestCase(Domains.Ruby, "Ruby victory")]
        [TestCase(Domains.Gold, "Gold victory")]
        public void Banner_NamesTheWinningDomain(Domains domain, string expected)
            => Assert.AreEqual(expected, Scoreboard.BannerLine(domain));

        /// <summary>Blue is the "no domain won" sentinel; "BLUE VICTORY" used to be reachable.</summary>
        [Test]
        public void Banner_NeverNamesBlue()
            => Assert.AreEqual("Game over", Scoreboard.BannerLine(Domains.Blue));

        [TestCase("RACE TIME", "Race time")]
        [TestCase("CRYSTALS LEFT", "Crystals left")]
        [TestCase("", "")]
        public void RevealLabel_IsSentenceCase(string caps, string expected)
            => Assert.AreEqual(expected, EndGameSequencer.SentenceCase(caps));
    }
}
