using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// V11 cell-layer coverage: BlockDensityGrid math (cell-sized grid derivation, world↔voxel
// indexing, count add/remove with saturation/underflow guards, densest-region pipeline +
// result cache, the Domains.Blue wildcard "any team" bucket semantics), CellConfigDataSO
// defaults, and CellRuntimeDataSO pure logic (crystal registry, per-cell stats, phase-write
// event contract, runtime reset). Conserved-mass note (Docs/ECOSYSTEM.md): nothing here adds
// decay — grids only ever change via explicit Add/RemoveBlock, and ResetRuntimeData is the
// original's replay-reset, not a culler.
public class CellLayerTests
{
    class TestBlock : MonoBehaviour { }

    // ── helpers ─────────────────────────────────────────────────────────────

    static TestBlock MakeBlockAt(Vector3 position)
    {
        var go = new GameObject("block");
        go.transform.position = position;
        return go.AddComponent<TestBlock>();
    }

    static Crystal MakeCrystal(int id, Domains domain)
    {
        var go = new GameObject($"crystal-{id}");
        var crystal = go.AddComponent<Crystal>();
        crystal.Initialize(id);
        crystal.ownDomain = domain;
        return crystal;
    }

    static CellRuntimeDataSO MakeCellData()
    {
        var data = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
        data.OnResetForReplay = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnCrystalSpawned = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnCellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        data.OnPhaseChanged = ScriptableObject.CreateInstance<ScriptableEventCellPhase>();
        return data;
    }

    /// <summary>
    /// 9³ grid over a 600m cube centred on the origin: stride 75, origin (-300,-300,-300),
    /// kernelHalfWidth 2. Voxel (6,6,6) is world (150,150,150); voxel (2,2,2) is (-150,-150,-150).
    /// </summary>
    static BlockCountDensityGrid MakeSmallGrid(Domains domain = Domains.Jade) =>
        new BlockCountDensityGrid(domain, Vector3.zero, 600f);

    /// <summary>
    /// A cross-shaped cluster the smoothing+mean-shift pipeline resolves exactly to its
    /// centre: <paramref name="centerCount"/> blocks at <paramref name="center"/> plus
    /// <paramref name="armCount"/> at each of the six face-neighbour voxels (±stride).
    /// </summary>
    static void AddCluster(BlockCountDensityGrid grid, Vector3 center, int centerCount, int armCount, float stride = 75f)
    {
        for (int i = 0; i < centerCount; i++) grid.AddBlock(MakeBlockAt(center));
        foreach (var axis in new[] { new Vector3(stride, 0, 0), new Vector3(0, stride, 0), new Vector3(0, 0, stride) })
        {
            for (int i = 0; i < armCount; i++) grid.AddBlock(MakeBlockAt(center + axis));
            for (int i = 0; i < armCount; i++) grid.AddBlock(MakeBlockAt(center - axis));
        }
    }

    static void AssertVector3Equal(Vector3 expected, Vector3 actual, float tolerance = 1e-3f)
    {
        Assert.True((expected - actual).magnitude <= tolerance,
            $"Expected {expected} but got {actual} (tolerance {tolerance})");
    }

    // ── BlockDensityGrid — grid derivation ─────────────────────────────────

    [Fact]
    public void Init_DerivesResolutionStrideAndOrigin_FromCellSize()
    {
        var grid = new BlockDensityGrid();
        grid.Init(Domains.Jade, new Vector3(100f, 200f, 300f), 2400f);

        // ceil(2400/75)+1 = 33 — exactly the resolution ceiling.
        Assert.Equal(33, grid.GridPointsPerDimensionActual);
        Assert.Equal(2400f / 32f, grid.Stride);
        Assert.Equal(new Vector3(-1100f, -1000f, -900f), grid.origin);
        Assert.Equal(Domains.Jade, grid.Domain);
    }

    [Fact]
    public void Init_ClampsResolution_ToFloorForSmallCells()
    {
        var grid = new BlockDensityGrid();
        grid.Init(Domains.Ruby, Vector3.zero, 100f);

        // ceil(100/75)+1 = 3 → clamped up to MinGridPointsPerDimension.
        Assert.Equal(BlockDensityGrid.MinGridPointsPerDimension, grid.GridPointsPerDimensionActual);
        Assert.Equal(100f / 8f, grid.Stride);
    }

    [Fact]
    public void Init_ClampsResolution_ToCeilingForHugeCells()
    {
        var grid = new BlockDensityGrid();
        grid.Init(Domains.Gold, Vector3.zero, 10000f);

        Assert.Equal(BlockDensityGrid.MaxGridPointsPerDimension, grid.GridPointsPerDimensionActual);
        Assert.Equal(10000f / 32f, grid.Stride);
    }

    // ── BlockDensityGrid — indexing math ────────────────────────────────────

    [Fact]
    public void MapGridIndicesToCoordinates_CornersSpanTheCell()
    {
        var grid = new BlockDensityGrid();
        grid.Init(Domains.Jade, new Vector3(100f, 200f, 300f), 2400f);

        Assert.Equal(new Vector3(-1100f, -1000f, -900f), grid.MapGridIndicesToCoordinates(new Vector3Int(0, 0, 0)));
        Assert.Equal(new Vector3(1300f, 1400f, 1500f), grid.MapGridIndicesToCoordinates(new Vector3Int(32, 32, 32)));
    }

    [Fact]
    public void MapCoordinatesToGridIndices_RoundTripsAndRoundsToNearestVoxel()
    {
        var grid = new BlockDensityGrid();
        grid.Init(Domains.Jade, Vector3.zero, 600f); // stride 75

        var idx = new Vector3Int(6, 2, 4);
        Assert.Equal(idx, grid.MapCoordinatesToGridIndices(grid.MapGridIndicesToCoordinates(idx)));

        // Offsets under half a stride snap to the same voxel.
        Assert.Equal(idx, grid.MapCoordinatesToGridIndices(
            grid.MapGridIndicesToCoordinates(idx) + new Vector3(30f, -30f, 30f)));
    }

    // ── BlockCountDensityGrid — density add/remove ───────────────────────────

    [Fact]
    public void AddBlock_IncrementsDensity_RemoveBlock_Decrements()
    {
        using var loop = new GameLoop();
        var grid = MakeSmallGrid();
        var position = new Vector3(150f, 150f, 150f);
        var block = MakeBlockAt(position);

        grid.AddBlock(block);
        grid.AddBlock(block);
        Assert.Equal(2, grid.GetDensityAtPosition(position));

        grid.RemoveBlock(block);
        Assert.Equal(1, grid.GetDensityAtPosition(position));
    }

    [Fact]
    public void RemoveBlock_OnEmptyVoxel_DoesNotUnderflow()
    {
        using var loop = new GameLoop();
        var grid = MakeSmallGrid();
        var block = MakeBlockAt(new Vector3(150f, 150f, 150f));

        grid.RemoveBlock(block);

        Assert.Equal(0, grid.GetDensityAtPosition(block.transform.position));
    }

    [Fact]
    public void AddBlock_OutsideGrid_IsIgnored()
    {
        using var loop = new GameLoop();
        var grid = MakeSmallGrid();
        var farAway = new Vector3(10000f, 0f, 0f);

        grid.AddBlock(MakeBlockAt(farAway));

        Assert.Equal(0, grid.GetDensityAtPosition(farAway)); // out-of-grid reads density 0
    }

    [Fact]
    public void GetDensityAtPosition_OutOfBounds_ReturnsZero()
    {
        var grid = MakeSmallGrid();
        Assert.Equal(0, grid.GetDensityAtPosition(new Vector3(-9000f, 0f, 0f)));
    }

    // ── BlockCountDensityGrid — densest region + cache ───────────────────────

    [Fact]
    public void FindDensestRegion_EmptyGrid_ReportsZeroDensity()
    {
        using var loop = new GameLoop();
        var grid = MakeSmallGrid();

        grid.FindDensestRegion();

        // 0 peak density is the caller's "fall back to anchor" contract.
        Assert.Equal(0f, grid.LastResultDensity);
    }

    [Fact]
    public void FindDensestRegion_ResolvesClusterCentre()
    {
        using var loop = new GameLoop();
        var grid = MakeSmallGrid();
        var center = new Vector3(150f, 150f, 150f);
        AddCluster(grid, center, centerCount: 4, armCount: 1); // 10 blocks
        grid.AddBlock(MakeBlockAt(new Vector3(-150f, -150f, -150f))); // lone distractor

        var result = grid.FindDensestRegion();

        AssertVector3Equal(center, result);
        Assert.Equal(10f, grid.LastResultDensity);
    }

    [Fact]
    public void FindDensestRegion_CachesWhileFresh_RecomputesWhenDirtyAndStale()
    {
        using var loop = new GameLoop();
        var grid = MakeSmallGrid();
        var oldCenter = new Vector3(150f, 150f, 150f);
        var newCenter = new Vector3(-150f, -150f, -150f);
        AddCluster(grid, oldCenter, centerCount: 4, armCount: 1);   // 10 blocks

        var first = grid.FindDensestRegion();
        AssertVector3Equal(oldCenter, first);

        // Dirty the grid with a 3x denser cluster elsewhere — but inside the
        // MinRecomputeIntervalSeconds window the cached answer must hold.
        AddCluster(grid, newCenter, centerCount: 12, armCount: 3);  // 30 blocks
        AssertVector3Equal(first, grid.FindDensestRegion());

        // Past the interval the dirty grid recomputes and tracks the new mass.
        loop.Tick(BlockDensityGrid.MinRecomputeIntervalSeconds + 0.05f);
        var recomputed = grid.FindDensestRegion();
        AssertVector3Equal(newCenter, recomputed);
        Assert.Equal(30f, grid.LastResultDensity);
    }

    [Fact]
    public void WildcardAnyTeamBucket_IsDomainsBlue_AndCountsMassFromEveryDomain()
    {
        using var loop = new GameLoop();
        // Blue is the "no team" sentinel — a grid keyed Blue is the wildcard any-team
        // bucket. The grid itself is domain-agnostic: bucket selection is the owning
        // Cell's job, so the Blue grid counts whatever mass it is handed.
        var grid = MakeSmallGrid(Domains.Blue);
        Assert.Equal(Domains.Blue, grid.Domain);

        var position = new Vector3(150f, 150f, 150f);
        foreach (var domain in new[] { Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue })
        {
            var crystal = MakeCrystal(0, domain);
            crystal.transform.position = position;
            grid.AddBlock(crystal);
        }

        Assert.Equal(4, grid.GetDensityAtPosition(position));
    }

    // ── CellConfigDataSO defaults ────────────────────────────────────────────

    [Fact]
    public void CellConfigDataSO_Defaults_AreSenseRadiusZeroAndDefaultThresholds()
    {
        var config = ScriptableObject.CreateInstance<CellConfigDataSO>();

        Assert.Equal(0f, config.SenseRadiusOverride);
        Assert.Equal(0f, config.Difficulty);
        Assert.Equal(0, config.CellEndGameScore);

        // Field initializer seeds the 3-phase hysteresis table with the shared defaults.
        Assert.Equal(8000, config.PhaseThresholds.RestlessEnter);
        Assert.Equal(7500, config.PhaseThresholds.RestlessExit);
        Assert.Equal(15000, config.PhaseThresholds.FrenzyEnter);
        Assert.Equal(14000, config.PhaseThresholds.FrenzyExit);
        Assert.False(config.PhaseThresholds.IsAllZero);
    }

    // ── CellRuntimeDataSO — per-cell stats ──────────────────────────────────

    [Fact]
    public void EnsureCellStats_SeedsCalmBlueDefaults()
    {
        var data = MakeCellData();

        data.EnsureCellStats(7);

        var stats = data.CellStatsList[7];
        Assert.Equal(0, stats.LifeFormsInCell);
        Assert.Equal(0, stats.LiveBlockCount);
        Assert.Equal(CellPhase.Calm, stats.Phase);
        Assert.Equal(Domains.Blue, stats.DominantDomain);
    }

    [Fact]
    public void GetLifeFormsInCellSafe_UnknownCell_ReturnsZeroAndSeedsEntry()
    {
        var data = MakeCellData();

        Assert.Equal(0, data.GetLifeFormsInCellSafe(3));
        Assert.True(data.CellStatsList.ContainsKey(3));
    }

    [Fact]
    public void WriteCellRuntimeStats_WritesStats_AndRaisesOnlyOnPhaseTransition()
    {
        var data = MakeCellData();
        var raised = new List<CellPhase>();
        data.OnPhaseChanged.OnRaised += phase => raised.Add(phase);

        data.WriteCellRuntimeStats(1, 100, CellPhase.Calm, Domains.Jade);     // Calm → Calm: no raise
        data.WriteCellRuntimeStats(1, 16000, CellPhase.Frenzy, Domains.Jade); // Calm → Frenzy: raise
        data.WriteCellRuntimeStats(1, 17000, CellPhase.Frenzy, Domains.Ruby); // Frenzy → Frenzy: no raise

        Assert.Equal(new[] { CellPhase.Frenzy }, raised);
        var stats = data.CellStatsList[1];
        Assert.Equal(17000, stats.LiveBlockCount);
        Assert.Equal(CellPhase.Frenzy, stats.Phase);
        Assert.Equal(Domains.Ruby, stats.DominantDomain);
    }

    // ── CellRuntimeDataSO — crystal registry ────────────────────────────────

    [Fact]
    public void AddCrystalToList_AddsToBothLists_AndRaisesCellItemsUpdated()
    {
        using var loop = new GameLoop();
        var data = MakeCellData();
        int raises = 0;
        data.OnCellItemsUpdated.OnRaised += () => raises++;
        var crystal = MakeCrystal(1, Domains.Jade);

        data.AddCrystalToList(crystal);

        Assert.Equal(new CellItem[] { crystal }, data.CellItems);
        Assert.Equal(new[] { crystal }, data.Crystals);
        Assert.Equal(1, raises);

        data.AddCrystalToList(null); // destroyed/null crystals are ignored
        Assert.Equal(1, raises);
        Assert.Single(data.Crystals);
    }

    [Fact]
    public void TryRemoveItem_RemovesCrystalFromBothLists()
    {
        using var loop = new GameLoop();
        var data = MakeCellData();
        int raises = 0;
        data.OnCellItemsUpdated.OnRaised += () => raises++;
        var crystal = MakeCrystal(1, Domains.Jade);
        data.AddCrystalToList(crystal);

        Assert.True(data.TryRemoveItem(crystal));
        Assert.Empty(data.CellItems);
        Assert.Empty(data.Crystals);
        Assert.Equal(2, raises); // add + remove

        Assert.False(data.TryRemoveItem(crystal)); // absent item: no-op, no raise
        Assert.Equal(2, raises);
    }

    [Fact]
    public void TryGetLocalCrystal_PrefersBlueSentinel_ThenFallsBackToFirst()
    {
        using var loop = new GameLoop();
        var data = MakeCellData();
        var jade = MakeCrystal(1, Domains.Jade);
        var blue = MakeCrystal(2, Domains.Blue);
        data.AddCrystalToList(jade);
        data.AddCrystalToList(blue);

        // Local domain reads Blue (no-team sentinel), so the uncommitted crystal wins.
        Assert.True(data.TryGetLocalCrystal(out var found));
        Assert.Same(blue, found);

        // Without a Blue crystal the first crystal is the fallback.
        data.TryRemoveItem(blue);
        Assert.True(data.TryGetLocalCrystal(out found));
        Assert.Same(jade, found);

        data.TryRemoveItem(jade);
        Assert.False(data.TryGetLocalCrystal(out found));
        Assert.Null(data.CrystalTransform);
    }

    [Fact]
    public void TryGetLocalCrystal_SkipsDestroyedCrystals()
    {
        using var loop = new GameLoop();
        var data = MakeCellData();
        var jade = MakeCrystal(1, Domains.Jade);
        var blue = MakeCrystal(2, Domains.Blue);
        data.AddCrystalToList(jade);
        data.AddCrystalToList(blue);

        Object.Destroy(blue.gameObject);
        loop.Tick(0.02f); // destroyed-object equality kicks in at end of frame

        Assert.True(data.TryGetLocalCrystal(out var found));
        Assert.Same(jade, found);
    }

    [Fact]
    public void TryGetCrystalById_FindsMatch_OrReturnsFalse()
    {
        using var loop = new GameLoop();
        var data = MakeCellData();
        var crystal = MakeCrystal(42, Domains.Gold);
        data.AddCrystalToList(crystal);

        Assert.True(data.TryGetCrystalById(42, out var found));
        Assert.Same(crystal, found);
        Assert.False(data.TryGetCrystalById(43, out _));
    }

    // ── CellRuntimeDataSO — runtime reset ────────────────────────────────────

    [Fact]
    public void ResetRuntimeData_DestroysCrystals_ClearsState_KeepsConfig()
    {
        using var loop = new GameLoop();
        var data = MakeCellData();
        data.Config = ScriptableObject.CreateInstance<CellConfigDataSO>();
        var crystal = MakeCrystal(1, Domains.Jade);
        var crystalGameObject = crystal.gameObject;
        data.AddCrystalToList(crystal);
        data.EnsureCellStats(1);

        data.ResetRuntimeData();
        loop.Tick(0.02f); // flush the deferred Destroy

        bool alive = crystalGameObject;
        Assert.False(alive);
        Assert.Empty(data.Crystals);
        Assert.Empty(data.CellItems);
        Assert.Empty(data.CellStatsList);
        Assert.NotNull(data.Config); // Config is NOT cleared
    }
}
