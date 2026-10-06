using System.IO;
using System.Text;
using CosmicShore.Content;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

/// <summary>
/// TextAsset: the file's bytes unchanged, text decoded like Unity (BOM-aware, UTF-8 by default),
/// and Content loads one wherever a field references a .json/.txt/.bytes... file - the swarm
/// fauna's body plans are TextAssets.
/// </summary>
public class TextAssetTests
{
    [Fact]
    public void Text_HonoursByteOrderMarks_AndBytesStayExact()
    {
        var utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i' };
        var asset = new TextAsset(utf8Bom);
        Assert.Equal("hi", asset.text);
        Assert.Equal(utf8Bom, asset.bytes);
        Assert.Equal(5, asset.dataSize);

        var utf16 = Encoding.Unicode.GetPreamble();
        var utf16Text = new TextAsset(Concat(utf16, Encoding.Unicode.GetBytes("é")));
        Assert.Equal("é", utf16Text.text);
        Assert.Equal("made in code", new TextAsset("made in code").text);
    }

    [Fact]
    public void TheExtensionsUnityImportsAsText()
    {
        foreach (var ext in new[] { ".txt", ".JSON", ".bytes", ".csv", ".xml", ".yaml", ".html", ".htm", ".fnt" })
            Assert.True(TextAsset.IsTextAssetExtension(ext), ext);
        foreach (var ext in new[] { ".cs", ".asset", ".png", ".md", "" })
            Assert.False(TextAsset.IsTextAssetExtension(ext), ext);
    }

    [Fact]
    public void AFieldReferencingAJsonFile_LoadsItAsATextAsset()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        using var loop = new GameLoop("TextAssetTests");
        var runtime = new ContentRuntime(ContentYamlTests.ProjectRoot, new[] { typeof(GameObject).Assembly });
        // SwarmFaunaConfig.asset: ChargePlan: {fileID: 4900000, guid: 07dfea02..., type: 3}
        var plan = runtime.Assets.Load<TextAsset>(new ObjRef(4900000, "07dfea02463d653b53c1d4c2e33d628c", 3));
        Assert.NotNull(plan);
        Assert.Equal("SwarmPlan_charge", plan.name);
        Assert.StartsWith("{", plan.text.TrimStart());
        Assert.Equal(new FileInfo(Path.Combine(ContentYamlTests.ProjectRoot, "Assets/_SO_Assets/Swarm Fauna/Plans/SwarmPlan_charge.json")).Length, plan.dataSize);
    }

    static byte[] Concat(byte[] a, byte[] b) { var r = new byte[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }
}
