using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The style-B pause window titles itself from the <c>GameModes</c> id, the only mode name a
    /// running match carries (Docs/MENU_REDESIGN_B.md section 22.3).
    /// </summary>
    [TestFixture]
    public class PauseMenuTests
    {
        [TestCase("SkimRace", "Skim Race")]
        [TestCase("AstroLeague", "Astro League")]
        [TestCase("Joust", "Joust")]
        [TestCase("WildlifeLiberation", "Wildlife Liberation")]
        [TestCase("CoOpWildlifeBlitz", "Co Op Wildlife Blitz")]
        [TestCase("", "")]
        public void SpacedName_SplitsTheModeIdAtWordBoundaries(string id, string expected)
            => Assert.AreEqual(expected, PauseMenu.SpacedName(id));
    }
}
