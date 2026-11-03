using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The nest at every Thresher Time gate (ThresherTimeNest, 2026-11-04): the core first, an armour ladder that only
    /// grows with the intensity, a budget the arena can carry, closed form, and the rule that clears an opened gate.
    /// </summary>
    public class ThresherTimeNestTests
    {
        static readonly RaceGate Gate = new(new Vector3(100f, 50f, -20f), new Vector3(0.3f, 0.2f, 0.93f).normalized, 72f);

        static List<ThresherTimeNest.Part> Build(int intensity, int gateIndex = 0)
        {
            var parts = new List<ThresherTimeNest.Part>();
            ThresherTimeNest.Build(Gate, gateIndex, intensity, parts);
            return parts;
        }

        [Test]
        public void TheCoreIsListedFirst_AndSitsAtTheGate([Values(1, 2, 3, 4)] int intensity)
        {
            var parts = Build(intensity);
            var spec = ThresherTimeNest.SpecFor(intensity);
            for (int k = 0; k < parts.Count; k++)
                Assert.AreEqual(k < spec.CoreBlocks, parts[k].Core, $"part {k}: the core blocks come first, then the cage");
            Assert.AreEqual(0f, (parts[0].Position - Gate.Position).magnitude, 1e-3f, "one core block at the gate's centre");
        }

        [Test]
        public void TheCoreShrinksAndTheCageArmsAsTheIntensityRises()
        {
            for (int i = 1; i < 4; i++)
            {
                var a = ThresherTimeNest.SpecFor(i);
                var b = ThresherTimeNest.SpecFor(i + 1);
                Assert.LessOrEqual(b.CoreBlocks * b.CoreCube, a.CoreBlocks * a.CoreCube, $"I{i + 1}'s core is no bigger");
                Assert.GreaterOrEqual(Build(i + 1).Count - b.CoreBlocks, Build(i).Count - a.CoreBlocks, $"I{i + 1}'s cage is no thinner");
            }
            Assert.IsFalse(ThresherTimeNest.SpecFor(1).HasCage, "I1 has no cage: learn to hit things");
        }

        [Test]
        public void OnlyTheTopIntensityIsShielded_AndDangerStartsAtI3()
        {
            bool Has(int i, PrismKind kind) => Build(i).Exists(p => p.Kind == kind);
            Assert.IsFalse(Has(1, PrismKind.Danger) || Has(2, PrismKind.Danger), "I1-I2 cannot hurt you");
            Assert.IsTrue(Has(3, PrismKind.Danger) && Has(4, PrismKind.Danger));
            Assert.IsTrue(Has(4, PrismKind.Shielded) && !Has(3, PrismKind.Shielded));
            Assert.IsFalse(Build(4).Exists(p => p.Kind == PrismKind.SuperShielded), "nothing the kit cannot break");
            Assert.IsTrue(Build(4).TrueForAll(p => !p.Core || p.Kind == PrismKind.Plain), "the core is always breakable in one hit");
        }

        [Test]
        public void EveryPartIsInsideTheReach_AndTheBudgetHolds([Values(1, 2, 3, 4)] int intensity)
        {
            var parts = Build(intensity);
            float reach = ThresherTimeNest.OuterReach(intensity);
            foreach (var p in parts)
                Assert.LessOrEqual((p.Position - Gate.Position).magnitude, reach, "a hull outside the reach is clear of the nest");
            Assert.LessOrEqual(parts.Count, 140, "eight nests a lap stay within the arena's prism budget");
        }

        [Test]
        public void ClosedForm_TheSameGateGivesTheSameNest_NeighboursDiffer()
        {
            var a = Build(4, 3);
            var b = Build(4, 3);
            Assert.AreEqual(a.Count, b.Count);
            for (int k = 0; k < a.Count; k++) Assert.AreEqual(a[k].Position, b[k].Position, "every peer lays the same nest");
            var c = Build(4, 4);
            Assert.AreNotEqual(a[a.Count - 1].Position, c[c.Count - 1].Position, "the next gate's cage is turned");
        }

        [Test]
        public void AnOpenedGateCreditsAPassNearIt_NotOneFarAway()
        {
            Vector3 centre = Gate.Position;
            Assert.IsTrue(ThresherTimeNest.PassesNear(centre, centre + Vector3.left * 500f, centre + Vector3.right * 500f, 200f),
                "a fast step straight through the wreck counts, however long the step");
            Assert.IsTrue(ThresherTimeNest.PassesNear(centre, centre + Vector3.up * 150f, centre + Vector3.up * 150f, 200f),
                "beside it as it broke counts");
            Assert.IsFalse(ThresherTimeNest.PassesNear(centre, centre + Vector3.up * 400f, centre + new Vector3(50f, 400f, 0f), 200f),
                "negative control: passing well clear does not");
        }
    }
}
