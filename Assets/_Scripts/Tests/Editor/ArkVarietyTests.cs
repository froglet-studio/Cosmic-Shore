using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>A voyage's roll (<see cref="ArkEscortVariety"/>): deterministic from the seed, the hungriest world last,
    /// a threat ramp otherwise, variants by weight, at most two modifiers and never two of a kind.</summary>
    public class ArkVarietyTests
    {
        ArkEscortSO _escort;

        [SetUp]
        public void SetUp()
        {
            _escort = ScriptableObject.CreateInstance<ArkEscortSO>();
            var legs = new List<ArkEscortSO.Leg>();
            string[] names = { "A", "B", "C", "D", "Maw" };
            float[] threat = { 0.6f, 0.8f, 1.0f, 1.1f, 1.2f };
            for (int i = 0; i < names.Length; i++)
                legs.Add(new ArkEscortSO.Leg { Name = names[i], Threat = threat[i] });
            legs[2].Variants = new List<ArkEscortSO.WorldVariant>
            {
                new() { Name = "C-scavengers", Weight = 1f },
                new() { Name = "C-wurm", Weight = 1f, ThreatScale = 1.2f },
            };
            _escort.SetLegs(legs);
            _escort.SetModifiers(new List<ArkEscortSO.VoyageModifier>
            {
                new() { Name = "SWARMING", Kind = ArkEscortSO.ModifierKind.Population, Factor = 1.3f },
                new() { Name = "CALM", Kind = ArkEscortSO.ModifierKind.Population, Factor = 0.8f },
                new() { Name = "SWIFT", Kind = ArkEscortSO.ModifierKind.FaunaSpeed, Factor = 1.25f },
                new() { Name = "RED TIDE", Kind = ArkEscortSO.ModifierKind.AllHostile, MinDifficulty = 5 },
            });
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_escort);

        [Test]
        public void SameSeedSameVoyage()
        {
            var a = ArkEscortVariety.Roll(_escort, 4, 12345);
            var b = ArkEscortVariety.Roll(_escort, 4, 12345);
            Assert.AreEqual(a.Describe(), b.Describe());
        }

        [Test]
        public void SeedsVaryTheVoyage()
        {
            var seen = new HashSet<string>();
            for (int s = 1; s <= 40; s++) seen.Add(ArkEscortVariety.Roll(_escort, 4, s * 7919).Describe());
            Assert.Greater(seen.Count, 5, "40 seeds should roll more than five different voyages");
        }

        [Test]
        public void HungriestLastAndRampHolds()
        {
            for (int s = 1; s <= 60; s++)
            {
                var v = ArkEscortVariety.Roll(_escort, 4, s);
                Assert.AreEqual(4, v.Legs.Count);
                Assert.AreEqual("Maw", v.WorldNames[^1]);
                for (int i = 1; i < v.Legs.Count - 1; i++)
                    Assert.LessOrEqual(v.Legs[i - 1].Threat, v.Legs[i].Threat + 2f * _escort.RampJitter + 0.25f,
                        "the ramp may only swap near-equal worlds");
                Assert.LessOrEqual(v.Modifiers.Count, 2);
                var kinds = new HashSet<ArkEscortSO.ModifierKind>();
                foreach (var m in v.Modifiers) Assert.IsTrue(kinds.Add(m.Kind), "two modifiers of one kind");
                foreach (var m in v.Modifiers) Assert.AreNotEqual("RED TIDE", m.Name, "below its minimum difficulty");
            }
        }

        [Test]
        public void ForcedModifiersAndWorldCount()
        {
            var v = ArkEscortVariety.Roll(_escort, 4, 99, forcedModifiers: 0b1010, worlds: 0);
            Assert.AreEqual(5, v.Legs.Count);
            CollectionAssert.AreEqual(new[] { "CALM", "RED TIDE" }, v.ModifierNames);
            Assert.AreEqual(0, ArkEscortVariety.Roll(_escort, 4, 99, forcedModifiers: 0).Modifiers.Count);
        }

        [Test]
        public void VariantsRollByWeight()
        {
            var names = new HashSet<string>();
            for (int s = 1; s <= 60; s++)
                foreach (var n in ArkEscortVariety.Roll(_escort, 4, s, worlds: 0).WorldNames) names.Add(n);
            Assert.IsTrue(names.Contains("C-scavengers") && names.Contains("C-wurm"));
            Assert.IsFalse(names.Contains("C"), "a leg with variants always sails one of them");
        }

        [Test]
        public void VoyageParamsApply()
        {
            var t = new ArkEscortParams.Target { Escort = _escort };
            var echo = ArkEscortParams.Apply(t, new Dictionary<string, float>
                { ["voyage.seed"] = 777f, ["voyage.modifiers"] = 4f, ["voyage.worlds"] = 3f });
            foreach (var e in echo) Assert.IsTrue(e.Applied, e.Key);
            Assert.AreEqual(777, t.VoyageSeed);
            Assert.AreEqual(4, t.ModifierMask);
            Assert.AreEqual(3, _escort.WorldsPerVoyage);
        }
    }
}
