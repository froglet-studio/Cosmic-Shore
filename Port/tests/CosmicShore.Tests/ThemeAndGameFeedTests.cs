using System;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Rung-5 groundwork: ThemeManager ported verbatim (the one writer of
// ThemeManagerDataContainerSO.TeamMaterialSets and of GameFeedAPI.ColorSet — the
// single domain-color source every surface shares). GameFeedAPI + payload + SOAP
// channel ported with it. Engine Resources gained the path-keyed Register/Load the
// original Resources.Load contract needs.
//
// GameFeedAPI.ColorSet and the Resources path registry are process-global statics, so
// every test that touches them resets in Dispose (parallelization is disabled
// assembly-wide, so no cross-test races — just ordering hygiene).
public class ThemeAndGameFeedTests : IDisposable
{
    readonly GameLoop loop = new();

    public ThemeAndGameFeedTests()
    {
        GameFeedAPI.ColorSet = null;
        Resources.Clear();
    }

    public void Dispose()
    {
        GameFeedAPI.ColorSet = null;
        Resources.Clear();
        loop.Dispose();
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static void SetField(object target, string name, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var field = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{name}' not found on {target.GetType().Name}.");
    }

    static SO_MaterialSet MakeBaseMaterialSet()
    {
        var set = ScriptableObject.CreateInstance<SO_MaterialSet>();
        set.ShipMaterial = new Material(Shader.Find("Standard"));
        set.BlockMaterial = new Material(Shader.Find("Standard"));
        set.TransparentBlockMaterial = new Material(Shader.Find("Standard"));
        set.CrystalMaterial = new Material(Shader.Find("Standard"));
        set.CrystalMaterial1 = new Material(Shader.Find("Standard"));
        set.CrystalMaterial2 = new Material(Shader.Find("Standard"));
        set.CrystalMaterial3 = new Material(Shader.Find("Standard"));
        set.ExplodingBlockMaterial = new Material(Shader.Find("Standard"));
        set.ShieldedBlockMaterial = new Material(Shader.Find("Standard"));
        set.TransparentShieldedBlockMaterial = new Material(Shader.Find("Standard"));
        set.SuperShieldedBlockMaterial = new Material(Shader.Find("Standard"));
        set.TransparentSuperShieldedBlockMaterial = new Material(Shader.Find("Standard"));
        set.DangerousBlockMaterial = new Material(Shader.Find("Standard"));
        set.TransparentDangerousBlockMaterial = new Material(Shader.Find("Standard"));
        set.AOEExplosionMaterial = new Material(Shader.Find("Standard"));
        set.AOEConicExplosionMaterial = new Material(Shader.Find("Standard"));
        set.SpikeMaterial = new Material(Shader.Find("Standard"));
        set.SkimmerMaterial = new Material(Shader.Find("Standard"));
        return set;
    }

    static SO_ColorSet MakeColorSet()
    {
        var colors = ScriptableObject.CreateInstance<SO_ColorSet>();
        colors.JadeColors = MakeDomainColors(new Color(0f, 1f, 0f));
        colors.RubyColors = MakeDomainColors(new Color(1f, 0f, 0f));
        colors.GoldColors = MakeDomainColors(new Color(1f, 0.8f, 0f));
        colors.BlueColors = MakeDomainColors(new Color(0f, 0.4f, 1f));
        colors.EnvironmentColors = new EnvironmentColorSet { Danger = new Color(1f, 0f, 1f) };
        return colors;
    }

    static DomainColorSet MakeDomainColors(Color basis) => new()
    {
        InsideBlockColor = basis,
        OutsideBlockColor = basis * 0.5f,
        BrightCrystalColor = basis,
        DullCrystalColor = basis * 0.25f,
        ShieldedInsideBlockColor = basis,
        ShieldedOutsideBlockColor = basis * 0.5f,
        SuperShieldedInsideBlockColor = basis,
        SuperShieldedOutsideBlockColor = basis * 0.5f,
        ShipColor1 = basis,
        ShipColor2 = basis * 0.75f,
        AOETextureColor = basis,
        AOEFresnelColor = basis,
        AOEConicColor = basis,
        AOEConicEdgeColor = basis,
        SpikeLightColor = basis,
        SpikeDarkColor = basis * 0.5f,
        SkimmerColor = basis,
        TrailHighlightColor = basis,
    };

    static ThemeManagerDataContainerSO MakeContainer(out SO_ColorSet colors)
    {
        var container = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
        container.BaseMaterialSet = MakeBaseMaterialSet();
        container.ColorSet = colors = MakeColorSet();
        return container;
    }

    GameObject SpawnThemeManager(ThemeManagerDataContainerSO container)
    {
        var go = new GameObject("theme-manager");
        go.SetActive(false);
        var manager = go.AddComponent<ThemeManager>();
        SetField(manager, "_dataContainer", container);
        go.SetActive(true); // Awake generates the per-domain material sets
        return go;
    }

    // ── ThemeManager ────────────────────────────────────────────────────────

    [Fact]
    public void Awake_GeneratesMaterialSetsForAllFourDomains()
    {
        var container = MakeContainer(out _);
        SpawnThemeManager(container);

        Assert.NotNull(container.TeamMaterialSets);
        Assert.Equal(4, container.TeamMaterialSets.Count);
        foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue })
            Assert.True(container.TeamMaterialSets.ContainsKey(domain), $"missing {domain}");
    }

    [Fact]
    public void Awake_DomainMaterialsAreIndependentCopiesWithDomainColors()
    {
        var container = MakeContainer(out var colors);
        SpawnThemeManager(container);

        var jadeBlock = container.TeamMaterialSets[Domains.Jade].BlockMaterial;
        var rubyBlock = container.TeamMaterialSets[Domains.Ruby].BlockMaterial;

        Assert.NotSame(container.BaseMaterialSet.BlockMaterial, jadeBlock); // copied, not shared
        Assert.NotSame(jadeBlock, rubyBlock);
        Assert.Equal(colors.JadeColors.InsideBlockColor, jadeBlock.GetColor("_BrightColor"));
        Assert.Equal(colors.JadeColors.OutsideBlockColor, jadeBlock.GetColor("_DarkColor"));
        Assert.Equal(colors.RubyColors.InsideBlockColor, rubyBlock.GetColor("_BrightColor"));

        // Dangerous blocks blend the shared environment danger color with the domain dark.
        var jadeDanger = container.TeamMaterialSets[Domains.Jade].DangerousBlockMaterial;
        Assert.Equal(colors.EnvironmentColors.Danger, jadeDanger.GetColor("_BrightColor"));

        var goldShip = container.TeamMaterialSets[Domains.Gold].ShipMaterial;
        Assert.Equal(colors.GoldColors.ShipColor1, goldShip.GetColor("_Color1"));
        Assert.Equal(colors.GoldColors.ShipColor2, goldShip.GetColor("_Color2"));
    }

    [Fact]
    public void Awake_HandsColorSetToGameFeedAPI()
    {
        var container = MakeContainer(out var colors);
        SpawnThemeManager(container);

        // GameFeedAPI.ColorSet has a private getter; observe through GetDomainColor.
        Assert.Equal(colors.GetDomainUIColor(Domains.Jade), GameFeedAPI.GetDomainColor(Domains.Jade));
        Assert.Equal(colors.GetDomainUIColor(Domains.Ruby), GameFeedAPI.GetDomainColor(Domains.Ruby));
    }

    [Fact]
    public void Container_GetTeamCrystalMaterial_SelectsByIndex()
    {
        var container = MakeContainer(out _);
        SpawnThemeManager(container);

        var set = container.TeamMaterialSets[Domains.Ruby];
        Assert.Same(set.CrystalMaterial, container.GetTeamCrystalMaterial(Domains.Ruby, 0));
        Assert.Same(set.CrystalMaterial1, container.GetTeamCrystalMaterial(Domains.Ruby, 1));
        Assert.Same(set.CrystalMaterial2, container.GetTeamCrystalMaterial(Domains.Ruby, 2));
        Assert.Same(set.CrystalMaterial3, container.GetTeamCrystalMaterial(Domains.Ruby, 3));
        Assert.Same(set.CrystalMaterial, container.GetTeamCrystalMaterial(Domains.Ruby, 99)); // fallback
    }

    // ── GameFeedAPI ─────────────────────────────────────────────────────────

    [Fact]
    public void GetDomainColor_FallsBackToWhiteWithoutColorSet()
    {
        Assert.Equal(Color.white, GameFeedAPI.GetDomainColor(Domains.Jade));
    }

    [Fact]
    public void Post_RaisesRegisteredChannelWithPayload()
    {
        var channel = ScriptableObject.CreateInstance<ScriptableEventGameFeedPayload>();
        Resources.Register("Channels/GameFeedChannel", channel);
        ResetChannelCache();

        GameFeedPayload? received = null;
        channel.OnRaised += p => received = p;

        GameFeedAPI.Post("hello feed", Domains.Gold, GameFeedType.PlayerJoined);

        Assert.NotNull(received);
        Assert.Equal("hello feed", received.Value.Message);
        Assert.Equal(Domains.Gold, received.Value.Domain);
        Assert.Equal(GameFeedType.PlayerJoined, received.Value.Type);
    }

    [Fact]
    public void PostJoust_ColorsNamesWithDomainColors()
    {
        var channel = ScriptableObject.CreateInstance<ScriptableEventGameFeedPayload>();
        Resources.Register("Channels/GameFeedChannel", channel);
        ResetChannelCache();

        var container = MakeContainer(out var colors);
        SpawnThemeManager(container); // assigns GameFeedAPI.ColorSet

        GameFeedPayload? received = null;
        channel.OnRaised += p => received = p;

        GameFeedAPI.PostJoust("Attacker", Domains.Jade, "Target", Domains.Ruby);

        Assert.NotNull(received);
        var atkHex = ColorUtility.ToHtmlStringRGB(colors.GetDomainUIColor(Domains.Jade));
        var defHex = ColorUtility.ToHtmlStringRGB(colors.GetDomainUIColor(Domains.Ruby));
        Assert.Contains($"<color=#{atkHex}><b>Attacker</b></color>", received.Value.Message);
        Assert.Contains($"<color=#{defHex}><b>Target</b></color>", received.Value.Message);
        Assert.Equal(GameFeedType.JoustHit, received.Value.Type);
    }

    // ── Engine Resources path registry ──────────────────────────────────────

    [Fact]
    public void ResourcesLoad_ReturnsNullForUnregisteredPathOrWrongType()
    {
        Assert.Null(Resources.Load<ScriptableEventGameFeedPayload>("nope/missing"));

        Resources.Register("some/path", ScriptableObject.CreateInstance<SO_MaterialSet>());
        Assert.Null(Resources.Load<ScriptableEventGameFeedPayload>("some/path"));
        Assert.NotNull(Resources.Load<SO_MaterialSet>("some/path"));
    }

    /// <summary>GameFeedAPI caches its channel in a static; reset between tests.</summary>
    static void ResetChannelCache()
        => typeof(GameFeedAPI)
            .GetField("_channel", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, null);
}
