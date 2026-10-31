#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Each free elemental crystal wears its own shade of the collectability lime, the Wrasse's four lasso shades
    /// (Garrett, 2026-10-27: the Mass and omni crystals were "the hardest to distinguish"). Docs/PALETTE.md §2.11.
    /// </summary>
    public class ElementalCrystalShadeTests
    {
        const string PalettePath = "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset";

        [Test]
        public void WithHueOf_TakesTheHueAtTheColoursBrightestChannel_AndKeepsAlpha()
        {
            var cta = new Color(0.59f, 0.92f, 0.16f, 0.5f);
            var shaded = cta.WithHueOf(new Color(0.25f, 1f, 0f, 1f));
            Assert.AreEqual(0.25f * 0.92f, shaded.r, 1e-6f);
            Assert.AreEqual(0.92f, shaded.g, 1e-6f, "a shade whose brightest channel is 1 lands on the CTA's peak");
            Assert.AreEqual(0f, shaded.b, 1e-6f);
            Assert.AreEqual(0.5f, shaded.a, 1e-6f, "alpha is the colour's, never the shade's");

            var darker = cta.WithHueOf(new Color(0.3682f, 0.7764f, 0f, 1f));
            Assert.AreEqual(0.7764f * 0.92f, darker.g, 1e-6f, "a darker shade stays darker by the same ratio");
        }

        [Test]
        public void TryGetElementalCrystalColor_IndexesByElementMinusOne_AndRefusesTheRest()
        {
            var env = new EnvironmentColorSet
            {
                ElementalCrystalColors = new[]
                {
                    new Color(1f, 0f, 0f, 1f), new Color(0f, 1f, 0f, 1f), new Color(0f, 0f, 1f, 1f), new Color(1f, 1f, 0f, 0f),
                },
            };
            Assert.IsTrue(env.TryGetElementalCrystalColor(Element.Charge, out var charge));
            Assert.AreEqual(new Color(1f, 0f, 0f, 1f), charge);
            Assert.IsTrue(env.TryGetElementalCrystalColor(Element.Space, out var space));
            Assert.AreEqual(new Color(0f, 0f, 1f, 1f), space);
            Assert.IsFalse(env.TryGetElementalCrystalColor(Element.Time, out _), "alpha 0 is unauthored: the crystal keeps the CTA");
            Assert.IsFalse(env.TryGetElementalCrystalColor(Element.Omni, out _), "the omni wears the palette's lime");
            Assert.IsFalse(env.TryGetElementalCrystalColor(Element.None, out _));
            Assert.IsFalse(new EnvironmentColorSet { ElementalCrystalColors = null }.TryGetElementalCrystalColor(Element.Mass, out _));
        }

        [Test]
        public void ShippedPalette_AuthorsFourDistinctLimeShades()
        {
            var palette = AssetDatabase.LoadAssetAtPath<SO_ColorSet>(PalettePath);
            Assert.IsNotNull(palette, PalettePath);
            var shades = new Color[4];
            for (int i = 0; i < 4; i++)
            {
                var element = (Element)(i + 1);
                Assert.IsTrue(palette.EnvironmentColors.TryGetElementalCrystalColor(element, out shades[i]),
                    $"{element} has no crystal shade (run Tools/Build/kite_mote_colors.py)");
                Assert.Greater(shades[i].g, shades[i].r, $"{element}: a lime leads with green");
                Assert.AreEqual(0f, shades[i].b, 1e-6f, $"{element}: a lime carries no blue");
            }
            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                    Assert.AreNotEqual(shades[i], shades[j], $"{(Element)(i + 1)} and {(Element)(j + 1)} share a shade");
        }
    }
}
#endif
