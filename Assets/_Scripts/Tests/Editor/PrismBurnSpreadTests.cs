using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The burn "forest fire" rule (<see cref="PrismBurnSpread"/>) and Fulmen's burn defaults: the chance decays per
    /// generation and stops at the cap, children burn shorter, a bounded rule has a finite worst case, and the legacy
    /// (claw) rule is the old unbounded constant-chance spread.
    /// </summary>
    [TestFixture]
    public class PrismBurnSpreadTests
    {
        const float Tol = 1e-5f;

        [Test]
        public void ChanceDecaysPerGenerationAndStopsAtTheCap()
        {
            var s = PrismBurnSpread.FulmenDefault;
            Assert.AreEqual(0.45f, s.ChanceAt(0), Tol);
            Assert.AreEqual(0.225f, s.ChanceAt(1), Tol);
            Assert.AreEqual(0f, s.ChanceAt(2), "generation == MaxGenerations must not spread");
            Assert.IsTrue(s.CanSpread(1));
            Assert.IsFalse(s.CanSpread(2));
            Assert.AreEqual(0f, s.ChanceAt(-1));
        }

        [Test]
        public void ChildrenBurnShorter()
        {
            var s = PrismBurnSpread.FulmenDefault;
            Assert.AreEqual(5f, s.SecondsAt(5f, 0), Tol);
            Assert.AreEqual(4f, s.SecondsAt(5f, 1), Tol);
            Assert.AreEqual(3.2f, s.SecondsAt(5f, 2), Tol);
        }

        [Test]
        public void BoundedRuleHasAFiniteWorstCase()
        {
            Assert.AreEqual(3 + 9, PrismBurnSpread.FulmenDefault.MaxDescendants());
            Assert.AreEqual(0, PrismBurnSpread.None.MaxDescendants());
            Assert.IsFalse(PrismBurnSpread.None.Enabled);
        }

        [Test]
        public void LegacyRuleIsTheOldUnboundedSpread()
        {
            var s = PrismBurnSpread.Unbounded(25f, 0.35f, 0.75f);
            Assert.AreEqual(25f, s.Radius, Tol);
            Assert.AreEqual(0.75f, s.Interval, Tol);
            for (int g = 0; g < 50; g += 7) Assert.AreEqual(0.35f, s.ChanceAt(g), Tol, $"generation {g}");
            Assert.AreEqual(3f, s.SecondsAt(3f, 9), Tol);
            Assert.AreEqual(int.MaxValue, s.MaxDescendants());
        }

        [Test]
        public void FulmenBurnDefaults()
        {
            var cfg = ScriptableObject.CreateInstance<FulmenConfigSO>();
            try
            {
                Assert.AreEqual(10f, cfg.BurnRadius, Tol);
                Assert.AreEqual(5f, cfg.BurnSeconds, Tol);
                Assert.IsTrue(cfg.BurnSpread.Enabled);
                Assert.Less(cfg.BurnSpread.MaxDescendants(), 100);
            }
            finally { Object.DestroyImmediate(cfg); }
        }
    }
}
