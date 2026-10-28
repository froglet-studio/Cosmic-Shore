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

        /// <summary>A population drawn as data (swarm, builder colony, substrate) takes the same law through
        /// SwarmInstance.ElementLaw (Docs/ECOSYSTEM.md §65.7): shielded exactly where ElementalArmour shields, danger
        /// exactly where it arms every prism, and a Mass member that strikes shows danger and keeps its shield.</summary>
        [Test]
        public void PopulationsDrawnAsData_TakeTheSameLaw()
        {
            for (int index = 0; index < 4; index++)
            {
                var e = Gameplay.SwarmFaunaConfigSO.ToElement(index);
                int tier = 0; bool shielded = false;
                Gameplay.SwarmInstance.ElementLaw(index, ref tier, ref shielded);
                Assert.That(shielded, Is.EqualTo(ElementalArmour.Shields(e)), $"{e} shielded");
                Assert.That(tier == 1, Is.EqualTo(ElementalArmour.ArmsEveryPrism(e)), $"{e} danger");
                int striking = 1; bool kept = false;
                Gameplay.SwarmInstance.ElementLaw(index, ref striking, ref kept);
                Assert.That(striking, Is.EqualTo(1), $"{e}: a strike still shows danger");
            }
        }

        /// <summary>The hearts of those populations are drawn instanced with each elemental crystal's own model
        /// materials (CrystalHeartDraw); a material without GPU instancing is refused at bind and that element's hearts
        /// go missing, and the asset flag is also what keeps the instancing variant in a build.</summary>
        [Test]
        public void EveryHeartModelMaterial_IsInstanced()
        {
            var set = ScriptableObjects.ElementalCrystalSetSO.Load();
            Assert.That(set, Is.Not.Null, "Resources/ElementalCrystalSet");
            foreach (var e in Four)
            {
                var crystal = set.GetPrefab(e);
                Assert.That(crystal, Is.Not.Null, $"{e} crystal");
                int models = 0;
                foreach (var model in crystal.CrystalModels)
                {
                    if (model?.model == null || !model.model.TryGetComponent<UnityEngine.Renderer>(out var r)) continue;
                    models++;
                    foreach (var m in r.sharedMaterials)
                        Assert.That(m && m.enableInstancing, Is.True, $"{e}: '{(m ? m.name : "null")}' on {r.name} has GPU instancing off");
                }
                Assert.That(models, Is.GreaterThan(0), $"{e} has a crystal model to draw");
            }
        }
    }
}
