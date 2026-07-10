using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Cli;
using CosmicShore.Data;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using EngineVector3 = CosmicShore.Engine.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace CosmicShore.Client
{
    /// <summary>
    /// Arc-G verification host (`--mode play`): the WINDOWED game-mode host. The exact
    /// same round the CLI proves headlessly — <see cref="HexRaceRound"/>'s verbatim
    /// world (Cell + course registry + AI field + real trigger/impactor claims +
    /// HexRaceScoringRuleSO) — stepped ONE engine frame per window update from the
    /// steppable <see cref="HexRaceRoundHandle"/> (the Arc-G Setup/Step/Finish split;
    /// the CLI's blocking Run is the same handle in a while-loop, transcript-identical).
    ///
    /// Rendering is a deterministic wireframe pass (line/point shader shared with the
    /// RaceWindow idiom): seeded starfield, upcoming course crosses, the ACTIVE crystal
    /// as a spinning element-tinted octahedron, each AI vessel as a domain-tinted
    /// arrow, plus a UiRenderer HUD — domain sums, per-pilot crystals, and the final
    /// standings block once the objective lands. Fixed 1/60 stepping + sim-derived
    /// camera → `--screenshot` captures are byte-identical across runs.
    ///
    /// This host proves Arc G's "construction" criterion: a real mode world stands up,
    /// steps, scores, and renders in a window. The menu→game→menu handoff (reacting to
    /// GameDataSO.OnLaunchGame from the menushell world) is Arc I's loop.
    /// </summary>
    public sealed class ModeHostWindow
    {
        readonly int _seed;
        readonly int _playerCount;
        readonly int _crystalTarget;
        readonly string _screenshotPath;
        readonly int _screenshotFrame;

        IWindow _window;
        GL _gl;
        UiRenderer _ui;
        HexRaceRoundHandle _handle;

        uint _program;
        int _uMvp;
        uint _starVao, _starVbo;
        int _starCount;
        uint _lineVao, _lineVbo;
        readonly List<float> _lineData = new();

        int _frameIndex;
        bool _raceDone;

        public ModeHostWindow(int seed, int playerCount, int crystalTarget,
            string screenshotPath, int screenshotFrame)
        {
            _seed = seed;
            _playerCount = playerCount;
            _crystalTarget = crystalTarget;
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
        }

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1280, 720),
                Title = "Cosmic Shore — HexRace mode host (port progress build)",
                VSync = true,
            };
            Console.WriteLine("[1/3] creating window (GLFW)...");
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
            _handle?.Dispose();
        }

        void OnLoad()
        {
            Console.WriteLine("[2/3] window open — initializing GL/round...");
            _gl = GL.GetApi(_window);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.Enable(EnableCap.ProgramPointSize);
            _ui = new UiRenderer(_gl);

            _program = CompileProgram();
            _uMvp = _gl.GetUniformLocation(_program, "uMvp");
            BuildStarfield();
            _lineVao = _gl.GenVertexArray();
            _lineVbo = _gl.GenBuffer();
            ConfigureVao(_lineVao, _lineVbo);

            // The REAL round world — same construction the CLI gate proves headless.
            _handle = HexRaceRound.Setup(new HexRaceRoundOptions
            {
                PlayerCount = _playerCount,
                Seed = _seed,
                CrystalTarget = _crystalTarget,
            }, line => Console.WriteLine("  " + line));

            Console.WriteLine($"[3/3] ready — HexRace, {_playerCount} AI pilots, first domain to {_crystalTarget}.");
        }

        void OnUpdate(double dt)
        {
            // ONE deterministic engine frame per window update (fixed 1/60 inside the
            // handle) — the windowed twin of the CLI's while-loop.
            if (!_raceDone && _handle != null)
            {
                if (_handle.StepFrame())
                {
                    _raceDone = true;
                    _handle.FinishAndScore();
                }
                else if (_handle.FramesStepped >= _handle.Options.MaxFrames)
                {
                    _raceDone = true;
                    _handle.CompleteStepping();
                }
            }
            _frameIndex++;
        }

        // ── render ─────────────────────────────────────────────────────────────

        unsafe void OnRender(double dt)
        {
            _gl.Viewport(0, 0, (uint)_window.FramebufferSize.X, (uint)_window.FramebufferSize.Y);
            _gl.ClearColor(0.012f, 0.0f, 0.045f, 1f); // deep space indigo
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            // UiRenderer.End hands back "sim" state (depth ON, additive blend) — this
            // wireframe pass owns its own: no depth (lines over cleared space), additive
            // neon on the dark clear.
            _gl.Disable(EnableCap.DepthTest);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

            if (_handle != null)
            {
                var viewProjection = BuildCamera();
                _gl.UseProgram(_program);
                SetMvp(viewProjection);

                _gl.BindVertexArray(_starVao);
                _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_starCount);

                BuildLineGeometry();
                UploadLines();
                _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(_lineData.Count / 7));

                DrawHud();
            }

            if (_screenshotPath != null && _frameIndex >= _screenshotFrame)
            {
                CaptureScreenshot();
                _window.Close();
            }
        }

        Matrix4x4 BuildCamera()
        {
            // Sim-derived chase camera (no wall-clock smoothing — deterministic):
            // behind AI-1, looking at the active crystal (or ahead of the vessel).
            var v0 = _handle.Players[0].Vessel.Transform;
            EngineVector3 eye = v0.position - v0.forward * 70f + EngineVector3.up * 26f;
            var crystal = _handle.ActiveCrystal;
            EngineVector3 look = crystal
                ? crystal.transform.position
                : v0.position + v0.forward * 120f;

            float aspect = _window.FramebufferSize.X / (float)Math.Max(1, _window.FramebufferSize.Y);
            var view = Matrix4x4.CreateLookAt(eye, look, new Vector3(0f, 1f, 0f));
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(65f * MathF.PI / 180f, aspect, 0.5f, 8000f);
            return view * projection;
        }

        void BuildLineGeometry()
        {
            _lineData.Clear();

            // Upcoming course waypoints — dim crosses fading with distance in the order.
            var course = _handle.Course;
            for (int i = _handle.CourseIndex + 1; i < course.Length && i <= _handle.CourseIndex + 12; i++)
            {
                float fade = 1f - (i - _handle.CourseIndex) / 14f;
                var c = new Vector4(0.45f, 0.5f, 0.85f, 0.55f * fade);
                AddCross(course[i], 6f, c);
            }

            // Active crystal — spinning element-tinted octahedron (spin angle derives
            // from the simulated frame count, so it is deterministic per frame).
            var crystal = _handle.ActiveCrystal;
            if (crystal)
            {
                float yaw = _handle.FramesStepped * (1f / 60f) * 1.1f;
                var element = _handle.CourseElements[_handle.CourseIndex];
                AddOctahedron(crystal.transform.position, 10f, yaw, ElementColor(element));
            }

            // The AI field — domain-tinted arrows built from each vessel's live pose.
            foreach (var player in _handle.Players)
            {
                var t = player.Vessel.Transform;
                var color = DomainColor(player.Domain);
                EngineVector3 nose = t.position + t.forward * 9f;
                EngineVector3 tailL = t.position - t.forward * 6f + t.right * 4.5f;
                EngineVector3 tailR = t.position - t.forward * 6f - t.right * 4.5f;
                EngineVector3 tailU = t.position - t.forward * 6f + t.up * 3f;
                AddLine(nose, tailL, color);
                AddLine(nose, tailR, color);
                AddLine(nose, tailU, color);
                AddLine(tailL, tailR, color);
                AddLine(tailL, tailU, color);
                AddLine(tailR, tailU, color);
            }
        }

        void AddLine(EngineVector3 a, EngineVector3 b, Vector4 color)
        {
            _lineData.Add(a.x); _lineData.Add(a.y); _lineData.Add(a.z);
            _lineData.Add(color.X); _lineData.Add(color.Y); _lineData.Add(color.Z); _lineData.Add(color.W);
            _lineData.Add(b.x); _lineData.Add(b.y); _lineData.Add(b.z);
            _lineData.Add(color.X); _lineData.Add(color.Y); _lineData.Add(color.Z); _lineData.Add(color.W);
        }

        void AddCross(EngineVector3 p, float s, Vector4 color)
        {
            AddLine(p + new EngineVector3(-s, 0, 0), p + new EngineVector3(s, 0, 0), color);
            AddLine(p + new EngineVector3(0, -s, 0), p + new EngineVector3(0, s, 0), color);
            AddLine(p + new EngineVector3(0, 0, -s), p + new EngineVector3(0, 0, s), color);
        }

        void AddOctahedron(EngineVector3 center, float s, float yaw, Vector4 color)
        {
            float cos = MathF.Cos(yaw), sin = MathF.Sin(yaw);
            EngineVector3 Spin(float x, float z) => new(x * cos - z * sin, 0f, x * sin + z * cos);

            var top = center + new EngineVector3(0, s, 0);
            var bottom = center + new EngineVector3(0, -s, 0);
            var e0 = center + Spin(s, 0);
            var e1 = center + Spin(0, s);
            var e2 = center + Spin(-s, 0);
            var e3 = center + Spin(0, -s);

            AddLine(top, e0, color); AddLine(top, e1, color); AddLine(top, e2, color); AddLine(top, e3, color);
            AddLine(bottom, e0, color); AddLine(bottom, e1, color); AddLine(bottom, e2, color); AddLine(bottom, e3, color);
            AddLine(e0, e1, color); AddLine(e1, e2, color); AddLine(e2, e3, color); AddLine(e3, e0, color);
        }

        unsafe void UploadLines()
        {
            if (_lineData.Count == 0) return;
            var array = _lineData.ToArray();
            _gl.BindVertexArray(_lineVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _lineVbo);
            fixed (float* p = array)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(array.Length * sizeof(float)), p, BufferUsageARB.DynamicDraw);
        }

        static Vector4 DomainColor(Domains domain) => domain switch
        {
            Domains.Jade => new Vector4(0.25f, 0.9f, 0.55f, 1f),
            Domains.Ruby => new Vector4(0.95f, 0.28f, 0.4f, 1f),
            Domains.Gold => new Vector4(0.95f, 0.8f, 0.28f, 1f),
            _ => new Vector4(0.35f, 0.55f, 0.95f, 1f),
        };

        static Vector4 ElementColor(Element element) => element switch
        {
            Element.Charge => new Vector4(0.4f, 0.92f, 1f, 1f),
            Element.Mass => new Vector4(1f, 0.55f, 0.3f, 1f),
            Element.Space => new Vector4(0.75f, 0.55f, 1f, 1f),
            Element.Time => new Vector4(0.55f, 1f, 0.65f, 1f),
            _ => new Vector4(1f, 1f, 1f, 1f), // Omni
        };

        // ── HUD (UiRenderer text overlay, y-up pixels) ─────────────────────────

        int DomainSum(Domains domain)
        {
            int sum = 0;
            foreach (var stats in _handle.GameData.RoundStatsList)
                if (stats.Domain == domain)
                    sum += stats.CrystalsCollected;
            return sum;
        }

        void DrawHud()
        {
            float w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            _ui.Begin(w, h);

            float t = CosmicShore.Engine.Time.time - _handle.RaceStartTime;
            _ui.DrawText("HEX RACE", 24f, h - 46f, 30f, new Vector4(0.55f, 0.95f, 1f, 1f));
            _ui.DrawText($"FIRST DOMAIN TO {_handle.Target}   t={t:0.0}s   claims {_handle.Result.TotalClaims}",
                24f, h - 78f, 18f, new Vector4(0.85f, 0.9f, 1f, 0.9f));
            _ui.DrawText($"JADE {DomainSum(Domains.Jade)}", 24f, h - 112f, 22f, DomainColor(Domains.Jade));
            _ui.DrawText($"RUBY {DomainSum(Domains.Ruby)}", 190f, h - 112f, 22f, DomainColor(Domains.Ruby));
            _ui.DrawText($"GOLD {DomainSum(Domains.Gold)}", 356f, h - 112f, 22f, DomainColor(Domains.Gold));

            float y = h - 156f;
            foreach (var stats in _handle.GameData.RoundStatsList)
            {
                _ui.DrawText($"{stats.Name,-6} {stats.CrystalsCollected,2}", 24f, y, 16f, DomainColor(stats.Domain));
                y -= 24f;
            }

            if (_handle.Result.Finished)
            {
                _ui.DrawText($"WINNER  {_handle.Result.WinnerName} ({_handle.Result.WinnerDomain})  {_handle.Result.FinishTime:0.00}s",
                    24f, 130f, 26f, new Vector4(0.4f, 1f, 0.6f, 1f));
                float sy = 96f;
                foreach (var standing in _handle.Result.Standings)
                {
                    _ui.DrawText($"#{standing.Rank} {standing.Name,-6} {standing.Domain,-5} {standing.Crystals,2} crystals  {standing.ScoreText}",
                        24f, sy, 16f, DomainColor(standing.Domain));
                    sy -= 22f;
                }
            }

            _ui.End();
        }

        // ── shader / buffers (RaceWindow's pos3+color4 idiom) ──────────────────

        const string VertexSrc = @"#version 330 core
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
        const string FragmentSrc = @"#version 330 core
in vec4 vColor;
out vec4 frag;
void main() { frag = vColor; }";

        uint CompileProgram()
        {
            uint vs = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vs, VertexSrc);
            _gl.CompileShader(vs);
            CheckShader(vs, "vertex");
            uint fs = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fs, FragmentSrc);
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

        unsafe void BuildStarfield()
        {
            // Fixed-seed System.Random — NEVER the engine RNG, whose stream the sim owns.
            var rng = new Random(1234);
            _starCount = 700;
            var data = new float[_starCount * 7];
            for (int i = 0; i < _starCount; i++)
            {
                float radius = 2500f + (float)rng.NextDouble() * 2500f;
                double theta = rng.NextDouble() * Math.PI * 2.0;
                double phi = Math.Acos(rng.NextDouble() * 2.0 - 1.0);
                float brightness = 0.35f + (float)rng.NextDouble() * 0.6f;
                int o = i * 7;
                data[o + 0] = radius * (float)(Math.Sin(phi) * Math.Cos(theta));
                data[o + 1] = radius * (float)(Math.Cos(phi));
                data[o + 2] = radius * (float)(Math.Sin(phi) * Math.Sin(theta));
                data[o + 3] = brightness;
                data[o + 4] = brightness;
                data[o + 5] = brightness * 1.05f;
                data[o + 6] = 1f;
            }

            _starVao = _gl.GenVertexArray();
            _starVbo = _gl.GenBuffer();
            _gl.BindVertexArray(_starVao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _starVbo);
            fixed (float* p = data)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            SetVertexLayout();
        }

        unsafe void ConfigureVao(uint vao, uint vbo)
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

        unsafe void SetMvp(Matrix4x4 mvp) => _gl.UniformMatrix4(_uMvp, 1, false, (float*)&mvp);

        unsafe void CaptureScreenshot()
        {
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            var pixels = new byte[w * h * 4];
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            MiniPng.Write(_screenshotPath, pixels, w, h);

            float t = CosmicShore.Engine.Time.time - _handle.RaceStartTime;
            string state = _handle.Result.Finished ? "Finished" : "Racing";
            string winner = _handle.Result.Finished ? _handle.Result.WinnerName : "none";
            Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, " +
                $"mode play, game HexRace, players {_playerCount}, target {_handle.Target}, " +
                $"t {t:0.00}, claims {_handle.Result.TotalClaims}, " +
                $"jade {DomainSum(Domains.Jade)} ruby {DomainSum(Domains.Ruby)} gold {DomainSum(Domains.Gold)}, " +
                $"state {state}, winner {winner}");
        }
    }
}
