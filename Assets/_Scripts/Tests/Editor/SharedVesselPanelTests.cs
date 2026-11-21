#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The configure window's seat selection: tap a seat row and the hull strip (Arena) and the AI row (every card
    /// with AI) edit that seat.
    /// These hold which seat a tap leaves the panel on (<see cref="ArcadeGameConfigureModal.NextVesselSeat"/>);
    /// the host's rules for a pick (<c>AIHullSeating</c>) have their own tests, and the drawing is checked by hand.
    /// </summary>
    public class SharedVesselPanelTests
    {
        const int Own = -1;

        [Test]
        public void TappingAnAiSeat_SelectsIt()
        {
            Assert.AreEqual(2, ArcadeGameConfigureModal.NextVesselSeat(Own, 2));
            Assert.AreEqual(0, ArcadeGameConfigureModal.NextVesselSeat(3, 0));
        }

        [Test]
        public void TappingTheSelectedAiSeatAgain_GivesThePanelBack()
        {
            Assert.AreEqual(Own, ArcadeGameConfigureModal.NextVesselSeat(2, 2));
        }

        [Test]
        public void TappingYourOwnSeat_AlwaysLandsOnYourOwnHull()
        {
            Assert.AreEqual(Own, ArcadeGameConfigureModal.NextVesselSeat(1, Own));
            Assert.AreEqual(Own, ArcadeGameConfigureModal.NextVesselSeat(Own, Own));
        }

        [Test]
        public void AKick_TakesItsSelection_AndTheSelectionFollowsItsAI()
        {
            Assert.AreEqual(Own, ArcadeGameConfigureModal.SeatAfterKick(2, 2), "the selected AI left");
            Assert.AreEqual(2, ArcadeGameConfigureModal.SeatAfterKick(3, 1), "a bot before it left: one place down");
            Assert.AreEqual(1, ArcadeGameConfigureModal.SeatAfterKick(1, 3), "a bot after it left: unchanged");
            Assert.AreEqual(Own, ArcadeGameConfigureModal.SeatAfterKick(Own, 0));
        }
    }
}
#endif
