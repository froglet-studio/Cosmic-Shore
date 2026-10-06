using System;
using System.Reflection;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// MenuCrystalClickHandler UI fade — FULLY live after the CanvasGroup arc:
// entering freestyle fades menu groups out / freestyle groups in (in parallel
// with the camera-duration arm via GameTask.WhenAll), exiting restores each
// menu group to its SAVED pre-freestyle alpha (hidden panels stay hidden),
// and the _isTransitioning gate blocks click spam for the full blend.
// ─────────────────────────────────────────────────────────────────────────────

public class MenuFreestyleFadeTests : IDisposable
{
    readonly GameLoop loop = new(nameof(MenuFreestyleFadeTests));

    public MenuFreestyleFadeTests() => NetworkManager.Singleton = null;

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

    /// <summary>
    /// Invokes the action INSIDE the next Tick (Update phase) so async chains start on the
    /// loop's own context instead of xunit's AsyncTestSyncContext — the C4/C6 test-rig trap:
    /// a Task tail resumed on xunit's context races on the thread pool against the asserts.
    /// </summary>
    sealed class TickAction : MonoBehaviour
    {
        public Action Action;
        void Update() { var a = Action; Action = null; a?.Invoke(); }
    }

    TickAction _driver;

    void RunInTick(Action action)
    {
        _driver ??= new GameObject("tick-driver").AddComponent<TickAction>();
        _driver.Action = action;
        loop.Tick(1f / 60f);
    }

    (MenuCrystalClickHandler handler, MenuFreestyleEventsContainerSO freestyle,
     CanvasGroup menuShown, CanvasGroup menuHidden, CanvasGroup freestyleGroup) MakeRig()
    {
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        var freestyle = ScriptableObject.CreateInstance<MenuFreestyleEventsContainerSO>();
        freestyle.OnGameStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        freestyle.OnGameStateTransitionEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        freestyle.OnMenuStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        freestyle.OnMenuStateTransitionEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        // Local player: stub vessel + REAL InputController (ToySystemTests rig precedent).
        var (vesselGo, _, stub, player) = ToyRig.MakeVessel(Vector3.zero);
        player.Input = vesselGo.AddComponent<InputController>();
        ToyRig.SetLocalPlayer(gameData, player);

        // Menu chrome: one visible group + one pre-hidden panel (the ArcadeScreen case).
        var menuShown = new GameObject("menu-shown").AddComponent<CanvasGroup>();
        var menuHidden = new GameObject("menu-hidden").AddComponent<CanvasGroup>();
        menuHidden.alpha = 0.25f;
        var freestyleGroup = new GameObject("game-ui").AddComponent<CanvasGroup>();

        var handler = new GameObject("crystal-click").AddComponent<MenuCrystalClickHandler>();
        Set(handler, "gameData", gameData);
        Set(handler, "freestyleEvents", freestyle);
        Set(handler, "menuCanvasGroups", new[] { menuShown, menuHidden });
        Set(handler, "freestyleCanvasGroups", new[] { freestyleGroup });
        Set(handler, "fadeDuration", 0.2f);
        Set(handler, "cameraTransitionDuration", 0.3f);
        loop.Tick(1f / 60f); // OnEnable (CTS) + Start (hides freestyle groups)

        return (handler, freestyle, menuShown, menuHidden, freestyleGroup);
    }

    static void RunSeconds(GameLoop loop, float seconds)
    {
        int frames = (int)MathF.Ceiling(seconds * 60f) + 2;
        for (int i = 0; i < frames; i++) loop.Tick(1f / 60f);
    }

    [Fact]
    public void Start_HidesFreestyleGroups()
    {
        var (_, _, _, _, freestyleGroup) = MakeRig();
        Assert.Equal(0f, freestyleGroup.alpha);
        Assert.False(freestyleGroup.interactable);
        Assert.False(freestyleGroup.blocksRaycasts);
    }

    [Fact]
    public void EnterFreestyle_FadesMenuOut_FreestyleIn_InParallelWithTheBlend()
    {
        var (handler, freestyle, menuShown, _, freestyleGroup) = MakeRig();
        int ended = 0;
        freestyle.OnGameStateTransitionEnd.OnRaised += () => ended++;

        RunInTick(handler.ToggleTransition);

        // Mid-blend (fade 0.2s done, camera arm 0.3s still running): the fade already
        // landed because both arms run in PARALLEL through GameTask.WhenAll.
        RunSeconds(loop, 0.24f);
        Assert.Equal(1f, freestyleGroup.alpha);
        Assert.Equal(0f, menuShown.alpha);
        Assert.Equal(0, ended); // camera arm still holds the gate

        RunSeconds(loop, 0.15f);
        Assert.True(handler.IsInFreestyle);
        Assert.Equal(1, ended);
        Assert.True(freestyleGroup.interactable);
        Assert.False(menuShown.blocksRaycasts);
    }

    [Fact]
    public void ExitFreestyle_RestoresSavedMenuAlphas_HiddenPanelsStayHidden()
    {
        var (handler, _, menuShown, menuHidden, freestyleGroup) = MakeRig();

        RunInTick(handler.ToggleTransition);
        RunSeconds(loop, 0.4f);
        Assert.True(handler.IsInFreestyle);
        Assert.Equal(0f, menuHidden.alpha); // faded out with the rest

        RunInTick(handler.ToggleTransition);
        RunSeconds(loop, 0.4f);

        Assert.False(handler.IsInFreestyle);
        Assert.Equal(1f, menuShown.alpha, 3);
        Assert.Equal(0.25f, menuHidden.alpha, 3); // restored to SAVED alpha, not forced to 1
        Assert.Equal(0f, freestyleGroup.alpha);
        Assert.True(menuShown.interactable);
        Assert.True(menuHidden.interactable);   // alpha 0.25 > 0.01 threshold
        Assert.False(freestyleGroup.interactable);
    }

    [Fact]
    public void TransitioningGate_IgnoresClickSpam()
    {
        var (handler, freestyle, _, _, _) = MakeRig();
        int started = 0;
        freestyle.OnGameStateTransitionStart.OnRaised += () => started++;

        RunInTick(handler.ToggleTransition);
        RunSeconds(loop, 0.05f);   // mid-blend
        RunInTick(handler.ToggleTransition); // spam — ignored while _isTransitioning
        RunSeconds(loop, 0.4f);

        Assert.Equal(1, started);
        Assert.True(handler.IsInFreestyle);
    }
}
