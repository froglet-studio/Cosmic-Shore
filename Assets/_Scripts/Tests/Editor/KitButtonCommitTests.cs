#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Play / Ready button of Frogma's Style Guide 5 "Buttons and their states" (Docs/UI/specs/sg5-play-ready.md,
    /// built by /frogma-to-unity): a commitment the player cannot take back is the commit Ruby (Unity's Tooltip token,
    /// #A600FF) under the on-commit white label, flat. The lime Play variant is unchanged.
    /// Tools/Build/author_ui_kit_prefabs.py --check guards the prefab side.
    /// </summary>
    public class KitButtonCommitTests
    {
        [Test]
        public void CommitKeepsItsSerializedValue()
        {
            Assert.AreEqual(10, (int)KitButtonVariant.Commit);
            Assert.AreEqual(5, (int)KitButtonVariant.Play, "the lime Play keeps its value");
        }

        [Test]
        public void OpaqueCommitIsRubyUnderWhite()
        {
            var (border, fill, content) = KitButton.Colors(KitButtonVariant.Commit, true, null);
            Assert.AreEqual((Color32)UITheme.Tooltip, (Color32)fill, "the fill is the commit Ruby");
            Assert.AreEqual((Color32)UITheme.Tooltip, (Color32)border, "the border is the fill's own Ruby (flat)");
            Assert.AreEqual((Color32)UITheme.OnCommit, (Color32)content, "the label is on-commit white");
            Assert.AreEqual(new Color32(0xA6, 0x00, 0xFF, 0xFF), (Color32)fill);
        }

        [Test]
        public void TransparentCommitFollowsTheKitRule()
        {
            var (border, fill, content) = KitButton.Colors(KitButtonVariant.Commit, false, null);
            Assert.AreEqual((Color32)UITheme.Tooltip, (Color32)border);
            Assert.AreEqual((Color32)UITheme.Tooltip, (Color32)content);
            Assert.AreEqual(0.12f, fill.a, 1e-4f, "the role colour at 12%, as every transparent kit button");
        }

        [Test]
        public void PlayIsStillTheLimeCallToAction()
        {
            var (_, fill, _) = KitButton.Colors(KitButtonVariant.Play, true, null);
            Assert.AreEqual((Color32)UITheme.Cta, (Color32)fill);
        }

        [Test]
        public void AThemeAssetDrivesTheCommitColour()
        {
            var theme = ScriptableObject.CreateInstance<UIThemeSO>();
            try
            {
                theme.tooltip = Color.red;
                var (_, fill, _) = KitButton.Colors(KitButtonVariant.Commit, true, theme);
                Assert.AreEqual(Color.red, fill, "the Commit fill reads the theme's commit (tooltip) token, never a literal");
            }
            finally
            {
                Object.DestroyImmediate(theme);
            }
        }
    }
}
#endif
