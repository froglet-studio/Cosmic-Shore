using System;
using System.Reflection;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Engine.Services;
using CosmicShore.Engine.Soap;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Bootstrap arc — AuthenticationServiceFacade driven LIVE through the grown
// engine auth shim (init coalescing, anonymous sign-in with the double-raise
// dedup, the cached-session three-branch flow, sign-out, the failure path),
// the full ApplicationLifecycleManager's dual static+SOAP raise pipeline and
// scene bridge, and the BootstrapConfigSO defaults.
// ─────────────────────────────────────────────────────────────────────────────

public class BootstrapArcTests : IDisposable
{
    readonly GameLoop loop = new(nameof(BootstrapArcTests));

    public BootstrapArcTests()
    {
        AuthenticationService.Reset();
        UnityServices.Reset();
    }

    public void Dispose()
    {
        AuthenticationService.Reset();
        UnityServices.Reset();
        // IsQuitting leaks into Singleton<T>.Awake's duplicate handling — always restore.
        typeof(ApplicationLifecycleManager)
            .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, null);
        loop.Dispose();
    }

    static void Set(object target, string field, object value)
        => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);

    // ── AuthenticationServiceFacade ─────────────────────────────────────

    sealed class AuthRig
    {
        public AuthenticationServiceFacade Facade;
        public AuthenticationData Data;
        public int SignedIn, SignedOut, Failed;
    }

    AuthRig MakeAuthRig()
    {
        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        authVar.Value = new AuthenticationData
        {
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnSignedOut = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnSignInFailed = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        };

        var rig = new AuthRig { Facade = new AuthenticationServiceFacade(authVar, allowLog: false), Data = authVar.Value };
        rig.Data.OnSignedIn.OnRaised += () => rig.SignedIn++;
        rig.Data.OnSignedOut.OnRaised += () => rig.SignedOut++;
        rig.Data.OnSignInFailed.OnRaised += () => rig.Failed++;
        return rig;
    }

    [Fact]
    public void StartAuthentication_SignsInAnonymously_RaisingOnSignedInOnce()
    {
        var rig = MakeAuthRig();

        rig.Facade.StartAuthentication();

        // Shim paths complete synchronously: Initializing → Ready → SigningIn → SignedIn.
        Assert.Equal(AuthenticationData.AuthState.SignedIn, rig.Data.State);
        Assert.True(rig.Data.IsSignedIn);
        Assert.Equal("local-player", rig.Data.PlayerId);
        Assert.True(rig.Facade.IsSignedIn);
        Assert.True(rig.Facade.SessionTokenExists);

        // The shim's SignedIn event AND the awaited completion both call
        // OnSignInSuccess — the dedup keeps the SOAP raise at exactly one.
        Assert.Equal(1, rig.SignedIn);

        // The startup guard makes a second call a no-op.
        rig.Facade.StartAuthentication();
        Assert.Equal(1, rig.SignedIn);
    }

    [Fact]
    public async Task TrySignInCached_CoversAllThreeBranches()
    {
        var rig = MakeAuthRig();

        // No cached token → false, still signed out.
        Assert.False(await rig.Facade.TrySignInCachedAsync());
        Assert.False(rig.Data.IsSignedIn);

        // Cached token → silent re-auth succeeds.
        AuthenticationService.Instance.SessionTokenExists = true;
        Assert.True(await rig.Facade.TrySignInCachedAsync());
        Assert.Equal(AuthenticationData.AuthState.SignedIn, rig.Data.State);
        Assert.Equal(1, rig.SignedIn);

        // Already signed in → immediate true (dedup: no second raise).
        Assert.True(await rig.Facade.TrySignInCachedAsync());
        Assert.Equal(1, rig.SignedIn);
    }

    [Fact]
    public void SignOut_ResetsStateAndClearsToken()
    {
        var rig = MakeAuthRig();
        rig.Facade.StartAuthentication();
        Assert.True(rig.Facade.SessionTokenExists);

        rig.Facade.SignOut(clearSessionToken: true);

        Assert.Equal(AuthenticationData.AuthState.Ready, rig.Data.State);
        Assert.False(rig.Data.IsSignedIn);
        Assert.Equal(string.Empty, rig.Data.PlayerId);
        Assert.False(rig.Facade.SessionTokenExists);
        // The shim's SignedOut event and the manual path both land in OnSignedOut;
        // upstream accepts the double notification (no dedup on sign-out).
        Assert.True(rig.SignedOut >= 1);
    }

    sealed class FailingAuthService : AuthenticationService
    {
        public override Task SignInAnonymouslyAsync()
            => throw new RequestFailedException(401, "invalid credentials");
    }

    [Fact]
    public void SignInFailure_LandsInFailedState_AndRaisesOnSignInFailed()
    {
        var rig = MakeAuthRig();
        AuthenticationService.Instance = new FailingAuthService();

        rig.Facade.StartAuthentication();

        Assert.Equal(AuthenticationData.AuthState.Failed, rig.Data.State);
        Assert.False(rig.Data.IsSignedIn);
        Assert.Equal(1, rig.Failed);
        Assert.Equal(0, rig.SignedIn);

        // ResetStartupState lets a later retry succeed.
        AuthenticationService.Reset();
        rig.Facade.ResetStartupState();
        rig.Facade.StartAuthentication();
        Assert.Equal(AuthenticationData.AuthState.SignedIn, rig.Data.State);
        Assert.Equal(1, rig.SignedIn);
    }

    // ── ApplicationLifecycleManager ─────────────────────────────────────

    [Fact]
    public void Lifecycle_RaisesStaticAndSoapPipelines_AndBridgesSceneEvents()
    {
        var container = ScriptableObject.CreateInstance<ApplicationLifecycleEventsContainerSO>();
        container.OnAppPaused = ScriptableObject.CreateInstance<ScriptableEventBool>();
        container.OnAppFocusChanged = ScriptableObject.CreateInstance<ScriptableEventBool>();
        container.OnAppQuitting = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        container.OnSceneLoaded = ScriptableObject.CreateInstance<ScriptableEventString>();
        container.OnSceneUnloading = ScriptableObject.CreateInstance<ScriptableEventString>();

        var manager = new GameObject("lifecycle").AddComponent<ApplicationLifecycleManager>();
        Set(manager, "_lifecycleEvents", container);
        loop.Tick(1f / 60f); // OnEnable subscribes the scene bridge

        bool? staticPaused = null; bool? soapPaused = null;
        int staticQuit = 0, soapQuit = 0;
        string soapLoaded = null; Scene staticLoaded = null;
        ApplicationLifecycleManager.OnAppPaused += p => staticPaused = p;
        ApplicationLifecycleManager.OnAppQuitting += () => staticQuit++;
        ApplicationLifecycleManager.OnSceneLoaded += (s, _) => staticLoaded = s;
        container.OnAppPaused.OnRaised += p => soapPaused = p;
        container.OnAppQuitting.OnRaised += () => soapQuit++;
        container.OnSceneLoaded.OnRaised += name => soapLoaded = name;

        // OS pause message → both pipelines.
        typeof(ApplicationLifecycleManager)
            .GetMethod("OnApplicationPause", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, new object[] { true });
        Assert.True(staticPaused);
        Assert.True(soapPaused);

        // Scene bridge: engine announce → static event + SOAP string raise.
        SceneManager.NotifySceneLoaded("Menu_Main", LoadSceneMode.Single);
        Assert.Equal("Menu_Main", staticLoaded?.name);
        Assert.Equal("Menu_Main", soapLoaded);

        // OS quit message → IsQuitting latches + both pipelines.
        Assert.False(ApplicationLifecycleManager.IsQuitting);
        typeof(ApplicationLifecycleManager)
            .GetMethod("OnApplicationQuit", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, null);
        Assert.True(ApplicationLifecycleManager.IsQuitting);
        Assert.Equal(1, staticQuit);
        Assert.Equal(1, soapQuit);
    }

    // ── BootstrapConfigSO ───────────────────────────────────────────────

    [Fact]
    public void BootstrapConfig_CarriesTheUpstreamDefaults()
    {
        var config = ScriptableObject.CreateInstance<BootstrapConfigSO>();

        Assert.Equal(15f, config.ServiceInitTimeoutSeconds);
        Assert.Equal(1f, config.MinimumSplashDuration);
        Assert.Equal(60, config.TargetFrameRate);
        Assert.True(config.PreventScreenSleep);
        Assert.Equal(0, config.VSyncCount);
        Assert.False(config.VerboseLogging);
    }
}
