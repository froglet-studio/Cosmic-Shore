using System;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine.UI;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Bootstrap-arc remainder — SplashToAuthFlow's hold-then-route, and
// AuthenticationSceneController LIVE end to end: the already-signed-in
// auto-skip through the networked Menu_Main load, the auth-panel fork + guest
// login, the username-setup fork + confirm, and WaitForRelayReadyAsync's
// dual-condition gate (OnHostConnectionEstablished AND NM.IsListening).
// ─────────────────────────────────────────────────────────────────────────────

public class AuthFlowTests : IDisposable
{
    readonly GameLoop loop = new(nameof(AuthFlowTests));

    public AuthFlowTests()
    {
        AuthenticationService.Reset();
        UnityServices.Reset();
        NetworkManager.Singleton = null;
    }

    public void Dispose()
    {
        AuthenticationService.Reset();
        UnityServices.Reset();
        NetworkManager.Singleton = null;
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

    static AuthenticationDataVariable MakeAuthVar(out AuthenticationData data)
    {
        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        data = ForceValue(authVar, new AuthenticationData
        {
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnSignedOut = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnSignInFailed = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        });
        return authVar;
    }

    // ── SplashToAuthFlow ────────────────────────────────────────────────

    [Fact]
    public void Splash_HoldsThenRoutesThroughTheAuthScene()
    {
        var stm = new GameObject("stm").AddComponent<SceneTransitionManager>();
        var authVar = MakeAuthVar(out var authData);
        authData.State = AuthenticationData.AuthState.SignedIn;
        authData.IsSignedIn = true;

        var go = new GameObject("splash-flow");
        go.SetActive(false);
        var flow = go.AddComponent<SplashToAuthFlow>();
        Set(flow, "splashDisplayDuration", 0.05f); // keep the hold short for the test
        Set(flow, "authenticationDataVariable", authVar);
        Set(flow, "_sceneNames", ScriptableObject.CreateInstance<SceneNameListSO>());
        Set(flow, "_sceneTransitionManager", stm);
        go.SetActive(true);

        // Even signed in, the flow ALWAYS routes through the Authentication scene
        // (the network host must start there before Menu_Main can load via Netcode).
        Pump(() => SceneManager.GetActiveScene().name == "Authentication");
    }

    // ── AuthenticationSceneController ───────────────────────────────────

    sealed class Rig
    {
        public AuthenticationSceneController Controller;
        public AuthenticationServiceFacade Facade;
        public AuthenticationData AuthData;
        public ApplicationStateData AppState;
        public HostConnectionDataSO Conn;
        public SceneTransitionManager Stm;
        public GameObject AuthPanel;
        public GameObject UsernamePanel;
        public Button GuestButton;
        public Button ConfirmButton;
        public TMP_InputField UsernameInput;
        public PlayerDataService PlayerData;
    }

    Rig MakeRig(bool withPanels = false, PlayerDataService playerData = null)
    {
        var stm = new GameObject("stm").AddComponent<SceneTransitionManager>();

        var authVar = MakeAuthVar(out var authData);
        var facade = new AuthenticationServiceFacade(authVar, allowLog: false);

        var appStateVar = ScriptableObject.CreateInstance<ApplicationStateDataVariable>();
        var appState = ForceValue(appStateVar, new ApplicationStateData
        {
            OnStateChanged = ScriptableObject.CreateInstance<ScriptableEventApplicationState>(),
        });
        // The auth scene only performs Authenticating → MainMenu; seed the prior phase.
        appState.State = ApplicationState.Authenticating;

        var conn = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        conn.OnHostConnectionEstablished = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var go = new GameObject("auth-scene-controller");
        go.SetActive(false);
        var controller = go.AddComponent<AuthenticationSceneController>();
        Set(controller, "_facade", facade);
        Set(controller, "_authDataVariable", authVar);
        Set(controller, "_playerDataService", playerData);
        Set(controller, "_sceneNames", ScriptableObject.CreateInstance<SceneNameListSO>());
        Set(controller, "_sceneTransitionManager", stm);
        Set(controller, "_appStateMachine", new ApplicationStateMachine(appStateVar, null, null, allowLog: false));
        Set(controller, "_connectionData", conn);

        var rig = new Rig
        {
            Controller = controller, Facade = facade, AuthData = authData,
            AppState = appState, Conn = conn, Stm = stm, PlayerData = playerData,
        };

        if (withPanels)
        {
            // Panels start inactive (scene authoring) so activeSelf reflects the
            // controller's ShowAuthPanel/ShowUsernameSetup decisions, not defaults.
            rig.AuthPanel = new GameObject("auth-panel");
            rig.AuthPanel.SetActive(false);
            rig.UsernamePanel = new GameObject("username-panel");
            rig.UsernamePanel.SetActive(false);
            rig.GuestButton = new GameObject("guest-button").AddComponent<Button>();
            rig.ConfirmButton = new GameObject("confirm-button").AddComponent<Button>();
            rig.UsernameInput = new GameObject("username-input").AddComponent<TMP_InputField>();
            Set(controller, "authPanel", rig.AuthPanel);
            Set(controller, "usernameSetupPanel", rig.UsernamePanel);
            Set(controller, "guestLoginButton", rig.GuestButton);
            Set(controller, "confirmUsernameButton", rig.ConfirmButton);
            Set(controller, "usernameInputField", rig.UsernameInput);
        }

        go.SetActive(true); // OnEnable (button wiring) + Start (auth flow) on the next tick
        return rig;
    }

    [Fact]
    public void AlreadySignedIn_AutoSkips_ToTheNetworkedMenuLoad()
    {
        AuthenticationService.Instance.IsSignedIn = true;
        AuthenticationService.Instance.PlayerId = "cached-player";
        var nm = new GameObject("nm").AddComponent<NetworkManager>(); // IsListening=true default
        NetworkManager.Singleton = nm;

        var rig = MakeRig();

        Pump(() => SceneManager.GetActiveScene().name == "Menu_Main");
        Assert.Equal(ApplicationState.MainMenu, rig.AppState.State);

        // The splash stays opaque through the transition — SceneLoader releases it
        // on OnClientReady once the menu vessel spawns.
        var overlay = typeof(SceneTransitionManager)
            .GetField("_fadeCanvasGroup", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(rig.Stm) as CanvasGroup;
        Assert.Equal(1f, overlay.alpha);
    }

    [Fact]
    public void NoPanel_AutoSignsInAnonymously_ThenNavigates()
    {
        NetworkManager.Singleton = new GameObject("nm").AddComponent<NetworkManager>();

        var rig = MakeRig(); // signed out, no cached token, no auth panel

        Pump(() => SceneManager.GetActiveScene().name == "Menu_Main");
        Assert.True(rig.AuthData.IsSignedIn);
        Assert.Equal("local-player", rig.AuthData.PlayerId);
        Assert.Equal(ApplicationState.MainMenu, rig.AppState.State);
    }

    [Fact]
    public void WithPanel_ShowsIt_AndGuestLoginNavigates()
    {
        NetworkManager.Singleton = new GameObject("nm").AddComponent<NetworkManager>();

        var rig = MakeRig(withPanels: true); // signed out → the panel fork

        Pump(() => rig.AuthPanel.activeSelf);
        Assert.False(rig.UsernamePanel.activeSelf);
        Assert.False(rig.AuthData.IsSignedIn); // waiting on the user

        // Guest login button → anonymous sign-in → post-auth → networked menu load.
        rig.GuestButton.onClick.Invoke();
        Pump(() => SceneManager.GetActiveScene().name == "Menu_Main");
        Assert.True(rig.AuthData.IsSignedIn);
    }

    [Fact]
    public void UsernameNeeded_ShowsSetup_AndConfirmNavigates()
    {
        AuthenticationService.Instance.IsSignedIn = true; // auto-skip path
        NetworkManager.Singleton = new GameObject("nm").AddComponent<NetworkManager>();

        // A profile still carrying the auto-assigned "Pilot####" name needs setup.
        var playerData = new GameObject("player-data").AddComponent<PlayerDataService>();
        typeof(PlayerDataService).GetProperty("IsInitialized")!.SetValue(playerData, true);
        typeof(PlayerDataService).GetProperty("CurrentProfile")!
            .SetValue(playerData, new PlayerProfileData { displayName = "Pilot9898" });

        var rig = MakeRig(withPanels: true, playerData: playerData);

        Pump(() => rig.UsernamePanel.activeSelf);
        Assert.False(rig.AuthPanel.activeSelf);

        // Too-short name is rejected without navigating.
        rig.UsernameInput.text = "ab";
        rig.ConfirmButton.onClick.Invoke();
        loop.Tick(1f / 60f);
        Assert.NotEqual("Menu_Main", SceneManager.GetActiveScene().name);

        // Valid name: persisted locally + pushed to the UGS shim, then navigate.
        rig.UsernameInput.text = "dragon";
        rig.ConfirmButton.onClick.Invoke();
        Pump(() => SceneManager.GetActiveScene().name == "Menu_Main");
        Assert.Equal("dragon", playerData.CurrentProfile.displayName);
        Assert.Equal("dragon", AuthenticationService.Instance.PlayerName);
    }

    [Fact]
    public void RelayGate_OnlyOpensWhenEstablishedFires_WithNetcodeListening()
    {
        AuthenticationService.Instance.IsSignedIn = true;
        var nm = new GameObject("nm").AddComponent<NetworkManager>();
        nm.IsListening = false; // lobby joined, Relay not up yet
        NetworkManager.Singleton = nm;

        var rig = MakeRig();
        for (int i = 0; i < 30; i++) loop.Tick(1f / 60f);

        // First fire (lobby join, NM not listening) must be ignored.
        rig.Conn.OnHostConnectionEstablished.Raise();
        for (int i = 0; i < 30; i++) loop.Tick(1f / 60f);
        Assert.NotEqual("Menu_Main", SceneManager.GetActiveScene().name);

        // Second fire with the Relay host actually listening opens the gate.
        nm.IsListening = true;
        rig.Conn.OnHostConnectionEstablished.Raise();
        Pump(() => SceneManager.GetActiveScene().name == "Menu_Main");
        Assert.Equal(ApplicationState.MainMenu, rig.AppState.State);
    }
}
