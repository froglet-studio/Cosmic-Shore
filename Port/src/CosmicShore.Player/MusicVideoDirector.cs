using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CosmicShore.Engine;

namespace CosmicShore.Player
{
    /// <summary>
    /// The music-video director: orchestrated gameplay, a steady camera and a frame-accurate schedule
    /// inside a scripted run, driven by the <c>mv</c> verb (control port or <c>--do</c>).
    ///
    /// It does three things. It stands the HUD down and puts the local pilot on autopilot. It poses a
    /// camera AFTER every simulation tick (so the picture is never a frame behind the hull) as a
    /// steady MOUNT on a subject, a slow orbit or a straight dolly - no accents, no pulses: the music is
    /// answered by what the GAME does, not by the camera. And it orchestrates the game: steers AI
    /// pilots at a point or at each other (<c>aim</c>, <c>converge</c>), fires a vessel's own ability
    /// (<c>action</c>), devastates the prisms around a subject (<c>blast</c>, the game's own
    /// <c>Prism.Damage</c>, so the explosion is the real one) and herds every swarm creature toward a
    /// direction (<c>herd</c>) - each at a frame the song chose (<c>cue</c>). Recording is a
    /// <see cref="FrameRecorder"/> range started from the next frame, with sub-frame accumulation for
    /// motion blur when the run steps faster than the output rate (<c>--step-hz 240</c>, EVERY 4, ACCUM 4).
    ///
    /// Commands (every number invariant-culture). SUBJECT is <c>local</c>, <c>ai0</c>…<c>aiN</c> (the
    /// non-local vessels, in spawn order), <c>nearest</c> (the non-local vessel closest to the local one),
    /// <c>fastest</c> (the vessel with the highest speed), or <c>name:TEXT</c> (the first active object
    /// whose name contains TEXT, e.g. a kaiju's head). A camera's subject is resolved ONCE when the shot
    /// starts and held; a held subject that stops dead for half a second (destroyed, respawning) hands
    /// the shot to the fastest vessel.
    ///
    ///   mv hud off|on                              hide / restore every Canvas
    ///   mv autopilot on|off                        the local pilot flies itself (and its stick is paused)
    ///   mv camera main|replay                      which camera to pose (main: the gameplay camera with its rig
    ///                                              stood down, keeps the game's look; replay: the CameraManager's)
    ///   mv mount SUBJECT BACK UP SIDE [LEAD] [SMOOTH]  ride the subject from behind-above-beside, looking LEAD units
    ///                                              ahead; its heading is low-passed at SMOOTH e-folds / s (default 2)
    ///   mv orbit SUBJECT DIST HEIGHT DEGPS [PHASE]  orbit the subject (world up) at DEGPS degrees / s
    ///   mv dolly X,Y,Z X,Y,Z FRAMES [SUBJECT|X,Y,Z] straight move over FRAMES, looking at a subject or point
    ///   mv fov DEGREES                             field of view
    ///   mv action SUBJECT INPUTEVENT [FRAMES]      perform the vessel's bound ability (an InputEvents name, e.g.
    ///                                              FullSpeedStraightAction) and release it FRAMES later (default 30)
    ///   mv aim SUBJECT|all SUBJECT2|X,Y,Z|clear    steer an AI pilot (or every AI pilot) at a subject or a point (SetExternalTargetProvider)
    ///   mv converge A B                            A and B fly at each other (a pass, a joust)
    ///   mv blast SUBJECT N [FORCE]                 devastate the N prisms nearest the subject, flung outward
    ///   mv herd follow SUBJECT DIST [NAME]         every swarm creature is drawn to DIST ahead of a subject, every frame
    ///   mv herd X,Y,Z DIST [NAME]                  every swarm creature (Boid; NAME filters) is drawn toward
    ///                                              flock-centroid + direction * DIST, every frame until `mv herd off`
    ///   mv record DIR FRAMES [EVERY] [ACCUM]       record from the NEXT frame: FRAMES sim frames, one PNG per EVERY,
    ///                                              each the average of ACCUM sub-frames (ACCUM &lt;= EVERY)
    ///   mv cue N COMMAND…                          run COMMAND N frames after the next record's first frame
    ///   mv in N COMMAND…                           run COMMAND N frames from now
    ///   mv cell NAME                               swap the scene's cell to a CellConfigDataSO by name (mv cells lists them)
    ///   mv spawn vessel CLASS|all [N] [DOMAIN] [R]  release N AI-piloted vessels (the roster's 21 classes, cycled) in a ring
    ///                                              of radius R ahead of the local pilot (the lava lamp's pipeline)
    ///   mv spawn fauna|flora NAME [N] [ELEMENT]    release N lifeforms of a Toy Box catalogue species (mv lifeforms lists them)
    ///   mv release                                 give the camera back to the game
    ///   mv status                                  print the camera, subject, herd and schedule
    /// </summary>
    public static class MusicVideoDirector
    {
        enum CamMode { None, Mount, Orbit, Dolly }

        static CamMode _mode = CamMode.None;
        static Transform _cam;
        static bool _manual;
        static bool _useMain = true;
        static readonly List<MonoBehaviour> _stoodDown = new();
        static string _subjectSpec = "local";
        static Transform _subject; static int _stillFrames;
        static float _back = 24f, _up = 8f, _side = 8f, _lead = 40f, _smooth = 2f;
        static float _dist = 40f, _height = 12f, _degPerSec = 8f, _phase;
        static Vector3 _p0, _p1, _lookPoint; static string _lookSubject; static int _moveFrames = 120, _moveStart = -1;
        static Vector3 _smoothedFwd, _smoothedUp; static bool _haveSmoothed;
        static float _fov = 60f;
        static readonly List<Canvas> _hiddenCanvases = new();
        static readonly SortedDictionary<int, List<string>> _schedule = new();
        static readonly List<(int at, string cmd)> _cues = new();
        static readonly List<(int at, Transform vessel, string ev)> _releases = new();
        static Vector3 _herdDir; static float _herdDist; static string _herdName; static bool _herding; static string _herdFollow;
        static int _frame;

        public static void Command(string arg)
        {
            var words = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) { Console.WriteLine("[mv] usage: see MusicVideoDirector"); return; }
            try { Run(words); }
            catch (Exception e) { Console.WriteLine($"[mv] '{arg}' failed: {e.Message}"); }
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);
        static int I(string s) => int.Parse(s, CultureInfo.InvariantCulture);
        static Vector3 V(string s) { var p = s.Split(','); return new Vector3(F(p[0]), F(p[1]), F(p[2])); }

        static void Run(string[] w)
        {
            switch (w[0])
            {
                case "in":
                {
                    Schedule(_frame + I(w[1]), string.Join(' ', w.Skip(2)));
                    return;
                }
                case "cue": _cues.Add((I(w[1]), string.Join(' ', w.Skip(2)))); return;
                case "hud": if (w[1] == "off") HideHud(); else ShowHud(); return;
                case "autopilot": Autopilot(w[1] == "on"); return;
                case "camera": Release(); _useMain = w[1] == "main"; Console.WriteLine($"[mv] camera {(_useMain ? "main" : "replay")}"); return;
                case "fov": _fov = F(w[1]); ApplyFov(); Console.WriteLine($"[mv] fov {_fov}"); return;
                case "record":
                {
                    // DIR may hold spaces: the trailing numeric words are FRAMES [EVERY [ACCUM]], the rest is the path.
                    int n = w.Length - 1; var nums = new List<int>();
                    while (n >= 2 && nums.Count < 3 && int.TryParse(w[n], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) { nums.Insert(0, v); n--; }
                    string dir = string.Join(' ', w, 1, n);
                    int frames = nums[0], every = nums.Count > 1 ? nums[1] : 1, accum = nums.Count > 2 ? nums[2] : 1;
                    int from = _frame + 1;
                    bool ok = FrameRecorder.TryAdd($"{dir}:{from}-{from + frames - 1}:{every}:{accum}");
                    Console.WriteLine(ok ? $"[mv] recording {dir} frames {from}-{from + frames - 1} every {every} accum {accum}" : "[mv] bad record spec");
                    foreach (var (at, cmd) in _cues) Schedule(from + at, cmd, quiet: true);
                    if (_cues.Count > 0) Console.WriteLine($"[mv] {_cues.Count} cues scheduled from frame {from}");
                    _cues.Clear();
                    return;
                }
                case "mount":
                    TakeCamera(); _mode = CamMode.Mount; SetSubject(w[1]); _back = F(w[2]); _up = F(w[3]); _side = F(w[4]);
                    _lead = w.Length > 5 ? F(w[5]) : 40f; _smooth = w.Length > 6 ? F(w[6]) : 2f; _haveSmoothed = false;
                    Console.WriteLine($"[mv] mount {_subjectSpec} back {_back} up {_up} side {_side} lead {_lead} smooth {_smooth}");
                    return;
                case "orbit":
                    TakeCamera(); _mode = CamMode.Orbit; SetSubject(w[1]); _dist = F(w[2]); _height = F(w[3]); _degPerSec = F(w[4]);
                    _phase = w.Length > 5 ? F(w[5]) : 0f;
                    Console.WriteLine($"[mv] orbit {_subjectSpec} dist {_dist} height {_height} {_degPerSec} deg/s");
                    return;
                case "dolly":
                    TakeCamera(); _mode = CamMode.Dolly; _p0 = V(w[1]); _p1 = V(w[2]); _moveFrames = I(w[3]); _moveStart = _frame; _lookSubject = null;
                    if (w.Length > 4) { if (w[4].Contains(',')) _lookPoint = V(w[4]); else _lookSubject = w[4]; } else _lookSubject = "local";
                    Console.WriteLine($"[mv] dolly {_p0} -> {_p1} over {_moveFrames} frames");
                    return;
                case "action": Action(w[1], w[2], w.Length > 3 ? I(w[3]) : 30); return;
                case "aim": Aim(w[1], w[2]); return;
                case "converge": Aim(w[1], w[2]); Aim(w[2], w[1]); return;
                case "blast": Blast(w[1], I(w[2]), w.Length > 3 ? F(w[3]) : 30f); return;
                case "herd":
                    if (w[1] == "off") { _herding = false; _herdFollow = null; Console.WriteLine("[mv] herd off"); return; }
                    if (w[1] == "follow") { _herdFollow = w[2]; _herdDist = w.Length > 3 ? F(w[3]) : 40f; _herdName = w.Length > 4 ? w[4] : null; _herding = true; Console.WriteLine($"[mv] herd follows {_herdFollow} at {_herdDist}"); return; }
                    _herdDir = V(w[1]).normalized; _herdDist = F(w[2]); _herdName = w.Length > 3 ? w[3] : null; _herding = true;
                    Console.WriteLine($"[mv] herd {_herdDir} x {_herdDist}{(_herdName != null ? " " + _herdName : "")}");
                    return;
                case "cell": SwapCell(string.Join(' ', w.Skip(1))); return;
                case "cells": ListCells(); return;
                case "lifeforms": ListLifeforms(w.Length > 1 ? w[1] : null); return;
                case "spawn":
                    if (w[1] == "vessel") SpawnVessels(w[2], w.Length > 3 ? I(w[3]) : 1, w.Length > 4 ? w[4] : null, w.Length > 5 ? F(w[5]) : 60f);
                    else if (w[1] == "fauna" || w[1] == "flora") SpawnLifeforms(w[1] == "fauna", w[2], w.Length > 3 ? I(w[3]) : 1, w.Length > 4 ? w[4] : null);
                    else Console.WriteLine("[mv] spawn vessel CLASS|all [N] [DOMAIN] [RADIUS] | spawn fauna|flora NAME [N] [ELEMENT]");
                    return;
                case "release": Release(); return;
                case "status":
                {
                    var local = Resolve("local");
                    var st = local != null ? local.GetComponent<CosmicShore.Gameplay.IVesselStatus>() : null;
                    Console.WriteLine($"[mv] frame {_frame} mode {_mode} subject {_subjectSpec} ({(_subject != null ? _subject.name : "none")}) cam {(_cam != null ? _cam.position.ToString() : "none")} fov {_fov} herd {_herding} schedule {_schedule.Count} hidden canvases {_hiddenCanvases.Count} local speed {(st != null ? st.Speed : 0f).ToString("F1", CultureInfo.InvariantCulture)} vessels {Vessels().Count} swapping {(HostCell()?.IsSwappingConfig ?? false)}");
                    foreach (var kv in _schedule) foreach (var c in kv.Value) Console.WriteLine($"[mv]   {kv.Key}: {c}");
                    return;
                }
                default: Console.WriteLine($"[mv] unknown command '{w[0]}'"); return;
            }
        }

        static void Schedule(int at, string cmd, bool quiet = false)
        {
            if (!_schedule.TryGetValue(at, out var list)) _schedule[at] = list = new List<string>();
            list.Add(cmd);
            if (!quiet) Console.WriteLine($"[mv] scheduled frame {at}: {cmd}");
        }

        /// <summary>Before the engine tick: the schedule and the herd (so the creatures steer this tick).</summary>
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
            for (int i = _releases.Count - 1; i >= 0; i--)
                if (_releases[i].at <= frame)
                {
                    var (_, vessel, ev) = _releases[i]; _releases.RemoveAt(i);
                    var vc = vessel != null ? vessel.GetComponent<CosmicShore.Gameplay.VesselController>() : null;
                    if (vc != null && Enum.TryParse<CosmicShore.Data.InputEvents>(ev, out var ie)) vc.StopShipControllerActions(ie);
                }
            if (_herding) Herd();
        }

        /// <summary>After the engine tick, before the frame is drawn: the camera on where the subject IS this frame.</summary>
        public static void AfterTick(int frame, float step)
        {
            if (_mode == CamMode.None || _cam == null) return;
            if (_useMain && _stoodDown.Count > 0 && Camera.main != null && Camera.main.transform != _cam)
            {
                // The game switched its active camera (a cell swap, a spawn): take the new one the same way.
                _manual = false; _cam = null;
                TakeCamera();
                if (_cam == null) return;
            }
            Pose(step);
        }

        // ---- camera ----

        static void TakeCamera()
        {
            if (_manual && _cam != null) return;
            var cm = CosmicShore.Gameplay.CameraManager.Instance;
            Transform t = !_useMain && cm != null ? cm.BeginManualReplayCamera() : null;
            if (t == null)
            {
                var main = Camera.main;
                if (main == null) { Console.WriteLine("[mv] no camera to take"); return; }
                // Stand down what MOVES the camera (CameraManager and its controllers), never what renders it
                // (URP's per-camera data is the post-processing switch) or keeps it alive.
                foreach (var mb in main.GetComponentsInParent<MonoBehaviour>())
                {
                    string tn = mb.GetType().Name;
                    if (tn == "UniversalAdditionalCameraData" || tn == "DontDestroyOnLoad" || tn == "AudioListener" || tn == "Volume") continue;
                    if (mb.enabled) { mb.enabled = false; _stoodDown.Add(mb); }
                }
                // Camera controllers that live elsewhere in the scene (the menu's MainMenuCameraController poses the
                // scene camera in LateUpdate, which the engine flushes after this director's post-tick pose).
                foreach (var mb in CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb == null || !mb.enabled || _stoodDown.Contains(mb)) continue;
                    string tn = mb.GetType().Name;
                    if (tn.EndsWith("CameraController", StringComparison.Ordinal) || tn == "CameraManager") { mb.enabled = false; _stoodDown.Add(mb); }
                }
                t = main.transform;
                Console.WriteLine($"[mv] took Camera.main ({_stoodDown.Count} rig behaviours stood down: {string.Join(", ", _stoodDown.Select(m => m.GetType().Name).Distinct())})");
            }
            else Console.WriteLine("[mv] took the replay camera");
            _cam = t;
            _manual = true;
            ApplyFov();
        }

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
            if (cam != null) cam.fieldOfView = _fov;
        }

        static void SetSubject(string spec)
        {
            _subjectSpec = spec; _subject = Resolve(spec); _stillFrames = 0;
            Console.WriteLine($"[mv] subject {spec} -> {(_subject != null ? _subject.name : "none")}");
        }

        /// <summary>The held camera subject, re-resolved only when it is gone or has stood still for half a second.</summary>
        static Transform Subject()
        {
            if (_subject == null || _subject.gameObject == null || !_subject.gameObject.activeInHierarchy) { _subject = Resolve(_subjectSpec); _stillFrames = 0; }
            if (_subject == null) return null;
            var st = _subject.GetComponent<CosmicShore.Gameplay.IVesselStatus>();
            bool still = st != null && st.Speed < 1f;
            _stillFrames = still ? _stillFrames + 1 : 0;
            if (_stillFrames > PlayerWindow.StepHz / 2)   // half a second, whatever the step rate
            {
                var fast = Resolve("fastest");
                if (fast != null && fast != _subject) { Console.WriteLine($"[mv] subject {_subjectSpec} stood still; following the fastest vessel"); _subject = fast; _stillFrames = 0; }
            }
            return _subject;
        }

        static void Pose(float step)
        {
            Vector3 pos, look, up = Vector3.up;
            switch (_mode)
            {
                case CamMode.Mount:
                {
                    var s = Subject(); if (s == null) return;
                    if (!_haveSmoothed) { _smoothedFwd = s.forward; _smoothedUp = s.up; _haveSmoothed = true; }
                    float k = 1f - Mathf.Exp(-_smooth * step);
                    _smoothedFwd = Vector3.Lerp(_smoothedFwd, s.forward, k).normalized;
                    _smoothedUp = Vector3.Lerp(_smoothedUp, s.up, k).normalized;
                    var right = Vector3.Cross(_smoothedUp, _smoothedFwd).normalized;
                    pos = s.position - _smoothedFwd * _back + _smoothedUp * _up + right * _side;
                    look = s.position + _smoothedFwd * _lead;
                    up = Vector3.Lerp(Vector3.up, _smoothedUp, 0.6f).normalized;
                    break;
                }
                case CamMode.Orbit:
                {
                    var s = Subject(); if (s == null) return;
                    _phase += _degPerSec * step;
                    float a = _phase * Mathf.Deg2Rad;
                    pos = s.position + new Vector3(Mathf.Cos(a) * _dist, _height, Mathf.Sin(a) * _dist);
                    look = s.position;
                    break;
                }
                case CamMode.Dolly:
                {
                    float t = _moveFrames <= 0 ? 1f : Mathf.Clamp01((_frame - _moveStart) / (float)_moveFrames);
                    t = t * t * (3f - 2f * t);
                    pos = Vector3.Lerp(_p0, _p1, t);
                    if (_lookSubject != null) { var s = Resolve(_lookSubject); look = s != null ? s.position : pos + Vector3.forward; }
                    else look = _lookPoint;
                    break;
                }
                default: return;
            }
            var dir = look - pos;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            _cam.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.normalized, up));
        }

        // ---- subjects ----

        static List<CosmicShore.Gameplay.VesselController> Vessels() =>
            CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.VesselController>(FindObjectsSortMode.None)
                .Where(v => v != null && v.gameObject.activeInHierarchy).ToList();

        static Transform Resolve(string spec)
        {
            if (spec.Contains(',')) return null;
            if (spec.StartsWith("name:", StringComparison.Ordinal))
            {
                string text = spec[5..];
                foreach (var mb in CosmicShore.Engine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if (mb != null && mb.gameObject.activeInHierarchy && mb.name.Contains(text, StringComparison.OrdinalIgnoreCase)) return mb.transform;
                return null;
            }
            var vessels = Vessels();
            Transform local = null; var others = new List<Transform>();
            foreach (var v in vessels)
            {
                var status = v.GetComponent<CosmicShore.Gameplay.IVesselStatus>();
                if (status?.Player != null && status.Player.IsLocalPilot) local = v.transform; else others.Add(v.transform);
            }
            if (spec == "local") return local ?? others.FirstOrDefault();
            if (spec == "fleet")
            {
                // The centre of the AI fleet, as a transform the camera and the pilots can aim at.
                if (others.Count == 0) return local;
                var c = Vector3.zero; foreach (var o in others) c += o.position; c /= others.Count;
                if (s_fleetAnchor == null) s_fleetAnchor = new GameObject("MvFleetAnchor").transform;
                s_fleetAnchor.position = c;
                s_fleetAnchor.rotation = others[0].rotation;
                return s_fleetAnchor;
            }
            if (spec == "flora" || spec == "fauna")
            {
                // The nearest planted flora / released creature to the local pilot: where the life is.
                var from = local != null ? local.position : Vector3.zero;
                Transform best = null; float bestD = float.MaxValue;
                if (spec == "flora")
                    foreach (var f in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Flora>(FindObjectsSortMode.None))
                    { if (f == null || !f.gameObject.activeInHierarchy) continue; float d = (f.transform.position - from).sqrMagnitude; if (d < bestD) { bestD = d; best = f.transform; } }
                else
                    foreach (var f in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Fauna>(FindObjectsSortMode.None))
                    { if (f == null || !f.gameObject.activeInHierarchy) continue; float d = (f.transform.position - from).sqrMagnitude; if (d < bestD) { bestD = d; best = f.transform; } }
                return best;
            }
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

        static Vector3? Point(string spec)
        {
            if (spec.Contains(',')) return V(spec);
            var t = Resolve(spec);
            return t != null ? t.position : null;
        }

        // ---- orchestration ----

        static void Action(string subject, string ev, int frames)
        {
            var t = Resolve(subject);
            var vc = t != null ? t.GetComponent<CosmicShore.Gameplay.VesselController>() : null;
            if (vc == null) { Console.WriteLine($"[mv] action: no vessel for {subject}"); return; }
            if (!Enum.TryParse<CosmicShore.Data.InputEvents>(ev, true, out var ie)) { Console.WriteLine($"[mv] action: no InputEvents '{ev}'"); return; }
            vc.PerformShipControllerActions(ie);
            _releases.Add((_frame + frames, t, ie.ToString()));
            Console.WriteLine($"[mv] action {t.name} {ie} for {frames} frames");
        }

        static void Aim(string subject, string target)
        {
            if (subject == "all")
            {
                int n = 0;
                foreach (var v in Vessels())
                {
                    var st = v.GetComponent<CosmicShore.Gameplay.IVesselStatus>();
                    if (st?.Player != null && st.Player.IsLocalPilot) continue;
                    n++;
                }
                for (int i = 0; i < n; i++) Aim($"ai{i}", target);
                return;
            }
            var t = Resolve(subject);
            var status = t != null ? t.GetComponent<CosmicShore.Gameplay.IVesselStatus>() : null;
            var pilot = status?.AIPilot;
            if (pilot == null) { Console.WriteLine($"[mv] aim: no AI pilot on {subject}"); return; }
            if (target == "clear") { pilot.ClearExternalTargetProvider(); Console.WriteLine($"[mv] aim {t.name} cleared"); return; }
            if (target.Contains(','))
            {
                var p = V(target);
                pilot.SetExternalTargetProvider(() => p);
                Console.WriteLine($"[mv] aim {t.name} at {p}");
                return;
            }
            var other = Resolve(target);
            if (other == null) { Console.WriteLine($"[mv] aim: no subject '{target}'"); return; }
            pilot.SetExternalTargetProvider(() => other != null ? other.position + other.forward * 10f : t.position + t.forward * 50f);
            Console.WriteLine($"[mv] aim {t.name} at {other.name}");
        }

        static void Blast(string subject, int count, float force)
        {
            var at = Point(subject);
            if (at == null) { Console.WriteLine($"[mv] blast: no subject '{subject}'"); return; }
            var origin = at.Value;
            var prisms = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Prism>(FindObjectsSortMode.None)
                .Where(pr => pr && pr.isActiveAndEnabled && !pr.destroyed)
                .OrderBy(pr => (pr.transform.position - origin).sqrMagnitude)
                .Take(count).ToList();
            foreach (var pr in prisms)
            {
                var dir = pr.transform.position - origin;
                dir = dir.sqrMagnitude < 1e-4f ? Vector3.up : dir.normalized;
                pr.Damage(dir * force, CosmicShore.Data.Domains.Blue, "director", devastate: true);
            }
            Console.WriteLine($"[mv] blast {prisms.Count} prism(s) around {subject}");
        }

        static void Herd()
        {
            var boids = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Boid>(FindObjectsSortMode.None)
                .Where(b => b != null && b.gameObject.activeInHierarchy && (_herdName == null || b.name.Contains(_herdName, StringComparison.OrdinalIgnoreCase))).ToList();
            if (boids.Count == 0) return;
            Vector3 goal;
            if (_herdFollow != null)
            {
                var s = Resolve(_herdFollow);
                if (s == null) return;
                goal = s.position + s.forward * _herdDist;
            }
            else
            {
                var centroid = Vector3.zero;
                foreach (var b in boids) centroid += b.transform.position;
                centroid /= boids.Count;
                goal = centroid + _herdDir * _herdDist;
            }
            foreach (var b in boids) b.Goal = goal;
        }

        // ---- staging: the cell, the fleet, the life ----

        static CosmicShore.Gameplay.Cell HostCell()
        {
            // The scene's cell: the one with a config list (the lava lamp's, a mode's), never a toy miniature.
            CosmicShore.Gameplay.Cell best = null; float bestScale = -1f;
            foreach (var c in CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.Cell>(FindObjectsSortMode.None))
            {
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                float sc = c.transform.lossyScale.x;
                if (sc > bestScale) { bestScale = sc; best = c; }
            }
            return best;
        }

        /// <summary>Every CellConfigDataSO asset in the project (Assets/_SO_Assets/Cell Configs/**), by path.</summary>
        static List<string> CellConfigPaths()
        {
            var root = Environment.GetEnvironmentVariable("COSMIC_SHORE_PROJECT") ?? System.IO.Directory.GetCurrentDirectory();
            var dir = System.IO.Path.Combine(root, "Assets", "_SO_Assets", "Cell Configs");
            if (!System.IO.Directory.Exists(dir)) return new List<string>();
            return System.IO.Directory.GetFiles(dir, "*Cell Config*.asset", System.IO.SearchOption.AllDirectories)
                .Select(f => "Assets/" + System.IO.Path.GetRelativePath(System.IO.Path.Combine(root, "Assets"), f).Replace('\\', '/'))
                .OrderBy(f => f).ToList();
        }

        static void ListCells()
        {
            var paths = CellConfigPaths();
            Console.WriteLine($"[mv] {paths.Count} cell config asset(s): " + string.Join(", ", paths.Select(System.IO.Path.GetFileNameWithoutExtension)));
        }

        static CosmicShore.Utility.CellConfigDataSO FindCellConfig(string query)
        {
            // The scene's own list first (what the cell selector offers there), then any config asset in the project.
            var cell = HostCell();
            var field = cell?.GetType().GetField("CellConfigs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (field?.GetValue(cell) is System.Collections.IEnumerable own)
                foreach (var o in own)
                    if (o is CosmicShore.Utility.CellConfigDataSO c && c.name.Contains(query, StringComparison.OrdinalIgnoreCase)) return c;
            foreach (var path in CellConfigPaths())
            {
                string stem = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!stem.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
                var loaded = Resources.Load<CosmicShore.Utility.CellConfigDataSO>(path);
                if (loaded != null) return loaded;
                Console.WriteLine($"[mv] cell: {path} did not load");
            }
            return null;
        }

        static void SwapCell(string query)
        {
            var cell = HostCell();
            if (cell == null) { Console.WriteLine("[mv] cell: no Cell in the scene"); return; }
            var cfg = FindCellConfig(query);
            if (cfg == null) { Console.WriteLine($"[mv] cell: no config matching '{query}' (mv cells lists them)"); return; }
            if (cell.IsSwappingConfig) { Console.WriteLine("[mv] cell: a swap is already in flight"); return; }
            bool ok = cell.RequestCellSwap(cfg, true);
            Console.WriteLine($"[mv] cell -> {cfg.name} ({(string.IsNullOrEmpty(cfg.CellName) ? "" : cfg.CellName + ", ")}environment {(cfg.EnvironmentPrefab ? cfg.EnvironmentPrefab.name : "none")}): {(ok ? "swapping" : "refused")}");
        }

        static CosmicShore.ScriptableObjects.SpawnMatrixToyDefinitionSO s_catalogue;

        /// <summary>The Toy Box's spawn-matrix definition: every species a player can release (Resources/Toybox).</summary>
        static CosmicShore.ScriptableObjects.SpawnMatrixToyDefinitionSO Catalogue()
        {
            if (s_catalogue != null) return s_catalogue;
            var box = Resources.Load<CosmicShore.ScriptableObjects.ToyboxSO>("Toybox");
            if (box?.Toys != null)
                foreach (var t in box.Toys)
                    if (t is CosmicShore.ScriptableObjects.SpawnMatrixToyDefinitionSO sm) { s_catalogue = sm; break; }
            return s_catalogue;
        }

        static void ListLifeforms(string filter)
        {
            var def = Catalogue();
            if (def == null) { Console.WriteLine("[mv] lifeforms: no SpawnMatrixToyDefinitionSO loaded (the Toy Box's catalogue; Menu_Main loads it)"); return; }
            bool Hit(string n) => filter == null || n.Contains(filter, StringComparison.OrdinalIgnoreCase);
            Console.WriteLine("[mv] fauna: " + string.Join(", ", def.Fauna.Where(f => Hit(f.Name)).Select(f => $"{f.Name}({f.ElementConfigs?.Count(c => c) ?? 0})")));
            Console.WriteLine("[mv] flora: " + string.Join(", ", def.Flora.Where(f => Hit(f.Name)).Select(f => $"{f.Name}({f.ElementConfigs?.Count(c => c) ?? 0})")));
        }

        static int ElementIndex(string element, int count, int i)
        {
            if (count <= 0) return 0;
            if (string.IsNullOrEmpty(element) || element == "cycle") return i % count;
            // Element configs are authored Mass, Charge, Space, Time in the catalogue's order; match by the config's own name later.
            return i % count;
        }

        static void SpawnLifeforms(bool fauna, string name, int count, string element)
        {
            var def = Catalogue(); var cell = HostCell();
            if (def == null || cell == null) { Console.WriteLine("[mv] spawn: needs the Toy Box catalogue and a Cell (Menu_Main's lava lamp has both)"); return; }
            var local = Resolve("local");
            var origin = local != null ? local.position : cell.transform.position;
            int spawned = 0;
            if (fauna)
            {
                var entry = def.Fauna.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                         ?? def.Fauna.FirstOrDefault(f => f.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                var configs = entry?.ElementConfigs?.Where(c => c).ToArray();
                if (configs == null || configs.Length == 0) { Console.WriteLine($"[mv] spawn fauna: no species matching '{name}' (mv lifeforms)"); return; }
                var pick = string.IsNullOrEmpty(element) ? configs : configs.Where(c => c.name.Contains(element, StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty(configs[0]).ToArray();
                for (int i = 0; i < count; i++)
                {
                    var cfg = CosmicShore.Engine.Object.Instantiate(pick[i % pick.Length]);
                    var pos = origin + UnityRandomInsideSphere() * 80f + local.forward * 60f;
                    var f = CosmicShore.Gameplay.CellLifeSpawnerBase.SpawnFaunaBanded(cell, cfg, cell.ControllingDomain, origin, pos);
                    if (f != null) spawned++;
                }
                Console.WriteLine($"[mv] spawned {spawned}/{count} {entry.Name} fauna");
            }
            else
            {
                var entry = def.Flora.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                         ?? def.Flora.FirstOrDefault(f => f.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                var configs = entry?.ElementConfigs?.Where(c => c).ToArray();
                if (configs == null || configs.Length == 0) { Console.WriteLine($"[mv] spawn flora: no species matching '{name}' (mv lifeforms)"); return; }
                var pick = string.IsNullOrEmpty(element) ? configs : configs.Where(c => c.name.Contains(element, StringComparison.OrdinalIgnoreCase)).DefaultIfEmpty(configs[0]).ToArray();
                for (int i = 0; i < count; i++)
                {
                    var cfg = CosmicShore.Engine.Object.Instantiate(pick[i % pick.Length]);
                    var f = CosmicShore.Gameplay.CellLifeSpawnerBase.PlantFlora(cell, cfg, null);
                    if (f != null) spawned++;
                }
                Console.WriteLine($"[mv] planted {spawned}/{count} {entry.Name} flora");
            }
        }

        static Transform s_fleetAnchor;
        static readonly System.Random s_rng = new(11);
        static Vector3 UnityRandomInsideSphere()
        {
            while (true)
            {
                var v = new Vector3((float)(s_rng.NextDouble() * 2 - 1), (float)(s_rng.NextDouble() * 2 - 1), (float)(s_rng.NextDouble() * 2 - 1));
                if (v.sqrMagnitude <= 1f) return v;
            }
        }

        static void SpawnVessels(string cls, int count, string domainName, float radius)
        {
            var init = CosmicShore.Engine.Object.FindObjectsByType<CosmicShore.Gameplay.MenuServerPlayerVesselInitializer>(FindObjectsSortMode.None).FirstOrDefault();
            if (init == null) { Console.WriteLine("[mv] spawn vessel: no MenuServerPlayerVesselInitializer (the lava lamp's pipeline; Menu_Main only)"); return; }
            var local = Resolve("local");
            var origin = local != null ? local.position : Vector3.zero;
            var fwd = local != null ? local.forward : Vector3.forward;
            var classes = new List<CosmicShore.Data.VesselClassType>();
            if (cls == "all") classes.AddRange(CosmicShore.Gameplay.ToyVesselRoster.Default);
            else foreach (var part in cls.Split(','))
                if (Enum.TryParse<CosmicShore.Data.VesselClassType>(part, true, out var vc)) classes.Add(vc);
            if (classes.Count == 0) { Console.WriteLine($"[mv] spawn vessel: no class in '{cls}'"); return; }
            var domains = new[] { CosmicShore.Data.Domains.Jade, CosmicShore.Data.Domains.Ruby, CosmicShore.Data.Domains.Blue, CosmicShore.Data.Domains.Gold };
            for (int i = 0; i < count; i++)
            {
                var vc = classes[i % classes.Count];
                var domain = !string.IsNullOrEmpty(domainName) && Enum.TryParse<CosmicShore.Data.Domains>(domainName, true, out var d) ? d : domains[i % domains.Length];
                float a = i * (2f * Mathf.PI / Math.Max(count, 1));
                var up = Vector3.up; var right = Vector3.Cross(up, fwd).normalized;
                var pos = origin + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius + fwd * (radius * 0.5f);
                init.RequestSpawnAiCompanion(vc, domain, new Pose(pos, Quaternion.LookRotation(fwd, up)));
                Console.WriteLine($"[mv] released {vc} ({domain}) at {pos}");
            }
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
            foreach (var v in Vessels())
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
