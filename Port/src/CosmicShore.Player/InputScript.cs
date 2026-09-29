using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CosmicShore.Engine;
using CosmicShore.Engine.InputSystem;
using EKey = CosmicShore.Engine.InputSystem.Key;

namespace CosmicShore.Player
{
    /// <summary>
    /// Scripted input for unattended runs: <c>--do FRAME:ACTION</c>, written through the same
    /// Input System devices the window backend feeds, so a script exercises exactly the path a
    /// player's hands do. Coordinates are screenshot pixels (top-left origin).
    ///
    ///   click X,Y      press + release the left mouse button at a point
    ///   move X,Y       move the pointer
    ///   type TEXT      text input, one character per call (as a keyboard's KeyChar stream)
    ///   key NAME       tap a key (Input System Key name: Enter, Escape, Tab, UpArrow…)
    ///   hold NAME N    hold a key for N frames
    ///   pad BUTTON     tap a gamepad button (buttonSouth, startButton, dpad.up…)
    /// </summary>
    public sealed class InputScript
    {
        readonly SortedDictionary<int, List<string>> _steps = new();
        readonly List<(int frame, Action release)> _releases = new();
        readonly List<(int until, Action act)> _repeats = new();
        Keyboard _kb;
        Mouse _mouse;
        Gamepad _pad;

        public bool IsEmpty => _steps.Count == 0;
        public int LastFrame => _steps.Count == 0 ? 0 : _steps.Keys.Max() + 4;

        public void Add(string spec)
        {
            int colon = spec.IndexOf(':');
            if (colon <= 0 || !int.TryParse(spec[..colon], out int frame))
                throw new ArgumentException($"--do expects FRAME:ACTION, got '{spec}'");
            if (!_steps.TryGetValue(frame, out var list)) _steps[frame] = list = new List<string>();
            list.Add(spec[(colon + 1)..].Trim());
        }

        /// <summary>Ensures a keyboard/mouse/pad exist (headless has no window backend to add them).</summary>
        public void EnsureDevices()
        {
            _kb = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            _mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            _pad = Gamepad.current ?? InputSystem.AddDevice<Gamepad>("ScriptPad");
        }

        /// <summary>Runs the steps due this frame (call before the engine tick).</summary>
        public void BeforeTick(int frame)
        {
            for (int i = _repeats.Count - 1; i >= 0; i--)
            {
                if (_repeats[i].until < frame) { _repeats.RemoveAt(i); continue; }
                _repeats[i].act();
            }
            for (int i = _releases.Count - 1; i >= 0; i--)
                if (_releases[i].frame <= frame) { _releases[i].release(); _releases.RemoveAt(i); }

            if (!_steps.TryGetValue(frame, out var steps)) return;
            foreach (var step in steps)
            {
                Console.WriteLine($"[input] frame {frame}: {step}");
                int sp = step.IndexOf(' ');
                string verb = sp < 0 ? step : step[..sp];
                string arg = sp < 0 ? string.Empty : step[(sp + 1)..];
                switch (verb)
                {
                    case "move": _mouse.position.SetRaw(Point(arg)); break;
                    case "click":
                        _mouse.position.SetRaw(Point(arg));
                        _mouse.leftButton.SetRaw(true);
                        _releases.Add((frame + 2, () => _mouse.leftButton.SetRaw(false)));
                        break;
                    case "type":
                        foreach (char c in arg) _kb.RaiseTextInput(c);
                        break;
                    case "key":
                        Tap(_kb[Enum.Parse<EKey>(arg, ignoreCase: true)], frame, 2);
                        break;
                    case "hold":
                    {
                        var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        Tap(_kb[Enum.Parse<EKey>(parts[0], ignoreCase: true)], frame, int.Parse(parts[1], CultureInfo.InvariantCulture));
                        break;
                    }
                    case "pad":
                        Tap(PadButton(arg), frame, 2);
                        break;
                    case "inspect":
                    {
                        // inspect OBJECT COMPONENT — every field of that component, this frame.
                        var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        Inspector.Print(parts[0], parts.Length > 1 ? parts[1] : null);
                        break;
                    }
                    case "ancestry":
                        Inspector.Ancestry(arg.Trim());
                        break;
                    case "trails":
                        Inspector.Trails();
                        break;
                    case "renderers":
                        Inspector.Renderers(arg.Trim());
                        break;
                    case "lit":
                    {
                        var a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        float radius = float.Parse(a[0], CultureInfo.InvariantCulture);
                        int frames = a.Length > 1 ? int.Parse(a[1], CultureInfo.InvariantCulture) : 60;
                        var light = Inspector.LitSphere(radius);
                        if (light != null) _repeats.Add((frame + frames, light));
                        break;
                    }
                    case "blast":
                        Inspector.Blast(int.Parse(arg.Trim(), CultureInfo.InvariantCulture));
                        break;
                    case "eval":
                        // eval Type.StaticMember[.member…] — read a static chain, this frame.
                        Inspector.PrintStatic(arg);
                        break;
                    default:
                        Console.WriteLine($"[input] unknown action '{verb}'");
                        break;
                }
            }
        }

        void Tap(ButtonControl c, int frame, int frames)
        {
            c.SetRaw(true);
            _releases.Add((frame + Math.Max(1, frames), () => c.SetRaw(false)));
        }

        ButtonControl PadButton(string name)
        {
            object cur = _pad;
            foreach (var part in name.Split('.'))
                cur = cur.GetType().GetProperty(part)?.GetValue(cur) ?? throw new ArgumentException($"no gamepad control '{name}'");
            return (ButtonControl)cur;
        }

        static Vector2 Point(string arg)
        {
            var p = arg.Split(',');
            float x = float.Parse(p[0], CultureInfo.InvariantCulture);
            float y = float.Parse(p[1], CultureInfo.InvariantCulture);
            return new Vector2(x, Screen.height - y); // screenshot pixels → Unity screen space
        }
    }
}
