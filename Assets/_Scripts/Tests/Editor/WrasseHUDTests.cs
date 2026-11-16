using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Wrasse's own HUD (2026-11-13, Tools/Build/author_wrasse_hud.py): its own view, a kit picture on every
    /// card and the level-5 card bound as each one's upgraded sprite, nested in Wrasse.prefab.</summary>
    public class WrasseHUDTests
    {
        const string VariantPath = "Assets/_Prefabs/UI Elements/VesselHUD/WrasseHUDVariant.prefab";
        const string WrassePath = "Assets/_Prefabs/Spacevessels/Wrasse.prefab";

        [Test]
        public void EveryCardHasItsPictureAndItsLevelFiveCard()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            Assert.IsNotNull(hud, VariantPath);
            Assert.IsTrue(hud.TryGetComponent(out WrasseHUDView view), "the variant carries WrasseHUDView");
            Assert.AreEqual(4, view.abilityIcons.Count);
            foreach (var binding in view.abilityIcons)
            {
                Assert.IsNotNull(binding.icon, $"{binding.element} icon");
                Assert.IsNotNull(binding.icon.sprite, $"{binding.element} picture");
                StringAssert.StartsWith("Wrasse_", binding.icon.sprite.name);
                Assert.IsNotNull(binding.upgradedSprite, $"{binding.element} level-5 card");
                Assert.AreNotEqual(binding.icon.sprite, binding.upgradedSprite);
                Assert.AreEqual(1f, binding.icon.color.a, 1e-4f, $"{binding.element} is visible");
            }
        }

        [Test]
        public void TheWrasseFliesItsOwnHud()
        {
            var wrasse = AssetDatabase.LoadAssetAtPath<GameObject>(WrassePath);
            Assert.IsNotNull(wrasse, WrassePath);
            Assert.IsNotNull(wrasse.GetComponentInChildren<WrasseHUDView>(true), "Wrasse.prefab nests the Wrasse's HUD");
            Assert.IsNull(wrasse.GetComponentInChildren<SparrowHUDView>(true), "no longer the Sparrow's");
        }
    }
}
