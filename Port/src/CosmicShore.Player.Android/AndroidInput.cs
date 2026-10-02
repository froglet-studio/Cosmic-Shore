using System.Collections.Generic;
using Silk.NET.SDL;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.Engine.InputSystem;
using CosmicShore.Engine.InputSystem.EnhancedTouch;
using EngineTouchPhase = CosmicShore.Engine.InputSystem.TouchPhase;
using EngineTouch = CosmicShore.Engine.InputSystem.EnhancedTouch.Touch;
using Screen = CosmicShore.Engine.Screen;

namespace CosmicShore.Player
{
    /// <summary>SDL hints the Android head sets before the view (and SDL's video subsystem) comes up.</summary>
    static class AndroidSdl
    {
        public static void ConfigureHints()
        {
            var sdl = Sdl.GetApi();
            // A finger also drives SDL's mouse, which is what the player's input bridge feeds to the
            // Input System's Mouse - and the uGUI module reads the pointer from Mouse.current. So a
            // tap presses buttons exactly as a click does, with no second pointer path to keep in step.
            sdl.SetHint(Sdl.HintTouchMouseEvents, "1");
            sdl.SetHint(Sdl.HintMouseTouchEvents, "0");
            sdl.SetHint(Sdl.HintOrientations, "LandscapeLeft LandscapeRight");
            // Back arrives as a key the game can read instead of finishing the activity.
            sdl.SetHint(Sdl.HintAndroidTrapBackButton, "1");
            sdl.SetHint(Sdl.HintAndroidBlockOnPause, "1");
        }
    }

    /// <summary>
    /// The touch backend: each Update mirrors SDL's finger state into the Input System - the
    /// EnhancedTouch <c>Touch.activeTouches</c> list the game's TouchInputStrategy reads verbatim (the
    /// authentic dual-thumb scheme the Unity build flies with), plus a <see cref="Touchscreen"/>
    /// device so <c>Input.touchSupported</c> answers as it does on a phone.
    ///
    /// SDL reports fingers normalised [0..1] with y = 0 at the TOP; Unity screen space puts y = 0
    /// at the BOTTOM, so y is flipped. A finger id unseen last frame is Began, else Moved (or
    /// Stationary when it has not moved). Lifted fingers leave the list, which is what the
    /// strategy keys its lift transitions off (the July head shipped this rule and it was
    /// verified on device).
    /// </summary>
    public sealed unsafe class AndroidTouchBridge
    {
        readonly Sdl _sdl = Sdl.GetApi();
        readonly Dictionary<long, (Vector2 start, Vector2 last, double startTime)> _previous = new();
        readonly Dictionary<long, (Vector2 start, Vector2 last, double startTime)> _current = new();
        Touchscreen _screen;

        public void Install()
        {
            EnhancedTouchSupport.Enable();
            _screen ??= InputSystem.AddDevice<Touchscreen>();
            _screen.MakeCurrent();
        }

        public void Pump()
        {
            var touches = EngineTouch.activeTouches;
            touches.Clear();
            _current.Clear();
            double now = Time.realtimeSinceStartupAsDouble;

            int deviceCount = _sdl.GetNumTouchDevices();
            for (int device = 0; device < deviceCount; device++)
            {
                long touchId = _sdl.GetTouchDevice(device);
                if (touchId == 0) continue;
                int fingers = _sdl.GetNumTouchFingers(touchId);
                for (int i = 0; i < fingers; i++)
                {
                    Finger* finger = _sdl.GetTouchFinger(touchId, i);
                    if (finger == null) continue;
                    long id = finger->Id;
                    var pos = new Vector2(finger->X * Screen.width, (1f - finger->Y) * Screen.height);
                    bool seen = _previous.TryGetValue(id, out var prev);
                    var start = seen ? prev.start : pos;
                    double startTime = seen ? prev.startTime : now;
                    var delta = seen ? pos - prev.last : Vector2.zero;
                    _current[id] = (start, pos, startTime);
                    touches.Add(new EngineTouch
                    {
                        touchId = (int)id,
                        screenPosition = pos,
                        startScreenPosition = start,
                        delta = delta,
                        startTime = startTime,
                        time = now,
                        phase = !seen ? EngineTouchPhase.Began
                              : delta.sqrMagnitude > 0f ? EngineTouchPhase.Moved
                              : EngineTouchPhase.Stationary,
                    });
                }
            }

            _previous.Clear();
            foreach (var kv in _current) _previous[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// Raises and lowers Android's on-screen keyboard to match the game's text fields: while an
    /// InputField / TMP_InputField holds focus, SDL text input is on (which shows the keyboard and
    /// streams typed characters through the same KeyChar path a desktop keyboard uses); when focus
    /// leaves, it is switched off again. The age gate and the username prompt both need it.
    /// </summary>
    public sealed class SoftKeyboardBridge
    {
        readonly Sdl _sdl = Sdl.GetApi();
        bool _active;

        public void Pump()
        {
            bool want = FieldFocused();
            if (want == _active) return;
            _active = want;
            if (want) _sdl.StartTextInput();
            else _sdl.StopTextInput();
        }

        static bool FieldFocused()
        {
            var es = EventSystem.current;
            var go = es != null ? es.currentSelectedGameObject : null;
            if (go == null) return false;
            var tmp = go.GetComponent<TMP_InputField>();
            if (tmp != null && tmp.isFocused) return true;
            var legacy = go.GetComponent<InputField>();
            return legacy != null && legacy.isFocused;
        }
    }
}
