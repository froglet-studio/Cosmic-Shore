using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Shaders;
using CosmicShore.Engine;
using CosmicShore.Render;

namespace CosmicShore.Tests;

// Graphics parity with Unity's URP in the project's linear colour space: how a colour reaches
// the GPU, which property Material.color addresses, where a material's queue comes from, the
// bloom pyramid's depth and how the built-in shaders are identified. Each case is the original
// engine's documented behaviour; a wrong answer here renders a whole family of objects too dark,
// too bright or opaque.
public class RenderParityTests : IDisposable
{
    readonly Func<string, IReadOnlyList<KeyValuePair<string, object>>> _props = Shader.PropertyCatalog;
    readonly Func<string, IReadOnlyCollection<string>> _hdr = Shader.HdrColorCatalog;
    readonly Func<string, ShaderSourceInfo> _info = Shader.SourceInfoCatalog;

    public RenderParityTests()
    {
        // The built-in table needs no project: the package and editor shaders the tests use.
        var catalog = new ShaderPropertyCatalog(null);
        Shader.PropertyCatalog = catalog.For;
        Shader.HdrColorCatalog = catalog.HdrColors;
        Shader.SourceInfoCatalog = catalog.SourceInfo;
    }

    public void Dispose()
    {
        Shader.PropertyCatalog = _props;
        Shader.HdrColorCatalog = _hdr;
        Shader.SourceInfoCatalog = _info;
    }

    [Fact]
    public void GammaToLinear_FollowsTheSrgbCurve_AndPow22AboveOne()
    {
        Assert.Equal(0.21404114f, Mathf.GammaToLinearSpace(0.5f), 5);
        Assert.Equal(0.03f / 12.92f, Mathf.GammaToLinearSpace(0.03f), 6);   // the linear toe
        Assert.Equal(MathF.Pow(4f, 2.2f), Mathf.GammaToLinearSpace(4f), 3);   // the jet flares' _Color = 4
        foreach (var x in new[] { 0.02f, 0.2f, 0.5f, 0.9f, 2f, 4f })
            Assert.Equal(x, Mathf.LinearToGammaSpace(Mathf.GammaToLinearSpace(x)), 4);
    }

    [Fact]
    public void PlainColour_ReachesTheGpuLinearised_HdrColourAsStored()
    {
        var particles = new Material(Shader.Find("Particles/Standard Unlit"));
        particles.SetColor("_Color", new Color(4f, 4f, 4f, 1f));
        Assert.Equal(MathF.Pow(4f, 2.2f), particles.GetShaderColor(Shader.PropertyToID("_Color")).r, 2);
        Assert.Equal(4f, particles.GetColor("_Color").r, 4);   // GetColor gives back what was set

        // URP Lit's _EmissionColor is [HDR]: SetColor converts its sRGB argument, the GPU takes the stored value.
        var lit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        lit.SetColor("_EmissionColor", new Color(0.5f, 0.5f, 0.5f, 1f));
        Assert.Equal(0.21404114f, lit.GetShaderColor(Shader.PropertyToID("_EmissionColor")).r, 4);
        Assert.Equal(0.5f, lit.GetColor("_EmissionColor").r, 4);
        // SetVector is raw on either kind.
        lit.SetVector("_EmissionColor", new Vector4(0.5f, 0.5f, 0.5f, 1f));
        Assert.Equal(0.5f, lit.GetShaderColor(Shader.PropertyToID("_EmissionColor")).r, 4);
    }

    [Fact]
    public void PropertyBlock_SetColorLinearises_SetVectorDoesNot()
    {
        var block = new MaterialPropertyBlock();
        int id = Shader.PropertyToID("_BaseColor");
        block.SetColor(id, new Color(0.5f, 0.5f, 0.5f, 0.25f));
        Assert.True(block.TryGetShaderVector(id, out var v));
        Assert.Equal(0.21404114f, v.x, 4);
        Assert.Equal(0.25f, v.w, 4);
        // A vector slot goes up raw.
        int raw = Shader.PropertyToID("_RenderParityRawVector");
        block.SetVector(raw, new Vector4(0.5f, 0.5f, 0.5f, 0.25f));
        Assert.True(block.TryGetShaderVector(raw, out v));
        Assert.Equal(0.5f, v.x, 4);
        // One slot per name: SetVector on the slot SetColor made keeps treating it as a colour.
        block.SetVector(id, new Vector4(0.5f, 0.5f, 0.5f, 0.25f));
        Assert.True(block.TryGetShaderVector(id, out v));
        Assert.Equal(0.21404114f, v.x, 4);
    }

    [Fact]
    public void MaterialColor_AddressesTheShadersMainColour()
    {
        // URP's shaders tag _BaseColor [MainColor] and _BaseMap [MainTexture].
        var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.2f, 0.4f, 0.6f, 1f) };
        Assert.Equal(new Color(0.2f, 0.4f, 0.6f, 1f), unlit.GetColor("_BaseColor"));
        Assert.Equal(new Color(0.2f, 0.4f, 0.6f, 1f), unlit.color);
        var tex = new Texture2D(1, 1);
        unlit.mainTexture = tex;
        Assert.Same(tex, unlit.GetTexture("_BaseMap"));

        // Without a [MainColor], _Color.
        var sprite = new Material(Shader.Find("Sprites/Default")) { color = Color.red };
        Assert.Equal(Color.red, sprite.GetColor("_Color"));
    }

    [Fact]
    public void RenderQueue_IsTheShadersUntilTheMaterialSetsOne()
    {
        var sprite = new Material(Shader.Find("Sprites/Default"));
        Assert.Equal(3000, sprite.renderQueue);          // Tags { "Queue"="Transparent" }
        Assert.False(sprite.HasCustomRenderQueue);
        sprite.renderQueue = 2450;
        Assert.Equal(2450, sprite.renderQueue);
        Assert.True(sprite.HasCustomRenderQueue);
        sprite.renderQueue = -1;                          // back to the shader's
        Assert.Equal(3000, sprite.renderQueue);
        Assert.Equal(3000, new Material(sprite).renderQueue);

        Assert.Equal(2000, new Material(Shader.Find("Universal Render Pipeline/Lit")).renderQueue);
        Assert.Equal(2000, new Material(Shader.Find("RenderParityTests/Unknown")).renderQueue);
    }

    [Fact]
    public void HandWrittenShader_MainColourAndQueueTag()
    {
        const string src = @"Shader ""Test/Q"" {
    Properties
    {
        [MainColor] _Tint (""Tint"", Color) = (1,1,1,1)
        [MainTexture] _Sheet (""Sheet"", 2D) = ""white"" {}
    }
    SubShader { Tags { ""RenderType""=""Transparent"" ""Queue"" = ""Transparent+2"" } Pass { } }
}";
        var info = new ShaderSourceInfo();
        ShaderPropertyCatalog.ParseShader(src, new List<string>(), info);
        Assert.Equal("_Tint", info.MainColor);
        Assert.Equal("_Sheet", info.MainTexture);
        Assert.Equal(3002, info.Queue);
        Assert.Equal(2449, ShaderPropertyCatalog.QueueValue("AlphaTest", "-1"));
        Assert.Equal(1000, ShaderPropertyCatalog.QueueValue("Background", ""));
    }

    [Theory]
    [InlineData(1920, 1080, 16, false, 8)]   // half: 960 x 540, floor(log2 960) - 1
    [InlineData(1920, 1080, 6, false, 6)]    // capped by Max Iterations
    [InlineData(1920, 1080, 16, true, 7)]    // quarter: 480 x 270
    [InlineData(64, 64, 16, false, 4)]
    [InlineData(4, 4, 16, false, 1)]         // never below one level
    public void BloomPyramid_DepthFollowsUrp(int w, int h, int maxIterations, bool quarter, int expected)
        => Assert.Equal(expected, PostSettings.BloomMipCount(w, h, maxIterations, quarter));

    [Fact]
    public void BuiltinShaders_AreIdentifiedByName()
    {
        Assert.Equal("Particles/Standard Unlit", MaterialImporter.ShaderNameFor(new ObjRef(211, AssetLoader.BuiltinExtraGuid, 0), null));
        Assert.Equal(SceneRenderer.BuiltinShader.StandardParticlesUnlit, SceneRenderer.BuiltinFor("Particles/Standard Unlit"));
        Assert.Equal(SceneRenderer.BuiltinShader.LegacyPremultiply, SceneRenderer.BuiltinFor(BuiltinMaterials.DefaultLine.shader.name));
        Assert.Equal(SceneRenderer.BuiltinShader.SpritesDefault, SceneRenderer.BuiltinFor("Sprites/Default"));
        Assert.Equal(SceneRenderer.BuiltinShader.UrpParticlesUnlit, SceneRenderer.BuiltinFor("Universal Render Pipeline/Particles/Unlit"));
        Assert.Equal(SceneRenderer.BuiltinShader.None, SceneRenderer.BuiltinFor("Shader Graphs/BlockGraph"));
    }

    // The vessel jet's flares (VesselJet.prefab > vfx_Projectile_02): the built-in
    // Particles/Standard Unlit at _Color 4 in Fade mode. URP 17.3 draws its untagged pass, so the
    // flare is the texture x 4^2.2 x the particle colour, alpha blended.
    [Fact]
    public void JetFlareMaterial_LoadsAsUnityHasIt()
    {
        if (ContentYamlTests.ProjectRoot == null) return;
        using var loop = new GameLoop("RenderParityTests");
        var runtime = new ContentRuntime(ContentYamlTests.ProjectRoot, new[] { typeof(GameObject).Assembly });
        var guid = runtime.Db.GuidOf("Assets/Effects Library/Froglet Stuff/Materials/Flare00_AB_2.mat");
        var mat = runtime.Assets.Load<Material>(new ObjRef(2100000, guid, 2));
        Assert.NotNull(mat);
        Assert.Equal("Particles/Standard Unlit", mat.shader.name);
        Assert.Equal(3000, mat.renderQueue);
        Assert.True(mat.IsKeywordEnabled("_ALPHABLEND_ON"));
        Assert.NotNull(mat.GetTexture("_MainTex"));
        Assert.Null(mat.GetTexture("_BaseMap"));          // the stale URP slot the flare must not read
        Assert.Equal(MathF.Pow(4f, 2.2f), mat.GetShaderColor(Shader.PropertyToID("_Color")).r, 2);
    }
}
