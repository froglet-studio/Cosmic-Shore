using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The domain accent reads the live palette accessor, and the fallback table matches it.</summary>
    public class CharacterPaletteBindingTests
    {
        const string ColorSetPath = "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset";

        [Test]
        public void FallbackMatchesTheLivePaletteSignalColours()
        {
            var set = AssetDatabase.LoadAssetAtPath<SO_ColorSet>(ColorSetPath);
            Assert.IsNotNull(set, "live palette asset missing (PALETTE.md §1)");
            foreach (var d in new[] { Domains.Jade, Domains.Ruby, Domains.Gold })
            {
                var live = set.GetDomainSignalColor(d);
                var fallback = CharacterPaletteBinding.FallbackSignal(d);
                Assert.AreEqual(live.r, fallback.r, 0.01f, d.ToString());
                Assert.AreEqual(live.g, fallback.g, 0.01f, d.ToString());
                Assert.AreEqual(live.b, fallback.b, 0.01f, d.ToString());
                var resolved = CharacterPaletteBinding.Resolve(d, set);
                Assert.AreEqual(live, resolved.Accent);
            }
        }

        [Test]
        public void DefaultModeKeepsSkinOffTheDomainPalette()
        {
            var accent = CharacterPaletteBinding.Resolve(Domains.Jade, null);
            Assert.AreEqual(CharacterPaletteBinding.Mode == CharacterPaletteBinding.DomainAccentMode.Accent ? 0f : CharacterPaletteBinding.DomainSkinTint, accent.SkinTint);
            Assert.Greater(accent.IrisTint, 0f);
        }
    }
}
