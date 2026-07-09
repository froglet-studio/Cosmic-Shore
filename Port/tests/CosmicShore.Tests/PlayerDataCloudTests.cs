using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// #14 consumers LIVE against the ported UGSDataService — PlayerDataService's
// cloud-profile merge / push-defaults / write-back, and GameSetting's
// cloud-settings apply on top of PlayerPrefs. These were the last carried
// services-phase regions; they now run through the real CloudData repositories.
// ─────────────────────────────────────────────────────────────────────────────

public class PlayerDataCloudTests : IDisposable
{
    readonly GameLoop loop = new(nameof(PlayerDataCloudTests));

    public PlayerDataCloudTests()
    {
        CloudSaveService.Reset();
        AuthenticationService.Reset();
        UnityServices.Reset();
        ResetSingletons();
        UnityServices.State = ServicesInitializationState.Initialized;
        AuthenticationService.Instance.IsSignedIn = true;
        AuthenticationService.Instance.PlayerId = "auth-player-id";
    }

    public void Dispose()
    {
        ResetSingletons();
        CloudSaveService.Reset();
        AuthenticationService.Reset();
        UnityServices.Reset();
        loop.Dispose();
    }

    static void ResetSingletons()
    {
        NullStaticAutoProp(typeof(PlayerDataService), "Instance");
        NullStaticAutoProp(typeof(UGSDataService), "Instance");
        NullStaticAutoProp(typeof(SingletonPersistent<GameSetting>), "Instance");
    }

    static void NullStaticAutoProp(Type t, string name)
    {
        var f = t.GetField($"<{name}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        f?.SetValue(null, null);
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

    /// <summary>Builds an initialized, signed-in UGSDataService (repos loaded from the
    /// current LocalCloudSaveService store). Pre-seed the store BEFORE calling this.</summary>
    UGSDataService MakeInitializedDataService()
    {
        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        ForceValue(authVar, new AuthenticationData
        {
            IsSignedIn = true,
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        });

        var go = new GameObject("ugs-data-service");
        go.SetActive(false);
        var service = go.AddComponent<UGSDataService>();
        Set(service, "_authData", authVar);
        go.SetActive(true); // Awake creates repos; Start (next tick) drives init off IsSignedIn

        Pump(() => service.IsInitialized);
        return service;
    }

    static void SeedCloud(string key, object data)
        => CloudSaveService.Instance.Data.Player
            .SaveAsync(new Dictionary<string, object> { { key, data } })
            .GetAwaiter().GetResult();

    // ── PlayerDataService ───────────────────────────────────────────────

    PlayerDataService MakePlayerDataService(UGSDataService ds, out GameDataSO gameData)
    {
        gameData = ScriptableObject.CreateInstance<GameDataSO>();
        var go = new GameObject("player-data-service");
        go.SetActive(false);
        var pds = go.AddComponent<PlayerDataService>();
        Set(pds, "gameData", gameData);
        Set(pds, "_ugsDataService", ds);
        go.SetActive(true); // Awake: local default profile; Start (tick): merge fork
        return pds;
    }

    [Fact]
    public void ReturningPlayer_MergesTheCloudProfile_OverLocalDefaults()
    {
        var cloud = new PlayerProfileData
        {
            userId = "cloud-user",
            displayName = "CloudPilot",
            avatarId = 5,
            crystalBalance = 250,
            xp = 1200,
            unlockedRewardIds = new List<string> { "reward_a" },
        };
        SeedCloud(UGSKeys.PlayerProfile, cloud);
        var ds = MakeInitializedDataService();

        var pds = MakePlayerDataService(ds, out var gameData);
        Pump(() => pds.IsInitialized);

        // The cloud record replaced the local "Pilot####" default.
        Assert.Equal("CloudPilot", pds.CurrentProfile.displayName);
        Assert.Equal(5, pds.CurrentProfile.avatarId);
        Assert.Equal(250, pds.CurrentProfile.crystalBalance);
        Assert.Equal(1200, pds.CurrentProfile.xp);
        // userId is refreshed from auth during the merge.
        Assert.Equal("auth-player-id", pds.CurrentProfile.userId);
        // OnProfileChanged → gameData mirror is live.
        Assert.Equal("CloudPilot", gameData.LocalPlayerDisplayName);
        Assert.Equal(5, gameData.LocalPlayerAvatarId);
    }

    [Fact]
    public void NewPlayer_PushesLocalDefaultsToTheCloudRepo_AndMarksDirty()
    {
        var ds = MakeInitializedDataService(); // empty cloud → repo has a blank profile

        var pds = MakePlayerDataService(ds, out _);
        Pump(() => pds.IsInitialized);

        // MergeCloudProfile saw an empty (no-userId) cloud record and pushed our local
        // defaults into the repo via SyncCurrentProfileToRepo, marking it dirty.
        Assert.Equal(pds.CurrentProfile.displayName, ds.ProfileRepo.Data.displayName);
        Assert.StartsWith("Pilot", ds.ProfileRepo.Data.displayName);
        Assert.True(ds.ProfileRepo.IsDirty);
    }

    [Fact]
    public void SetDisplayName_WritesThroughToTheCloudRepo()
    {
        var ds = MakeInitializedDataService();
        var pds = MakePlayerDataService(ds, out _);
        Pump(() => pds.IsInitialized);

        pds.SetDisplayName("Renamed");

        Assert.Equal("Renamed", pds.CurrentProfile.displayName);
        Assert.Equal("Renamed", ds.ProfileRepo.Data.displayName); // ScheduleSave → repo
        Assert.True(ds.ProfileRepo.IsDirty);
    }

    [Fact]
    public void NoDataService_FallsBackToLocalProfile_WithoutThrowing()
    {
        // The injected service is absent (e.g. instantiated outside a ContainerScope).
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        var go = new GameObject("player-data-service");
        go.SetActive(false);
        var pds = go.AddComponent<PlayerDataService>();
        Set(pds, "gameData", gameData);
        go.SetActive(true);
        for (int i = 0; i < 5; i++) loop.Tick(1f / 60f);

        // Start's null-guard kept the local default; init never completes but nothing throws.
        Assert.False(pds.IsInitialized);
        Assert.StartsWith("Pilot", pds.CurrentProfile.displayName);
    }

    // ── GameSetting ─────────────────────────────────────────────────────

    [Fact]
    public void GameSetting_AppliesCloudSettings_OnTopOfLocalDefaults()
    {
        var cloudSettings = new PlayerSettingsCloudData
        {
            MusicEnabled = false,
            SFXEnabled = false,
            MusicLevel = 0.3f,
            HapticsLevel = 0.7f,
            InvertYEnabled = true,
        };
        SeedCloud(UGSKeys.PlayerSettings, cloudSettings);
        var ds = MakeInitializedDataService();

        var go = new GameObject("game-setting");
        go.SetActive(false);
        var settings = go.AddComponent<GameSetting>();
        Set(settings, "_ugsDataService", ds);
        go.SetActive(true); // Awake: PlayerPrefs load, then the cloud fork (service is initialized)

        // Cloud values won over the local PlayerPrefs defaults.
        Assert.False(settings.MusicEnabled);
        Assert.False(settings.SFXEnabled);
        Assert.Equal(0.3f, settings.MusicLevel);
        Assert.Equal(0.7f, settings.HapticsLevel);
        Assert.True(settings.InvertYEnabled);
    }

    [Fact]
    public void GameSetting_ChangeSetting_SyncsToTheCloudRepo()
    {
        var ds = MakeInitializedDataService();
        var go = new GameObject("game-setting");
        go.SetActive(false);
        var settings = go.AddComponent<GameSetting>();
        Set(settings, "_ugsDataService", ds);
        go.SetActive(true);

        bool before = settings.MusicEnabled;
        settings.ChangeMusicEnabledSetting();

        Assert.Equal(!before, settings.MusicEnabled);
        Assert.Equal(!before, ds.SettingsRepo.Data.MusicEnabled); // SyncToCloud wrote through
        Assert.True(ds.SettingsRepo.IsDirty);
    }
}
