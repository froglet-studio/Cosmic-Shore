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
    /// <para><b>Flight</b> is <see cref="FreeFlyCamera"/>, shared with the in-game AI Diagnose camera (unscaled time, so it still flies while the menu is paused): WASD move, E / Space up,
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
        [Tooltip("The free-fly model (speeds, glide, look). Shared with the in-game AI Diagnose panel's Free fly, " +
                 "which uses these same defaults.")]
        [SerializeField] FreeFlyCamera flight = new();

        [Header("Overlay")]
        [Tooltip("Show a one-line reminder of the controls and the current speed while in god mode.")]
        [SerializeField] bool showHint = true;

        bool _active;
        Transform _camT;
        EventSystem _navEventSystem;
        bool _savedSendNavigation;

        GUIStyle _hintStyle;

        /// <summary>Whether god mode is flying the camera right now.</summary>
        public bool IsActive => _active;

        void Awake()
        {
            if (!menuCamera) menuCamera = GetComponent<MainMenuCameraController>();
        }

        /// <summary>The enabled god-mode camera, if any. The in-game Third Eye leaves F2 to it while one exists.</summary>
        public static MenuGodModeCamera Current { get; private set; }

        void OnEnable()
        {
            Current = this;
            if (menuCamera) menuCamera.InspectionEnded += HandleInspectionEnded;
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
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

            Vector3 pos = _camT.position;
            flight.Tick(dt, keys: !TextFieldHasFocus(), look: true, wheel: !PointerOverUI(), ref pos, out Quaternion rot);
            _camT.SetPositionAndRotation(pos, rot);
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
            flight.Begin(_camT.rotation);
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
            flight.End();
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
                $"Shift fast  Alt slow  ·  wheel speed {flight.Speed:0.#}", _hintStyle);
        }
    }
}
