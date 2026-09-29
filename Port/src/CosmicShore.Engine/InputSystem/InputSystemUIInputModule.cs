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

        void Update()
        {
            if (!pollMouse) return;
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

        public void AssignDefaultActions() { }
        public void UnassignActions() { }
    }
}
