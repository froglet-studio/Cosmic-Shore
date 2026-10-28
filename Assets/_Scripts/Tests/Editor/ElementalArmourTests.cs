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

        [Test]
        public void TheShieldReachesHalfTheCircumscribingScale()
        {
            // The octahedron is built from the box's HALF-extents x CIRCUMSCRIBING_SCALE: 1.5 x the full size.
            Assert.That(ElementalArmour.ShieldReach, Is.EqualTo(OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE / 2f));
            Assert.That(ElementalArmour.PrismReach(Element.Mass), Is.EqualTo(ElementalArmour.ShieldReach));
            foreach (var e in new[] { Element.Charge, Element.Space, Element.Time })
                Assert.That(ElementalArmour.PrismReach(e), Is.EqualTo(0.5f), $"{e} is measured to its box");
        }

        [Test]
        public void AnArmouredHeartKeepsItsSwingClear_EveryOtherItsSeat()
        {
            // Docs/ECOSYSTEM.md §64.8: the Mass crystal's depth prisms reach 11.85 u, shipped with a 5% margin.
            Assert.That(ElementalArmour.HeartClearance(Element.Mass), Is.EqualTo(11.85f * 1.05f).Within(1e-5f));
            foreach (var e in new[] { Element.Charge, Element.Space, Element.Time })
                Assert.That(ElementalArmour.HeartClearance(e), Is.EqualTo(ElementalArmour.HeartSeatClearance), $"{e}");
            Assert.That(ElementalArmour.HeartSwingClearance, Is.GreaterThan(ElementalArmour.HeartSeatClearance));
        }
    }
}
