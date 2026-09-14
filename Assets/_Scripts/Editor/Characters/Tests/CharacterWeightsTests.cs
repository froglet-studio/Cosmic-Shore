using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>The weight triplet invariant: sum 1, each in [0.20, 0.60] — under rolling, constraining and slider holds.</summary>
    public class CharacterWeightsTests
    {
        [Test]
        public void RolledWeightsAlwaysValid()
        {
            for (int seed = 0; seed < 3000; seed++)
            {
                var rng = new CharacterRandom(seed);
                var w = CharacterWeights.Roll(ref rng);
                Assert.IsTrue(w.IsValid, $"seed {seed}: {w}");
                Assert.AreEqual(1f, w.Human + w.CladeA + w.CladeB, 1e-4f);
            }
        }

        [Test]
        public void ConstrainProjectsAnyTripletIntoTheRegion()
        {
            var rng = new CharacterRandom(99);
            for (int i = 0; i < 2000; i++)
            {
                var w = CharacterWeights.Constrain(rng.Range(-1f, 3f), rng.Range(-1f, 3f), rng.Range(-1f, 3f));
                Assert.IsTrue(w.IsValid, $"case {i}: {w}");
            }
            Assert.IsTrue(CharacterWeights.Constrain(1f, 0f, 0f).IsValid);
            Assert.IsTrue(CharacterWeights.Constrain(0f, 0f, 0f).IsValid);
        }

        [Test]
        public void ConstrainHoldingKeepsTheHeldValueWhenItIsFeasible()
        {
            var w = CharacterWeights.EqualThirds;
            var held = CharacterWeights.ConstrainHolding(w, 1, 0.55f);
            Assert.IsTrue(held.IsValid, held.ToString());
            Assert.AreEqual(0.55f, held.CladeA, 1e-4f);
            var maxed = CharacterWeights.ConstrainHolding(held, 0, CharacterWeights.Max);
            Assert.IsTrue(maxed.IsValid, maxed.ToString());
            // 0.60 + the other two at least 0.20 each = 1.00 exactly: feasible, and both floors bind.
            Assert.AreEqual(CharacterWeights.Max, maxed.Human, 1e-4f);
            Assert.AreEqual(CharacterWeights.Min, maxed.CladeA, 1e-4f);
            Assert.AreEqual(CharacterWeights.Min, maxed.CladeB, 1e-4f);
        }

        [Test]
        public void EqualThirdsIsInsideNotOnTheBoundary()
        {
            var w = CharacterWeights.EqualThirds;
            Assert.IsTrue(w.IsValid);
            Assert.Greater(w.Human, CharacterWeights.Min);
            Assert.Less(w.Human, CharacterWeights.Max);
        }
    }
}
