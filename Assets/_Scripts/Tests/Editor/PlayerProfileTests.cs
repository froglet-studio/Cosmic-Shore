#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Core;

namespace CosmicShore.Tests
{
    /// <summary>
    /// PlayerProfile Tests - the avatar id a profile string resolves to.
    ///
    /// WHY THIS MATTERS:
    /// ProfileIconId is a PROPERTY GETTER over cloud-backed text, read by profile rows,
    /// scoreboards and party slots. It guarded null and used a bare int.Parse for
    /// everything else, so an empty or non-numeric AvatarUrl threw a FormatException at
    /// whatever read it rather than anywhere near the bad data - and "" is at least as
    /// likely an unset cloud value as null. The contract asserted here is that every
    /// input which previously produced a number still produces the same number, and every
    /// input which previously threw now falls back to 1: the same default the null path
    /// and this class's own constructor already use.
    /// </summary>
    [TestFixture]
    public class PlayerProfileTests
    {
        [TestCase("1", 1)]
        [TestCase("7", 7)]
        [TestCase("0", 0)]
        [TestCase("12", 12)]
        public void ProfileIconId_ParsesANumericAvatarUrl(string avatarUrl, int expected)
        {
            Assert.AreEqual(expected, new PlayerProfile("Player", avatarUrl).ProfileIconId);
        }

        // Each of these threw before 2026-09-08. `null` is the one case that was already
        // guarded, and it is kept so the guard cannot be lost while the others are fixed.
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("abc")]
        [TestCase("3.5")]
        [TestCase("1,2")]
        public void ProfileIconId_FallsBackToOne_WhenTheAvatarUrlIsNotAnInt(string avatarUrl)
        {
            Assert.AreEqual(1, new PlayerProfile("Player", avatarUrl).ProfileIconId);
        }

        [Test]
        public void ProfileIconId_MatchesTheConstructorDefault()
        {
            // The fallback is only the right number while the default AvatarUrl is "1".
            // If that default moves, this fails rather than leaving the two out of step.
            Assert.AreEqual(new PlayerProfile().ProfileIconId,
                            new PlayerProfile("Player", "nonsense").ProfileIconId);
        }
    }
}
#endif
