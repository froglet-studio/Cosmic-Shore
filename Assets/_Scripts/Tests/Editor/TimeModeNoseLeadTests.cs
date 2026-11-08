using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-hull AI nose lead a Time minigame sets as it arms each AI (<see cref="GateRaceController.AINoseLeadRow"/>,
    /// <see cref="GateRaceController.NoseLeadFor"/>): a hull's row when the mode has one, the lead off otherwise, so no AI
    /// carries another mode's value into the race. Plan step: time-arcade.
    /// </summary>
    public class TimeModeNoseLeadTests
    {
        [Test]
        public void AHullWithARow_GetsItsRow()
        {
            var table = new[]
            {
                new GateRaceController.AINoseLeadRow(VesselClassType.Serpent, 1f),
                new GateRaceController.AINoseLeadRow(VesselClassType.Thresher, 2.5f, 30f),
            };
            var row = GateRaceController.NoseLeadFor(table, VesselClassType.Thresher);
            Assert.AreEqual(2.5f, row.Gain);
            Assert.AreEqual(30f, row.MaxExtraDegrees);
        }

        [Test]
        public void AHullWithNoRow_FliesWithTheLeadOff()
        {
            var table = new[] { new GateRaceController.AINoseLeadRow(VesselClassType.Thresher, 2.5f) };
            var row = GateRaceController.NoseLeadFor(table, VesselClassType.Manta);
            Assert.AreEqual(0f, row.Gain);
            Assert.AreEqual(AIPilot.DefaultNoseLeadMaxExtraDegrees, row.MaxExtraDegrees);
        }

        [Test]
        public void ANullOrEmptyTable_IsTheLeadOff()
        {
            Assert.AreEqual(0f, GateRaceController.NoseLeadFor(null, VesselClassType.Dolphin).Gain);
            Assert.AreEqual(0f, GateRaceController.NoseLeadFor(new GateRaceController.AINoseLeadRow[0], VesselClassType.Dolphin).Gain);
        }

        [Test]
        public void TheFirstRowForAHull_Wins()
        {
            var table = new[]
            {
                new GateRaceController.AINoseLeadRow(VesselClassType.Sparrow, 2f),
                new GateRaceController.AINoseLeadRow(VesselClassType.Sparrow, 5f),
            };
            Assert.AreEqual(2f, GateRaceController.NoseLeadFor(table, VesselClassType.Sparrow).Gain);
        }

        [Test]
        public void TheDefaultMaxExtra_IsTheFleetDefault()
        {
            var row = new GateRaceController.AINoseLeadRow(VesselClassType.Stoat, 2.5f);
            Assert.AreEqual(AIPilot.DefaultNoseLeadMaxExtraDegrees, row.MaxExtraDegrees);
        }
    }
}
