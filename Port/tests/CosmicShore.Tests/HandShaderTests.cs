using System.IO;
using System.Linq;
using CosmicShore.Content.Shaders;

namespace CosmicShore.Tests;

// C2: hand translations of the hand-written .shader files. The GLSL is the shader's maths only;
// its render state and uniforms come from the ShaderLab source, so a .shader whose Blend, queue or
// properties change is read afresh, and a translation that loses its asset fails here by name.
public class HandShaderTests
{
    [Fact]
    public void ShaderLab_StateAndProperties_AreReadFromTheSource()
    {
        const string src = """
Shader "Test/State"
{
    Properties
    {
        [Header(Halo)]
        [HDR] _BrightColor ("Bright", Color) = (1,1,1,1)
        _Spread ("Spread", Vector) = (0.01, 0.01, 0.01, 0)
        _Width ("Width", Range(0.001, 0.08)) = 0.005 // a comment
        [Toggle] _On ("On", Float) = 1
        _MainTex ("Texture", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+2" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            // Blend SrcAlpha Zero inside the program is not state
            ENDHLSL
        }
    }
}
""";
        var lab = ShaderLabSource.Parse(src);
        Assert.Equal("Test/State", lab.Name);
        Assert.Equal(new[] { "_BrightColor", "_Spread", "_Width", "_On", "_MainTex" }, lab.Properties.Select(p => p.Name));
        Assert.Equal(new[] { "Color", "Vector", "Range", "Float", "2D" }, lab.Properties.Select(p => p.Type));
        Assert.Equal(("One", "One"), (lab.BlendSrc, lab.BlendDst));
        Assert.Equal("Off", lab.ZWrite);
        Assert.Equal("Back", lab.Cull);
        Assert.Equal(3002, lab.Queue);
        Assert.False(lab.SurfaceLit);
    }

    [Fact]
    public void EveryHandTranslation_HasItsAsset_ItsGlsl_AndAUniformPerProperty()
    {
        var db = ContentYamlTests.Db;
        if (db == null) return;
        foreach (var (guid, (glsl, path)) in HandShaders.ByGuid)
        {
            var full = db.PathOf(guid);
            Assert.True(full != null, $"{glsl}: no asset has guid {guid} (written against {path})");
            Assert.Equal(path, db.ProjectRelative(full));
            Assert.NotNull(HandShaders.Glsl(glsl));
            var prog = HandShaders.For(guid, full, path);
            Assert.True(prog.Ok, $"{glsl}: {prog.Error}");
            foreach (var p in ShaderLabSource.Parse(File.ReadAllText(full)).Properties)
                Assert.Contains(" " + p.Name + ";", prog.Uniforms);
        }
    }

    [Fact]
    public void HandShaders_TakeTheirRenderStateFromTheShader()
    {
        var db = ContentYamlTests.Db;
        if (db == null) return;
        var catalog = new ShaderGraphCatalog(db);
        // OmniShepardFresnel: Blend SrcAlpha OneMinusSrcAlpha, ZWrite Off, Cull Off, Queue Transparent.
        var shepard = catalog.For("b55e562cd5045859607550ce7ba8267f");
        Assert.True(shepard.Transparent);
        Assert.Equal((5, 10), (shepard.BlendSrc, shepard.BlendDst));
        Assert.Equal((0, 0, 3000), (shepard.ZWrite, shepard.Cull, shepard.Queue));
        // The capsule shield adds light: Blend One One.
        var capsule = catalog.For("e66cc995d379448caf38b18d87140dde");
        Assert.Equal((1, 1), (capsule.BlendSrc, capsule.BlendDst));
        // SpreadFresnel is opaque and writes depth.
        var spread = catalog.For("db3a7d241b308d845a0133c22554acad");
        Assert.False(spread.Transparent);
        Assert.Equal(1, spread.ZWrite);
        // JetShader is a Lambert surface shader with alpha:fade and a white default texture.
        var jet = catalog.For("8efe19062b480f445801d12c6fca1ded");
        Assert.True(jet.Lit);
        Assert.True(jet.Transparent);
        Assert.Equal("white", jet.TextureDefaults["_MainTex"]);
    }
}
