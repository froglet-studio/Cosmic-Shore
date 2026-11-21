using System;
using System.Reflection;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Engine.Soap;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// UI-shell arc — the LIVE surfaces of ToastChannel (unified event payloads),
// SceneTransitionManager (programmatic CanvasGroup overlay, immediate fades,
// the local load flow, and the Netcode-placeholder network load), and
// SceneLoader (launch: splash cover → client-defer guard → load → fade on
// OnClientReady; return-to-menu; the idempotent splash re-arm).
// ─────────────────────────────────────────────────────────────────────────────

public class UiShellTests : IDisposable
{
    readonly GameLoop loop = new(nameof(UiShellTests));

    public UiShellTests() => NetworkManager.Singleton = null;

    public void Dispose()
    {
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

    static T Get<T>(object target, string field)
        => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(target);

    sealed class TickDriver : MonoBehaviour
    {
        public Action Action;
        void Update() { var a = Action; Action = null; a?.Invoke(); }
    }

    void Drive(Action action, int pumpFrames = 0)
    {
        var driver = new GameObject("driver").AddComponent<TickDriver>();
        driver.Action = action;
        loop.Tick(1f / 60f);
        for (int i = 0; i < pumpFrames; i++) loop.Tick(1f / 60f);
    }

    void PumpUntil(Func<bool> done, int maxFrames = 600)
    {
        for (int i = 0; i < maxFrames && !done(); i++) loop.Tick(1f / 60f);
        Assert.True(done(), "condition should settle within the pump budget");
    }

    // ── ToastChannel ────────────────────────────────────────────────────

    [Fact]
    public void Toast_HelpersRaiseTheUnifiedEvent()
    {
        var channel = ScriptableObject.CreateInstance<ToastChannel>();
        var requests = new System.Collections.Generic.List<(ChatToastRequest req, Action onDone)>();
        channel.OnChatToast += (req, onDone) => requests.Add((req, onDone));

        channel.ShowPrefix("Couldn't join — returned to your menu.");
        channel.ShowPrefixPostfix("Combo", "x2", duration: 1.5f, anim: ToastAnimation.Pop);
        bool countdownDone = false;
        channel.ShowCountdown("Overcharging", 3, "in {0}", onDone: () => countdownDone = true);

        Assert.Equal(3, requests.Count);

        Assert.Equal("Couldn't join — returned to your menu.", requests[0].req.Prefix);
        Assert.Equal(4.5f, requests[0].req.Duration);
        Assert.Equal(ToastAnimation.ChatSubtleSlide, requests[0].req.Animation);
        Assert.Null(requests[0].onDone);

        Assert.Equal("x2", requests[1].req.Postfix);
        Assert.Equal(ToastAnimation.Pop, requests[1].req.Animation);

        Assert.Equal(3, requests[2].req.PostfixCountdownFrom);
        Assert.Equal("in {0}", requests[2].req.PostfixCountdownFormat);
        requests[2].onDone();
        Assert.True(countdownDone);
    }

    // ── SceneTransitionManager ──────────────────────────────────────────

    SceneTransitionManager MakeStm()
    {
        var stm = new GameObject("scene-transition-manager").AddComponent<SceneTransitionManager>();
        loop.Tick(1f / 60f); // Awake → CreateFadeOverlay (no splash wired)
        return stm;
    }

    [Fact]
    public void Stm_ProgrammaticOverlay_AndImmediateFades()
    {
        var stm = MakeStm();
        var group = Get<CanvasGroup>(stm, "_fadeCanvasGroup");

        Assert.NotNull(group);
        Assert.Equal(0f, group.alpha);
        Assert.False(group.blocksRaycasts);

        stm.SetFadeImmediate(1f);
        Assert.Equal(1f, group.alpha);
        Assert.True(group.blocksRaycasts);
        Assert.True(group.interactable);

        stm.SetFadeImmediate(0f);
        Assert.Equal(0f, group.alpha);
        Assert.False(group.blocksRaycasts);
    }

    [Fact]
    public void Stm_LoadSceneAsync_FadesLoadsAndAnnounces()
    {
        var stm = MakeStm();
        var group = Get<CanvasGroup>(stm, "_fadeCanvasGroup");
        string completed = null;
        stm.OnSceneLoadComplete += name => completed = name;

        Task load = null;
        Drive(() => load = stm.LoadSceneAsync("Menu_Main"));
        PumpUntil(() => load.IsCompleted);

        Assert.Equal("Menu_Main", SceneManager.GetActiveScene().name);
        Assert.Equal("Menu_Main", completed);
        Assert.Equal(0f, group.alpha, 3);  // faded back in
        Assert.False(stm.IsTransitioning);
    }

    [Fact]
    public void Stm_LoadNetworkSceneAsync_ServerRoutesThroughTheNetcodePlaceholder()
    {
        var stm = MakeStm();
        NetworkManager.Singleton = new GameObject("nm").AddComponent<NetworkManager>(); // IsServer=true default

        Task load = null;
        Drive(() => load = stm.LoadNetworkSceneAsync("MinigameHexRace"));
        PumpUntil(() => load.IsCompleted);

        Assert.Equal("MinigameHexRace", SceneManager.GetActiveScene().name);
        Assert.False(stm.IsTransitioning);
    }

    // ── SceneLoader ─────────────────────────────────────────────────────

    sealed class LoaderRig
    {
        public SceneLoader Loader;
        public SceneTransitionManager Stm;
        public CanvasGroup Overlay;
        public GameDataSO GameData;
    }

    LoaderRig MakeLoaderRig()
    {
        var stm = new GameObject("scene-transition-manager").AddComponent<SceneTransitionManager>();

        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnLaunchGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
        gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
        gameData.SelectedPlayerCount = ScriptableObject.CreateInstance<IntVariable>();

        var loader = new GameObject("scene-loader").AddComponent<SceneLoader>();
        Set(loader, "gameData", gameData);
        Set(loader, "_sceneNames", ScriptableObject.CreateInstance<SceneNameListSO>()); // "Menu_Main" default
        Set(loader, "_sceneTransitionManager", stm);

        loop.Tick(1f / 60f); // Awake (STM overlay) + OnEnable/Start (loader subscriptions)

        return new LoaderRig
        {
            Loader = loader, Stm = stm,
            Overlay = Get<CanvasGroup>(stm, "_fadeCanvasGroup"),
            GameData = gameData,
        };
    }

    [Fact]
    public void Loader_LaunchGame_CoversSplash_LoadsScene_AndFadesOnClientReady()
    {
        var rig = MakeLoaderRig();
        rig.GameData.SceneName = "MinigameHexRace";

        Drive(() => rig.GameData.InvokeGameLaunch());

        // Splash covered immediately; the load itself waits out waitBeforeLoading (0.5s unscaled).
        Assert.Equal(1f, rig.Overlay.alpha);
        PumpUntil(() => SceneManager.GetActiveScene().name == "MinigameHexRace");
        Assert.Equal(1f, rig.Overlay.alpha); // still covered until the vessel is ready

        // OnClientReady (vessel initialized) → FadeFromBlack.
        Drive(() => rig.GameData.InvokeClientReady());
        PumpUntil(() => rig.Overlay.alpha == 0f);
    }

    [Fact]
    public void Loader_LaunchGame_ClientDefersToTheServer()
    {
        var rig = MakeLoaderRig();
        rig.GameData.SceneName = "MinigameHexRace";
        var nm = new GameObject("nm").AddComponent<NetworkManager>();
        nm.IsServer = false; // connected CLIENT — must not race the server's load
        NetworkManager.Singleton = nm;

        Drive(() => rig.GameData.InvokeGameLaunch(), pumpFrames: 60);

        Assert.Equal(1f, rig.Overlay.alpha);                 // visual cover still applies
        Assert.Equal(nameof(UiShellTests), SceneManager.GetActiveScene().name); // no local load
    }

    [Fact]
    public void Loader_ReturnToMainMenu_LoadsMenuAndArmsTheSplashFade()
    {
        var rig = MakeLoaderRig();

        Drive(() => rig.Loader.ReturnToMainMenu());
        PumpUntil(() => SceneManager.GetActiveScene().name == "Menu_Main");
        Assert.Equal(1f, rig.Overlay.alpha);

        // The armed FadeFromSplashOnReady clears the splash on the menu vessel spawn.
        Drive(() => rig.GameData.InvokeClientReady());
        PumpUntil(() => rig.Overlay.alpha == 0f);
    }
}
