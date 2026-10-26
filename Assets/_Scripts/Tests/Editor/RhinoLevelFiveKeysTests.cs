using CosmicShore.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino sword's two level-5 keys (Garrett, 2026-10-26; RHINO_ENERGY_SWORD.md): SPACE 5 is the
    /// only way the sword destroys super-shielded prisms, CHARGE 5 is the only way it STEALS from a
    /// rival vessel (below it the petals are ejected). Both live on serialized element fields, so the
    /// shipped assets are the record - a field reset to None would silently hand the hull a free
    /// upgrade.
    /// </summary>
    public class RhinoLevelFiveKeysTests
    {
        const string SwordConfigPath =
            "Assets/_SO_Assets/VesselActions/Rhino/ShieldSkimmerScaleConfig.asset";
        const string StealEffectPath =
            "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/RhinoSwordStealBySkimmerEffect.asset";
        const string SquirrelJoustPath =
            "Assets/_SO_Assets/Effects/Vessel Skimmer Effects/VesselOvertakeBySkimmerEffect.asset";

        static Element ElementField(string assetPath, string field)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            Assert.IsNotNull(asset, $"missing asset {assetPath}");
            var prop = new SerializedObject(asset).FindProperty(field);
            Assert.IsNotNull(prop, $"{assetPath} has no serialized '{field}'");
            return (Element)prop.intValue;
        }

        [Test]
        public void SuperShieldPop_IsTheSpaceLevelFiveUpgrade() =>
            Assert.AreEqual(Element.Space, ElementField(SwordConfigPath, "superShieldUpgradeElement"));

        [Test]
        public void SwordSteal_IsTheChargeLevelFiveUpgrade() =>
            Assert.AreEqual(Element.Charge, ElementField(StealEffectPath, "stealUpgradeElement"));

        [Test]
        public void SquirrelJoust_AlwaysSteals() =>
            Assert.AreEqual(Element.None, ElementField(SquirrelJoustPath, "stealUpgradeElement"));
    }
}
