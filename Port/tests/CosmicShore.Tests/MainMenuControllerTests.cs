using System;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// MainMenuController (Menu_Main scene-controller arc) — the menu sub-state
// machine driven end-to-end through the REAL SOAP events:
//   None → Initializing (Start) → Ready (OnClientReady) → Freestyle
//   (OnGameStateTransitionStart) → Ready (OnMenuStateTransitionStart) →
//   LaunchingGame (OnLaunchGame); invalid transitions rejected.
// ─────────────────────────────────────────────────────────────────────────────

public class MainMenuControllerTests : IDisposable
{
    readonly GameLoop loop = new(nameof(MainMenuControllerTests));

    public MainMenuControllerTests() => NetworkManager.Singleton = null;

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

    (MainMenuController controller, GameDataSO gameData, MenuFreestyleEventsContainerSO freestyle,
     AnalyticsServiceFacade analytics) MakeRig()
    {
        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnInitializeGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnLaunchGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.OnPlayerPairInitialized = ScriptableObject.CreateInstance<ScriptableEventUlong>();
        gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
        gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();

        var freestyle = ScriptableObject.CreateInstance<MenuFreestyleEventsContainerSO>();
        freestyle.OnGameStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        freestyle.OnGameStateTransitionEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        freestyle.OnMenuStateTransitionStart = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        freestyle.OnMenuStateTransitionEnd = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        var analytics = new AnalyticsServiceFacade();

        // Fields are set between AddComponent and the first tick — Start() only runs on
        // the tick, so the [Inject] fields are populated before it (the ToyRig pattern).
        var go = new GameObject("main-menu-controller");
        var controller = go.AddComponent<MainMenuController>();
        Set(controller, "_playerOrigins", new[] { new GameObject("origin0").transform });
        Set(controller, "_gameData", gameData);
        Set(controller, "_freestyleEvents", freestyle);
        Set(controller, "_analytics", analytics);
        loop.Tick(1f / 60f); // run Start()

        return (controller, gameData, freestyle, analytics);
    }

    [Fact]
    public void Start_ConfiguresMenuGameData_AndEntersInitializing()
    {
        var (controller, gameData, _, _) = MakeRig();

        Assert.Equal(MainMenuState.Initializing, controller.CurrentState);
        Assert.Equal(VesselClassType.Squirrel, gameData.selectedVesselClass.Value); // inspector default
        Assert.Equal(1, gameData.SelectedIntensity.Value);
    }

    [Fact]
    public void FullMenuFlow_Ready_Freestyle_Ready_LaunchingGame()
    {
        var (controller, gameData, freestyle, analytics) = MakeRig();
        var observed = new System.Collections.Generic.List<MainMenuState>();
        controller.OnStateChanged += s => observed.Add(s);

        gameData.OnClientReady.Raise();
        Assert.Equal(MainMenuState.Ready, controller.CurrentState);
        Assert.True(analytics.MenuReadyThisSession); // RecordMenuReady fired through the shell

        freestyle.OnGameStateTransitionStart.Raise();
        Assert.Equal(MainMenuState.Freestyle, controller.CurrentState);

        freestyle.OnMenuStateTransitionStart.Raise();
        Assert.Equal(MainMenuState.Ready, controller.CurrentState);

        gameData.OnLaunchGame.Raise();
        Assert.Equal(MainMenuState.LaunchingGame, controller.CurrentState);

        Assert.Equal(new[]
        {
            MainMenuState.Ready, MainMenuState.Freestyle,
            MainMenuState.Ready, MainMenuState.LaunchingGame,
        }, observed.ToArray());
    }

    [Fact]
    public void InvalidTransition_IsRejected_StatePreserved()
    {
        var (controller, gameData, freestyle, _) = MakeRig();

        // Initializing → Freestyle is not in the transition table.
        freestyle.OnGameStateTransitionStart.Raise();
        Assert.Equal(MainMenuState.Initializing, controller.CurrentState);

        // Initializing → LaunchingGame is not in the table either.
        gameData.OnLaunchGame.Raise();
        Assert.Equal(MainMenuState.Initializing, controller.CurrentState);
    }

    [Fact]
    public void CanLaunchGame_DirectlyFromFreestyle()
    {
        var (controller, gameData, freestyle, _) = MakeRig();
        gameData.OnClientReady.Raise();
        freestyle.OnGameStateTransitionStart.Raise();
        Assert.Equal(MainMenuState.Freestyle, controller.CurrentState);

        gameData.OnLaunchGame.Raise();
        Assert.Equal(MainMenuState.LaunchingGame, controller.CurrentState);
    }
}
