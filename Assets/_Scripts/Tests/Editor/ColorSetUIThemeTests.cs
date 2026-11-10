#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The UI theme a worn colour set gives (<see cref="ColorSetUITheme"/>) is the analytics console's and the Fleet
    /// Codex's theme for that set: every shipped set's tokens match <c>Tools/Analytics/console/themes.css</c>
    /// (written by <c>make_themes.py</c> from the same assets) to within an 8-bit step; and the live theme answers
    /// <see cref="UITheme.Resolve"/> while worn and leaves it authored once cleared.
    /// </summary>
    public class ColorSetUIThemeTests
    {
        const string CatalogPath = "Assets/_SO_Assets/Color Palettes/ColorSetCatalog.asset";
        const string ThemePath = "Assets/_SO_Assets/UI/UITheme.asset";
        const string ContainerPath = "Assets/_SO_Assets/ThemeManagerDataContainer.asset";

        // The console's token for each Unity role it themes.
        static readonly (UIColorToken token, string css)[] Pairs =
        {
            (UIColorToken.SurfaceVeryDark, "--fs-ground"), (UIColorToken.SurfaceBlack, "--fs-ground-deep"),
            (UIColorToken.SurfaceDark, "--fs-surface"), (UIColorToken.SurfaceLight, "--fs-surface-raised"),
            (UIColorToken.TextLight, "--fs-text"), (UIColorToken.NeutralLightest, "--fs-text-soft"),
            (UIColorToken.InactiveLight, "--fs-text-off"), (UIColorToken.Cta, "--fs-cta"),
            (UIColorToken.Danger, "--fs-danger"), (UIColorToken.Selection, "--fs-selection"),
            (UIColorToken.ControlActive, "--fs-control"), (UIColorToken.Purchase, "--fs-purchase"),
        };

        static string ThemesCss => Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Tools", "Analytics", "console", "themes.css");

        static Dictionary<string, string> Block(string css, string id)
        {
            var m = Regex.Match(css, "\\[data-fs-theme=\"" + Regex.Escape(id) + "\"\\] \\{([^}]*)\\}");
            Assert.IsTrue(m.Success, $"themes.css has no theme {id}: run python3 Tools/Analytics/console/make_themes.py");
            var vars = new Dictionary<string, string>();
            foreach (Match v in Regex.Matches(m.Groups[1].Value, "(--[\\w-]+): (#[0-9A-Fa-f]{6});"))
                vars[v.Groups[1].Value] = v.Groups[2].Value;
            return vars;
        }

        [Test]
        public void EveryShippedSet_GivesTheConsolesTheme()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ColorSetCatalogSO>(CatalogPath);
            var theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(ThemePath);
            Assert.IsNotNull(catalog);
            Assert.IsTrue(File.Exists(ThemesCss), ThemesCss);
            var css = File.ReadAllText(ThemesCss);
            for (int i = 0; i < catalog.Count; i++)
            {
                var entry = catalog.Get(i);
                var vars = Block(css, entry.id);
                var palette = ColorSetUITheme.Build(theme, entry.colorSet);
                foreach (var (token, name) in Pairs)
                {
                    Assert.IsTrue(vars.ContainsKey(name), $"{entry.id}: themes.css has no {name}");
                    Assert.IsTrue(ColorUtility.TryParseHtmlString(vars[name], out var want));
                    var got = palette[token];
                    float off = Mathf.Max(Mathf.Abs(got.r - want.r), Mathf.Max(Mathf.Abs(got.g - want.g), Mathf.Abs(got.b - want.b)));
                    Assert.LessOrEqual(off, 1.01f / 255f,
                        $"{entry.id} {token}: #{ColorUtility.ToHtmlStringRGB(got)} in the game, {vars[name]} on the console");
                }
            }
        }

        // The tokens a colour set gives directly (TracedColors ui-token ids, Docs/VesselModels/COLOR_TRACE.md).
        static readonly UIColorToken[] Given =
        {
            UIColorToken.ElementCharge, UIColorToken.ElementMass, UIColorToken.ElementSpace, UIColorToken.ElementTime,
            UIColorToken.Tooltip,
        };

        [Test]
        public void TheElementColours_AreTheSetsOwn_AndPhoticsAreTheAuthored()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(ThemePath);
            var container = AssetDatabase.LoadAssetAtPath<ThemeManagerDataContainerSO>(ContainerPath);
            var catalog = AssetDatabase.LoadAssetAtPath<ColorSetCatalogSO>(CatalogPath);
            Assert.AreSame(theme, container.UITheme, "ThemeManagerDataContainer names no UI theme to re-hue");
            for (int i = 0; i < catalog.Count; i++)
            {
                var entry = catalog.Get(i);
                var palette = ColorSetUITheme.Build(theme, entry.colorSet);
                foreach (var t in Given)
                {
                    Assert.IsTrue(entry.colorSet.TryGetTracedColor(ColorSetUITheme.TokenId(t), out var given),
                        $"{entry.id} carries no {t}: run python3 Tools/Build/author_colour_set_assets.py");
                    Assert.AreEqual(new Color(given.r, given.g, given.b, 1f), palette[t], $"{entry.id} {t} is not the set's own");
                }
            }
            var photic = catalog.Get(catalog.IndexOfId(ColorTrace.AuthoredSetId));
            Assert.IsNotNull(photic, "the catalog names no Photic");
            var authored = ColorSetUITheme.Build(theme, photic.colorSet);
            foreach (var t in Given)
            {
                var want = UITheme.ResolveAuthored(theme, t);
                float off = Mathf.Max(Mathf.Abs(authored[t].r - want.r), Mathf.Max(Mathf.Abs(authored[t].g - want.g), Mathf.Abs(authored[t].b - want.b)));
                Assert.LessOrEqual(off, 1e-5f, $"Photic's {t} is not the authored colour");
            }
        }

        [Test]
        public void Resolve_FollowsTheWornTheme_UntilCleared()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(ThemePath);
            var catalog = AssetDatabase.LoadAssetAtPath<ColorSetCatalogSO>(CatalogPath);
            var dartFrog = catalog.Get(catalog.IndexOfId("dartfrog"));
            Assert.IsNotNull(dartFrog);
            var palette = ColorSetUITheme.Build(theme, dartFrog.colorSet);
            try
            {
                UIThemeLive.Wear(palette, theme);
                Assert.AreEqual(palette[UIColorToken.SurfaceDark], UITheme.Resolve(theme, UIColorToken.SurfaceDark));
                Assert.AreNotEqual(theme.surfaceDark, UITheme.Resolve(theme, UIColorToken.SurfaceDark), "Dart Frog's panels are bronze");
                Assert.IsTrue(UIThemeLive.TokenOf(theme.textLight, ColorSetUITheme.Authored(theme), out var token));
                Assert.AreEqual(UIColorToken.TextLight, token, "a raw colour typed from the theme is found as its token");
            }
            finally
            {
                UIThemeLive.Clear();
            }
            Assert.AreEqual(theme.surfaceDark, UITheme.Resolve(theme, UIColorToken.SurfaceDark));
        }
    }
}
#endif
