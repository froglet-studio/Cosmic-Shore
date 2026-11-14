using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Squirrel's CHARGE row is the least speed a joust needs, vessel or lifeform (Garrett, 2026-11-11), and its Charge
    /// card is a speed gauge with that threshold marked. It used to be the petals a steal took, a dial in whole petals.
    ///
    /// <para>Locks the curve's load-bearing values, the shipped asset and its FOUR readers (the vessel joust's take, the
    /// lifeform joust, the HUD controller; the reporter reaches it through the take), that the steal is flat, the mark's
    /// show/hide rule and the gauge's full-scale speed. What a re-wire or a hand edit could silently move.</para>
    /// </summary>
    public class SquirrelJoustSpeedTests
    {
        const string JoustSpeedPath = "Assets/_SO_Assets/VesselActions/Squirrel/SquirrelJoustSpeed.asset";
        const string OvertakePath = "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffect.asset";
        const string WitherPath =
            "Assets/_SO_Assets/Effects/Vessel Crystal Effects/SquirrelVesselWitherLifeformByCrystalEffect.asset";
        const string SquirrelPrefabPath = "Assets/_Prefabs/Spacevessels/Squirrel.prefab";

        JoustSpeedConfigSO _fresh;

        [SetUp]
        public void Fresh() => _fresh = ScriptableObject.CreateInstance<JoustSpeedConfigSO>();

        [TearDown]
        public void Clear()
        {
            if (_fresh) Object.DestroyImmediate(_fresh);
        }

        [Test]
        public void TheMinimumIs90AtRest_AndFallsTo45AtChargeTen()
        {
            Assert.AreEqual(90f, _fresh.MinimumSpeedAtLevel(0f), 1e-4f, "Charge 0");
            Assert.AreEqual(67.5f, _fresh.MinimumSpeedAtLevel(0.5f), 1e-4f, "Charge 5");
            Assert.AreEqual(45f, _fresh.MinimumSpeedAtLevel(1f), 1e-4f, "Charge 10");
            Assert.AreEqual(Element.Charge, _fresh.ScaledElement);
        }

        [Test]
        public void ADrainedChargeRaisesTheMinimum_AndItNeverGoesBelowZero()
        {
            Assert.AreEqual(112.5f, _fresh.MinimumSpeedAtLevel(-0.5f), 1e-4f, "Charge -5");
            Assert.AreEqual(22.5f, _fresh.MinimumSpeedAtLevel(1.5f), 1e-4f, "Charge 15");
            Assert.AreEqual(0f, _fresh.MinimumSpeedAtLevel(3f), 1e-4f, "past the band, the floor holds it at no minimum");
        }

        [Test]
        public void NoJousterIsNeverFastEnough()
        {
            Assert.IsFalse(_fresh.Admits(null));
        }

        [Test]
        public void AJoustWithNoMinimumAdmitsEveryContact()
        {
            // The Swordfish's sword shares the take's type and wires no joust speed: it must land as before.
            var take = ScriptableObject.CreateInstance<VesselOvertakeBySkimmerEffectSO>();
            try { Assert.IsTrue(take.AdmitsContact(null, null)); }
            finally { Object.DestroyImmediate(take); }
        }

        [Test]
        public void TheShippedMinimumRunsFrom90To45OnCharge()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<JoustSpeedConfigSO>(JoustSpeedPath);
            Assert.IsNotNull(shipped, $"{JoustSpeedPath} is missing");
            Assert.AreEqual(Element.Charge, shipped.ScaledElement);
            Assert.AreEqual(90f, shipped.MinimumSpeedAtLevel(0f), 1e-4f, "Charge 0");
            Assert.AreEqual(45f, shipped.MinimumSpeedAtLevel(1f), 1e-4f, "Charge 10");
        }

        [Test]
        public void TheVesselJoust_TheLifeformJoust_AndTheHudAllReadTheSameAsset()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<JoustSpeedConfigSO>(JoustSpeedPath);
            Assert.IsNotNull(shipped, $"{JoustSpeedPath} is missing");

            Assert.AreSame(shipped, JoustSpeedOn(AssetDatabase.LoadAssetAtPath<ScriptableObject>(OvertakePath)),
                "the vessel joust (score, steal and friendly buff) must ask SquirrelJoustSpeed");
            Assert.AreSame(shipped, JoustSpeedOn(AssetDatabase.LoadAssetAtPath<ScriptableObject>(WitherPath)),
                "the lifeform joust must ask SquirrelJoustSpeed");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SquirrelPrefabPath);
            Assert.IsNotNull(prefab, $"{SquirrelPrefabPath} is missing");
            var controller = prefab.GetComponentInChildren<SquirrelHUDController>(true);
            Assert.IsNotNull(controller, "Squirrel.prefab carries no SquirrelHUDController");
            Assert.AreSame(shipped, JoustSpeedOn(controller),
                "the Charge card's tick must be drawn from the same asset the joust asks");
        }

        [Test]
        public void TheSquirrelsStealNoLongerScalesWithCharge()
        {
            var overtake = AssetDatabase.LoadAssetAtPath<ScriptableObject>(OvertakePath);
            Assert.IsNotNull(overtake, $"{OvertakePath} is missing");
            var enabled = new SerializedObject(overtake).FindProperty("stealScale.Enabled");
            Assert.IsNotNull(enabled, "VesselOvertakeBySkimmerEffectSO.stealScale was renamed");
            Assert.IsFalse(enabled.boolValue, "Charge moved to the joust's speed (2026-11-11): the steal is the flat price");
        }

        [Test]
        public void TheMarkShowsTicksBelowTheMinimum_AndAGapFromItOn()
        {
            var below = SquirrelVesselHUDView.JoustMarksFor(0.2f, 0.3f);
            Assert.IsTrue(below.Ticks, "too slow: the ticks show where the joust starts");
            Assert.IsFalse(below.Gap);

            var at = SquirrelVesselHUDView.JoustMarksFor(0.3f, 0.3f);
            Assert.IsTrue(at.Gap, "exactly at the minimum the joust lands, so the gap shows");
            Assert.IsFalse(at.Ticks);

            var none = SquirrelVesselHUDView.JoustMarksFor(0.5f, 0f);
            Assert.IsFalse(none.Ticks || none.Gap, "no minimum, no mark");
        }

        [Test]
        public void TheGaugeIsFullAtCruiseTimesTheBoostCeiling()
        {
            Assert.AreEqual(300f, SquirrelHUDController.JoustGaugeTopSpeed(60f, 5f), 1e-4f, "the Squirrel: 60 x 5");
            Assert.AreEqual(60f, SquirrelHUDController.JoustGaugeTopSpeed(60f, 0.5f), 1e-4f, "a ceiling under 1 never shrinks it");
            Assert.AreEqual(0f, SquirrelHUDController.JoustGaugeTopSpeed(0f, 5f), 1e-4f, "no cruise yet: no gauge");
        }

        static Object JoustSpeedOn(Object owner)
        {
            Assert.IsNotNull(owner);
            var property = new SerializedObject(owner).FindProperty("joustSpeed");
            Assert.IsNotNull(property, $"{owner.name} has no joustSpeed field");
            return property.objectReferenceValue;
        }
    }
}
