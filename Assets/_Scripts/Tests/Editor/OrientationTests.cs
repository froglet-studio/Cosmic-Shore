using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The portrait plan (Shombith, 2026-11-08): a turn in the app shell fades the UI back in; on a phone, turning upright
    /// on the Goby takes the Pistol's view and turning back takes the Goby; every other hull turns nothing.
    /// </summary>
    public class OrientationTests
    {
        sealed class Crew : ICrewStations
        {
            public int SeatCount { get; set; } = 2;
            public int LocalSeat { get; set; }
            public bool HostsRiders => false;
            public int Taken = -1;
            public void TakeSeat(int seat) { Taken = seat; LocalSeat = seat; }
        }

        sealed class UprightCrew : ICrewStations, IUprightSeat
        {
            public int SeatCount => 2;
            public int LocalSeat { get; set; }
            public bool HostsRiders => true;
            public int UprightSeat { get; set; } = 1;
            public void TakeSeat(int seat) => LocalSeat = seat;
        }

        [Test]
        public void OnlyAnUprightSeatOnAPhoneTurns()
        {
            var goby = new UprightCrew();
            Assert.AreEqual(1, CrewOrientation.UprightSeatOf(true, goby, 0, false), "the Goby on a phone: the Pistol");
            Assert.AreEqual(-1, CrewOrientation.UprightSeatOf(false, goby, 0, false), "a desktop turns nothing");
            Assert.AreEqual(-1, CrewOrientation.UprightSeatOf(true, new Crew(), 0, false), "a crew with no upright seat");
            Assert.AreEqual(-1, CrewOrientation.UprightSeatOf(true, null, 0, false), "a plain hull");
            Assert.AreEqual(-1, CrewOrientation.UprightSeatOf(true, goby, -1, false), "a hull the menu parked on its AI");
            Assert.AreEqual(-1, CrewOrientation.UprightSeatOf(true, goby, 0, true), "riding another hull");
            goby.UprightSeat = 0;
            Assert.AreEqual(-1, CrewOrientation.UprightSeatOf(true, goby, 0, false), "the helm is never the upright seat");
            goby.UprightSeat = 2;
            Assert.AreEqual(-1, CrewOrientation.UprightSeatOf(true, goby, 0, false), "a seat the crew does not have");
        }

        [Test]
        public void TurningUprightTakesTheUprightSeat()
        {
            Assert.AreEqual(1, CrewOrientation.SeatFor(true, false, 0, 1), "landscape to upright on the helm");
            Assert.AreEqual(-1, CrewOrientation.SeatFor(true, false, 1, 1), "already in the Pistol");
            Assert.AreEqual(1, CrewOrientation.SeatFor(true, null, 0, 1), "arriving upright (from the upright menu)");
        }

        [Test]
        public void TurningBackTakesTheHelm()
        {
            Assert.AreEqual(0, CrewOrientation.SeatFor(false, true, 1, 1), "upright to landscape in the Pistol");
            Assert.AreEqual(-1, CrewOrientation.SeatFor(false, true, 0, 1), "a swap already took the helm");
            Assert.AreEqual(-1, CrewOrientation.SeatFor(false, null, 1, 1), "arriving in landscape moves nobody");
        }

        [Test]
        public void NoTurnMovesNobody()
        {
            // a seat the swap gesture or a double tap chose stays until the phone turns again
            Assert.AreEqual(-1, CrewOrientation.SeatFor(true, true, 0, 1));
            Assert.AreEqual(-1, CrewOrientation.SeatFor(false, false, 1, 1));
        }

        [Test]
        public void TheFadeRisesToFull()
        {
            Assert.AreEqual(0f, OrientationFade.AlphaAt(0f, 0.28f, KitEase.Linear), 1e-5f);
            Assert.AreEqual(0.5f, OrientationFade.AlphaAt(0.14f, 0.28f, KitEase.Linear), 1e-4f);
            Assert.AreEqual(1f, OrientationFade.AlphaAt(0.5f, 0.28f, KitEase.OutCubic), 1e-5f);
            Assert.AreEqual(1f, OrientationFade.AlphaAt(0f, 0f, KitEase.OutCubic), 1e-5f, "no fade time: at once");
            float a = 0f;
            for (float t = 0f; t <= 0.3f; t += 0.02f)
            {
                float next = OrientationFade.AlphaAt(t, 0.28f, KitEase.OutCubic);
                Assert.GreaterOrEqual(next, a, "never dips back while it fades in");
                a = next;
            }
        }
    }
}
