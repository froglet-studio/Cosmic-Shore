using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Honeyguide's own HUD (2026-11-13, Tools/Build/author_honeyguide_hud.py): its own view, a kit picture on every
    /// card and the level-5 card bound as each one's upgraded sprite, nested in Honeyguide.prefab.</summary>
    public class HoneyguideHUDTests
    {
        const string VariantPath = "Assets/_Prefabs/UI Elements/VesselHUD/HoneyguideHUDVariant.prefab";
        const string HoneyguidePath = "Assets/_Prefabs/Spacevessels/Honeyguide.prefab";

        [Test]
        public void EveryCardHasItsPictureAndItsLevelFiveCard()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            Assert.IsNotNull(hud, VariantPath);
            Assert.IsTrue(hud.TryGetComponent(out HoneyguideHUDView view), "the variant carries HoneyguideHUDView");
            Assert.AreEqual(4, view.abilityIcons.Count);
            foreach (var binding in view.abilityIcons)
            {
                Assert.IsNotNull(binding.icon, $"{binding.element} icon");
                Assert.IsNotNull(binding.icon.sprite, $"{binding.element} picture");
                StringAssert.StartsWith("Honeyguide_", binding.icon.sprite.name);
                Assert.IsNotNull(binding.upgradedSprite, $"{binding.element} level-5 card");
                Assert.AreNotEqual(binding.icon.sprite, binding.upgradedSprite);
                Assert.AreEqual(1f, binding.icon.color.a, 1e-4f, $"{binding.element} is visible");
            }
        }

        [Test]
        public void TheHoneyguideFliesItsOwnHud()
        {
            var honeyguide = AssetDatabase.LoadAssetAtPath<GameObject>(HoneyguidePath);
            Assert.IsNotNull(honeyguide, HoneyguidePath);
            Assert.IsNotNull(honeyguide.GetComponentInChildren<HoneyguideHUDView>(true), "Honeyguide.prefab nests the Honeyguide's HUD");
            Assert.IsNull(honeyguide.GetComponentInChildren<SparrowHUDView>(true), "no longer the Sparrow's");
            Assert.IsNull(honeyguide.GetComponentInChildren<SparrowHUDController>(true), "nor the Sparrow's controller");
        }
    }
}
