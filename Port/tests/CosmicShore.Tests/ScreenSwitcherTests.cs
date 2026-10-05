using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.Engine.Soap;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Arc F — the ported ScreenSwitcher, headless (the menushell screenshot proves
// pixels; these prove the CONTRACT): HOME landing + viewport panel layout,
// disabled-screen skipping in both arrow directions, direct-nav rejection of
// disabled screens, IScreen exit-before-enter ordering, PlayerPrefs
// return-state consumed across switcher generations, the modal stack tracking
// ReturnToModal, the pause-on-non-HOME rule, and the freestyle handoff
// (sendNavigationEvents flip + screens CanvasGroup hide + IScreen re-entry).
// PlayerPrefs return-state keys are cleared via the switcher's own RunOnStart
// (the data-only runtime-init the hosts invoke) before AND after each test.
// ─────────────────────────────────────────────────────────────────────────────

public class ScreenSwitcherTests : IDisposable
{
    GameLoop loop;
    readonly int _savedWidth = Screen.width;
    readonly int _savedHeight = Screen.height;

    EventSystem eventSystem;
    ScreenSwitcher switcher;
    CanvasGroup screensGroup;
    MenuFreestyleEventsContainerSO freestyleEvents;
    readonly List<string> screenLog = new();

    static readonly ScreenSwitcher.MenuScreens[] Order =
    {
        ScreenSwitcher.MenuScreens.STORE,
        ScreenSwitcher.MenuScreens.ARK,
        ScreenSwitcher.MenuScreens.HOME,
        ScreenSwitcher.MenuScreens.PORT,
        ScreenSwitcher.MenuScreens.HANGAR,
    };

    public ScreenSwitcherTests()
    {
        Screen.width = 1280;
        Screen.height = 720;
        ClearReturnState();
        loop = new GameLoop(nameof(ScreenSwitcherTests));
        BuildShell();
    }

    public void Dispose()
    {
        PauseSystem.TogglePauseGame(false); // static — never leak a paused world
        ClearReturnState();
        Screen.width = _savedWidth;
        Screen.height = _savedHeight;
        loop.Dispose();
    }

    static void ClearReturnState() =>
        typeof(ScreenSwitcher).GetMethod("RunOnStart", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, null);

    static void SetField(object target, string name, object value) =>
        (target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);

    static T GetField<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(target.GetType().Name, name)).GetValue(target);

    sealed class TestScreen : MonoBehaviour, IScreen
    {
        public List<string> log;
        public string id;
        public void OnScreenEnter() => log.Add($"enter:{id}");
        public void OnScreenExit() => log.Add($"exit:{id}");
    }

    /// <summary>Menu_Main transcription: canvas + Screens root + 5 panels + services.</summary>
    void BuildShell(bool wireFreestyle = false)
    {
        var esGo = new GameObject("EventSystem");
        eventSystem = esGo.AddComponent<EventSystem>();
        new GameObject("UserActionSystem").AddComponent<UserActionSystem>();

        var canvasGo = new GameObject("Canvas", typeof(RectTransform));
        canvasGo.AddComponent<Canvas>();
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var canvasRect = (RectTransform)canvasGo.transform;

        // Screens root: pivot rests at world (0,0) — the NavigateTo y=0 contract.
        var screensGo = new GameObject("Screens", typeof(RectTransform));
        screensGo.SetActive(false); // wire injected fields BEFORE OnEnable subscribes
        var screensRoot = (RectTransform)screensGo.transform;
        screensRoot.SetParent(canvasRect, worldPositionStays: false);
        screensRoot.anchorMin = screensRoot.anchorMax = Vector2.zero;
        screensRoot.pivot = Vector2.zero;
        screensRoot.anchoredPosition = Vector2.zero;
        screensRoot.sizeDelta = new Vector2(canvasRect.rect.width, canvasRect.rect.height);
        screensGroup = screensGo.AddComponent<CanvasGroup>();
        switcher = screensGo.AddComponent<ScreenSwitcher>();

        var entries = new List<ScreenSwitcher.ScreenEntry>();
        foreach (var id in Order)
        {
            var panel = (RectTransform)new GameObject($"Screen_{id}", typeof(RectTransform)).transform;
            panel.SetParent(screensRoot, worldPositionStays: false);
            if (id is ScreenSwitcher.MenuScreens.HOME or ScreenSwitcher.MenuScreens.HANGAR
                   or ScreenSwitcher.MenuScreens.STORE)
            {
                var probe = panel.gameObject.AddComponent<TestScreen>();
                probe.log = screenLog;
                probe.id = id.ToString();
            }
            entries.Add(new ScreenSwitcher.ScreenEntry { id = id, root = panel });
        }
        SetField(switcher, "screens", entries);
        SetField(switcher, "screensCanvasGroup", screensGroup);

        if (wireFreestyle)
        {
            freestyleEvents = ScriptableObject.CreateInstance<MenuFreestyleEventsContainerSO>();
            freestyleEvents.OnGameStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            freestyleEvents.OnMenuStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
            SetField(switcher, "freestyleEvents", freestyleEvents);
        }

        screensGo.SetActive(true);
        loop.Tick(1f / 60f); // Start runs: layout + HOME landing
    }

    void SettleSlide() => loop.Run(40, 1f / 60f); // 0.5s easing + slack

    // ── landing + layout ─────────────────────────────────────────────────

    [Fact]
    public void Start_LandsOnHome_AndLaysPanelsOneViewportApart()
    {
        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.HOME));
        Assert.Contains("enter:HOME", screenLog);

        var entries = GetField<List<ScreenSwitcher.ScreenEntry>>(switcher, "screens");
        for (int i = 0; i < entries.Count; i++)
        {
            Assert.Equal(1920f, entries[i].root.rect.width, 2);      // viewport width in canvas units
            Assert.Equal(i * 1920f, entries[i].root.anchoredPosition.x, 2);
        }

        // HOME is index 2 → the root slid two viewport widths left (world pixels).
        Assert.Equal(-2f * 1920f * (2f / 3f), switcher.transform.position.x, 1);
    }

    // ── navigation rules ─────────────────────────────────────────────────

    [Fact]
    public void ArrowNavigation_SkipsDisabledScreens_BothDirections()
    {
        switcher.OnClickRightArrow();                                 // HOME → (PORT disabled) → HANGAR
        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.HANGAR));

        SettleSlide();
        switcher.OnClickLeftArrow();                                  // HANGAR → (PORT) → HOME
        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.HOME));

        SettleSlide();
        switcher.OnClickLeftArrow();                                  // HOME → (ARK disabled) → STORE
        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.STORE));

        SettleSlide();
        switcher.OnClickLeftArrow();                                  // off the end — stays put
        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.STORE));
    }

    [Fact]
    public void DirectNavigation_ToDisabledScreen_IsRejected()
    {
        switcher.OnClickPortNav();
        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.HOME));

        switcher.OnClickArkNav();
        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.HOME));
    }

    [Fact]
    public void IScreen_ExitFiresBeforeEnter_OnNavigation()
    {
        screenLog.Clear();
        switcher.OnClickHangarNav();

        Assert.Equal(new[] { "exit:HOME", "enter:HANGAR" }, screenLog);
    }

    [Fact]
    public void PauseRule_NonHomeScreensPause_HomeResumes()
    {
        Assert.False(PauseSystem.Paused);                             // HOME landing resumes

        switcher.OnClickHangarNav();
        Assert.True(PauseSystem.Paused);                              // shipping CPU-saver rule

        SettleSlide();
        switcher.OnClickHomeNav();
        Assert.False(PauseSystem.Paused);
    }

    // ── return-state persistence ─────────────────────────────────────────

    [Fact]
    public void ReturnState_IsConsumedByTheNextSwitcherGeneration()
    {
        switcher.OnClickHangarNav();                                  // writes ReturnToScreen=HANGAR
        SettleSlide();

        // A new world (scene reload): the next switcher must land on HANGAR and
        // consume the key so a third generation lands on HOME again.
        loop.Dispose();
        screenLog.Clear();
        loop = new GameLoop(nameof(ReturnState_IsConsumedByTheNextSwitcherGeneration));
        BuildShell();

        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.HANGAR));
        Assert.Contains("enter:HANGAR", screenLog);
        Assert.False(PlayerPrefs.HasKey("ReturnToScreen"));           // consumed
    }

    // ── modal stack ──────────────────────────────────────────────────────

    [Fact]
    public void ModalStack_PushPop_TracksActiveAndReturnState()
    {
        Assert.False(switcher.HasActiveModal);

        switcher.PushModal(ScreenSwitcher.ModalWindows.SETTINGS);
        Assert.True(switcher.HasActiveModal);
        Assert.True(switcher.ModalIsActive(ScreenSwitcher.ModalWindows.SETTINGS));
        Assert.Equal((int)ScreenSwitcher.ModalWindows.SETTINGS, PlayerPrefs.GetInt("ReturnToModal"));

        switcher.PushModal(ScreenSwitcher.ModalWindows.PROFILE);      // stacked — top wins
        Assert.True(switcher.ModalIsActive(ScreenSwitcher.ModalWindows.PROFILE));
        Assert.False(switcher.ModalIsActive(ScreenSwitcher.ModalWindows.SETTINGS));

        switcher.PopModal();
        Assert.True(switcher.ModalIsActive(ScreenSwitcher.ModalWindows.SETTINGS));

        switcher.PopModal();
        Assert.False(switcher.HasActiveModal);
        Assert.False(PlayerPrefs.HasKey("ReturnToModal"));            // cleared at empty
    }

    // ── freestyle handoff ────────────────────────────────────────────────

    [Fact]
    public void FreestyleHandoff_FlipsNavEvents_HidesScreens_AndBlocksNavigation()
    {
        // Rebuild with the freestyle events wired (OnEnable subscribes on activation).
        loop.Dispose();
        screenLog.Clear();
        loop = new GameLoop(nameof(FreestyleHandoff_FlipsNavEvents_HidesScreens_AndBlocksNavigation));
        BuildShell(wireFreestyle: true);

        Assert.True(eventSystem.sendNavigationEvents);
        screenLog.Clear();

        freestyleEvents.OnGameStateTransitionStart.Raise();           // menu → freestyle
        Assert.False(eventSystem.sendNavigationEvents);               // the pad flies the ship
        Assert.Equal(0f, screensGroup.alpha);
        Assert.Contains("exit:HOME", screenLog);

        switcher.OnClickHangarNav();                                  // blocked while flying
        Assert.True(switcher.ScreenIsActive(ScreenSwitcher.MenuScreens.HOME));

        freestyleEvents.OnMenuStateTransitionStart.Raise();           // freestyle → menu
        Assert.True(eventSystem.sendNavigationEvents);
        Assert.Equal(1f, screensGroup.alpha);
        Assert.Contains("enter:HOME", screenLog);
    }
}
