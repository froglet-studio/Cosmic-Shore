using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Rung-6 / ecosystem groundwork: LifeForm + HealthPrism + Spindle +
// HealthBlockTracker + SpindleTracker + ILifeFormEntity (+ ITeamAssignable) ported
// verbatim. This closes the SA1 deviation in LifeFormsKilledScoring (the static
// LifeForm.OnLifeFormDeath subscription is restored). Conserved-mass note: health
// prisms are mass — nothing here decays them; death happens only through the active
// Damage path (vessel abilities / fauna), exactly as Docs/ECOSYSTEM.md requires.
public class LifeFormFamilyTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public LifeFormFamilyTests() => NetworkManager.Singleton = null;

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    // ── rig helpers ─────────────────────────────────────────────────────────

    static readonly BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

    static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, Priv | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    /// <summary>HealthPrism with the full prism wiring (mirror of PrismTestRig.Create).</summary>
    HealthPrism CreateHealthPrism(string name = "health-prism", Transform parent = null)
    {
        var theme = PrismTestRig.CreateTheme();
        var go = new GameObject(name);
        spawned.Add(go);
        go.SetActive(false);
        if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);

        go.AddComponent<MeshRenderer>();
        go.AddComponent<BoxCollider>();

        var materialAnimator = go.AddComponent<MaterialPropertyAnimator>();
        Set(materialAnimator, "_themeManagerData", theme);

        var scaleAnimator = go.AddComponent<PrismScaleAnimator>();
        Set(scaleAnimator, "onPrismVolumeModified", ScriptableObject.CreateInstance<ScriptableEventPrismStats>());

        var teamManager = go.AddComponent<PrismTeamManager>();
        Set(teamManager, "_themeManagerData", theme);
        Set(teamManager, "onPrismStolen", ScriptableObject.CreateInstance<ScriptableEventPrismStats>());

        var stateManager = go.AddComponent<PrismStateManager>();
        Set(stateManager, "_themeManagerData", theme);

        var prism = go.AddComponent<HealthPrism>();
        prism.prismProperties = new PrismProperties();
        Set(prism, "_onTrailBlockCreatedEventChannel", ScriptableObject.CreateInstance<ScriptableEventPrismStats>());
        Set(prism, "_onTrailBlockDestroyedEventChannel", ScriptableObject.CreateInstance<ScriptableEventPrismStats>());
        Set(prism, "_onTrailBlockRestoredEventChannel", ScriptableObject.CreateInstance<ScriptableEventPrismStats>());
        Set(prism, "OnBlockImpactedEventChannel", ScriptableObject.CreateInstance<PrismEventChannelWithReturnSO>());

        go.SetActive(true);
        return prism;
    }

    class TestLifeForm : LifeForm { }

    /// <summary>Inactive LifeForm GO with autoInitialize off — caller adds parts then activates.</summary>
    TestLifeForm CreateLifeForm(Domains domain = Domains.Jade)
    {
        var go = new GameObject("lifeform");
        spawned.Add(go);
        go.SetActive(false);
        var lifeForm = go.AddComponent<TestLifeForm>();
        Set(lifeForm, "autoInitialize", false);
        lifeForm.domain = domain;
        return lifeForm;
    }

    // ── HealthBlockTracker ──────────────────────────────────────────────────

    [Fact]
    public void Tracker_ReachesMaturityAtThreshold_AndStaysMature()
    {
        var tracker = new HealthBlockTracker(healthBlocksForMaturity: 2, minHealthBlocks: 0);

        tracker.Add(CreateHealthPrism("hp1"), null, Domains.Jade);
        Assert.False(tracker.IsMature);

        var hp2 = CreateHealthPrism("hp2");
        tracker.Add(hp2, null, Domains.Jade);
        Assert.True(tracker.IsMature);

        tracker.Remove(hp2);
        Assert.True(tracker.IsMature); // maturity is one-way
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void Tracker_IsLethalWhenCountFallsToMinimum()
    {
        var tracker = new HealthBlockTracker(healthBlocksForMaturity: 1, minHealthBlocks: 0);
        var hp = CreateHealthPrism();
        tracker.Add(hp, null, Domains.Ruby);
        Assert.False(tracker.IsLethal());

        tracker.Remove(hp);
        Assert.True(tracker.IsLethal());
    }

    [Fact]
    public void Tracker_AddSetsDomainOnPrism_DuplicateAddsCountOnce()
    {
        var tracker = new HealthBlockTracker(healthBlocksForMaturity: 5, minHealthBlocks: 0);
        var hp = CreateHealthPrism();

        tracker.Add(hp, null, Domains.Gold);
        tracker.Add(hp, null, Domains.Gold);

        Assert.Equal(1, tracker.Count);
        Assert.Equal(Domains.Gold, hp.Domain);
    }

    // ── LifeForm ────────────────────────────────────────────────────────────

    [Fact]
    public void Initialize_BindsEmbeddedHealthPrisms_WithLifeFormDomain()
    {
        var lifeForm = CreateLifeForm(Domains.Ruby);
        var hp = CreateHealthPrism("embedded", lifeForm.transform);

        lifeForm.gameObject.SetActive(true);
        lifeForm.Initialize(null);

        Assert.Same(lifeForm, hp.LifeForm);
        Assert.Equal(Domains.Ruby, hp.Domain);
    }

    [Fact]
    public void RemovingLastHealthBlock_FiresDeathEventWithKiller_AndDestroysLifeForm()
    {
        var lifeForm = CreateLifeForm();
        var hp = CreateHealthPrism("embedded", lifeForm.transform);
        lifeForm.gameObject.SetActive(true);
        lifeForm.Initialize(null);

        string killer = null;
        int cellId = -99;
        Action<string, int> handler = (name, id) => { killer = name; cellId = id; };
        LifeForm.OnLifeFormDeath += handler;
        try
        {
            lifeForm.RemoveHealthBlock(hp, "Slayer");

            Assert.Equal("Slayer", killer);
            Assert.Equal(-1, cellId); // no cell bound

            loop.Tick(0.02f); // DieCoroutine: spindles empty → Destroy(gameObject)
            loop.Tick(0.02f);
            Assert.False((bool)lifeForm.gameObject);
        }
        finally { LifeForm.OnLifeFormDeath -= handler; }
    }

    [Fact]
    public void SetTeam_PropagatesDomainToAllHealthPrisms()
    {
        var lifeForm = CreateLifeForm(Domains.Jade);
        var hp = CreateHealthPrism("embedded", lifeForm.transform);
        lifeForm.gameObject.SetActive(true);
        lifeForm.Initialize(null);

        lifeForm.SetTeam(Domains.Gold);

        Assert.Equal(Domains.Gold, lifeForm.Domain);
        Assert.Equal(Domains.Gold, hp.Domain);
    }
}
