using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Swordfish's three level-5 upgrades are GATES: below level 5 the ability does not do the upgraded thing,
    /// at level 5 it does (R_VesselActions/SWORDFISH.md). <c>RhinoLevelFiveKeysTests</c> pins which element each gate
    /// reads; this drives each gate method on the SHIPPED asset with the upgrade off and then on, so removing a gate
    /// (an early <c>return true</c>, a field reset to None, a deleted <c>IsUpgradeActive</c>) fails here.
    /// <list type="bullet">
    /// <item><b>Shield Breaker</b> (Space 5): only then does the sword destroy super-shielded prisms.</item>
    /// <item><b>Danger Afterimages</b> (Mass 5): only then are the afterimages laid as danger prisms.</item>
    /// <item><b>Sword Steal</b> (Charge 5): only then do a sword hit's petals land on the Swordfish.</item>
    /// </list>
    /// Tests only: nothing here changes the kit.
    /// </summary>
    public class SwordfishLevelFiveGateTests
    {
        const string SwordConfigPath = "Assets/_SO_Assets/VesselActions/Swordfish/ShieldSkimmerScaleConfig.asset";
        const string AfterimageConfigPath = "Assets/_SO_Assets/VesselActions/Swordfish/SwordfishSwordAfterimageConfig.asset";
        const string StealEffectPath =
            "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/SwordfishSwordStealBySkimmerEffect.asset";

        readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o) Object.DestroyImmediate(o);
            _made.Clear();
        }

        static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"{path} is missing or is not a {typeof(T).Name}");
            return asset;
        }

        /// <summary>A vessel status whose ability handler has exactly <paramref name="unlocked"/> upgraded (none for
        /// <see cref="Element.None"/>). The handler is never spawned, so it answers from its local latch.</summary>
        IVesselStatus StatusWith(Element unlocked)
        {
            var go = new GameObject("SwordfishGateTest");
            _made.Add(go);
            var handler = go.AddComponent<R_VesselElementalAbilityHandler>();
            var latch = (Dictionary<Element, bool>)typeof(R_VesselElementalAbilityHandler)
                .GetField("_unlocked", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(handler);
            Assert.IsNotNull(latch, "R_VesselElementalAbilityHandler no longer keeps its latch in _unlocked; update this fake");
            if (unlocked != Element.None) latch[unlocked] = true;

            var status = DispatchProxy.Create<IVesselStatus, StatusProxy>();
            ((StatusProxy)(object)status).Handler = handler;
            return status;
        }

        static readonly Element[] Others = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        [Test]
        public void ShieldBreaker_PopsSuperShields_OnlyAtSpaceFive()
        {
            var config = Load<ShieldSkimmerScaleConfigSO>(SwordConfigPath);
            Assert.IsFalse(config.SuperShieldUpgradeActive(StatusWith(Element.None)),
                "below Space 5 the blade must bind in super-shielded mass, not pop it");
            foreach (var e in Others)
                if (e != Element.Space)
                    Assert.IsFalse(config.SuperShieldUpgradeActive(StatusWith(e)), $"{e} 5 must not open Shield Breaker");
            Assert.IsTrue(config.SuperShieldUpgradeActive(StatusWith(Element.Space)), "Space 5 is Shield Breaker");
        }

        [Test]
        public void DangerAfterimages_AreDanger_OnlyAtMassFive()
        {
            var config = Load<SwordfishSwordAfterimageConfigSO>(AfterimageConfigPath);
            Assert.AreEqual(Element.Mass, config.DangerUpgradeElement);
            Assert.IsFalse(config.DangerActive(StatusWith(Element.None)), "below Mass 5 the afterimages are plain mass");
            foreach (var e in Others)
                if (e != Element.Mass)
                    Assert.IsFalse(config.DangerActive(StatusWith(e)), $"{e} 5 must not open Danger Afterimages");
            Assert.IsTrue(config.DangerActive(StatusWith(Element.Mass)), "Mass 5 is Danger Afterimages");
        }

        [Test]
        public void SwordSteal_KeepsThePetals_OnlyAtChargeFive()
        {
            var effect = Load<VesselOvertakeBySkimmerEffectSO>(StealEffectPath);
            // The gate is private on the shared effect class; read it rather than widen the kit's code for a test.
            var gate = typeof(VesselOvertakeBySkimmerEffectSO).GetMethod("StealUnlocked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(gate, "VesselOvertakeBySkimmerEffectSO.StealUnlocked is gone; find the Sword Steal gate");
            bool Steals(Element unlocked) => (bool)gate.Invoke(effect, new object[] { StatusWith(unlocked) });

            Assert.IsFalse(Steals(Element.None), "below Charge 5 a sword hit ejects the petals as crystals");
            foreach (var e in Others)
                if (e != Element.Charge)
                    Assert.IsFalse(Steals(e), $"{e} 5 must not open Sword Steal");
            Assert.IsTrue(Steals(Element.Charge), "Charge 5 is Sword Steal");
        }

        /// <summary>The one member these gates read: the ability handler. Everything else answers its default.</summary>
        public class StatusProxy : DispatchProxy
        {
            public R_VesselElementalAbilityHandler Handler;

            protected override object Invoke(MethodInfo m, object[] args)
            {
                if (m.Name == "get_ElementalAbilityHandler") return Handler;
                return m.ReturnType.IsValueType && m.ReturnType != typeof(void) ? Activator.CreateInstance(m.ReturnType) : null;
            }
        }
    }
}
