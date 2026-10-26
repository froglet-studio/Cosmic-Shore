#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The configure window's settings column fits its tallest domain column (ConfigurePilotsFit): it grows
    /// first, tightens the rows only past the room above the screen's bottom, and never covers the AI row.
    /// The numbers are the generated window's (Tools/Build/author_menu_configure.py, whose check_pilots_fit
    /// runs the same arithmetic over every seat count).
    /// </summary>
    [TestFixture]
    public class ConfigurePilotsFitTests
    {
        const float Seat = 40f, Gap = 6f, CompactGap = 2f, MinSeat = 32f;
        const float RoomUnderRow = 164f;           // the columns' space above the AI row
        const float ArcadeGrowth = 144.6f, ArenaGrowth = 86.6f;

        // A domain column: tile 62, its gaps, the seat strip (4 px inset) and the open seat when shown.
        static float Column(int seats, bool openSeat) =>
            62f + (openSeat ? 2 : 1) * Gap + 4f + seats * Seat + System.Math.Max(0, seats - 1) * Gap + (openSeat ? Seat : 0f);

        static ConfigurePilotsFit.Fit Solve(int seats, bool openSeat, float room, float maxGrowth) =>
            ConfigurePilotsFit.Solve(Column(seats, openSeat), room, maxGrowth, seats + (openSeat ? 1 : 0),
                                     System.Math.Max(0, seats - 1), openSeat ? 2 : 1, Seat, Gap, Gap, CompactGap, MinSeat);

        [Test]
        public void OnePilotAndTheOpenSeat_FitAsAuthored()
        {
            var fit = Solve(1, true, RoomUnderRow, ArcadeGrowth);
            Assert.AreEqual(0f, fit.Growth);
            Assert.IsFalse(fit.Compact);
        }

        [Test]
        public void TwoPilotsInADomain_GrowTheColumn_RowsUntouched()
        {
            // The defect: 204 px of column in 164 px above the AI row.
            var fit = Solve(2, true, RoomUnderRow, ArcadeGrowth);
            Assert.AreEqual(Column(2, true) - RoomUnderRow, fit.Growth, 0.01f);
            Assert.IsFalse(fit.Compact);
            Assert.AreEqual(Seat, fit.SeatHeight);
        }

        [Test]
        public void EveryArcadeSeatInOneDomain_StillFitsByGrowingAlone()
        {
            var fit = Solve(4, false, RoomUnderRow, ArcadeGrowth);
            Assert.LessOrEqual(fit.Growth, ArcadeGrowth);
            Assert.IsFalse(fit.Compact, "an Arcade column never needs its rows tightened");
        }

        [Test]
        public void ACrowdedArenaColumn_TightensItsRows_WithinTheRoom()
        {
            var fit = Solve(5, false, RoomUnderRow, ArenaGrowth);
            Assert.AreEqual(ArenaGrowth, fit.Growth, 0.01f, "grow all the way before tightening");
            Assert.IsTrue(fit.Compact);
            Assert.Less(fit.SeatHeight, Seat);
            Assert.GreaterOrEqual(fit.SeatHeight, MinSeat);

            // The tightened column ends exactly where the grown room does.
            float tightened = 62f + CompactGap + 4f + 5 * fit.SeatHeight + 4 * CompactGap;
            Assert.AreEqual(RoomUnderRow + fit.Growth, tightened, 0.05f);
        }

        [Test]
        public void RowsAtTheirFloor_GrowPastTheRoom_RatherThanCoverTheRow()
        {
            // Every Arena pilot in one domain: a lobby that cannot launch.
            var fit = Solve(6, false, RoomUnderRow, ArenaGrowth);
            Assert.AreEqual(MinSeat, fit.SeatHeight);
            float tightened = 62f + CompactGap + 4f + 6 * MinSeat + 5 * CompactGap;
            Assert.AreEqual(tightened - RoomUnderRow, fit.Growth, 0.05f);
            Assert.Greater(fit.Growth, ArenaGrowth);
        }

        [Test]
        public void AHiddenAIRow_GivesTheColumnsItsSpace()
        {
            var fit = Solve(3, true, RoomUnderRow + 8f + 82f, ArcadeGrowth);
            Assert.AreEqual(0f, fit.Growth, "three pilots fit where the hidden row would be");
        }
    }
}
#endif
