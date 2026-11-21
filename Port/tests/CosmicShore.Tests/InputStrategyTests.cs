using System;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests;

/// <summary>Ported IO layer: strategies run inert (no devices) without faulting.</summary>
public class InputStrategyTests
{
    sealed class StubInputStatus : IInputStatus
    {
        public event Action<bool> OnToggleInputPaused { add { } remove { } }
        public ScriptableEventInputEvents OnButtonPressed { get; } = new();
        public ScriptableEventInputEvents OnButtonReleased { get; } = new();
        public InputController InputController { get; set; }
        public Quaternion GetGyroRotation() => Quaternion.identity;
        public float XSum { get; set; }
        public float YSum { get; set; }
        public float XDiff { get; set; }
        public float YDiff { get; set; }
        public float Throttle { get; set; }
        public float LeftTriggerAnalog { get; set; }
        public float RightTriggerAnalog { get; set; }
        public bool Idle { get; set; }
        public bool Paused { get; set; }
        public bool IsGyroEnabled { get; set; }
        public bool InvertYEnabled { get; set; }
        public bool InvertThrottleEnabled { get; set; }
        public bool OneTouchLeft { get; set; }
        public bool CommandStickControls { get; set; }
        public InputDeviceType ActiveInputDevice { get; set; }
        public Vector2 RightJoystickHome { get; set; }
        public Vector2 LeftJoystickHome { get; set; }
        public Vector2 RightClampedPosition { get; set; }
        public Vector2 LeftClampedPosition { get; set; }
        public Vector2 RightJoystickStart { get; set; }
        public Vector2 LeftJoystickStart { get; set; }
        public Vector2 RightNormalizedJoystickPosition { get; set; }
        public Vector2 LeftNormalizedJoystickPosition { get; set; }
        public Vector2 EasedRightJoystickPosition { get; set; }
        public Vector2 EasedLeftJoystickPosition { get; set; }
        public Vector2 SingleTouchValue { get; set; }
        public Vector3 ThreeDPosition { get; set; }
        public void ResetForReplay() { }
    }

    [Theory]
    [InlineData(typeof(KeyboardInputStrategy))]
    [InlineData(typeof(GamepadInputStrategy))]
    [InlineData(typeof(TouchInputStrategy))]
    public void Strategy_ProcessInput_InertDevices_DoesNotThrow(Type strategyType)
    {
        var strategy = (IInputStrategy)Activator.CreateInstance(strategyType);
        var status = new StubInputStatus();
        strategy.Initialize(status);
        strategy.OnStrategyActivated();
        for (int i = 0; i < 5; i++) strategy.ProcessInput();
        strategy.OnStrategyDeactivated();
    }

    [Fact]
    public void InvertToggles_WriteThroughToStatus()
    {
        var strategy = new KeyboardInputStrategy();
        var status = new StubInputStatus();
        strategy.Initialize(status);
        strategy.SetInvertY(true);
        strategy.SetInvertThrottle(true);
        Assert.True(status.InvertYEnabled);
        Assert.True(status.InvertThrottleEnabled);
    }
}
