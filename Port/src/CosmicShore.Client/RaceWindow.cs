using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Engine;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Vector3 = CosmicShore.Engine.Vector3;
using Quaternion = CosmicShore.Engine.Quaternion;

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
        readonly PilotInput _pilot = new();

        uint _program;
        int _uMvp;

        // geometry
        uint _starVao, _starVbo; int _starCount;
        uint _railVao, _railVbo; int _railCount;
        uint _ringVao, _ringVbo; int _ringCount;
        uint _crystalVao, _crystalVbo; int _crystalVertexCount;
        uint _vesselVao, _vesselVbo; int _vesselVertexCount;
        uint _trailVao, _trailVbo;
        uint _hudVao, _hudVbo;

        readonly List<(Vector3 pos, Vector3 right)> _trail = new();
        const int TrailMax = 110;

        Vector3 _camPos, _camLook;
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
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
        }

        // ── setup ────────────────────────────────────────────────────

        void OnLoad()
        {
            _gl = GL.GetApi(_window);
            _inputContext = _window.CreateInput();
            foreach (var keyboard in _inputContext.Keyboards)
                keyboard.KeyDown += (_, key, _) =>
                {
                    if (key == Key.Escape) _window.Close();
                    if (key == Key.R) _pilot.Restart = true;
                };

            (_loop, _race) = SkimRaceFactory.Create(_seed, _crystalTarget, _pilot);
            _race.OnCrystalCollected += (_, pos) => _bursts.Add((pos, 0f));

            _program = CompileProgram();
            _uMvp = _gl.GetUniformLocation(_program, "uMvp");

            BuildStars();
            BuildTrack();
            BuildCrystalMesh();
            BuildVesselMesh();
            _trailVao = _gl.GenVertexArray();
            _trailVbo = _gl.GenBuffer();
            ConfigureDynamicVao(_trailVao, _trailVbo);
            _hudVao = _gl.GenVertexArray();
            _hudVbo = _gl.GenBuffer();
            ConfigureDynamicVao(_hudVao, _hudVbo);

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One); // additive neon
            _gl.Enable(EnableCap.DepthTest);
            _gl.Enable(EnableCap.ProgramPointSize);

            _camPos = _race.transform.position - new Vector3(0f, -2.5f, 9f);
        }

        uint CompileProgram()
        {
            const string vertexSrc = @"#version 330 core
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
            const string fragmentSrc = @"#version 330 core
in vec4 vColor;
out vec4 frag;
void main() { frag = vColor; }";

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
            Tri(nose, left, tail, 0.1f, 1f, 0.62f, 0.95f);      // jade top-left
            Tri(nose, tail, right, 0.08f, 0.85f, 0.55f, 0.95f); // jade top-right
            Tri(nose, belly, left, 0.05f, 0.55f, 0.4f, 0.9f);   // under-left
            Tri(nose, right, belly, 0.05f, 0.5f, 0.36f, 0.9f);  // under-right
            Tri(tail, left, belly, 0.04f, 0.4f, 0.3f, 0.9f);    // aft-left
            Tri(tail, belly, right, 0.04f, 0.38f, 0.28f, 0.9f); // aft-right
            Tri(tail, fin, nose, 0.65f, 1f, 0.9f, 0.75f);       // glow fin
            _vesselVertexCount = data.Count / 7;
            (_vesselVao, _vesselVbo) = UploadStatic(data.ToArray());
        }

        // ── per-frame ────────────────────────────────────────────────

        void OnUpdate(double dt)
        {
            // keyboard → pilot intent
            float pitch = 0f, yaw = 0f;
            bool boost = false;
            foreach (var keyboard in _inputContext.Keyboards)
            {
                if (keyboard.IsKeyPressed(Key.W) || keyboard.IsKeyPressed(Key.Up)) pitch += 1f;
                if (keyboard.IsKeyPressed(Key.S) || keyboard.IsKeyPressed(Key.Down)) pitch -= 1f;
                if (keyboard.IsKeyPressed(Key.A) || keyboard.IsKeyPressed(Key.Left)) yaw -= 1f;
                if (keyboard.IsKeyPressed(Key.D) || keyboard.IsKeyPressed(Key.Right)) yaw += 1f;
                if (keyboard.IsKeyPressed(Key.Space)) boost = true;
            }

            // headless screenshot mode: autopilot seeks the next crystal (a real racing
            // line — also the seed of the future AI opponent)
            if (_screenshotPath != null)
            {
                if (_race.State == RaceState.Countdown) _race.Countdown = 0.01f;
                var t0 = _race.transform;
                Vector3? target = null;
                float best = float.MaxValue;
                for (int i = 0; i < _race.Track.Crystals.Count; i++)
                {
                    if (_race.Collected.Contains(i)) continue;
                    var c = _race.Track.Crystals[i];
                    float ahead = c.z - t0.position.z;
                    if (ahead < -5f) continue;
                    if (ahead < best) { best = ahead; target = c; }
                }
                if (target.HasValue)
                {
                    var local = Quaternion.Inverse(t0.rotation) * (target.Value - t0.position);
                    yaw = Mathf.Clamp(local.x * 0.25f, -1f, 1f);
                    pitch = Mathf.Clamp(local.y * 0.25f, -1f, 1f);
                }
                boost = _frameIndex > 150;
            }

            _pilot.Pitch = pitch;
            _pilot.Yaw = yaw;
            _pilot.Boost = boost;

            if (_screenshotPath == null)
                _loop.Tick((float)dt); // the ported engine drives the sim (real time)

            EmitTrailAndCamera((float)dt);

        }

        void EmitTrailAndCamera(float dt)
        {
            var t3 = _race.transform;
            if (_race.State != RaceState.Countdown && _race.Speed > 4f)
            {
                var emit = t3.position - t3.forward * 1.6f - t3.up * 0.6f;
                if (_trail.Count == 0 || (emit - _trail[^1].pos).sqrMagnitude > 0.2f)
                {
                    _trail.Add((emit, t3.right));
                    if (_trail.Count > TrailMax) _trail.RemoveAt(0);
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
        }

        unsafe void OnRender(double dt)
        {
            _frameIndex++;
            if (_screenshotPath != null)
            {
                _loop.Tick(1f / 60f); // deterministic sim frames
                EmitTrailAndCamera(1f / 60f);
            }
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
                if (_race.Collected.Contains(i)) continue;
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

            DrawTrail(viewProjection);
            DrawHud();

            if (_screenshotPath != null && _frameIndex >= _screenshotFrame)
            {
                CaptureScreenshot();
                _window.Close();
            }
        }

        unsafe void SetMvp(Matrix4x4 mvp) => _gl.UniformMatrix4(_uMvp, 1, false, (float*)&mvp);

        unsafe void DrawTrail(Matrix4x4 viewProjection)
        {
            if (_trail.Count < 2) return;
            var data = new List<float>(_trail.Count * 14);
            bool boosting = _pilot.Boost;
            for (int i = 0; i < _trail.Count; i++)
            {
                float age = i / (float)(_trail.Count - 1);        // 0 old → 1 fresh
                float width = 0.08f + age * (boosting ? 0.55f : 0.32f);
                float alpha = age * age * 0.65f;
                // newest points ramp in so the chase cam never sits inside the ribbon
                int fromNewest = _trail.Count - 1 - i;
                if (fromNewest < 16) alpha *= fromNewest / 16f;
                // cyan → magenta along the ribbon, Cosmic Shore signature
                float r = 0.15f + (1f - age) * 0.8f;
                float g = 0.35f + age * 0.55f;
                float b = 1f;
                var (pos, right) = _trail[i];
                Push(data, pos - right * width, r, g, b, alpha);
                Push(data, pos + right * width, r, g, b, alpha);
            }
            _gl.DepthMask(false);
            _gl.BindVertexArray(_trailVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _trailVbo);
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
            Number(_race.Stats.CrystalsCollected, 2, 72f, h - 58f, 26f, 1f, 0.85f, 0.3f, 0.95f);
            Segment(122f, h - 36f, 134f, h - 54f, 1f, 0.85f, 0.3f, 0.6f); // slash
            Number(_race.CrystalTarget, 2, 142f, h - 58f, 26f, 1f, 0.85f, 0.3f, 0.6f);

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
                int centiseconds = (int)(_race.Stats.Score * 100f) % 100;
                float fs = 44f, fx = w * 0.5f - fs * 2.6f, fy = h * 0.5f;
                Number((int)_race.Stats.Score / 60, 2, fx, fy, fs, 1f, 0.85f, 0.3f, 1f);
                Number((int)_race.Stats.Score % 60, 2, fx + fs * 2.0f, fy, fs, 1f, 0.85f, 0.3f, 1f);
                Number(centiseconds, 2, fx + fs * 4.0f, fy, fs, 1f, 0.85f, 0.3f, 0.8f);
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
                $"crystals {_race.Stats.CrystalsCollected}/{_race.CrystalTarget}, state {_race.State}");
        }
    }
}
