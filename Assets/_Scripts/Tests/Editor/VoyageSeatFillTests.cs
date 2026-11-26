using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Which asked seat each squad vessel fills (Frogma Rev 146 A; <see cref="VoyageSeatFill"/>): the per-seat answer adds
    /// up to <see cref="VesselArchetypes.Fit"/>, names the vessel and its rank, and leaves a seat nobody meets empty.
    /// </summary>
    public class VoyageSeatFillTests
    {
        static readonly VesselArchetype[] Salvage = { VesselArchetype.Nuke, VesselArchetype.Sapper, VesselArchetype.Harraser };

        // The default squad as the board has it: Sparrow (Sapper, Harraser, Builder), Squirrel (Pumper, Harraser),
        // Dolphin (Nuke, Harraser, Pumper).
        static readonly IReadOnlyList<VesselArchetype>[] Squad =
        {
            new[] { VesselArchetype.Sapper, VesselArchetype.Harraser, VesselArchetype.Builder },
            new[] { VesselArchetype.Pumper, VesselArchetype.Harraser },
            new[] { VesselArchetype.Nuke, VesselArchetype.Harraser, VesselArchetype.Pumper },
        };

        [Test]
        public void DefaultSquad_OnSalvage_FillsEachSeat()
        {
            var filled = VoyageSeatFill.Assign(Salvage, Squad);
            Assert.AreEqual(3, filled.Length);
            Assert.AreEqual(2, filled[0].Vessel, "Dolphin is the Nuke");
            Assert.AreEqual(1f, filled[0].Meets);
            Assert.AreEqual(0, filled[1].Vessel, "Sparrow is the Sapper");
            Assert.AreEqual(1, filled[2].Vessel, "Squirrel is the Harraser, by half");
            Assert.AreEqual(0.5f, filled[2].Meets);
            Assert.AreEqual(1, filled[2].Rank(Squad), "Harraser is Squirrel's second");
            float total = 0f;
            foreach (var f in filled) total += f.Meets;
            Assert.AreEqual(VesselArchetypes.Fit(Salvage, Squad), total);
            Assert.AreEqual(2, VoyageSeatFill.SeatOf(filled, 1));
        }

        [Test]
        public void UnmetSeat_StaysEmpty()
        {
            var seats = new[] { VesselArchetype.Zoner, VesselArchetype.Sapper };
            var filled = VoyageSeatFill.Assign(seats, Squad);
            Assert.AreEqual(-1, filled[0].Vessel);
            Assert.AreEqual(0f, filled[0].Meets);
            Assert.AreEqual(-1, filled[0].Rank(Squad));
            Assert.AreEqual(0, filled[1].Vessel);
            Assert.AreEqual(-1, VoyageSeatFill.SeatOf(filled, 2), "Dolphin fills nothing asked");
            Assert.IsEmpty(VoyageSeatFill.Assign(null, Squad));
        }
    }
}
