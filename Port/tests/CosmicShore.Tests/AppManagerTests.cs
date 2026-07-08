using System;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// AppManager — the DI root, LIVE end to end: InstallBindings registering the
// full surface (assets, the fifteen manager singletons, the pure-C# service
// quartet + analytics + tournament, the nine party-ring factories), and the
// bootstrap sequence itself — platform config, game-data defaults, network
// monitor + auth startup, the splash hold, and the Authentication-scene
// handoff through SceneTransitionManager. Plus the NetworkMonitor reachability
// flip and the bootstrapped-once duplicate guard.
// ─────────────────────────────────────────────────────────────────────────────

public class AppManagerTests : IDisposable
{
    readonly GameLoop loop = new(nameof(AppManagerTests));

    public AppManagerTests()
    {
        ResetAppManagerStatics();
        AuthenticationService.Reset();
        UnityServices.Reset();
        NetworkManager.Singleton = null;
        Application.internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork;
    }

    public void Dispose()
    {
        ResetAppManagerStatics();
        AuthenticationService.Reset();
        UnityServices.Reset();
        NetworkManager.Singleton = null;
        Application.internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork;
        SceneManager.ResetSceneLoadedSubscribers(); // TournamentController subscribes for app lifetime
        typeof(TournamentController).GetProperty("Instance")!.SetValue(null, null);
        typeof(ApplicationLifecycleManager)
            .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, null);
        loop.Dispose();
    }

    static void ResetAppManagerStatics()
        => typeof(AppManager)
            .GetMethod("EnsureBootstrapOnStartup", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, null); // the tagged domain-reload reset — harnesses call it directly

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

    sealed class Rig
    {
        public AppManager App;
        public Container Container;
        public GameDataSO GameData;
        public AuthenticationData AuthData;
        public ApplicationStateData AppState;
        public NetworkMonitorData NetData;
        public SceneTransitionManager Stm;
        public BootstrapConfigSO Config;
    }

    Rig MakeRig()
    {
        // Managers pre-placed in the "Bootstrap scene" — AppManager early-resolves
        // them via the scene search and stamps them persistent.
        var stm = new GameObject("scene-transition-manager").AddComponent<SceneTransitionManager>();

        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnLaunchGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnSessionStarted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnMiniGameEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>(); // TournamentController subscribes in its ctor
        gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
        gameData.VesselClassSelectedIndex = ScriptableObject.CreateInstance<IntVariable>(); // ResetAllData writes it
        gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
        gameData.SelectedPlayerCount = ScriptableObject.CreateInstance<IntVariable>();

        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        var authData = ForceValue(authVar, new AuthenticationData
        {
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnSignedOut = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnSignInFailed = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        });

        var netVar = ScriptableObject.CreateInstance<NetworkMonitorDataVariable>();
        var netData = ForceValue(netVar, new NetworkMonitorData
        {
            OnNetworkLost = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnNetworkFound = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        });

        var appStateVar = ScriptableObject.CreateInstance<ApplicationStateDataVariable>();
        var appState = ForceValue(appStateVar, new ApplicationStateData
        {
            OnStateChanged = ScriptableObject.CreateInstance<ScriptableEventApplicationState>(),
        });

        var config = ScriptableObject.CreateInstance<BootstrapConfigSO>();

        // Awake runs at AddComponent time on an ACTIVE GameObject (engine lifecycle),
        // so stage the composition root inactive, configure its serialized fields,
        // and activate after injection — Awake then sees the wired config, matching
        // the Unity flow where serialized fields deserialize before Awake.
        var go = new GameObject("app-manager");
        go.SetActive(false);
        var app = go.AddComponent<AppManager>();
        Set(app, "_bootstrapConfig", config);
        Set(app, "_sceneNames", ScriptableObject.CreateInstance<SceneNameListSO>()); // "Authentication" default
        Set(app, "authenticationDataVariable", authVar);
        Set(app, "networkMonitorDataVariable", netVar);
        Set(app, "gameData", gameData);
        Set(app, "friendsData", ScriptableObject.CreateInstance<FriendsDataSO>());
        Set(app, "hostConnectionData", ScriptableObject.CreateInstance<HostConnectionDataSO>());
        Set(app, "gameList", ScriptableObject.CreateInstance<SO_GameList>());
        Set(app, "tournamentData", ScriptableObject.CreateInstance<TournamentDataSO>());
        Set(app, "menuFreestyleEvents", ScriptableObject.CreateInstance<MenuFreestyleEventsContainerSO>());
        Set(app, "lifecycleEvents", ScriptableObject.CreateInstance<ApplicationLifecycleEventsContainerSO>());
        Set(app, "applicationStateDataVariable", appStateVar);

        // The host flow: install into a builder, build the root scope, inject the
        // composition root (upstream Reflex does exactly this between Awake and Start;
        // the engine runs Awake+Start on the first tick with fields already injected,
        // satisfying the "access injected fields in Start or later" contract).
        var builder = new ContainerBuilder();
        ((IInstaller)app).InstallBindings(builder);
        var container = builder.Build();
        container.Inject(app);

        go.SetActive(true); // Awake fires now, with fields + injections in place

        return new Rig
        {
            App = app, Container = container, GameData = gameData,
            AuthData = authData, AppState = appState, NetData = netData,
            Stm = stm, Config = config,
        };
    }

    [Fact]
    public void InstallBindings_RegistersTheFullSurface()
    {
        var rig = MakeRig();
        var c = rig.Container;

        // Assets.
        Assert.Same(rig.GameData, c.Resolve<GameDataSO>());
        Assert.NotNull(c.Resolve<SceneNameListSO>());
        Assert.NotNull(c.Resolve<AuthenticationDataVariable>());
        Assert.NotNull(c.Resolve<NetworkMonitorDataVariable>());
        Assert.NotNull(c.Resolve<FriendsDataSO>());
        Assert.NotNull(c.Resolve<HostConnectionDataSO>());
        Assert.NotNull(c.Resolve<SO_GameList>());
        Assert.NotNull(c.Resolve<TournamentDataSO>());
        Assert.NotNull(c.Resolve<ApplicationStateDataVariable>());

        // Manager singletons — the pre-placed STM resolves to the scene instance;
        // the shelled family resolves through the deferred scene search or errors
        // gracefully (null) when absent, per upstream's fail-loud log + null.
        Assert.Same(rig.Stm, c.Resolve<SceneTransitionManager>());

        // Pure C# services (quartet + analytics + tournament).
        Assert.NotNull(c.Resolve<AuthenticationServiceFacade>());
        Assert.NotNull(c.Resolve<NetworkMonitor>());
        Assert.NotNull(c.Resolve<FriendsServiceFacade>());
        Assert.NotNull(c.Resolve<ApplicationStateMachine>());
        Assert.NotNull(c.Resolve<AnalyticsServiceFacade>());
        Assert.NotNull(c.Resolve<TournamentController>());

        // The party ring — interface bindings + concrete services.
        Assert.NotNull(c.Resolve<IPresenceLobbyService>());
        Assert.NotNull(c.Resolve<IPartySessionService>());
        Assert.NotNull(c.Resolve<IPartyMemberService>());
        Assert.NotNull(c.Resolve<INetworkTransitionService>());
        Assert.NotNull(c.Resolve<LobbyPropertyWriter>());
        Assert.NotNull(c.Resolve<SoapPartyEventBus>());
        Assert.NotNull(c.Resolve<LobbyRefreshScheduler>());
        Assert.NotNull(c.Resolve<InviteService>());
        Assert.NotNull(c.Resolve<AcceptanceSignalService>());

        // Singletons: same instance on re-resolve.
        Assert.Same(c.Resolve<ApplicationStateMachine>(), c.Resolve<ApplicationStateMachine>());
    }

    [Fact]
    public void Bootstrap_RunsToTheAuthenticationHandoff()
    {
        var rig = MakeRig();
        int bootstrapCompleted = 0;
        AppManager.OnBootstrapComplete += () => bootstrapCompleted++;
        int signedIn = 0;
        rig.AuthData.OnSignedIn.OnRaised += () => signedIn++;

        loop.Tick(1f / 60f); // Awake (platform config) + Start (state machine, services, bootstrap kick)

        // Platform config applied from BootstrapConfigSO.
        Assert.Equal(60, Application.targetFrameRate);
        Assert.Equal(SleepTimeout.NeverSleep, Screen.sleepTimeout);

        // Start(): game-data menu defaults + auth + network monitor are live.
        Assert.Equal(1, rig.GameData.SelectedPlayerCount.Value);
        Assert.Equal(VesselClassType.Squirrel, rig.GameData.selectedVesselClass.Value);
        Assert.Equal(1, rig.GameData.SelectedIntensity.Value);
        Assert.Equal(1, signedIn);
        Assert.True(rig.AuthData.IsSignedIn);
        Assert.True(rig.NetData.IsOnline);
        Assert.Equal(ApplicationState.Bootstrapping, rig.AppState.State);

        // The splash hold (MinimumSplashDuration = 1s unscaled) → handoff.
        for (int i = 0; i < 300 && !AppManager.HasBootstrapped; i++) loop.Tick(1f / 60f);
        Assert.True(AppManager.HasBootstrapped);
        Assert.Equal(1, bootstrapCompleted);

        // Authenticating + the Authentication scene actually loaded through STM.
        for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != "Authentication"; i++) loop.Tick(1f / 60f);
        Assert.Equal(ApplicationState.Authenticating, rig.AppState.State);
        Assert.Equal("Authentication", SceneManager.GetActiveScene().name);
    }

    [Fact]
    public void NetworkMonitor_RaisesLostAndFound_OnReachabilityFlips()
    {
        var rig = MakeRig();
        int lost = 0, found = 0;
        rig.NetData.OnNetworkLost.OnRaised += () => lost++;
        rig.NetData.OnNetworkFound.OnRaised += () => found++;

        var monitor = rig.Container.Resolve<NetworkMonitor>();
        monitor.StartMonitoring(intervalSeconds: 1);

        Application.internetReachability = NetworkReachability.NotReachable;
        for (int i = 0; i < 90 && lost == 0; i++) loop.Tick(1f / 60f);
        Assert.Equal(1, lost);
        Assert.False(rig.NetData.IsOnline);

        Application.internetReachability = NetworkReachability.ReachableViaLocalAreaNetwork;
        for (int i = 0; i < 90 && found == 0; i++) loop.Tick(1f / 60f);
        Assert.Equal(1, found);
        Assert.True(rig.NetData.IsOnline);

        monitor.StopMonitoring();
    }

    [Fact]
    public void SecondAppManager_AfterBootstrap_DestroysItself()
    {
        typeof(AppManager)
            .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, true);

        var go = new GameObject("duplicate-app-manager");
        go.AddComponent<AppManager>();
        loop.Tick(1f / 60f); // Awake takes the already-bootstrapped guard → Destroy
        loop.Tick(1f / 60f); // destruction applies at end of frame

        Assert.True(go.IsDestroyed);
    }
}
