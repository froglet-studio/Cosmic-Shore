#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Dev tool: compiled only into the editor and development builds (as PilotControl, which it drives).
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI Diagnose panel (the Third Eye, in the game): a dev tool that chooses who flies each hull (You / AI),
    /// toggles the Game-view debug overlays (each AI's aim, trails, labels, and whatever systems register in
    /// <see cref="ThirdEyeOverlays"/> - Switchback's AI plan / AI sense), runs console commands
    /// (<c>DiagnosticsHUD.RunCommand</c>) and can fly the game's main camera FREE.
    ///
    /// <para><b>One camera rule (Yash, 2026-11-22): a hull is watched through the player's own camera, whoever flies
    /// it.</b> Handing your hull to the AI changes the sticks and nothing else: the game's follow camera stays on it
    /// with the hull's own settings and framing (<see cref="CameraManager.ActiveRigFollowing"/>). The panel has no
    /// camera that follows a hull. Its Chase (30 behind, 8 up), Follow (90 behind, 35 up) and Orbit cameras were
    /// removed: they sat at their own distances, so the AI was watched from nearer than the player flies. The ONE
    /// extra camera is Free fly, which is attached to nothing; Frame pilot jumps it once to a view of the picked
    /// pilot and it flies on from there.</para>
    ///
    /// <para><b>One hotkey</b>: <b>F2</b> opens / closes the panel (in the menu's lava lamp F2 stays the god-mode
    /// camera's). Everything else is a button in the panel (Yash, 2026-11-01: "Dont keep so many hotkeys"). The only
    /// other keys are the lava lamp's own fly controls, while the camera is in Free fly (<see cref="FreeFlyCamera"/>,
    /// the same class and defaults as <see cref="MenuGodModeCamera"/>): WASD, E / Space up, Q / Ctrl down, hold RMB
    /// to look, Shift fast, Alt slow, wheel = cruise speed. While Free fly drives, your vessel's input is paused, as the
    /// lava lamp's is (hand it to the AI in Pilots if it should keep racing).</para>
    ///
    /// <para>Compiled only into the editor and development builds (the whole file is #if-ed). With the panel closed,
    /// the camera off and every overlay off, Update reads one key and returns: no scan, no allocation, no OnGUI, no
    /// pipeline callback.</para>
    /// </summary>
    [DefaultExecutionOrder(31000)] // after every gameplay camera script
    public sealed class ThirdEye : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance) return;
            var go = new GameObject("[ThirdEye]") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<ThirdEye>();
        }

        public static ThirdEye Instance { get; private set; }

        // Nothing survives a play session or a domain reload: no instance, no overlay left on (Yash, 2026-11-01).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            AllOverlaysOff(null);
        }

        static void AllOverlaysOff(ThirdEyeSettings st)
        {
            if (st != null) st.ShowThinking = st.ShowTrails = st.ShowLabels = false;
            var all = ThirdEyeOverlays.All;
            for (int i = 0; i < all.Count; i++) all[i].On = false;
        }

        // The game stopping closes the panel: a scene unloading (match end, back to menu), quitting, editor Play stop.
        void OnSceneUnloaded(UnityEngine.SceneManagement.Scene _) => SetPanel(false);
        void OnApplicationQuit() => SetPanel(false);
#if UNITY_EDITOR
        void OnPlayModeChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode) SetPanel(false);
        }
#endif

        public ThirdEyeSettings Settings { get; private set; }

        // ── State ────────────────────────────────────────────────────────────
        bool _driving, _panel;
        Camera _cam;
        Behaviour _suspended;          // the camera's own driver (CustomCameraController), off while we drive
        float _savedFov;

        readonly List<ThirdEyePilot> _pilots = new();
        int _watchedId;
        float _nextScan;
        const float ScanSeconds = 0.5f;

        readonly Dictionary<int, ThirdEyeTrail> _trails = new();
        const float TrailHz = 15f;
        float _nextTrailSample;

        ThirdEyeRig.Pose _pose = new(Vector3.zero, Quaternion.identity);

        // Where "Frame pilot" puts Free fly: this far behind the picked hull's heading and this far above it.
        const float FrameDistance = 90f, FrameHeight = 35f;

        // The lava lamp's fly model (MenuGodModeCamera's FreeFlyCamera, same defaults).
        readonly FreeFlyCamera _fly = new();
        public FreeFlyCamera Fly => _fly;

        // The panel shows the cursor (gameplay may hide / lock it); restored on close.
        bool _panelCursor;
        CursorLockMode _panelSavedLock;
        bool _panelSavedVisible;

        // Your vessel's input, paused while Free fly drives or the command field has focus (WASD would steer it).
        Player _heldInputOf;

        public const string CommandName = "aidiag";

        ThirdEyeHud _hud;
        string _note;

        public bool Driving => _driving;
        public bool PanelOpen => _panel;
        public IReadOnlyList<ThirdEyePilot> Pilots => _pilots;
        public string Note => _note;
        public Camera DrivenCamera => _cam;

        bool AnyOverlay
        {
            get
            {
                var s = Settings;
                if (s.ShowThinking || s.ShowTrails || s.ShowLabels) return true;
                var all = ThirdEyeOverlays.All;
                for (int i = 0; i < all.Count; i++) if (all[i].On) return true;
                return false;
            }
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Settings = ThirdEyeSettings.Load();
            _hud = gameObject.AddComponent<ThirdEyeHud>();
            _hud.Eye = this;
            _hud.enabled = false;
            Utility.PerformanceBenchmark.DiagnosticsHUD.RegisterCommand(CommandName, HandleCommand);
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
#endif
            StopDriving();
            SetPanel(false);
            HoldInput(false);
            Utility.PerformanceBenchmark.DiagnosticsHUD.UnregisterCommand(CommandName);
            ThirdEyeLines.Instance.Shutdown();
            Instance = null;
        }

        // ── Input ────────────────────────────────────────────────────────────
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f2Key.wasPressedThisFrame && !TextFieldHasFocus() && MenuGodModeCamera.Current == null)
                SetPanel(!_panel);

            bool flying = _driving;   // the one diagnose camera is Free fly
            if (_heldInputOf || flying || _hud.TypingCommand) HoldInput(flying || _hud.TypingCommand);
            if (_panelCursor && !(Mouse.current != null && Mouse.current.rightButton.isPressed))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            bool busy = _driving || _panel || AnyOverlay;
            _hud.enabled = _driving || _panel || Settings.ShowLabels; // OnGUI only while it has something to show
            if (!busy) return;

            float now = Time.unscaledTime;
            if (now >= _nextScan)
            {
                _nextScan = now + ScanSeconds;
                ThirdEyePilots.Scan(_pilots);
                PruneTrails();
            }
            if (Settings.ShowTrails) SampleTrails();
        }

        static bool TextFieldHasFocus()
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null) return false;
            return selected.TryGetComponent<UnityEngine.UI.InputField>(out _) ||
                   selected.TryGetComponent<TMPro.TMP_InputField>(out _);
        }

        // ── Public controls (hotkeys and the panel) ──────────────────────────
        public void SetPanel(bool open)
        {
            if (open == _panel) return;
            _panel = open;
            if (open && !_panelCursor)
            {
                _panelCursor = true;
                _panelSavedLock = Cursor.lockState;
                _panelSavedVisible = Cursor.visible;
            }
            if (!open)
            {
                // Closed = the default view and zero cost (Yash, 2026-11-01): release the camera (the gameplay /
                // menu camera, its FOV and driver come back) and turn every overlay off.
                StopDriving();
                AllOverlaysOff(Settings);
                _trails.Clear();
            }
            if (!open && _panelCursor)
            {
                _panelCursor = false;
                Cursor.lockState = _panelSavedLock;
                Cursor.visible = _panelSavedVisible;
            }
        }

        // Pause / resume the local human's flight input (InputController.SetPause, as PilotControl does). On resume it
        // goes back to paused only if the AI is holding the hull (PilotControl's own pause).
        void HoldInput(bool hold)
        {
            if (hold == (_heldInputOf != null)) return;
            if (hold)
            {
                var you = LocalHuman();
                if (!you || you.InputController == null) return;
                you.InputController.SetPause(true);
                _heldInputOf = you;
            }
            else
            {
                var you = _heldInputOf;
                _heldInputOf = null;
                if (you && you.InputController != null) you.InputController.SetPause(PilotControl.IsHeldByAI(you));
            }
        }

        public void SetDriving(bool on)
        {
            if (on) StartDriving();
            else StopDriving();
        }

        /// <summary>The pilot the panel has picked: the info strip reads it and Frame pilot jumps Free fly to it. Picking
        /// one moves no camera.</summary>
        public ThirdEyePilot Watched
        {
            get
            {
                foreach (var p in _pilots) if (p.Id == _watchedId && p.Alive) return p;
                foreach (var p in _pilots) if (p.Alive) { _watchedId = p.Id; return p; }
                return null;
            }
        }

        public void Watch(ThirdEyePilot pilot)
        {
            if (pilot == null) return;
            _watchedId = pilot.Id;
        }

        public void Step(int dir)
        {
            if (_pilots.Count == 0) return;
            int at = 0;
            for (int i = 0; i < _pilots.Count; i++) if (_pilots[i].Id == _watchedId) at = i;
            for (int n = 1; n <= _pilots.Count; n++)
            {
                var p = _pilots[((at + dir * n) % _pilots.Count + _pilots.Count) % _pilots.Count];
                if (p.Alive) { Watch(p); return; }
            }
        }

        /// <summary>Free fly: jump once to a view of the picked pilot (<see cref="ThirdEyeRig.FramePose"/>) and keep
        /// flying from there. Nothing keeps the camera on the hull afterwards.</summary>
        public void Frame()
        {
            var w = Watched;
            if (w == null) return;
            _pose = ThirdEyeRig.FramePose(w.Hull.position, w.Hull.rotation, FrameDistance, FrameHeight);
            _fly.Begin(_pose.Rotation);
        }

        /// <summary>The You / AI toggle: the host hands your hull to the AI, or takes over an AI's hull.</summary>
        public void SetFlier(ThirdEyePilot pilot, bool youFly)
        {
            _note = null;
            Player you = LocalHuman();
            if (pilot == null || you == null) return;
            string refusal;
            bool ok = pilot.IsLocalHuman
                ? PilotControl.TrySetAIFlies(you, aiFlies: !youFly, LoadGameData(), out refusal)
                : youFly ? PilotControl.TryTakeOver(you, pilot.Player, out refusal)
                : Refuse("that hull is already flown by the AI", out refusal);
            if (!ok) _note = $"Not changed: {refusal}.";
            _nextScan = 0f;
        }

        static bool Refuse(string why, out string refusal)
        {
            refusal = why;
            return false;
        }

        public Player LocalHuman()
        {
            foreach (var p in _pilots)
                if (p.IsLocalHuman && p.Player) return p.Player;
            return null;
        }

        static GameDataSO s_gameData;
        static GameDataSO LoadGameData()
        {
            if (s_gameData) return s_gameData;
            var found = Resources.FindObjectsOfTypeAll<GameDataSO>();
            return s_gameData = found.Length > 0 ? found[0] : null;
        }

        // ── Driving the main camera ──────────────────────────────────────────
        void StartDriving()
        {
            if (_driving) return;
            if (!_panel) SetPanel(true);   // diagnose cameras exist only while the panel is open
            var cam = Camera.main;
            if (!cam) { _note = "No main camera to drive."; return; }
            _driving = true;
            Acquire(cam);
            // Free fly starts from where the game's camera is, so taking the camera moves nothing on screen.
            _pose = new ThirdEyeRig.Pose(cam.transform.position, cam.transform.rotation);
            _nextScan = 0f;
            _fly.Begin(_pose.Rotation);
        }

        void StopDriving()
        {
            if (!_driving) return;
            _driving = false;
            _fly.End();
            HoldInput(false);
            Release();
        }

        // Take the camera from its own driver: disabling CustomCameraController stops its LateUpdate (and undoes its
        // shake), so its smoothing state is never fed our pose. Released, it is re-enabled and snapped to its target.
        void Acquire(Camera cam)
        {
            _cam = cam;
            _savedFov = cam.fieldOfView;
            var driver = cam.GetComponent<CustomCameraController>();
            if (driver && driver.enabled)
            {
                driver.enabled = false;
                _suspended = driver;
            }
        }

        void Release()
        {
            if (_cam) _cam.fieldOfView = _savedFov;
            if (_suspended)
            {
                _suspended.enabled = true;
                if (_suspended is CustomCameraController ccc && ccc.isActiveAndEnabled) ccc.SnapToTarget();
            }
            _suspended = null;
            _cam = null;
        }

        void LateUpdate()
        {
            if (_driving) Drive();

            var lines = ThirdEyeLines.Instance;
            lines.Clear();
            lines.Target = _driving ? _cam : Camera.main;
            if (lines.Target && AnyOverlay) DrawOverlays(lines);
            lines.Sync();
        }

        void Drive()
        {
            // The game swapped cameras (death cam, end cam, a scene load): hand the old one back, take the new one.
            var main = Camera.main;
            if (main != _cam)
            {
                Release();
                if (!main) return;
                Acquire(main);
            }
            if (!_cam) return;

            // The lava lamp's god-mode flight, unchanged (FreeFlyCamera): the one diagnose camera, attached to no hull.
            float dt = Time.unscaledDeltaTime; // keeps working while the game is paused
            bool overPanel = _panel && _hud.MouseOverPanel;
            Vector3 pos = _pose.Position;
            _fly.Tick(dt, keys: !_hud.TypingCommand, look: !overPanel, wheel: !overPanel, ref pos, out Quaternion rot);
            _pose = new ThirdEyeRig.Pose(pos, rot);
            _cam.transform.SetPositionAndRotation(_pose.Position, _pose.Rotation);
            _cam.fieldOfView = Settings.Fov;
        }

        // ── Console command (the panel's command field, or the diagnostics HUD's) ──
        string HandleCommand(string[] a)
        {
            const string Usage = "aidiag panel|cam|fly [on|off] · next|prev|frame · " +
                                 "thinking|trails|labels [on|off] · overlay <name> [on|off] · you|ai (watched pilot) · hits (or any readout)";
            if (a.Length == 0) return Usage;
            string verb = a[0].ToLowerInvariant();
            var readout = ThirdEyeReadouts.Find(verb);
            if (readout != null) return readout.Text?.Invoke() ?? "";
            bool? arg = a.Length > 1 ? a[1].ToLowerInvariant() switch { "on" or "1" or "true" => true, "off" or "0" or "false" => false, _ => null } : null;
            var s = Settings;
            if (verb != "panel" && !_panel) SetPanel(true);   // every diagnose feature lives with the open panel
            switch (verb)
            {
                case "panel": SetPanel(arg ?? !_panel); return $"panel {(_panel ? "open" : "closed")}";
                case "cam": case "camera": case "fly":
                    SetDriving(arg ?? !_driving);
                    return _driving ? "free fly on" : "free fly off (the game's camera)";
                // The follow cameras are gone (Yash, 2026-11-22): say where the view is now, not "unknown command".
                case "chase": case "follow": case "orbit":
                    return $"{verb} was removed: a hull is watched through the game's own camera, whoever flies it. " +
                           "The one extra camera is free fly (aidiag fly).";
                case "next": ThirdEyePilots.Scan(_pilots); Step(+1); return Watched?.Label ?? "no pilot";
                case "prev": ThirdEyePilots.Scan(_pilots); Step(-1); return Watched?.Label ?? "no pilot";
                case "frame": Frame(); return Watched?.Label ?? "no pilot";
                case "thinking": s.ShowThinking = arg ?? !s.ShowThinking; s.Save(); return $"AI thinking {(s.ShowThinking ? "on" : "off")}";
                case "trails": s.ShowTrails = arg ?? !s.ShowTrails; s.Save(); return $"trails {(s.ShowTrails ? "on" : "off")}";
                case "labels": s.ShowLabels = arg ?? !s.ShowLabels; s.Save(); return $"labels {(s.ShowLabels ? "on" : "off")}";
                case "overlay":
                {
                    if (a.Length < 2) return "overlays: " + OverlayNames();
                    bool? on = a.Length > 2 ? a[a.Length - 1].ToLowerInvariant() switch { "on" => true, "off" => false, _ => null } : null;
                    string q = string.Join(" ", a, 1, a.Length - 1 - (on.HasValue ? 1 : 0)).ToLowerInvariant();
                    foreach (var o in ThirdEyeOverlays.All)
                        if (o.Name.ToLowerInvariant().Contains(q)) { o.On = on ?? !o.On; return $"{o.Name} {(o.On ? "on" : "off")}"; }
                    return $"no overlay '{q}' - overlays: {OverlayNames()}";
                }
                case "you": case "ai":
                {
                    ThirdEyePilots.Scan(_pilots);
                    var w = Watched;
                    if (w == null) return "no pilot";
                    SetFlier(w, youFly: verb == "you");
                    return _note ?? $"{w.Label}: {verb}";
                }
                default: return Usage;
            }
        }

        static string OverlayNames()
        {
            var all = ThirdEyeOverlays.All;
            if (all.Count == 0) return "(none in this mode)";
            var names = new string[all.Count];
            for (int i = 0; i < all.Count; i++) names[i] = all[i].Name;
            return string.Join(", ", names);
        }

        // ── Overlays (Game view, one GL pass on the main camera) ─────────────
        void DrawOverlays(ThirdEyeLines lines)
        {
            var s = Settings;
            if (s.ShowTrails) DrawTrails(lines);
            if (s.ShowThinking)
            {
                foreach (var p in _pilots)
                {
                    if (!p.Alive) continue;
                    var t = ThirdEyePilots.Read(p, text: false);
                    if (!t.Has) continue;
                    lines.Line(p.Hull.position, t.Aim, t.Color);
                    lines.WireSphere(t.Aim, 2f, t.Color);
                    if (t.HasTarget)
                    {
                        Vector3 n = lines.Target.transform.position - t.Target;
                        if (n.sqrMagnitude < 1e-4f) n = Vector3.up;
                        lines.Circle(t.Target, n.normalized, 6f, ThirdEyeColors.Cyan);
                        lines.Circle(t.Target, n.normalized, 9f, ThirdEyeColors.Cyan);
                    }
                }
            }

            var all = ThirdEyeOverlays.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].On) all[i].Draw?.Invoke(lines);
        }

        void SampleTrails()
        {
            float t = Time.time;
            if (t < _nextTrailSample && t >= _nextTrailSample - 1f) return;
            _nextTrailSample = t + 1f / TrailHz;
            int capacity = Mathf.CeilToInt(30f * TrailHz) + 2;
            foreach (var p in _pilots)
            {
                if (!p.Alive) continue;
                if (!_trails.TryGetValue(p.Id, out var trail)) _trails[p.Id] = trail = new ThirdEyeTrail(capacity);
                trail.Add(p.Hull.position, t);
            }
        }

        readonly List<int> _gone = new();
        void PruneTrails()
        {
            if (_trails.Count == 0) return;
            _gone.Clear();
            foreach (var id in _trails.Keys)
            {
                bool live = false;
                foreach (var p in _pilots) if (p.Id == id && p.Alive) { live = true; break; }
                if (!live) _gone.Add(id);
            }
            foreach (var id in _gone) _trails.Remove(id);
        }

        void DrawTrails(ThirdEyeLines lines)
        {
            float now = Time.time, keep = Settings.TrailSeconds;
            foreach (var p in _pilots)
            {
                if (!_trails.TryGetValue(p.Id, out var trail) || trail.Count < 2) continue;
                Color domain = ThirdEyePilots.DomainColor(p);
                trail.Get(0, out Vector3 prev, out _);
                for (int i = 1; i < trail.Count; i++)
                {
                    trail.Get(i, out Vector3 pt, out float time);
                    float age = now - time;
                    if (age <= keep)
                    {
                        // Four fade bands, oldest faintest, as the window drew them.
                        int band = Mathf.Min(3, (int)(age / keep * 4f));
                        var c = domain;
                        c.a = 1f - band * 0.22f;
                        lines.Line(prev, pt, c);
                    }
                    prev = pt;
                }
            }
        }
    }
}
#endif
