using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// CloudSave arc — the engine CloudSaveSdk placeholder (LocalCloudSaveService's
// real serialize-on-save round-trip), UGSCloudSaveProvider (availability gate,
// load fallback, cancellation), CloudDataRepository (load/dirty/debounce/reset
// lifecycle), and UGSDataService LIVE end to end (auth-driven init over all 11
// repositories, hangar→vessel unlock sync, flush-dirty-only, reset-all).
// ─────────────────────────────────────────────────────────────────────────────

public class CloudDataTests : IDisposable
{
    public CloudDataTests()
    {
        CloudSaveService.Reset();
        AuthenticationService.Reset();
        UnityServices.Reset();
    }

    public void Dispose()
    {
        CloudSaveService.Reset();
        AuthenticationService.Reset();
        UnityServices.Reset();
    }

    static void SignIn()
    {
        UnityServices.State = ServicesInitializationState.Initialized;
        AuthenticationService.Instance.IsSignedIn = true;
    }

    // ── LocalCloudSaveService ───────────────────────────────────────────

    [Fact]
    public async Task LocalStore_RoundTripsFieldBasedModels_IncludingDictionaries()
    {
        var svc = new LocalCloudSaveService();
        var hangar = new HangarCloudData { SelectedVessel = "Squirrel" };
        hangar.UnlockVessel("Squirrel");
        hangar.GetOrCreatePreference("Squirrel").Favorited = true;

        await svc.Player.SaveAsync(new Dictionary<string, object> { { "HANGAR_DATA", hangar } });
        var loaded = await svc.Player.LoadAsync(new HashSet<string> { "HANGAR_DATA", "missing-key" });

        Assert.False(loaded.ContainsKey("missing-key")); // only saved keys come back
        var copy = loaded["HANGAR_DATA"].Value.GetAs<HangarCloudData>();
        Assert.NotSame(hangar, copy); // a REAL serialize/deserialize, not a reference cache
        Assert.True(copy.IsVesselUnlocked("Squirrel"));
        Assert.True(copy.VesselPreferences["Squirrel"].Favorited); // Dictionary<,> fields survive
        Assert.Equal("Squirrel", copy.SelectedVessel);
    }

    // ── UGSCloudSaveProvider ────────────────────────────────────────────

    [Fact]
    public async Task Provider_GatesOnAvailability_LoadNullSaveFalse_WhenSignedOut()
    {
        var provider = new UGSCloudSaveProvider();
        Assert.False(provider.IsAvailable);

        Assert.Null(await provider.LoadAsync<HangarCloudData>("HANGAR_DATA"));
        Assert.False(await provider.SaveAsync("HANGAR_DATA", new HangarCloudData()));
    }

    [Fact]
    public async Task Provider_SaveThenLoad_RoundTripsThroughTheLocalService()
    {
        SignIn();
        var provider = new UGSCloudSaveProvider();
        var squad = new SquadCloudData
        {
            SquadLeaderClass = VesselClassType.Sparrow,
            SquadLeaderElement = Element.Charge,
            Initialized = true,
        };

        Assert.True(await provider.SaveAsync("SQUAD_DATA", squad));
        var loaded = await provider.LoadAsync<SquadCloudData>("SQUAD_DATA");

        Assert.Equal(VesselClassType.Sparrow, loaded.SquadLeaderClass);
        Assert.Equal(Element.Charge, loaded.SquadLeaderElement);
        Assert.True(loaded.Initialized);
        Assert.Null(await provider.LoadAsync<SquadCloudData>("NO_SUCH_KEY"));
    }

    [Fact]
    public async Task Provider_FallsBackToStringPayload_ForLegacyJsonWrites()
    {
        SignIn();
        var provider = new UGSCloudSaveProvider();

        // Legacy shape: the value itself is a JSON STRING (old JsonUtility writes),
        // so GetAs<T> throws and the verbatim fallback re-parses the inner json.
        var legacyJson = "{\"MusicEnabled\":false,\"MusicLevel\":0.25}";
        await CloudSaveService.Instance.Data.Player.SaveAsync(
            new Dictionary<string, object> { { "PLAYER_SETTINGS", legacyJson } });

        var loaded = await provider.LoadAsync<PlayerSettingsCloudData>("PLAYER_SETTINGS");

        Assert.False(loaded.MusicEnabled);
        Assert.Equal(0.25f, loaded.MusicLevel);
    }

    [Fact]
    public async Task Provider_CancelledRetryBackoff_ReturnsFalse_WithoutFailureEpisode()
    {
        SignIn();
        CloudSaveService.Instance = new ThrowingCloudSaveService();
        var provider = new UGSCloudSaveProvider();
        int failEpisodes = 0;
        Action<string> onFailed = _ => failEpisodes++;
        UGSCloudSaveProvider.OnSaveFailed += onFailed;

        try
        {
            // First attempt throws; the backoff Task.Delay observes the cancelled
            // token and bails out — no 14s retry ladder, no failure episode.
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.False(await provider.SaveAsync("KEY", new HangarCloudData(), cts.Token));
            Assert.Equal(0, failEpisodes);
        }
        finally
        {
            UGSCloudSaveProvider.OnSaveFailed -= onFailed;
        }
    }

    sealed class ThrowingCloudSaveService : ICloudSaveService, ICloudSaveDataApi, IPlayerDataApi
    {
        public ICloudSaveDataApi Data => this;
        public IPlayerDataApi Player => this;
        public Task<Dictionary<string, Item>> LoadAsync(HashSet<string> keys)
            => throw new Exception("backend down");
        public Task SaveAsync(Dictionary<string, object> data)
            => throw new Exception("backend down");
    }

    // ── CloudDataRepository lifecycle ───────────────────────────────────

    sealed class TestRepo : CloudDataRepository<HangarCloudData>
    {
        public int AfterLoads;
        public override string CloudKey => "TEST_REPO";
        // 50ms debounce so the dirty→save loop is testable in real time.
        public TestRepo(ICloudSaveProvider provider) : base(provider, 0.05f) { }
        protected override void OnAfterLoad(HangarCloudData data)
        {
            AfterLoads++;
            data.UnlockedVessels ??= new List<string>();
        }
    }

    static async Task WaitUntil(Func<bool> done, int timeoutMs = 4000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!done() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.True(done(), "condition should settle within the timeout");
    }

    [Fact]
    public async Task Repository_Load_KeepsDefaults_WhenCloudIsEmpty_AndRaisesChanged()
    {
        SignIn();
        var repo = new TestRepo(new UGSCloudSaveProvider());
        int changed = 0;
        repo.OnDataChanged += () => changed++;

        await repo.LoadAsync();

        Assert.True(repo.IsLoaded);
        Assert.Equal(1, changed);
        Assert.Equal(0, repo.AfterLoads); // no cloud payload → the default instance stands
        Assert.Empty(repo.Data.UnlockedVessels);
    }

    [Fact]
    public async Task Repository_MarkDirty_DebouncesASave_ThenComesClean()
    {
        SignIn();
        var provider = new UGSCloudSaveProvider();
        var repo = new TestRepo(provider);
        await repo.LoadAsync();

        repo.Data.UnlockVessel("Manta");
        repo.MarkDirty();
        Assert.True(repo.IsDirty);

        await WaitUntil(() => !repo.IsDirty); // the 50ms debounce loop flushed

        var cloud = await provider.LoadAsync<HangarCloudData>("TEST_REPO");
        Assert.True(cloud.IsVesselUnlocked("Manta"));
    }

    [Fact]
    public async Task Repository_KeepsDirtyWhileOffline_ThenRecoversOnSignIn()
    {
        SignIn();
        var provider = new UGSCloudSaveProvider();
        var repo = new TestRepo(provider);
        await repo.LoadAsync();

        // Go offline BEFORE the mutation: the save fails silently and the data
        // stays dirty — never drop a pending change.
        AuthenticationService.Instance.IsSignedIn = false;
        repo.Data.UnlockVessel("Rhino");
        repo.MarkDirty();
        await Task.Delay(200);
        Assert.True(repo.IsDirty);

        // Reconnect: the background retry loop flushes the pending change. Poll the
        // PERSISTED value, not !IsDirty — the debounce loop clears the dirty flag before
        // the (cross-thread) save completes, so asserting on IsDirty races the store write.
        AuthenticationService.Instance.IsSignedIn = true;
        HangarCloudData cloud = null;
        var deadline = DateTime.UtcNow.AddMilliseconds(4000);
        while (DateTime.UtcNow < deadline)
        {
            cloud = await provider.LoadAsync<HangarCloudData>("TEST_REPO");
            if (cloud != null && cloud.IsVesselUnlocked("Rhino")) break;
            await Task.Delay(10);
        }
        Assert.True(cloud != null && cloud.IsVesselUnlocked("Rhino"),
            "the pending change should flush to cloud on reconnect");
    }

    [Fact]
    public async Task Repository_Reset_RestoresDefaults_AndPersistsThem()
    {
        SignIn();
        var provider = new UGSCloudSaveProvider();
        var repo = new TestRepo(provider);
        await repo.LoadAsync();
        repo.Data.UnlockVessel("Manta");
        await repo.SaveAsync();

        await repo.ResetAsync();

        Assert.Empty(repo.Data.UnlockedVessels);
        var cloud = await provider.LoadAsync<HangarCloudData>("TEST_REPO");
        Assert.Empty(cloud.UnlockedVessels); // the reset was saved, not just local
    }
}

/// <summary>
/// UGSDataService LIVE end to end — the auth-driven init over all 11
/// repositories, hangar→vessel unlock sync, flush-dirty-only, and reset-all.
/// MonoBehaviour lifecycle needs the GameLoop (Start runs on the first tick).
/// </summary>
public class UgsDataServiceTests : IDisposable
{
    readonly GameLoop loop = new(nameof(UgsDataServiceTests));

    public UgsDataServiceTests()
    {
        CloudSaveService.Reset();
        AuthenticationService.Reset();
        UnityServices.Reset();
    }

    public void Dispose()
    {
        CloudSaveService.Reset();
        AuthenticationService.Reset();
        UnityServices.Reset();
        loop.Dispose();
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

    static T ForceValue<T>(ScriptableVariable<T> variable, T value) where T : class
    {
        typeof(ScriptableVariable<T>)
            .GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(variable, value);
        return value;
    }

    void Pump(Func<bool> done, int maxFrames = 600)
    {
        for (int i = 0; i < maxFrames && !done(); i++) loop.Tick(1f / 60f);
        Assert.True(done(), "condition should settle within the pump budget");
    }

    sealed class Rig
    {
        public UGSDataService Service;
        public AuthenticationData AuthData;
        public SO_VesselList VesselList;
        public SO_Vessel Vessel;
    }

    Rig MakeRig(bool signedInAtStart)
    {
        UnityServices.State = ServicesInitializationState.Initialized;
        AuthenticationService.Instance.IsSignedIn = signedInAtStart;

        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        var authData = ForceValue(authVar, new AuthenticationData
        {
            IsSignedIn = signedInAtStart,
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        });

        var vessel = ScriptableObject.CreateInstance<SO_Vessel>();
        vessel.Name = "TestVessel";
        Set(vessel, "isLocked", true);
        var vesselList = ScriptableObject.CreateInstance<SO_VesselList>();
        vesselList.VesselList = new List<SO_Vessel> { vessel };

        var go = new GameObject("ugs-data-service");
        go.SetActive(false);
        var service = go.AddComponent<UGSDataService>();
        Set(service, "vesselList", vesselList);
        Set(service, "_authData", authVar);
        go.SetActive(true); // Awake (repos created) now, Start (auth wiring) on tick

        return new Rig { Service = service, AuthData = authData, VesselList = vesselList, Vessel = vessel };
    }

    [Fact]
    public void SignedInAtStart_InitializesAllRepos_AndSyncsHangarUnlocks()
    {
        // Pre-seed the cloud BEFORE the service exists — a returning player.
        var hangar = new HangarCloudData();
        hangar.UnlockVessel("TestVessel");
        CloudSaveService.Instance.Data.Player.SaveAsync(
            new Dictionary<string, object> { { UGSKeys.HangarData, hangar } }).GetAwaiter().GetResult();

        var rig = MakeRig(signedInAtStart: true);
        int initialized = 0;
        rig.Service.OnInitialized += () => initialized++;

        Pump(() => rig.Service.IsInitialized);

        Assert.Equal(1, initialized);
        Assert.True(rig.Service.Hangar.IsLoaded);
        Assert.True(rig.Service.Hangar.Data.IsVesselUnlocked("TestVessel"));
        Assert.False(rig.Vessel.IsLocked); // hangar sync unlocked the SO asset
        Assert.True(rig.Service.Profile.IsLoaded); // all 11 domains loaded
        Assert.True(rig.Service.Loadout.IsLoaded);
        Assert.True(rig.Service.Squad.IsLoaded);
    }

    [Fact]
    public void SignedInLater_InitializesOnTheSoapEvent()
    {
        var rig = MakeRig(signedInAtStart: false);
        for (int i = 0; i < 10; i++) loop.Tick(1f / 60f);
        Assert.False(rig.Service.IsInitialized); // waiting on auth

        AuthenticationService.Instance.IsSignedIn = true; // provider availability
        rig.AuthData.IsSignedIn = true;
        rig.AuthData.OnSignedIn.Raise();

        Pump(() => rig.Service.IsInitialized);
        Assert.Empty(rig.Service.Hangar.Data.UnlockedVessels); // fresh cloud — nothing unlocked
        Assert.True(rig.Vessel.IsLocked);                      // so the SO asset stays locked
    }

    [Fact]
    public void FlushAll_SavesOnlyDirtyRepos_AndResetAllRestoresDefaults()
    {
        var rig = MakeRig(signedInAtStart: true);
        Pump(() => rig.Service.IsInitialized);

        // Mutate one domain and flush — only the dirty repo uploads.
        rig.Service.ProfileRepo.Data.displayName = "FlushedName";
        rig.Service.ProfileRepo.MarkDirty();
        var flush = rig.Service.FlushAllAsync();
        Pump(() => flush.IsCompleted);

        var provider = new UGSCloudSaveProvider();
        var cloudProfile = provider.LoadAsync<PlayerProfileData>(UGSKeys.PlayerProfile).GetAwaiter().GetResult();
        Assert.Equal("FlushedName", cloudProfile.displayName);

        // Reset-all wipes every domain back to defaults and persists that.
        var reset = rig.Service.ResetAllDataAsync();
        Pump(() => reset.IsCompleted);
        Assert.True(reset.GetAwaiter().GetResult());
        Assert.NotEqual("FlushedName", rig.Service.Profile.Data.displayName ?? string.Empty);
        var cloudAfter = provider.LoadAsync<PlayerProfileData>(UGSKeys.PlayerProfile).GetAwaiter().GetResult();
        Assert.NotEqual("FlushedName", cloudAfter.displayName ?? string.Empty);
    }
}
