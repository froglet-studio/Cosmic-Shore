#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The selection colour: one token (Rev 7, Docs/MENU_REDESIGN_B.md §47.1), appended at a fixed index so every prefab
    /// that serialized it keeps it. Since 2026-11-05 it is Jade, the controlActive colour (Docs/UI_STYLE_GUIDE.md D3: a
    /// choice the player can claim and reclaim), falling back to #00D4FF when no theme is wired; the old sky blue #3DB2FF
    /// sat in the forbidden band between Blue and Jade. The Commit token beside it is Ruby, for Play and Ready.
    /// Tools/Build/author_ui_kit_prefabs.py --check guards the prefab side.
    /// </summary>
    public class SelectionStyleTests
    {
        [Test]
        public void SelectionKeepsItsSerializedIndex()
        {
            Assert.AreEqual(17, (int)UIColorToken.Selection);
            Assert.AreEqual(16, (int)UIColorToken.ElementTime, "Selection was appended after the element colours");
        }

        [Test]
        public void SelectionFallsBackToJade()
        {
            Color32 jade = UITheme.Resolve(null, UIColorToken.Selection);
            Assert.AreEqual(new Color32(0x00, 0xD4, 0xFF, 0xFF), jade);
            Assert.AreEqual((Color32)UITheme.Resolve(null, UIColorToken.ControlActive), jade,
                "the selection is the Jade control colour: a choice you can claim and reclaim");
        }

        [Test]
        public void CommitKeepsItsSerializedIndexAndFallsBackToRuby()
        {
            Assert.AreEqual(18, (int)UIColorToken.Commit, "Commit was appended after Selection");
            Assert.AreEqual(new Color32(0xA6, 0x00, 0xFF, 0xFF), (Color32)UITheme.Resolve(null, UIColorToken.Commit));
            Assert.AreEqual(10, (int)KitButtonVariant.Commit, "the Commit button was appended after the retired variants");
        }

        [Test]
        public void CommitIsNeitherTheCallToActionNorTheSelection()
        {
            var commit = UITheme.Resolve(null, UIColorToken.Commit);
            Assert.AreNotEqual(UITheme.Resolve(null, UIColorToken.Cta), commit, "Play and Ready are Ruby, not the lime call to action");
            Assert.AreNotEqual(UITheme.Resolve(null, UIColorToken.Selection), commit, "a commitment must read apart from a selection");
        }

        [Test]
        public void SelectionIsNotTheCallToAction()
        {
            Assert.AreNotEqual(UITheme.Resolve(null, UIColorToken.Cta), UITheme.Resolve(null, UIColorToken.Selection),
                "a selection must read apart from the green call to action");
        }

        [Test]
        public void AThemeAssetOverridesTheFallback()
        {
            var theme = ScriptableObject.CreateInstance<UIThemeSO>();
            try
            {
                theme.selection = Color.magenta;
                theme.commit = Color.cyan;
                Assert.AreEqual(Color.magenta, UITheme.Resolve(theme, UIColorToken.Selection));
                Assert.AreEqual(Color.cyan, UITheme.Resolve(theme, UIColorToken.Commit));
            }
            finally
            {
                Object.DestroyImmediate(theme);
            }
        }
    }
}
#endif
