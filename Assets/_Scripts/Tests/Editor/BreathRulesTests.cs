#if UNITY_EDITOR
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary><see cref="BreathRules"/>: the one next step the scoreboard offers after a result (P5).</summary>
    public class BreathRulesTests
    {
        [Test]
        public void Win_WithAHigherRungOpen_OffersTheNextRung()
        {
            Assert.AreEqual(BreathStep.NextRung, BreathRules.Choose(true, 2, 3, true, 9, 3, true));
        }

        [Test]
        public void Win_AtTheTopRung_CrossesAfterEnoughRounds()
        {
            Assert.AreEqual(BreathStep.Crossing, BreathRules.Choose(true, 3, 3, true, 3, 3, false));
            Assert.AreEqual(BreathStep.None, BreathRules.Choose(true, 3, 3, true, 2, 3, false));
        }

        [Test]
        public void Loss_NeverOffersTheNextRung()
        {
            Assert.AreEqual(BreathStep.None, BreathRules.Choose(false, 2, 4, false, 1, 3, false));
        }

        [Test]
        public void DailyJob_IsTheLastOffer()
        {
            Assert.AreEqual(BreathStep.DailyJob, BreathRules.Choose(false, 2, 2, false, 1, 3, true));
            Assert.AreEqual(BreathStep.Crossing, BreathRules.Choose(false, 2, 2, true, 5, 3, true));
        }

        [Test]
        public void EachElement_CrossesToItsArenaGame()
        {
            Assert.AreEqual(GameModes.Regatta, BreathRules.ArenaFor(Element.Time));
            Assert.AreEqual(GameModes.Broadside, BreathRules.ArenaFor(Element.Charge));
            Assert.AreEqual(GameModes.BroodRush, BreathRules.ArenaFor(Element.Mass));
            Assert.AreEqual(GameModes.BroodRush, BreathRules.ArenaFor(Element.Space));
            Assert.AreEqual(GameModes.Random, BreathRules.ArenaFor(Element.Omni));
        }
    }
}
#endif
