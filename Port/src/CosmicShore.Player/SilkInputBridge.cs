using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Engine.InputSystem;
using Silk.NET.Input;
using Silk.NET.Windowing;
using EKey = CosmicShore.Engine.InputSystem.Key;
using SKey = Silk.NET.Input.Key;
using EVector2 = CosmicShore.Engine.Vector2;

namespace CosmicShore.Player
{
    /// <summary>
    /// The player's input backend: turns Silk.NET (GLFW) window input into Input System device
    /// state — one <see cref="Keyboard"/>, one <see cref="Mouse"/> and one <see cref="Gamepad"/>
    /// per connected pad — the way Unity's native backend feeds its devices. Everything is a
    /// raw write that the engine's per-frame <c>InputSystem.Update</c> commits, so a script
    /// reads the same frame edges it would in a Unity player.
    ///
    /// A press that is released before the engine's next frame would be lost to a
    /// level-sampled device, so releases are deferred until the press has been committed
    /// (<see cref="AfterTick"/>) — a tap is always at least one frame long, as in Unity.
    /// </summary>
    public sealed class SilkInputBridge : IDisposable
    {
        readonly IView _window;
        readonly IInputContext _input;
        readonly Keyboard _keyboard;
        readonly Mouse _mouse;
        readonly Dictionary<IGamepad, Gamepad> _pads = new();

        readonly List<ButtonControl> _pendingRelease = new();
        readonly HashSet<ButtonControl> _pressedSinceTick = new();
        System.Numerics.Vector2 _delta, _scroll;

        public SilkInputBridge(IView window)
        {
            _window = window;
            _input = window.CreateInput();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();

            foreach (var kb in _input.Keyboards) Hook(kb);
            foreach (var m in _input.Mice) Hook(m);
            foreach (var g in _input.Gamepads) Hook(g);
            _input.ConnectionChanged += OnConnectionChanged;
        }

        void OnConnectionChanged(IInputDevice device, bool connected)
        {
            if (device is IGamepad g)
            {
                if (connected) Hook(g);
                else if (_pads.Remove(g, out var pad)) InputSystem.RemoveDevice(pad);
            }
        }

        // ── keyboard ─────────────────────────────────────────────────────

        void Hook(IKeyboard kb)
        {
            kb.KeyDown += (_, k, _) => { if (Map(k) is { } key) Press(_keyboard[key]); };
            kb.KeyUp += (_, k, _) => { if (Map(k) is { } key) Release(_keyboard[key]); };
            kb.KeyChar += (_, c) => { if (!char.IsControl(c)) _keyboard.RaiseTextInput(c); };
        }

        static EKey? Map(SKey k) => k switch
        {
            >= SKey.A and <= SKey.Z => EKey.A + (k - SKey.A),
            >= SKey.Number1 and <= SKey.Number9 => EKey.Digit1 + (k - SKey.Number1),
            SKey.Number0 => EKey.Digit0,
            >= SKey.F1 and <= SKey.F12 => EKey.F1 + (k - SKey.F1),
            >= SKey.Keypad0 and <= SKey.Keypad9 => EKey.Numpad0 + (k - SKey.Keypad0),
            SKey.Space => EKey.Space,
            SKey.Enter => EKey.Enter,
            SKey.Tab => EKey.Tab,
            SKey.GraveAccent => EKey.Backquote,
            SKey.Apostrophe => EKey.Quote,
            SKey.Semicolon => EKey.Semicolon,
            SKey.Comma => EKey.Comma,
            SKey.Period => EKey.Period,
            SKey.Slash => EKey.Slash,
            SKey.BackSlash => EKey.Backslash,
            SKey.LeftBracket => EKey.LeftBracket,
            SKey.RightBracket => EKey.RightBracket,
            SKey.Minus => EKey.Minus,
            SKey.Equal => EKey.Equals,
            SKey.ShiftLeft => EKey.LeftShift,
            SKey.ShiftRight => EKey.RightShift,
            SKey.AltLeft => EKey.LeftAlt,
            SKey.AltRight => EKey.RightAlt,
            SKey.ControlLeft => EKey.LeftCtrl,
            SKey.ControlRight => EKey.RightCtrl,
            SKey.SuperLeft => EKey.LeftMeta,
            SKey.SuperRight => EKey.RightMeta,
            SKey.Menu => EKey.ContextMenu,
            SKey.Escape => EKey.Escape,
            SKey.Left => EKey.LeftArrow,
            SKey.Right => EKey.RightArrow,
            SKey.Up => EKey.UpArrow,
            SKey.Down => EKey.DownArrow,
            SKey.Backspace => EKey.Backspace,
            SKey.PageDown => EKey.PageDown,
            SKey.PageUp => EKey.PageUp,
            SKey.Home => EKey.Home,
            SKey.End => EKey.End,
            SKey.Insert => EKey.Insert,
            SKey.Delete => EKey.Delete,
            SKey.CapsLock => EKey.CapsLock,
            SKey.NumLock => EKey.NumLock,
            SKey.PrintScreen => EKey.PrintScreen,
            SKey.ScrollLock => EKey.ScrollLock,
            SKey.Pause => EKey.Pause,
            SKey.KeypadEnter => EKey.NumpadEnter,
            SKey.KeypadDivide => EKey.NumpadDivide,
            SKey.KeypadMultiply => EKey.NumpadMultiply,
            SKey.KeypadAdd => EKey.NumpadPlus,
            SKey.KeypadSubtract => EKey.NumpadMinus,
            SKey.KeypadDecimal => EKey.NumpadPeriod,
            SKey.KeypadEqual => EKey.NumpadEquals,
            _ => null,
        };

        // ── mouse ────────────────────────────────────────────────────────

        void Hook(IMouse m)
        {
            m.MouseMove += (_, p) => MoveTo(p);
            m.MouseDown += (_, b) => { if (Button(b) is { } c) Press(c); };
            m.MouseUp += (_, b) => { if (Button(b) is { } c) Release(c); };
            m.Scroll += (_, w) => _scroll += new System.Numerics.Vector2(w.X, w.Y) * 120f; // Unity reports wheel notches ×120
        }

        System.Numerics.Vector2? _lastWindowPos;

        void MoveTo(System.Numerics.Vector2 windowPos)
        {
            // Window coordinates (top-left origin, logical pixels) → Unity screen space
            // (bottom-left origin, Screen pixels: the framebuffer, or the control port's
            // virtual resolution, which the window shows scaled).
            float fw = CosmicShore.Engine.Screen.width > 0 ? CosmicShore.Engine.Screen.width : _window.FramebufferSize.X;
            float fh = CosmicShore.Engine.Screen.height > 0 ? CosmicShore.Engine.Screen.height : _window.FramebufferSize.Y;
            float sx = _window.Size.X > 0 ? fw / _window.Size.X : 1f;
            float sy = _window.Size.Y > 0 ? fh / _window.Size.Y : 1f;
            var screen = new EVector2(windowPos.X * sx, fh - windowPos.Y * sy);
            _mouse.position.SetRaw(screen);
            if (_lastWindowPos is { } last)
                _delta += new System.Numerics.Vector2((windowPos.X - last.X) * sx, -(windowPos.Y - last.Y) * sy);
            _lastWindowPos = windowPos;
        }

        ButtonControl Button(MouseButton b) => b switch
        {
            MouseButton.Left => _mouse.leftButton,
            MouseButton.Right => _mouse.rightButton,
            MouseButton.Middle => _mouse.middleButton,
            MouseButton.Button4 => _mouse.backButton,
            MouseButton.Button5 => _mouse.forwardButton,
            _ => null,
        };

        // ── gamepads ─────────────────────────────────────────────────────

        void Hook(IGamepad g)
        {
            if (_pads.ContainsKey(g)) return;
            var pad = InputSystem.AddDevice<Gamepad>(g.Name);
            _pads[g] = pad;
            g.ButtonDown += (_, b) => { if (PadButton(pad, b.Name) is { } c) Press(c); };
            g.ButtonUp += (_, b) => { if (PadButton(pad, b.Name) is { } c) Release(c); };
        }

        static ButtonControl PadButton(Gamepad pad, ButtonName b) => b switch
        {
            ButtonName.A => pad.buttonSouth,
            ButtonName.B => pad.buttonEast,
            ButtonName.X => pad.buttonWest,
            ButtonName.Y => pad.buttonNorth,
            ButtonName.LeftBumper => pad.leftShoulder,
            ButtonName.RightBumper => pad.rightShoulder,
            ButtonName.Back => pad.selectButton,
            ButtonName.Start => pad.startButton,
            ButtonName.LeftStick => pad.leftStickButton,
            ButtonName.RightStick => pad.rightStickButton,
            ButtonName.DPadUp => pad.dpad.up,
            ButtonName.DPadDown => pad.dpad.down,
            ButtonName.DPadLeft => pad.dpad.left,
            ButtonName.DPadRight => pad.dpad.right,
            _ => null,
        };

        /// <summary>Samples the analogue pad state (GLFW polls these) — call before each engine tick.</summary>
        public void BeforeTick()
        {
            foreach (var (g, pad) in _pads)
            {
                var sticks = g.Thumbsticks;
                if (sticks.Count > 0) pad.leftStick.SetRaw(new EVector2(sticks[0].X, -sticks[0].Y));
                if (sticks.Count > 1) pad.rightStick.SetRaw(new EVector2(sticks[1].X, -sticks[1].Y));
                var triggers = g.Triggers;
                // GLFW reports triggers in [-1, 1] with -1 at rest; Unity's are [0, 1].
                if (triggers.Count > 0) pad.leftTrigger.SetRaw(Math.Clamp((triggers[0].Position + 1f) * 0.5f, 0f, 1f));
                if (triggers.Count > 1) pad.rightTrigger.SetRaw(Math.Clamp((triggers[1].Position + 1f) * 0.5f, 0f, 1f));
            }
            _mouse.delta.SetRaw(new EVector2(_delta.X, _delta.Y));
            _mouse.scroll.SetRaw(new EVector2(_scroll.X, _scroll.Y));
            _delta = default;
            _scroll = default;
        }

        /// <summary>The engine has committed a frame: every press is now visible, so deferred releases may land.</summary>
        public void AfterTick()
        {
            _pressedSinceTick.Clear();
            foreach (var c in _pendingRelease) c.SetRaw(false);
            _pendingRelease.Clear();
        }

        void Press(ButtonControl c)
        {
            _pendingRelease.Remove(c);
            _pressedSinceTick.Add(c);
            c.SetRaw(true);
        }

        void Release(ButtonControl c)
        {
            if (_pressedSinceTick.Contains(c)) { if (!_pendingRelease.Contains(c)) _pendingRelease.Add(c); }
            else c.SetRaw(false);
        }

        public void Dispose()
        {
            _input.ConnectionChanged -= OnConnectionChanged;
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.RemoveDevice(_mouse);
            foreach (var pad in _pads.Values) InputSystem.RemoveDevice(pad);
            _pads.Clear();
            _input.Dispose();
        }
    }
}
