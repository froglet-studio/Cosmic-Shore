using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// CaptainManager unit (2026-07-10) — the REAL captain economy manager + the
// XpHandler local lanes (replacing the Store-unit shell + the struct-only
// XpHandler extraction). Covers: XpHandler's per-element XP accumulation
// (IssueXP creates the class row on demand, adds only to the captain's primary
// element; GetCaptainXP reads it back) and EncounterCaptain's dedupe;
// LoadCaptainsData deriving the full roster from the serialized SO_CaptainList
// over the LIVE XpHandler + CatalogManager.Inventory lanes (XP, Encountered,
// Unlocked, Level = 1 + matching upgrade count; locked-or-unencountered → 0;
// UnlockedShips fed; CaptainDataLoaded + OnLoadCaptainData raised); the manager
// IssueXP double-write (model + roster + XpHandler); the query surface
// (GetCaptainByName / GetCaptainSOByName / GetCaptainFromUpgrade tag match /
// IsCaptainEncountered); and the UpgradeXPRequirements ladder.
// ─────────────────────────────────────────────────────────────────────────────

public class CaptainXpFamilyTests : IDisposable
{
    readonly GameLoop loop = new(nameof(CaptainXpFamilyTests));

    readonly CaptainManager manager;
    readonly SO_CaptainList captainList;
    readonly SO_Captain aureliaSO;   // Manta / Space — encountered + unlocked
    readonly SO_Captain korvaxSO;    // Rhino / Mass  — never encountered, not owned

    public CaptainXpFamilyTests()
    {
        ResetStatics();

        SO_Captain MakeSO(string name, VesselClassType cls, Element element)
        {
            var vessel = ScriptableObject.CreateInstance<SO_Vessel>();
            vessel.Name = cls.ToString();
            vessel.Class = cls;
            var so = ScriptableObject.CreateInstance<SO_Captain>();
            so.Name = name;
            so.Description = $"{name} desc";
            so.Vessel = vessel;
            so.PrimaryElement = element;
            return so;
        }

        aureliaSO = MakeSO("AURELIA", VesselClassType.Manta, Element.Space);
        korvaxSO = MakeSO("KORVAX", VesselClassType.Rhino, Element.Mass);
        captainList = ScriptableObject.CreateInstance<SO_CaptainList>();
        captainList.CaptainList = new List<SO_Captain> { aureliaSO, korvaxSO };

        // The rig stands in for the PlayFab login: it lands the XpHandler
        // dictionaries (AURELIA encountered with 120 Space XP) that
        // OnLoadCaptainXpData would have landed.
        XpHandler.ClassXpData = new Dictionary<VesselClassType, XpData>
        {
            [VesselClassType.Manta] = new XpData(space: 120, time: 0, mass: 0, charge: 0),
        };
        XpHandler.EncounteredCaptainsData = new Dictionary<VesselClassType, List<Element>>
        {
            [VesselClassType.Manta] = new List<Element> { Element.Space },
        };

        var go = new GameObject("CaptainManager");
        go.SetActive(false);
        manager = go.AddComponent<CaptainManager>();
        Set(manager, "AllCaptains", captainList);
        go.SetActive(true); // OnEnable: the upstream "[PLAYFAB DISABLED]" early return

        // CatalogManager wired the StoreScreenTests way (its inventory Captain
        // lane calls the injected _captainManager.EncounterCaptain).
        var catalogGo = new GameObject("CatalogManager");
        catalogGo.SetActive(false);
        var catalog = catalogGo.AddComponent<CatalogManager>();
        var netVariable = ScriptableObject.CreateInstance<NetworkMonitorDataVariable>();
        netVariable.Value = new NetworkMonitorData
        {
            OnNetworkFound = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnNetworkLost = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        };
        Set(catalog, "_networkMonitorDataVariable", netVariable);
        Set(catalog, "_captainManager", manager);
        catalogGo.SetActive(true);

        // First derivation over the still-empty inventory (populates
        // captainData so the inventory load's EncounterCaptain lane can
        // resolve names — upstream ordering: captains load, then inventory
        // events re-derive). Then AURELIA lands owned + one matching upgrade.
        LoadRoster();
        catalog.LoadLocalInventory(new List<VirtualItem>
        {
            new() { ItemId = "captain-aurelia", Name = "AURELIA", ContentType = "Captain",
                    Tags = new List<string>(), Amount = 1 },
            new() { ItemId = "upgrade-1", Name = "Aurelia Upgrade I", ContentType = "CaptainUpgrade",
                    Tags = new List<string> { "Manta", "Space" }, Amount = 1 },
        });
    }

    public void Dispose()
    {
        ResetStatics();
        loop.Dispose();
    }

    static void ResetStatics()
    {
        typeof(SingletonPersistent<CaptainManager>).GetProperty("Instance")!.SetValue(null, null);
        typeof(SingletonPersistent<CatalogManager>).GetProperty("Instance")!.SetValue(null, null);
        typeof(CaptainManager).GetProperty("CaptainDataLoaded")!.SetValue(null, false);
        typeof(CaptainManager)
            .GetField("OnLoadCaptainData", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, null);
        XpHandler.ClassXpData = null;
        XpHandler.EncounteredCaptainsData = null;
        XpHandler.OnCaptainDataLoaded = null;
        CatalogManager.ResetLocalEconomy();
    }

    static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    void LoadRoster()
        => typeof(CaptainManager)
            .GetMethod("LoadCaptainsData", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, null);

    [Fact]
    public void LoadCaptainsData_DerivesTheRoster_FromXpHandlerAndInventory()
    {
        int loadedRaises = 0;
        CaptainManager.OnLoadCaptainData += () => loadedRaises++;

        LoadRoster();

        Assert.True(CaptainManager.CaptainDataLoaded);
        Assert.Equal(1, loadedRaises); // one batch raise (per-captain callbacks suppressed)

        var aurelia = manager.GetCaptainByName("AURELIA");
        Assert.Equal(120, aurelia.XP);            // XpHandler.GetCaptainXP (Space lane)
        Assert.True(aurelia.Encountered);         // XpHandler.EncounteredCaptainsData
        Assert.True(aurelia.Unlocked);            // CatalogManager.Inventory.ContainsCaptain
        Assert.Equal(2, aurelia.Level);           // 1 + one matching Manta/Space upgrade
        Assert.Contains(aureliaSO.Vessel, manager.UnlockedShips);

        var korvax = manager.GetCaptainByName("KORVAX");
        Assert.Equal(0, korvax.XP);
        Assert.False(korvax.Encountered);
        Assert.False(korvax.Unlocked);
        Assert.Equal(0, korvax.Level);            // locked-or-unencountered floor

        Assert.True(manager.IsCaptainEncountered("AURELIA"));
        Assert.False(manager.IsCaptainEncountered("KORVAX"));
        Assert.Single(manager.GetUnlockedCaptains());
        Assert.Equal(2, manager.GetAllCaptains().Count);
    }

    [Fact]
    public void IssueXP_WritesTheModel_AndAccumulatesThePrimaryElementLane()
    {
        LoadRoster();

        manager.IssueXP("AURELIA", 30);

        // Pins the upstream aliasing double-add: the string overload resolves
        // the captain via GetCaptainByName — the SAME instance held in
        // captainData.AllCaptains — so `captain.XP += amount` and
        // `captainData.AllCaptains[...].XP += amount` both hit it (120+30+30).
        // The XpHandler element lane is written once (150).
        Assert.Equal(180, manager.GetCaptainByName("AURELIA").XP);
        Assert.Equal(150, XpHandler.ClassXpData[VesselClassType.Manta].Space); // primary element only
        Assert.Equal(0, XpHandler.ClassXpData[VesselClassType.Manta].Time);

        // A class with no row yet: IssueXP creates it on demand.
        var korvax = manager.GetCaptainByName("KORVAX");
        manager.IssueXP(korvax, 40);
        Assert.Equal(40, XpHandler.ClassXpData[VesselClassType.Rhino].Mass);
    }

    [Fact]
    public void EncounterCaptain_MarksTheElement_AndDedupes()
    {
        LoadRoster();

        manager.EncounterCaptain("KORVAX");
        Assert.Contains(Element.Mass, XpHandler.EncounteredCaptainsData[VesselClassType.Rhino]);

        manager.EncounterCaptain("KORVAX"); // dedupe: no duplicate entry
        Assert.Single(XpHandler.EncounteredCaptainsData[VesselClassType.Rhino]);

        // The reload derives the new encounter into the roster.
        LoadRoster();
        Assert.True(manager.GetCaptainByName("KORVAX").Encountered);
    }

    [Fact]
    public void QuerySurface_SOLookup_UpgradeTagMatch_XpLadder()
    {
        LoadRoster();

        Assert.Same(aureliaSO, manager.GetCaptainSOByName("AURELIA"));
        Assert.Equal(2, manager.GetAllSOCaptains().Count);

        var upgrade = new VirtualItem { Tags = new List<string> { "Rhino", "Mass" } };
        Assert.Equal("KORVAX", manager.GetCaptainFromUpgrade(upgrade).Name);
        Assert.Null(manager.GetCaptainFromUpgrade(new VirtualItem { Tags = new List<string> { "Rhino", "Time" } }));

        // Ladder: next-level requirements (level 0 → 100 ... level 4 → 400).
        var korvax = manager.GetCaptainByName("KORVAX"); // Level 0
        Assert.Equal(100, manager.GetCaptainUpgradeXPRequirement(korvax));
        var aurelia = manager.GetCaptainByName("AURELIA"); // Level 2
        Assert.Equal(200, manager.GetCaptainUpgradeXPRequirement(aurelia));
    }
}
