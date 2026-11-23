using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Client;
using CosmicShore.Content;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

// The port paints the palette the game paints: whatever SO_ColorSet ThemeManagerDataContainer.asset's ColorSet slot
// names (Nocturne since 2026-11-03, Docs/PALETTE.md §1). It used to carry a hand transcription of the original
// palette, which kept rendering Photic after Unity moved on. These tests read the container and the palette with
// plain text, independently of the YAML reader under test.
public class LivePaletteTests
{
    static readonly string Root = AssetDatabase.FindProjectRoot();

    static string ContainerGuid()
    {
        string text = File.ReadAllText(Path.Combine(Root, SkimRaceTheme.ContainerAssetPath));
        return Regex.Match(text, @"\n  ColorSet: \{fileID: \d+, guid: ([0-9a-f]{32})").Groups[1].Value;
    }

    static string PaletteWithGuid(string guid) =>
        Directory.EnumerateFiles(Path.Combine(Root, "Assets"), "*.asset.meta", SearchOption.AllDirectories)
            .First(m => File.ReadLines(m).Take(4).Any(l => l == "guid: " + guid))[..^".meta".Length];

    static Color AuthoredColor(string palettePath, string block, string field)
    {
        string text = File.ReadAllText(palettePath);
        int at = text.IndexOf("\n  " + block + ":", StringComparison.Ordinal);
        var m = Regex.Match(text[at..], @"\n    " + field + @": \{r: ([-\d.e]+), g: ([-\d.e]+), b: ([-\d.e]+), a: ([-\d.e]+)\}");
        float F(int i) => float.Parse(m.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture);
        return new Color(F(1), F(2), F(3), F(4));
    }

    [Fact]
    public void ResolvesThePaletteTheContainerNames()
    {
        string guid = ContainerGuid();
        Assert.Matches("^[0-9a-f]{32}$", guid);

        string path = SkimRaceTheme.ResolveColorSetAssetPath(Root, out string resolvedGuid);

        Assert.Equal(guid, resolvedGuid);
        Assert.Equal(Path.GetFullPath(PaletteWithGuid(guid)), Path.GetFullPath(path));
    }

    [Fact]
    public void ColorSetCarriesTheLivePalettesAuthoredValues()
    {
        string path = PaletteWithGuid(ContainerGuid());
        var set = SkimRaceTheme.CreateColorSet(Root);

        Assert.Equal(Path.GetFileNameWithoutExtension(path), set.name);
        foreach (var (block, colors) in new[] { ("JadeColors", set.JadeColors), ("RubyColors", set.RubyColors),
                     ("GoldColors", set.GoldColors), ("BlueColors", set.BlueColors) })
        {
            Assert.Equal(AuthoredColor(path, block, "InsideBlockColor"), colors.InsideBlockColor);
            Assert.Equal(AuthoredColor(path, block, "OutsideBlockColor"), colors.OutsideBlockColor);
            Assert.Equal(AuthoredColor(path, block, "ShieldedOutsideBlockColor"), colors.ShieldedOutsideBlockColor);
            Assert.Equal(AuthoredColor(path, block, "TrailHighlightColor"), colors.TrailHighlightColor);
        }
        Assert.Equal(AuthoredColor(path, "EnvironmentColors", "Danger"), set.EnvironmentColors.Danger);
    }

    [Fact]
    public void ContainerNamingAnUnknownGuid_FailsLoudlyAndNamesTheGuid()
    {
        // Negative control: a project whose container names a palette nobody carries.
        string temp = Path.Combine(Path.GetTempPath(), "cs-palette-" + Guid.NewGuid().ToString("N"));
        try
        {
            string so = Path.Combine(temp, "Assets", "_SO_Assets");
            Directory.CreateDirectory(Path.Combine(so, "Color Palettes"));
            const string bogus = "0123456789abcdef0123456789abcdef";
            File.WriteAllText(Path.Combine(so, "ThemeManagerDataContainer.asset"),
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_Name: ThemeManagerDataContainer\n" +
                $"  ColorSet: {{fileID: 11400000, guid: {bogus}, type: 2}}\n");

            var ex = Assert.Throws<InvalidDataException>(() => SkimRaceTheme.CreateColorSet(temp));
            Assert.Contains(bogus, ex.Message);
        }
        finally
        {
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }
}
