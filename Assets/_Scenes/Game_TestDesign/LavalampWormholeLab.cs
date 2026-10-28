using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Lavalamp wormhole lab (<c>LavalampWormholeLab.unity</c>): the Meerkat's ORIGINAL tube look (the full-screen
    /// contour-field lens + the additive calm wall) and the NEW cheap mobile water pipe (<see cref="MeerkatWaterTubeView"/>)
    /// side by side, switched live with on-screen buttons, with a frame-time readout, so the frame gain can be judged before
    /// anything is kept or deleted (Yash, 2026-10-28: "make a test scene of lavalamp, with bunch of buttons, to swap between
    /// original wormhole shader and the new version, to judge the frame gain").
    /// Everything is built at runtime from the game's own code: one hull-less <see cref="MeerkatWormholeExecutor"/> (its lab
    /// hooks) forms real tunnels and grows a real omnicrystal network from the shipped <see cref="MeerkatWormholeConfigSO"/>,
    /// and holds them open; the shipped views and lens pass draw them. The switch is <see cref="MeerkatTubeLook.Mode"/>; the
    /// lab puts it back to Original when it closes.
    /// Controls: WASD/arrows move, Q/E down/up, right mouse drag (or one-finger drag) looks, Shift fast, the wheel sets the speed.
    /// Scene-local harness, IMGUI on purpose (no fonts, sprites or TMP), NOT in the build settings.
    /// </summary>
    public sealed class LavalampWormholeLab : MonoBehaviour
    {
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/Meerkat/MeerkatWormholeConfig.asset";

        [Tooltip("The Meerkat's wormhole config (the shipped one). In the editor it is found by path if left empty.")]
        [SerializeField] MeerkatWormholeConfigSO config;
        [Tooltip("The look the lab opens with.")]
        [SerializeField] MeerkatTubeLookMode startMode = MeerkatTubeLookMode.HighQuality;
        [Tooltip("Tunnels formed side by side.")]
        [Range(0, 12)] [SerializeField] int tubeCount = 3;
        [Tooltip("Grow an omnicrystal's wormhole network too.")]
        [SerializeField] bool network = true;
        [Tooltip("The network's reach, a share of the cell (no cell here: the race shell, 1080 u).")]
        [Range(0.05f, 1f)] [SerializeField] float networkShare = 0.25f;
        [SerializeField] uint networkSeed = 12345;
        [SerializeField] Vector3 tunnelOrigin = Vector3.zero;
        [SerializeField] Vector3 networkOrigin = new(0f, 0f, 500f);
        [Tooltip("Lateral spacing between the tunnels (u).")]
        [SerializeField] float tubeSpacing = 90f;
        [Tooltip("Uncap the frame rate (vSync off, target 1000) so the frame time shows the real cost.")]
        [SerializeField] bool uncapFrameRate = true;
        [Tooltip("A cloud of reference cubes round the tubes, so bent space and transparency have something to show.")]
        [SerializeField] bool scenery = true;

        enum Move { Fly, RideTunnel, RideNetwork }

        static readonly MeerkatTubeLookMode[] s_modes =
        {
            MeerkatTubeLookMode.Original, MeerkatTubeLookMode.WaterOnly, MeerkatTubeLookMode.LensOnly,
            MeerkatTubeLookMode.WallOnly, MeerkatTubeLookMode.None, MeerkatTubeLookMode.WaterAndLens,
            MeerkatTubeLookMode.HighQuality,
        };
        static readonly string[] s_modeNames =
            { "Original (lens + wall)", "New water pipe", "Lens only", "Wall only", "None (off)", "Water + lens",
              "PC high quality (lens + layer)" };

        Camera _cam;
        MeerkatWormholeExecutor _exec;
        GameObject _scenery;
        readonly List<MeerkatWormholeExecutor.Tunnel> _tunnels = new();
        MeerkatRootNetwork _net;
        string _error;

        // the camera
        Move _move = Move.Fly;
        float _yaw, _pitch, _speed = 40f, _rideSpeed = 60f, _rideS;
        int _rideChan;
        bool _panelOpen = true;
        Rect _panel;

        // the frame readout: every frame's time over a rolling window
        struct Sample { public float T, Ms, Gpu; }
        const float Window = 2f;
        readonly Sample[] _ring = new Sample[8192];
        int _head, _count;
        float _avg, _min, _max, _gpuAvg, _fps;
        readonly FrameTiming[] _ft = new FrameTiming[1];

        // the bench: every look in turn, the same view, settle then measure
        const float BenchSettle = 1f, BenchMeasure = 3f;
        bool _bench;
        int _benchIdx;
        float _benchT, _benchSum, _benchGpuSum, _benchMax;
        int _benchN, _benchGpuN;
        readonly string[] _benchResult = new string[s_modes.Length];

        // what the lab changed, put back when it closes
        int _oldVSync, _oldTarget;

        // the water's live tuning
        bool _tune;

        void Awake()
        {
#if UNITY_EDITOR
            if (!config) config = AssetDatabase.LoadAssetAtPath<MeerkatWormholeConfigSO>(ConfigPath);
#endif
            MeerkatTubeLook.Mode = startMode;
            _oldVSync = QualitySettings.vSyncCount;
            _oldTarget = Application.targetFrameRate;
            ApplyCap();
        }

        void Start()
        {
            _cam = Camera.main;
            if (!_cam)
            {
                var go = new GameObject("Lab Camera") { tag = "MainCamera" };
                _cam = go.AddComponent<Camera>();
            }
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 6000f;
            _cam.fieldOfView = 70f;
            _cam.transform.SetPositionAndRotation(tunnelOrigin + new Vector3(0f, 25f, -160f), Quaternion.Euler(8f, 0f, 0f));
            _yaw = 0f; _pitch = 8f;

            if (!config)
            {
                _error = $"No MeerkatWormholeConfigSO: set one on {name} (the shipped one is {ConfigPath}).";
                Debug.LogError("[LavalampWormholeLab] " + _error);
                return;
            }
            var maker = new GameObject("Lab Meerkat (no hull)");
            maker.transform.SetParent(transform, false);
            _exec = maker.AddComponent<MeerkatWormholeExecutor>();
            _exec.LabBind(config);
            if (scenery) BuildScenery();
            Rebuild();
        }

        void OnDestroy()
        {
            if (_exec) _exec.LabClear();
            MeerkatTubeLook.Mode = MeerkatTubeLook.Default;
            MeerkatTubeLook.FormedOverride = null;
            MeerkatWaterTubeView.Tuning.Active = false;
            MeerkatWormholeHQ.Tuning.Active = false;
            QualitySettings.vSyncCount = _oldVSync;
            Application.targetFrameRate = _oldTarget;
        }

        void ApplyCap()
        {
            QualitySettings.vSyncCount = uncapFrameRate ? 0 : _oldVSync;
            Application.targetFrameRate = uncapFrameRate ? 1000 : _oldTarget;
        }

        /// <summary>Clears and forms everything again: <see cref="tubeCount"/> tunnels side by side (straight and bent), and the network.</summary>
        void Rebuild()
        {
            if (!_exec) return;
            _exec.LabClear();
            _tunnels.Clear();
            _net = null;
            for (int i = 0; i < tubeCount; i++)
            {
                float x = (i - 0.5f * (tubeCount - 1)) * tubeSpacing;
                float sx = (i % 3) switch { 0 => 0f, 1 => 0.75f, _ => -0.75f };
                float sy = (i % 2) == 0 ? 0f : 0.45f;
                var t = _exec.LabFormTunnel(tunnelOrigin + new Vector3(x, 0f, 0f), Vector3.forward, Vector3.right, 0.5f, sx, sy);
                if (t != null) _tunnels.Add(t);
            }
            if (network)
                _net = _exec.LabGrowNetwork(networkSeed, networkShare, networkOrigin, Vector3.forward, Vector3.right, Vector3.up);
            _rideS = -30f;
            _rideChan = 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_exec) _exec.LabStep(dt);
            RecordFrame();
            if (_bench) StepBench();
            else MoveCamera(dt);
        }

        // ---------------------------------------------------------------- the frame readout

        void RecordFrame()
        {
            float now = Time.realtimeSinceStartup, ms = Time.unscaledDeltaTime * 1000f, gpu = 0f;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _ft) > 0) gpu = (float)_ft[0].gpuFrameTime;
            _ring[_head] = new Sample { T = now, Ms = ms, Gpu = gpu };
            _head = (_head + 1) % _ring.Length;
            if (_count < _ring.Length) _count++;

            float sum = 0f, gsum = 0f, lo = float.MaxValue, hi = 0f;
            int n = 0, gn = 0;
            for (int k = 0; k < _count; k++)
            {
                var s = _ring[(_head - 1 - k + _ring.Length) % _ring.Length];
                if (s.T < now - Window) break;
                sum += s.Ms; n++;
                if (s.Ms < lo) lo = s.Ms;
                if (s.Ms > hi) hi = s.Ms;
                if (s.Gpu > 0f) { gsum += s.Gpu; gn++; }
            }
            _avg = n > 0 ? sum / n : 0f;
            _min = n > 0 ? lo : 0f;
            _max = hi;
            _fps = _avg > 0f ? 1000f / _avg : 0f;
            _gpuAvg = gn > 0 ? gsum / gn : 0f;

            if (_bench && _benchT >= BenchSettle)
            {
                _benchSum += ms; _benchN++;
                if (ms > _benchMax) _benchMax = ms;
                if (gpu > 0f) { _benchGpuSum += gpu; _benchGpuN++; }
            }
        }

        void StartBench()
        {
            _bench = true;
            _benchIdx = 0;
            for (int i = 0; i < _benchResult.Length; i++) _benchResult[i] = null;
            BeginBenchMode();
        }

        void BeginBenchMode()
        {
            MeerkatTubeLook.Mode = s_modes[_benchIdx];
            _benchT = 0f; _benchSum = _benchGpuSum = _benchMax = 0f; _benchN = _benchGpuN = 0;
        }

        void StepBench()
        {
            _benchT += Time.unscaledDeltaTime;
            if (_benchT < BenchSettle + BenchMeasure) return;
            float avg = _benchN > 0 ? _benchSum / _benchN : 0f;
            string gpu = _benchGpuN > 0 ? $"  gpu {_benchGpuSum / _benchGpuN:0.00} ms" : "";
            _benchResult[_benchIdx] = $"{s_modeNames[_benchIdx],-24} {avg:0.00} ms avg  {(avg > 0f ? 1000f / avg : 0f):0} fps  max {_benchMax:0.0}{gpu}";
            Debug.Log("[LavalampWormholeLab] bench " + _benchResult[_benchIdx]);
            if (++_benchIdx >= s_modes.Length)
            {
                _bench = false;
                MeerkatTubeLook.Mode = MeerkatTubeLook.Default;
                return;
            }
            BeginBenchMode();
        }

        // ---------------------------------------------------------------- the camera

        void MoveCamera(float dt)
        {
            switch (_move)
            {
                case Move.RideTunnel: RideTunnel(dt); return;
                case Move.RideNetwork: RideNetwork(dt); return;
            }
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            Vector2 look = Vector2.zero;
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed) look += mouse.delta.ReadValue() * 0.15f;
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel != 0f) _speed = Mathf.Clamp(_speed * (wheel > 0f ? 1.2f : 1f / 1.2f), 2f, 2000f);
            }
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                var p = touch.primaryTouch.position.ReadValue();
                if (!_panel.Contains(GuiPoint(p))) look += touch.primaryTouch.delta.ReadValue() * 0.12f;
            }
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -89f, 89f);
            var rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 dir = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) dir += Vector3.forward;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dir += Vector3.back;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += Vector3.right;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dir += Vector3.left;
                if (kb.eKey.isPressed) dir += Vector3.up;
                if (kb.qKey.isPressed) dir += Vector3.down;
            }
            float v = _speed * (kb != null && kb.leftShiftKey.isPressed ? 4f : 1f);
            var tr = _cam.transform;
            tr.SetPositionAndRotation(tr.position + rot * dir * (v * dt), rot);
        }

        /// <summary>The camera rides tunnel 0's centreline from 30 u before its way in to 30 u past its way out, and round again.</summary>
        void RideTunnel(float dt)
        {
            if (_tunnels.Count == 0) { _move = Move.Fly; return; }
            var t = _tunnels[0];
            _rideS += _rideSpeed * dt;
            if (_rideS > t.L + 30f) _rideS = -30f;
            var pos = TunnelPoint(t, _rideS, out var tan);
            var ahead = TunnelPoint(t, _rideS + 15f, out _);
            var fwd = (ahead - pos).sqrMagnitude > 1e-4f ? (ahead - pos).normalized : tan;
            Place(pos, fwd);
        }

        static Vector3 TunnelPoint(MeerkatWormholeExecutor.Tunnel t, float s, out Vector3 tan)
        {
            float c = Mathf.Clamp(s, 0f, t.L);
            var p = t.At(c, out tan);
            return p + tan * (s - c);   // past an end: on along its heading
        }

        /// <summary>The camera rides the network's channels: along one, then on into the channel that starts nearest its end
        /// (or the next one, a jump, when none does).</summary>
        void RideNetwork(float dt)
        {
            if (_net == null || _net.Channels.Count == 0) { _move = Move.Fly; return; }
            var chans = _net.Channels;
            if (_rideChan >= chans.Count) _rideChan = 0;
            var c = chans[_rideChan];
            _rideS = Mathf.Max(0f, _rideS) + _rideSpeed * dt;
            if (_rideS >= c.L)
            {
                var end = MeerkatRootNetwork.V(MeerkatRootMath.ChanAt(c, c.L, out _));
                int next = (_rideChan + 1) % chans.Count;
                float best = (float)c.M * 2f + 4f;
                for (int i = 0; i < chans.Count; i++)
                {
                    if (i == _rideChan) continue;
                    float d = Vector3.Distance(MeerkatRootNetwork.V(MeerkatRootMath.ChanAt(chans[i], 0, out _)), end);
                    if (d < best) { best = d; next = i; }
                }
                _rideChan = next;
                _rideS = 0f;
                c = chans[_rideChan];
            }
            var pos = MeerkatRootNetwork.V(MeerkatRootMath.ChanAt(c, _rideS, out var tan));
            var ahead = MeerkatRootNetwork.V(MeerkatRootMath.ChanAt(c, Mathf.Min((float)c.L, _rideS + 15f), out _));
            var fwd = (ahead - pos).sqrMagnitude > 1e-4f ? (ahead - pos).normalized : MeerkatRootNetwork.V(tan).normalized;
            Place(pos, fwd);
        }

        void Place(Vector3 pos, Vector3 fwd)
        {
            var tr = _cam.transform;
            var rot = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(fwd, Vector3.up), 0.15f);
            tr.SetPositionAndRotation(pos, rot);
            var e = rot.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        // ---------------------------------------------------------------- scenery

        /// <summary>One mesh (one draw) of reference cubes round the tubes, so the lens has something to bend and the water
        /// something to be seen against. The same in every look, so it never skews the comparison.</summary>
        void BuildScenery()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mat = probe.GetComponent<MeshRenderer>().sharedMaterial;
            var cube = probe.GetComponent<MeshFilter>().sharedMesh;
            var rng = new System.Random(7);
            var combine = new List<CombineInstance>();
            for (int i = 0; i < 700; i++)
            {
                var p = new Vector3((float)(rng.NextDouble() * 2 - 1) * 700f, (float)(rng.NextDouble() * 2 - 1) * 250f,
                                    (float)(rng.NextDouble() * 1400 - 300));
                float s = 2f + (float)rng.NextDouble() * 10f;
                var rot = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, 0f);
                combine.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(p, rot, Vector3.one * s) });
            }
            var mesh = new Mesh { name = "LavalampLabScenery", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(combine.ToArray(), true, true);
            Destroy(probe);
            _scenery = new GameObject("Lab Scenery");
            _scenery.transform.SetParent(transform, false);
            _scenery.AddComponent<MeshFilter>().sharedMesh = mesh;
            _scenery.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ---------------------------------------------------------------- the buttons

        float GuiScale => Mathf.Max(1f, Screen.height / 1080f);
        Vector2 GuiPoint(Vector2 screen) => new(screen.x / GuiScale, (Screen.height - screen.y) / GuiScale);

        void OnGUI()
        {
            float k = GuiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1f));
            float w = 380f;
            _panel = new Rect(10f, 10f, w, _panelOpen ? Screen.height / k - 20f : 120f);
            GUILayout.BeginArea(_panel, GUI.skin.box);

            // always visible: the look and its frame time
            string mode = s_modeNames[System.Array.IndexOf(s_modes, MeerkatTubeLook.Mode)];
            GUILayout.Label($"<b>LOOK: {mode}</b>", Rich(16));
            GUILayout.Label($"{_fps:0} fps   avg {_avg:0.00} ms   min {_min:0.00}   max {_max:0.00}   ({Window:0} s)", Rich(13));
            GUILayout.Label(_gpuAvg > 0f ? $"GPU {_gpuAvg:0.00} ms (FrameTimingManager)" : "GPU time n/a (enable Frame Timing Stats)", Rich(11));
            if (GUILayout.Button(_panelOpen ? "Hide controls" : "Show controls")) _panelOpen = !_panelOpen;
            if (!_panelOpen) { GUILayout.EndArea(); return; }

            if (_error != null) GUILayout.Label($"<color=#ff7070>{_error}</color>", Rich(12));

            GUILayout.Space(6f);
            GUILayout.Label("Tube look", Rich(13));
            GUI.enabled = !_bench;
            for (int i = 0; i < s_modes.Length; i++)
            {
                bool on = MeerkatTubeLook.Mode == s_modes[i];
                if (GUILayout.Toggle(on, s_modeNames[i], GUI.skin.button, GUILayout.Height(30f)) && !on) MeerkatTubeLook.Mode = s_modes[i];
            }
            GUI.enabled = true;
            if (GUILayout.Button(_bench ? $"Benchmarking {s_modeNames[_benchIdx]}..." : "Bench every look (4 s each, camera held)", GUILayout.Height(30f)) && !_bench)
                StartBench();
            foreach (var r in _benchResult) if (r != null) GUILayout.Label(r, Rich(11));

            GUILayout.Space(6f);
            GUILayout.Label("Camera", Rich(13));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_move == Move.Fly, "Fly", GUI.skin.button)) _move = Move.Fly;
            if (GUILayout.Toggle(_move == Move.RideTunnel, "Ride tunnel", GUI.skin.button) && _move != Move.RideTunnel) { _move = Move.RideTunnel; _rideS = -30f; }
            if (GUILayout.Toggle(_move == Move.RideNetwork, "Ride network", GUI.skin.button) && _move != Move.RideNetwork) { _move = Move.RideNetwork; _rideS = 0f; _rideChan = 0; }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Ride speed {_rideSpeed:0} u/s   fly speed {_speed:0} u/s", Rich(11));
            _rideSpeed = GUILayout.HorizontalSlider(_rideSpeed, 5f, 400f);

            GUILayout.Space(6f);
            GUILayout.Label("Scene", Rich(13));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Tunnels {tubeCount}", Rich(12), GUILayout.Width(90f));
            if (GUILayout.Button("-")) { tubeCount = Mathf.Max(0, tubeCount - 1); Rebuild(); }
            if (GUILayout.Button("+")) { tubeCount = Mathf.Min(12, tubeCount + 1); Rebuild(); }
            GUILayout.EndHorizontal();
            bool net = GUILayout.Toggle(network, $" Omnicrystal network ({(_net != null ? _net.Channels.Count : 0)} channels)");
            if (net != network) { network = net; Rebuild(); }
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Network reach {networkShare:0.00}", Rich(12), GUILayout.Width(150f));
            networkShare = GUILayout.HorizontalSlider(networkShare, 0.05f, 1f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild")) Rebuild();
            if (GUILayout.Button("New seed")) { networkSeed = networkSeed * 1664525u + 1013904223u; Rebuild(); }
            GUILayout.EndHorizontal();
            bool sc = GUILayout.Toggle(_scenery && _scenery.activeSelf, " Reference cubes");
            if (_scenery && sc != _scenery.activeSelf) _scenery.SetActive(sc);
            bool cap = GUILayout.Toggle(uncapFrameRate, " Uncapped frame rate (vSync off)");
            if (cap != uncapFrameRate) { uncapFrameRate = cap; ApplyCap(); }

            GUILayout.Space(6f);
            GUILayout.Label("Formed look (not saved to the config)", Rich(13));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(MeerkatTubeLook.FormedOverride == null, "Config's", GUI.skin.button)) MeerkatTubeLook.FormedOverride = null;
            if (GUILayout.Toggle(MeerkatTubeLook.FormedOverride == ScriptableObjects.MeerkatFormedLook.Bridge, "YASH'S", GUI.skin.button))
                MeerkatTubeLook.FormedOverride = ScriptableObjects.MeerkatFormedLook.Bridge;
            if (GUILayout.Toggle(MeerkatTubeLook.FormedOverride == ScriptableObjects.MeerkatFormedLook.ContourField, "GARRETT'S", GUI.skin.button))
                MeerkatTubeLook.FormedOverride = ScriptableObjects.MeerkatFormedLook.ContourField;
            GUILayout.EndHorizontal();

            bool hqTune = GUILayout.Toggle(MeerkatWormholeHQ.Tuning.Active, " Tune the PC look (live)");
            if (hqTune && !MeerkatWormholeHQ.Tuning.Active)   // start from the preset on screen
                MeerkatWormholeHQ.Tuning.Live = MeerkatTubeLook.FormedOverride == ScriptableObjects.MeerkatFormedLook.ContourField
                    ? MeerkatWormholeHQ.GarrettPreset : MeerkatWormholeHQ.YashPreset;
            MeerkatWormholeHQ.Tuning.Active = hqTune;
            if (hqTune)
            {
                ref var L = ref MeerkatWormholeHQ.Tuning.Live;
                Slider("Layer", ref L.Outer, 0f, 0.5f);
                Slider("Rim", ref L.Rim, 0f, 1f);
                Slider("Rim power", ref L.RimPower, 0.5f, 8f);
                Slider("Absorb", ref L.Absorb, 0f, 1f);
                Slider("Glint", ref L.Glint, 0f, 2f);
                Slider("Dispersion", ref L.Dispersion, 0f, 0.2f);
                Slider("Net dispersion", ref L.NetDispersion, 0f, 0.3f);
                Slider("Net iridescence", ref L.NetIridescence, 0f, 1f);
                Slider("Net pulses", ref L.NetPulse, 0f, 1f);
                Slider("Net glow", ref L.NetGlow, 0f, 4f);
            }

            GUILayout.Space(6f);
            _tune = GUILayout.Toggle(_tune, " Tune the water pipe (live)");
            MeerkatWaterTubeView.Tuning.Active = _tune;
            if (_tune)
            {
                Slider("Clarity", ref MeerkatWaterTubeView.Tuning.Clarity, 0f, 1f);
                Slider("Rim", ref MeerkatWaterTubeView.Tuning.Rim, 0f, 1f);
                Slider("Flow", ref MeerkatWaterTubeView.Tuning.Flow, 0f, 1f);
                Slider("Caustic", ref MeerkatWaterTubeView.Tuning.Caustic, 0f, 1f);
                Slider("Twist", ref MeerkatWaterTubeView.Tuning.Twist, -1f, 1f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Cull", Rich(12), GUILayout.Width(70f));
                string[] culls = { "Off", "Front", "Back" };
                for (int i = 0; i < 3; i++)
                    if (GUILayout.Toggle(Mathf.RoundToInt(MeerkatWaterTubeView.Tuning.Cull) == i, culls[i], GUI.skin.button))
                        MeerkatWaterTubeView.Tuning.Cull = i;
                GUILayout.EndHorizontal();
            }
            GUILayout.Label("WASD/QE move, right-drag look, Shift fast, wheel speed", Rich(10));
            GUILayout.EndArea();
        }

        void Slider(string label, ref float v, float lo, float hi)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label} {v:0.00}", Rich(12), GUILayout.Width(110f));
            v = GUILayout.HorizontalSlider(v, lo, hi);
            GUILayout.EndHorizontal();
        }

        readonly Dictionary<int, GUIStyle> _styles = new();
        GUIStyle Rich(int size)
        {
            if (_styles.TryGetValue(size, out var s)) return s;
            s = new GUIStyle(GUI.skin.label) { fontSize = size, richText = true, wordWrap = true };
            _styles[size] = s;
            return s;
        }
    }
}
