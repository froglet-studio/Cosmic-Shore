#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rev 7 selection colour (Docs/MENU_REDESIGN_B.md §47.1, the owner 2026-10-17: "sky blue"): one token,
    /// appended at a fixed index so every prefab that serialized it keeps it, falling back to #3DB2FF when no theme
    /// is wired. Tools/Build/author_ui_kit_prefabs.py --self-test guards the prefab side.
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
        public void SelectionFallsBackToSkyBlue()
        {
            Color32 blue = UITheme.Resolve(null, UIColorToken.Selection);
            Assert.AreEqual(new Color32(0x3D, 0xB2, 0xFF, 0xFF), blue);
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
                Assert.AreEqual(Color.magenta, UITheme.Resolve(theme, UIColorToken.Selection));
            }
            finally
            {
                Object.DestroyImmediate(theme);
            }
        }
    }
}
#endif
