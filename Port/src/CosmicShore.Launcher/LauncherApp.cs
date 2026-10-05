using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using ImGuiNET;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using StbImageSharp;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The launcher window: a Silk.NET GL window running Dear ImGui, with four pages -
    /// PLAY, BUILD, OPTIONS, CONSOLE - over an animated warp-field background.
    /// </summary>
    public sealed class LauncherApp
    {
        enum Page { Play, Build, Options, Console }

        public sealed record Args(string? Screenshot, int Frames, string? Page, bool Offline, string? Auto = null);

        readonly Args _args;
        readonly LauncherSettings _s;
        readonly Toolchain _tools = new();
        readonly Workspace _ws;
        readonly LauncherJobs _jobs;

        IWindow _window = null!;
        GL _gl = null!;
        IInputContext _input = null!;
        ImGuiController _imgui = null!;
        uint _logo, _manta, _froglet;
        Vector2 _logoSize, _mantaSize, _frogletSize;

        Page _page = Page.Play;
        bool _toolsScanned;
        bool _autoFired;
        bool _dirty;
        double _saveTimer;
        int _frame;
        string _branchFilter = "";
        bool _autoScroll = true;
        readonly List<LogLine> _logSnapshot = new();
        readonly Star[] _stars = new Star[320];
        readonly Random _rng = new(7);

        struct Star { public Vector3 P; public float Hue; }

        public LauncherApp(Args args)
        {
            _args = args;
            _s = LauncherSettings.Load();
            _ws = new Workspace(_s, _tools);
            _jobs = new LauncherJobs(_s, _tools, _ws);
            if (args.Page != null && Enum.TryParse<Page>(args.Page, true, out var p)) _page = p;
            if (LauncherSettings.FirstRun) DetectExistingClone();
            for (int i = 0; i < _stars.Length; i++) _stars[i] = NewStar(randomDepth: true);
        }

        /// <summary>A tester who already has the repo should not download it a second time.</summary>
        void DetectExistingClone()
        {
            var candidates = new List<string>();
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent) candidates.Add(d.FullName);
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub", "Cosmic-Shore"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Cosmic-Shore"));
            foreach (var c in candidates)
            {
                if (Directory.Exists(Path.Combine(c, ".git")) && Directory.Exists(Path.Combine(c, "Assets")))
                {
                    _s.MyClonePath = c;
                    _s.Workspace = WorkspaceMode.WorktreeOfMyClone;
                    _s.Save();
                    return;
                }
            }
        }

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(1360, 820),
                Title = "Froglet Engine Launcher - Cosmic Shore",
                VSync = true,
                PreferredStencilBufferBits = 8,
                Samples = 4,
            };
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Render += OnRender;
            _window.FramebufferResize += s => _gl?.Viewport(s);
            _window.Closing += () => { _s.Save(); _imgui?.Dispose(); };
            _window.Run();
            _window.Dispose();
        }

        // ---------------------------------------------------------------- setup

        void OnLoad()
        {
            _gl = _window.CreateOpenGL();
            _input = _window.CreateInput();
            var fontDir = Path.Combine(LauncherSettings.DataDir, "fonts");
            Directory.CreateDirectory(fontDir);
            string Font(string name)
            {
                var path = Path.Combine(fontDir, name);
                if (!File.Exists(path)) File.WriteAllBytes(path, Resource(name));
                return path;
            }
            _imgui = new ImGuiController(_gl, _window, _input, null, () =>
            {
                var io = ImGui.GetIO();
                io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
                unsafe { io.NativePtr->IniFilename = null; }
                Neon.Body = io.Fonts.AddFontFromFileTTF(Font("ChakraPetch-Regular.ttf"), 19);
                Neon.Small = io.Fonts.AddFontFromFileTTF(Font("ChakraPetch-Regular.ttf"), 15);
                Neon.Heading = io.Fonts.AddFontFromFileTTF(Font("Aldrich-Regular.ttf"), 24);
                Neon.Title = io.Fonts.AddFontFromFileTTF(Font("Aldrich-Regular.ttf"), 30);
                Neon.Hero = io.Fonts.AddFontFromFileTTF(Font("Aldrich-Regular.ttf"), 46);
                Neon.Mono = io.Fonts.AddFontFromFileTTF(Font("RobotoMono-Regular.ttf"), 15);
                unsafe { io.NativePtr->FontDefault = Neon.Body.NativePtr; }
            });
            Neon.ApplyStyle();
            (_logo, _logoSize) = Texture("logo.png");
            (_manta, _mantaSize) = Texture("manta.png");
            (_froglet, _frogletSize) = Texture("froglet.png");
            SetIcon();

            Task.Run(() =>
            {
                _tools.Detect(_s);
                _toolsScanned = true;
                _jobs.RefreshLocalState();
                if (!_args.Offline) _jobs.LoadBranches();
            });
        }

        static byte[] Resource(string name)
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                ?? throw new FileNotFoundException("missing embedded resource " + name);
            using var m = new MemoryStream();
            s.CopyTo(m);
            return m.ToArray();
        }

        unsafe (uint, Vector2) Texture(string name)
        {
            var img = ImageResult.FromMemory(Resource(name), ColorComponents.RedGreenBlueAlpha);
            uint tex = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            fixed (byte* p = img.Data)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            _gl.GenerateMipmap(TextureTarget.Texture2D);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            return (tex, new Vector2(img.Width, img.Height));
        }

        void SetIcon()
        {
            try
            {
                var img = ImageResult.FromMemory(Resource("icon.png"), ColorComponents.RedGreenBlueAlpha);
                var raw = new RawImage(img.Width, img.Height, new Memory<byte>(img.Data));
                _window.SetWindowIcon(ref raw);
            }
            catch (Exception) { /* some window systems have no icons */ }
        }

        // ---------------------------------------------------------------- frame

        void OnRender(double dt)
        {
            _frame++;
            _gl.ClearColor(0.01f, 0.01f, 0.04f, 1f);
            _gl.Clear(ClearBufferMask.ColorBufferBit);
            _imgui.Update((float)dt);
            if (_args.Auto != null && !_autoFired && _toolsScanned && _frame > 30)
            {
                _autoFired = true;
                _jobs.Log.Echo = true;
                switch (_args.Auto)
                {
                    case "play": _jobs.Play(); break;
                    case "update": _jobs.Update(); break;
                    case "android": _jobs.BuildPhone(ios: false); break;
                    case "ios": _jobs.BuildPhone(ios: true); break;
                }
            }
            DrawFrame((float)dt);
            _imgui.Render();

            _saveTimer += dt;
            if (_dirty && _saveTimer > 0.75) { _s.Save(); _dirty = false; _saveTimer = 0; }

            if (_args.Screenshot != null && _frame == _args.Frames)
            {
                SaveScreenshot(_args.Screenshot);
                _window.Close();
            }
            else if (_args.Screenshot == null && _args.Frames > 0 && _frame >= _args.Frames) _window.Close();
        }

        void DrawFrame(float dt)
        {
            var size = ImGui.GetIO().DisplaySize;
            DrawBackground(size, dt);

            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(size);
            ImGui.Begin("##root", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus |
                ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollWithMouse);
            DrawTopBar(size);
            var contentA = new Vector2(36, 104);
            var contentB = new Vector2(size.X - 36, size.Y - 96);
            switch (_page)
            {
                case Page.Play: DrawPlay(contentA, contentB); break;
                case Page.Build: DrawBuild(contentA, contentB); break;
                case Page.Options: DrawOptions(contentA, contentB); break;
                case Page.Console: DrawConsole(contentA, contentB); break;
            }
            DrawStatusBar(size);
            ImGui.End();
            DrawOverlay(size);
        }

        // ---------------------------------------------------------------- background

        Star NewStar(bool randomDepth) => new()
        {
            P = new Vector3((float)(_rng.NextDouble() * 2 - 1) * 1.6f, (float)(_rng.NextDouble() * 2 - 1) * 1.0f,
                randomDepth ? (float)_rng.NextDouble() * 3f + 0.05f : 3f),
            Hue = (float)_rng.NextDouble(),
        };

        void DrawBackground(Vector2 size, float dt)
        {
            var dl = ImGui.GetBackgroundDrawList();
            float t = Neon.Time;
            dl.AddRectFilledMultiColor(Vector2.Zero, size, Neon.U(Neon.Space1), Neon.U(Neon.Space1), Neon.U(Neon.Space0), Neon.U(Neon.Space0));

            // nebula: big soft discs drifting slowly
            void Blob(Vector2 c, float r, Vector4 col, float a)
            {
                for (int i = 10; i >= 1; i--) dl.AddCircleFilled(c, r * i / 10f, Neon.U(col, a * 0.05f), 48);
            }
            Blob(new Vector2(size.X * (0.78f + 0.03f * MathF.Sin(t * 0.07f)), size.Y * 0.30f), size.X * 0.30f, Neon.Magenta, 0.55f);
            Blob(new Vector2(size.X * (0.18f + 0.03f * MathF.Cos(t * 0.05f)), size.Y * 0.62f), size.X * 0.33f, Neon.Violet, 0.6f);
            Blob(new Vector2(size.X * 0.50f, size.Y * 0.05f), size.X * 0.22f, Neon.Cyan, 0.30f);

            // warp starfield toward a vanishing point slightly above centre
            var vp = new Vector2(size.X * 0.5f, size.Y * 0.44f);
            float speed = _jobs.Busy ? 1.6f : 0.35f;
            for (int i = 0; i < _stars.Length; i++)
            {
                ref var s = ref _stars[i];
                float z0 = s.P.Z;
                s.P.Z -= dt * speed;
                if (s.P.Z < 0.05f) { s = NewStar(randomDepth: false); continue; }
                var p1 = vp + new Vector2(s.P.X / s.P.Z, s.P.Y / s.P.Z) * size.Y * 0.5f;
                var p0 = vp + new Vector2(s.P.X / z0, s.P.Y / z0) * size.Y * 0.5f;
                float bright = Math.Clamp(1.2f - s.P.Z / 3f, 0f, 1f);
                var col = s.Hue < 0.7f ? Neon.Ink : s.Hue < 0.85f ? Neon.Cyan : Neon.Magenta;
                if ((p1 - p0).LengthSquared() < 0.5f) dl.AddCircleFilled(p1, 0.6f + bright * 1.2f, Neon.U(col, bright));
                else dl.AddLine(p0, p1, Neon.U(col, bright), 0.8f + bright * 1.4f);
            }

            // synthwave floor grid
            float horizon = size.Y * 0.70f;
            dl.AddRectFilledMultiColor(new Vector2(0, horizon - 2), new Vector2(size.X, size.Y),
                Neon.U(Neon.Magenta, 0.10f), Neon.U(Neon.Magenta, 0.10f), Neon.U(Neon.Space0, 0.9f), Neon.U(Neon.Space0, 0.9f));
            dl.AddLine(new Vector2(0, horizon), new Vector2(size.X, horizon), Neon.U(Neon.Magenta, 0.65f), 2f);
            float scroll = (t * (_jobs.Busy ? 1.4f : 0.45f)) % 1f;
            for (int i = 0; i < 18; i++)
            {
                float d = (i + 1 - scroll) / 18f;          // 0 = horizon, 1 = bottom
                float y = horizon + (size.Y - horizon) * d * d;
                dl.AddLine(new Vector2(0, y), new Vector2(size.X, y), Neon.U(Neon.Magenta, 0.10f + 0.45f * d), 1.2f);
            }
            var gv = new Vector2(size.X * 0.5f, horizon);
            for (int i = -16; i <= 16; i++)
            {
                float x = size.X * 0.5f + i * size.X * 0.09f;
                dl.AddLine(gv + new Vector2(i * 8f, 0), new Vector2(x, size.Y), Neon.U(Neon.Magenta, 0.30f), 1.1f);
            }
        }

        void DrawOverlay(Vector2 size)
        {
            var dl = ImGui.GetForegroundDrawList();
            for (float y = 0; y < size.Y; y += 3) dl.AddLine(new Vector2(0, y), new Vector2(size.X, y), Neon.U(Neon.Space0, 0.10f), 1f);
            float e = 90;
            dl.AddRectFilledMultiColor(Vector2.Zero, new Vector2(e, size.Y), Neon.U(Neon.Space0, 0.55f), 0, 0, Neon.U(Neon.Space0, 0.55f));
            dl.AddRectFilledMultiColor(new Vector2(size.X - e, 0), size, 0, Neon.U(Neon.Space0, 0.55f), Neon.U(Neon.Space0, 0.55f), 0);
        }

        // ---------------------------------------------------------------- top bar

        void DrawTopBar(Vector2 size)
        {
            var dl = ImGui.GetWindowDrawList();
            dl.AddRectFilledMultiColor(Vector2.Zero, new Vector2(size.X, 84), Neon.U(Neon.Space0, 0.85f), Neon.U(Neon.Space0, 0.85f), Neon.U(Neon.Space0, 0f), Neon.U(Neon.Space0, 0f));
            dl.AddLine(new Vector2(0, 78), new Vector2(size.X, 78), Neon.U(Neon.Cyan, 0.25f), 1f);

            float glow = 0.55f + 0.45f * MathF.Sin(Neon.Time * 1.7f);
            for (int i = 2; i >= 1; i--)
                dl.AddImage((IntPtr)_manta, new Vector2(22 - i * 3, 4 - i * 3), new Vector2(90 + i * 3, 72 + i * 3), Vector2.Zero, Vector2.One, Neon.U(Neon.Cyan, 0.25f * glow));
            dl.AddImage((IntPtr)_manta, new Vector2(22, 4), new Vector2(90, 72), Vector2.Zero, Vector2.One, Neon.U(Neon.Ink));
            Neon.GlowText(dl, Neon.Title, 30, new Vector2(96, 14), Neon.Cyan, "FROGLET ENGINE", 1f);
            ImGui.PushFont(Neon.Title);
            float tw = ImGui.CalcTextSize("FROGLET ENGINE").X;
            ImGui.PopFont();
            var chipA = new Vector2(96 + tw + 12, 20);
            Neon.ChamferFill(dl, chipA, chipA + new Vector2(52, 24), 6, Neon.U(Neon.Magenta, 0.25f));
            Neon.ChamferGlow(dl, chipA, chipA + new Vector2(52, 24), 6, Neon.Magenta, 0.8f, 1f);
            dl.AddText(Neon.Small, 15, chipA + new Vector2(11, 3), Neon.U(Neon.Ink), "v0.1");
            dl.AddText(Neon.Small, 15, new Vector2(98, 50), Neon.U(Neon.Dim), "COSMIC SHORE  //  LAUNCHER");

            // tabs
            string[] names = { "PLAY", "BUILD", "OPTIONS", "CONSOLE" };
            float x = Math.Max(size.X * 0.5f - 230, 96 + tw + 90);
            for (int i = 0; i < names.Length; i++)
            {
                var pg = (Page)i;
                ImGui.PushFont(Neon.Heading);
                var ts = ImGui.CalcTextSize(names[i]);
                ImGui.PopFont();
                var a = new Vector2(x, 20);
                ImGui.SetCursorScreenPos(a);
                if (ImGui.InvisibleButton("tab" + i, new Vector2(ts.X + 28, 44))) _page = pg;
                bool hov = ImGui.IsItemHovered();
                if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                bool on = _page == pg;
                var col = on ? Neon.Cyan : hov ? Neon.Mix(Neon.Dim, Neon.Cyan, 0.6f) : Neon.Dim;
                if (on) Neon.GlowText(dl, Neon.Heading, 24, a + new Vector2(14, 8), Neon.Cyan, names[i], 1.2f);
                else dl.AddText(Neon.Heading, 24, a + new Vector2(14, 8), Neon.U(col), names[i]);
                if (on)
                {
                    dl.AddRectFilled(new Vector2(a.X + 10, 66), new Vector2(a.X + ts.X + 18, 69), Neon.U(Neon.Cyan));
                    dl.AddRectFilled(new Vector2(a.X + 4, 62), new Vector2(a.X + ts.X + 24, 74), Neon.U(Neon.Cyan, 0.12f));
                }
                if (i == 3 && _jobs.LastOk == false && !on)
                    dl.AddCircleFilled(new Vector2(a.X + ts.X + 26, a.Y + 10), 4, Neon.U(Neon.Red));
                x += ts.X + 40;
            }

            // tool status pills
            float px = size.X - 36;
            void PillRight(string text, Vector4 col)
            {
                ImGui.PushFont(Neon.Small);
                float w = ImGui.CalcTextSize(text).X + 30;
                ImGui.PopFont();
                px -= w;
                Neon.Pill(dl, new Vector2(px, 28), text, col, out _);
                px -= 10;
            }
            if (!_toolsScanned) { PillRight("SCANNING", Neon.Amber); return; }
            PillRight(_tools.Dotnet != null ? ".NET " + _tools.DotnetSdk : ".NET MISSING", _tools.Dotnet != null ? Neon.Lime : Neon.Amber);
            PillRight(_tools.Git != null ? "GIT " + (_tools.GitVersion ?? "").Split(' ')[0].Split(".windows")[0] : "GIT MISSING", _tools.Git != null ? Neon.Lime : Neon.Red);
        }

        // ---------------------------------------------------------------- PLAY

        void DrawPlay(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            float split = a.X + (b.X - a.X) * 0.60f;

            // hero logo with a breathing glow
            float logoW = Math.Min(560, split - a.X - 40);
            var logoSize = new Vector2(logoW, logoW * _logoSize.Y / _logoSize.X);
            var lp = new Vector2(a.X + (split - a.X - logoW) * 0.5f, a.Y + 2);
            float breathe = 0.5f + 0.5f * MathF.Sin(Neon.Time * 1.3f);
            for (int i = 3; i >= 1; i--)
            {
                var g = new Vector2(i * 5f);
                dl.AddImage((IntPtr)_logo, lp - g, lp + logoSize + g, Vector2.Zero, Vector2.One, Neon.U(Neon.Cyan, 0.10f + 0.06f * breathe));
            }
            dl.AddImage((IntPtr)_logo, lp, lp + logoSize, Vector2.Zero, Vector2.One, Neon.U(Neon.Ink));
            float y = lp.Y + logoSize.Y + 6;
            CenterText(dl, Neon.Small, 15, (a.X + split) * 0.5f, y, Neon.Magenta, "THE  PARTY  GAME  FOR  PILOTS");

            // branch card
            var ca = new Vector2(a.X, y + 34);
            var cb = new Vector2(split - 20, ca.Y + 118);
            Neon.PanelFrame(dl, ca, cb, Neon.Cyan, "SOURCE BRANCH");
            ImGui.SetCursorScreenPos(ca + new Vector2(22, 38));
            ImGui.PushItemWidth(cb.X - ca.X - 44 - 130);
            BranchCombo();
            ImGui.PopItemWidth();
            ImGui.SameLine();
            if (SmallButton("REFRESH", 120, !_jobs.BranchesLoading && _tools.Git != null)) _jobs.LoadBranches();
            Neon.Tooltip("Reload the branch list from GitHub");
            ImGui.SetCursorScreenPos(ca + new Vector2(22, 84));
            var c = _jobs.Commit;
            ImGui.PushFont(Neon.Small);
            if (c != null)
            {
                ImGui.TextColored(Neon.Lime, "#" + c.Sha);
                ImGui.SameLine();
                ImGui.TextColored(Neon.Ink, Trim(c.Subject, 70));
                ImGui.SameLine();
                ImGui.TextColored(Neon.Dim, $"{c.Author} · {c.When}");
            }
            else ImGui.TextColored(Neon.Dim, _ws.Exists ? "Workspace present - press START to update it" : "Not downloaded yet - START fetches it");
            ImGui.PopFont();

            // START
            var sa = new Vector2(a.X, cb.Y + 22);
            ImGui.SetCursorScreenPos(sa);
            float bw = cb.X - ca.X;
            bool ready = _toolsScanned && _tools.Git != null && !_jobs.Busy;
            if (_jobs.GameRunning)
            {
                Neon.Button("start", "GAME RUNNING", new Vector2(bw * 0.64f, 96), Neon.Lime, Neon.Hero, 46, enabled: false, sub: "alt-tab back to it");
                ImGui.SameLine(0, 14);
                if (Neon.Button("stop", "STOP", new Vector2(bw * 0.36f - 14, 96), Neon.Red, Neon.Title, 30)) _jobs.StopGame();
            }
            else if (_jobs.Busy && _jobs.JobName == "Start game")
            {
                Neon.Button("start", "LAUNCHING", new Vector2(bw, 96), Neon.Magenta, Neon.Hero, 46, enabled: false, sub: _jobs.Stage.ToUpperInvariant());
            }
            else if (Neon.Button("start", "START GAME", new Vector2(bw, 96), Neon.Cyan, Neon.Hero, 46, ready, hero: true, playIcon: true,
                         sub: _s.PullBeforePlay ? "pull  ·  build  ·  launch" : "build  ·  launch"))
                _jobs.Play();

            // secondary row
            ImGui.SetCursorScreenPos(new Vector2(a.X, sa.Y + 112));
            float third = (bw - 28) / 3f;
            if (Neon.Button("upd", "UPDATE", new Vector2(third, 54), Neon.Violet, Neon.Heading, 22, ready, sub: "fetch branch only")) _jobs.Update();
            ImGui.SameLine(0, 14);
            if (Neon.Button("bld", "BUILD PHONE", new Vector2(third, 54), Neon.Magenta, Neon.Heading, 22, true, sub: "android / ios")) _page = Page.Build;
            ImGui.SameLine(0, 14);
            if (Neon.Button("ws", "WORKSPACE", new Vector2(third, 54), Neon.Dim, Neon.Heading, 22, _ws.Exists, sub: "open folder")) OpenFolder(_ws.Dir);

            DrawLaunchProfile(new Vector2(split + 10, a.Y), b);
        }

        void DrawLaunchProfile(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            Neon.PanelFrame(dl, a, b, Neon.Magenta, "LAUNCH PROFILE");
            float x = a.X + 24, w = b.X - a.X - 48;
            ImGui.SetCursorScreenPos(new Vector2(x, a.Y + 40));
            ImGui.BeginGroup();
            ImGui.PushItemWidth(w - 150);

            Label("Resolution");
            string[] res = { "1280x720", "1600x900", "1920x1080", "2560x1440" };
            Combo("##res", res, _s.Resolution, v => _s.Resolution = v);
            Label("Start in");
            var scenes = new List<string> { "" };
            scenes.AddRange(_jobs.Scenes);
            Combo("##scene", scenes.ToArray(), _s.StartScene, v => _s.StartScene = v, v => v == "" ? "Default (Bootstrap)" : v);
            ImGui.PopItemWidth();
            ImGui.Dummy(new Vector2(0, 4));
            Toggle("Fullscreen", () => _s.Fullscreen, v => _s.Fullscreen = v, "Open full-screen; F11 toggles in game");
            Toggle("Audio (FMOD)", () => _s.Audio, v => _s.Audio = v, "Fetch FMOD and play sound");
            Toggle("Online services", () => _s.Network, v => _s.Network = v, "Off = single-player only, no party/Relay");
            Toggle("Phone render path", () => _s.MobileRenderPath, v => _s.MobileRenderPath = v, "Render with OpenGL ES 3.0, exactly as a phone does");
            Toggle("Pull before play", () => _s.PullBeforePlay, v => _s.PullBeforePlay = v, "Fetch the branch's newest commit every START");
            ImGui.EndGroup();

            // in-game controls cheat sheet
            float cy = ImGui.GetCursorScreenPos().Y + 18;
            dl.AddLine(new Vector2(x, cy - 8), new Vector2(x + w, cy - 8), Neon.U(Neon.Magenta, 0.25f), 1f);
            dl.AddText(Neon.Small, 15, new Vector2(x, cy), Neon.U(Neon.Dim), "IN THE GAME");
            string[,] keys = { { "W A S D", "fly" }, { "SHIFT L / R", "triggers" }, { "SPACE  R  Q", "abilities" }, { "ESC", "menu / leave flight" }, { "F11", "fullscreen" }, { "0", "photo" } };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                int col2 = i % 2, row = i / 2;
                var kp = new Vector2(x + col2 * (w * 0.5f), cy + 26 + row * 30);
                ImGui.PushFont(Neon.Small);
                var ks = ImGui.CalcTextSize(keys[i, 0]);
                ImGui.PopFont();
                Neon.ChamferFill(dl, kp, kp + new Vector2(ks.X + 16, 24), 5, Neon.U(Neon.Cyan, 0.12f));
                Neon.ChamferPath(dl, kp, kp + new Vector2(ks.X + 16, 24), 5);
                dl.PathStroke(Neon.U(Neon.Cyan, 0.6f), ImDrawFlags.Closed, 1f);
                dl.AddText(Neon.Small, 15, kp + new Vector2(8, 3), Neon.U(Neon.Ink), keys[i, 0]);
                dl.AddText(Neon.Small, 15, kp + new Vector2(ks.X + 24, 3), Neon.U(Neon.Dim), keys[i, 1]);
            }

            // pipeline diagram: SYNC > AUDIO > BUILD > LAUNCH
            float py = b.Y - 92;
            dl.AddText(Neon.Small, 15, new Vector2(x, py - 26), Neon.U(Neon.Dim), "PIPELINE");
            string[] steps = { "SYNC", "AUDIO", "BUILD", "LAUNCH" };
            int active = !_jobs.Busy || _jobs.JobName != "Start game" ? (_jobs.GameRunning ? 4 : -1)
                : _jobs.Stage.StartsWith("Compil") ? 2 : _jobs.Stage.StartsWith("Starting the game") ? 3
                : _jobs.Stage.StartsWith("Audio") ? 1 : 0;
            float step = (w - 20) / (steps.Length - 1);
            x += 10;
            for (int i = 0; i < steps.Length; i++)
            {
                var p = new Vector2(x + i * step, py + 18);
                if (i < steps.Length - 1)
                    dl.AddLine(p + new Vector2(12, 0), p + new Vector2(step - 12, 0), Neon.U(i < active ? Neon.Cyan : Neon.Dim, i < active ? 0.9f : 0.3f), 2f);
                bool done = i < active, now = i == active;
                var col = done ? Neon.Cyan : now ? Neon.Magenta : Neon.Dim;
                float pulse = now ? 0.5f + 0.5f * MathF.Sin(Neon.Time * 6f) : 0f;
                dl.AddCircleFilled(p, 9, Neon.U(col, done || now ? 0.9f : 0.25f), 6);
                dl.AddCircle(p, 13 + pulse * 5, Neon.U(col, 0.35f + pulse * 0.4f), 6, 1.5f);
                ImGui.PushFont(Neon.Small);
                var ts = ImGui.CalcTextSize(steps[i]);
                ImGui.PopFont();
                float lx = i == 0 ? p.X - 10 : i == steps.Length - 1 ? p.X - ts.X + 10 : p.X - ts.X * 0.5f;
                dl.AddText(Neon.Small, 15, new Vector2(lx, p.Y + 20), Neon.U(done || now ? Neon.Ink : Neon.Dim), steps[i]);
            }
        }

        void BranchCombo()
        {
            if (ImGui.BeginCombo("##branch", _s.Branch, ImGuiComboFlags.HeightLarge))
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                ImGui.InputTextWithHint("##filter", "type to filter...", ref _branchFilter, 128);
                if (_jobs.BranchesLoading) ImGui.TextColored(Neon.Amber, "loading branches...");
                foreach (var br in _jobs.Branches)
                {
                    if (_branchFilter.Length > 0 && !br.Contains(_branchFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    if (ImGui.Selectable(br, br == _s.Branch)) { _s.Branch = br; _dirty = true; }
                }
                if (_branchFilter.Length > 0 && !_jobs.Branches.Contains(_branchFilter) && ImGui.Selectable($"use \"{_branchFilter}\""))
                { _s.Branch = _branchFilter; _dirty = true; }
                ImGui.EndCombo();
            }
        }

        // ---------------------------------------------------------------- BUILD

        void DrawBuild(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            float mid = (a.X + b.X) * 0.5f;
            float resultH = 120;
            var aa = a; var ab = new Vector2(mid - 12, b.Y - resultH - 20);
            var ia = new Vector2(mid + 12, a.Y); var ib = new Vector2(b.X, b.Y - resultH - 20);

            // ANDROID
            Neon.PanelFrame(dl, aa, ab, Neon.Lime, "ANDROID");
            Neon.GlowText(dl, Neon.Title, 30, aa + new Vector2(24, 34), Neon.Lime, "APK / AAB");
            dl.AddText(Neon.Small, 15, aa + new Vector2(24, 72), Neon.U(Neon.Dim), "Installs on any Android phone. First build sets up the Android SDK (once).");
            ImGui.SetCursorScreenPos(aa + new Vector2(24, 104));
            ImGui.BeginGroup();
            ImGui.PushItemWidth(ab.X - aa.X - 220);
            Label("CPU");
            string[] abis = { "arm64", "arm64,x64", "arm64,arm,x64,x86" };
            Combo("##abi", abis, _s.AndroidAbis, v => _s.AndroidAbis = v, v => v switch
            {
                "arm64" => "arm64 (every modern phone)",
                "arm64,x64" => "arm64 + x64 (phones + emulators)",
                _ => "all four",
            });
            Label("Package");
            int fmt = _s.AndroidBundle ? 1 : 0;
            if (ImGui.RadioButton("APK  (install directly)", fmt == 0)) { _s.AndroidBundle = false; _dirty = true; }
            ImGui.SameLine();
            if (ImGui.RadioButton("AAB  (Play Store)", fmt == 1)) { _s.AndroidBundle = true; _dirty = true; }
            Label("Signing");
            Text("##ks", "keystore (.keystore) - empty = debug key", () => _s.KeystorePath, v => _s.KeystorePath = v);
            if (!string.IsNullOrWhiteSpace(_s.KeystorePath))
                Text("##alias", "key alias", () => _s.KeystoreAlias, v => _s.KeystoreAlias = v);
            ImGui.PopItemWidth();
            Toggle("Debug build", () => _s.DebugBuild, v => _s.DebugBuild = v, "Debuggable, unoptimised");
            ImGui.EndGroup();
            ImGui.SetCursorScreenPos(new Vector2(aa.X + 24, ab.Y - 90));
            bool can = _toolsScanned && _tools.Git != null && !_jobs.Busy;
            if (Neon.Button("apk", _s.AndroidBundle ? "BUILD AAB" : "BUILD APK", new Vector2(ab.X - aa.X - 48, 66), Neon.Lime, Neon.Title, 30, can,
                    sub: "> Builds/Android in the workspace"))
                _jobs.BuildPhone(ios: false);

            // iOS
            bool mac = OperatingSystem.IsMacOS();
            Neon.PanelFrame(dl, ia, ib, Neon.Violet, "iOS");
            Neon.GlowText(dl, Neon.Title, 30, ia + new Vector2(24, 34), Neon.Violet, mac ? "IPA" : "XCODE EXPORT");
            var lines = mac
                ? new[] { "Builds and signs the .ipa with this Mac's Xcode.", "Set CS_IOS_CODESIGN_KEY / CS_IOS_PROVISIONING_PROFILE", "to choose an identity; otherwise Xcode picks one." }
                : new[] { "Apple only allows iPhone apps to be built on a Mac.", "On Windows this writes the complete iOS project", "(player data + build script), like Unity's Xcode export.", "Open the launcher on a Mac and press BUILD IPA there." };
            for (int i = 0; i < lines.Length; i++)
                dl.AddText(Neon.Small, 15, ia + new Vector2(24, 76 + i * 22), Neon.U(i == 0 ? Neon.Ink : Neon.Dim), lines[i]);
            ImGui.SetCursorScreenPos(new Vector2(ia.X + 24, ib.Y - 90));
            if (Neon.Button("ipa", mac ? "BUILD IPA" : "EXPORT FOR XCODE", new Vector2(ib.X - ia.X - 48, 66), Neon.Violet, Neon.Title, 30, can,
                    sub: mac ? "> Builds/iOS/*.ipa" : "> Builds/iOS  ·  finish on a Mac"))
                _jobs.BuildPhone(ios: true);

            // result
            var ra = new Vector2(a.X, b.Y - resultH); var rb = b;
            Neon.PanelFrame(dl, ra, rb, _jobs.LastOk == false ? Neon.Red : Neon.Cyan, "OUTPUT");
            ImGui.SetCursorScreenPos(ra + new Vector2(24, 40));
            ImGui.PushFont(Neon.Mono);
            if (_jobs.LastArtifact != null) ImGui.TextColored(Neon.Lime, _jobs.LastArtifact);
            else if (_jobs.Busy) ImGui.TextColored(Neon.Amber, _jobs.Stage);
            else ImGui.TextColored(Neon.Dim, "Nothing built yet in this session.");
            ImGui.PopFont();
            if (_jobs.LastArtifact != null)
            {
                ImGui.SetCursorScreenPos(new Vector2(rb.X - 220, ra.Y + 36));
                if (Neon.Button("open", "OPEN FOLDER", new Vector2(190, 50), Neon.Cyan, Neon.Heading, 20))
                    OpenFolder(File.Exists(_jobs.LastArtifact) ? Path.GetDirectoryName(_jobs.LastArtifact)! : _jobs.LastArtifact);
            }
        }

        // ---------------------------------------------------------------- OPTIONS

        void DrawOptions(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            float mid = (a.X + b.X) * 0.5f;
            float colW = mid - a.X - 12;
            var sa = a; var sb = new Vector2(mid - 12, a.Y + 400);
            var xa = new Vector2(a.X, sb.Y + 20); var xb = new Vector2(mid - 12, b.Y);
            var ta = new Vector2(mid + 12, a.Y); var tb = new Vector2(b.X, a.Y + 210);
            var ga = new Vector2(mid + 12, tb.Y + 20); var gb = b;

            // SOURCE
            Neon.PanelFrame(dl, sa, sb, Neon.Cyan, "SOURCE");
            ImGui.SetCursorScreenPos(sa + new Vector2(24, 40));
            ImGui.BeginGroup();
            ImGui.PushItemWidth(colW - 48);
            Label("Repository");
            Text("##remote", "https://github.com/.../Cosmic-Shore.git", () => _s.RemoteUrl, v => _s.RemoteUrl = v);
            Label("Branch");
            BranchCombo();
            Label("GitHub token (only if git has no sign-in)");
            {
                var tok = _s.GitHubToken ?? "";
                if (ImGui.InputTextWithHint("##token", "github_pat_...  (read access to the repo is enough)", ref tok, 256, ImGuiInputTextFlags.Password))
                { _s.GitHubToken = tok; _dirty = true; }
                Neon.Tooltip("Create one at github.com/settings/personal-access-tokens (Contents: read-only). Stored only on this PC.");
            }
            Label("Workspace");
            if (ImGui.RadioButton("Launcher's own copy", _s.Workspace == WorkspaceMode.Managed)) { _s.Workspace = WorkspaceMode.Managed; _dirty = true; }
            Neon.Tooltip(_s.ResolvedManagedPath);
            ImGui.SameLine();
            if (ImGui.RadioButton("Beside my clone", _s.Workspace == WorkspaceMode.WorktreeOfMyClone)) { _s.Workspace = WorkspaceMode.WorktreeOfMyClone; _dirty = true; }
            Neon.Tooltip("Uses a git worktree next to your clone: no second download, your checkout is never touched");
            if (_s.Workspace == WorkspaceMode.WorktreeOfMyClone)
                Text("##clone", "your Cosmic-Shore folder, e.g. C:\\Users\\you\\Documents\\GitHub\\Cosmic-Shore", () => _s.MyClonePath, v => _s.MyClonePath = v);
            ImGui.PopItemWidth();
            ImGui.PushFont(Neon.Small);
            ImGui.TextColored(Neon.Dim, "Builds run from: " + Trim(_ws.Dir, 64));
            ImGui.PopFont();
            ImGui.EndGroup();

            // GAME
            Neon.PanelFrame(dl, ga, gb, Neon.Magenta, "GAME");
            ImGui.SetCursorScreenPos(ga + new Vector2(24, 40));
            ImGui.BeginGroup();
            ImGui.PushItemWidth(colW - 48);
            Label("Extra player arguments");
            Text("##extra", "e.g. --seed 42", () => _s.ExtraArgs, v => _s.ExtraArgs = v);
            Label("Profile (a second local player)");
            Text("##profile", "empty = default", () => _s.Profile, v => _s.Profile = v);
            ImGui.PopItemWidth();
            Toggle("Release build (faster game)", () => _s.ReleaseBuild, v => _s.ReleaseBuild = v, "Off = Debug build, slower but easier to debug");
            Toggle("Verbose logs", () => _s.VerboseLogs, v => _s.VerboseLogs = v, "Turn on every log channel");
            ImGui.EndGroup();

            // TOOLCHAIN
            Neon.PanelFrame(dl, ta, tb, Neon.Lime, "TOOLCHAIN");
            ImGui.SetCursorScreenPos(ta + new Vector2(24, 40));
            ImGui.BeginGroup();
            StatusRow("git", _tools.Git != null, _tools.Git != null ? $"{_tools.GitVersion}  ·  {Trim(_tools.Git, 44)}" : "not found - install GitHub Desktop or Git for Windows");
            StatusRow("git-lfs", _tools.GitLfs, _tools.GitLfs ? "available" : "not needed (the launcher fetches FMOD itself)", soft: true);
            StatusRow(".NET SDK", _tools.Dotnet != null, _tools.Dotnet != null ? $"{_tools.DotnetSdk}  ·  {Trim(_tools.Dotnet, 40)}" : "missing - installed automatically on START");
            if (OperatingSystem.IsWindows()) StatusRow("VC++ runtime", _tools.VcRuntime, _tools.VcRuntime ? "present" : "missing - aka.ms/vs/17/release/vc_redist.x64.exe");
            ImGui.Dummy(new Vector2(0, 6));
            if (SmallButton("RESCAN", 130, !_jobs.Busy)) Task.Run(() => { _tools.Detect(_s); _jobs.RefreshLocalState(); });
            ImGui.SameLine();
            if (SmallButton("INSTALL .NET", 170, !_jobs.Busy && _tools.Dotnet == null)) _jobs.InstallDotnet();
            ImGui.SameLine();
            if (SmallButton("DATA FOLDER", 170, true)) OpenFolder(LauncherSettings.DataDir);
            ImGui.EndGroup();

            // ABOUT
            Neon.PanelFrame(dl, xa, xb, Neon.Violet, "ABOUT");
            var fs = new Vector2(170, 170 * _frogletSize.Y / _frogletSize.X);
            dl.AddImage((IntPtr)_froglet, new Vector2(xb.X - fs.X - 24, xa.Y + 34), new Vector2(xb.X - 24, xa.Y + 34 + fs.Y));
            string[] about =
            {
                "Froglet Engine v0.1 - Froglet Inc.",
                "Runs Cosmic Shore's real C# with no Unity at runtime:",
                "our own renderer, physics, UI, audio bridge and netcode.",
                "Unity stays the editor; this launcher is the player.",
                "Fonts: Chakra Petch, Aldrich (OFL), Roboto Mono (Apache 2.0).",
                "UI: Dear ImGui (MIT) on Silk.NET (MIT).",
            };
            for (int i = 0; i < about.Length; i++)
                dl.AddText(Neon.Small, 15, xa + new Vector2(24, 40 + i * 22), Neon.U(i == 0 ? Neon.Ink : Neon.Dim), about[i]);
        }

        void StatusRow(string name, bool ok, string detail, bool soft = false)
        {
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            var col = ok ? Neon.Lime : soft ? Neon.Dim : Neon.Amber;
            dl.AddCircleFilled(p + new Vector2(7, 12), 5, Neon.U(col));
            ImGui.SetCursorScreenPos(p + new Vector2(22, 0));
            ImGui.TextColored(Neon.Ink, name);
            ImGui.SameLine(140);
            ImGui.PushFont(Neon.Small);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
            ImGui.TextColored(Neon.Dim, detail);
            ImGui.PopFont();
        }

        // ---------------------------------------------------------------- CONSOLE

        void DrawConsole(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            Neon.PanelFrame(dl, a, b, Neon.Cyan, "CONSOLE");
            ImGui.SetCursorScreenPos(new Vector2(b.X - 470, a.Y + 10));
            if (SmallButton("COPY ALL", 130, true)) ImGui.SetClipboardText(_jobs.Log.AllText());
            ImGui.SameLine();
            if (SmallButton("CLEAR", 100, true)) _jobs.Log.Clear();
            ImGui.SameLine();
            if (ImGui.Checkbox("follow", ref _autoScroll)) { }

            ImGui.SetCursorScreenPos(a + new Vector2(18, 52));
            ImGui.PushFont(Neon.Mono);
            ImGui.BeginChild("##log", b - a - new Vector2(36, 70));
            _jobs.Log.CopyTo(_logSnapshot);
            unsafe
            {
                var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
                clipper.Begin(_logSnapshot.Count);
                while (clipper.Step())
                    for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                    {
                        var l = _logSnapshot[i];
                        var col = l.Kind switch
                        {
                            LogKind.Command => Neon.Cyan,
                            LogKind.Error => Neon.Red,
                            LogKind.Success => Neon.Lime,
                            LogKind.Warn => Neon.Amber,
                            LogKind.Info => Neon.Magenta,
                            _ => Neon.Mix(Neon.Dim, Neon.Ink, 0.4f),
                        };
                        ImGui.TextColored(Neon.Dim, l.Time.ToString("HH:mm:ss"));
                        ImGui.SameLine();
                        ImGui.TextColored(col, l.Text);
                    }
                clipper.End();
                clipper.Destroy();
            }
            if (_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 40) ImGui.SetScrollHereY(1f);
            ImGui.EndChild();
            ImGui.PopFont();
        }

        // ---------------------------------------------------------------- status bar

        void DrawStatusBar(Vector2 size)
        {
            var dl = ImGui.GetWindowDrawList();
            var a = new Vector2(36, size.Y - 76);
            var b = new Vector2(size.X - 36, size.Y - 20);
            var col = _jobs.Busy ? Neon.Magenta : _jobs.LastOk == false ? Neon.Red : _jobs.GameRunning ? Neon.Lime : Neon.Cyan;
            Neon.ChamferFill(dl, a, b, 10, Neon.U(Neon.Space0, 0.85f));
            Neon.ChamferGlow(dl, a, b, 10, col, 0.6f, 1f);

            string state = _jobs.Busy ? _jobs.Stage.ToUpperInvariant()
                : _jobs.GameRunning ? "GAME RUNNING"
                : _jobs.LastOk == false ? "STOPPED - SEE CONSOLE"
                : _jobs.LastOk == true ? "DONE" : "READY";
            Neon.GlowText(dl, Neon.Heading, 20, a + new Vector2(18, 7), col, Trim(state, 46), 0.8f);
            var last = _jobs.Log.LastLine;
            if (string.IsNullOrEmpty(last))
                last = _ws.Exists ? "workspace: " + _ws.Dir : "press START GAME - the launcher fetches, builds and runs " + _s.Branch;
            dl.AddText(Neon.Mono, 14, a + new Vector2(20, 33), Neon.U(Neon.Dim), Trim(last, 120));

            float barX = b.X - 520;
            if (_jobs.Busy || _jobs.Progress >= 0)
                Neon.Progress(dl, new Vector2(barX, a.Y + 14), new Vector2(b.X - (_jobs.Busy ? 140 : 18), b.Y - 14), _jobs.Busy ? _jobs.Progress : 1f, col);
            if (_jobs.Busy)
            {
                ImGui.SetCursorScreenPos(new Vector2(b.X - 126, a.Y + 10));
                if (Neon.Button("cancel", "CANCEL", new Vector2(110, 36), Neon.Red, Neon.Small, 15)) _jobs.Cancel();
            }
            else if (_jobs.LastOk == false && _page != Page.Console)
            {
                ImGui.SetCursorScreenPos(new Vector2(b.X - 186, a.Y + 10));
                if (Neon.Button("seelog", "OPEN CONSOLE", new Vector2(170, 36), Neon.Red, Neon.Small, 15)) _page = Page.Console;
            }
        }

        // ---------------------------------------------------------------- small widgets

        static void Label(string text)
        {
            ImGui.PushFont(Neon.Small);
            ImGui.TextColored(Neon.Dim, text.ToUpperInvariant());
            ImGui.PopFont();
        }

        void Combo(string id, string[] items, string current, Action<string> set, Func<string, string>? show = null)
        {
            show ??= s => s;
            if (!ImGui.BeginCombo(id, show(current))) return;
            foreach (var it in items)
                if (ImGui.Selectable(show(it), it == current)) { set(it); _dirty = true; }
            ImGui.EndCombo();
        }

        void Toggle(string label, Func<bool> get, Action<bool> set, string? tip = null)
        {
            bool v = get();
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            var size = new Vector2(46, 24);
            if (ImGui.InvisibleButton("##t" + label, new Vector2(size.X + 12 + ImGui.CalcTextSize(label).X, size.Y))) { v = !v; set(v); _dirty = true; }
            bool hov = ImGui.IsItemHovered();
            if (tip != null) Neon.Tooltip(tip);
            var col = v ? Neon.Cyan : Neon.Dim;
            Neon.ChamferFill(dl, p, p + size, 6, Neon.U(v ? Neon.Cyan : Neon.Space0, v ? 0.28f : 0.9f));
            Neon.ChamferPath(dl, p, p + size, 6);
            dl.PathStroke(Neon.U(col, hov ? 1f : 0.7f), ImDrawFlags.Closed, 1.2f);
            float kx = v ? p.X + size.X - 18 : p.X + 4;
            dl.AddRectFilled(new Vector2(kx, p.Y + 4), new Vector2(kx + 14, p.Y + size.Y - 4), Neon.U(col));
            dl.AddText(p + new Vector2(size.X + 12, 1), Neon.U(v ? Neon.Ink : Neon.Dim), label);
        }

        void Text(string id, string hint, Func<string> get, Action<string> set)
        {
            var v = get() ?? "";
            if (ImGui.InputTextWithHint(id, hint, ref v, 512)) { set(v); _dirty = true; }
        }

        static bool SmallButton(string label, float w, bool enabled) =>
            Neon.Button("sb" + label, label, new Vector2(w, 38), Neon.Cyan, Neon.Small, 15, enabled);

        static void CenterText(ImDrawListPtr dl, ImFontPtr font, float size, float cx, float y, Vector4 col, string text)
        {
            ImGui.PushFont(font);
            float w = ImGui.CalcTextSize(text).X * size / font.FontSize;
            ImGui.PopFont();
            Neon.GlowText(dl, font, size, new Vector2(cx - w * 0.5f, y), col, text, 0.7f);
        }

        static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "...";

        static void OpenFolder(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;
                if (OperatingSystem.IsWindows()) Process.Start("explorer.exe", path);
                else if (OperatingSystem.IsMacOS()) Process.Start("open", path);
                else Process.Start("xdg-open", path);
            }
            catch (Exception) { /* no file manager */ }
        }

        // ---------------------------------------------------------------- screenshot (docs + tests)

        unsafe void SaveScreenshot(string path)
        {
            var fb = _window.FramebufferSize;
            int w = fb.X, h = fb.Y;
            var px = new byte[w * h * 4];
            fixed (byte* p = px) _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            using var raw = new MemoryStream();
            for (int y = h - 1; y >= 0; y--)
            {
                raw.WriteByte(0);
                for (int x = 0; x < w; x++)
                {
                    int i = (y * w + x) * 4;
                    raw.WriteByte(px[i]); raw.WriteByte(px[i + 1]); raw.WriteByte(px[i + 2]);
                }
            }
            using var f = File.Create(path);
            f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            void Chunk(string type, byte[] data)
            {
                var len = BitConverter.GetBytes(data.Length); Array.Reverse(len); f.Write(len);
                var t = System.Text.Encoding.ASCII.GetBytes(type);
                f.Write(t); f.Write(data);
                var crc = BitConverter.GetBytes(Crc(t.Concat(data).ToArray())); Array.Reverse(crc); f.Write(crc);
            }
            var ihdr = new byte[13];
            BitConverter.GetBytes(w).Reverse().ToArray().CopyTo(ihdr, 0);
            BitConverter.GetBytes(h).Reverse().ToArray().CopyTo(ihdr, 4);
            ihdr[8] = 8; ihdr[9] = 2;
            Chunk("IHDR", ihdr);
            using var z = new MemoryStream();
            using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) { raw.Position = 0; raw.CopyTo(zs); }
            Chunk("IDAT", z.ToArray());
            Chunk("IEND", Array.Empty<byte>());
        }

        static uint Crc(byte[] data)
        {
            uint c = 0xFFFFFFFF;
            foreach (var b in data)
            {
                c ^= b;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }
            return c ^ 0xFFFFFFFF;
        }
    }
}
