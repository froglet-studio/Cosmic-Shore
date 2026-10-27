using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// The music-video director: a camera, a clapperboard and a frame-accurate schedule inside a
    /// scripted run, driven by the <c>mv</c> verb (control port or <c>--do</c>). It poses the game's
    /// replay camera every frame around a SUBJECT (a vessel or a named creature), stands the HUD
    /// down, puts the local pilot on autopilot, and starts a <see cref="FrameRecorder"/> range from
    /// the next frame — so a shot list computed from a song's beats lands on exact game frames
    /// (a scripted run steps every clock by 1/60 s, so frame 300 is always game second 5).
    ///
    /// Commands (every number invariant-culture; SUBJECT is <c>local</c>, <c>ai0</c>…<c>aiN</c>
    /// (the non-local vessels, in spawn order), <c>nearest</c> (the non-local vessel closest to the
    /// local one), or <c>name:TEXT</c> (the first active object whose name contains TEXT, e.g. a
    /// kaiju's worm head), or <c>fastest</c> (the vessel with the highest speed; a subject that stops dead for
    /// half a second hands over to it)):
    ///
    ///   mv hud off|on                      hide / restore every Canvas
    ///   mv autopilot on|off                the local pilot flies itself (and its stick is paused)
    ///   mv orbit SUBJECT DIST HEIGHT DEGPS [PHASE]   orbit the subject (world up), DEGPS degrees / s
    ///   mv chase SUBJECT BACK UP [LEAD] [SMOOTH]     ride behind the subject, looking LEAD units ahead
    ///   mv dolly X,Y,Z X,Y,Z FRAMES [SUBJECT|X,Y,Z]  straight move over FRAMES, looking at a subject or point
    ///   mv flyby SUBJECT SIDE AHEAD UP FRAMES        pass the subject from AHEAD units in front to AHEAD behind
    ///   mv fov DEGREES                     field of view (also a per-shot default)
    ///   mv pulse AMOUNT FRAMES             a beat accent: the FOV tightens by AMOUNT degrees and decays
    ///   mv shake AMOUNT FRAMES             a beat accent: positional jitter of AMOUNT units, decaying
    ///   mv record DIR FRAMES [EVERY]       record FRAMES frames (every EVERYth) from the NEXT frame
    ///   mv in N COMMAND…                   run COMMAND N frames from now (frame-accurate scheduling)
    ///   mv cue N COMMAND…                  run COMMAND N frames after the NEXT record's first frame
    ///   mv camera main|replay              which camera to pose: Camera.main with its rig stood down (default,
    ///                                      keeps the gameplay look) or the CameraManager's replay camera
    ///   mv release                         give the camera back to the game
    ///   mv status                          print the camera, subject and schedule
    /// </summary>
    public static class MusicVideoDirector
    {
        enum CamMode { None, Orbit, Chase, Dolly, Flyby }

        static CamMode _mode = CamMode.None;
        static Transform _cam;
        static bool _manual;
        /// <summary>Pose Camera.main with its rig stood down (keeps the gameplay camera's look) instead of the replay camera.</summary>
        static bool _useMain = true;
        static string _subject = "local";
        static float _dist = 60f, _height = 20f, _degPerSec = 30f, _phase, _lead = 30f, _smooth = 6f;
        static Vector3 _p0, _p1, _lookPoint;
        static string _lookSubject;
        static int _moveFrames = 120, _moveStart = -1;
        static float _side = 25f, _ahead = 120f;
        static float _fov = 60f, _pulseAmount, _pulseLeft, _pulseFrames, _shakeAmount, _shakeLeft, _shakeFrames;
        static Vector3 _smoothedFwd, _smoothedUp; static bool _haveSmoothed;
        static readonly List<Canvas> _hiddenCanvases = new();
        static readonly SortedDictionary<int, List<string>> _schedule = new();
        static readonly List<(int at, string cmd)> _cues = new();
        static int _frame;
        static readonly System.Random _rng = new(7);

        public static void Command(string arg)
        {
            var words = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) { Console.WriteLine("[mv] usage: see MusicVideoDirector"); return; }
            try { Run(words); }
            catch (Exception e) { Console.WriteLine($"[mv] '{arg}' failed: {e.Message}"); }
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static Vector3 V(string s) { var p = s.Split(','); return new Vector3(F(p[0]), F(p[1]), F(p[2])); }

        static void Run(string[] w)
        {
            switch (w[0])
            {
                case "in":
                {
                    int at = _frame + int.Parse(w[1], CultureInfo.InvariantCulture);
                    string cmd = string.Join(' ', w.Skip(2));
                    if (!_schedule.TryGetValue(at, out var list)) _schedule[at] = list = new List<string>();
                    list.Add(cmd);
                    Console.WriteLine($"[mv] scheduled frame {at}: {cmd}");
                    return;
                }
                case "cue":
                {
                    // Relative to the NEXT record's first frame, so a beat computed from the song lands on its exact frame.
                    int at = int.Parse(w[1], CultureInfo.InvariantCulture);
                    _cues.Add((at, string.Join(' ', w.Skip(2))));
                    return;
                }
                case "hud": if (w[1] == "off") HideHud(); else ShowHud(); return;
                case "autopilot": Autopilot(w[1] == "on"); return;
                case "fov": _fov = F(w[1]); ApplyFov(); Console.WriteLine($"[mv] fov {_fov}"); return;
                case "pulse": _pulseAmount = F(w[1]); _pulseFrames = _pulseLeft = F(w[2]); return;
                case "shake": _shakeAmount = F(w[1]); _shakeFrames = _shakeLeft = F(w[2]); return;
                case "record":
                {
                    // DIR may hold spaces: the trailing one or two numeric words are FRAMES [EVERY], the rest is the path.
                    int n = w.Length - 1, every = 1;
                    if (n >= 3 && int.TryParse(w[n], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ev) && int.TryParse(w[n - 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) { every = ev; n--; }
                    int frames = int.Parse(w[n], CultureInfo.InvariantCulture);
                    string dir = string.Join(' ', w, 1, n - 1);
                    int from = _frame + 1;
                    bool ok = FrameRecorder.TryAdd($"{dir}:{from}-{from + frames - 1}:{every}");
                    Console.WriteLine(ok ? $"[mv] recording {dir} frames {from}-{from + frames - 1} every {every}" : $"[mv] bad record spec");
                    foreach (var (at, cmd) in _cues)
                    {
                        int f = from + at;
                        if (!_schedule.TryGetValue(f, out var list)) _schedule[f] = list = new List<string>();
                        list.Add(cmd);
                    }
                    if (_cues.Count > 0) Console.WriteLine($"[mv] {_cues.Count} cues scheduled from frame {from}");
                    _cues.Clear();
                    return;
                }
                case "orbit":
                    TakeCamera(); _mode = CamMode.Orbit; _subject = w[1]; _dist = F(w[2]); _height = F(w[3]); _degPerSec = F(w[4]);
                    _phase = w.Length > 5 ? F(w[5]) : 0f; _haveSmoothed = false;
                    Console.WriteLine($"[mv] orbit {_subject} dist {_dist} height {_height} {_degPerSec} deg/s");
                    return;
                case "chase":
                    TakeCamera(); _mode = CamMode.Chase; _subject = w[1]; _dist = F(w[2]); _height = F(w[3]);
                    _lead = w.Length > 4 ? F(w[4]) : 30f; _smooth = w.Length > 5 ? F(w[5]) : 6f; _haveSmoothed = false;
                    Console.WriteLine($"[mv] chase {_subject} back {_dist} up {_height} lead {_lead}");
                    return;
                case "dolly":
                    TakeCamera(); _mode = CamMode.Dolly; _p0 = V(w[1]); _p1 = V(w[2]); _moveFrames = int.Parse(w[3], CultureInfo.InvariantCulture);
                    _moveStart = _frame; _lookSubject = null;
                    if (w.Length > 4) { if (w[4].Contains(',')) _lookPoint = V(w[4]); else _lookSubject = w[4]; }
                    else _lookSubject = "local";
                    Console.WriteLine($"[mv] dolly {_p0} -> {_p1} over {_moveFrames} frames");
                    return;
                case "flyby":
                    TakeCamera(); _mode = CamMode.Flyby; _subject = w[1]; _side = F(w[2]); _ahead = F(w[3]); _height = F(w[4]);
                    _moveFrames = int.Parse(w[5], CultureInfo.InvariantCulture); _moveStart = _frame;
                    Console.WriteLine($"[mv] flyby {_subject} side {_side} ahead {_ahead} over {_moveFrames} frames");
                    return;
                case "camera": Release(); _useMain = w[1] == "main"; Console.WriteLine($"[mv] camera {(_useMain ? "main" : "replay")}"); return;
                case "release": Release(); return;
                case "status":
                    Console.WriteLine($"[mv] frame {_frame} mode {_mode} subject {_subject} cam {(_cam != null ? _cam.position.ToString() : "none")} fov {_fov} schedule {_schedule.Count} hidden canvases {_hiddenCanvases.Count}");
                    foreach (var kv in _schedule) foreach (var c in kv.Value) Console.WriteLine($"[mv]   {kv.Key}: {c}");
                    return;
                default: Console.WriteLine($"[mv] unknown command '{w[0]}'"); return;
            }
        }

        /// <summary>Called before every engine tick (PlayerWindow.OnUpdate and the headless loop).</summary>
        public static void Tick(int frame, float step)
        {
            _frame = frame;
            while (_schedule.Count > 0 && _schedule.Keys.First() <= frame)
            {
                int key = _schedule.Keys.First();
                var due = _schedule[key];
                _schedule.Remove(key);
                foreach (var c in due) { Console.WriteLine($"[mv] frame {frame}: {c}"); Command(c); }
            }
            if (_mode == CamMode.None || _cam == null) return;
            Pose(step);
        }

        static void TakeCamera()
        {
            if (_manual && _cam != null) return;
            var cm = CosmicShore.Gameplay.CameraManager.Instance;
            Transform t = !_useMain && cm != null ? cm.BeginManualReplayCamera() : null;
            if (t == null)
            {
                var main = Camera.main;
                if (main == null) { Console.WriteLine("[mv] no camera to take"); return; }
                foreach (var mb in main.GetComponentsInParent<MonoBehaviour>())
                    if (mb.enabled) { mb.enabled = false; _stoodDown.Add(mb); }
                t = main.transform;
                Console.WriteLine($"[mv] took Camera.main ({_stoodDown.Count} rig behaviours stood down)");
            }
            else Console.WriteLine("[mv] took the replay camera");
            _cam = t;
            _manual = true;
            ApplyFov();
        }

        static readonly List<MonoBehaviour> _stoodDown = new();

        static void Release()
        {
            _mode = CamMode.None;
            if (!_manual) return;
            if (_stoodDown.Count > 0) { foreach (var mb in _stoodDown) if (mb != null) mb.enabled = true; _stoodDown.Clear(); }
            else CosmicShore.Gameplay.CameraManager.Instance?.RestoreGameplayCamera();
            _manual = false; _cam = null;
            Console.WriteLine("[mv] camera released");
        }

        static Camera ActiveCamera() => _cam != null ? (_cam.GetComponent<Camera>() ?? _cam.GetComponentInChildren<Camera>() ?? Camera.main) : Camera.main;

        static void ApplyFov()
        {
            var cam = ActiveCamera();
            if (cam == null) return;
            float pulse = _pulseLeft > 0f && _pulseFrames > 0f ? _pulseAmount * (_pulseLeft / _pulseFrames) : 0f;
            cam.fieldOfView = _fov - pulse;
        }

        // ---- subjects ----

        static Transform _held; static int _stillFrames; static string _heldSpec;

        /// <summary>
        /// The subject to shoot this frame. A subject that stops dead for half a second (destroyed, respawning,
        /// parked) hands the shot to the fastest vessel, so a chase never sits on an empty respawn point.
        /// </summary>
        static Transform Subject(string spec)
        {
            if (spec != _heldSpec) { _heldSpec = spec; _held = null; _stillFrames = 0; }
            var want = Resolve(spec);
            if (_held == null || _held.gameObject == null || !_held.gameObject.activeInHierarchy) { _held = want; _stillFrames = 0; }
            if (_held != null)
            {
                var st = _held.GetComponent<CosmicShore.Gameplay.IVesselStatus>();
                bool still = st != null && st.Speed < 1f;
                _stillFrames = still ? _stillFrames + 1 : 0;
                if (_stillFrames > 30)
                {
                    var fast = Resolve("fastest");
                    if (fast != null && fast != _held) { Console.WriteLine($"[mv] subject {spec} stood still; following the fastest vessel"); _held = fast; _stillFrames = 0; }
                }
                else if (!still && want != null && want != _held && _stillFrames == 0 && spec != "fastest" && spec != "nearest") _held = want;
            }
            return _held ?? want;
        }

        static Transform Resolve(string spec)
        {
            if (spec.StartsWith("name:", StringComparison.Ordinal))
            {
                string text = spec[5..];
                foreach (var mb in CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (mb != null && mb.gameObject.activeInHierarchy && mb.name.Contains(text, StringComparison.OrdinalIgnoreCase)) return mb.transform;
                return null;
            }
            var vessels = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.VesselController>(FindObjectsSortMode.None)
                .Where(v => v != null && v.gameObject.activeInHierarchy).ToList();
            Transform local = null; var others = new List<Transform>();
            foreach (var v in vessels)
            {
                var status = v.GetComponent<CosmicShore.Gameplay.IVesselStatus>();
                if (status?.Player != null && status.Player.IsLocalPilot) local = v.transform; else others.Add(v.transform);
            }
            if (spec == "local") return local ?? others.FirstOrDefault();
            if (spec == "fastest")
            {
                Transform best = null; float bestSpeed = -1f;
                foreach (var v in vessels)
                {
                    var st = v.GetComponent<CosmicShore.Gameplay.IVesselStatus>();
                    float sp = st != null ? st.Speed : 0f;
                    if (sp > bestSpeed) { bestSpeed = sp; best = v.transform; }
                }
                return best ?? local;
            }
            if (spec == "nearest")
            {
                if (local == null) return others.FirstOrDefault();
                return others.OrderBy(o => (o.position - local.position).sqrMagnitude).FirstOrDefault();
            }
            if (spec.StartsWith("ai", StringComparison.Ordinal) && int.TryParse(spec[2..], out int i))
                return i < others.Count ? others[i] : others.LastOrDefault();
            return local;
        }

        // ---- camera poses ----

        static void Pose(float step)
        {
            Vector3 pos; Vector3 look; Vector3 up = Vector3.up;
            switch (_mode)
            {
                case CamMode.Orbit:
                {
                    var s = Subject(_subject); if (s == null) return;
                    _phase += _degPerSec * step;
                    float a = _phase * Mathf.Deg2Rad;
                    pos = s.position + new Vector3(Mathf.Cos(a) * _dist, _height, Mathf.Sin(a) * _dist);
                    look = s.position;
                    break;
                }
                case CamMode.Chase:
                {
                    // The offset rides the subject exactly (no positional lag at racing speed); only the
                    // subject's heading is low-passed, so a twitchy hull does not whip the shot.
                    var s = Subject(_subject); if (s == null) return;
                    if (!_haveSmoothed) { _smoothedFwd = s.forward; _smoothedUp = s.up; _haveSmoothed = true; }
                    float k = 1f - Mathf.Exp(-_smooth * step);
                    _smoothedFwd = Vector3.Lerp(_smoothedFwd, s.forward, k).normalized;
                    _smoothedUp = Vector3.Lerp(_smoothedUp, s.up, k).normalized;
                    pos = s.position - _smoothedFwd * _dist + _smoothedUp * _height;
                    look = s.position + _smoothedFwd * _lead;
                    up = Vector3.Lerp(Vector3.up, _smoothedUp, 0.6f).normalized;
                    break;
                }
                case CamMode.Dolly:
                {
                    float t = _moveFrames <= 0 ? 1f : Mathf.Clamp01((_frame - _moveStart) / (float)_moveFrames);
                    t = t * t * (3f - 2f * t);
                    pos = Vector3.Lerp(_p0, _p1, t);
                    if (_lookSubject != null) { var s = Subject(_lookSubject); look = s != null ? s.position : pos + Vector3.forward; }
                    else look = _lookPoint;
                    break;
                }
                case CamMode.Flyby:
                {
                    var s = Subject(_subject); if (s == null) return;
                    float t = _moveFrames <= 0 ? 1f : Mathf.Clamp01((_frame - _moveStart) / (float)_moveFrames);
                    float along = Mathf.Lerp(_ahead, -_ahead, t);
                    pos = s.position + s.forward * along + s.right * _side + s.up * _height;
                    look = s.position;
                    break;
                }
                default: return;
            }
            if (_shakeLeft > 0f && _shakeFrames > 0f)
            {
                float amp = _shakeAmount * (_shakeLeft / _shakeFrames);
                pos += new Vector3((float)(_rng.NextDouble() - 0.5), (float)(_rng.NextDouble() - 0.5), (float)(_rng.NextDouble() - 0.5)) * (2f * amp);
                _shakeLeft -= 1f;
            }
            var dir = look - pos;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            _cam.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.normalized, up));
            if (_pulseLeft > 0f) { ApplyFov(); _pulseLeft -= 1f; if (_pulseLeft <= 0f) ApplyFov(); }
        }

        // ---- HUD and pilots ----

        static void HideHud()
        {
            foreach (var canvas in CosmicShore.Engine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (canvas == null || !canvas.enabled) continue;
                canvas.enabled = false;
                _hiddenCanvases.Add(canvas);
            }
            Console.WriteLine($"[mv] hud off ({_hiddenCanvases.Count} canvases)");
        }

        static void ShowHud()
        {
            foreach (var c in _hiddenCanvases) if (c != null) c.enabled = true;
            Console.WriteLine($"[mv] hud on ({_hiddenCanvases.Count} canvases)");
            _hiddenCanvases.Clear();
        }

        static void Autopilot(bool on)
        {
            int n = 0;
            foreach (var v in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.VesselController>(FindObjectsSortMode.None))
            {
                var status = v.GetComponent<CosmicShore.Gameplay.IVesselStatus>();
                if (status?.Player == null || !status.Player.IsLocalPilot) continue;
                v.ToggleAIPilot(on);
                status.Player.InputController?.SetPause(on);
                n++;
            }
            Console.WriteLine($"[mv] autopilot {(on ? "on" : "off")} for {n} local vessel(s)");
        }
    }
}
