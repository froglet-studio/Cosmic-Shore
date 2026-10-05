using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests;

// V7: concrete InputStatus — IsSpawned-switched local/replicated storage,
// owner-gated writes, pause notification on both paths, ResetForReplay contract.
public class InputStatusTests
{
    static InputStatus Make()
    {
        var go = new GameObject("input");
        return go.AddComponent<InputStatus>();
    }

    [Fact]
    public void Unspawned_WritesAndReadsLocalFallbacks()
    {
        using var loop = new GameLoop();
        var status = Make();

        status.XSum = 0.4f;
        status.LeftNormalizedJoystickPosition = new Vector2(0.1f, -0.2f);
        status.Idle = true;

        Assert.Equal(0.4f, status.XSum);
        Assert.Equal(new Vector2(0.1f, -0.2f), status.LeftNormalizedJoystickPosition);
        Assert.True(status.Idle);
    }

    [Fact]
    public void Unspawned_PausedSetter_RaisesToggleEventImmediately()
    {
        using var loop = new GameLoop();
        var status = Make();
        var toggles = new List<bool>();
        status.OnToggleInputPaused += toggles.Add;

        status.Paused = true;
        status.Paused = false;

        Assert.Equal(new[] { true, false }, toggles);
    }

    [Fact]
    public void SpawnedOwner_WritesReplicatedState_AndPausedNotifiesViaNetworkVariable()
    {
        using var loop = new GameLoop();
        var status = Make();
        status.Spawn(isOwner: true);
        var toggles = new List<bool>();
        status.OnToggleInputPaused += toggles.Add;

        status.XSum = 0.7f;
        status.Paused = true;

        Assert.Equal(0.7f, status.XSum);
        // Network path: the toggle event fires from n_paused.OnValueChanged, once.
        Assert.Equal(new[] { true }, toggles);
    }

    [Fact]
    public void SpawnedNonOwner_WritesAreGated()
    {
        using var loop = new GameLoop();
        var status = Make();
        status.Spawn(isOwner: false);

        status.XSum = 0.9f;
        status.Idle = true;

        Assert.Equal(0f, status.XSum);
        Assert.False(status.Idle);
    }

    [Fact]
    public void ResetForReplay_ZeroesInputs_SetsIdlePaused_PreservesInvertSettings()
    {
        using var loop = new GameLoop();
        var status = Make();
        status.XSum = 1f;
        status.Throttle = 0.8f;
        status.EasedLeftJoystickPosition = new Vector2(1f, 1f);
        status.InvertYEnabled = true;
        status.InvertThrottleEnabled = true;
        status.Idle = false;

        status.ResetForReplay();

        Assert.Equal(0f, status.XSum);
        Assert.Equal(0f, status.Throttle);
        Assert.Equal(Vector2.zero, status.EasedLeftJoystickPosition);
        Assert.True(status.Idle);
        Assert.True(status.Paused);
        // The original deliberately leaves player preferences alone (commented block).
        Assert.True(status.InvertYEnabled);
        Assert.True(status.InvertThrottleEnabled);
    }

    [Fact]
    public void ResetForReplay_OnSpawnedNonOwner_IsANoOp()
    {
        using var loop = new GameLoop();
        var status = Make();
        status.Spawn(isOwner: false);

        status.ResetForReplay();

        Assert.False(status.Idle); // untouched — non-owners must not write replicated state
        Assert.False(status.Paused);
    }

    [Fact]
    public void InputController_Awake_AddsConcreteInputStatus_DeviationFifteenClosed()
    {
        using var loop = new GameLoop();
        var go = new GameObject("controller");
        go.SetActive(false);
        var controller = go.AddComponent<InputController>();
        go.SetActive(true);
        loop.Tick(1f / 60f); // run Awake

        Assert.IsType<InputStatus>(controller.InputStatus);
        Assert.Same(controller, controller.InputStatus.InputController);
    }
}
