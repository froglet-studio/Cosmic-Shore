using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Starlings HUD's level-scaling lines (<see cref="StarlingHUDController.StatLine"/>,
    /// <see cref="StarlingHUDController.PickedLevel"/>): a number that changes between two levels reads "now -> next", one
    /// that does not reads once, and a pick reaches the level held plus the hand card's, capped at 15.
    /// </summary>
    [TestFixture]
    public class StarlingHUDLevelScalingTests
    {
        const string Plain = "{label}: {value}{unit}";
        const string Up = "{label}: {value}{unit} -> {next}{unit}";

        [Test]
        public void PickedLevel_AddsTheHandLevel_CappedAtFifteen()
        {
            Assert.AreEqual(4, StarlingHUDController.PickedLevel(0, 4), "a new card arrives at the hand's level");
            Assert.AreEqual(7, StarlingHUDController.PickedLevel(3, 4), "a held card adds the hand's level (StarlingRules.Pick)");
            Assert.AreEqual(StarlingRules.MaxLevel, StarlingHUDController.PickedLevel(13, 6));
        }

        [Test]
        public void ANumberThatChanges_ShowsBothLevels()
        {
            var range = new StarlingCardStat("Range", StarlingStatKind.Linear, 120f, 15f, "u", 0);
            Assert.AreEqual("Range: 150u -> 165u", StarlingHUDController.StatLine(range, 3, 4, Plain, Up));
            Assert.AreEqual("Range: 150u -> 210u", StarlingHUDController.StatLine(range, 3, 7, Plain, Up));
        }

        [Test]
        public void ANumberThatDoesNotChange_ReadsOnce()
        {
            var range = new StarlingCardStat("Range", StarlingStatKind.Linear, 120f, 15f, "u", 0);
            Assert.AreEqual("Range: 330u", StarlingHUDController.StatLine(range, 15, 15, Plain, Up), "the top level has no next");

            // A count that rounds down to the same number between two levels: 12 x 0.86^4 = 6.56 and 12 x 0.86^5 = 5.64.
            var stings = new StarlingCardStat("Stings", StarlingStatKind.Geometric, 12f, 0.86f, "", 0);
            Assert.AreEqual("Stings: 6 -> 5", StarlingHUDController.StatLine(stings, 5, 6, Plain, Up));
            var flat = new StarlingCardStat("Swarms", StarlingStatKind.Linear, 2f, 0.2f, "", 0);
            Assert.AreEqual("Swarms: 2", StarlingHUDController.StatLine(flat, 2, 3, Plain, Up), "2.2 and 2.4 both count as 2");
        }

        [Test]
        public void AStepStat_ChangesOnlyAtItsSteps()
        {
            var swarms = new StarlingCardStat("Swarms", StarlingStatKind.Steps, 0f, 0f, "", 0, new[] { "1", "2", "3", "4" });
            Assert.AreEqual("Swarms: 1", StarlingHUDController.StatLine(swarms, 2, 3, Plain, Up));
            Assert.AreEqual("Swarms: 1 -> 2", StarlingHUDController.StatLine(swarms, 4, 5, Plain, Up));
        }

        [Test]
        public void AnEmptyTemplate_ShowsNothing()
        {
            var range = new StarlingCardStat("Range", StarlingStatKind.Linear, 120f, 15f, "u", 0);
            Assert.AreEqual(string.Empty, StarlingHUDController.StatLine(range, 3, 4, Plain, string.Empty));
        }
    }
}
