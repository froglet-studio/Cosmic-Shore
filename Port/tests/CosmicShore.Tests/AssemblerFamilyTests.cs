using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Rung-6 ambience groundwork: the Assemblers family (Assembler base + GyroidAssembler
// + bond-mate data + WallAssembler + SchwarzPAssembler + AssembledFlora's GrowthInfo)
// ported verbatim. These tests pin the deterministic, headless-testable core: the
// baked gyroid bond-mate table, bond-mate construction, and growth-site resolution
// through the real Physics.CheckBox occupancy probe. Conserved-mass note: assemblers
// only ever PLACE growth sites — nothing here removes prisms; consumption stays with
// the active forces (vessel abilities / fauna).
public class AssemblerFamilyTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public AssemblerFamilyTests()
    {
        // Upstream (c833c580) moved gyroid/wall occupancy + mate scans onto
        // PrismSpatialIndex — a process-global Singleton whose TryReserve claims
        // would otherwise leak between tests (every rig sits at the origin).
        typeof(Singleton<PrismSpatialIndex>)
            .GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .SetValue(null, null);
    }

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    static readonly CornerSiteType[] CornerSites =
    {
        CornerSiteType.TopLeft, CornerSiteType.TopRight,
        CornerSiteType.BottomLeft, CornerSiteType.BottomRight,
    };

    /// <summary>Full prism rig with an assembler attached before activation, ticked once so Start binds Prism.</summary>
    T CreateAssemblerOnPrism<T>(string name = "assembler-prism") where T : Assembler
    {
        T assembler = null;
        var rig = PrismTestRig.Create(name, r => assembler = r.GameObject.AddComponent<T>());
        spawned.Add(rig.GameObject);
        loop.Tick(0.02f); // run Start: assembler binds Prism = GetComponent<Prism>()
        if (!assembler.Prism) assembler.Prism = rig.Prism;
        return assembler;
    }

    /// <summary>
    /// Occupies a growth site with REGISTERED prism mass. Since the upstream
    /// spatial-index rework (c833c580), gyroid/wall occupancy probes go through
    /// PrismSpatialIndex.TryReserve — physics colliders are invisible to them by
    /// design (colliders are disabled through the 0.6s spawn window), so a blocker
    /// must be index-registered mass, exactly like live prisms in the game.
    /// </summary>
    Prism MakeBlocker(Vector3 position)
    {
        var rig = PrismTestRig.CreatePrismAt(position, $"blocker@{position}");
        spawned.Add(rig.gameObject);
        PrismSpatialIndex.EnsureInstance().Register(rig);
        return rig;
    }

    static void AssertVector3Equal(Vector3 expected, Vector3 actual, float tolerance = 1e-3f)
    {
        Assert.True((expected - actual).magnitude <= tolerance,
            $"Expected {expected} but got {actual} (tolerance {tolerance}).");
    }

    // ── Gyroid bond-mate table ──────────────────────────────────────────────

    [Fact]
    public void BondMateDataMap_CoversEveryBlockTypeCornerSitePair()
    {
        foreach (GyroidBlockType blockType in Enum.GetValues(typeof(GyroidBlockType)))
            foreach (var site in CornerSites)
            {
                Assert.True(
                    GyroidBondMateDataContainer.BondMateDataMap.TryGetValue((blockType, site), out var data),
                    $"Missing bond-mate data for ({blockType}, {site}).");
                Assert.Equal(site, data.Substrate);
            }

        // 12 block types × 4 corner sites — nothing extra baked in.
        Assert.Equal(48, GyroidBondMateDataContainer.BondMateDataMap.Count);
    }

    [Fact]
    public void GetBondMateData_ThrowsOnUnbakedSite()
    {
        Assert.Throws<KeyNotFoundException>(
            () => GyroidBondMateDataContainer.GetBondMateData(GyroidBlockType.AB, CornerSiteType.None));
    }

    [Fact]
    public void CreateGyroidBondMate_CopiesTableDataAndBindsMate()
    {
        var assembler = CreateAssemblerOnPrism<GyroidAssembler>("gyroid-a");
        var mate = CreateAssemblerOnPrism<GyroidAssembler>("gyroid-b");

        var bond = assembler.CreateGyroidBondMate(mate, GyroidBlockType.AB, CornerSiteType.TopRight);
        var expected = GyroidBondMateDataContainer.GetBondMateData(GyroidBlockType.AB, CornerSiteType.TopRight);

        Assert.Same(mate, bond.Mate);
        Assert.Equal(expected.Substrate, bond.Substrate);
        Assert.Equal(expected.Bondee, bond.Bondee);
        Assert.Equal(expected.BlockType, bond.BlockType);
        Assert.Equal(expected.isTail, bond.isTail);
        AssertVector3Equal(expected.DeltaPosition, bond.DeltaPosition, 0f);

        Assert.Throws<Exception>(
            () => assembler.CreateGyroidBondMate(mate, GyroidBlockType.AB, CornerSiteType.None));
    }

    // ── Gyroid growth-site resolution (GetGrowthInfo) ───────────────────────

    [Fact]
    public void GyroidGetGrowthInfo_GrowsFromFirstOpenSite_WithTableData()
    {
        var assembler = CreateAssemblerOnPrism<GyroidAssembler>();
        assembler.depth = 5;

        var info = assembler.GetGrowthInfo();

        var gyroidInfo = Assert.IsType<GyroidGrowthInfo>(info);
        Assert.True(gyroidInfo.CanGrow);

        // First open site is TopRight; for an AB block the table grows a BC mate there.
        var expected = GyroidBondMateDataContainer.GetBondMateData(GyroidBlockType.AB, CornerSiteType.TopRight);
        Assert.Equal(expected.BlockType, gyroidInfo.BlockType);
        Assert.False(gyroidInfo.IsDangerous); // BC is not in the dangerous set
        Assert.Equal(4, gyroidInfo.Depth);    // depth - 1

        // Position is the global TopRight bond site: DeltaPosition × separationDistance (3)
        // in the prism's frame (identity here).
        AssertVector3Equal(assembler.CalculateGlobalBondSite(CornerSiteType.TopRight), gyroidInfo.Position);
        AssertVector3Equal(expected.DeltaPosition * 3f, gyroidInfo.Position);
    }

    [Fact]
    public void GyroidGetGrowthInfo_SkipsOccupiedSite_AndMarksItBonded()
    {
        var assembler = CreateAssemblerOnPrism<GyroidAssembler>();
        assembler.depth = 5;

        // Occupy the TopRight bond site with registered prism mass — the TryReserve
        // occupancy probe must reject it and growth must fall through to TopLeft.
        MakeBlocker(assembler.CalculateGlobalBondSite(CornerSiteType.TopRight));

        var info = assembler.GetGrowthInfo();

        var gyroidInfo = Assert.IsType<GyroidGrowthInfo>(info);
        Assert.True(gyroidInfo.CanGrow);
        Assert.True(assembler.TopRightIsBonded); // blocked site is treated as filled

        var expected = GyroidBondMateDataContainer.GetBondMateData(GyroidBlockType.AB, CornerSiteType.TopLeft);
        Assert.Equal(expected.BlockType, gyroidInfo.BlockType);
        AssertVector3Equal(assembler.CalculateGlobalBondSite(CornerSiteType.TopLeft), gyroidInfo.Position);
    }

    [Fact]
    public void GyroidGetGrowthInfo_FullyBonded_CannotGrow()
    {
        var assembler = CreateAssemblerOnPrism<GyroidAssembler>();
        assembler.TopLeftIsBonded = true;
        assembler.TopRightIsBonded = true;
        assembler.BottomLeftIsBonded = true;
        assembler.BottomRightIsBonded = true;

        Assert.True(assembler.IsFullyBonded());
        Assert.False(assembler.GetGrowthInfo().CanGrow);
    }

    [Fact]
    public void GyroidDangerousBlockTypes_FlagGrowthAsDangerous()
    {
        var assembler = CreateAssemblerOnPrism<GyroidAssembler>();
        assembler.depth = 5;
        assembler.BlockType = GyroidBlockType.CD; // (CD, TopRight) grows DE — a dangerous type

        var info = Assert.IsType<GyroidGrowthInfo>(assembler.GetGrowthInfo());

        Assert.True(info.CanGrow);
        Assert.Equal(GyroidBlockType.DE, info.BlockType);
        Assert.True(info.IsDangerous);
    }

    [Fact]
    public void GyroidClearMateList_ResetsBondsOnBothSides()
    {
        var a = CreateAssemblerOnPrism<GyroidAssembler>("gyroid-a");
        var b = CreateAssemblerOnPrism<GyroidAssembler>("gyroid-b");

        a.MateList.Add(b);
        b.MateList.Add(a);
        a.TopRightIsBonded = true;

        Assert.True(a.IsBonded());
        Assert.True(b.IsBonded());

        a.ClearMateList();

        Assert.False(a.IsBonded());
        Assert.Empty(a.MateList);
        Assert.Empty(b.MateList); // reciprocal entry removed
    }

    // ── WallAssembler growth ────────────────────────────────────────────────

    [Fact]
    public void WallGetGrowthInfo_AlternatesRightThenLeft_ThenExhausts()
    {
        var wall = CreateAssemblerOnPrism<WallAssembler>("wall");
        wall.Depth = 3;

        // Right first: prism scale (1,1,1) + separation 2 → +3 on local right.
        var first = wall.GetGrowthInfo();
        Assert.True(first.CanGrow);
        Assert.Equal(2, first.Depth);
        AssertVector3Equal(new Vector3(3f, 0f, 0f), first.Position);
        Assert.True(wall.RightIsBonded);

        var second = wall.GetGrowthInfo();
        Assert.True(second.CanGrow);
        AssertVector3Equal(new Vector3(-3f, 0f, 0f), second.Position);
        Assert.True(wall.LeftIsBonded);

        // Both priority sites filled — the herringbone row is done growing here.
        Assert.False(wall.GetGrowthInfo().CanGrow);
    }

    [Fact]
    public void WallGetGrowthInfo_BlockedSiteIsConsumedWithoutGrowth()
    {
        var wall = CreateAssemblerOnPrism<WallAssembler>("wall-blocked");
        wall.Depth = 3;

        MakeBlocker(new Vector3(3f, 0f, 0f));   // occupy the Right site with registered mass

        var info = wall.GetGrowthInfo();

        // Right was blocked (marked bonded, no growth) — Left grows instead.
        Assert.True(info.CanGrow);
        Assert.True(wall.RightIsBonded);
        AssertVector3Equal(new Vector3(-3f, 0f, 0f), info.Position);
    }

    // ── SchwarzPAssembler growth ────────────────────────────────────────────

    [Fact]
    public void SchwarzPGetGrowthInfo_StepsAlongSurface_AndReservesTheSite()
    {
        var schwarz = CreateAssemblerOnPrism<SchwarzPAssembler>("schwarz-p");
        schwarz.Depth = 5;

        var first = Assert.IsType<SchwarzPGrowthInfo>(schwarz.GetGrowthInfo());

        Assert.True(first.CanGrow);
        Assert.Equal(4, first.Depth);
        Assert.NotNull(first.Frame);

        // The child landed back on the zero level set: cos x + cos y + cos z ≈ 0.
        var p = first.ParamPosition;
        float surfaceValue = Mathf.Cos(p.x) + Mathf.Cos(p.y) + Mathf.Cos(p.z);
        Assert.True(Mathf.Abs(surfaceValue) < 1e-2f, $"Child param off the surface: f = {surfaceValue}");

        // World position is one lattice step away from the seed, not on top of it.
        Assert.True((first.Position - schwarz.transform.position).magnitude > 1f);

        // Same-frame reservation: the next growth query must pick a DIFFERENT site.
        var second = Assert.IsType<SchwarzPGrowthInfo>(schwarz.GetGrowthInfo());
        Assert.True(second.CanGrow);
        Assert.True((second.ParamPosition - first.ParamPosition).magnitude > 1e-3f,
            "Second growth reused the reserved site.");
    }
}
