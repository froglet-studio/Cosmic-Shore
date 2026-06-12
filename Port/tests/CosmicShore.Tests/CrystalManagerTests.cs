using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Rung 3 — the CrystalManager family, ported verbatim.
//
// CrystalManager (abstract): anchor-based spawn/respawn placement, stable CellItem
// ids, batch spawning (one anchor per batch), per-crystal next-anchor progression.
// LocalCrystalManager: the single-process game-mode manager — spawns the batch on
// turn start, relocates a claimed crystal to its next anchor (the REAL respawn
// semantics: the crystal MOVES, it is not destroyed), explodes via Crystal.Explode,
// and destroys the course on turn end.
//
// The Crystal shell's staged CT1 deviations are restored here too: Respawn() routes
// into CrystalManager.RespawnCrystal (allowRespawnOnImpact) and
// NotifyManagerToExplodeCrystal into CrystalManager.ExplodeCrystal.
// ─────────────────────────────────────────────────────────────────────────────

public class CrystalManagerTests
{
    const float Dt = 1f / 60f;

    static GameDataSO MakeGameData()
    {
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameTurnStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameTurnEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        return gameData;
    }

    static CellRuntimeDataSO MakeCellData(GameDataSO gameData)
    {
        var cellData = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
        V19Rig.Set(cellData, "gameData", gameData);
        cellData.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        cellData.OnCellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        return cellData;
    }

    /// <summary>Inactive Crystal prefab template (cellData pre-wired, like the real prefab asset).</summary>
    static Crystal MakeCrystalPrefab(CellRuntimeDataSO cellData)
    {
        var prefabGo = new GameObject("crystal-prefab");
        prefabGo.SetActive(false); // prefab: never live in the scene
        var prefab = prefabGo.AddComponent<Crystal>();
        V19Rig.Set(prefab, "cellData", cellData);
        return prefab;
    }

    static LocalCrystalManager MakeLocalManager(
        GameDataSO gameData, CellRuntimeDataSO cellData, Crystal prefab,
        Vector3[] anchors, int fixedCount)
    {
        var go = new GameObject("local-crystal-manager");
        go.SetActive(false); // configure-before-activation
        var manager = go.AddComponent<LocalCrystalManager>();
        V19Rig.Set(manager, "gameData", gameData);
        V19Rig.Set(manager, "cellData", cellData);
        V19Rig.Set(manager, "crystalPrefab", prefab);
        V19Rig.Set(manager, "crystalCountMode", CrystalManager.CrystalCountMode.FixedCount);
        V19Rig.Set(manager, "fixedCrystalCount", fixedCount);
        V19Rig.Set(manager, "listOfCrystalPositions", new List<CrystalPositionSet>
        {
            new() { positions = new List<Vector3>(anchors) },
        });
        go.SetActive(true); // Awake initializes the cellData runtime lists, OnEnable subscribes
        return manager;
    }

    [Fact]
    public void LocalCrystalManager_SpawnsTheBatch_OnTurnStarted_WithStableIds()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var cellData = MakeCellData(gameData);
        var prefab = MakeCrystalPrefab(cellData);
        var anchors = new[] { new Vector3(100f, 0f, 0f), new Vector3(0f, 0f, 100f) };
        MakeLocalManager(gameData, cellData, prefab, anchors, fixedCount: 3);

        Assert.Empty(cellData.Crystals);
        gameData.OnMiniGameTurnStarted.Raise();

        // The whole batch spawned around ONE anchor (anchor 0), ids 1..3 stable.
        Assert.Equal(3, cellData.Crystals.Count);
        Assert.Equal(3, cellData.CellItems.Count);
        for (int id = 1; id <= 3; id++)
        {
            Assert.True(cellData.TryGetCrystalById(id, out var crystal));
            Assert.True((crystal.transform.position - anchors[0]).magnitude <= 35.001f,
                "batch crystals cluster around the single batch anchor");
        }

        // Re-raising the turn start spawns nothing new (batch already present).
        gameData.OnMiniGameTurnStarted.Raise();
        Assert.Equal(3, cellData.Crystals.Count);
    }

    [Fact]
    public void LocalCrystalManager_RespawnCrystal_RelocatesTheSameCrystal_ToTheNextAnchor()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var cellData = MakeCellData(gameData);
        var prefab = MakeCrystalPrefab(cellData);
        var anchors = new[] { new Vector3(100f, 0f, 0f), new Vector3(0f, 0f, 100f) };
        var manager = MakeLocalManager(gameData, cellData, prefab, anchors, fixedCount: 1);

        gameData.OnMiniGameTurnStarted.Raise();
        Assert.True(cellData.TryGetCrystalById(1, out var crystal));
        var firstPos = crystal.transform.position;

        int courseUpdates = 0;
        cellData.OnCellItemsUpdated.OnRaised += () => courseUpdates++;

        // The REAL respawn semantics: the crystal MOVES (relocates to the next anchor);
        // it is never destroyed and never re-instantiated.
        manager.RespawnCrystal(1);

        Assert.True(cellData.TryGetCrystalById(1, out var moved));
        Assert.Same(crystal, moved);
        Assert.False(crystal.IsDestroyed);
        Assert.True((crystal.transform.position - anchors[1]).magnitude <= 35.001f,
            "respawn relocates around the crystal's NEXT anchor");
        Assert.NotEqual(firstPos, crystal.transform.position);
        Assert.Equal(1, courseUpdates); // UpdateCrystalPos raises OnCellItemsUpdated → AI retargets
    }

    [Fact]
    public void LocalCrystalManager_TurnEnd_DestroysTheCourse()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var cellData = MakeCellData(gameData);
        var prefab = MakeCrystalPrefab(cellData);
        var manager = MakeLocalManager(gameData, cellData, prefab,
            new[] { new Vector3(50f, 0f, 0f) }, fixedCount: 2);

        gameData.OnMiniGameTurnStarted.Raise();
        Assert.Equal(2, cellData.Crystals.Count);
        var spawned = new List<Crystal>(cellData.Crystals);

        gameData.OnMiniGameTurnEnd.Raise();
        loop.Tick(Dt); // end-of-frame destroy flush

        Assert.Empty(cellData.Crystals);
        Assert.Empty(cellData.CellItems);
        foreach (var crystal in spawned)
            Assert.True(crystal.IsDestroyed);
        _ = manager; // lifetime anchor
    }

    [Fact]
    public void LocalCrystalManager_ExplodeCrystal_OpensTheImpactWindow_ThenCloses()
    {
        using var loop = new GameLoop();
        var gameData = MakeGameData();
        var cellData = MakeCellData(gameData);
        var prefab = MakeCrystalPrefab(cellData);
        var manager = MakeLocalManager(gameData, cellData, prefab,
            new[] { new Vector3(50f, 0f, 0f) }, fixedCount: 1);

        gameData.OnMiniGameTurnStarted.Raise();
        Assert.True(cellData.TryGetCrystalById(1, out var crystal));

        manager.ExplodeCrystal(1, new Crystal.ExplodeParams { Speed = 12f });
        Assert.True(crystal.IsExploding);

        // The 0.5s WaitForImpact window closes on the loop clock (verbatim contract).
        for (int frame = 0; frame < 35; frame++) loop.Tick(Dt);
        Assert.False(crystal.IsExploding);
    }

    // ── the Crystal shell's restored manager chain ──────────────────────────

    [Fact]
    public void CrystalShell_Respawn_RoutesThroughTheManager_WhenRespawnOnImpactAllowed()
    {
        using var loop = new GameLoop();
        var (_, _, _, crystal, _) = ContactRig.MakeCrystal<OmniCrystalImpactor>(new Vector3(500f, 0f, 0f));
        var manager = (RecordingCrystalManager)crystal.CrystalManager;

        V19Rig.Set(crystal, "allowRespawnOnImpact", true);
        crystal.Respawn();
        Assert.Equal(new[] { crystal.Id }, manager.RespawnCalls);
        Assert.False(crystal.IsDestroyed);

        crystal.NotifyManagerToExplodeCrystal(new Crystal.ExplodeParams { Speed = 3f });
        var explode = Assert.Single(manager.ExplodeCalls);
        Assert.Equal(crystal.Id, explode.id);
        Assert.True(crystal.IsExploding);

        // Tick the 0.5s WaitForImpact window closed — Explode's async-void was started
        // from the test body (not inside a tick), so the run must complete it.
        for (int frame = 0; frame < 35; frame++) loop.Tick(Dt);
        Assert.False(crystal.IsExploding);
    }

    [Fact]
    public void CrystalShell_Respawn_DestroysTheCrystal_WhenRespawnOnImpactDisallowed()
    {
        using var loop = new GameLoop();
        var (_, _, _, crystal, cellData) = ContactRig.MakeCrystal<OmniCrystalImpactor>(new Vector3(500f, 0f, 0f));
        var manager = (RecordingCrystalManager)crystal.CrystalManager;

        crystal.Respawn(); // allowRespawnOnImpact defaults false → DestroyCrystal
        loop.Tick(Dt);     // end-of-frame destroy flush

        Assert.Empty(manager.RespawnCalls);
        Assert.True(crystal.IsDestroyed);
        Assert.Empty(cellData.Crystals);
    }

    [Fact]
    public void CrystalShell_CanBeCollected_BlueIsForEveryone_OwnedIsForTheMatchingDomain()
    {
        using var loop = new GameLoop();
        var go = new GameObject("crystal");
        var crystal = go.AddComponent<Crystal>();

        crystal.ownDomain = Domains.Blue; // uncommitted — the "no team" sentinel
        Assert.True(crystal.CanBeCollected(Domains.Jade));
        Assert.True(crystal.CanBeCollected(Domains.Ruby));
        Assert.True(crystal.CanBeCollected(Domains.Blue));

        crystal.ownDomain = Domains.Ruby;
        Assert.True(crystal.CanBeCollected(Domains.Ruby));
        Assert.False(crystal.CanBeCollected(Domains.Jade));
        Assert.False(crystal.CanBeCollected(Domains.Gold));
        Assert.False(crystal.CanBeCollected(Domains.Blue));
    }
}
