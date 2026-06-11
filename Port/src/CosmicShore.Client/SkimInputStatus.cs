using System;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Client
{
    /// <summary>
    /// Concrete IInputStatus for the SkimRace slice: the ported input strategies write
    /// the authentic sums/differences into it, and the flight model consumes them.
    /// (The full ported InputStatus class lands with vessel-layer step V7.)
    /// </summary>
    public sealed class SkimInputStatus : IInputStatus
    {
        public event Action<bool> OnToggleInputPaused { add { } remove { } }
        public ScriptableEventInputEvents OnButtonPressed { get; } = new() { name = "OnButtonPressed" };
        public ScriptableEventInputEvents OnButtonReleased { get; } = new() { name = "OnButtonReleased" };

        public InputController InputController { get; set; }
        public Quaternion GetGyroRotation() => Quaternion.identity;

        public float XSum { get; set; }
        public float YSum { get; set; }
        public float XDiff { get; set; } = 0.5f; // neutral sticks = mid throttle
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

        public void ResetForReplay()
        {
            XSum = 0f; YSum = 0f; YDiff = 0f;
            XDiff = 0.5f;
            Throttle = 0f;
        }
    }
}
