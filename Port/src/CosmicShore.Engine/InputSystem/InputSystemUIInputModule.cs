using CosmicShore.Engine.UI;

namespace CosmicShore.Engine.InputSystem.UI
{
    /// <summary>
    /// The Input-System-driven UI module (original: InputSystemUIInputModule). The pointer
    /// state machine is the standalone module's; this adds the action references the game
    /// toggles (move/submit/cancel) and feeds the pointer from <see cref="Mouse.current"/>
    /// each frame when no other driver does.
    /// </summary>
    public class InputSystemUIInputModule : StandaloneInputModule
    {
        public InputActionReference point, leftClick, rightClick, middleClick, scrollWheel,
            move, submit, cancel, trackedDevicePosition, trackedDeviceOrientation;

        public InputActionAsset actionsAsset { get; set; }
        public bool deselectOnBackgroundClick = true;
        public float moveRepeatDelay = 0.5f, moveRepeatRate = 0.1f;

        /// <summary>When true the module polls <see cref="Mouse.current"/>; the client may drive the synthetic API directly instead.</summary>
        public bool pollMouse = true;

        bool _wasDown;
        Vector2 _last = new(float.NaN, float.NaN);
        MoveDirection _heldDir = MoveDirection.None;
        float _nextRepeat;

        void Update()
        {
            if (!pollMouse) return;
            ProcessNavigation();
            var mouse = Mouse.current;
            if (mouse == null) return;
            var p = mouse.position.ReadValue();
            bool down = mouse.leftButton.isPressed;
            if (p != _last) { PointerMove(p); _last = p; }
            if (down && !_wasDown) PointerDown(p);
            else if (!down && _wasDown) PointerUp(p);
            var scroll = mouse.scroll.ReadValue();
            if (scroll != Vector2.zero) Scroll(scroll, p);
            _wasDown = down;
        }

        /// <summary>
        /// The default UI action map's Navigate / Submit / Cancel (arrow keys, WASD-free, gamepad
        /// dpad + left stick, Enter / gamepad South, Escape / gamepad East), with the original's
        /// repeat delay and rate. A focused input field owns the keyboard, so keyboard navigation
        /// stands down while one has focus (gamepad navigation still moves off it).
        /// </summary>
        void ProcessNavigation()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            bool fieldFocused = selected != null && selected.TryGetComponent<TMP_InputField>(out var field) && field.isFocused;

            Vector2 v = Vector2.zero;
            if (kb != null && !fieldFocused)
            {
                if (kb.leftArrowKey.isPressed) v.x -= 1f;
                if (kb.rightArrowKey.isPressed) v.x += 1f;
                if (kb.upArrowKey.isPressed) v.y += 1f;
                if (kb.downArrowKey.isPressed) v.y -= 1f;
            }
            if (pad != null)
            {
                v += pad.dpad.ReadValue();
                var stick = pad.leftStick.ReadValue();
                if (stick.magnitude > 0.5f) v += stick;
            }

            var dir = MoveDirection.None;
            if (v.sqrMagnitude > 0.25f)
                dir = System.Math.Abs(v.x) > System.Math.Abs(v.y)
                    ? (v.x > 0 ? MoveDirection.Right : MoveDirection.Left)
                    : (v.y > 0 ? MoveDirection.Up : MoveDirection.Down);

            float now = Time.unscaledTime;
            if (dir == MoveDirection.None) _heldDir = MoveDirection.None;
            else if (dir != _heldDir) { _heldDir = dir; _nextRepeat = now + moveRepeatDelay; Move(dir); }
            else if (now >= _nextRepeat) { _nextRepeat = now + moveRepeatRate; Move(dir); }

            bool submit = (kb != null && !fieldFocused && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                          || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool cancel = (kb != null && !fieldFocused && kb.escapeKey.wasPressedThisFrame)
                          || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (submit) Submit();
            if (cancel) Cancel();
        }

        public void AssignDefaultActions() { }
        public void UnassignActions() { }
    }
}
