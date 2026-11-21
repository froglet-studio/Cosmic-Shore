using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// V12 coverage: Cell ported verbatim. Exercises the pure logic that survives the
// flora/fauna deviations: Add/RemoveBlock per-domain bookkeeping (registration-time
// domain snapshot, NotifyBlockDomainChanged re-registration), the density-grid wiring
// (3 opposing-domain grids + the Domains.Blue wildcard bucket, anchor fallbacks),
// phase advancement through Update + CellPhaseRules hysteresis with the runtime SO as
// the observable source of truth, the ControllingDomain fallback chain, the static
// ActiveCells spatial registry, SenseRadius/ContainsPosition, lifeform + live-fauna
// registries, replay reset, and the restored CellRuntimeDataSO.Cell/CellTransform
// bindings. Conserved-mass note (Docs/ECOSYSTEM.md): nothing here adds decay — block
// counts only ever change via explicit Add/RemoveBlock, and ResetCell is the
// original's replay-reset, not a culler.
//
// Cell keeps a STATIC ActiveCells registry that OnEnable/OnDisable maintain, so the
// loop lives on the test class (xunit: fresh instance per test) and Dispose deactivates
// every cell BEFORE the loop goes away — otherwise stale enabled cells leak into the
// next test's FindCellContaining queries.
public class CellTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> activatedCells = new();

    public void Dispose()
    {
        foreach (var go in activatedCells)
            if (go) go.SetActive(false);
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

    static GameDataSO MakeGameData()
    {
        var data = ScriptableObject.CreateInstance<GameDataSO>();
        data.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
        return data;
    }

    static CellRuntimeDataSO MakeRuntime(GameDataSO gameData = null)
    {
        var data = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
        if (gameData != null) SetField(data, "gameData", gameData);
        data.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnCellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();
        return data;
    }

    /// <summary>
    /// Cell config with a small hysteresis table (Restless 3/2, Frenzy 5/4) and a
    /// 300m sense override → density grids cover a 600m cube: 9 points/axis,
    /// stride 75 — the same shape CellLayerTests' MakeSmallGrid proves out.
    /// </summary>
    static CellConfigDataSO MakeConfig(float senseRadiusOverride = 300f)
    {
        var config = ScriptableObject.CreateInstance<CellConfigDataSO>();
        config.SenseRadiusOverride = senseRadiusOverride;
        config.PhaseThresholds = new CellPhaseThresholds
        {
            RestlessEnter = 3, RestlessExit = 2,
            FrenzyEnter = 5, FrenzyExit = 4,
            // Volume is the spine since the upstream volume rework (c833c580): the phase
            // ladder climbs on LiveVolume; counts are only the Frenzy perf backstop. Rig
            // prisms are unit-scale (volume 1 each), so author explicit volume thresholds
            // matching the counts 1:1 — the same authoring rule modes with low-volume
            // prisms use (else WithDerivedVolumeScale ×16 puts the ladder out of reach).
            RestlessEnterVolume = 3f, RestlessExitVolume = 2f,
            FrenzyEnterVolume = 5f, FrenzyExitVolume = 4f,
        };
        return config;
    }

    /// <summary>
    /// Builds an inactive cell host, reflection-configures the [SerializeField]/[Inject]
    /// fields, then activates (OnEnable subscribes). Initialize() still needs the
    /// gameData.OnInitializeGame raise — tests do that explicitly.
    /// </summary>
    Cell MakeCell(CellRuntimeDataSO runtime, GameDataSO gameData, CellConfigDataSO config, int id = 1)
    {
        var go = new GameObject($"cell-{id}");
        go.SetActive(false);
        var cell = go.AddComponent<Cell>();
        cell.ID = id;
        if (runtime != null) SetField(cell, "runtime", runtime);
        if (gameData != null) SetField(cell, "gameData", gameData);
        if (config != null) SetField(cell, "CellConfigs", new List<CellConfigDataSO> { config });
        go.SetActive(true);
        activatedCells.Add(go);
        return cell;
    }

    /// <summary>One fully-initialized cell: configured, activated, OnInitializeGame raised.</summary>
    Cell MakeInitializedCell(out CellRuntimeDataSO runtime, out GameDataSO gameData, int id = 1)
    {
        gameData = MakeGameData();
        runtime = MakeRuntime(gameData);
        var cell = MakeCell(runtime, gameData, MakeConfig(), id);
        gameData.OnInitializeGame.Raise();
        return cell;
    }

    /// <summary>A full rig prism (V15 shared rig) placed and assigned its initial team.</summary>
    static Prism MakePrism(Domains domain, Vector3 position)
    {
        var prism = PrismTestRig.CreatePrismAt(position);
        prism.GetComponent<PrismTeamManager>().SetInitialTeam(domain);
        return prism;
    }

    static Crystal MakeCrystal(int id, Domains domain, Vector3 position)
    {
        var go = new GameObject($"crystal-{id}");
        go.transform.position = position;
        var crystal = go.AddComponent<Crystal>();
        crystal.Initialize(id);
        crystal.ownDomain = domain;
        return crystal;
    }

    /// <summary>Cross-shaped cluster the density pipeline resolves exactly to its centre.</summary>
    void AddCluster(Cell cell, Domains domain, Vector3 center, int centerCount, int armCount, float stride = 75f)
    {
        for (int i = 0; i < centerCount; i++) cell.AddBlock(MakePrism(domain, center));
        foreach (var axis in new[] { new Vector3(stride, 0, 0), new Vector3(0, stride, 0), new Vector3(0, 0, stride) })
        {
            for (int i = 0; i < armCount; i++) cell.AddBlock(MakePrism(domain, center + axis));
            for (int i = 0; i < armCount; i++) cell.AddBlock(MakePrism(domain, center - axis));
        }
    }

    static void AssertVector3Equal(Vector3 expected, Vector3 actual, float tolerance = 1e-3f)
    {
        Assert.True((expected - actual).magnitude <= tolerance,
            $"Expected {expected} but got {actual} (tolerance {tolerance})");
    }

    // ── Initialize — runtime binding + density grid wiring ──────────────────

    [Fact]
    public void Initialize_BindsRuntimeCell_AssignsConfig_AndBuildsAllFourGrids()
    {
        var cell = MakeInitializedCell(out var runtime, out _, id: 7);

        // Restored V11 deviations: the runtime SO now binds back to the live cell.
        Assert.Same(cell, runtime.Cell);
        Assert.Same(cell.transform, runtime.CellTransform);

        Assert.True(cell.HasConfigAssigned);
        Assert.NotNull(runtime.Config);
        Assert.Same(runtime.Config, cell.Config);
        Assert.True(runtime.CellStatsList.ContainsKey(7));

        // Three opposing-domain grids + the Blue wildcard "any team" bucket.
        Assert.Equal(4, cell.countGrids.Count);
        foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue })
        {
            Assert.True(cell.countGrids.ContainsKey(domain));
            Assert.Equal(domain, cell.countGrids[domain].Domain);
        }
    }

    [Fact]
    public void Initialize_ViaLazyPath_WhenCrystalArrivesBeforeInitializeGame()
    {
        var gameData = MakeGameData();
        var runtime = MakeRuntime(gameData);
        var cell = MakeCell(runtime, gameData, MakeConfig(), id: 2);

        // No OnInitializeGame raise — the crystal registration event triggers
        // InitilizePostFirstCellItem's lazy Initialize fallback instead.
        runtime.AddCrystalToList(MakeCrystal(1, Domains.Blue, Vector3.zero));

        Assert.True(cell.HasConfigAssigned);
        Assert.Same(cell, runtime.Cell);
        Assert.Equal(4, cell.countGrids.Count);
    }

    // ── SenseRadius / ContainsPosition / static registry ────────────────────

    [Fact]
    public void ContainsPosition_UsesSenseRadiusOverride_AndZeroRadiusContainsNothing()
    {
        var cell = MakeInitializedCell(out _, out _);

        Assert.Equal(300f, cell.SenseRadius); // no membrane spawned — override wins
        Assert.True(cell.ContainsPosition(new Vector3(299f, 0f, 0f)));
        Assert.False(cell.ContainsPosition(new Vector3(301f, 0f, 0f)));

        // An unconfigured cell has no membrane and no override → senses nothing.
        var bare = MakeCell(null, null, null, id: 3);
        Assert.Equal(0f, bare.SenseRadius);
        Assert.False(bare.ContainsPosition(Vector3.zero));
    }

    [Fact]
    public void ActiveCellsRegistry_TracksEnabledCells_AndAnswersSpatialQueries()
    {
        var cell = MakeInitializedCell(out _, out _);
        Assert.Contains(cell, Cell.ActiveCellsSnapshot);

        Assert.Same(cell, Cell.FindCellContaining(new Vector3(100f, 0f, 0f)));
        Assert.Null(Cell.FindCellContaining(new Vector3(10_000f, 0f, 0f)));
        Assert.Same(cell, Cell.FindNearestActiveCell(new Vector3(10_000f, 0f, 0f)));

        cell.gameObject.SetActive(false);
        Assert.DoesNotContain(cell, Cell.ActiveCellsSnapshot);
        Assert.Null(Cell.FindCellContaining(new Vector3(100f, 0f, 0f)));
    }

    // ── Add/RemoveBlock — per-domain bookkeeping ────────────────────────────

    [Fact]
    public void AddBlock_CountsPerDomain_AndPopulatesOpposingPlusWildcardGrids()
    {
        var cell = MakeInitializedCell(out _, out _);
        var position = new Vector3(150f, 150f, 150f);
        var prism = MakePrism(Domains.Jade, position);

        cell.AddBlock(prism);
        cell.AddBlock(prism); // duplicate registration is ignored

        Assert.Equal(1, cell.LiveBlockCount);
        Assert.Equal(1, cell.GetDomainBlockCount(Domains.Jade));
        Assert.Equal(Domains.Jade, cell.DominantDomain);

        // A Jade block is opposing mass for Ruby/Gold seekers, invisible to Jade's
        // own grid, and always counted in the Blue wildcard bucket.
        Assert.Equal(0, cell.countGrids[Domains.Jade].GetDensityAtPosition(position));
        Assert.Equal(1, cell.countGrids[Domains.Ruby].GetDensityAtPosition(position));
        Assert.Equal(1, cell.countGrids[Domains.Gold].GetDensityAtPosition(position));
        Assert.Equal(1, cell.countGrids[Domains.Blue].GetDensityAtPosition(position));

        cell.AddBlock(null); // `is null` guard
        Assert.Equal(1, cell.LiveBlockCount);
    }

    [Fact]
    public void RemoveBlock_UsesRegistrationTimeDomain_SoStealsCannotDesync()
    {
        var cell = MakeInitializedCell(out _, out _);
        var position = new Vector3(150f, 150f, 150f);
        var prism = MakePrism(Domains.Jade, position);
        cell.AddBlock(prism);

        // Steal between Add and Remove — the registration-time snapshot must win.
        prism.ChangeTeam(Domains.Ruby);
        cell.RemoveBlock(prism);

        Assert.Equal(0, cell.LiveBlockCount);
        Assert.Equal(0, cell.GetDomainBlockCount(Domains.Jade));
        Assert.Equal(0, cell.GetDomainBlockCount(Domains.Ruby));
        Assert.Equal(Domains.Blue, cell.DominantDomain);
        foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue })
            Assert.Equal(0, cell.countGrids[domain].GetDensityAtPosition(position));

        cell.RemoveBlock(prism); // untracked removal is a no-op
        Assert.Equal(0, cell.LiveBlockCount);
    }

    [Fact]
    public void NotifyBlockDomainChanged_MovesBlockBetweenDomainBuckets()
    {
        var cell = MakeInitializedCell(out _, out _);
        var position = new Vector3(150f, 150f, 150f);
        var prism = MakePrism(Domains.Jade, position);
        cell.AddBlock(prism);

        prism.ChangeTeam(Domains.Ruby);
        cell.NotifyBlockDomainChanged(prism);

        Assert.Equal(1, cell.LiveBlockCount);
        Assert.Equal(0, cell.GetDomainBlockCount(Domains.Jade));
        Assert.Equal(1, cell.GetDomainBlockCount(Domains.Ruby));
        Assert.Equal(Domains.Ruby, cell.DominantDomain);

        // The Ruby block is now opposing mass for Jade/Gold, not Ruby.
        Assert.Equal(1, cell.countGrids[Domains.Jade].GetDensityAtPosition(position));
        Assert.Equal(0, cell.countGrids[Domains.Ruby].GetDensityAtPosition(position));
        Assert.Equal(1, cell.countGrids[Domains.Gold].GetDensityAtPosition(position));
        Assert.Equal(1, cell.countGrids[Domains.Blue].GetDensityAtPosition(position));

        // Untracked block: no-op.
        var stranger = MakePrism(Domains.Gold, position);
        cell.NotifyBlockDomainChanged(stranger);
        Assert.Equal(1, cell.LiveBlockCount);
    }

    [Fact]
    public void DominantDomain_TieBreaksJadeFirst_AndOpposingVolumeExcludesOwnMass()
    {
        // Upstream volume rework (bleeding-edge merge c833c580): "volume is the spine" —
        // OpposingBlockCount (prism-count prey signal) was replaced by OpposingVolume
        // (live env volume not of the domain), and DominantDomain now reads the cadenced
        // per-domain volume sums instead of add/remove-driven counts.
        var cell = MakeInitializedCell(out _, out _);
        Assert.False(cell.FaunaSpawningEnabled); // no mass yet

        var ruby = MakePrism(Domains.Ruby, new Vector3(75f, 0f, 0f));
        var jade = MakePrism(Domains.Jade, new Vector3(0f, 75f, 0f));
        cell.AddBlock(ruby);
        cell.AddBlock(jade);
        loop.Tick(0.3f); // step past the 0.25s live-volume recompute cadence

        Assert.True(ruby.CurrentVolume > 0f); // volume accounting needs live mass
        Assert.Equal(Domains.Jade, cell.DominantDomain); // equal-volume tie → fixed order
        Assert.Equal(ruby.CurrentVolume, cell.OpposingVolume(Domains.Jade), 3);
        Assert.Equal(jade.CurrentVolume, cell.OpposingVolume(Domains.Ruby), 3);
        Assert.Equal(ruby.CurrentVolume + jade.CurrentVolume, cell.OpposingVolume(Domains.Gold), 3);
        Assert.True(cell.FaunaSpawningEnabled);
    }

    // ── Phase machine — Update tick, hysteresis, runtime SO writes ──────────

    [Fact]
    public void Update_AdvancesPhaseThroughThresholds_WithHysteresis()
    {
        var cell = MakeInitializedCell(out var runtime, out _, id: 4);
        var raised = new List<CellPhase>();
        runtime.OnPhaseChanged.OnRaised += phase => raised.Add(phase);

        var prisms = new List<Prism>();
        for (int i = 0; i < 5; i++)
        {
            var prism = MakePrism(Domains.Ruby, new Vector3(75f * i - 150f, 0f, 0f));
            prisms.Add(prism);
            cell.AddBlock(prism);
        }

        // FrenzyEnter=5 — a count spike resolves multi-step (Calm → Frenzy) in one tick.
        loop.Tick(0.6f);
        Assert.Equal(CellPhase.Frenzy, cell.Phase);
        Assert.Equal(CellAggressionLevel.Level2, cell.AggressionLevel);
        Assert.False(cell.FloraGrowingEnabled);
        Assert.False(cell.FloraPlantingEnabled);

        // count 4 == FrenzyExit → inside the hysteresis band, phase holds.
        cell.RemoveBlock(prisms[0]);
        loop.Tick(0.6f);
        Assert.Equal(CellPhase.Frenzy, cell.Phase);

        // count 3 < FrenzyExit(4) but >= RestlessExit(2) → drop exactly one rung.
        cell.RemoveBlock(prisms[1]);
        loop.Tick(0.6f);
        Assert.Equal(CellPhase.Restless, cell.Phase);
        Assert.Equal(CellAggressionLevel.Level1, cell.AggressionLevel);
        Assert.True(cell.FloraGrowingEnabled);

        Assert.Equal(new[] { CellPhase.Frenzy, CellPhase.Restless }, raised);

        // The runtime SO is the observable source of truth for the cell's stats.
        var stats = runtime.CellStatsList[4];
        Assert.Equal(3, stats.LiveBlockCount);
        Assert.Equal(CellPhase.Restless, stats.Phase);
        Assert.Equal(Domains.Ruby, stats.DominantDomain);
    }

    [Fact]
    public void Update_RespectsPhaseTickInterval()
    {
        var cell = MakeInitializedCell(out _, out _);
        var prisms = new List<Prism>();
        for (int i = 0; i < 5; i++)
        {
            var prism = MakePrism(Domains.Ruby, new Vector3(75f * i - 150f, 0f, 0f));
            prisms.Add(prism);
            cell.AddBlock(prism);
        }

        loop.Tick(0.1f); // first tick always computes (next tick scheduled +0.5s)
        Assert.Equal(CellPhase.Frenzy, cell.Phase);

        // Within the 0.5s interval the phase does not recompute even though the
        // count collapsed to zero.
        foreach (var prism in prisms)
            cell.RemoveBlock(prism);
        Assert.Equal(0, cell.LiveBlockCount);
        loop.Tick(0.1f);
        Assert.Equal(CellPhase.Frenzy, cell.Phase);

        // Past the interval it recomputes and falls all the way back to Calm.
        loop.Tick(0.5f);
        Assert.Equal(CellPhase.Calm, cell.Phase);
        Assert.Equal(CellAggressionLevel.Level0, cell.AggressionLevel);
    }

    [Fact]
    public void ApplyAuthoritativePhaseAndDomain_IsTheSolePhaseWriter()
    {
        var cell = MakeInitializedCell(out var runtime, out _, id: 9);
        var raised = new List<CellPhase>();
        runtime.OnPhaseChanged.OnRaised += phase => raised.Add(phase);

        cell.ApplyAuthoritativePhaseAndDomain(CellPhase.Restless, Domains.Gold);

        Assert.Equal(CellPhase.Restless, cell.Phase);
        Assert.Equal(new[] { CellPhase.Restless }, raised);
        var stats = runtime.CellStatsList[9];
        Assert.Equal(CellPhase.Restless, stats.Phase);
        Assert.Equal(Domains.Gold, stats.DominantDomain);
        Assert.Equal(0, stats.LiveBlockCount);

        // Same phase again: stats refresh but no second raise.
        cell.ApplyAuthoritativePhaseAndDomain(CellPhase.Restless, Domains.Jade);
        Assert.Single(raised);
        Assert.Equal(Domains.Jade, runtime.CellStatsList[9].DominantDomain);
    }

    // ── Threshold resolution ────────────────────────────────────────────────

    [Fact]
    public void ResolveThresholds_FallsBackToDefault_WithoutConfig_OrWithZeroedTable()
    {
        // No runtime/config wired at all → Default table.
        var bare = MakeCell(null, null, null, id: 5);
        Assert.False(bare.HasConfigAssigned);
        Assert.Equal(CellPhaseThresholds.Default.FrenzyEnter, bare.FrenzyEnterThreshold);
        Assert.Equal(CellPhaseThresholds.Default.RestlessEnter, bare.ResolvedThresholds.RestlessEnter);

        // Legacy zeroed asset → Default substituted so the cell can't snap to Frenzy.
        var gameData = MakeGameData();
        var runtime = MakeRuntime(gameData);
        var config = MakeConfig();
        config.PhaseThresholds = default;
        var cell = MakeCell(runtime, gameData, config, id: 6);
        gameData.OnInitializeGame.Raise();
        Assert.True(cell.HasConfigAssigned);
        Assert.Equal(CellPhaseThresholds.Default.FrenzyEnter, cell.FrenzyEnterThreshold);
    }

    // ── Density goals — explosion targets + anchors ─────────────────────────

    [Fact]
    public void GetExplosionTarget_ResolvesOpposingCluster_OrFallsBackToAnchor()
    {
        var cell = MakeInitializedCell(out _, out _);
        var center = new Vector3(150f, 150f, 150f);

        // Empty grids → anchor = cell transform (no crystal exists).
        AssertVector3Equal(cell.transform.position, cell.GetExplosionTarget(Domains.Jade));

        AddCluster(cell, Domains.Ruby, center, centerCount: 4, armCount: 1); // 10 Ruby blocks
        loop.Tick(0.3f); // step past the grids' MinRecomputeIntervalSeconds result cache

        // Ruby mass is opposing for a Jade seeker…
        AssertVector3Equal(center, cell.GetExplosionTarget(Domains.Jade));
        // …and invisible to a Ruby seeker, whose grid stays empty → anchor.
        AssertVector3Equal(cell.transform.position, cell.GetExplosionTarget(Domains.Ruby));
        // Unbuilt-domain queries also take the anchor path.
        AssertVector3Equal(cell.transform.position, cell.GetExplosionTarget((Domains)99));
    }

    [Fact]
    public void GetDensestRegionAnyDomain_ReadsTheBlueWildcardBucket()
    {
        var cell = MakeInitializedCell(out _, out _);
        var center = new Vector3(-150f, -150f, -150f);

        AssertVector3Equal(cell.transform.position, cell.GetDensestRegionAnyDomain()); // empty → anchor

        // Friendly + enemy mass both count toward the any-domain centroid.
        AddCluster(cell, Domains.Jade, center, centerCount: 2, armCount: 1);
        AddCluster(cell, Domains.Ruby, center, centerCount: 2, armCount: 0);
        loop.Tick(0.3f); // step past the grids' MinRecomputeIntervalSeconds result cache

        AssertVector3Equal(center, cell.GetDensestRegionAnyDomain());
        AssertVector3Equal(cell.GetDensestRegionAnyDomain(), cell.GetPrimaryCentroid()); // alias
    }

    [Fact]
    public void EmptyGridGoals_PreferTheLocalCrystalAnchor()
    {
        var cell = MakeInitializedCell(out var runtime, out _);
        var crystalPosition = new Vector3(75f, 0f, 0f);
        runtime.AddCrystalToList(MakeCrystal(1, Domains.Blue, crystalPosition));

        AssertVector3Equal(crystalPosition, cell.GetExplosionTarget(Domains.Jade));
        AssertVector3Equal(crystalPosition, cell.GetDensestRegionAnyDomain());
    }

    // ── ControllingDomain fallback chain ────────────────────────────────────

    [Fact]
    public void ControllingDomain_PrefersDominant_ThenVolumeLeader_ThenLocal_ThenJade()
    {
        var cell = MakeInitializedCell(out _, out var gameData);

        // (d→) no blocks, no scores, no local player → Jade last resort.
        Assert.Equal(Domains.Jade, cell.ControllingDomain);

        // (c) local player's domain, when there's no scored controlling team.
        var localStats = new RoundStats { Name = "local", Domain = Domains.Ruby };
        SetField(gameData, "<LocalRoundStats>k__BackingField", localStats);
        Assert.Equal(Domains.Ruby, cell.ControllingDomain);

        // (b) controlling team by remaining volume outranks the local fallback.
        gameData.RoundStatsList.Add(new RoundStats { Name = "leader", Domain = Domains.Gold, VolumeRemaining = 5f });
        Assert.Equal(Domains.Gold, cell.ControllingDomain);

        // (a) live dominant domain outranks everything. DominantDomain is volume-keyed
        // since the upstream volume rework (c833c580) and recomputes on a 0.25s cadence —
        // tick past it so the freshly added mass is visible.
        cell.AddBlock(MakePrism(Domains.Jade, new Vector3(75f, 0f, 0f)));
        loop.Tick(0.3f);
        Assert.Equal(Domains.Jade, cell.ControllingDomain);
    }

    [Fact]
    public void ControllingDomain_WithoutGameData_FallsBackToJade()
    {
        var cell = MakeCell(null, null, null, id: 8); // [Inject] never populated
        Assert.Equal(Domains.Jade, cell.ControllingDomain);
    }

    // ── Volumes ─────────────────────────────────────────────────────────────

    [Fact]
    public void ChangeVolume_AccumulatesPerDomain()
    {
        var cell = MakeInitializedCell(out _, out _);

        Assert.Equal(0f, cell.GetTeamVolume(Domains.Jade)); // seeded by ResetVolumes
        cell.ChangeVolume(Domains.Jade, 2.5f);
        cell.ChangeVolume(Domains.Jade, 1.5f);
        cell.ChangeVolume(Domains.Ruby, -1f);

        Assert.Equal(4f, cell.GetTeamVolume(Domains.Jade));
        Assert.Equal(-1f, cell.GetTeamVolume(Domains.Ruby));
        Assert.Equal(0f, cell.GetTeamVolume(Domains.Gold));
    }

    // ── Lifeform registry ───────────────────────────────────────────────────

    [Fact]
    public void RegisteredLifeForms_ToggleWithSetLifeFormsActive()
    {
        var cell = MakeInitializedCell(out _, out _);
        var flora = new GameObject("flora");
        var fauna = new GameObject("fauna");
        cell.RegisterSpawnedObject(flora);
        cell.RegisterSpawnedObject(fauna);
        cell.RegisterSpawnedObject(null); // guard

        cell.SetLifeFormsActive(false);
        Assert.False(flora.activeSelf);
        Assert.False(fauna.activeSelf);

        cell.UnregisterSpawnedObject(fauna);
        cell.SetLifeFormsActive(true);
        Assert.True(flora.activeSelf);
        Assert.False(fauna.activeSelf); // no longer managed by the cell
    }

    // ── Live fauna registry (real Fauna types — V12 deviation closed) ───────

    class RegistryFauna : Fauna { }

    [Fact]
    public void LiveFaunaRegistry_TracksLineage_AndDestroyedFaunaStayRemovable()
    {
        var cell = MakeInitializedCell(out _, out _);
        var faunaHost = new GameObject("fauna");
        var fauna = faunaHost.AddComponent<RegistryFauna>();
        var species = ScriptableObject.CreateInstance<FaunaConfigurationSO>();

        // AssignLineage binds host + species and registers in one step.
        fauna.AssignLineage(cell, species);
        cell.RegisterLiveFauna(null); // guard
        Assert.Single(cell.LiveFauna);
        Assert.Equal(1, cell.GetLiveFaunaCount(species));
        Assert.Equal(1, cell.GetLiveHerbivoreCount()); // default diet is Herbivore + alive prey
        Assert.Equal(0, cell.GetLiveFaunaCount(null));

        // Lineage-less fauna are invisible to the registry (no SourceConfig).
        var stray = new GameObject("stray").AddComponent<RegistryFauna>();
        cell.RegisterLiveFauna(stray);
        Assert.Single(cell.LiveFauna);

        // Destroyed fauna: OnDestroy unregisters the lineage; the species count
        // decrements and the herbivore count drops.
        Object.Destroy(faunaHost);
        loop.Tick(0.02f);
        Assert.Equal(0, cell.GetLiveFaunaCount(species));
        Assert.Equal(0, cell.GetLiveHerbivoreCount());
        Assert.Empty(cell.LiveFauna);

        cell.UnregisterLiveFauna(null); // guard
    }

    // ── Fauna spawn-cycle telemetry (SpawnProfile deviated out) ─────────────

    [Fact]
    public void FaunaSpawnCycle_ReportsNoCycle_WhileSpawnProfileIsUnported()
    {
        var cell = MakeInitializedCell(out _, out _);

        Assert.Equal(0f, cell.CurrentFaunaSpawnPeriod);
        Assert.Equal(0f, cell.FaunaSpawnCycleFraction);

        cell.RecordFaunaSpawn();
        loop.Tick(0.5f);
        Assert.Equal(0f, cell.FaunaSpawnCycleFraction); // period 0 → "no cycle to show"
    }

    // ── Replay reset ────────────────────────────────────────────────────────

    [Fact]
    public void ResetCell_ViaOnResetForReplay_ClearsStateAndDestroysLifeForms()
    {
        var cell = MakeInitializedCell(out var runtime, out _, id: 11);
        var lifeForm = new GameObject("flora");
        cell.RegisterSpawnedObject(lifeForm);
        cell.AddBlock(MakePrism(Domains.Ruby, new Vector3(75f, 0f, 0f)));
        cell.ChangeVolume(Domains.Jade, 3f);
        cell.ApplyAuthoritativePhaseAndDomain(CellPhase.Frenzy, Domains.Ruby);

        runtime.OnResetForReplay.Raise();
        loop.Tick(0.02f); // flush the deferred lifeform Destroy

        bool lifeFormAlive = lifeForm;
        Assert.False(lifeFormAlive);
        Assert.Equal(0, cell.LiveBlockCount);
        Assert.Equal(0, cell.GetDomainBlockCount(Domains.Ruby));
        Assert.Equal(CellPhase.Calm, cell.Phase);
        Assert.Empty(cell.LiveFauna);
        Assert.NotNull(runtime.Config);                  // AssignConfig re-ran
        Assert.True(runtime.CellStatsList.ContainsKey(11));
        Assert.Equal(0f, cell.GetTeamVolume(Domains.Jade)); // ResetVolumes re-ran
    }

    // ── AssignConfig — IntensityWise selection ──────────────────────────────

    [Fact]
    public void AssignConfig_IntensityWise_PicksIntensityIndexedConfig()
    {
        var gameData = MakeGameData();
        gameData.SelectedIntensity.Value = 2;
        var runtime = MakeRuntime(gameData);
        var configs = new List<CellConfigDataSO> { MakeConfig(), MakeConfig(), MakeConfig() };

        var go = new GameObject("cell-intensity");
        go.SetActive(false);
        var cell = go.AddComponent<Cell>();
        cell.ID = 12;
        SetField(cell, "runtime", runtime);
        SetField(cell, "gameData", gameData);
        SetField(cell, "CellConfigs", configs);
        var choiceType = typeof(Cell).GetNestedType("CellTypeChoiceOptions", BindingFlags.NonPublic);
        SetField(cell, "cellTypeChoiceOptions", Enum.ToObject(choiceType, 1)); // IntensityWise
        go.SetActive(true);
        activatedCells.Add(go);

        gameData.OnInitializeGame.Raise();

        Assert.Same(configs[1], runtime.Config); // intensity 2 → index 1 (clamped range)
    }
}
