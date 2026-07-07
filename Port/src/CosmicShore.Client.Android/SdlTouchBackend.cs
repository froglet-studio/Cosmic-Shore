using System.Collections.Generic;
using Silk.NET.SDL;
using CosmicShore.Engine;
using EngineTouchPhase = CosmicShore.Engine.InputSystem.TouchPhase;
using EngineTouch = CosmicShore.Engine.InputSystem.EnhancedTouch.Touch;
using Screen = CosmicShore.Engine.Screen;

namespace CosmicShore.Client.Android
{
    /// <summary>
    /// The touch backend the engine's inert EnhancedTouch shim was waiting for
    /// (see CosmicShore.Engine.InputSystem — "zero active touches until a touch
    /// backend lands"). Each Update tick this polls SDL's finger state and mirrors
    /// it into <c>Touch.activeTouches</c>, which the ported TouchInputStrategy
    /// reads verbatim — the game's authentic dual-thumb scheme, untouched.
    ///
    /// SDL reports fingers normalized [0..1] with y=0 at the TOP of the surface;
    /// Unity screen coordinates put y=0 at the BOTTOM — flipped here so the
    /// strategy's bottom-corner virtual thumbsticks land where thumbs do.
    /// Phases: a finger id unseen last frame is Began, else Moved. Lifted fingers
    /// simply leave the list — the strategy keys its lift transitions off the
    /// touch count, not an Ended phase.
    /// </summary>
    public sealed unsafe class SdlTouchBackend
    {
        readonly Sdl _sdl = Sdl.GetApi();
        HashSet<long> _previous = new();
        HashSet<long> _current = new();

        public void Pump()
        {
            var touches = EngineTouch.activeTouches;
            touches.Clear();
            _current.Clear();

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
                    _current.Add(id);
                    touches.Add(new EngineTouch
                    {
                        touchId = (int)id,
                        screenPosition = new Vector2(
                            finger->X * Screen.width,
                            (1f - finger->Y) * Screen.height),
                        phase = _previous.Contains(id) ? EngineTouchPhase.Moved : EngineTouchPhase.Began,
                    });
                }
            }

            (_previous, _current) = (_current, _previous);
        }
    }
}
