using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CosmicShore.Engine;
using CosmicShore.Engine.InputSystem;
using CosmicShore.Engine.InputSystem.Controls;
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
            for (int i = _releases.Count - 1; i >= 0; i--)
                if (_releases[i].frame <= frame) { _releases[i].release(); _releases.RemoveAt(i); }

            // Every step due at or before this frame: the window's update and render callbacks are
            // not 1:1, so a frame index can pass without an update ever seeing it.
            if (_steps.Count == 0 || _steps.Keys.First() > frame) return;
            var due = new List<string>();
            while (_steps.Count > 0 && _steps.Keys.First() <= frame)
            {
                int key = _steps.Keys.First();
                due.AddRange(_steps[key]);
                _steps.Remove(key);
            }
            foreach (var step in due)
            {
                Console.WriteLine($"[input] frame {frame}: {step}");
                Run(step, frame);
            }
        }

        /// <summary>Runs one action now (the control port's path; a script runs it when its frame is due).</summary>
        public void Run(string step, int frame)
        {
            {
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
                    case "buttons":
                        Inspector.Buttons(arg.Trim());
                        break;
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
                        if (light != null) ScriptTicker.Run("lit", light, frames);
                        break;
                    }
                    case "cradle":
                    {
                        var a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        float radius = float.Parse(a[0], CultureInfo.InvariantCulture);
                        int frames = a.Length > 1 ? int.Parse(a[1], CultureInfo.InvariantCulture) : 60;
                        var hull = Inspector.CradleHull(radius);
                        if (hull != null) ScriptTicker.Run("cradle", hull, frames);
                        break;
                    }
                    case "timescale": Time.timeScale = float.Parse(arg.Trim(), CultureInfo.InvariantCulture); Console.WriteLine($"[input] timeScale {Time.timeScale}"); break;
                    case "lookat": Inspector.LookAt(float.Parse(arg.Trim(), CultureInfo.InvariantCulture)); break;
                    case "vessels": Inspector.Vessels(); break;
                    case "party": Inspector.Party(arg.Trim()); break;
                    case "domain": Inspector.Domain(arg.Trim()); break;
                    case "arcade": Inspector.Arcade(arg.Trim()); break;
                    case "score": Inspector.Score(arg.Trim()); break;
                    case "stage": Inspector.Stage(float.Parse(arg.Trim(), CultureInfo.InvariantCulture)); break;
                    case "animators":
                        foreach (var an in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Engine.Animator>(CosmicShore.Engine.FindObjectsSortMode.None))
                            if (an.isActiveAndEnabled && (arg.Length == 0 || an.name.Contains(arg.Trim(), StringComparison.OrdinalIgnoreCase)))
                                Console.WriteLine("[animator] " + an.DebugSummary());
                        break;
                    case "slice": Inspector.Slice(int.Parse(arg.Trim(), CultureInfo.InvariantCulture)); break;
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
