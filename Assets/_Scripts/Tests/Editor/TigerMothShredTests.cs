using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Tiger Moth's shred (TIGER_MOTH.md §1.3): one petal of one random element per Charge-scaled
    /// interval, with score and silence priced per petal. These pin the pure rules and the shipped asset's
    /// numbers; the steal itself runs through ElementalTransfer, which the transfer harness covers.
    /// </summary>
    public class TigerMothShredTests
    {
        const string DustAsset =
            "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/ButterflyScaleDustDebuffBySkimmerEffect.asset";

        [Test]
        public void AStrikeIsWorthThreePointTwoPetals()
        {
            Assert.AreEqual(3.2f, CombatHitDrain.PetalsFor(CombatHitClass.Strike), 1e-5f);
            Assert.AreEqual(0.4f, VesselSilence.PetalsForPoints(1), 1e-5f, "a bullet");
            Assert.AreEqual(0f, VesselSilence.PetalsForPoints(0));
        }

        [Test]
        public void ThePickSpreadsEvenlyOverTheHeldElements()
        {
            var bins = new int[4];
            for (uint serial = 1; serial <= 4000; serial++)
                bins[VesselElementalDebuffBySkimmerEffectSO.PickIndex(serial, 4)]++;
            foreach (int n in bins)
                Assert.That(n, Is.InRange(850, 1150), "each of four elements gets about a quarter of the shreds");

            for (uint serial = 1; serial <= 50; serial++)
            {
                Assert.AreEqual(0, VesselElementalDebuffBySkimmerEffectSO.PickIndex(serial, 1));
                Assert.That(VesselElementalDebuffBySkimmerEffectSO.PickIndex(serial, 3), Is.InRange(0, 2));
            }
        }

        [Test]
        public void TheShippedShredQuickensWithChargeFromOneSecondToATenth()
        {
            var dust = AssetDatabase.LoadAssetAtPath<VesselElementalDebuffBySkimmerEffectSO>(DustAsset);
            Assert.IsNotNull(dust, DustAsset);
            var field = typeof(VesselElementalDebuffBySkimmerEffectSO)
                .GetField("shredInterval", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "shredInterval");
            var interval = (ElementalFloat)field.GetValue(dust);

            Assert.AreEqual(Element.Charge, interval.ScaledElement);
            Assert.AreEqual(1f, interval.EvaluateAtNormalizedLevel(0f), 1e-5f, "Charge 0");
            Assert.AreEqual(0.55f, interval.EvaluateAtNormalizedLevel(0.5f), 1e-5f, "Charge 5");
            Assert.AreEqual(0.1f, interval.EvaluateAtNormalizedLevel(1f), 1e-5f, "Charge 10");
            Assert.AreEqual(0.1f, interval.EvaluateAtNormalizedLevel(1.5f), 1e-5f, "Charge 15 is floored");
            Assert.AreEqual(1f, dust.PetalsPerTake, "one petal a shred");
        }
    }
}
