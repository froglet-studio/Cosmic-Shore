using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Engine;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using EngineInput = CosmicShore.Engine.InputSystem;
using Vector3 = CosmicShore.Engine.Vector3;
using Quaternion = CosmicShore.Engine.Quaternion;
using Vector2 = CosmicShore.Engine.Vector2;
using Random = System.Random; // disambiguate from CosmicShore.Engine.Random (V10 engine addition)

namespace CosmicShore.Client
{
    /// <summary>
    /// Presentation host: drives the engine GameLoop from the window's update callback
    /// and renders the race in the Cosmic Shore vaporwave idiom — additive neon on deep
    /// space, glow trail, wireframe-lit crystals. Headless `--screenshot` mode renders
    /// under Xvfb/Mesa for autonomous visual verification.
    /// </summary>
    public sealed class RaceWindow
    {
        readonly int _seed;
        readonly int _crystalTarget;
        readonly int _rivalCount;
        readonly string _screenshotPath;
        readonly int _screenshotFrame;

        IWindow _window;
        GL _gl;
        IInputContext _inputContext;

        GameLoop _loop;
        SkimRaceDirector _race;          // race rules — flight itself is the real ported rig
        SkimRacePilot _pilot;            // the human's rig (vessel transform, stats, resources)
        IInputStatus _playerStatus;      // the rig's REAL InputStatus (V7) — strategy writes here
        readonly GamepadInputStrategy _gamepadStrategy = new(); // the ported, authentic dual-stick scheme
        EngineInput.Gamepad _shimPad;
        bool _prevA, _prevStart;
        bool _prevKbSpace, _prevKbShift; // keyboard fallback edges → real SOAP button events
        bool _screenshotAutopilot;

        uint _program;
        int _uMvp;

        // post chain: scene FBO → bright → blur ping/pong (half res) → composite
        uint _postProgram, _blurProgram, _compositeProgram;
        uint _sceneFbo, _sceneTex, _sceneDepth;
        uint _pingFbo, _pingTex, _pongFbo, _pongTex;
        uint _fsVao, _fsVbo;
        int _fbWidth, _fbHeight;

        // geometry
        uint _starVao, _starVbo; int _starCount;
        uint _railVao, _railVbo; int _railCount;
        uint _ringVao, _ringVbo; int _ringCount;
        readonly Dictionary<Element, (uint vao, int count)> _crystalMeshes = new();
        readonly Dictionary<Domains, (uint vao, int count)> _vesselMeshes = new();
        uint _hudVao, _hudVbo;

        // per-pilot trail render slots (trails live in the sim — persistent, skimmable);
        // scratch buffers are reused so the per-frame ribbon rebuild doesn't churn GC
        uint[] _pilotTrailVaos, _pilotTrailVbos;
        List<float>[] _pilotTrailVerts;

        Vector3 _camPos, _camLook;
        AudioEngine _audio;
        int _lastCountdownSecond = -1;
        RaceState _lastRaceState = RaceState.Countdown;
        readonly List<(Vector3 pos, float age)> _bursts = new();
        int _frameIndex;

        public RaceWindow(int seed, int crystalTarget, int rivalCount, string screenshotPath, int screenshotFrame)
        {
            _seed = seed;
            _crystalTarget = crystalTarget;
            _rivalCount = rivalCount;
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
        }

        /// <summary>Domain palette — jade player, ruby/gold/blue rivals.</summary>
        static (float r, float g, float b) DomainColor(Domains domain) => domain switch
        {
            Domains.Jade => (0.07f, 1f, 0.62f),
            Domains.Ruby => (1f, 0.17f, 0.32f),
            Domains.Gold => (1f, 0.8f, 0.25f),
            _ => (0.35f, 0.6f, 1f), // Blue — the neutral domain
        };

        /// <summary>Element palette for crystals (the buff each station carries).</summary>
        static (float r, float g, float b) ElementColor(Element element) => element switch
        {
            Element.Charge => (0.5f, 0.95f, 1f),
            Element.Mass => (1f, 0.78f, 0.18f),
            Element.Space => (1f, 0.3f, 0.9f),
            Element.Time => (0.4f, 0.6f, 1f),
            _ => (1f, 1f, 1f), // Omni
        };

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1280, 720),
                Title = "Cosmic Shore — SkimRace (port progress build)",
                VSync = true,
            };
            Console.WriteLine("[1/4] creating window (GLFW)...");
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            Console.WriteLine("[2/4] entering run loop...");
            _window.Run();
            _race?.Shutdown(); // stop the rigs' async spawn/AI loops deterministically
        }

        // ── setup ────────────────────────────────────────────────────

        void OnLoad()
        {
            Console.WriteLine("[3/4] window open — initializing GL/audio/scene...");
            _gl = GL.GetApi(_window);
            _inputContext = _window.CreateInput();
            foreach (var keyboard in _inputContext.Keyboards)
                keyboard.KeyDown += (_, key, _) =>
                {
                    if (key == Key.Escape) _window.Close();
                    if (key == Key.R || (key == Key.Enter && _race.State == RaceState.Finished))
                        _race.RestartRace();
                };
            // replay button: any click on the finish screen starts the rematch
            foreach (var mouse in _inputContext.Mice)
                mouse.MouseDown += (_, _) =>
                {
                    if (_race.State == RaceState.Finished)
                        _race.RestartRace();
                };

            (_loop, _race) = SkimRaceFactory.Create(_seed, _crystalTarget, _rivalCount);
            _pilot = _race.HumanPilot;
            _playerStatus = _pilot.Input; // the rig's real InputStatus — single input sink
            _gamepadStrategy.Initialize(_playerStatus);
            _gamepadStrategy.OnStrategyActivated(); // ActiveInputDevice = Gamepad → pure analog triggers
            _audio = new AudioEngine(disabled: _screenshotPath != null);
            _race.OnCrystalClaimed += (_, pos, byHuman) => { _bursts.Add((pos, 0f)); _audio.CrystalChime(player: byHuman); };

            _program = CompileProgram();
            _uMvp = _gl.GetUniformLocation(_program, "uMvp");

            BuildStars();
            BuildTrack();
            BuildCrystalMeshes();
            BuildVesselMeshes();
            var pilots = _race.Pilots;
            _pilotTrailVaos = new uint[pilots.Count];
            _pilotTrailVbos = new uint[pilots.Count];
            _pilotTrailVerts = new List<float>[pilots.Count];
            for (int i = 0; i < pilots.Count; i++)
            {
                _pilotTrailVaos[i] = _gl.GenVertexArray();
                _pilotTrailVbos[i] = _gl.GenBuffer();
                ConfigureDynamicVao(_pilotTrailVaos[i], _pilotTrailVbos[i]);
                _pilotTrailVerts[i] = new List<float>();
            }
            _hudVao = _gl.GenVertexArray();
            _hudVbo = _gl.GenBuffer();
            ConfigureDynamicVao(_hudVao, _hudVbo);

            _postProgram = CompileProgram(FullscreenVertexSrc, BrightFragmentSrc);
            _blurProgram = CompileProgram(FullscreenVertexSrc, BlurFragmentSrc);
            _compositeProgram = CompileProgram(FullscreenVertexSrc, CompositeFragmentSrc);
            BuildFullscreenTriangle();
            EnsureRenderTargets();

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // additive neon
            _gl.Enable(EnableCap.DepthTest);
            _gl.Enable(EnableCap.ProgramPointSize);

            _camPos = _pilot.Transform.position - new Vector3(0f, -2.5f, 9f);
            Console.WriteLine("[4/4] ready — racing. (If the game window isn't visible now, check the taskbar.)");
        }

        uint CompileProgram() => CompileProgram(MainVertexSrc, MainFragmentSrc);

        const string MainVertexSrc = @"#version 330 core
layout(location=0) in vec3 aPos;
layout(location=1) in vec4 aColor;
uniform mat4 uMvp;
out vec4 vColor;
void main()
{
    gl_Position = uMvp * vec4(aPos, 1.0);
    vColor = aColor;
    gl_PointSize = max(1.0, 7.0 / max(gl_Position.w * 0.06, 1.0));
}";
        const string MainFragmentSrc = @"#version 330 core
in vec4 vColor;
out vec4 frag;
void main() { frag = vColor; }";

        const string FullscreenVertexSrc = @"#version 330 core
layout(location=0) in vec2 aPos;
out vec2 vUv;
void main() { vUv = aPos * 0.5 + 0.5; gl_Position = vec4(aPos, 0.0, 1.0); }";

        const string BrightFragmentSrc = @"#version 330 core
in vec2 vUv;
out vec4 frag;
uniform sampler2D uTex;
void main()
{
    vec3 c = texture(uTex, vUv).rgb;
    float lum = dot(c, vec3(0.30, 0.55, 0.15));
    frag = vec4(c * smoothstep(0.32, 0.75, lum), 1.0);
}";

        const string BlurFragmentSrc = @"#version 330 core
in vec2 vUv;
out vec4 frag;
uniform sampler2D uTex;
uniform vec2 uDir; // (1/w,0) or (0,1/h)
void main()
{
    float weights[5] = float[](0.227027, 0.194594, 0.121622, 0.054054, 0.016216);
    vec3 sum = texture(uTex, vUv).rgb * weights[0];
    for (int i = 1; i < 5; i++)
    {
        sum += texture(uTex, vUv + uDir * float(i) * 1.6).rgb * weights[i];
        sum += texture(uTex, vUv - uDir * float(i) * 1.6).rgb * weights[i];
    }
    frag = vec4(sum, 1.0);
}";

        const string CompositeFragmentSrc = @"#version 330 core
in vec2 vUv;
out vec4 frag;
uniform sampler2D uScene;
uniform sampler2D uBloom;
void main()
{
    vec3 scene = texture(uScene, vUv).rgb;
    vec3 bloom = texture(uBloom, vUv).rgb;
    vec3 c = scene + bloom * 1.35;          // additive glow
    c = c / (c + vec3(0.55));               // soft tonemap keeps neon from clipping
    c = pow(c, vec3(0.85));                 // slight lift
    frag = vec4(c, 1.0);
}";

        uint CompileProgram(string vertexSrc, string fragmentSrc)
        {
            uint vs = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vs, vertexSrc);
            _gl.CompileShader(vs);
            CheckShader(vs, "vertex");
            uint fs = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fs, fragmentSrc);
            _gl.CompileShader(fs);
            CheckShader(fs, "fragment");

            uint program = _gl.CreateProgram();
            _gl.AttachShader(program, vs);
            _gl.AttachShader(program, fs);
            _gl.LinkProgram(program);
            _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
            if (linked == 0) throw new InvalidOperationException($"link: {_gl.GetProgramInfoLog(program)}");
            _gl.DeleteShader(vs);
            _gl.DeleteShader(fs);
            return program;
        }

        void CheckShader(uint shader, string stage)
        {
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0) throw new InvalidOperationException($"{stage}: {_gl.GetShaderInfoLog(shader)}");
        }

        // ── geometry builders (pos.xyz + color.rgba interleaved) ─────

        unsafe (uint vao, uint vbo) UploadStatic(float[] data)
        {
            uint vao = _gl.GenVertexArray();
            uint vbo = _gl.GenBuffer();
            _gl.BindVertexArray(vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            fixed (float* p = data)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            SetVertexLayout();
            return (vao, vbo);
        }

        unsafe void ConfigureDynamicVao(uint vao, uint vbo)
        {
            // layout only — dynamic users re-BufferData each frame (trails grow unbounded)
            _gl.BindVertexArray(vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            SetVertexLayout();
        }

        unsafe void SetVertexLayout()
        {
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 7 * sizeof(float), (void*)(3 * sizeof(float)));
        }

        static void Push(List<float> list, Vector3 p, float r, float g, float b, float a)
        {
            list.Add(p.x); list.Add(p.y); list.Add(p.z);
            list.Add(r); list.Add(g); list.Add(b); list.Add(a);
        }

        void BuildStars()
        {
            var rng = new Random(_seed * 7919 + 17);
            var data = new List<float>();
            for (int i = 0; i < 2600; i++)
            {
                // shell of stars around the circuit (the closed loop is origin-centered)
                var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f).normalized;
                float radius = 460f + (float)rng.NextDouble() * 700f;
                var p = dir * radius;
                float t = (float)rng.NextDouble();
                // white core stars with magenta/cyan dust
                (float r, float g, float b) = t < 0.72f ? (0.85f, 0.88f, 1f) : t < 0.86f ? (0.95f, 0.45f, 0.95f) : (0.4f, 0.95f, 1f);
                float brightness = 0.25f + (float)rng.NextDouble() * 0.75f;
                Push(data, p, r * brightness, g * brightness, b * brightness, 0.9f);
            }
            _starCount = data.Count / 7;
            (_starVao, _starVbo) = UploadStatic(data.ToArray());
        }

        void BuildTrack()
        {
            var track = _race.Track;
            var rails = new List<float>();
            // twin neon guide rails offset left/right of the closed centerline, in the
            // loop's local frame (outward side vector + a slight drop below the line)
            int samples = track.Centerline.Count - 1; // last point repeats the first
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < samples; i++)
                {
                    float t0 = i / (float)samples * MathF.Tau;
                    float t1 = (i + 1) / (float)samples * MathF.Tau;
                    var a = track.Centerline[i] + track.SideAt(t0) * (7.5f * side) - Vector3.up * 2.5f;
                    var b = track.Centerline[i + 1] + track.SideAt(t1) * (7.5f * side) - Vector3.up * 2.5f;
                    float pulse = 0.55f + 0.25f * Mathf.Sin(i * 0.22f);
                    Push(rails, a, 0.05f, 0.85f * pulse, 1f * pulse, 0.85f);
                    Push(rails, b, 0.05f, 0.85f * pulse, 1f * pulse, 0.85f);
                }
            }
            _railCount = rails.Count / 7;
            (_railVao, _railVbo) = UploadStatic(rails.ToArray());

            // magenta gate rings around the loop, facing along the direction of travel
            var rings = new List<float>();
            const int gateCount = 16;
            for (int gate = 0; gate < gateCount; gate++)
            {
                float t = gate / (float)gateCount * MathF.Tau;
                var center = track.PointAt(t);
                var side = track.SideAt(t);
                var lift = Vector3.Cross(track.TangentAt(t), side).normalized;
                const int segments = 26;
                for (int i = 0; i < segments; i++)
                {
                    float t0 = i / (float)segments * MathF.Tau;
                    float t1 = (i + 1) / (float)segments * MathF.Tau;
                    var a = center + side * (MathF.Cos(t0) * 11f) + lift * (MathF.Sin(t0) * 11f);
                    var b = center + side * (MathF.Cos(t1) * 11f) + lift * (MathF.Sin(t1) * 11f);
                    Push(rings, a, 1f, 0.25f, 0.85f, 0.5f);
                    Push(rings, b, 1f, 0.25f, 0.85f, 0.5f);
                }
            }
            _ringCount = rings.Count / 7;
            (_ringVao, _ringVbo) = UploadStatic(rings.ToArray());
        }

        void BuildCrystalMeshes()
        {
            // one octahedron per element — stations broadcast their buff by color
            foreach (var element in new[] { Element.Charge, Element.Mass, Element.Space, Element.Time, Element.Omni })
                _crystalMeshes[element] = BuildCrystalMesh(ElementColor(element));
        }

        (uint vao, int count) BuildCrystalMesh((float r, float g, float b) tint)
        {
            // unit octahedron in the element tint, white-lifted equator for the hot core
            var top = new Vector3(0f, 1.2f, 0f);
            var bottom = new Vector3(0f, -1.2f, 0f);
            var equator = new[]
            {
                new Vector3(0.9f, 0f, 0f), new Vector3(0f, 0f, 0.9f),
                new Vector3(-0.9f, 0f, 0f), new Vector3(0f, 0f, -0.9f),
            };
            float Lift(float c) => c + (1f - c) * 0.45f; // toward white
            var data = new List<float>();
            for (int i = 0; i < 4; i++)
            {
                var a = equator[i];
                var b = equator[(i + 1) % 4];
                float warm = i % 2 == 0 ? 1f : 0.86f;
                Push(data, top, tint.r * warm, tint.g * warm, tint.b * warm, 0.95f);
                Push(data, a, Lift(tint.r), Lift(tint.g), Lift(tint.b), 0.8f);
                Push(data, b, tint.r * 0.85f, tint.g * 0.85f, tint.b * 0.85f, 0.8f);
                Push(data, bottom, tint.r * 0.8f * warm, tint.g * 0.8f * warm, tint.b * 0.8f * warm, 0.95f);
                Push(data, b, tint.r * 0.85f, tint.g * 0.85f, tint.b * 0.85f, 0.8f);
                Push(data, a, Lift(tint.r), Lift(tint.g), Lift(tint.b), 0.8f);
            }
            var (vao, _) = UploadStatic(data.ToArray());
            return (vao, data.Count / 7);
        }

        void BuildVesselMeshes()
        {
            // The Squirrel's real hull, extracted from the game's FBX and baked with
            // flat per-face lighting in each pilot's domain palette. Dart fallback.
            foreach (var pilot in _race.Pilots)
            {
                var domain = pilot.Domain;
                if (_vesselMeshes.ContainsKey(domain)) continue;
                try
                {
                    _vesselMeshes[domain] = BuildSquirrel(DomainColor(domain));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"squirrel mesh unavailable ({e.Message}) — dart fallback");
                    _vesselMeshes[domain] = BuildDart(DomainColor(domain));
                }
            }
        }

        (uint vao, int count) BuildSquirrel((float r, float g, float b) baseColor)
        {
            using var stream = typeof(RaceWindow).Assembly
                .GetManifestResourceStream("CosmicShore.Client.Assets.squirrel.mesh")
                ?? throw new InvalidOperationException("embedded squirrel.mesh missing");
            using var reader = new System.IO.BinaryReader(stream);
            int triCount = reader.ReadInt32();

            // two-light flat shade in the pilot palette, emissive floor for the neon look
            var keyLight = new Vector3(0.42f, 0.78f, -0.46f).normalized;
            var rimLight = new Vector3(-0.3f, -0.2f, 0.93f).normalized;

            var dataList = new List<float>(triCount * 3 * 7);
            for (int t = 0; t < triCount; t++)
            {
                for (int v = 0; v < 3; v++)
                {
                    float px = reader.ReadSingle(), py = reader.ReadSingle(), pz = reader.ReadSingle();
                    float nx = reader.ReadSingle(), ny = reader.ReadSingle(), nz = reader.ReadSingle();
                    // Prompter feedback (2026-06-12): hull faced backwards — rotate the
                    // baked mesh 180° about up (proper rotation: winding/shading intact)
                    px = -px; pz = -pz; nx = -nx; nz = -nz;
                    var normal = new Vector3(nx, ny, nz);
                    float key = MathF.Max(0f, Vector3.Dot(normal, keyLight));
                    float rim = MathF.Max(0f, Vector3.Dot(normal, rimLight));
                    float shade = 0.22f + 0.62f * key + 0.3f * rim;
                    Push(dataList, new Vector3(px, py, pz),
                        baseColor.r * shade, baseColor.g * shade, baseColor.b * shade, 0.95f);
                }
            }
            var (vao, _) = UploadStatic(dataList.ToArray());
            return (vao, triCount * 3);
        }

        (uint vao, int count) BuildDart((float r, float g, float b) baseColor)
        {
            // dart fallback: nose, twin swept wings, tail fin — flat-shaded neon
            var nose = new Vector3(0f, 0f, 2.6f);
            var tail = new Vector3(0f, 0.25f, -1.4f);
            var left = new Vector3(-1.7f, -0.15f, -1.2f);
            var right = new Vector3(1.7f, -0.15f, -1.2f);
            var belly = new Vector3(0f, -0.35f, -0.9f);
            var fin = new Vector3(0f, 1.05f, -1.5f);

            var data = new List<float>();
            void Tri(Vector3 a, Vector3 b, Vector3 c, float r, float g, float bl, float al)
            {
                Push(data, a, r, g, bl, al);
                Push(data, b, r, g, bl, al);
                Push(data, c, r, g, bl, al);
            }
            void Hull(Vector3 a, Vector3 b, Vector3 c, float bright, float alpha)
                => Tri(a, b, c, baseColor.r * bright, baseColor.g * bright, baseColor.b * bright, alpha);
            Hull(nose, left, tail, 1f, 0.95f);
            Hull(nose, tail, right, 0.88f, 0.95f);
            Hull(nose, belly, left, 0.55f, 0.9f);
            Hull(nose, right, belly, 0.5f, 0.9f);
            Hull(tail, left, belly, 0.4f, 0.9f);
            Hull(tail, belly, right, 0.38f, 0.9f);
            Tri(tail, fin, nose, baseColor.r * 0.8f + 0.2f, baseColor.g * 0.8f + 0.2f, baseColor.b * 0.8f + 0.2f, 0.75f);
            var (vao, _) = UploadStatic(data.ToArray());
            return (vao, data.Count / 7);
        }

        // ── per-frame ────────────────────────────────────────────────

        void OnUpdate(double dt)
        {
            if (_screenshotPath != null) return; // sim + autopilot tick in OnRender

            ApplyHumanInput();

            if (_screenshotPath == null)
                _loop.Tick((float)dt); // the ported engine drives the rigs (real time)

            UpdateFxCameraAudio((float)dt);

        }

        /// <summary>
        /// Feed Silk.NET devices into the engine input shim, then run the PORTED
        /// GamepadInputStrategy against the rig's REAL InputStatus — the authentic
        /// Cosmic Shore dual-stick scheme: XSum=yaw, YSum=pitch, YDiff=roll,
        /// XDiff=stick-spread throttle, analog triggers=two-tier drift, A=boost.
        /// The real VesselTransformer consumes these in the engine tick. Keyboard
        /// fallback writes the same fields (WASD=left stick, arrows=right stick)
        /// and raises the same SOAP button events on Space/Shift edges.
        /// </summary>
        void ApplyHumanInput()
        {
            bool finished = _race.State == RaceState.Finished;
            var silkPad = _inputContext.Gamepads.Count > 0 ? _inputContext.Gamepads[0] : null;
            if (silkPad != null && silkPad.Thumbsticks.Count >= 2)
            {
                _shimPad ??= EngineInput.Gamepad.current = new EngineInput.Gamepad();

                // Silk thumbstick Y is +down; the original scheme expects +up.
                _shimPad.leftStick.value = new Vector2(silkPad.Thumbsticks[0].X, -silkPad.Thumbsticks[0].Y);
                _shimPad.rightStick.value = new Vector2(silkPad.Thumbsticks[1].X, -silkPad.Thumbsticks[1].Y);
                _shimPad.leftTrigger.value = silkPad.Triggers.Count > 0 ? silkPad.Triggers[0].Position : 0f;
                _shimPad.rightTrigger.value = silkPad.Triggers.Count > 1 ? silkPad.Triggers[1].Position : 0f;

                bool a = false, start = false;
                foreach (var button in silkPad.Buttons)
                {
                    if (button.Name == ButtonName.A) a = button.Pressed;
                    if (button.Name == ButtonName.Start) start = button.Pressed;
                }
                _shimPad.buttonSouth.isPressed = a;
                _shimPad.buttonSouth.wasPressedThisFrame = a && !_prevA;
                _shimPad.buttonSouth.wasReleasedThisFrame = !a && _prevA;
                bool aPressed = a && !_prevA;
                _prevA = a;
                if (start && !_prevStart)
                    _race.RestartRace();
                _prevStart = start;
                // replay button: A on the finish screen starts the rematch
                if (aPressed && finished)
                    _race.RestartRace();

                // victory lap: the real AIPilot owns the rig — don't fight it for the sticks
                if (finished) return;

                _gamepadStrategy.ProcessInput(); // the real scheme computes sums/diffs + button events

                // Prompter preference (2026-06-11): inverted yaw on gamepad. Applied
                // after the ported strategy so XDiff throttle and YDiff roll semantics
                // stay authentic; only the steering sense flips.
                _playerStatus.XSum = -_playerStatus.XSum;
                // Prompter preference (2026-06-11, second feedback round): roll sense
                // flips with it — yaw-inverted steering banks opposite to the turn
                // unless roll inverts too. Same post-strategy treatment; the ported
                // strategy's YDiff wire semantic stays authentic, AI input untouched.
                _playerStatus.YDiff = -_playerStatus.YDiff;
                return;
            }

            if (finished) return; // keyboard restart handled by the KeyDown hook

            // Keyboard fallback in the same idiom: WASD = left stick, arrows = right.
            float lx = 0f, ly = 0f, rx = 0f, ry = 0f;
            bool space = false, shift = false;
            foreach (var keyboard in _inputContext.Keyboards)
            {
                if (keyboard.IsKeyPressed(Key.W)) ly += 1f;
                if (keyboard.IsKeyPressed(Key.S)) ly -= 1f;
                if (keyboard.IsKeyPressed(Key.A)) lx -= 1f;
                if (keyboard.IsKeyPressed(Key.D)) lx += 1f;
                if (keyboard.IsKeyPressed(Key.Up)) ry += 1f;
                if (keyboard.IsKeyPressed(Key.Down)) ry -= 1f;
                if (keyboard.IsKeyPressed(Key.Left)) rx -= 1f;
                if (keyboard.IsKeyPressed(Key.Right)) rx += 1f;
                if (keyboard.IsKeyPressed(Key.Space)) space = true;
                if (keyboard.IsKeyPressed(Key.ShiftLeft) || keyboard.IsKeyPressed(Key.ShiftRight)) shift = true;
            }
            // No second stick touched → mirror the left so single-hand play still flies.
            if (rx == 0f && ry == 0f && (lx != 0f || ly != 0f)) { rx = lx; ry = ly; }
            _playerStatus.XSum = Mathf.Clamp(rx + lx, -1f, 1f);
            _playerStatus.YSum = Mathf.Clamp(-(ry + ly), -1f, 1f);
            _playerStatus.YDiff = Mathf.Clamp(ry - ly, -1f, 1f);
            _playerStatus.XDiff = (Mathf.Clamp(rx - lx, -2f, 2f) + 2f) / 4f + (space ? 0.5f : 0.12f);
            _playerStatus.XDiff = Mathf.Clamp01(_playerStatus.XDiff);
            // keyboard has no analog triggers — Shift is full single-tier drift, fed
            // through the same analog field + SOAP button event the gamepad path uses
            _playerStatus.LeftTriggerAnalog = shift ? 1f : 0f;
            _playerStatus.RightTriggerAnalog = 0f;
            if (shift != _prevKbShift)
            {
                if (shift) _playerStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);
                else _playerStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);
                _prevKbShift = shift;
            }
            // Space = boost through the authentic event channel (Button1Action)
            if (space != _prevKbSpace)
            {
                if (space) _playerStatus.OnButtonPressed.Raise(InputEvents.Button1Action);
                else _playerStatus.OnButtonReleased.Raise(InputEvents.Button1Action);
                _prevKbSpace = space;
            }
        }

        void UpdateFxCameraAudio(float dt)
        {
            var t3 = _pilot.Transform;
            // trail emission lives in the director (persistent, skimmable race state)

            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var burst = _bursts[i];
                burst.age += dt;
                if (burst.age > 0.6f) _bursts.RemoveAt(i);
                else _bursts[i] = burst;
            }

            // chase camera
            var desired = t3.position - t3.forward * 9f + t3.up * 2.6f;
            float lag = _screenshotPath != null ? 1f : Mathf.Clamp01(dt * 5f);
            _camPos = Vector3.Lerp(_camPos, desired, lag);
            _camLook = t3.position + t3.forward * 12f;

            // audio: engine bed + skim shimmer + drift rush + countdown/finish stingers
            _audio.SetEngineState(Mathf.Clamp01(_pilot.Status.Speed / 115f),
                _pilot.Status.IsBoosting && _race.State == RaceState.Racing);
            _audio.SetSkimDriftState(
                _race.State == RaceState.Racing ? _pilot.SkimStrength : 0f,
                _race.State == RaceState.Racing ? _pilot.DriftAmount : 0f);
            if (_race.State == RaceState.Countdown)
            {
                int second = (int)MathF.Ceiling(_race.Countdown);
                if (second != _lastCountdownSecond) { _audio.CountdownBeep(); _lastCountdownSecond = second; }
            }
            if (_race.State != _lastRaceState)
            {
                if (_race.State == RaceState.Racing) _audio.GoBeep();
                else if (_race.State == RaceState.Finished)
                    _audio.FinishJingle(won: _race.WinnerPilot == 0);
                _lastRaceState = _race.State;
            }
        }

        unsafe void OnRender(double dt)
        {
            _frameIndex++;
            if (_screenshotPath != null)
            {
                ApplyScreenshotAutopilot();
                _loop.Tick(1f / 60f); // deterministic sim frames
                UpdateFxCameraAudio(1f / 60f);
            }
            EnsureRenderTargets();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
            _gl.ClearColor(0.012f, 0.0f, 0.045f, 1f); // deep space indigo
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            _gl.UseProgram(_program);

            float aspect = _window.FramebufferSize.X / (float)Math.Max(1, _window.FramebufferSize.Y);
            var view = Matrix4x4.CreateLookAt(_camPos, _camLook, _pilot.Transform.up);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(70f * MathF.PI / 180f, aspect, 0.1f, 2200f);
            var viewProjection = view * projection;

            SetMvp(viewProjection);
            _gl.BindVertexArray(_starVao);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_starCount);
            _gl.BindVertexArray(_railVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_railCount);
            _gl.BindVertexArray(_ringVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_ringCount);

            // crystals: spin/pulse via model matrix, tinted by their element; drawn at
            // the live Crystal transform so claimed elemental ones visibly fly to their
            // claimer before vanishing; dark (claimed) stations hide until they relight
            var track = _race.Track;
            for (int i = 0; i < track.Crystals.Count; i++)
            {
                if (!_race.TryGetDrawableCrystal(i, out var crystalPos)) continue;
                float spin = Time.time * 1.6f + i * 0.7f;
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 3f + i);
                var model = Matrix4x4.CreateScale(pulse) * Matrix4x4.CreateRotationY(spin) *
                            Matrix4x4.CreateTranslation(crystalPos);
                SetMvp(model * viewProjection);
                var mesh = _crystalMeshes[track.StationElements[i]];
                _gl.BindVertexArray(mesh.vao);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)mesh.count);
            }

            // collection bursts: expanding fading octahedra (white-hot omni flash)
            var burstMesh = _crystalMeshes[Element.Omni];
            foreach (var (pos, age) in _bursts)
            {
                // bursts spawn on the camera's own path — cull when the expanding shell
                // gets near the camera or it blooms into a screen-filling white wedge
                // (the cull radius grows with the shell so close claims flash briefly
                // instead of whiting out the frame)
                float scale = 1f + age * 7f;
                if ((pos - _camPos).magnitude < 7f + scale * 2.2f) continue;
                var model = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(pos);
                SetMvp(model * viewProjection);
                _gl.BindVertexArray(burstMesh.vao);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)burstMesh.count);
            }

            // the field: every vessel oriented by its real VesselTransformer rotation
            // (the real rig rolls the hull itself — no synthetic bank), domain palette
            foreach (var pilot in _race.Pilots)
            {
                var t3 = pilot.Transform;
                var rotation = Matrix4x4.CreateFromQuaternion(t3.rotation);
                var model = rotation * Matrix4x4.CreateTranslation(t3.position);
                SetMvp(model * viewProjection);
                var mesh = _vesselMeshes[pilot.Domain];
                _gl.BindVertexArray(mesh.vao);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)mesh.count);
            }

            DrawTrails(viewProjection);
            DrawHud();

            // post: bright extract → 2× separable blur (half res) → tonemapped composite
            _gl.Disable(EnableCap.Blend);
            _gl.Disable(EnableCap.DepthTest);
            int hw = _fbWidth / 2, hh = _fbHeight / 2;
            BlitPass(_postProgram, _sceneTex, _pingFbo, hw, hh, 0f, 0f);
            for (int i = 0; i < 2; i++)
            {
                BlitPass(_blurProgram, _pingTex, _pongFbo, hw, hh, 1f / hw, 0f);
                BlitPass(_blurProgram, _pongTex, _pingFbo, hw, hh, 0f, 1f / hh);
            }
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            _gl.Viewport(0, 0, (uint)_fbWidth, (uint)_fbHeight);
            _gl.UseProgram(_compositeProgram);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, _sceneTex);
            _gl.Uniform1(_gl.GetUniformLocation(_compositeProgram, "uScene"), 0);
            _gl.ActiveTexture(TextureUnit.Texture1);
            _gl.BindTexture(TextureTarget.Texture2D, _pingTex);
            _gl.Uniform1(_gl.GetUniformLocation(_compositeProgram, "uBloom"), 1);
            _gl.BindVertexArray(_fsVao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
            _gl.Enable(EnableCap.Blend);
            _gl.Enable(EnableCap.DepthTest);

            if (_screenshotPath != null && _frameIndex >= _screenshotFrame)
            {
                CaptureScreenshot();
                _window.Close();
            }
        }

        unsafe void BuildFullscreenTriangle()
        {
            _fsVao = _gl.GenVertexArray();
            _fsVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_fsVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _fsVbo);
            float[] verts = { -1f, -1f, 3f, -1f, -1f, 3f };
            fixed (float* p = verts)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(verts.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), (void*)0);
        }

        unsafe (uint fbo, uint tex) CreateColorTarget(int width, int height)
        {
            uint fbo = _gl.GenFramebuffer();
            uint tex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D, tex, 0);
            return (fbo, tex);
        }

        unsafe void EnsureRenderTargets()
        {
            int w = Math.Max(8, _window.FramebufferSize.X), h = Math.Max(8, _window.FramebufferSize.Y);
            if (w == _fbWidth && h == _fbHeight) return;
            _fbWidth = w; _fbHeight = h;

            if (_sceneFbo != 0)
            {
                _gl.DeleteFramebuffer(_sceneFbo); _gl.DeleteTexture(_sceneTex); _gl.DeleteRenderbuffer(_sceneDepth);
                _gl.DeleteFramebuffer(_pingFbo); _gl.DeleteTexture(_pingTex);
                _gl.DeleteFramebuffer(_pongFbo); _gl.DeleteTexture(_pongTex);
            }

            (_sceneFbo, _sceneTex) = CreateColorTarget(w, h);
            _sceneDepth = _gl.GenRenderbuffer();
            _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _sceneDepth);
            _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)w, (uint)h);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
            _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                RenderbufferTarget.Renderbuffer, _sceneDepth);

            (_pingFbo, _pingTex) = CreateColorTarget(w / 2, h / 2);
            (_pongFbo, _pongTex) = CreateColorTarget(w / 2, h / 2);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        void BlitPass(uint program, uint sourceTex, uint targetFbo, int width, int height, float dirX, float dirY)
        {
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo);
            _gl.Viewport(0, 0, (uint)width, (uint)height);
            _gl.UseProgram(program);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, sourceTex);
            _gl.Uniform1(_gl.GetUniformLocation(program, "uTex"), 0);
            int dirLoc = _gl.GetUniformLocation(program, "uDir");
            if (dirLoc >= 0) _gl.Uniform2(dirLoc, dirX, dirY);
            _gl.BindVertexArray(_fsVao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }

        void ApplyScreenshotAutopilot()
        {
            // Skip the grid hold, then hand the player's rig to the REAL AIPilot —
            // it drives the rig's InputStatus exactly like a rival, so headless runs
            // exercise the genuine AI → InputStatus → VesselTransformer path.
            _race.SkipCountdown();
            if (!_screenshotAutopilot && _race.State == RaceState.Racing)
            {
                _race.EngageHumanAutopilot();
                _screenshotAutopilot = true;
            }
        }

        unsafe void SetMvp(Matrix4x4 mvp) => _gl.UniformMatrix4(_uMvp, 1, false, (float*)&mvp);

        unsafe void DrawTrails(Matrix4x4 viewProjection)
        {
            var pilots = _race.Pilots;
            for (int i = 0; i < pilots.Count; i++)
                DrawPrismTrail(viewProjection, pilots[i].TrailPrisms, _pilotTrailVerts[i],
                    _pilotTrailVaos[i], _pilotTrailVbos[i], DomainColor(pilots[i].Domain));
        }

        /// <summary>
        /// Draws the REAL prismscape: every quad is a live <see cref="Prism"/> from the
        /// rig's VesselPrismController trail — position/rotation from its Transform, size
        /// from its animated localScale (zero at spawn, growing to TargetScale), tint by
        /// domain. Mass is conserved: nothing here fades a prism out of existence — aged
        /// prisms settle to a steady neon body; the fresh end flares white-hot.
        /// </summary>
        unsafe void DrawPrismTrail(Matrix4x4 viewProjection, List<Prism> trail, List<float> data,
            uint vao, uint vbo, (float r, float g, float b) domain)
        {
            if (trail.Count == 0) return;
            data.Clear(); // reusable scratch — capacity persists across frames
            float now = Time.time;
            for (int i = 0; i < trail.Count; i++)
            {
                var prism = trail[i];
                if (!prism || prism.destroyed) continue;
                var t = prism.transform;
                var scale = t.localScale;
                if (scale.x < 0.05f) continue; // still growing in from zero — invisible

                float freshness = Mathf.Clamp01(1f - (now - prism.prismProperties.TimeCreated) / 4f);
                float alpha = 0.34f + freshness * freshness * 0.4f;
                // newest blocks ramp in so the chase cam never sits inside its own slab
                int fromNewest = trail.Count - 1 - i;
                if (fromNewest < 4) alpha *= fromNewest / 4f;
                if (alpha <= 0.01f) continue;
                // domain hue, lifted toward white while fresh
                float lift = 0.45f * freshness;
                float r = domain.r + (1f - domain.r) * lift;
                float g = domain.g + (1f - domain.g) * lift;
                float b = domain.b + (1f - domain.b) * lift;

                // flat slab spanning the prism's oriented X (width) and Z (length) axes
                var pos = t.position;
                var right = t.rotation * Vector3.right * (scale.x * 0.5f + 0.18f * freshness);
                var forward = t.rotation * Vector3.forward * (scale.z * 0.5f);
                var c0 = pos - right - forward;
                var c1 = pos + right - forward;
                var c2 = pos + right + forward;
                var c3 = pos - right + forward;
                // per-corner camera fade (a slab crossing the frustum otherwise blooms
                // into a giant white wedge — same guard the old per-point ribbon had)
                float a0 = alpha * Mathf.Clamp01(((c0 - _camPos).magnitude - 3f) / 9f);
                float a1 = alpha * Mathf.Clamp01(((c1 - _camPos).magnitude - 3f) / 9f);
                float a2 = alpha * Mathf.Clamp01(((c2 - _camPos).magnitude - 3f) / 9f);
                float a3 = alpha * Mathf.Clamp01(((c3 - _camPos).magnitude - 3f) / 9f);
                if (a0 + a1 + a2 + a3 <= 0.02f) continue;
                Push(data, c0, r, g, b, a0);
                Push(data, c1, r, g, b, a1);
                Push(data, c2, r, g, b, a2);
                Push(data, c0, r, g, b, a0);
                Push(data, c2, r, g, b, a2);
                Push(data, c3, r, g, b, a3);
            }
            if (data.Count == 0) return;
            _gl.DepthMask(false);
            _gl.BindVertexArray(vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            var array = data.ToArray();
            fixed (float* p = array)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(array.Length * sizeof(float)), p, BufferUsageARB.DynamicDraw);
            SetMvp(viewProjection);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(array.Length / 7));
            _gl.DepthMask(true);
        }

        // ── HUD: ortho pass — seven-segment digits, boost bar, crystal diamond ──

        static readonly byte[] SegmentMasks = { 0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110, 0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111 };

        unsafe void DrawHud()
        {
            var data = new List<float>();
            float w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;

            void Segment(float x, float y, float x2, float y2, float r, float g, float b, float a)
            {
                Push(data, new Vector3(x, y, 0f), r, g, b, a);
                Push(data, new Vector3(x2, y2, 0f), r, g, b, a);
            }

            // seven-segment digit at (x,y), size s
            void Digit(int d, float x, float y, float s, float r, float g, float b, float a)
            {
                byte mask = SegmentMasks[d];
                float half = s * 0.5f;
                if ((mask & 1) != 0) Segment(x, y + s, x + half, y + s, r, g, b, a);          // top
                if ((mask & 2) != 0) Segment(x + half, y + s, x + half, y + half, r, g, b, a); // top-right
                if ((mask & 4) != 0) Segment(x + half, y + half, x + half, y, r, g, b, a);     // bottom-right
                if ((mask & 8) != 0) Segment(x, y, x + half, y, r, g, b, a);                   // bottom
                if ((mask & 16) != 0) Segment(x, y + half, x, y, r, g, b, a);                  // bottom-left
                if ((mask & 32) != 0) Segment(x, y + s, x, y + half, r, g, b, a);              // top-left
                if ((mask & 64) != 0) Segment(x, y + half, x + half, y + half, r, g, b, a);    // middle
            }

            void Number(int value, int digits, float x, float y, float s, float r, float g, float b, float a)
            {
                for (int i = digits - 1; i >= 0; i--)
                {
                    Digit(value % 10, x + i * s * 0.85f, y, s, r, g, b, a);
                    value /= 10;
                }
            }

            // timer mm:ss top center
            int total = (int)_race.ElapsedTime;
            float ts = 26f, tx = w * 0.5f - ts * 1.8f, ty = h - 58f;
            Number(total / 60, 2, tx, ty, ts, 0.5f, 0.95f, 1f, 0.9f);
            Segment(tx + ts * 1.85f, ty + ts * 0.68f, tx + ts * 1.85f, ty + ts * 0.78f, 0.5f, 0.95f, 1f, 0.9f);
            Segment(tx + ts * 1.85f, ty + ts * 0.25f, tx + ts * 1.85f, ty + ts * 0.35f, 0.5f, 0.95f, 1f, 0.9f);
            Number(total % 60, 2, tx + ts * 2.1f, ty, ts, 0.5f, 0.95f, 1f, 0.9f);

            // domain panels (rung 4 — the real MultiplayerHUD semantic): ally domain's
            // summed crystals / target on the left, opposing domains' sums on the right.
            // The sums are GameDataSO.GetDomainMetricSum — the director publishes
            // ScoringMetrics.SumByDomain exactly like MultiplayerDomainGamesController's
            // server routine; the target is the REAL turn monitor's published
            // GameDataSO.CrystalTargetCount.
            var gameData = _race.GameData;
            var humanDomain = _pilot.Domain;
            var (ar, ag, ab) = DomainColor(humanDomain);
            Segment(34f, h - 44f, 46f, h - 30f, 1f, 0.82f, 0.25f, 0.95f); // golden diamond glyph
            Segment(46f, h - 30f, 58f, h - 44f, 1f, 0.82f, 0.25f, 0.95f);
            Segment(58f, h - 44f, 46f, h - 58f, 1f, 0.82f, 0.25f, 0.95f);
            Segment(46f, h - 58f, 34f, h - 44f, 1f, 0.82f, 0.25f, 0.95f);
            Number(gameData.GetDomainMetricSum(humanDomain), 2, 72f, h - 58f, 26f, ar, ag, ab, 0.95f);
            Segment(122f, h - 36f, 134f, h - 54f, 1f, 0.85f, 0.3f, 0.6f); // slash
            Number(_race.CrystalTarget, 2, 142f, h - 58f, 26f, 1f, 0.85f, 0.3f, 0.6f);
            // remaining-to-target for the ally domain — the turn monitor's display channel
            Number(_race.RemainingToTarget, 2, 72f, h - 96f, 18f, 0.5f, 0.95f, 1f, 0.7f);

            // opposing domains' totals, top-right, each in its domain color
            {
                int dc2 = Math.Clamp(gameData.RequestedDomainCount, 1, GameDataSO.ActiveDomains.Length);
                int panel = 0;
                for (int i = 0; i < dc2; i++)
                {
                    var domain = GameDataSO.ActiveDomains[i];
                    if (domain == humanDomain) continue;
                    var (dr, dg, db) = DomainColor(domain);
                    Number(gameData.GetDomainMetricSum(domain), 2, w - 120f - panel * 70f, h - 58f, 26f, dr, dg, db, 0.95f);
                    panel++;
                }
            }

            // race position (P/total) and lap, under the rival count
            int position = _race.PositionOf(_pilot);
            int fieldSize = _race.Pilots.Count;
            Number(position, 1, w - 120f, h - 104f, 24f, 1f, 1f, 1f, 0.95f);
            Segment(w - 100f, h - 84f, w - 88f, h - 102f, 0.7f, 0.7f, 0.7f, 0.6f); // slash
            Number(fieldSize, 1, w - 84f, h - 104f, 24f, 0.7f, 0.7f, 0.7f, 0.7f);
            // lap counter: small diamond + lap number
            Segment(w - 162f, h - 92f, w - 154f, h - 84f, 0.5f, 0.95f, 1f, 0.8f);
            Segment(w - 154f, h - 84f, w - 146f, h - 92f, 0.5f, 0.95f, 1f, 0.8f);
            Segment(w - 146f, h - 92f, w - 154f, h - 100f, 0.5f, 0.95f, 1f, 0.8f);
            Segment(w - 154f, h - 100f, w - 162f, h - 92f, 0.5f, 0.95f, 1f, 0.8f);
            Number(_pilot.Lap, 1, w - 196f, h - 104f, 24f, 0.5f, 0.95f, 1f, 0.9f);

            // minimap, bottom-right: the circuit outline + a dot per pilot in domain color
            {
                float mapCx = w - 110f, mapCy = 120f, mapScale = 78f / (SkimTrack.BaseRadius + 60f);
                var line = _race.Track.Centerline;
                int step = 8;
                for (int i = 0; i < line.Count - step; i += step)
                {
                    var a = line[i];
                    var b = line[Math.Min(i + step, line.Count - 1)];
                    Segment(mapCx + a.x * mapScale, mapCy + a.z * mapScale,
                            mapCx + b.x * mapScale, mapCy + b.z * mapScale, 0.25f, 0.5f, 0.65f, 0.5f);
                }
                foreach (var pilot in _race.Pilots)
                {
                    var dc = DomainColor(pilot.Domain);
                    bool me = ReferenceEquals(pilot, _pilot);
                    float size = me ? 5f : 3.5f;
                    float px = mapCx + pilot.Transform.position.x * mapScale;
                    float py = mapCy + pilot.Transform.position.z * mapScale;
                    Segment(px - size, py - size, px + size, py + size, dc.r, dc.g, dc.b, me ? 1f : 0.85f);
                    Segment(px - size, py + size, px + size, py - size, dc.r, dc.g, dc.b, me ? 1f : 0.85f);
                }
            }

            // energy bar bottom center (reads the rig's real ResourceSystem) — energy IS
            // top speed now; the fill flares white-hot while skimming a trail
            float boost = _pilot.Resources.Resources[0].CurrentAmount;
            float barWidth = w * 0.3f, bx = (w - barWidth) * 0.5f;
            Segment(bx, 36f, bx + barWidth, 36f, 0.2f, 0.4f, 0.6f, 0.35f);
            (float er, float eg, float eb) = _pilot.IsSkimming ? (0.75f, 1f, 1f) : (0.2f, 0.9f, 1f);
            for (int i = 0; i < 3; i++) // thick neon fill
                Segment(bx, 33f + i * 3f, bx + barWidth * boost, 33f + i * 3f, er, eg, eb, _pilot.IsSkimming ? 1f : 0.85f);

            // analog drift gauge: thin magenta strip riding above the energy bar
            if (_pilot.DriftAmount > 0.02f)
                Segment(bx, 44f, bx + barWidth * _pilot.DriftAmount, 44f, 1f, 0.3f, 0.9f, 0.9f);

            // countdown / finish banner: oversized center digits
            if (_race.State == RaceState.Countdown)
                Digit(Math.Max(1, (int)MathF.Ceiling(_race.Countdown)), w * 0.5f - 30f, h * 0.5f - 40f, 80f, 1f, 0.3f, 0.9f, 0.95f);
            else if (_race.State == RaceState.Finished)
            {
                // gold digits = your DOMAIN took the race (teammates win together); ruby = a
                // rival domain did. WinnerDomain is the scoring rule's domain-aggregated call.
                bool won = humanDomain == gameData.WinnerDomain;
                (float fr, float fg, float fb) = won ? (1f, 0.85f, 0.3f) : (1f, 0.2f, 0.3f);
                // Golf scoring: the first row of the rule-sorted RoundStatsList carries the
                // winning domain's finish time (losers carry the 10000+deficit sentinel).
                float finalTime = gameData.RoundStatsList.Count > 0 ? gameData.RoundStatsList[0].Score : 0f;
                int centiseconds = (int)(finalTime * 100f) % 100;
                float fs = 44f, fx = w * 0.5f - fs * 2.6f, fy = h * 0.62f;
                Number((int)finalTime / 60, 2, fx, fy, fs, fr, fg, fb, 1f);
                Number((int)finalTime % 60, 2, fx + fs * 2.0f, fy, fs, fr, fg, fb, 0.85f);
                Number(centiseconds, 2, fx + fs * 4.0f, fy, fs, fr, fg, fb, 0.7f);

                // scoreboard: gameData.RoundStatsList IS the standings — golf-sorted by the
                // real pipeline (SortRoundStats(golfRules: true) after the rule's
                // AssignScores). Domain marker, rank, crystals per row.
                var standings = gameData.RoundStatsList;
                float rowY = h * 0.62f - 64f;
                for (int rank = 0; rank < standings.Count; rank++, rowY -= 42f)
                {
                    var stats = standings[rank];
                    var dc = DomainColor(stats.Domain);
                    bool me = ReferenceEquals(stats, _pilot.Stats);
                    float rx0 = w * 0.5f - 120f;
                    // domain marker: filled diamond (player's pulses brighter)
                    float ma = me ? 1f : 0.7f;
                    Segment(rx0, rowY + 12f, rx0 + 12f, rowY + 24f, dc.r, dc.g, dc.b, ma);
                    Segment(rx0 + 12f, rowY + 24f, rx0 + 24f, rowY + 12f, dc.r, dc.g, dc.b, ma);
                    Segment(rx0 + 24f, rowY + 12f, rx0 + 12f, rowY, dc.r, dc.g, dc.b, ma);
                    Segment(rx0 + 12f, rowY, rx0, rowY + 12f, dc.r, dc.g, dc.b, ma);
                    Number(rank + 1, 1, rx0 + 44f, rowY, 24f, 1f, 1f, 1f, me ? 1f : 0.7f);
                    Number(stats.CrystalsCollected, 2, rx0 + 100f, rowY, 24f, dc.r, dc.g, dc.b, 0.95f);
                }

                // replay button: pulsing circular arrow under the standings —
                // A / Enter / R / Start / click all start the rematch
                {
                    float cx = w * 0.5f, cy = rowY - 26f, radius = 20f;
                    float pulse = 0.55f + 0.4f * Mathf.Sin(Time.time * 3f);
                    const int arcSegments = 20;
                    const float arcStart = 0.6f, arcEnd = MathF.Tau - 0.6f; // gap at the right
                    for (int i = 0; i < arcSegments; i++)
                    {
                        float a0 = arcStart + (arcEnd - arcStart) * (i / (float)arcSegments);
                        float a1 = arcStart + (arcEnd - arcStart) * ((i + 1) / (float)arcSegments);
                        Segment(cx + MathF.Cos(a0) * radius, cy + MathF.Sin(a0) * radius,
                                cx + MathF.Cos(a1) * radius, cy + MathF.Sin(a1) * radius,
                                0.5f, 0.95f, 1f, pulse);
                    }
                    // arrowhead at the arc's end
                    float tipX = cx + MathF.Cos(arcStart) * radius, tipY = cy + MathF.Sin(arcStart) * radius;
                    Segment(tipX, tipY, tipX + 9f, tipY + 4f, 0.5f, 0.95f, 1f, pulse);
                    Segment(tipX, tipY, tipX - 2f, tipY + 10f, 0.5f, 0.95f, 1f, pulse);
                }
            }

            if (data.Count == 0) return;
            var ortho = Matrix4x4.CreateOrthographicOffCenter(0f, w, 0f, h, -1f, 1f);
            _gl.Disable(EnableCap.DepthTest);
            _gl.BindVertexArray(_hudVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _hudVbo);
            var array = data.ToArray();
            fixed (float* p = array)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(array.Length * sizeof(float)), p, BufferUsageARB.DynamicDraw);
            }
            SetMvp(ortho);
            _gl.LineWidth(3f);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(array.Length / 7));
            _gl.Enable(EnableCap.DepthTest);
        }

        unsafe void CaptureScreenshot()
        {
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            var pixels = new byte[w * h * 4];
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            MiniPng.Write(_screenshotPath, pixels, w, h);
            int totalTrail = 0; // REAL prisms — the VesselPrismController trail lists
            var counts = new List<string>();
            foreach (var pilot in _race.Pilots)
            {
                totalTrail += pilot.TrailPrisms.Count;
                counts.Add($"{pilot.Stats.CrystalsCollected}");
            }
            var gameData = _race.GameData;
            Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, " +
                $"crystals [{string.Join(",", counts)}] (target {_race.CrystalTarget}, claims {_race.TotalClaims}), state {_race.State}, " +
                $"domains J{gameData.GetDomainMetricSum(Domains.Jade)}/R{gameData.GetDomainMetricSum(Domains.Ruby)}" +
                $"/G{gameData.GetDomainMetricSum(Domains.Gold)} (remaining {_race.RemainingToTarget}), " +
                $"P{_race.PositionOf(_pilot)} lap {_pilot.Lap}, speed {_pilot.Status.Speed:F1}, " +
                $"energy {_pilot.Resources.Resources[0].CurrentAmount:F2}, skim {_pilot.IsSkimming}, " +
                $"levels C{_pilot.Resources.GetLevel(Element.Charge)}/M{_pilot.Resources.GetLevel(Element.Mass)}" +
                $"/S{_pilot.Resources.GetLevel(Element.Space)}/T{_pilot.Resources.GetLevel(Element.Time)}, " +
                $"trail {totalTrail}");
        }
    }
}
