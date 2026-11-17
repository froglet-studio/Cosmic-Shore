#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The standard element ladder's starting-element half (Garrett, 2026-11-16): I1 at rest, I2-I4 at level 5 in
    /// the game's element only, one row per distinct hull per rung.
    /// </summary>
    public class ElementLadderTests
    {
        [Test]
        public void Rungs_AreCumulative()
        {
            Assert.IsFalse(ElementLadder.StartsAtLevelFive(1));
            Assert.IsTrue(ElementLadder.StartsAtLevelFive(2) && ElementLadder.StartsAtLevelFive(4));
            Assert.IsFalse(ElementLadder.HasTeamCrystals(2));
            Assert.IsTrue(ElementLadder.HasTeamCrystals(3) && ElementLadder.HasTeamCrystals(4));
            Assert.IsFalse(ElementLadder.HasFauna(3));
            Assert.IsTrue(ElementLadder.HasFauna(4));
        }

        [Test]
        public void Default_LeavesI1AtRest_AndSeedsOnlyTheGamesElementAboveIt()
        {
            var table = new List<VesselStartingElements>();
            ElementLadder.AppendDefaultStartingElements(
                new[] { VesselClassType.Manta, VesselClassType.Manta, VesselClassType.Sparrow },
                Element.Time, Element.None, table);

            Assert.AreEqual(6, table.Count, "two distinct hulls x rungs 2-4");
            Assert.IsFalse(VesselStartingElements.TryResolve(table, VesselClassType.Manta, 1, out _),
                "I1 is nothing: the hull is left at rest");
            for (int rung = 2; rung <= 4; rung++)
            {
                Assert.IsTrue(VesselStartingElements.TryResolve(table, VesselClassType.Sparrow, rung, out var l));
                Assert.AreEqual(0.5f, l.Time, 1e-6f);
                Assert.AreEqual(0f, l.Mass + l.Charge + l.Space, 1e-6f, "only the game's element");
            }
        }

        [Test]
        public void Default_TwoElementGame_SeedsBoth()
        {
            var l = ElementLadder.LevelFiveIn(Element.Mass, Element.Space);
            Assert.AreEqual(0.5f, l.Mass, 1e-6f);
            Assert.AreEqual(0.5f, l.Space, 1e-6f);
            Assert.AreEqual(0f, l.Charge + l.Time, 1e-6f);
        }

        [Test]
        public void Default_NoElement_WritesNothing()
        {
            var table = new List<VesselStartingElements>();
            ElementLadder.AppendDefaultStartingElements(new[] { VesselClassType.Manta }, Element.None, Element.None, table);
            ElementLadder.AppendDefaultStartingElements(new[] { VesselClassType.Manta }, Element.Omni, Element.None, table);
            Assert.AreEqual(0, table.Count);
        }
    }
}
#endif
