namespace CosmicShore.Engine.InputSystem
{
    // Inert input-device shim (vessel-layer survey E1): ported strategies compile and
    // run verbatim; devices report nothing pressed until platform input backends land
    // (phase 5). The SkimRace client reads Silk.NET directly in the meantime.

    public sealed class KeyControl
    {
        public bool isPressed;
        public bool wasPressedThisFrame;
        public bool wasReleasedThisFrame;
    }

    public sealed class Keyboard
    {
        /// <summary>Null when no keyboard backend is attached — matches the original API shape.</summary>
        public static Keyboard current;

        public readonly KeyControl aKey = new(), bKey = new(), cKey = new(), dKey = new(), eKey = new(),
            nKey = new(), pKey = new(), qKey = new(), rKey = new(), sKey = new(), wKey = new(),
            spaceKey = new(), quoteKey = new(), leftShiftKey = new(), rightShiftKey = new(),
            upArrowKey = new(), downArrowKey = new(), leftArrowKey = new(), rightArrowKey = new(),
            escapeKey = new(), enterKey = new(), tabKey = new(), leftCtrlKey = new(),
            fKey = new(), gKey = new(), hKey = new(), iKey = new(), jKey = new(), kKey = new(),
            lKey = new(), mKey = new(), oKey = new(), tKey = new(), uKey = new(), vKey = new(),
            xKey = new(), yKey = new(), zKey = new(), semicolonKey = new(), commaKey = new(),
            periodKey = new(), slashKey = new(), leftBracketKey = new(), rightBracketKey = new();
    }

    public sealed class ButtonControl
    {
        public bool isPressed;
        public bool wasPressedThisFrame;
        public bool wasReleasedThisFrame;
        public float value;
        public float ReadValue() => value;
    }

    public sealed class StickControl
    {
        public Vector2 value;
        public Vector2 ReadValue() => value;
    }

    public sealed class Gamepad
    {
        public static Gamepad current;

        public readonly StickControl leftStick = new(), rightStick = new();
        public readonly ButtonControl leftTrigger = new(), rightTrigger = new(),
            buttonSouth = new(), buttonNorth = new(), buttonEast = new(), buttonWest = new(),
            leftShoulder = new(), rightShoulder = new(), startButton = new(), selectButton = new();
    }

    public sealed class MouseButtonControl
    {
        public bool isPressed;
        public bool wasPressedThisFrame;
        public bool wasReleasedThisFrame;
    }

    public sealed class PositionControl
    {
        public Vector2 value;
        public Vector2 ReadValue() => value;
    }

    public sealed class Mouse
    {
        public static Mouse current;

        /// <summary>All attached mice (empty until a platform input backend registers devices).</summary>
        public static readonly System.Collections.Generic.List<Mouse> all = new();

        public readonly MouseButtonControl leftButton = new(), rightButton = new(), middleButton = new();
        public readonly PositionControl position = new(), delta = new(), scroll = new();
        public string name = "Mouse";
        public string deviceId = "";
        public string displayName = "Mouse";
    }

    public sealed class QuaternionControl
    {
        public Quaternion value = Quaternion.identity;
        public Quaternion ReadValue() => value;
    }

    public sealed class Accelerometer
    {
        public static Accelerometer current;
        public bool enabled;
        public StickControl acceleration = new();
    }

    public sealed class AttitudeSensor
    {
        public static AttitudeSensor current;
        public QuaternionControl attitude = new();
        public bool enabled;
    }

    public enum TouchPhase { None = 0, Began = 1, Moved = 2, Ended = 3, Canceled = 4, Stationary = 5 }

    public static class InputSystem
    {
        /// <summary>All registered devices (empty until platform backends register).</summary>
        public static readonly System.Collections.Generic.List<object> devices = new();

        public static void EnableDevice(object device) { }
        public static void DisableDevice(object device) { }
    }
}

namespace CosmicShore.Engine.InputSystem.EnhancedTouch
{
    /// <summary>No-op touch support: zero active touches until a touch backend lands.</summary>
    public static class EnhancedTouchSupport
    {
        public static bool enabled { get; private set; }
        public static void Enable() => enabled = true;
        public static void Disable() => enabled = false;
    }

    public struct Touch
    {
        public static readonly System.Collections.Generic.List<Touch> activeTouches = new();
        public Vector2 screenPosition;
        // Same enum as the parent namespace — the original API shares one TouchPhase.
        public CosmicShore.Engine.InputSystem.TouchPhase phase;
        public int touchId;
    }
}
