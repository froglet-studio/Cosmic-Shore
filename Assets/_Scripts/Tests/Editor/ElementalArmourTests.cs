using CosmicShore.Data;
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins THE ARMOUR LAW (Docs/ECOSYSTEM.md §65): Mass is armoured, Charge is dangerous, and the two never meet -
    /// a shield clears danger, so a Charge lifeform runs no shield whatever its config authors.
    /// </summary>
    public class ElementalArmourTests
    {
        static readonly Element[] Four = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        [Test]
        public void OnlyMassShields_AndOnlyChargeArmsEveryPrism()
        {
            foreach (var e in Four)
            {
                Assert.That(ElementalArmour.Shields(e), Is.EqualTo(e == Element.Mass), $"{e} shields");
                Assert.That(ElementalArmour.ArmsEveryPrism(e), Is.EqualTo(e == Element.Charge), $"{e} arms every prism");
                Assert.That(ElementalArmour.Shields(e) && ElementalArmour.ArmsEveryPrism(e), Is.False,
                    "danger and shield are mutually exclusive");
            }
            Assert.That(ElementalArmour.Shields(Element.None), Is.False);
            Assert.That(ElementalArmour.ArmsEveryPrism(Element.None), Is.False);
        }

        [Test]
        public void MassIsFlooredAtTheLawsCadence_AnAuthoredOneStillWins()
        {
            Assert.That(ElementalArmour.ResolveShieldPeriod(Element.Mass, 0f), Is.EqualTo(ElementalArmour.ShieldPeriod));
            Assert.That(ElementalArmour.ResolveShieldPeriod(Element.Mass, -1f), Is.EqualTo(ElementalArmour.ShieldPeriod));
            Assert.That(ElementalArmour.ResolveShieldPeriod(Element.Mass, 2.5f), Is.EqualTo(2.5f));
        }

        [Test]
        public void ChargeNeverShields_WhateverItsConfigAuthors()
        {
            foreach (var authored in new[] { -1f, 0f, 1f, 5f })
                Assert.That(ElementalArmour.ResolveShieldPeriod(Element.Charge, authored), Is.EqualTo(0f), $"authored {authored}");
        }

        [Test]
        public void SpaceAndTimeKeepWhatTheyAuthor()
        {
            foreach (var e in new[] { Element.Space, Element.Time, Element.None })
            foreach (var authored in new[] { 0f, 3f })
                Assert.That(ElementalArmour.ResolveShieldPeriod(e, authored), Is.EqualTo(authored), $"{e} {authored}");
        }
    }
}
