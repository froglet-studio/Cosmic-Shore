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

// Rung-6 ambience groundwork: the Boid fauna chain (Boid + BoidManager +
// BoidController) ported verbatim on top of the Fauna base. Covers the behavior
// tick (flocking forces steer toward the goal), prism interaction through the real
// Physics.OverlapSphereNonAlloc spatial query (Attach grazing and Explode combat per
// BoidCollisionEffects), and BoidManager swarm spawning bound to a cell. Conserved-mass
// note (Docs/ECOSYSTEM.md): every prism removal here is an ACTIVE force — a boid
// eating opposing mass — and same-domain mass is untouched; no decay anywhere.
public class BoidChainTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public BoidChainTests()
    {
        // Upstream (c833c580) moved fauna senses onto PrismSpatialIndex — a process-global
        // Singleton whose reservations/registrations would otherwise leak between tests.
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

    static T Get<T>(object target, string field)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, Priv | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            return (T)f.GetValue(target);
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    /// <summary>HealthPrism with the full prism wiring (mirror of LifeFormFamilyTests).</summary>
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

    /// <summary>
    /// Boid with its embedded body HealthPrism rig as a child. Activated but NOT
    /// initialized — callers configure then call Initialize(cell) themselves
    /// (mirrors BoidManager.SpawnBoids ordering).
    /// </summary>
    Boid CreateBoid(Domains domain, Vector3 position, params BoidCollisionEffects[] effects)
    {
        var go = new GameObject("boid");
        spawned.Add(go);
        go.SetActive(false);
        go.transform.position = position;

        var boid = go.AddComponent<Boid>();
        boid.domain = domain;
        Set(boid, "collisionEffects", new List<BoidCollisionEffects>(effects));

        CreateHealthPrism("boid-body", go.transform);

        go.SetActive(true);
        return boid;
    }

    /// <summary>
    /// Full prism rig placed and team-assigned — the boid's prey/neighbor mass.
    /// Registered with PrismSpatialIndex: since the upstream spatial-index rework
    /// (c833c580) boids sense prisms through QuerySphere, not physics colliders, so
    /// unregistered mass is invisible to them (the game path registers in
    /// Prism.CreateBlockCoroutine after the 0.6s spawn window).
    /// </summary>
    Prism CreatePrey(Domains domain, Vector3 position, string name = "prey")
    {
        var rig = PrismTestRig.Create(name);
        spawned.Add(rig.GameObject);
        rig.GameObject.transform.position = position;
        rig.TeamManager.SetInitialTeam(domain);
        PrismSpatialIndex.EnsureInstance().Register(rig.Prism);
        return rig.Prism;
    }

    // ── cell rig (mirror of CellTests) ──────────────────────────────────────

    Cell MakeInitializedCell(int id = 1)
    {
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();

        var runtime = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
        Set(runtime, "gameData", gameData);
        runtime.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        runtime.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        runtime.OnCellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        runtime.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();

        var config = ScriptableObject.CreateInstance<CellConfigDataSO>();
        config.SenseRadiusOverride = 300f;
        config.PhaseThresholds = new CellPhaseThresholds
        {
            RestlessEnter = 3, RestlessExit = 2,
            FrenzyEnter = 5, FrenzyExit = 4,
        };

        var go = new GameObject($"cell-{id}");
        spawned.Add(go);
        go.SetActive(false);
        var cell = go.AddComponent<Cell>();
        cell.ID = id;
        Set(cell, "runtime", runtime);
        Set(cell, "gameData", gameData);
        Set(cell, "CellConfigs", new List<CellConfigDataSO> { config });
        go.SetActive(true);

        gameData.OnInitializeGame.Raise();
        return cell;
    }

    // ── (b) behavior tick: flocking forces steer toward the goal ────────────

    [Fact]
    public void BehaviorTick_SteersBoidTowardGoal()
    {
        var boid = CreateBoid(Domains.Jade, Vector3.zero, BoidCollisionEffects.Attach);
        boid.Initialize(null);
        boid.Goal = new Vector3(300f, 0f, 0f);

        // 2 simulated seconds: first behavior tick lands on the first coroutine frame,
        // a second at +1.5s; goal-update (5s) never fires so Goal stays ours.
        for (int i = 0; i < 100; i++) loop.Tick(0.02f);

        Vector3 position = boid.transform.position;
        Assert.True(position.x > 3f, $"Boid did not advance toward the goal: {position}");
        Assert.True(Mathf.Abs(position.y) < 1f && Mathf.Abs(position.z) < 1f,
            $"Boid drifted off the goal axis: {position}");
        Assert.True((boid.Goal - position).magnitude < 300f - 3f, "Distance to goal did not shrink.");
    }

    // ── (c) prism interaction through the real spatial query ────────────────

    [Fact]
    public void BehaviorTick_AttachEffect_LatchesOntoOpposingPrism_AndGrazesIt()
    {
        var boid = CreateBoid(Domains.Jade, Vector3.zero, BoidCollisionEffects.Attach);
        var prey = CreatePrey(Domains.Ruby, new Vector3(0f, 0f, 6f));
        float preyScaleBefore = prey.TargetScale.y;

        boid.Initialize(null);
        for (int i = 0; i < 10; i++) loop.Tick(0.02f);

        Assert.True(Get<bool>(boid, "isAttached"), "Boid did not attach to the opposing prism.");
        Assert.Equal(prey.transform.position, boid.target);
        // Attach grazes: the prey shrank (Grow(-1)) — an ACTIVE removal of opposing mass.
        Assert.True(prey.TargetScale.y < preyScaleBefore,
            $"Prey was not grazed: {prey.TargetScale.y} vs {preyScaleBefore}");
    }

    [Fact]
    public void BehaviorTick_ExplodeEffect_DestroysOpposingMassOnly()
    {
        var boid = CreateBoid(Domains.Jade, Vector3.zero, BoidCollisionEffects.Explode);
        var opposing = CreatePrey(Domains.Ruby, new Vector3(0f, 0f, 6f), "opposing");
        var friendly = CreatePrey(Domains.Jade, new Vector3(0f, 6f, 0f), "friendly");

        boid.Initialize(null);
        for (int i = 0; i < 10; i++) loop.Tick(0.02f);

        // Opposing mass eaten (active force): renderer + collider off via SetupDestruction.
        Assert.False(opposing.GetComponent<BoxCollider>().enabled, "Opposing prism survived the boid.");
        Assert.False(opposing.GetComponent<MeshRenderer>().enabled);

        // Same-domain mass is NOT food — conserved until an active force consumes it.
        Assert.True(friendly.GetComponent<BoxCollider>().enabled, "Friendly prism was wrongly consumed.");
        Assert.True(friendly.GetComponent<MeshRenderer>().enabled);
    }

    // ── (d) BoidManager swarm spawn ─────────────────────────────────────────

    [Fact]
    public void BoidManager_SpawnsConfiguredSwarm_BoundToCellWithTrailAndDomain()
    {
        var cell = MakeInitializedCell();

        // "Prefab": a fully-rigged boid that is never initialized itself.
        var prefab = CreateBoid(Domains.Jade, new Vector3(500f, 500f, 500f), BoidCollisionEffects.Attach);

        var managerGo = new GameObject("boid-manager");
        spawned.Add(managerGo);
        managerGo.SetActive(false);
        var manager = managerGo.AddComponent<BoidManager>();
        manager.boidPrefab = prefab;
        manager.numberOfBoids = 3;
        manager.spawnRadius = 10f;
        manager.domain = Domains.Ruby;
        manager.Initialize(cell);
        managerGo.SetActive(true);

        loop.Tick(0.02f); // Start → SpawnBoids
        foreach (var b in manager.Boids) spawned.Add(b.gameObject);

        Assert.Equal(3, manager.Boids.Count);
        Assert.Equal(3, manager.boidTrail.TrailList.Count);

        for (int i = 0; i < manager.Boids.Count; i++)
        {
            var b = manager.Boids[i];
            Assert.Equal(Domains.Ruby, b.domain);                       // domain propagated
            Assert.Same(cell, Get<Cell>(b, "hostCell"));                // lineage cell binding
            Assert.Equal((float)i / 3, b.normalizedIndex, 3);           // staggered behavior ticks

            var block = b.GetComponentInChildren<Prism>(true);
            Assert.NotNull(block);
            Assert.Equal(Domains.Ruby, block.Domain);                   // body mass carries the domain
            Assert.Same(manager.boidTrail, block.Trail);                // registered on the swarm trail
        }

        // Spawn ring: boids appear on the manager's spawn radius, not stacked on it.
        foreach (var b in manager.Boids)
        {
            float distance = (b.transform.position - managerGo.transform.position).magnitude;
            Assert.True(Mathf.Abs(distance - 10f) < 1.5f, $"Boid spawned off the ring: {distance}");
        }
    }
}
