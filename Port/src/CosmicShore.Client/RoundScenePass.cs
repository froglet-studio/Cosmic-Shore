using System;
using System.Collections.Generic;
using System.Numerics;
using CosmicShore.Cli;
using CosmicShore.Data;
using Silk.NET.OpenGL;
using EngineVector3 = CosmicShore.Engine.Vector3;
using Matrix4x4 = System.Numerics.Matrix4x4;

namespace CosmicShore.Client
{
    /// <summary>
    /// Arc-G render pass over any <see cref="IRoundDriver"/>: the deterministic
    /// wireframe scene (seeded starfield, upcoming-course crosses, the active crystal
    /// as a spinning element-tinted octahedron, domain-tinted vessel arrows,
    /// sim-derived chase camera) plus the shared round HUD (title, domain sums,
    /// per-pilot crystals, WINNER + standings once the round ends). Extracted from
    /// ModeHostWindow so the menu shell's game phase renders the SAME pass —
    /// one scene idiom for every windowed round host.
    /// </summary>
    public sealed class RoundScenePass : IDisposable
    {
        readonly GL _gl;
        readonly uint _program;
        readonly int _uMvp;
        readonly uint _starVao, _starVbo;
        readonly int _starCount;
        readonly uint _lineVao, _lineVbo;
        readonly List<float> _lineData = new();

        public RoundScenePass(GL gl)
        {
            _gl = gl;
            _program = CompileProgram();
            _uMvp = _gl.GetUniformLocation(_program, "uMvp");
            (_starVao, _starVbo, _starCount) = BuildStarfield();
            _lineVao = _gl.GenVertexArray();
            _lineVbo = _gl.GenBuffer();
            ConfigureVao(_lineVao, _lineVbo);
        }

        /// <summary>Viewport + clear + the full 3D wireframe pass for one frame.</summary>
        public void Render(IRoundDriver round, int fbWidth, int fbHeight)
        {
            _gl.Viewport(0, 0, (uint)fbWidth, (uint)fbHeight);
            _gl.ClearColor(0.012f, 0.0f, 0.045f, 1f); // deep space indigo
            _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

            // UiRenderer.End hands back "sim" state (depth ON, additive blend) — this
            // wireframe pass owns its own: no depth (lines over cleared space), additive
            // neon on the dark clear.
            _gl.Disable(EnableCap.DepthTest);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);

            var viewProjection = BuildCamera(round, fbWidth, fbHeight);
            _gl.UseProgram(_program);
            SetMvp(viewProjection);

            _gl.BindVertexArray(_starVao);
            _gl.DrawArrays(PrimitiveType.Points, 0, (uint)_starCount);

            BuildLineGeometry(round);
            UploadLines();
            _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)(_lineData.Count / 7));
        }

        /// <summary>Elapsed round-clock seconds (0 until the round goes live).</summary>
        public static float Clock(IRoundDriver round) =>
            round.Live ? CosmicShore.Engine.Time.time - round.ClockStart : 0f;

        public static int DomainSum(IRoundDriver round, Domains domain)
        {
            int sum = 0;
            foreach (var stats in round.GameData.RoundStatsList)
                if (stats.Domain == domain)
                    sum += stats.CrystalsCollected;
            return sum;
        }

        /// <summary>The shared round HUD (UiRenderer text overlay, y-up pixels).</summary>
        public void DrawHud(UiRenderer ui, IRoundDriver round, float w, float h)
        {
            ui.Begin(w, h);

            ui.DrawText(round.GameLabel, 24f, h - 46f, 30f, new Vector4(0.55f, 0.95f, 1f, 1f));
            ui.DrawText($"FIRST DOMAIN TO {round.Target}   t={Clock(round):0.0}s   claims {round.TotalClaims}",
                24f, h - 78f, 18f, new Vector4(0.85f, 0.9f, 1f, 0.9f));
            ui.DrawText($"JADE {DomainSum(round, Domains.Jade)}", 24f, h - 112f, 22f, DomainColor(Domains.Jade));
            ui.DrawText($"RUBY {DomainSum(round, Domains.Ruby)}", 190f, h - 112f, 22f, DomainColor(Domains.Ruby));
            ui.DrawText($"GOLD {DomainSum(round, Domains.Gold)}", 356f, h - 112f, 22f, DomainColor(Domains.Gold));

            float y = h - 156f;
            foreach (var stats in round.GameData.RoundStatsList)
            {
                ui.DrawText($"{stats.Name,-6} {stats.CrystalsCollected,2}", 24f, y, 16f, DomainColor(stats.Domain));
                y -= 24f;
            }

            if (round.Finished)
            {
                ui.DrawText($"WINNER  {round.WinnerName} ({round.WinnerDomain})", 24f, 130f, 26f, new Vector4(0.4f, 1f, 0.6f, 1f));
                float sy = 96f;
                foreach (var standing in round.StandingRows)
                {
                    ui.DrawText($"#{standing.Rank} {standing.Name,-6} {standing.Domain,-5} {standing.Crystals,2} crystals  {standing.ScoreText}",
                        24f, sy, 16f, DomainColor(standing.Domain));
                    sy -= 22f;
                }
            }
            else if (!round.Live)
            {
                ui.DrawText("GET READY...", 24f, 130f, 26f, new Vector4(0.9f, 0.9f, 0.6f, 1f));
            }

            ui.End();
        }

        Matrix4x4 BuildCamera(IRoundDriver round, int fbWidth, int fbHeight)
        {
            // Sim-derived chase camera (no wall-clock smoothing — deterministic):
            // behind the first pilot, looking at the active crystal (or ahead).
            var v0 = round.Players[0].Vessel.Transform;
            EngineVector3 eye = v0.position - v0.forward * 70f + EngineVector3.up * 26f;
            var crystal = round.ActiveCrystal;
            EngineVector3 look = crystal
                ? crystal.transform.position
                : v0.position + v0.forward * 120f;

            float aspect = fbWidth / (float)Math.Max(1, fbHeight);
            var view = Matrix4x4.CreateLookAt(eye, look, new Vector3(0f, 1f, 0f));
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(65f * MathF.PI / 180f, aspect, 0.5f, 8000f);
            return view * projection;
        }

        void BuildLineGeometry(IRoundDriver round)
        {
            _lineData.Clear();

            // Upcoming course waypoints — dim crosses fading with distance in the order.
            var course = round.Course;
            for (int i = round.CourseIndex + 1; i < course.Length && i <= round.CourseIndex + 12; i++)
            {
                float fade = 1f - (i - round.CourseIndex) / 14f;
                var c = new Vector4(0.45f, 0.5f, 0.85f, 0.55f * fade);
                AddCross(course[i], 6f, c);
            }

            // Active crystal — spinning element-tinted octahedron (spin angle derives
            // from the simulated frame count, so it is deterministic per frame).
            var crystal = round.ActiveCrystal;
            if (crystal)
            {
                float yaw = round.FramesStepped * (1f / 60f) * 1.1f;
                var element = round.CourseElements[round.CourseIndex];
                AddOctahedron(crystal.transform.position, 10f, yaw, ElementColor(element));
            }

            // The AI field — domain-tinted arrows built from each vessel's live pose.
            foreach (var player in round.Players)
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

        public static Vector4 DomainColor(Domains domain) => domain switch
        {
            Domains.Jade => new Vector4(0.25f, 0.9f, 0.55f, 1f),
            Domains.Ruby => new Vector4(0.95f, 0.28f, 0.4f, 1f),
            Domains.Gold => new Vector4(0.95f, 0.8f, 0.28f, 1f),
            _ => new Vector4(0.35f, 0.55f, 0.95f, 1f),
        };

        public static Vector4 ElementColor(Element element) => element switch
        {
            Element.Charge => new Vector4(0.4f, 0.92f, 1f, 1f),
            Element.Mass => new Vector4(1f, 0.55f, 0.3f, 1f),
            Element.Space => new Vector4(0.75f, 0.55f, 1f, 1f),
            Element.Time => new Vector4(0.55f, 1f, 0.65f, 1f),
            _ => new Vector4(1f, 1f, 1f, 1f), // Omni
        };

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

        unsafe (uint vao, uint vbo, int count) BuildStarfield()
        {
            // Fixed-seed System.Random — NEVER the engine RNG, whose stream the sim owns.
            var rng = new Random(1234);
            int starCount = 700;
            var data = new float[starCount * 7];
            for (int i = 0; i < starCount; i++)
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

            uint vao = _gl.GenVertexArray();
            uint vbo = _gl.GenBuffer();
            _gl.BindVertexArray(vao);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            fixed (float* p = data)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
            SetVertexLayout();
            return (vao, vbo, starCount);
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

        public void Dispose()
        {
            _gl.DeleteProgram(_program);
            _gl.DeleteVertexArray(_starVao);
            _gl.DeleteBuffer(_starVbo);
            _gl.DeleteVertexArray(_lineVao);
            _gl.DeleteBuffer(_lineVbo);
        }
    }
}
