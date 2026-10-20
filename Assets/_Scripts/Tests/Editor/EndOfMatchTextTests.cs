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
            => Assert.AreEqual(expected, Scoreboard.BannerLine(domain, "{domain} victory", "Game over"));

        /// <summary>Blue is the "no domain won" sentinel; "BLUE VICTORY" used to be reachable.</summary>
        [Test]
        public void Banner_NeverNamesBlue()
            => Assert.AreEqual("Game over", Scoreboard.BannerLine(Domains.Blue, "{domain} victory", "Game over"));

        /// <summary>An authored line blanked in the inspector shows nothing - never a hardcoded default.</summary>
        [Test]
        public void Banner_BlankLineShowsNothing()
            => Assert.AreEqual(string.Empty, Scoreboard.BannerLine(Domains.Jade, string.Empty, "Game over"));

        [Test]
        public void TemplateText_FillsEveryToken()
            => Assert.AreEqual("Play again (2/3)", TemplateText.Fill("Play again ({votes}/{pilots})", "votes", "2", "pilots", "3"));

        [TestCase("RACE TIME", "Race time")]
        [TestCase("CRYSTALS LEFT", "Crystals left")]
        [TestCase("", "")]
        public void RevealLabel_IsSentenceCase(string caps, string expected)
            => Assert.AreEqual(expected, EndGameSequencer.SentenceCase(caps));
    }
}
