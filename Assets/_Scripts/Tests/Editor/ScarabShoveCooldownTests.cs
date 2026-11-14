using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab's CHARGE row is the shove plate's cooldown (Garrett, 2026-11-11): 2.5 s at rest down to 1.25 s at
    /// Charge 10. It was the petals the shove's debuff bit off (x1 -> x2.5), a dial in whole petals and too coarse to tune.
    /// Locks the shipped curve on Scarab.prefab, its floor, its element, and that the bite is flat again.
    /// </summary>
    public class ScarabShoveCooldownTests
    {
        const string ScarabPrefabPath = "Assets/_Prefabs/Spacevessels/Scarab.prefab";
        const string ShoveBitePath =
            "Assets/_SO_Assets/Effects/Vessel Explosion Effects/ScarabShoveDebuffByExplosionEffect.asset";

        static ScarabCavitationBlast ShippedBlast()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScarabPrefabPath);
            Assert.IsNotNull(prefab, $"{ScarabPrefabPath} is missing");
            var blast = prefab.GetComponentInChildren<ScarabCavitationBlast>(true);
            Assert.IsNotNull(blast, "Scarab.prefab carries no ScarabCavitationBlast");
            return blast;
        }

        [Test]
        public void TheShoveWaits2Point5sAtRest_AndHalfThatAtChargeTen()
        {
            var blast = ShippedBlast();
            Assert.AreEqual(2.5f, blast.CooldownSecondsAtLevel(0f), 1e-4f, "Charge 0");
            Assert.AreEqual(1.875f, blast.CooldownSecondsAtLevel(0.5f), 1e-4f, "Charge 5");
            Assert.AreEqual(1.25f, blast.CooldownSecondsAtLevel(1f), 1e-4f, "Charge 10");
        }

        [Test]
        public void ADrainedChargeSlowsTheShove_AndTheFloorKeepsItFromFiringEveryFrame()
        {
            var blast = ShippedBlast();
            Assert.AreEqual(3.125f, blast.CooldownSecondsAtLevel(-0.5f), 1e-4f, "Charge -5");
            Assert.AreEqual(0.625f, blast.CooldownSecondsAtLevel(1.5f), 1e-4f, "Charge 15");
            Assert.AreEqual(0.625f, blast.CooldownSecondsAtLevel(3f), 1e-4f,
                "past the band the floor (x0.25) holds the wait at a quarter of the resting one");
        }

        [Test]
        public void TheCooldownIsScaledByCharge()
        {
            var element = new SerializedObject(ShippedBlast()).FindProperty("cooldownScale.element");
            Assert.IsNotNull(element, "ScarabCavitationBlast.cooldownScale was renamed");
            Assert.AreEqual((int)Element.Charge, element.intValue);
        }

        [Test]
        public void TheShovesBiteNoLongerScalesWithCharge()
        {
            var bite = AssetDatabase.LoadAssetAtPath<VesselElementalDebuffByExplosionEffectSO>(ShoveBitePath);
            Assert.IsNotNull(bite, $"{ShoveBitePath} is missing");
            var atFullCharge = new SerializedObject(bite).FindProperty("magnitudeAtFullCharge");
            Assert.IsNotNull(atFullCharge, "VesselElementalDebuffByExplosionEffectSO.magnitudeAtFullCharge was renamed");
            Assert.AreEqual(1f, atFullCharge.floatValue, 1e-6f,
                "Charge moved from the shove's bite to its cooldown (2026-11-11): the bite must not scale with it");
        }
    }
}
