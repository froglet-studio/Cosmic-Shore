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
using EngineInput = CosmicShore.Engine.InputSystem;
using Vector3 = CosmicShore.Engine.Vector3;
using Quaternion = CosmicShore.Engine.Quaternion;
using Vector2 = CosmicShore.Engine.Vector2;

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
        readonly string _screenshotPath;
        readonly int _screenshotFrame;

        IWindow _window;
        GL _gl;
        IInputContext _inputContext;

        GameLoop _loop;
        SkimRaceController _race;
        SkimRaceController _rival;
        readonly SkimInputStatus _playerStatus = new();
        readonly GamepadInputStrategy _gamepadStrategy = new(); // the ported, authentic dual-stick scheme
        EngineInput.Gamepad _shimPad;
        bool _prevA, _prevStart;

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
        uint _crystalVao, _crystalVbo; int _crystalVertexCount;
        uint _vesselVao, _vesselVbo; int _vesselVertexCount;
        uint _rivalVao, _rivalVbo; int _rivalVertexCount;
        uint _trailVao, _trailVbo;
        uint _rivalTrailVao, _rivalTrailVbo;
        uint _hudVao, _hudVbo;

        readonly List<(Vector3 pos, Vector3 right)> _trail = new();
        readonly List<(Vector3 pos, Vector3 right)> _rivalTrail = new();
        const int TrailMax = 110;

        Vector3 _camPos, _camLook;
        AudioEngine _audio;
        int _lastCountdownSecond = -1;
        RaceState _lastRaceState = RaceState.Countdown;
        readonly List<(Vector3 pos, float age)> _bursts = new();
        int _frameIndex;

        public RaceWindow(int seed, int crystalTarget, string screenshotPath, int screenshotFrame)
        {
            _seed = seed;
            _crystalTarget = crystalTarget;
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
        }

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
                    if (key == Key.R)
                    {
                        SkimRaceFactory.ResetRace(_race.Shared, _race, _rival);
                        _trail.Clear();
                        _rivalTrail.Clear();
                    }
                };

            (_loop, _race, _rival) = SkimRaceFactory.Create(_seed, _crystalTarget, _playerStatus);
            _gamepadStrategy.Initialize(_playerStatus);
            _gamepadStrategy.OnStrategyActivated();
            _audio = new AudioEngine(disabled: _screenshotPath != null);
            _race.OnCrystalCollected += (_, pos) => { _bursts.Add((pos, 0f)); _audio.CrystalChime(player: true); };
            _rival.OnCrystalCollected += (_, pos) => { _bursts.Add((pos, 0f)); _audio.CrystalChime(player: false); };

            _program = CompileProgram();
            _uMvp = _gl.GetUniformLocation(_program, "uMvp");

            BuildStars();
            BuildTrack();
            BuildCrystalMesh();
            BuildVesselMesh();
            _trailVao = _gl.GenVertexArray();
            _trailVbo = _gl.GenBuffer();
            ConfigureDynamicVao(_trailVao, _trailVbo);
            _rivalTrailVao = _gl.GenVertexArray();
            _rivalTrailVbo = _gl.GenBuffer();
            ConfigureDynamicVao(_rivalTrailVao, _rivalTrailVbo);
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

            _camPos = _race.transform.position - new Vector3(0f, -2.5f, 9f);
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
            _gl.BindVertexArray(vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(TrailMax * 2 * 7 * sizeof(float) * 4), null, BufferUsageARB.DynamicDraw);
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
                // shell of stars around the course volume
                var dir = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 2f - 1f).normalized;
                float radius = 380f + (float)rng.NextDouble() * 700f;
                var p = dir * radius + new Vector3(0f, 0f, SkimTrack.Length * 0.5f);
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
            // twin neon guide rails offset left/right of the centerline
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < track.Centerline.Count - 1; i++)
                {
                    var a = track.Centerline[i] + new Vector3(7.5f * side, -2.5f, 0f);
                    var b = track.Centerline[i + 1] + new Vector3(7.5f * side, -2.5f, 0f);
                    float pulse = 0.55f + 0.25f * Mathf.Sin(i * 0.22f);
                    Push(rails, a, 0.05f, 0.85f * pulse, 1f * pulse, 0.85f);
                    Push(rails, b, 0.05f, 0.85f * pulse, 1f * pulse, 0.85f);
                }
            }
            _railCount = rails.Count / 7;
            (_railVao, _railVbo) = UploadStatic(rails.ToArray());

            // magenta gate rings every ~84u
            var rings = new List<float>();
            for (float z = 60f; z < SkimTrack.Length; z += 84f)
            {
                var center = track.PointAt(z);
                const int segments = 26;
                for (int i = 0; i < segments; i++)
                {
                    float t0 = i / (float)segments * MathF.Tau;
                    float t1 = (i + 1) / (float)segments * MathF.Tau;
                    var a = center + new Vector3(MathF.Cos(t0) * 11f, MathF.Sin(t0) * 11f, 0f);
                    var b = center + new Vector3(MathF.Cos(t1) * 11f, MathF.Sin(t1) * 11f, 0f);
                    Push(rings, a, 1f, 0.25f, 0.85f, 0.5f);
                    Push(rings, b, 1f, 0.25f, 0.85f, 0.5f);
                }
            }
            _ringCount = rings.Count / 7;
            (_ringVao, _ringVbo) = UploadStatic(rings.ToArray());
        }

        void BuildCrystalMesh()
        {
            // unit octahedron, golden with a white-hot core face mix
            var top = new Vector3(0f, 1.2f, 0f);
            var bottom = new Vector3(0f, -1.2f, 0f);
            var equator = new[]
            {
                new Vector3(0.9f, 0f, 0f), new Vector3(0f, 0f, 0.9f),
                new Vector3(-0.9f, 0f, 0f), new Vector3(0f, 0f, -0.9f),
            };
            var data = new List<float>();
            for (int i = 0; i < 4; i++)
            {
                var a = equator[i];
                var b = equator[(i + 1) % 4];
                float warm = i % 2 == 0 ? 1f : 0.86f;
                Push(data, top, 1f * warm, 0.78f * warm, 0.18f, 0.95f);
                Push(data, a, 1f, 0.9f, 0.45f, 0.8f);
                Push(data, b, 0.95f, 0.7f, 0.12f, 0.8f);
                Push(data, bottom, 0.9f * warm, 0.6f * warm, 0.1f, 0.95f);
                Push(data, b, 0.95f, 0.7f, 0.12f, 0.8f);
                Push(data, a, 1f, 0.9f, 0.45f, 0.8f);
            }
            _crystalVertexCount = data.Count / 7;
            (_crystalVao, _crystalVbo) = UploadStatic(data.ToArray());
        }

        void BuildVesselMesh()
        {
            (_vesselVao, _vesselVbo, _vesselVertexCount) = BuildDart(jade: true);
            (_rivalVao, _rivalVbo, _rivalVertexCount) = BuildDart(jade: false);
        }

        (uint vao, uint vbo, int count) BuildDart(bool jade)
        {
            // jade dart: nose, twin swept wings, tail fin — flat-shaded neon
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
            // hue swap: jade player vs ruby rival
            void Hull(Vector3 a, Vector3 b, Vector3 c, float bright, float alpha)
            {
                if (jade) Tri(a, b, c, 0.06f * bright, 1f * bright, 0.6f * bright, alpha);
                else Tri(a, b, c, 1f * bright, 0.16f * bright, 0.3f * bright, alpha);
            }
            Hull(nose, left, tail, 1f, 0.95f);
            Hull(nose, tail, right, 0.88f, 0.95f);
            Hull(nose, belly, left, 0.55f, 0.9f);
            Hull(nose, right, belly, 0.5f, 0.9f);
            Hull(tail, left, belly, 0.4f, 0.9f);
            Hull(tail, belly, right, 0.38f, 0.9f);
            Tri(tail, fin, nose, jade ? 0.65f : 1f, jade ? 1f : 0.7f, jade ? 0.9f : 0.75f, 0.75f);
            var (vao, vbo) = UploadStatic(data.ToArray());
            return (vao, vbo, data.Count / 7);
        }

        // ── per-frame ────────────────────────────────────────────────

        void OnUpdate(double dt)
        {
            if (_screenshotPath != null) return; // sim + autopilot tick in OnRender

            ApplyHumanInput();

            if (_screenshotPath == null)
                _loop.Tick((float)dt); // the ported engine drives the sim (real time)

            EmitTrailAndCamera((float)dt);

        }

        /// <summary>
        /// Feed Silk.NET devices into the engine input shim, then run the PORTED
        /// GamepadInputStrategy — the authentic Cosmic Shore dual-stick scheme:
        /// XSum=yaw, YSum=pitch, YDiff=roll, XDiff=stick-spread throttle. Keyboard
        /// fallback writes the same fields (WASD=left stick, arrows=right stick).
        /// </summary>
        void ApplyHumanInput()
        {
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
                _prevA = a;
                if (start && !_prevStart)
                {
                    SkimRaceFactory.ResetRace(_race.Shared, _race, _rival);
                    _trail.Clear();
                    _rivalTrail.Clear();
                }
                _prevStart = start;

                _gamepadStrategy.ProcessInput(); // the real scheme computes sums/diffs
                return;
            }

            // Keyboard fallback in the same idiom: WASD = left stick, arrows = right.
            float lx = 0f, ly = 0f, rx = 0f, ry = 0f;
            bool space = false;
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
            }
            // No second stick touched → mirror the left so single-hand play still flies.
            if (rx == 0f && ry == 0f && (lx != 0f || ly != 0f)) { rx = lx; ry = ly; }
            _playerStatus.XSum = Mathf.Clamp(rx + lx, -1f, 1f);
            _playerStatus.YSum = Mathf.Clamp(-(ry + ly), -1f, 1f);
            _playerStatus.YDiff = Mathf.Clamp(ry - ly, -1f, 1f);
            _playerStatus.XDiff = (Mathf.Clamp(rx - lx, -2f, 2f) + 2f) / 4f + (space ? 0.5f : 0.12f);
            _playerStatus.XDiff = Mathf.Clamp01(_playerStatus.XDiff);
            _race.BoostHeld = space;
        }

        void EmitTrailAndCamera(float dt)
        {
            var t3 = _race.transform;
            EmitFor(_race, _trail);
            EmitFor(_rival, _rivalTrail);

            static void EmitFor(SkimRaceController pilot, List<(Vector3 pos, Vector3 right)> buffer)
            {
                if (pilot.State == RaceState.Countdown || pilot.Speed <= 4f) return;
                var t = pilot.transform;
                var emit = t.position - t.forward * 1.6f - t.up * 0.6f;
                if (buffer.Count == 0 || (emit - buffer[^1].pos).sqrMagnitude > 0.2f)
                {
                    buffer.Add((emit, t.right));
                    if (buffer.Count > TrailMax) buffer.RemoveAt(0);
                }
            }

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

            // audio: engine bed + countdown/finish stingers
            _audio.SetEngineState(Mathf.Clamp01(_race.Speed / 115f), _race.BoostHeld && _race.State == RaceState.Racing);
            if (_race.State == RaceState.Countdown)
            {
                int second = (int)MathF.Ceiling(_race.Countdown);
                if (second != _lastCountdownSecond) { _audio.CountdownBeep(); _lastCountdownSecond = second; }
            }
            if (_race.State != _lastRaceState)
            {
                if (_race.State == RaceState.Racing) _audio.GoBeep();
                else if (_race.State == RaceState.Finished)
                    _audio.FinishJingle(won: _race.Shared.WinnerPilot == _race.PilotId);
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
                EmitTrailAndCamera(1f / 60f);
            }
            EnsureRenderTargets();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _sceneFbo);
            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
            _gl.ClearColor(0.012f, 0.0f, 0.045f, 1f); // deep space indigo
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
            _gl.UseProgram(_program);

            float aspect = _window.FramebufferSize.X / (float)Math.Max(1, _window.FramebufferSize.Y);
            var view = Matrix4x4.CreateLookAt(_camPos, _camLook, _race.transform.up);
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(70f * MathF.PI / 180f, aspect, 0.1f, 2200f);
            var viewProjection = view * projection;

            SetMvp(viewProjection);
            _gl.BindVertexArray(_starVao);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_starCount);
            _gl.BindVertexArray(_railVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_railCount);
            _gl.BindVertexArray(_ringVao);
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_ringCount);

            // crystals: spin/pulse via model matrix; collected ones skipped
            var track = _race.Track;
            for (int i = 0; i < track.Crystals.Count; i++)
            {
                if (_race.Shared.IsTaken(i)) continue;
                float spin = Time.time * 1.6f + i * 0.7f;
                float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 3f + i);
                var model = Matrix4x4.CreateScale(pulse) * Matrix4x4.CreateRotationY(spin) *
                            Matrix4x4.CreateTranslation(track.Crystals[i]);
                SetMvp(model * viewProjection);
                _gl.BindVertexArray(_crystalVao);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_crystalVertexCount);
            }

            // collection bursts: expanding fading octahedra
            foreach (var (pos, age) in _bursts)
            {
                // bursts spawn on the camera's own path — cull when too close or they
                // bloom into screen-filling wedges
                if ((pos - _camPos).magnitude < 7f) continue;
                float scale = 1f + age * 7f;
                var model = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateTranslation(pos);
                SetMvp(model * viewProjection);
                _gl.BindVertexArray(_crystalVao);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_crystalVertexCount);
            }

            // vessel: oriented + banked
            {
                var t3 = _race.transform;
                var rotation = Matrix4x4.CreateFromQuaternion(t3.rotation) ;
                var bank = Matrix4x4.CreateFromAxisAngle(t3.forward, _race.BankAngle * MathF.PI / 180f);
                var model = rotation * bank * Matrix4x4.CreateTranslation(t3.position);
                SetMvp(model * viewProjection);
                _gl.BindVertexArray(_vesselVao);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_vesselVertexCount);
            }

            {
                var tr = _rival.transform;
                var rotation = Matrix4x4.CreateFromQuaternion(tr.rotation);
                var bank = Matrix4x4.CreateFromAxisAngle(tr.forward, _rival.BankAngle * MathF.PI / 180f);
                var model = rotation * bank * Matrix4x4.CreateTranslation(tr.position);
                SetMvp(model * viewProjection);
                _gl.BindVertexArray(_rivalVao);
                _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)_rivalVertexCount);
            }

            DrawTrail(viewProjection);
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
            if (_race.State == RaceState.Countdown) { _race.Countdown = 0.01f; _rival.Countdown = 0.01f; }
            var t0 = _race.transform;
            Vector3? target = null;
            float best = float.MaxValue;
            for (int i = 0; i < _race.Track.Crystals.Count; i++)
            {
                if (_race.Shared.IsTaken(i)) continue;
                var c = _race.Track.Crystals[i];
                float ahead = c.z - t0.position.z;
                if (ahead < -5f) continue;
                if (ahead < best) { best = ahead; target = c; }
            }
            if (target.HasValue)
            {
                var local = Quaternion.Inverse(t0.rotation) * (target.Value - t0.position);
                _playerStatus.XSum = Mathf.Clamp(local.x * 0.25f, -1f, 1f);
                _playerStatus.YSum = Mathf.Clamp(-local.y * 0.25f, -1f, 1f);
            }
            _playerStatus.XDiff = _frameIndex > 60 ? 1f : 0.62f;
            _race.BoostHeld = _frameIndex > 60 && _race.Resources.Resources[0].CurrentAmount > 0.2f;
        }

        unsafe void SetMvp(Matrix4x4 mvp) => _gl.UniformMatrix4(_uMvp, 1, false, (float*)&mvp);

        unsafe void DrawTrail(Matrix4x4 viewProjection)
        {
            DrawRibbon(viewProjection, _trail, _trailVao, _trailVbo, _race.BoostHeld, jade: true);
            DrawRibbon(viewProjection, _rivalTrail, _rivalTrailVao, _rivalTrailVbo, false, jade: false);
        }

        unsafe void DrawRibbon(Matrix4x4 viewProjection, List<(Vector3 pos, Vector3 right)> trail,
            uint vao, uint vbo, bool boosting, bool jade)
        {
            if (trail.Count < 2) return;
            var data = new List<float>(trail.Count * 14);
            for (int i = 0; i < trail.Count; i++)
            {
                float age = i / (float)(trail.Count - 1);        // 0 old → 1 fresh
                float width = 0.08f + age * (boosting ? 0.55f : 0.32f);
                float alpha = age * age * 0.65f;
                // newest points ramp in so the chase cam never sits inside the ribbon
                int fromNewest = trail.Count - 1 - i;
                if (fromNewest < 16) alpha *= fromNewest / 16f;
                // and ANY ribbon fades near the camera (rival trails crossing the
                // frustum otherwise bloom into giant wedges)
                float camDistance = (trail[i].pos - _camPos).magnitude;
                alpha *= Mathf.Clamp01((camDistance - 3f) / 7f);
                float r, g, b;
                if (jade) { r = 0.15f + (1f - age) * 0.8f; g = 0.35f + age * 0.55f; b = 1f; }
                else { r = 1f; g = 0.2f + age * 0.4f; b = 0.25f + (1f - age) * 0.5f; }
                var (pos, right) = trail[i];
                Push(data, pos - right * width, r, g, b, alpha);
                Push(data, pos + right * width, r, g, b, alpha);
            }
            _gl.DepthMask(false);
            _gl.BindVertexArray(vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            var array = data.ToArray();
            fixed (float* p = array)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)(array.Length * sizeof(float)), p);
            SetMvp(viewProjection);
            _gl.DrawArrays(PrimitiveType.TriangleStrip, 0, (uint)(array.Length / 7));
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

            // crystal count / target, top-left, golden — with a diamond glyph
            Segment(34f, h - 44f, 46f, h - 30f, 1f, 0.82f, 0.25f, 0.95f);
            Segment(46f, h - 30f, 58f, h - 44f, 1f, 0.82f, 0.25f, 0.95f);
            Segment(58f, h - 44f, 46f, h - 58f, 1f, 0.82f, 0.25f, 0.95f);
            Segment(46f, h - 58f, 34f, h - 44f, 1f, 0.82f, 0.25f, 0.95f);
            Number(_race.Stats.CrystalsCollected, 2, 72f, h - 58f, 26f, 0.3f, 1f, 0.7f, 0.95f);
            Segment(122f, h - 36f, 134f, h - 54f, 1f, 0.85f, 0.3f, 0.6f); // slash
            Number(_race.WinTarget, 2, 142f, h - 58f, 26f, 1f, 0.85f, 0.3f, 0.6f);
            // rival count, top-right, ruby
            Number(_rival.Stats.CrystalsCollected, 2, w - 120f, h - 58f, 26f, 1f, 0.25f, 0.35f, 0.95f);

            // boost bar bottom center (reads the ported ResourceSystem)
            float boost = _race.Resources.Resources[0].CurrentAmount;
            float barWidth = w * 0.3f, bx = (w - barWidth) * 0.5f;
            Segment(bx, 36f, bx + barWidth, 36f, 0.2f, 0.4f, 0.6f, 0.35f);
            for (int i = 0; i < 3; i++) // thick neon fill
                Segment(bx, 33f + i * 3f, bx + barWidth * boost, 33f + i * 3f, 0.2f, 0.9f, 1f, 0.85f);

            // countdown / finish banner: oversized center digits
            if (_race.State == RaceState.Countdown)
                Digit(Math.Max(1, (int)MathF.Ceiling(_race.Countdown)), w * 0.5f - 30f, h * 0.5f - 40f, 80f, 1f, 0.3f, 0.9f, 0.95f);
            else if (_race.State == RaceState.Finished)
            {
                // gold digits = you won the crystal majority; ruby = the rival did
                bool won = _race.Shared.WinnerPilot == _race.PilotId;
                (float fr, float fg, float fb) = won ? (1f, 0.85f, 0.3f) : (1f, 0.2f, 0.3f);
                float finalTime = won ? _race.Stats.Score : _rival.Stats.Score;
                int centiseconds = (int)(finalTime * 100f) % 100;
                float fs = 44f, fx = w * 0.5f - fs * 2.6f, fy = h * 0.5f;
                Number((int)finalTime / 60, 2, fx, fy, fs, fr, fg, fb, 1f);
                Number((int)finalTime % 60, 2, fx + fs * 2.0f, fy, fs, fr, fg, fb, 0.85f);
                Number(centiseconds, 2, fx + fs * 4.0f, fy, fs, fr, fg, fb, 0.7f);
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
            Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, " +
                $"crystals {_race.Stats.CrystalsCollected} vs rival {_rival.Stats.CrystalsCollected} (target {_race.WinTarget}), state {_race.State}");
        }
    }
}
