using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Silk.NET.Input;
using Silk.NET.Maths;
#if GLES
using Silk.NET.OpenGLES;   // Android head — same generated API surface as Silk.NET.OpenGL
#else
using Silk.NET.OpenGL;
#endif
using Silk.NET.Windowing;
using EngineInput = CosmicShore.Engine.InputSystem;
using EngineTouch = CosmicShore.Engine.InputSystem.EnhancedTouch.Touch;
using EngineObject = CosmicShore.Engine.Object;
using Quaternion = CosmicShore.Engine.Quaternion;
using Vector2 = CosmicShore.Engine.Vector2;
using Vector3 = CosmicShore.Engine.Vector3;
using Random = System.Random;

namespace CosmicShore.Client
{
    /// <summary>
    /// Presentation host for FREESTYLE (lava-lamp) mode — the client's Menu_Main: one
    /// living Cell (membrane wireframe, drifting cytoplasm motes, flora canopies and
    /// fauna bodies drawn as the same neon prism slabs as vessel trails — they ARE the
    /// same Prism family), the toybox ring (tinted spheres + vector-font labels, the
    /// painting toy's guide lines read straight from its LineRenderers), and the local
    /// vessel drifting on autopilot until the player takes the stick (Tab / gamepad Y).
    /// Headless `--screenshot` mode renders under Xvfb/Mesa and prints the freestyle
    /// diag line (populations, vessel class, domain, autopilot state) — deterministic
    /// per seed.
    /// </summary>
    public sealed class FreestyleWindow
    {
        readonly int _seed;
        readonly string _screenshotPath;
        readonly int _screenshotFrame;

        IView _window; // IWindow on desktop; the SDL view on the Android head
        GL _gl;
        IInputContext _inputContext;

        GameLoop _loop;
        FreestyleDirector _director;
        ThemeManagerDataContainerSO _theme;
        IInputStatus _playerStatus;                      // wired once the chain spawns the vessel
        readonly GamepadInputStrategy _gamepadStrategy = new();
        // Mobile: the game's REAL dual-thumb touch scheme, fed by the host's EnhancedTouch backend
        readonly TouchInputStrategy _touchStrategy = Application.isMobilePlatform ? new TouchInputStrategy() : null;
        bool _touchDriving;
        bool _prevTriple; // three-finger tap = Tab (lava lamp ↔ freestyle)
        bool _strategyInitialized;
        EngineInput.Gamepad _shimPad;
        bool _prevA, _prevY;
        bool _prevKbSpace, _prevKbShift;

        uint _program;
        int _uMvp;

        // post chain: scene FBO → bright → blur ping/pong (half res) → composite
        uint _postProgram, _blurProgram, _compositeProgram;
        uint _sceneFbo, _sceneTex, _sceneDepth;
        uint _pingFbo, _pingTex, _pongFbo, _pongTex;
        uint _fsVao, _fsVbo;
        int _fbWidth, _fbHeight;

        // static geometry
        uint _starVao; int _starCount;
        uint _membraneVao; int _membraneCount;
        (uint vao, int count) _crystalMesh;
        readonly Dictionary<(bool squirrel, Domains domain), (uint vao, int count)> _hullMeshes = new();

        // dynamic buffers (rebuilt per frame)
        uint _prismVao, _prismVbo; readonly List<float> _prismVerts = new();
        uint _lineVao, _lineVbo; readonly List<float> _lineVerts = new();
        uint _hudVao, _hudVbo;

        Vector3 _camPos, _camLook = Vector3.zero;
        AudioEngine _audio;
        int _frameIndex;

        public FreestyleWindow(int seed, string screenshotPath, int screenshotFrame)
        {
            _seed = seed;
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
        }

        Color DomainUIColor(Domains domain) => _theme.GetDomainUIColor(domain);

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1280, 720),
                Title = "Cosmic Shore — Freestyle (port progress build)",
                VSync = true,
            };
            Console.WriteLine("[1/4] creating window (GLFW)...");
            Run(Window.Create(options));
        }

        /// <summary>Runs on a caller-provided surface — the Android head passes its SDL view.</summary>
        public void Run(IView view)
        {
            _window = view;
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            Console.WriteLine("[2/4] entering run loop...");
            _window.Run();
            _director?.Shutdown(); // stop the rig's spawn loop + the cell's coroutines deterministically
        }

        // ── setup ────────────────────────────────────────────────────

        void OnLoad()
        {
            Console.WriteLine("[3/4] window open — initializing GL/scene...");
            _gl = GL.GetApi(_window);
            _inputContext = _window.CreateInput();
            foreach (var keyboard in _inputContext.Keyboards)
                keyboard.KeyDown += (_, key, _) =>
                {
                    if (key == Key.Escape) _window.Close();
                    if (key == Key.Tab) _director.ToggleControl(); // lava lamp ↔ freestyle
                };

            (_loop, _director) = FreestyleFactory.Create(_seed);
            _theme = _director.GameData.ThemeManagerData;
            _audio = new AudioEngine(disabled: _screenshotPath != null);

            _program = CompileProgram(MainVertexSrc, MainFragmentSrc);
            _uMvp = _gl.GetUniformLocation(_program, "uMvp");

            BuildStars();
            BuildMembrane();
            _crystalMesh = BuildOctahedron(new Color(0.9f, 0.95f, 1f));

            _prismVao = _gl.GenVertexArray(); _prismVbo = _gl.GenBuffer();
            ConfigureDynamicVao(_prismVao, _prismVbo);
            _lineVao = _gl.GenVertexArray(); _lineVbo = _gl.GenBuffer();
            ConfigureDynamicVao(_lineVao, _lineVbo);
            _hudVao = _gl.GenVertexArray(); _hudVbo = _gl.GenBuffer();
            ConfigureDynamicVao(_hudVao, _hudVbo);

            _postProgram = CompileProgram(FullscreenVertexSrc, BrightFragmentSrc);
            _blurProgram = CompileProgram(FullscreenVertexSrc, BlurFragmentSrc);
            _compositeProgram = CompileProgram(FullscreenVertexSrc, CompositeFragmentSrc);
            BuildFullscreenTriangle();
            EnsureRenderTargets();

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // additive neon
            _gl.Enable(EnableCap.DepthTest);
#if !GLES
            _gl.Enable(EnableCap.ProgramPointSize); // ES 3.0: gl_PointSize is always on
#endif

            _camPos = new Vector3(0f, 40f, -520f);
            Console.WriteLine("[4/4] ready — lava lamp. Tab (or gamepad Y) to take the stick.");
        }

        // ── shaders (SkimRace idiom) ─────────────────────────────────

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
uniform vec2 uDir;
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
    vec3 c = scene + bloom * 1.35;
    c = c / (c + vec3(0.55));
    c = pow(c, vec3(0.85));
    frag = vec4(c, 1.0);
}";

        /// <summary>
        /// Shaders are authored once in GLSL 330 core; the GLES head (Android) compiles
        /// the same sources as GLSL ES 300 — mechanically retargeted here (ES requires an
        /// explicit default float precision; the rest of the dialect used is identical).
        /// </summary>
        static string PlatformShader(string src) =>
#if GLES
            src.Replace("#version 330 core", "#version 300 es\nprecision highp float;");
#else
            src;
#endif

        uint CompileProgram(string vertexSrc, string fragmentSrc)
        {
            uint vs = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vs, PlatformShader(vertexSrc));
            _gl.CompileShader(vs);
            _gl.GetShader(vs, ShaderParameterName.CompileStatus, out int okV);
            if (okV == 0) throw new InvalidOperationException($"vertex: {_gl.GetShaderInfoLog(vs)}");
            uint fs = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fs, PlatformShader(fragmentSrc));
            _gl.CompileShader(fs);
            _gl.GetShader(fs, ShaderParameterName.CompileStatus, out int okF);
            if (okF == 0) throw new InvalidOperationException($"fragment: {_gl.GetShaderInfoLog(fs)}");

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

        // ── geometry helpers (pos.xyz + color.rgba interleaved) ──────

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
                var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f).normalized;
                float radius = 900f + (float)rng.NextDouble() * 1200f; // beyond the membrane
                var p = dir * radius;
                float t = (float)rng.NextDouble();
                (float r, float g, float b) = t < 0.72f ? (0.85f, 0.88f, 1f) : t < 0.86f ? (0.95f, 0.45f, 0.95f) : (0.4f, 0.95f, 1f);
                float brightness = 0.25f + (float)rng.NextDouble() * 0.75f;
                Push(data, p, r * brightness, g * brightness, b * brightness, 0.9f);
            }
            _starCount = data.Count / 7;
            (_starVao, _) = UploadStatic(data.ToArray());
        }

        /// <summary>Wireframe sphere at the cell's REAL membrane radius: three great
        /// circles + two latitude rings — the playfield-boundary read made visible.</summary>
        void BuildMembrane()
        {
            float radius = Math.Max(1f, _director.Cell.MembraneRadius);
            var data = new List<float>();
            const int segments = 96;
            var tint = new Color(0.16f, 0.55f, 0.75f);

            void Ring(Func<float, Vector3> pointAt, float alpha)
            {
                for (int i = 0; i < segments; i++)
                {
                    float t0 = i / (float)segments * MathF.Tau;
                    float t1 = (i + 1) / (float)segments * MathF.Tau;
                    Push(data, pointAt(t0), tint.r, tint.g, tint.b, alpha);
                    Push(data, pointAt(t1), tint.r, tint.g, tint.b, alpha);
                }
            }

            Ring(t => new Vector3(MathF.Cos(t), 0f, MathF.Sin(t)) * radius, 0.45f);          // equator
            Ring(t => new Vector3(MathF.Cos(t), MathF.Sin(t), 0f) * radius, 0.30f);          // XY
            Ring(t => new Vector3(0f, MathF.Cos(t), MathF.Sin(t)) * radius, 0.30f);          // YZ
            float lat = radius * 0.7071f;
            Ring(t => new Vector3(MathF.Cos(t) * lat, radius * 0.7071f, MathF.Sin(t) * lat), 0.22f);
            Ring(t => new Vector3(MathF.Cos(t) * lat, -radius * 0.7071f, MathF.Sin(t) * lat), 0.22f);

            _membraneCount = data.Count / 7;
            (_membraneVao, _) = UploadStatic(data.ToArray());
        }

        (uint vao, int count) BuildOctahedron(Color tint)
        {
            var top = new Vector3(0f, 1.2f, 0f);
            var bottom = new Vector3(0f, -1.2f, 0f);
            var equator = new[]
            {
                new Vector3(0.9f, 0f, 0f), new Vector3(0f, 0f, 0.9f),
                new Vector3(-0.9f, 0f, 0f), new Vector3(0f, 0f, -0.9f),
            };
            float Lift(float c) => c + (1f - c) * 0.45f;
            var lifted = new Color(Lift(tint.r), Lift(tint.g), Lift(tint.b));
            var data = new List<float>();
            for (int i = 0; i < 4; i++)
            {
                var a = equator[i];
                var b = equator[(i + 1) % 4];
                float warm = i % 2 == 0 ? 1f : 0.86f;
                Push(data, top, tint.r * warm, tint.g * warm, tint.b * warm, 0.95f);
                Push(data, a, lifted.r, lifted.g, lifted.b, 0.8f);
                Push(data, b, tint.r * 0.85f, tint.g * 0.85f, tint.b * 0.85f, 0.8f);
                Push(data, bottom, tint.r * 0.8f * warm, tint.g * 0.8f * warm, tint.b * 0.8f * warm, 0.95f);
                Push(data, b, tint.r * 0.85f, tint.g * 0.85f, tint.b * 0.85f, 0.8f);
                Push(data, a, lifted.r, lifted.g, lifted.b, 0.8f);
            }
            var (vao, _) = UploadStatic(data.ToArray());
            return (vao, data.Count / 7);
        }

        /// <summary>Hull mesh per (is-squirrel, domain): the embedded Squirrel bake for the
        /// Squirrel, the dart fallback for every other class — colored from the LIVE
        /// domain's ShipMaterial pair, so a domain-toy flythrough re-tints the hull.</summary>
        (uint vao, int count) GetHullMesh(VesselClassType vesselClass, Domains domain)
        {
            bool squirrel = vesselClass == VesselClassType.Squirrel;
            if (_hullMeshes.TryGetValue((squirrel, domain), out var mesh)) return mesh;

            var shipMaterial = _theme.TeamMaterialSets[domain].ShipMaterial;
            var color1 = shipMaterial.GetColor("_Color1");
            var color2 = shipMaterial.GetColor("_Color2");
            float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            var (shadow, lit) = Luminance(color1) <= Luminance(color2) ? (color1, color2) : (color2, color1);

            if (squirrel)
            {
                try { mesh = BuildSquirrel(shadow, lit); }
                catch (Exception e)
                {
                    Console.WriteLine($"squirrel mesh unavailable ({e.Message}) — dart fallback");
                    mesh = BuildDart(shadow, lit);
                }
            }
            else
            {
                mesh = BuildDart(shadow, lit);
            }
            _hullMeshes[(squirrel, domain)] = mesh;
            return mesh;
        }

        (uint vao, int count) BuildSquirrel(Color shadow, Color lit)
        {
            using var stream = typeof(FreestyleWindow).Assembly
                .GetManifestResourceStream("CosmicShore.Client.Assets.squirrel.mesh")
                ?? throw new InvalidOperationException("embedded squirrel.mesh missing");
            using var reader = new System.IO.BinaryReader(stream);
            int triCount = reader.ReadInt32();

            var keyLight = new Vector3(0.42f, 0.78f, -0.46f).normalized;
            var rimLight = new Vector3(-0.3f, -0.2f, 0.93f).normalized;

            var dataList = new List<float>(triCount * 3 * 7);
            for (int t = 0; t < triCount; t++)
            {
                for (int v = 0; v < 3; v++)
                {
                    float px = reader.ReadSingle(), py = reader.ReadSingle(), pz = reader.ReadSingle();
                    float nx = reader.ReadSingle(), ny = reader.ReadSingle(), nz = reader.ReadSingle();
                    px = -px; pz = -pz; nx = -nx; nz = -nz; // nose-forward remap (RaceWindow parity)
                    var normal = new Vector3(nx, ny, nz);
                    float key = MathF.Max(0f, Vector3.Dot(normal, keyLight));
                    float rim = MathF.Max(0f, Vector3.Dot(normal, rimLight));
                    float shade = Mathf.Clamp01(0.62f * key + 0.3f * rim);
                    var c = Color.Lerp(shadow, lit, 0.15f + 0.85f * shade);
                    Push(dataList, new Vector3(px, py, pz), c.r, c.g, c.b, 0.95f);
                }
            }
            var (vao, _) = UploadStatic(dataList.ToArray());
            return (vao, triCount * 3);
        }

        (uint vao, int count) BuildDart(Color shadow, Color lit)
        {
            var nose = new Vector3(0f, 0f, 2.6f);
            var tail = new Vector3(0f, 0.25f, -1.4f);
            var left = new Vector3(-1.7f, -0.15f, -1.2f);
            var right = new Vector3(1.7f, -0.15f, -1.2f);
            var belly = new Vector3(0f, -0.35f, -0.9f);
            var fin = new Vector3(0f, 1.05f, -1.5f);

            var data = new List<float>();
            void Tri(Vector3 a, Vector3 b, Vector3 c, Color color, float al)
            {
                Push(data, a, color.r, color.g, color.b, al);
                Push(data, b, color.r, color.g, color.b, al);
                Push(data, c, color.r, color.g, color.b, al);
            }
            void Hull(Vector3 a, Vector3 b, Vector3 c, float bright, float alpha)
                => Tri(a, b, c, Color.Lerp(shadow, lit, bright), alpha);
            Hull(nose, left, tail, 1f, 0.95f);
            Hull(nose, tail, right, 0.88f, 0.95f);
            Hull(nose, belly, left, 0.55f, 0.9f);
            Hull(nose, right, belly, 0.5f, 0.9f);
            Hull(tail, left, belly, 0.4f, 0.9f);
            Hull(tail, belly, right, 0.38f, 0.9f);
            Tri(tail, fin, nose, Color.Lerp(shadow, lit, 0.8f), 0.75f);
            var (vao, _) = UploadStatic(data.ToArray());
            return (vao, data.Count / 7);
        }

        // ── per-frame ────────────────────────────────────────────────

        void OnUpdate(double dt)
        {
            if (_screenshotPath != null) return; // deterministic ticks happen in OnRender

            ApplyHumanInput();
            _loop.Tick((float)dt);
            UpdateCameraAudio((float)dt);
        }

        /// <summary>
        /// The rig's REAL InputStatus (wired once the initializer chain spawns the
        /// vessel). Only drives it in FREESTYLE — in the lava lamp the real AIPilot owns
        /// the sticks. Same authentic dual-stick scheme as the race window.
        /// </summary>
        void ApplyHumanInput()
        {
            var player = _director.LocalPlayer;
            if (player == null || player.InputStatus == null) return;
            if (!_strategyInitialized)
            {
                _playerStatus = player.InputStatus;
                _gamepadStrategy.Initialize(_playerStatus);
                _gamepadStrategy.OnStrategyActivated();
                _touchStrategy?.Initialize(_playerStatus); // sizes thumbsticks from Screen.dpi — host set it pre-Load
                _strategyInitialized = true;
            }

            var silkPad = _inputContext.Gamepads.Count > 0 ? _inputContext.Gamepads[0] : null;
            bool padPresent = silkPad != null && silkPad.Thumbsticks.Count >= 2;

            // Touch (mobile): same seam as the race window — the REAL TouchInputStrategy
            // reads the EnhancedTouch shim the Android host pumps. Three fingers down
            // together = Tab (take/release the stick); in the lava lamp the AIPilot owns
            // the rig so touches only feed the toggle.
            bool touchActive = _touchStrategy != null && EngineTouch.activeTouches.Count > 0;
            if (touchActive && !_touchDriving)
            {
                _touchStrategy.OnStrategyActivated(); // ActiveInputDevice = Touch
                _touchDriving = true;
            }
            else if (!touchActive && _touchDriving && padPresent)
            {
                _touchStrategy.ProcessInput(); // one zero-touch pass releases drift state
                _gamepadStrategy.OnStrategyActivated();
                _touchDriving = false;
            }
            if (_touchDriving)
            {
                bool triple = EngineTouch.activeTouches.Count >= 3;
                if (triple && !_prevTriple) _director.ToggleControl();
                _prevTriple = triple;
                if (!_director.IsFreestyle) return; // AIPilot owns the rig in the lava lamp
                _touchStrategy.ProcessInput();      // authentic dual-thumb scheme → InputStatus
                return;
            }

            if (padPresent)
            {
                _shimPad ??= EngineInput.Gamepad.current = new EngineInput.Gamepad();
                _shimPad.leftStick.value = new Vector2(silkPad.Thumbsticks[0].X, -silkPad.Thumbsticks[0].Y);
                _shimPad.rightStick.value = new Vector2(silkPad.Thumbsticks[1].X, -silkPad.Thumbsticks[1].Y);
                _shimPad.leftTrigger.value = silkPad.Triggers.Count > 0 ? silkPad.Triggers[0].Position : 0f;
                _shimPad.rightTrigger.value = silkPad.Triggers.Count > 1 ? silkPad.Triggers[1].Position : 0f;

                bool a = false, y = false;
                foreach (var button in silkPad.Buttons)
                {
                    if (button.Name == ButtonName.A) a = button.Pressed;
                    if (button.Name == ButtonName.Y) y = button.Pressed;
                }
                _shimPad.buttonSouth.isPressed = a;
                _shimPad.buttonSouth.wasPressedThisFrame = a && !_prevA;
                _shimPad.buttonSouth.wasReleasedThisFrame = !a && _prevA;
                _prevA = a;
                if (y && !_prevY) _director.ToggleControl(); // gamepad Y = lava lamp ↔ freestyle
                _prevY = y;

                if (!_director.IsFreestyle) return; // AIPilot owns the rig in the lava lamp

                _gamepadStrategy.ProcessInput();
                // Prompter preferences (RaceWindow parity): inverted yaw + roll.
                _playerStatus.XSum = -_playerStatus.XSum;
                _playerStatus.YDiff = -_playerStatus.YDiff;
                return;
            }

            if (!_director.IsFreestyle) return;

            // Keyboard fallback: WASD = left stick, arrows = right stick.
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
            if (rx == 0f && ry == 0f && (lx != 0f || ly != 0f)) { rx = lx; ry = ly; }
            _playerStatus.XSum = Mathf.Clamp(rx + lx, -1f, 1f);
            _playerStatus.YSum = Mathf.Clamp(-(ry + ly), -1f, 1f);
            _playerStatus.YDiff = Mathf.Clamp(ry - ly, -1f, 1f);
            _playerStatus.XDiff = (Mathf.Clamp(rx - lx, -2f, 2f) + 2f) / 4f + (space ? 0.5f : 0.12f);
            _playerStatus.XDiff = Mathf.Clamp01(_playerStatus.XDiff);
            _playerStatus.LeftTriggerAnalog = shift ? 1f : 0f;
            _playerStatus.RightTriggerAnalog = 0f;
            if (shift != _prevKbShift)
            {
                if (shift) _playerStatus.OnButtonPressed.Raise(InputEvents.OnlyLeftStickAction);
                else _playerStatus.OnButtonReleased.Raise(InputEvents.OnlyLeftStickAction);
                _prevKbShift = shift;
            }
            if (space != _prevKbSpace)
            {
                if (space) _playerStatus.OnButtonPressed.Raise(InputEvents.Button1Action);
                else _playerStatus.OnButtonReleased.Raise(InputEvents.Button1Action);
                _prevKbSpace = space;
            }
        }

        void UpdateCameraAudio(float dt)
        {
            var status = _director.VesselStatus;
            if (status != null)
            {
                var t3 = status.Transform;
                var desired = t3.position - t3.forward * 9f + t3.up * 2.6f;
                float lag = _screenshotPath != null ? 1f : Mathf.Clamp01(dt * 5f);
                _camPos = Vector3.Lerp(_camPos, desired, lag);
                _camLook = t3.position + t3.forward * 12f;

                _audio.SetEngineState(Mathf.Clamp01(status.Speed / 115f), status.IsBoosting);
                float drift = _playerStatus != null
                    ? Mathf.Clamp01(_playerStatus.LeftTriggerAnalog + _playerStatus.RightTriggerAnalog)
                    : 0f;
                _audio.SetSkimDriftState(_director.IsSkimming ? _director.SkimStrength : 0f,
                    _director.IsFreestyle ? drift : 0f);
            }
        }

        unsafe void OnRender(double dt)
        {
            _frameIndex++;
            if (_screenshotPath != null)
            {
                _loop.Tick(1f / 60f); // deterministic sim frames
                UpdateCameraAudio(1f / 60f);
            }
            EnsureRenderTargets();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
            _gl.ClearColor(0.012f, 0.0f, 0.045f, 1f);
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            _gl.UseProgram(_program);

            float aspect = _window.FramebufferSize.X / (float)Math.Max(1, _window.FramebufferSize.Y);
            var up = _director.VesselStatus?.Transform.up ?? Vector3.up;
            var view = Matrix4x4.CreateLookAt(_camPos, _camLook, up);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(70f * MathF.PI / 180f, aspect, 0.1f, 3200f);
            var viewProjection = view * projection;

            SetMvp(viewProjection);
            _gl.BindVertexArray(_starVao);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_starCount);
            _gl.BindVertexArray(_membraneVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_membraneCount);

            // the cell's anchor crystal — a slow white pulse at the nucleus. The autopilot
            // orbits right through it, so cull when the chase cam swoops close (the
            // RaceWindow burst-cull precedent: a near-camera additive octahedron blooms
            // into a screen-filling white wedge otherwise).
            {
                float spin = Time.time * 1.2f;
                float pulse = 8f * (1f + 0.1f * Mathf.Sin(Time.time * 2f));
                if (_camPos.magnitude > pulse * 4f)
                {
                    var model = Matrix4x4.CreateScale(pulse) * Matrix4x4.CreateRotationY(spin);
                    SetMvp(model * viewProjection);
                    _gl.BindVertexArray(_crystalMesh.vao);
                    _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_crystalMesh.count);
                }
            }

            DrawPrisms(viewProjection);   // vessel trails + flora canopies + fauna bodies
            DrawWorldLines(viewProjection); // motes, toys, painter guides, labels
            DrawVessel(viewProjection);
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

        // ── prism slabs: one pass for ALL live mass (trail + ecology) ──

        unsafe void DrawPrisms(Matrix4x4 viewProjection)
        {
            _prismVerts.Clear();
            float now = Time.time;

            var live = _director.PrismFactory.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var go = live[i];
                if (!go) continue;
                var prism = go.GetComponent<Prism>();
                if (prism == null || prism.destroyed) continue;
                PushPrismSlab(prism, now);
            }

            var lifeformPrisms = _director.LifeFormPrisms;
            for (int i = 0; i < lifeformPrisms.Count; i++)
            {
                var hp = lifeformPrisms[i];
                if (!hp || hp.destroyed) continue;
                PushPrismSlab(hp, now);
            }

            if (_prismVerts.Count == 0) return;
            _gl.DepthMask(false);
            _gl.BindVertexArray(_prismVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _prismVbo);
            var array = _prismVerts.ToArray();
            fixed (float* p = array)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(array.Length * sizeof(float)), p, BufferUsageARB.DynamicDraw);
            SetMvp(viewProjection);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(array.Length / 7));
            _gl.DepthMask(true);
        }

        /// <summary>One oriented slab per live prism — colors from the prism's LIVE shared
        /// material (the real PrismTeamManager/StateManager assignment), so domain steals
        /// and state changes recolor without any renderer-side bookkeeping.</summary>
        void PushPrismSlab(Prism prism, float now)
        {
            var t = prism.transform;
            var scale = t.localScale;
            if (scale.x < 0.05f && scale.y < 0.05f) return; // still growing in from zero

            var (bright, dark) = SkimRaceTheme.PrismDrawColors(prism, _theme);
            float freshness = Mathf.Clamp01(1f - (now - prism.prismProperties.TimeCreated) / 4f);
            var body = Color.Lerp(dark, bright, 0.35f + 0.65f * freshness);
            float alpha = 0.34f + freshness * freshness * 0.3f;

            var pos = t.position;
            var right = t.rotation * Vector3.right * (scale.x * 0.5f + 0.18f * freshness);
            var forward = t.rotation * Vector3.forward * (scale.z * 0.5f);
            var c0 = pos - right - forward;
            var c1 = pos + right - forward;
            var c2 = pos + right + forward;
            var c3 = pos - right + forward;
            float a0 = alpha * Mathf.Clamp01(((c0 - _camPos).magnitude - 3f) / 9f);
            float a1 = alpha * Mathf.Clamp01(((c1 - _camPos).magnitude - 3f) / 9f);
            float a2 = alpha * Mathf.Clamp01(((c2 - _camPos).magnitude - 3f) / 9f);
            float a3 = alpha * Mathf.Clamp01(((c3 - _camPos).magnitude - 3f) / 9f);
            if (a0 + a1 + a2 + a3 <= 0.02f) return;
            Push(_prismVerts, c0, body.r, body.g, body.b, a0);
            Push(_prismVerts, c1, body.r, body.g, body.b, a1);
            Push(_prismVerts, c2, body.r, body.g, body.b, a2);
            Push(_prismVerts, c0, body.r, body.g, body.b, a0);
            Push(_prismVerts, c2, body.r, body.g, body.b, a2);
            Push(_prismVerts, c3, body.r, body.g, body.b, a3);
        }

        // ── world lines: motes, toy rings + labels, painter guides ───

        unsafe void DrawWorldLines(Matrix4x4 viewProjection)
        {
            _lineVerts.Clear();

            DrawCytoplasmMotes();
            DrawToys();
            DrawPainter();

            if (_lineVerts.Count == 0) return;
            _gl.DepthMask(false);
            _gl.BindVertexArray(_lineVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
            var array = _lineVerts.ToArray();
            fixed (float* p = array)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(array.Length * sizeof(float)), p, BufferUsageARB.DynamicDraw);
            SetMvp(viewProjection);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(array.Length / 7));
            _gl.DepthMask(true);
        }

        void WorldSegment(Vector3 a, Vector3 b, Color c, float alpha)
        {
            Push(_lineVerts, a, c.r, c.g, c.b, alpha);
            Push(_lineVerts, b, c.r, c.g, c.b, alpha);
        }

        /// <summary>SnowChanger shard transforms as faint drifting motes (tiny diamonds).</summary>
        void DrawCytoplasmMotes()
        {
            var tint = new Color(0.55f, 0.75f, 0.95f);
            foreach (var root in GameLoop.Current.Scene.GetRootGameObjects())
            {
                if (!root || !root.activeSelf) continue;
                var snow = root.GetComponent<SnowChanger>();
                if (snow == null) continue;
                foreach (Transform shard in snow.transform)
                {
                    var p = shard.position;
                    float s = Mathf.Clamp(shard.localScale.z * 0.35f, 0.8f, 5f);
                    var f = shard.forward * s;
                    var r = shard.right * (s * 0.35f);
                    WorldSegment(p - f, p + r, tint, 0.16f);
                    WorldSegment(p + r, p + f, tint, 0.16f);
                    WorldSegment(p + f, p - r, tint, 0.16f);
                    WorldSegment(p - r, p - f, tint, 0.16f);
                }
            }
        }

        /// <summary>Every live toy: double ring at the body radius in the body's tint +
        /// billboarded vector-font label above (the TMP label data drawn as lines).
        /// Ring radius follows the toy root's localScale — the bloom-in made visible.</summary>
        void DrawToys()
        {
            if (!_director.Toybox || !_director.Toybox.gameObject.activeSelf) return;
            var toys = _director.Toybox.gameObject.GetComponentsInChildren<Toy>(true);
            foreach (var toy in toys)
            {
                if (!toy) continue;
                var root = toy.transform;
                float bloom = Mathf.Clamp01(root.localScale.x);
                if (bloom <= 0.01f) continue;

                var bodyRenderer = toy.gameObject.GetComponentInChildren<MeshRenderer>(true);
                var label = toy.gameObject.GetComponentInChildren<TMP_Text>(true);
                Color tint = bodyRenderer != null && bodyRenderer.sharedMaterial != null
                    ? bodyRenderer.sharedMaterial.color
                    : (label != null ? label.color : Color.white);

                // body radius: the sphere child's world scale (diameter) / 2, bloomed
                float radius = 22f * bloom;
                var sphere = toy.gameObject.GetComponentInChildren<SphereCollider>(true);
                if (bodyRenderer != null)
                    radius = bodyRenderer.transform.lossyScale.x * 0.5f;
                else if (sphere != null)
                    radius = Math.Min(radius, sphere.radius * bloom);

                DrawBillboardRing(root.position, radius, tint, 0.85f);
                DrawBillboardRing(root.position, radius * 0.7f, tint, 0.4f);

                if (label != null && !string.IsNullOrEmpty(label.text))
                    DrawWorldText(label.text.ToUpperInvariant(), root.position + Vector3.up * (radius * 1.6f),
                        radius * 0.5f, label.color, 0.95f * bloom);
            }
        }

        void DrawBillboardRing(Vector3 center, float radius, Color tint, float alpha)
        {
            var toCam = (_camPos - center).normalized;
            var right = Vector3.Cross(Vector3.up, toCam);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            right = right.normalized;
            var up = Vector3.Cross(toCam, right).normalized;
            const int segments = 28;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments * MathF.Tau;
                float t1 = (i + 1) / (float)segments * MathF.Tau;
                WorldSegment(center + (right * MathF.Cos(t0) + up * MathF.Sin(t0)) * radius,
                             center + (right * MathF.Cos(t1) + up * MathF.Sin(t1)) * radius, tint, alpha);
            }
        }

        /// <summary>The painting toy's live guide state: ghost outline + guide line read
        /// straight from the runner's LineRenderers, plus the next-waypoint marker.</summary>
        void DrawPainter()
        {
            var painter = EngineObject.FindFirstObjectByType<MenuShapePainter>();
            if (painter == null || !painter.gameObject.activeSelf) return;

            foreach (var lr in painter.gameObject.GetComponentsInChildren<LineRenderer>(true))
            {
                if (lr == null || !lr.enabled || lr.positionCount < 2) continue;
                var c = lr.startColor;
                for (int i = 0; i < lr.positionCount - 1; i++)
                    WorldSegment(lr.GetPosition(i), lr.GetPosition(i + 1), c, Math.Max(0.15f, c.a));
            }

            // the lit marker at the next waypoint (the "number" you fly to)
            foreach (Transform child in painter.transform)
            {
                if (child.gameObject.name != "Waypoint") continue;
                var renderer = child.gameObject.GetComponentInChildren<MeshRenderer>(true);
                var tint = renderer != null && renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.color : Color.white;
                float pulse = 1f + 0.2f * Mathf.Sin(Time.time * 5f);
                DrawBillboardRing(child.position, 8f * pulse, tint, 0.95f);
            }
        }

        void DrawVessel(Matrix4x4 viewProjection)
        {
            var status = _director.VesselStatus;
            if (status == null) return; // mid-swap window
            var vesselGo = ((VesselController)status.Vessel).gameObject;
            if (!vesselGo.activeSelf) return;

            var t3 = status.Transform;
            var mesh = GetHullMesh(status.VesselType, _director.CurrentDomain);
            var model = Matrix4x4.CreateFromQuaternion(t3.rotation) * Matrix4x4.CreateTranslation(t3.position);
            SetMvp(model * viewProjection);
            _gl.BindVertexArray(mesh.vao);
            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)mesh.count);
        }

        /// <summary>Billboarded vector-font text in world space (toy labels).</summary>
        void DrawWorldText(string text, Vector3 center, float size, Color color, float alpha)
        {
            var toCam = (_camPos - center).normalized;
            var right = Vector3.Cross(Vector3.up, toCam);
            if (right.sqrMagnitude < 1e-4f) right = Vector3.right;
            right = right.normalized;
            var up = Vector3.Cross(toCam, right).normalized;

            float advance = size * 0.95f;
            float totalWidth = advance * text.Length;
            var origin = center - right * (totalWidth * 0.5f);
            for (int i = 0; i < text.Length; i++)
            {
                foreach (var (a, b) in VectorFont.Strokes(text[i]))
                {
                    var pa = origin + right * ((i * advance) + a.X * size) + up * (a.Y * size);
                    var pb = origin + right * ((i * advance) + b.X * size) + up * (b.Y * size);
                    WorldSegment(pa, pb, color, alpha);
                }
            }
        }

        // ── HUD (ortho pass — vector font + bars) ────────────────────

        unsafe void DrawHud()
        {
            var data = new List<float>();
            float w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;

            void Segment(float x, float y, float x2, float y2, Color c, float a)
            {
                Push(data, new Vector3(x, y, 0f), c.r, c.g, c.b, a);
                Push(data, new Vector3(x2, y2, 0f), c.r, c.g, c.b, a);
            }

            void Text(string text, float x, float y, float size, Color c, float a)
            {
                float advance = size * 0.95f;
                for (int i = 0; i < text.Length; i++)
                    foreach (var (p, q) in VectorFont.Strokes(text[i]))
                        Segment(x + i * advance + p.X * size, y + p.Y * size,
                                x + i * advance + q.X * size, y + q.Y * size, c, a);
            }

            var cyan = new Color(0.5f, 0.95f, 1f);
            var magenta = new Color(1f, 0.3f, 0.9f);
            var domainColor = DomainUIColor(_director.CurrentDomain);

            // mode banner top center: the lava-lamp ↔ freestyle state + the toggle hint
            if (_director.IsFreestyle)
                Text("FREESTYLE", w * 0.5f - 9f * 0.95f * 14f * 0.5f, h - 52f, 14f, magenta, 0.95f);
            else
            {
                Text("LAVA LAMP", w * 0.5f - 9f * 0.95f * 14f * 0.5f, h - 52f, 14f, cyan, 0.9f);
                Text("TAB TO FLY", w * 0.5f - 10f * 0.95f * 9f * 0.5f, h - 78f, 9f, cyan, 0.55f);
            }

            // top-left: vessel class + domain (in the live domain color — the theme read)
            Text(_director.CurrentVesselClass.ToString().ToUpperInvariant(), 34f, h - 52f, 12f, Color.white, 0.9f);
            Text(_director.CurrentDomain.ToString().ToUpperInvariant(), 34f, h - 80f, 12f, domainColor, 0.95f);

            // top-right: the living census (flora / fauna / prisms)
            Text($"FLORA {_director.FloraCount}", w - 220f, h - 44f, 9f, new Color(0.3f, 1f, 0.5f), 0.8f);
            Text($"FAUNA {_director.FaunaCount}", w - 220f, h - 66f, 9f, new Color(1f, 0.6f, 0.3f), 0.8f);
            Text($"PRISMS {_director.TrailPrismCount + _director.LifeFormPrismCount}", w - 220f, h - 88f, 9f, cyan, 0.8f);

            // energy bar bottom center (the rig's real ResourceSystem)
            var status = _director.VesselStatus;
            if (status != null && status.ResourceSystem != null && status.ResourceSystem.Resources.Count > 0)
            {
                float energy = status.ResourceSystem.Resources[0].CurrentAmount;
                float barWidth = w * 0.3f, bx = (w - barWidth) * 0.5f;
                Segment(bx, 36f, bx + barWidth, 36f, new Color(0.2f, 0.4f, 0.6f), 0.35f);
                var fill = _director.IsSkimming ? new Color(0.75f, 1f, 1f) : new Color(0.2f, 0.9f, 1f);
                for (int i = 0; i < 3; i++)
                    Segment(bx, 33f + i * 3f, bx + barWidth * energy, 33f + i * 3f, fill, _director.IsSkimming ? 1f : 0.85f);

                float drift = _playerStatus != null
                    ? Mathf.Clamp01(_playerStatus.LeftTriggerAnalog + _playerStatus.RightTriggerAnalog) : 0f;
                if (_director.IsFreestyle && drift > 0.02f)
                    Segment(bx, 44f, bx + barWidth * drift, 44f, magenta, 0.9f);
            }

            if (data.Count == 0) return;
            var ortho = Matrix4x4.CreateOrthographicOffCenter(0f, w, 0f, h, -1f, 1f);
            _gl.Disable(EnableCap.DepthTest);
            _gl.BindVertexArray(_hudVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _hudVbo);
            var array = data.ToArray();
            fixed (float* p = array)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(array.Length * sizeof(float)), p, BufferUsageARB.DynamicDraw);
            SetMvp(ortho);
            _gl.LineWidth(2f);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(array.Length / 7));
            _gl.Enable(EnableCap.DepthTest);
        }

        // ── post-chain plumbing (SkimRace idiom) ─────────────────────

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

        unsafe void SetMvp(Matrix4x4 mvp) => _gl.UniformMatrix4(_uMvp, 1, false, (float*)&mvp);

        unsafe void CaptureScreenshot()
        {
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            var pixels = new byte[w * h * 4];
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            MiniPng.Write(_screenshotPath, pixels, w, h);

            int trail = _director.TrailPrismCount;
            int lifeform = _director.LifeFormPrismCount;
            Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, mode freestyle, " +
                $"flora {_director.FloraCount}, fauna {_director.FaunaCount}, " +
                $"prisms {trail + lifeform} (trail {trail} + lifeform {lifeform}), toys {_director.CountToys()}, " +
                $"vessel {_director.CurrentVesselClass}, domain {_director.CurrentDomain}, " +
                $"autopilot {_director.AutopilotEnabled}");
        }
    }

    /// <summary>
    /// Minimal 16-segment vector font (A-Z, 0-9, space) for the freestyle window's
    /// GL-line labels/HUD — the client's existing HUD text renderer only knew
    /// seven-segment digits; toy labels ("RUBY", "DOLPHIN") need letters. Strokes are
    /// (x, y) pairs in a 0.7 × 1.0 glyph cell.
    /// </summary>
    static class VectorFont
    {
        const float W = 0.7f, H = 1.0f;

        // 16-segment bits
        const int A1 = 1 << 0, A2 = 1 << 1, B = 1 << 2, C = 1 << 3, D1 = 1 << 4, D2 = 1 << 5,
                  E = 1 << 6, F = 1 << 7, G1 = 1 << 8, G2 = 1 << 9, HH = 1 << 10, I = 1 << 11,
                  J = 1 << 12, K = 1 << 13, L = 1 << 14, M = 1 << 15;

        static readonly (System.Numerics.Vector2 a, System.Numerics.Vector2 b)[] SegmentGeometry =
        {
            (new(0, H), new(W / 2, H)),         // A1 top-left
            (new(W / 2, H), new(W, H)),         // A2 top-right
            (new(W, H), new(W, H / 2)),         // B  right-top
            (new(W, H / 2), new(W, 0)),         // C  right-bottom
            (new(0, 0), new(W / 2, 0)),         // D1 bottom-left
            (new(W / 2, 0), new(W, 0)),         // D2 bottom-right
            (new(0, H / 2), new(0, 0)),         // E  left-bottom
            (new(0, H), new(0, H / 2)),         // F  left-top
            (new(0, H / 2), new(W / 2, H / 2)), // G1 mid-left
            (new(W / 2, H / 2), new(W, H / 2)), // G2 mid-right
            (new(0, H), new(W / 2, H / 2)),     // H  diag top-left
            (new(W / 2, H), new(W / 2, H / 2)), // I  mid-top vert
            (new(W, H), new(W / 2, H / 2)),     // J  diag top-right
            (new(W / 2, H / 2), new(0, 0)),     // K  diag bottom-left
            (new(W / 2, H / 2), new(W / 2, 0)), // L  mid-bottom vert
            (new(W / 2, H / 2), new(W, 0)),     // M  diag bottom-right
        };

        static readonly Dictionary<char, int> Masks = new()
        {
            ['A'] = A1 | A2 | B | C | E | F | G1 | G2,
            ['B'] = A1 | A2 | B | C | D1 | D2 | G2 | I | L,
            ['C'] = A1 | A2 | D1 | D2 | E | F,
            ['D'] = A1 | A2 | B | C | D1 | D2 | I | L,
            ['E'] = A1 | A2 | D1 | D2 | E | F | G1,
            ['F'] = A1 | A2 | E | F | G1,
            ['G'] = A1 | A2 | C | D1 | D2 | E | F | G2,
            ['H'] = B | C | E | F | G1 | G2,
            ['I'] = A1 | A2 | D1 | D2 | I | L,
            ['J'] = B | C | D1 | D2 | E,
            ['K'] = E | F | G1 | J | M,
            ['L'] = D1 | D2 | E | F,
            ['M'] = B | C | E | F | HH | J,
            ['N'] = B | C | E | F | HH | M,
            ['O'] = A1 | A2 | B | C | D1 | D2 | E | F,
            ['P'] = A1 | A2 | B | E | F | G1 | G2,
            ['Q'] = A1 | A2 | B | C | D1 | D2 | E | F | M,
            ['R'] = A1 | A2 | B | E | F | G1 | G2 | M,
            ['S'] = A1 | A2 | C | D1 | D2 | F | G1 | G2,
            ['T'] = A1 | A2 | I | L,
            ['U'] = B | C | D1 | D2 | E | F,
            ['V'] = E | F | K | J,
            ['W'] = B | C | E | F | K | M,
            ['X'] = HH | J | K | M,
            ['Y'] = HH | J | L,
            ['Z'] = A1 | A2 | D1 | D2 | J | K,
            ['0'] = A1 | A2 | B | C | D1 | D2 | E | F,
            ['1'] = B | C,
            ['2'] = A1 | A2 | B | D1 | D2 | E | G1 | G2,
            ['3'] = A1 | A2 | B | C | D1 | D2 | G1 | G2,
            ['4'] = B | C | F | G1 | G2,
            ['5'] = A1 | A2 | C | D1 | D2 | F | G1 | G2,
            ['6'] = A1 | A2 | C | D1 | D2 | E | F | G1 | G2,
            ['7'] = A1 | A2 | B | C,
            ['8'] = A1 | A2 | B | C | D1 | D2 | E | F | G1 | G2,
            ['9'] = A1 | A2 | B | C | D1 | D2 | F | G1 | G2,
        };

        public static IEnumerable<(System.Numerics.Vector2 a, System.Numerics.Vector2 b)> Strokes(char c)
        {
            c = char.ToUpperInvariant(c);
            if (!Masks.TryGetValue(c, out int mask)) yield break; // space/unknown → blank
            for (int bit = 0; bit < SegmentGeometry.Length; bit++)
                if ((mask & (1 << bit)) != 0)
                    yield return SegmentGeometry[bit];
        }
    }
}
