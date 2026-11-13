using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Jacks' own HUD (2026-11-13, Tools/Build/author_jacks_hud.py): its own view, a kit picture on every
    /// card and the level-5 card bound as each one's upgraded sprite, nested in Jacks.prefab.</summary>
    public class JacksHUDTests
    {
        const string VariantPath = "Assets/_Prefabs/UI Elements/VesselHUD/JacksHUDVariant.prefab";
        const string JacksPath = "Assets/_Prefabs/Spacevessels/Jacks.prefab";

        [Test]
        public void EveryCardHasItsPictureAndItsLevelFiveCard()
        {
            var hud = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath);
            Assert.IsNotNull(hud, VariantPath);
            Assert.IsTrue(hud.TryGetComponent(out JacksHUDView view), "the variant carries JacksHUDView");
            Assert.AreEqual(4, view.abilityIcons.Count);
            foreach (var binding in view.abilityIcons)
            {
                Assert.IsNotNull(binding.icon, $"{binding.element} icon");
                Assert.IsNotNull(binding.icon.sprite, $"{binding.element} picture");
                StringAssert.StartsWith("Jacks_", binding.icon.sprite.name);
                Assert.IsNotNull(binding.upgradedSprite, $"{binding.element} level-5 card");
                Assert.AreNotEqual(binding.icon.sprite, binding.upgradedSprite);
                Assert.AreEqual(1f, binding.icon.color.a, 1e-4f, $"{binding.element} is visible");
            }
        }

        [Test]
        public void TheJacksFlyTheirOwnHud()
        {
            var jacks = AssetDatabase.LoadAssetAtPath<GameObject>(JacksPath);
            Assert.IsNotNull(jacks, JacksPath);
            Assert.IsNotNull(jacks.GetComponentInChildren<JacksHUDView>(true), "Jacks.prefab nests the Jacks' HUD");
            Assert.IsNull(jacks.GetComponentInChildren<RhinoVesselHUDView>(true), "no longer the Rhino's (its katana covered the cards)");
        }
    }
}
