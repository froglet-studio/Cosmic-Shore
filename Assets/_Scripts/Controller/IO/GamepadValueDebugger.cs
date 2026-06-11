using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Zero-setup runtime diagnostic for controller input. Auto-installs at startup (no scene
    /// wiring needed) and logs the LIVE values it reads from <c>Gamepad.current</c> — completely
    /// independent of the game's input strategies, pause state, autopilot, or which scene you're
    /// in.
    ///
    /// Purpose: once a controller is promoted to a Gamepad (see <see cref="HidGamepadSupport"/>),
    /// this answers the only remaining question — "are the stick/button VALUES actually correct?"
    ///   - If moving a stick logs sensible values (≈ -1..1, centered at 0) -> the layout offsets
    ///     are right and any remaining problem is downstream (game not consuming input).
    ///   - If moving a stick logs nothing or garbage/stuck values -> the generated layout's byte
    ///     offsets are wrong and need correcting.
    ///   - If it logs "Gamepad.current == null" while you mash buttons -> the device's state
    ///     events aren't reaching the Input System at all (a platform/transport issue, not a
    ///     mapping one).
    ///
    /// Turn off by setting <see cref="HidGamepadSupport.DebugReadValues"/> = false (or once the
    /// controller is confirmed working).
    /// </summary>
    [AddComponentMenu("")] // hide from the Add Component menu
    public class GamepadValueDebugger : MonoBehaviour
    {
        private const float StickLogThreshold = 0.12f;   // ignore idle jitter
        private const float StickLogDelta = 0.10f;        // only log on meaningful change
        private const float MinLogInterval = 0.20f;       // throttle: at most ~5 logs/sec

        private string _lastDeviceName;
        private Vector2 _lastLeft, _lastRight;
        private float _lastLogTime;
        private bool _loggedNullOnce;

        private void Update()
        {
            var pad = Gamepad.current;
            if (pad == null)
            {
                if (!_loggedNullOnce)
                {
                    _loggedNullOnce = true;
                    Debug.Log("[GamepadValueDebugger] Gamepad.current == null. " +
                              "If a controller is connected, it isn't the active gamepad yet — " +
                              "press a button on it. If pressing buttons never changes this, its " +
                              "state events aren't reaching Unity (transport/platform issue).");
                }
                return;
            }
            _loggedNullOnce = false;

            if (pad.name != _lastDeviceName)
            {
                _lastDeviceName = pad.name;
                Debug.Log($"[GamepadValueDebugger] Active gamepad = '{pad.name}' " +
                          $"(displayName '{pad.displayName}', layout '{pad.layout}'). " +
                          "Move the sticks and press buttons; values below are read straight from it.");
            }

            // Buttons: log on the frame they're pressed.
            LogButtonIfPressed(pad.buttonSouth, "buttonSouth (A)");
            LogButtonIfPressed(pad.buttonEast, "buttonEast (B)");
            LogButtonIfPressed(pad.buttonWest, "buttonWest (X)");
            LogButtonIfPressed(pad.buttonNorth, "buttonNorth (Y)");
            LogButtonIfPressed(pad.leftShoulder, "leftShoulder (L1)");
            LogButtonIfPressed(pad.rightShoulder, "rightShoulder (R1 / boost)");
            LogButtonIfPressed(pad.leftTrigger, "leftTrigger (L2)");
            LogButtonIfPressed(pad.rightTrigger, "rightTrigger (R2)");
            LogButtonIfPressed(pad.startButton, "start");
            LogButtonIfPressed(pad.selectButton, "select");
            if (pad.dpad != null)
            {
                LogButtonIfPressed(pad.dpad.up, "dpad/up");
                LogButtonIfPressed(pad.dpad.down, "dpad/down");
                LogButtonIfPressed(pad.dpad.left, "dpad/left");
                LogButtonIfPressed(pad.dpad.right, "dpad/right");
            }

            // Sticks: throttled, on meaningful movement/change.
            Vector2 l = pad.leftStick.ReadValue();
            Vector2 r = pad.rightStick.ReadValue();

            bool leftMoved = l.magnitude > StickLogThreshold && (l - _lastLeft).magnitude > StickLogDelta;
            bool rightMoved = r.magnitude > StickLogThreshold && (r - _lastRight).magnitude > StickLogDelta;

            if ((leftMoved || rightMoved) && Time.unscaledTime - _lastLogTime >= MinLogInterval)
            {
                _lastLogTime = Time.unscaledTime;
                _lastLeft = l;
                _lastRight = r;
                // Unprocessed = value straight out of the state-block format, before normalize/
                // invert processors. Reveals whether the axis format (signed vs unsigned) is right.
                float lxu = pad.leftStick.x.ReadUnprocessedValue();
                float lyu = pad.leftStick.y.ReadUnprocessedValue();
                Debug.Log($"[GamepadValueDebugger] leftStick={Fmt(l)}  rightStick={Fmt(r)}  " +
                          $"L2={pad.leftTrigger.ReadValue():F2}  R2={pad.rightTrigger.ReadValue():F2}  " +
                          $"| leftStick raw(x,y)=({lxu:F2}, {lyu:F2})");
            }
        }

        private void LogButtonIfPressed(UnityEngine.InputSystem.Controls.ButtonControl button, string label)
        {
            if (button != null && button.wasPressedThisFrame)
                Debug.Log($"[GamepadValueDebugger] PRESSED {label}");
        }

        private static string Fmt(Vector2 v) => $"({v.x:F2}, {v.y:F2})";
    }
}
