using System;
using System.Collections.Generic;
using CosmicShore.Engine.InputSystem;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using EVector2 = CosmicShore.Engine.Vector2;

namespace CosmicShore.Mobile
{
    /// <summary>
    /// The phone's touch backend: reads SDL's finger state once per engine frame and hands it to
    /// the engine's <see cref="TouchFeed"/>, which writes the Touchscreen device and the
    /// EnhancedTouch list the game's TouchInputStrategy reads. SDL also turns the FIRST finger
    /// into mouse events (its default), which is what uGUI clicks with — the single-pointer UI
    /// behaviour Unity shows on a phone.
    /// </summary>
    public sealed unsafe class TouchBridge : IDisposable
    {
        readonly Sdl _sdl;
        readonly IView _view;
        readonly Touchscreen _screen;
        readonly TouchFeed _feed;
        readonly List<TouchFeed.Finger> _down = new();

        public TouchBridge(IView view)
        {
            _view = view;
            _sdl = Sdl.GetApi();
            _screen = InputSystem.AddDevice<Touchscreen>();
            _screen.MakeCurrent();
            _feed = new TouchFeed(_screen);
        }

        public void BeforeTick(float dt)
        {
            int w = _view.FramebufferSize.X, h = _view.FramebufferSize.Y;
            _down.Clear();
            int devices = _sdl.GetNumTouchDevices();
            for (int d = 0; d < devices; d++)
            {
                long device = _sdl.GetTouchDevice(d);
                int fingers = _sdl.GetNumTouchFingers(device);
                for (int i = 0; i < fingers; i++)
                {
                    Finger* f = _sdl.GetTouchFinger(device, i);
                    if (f == null) continue;
                    // SDL: normalised, top-left origin. Unity: pixels, bottom-left origin.
                    _down.Add(new TouchFeed.Finger(device * 1_000_003L + f->Id, new EVector2(f->X * w, (1f - f->Y) * h)));
                }
            }
            _feed.Update(_down, dt);
            _sdl.ClearError();   // see MobileHost: a stale SDL error would fail Silk's next call
        }

        public void Dispose()
        {
            _feed.Update(Array.Empty<TouchFeed.Finger>(), 0f);
            CosmicShore.Engine.InputSystem.EnhancedTouch.Touch.activeTouches.Clear();
            InputSystem.RemoveDevice(_screen);
        }
    }
}
