using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Shaders;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

// Material.HasProperty answers from the SHADER's declarations (original contract), not
// from the values a .mat happens to have saved. The spindle materials predate the graph's
// _DeathStartTime, so an answer from saved values alone made every spindle fade report
// "material does not declare _DeathStartTime" in strict mode.
public class ShaderPropertyCatalogTests
{
    [Fact]
    public void GraphBlackboard_YieldsReferenceNamesAndDefaults()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        var text = File.ReadAllText(Path.Combine(ContentYamlTests.ProjectRoot, "Assets/_Graphics/Materials/Graphs/SpindleGraph.shadergraph"));
        var props = ShaderPropertyCatalog.ParseGraph(text);
        var names = props.Select(p => p.Key).ToList();
        Assert.Contains("_DeathStartTime", names);
        Assert.Contains("_SwayAmplitude", names);
        Assert.IsType<float>(props.First(p => p.Key == "_DeathStartTime").Value);
    }

    [Fact]
    public void HandWrittenShader_PropertiesBlock_ParsesTypesAndDefaults()
    {
        const string src = @"Shader ""Test/X"" {
    Properties
    {
        [Header(Arc)]
        _ColA (""Core"", Color) = (1.5, 0.25, 0, 1.0) // hot
        _Span (""Span"", Range(0.2, 6.283)) = 1.45
        [HDR] _Tint (""Tint"", Vector) = (1,2,3,4)
        _MainTex (""Tex"", 2D) = ""white"" {}
    }
    SubShader { Pass { } }
}";
        var props = ShaderPropertyCatalog.ParseShader(src);
        Assert.Equal(new[] { "_ColA", "_Span", "_Tint", "_MainTex" }, props.Select(p => p.Key));
        Assert.Equal(new Color(1.5f, 0.25f, 0f, 1f), props[0].Value);
        Assert.Equal(1.45f, (float)props[1].Value, 4);
        Assert.Equal(new Vector4(1, 2, 3, 4), props[2].Value);
        Assert.Null(props[3].Value);
    }

    [Fact]
    public void SpindleMaterial_HasTheGraphsDeathProperty_AndReadsItsDefault()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        using var loop = new GameLoop("ShaderPropertyCatalogTests");
        var runtime = new ContentRuntime(ContentYamlTests.ProjectRoot, new[] { typeof(GameObject).Assembly });
        var db = runtime.Db;
        var guid = db.GuidOf("Assets/_Graphics/Materials/SpindleMaterial.mat");
        var mat = runtime.Assets.Load<Material>(new ObjRef(2100000, guid, 2));
        Assert.NotNull(mat);
        Assert.Equal("Shader Graphs/SpindleGraph", mat.shader.name);
        Assert.NotNull(runtime.ShaderProperties.For(mat.shader.name));
        Assert.False(mat.HasStoredProperty("_DeathStartTime"));   // the .mat never saved it
        Assert.True(mat.HasProperty("_DeathStartTime"));          // ...but its graph declares it
        Assert.True(mat.shader.FindPropertyIndex("_DeathStartTime") >= 0);
        Assert.False(mat.HasProperty("_NotAPropertyAnywhere"));
        Assert.Equal(0.08f, mat.GetFloat("_SwayAmplitude"), 4);  // saved value still wins
    }
}
