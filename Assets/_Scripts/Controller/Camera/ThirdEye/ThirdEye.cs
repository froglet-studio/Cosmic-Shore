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
    /// The Third Eye, in the game: a dev tool that takes over the game's OWN main camera to watch any pilot (Chase,
    /// Follow, Free fly, Free orbit), plus a panel (F3) for who flies each hull (You / AI), the camera settings and the
    /// Game-view debug overlays (each AI's aim, trails, labels, and whatever systems register in
    /// <see cref="ThirdEyeOverlays"/> - Switchback's AI plan / AI sense). It replaces the old editor window, which ran a
    /// second camera into a RenderTexture: everything here draws once, on the camera the game already renders.
    ///
    /// <para><b>Hotkeys</b> (keyboard): <b>F2</b> camera on / off (in the menu's lava lamp F2 stays the god-mode
    /// camera's); <b>F3</b> panel; <b>F4</b> quick overlays (thinking + trails + labels) on / off. While the camera is
    /// on: <b>C</b> cycles Chase / Follow / Free, <b>V</b> Free fly / orbit, <b>Tab</b> or <b>]</b> next pilot,
    /// <b>[</b> previous, <b>F</b> frame the watched pilot (free fly), hold <b>RMB</b> to look (and, in free fly, to
    /// move with WASD, E / Space up, Q / Ctrl down, Shift fast), mouse wheel = speed (fly) or distance.</para>
    ///
    /// <para>Compiled only into the editor and development builds (the whole file is #if-ed); a release build has no code,
    /// so no cost. With the camera off, the panel closed and every overlay off, Update returns after three key reads
    /// and LateUpdate does nothing: no scan, no allocation, no pipeline callback.</para>
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
        Vector3 _chaseOffset, _chaseUp, _followOffset, _followHeading;
        float _yaw, _pitch, _orbitYaw, _orbitPitch, _speedScale = 1f;
        bool _snap;

        bool _cursorCaptured;
        CursorLockMode _savedLock;
        bool _savedVisible;

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
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            StopDriving();
            ThirdEyeLines.Instance.Shutdown();
            Instance = null;
        }

        // ── Input ────────────────────────────────────────────────────────────
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && !TextFieldHasFocus())
            {
                if (kb.f2Key.wasPressedThisFrame && MenuGodModeCamera.Current == null) SetDriving(!_driving);
                if (kb.f3Key.wasPressedThisFrame) SetPanel(!_panel);
                if (kb.f4Key.wasPressedThisFrame) QuickOverlays();

                if (_driving)
                {
                    if (kb.cKey.wasPressedThisFrame) SetMode((ThirdEyeCameraMode)(((int)Settings.Mode + 1) % 3));
                    if (kb.vKey.wasPressedThisFrame)
                        SetFreeMode(Settings.FreeMode == ThirdEyeFreeMode.Fly ? ThirdEyeFreeMode.Orbit : ThirdEyeFreeMode.Fly);
                    if (kb.tabKey.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame) Step(+1);
                    if (kb.leftBracketKey.wasPressedThisFrame) Step(-1);
                    if (kb.fKey.wasPressedThisFrame) Frame();
                }
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
        public void SetPanel(bool open) => _panel = open;

        public void SetDriving(bool on)
        {
            if (on) StartDriving();
            else StopDriving();
        }

        public void SetMode(ThirdEyeCameraMode mode)
        {
            Settings.Mode = mode;
            Settings.Save();
            EnterMode();
        }

        public void SetFreeMode(ThirdEyeFreeMode mode)
        {
            Settings.FreeMode = mode;
            Settings.Save();
            EnterMode();
        }

        public void QuickOverlays()
        {
            var s = Settings;
            bool on = !(s.ShowThinking || s.ShowTrails || s.ShowLabels);
            s.ShowThinking = s.ShowTrails = s.ShowLabels = on;
            s.Save();
        }

        public ThirdEyePilot Watched
        {
            get
            {
                foreach (var p in _pilots) if (p.Id == _watchedId && p.Alive) return p;
                foreach (var p in _pilots) if (p.Alive) { _watchedId = p.Id; _snap = true; return p; }
                return null;
            }
        }

        public void Watch(ThirdEyePilot pilot)
        {
            if (pilot == null) return;
            _watchedId = pilot.Id;
            EnterMode();
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

        /// <summary>Free fly: jump to a Follow-style view of the watched pilot and keep flying from there.</summary>
        public void Frame()
        {
            var w = Watched;
            if (w == null) return;
            Vector3 offset = default, heading = default;
            var s = Settings;
            _pose = ThirdEyeRig.Follow(w.Hull.position, w.Hull.rotation, ref offset, ref heading,
                                       s.FollowDistance, s.FollowHeight, s.FollowSmoothing, 0f, snap: true);
            ThirdEyeRig.YawPitch(_pose.Rotation, out _yaw, out _pitch);
            if (Settings.Mode == ThirdEyeCameraMode.Free && Settings.FreeMode == ThirdEyeFreeMode.Orbit) EnterMode();
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
            var cam = Camera.main;
            if (!cam) { _note = "No main camera to drive."; return; }
            _driving = true;
            Acquire(cam);
            _pose = new ThirdEyeRig.Pose(cam.transform.position, cam.transform.rotation);
            ThirdEyeRig.YawPitch(_pose.Rotation, out _yaw, out _pitch);
            _nextScan = 0f;
            EnterMode();
        }

        void StopDriving()
        {
            if (!_driving) return;
            _driving = false;
            ReleaseCursor();
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

        void EnterMode()
        {
            _snap = true;
            var w = Watched;
            if (Settings.Mode == ThirdEyeCameraMode.Free && Settings.FreeMode == ThirdEyeFreeMode.Orbit && w != null)
            {
                Vector3 toHull = w.Hull.position - _pose.Position;
                if (toHull.sqrMagnitude < 1e-4f) toHull = w.Hull.forward;
                ThirdEyeRig.YawPitch(Quaternion.LookRotation(toHull), out _orbitYaw, out _orbitPitch);
            }
            else if (Settings.Mode == ThirdEyeCameraMode.Free)
                ThirdEyeRig.YawPitch(_pose.Rotation, out _yaw, out _pitch);
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

            float dt = Time.unscaledDeltaTime; // keeps working while the game is paused
            var s = Settings;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            bool look = mouse != null && mouse.rightButton.isPressed && !(_panel && _hud.MouseOverPanel);
            if (look) CaptureCursor(); else ReleaseCursor();
            Vector2 delta = look ? mouse.delta.ReadValue() * s.LookSensitivity : Vector2.zero;
            float wheel = mouse != null && !(_panel && _hud.MouseOverPanel) ? mouse.scroll.ReadValue().y : 0f;
            float wheelStep = wheel > 0f ? 1.1f : wheel < 0f ? 1f / 1.1f : 1f;

            var w = Watched;
            var mode = s.Mode;
            if (mode != ThirdEyeCameraMode.Free && w == null) mode = ThirdEyeCameraMode.Free; // nobody to watch

            switch (mode)
            {
                case ThirdEyeCameraMode.Chase:
                    if (wheelStep != 1f) s.ChaseDistance = Mathf.Clamp(s.ChaseDistance / wheelStep, 2f, 400f);
                    _pose = ThirdEyeRig.Chase(w.Hull.position, w.Hull.rotation, ref _chaseOffset, ref _chaseUp,
                        s.ChaseDistance, s.ChaseHeight, s.ChaseLookAhead, s.ChaseSmoothing, dt, _snap);
                    break;
                case ThirdEyeCameraMode.Follow:
                    if (wheelStep != 1f) s.FollowDistance = Mathf.Clamp(s.FollowDistance / wheelStep, 5f, 1000f);
                    _pose = ThirdEyeRig.Follow(w.Hull.position, w.Hull.rotation, ref _followOffset, ref _followHeading,
                        s.FollowDistance, s.FollowHeight, s.FollowSmoothing, dt, _snap);
                    break;
                default:
                    if (s.FreeMode == ThirdEyeFreeMode.Orbit && w != null)
                    {
                        if (wheelStep != 1f) s.OrbitDistance = Mathf.Clamp(s.OrbitDistance / wheelStep, 2f, 2000f);
                        _orbitYaw += delta.x;
                        _orbitPitch = Mathf.Clamp(_orbitPitch - delta.y, -89f, 89f);
                        _pose = ThirdEyeRig.Orbit(w.Hull.position, _orbitYaw, _orbitPitch, s.OrbitDistance);
                    }
                    else
                    {
                        if (wheelStep != 1f) _speedScale = Mathf.Clamp(_speedScale * wheelStep, 0.05f, 20f);
                        _yaw += delta.x;
                        _pitch = Mathf.Clamp(_pitch - delta.y, -89f, 89f);
                        // Moving needs RMB held: WASD also reaches a vessel you fly (hand it to the AI in the panel).
                        Vector3 move = look && kb != null ? FlyInput(kb) : Vector3.zero;
                        float speed = s.FreeSpeed * _speedScale * (kb != null && kb.shiftKey.isPressed ? s.FastMultiplier : 1f);
                        _pose = ThirdEyeRig.FreeFly(_pose.Position, _yaw, _pitch, move, speed, dt);
                    }
                    break;
            }
            _snap = false;
            _cam.transform.SetPositionAndRotation(_pose.Position, _pose.Rotation);
            _cam.fieldOfView = s.Fov;
        }

        static Vector3 FlyInput(Keyboard kb)
        {
            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            float y = (kb.eKey.isPressed || kb.spaceKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed || kb.ctrlKey.isPressed ? 1f : 0f);
            return new Vector3(x, y, z);
        }

        void CaptureCursor()
        {
            if (_cursorCaptured) return;
            _cursorCaptured = true;
            _savedLock = Cursor.lockState;
            _savedVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void ReleaseCursor()
        {
            if (!_cursorCaptured) return;
            _cursorCaptured = false;
            Cursor.lockState = _savedLock;
            Cursor.visible = _savedVisible;
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
