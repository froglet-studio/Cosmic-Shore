using System.Collections.Generic;
using CosmicShore.Engine.InputSystem.Controls;
using ETouch = CosmicShore.Engine.InputSystem.EnhancedTouch.Touch;

namespace CosmicShore.Engine.InputSystem
{
    /// <summary>
    /// The native touch backend's per-frame feed (what Unity's platform layer does under
    /// EnhancedTouch): a platform hands it the fingers that are down this frame, and it writes
    /// <see cref="ETouch.activeTouches"/> and the <see cref="Touchscreen"/> device — Began on the
    /// first frame a finger is down, Moved/Stationary while it stays, and Ended for exactly one
    /// frame after it lifts. Touch ids are stable for a finger's whole contact and never reused.
    /// </summary>
    public sealed class TouchFeed
    {
        public readonly struct Finger
        {
            public readonly long Key;          // the platform's id for this contact
            public readonly Vector2 Position;  // screen pixels, bottom-left origin
            public Finger(long key, Vector2 position) { Key = key; Position = position; }
        }

        sealed class Tracked { public int Id; public Vector2 Start, Last; public double StartTime; public bool Lifted; }

        readonly Touchscreen _screen;
        readonly Dictionary<long, Tracked> _live = new();
        readonly List<long> _scratch = new();
        int _nextId = 1;
        double _time;

        /// <summary>Movement under this many pixels in a frame reads as Stationary.</summary>
        public float MoveThreshold = 0.5f;

        public TouchFeed(Touchscreen screen) { _screen = screen; }

        public void Update(IReadOnlyList<Finger> down, float dt)
        {
            _time += dt;
            ETouch.activeTouches.Clear();
            _scratch.Clear();
            foreach (var (key, t) in _live) if (t.Lifted) _scratch.Add(key);
            foreach (var key in _scratch) _live.Remove(key);
            foreach (var t in _live.Values) t.Lifted = true;

            foreach (var f in down)
            {
                TouchPhase phase;
                if (!_live.TryGetValue(f.Key, out var t))
                {
                    t = new Tracked { Id = _nextId++, Start = f.Position, Last = f.Position, StartTime = _time };
                    _live[f.Key] = t;
                    phase = TouchPhase.Began;
                }
                else phase = (f.Position - t.Last).sqrMagnitude > MoveThreshold * MoveThreshold ? TouchPhase.Moved : TouchPhase.Stationary;
                t.Lifted = false;
                ETouch.activeTouches.Add(new ETouch
                {
                    screenPosition = f.Position, startScreenPosition = t.Start, delta = f.Position - t.Last,
                    phase = phase, touchId = t.Id, startTime = t.StartTime, time = _time,
                });
                t.Last = f.Position;
            }
            foreach (var t in _live.Values)
                if (t.Lifted)
                    ETouch.activeTouches.Add(new ETouch
                    {
                        screenPosition = t.Last, startScreenPosition = t.Start, delta = default,
                        phase = TouchPhase.Ended, touchId = t.Id, startTime = t.StartTime, time = _time,
                    });

            if (_screen == null) return;
            var controls = _screen.touches;
            for (int i = 0; i < controls.Count; i++)
                if (i < ETouch.activeTouches.Count) Write(controls[i], ETouch.activeTouches[i]);
                else Clear(controls[i]);
            if (ETouch.activeTouches.Count > 0) Write(_screen.primaryTouch, ETouch.activeTouches[0]);
            else Clear(_screen.primaryTouch);
        }

        static void Write(TouchControl c, in ETouch t)
        {
            c.press.SetRaw(t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled);
            c.position.SetRaw(t.screenPosition);
            c.delta.SetRaw(t.delta);
            c.startPosition.SetRaw(t.startScreenPosition);
            c.touchId.value = t.touchId;
            c.phase = t.phase;
        }

        static void Clear(TouchControl c) { c.press.SetRaw(false); c.delta.SetRaw(default); c.phase = TouchPhase.None; }
    }
}
