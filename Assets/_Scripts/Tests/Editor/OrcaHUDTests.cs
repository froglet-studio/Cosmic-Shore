using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Orca's own HUD (2026-11-13, Tools/Build/author_orca_hud.py): its own view, a kit picture on every
    /// card and the level-5 card bound as each one's upgraded sprite, nested in Orca.prefab.</summary>
    public class OrcaHUDTests
    {
        const string VariantPath = "Assets/_Prefabs/UI Elements/VesselHUD/OrcaHUDVariant.prefab";
        const string OrcaPath = "Assets/_Prefabs/Spacevessels/Orca.prefab";

        [Test]
        public void EveryCardHasItsPictureAndItsLevelFiveCard()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            Assert.IsNotNull(hud, VariantPath);
            Assert.IsTrue(hud.TryGetComponent(out OrcaHUDView view), "the variant carries OrcaHUDView");
            Assert.AreEqual(4, view.abilityIcons.Count);
            foreach (var binding in view.abilityIcons)
            {
                Assert.IsNotNull(binding.icon, $"{binding.element} icon");
                Assert.IsNotNull(binding.icon.sprite, $"{binding.element} picture");
                StringAssert.StartsWith("Orca_", binding.icon.sprite.name);
                Assert.IsNotNull(binding.upgradedSprite, $"{binding.element} level-5 card");
                Assert.AreNotEqual(binding.icon.sprite, binding.upgradedSprite);
                Assert.AreEqual(1f, binding.icon.color.a, 1e-4f, $"{binding.element} is visible");
            }
        }

        [Test]
        public void TheOrcaFliesItsOwnHud()
        {
            var orca = AssetDatabase.LoadAssetAtPath<GameObject>(OrcaPath);
            Assert.IsNotNull(orca, OrcaPath);
            Assert.IsNotNull(orca.GetComponentInChildren<OrcaHUDView>(true), "Orca.prefab nests the Orca's HUD");
            Assert.IsNull(orca.GetComponentInChildren<SparrowHUDView>(true), "no longer the Sparrow's");
        }
    }
}
