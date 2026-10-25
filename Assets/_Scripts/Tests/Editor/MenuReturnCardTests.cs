#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Leaving a match lands back on the launch panel of the card it was launched from
    /// (<see cref="MenuReturnCard"/>). These hold the record's lifetime - written on a launch, taken
    /// once by the menu that loads next - and who re-opens it. The re-opening itself (the waits, the
    /// window, the panel) runs in Menu_Main and is checked by hand.
    /// </summary>
    public class MenuReturnCardTests
    {
        [SetUp]
        public void SetUp() => MenuReturnCard.Clear();

        [TearDown]
        public void TearDown() => MenuReturnCard.Clear();

        [Test]
        public void NothingLaunched_NothingToTake()
        {
            Assert.IsFalse(MenuReturnCard.IsArmed);
            Assert.IsFalse(MenuReturnCard.TryTake(out _));
        }

        [Test]
        public void Launched_IsTakenWithItsCard()
        {
            MenuReturnCard.Remember(GameModes.SkimRace, weeklyChallenge: true);

            Assert.IsTrue(MenuReturnCard.TryTake(out var card));
            Assert.AreEqual(GameModes.SkimRace, card.Mode);
            Assert.IsTrue(card.WeeklyChallenge);
        }

        [Test]
        public void IsTakenOnce_SoALaterMenuReloadIsNotAReturn()
        {
            MenuReturnCard.Remember(GameModes.Maelstrom, weeklyChallenge: false);

            Assert.IsTrue(MenuReturnCard.TryTake(out _));
            Assert.IsFalse(MenuReturnCard.IsArmed);
            Assert.IsFalse(MenuReturnCard.TryTake(out _));
        }

        [Test]
        public void TheLatestLaunchWins()
        {
            MenuReturnCard.Remember(GameModes.SkimRace, weeklyChallenge: true);
            MenuReturnCard.Remember(GameModes.Maelstrom, weeklyChallenge: false);

            Assert.IsTrue(MenuReturnCard.TryTake(out var card));
            Assert.AreEqual(GameModes.Maelstrom, card.Mode);
            Assert.IsFalse(card.WeeklyChallenge);
        }

        [Test]
        public void Cleared_LandsOnHome()
        {
            MenuReturnCard.Remember(GameModes.SkimRace, weeklyChallenge: false);
            MenuReturnCard.Clear();

            Assert.IsFalse(MenuReturnCard.TryTake(out _));
        }

        [Test]
        public void TheHostReopens()
        {
            Assert.IsTrue(MenuReturnCard.ShouldReopen(isPartyClient: false, guided: false, inFreestyle: false, windowOpen: false));
        }

        [Test]
        public void AGuestFollowsTheHostsLobbyInstead()
        {
            Assert.IsFalse(MenuReturnCard.ShouldReopen(isPartyClient: true, guided: false, inFreestyle: false, windowOpen: false));
        }

        [Test]
        public void AGuideOrTheFirstOrientationDecidesWhereThePlayerGoes()
        {
            Assert.IsFalse(MenuReturnCard.ShouldReopen(isPartyClient: false, guided: true, inFreestyle: false, windowOpen: false));
        }

        [Test]
        public void APlayerWhoAlreadyWentSomewhereIsLeftThere()
        {
            Assert.IsFalse(MenuReturnCard.ShouldReopen(isPartyClient: false, guided: false, inFreestyle: true, windowOpen: false));
            Assert.IsFalse(MenuReturnCard.ShouldReopen(isPartyClient: false, guided: false, inFreestyle: false, windowOpen: true));
        }
    }
}
#endif
