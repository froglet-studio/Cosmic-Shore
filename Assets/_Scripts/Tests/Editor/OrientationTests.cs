using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The portrait plan (Shombith, 2026-11-08): a turn in the app shell turns, then glides every piece to its new place; on a phone, turning upright
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
        public void TheTurnAngleIsTheGlassTurning()
        {
            Assert.AreEqual(-90f, OrientationGlide.TurnAngle(UnityEngine.ScreenOrientation.LandscapeLeft, UnityEngine.ScreenOrientation.Portrait), 1e-4f,
                "landscape-left back to upright carries the old picture a quarter clockwise");
            Assert.AreEqual(90f, OrientationGlide.TurnAngle(UnityEngine.ScreenOrientation.Portrait, UnityEngine.ScreenOrientation.LandscapeLeft), 1e-4f);
            Assert.AreEqual(90f, OrientationGlide.TurnAngle(UnityEngine.ScreenOrientation.LandscapeRight, UnityEngine.ScreenOrientation.Portrait), 1e-4f);
            Assert.AreEqual(0f, OrientationGlide.TurnAngle(UnityEngine.ScreenOrientation.Portrait, UnityEngine.ScreenOrientation.Portrait), 1e-4f);
            Assert.AreEqual(180f, Mathf.Abs(OrientationGlide.TurnAngle(UnityEngine.ScreenOrientation.LandscapeLeft, UnityEngine.ScreenOrientation.LandscapeRight)), 1e-4f);
        }

        [Test]
        public void APointRidesTheGlassThroughTheTurn()
        {
            var oldScreen = new Vector2(2532, 1170);   // a phone held landscape
            var newScreen = new Vector2(1170, 2532);   // the same phone upright
            Assert.That(Vector2.Distance(OrientationGlide.CarryAcrossTurn(oldScreen * 0.5f, oldScreen, newScreen, -90f), newScreen * 0.5f),
                        Is.LessThan(1e-3f), "the centre stays the centre");
            // the top-left corner of the landscape glass is where the top-right of the upright glass is after a quarter clockwise
            var topLeft = new Vector2(0f, 1170f);
            Assert.That(Vector2.Distance(OrientationGlide.CarryAcrossTurn(topLeft, oldScreen, newScreen, -90f), new Vector2(1170f, 2532f)),
                        Is.LessThan(1e-2f));
            Assert.That(Vector2.Distance(OrientationGlide.CarryAcrossTurn(new Vector2(100, 200), oldScreen, oldScreen, 0f), new Vector2(100, 200)),
                        Is.LessThan(1e-4f), "no turn, no move");
        }

        [Test]
        public void TheGlideEndsExactlyAtRest()
        {
            Assert.AreEqual(0f, OrientationGlide.ProgressAt(0f, 0.35f, KitEase.OutCubic), 1e-5f);
            Assert.AreEqual(1f, OrientationGlide.ProgressAt(0.35f, 0.35f, KitEase.OutCubic), 1e-5f);
            Assert.AreEqual(1f, OrientationGlide.ProgressAt(0f, 0f, KitEase.OutCubic), 1e-5f, "no glide time: at once");
            float last = 0f;
            for (float t = 0f; t <= 0.4f; t += 0.02f)
            {
                float e = OrientationGlide.ProgressAt(t, 0.35f, KitEase.OutCubic);
                Assert.GreaterOrEqual(e, last, "never runs backwards");
                last = e;
            }
            Assert.AreEqual(0f, CrewPictureInPicture.GlideAt(0f, 0.35f), 1e-5f);
            Assert.AreEqual(1f, CrewPictureInPicture.GlideAt(0.35f, 0.35f), 1e-5f, "the crew's views land exactly");
        }
    }
}
