#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Arena configure window's shared vessel panel: tap a seat row and the hull strip picks for that seat.
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
    }
}
#endif
