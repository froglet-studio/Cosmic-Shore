using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Game;          // CapsuleMembrane
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// V12 completion: the ICellLifeSpawner / SpawnProfileSO / CellModifier / CapsuleMembrane /
// SnowChanger deviation families restored — a Cell now runs fully alive headless. These
// tests exercise the RESTORED code paths end-to-end:
//
//   • CapsuleMembrane feeds Cell.MembraneRadius (and thus SenseRadius / density grids).
//   • A wired SpawnProfileSO seeds flora + fauna through the REAL spawner
//     (RandomLifeSpawner / IntensityWiseLifeSpawner via CellLifeSpawnerBase).
//   • CellModifier.Apply runs on post-first-crystal init.
//   • SnowChanger cytoplasm spawns on init and is destroyed on replay-reset.
//   • Phase stays VOLUME-keyed with a live spawner attached.
//
// LOCKED ECOLOGY INVARIANTS asserted here (Docs/ECOSYSTEM.md):
//   • No imposed death — a soak proves a seeded population NEVER shrinks on its own;
//     the only way a lifeform leaves is an active force (starvation/predation/ability).
//   • Starvation = wither-to-crystal, mass conserved — a starving fauna drops its one
//     elemental crystal (LifeFormCrystal) as a collectible that OUTLIVES the creature.
//   • No domain asymmetry — fauna seed in the cell's controlling color (verbatim path).
//
// Fixture mirrors CellTests: xunit fresh-instance-per-test, static ActiveCells +
// PrismSpatialIndex singleton reset, Dispose deactivates every spawned GO before the
// GameLoop goes away (fauna have goal/behavior coroutines that must stop first).
public class CellEcologyTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public CellEcologyTests()
    {
        // Fauna senses ride PrismSpatialIndex — a process-global Singleton whose
        // registrations would otherwise leak between tests (BoidChainTests precedent).
        typeof(Singleton<PrismSpatialIndex>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);
    }

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    // ── reflection helpers (walk BaseType, DeclaredOnly per level) ──────────

    static readonly BindingFlags Priv =
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static void Set(object target, string name, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Priv | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{name}' not found on {target.GetType().Name}.");
    }

    static T Get<T>(object target, string name)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, Priv | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            return (T)f.GetValue(target);
        }
        throw new InvalidOperationException($"Field '{name}' not found on {target.GetType().Name}.");
    }

    GameObject Track(GameObject go) { spawned.Add(go); return go; }

    // ── SO builders ─────────────────────────────────────────────────────────

    static GameDataSO MakeGameData()
    {
        var data = ScriptableObject.CreateInstance<GameDataSO>();
        data.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
        return data;
    }

    static CellRuntimeDataSO MakeRuntime(GameDataSO gameData)
    {
        var data = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
        Set(data, "gameData", gameData);
        data.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnCellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();
        return data;
    }

    // Small hysteresis table, authored 1:1 in volume so unit-scale rig prisms
    // (volume 1 each) drive the ladder without WithDerivedVolumeScale's ×16.
    static CellConfigDataSO MakeConfig(float senseRadiusOverride = 300f)
    {
        var config = ScriptableObject.CreateInstance<CellConfigDataSO>();
        config.SenseRadiusOverride = senseRadiusOverride;
        config.PhaseThresholds = new CellPhaseThresholds
        {
            RestlessEnter = 3, RestlessExit = 2,
            FrenzyEnter = 5, FrenzyExit = 4,
            RestlessEnterVolume = 3f, RestlessExitVolume = 2f,
            FrenzyEnterVolume = 5f, FrenzyExitVolume = 4f,
        };
        return config;
    }

    Cell MakeCell(CellRuntimeDataSO runtime, GameDataSO gameData, CellConfigDataSO config, int id = 1)
    {
        var go = Track(new GameObject($"cell-{id}"));
        go.SetActive(false);
        var cell = go.AddComponent<Cell>();
        cell.ID = id;
        Set(cell, "runtime", runtime);
        Set(cell, "gameData", gameData);
        Set(cell, "CellConfigs", new List<CellConfigDataSO> { config });
        go.SetActive(true);
        return cell;
    }

    static Crystal MakeTriggerCrystal(int id)
    {
        var go = new GameObject($"trigger-crystal-{id}");
        var crystal = go.AddComponent<Crystal>();
        crystal.Initialize(id);
        crystal.ownDomain = Domains.Blue;
        return crystal;
    }

    // ── prefab builders (activeSelf=true so spawner clones run their lifecycle) ─

    /// <summary>A membrane prefab carrying a CapsuleMembrane at the given radius.</summary>
    GameObject MakeCapsuleMembranePrefab(float radius)
    {
        var go = Track(new GameObject("membrane-prefab"));
        var membrane = go.AddComponent<CapsuleMembrane>();
        Set(membrane, "radius", radius);
        return go;
    }

    /// <summary>A SnowChanger cytoplasm prefab (bare snow shard, cellData wired).</summary>
    SnowChanger MakeCytoplasmPrefab(CellRuntimeDataSO runtime)
    {
        var snowShard = Track(new GameObject("snow-shard"));
        var go = Track(new GameObject("cytoplasm-prefab"));
        var snowChanger = go.AddComponent<SnowChanger>();
        Set(snowChanger, "cellData", runtime);
        Set(snowChanger, "snow", snowShard);
        return snowChanger;
    }

    /// <summary>
    /// A bare herbivore Fauna "prefab": no self-terminating behavior, so once seeded it
    /// only ever leaves through an active force. The vehicle for the no-imposed-death soak.
    /// </summary>
    Fauna MakeInertFaunaPrefab()
    {
        var go = Track(new GameObject("inert-fauna-prefab"));
        var fauna = go.AddComponent<InertHerbivore>();
        return fauna;
    }

    /// <summary>
    /// A real LightFauna prefab wired with a small starvation clock and an authored
    /// elemental (Mass) crystal child — the vehicle for the starvation→wither→crystal test.
    /// </summary>
    LightFauna MakeStarvingLightFaunaPrefab(CellRuntimeDataSO runtime, float starvationSeconds, float behaviorUpdateRate)
    {
        var data = ScriptableObject.CreateInstance<LightFaunaDataSO>();
        data.detectionRadius = 50f;
        data.consumeRadius = 20f;
        data.behaviorUpdateRate = behaviorUpdateRate;
        data.minSpeed = 0f;
        data.maxSpeed = 0f;
        data.witherRingInterval = 0.05f;

        var go = Track(new GameObject("lightfauna-prefab"));

        // Authored elemental crystal child (the LifeFormCrystal fast path). SphereCollider
        // is required by Crystal.ActivateCrystal, which the sealed Fauna.Die drops on death.
        var crystalGo = new GameObject("elemental-crystal");
        crystalGo.transform.SetParent(go.transform, worldPositionStays: false);
        crystalGo.AddComponent<SphereCollider>();
        var crystal = crystalGo.AddComponent<Crystal>();
        crystal.Initialize(9000 + id_counter++);
        crystal.ownDomain = Domains.Blue;
        var props = crystal.crystalProperties;
        props.Element = Element.Mass; // elemental → EnsureElementalCrystal keeps it as-is
        crystal.crystalProperties = props;
        Set(crystal, "cellData", runtime);

        var fauna = go.AddComponent<LightFauna>();
        Set(fauna, "data", data);
        Set(fauna, "cellData", runtime);
        Set(fauna, "starvationSeconds", starvationSeconds);
        Set(fauna, "predationImmunitySeconds", 0f);
        return fauna;
    }
    static int id_counter;

    static FaunaConfigurationSO MakeFaunaConfig(Fauna prefab, int populationSize, int maxPopulation = 0)
    {
        var cfg = ScriptableObject.CreateInstance<FaunaConfigurationSO>();
        cfg.FaunaPrefab = prefab;
        cfg.PopulationSize = populationSize;
        cfg.MaxLivePopulation = maxPopulation;
        cfg.SpawnProbability = 1f;
        cfg.InitialSpawnCount = 0;   // periodic seeder drives the count (deficit → floor)
        return cfg;
    }

    /// <summary>
    /// A minimal Flora prefab: empty Plant/Grow, no body — just proves seeding. autoInitialize
    /// is off (and cellData wired) so neither the template nor its clones self-init through the
    /// [Inject]-less LifeForm.Start path; the spawner drives Initialize(host) explicitly.
    /// </summary>
    Flora MakeFloraPrefab(CellRuntimeDataSO runtime)
    {
        var go = Track(new GameObject("flora-prefab"));
        var flora = go.AddComponent<StubFlora>();
        Set(flora, "autoInitialize", false);
        Set(flora, "cellData", runtime);
        return flora;
    }

    static FloraConfigurationSO MakeFloraConfig(Flora prefab, int initialCount)
    {
        var cfg = ScriptableObject.CreateInstance<FloraConfigurationSO>();
        cfg.FloraPrefab = prefab;
        cfg.SpawnProbability = 1f;
        cfg.InitialSpawnCount = initialCount;
        return cfg;
    }

    static SpawnProfileSO MakeSpawnProfile(
        List<FaunaConfigurationSO> faunas = null,
        List<FloraConfigurationSO> floras = null,
        int faunaFoodFloor = 0,
        float initialFaunaWait = 0f,
        float baseFaunaSpawnTime = 100f)
    {
        var profile = ScriptableObject.CreateInstance<SpawnProfileSO>();
        profile.SupportedFaunas = faunas ?? new List<FaunaConfigurationSO>();
        profile.SupportedFloras = floras ?? new List<FloraConfigurationSO>();
        profile.FaunaFoodFloor = faunaFoodFloor;
        profile.InitialFaunaSpawnWaitTime = initialFaunaWait;
        profile.BaseFaunaSpawnTime = baseFaunaSpawnTime;
        profile.FloraSpawnIntervalSeconds = 0f;
        return profile;
    }

    /// <summary>Build + Initialize a cell, then raise the first crystal to kick off the spawner chain.</summary>
    Cell StartAliveCell(SpawnProfileSO profile, out CellRuntimeDataSO runtime, out GameDataSO gameData,
        GameObject membranePrefab = null, SnowChanger cytoplasmPrefab = null,
        List<CellModifier> modifiers = null, int id = 1)
    {
        gameData = MakeGameData();
        runtime = MakeRuntime(gameData);
        var config = MakeConfig();
        config.SpawnProfile = profile;
        config.MembranePrefab = membranePrefab;
        config.CytoplasmPrefab = cytoplasmPrefab;
        if (modifiers != null) config.CellModifiers = modifiers;

        var cell = MakeCell(runtime, gameData, config, id);
        gameData.OnInitializeGame.Raise();          // → Initialize (config + visuals + grids)
        runtime.AddCrystalToList(MakeTriggerCrystal(id)); // → InitilizePostFirstCellItem (modifiers + cytoplasm + spawner)
        return cell;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CapsuleMembrane → MembraneRadius
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MembraneRadius_ReadsCapsuleMembraneRadius()
    {
        var gameData = MakeGameData();
        var runtime = MakeRuntime(gameData);
        var config = MakeConfig(senseRadiusOverride: 0f); // no override → SenseRadius falls to membrane
        config.MembranePrefab = MakeCapsuleMembranePrefab(777f);

        var cell = MakeCell(runtime, gameData, config, id: 1);
        gameData.OnInitializeGame.Raise();

        Assert.Equal(777f, cell.MembraneRadius);
        Assert.Equal(777f, cell.SenseRadius);
        Assert.True(cell.ContainsPosition(new Vector3(700f, 0f, 0f)));
        Assert.False(cell.ContainsPosition(new Vector3(800f, 0f, 0f)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SpawnProfile seeds flora + fauna through the real spawner
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SpawnProfile_SeedsFaunaThroughRealSpawner()
    {
        var faunaCfg = MakeFaunaConfig(MakeInertFaunaPrefab(), populationSize: 3);
        var profile = MakeSpawnProfile(faunas: new() { faunaCfg });

        var cell = StartAliveCell(profile, out _, out _);
        loop.Run(3, 0.1f); // let Start + first coroutine steps settle

        // The periodic seeder topped the live population up to the seed floor (3),
        // through the real RandomLifeSpawner (CellLifeSpawnerBase → SpawnFaunaWithDomain).
        Assert.Equal(3, cell.GetLiveFaunaCount(faunaCfg));
        Assert.Equal(3, cell.LiveFauna.Count);

        // No domain asymmetry: with no scored mass, the controlling color falls to Jade,
        // and every seeded creature spawns in that ONE controlling color (never mixed).
        foreach (var f in cell.LiveFauna)
            Assert.Equal(Domains.Jade, f.Domain);
    }

    [Fact]
    public void SpawnProfile_SeedsFloraThroughRealSpawner()
    {
        var gameData = MakeGameData();
        var runtime = MakeRuntime(gameData);
        var config = MakeConfig();
        var floraCfg = MakeFloraConfig(MakeFloraPrefab(runtime), initialCount: 4);
        config.SpawnProfile = MakeSpawnProfile(floras: new() { floraCfg });

        var cell = MakeCell(runtime, gameData, config, id: 1);
        gameData.OnInitializeGame.Raise();
        runtime.AddCrystalToList(MakeTriggerCrystal(1));
        loop.Run(6, 0.1f);

        // The initial flora batch (InitialSpawnCount = 4) planted through the real spawner
        // while the cell is below Frenzy (FloraPlantingEnabled) — one synchronously, the
        // rest across frames. Read the cell's REAL lifeform registry (spawnedLifeForms):
        // the CellStats.LifeFormsInCell mirror is dead data — CellStats is a struct, so
        // the verbatim UpdateCellStats writes a copy (upstream quirk, preserved as-is).
        var lifeForms = Get<List<GameObject>>(cell, "spawnedLifeForms");
        Assert.True(lifeForms.Count >= 4, $"expected >= 4 seeded flora, got {lifeForms.Count}");

        // No domain asymmetry: flora seed uniformly across the three playable domains —
        // any of them may roll, but NEVER Blue (the "no team" sentinel / universal-bait bug).
        foreach (var go in lifeForms)
            Assert.NotEqual(Domains.Blue, go.GetComponent<Flora>().domain);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  No imposed death — populations change only through an active force
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Soak_NoImposedDeath_PopulationNeverShrinksOnItsOwn()
    {
        var faunaCfg = MakeFaunaConfig(MakeInertFaunaPrefab(), populationSize: 3, maxPopulation: 3);
        var profile = MakeSpawnProfile(faunas: new() { faunaCfg }, baseFaunaSpawnTime: 100f);

        var cell = StartAliveCell(profile, out _, out _);
        loop.Run(3, 0.1f);

        var seeded = new List<Fauna>(cell.LiveFauna);
        Assert.Equal(3, seeded.Count);

        // Soak 120 sim-seconds. No prey is consumed, nothing predates, no ability fires —
        // so with mass conserved and no imposed lifespan clock, NOTHING may leave.
        loop.Run(1200, 0.1f);

        Assert.Equal(3, cell.LiveFauna.Count);           // count held
        Assert.Equal(3, cell.GetLiveFaunaCount(faunaCfg));
        foreach (var f in seeded)
            Assert.True(f, "a seeded fauna vanished with no active force — imposed-death regression");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Starvation = wither-to-crystal, mass conserved
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Starvation_WithersAndDropsOneElementalCrystal()
    {
        // starvationSeconds tiny, no prey in the cell → the seeded creature starves.
        // baseFaunaSpawnTime huge so the seeder doesn't reseed inside the observation window.
        var faunaCfg = MakeFaunaConfig(null, populationSize: 1, maxPopulation: 1);
        CellRuntimeDataSO runtime = null;
        // Build the runtime first so the LightFauna prefab's crystal child can bind to it.
        var gameData = MakeGameData();
        runtime = MakeRuntime(gameData);

        faunaCfg.FaunaPrefab = MakeStarvingLightFaunaPrefab(runtime, starvationSeconds: 1f, behaviorUpdateRate: 0.4f);
        var profile = MakeSpawnProfile(faunas: new() { faunaCfg }, baseFaunaSpawnTime: 1000f);

        var config = MakeConfig();
        config.SpawnProfile = profile;
        var cell = MakeCell(runtime, gameData, config, id: 1);
        gameData.OnInitializeGame.Raise();
        runtime.AddCrystalToList(MakeTriggerCrystal(1));

        loop.Run(3, 0.1f);
        Assert.Equal(1, cell.LiveFauna.Count); // seeded exactly one LightFauna

        var creature = cell.LiveFauna[0];
        var creatureGo = creature.GetGameObject();
        // Its authored elemental crystal (LifeFormCrystal fast path).
        var crystal = Get<Crystal>(creature, "crystal");
        Assert.True(crystal, "LightFauna should have bound its authored elemental crystal at Initialize");
        var crystalGo = crystal.gameObject;

        // Run until the creature starves and is removed (active death path), bounded.
        bool died = false;
        for (int i = 0; i < 120 && !died; i++)
        {
            loop.Tick(0.1f);
            died = !creatureGo; // destroyed-but-non-null Unity ref → fake-null
        }

        Assert.True(died, "the un-fed creature never starved — starvation regression");
        Assert.Equal(0, cell.LiveFauna.Count); // OnDestroy unregistered it

        // Mass is conserved + continuity: the ONE elemental crystal OUTLIVES the creature,
        // reparented to the cell as a collectible powerup (it did not vanish with the body).
        Assert.True(crystalGo, "the starved creature's elemental crystal was not dropped (mass-conservation regression)");
        Assert.Same(cell.transform, crystalGo.transform.parent);
        Assert.True(crystal.crystalProperties.IsElemental);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CellModifier applied on post-first-crystal init
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CellModifiers_ApplyOnPostFirstCellItemInit()
    {
        var modGo = Track(new GameObject("modifier"));
        var modifier = modGo.AddComponent<RecordingModifier>();

        var profile = MakeSpawnProfile();
        var cell = StartAliveCell(profile, out _, out _, modifiers: new List<CellModifier> { modifier });

        Assert.Same(cell, modifier.Applied);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SnowChanger cytoplasm spawns on init, despawns on replay-reset
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cytoplasm_SpawnsOnInit_AndDespawnsOnReplayReset()
    {
        var profile = MakeSpawnProfile();
        CellRuntimeDataSO runtime;
        GameDataSO gameData;

        // Build the cell, wiring the cytoplasm prefab (needs the runtime up front).
        gameData = MakeGameData();
        runtime = MakeRuntime(gameData);
        var config = MakeConfig();
        config.SpawnProfile = profile;
        config.CytoplasmPrefab = MakeCytoplasmPrefab(runtime);
        var cell = MakeCell(runtime, gameData, config, id: 1);
        gameData.OnInitializeGame.Raise();
        runtime.AddCrystalToList(MakeTriggerCrystal(1)); // → SpawnCytoplasm

        var cytoplasm = Get<SnowChanger>(cell, "spawnedCytoplasm");
        Assert.True(cytoplasm, "cytoplasm SnowChanger was not spawned on init");

        var cytoplasmGo = cytoplasm.gameObject;
        runtime.OnResetForReplay.Raise(); // → ResetCell destroys the cytoplasm
        loop.Tick(0.1f);                   // flush the deferred destroy

        Assert.False(cytoplasmGo, "cytoplasm SnowChanger was not destroyed on replay reset");
        Assert.False(Get<SnowChanger>(cell, "spawnedCytoplasm"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Phase stays volume-keyed with a live spawner attached
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PhaseStillVolumeKeyed_WithLiveSpawner()
    {
        var faunaCfg = MakeFaunaConfig(MakeInertFaunaPrefab(), populationSize: 1);
        var profile = MakeSpawnProfile(faunas: new() { faunaCfg });
        var cell = StartAliveCell(profile, out _, out _);
        loop.Run(3, 0.1f);

        // FaunaSpawningEnabled is env-volume keyed: no environment mass yet → false
        // (the seeded fauna bodies here are bare, adding no tracked env volume).
        Assert.False(cell.FaunaSpawningEnabled);
        Assert.Equal(CellPhase.Calm, cell.Phase);

        // Add 5 unit-volume env prisms → LiveVolume 5 ≥ FrenzyEnterVolume(5): the ladder
        // climbs on VOLUME, not on the live fauna/prism count alone.
        for (int i = 0; i < 5; i++)
        {
            var prism = PrismTestRig.CreatePrismAt(new Vector3(75f * i - 150f, 0f, 0f));
            spawned.Add(prism.gameObject);
            prism.GetComponent<PrismTeamManager>().SetInitialTeam(Domains.Ruby);
            cell.AddBlock(prism);
        }

        // Tick past the cell's 0.25s live-volume recompute cadence AND the 0.5s phase
        // tick: the fresh env volume (5 ≥ FrenzyEnterVolume) now drives both reads.
        loop.Tick(0.6f);
        Assert.True(cell.FaunaSpawningEnabled);       // env mass present now
        Assert.Equal(CellPhase.Frenzy, cell.Phase);
        Assert.False(cell.FloraGrowingEnabled);       // frozen at Frenzy
    }

    // ── test doubles ─────────────────────────────────────────────────────────

    /// <summary>A herbivore Fauna with no self-terminating behavior — never dies on its own.</summary>
    sealed class InertHerbivore : Fauna { }

    /// <summary>Minimal Flora: empty grow/plant, just enough for the spawner to seed one.</summary>
    sealed class StubFlora : Flora
    {
        public override void Grow() { }
        public override void Plant() { }
    }

    /// <summary>A CellModifier that records the cell it was applied to.</summary>
    sealed class RecordingModifier : CellModifier
    {
        public Cell Applied;
        public override void Apply(Cell cell) => Applied = cell;
    }
}
