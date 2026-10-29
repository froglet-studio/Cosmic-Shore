using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// God mode for the Menu_Main lava lamp (Yash, 2026-10-29: "I just need a god mode, with which I can fly
    /// smoothly in the environment without the vessel, and have an eye on everything while the Vessel is flown
    /// by AI ... a new camera for inspection, a button shortcut to swap cameras in lavalamp"). Replaces the
    /// wormhole lab scene as the way to watch a vessel's ability up close.
    ///
    /// <para><b>Toggle</b>: F2, or L3 (left-stick click) on a pad. Works only in the settled lava lamp, where
    /// the AI flies the local vessel and the player's flight input is already paused
    /// (<see cref="MainMenuCameraController.CanBeginInspection"/>), so the vessel never notices: no input is
    /// handed to it and the AI keeps flying.</para>
    ///
    /// <para><b>Flight</b> (unscaled time, so it still flies while the menu is paused): WASD move, E / Space up,
    /// Q / Ctrl down, hold the right mouse button to look, Shift fast, Alt slow, mouse wheel scales the cruise
    /// speed. Pad: left stick move, right stick look, RT up, LT down, RB fast. Velocity eases toward the input
    /// and damps out when you let go; the look eases toward its target, so motion stays smooth.</para>
    ///
    /// <para><b>Cameras</b>: there is no second Camera. God mode borrows the scene's MainCamera from
    /// <see cref="MainMenuCameraController"/> (<see cref="MainMenuCameraController.BeginInspection"/>), starts at
    /// its current pose, and hands it back with <see cref="MainMenuCameraController.EndInspection"/>, which
    /// re-seeds the menu rig there so the lava-lamp framing glides back. Clicking the vessel to take control
    /// (freestyle) ends god mode first and the enter blend starts from the god camera's pose; CM PlayerCam and
    /// <see cref="CameraManager"/> are never touched, because the lava lamp does not use them.</para>
    ///
    /// <para>While flying, UI keyboard/pad navigation is switched off (WASD and Space would otherwise move and
    /// press the menu's selection) and restored on exit. Mouse clicks still reach the UI.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // after MainMenuCameraController's LateUpdate, so the end-of-state check sees this frame
    public sealed class MenuGodModeCamera : MonoBehaviour
    {
        [Header("Toggle")]
        [Tooltip("Master switch. Off: the shortcut does nothing and god mode cannot start.")]
        [SerializeField] bool godModeEnabled = true;

        [Tooltip("Keyboard toggle. F2 is unused elsewhere (F5-F9 are the diagnostics/benchmark HUDs, F11 fullscreen).")]
        [SerializeField] Key toggleKey = Key.F2;

        [Tooltip("Also toggle with L3 (left-stick click) on a gamepad. R3 is already the screenshot gesture.")]
        [SerializeField] bool gamepadToggle = true;

        [Tooltip("The lava lamp's camera rig. Found on this GameObject when left empty.")]
        [SerializeField] MainMenuCameraController menuCamera;

        [Header("Flight")]
        [Tooltip("Cruise speed in world units per second. The mouse wheel scales it at runtime.")]
        [Min(0.1f)] [SerializeField] float moveSpeed = 60f;

        [Tooltip("Limits for the wheel-scaled cruise speed.")]
        [SerializeField] Vector2 moveSpeedRange = new(2f, 2000f);

        [Tooltip("Each wheel notch multiplies the cruise speed by this.")]
        [Min(1.01f)] [SerializeField] float wheelSpeedStep = 1.2f;

        [Tooltip("Speed multiplier while Shift (or RB) is held.")]
        [Min(1f)] [SerializeField] float fastMultiplier = 4f;

        [Tooltip("Speed multiplier while Alt is held.")]
        [Range(0.01f, 1f)] [SerializeField] float slowMultiplier = 0.25f;

        [Tooltip("How quickly velocity reaches the input speed (1/s). Higher = snappier start.")]
        [Min(0.1f)] [SerializeField] float acceleration = 6f;

        [Tooltip("How quickly velocity dies once input stops (1/s). Lower = longer glide.")]
        [Min(0.1f)] [SerializeField] float damping = 4f;

        [Header("Look")]
        [Tooltip("Degrees per mouse pixel while the right button is held.")]
        [Min(0.001f)] [SerializeField] float mouseDegreesPerPixel = 0.12f;

        [Tooltip("Degrees per second at full right-stick deflection.")]
        [Min(1f)] [SerializeField] float stickDegreesPerSecond = 120f;

        [Tooltip("How quickly the view follows the look target (1/s). Higher = tighter, lower = floatier. " +
                 "Also how fast any roll the lava-lamp camera had is levelled out on entry.")]
        [Min(0.1f)] [SerializeField] float lookSmoothing = 14f;

        [Tooltip("Pitch limit, degrees, short of straight up/down.")]
        [Range(10f, 89.9f)] [SerializeField] float maxPitch = 89f;

        [Tooltip("Lock and hide the cursor while the right mouse button looks.")]
        [SerializeField] bool lockCursorWhileLooking = true;

        [Header("Overlay")]
        [Tooltip("Show a one-line reminder of the controls and the current speed while in god mode.")]
        [SerializeField] bool showHint = true;

        bool _active;
        Transform _camT;
        Vector3 _velocity;
        Quaternion _rot;
        float _yaw, _pitch, _speed;

        bool _cursorCaptured;
        CursorLockMode _savedLock;
        bool _savedVisible;

        EventSystem _navEventSystem;
        bool _savedSendNavigation;

        GUIStyle _hintStyle;

        /// <summary>Whether god mode is flying the camera right now.</summary>
        public bool IsActive => _active;

        void Awake()
        {
            if (!menuCamera) menuCamera = GetComponent<MainMenuCameraController>();
            _speed = Mathf.Clamp(moveSpeed, moveSpeedRange.x, moveSpeedRange.y);
        }

        void OnEnable()
        {
            if (menuCamera) menuCamera.InspectionEnded += HandleInspectionEnded;
        }

        void OnDisable()
        {
            if (menuCamera)
            {
                menuCamera.InspectionEnded -= HandleInspectionEnded;
                menuCamera.EndInspection();
            }
            Release();
        }

        void Update()
        {
            if (TogglePressed()) Toggle();
        }

        void LateUpdate()
        {
            if (!_active) return;
            if (!_camT || !menuCamera || !menuCamera.IsInspecting) { Release(); return; }

            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            bool typing = TextFieldHasFocus();

            // ── Look ──
            Vector2 look = Vector2.zero;
            bool mouseLook = mouse != null && mouse.rightButton.isPressed;
            if (mouseLook)
            {
                if (lockCursorWhileLooking) CaptureCursor();
                look += mouse.delta.ReadValue() * mouseDegreesPerPixel;
            }
            else ReleaseCursor();
            if (pad != null) look += pad.rightStick.ReadValue() * (stickDegreesPerSecond * dt);

            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -maxPitch, maxPitch);
            _rot = Quaternion.Slerp(_rot, Quaternion.Euler(_pitch, _yaw, 0f), 1f - Mathf.Exp(-lookSmoothing * dt));

            // ── Speed ──
            if (mouse != null && !PointerOverUI())
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0f)
                {
                    // Raw wheel is ±120 per notch on Windows, ±1 elsewhere; count notches either way.
                    float notches = Mathf.Abs(wheel) >= 30f ? wheel / 120f : Mathf.Sign(wheel);
                    _speed = Mathf.Clamp(_speed * Mathf.Pow(wheelSpeedStep, notches), moveSpeedRange.x, moveSpeedRange.y);
                }
            }

            // ── Move ──
            Vector3 input = Vector3.zero;
            float mult = 1f;
            if (kb != null && !typing)
            {
                input.x += Axis(kb.dKey.isPressed, kb.aKey.isPressed);
                input.z += Axis(kb.wKey.isPressed, kb.sKey.isPressed);
                input.y += Axis(kb.eKey.isPressed || kb.spaceKey.isPressed, kb.qKey.isPressed || kb.ctrlKey.isPressed);
                if (kb.shiftKey.isPressed) mult *= fastMultiplier;
                if (kb.altKey.isPressed) mult *= slowMultiplier;
            }
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                input.x += stick.x;
                input.z += stick.y;
                input.y += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (pad.rightShoulder.isPressed) mult *= fastMultiplier;
            }
            input = Vector3.ClampMagnitude(input, 1f);

            // Forward/strafe follow the view; up/down is world up, so climbing never drifts sideways.
            Vector3 wish = (_rot * new Vector3(input.x, 0f, input.z) + Vector3.up * input.y) * (_speed * mult);
            float rate = input.sqrMagnitude > 1e-4f ? acceleration : damping;
            _velocity = Vector3.Lerp(_velocity, wish, 1f - Mathf.Exp(-rate * dt));
            if (input.sqrMagnitude <= 1e-4f && _velocity.sqrMagnitude < 1e-6f) _velocity = Vector3.zero;

            _camT.SetPositionAndRotation(_camT.position + _velocity * dt, _rot);
        }

        /// <summary>Enters god mode from the current view, or leaves it. Does nothing when disabled or not in the lava lamp.</summary>
        public void Toggle()
        {
            if (_active) Exit();
            else Enter();
        }

        void Enter()
        {
            if (!godModeEnabled || !menuCamera) return;
            if (!menuCamera.CanBeginInspection)
            {
                CSDebug.LogVerbose(CSLogChannel.MenuUI,
                    "[MenuGodModeCamera] God mode is only available in the lava lamp (AI flying, not freestyle or a blend).");
                return;
            }
            var cam = menuCamera.SceneCamera;
            if (!cam || !menuCamera.BeginInspection()) return;

            _camT = cam.transform;
            _rot = _camT.rotation;
            Vector3 e = _rot.eulerAngles;
            _yaw = e.y;
            _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, e.x), -maxPitch, maxPitch);
            _velocity = Vector3.zero;
            _active = true;

            // WASD / Space / the pad would otherwise also drive the menu's UI selection.
            _navEventSystem = EventSystem.current;
            if (_navEventSystem)
            {
                _savedSendNavigation = _navEventSystem.sendNavigationEvents;
                _navEventSystem.sendNavigationEvents = false;
            }
        }

        void Exit()
        {
            // EndInspection raises InspectionEnded, which releases everything here.
            if (menuCamera && menuCamera.IsInspecting) menuCamera.EndInspection();
            else Release();
        }

        void HandleInspectionEnded() => Release();

        void Release()
        {
            if (!_active) return;
            _active = false;
            _velocity = Vector3.zero;
            ReleaseCursor();
            if (_navEventSystem) _navEventSystem.sendNavigationEvents = _savedSendNavigation;
            _navEventSystem = null;
        }

        bool TogglePressed()
        {
            if (!godModeEnabled && !_active) return false;
            var kb = Keyboard.current;
            if (kb != null && toggleKey != Key.None && kb[toggleKey].wasPressedThisFrame && !TextFieldHasFocus()) return true;
            var pad = Gamepad.current;
            return gamepadToggle && pad != null && pad.leftStickButton.wasPressedThisFrame;
        }

        static float Axis(bool positive, bool negative) => (positive ? 1f : 0f) - (negative ? 1f : 0f);

        void CaptureCursor()
        {
            if (_cursorCaptured) return;
            _savedLock = Cursor.lockState;
            _savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _cursorCaptured = true;
        }

        void ReleaseCursor()
        {
            if (!_cursorCaptured) return;
            Cursor.lockState = _savedLock;
            Cursor.visible = _savedVisible;
            _cursorCaptured = false;
        }

        static bool PointerOverUI()
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }

        static bool TextFieldHasFocus()
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null) return false;
            return selected.TryGetComponent<UnityEngine.UI.InputField>(out _) ||
                   selected.TryGetComponent<TMPro.TMP_InputField>(out _);
        }

        void OnGUI()
        {
            if (!_active || !showHint) return;
            _hintStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            GUI.Label(new Rect(12f, 8f, 1200f, 24f),
                $"GOD MODE  ·  {toggleKey} / L3 exit  ·  WASD move  ·  E/Space up  Q/Ctrl down  ·  RMB look  ·  " +
                $"Shift fast  Alt slow  ·  wheel speed {_speed:0.#}", _hintStyle);
        }
    }
}
