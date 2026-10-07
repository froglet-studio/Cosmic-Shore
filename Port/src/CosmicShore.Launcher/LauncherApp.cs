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
    public sealed partial class LauncherApp
    {
        enum Page { Play, Build, Project, Chat, Options, Console, Tracks, Board, Milestones, Git, Editor }

        public sealed record Args(string? Screenshot, int Frames, string? Page, bool Offline, string? Auto = null, string? UpdatedFrom = null, int Tour = -1);

        readonly Args _args;
        readonly LauncherSettings _s;
        readonly Toolchain _tools = new();
        readonly Workspace _ws;
        readonly LauncherJobs _jobs;
        readonly ChatStore _chats;
        /// <summary>The conversation the AGENT page shows; the others keep running behind it.</summary>
        ClaudeChat _chat => _chats.Active;

        IWindow _window = null!;
        GL _gl = null!;
        IInputContext _input = null!;
        ImGuiController _imgui = null!;
        uint _logo, _manta, _froglet;
        System.Runtime.InteropServices.GCHandle _glyphs;
        bool _tourOffered;
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
            _git = new SourceControl(_s, _tools, _ws);
            _chats = new ChatStore(_s, _tools, c =>
            {
                c.MilestoneStopped += stop => _ui.Enqueue(() => OnMilestoneStopped(stop));
                c.ReplyFinished += t => { if (_s.VoiceReplies && ReferenceEquals(c, _chats?.Active)) _voice?.Speak(t); };
                c.Saved += () => _ui.Enqueue(() => OnChatRunEnded(c));
            });
            _updater = new LauncherUpdater(_s, _tools, _ws);
            // A play session that ends is folded into the tracks, and Prisma says what it found.
            _jobs.GameExited += () => Task.Run(async () => { await Task.Delay(2000); IngestSessions(notify: true); });
            if (args.Tour >= 0) _tour = args.Tour;
            var pageArg = args.Page?.Split(':');
            if (pageArg != null && Enum.TryParse<Page>(pageArg[0], true, out var p)) _page = p;
            if (pageArg is { Length: > 1 } && int.TryParse(pageArg[1], out var tab)) { _projTab = tab; _edTab = tab; }
            if (pageArg is { Length: > 2 }) _edOpen = string.Join(":", pageArg.Skip(2)); // --page editor:1:Assets/x.asset opens that file (docs screenshots)
            if (pageArg is { Length: > 1 } && pageArg[1] == "usage") _usageOpen = true; // --page chat:usage (docs screenshots)
            else if (pageArg is { Length: > 1 }) { _open.Clear(); _open.Add(pageArg[1].ToUpperInvariant()); } // --page options:claude
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
                Title = "Prisma - Cosmic Shore",
                VSync = true,
                PreferredStencilBufferBits = 8,
                Samples = 4,
            };
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Render += OnRender;
            _window.FramebufferResize += s => _gl?.Viewport(s);
            _window.Closing += () => { _chats.StopAll(); _s.Save(); _imgui?.Dispose(); };
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
                // Latin plus the punctuation and arrows Claude's replies use (dashes, bullets, ellipsis, arrows, math).
                _glyphs = System.Runtime.InteropServices.GCHandle.Alloc(new ushort[] { 0x0020, 0x00FF, 0x2010, 0x2027, 0x2030, 0x205E, 0x2190, 0x21FF, 0x2200, 0x22FF, 0x25A0, 0x25FF, 0 }, System.Runtime.InteropServices.GCHandleType.Pinned);
                var ranges = _glyphs.AddrOfPinnedObject();
                Neon.Body = io.Fonts.AddFontFromFileTTF(Font("Inter-Regular.ttf"), 17, null, ranges);
                Neon.Small = io.Fonts.AddFontFromFileTTF(Font("Inter-Regular.ttf"), 14, null, ranges);
                Neon.Strong = io.Fonts.AddFontFromFileTTF(Font("Inter-SemiBold.ttf"), 16, null, ranges);
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
                IngestSessions(notify: true); // runs that ended while Prisma was closed are news too
                RunDoctor();
                // Only LOOK for a newer launcher; installing it is the user's call (UPDATE).
                if (!_args.Offline) _ = _updater.Check();
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

        /// <summary>Work handed to the UI thread by background threads (it touches state the frame reads).</summary>
        readonly System.Collections.Concurrent.ConcurrentQueue<Action> _ui = new();

        void OnRender(double dt)
        {
            _frame++;
            while (_ui.TryDequeue(out var work)) work();
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
                    case "ios": _jobs.BuildIos(); break;
                    case var u when u.StartsWith("launcher-update:"):
                        _installRev = u["launcher-update:".Length..];
                        Task.Run(() => _updater.Install(_installRev, _jobs.Log));
                        break;
                    case var u when u.StartsWith("launcher-use:"):
                        var pick = LauncherUpdater.Installed().FirstOrDefault(v => v.Short == u["launcher-use:".Length..]);
                        if (pick != null) _updater.Use(pick, _jobs.Log);
                        break;
                    case "ingest": Task.Run(() => IngestSessions(notify: true)); break;
                    case "claude-install":
                        _page = Page.Chat;
                        Task.Run(() => _chat.Install(_jobs.Log));
                        break;
                    case var c when c.StartsWith("chat:"):
                        _page = Page.Chat;
                        _chat.Detect();
                        SendChat(c[5..], ClaudeChat.Mode.Plan);
                        break;
                    case var m when m.StartsWith("milestone:"):
                        _chat.Detect();
                        StartMilestone(m["milestone:".Length..]);
                        break;
                }
            }
            DrawFrame((float)dt);
            _imgui.Render();

            SaveProjectIfDirty(dt);
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
            DrawRail(size);
            DrawTitleBar(size);
            var contentA = new Vector2(RailW + 44, TitleH + 22);
            var contentB = new Vector2(size.X - 44, size.Y - 64);
            float enter = PageEnter(dt);               // pages slide up into place as they open
            contentA.Y += 14 * (1 - enter); contentB.Y += 14 * (1 - enter);
            switch (_page)
            {
                case Page.Play: DrawPlay(contentA, contentB); break;
                case Page.Build: DrawBuild(contentA, contentB); break;
                case Page.Project: DrawProject(contentA, contentB); break;
                case Page.Chat: DrawChat(contentA, contentB); break;
                case Page.Options: DrawOptions(contentA, contentB); break;
                case Page.Console: DrawConsole(contentA, contentB); break;
                case Page.Tracks: DrawTracks(contentA, contentB); break;
                case Page.Board: DrawBoard(contentA, contentB); break;
                case Page.Milestones: DrawMilestones(contentA, contentB); break;
                case Page.Git: DrawGit(contentA, contentB); break;
                case Page.Editor: DrawEditor(contentA, contentB); break;
            }
            DrawStatusBar(size);
            ImGui.End();
            DrawOverlay(size);
            if (enter < 1) ImGui.GetForegroundDrawList().AddRectFilled(new Vector2(RailW + 1, 0), new Vector2(size.X, size.Y - 40), Neon.U(Neon.Space0, 0.65f * (1 - enter)));
            DrawUpdateUi(size, dt);
            DrawNotifications(size, dt);
            WatchJobs();
            if (!_s.TourDone && _tour < 0 && !_tourOffered && SplashDone && _args.Screenshot == null && _args.Auto == null) { _tourOffered = true; StartTour(); }
            DrawTour(size);
            DrawSplash(size, dt);
        }

        // ---------------------------------------------------------------- background

        Star NewStar(bool randomDepth) => new()
        {
            P = new Vector3((float)(_rng.NextDouble() * 2 - 1) * 1.6f, (float)(_rng.NextDouble() * 2 - 1) * 1.0f,
                randomDepth ? (float)_rng.NextDouble() * 3f + 0.05f : 3f),
            Hue = (float)_rng.NextDouble(),
        };

        /// <summary>The original look: nebula, warp starfield and the synthwave floor (each part optional).</summary>
        void DrawSynthwave(ImDrawListPtr dl, Vector2 size, float dt, float t, bool nebula, bool warp, bool grid)
        {

            // nebula: big soft discs drifting slowly
            void Blob(Vector2 c, float r, Vector4 col, float a)
            {
                for (int i = 10; i >= 1; i--) dl.AddCircleFilled(c, r * i / 10f, Neon.U(col, a * 0.05f), 48);
            }
            if (nebula) Blob(new Vector2(size.X * (0.78f + 0.03f * MathF.Sin(t * 0.07f)), size.Y * 0.30f), size.X * 0.30f, Neon.Magenta, 0.55f);
            if (nebula) Blob(new Vector2(size.X * (0.18f + 0.03f * MathF.Cos(t * 0.05f)), size.Y * 0.62f), size.X * 0.33f, Neon.Violet, 0.6f);
            if (nebula) Blob(new Vector2(size.X * 0.50f, size.Y * 0.05f), size.X * 0.22f, Neon.Cyan, 0.30f);

            // warp starfield toward a vanishing point slightly above centre
            var vp = new Vector2(size.X * 0.5f, size.Y * 0.44f);
            float speed = warp ? (_jobs.Busy ? 1.6f : 0.35f) : 0.06f;
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
            if (!grid) return;
            float horizon = size.Y * 0.70f;
            dl.AddRectFilledMultiColor(new Vector2(0, horizon - 2), new Vector2(size.X, size.Y),
                Neon.U(Neon.Magenta, 0.04f), Neon.U(Neon.Magenta, 0.04f), Neon.U(Neon.Space0, 0.9f), Neon.U(Neon.Space0, 0.9f));
            dl.AddLine(new Vector2(0, horizon), new Vector2(size.X, horizon), Neon.U(Neon.Magenta, 0.28f), 1.5f);
            float scroll = (t * (_jobs.Busy ? 1.4f : 0.45f)) % 1f;
            for (int i = 0; i < 18; i++)
            {
                float d = (i + 1 - scroll) / 18f;          // 0 = horizon, 1 = bottom
                float y = horizon + (size.Y - horizon) * d * d;
                dl.AddLine(new Vector2(0, y), new Vector2(size.X, y), Neon.U(Neon.Magenta, 0.03f + 0.16f * d), 1f);
            }
            var gv = new Vector2(size.X * 0.5f, horizon);
            for (int i = -16; i <= 16; i++)
            {
                float x = size.X * 0.5f + i * size.X * 0.09f;
                dl.AddLine(gv + new Vector2(i * 8f, 0), new Vector2(x, size.Y), Neon.U(Neon.Magenta, 0.10f), 1f);
            }
        }

        void DrawOverlay(Vector2 size)
        {
            var dl = ImGui.GetForegroundDrawList();
            float e = 90;
            dl.AddRectFilledMultiColor(Vector2.Zero, new Vector2(e, size.Y), Neon.U(Neon.Space0, 0.55f), 0, 0, Neon.U(Neon.Space0, 0.55f));
            dl.AddRectFilledMultiColor(new Vector2(size.X - e, 0), size, 0, Neon.U(Neon.Space0, 0.55f), Neon.U(Neon.Space0, 0.55f), 0);
        }

        // ---------------------------------------------------------------- rail (navigation)

        const float RailW = 92;

        void DrawRail(Vector2 size)
        {
            var dl = ImGui.GetWindowDrawList();
            // macOS sidebar: translucent, with the Prisma mark where the traffic lights would be.
            dl.AddRectFilled(Vector2.Zero, new Vector2(RailW, size.Y), Neon.U(Neon.Space0, 0.62f));
            dl.AddRectFilledMultiColor(Vector2.Zero, new Vector2(RailW, size.Y), Neon.U(Neon.Ink, 0.035f), Neon.U(Neon.Ink, 0.01f), Neon.U(Neon.Ink, 0.0f), Neon.U(Neon.Ink, 0.02f));
            dl.AddLine(new Vector2(RailW, 0), new Vector2(RailW, size.Y), Neon.U(Neon.Ink, 0.08f), 1f);
            Neon.PrismIcon(dl, new Vector2(RailW * 0.5f - 8, 30), 30, 1f, Neon.Time);
            var mb = new Vector2(RailW - 16, 50);

            (Page page, string name, Action<ImDrawListPtr, Vector2, uint> icon)[] items =
            {
                (Page.Play, "PLAY", Neon.IconPlay),
                (Page.Build, "BUILD", Neon.IconPhone),
                (Page.Project, "PROJECT", Neon.IconSliders),
                (Page.Chat, "AGENT", Neon.IconChat),
                (Page.Git, "GIT", IconBranch),
                (Page.Editor, "EDITOR", IconCube),
                (Page.Tracks, "TRACKS", IconTracks),
                (Page.Board, "BOARD", IconBoard),
                (Page.Milestones, "MILESTONES", IconFlag),
                (Page.Options, "SETTINGS", Neon.IconGear),
                (Page.Console, "CONSOLE", Neon.IconTerminal),
            };
            const float itemH = 60, step = 63;
            float y = mb.Y + 18;
            // The selection glides between items rather than jumping.
            if (_railY < 0) _railY = _railTarget;
            _railY += (_railTarget - _railY) * Math.Min(1f, ImGui.GetIO().DeltaTime * 14f);
            if (_railY > 0)
            {
                var sa = new Vector2(8, _railY); var sb = new Vector2(RailW - 8, _railY + itemH);
                Neon.ChamferFill(dl, sa, sb, 8, Neon.U(Neon.Cyan, 0.10f));
                dl.AddRectFilled(new Vector2(0, sa.Y + 10), new Vector2(3, sb.Y - 10), Neon.U(Neon.Cyan));
            }
            foreach (var it in items)
            {
                var a = new Vector2(8, y); var b = new Vector2(RailW - 8, y + itemH);
                ImGui.SetCursorScreenPos(a);
                if (ImGui.InvisibleButton("nav" + it.name, b - a)) _page = it.page;
                _railRects[it.page] = (a, b);
                bool hov = ImGui.IsItemHovered(), on = _page == it.page;
                if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                var col = on ? Neon.Cyan : hov ? Neon.Ink : Neon.Dim;
                if (on) _railTarget = a.Y;
                if (hov && !on) Neon.ChamferFill(dl, a, b, 8, Neon.U(Neon.Ink, 0.04f));
                it.icon(dl, new Vector2((a.X + b.X) * 0.5f, a.Y + 22), Neon.U(col));
                ImGui.PushFont(Neon.Small);
                float tw = ImGui.CalcTextSize(it.name).X;
                ImGui.PopFont();
                dl.AddText(Neon.Small, 11, new Vector2((a.X + b.X - tw * 11f / 14f) * 0.5f, a.Y + 41), Neon.U(col), it.name);
                int badge = RailBadge(it.page);
                if (badge > 0 && !on)
                {
                    var bc = new Vector2(b.X - 16, a.Y + 12);
                    dl.AddCircleFilled(bc, 8, Neon.U(Neon.Red), 16);
                    var bt = badge > 9 ? "9+" : badge.ToString();
                    dl.AddText(Neon.Small, 11, bc - new Vector2(bt.Length * 3f, 6.5f), Neon.U(new Vector4(1, 1, 1, 1)), bt);
                }
                if (it.page == Page.Console && _jobs.LastOk == false && !on)
                    dl.AddCircleFilled(new Vector2(b.X - 14, a.Y + 12), 4, Neon.U(Neon.Red));
                y += step;
            }

            // tools: two dots, details on hover
            var dp = new Vector2(RailW * 0.5f, size.Y - 30);
            void Dot(Vector2 p, bool? ok, string tip)
            {
                var c = ok == null ? Neon.Amber : ok.Value ? Neon.Lime : Neon.Red;
                dl.AddCircleFilled(p, 5, Neon.U(c, 0.9f));
                dl.AddCircle(p, 8, Neon.U(c, 0.3f), 16, 1.5f);
                ImGui.SetCursorScreenPos(p - new Vector2(10));
                ImGui.InvisibleButton("dot" + tip, new Vector2(20));
                Neon.Tooltip(tip);
            }
            Dot(dp - new Vector2(14, 0), _toolsScanned ? _tools.Git != null : null,
                _tools.Git != null ? "git " + _tools.GitVersion : "git not found - install GitHub Desktop");
            Dot(dp + new Vector2(14, 0), _toolsScanned ? _tools.Dotnet != null : null,
                _tools.Dotnet != null ? ".NET " + _tools.DotnetSdk : ".NET is installed automatically on START");
        }

        void PageHeader(Vector2 a, string title, string? sub = null)
        {
            var dl = ImGui.GetWindowDrawList();
            Neon.GlowText(dl, Neon.Title, 30, a, Neon.Cyan, title, 0.7f);
            if (sub != null) dl.AddText(Neon.Small, 15, a + new Vector2(2, 40), Neon.U(Neon.Dim), sub);
        }

        // ---------------------------------------------------------------- PLAY

        void DrawPlay(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            float cx = (a.X + b.X) * 0.5f;
            float colW = Math.Min(720, b.X - a.X - 40);
            float x0 = cx - colW * 0.5f;

            float logoW = Math.Min(520, colW);
            var logoSize = new Vector2(logoW, logoW * _logoSize.Y / _logoSize.X);
            var lp = new Vector2(cx - logoW * 0.5f, a.Y + 10);
            float breathe = 0.5f + 0.5f * MathF.Sin(Neon.Time * 1.3f);
            for (int i = 3; i >= 1; i--)
            {
                var g = new Vector2(i * 5f);
                dl.AddImage((IntPtr)_logo, lp - g, lp + logoSize + g, Vector2.Zero, Vector2.One, Neon.U(Neon.Cyan, 0.08f + 0.05f * breathe));
            }
            dl.AddImage((IntPtr)_logo, lp, lp + logoSize, Vector2.Zero, Vector2.One, Neon.U(Neon.Ink));
            float y = lp.Y + logoSize.Y + 30;

            // branch: one line, commit underneath in small type
            ImGui.SetCursorScreenPos(new Vector2(x0, y));
            ImGui.PushItemWidth(colW - 52);
            BranchCombo();
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 8);
            if (Neon.IconButton("refresh", Neon.IconRefresh, 38, !_jobs.BranchesLoading && _tools.Git != null)) _jobs.LoadBranches();
            Neon.Tooltip("Reload branches from GitHub");
            var c = _jobs.Commit;
            string commit = c != null ? $"#{c.Sha}  {Trim(c.Subject, 64)}" : _ws.Exists ? "workspace ready" : "not downloaded yet";
            dl.AddText(Neon.Small, 15, new Vector2(x0 + 4, y + 46), Neon.U(Neon.Dim), commit);
            if (c != null)
            {
                ImGui.SetCursorScreenPos(new Vector2(x0, y + 44));
                ImGui.InvisibleButton("commit", new Vector2(colW, 20));
                Neon.Tooltip($"{c.Author}  ·  {c.When}");
            }
            y += 84;

            // START
            ImGui.SetCursorScreenPos(new Vector2(x0, y));
            bool ready = _toolsScanned && _tools.Git != null && !_jobs.Busy;
            if (_jobs.GameRunning)
            {
                Neon.Button("start", "RUNNING", new Vector2(colW - 150, 96), Neon.Lime, Neon.Hero, 46, enabled: false);
                ImGui.SameLine(0, 12);
                if (Neon.Button("stop", "STOP", new Vector2(138, 96), Neon.Red, Neon.Title, 30)) _jobs.StopGame();
            }
            else if (_jobs.Busy && _jobs.JobName == "Start game")
                Neon.Button("start", "LAUNCHING", new Vector2(colW, 96), Neon.Magenta, Neon.Hero, 46, enabled: false);
            else if (Neon.Button("start", "START", new Vector2(colW, 96), Neon.Cyan, Neon.Hero, 46, ready, hero: true, playIcon: true))
                _jobs.Play();
            y += 112;

            // quick toggles, one row
            ImGui.SetCursorScreenPos(new Vector2(x0, y));
            Toggle("Fullscreen", () => _s.Fullscreen, v => _s.Fullscreen = v);
            ImGui.SameLine(0, 26);
            Toggle("Audio", () => _s.Audio, v => _s.Audio = v);
            ImGui.SameLine(0, 26);
            Toggle("Online", () => _s.Network, v => _s.Network = v, "Party, Relay and cloud services");
            ImGui.SameLine(0, 26);
            Toggle("Pull first", () => _s.PullBeforePlay, v => _s.PullBeforePlay = v, "Fetch the branch's newest commit on every START");

            // small ghost actions, bottom-right of the column
            ImGui.SetCursorScreenPos(new Vector2(x0 + colW - 2 * 44 - 8, y - 4));
            if (Neon.IconButton("upd", Neon.IconDownload, 40, ready)) _jobs.Update();
            Neon.Tooltip("Update: fetch the branch without starting");
            ImGui.SameLine(0, 8);
            if (Neon.IconButton("ws", Neon.IconFolder, 40, _ws.Exists)) OpenFolder(_ws.Dir);
            Neon.Tooltip("Open the workspace folder");

            // pipeline, only while launching
            if (_jobs.Busy && _jobs.JobName == "Start game") DrawPipeline(dl, new Vector2(x0, y + 62), colW);
        }

        void DrawPipeline(ImDrawListPtr dl, Vector2 p0, float w)
        {
            string[] steps = { "SYNC", "AUDIO", "BUILD", "LAUNCH" };
            int active = _jobs.Stage.StartsWith("Compil") ? 2 : _jobs.Stage.StartsWith("Starting the game") ? 3
                : _jobs.Stage.StartsWith("Audio") ? 1 : 0;
            float step = (w - 20) / (steps.Length - 1);
            for (int i = 0; i < steps.Length; i++)
            {
                var p = new Vector2(p0.X + 10 + i * step, p0.Y + 10);
                if (i < steps.Length - 1)
                    dl.AddLine(p + new Vector2(10, 0), p + new Vector2(step - 10, 0), Neon.U(i < active ? Neon.Cyan : Neon.Dim, i < active ? 0.9f : 0.25f), 2f);
                bool done = i < active, now = i == active;
                var col = done ? Neon.Cyan : now ? Neon.Magenta : Neon.Dim;
                float pulse = now ? 0.5f + 0.5f * MathF.Sin(Neon.Time * 6f) : 0f;
                dl.AddCircleFilled(p, 6, Neon.U(col, done || now ? 0.9f : 0.25f), 6);
                dl.AddCircle(p, 10 + pulse * 4, Neon.U(col, 0.3f + pulse * 0.4f), 6, 1.2f);
                ImGui.PushFont(Neon.Small);
                var ts = ImGui.CalcTextSize(steps[i]);
                ImGui.PopFont();
                float lx = i == 0 ? p.X - 8 : i == steps.Length - 1 ? p.X - ts.X + 8 : p.X - ts.X * 0.5f;
                dl.AddText(Neon.Small, 15, new Vector2(lx, p.Y + 14), Neon.U(done || now ? Neon.Ink : Neon.Dim), steps[i]);
            }
        }

        void BranchCombo()
        {
            if (ImGui.BeginCombo("##branch", _s.Branch, ImGuiComboFlags.HeightLarge))
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                ImGui.InputTextWithHint("##filter", "filter...", ref _branchFilter, 128);
                if (_jobs.BranchesLoading) ImGui.TextColored(Neon.Amber, "loading...");
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

        bool _androidMore, _iosMore;

        void DrawBuild(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            PageHeader(a, "BUILD", "Phone builds of " + _s.Branch);
            float top = a.Y + 86;
            float mid = (a.X + b.X) * 0.5f;
            float cardB = Math.Min(b.Y, top + 440);
            var aa = new Vector2(a.X, top); var ab = new Vector2(mid - 12, cardB);
            var ia = new Vector2(mid + 12, top); var ib = new Vector2(b.X, cardB);
            bool can = _toolsScanned && _tools.Git != null && !_jobs.Busy;

            // ANDROID
            BuildCard(dl, aa, ab, Neon.Lime, "ANDROID", _s.AndroidBundle ? ".aab" : ".apk",
                () =>
                {
                    Segmented("droidfmt", new[] { "APK", "AAB" }, _s.AndroidBundle ? 1 : 0, i => _s.AndroidBundle = i == 1, Neon.Lime);
                    Neon.Tooltip("APK installs directly. AAB is for the Play Store.");
                },
                ref _androidMore,
                () =>
                {
                    ImGui.PushItemWidth(ab.X - aa.X - 60);
                    Label("CPU");
                    string[] abis = { "arm64", "arm64,x64", "arm64,arm,x64,x86" };
                    Combo("##abi", abis, _s.AndroidAbis, v => _s.AndroidAbis = v, v => v switch { "arm64" => "arm64", "arm64,x64" => "arm64 + x64 (emulators)", _ => "all" });
                    Label("Keystore");
                    Text("##ks", "empty = debug key", () => _s.KeystorePath, v => _s.KeystorePath = v);
                    if (!string.IsNullOrWhiteSpace(_s.KeystorePath))
                        Text("##alias", "alias", () => _s.KeystoreAlias, v => _s.KeystoreAlias = v);
                    ImGui.PopItemWidth();
                    Toggle("Debug build", () => _s.DebugBuild, v => _s.DebugBuild = v);
                },
                _s.AndroidBundle ? "BUILD AAB" : "BUILD APK", can, () => _jobs.BuildPhone(ios: false),
                _jobs.JobName == "Build Android");

            // iOS
            bool mac = OperatingSystem.IsMacOS();
            string[] modes = mac ? new[] { "GITHUB", "XCODE", "THIS MAC" } : new[] { "GITHUB", "XCODE" };
            if (!mac && _s.Ios == IosMode.ThisMac) _s.Ios = IosMode.GitHub;
            string action = _s.Ios switch { IosMode.GitHub => "BUILD .IPA", IosMode.Xcode => "EXPORT XCODE", _ => "BUILD .IPA" };
            BuildCard(dl, ia, ib, Neon.Violet, "iOS", _s.Ios == IosMode.Xcode ? ".xcodeproj" : ".ipa",
                () =>
                {
                    Segmented("iosmode", modes, (int)_s.Ios, i => _s.Ios = (IosMode)i, Neon.Violet);
                    Neon.Tooltip(_s.Ios switch
                    {
                        IosMode.GitHub => "GitHub's free Mac compiles an unsigned .ipa from the branch.\nSign and install it with Sideloadly. No Mac needed.",
                        IosMode.Xcode => "An Xcode project, like Unity's iOS export.\nOpen it on a Mac, pick a team, Run or Archive.",
                        _ => "This Mac builds and signs the .ipa.",
                    });
                },
                ref _iosMore,
                () =>
                {
                    Toggle("Debug build", () => _s.DebugBuild, v => _s.DebugBuild = v);
                    if (_s.Ios == IosMode.GitHub)
                    {
                        ImGui.PushFont(Neon.Small);
                        ImGui.TextColored(Neon.Dim, "Uses your GitHub sign-in (or the token in SETTINGS).");
                        ImGui.TextColored(Neon.Dim, "Takes ~15-25 min on GitHub's Mac.");
                        ImGui.PopFont();
                        if (SmallButton("SIDELOADLY", 160, true)) OpenUrl("https://sideloadly.io");
                    }
                },
                action, can, () => _jobs.BuildIos(), _jobs.JobName.StartsWith("Build iOS"));

            if (_jobs.IosRunUrl != null && _jobs.JobName.StartsWith("Build iOS"))
            {
                ImGui.SetCursorScreenPos(new Vector2(ia.X + 24, ib.Y - 44));
                if (SmallButton("OPEN RUN", 140, true)) OpenUrl(_jobs.IosRunUrl);
            }
        }

        /// <summary>One build target: name, its one choice, a primary button, the rest folded away, the result.</summary>
        void BuildCard(ImDrawListPtr dl, Vector2 a, Vector2 b, Vector4 accent, string name, string ext,
            Action choice, ref bool more, Action options, string action, bool can, Action run, bool mine)
        {
            Neon.PanelFrame(dl, a, b, accent, null, 0.5f);
            Neon.GlowText(dl, Neon.Title, 30, a + new Vector2(26, 24), accent, name, 0.8f);
            ImGui.PushFont(Neon.Title);
            float nw = ImGui.CalcTextSize(name).X;
            ImGui.PopFont();
            dl.AddText(Neon.Mono, 15, a + new Vector2(34 + nw, 36), Neon.U(Neon.Dim), ext);

            ImGui.SetCursorScreenPos(a + new Vector2(26, 80));
            ImGui.BeginGroup();
            choice();
            ImGui.Dummy(new Vector2(0, 4));
            if (Disclosure("opt" + name, "Options", ref more))
            {
                ImGui.Indent(4);
                options();
                ImGui.Unindent(4);
            }
            ImGui.EndGroup();

            float w = b.X - a.X - 52;
            float by = b.Y - (mine && _jobs.LastArtifact != null ? 176 : 112);
            ImGui.SetCursorScreenPos(new Vector2(a.X + 26, by));
            bool busyHere = mine && _jobs.Busy;
            if (Neon.Button("go" + name, busyHere ? "WORKING" : action, new Vector2(w, 64), accent, Neon.Title, 28, can && !busyHere))
                run();
            if (mine && _jobs.LastArtifact != null && !_jobs.Busy)
            {
                var p = new Vector2(a.X + 26, by + 78);
                dl.AddText(Neon.Mono, 14, p, Neon.U(Neon.Lime), Trim(Path.GetFileName(_jobs.LastArtifact), 46));
                ImGui.SetCursorScreenPos(new Vector2(b.X - 26 - 140, p.Y - 8));
                if (SmallButton("SHOW" + "##" + name, 140, true))
                    OpenFolder(File.Exists(_jobs.LastArtifact) ? Path.GetDirectoryName(_jobs.LastArtifact)! :
                        Directory.Exists(_jobs.LastArtifact) && _jobs.LastArtifact.EndsWith(".xcodeproj") ? Path.GetDirectoryName(_jobs.LastArtifact)! : _jobs.LastArtifact);
            }
        }

        // ---------------------------------------------------------------- PROJECT (the engine's own Project Settings)

        Prisma.PrismaProjectSettings? _proj;
        Prisma.PrismaProjectSettings.UnityDefaults? _unity;
        string? _projRoot;
        bool _projDirty;
        double _projTimer;
        int _projTab;

        void EnsureProject()
        {
            if (_projRoot == _ws.Dir && _proj != null) return;
            _projRoot = _ws.Dir;
            _proj = Prisma.PrismaProjectSettings.Load(_ws.Dir);
            _unity = new Prisma.PrismaProjectSettings.UnityDefaults(_ws.Dir);
        }

        void SaveProjectIfDirty(double dt)
        {
            if (!_projDirty) return;
            _projTimer += dt;
            if (_projTimer < 0.6) return;
            _projDirty = false; _projTimer = 0;
            try { _proj!.Save(_projRoot!); _jobs.RefreshLocalState(); }
            catch (Exception ex) { _jobs.Log.Add(LogKind.Error, "Could not save project settings: " + ex.Message); }
        }

        void DrawProject(Vector2 a, Vector2 b)
        {
            PageHeader(a, "PROJECT", "Engine settings for this branch  ·  " + Prisma.PrismaProjectSettings.RelativePath);
            if (!_ws.Exists || !File.Exists(Path.Combine(_ws.Dir, "ProjectSettings", "ProjectSettings.asset")))
            {
                ImGui.GetWindowDrawList().AddText(Neon.Body, 19, a + new Vector2(0, 110), Neon.U(Neon.Dim), "Download the branch first - press START or UPDATE on PLAY.");
                return;
            }
            EnsureProject();
            var p = _proj!; var u = _unity!;

            ImGui.SetCursorScreenPos(new Vector2(a.X, a.Y + 78));
            Segmented("ptab", new[] { "PLAYER", "SCENES", "QUALITY" }, _projTab, i => _projTab = i, Neon.Cyan);

            ImGui.SetCursorScreenPos(new Vector2(a.X, a.Y + 132));
            ImGui.BeginChild("##project", new Vector2(b.X - a.X, b.Y - a.Y - 132));
            float w = Math.Min(760, b.X - a.X - 30);
            ImGui.PushItemWidth(w - 260);
            switch (_projTab)
            {
                case 0:
                    ProjText("Company", p.Player.CompanyName, u.CompanyName, v => p.Player.CompanyName = v);
                    ProjText("Product name", p.Player.ProductName, u.ProductName, v => p.Player.ProductName = v);
                    ProjText("Version", p.Player.Version, u.Version, v => p.Player.Version = v);
                    ImGui.Dummy(new Vector2(0, 8));
                    ProjText("Android package", p.Player.AndroidBundleId, u.AndroidBundleId, v => p.Player.AndroidBundleId = v);
                    ProjText("Android version code", p.Player.AndroidVersionCode?.ToString(), u.AndroidVersionCode.ToString(),
                        v => p.Player.AndroidVersionCode = int.TryParse(v, out var n) && n > 0 ? n : null);
                    ProjText("iOS bundle id", p.Player.IosBundleId, u.IosBundleId, v => p.Player.IosBundleId = v);
                    ProjText("iOS build number", p.Player.IosBuildNumber, u.IosBuildNumber, v => p.Player.IosBuildNumber = v);
                    ImGui.Dummy(new Vector2(0, 12));
                    Hint("Empty fields use Unity's Player Settings. Commit the file to share it with the branch.");
                    break;

                case 1:
                    DrawSceneList(p, u, w);
                    break;

                case 2:
                    Row("Anti-aliasing", () => Segmented("qmsaa", new[] { "DEFAULT", "OFF", "2x", "4x", "8x" },
                        p.Quality.Msaa switch { null => 0, 0 => 1, 2 => 2, 4 => 3, _ => 4 },
                        i => { p.Quality.Msaa = i switch { 0 => null, 1 => 0, 2 => 2, 3 => 4, _ => 8 }; _projDirty = true; }, Neon.Cyan));
                    Row("Render scale", () =>
                    {
                        float rs = p.Quality.RenderScale ?? 1f;
                        if (ImGui.SliderFloat("##qrs", ref rs, 0.5f, 2f, p.Quality.RenderScale == null ? "default (1.00x)" : "%.2fx"))
                        { p.Quality.RenderScale = MathF.Round(rs * 20) / 20; _projDirty = true; }
                        ImGui.SameLine();
                        if (Neon.IconButton("qrsreset", Neon.IconRefresh, 34, p.Quality.RenderScale != null)) { p.Quality.RenderScale = null; _projDirty = true; }
                        Neon.Tooltip("Back to default");
                    });
                    Row("Texture filtering", () => Segmented("qaniso", new[] { "DEFAULT", "1x", "4x", "8x", "16x" },
                        p.Quality.Anisotropy switch { null => 0, 1 => 1, 4 => 2, 8 => 3, _ => 4 },
                        i => { p.Quality.Anisotropy = i switch { 0 => null, 1 => 1, 2 => 4, 3 => 8, _ => 16 }; _projDirty = true; }, Neon.Cyan));
                    Row("VSync", () => Segmented("qvs", new[] { "DEFAULT", "ON", "OFF" },
                        p.Quality.VSync switch { null => 0, true => 1, false => 2 },
                        i => { p.Quality.VSync = i switch { 0 => null, 1 => true, _ => false }; _projDirty = true; }, Neon.Cyan));
                    if (p.Quality.VSync == false)
                        Row("Frame cap", () => Segmented("qfps", new[] { "NONE", "30", "60", "120", "144" },
                            p.Quality.TargetFps switch { 30 => 1, 60 => 2, 120 => 3, 144 => 4, _ => 0 },
                            i => { p.Quality.TargetFps = i switch { 1 => 30, 2 => 60, 3 => 120, 4 => 144, _ => null }; _projDirty = true; }, Neon.Cyan));
                    ImGui.Dummy(new Vector2(0, 12));
                    Hint("Default matches Unity's URP asset: 4x MSAA, 1.0 scale, 8x filtering, vsync on.");
                    break;
            }
            ImGui.PopItemWidth();
            ImGui.EndChild();
        }

        void DrawSceneList(Prisma.PrismaProjectSettings p, Prisma.PrismaProjectSettings.UnityDefaults u, float w)
        {
            var list = p.BuildScenes(u);
            bool own = p.Scenes is { Count: > 0 };
            var dl = ImGui.GetWindowDrawList();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 28);
            ImGui.PushFont(Neon.Small);
            ImGui.TextColored(Neon.Dim, own ? "Engine scene list (overrides Unity's)" : "Unity's Scenes In Build  -  edit to make an engine list");
            ImGui.PopFont();
            if (own)
            {
                ImGui.SameLine(w - 150);
                if (SmallButton("USE UNITY'S", 150, true)) { p.Scenes = null; _projDirty = true; }
            }
            ImGui.Dummy(new Vector2(0, 4));
            int enabledIndex = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                var pos = ImGui.GetCursorScreenPos();
                var rowB = pos + new Vector2(w, 34);
                if (i % 2 == 0) dl.AddRectFilled(pos, rowB, Neon.U(Neon.Cyan, 0.04f));
                ImGui.SetCursorScreenPos(pos + new Vector2(28, 5));
                bool en = s.Enabled;
                if (ImGui.Checkbox("##en" + i, ref en)) { MakeOwn(p, list)[i].Enabled = en; _projDirty = true; }
                string idx = s.Enabled ? (enabledIndex++).ToString() : "-";
                dl.AddText(Neon.Mono, 14, pos + new Vector2(66, 9), Neon.U(Neon.Dim), idx.PadLeft(2));
                dl.AddText(Neon.Body, 17, pos + new Vector2(96, 7), Neon.U(s.Enabled ? Neon.Ink : Neon.Dim), SceneName(Path.GetFileNameWithoutExtension(s.Path)));
                ImGui.SetCursorScreenPos(pos + new Vector2(96, 0));
                ImGui.InvisibleButton("##path" + i, new Vector2(w - 200, 34));
                Neon.Tooltip(s.Path);
                ImGui.SetCursorScreenPos(new Vector2(rowB.X - 72, pos.Y + 2));
                if (Neon.IconButton("up" + i, Neon.IconUp, 30, i > 0)) { var l = MakeOwn(p, list); (l[i - 1], l[i]) = (l[i], l[i - 1]); _projDirty = true; }
                ImGui.SameLine(0, 4);
                if (Neon.IconButton("dn" + i, Neon.IconDown, 30, i < list.Count - 1)) { var l = MakeOwn(p, list); (l[i + 1], l[i]) = (l[i], l[i + 1]); _projDirty = true; }
                ImGui.SetCursorScreenPos(new Vector2(pos.X, rowB.Y + 2));
            }
            ImGui.Dummy(new Vector2(0, 8));
            Hint("Scene 0 boots. The build tool, the player and phone builds all use this list.");
        }

        static List<Prisma.PrismaProjectSettings.SceneEntry> MakeOwn(
            Prisma.PrismaProjectSettings p, List<Prisma.PrismaProjectSettings.SceneEntry> current)
        {
            if (p.Scenes is not { Count: > 0 })
                p.Scenes = current.Select(s => new Prisma.PrismaProjectSettings.SceneEntry { Path = s.Path, Guid = s.Guid, Enabled = s.Enabled }).ToList();
            return p.Scenes;
        }

        void ProjText(string label, string? value, string inherited, Action<string?> set)
        {
            Row(label, () =>
            {
                var v = value ?? "";
                if (ImGui.InputTextWithHint("##p" + label, inherited, ref v, 256))
                { set(string.IsNullOrWhiteSpace(v) ? null : v); _projDirty = true; }
                if (value != null)
                {
                    ImGui.SameLine();
                    var pt = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddCircleFilled(pt + new Vector2(6, 17), 4, Neon.U(Neon.Magenta));
                    ImGui.Dummy(new Vector2(12, 34));
                    Neon.Tooltip("Overridden here. Unity: " + inherited);
                }
            });
        }

        static void Hint(string text)
        {
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 28);
            ImGui.PushFont(Neon.Small);
            ImGui.TextColored(Neon.Dim, text);
            ImGui.PopFont();
        }

        // ---------------------------------------------------------------- CHAT (Claude Code)

        readonly List<ChatItem> _chatSnap = new();
        string _chatInput = "";
        int _chatSeen;
        bool _chatDetected, _authChecked;

        // ---------------------------------------------------------------- SETTINGS

        readonly HashSet<string> _open = new() { "GAME" };

        void DrawOptions(Vector2 a, Vector2 b)
        {
            PageHeader(a, "SETTINGS");
            ImGui.SetCursorScreenPos(new Vector2(a.X, a.Y + 60));
            ImGui.BeginChild("##settings", new Vector2(b.X - a.X, b.Y - a.Y - 60));
            float w = Math.Min(760, b.X - a.X - 30);
            ImGui.PushItemWidth(w - 260);

            if (Section("GAME"))
            {
                Row("Start in", () =>
                {
                    var scenes = new List<string> { "" };
                    scenes.AddRange(_jobs.Scenes);
                    Combo("##scene", scenes.ToArray(), _s.StartScene, v => _s.StartScene = v, SceneName);
                });
                Row("Resolution", () => Combo("##res", new[] { "1280x720", "1600x900", "1920x1080", "2560x1440", "3840x2160" },
                    _s.Resolution, v => _s.Resolution = v));
                Row("", () => Toggle("Fullscreen", () => _s.Fullscreen, v => _s.Fullscreen = v, "F11 toggles in game"));
                Row("", () => Toggle("Audio", () => _s.Audio, v => _s.Audio = v));
                Row("", () => Toggle("Online services", () => _s.Network, v => _s.Network = v, "Party, Relay and cloud saves"));
                Row("", () => Toggle("Pull before play", () => _s.PullBeforePlay, v => _s.PullBeforePlay = v));
                Row("", () => Toggle("Phone render path", () => _s.MobileRenderPath, v => _s.MobileRenderPath = v, "OpenGL ES 3.0, exactly as a phone renders"));
            }
            if (Section("SOURCE"))
            {
                Row("Repository", () => Text("##remote", "https://github.com/.../Cosmic-Shore.git", () => _s.RemoteUrl, v => _s.RemoteUrl = v));
                Row("Branch", BranchCombo);
                Row("GitHub token", () =>
                {
                    SecretField("##token", "optional", () => _s.GitHubToken, v => _s.GitHubToken = v);
                    Neon.Tooltip("Only if git has no GitHub sign-in. iOS builds need Actions: read & write.\nStored only on this PC.");
                });
                Row("Workspace", () =>
                    Segmented("wsmode", new[] { "OWN COPY", "BESIDE MY CLONE" }, (int)_s.Workspace, i => _s.Workspace = (WorkspaceMode)i, Neon.Cyan));
                if (_s.Workspace == WorkspaceMode.WorktreeOfMyClone)
                    Row("My clone", () => Text("##clone", "C:\\...\\Cosmic-Shore", () => _s.MyClonePath, v => _s.MyClonePath = v));
                Row("", () => { ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, Trim(_ws.Dir, 80)); ImGui.PopFont(); });
            }
            if (Section("LOOK")) DrawLookSettings();
            if (Section("CLAUDE"))
            {
                Row("Account", () =>
                {
                    if (_chat.SignedIn == null && !_authChecked) { _authChecked = true; Task.Run(_chat.RefreshAuth); }
                    if (SmallButton("SIGN IN", 110, _chat.Cli != null)) _chat.SignIn();
                    Neon.Tooltip("Sign in with your Claude account to use your Pro/Max plan.");
                    ImGui.SameLine(0, 8);
                    if (SmallButton("CHECK", 90, _chat.Cli != null)) Task.Run(_chat.RefreshAuth);
                    ImGui.SameLine(0, 12);
                    ImGui.PushFont(Neon.Small);
                    ImGui.TextColored(Neon.Dim, _chat.Cli == null ? "Claude Code not installed" : _chat.SignedIn switch { true => "signed in", false => "not signed in", _ => "" });
                    ImGui.PopFont();
                });
                Row("API key", () =>
                {
                    SecretField("##akey", "empty = use your Claude plan", () => _s.AnthropicApiKey, v => _s.AnthropicApiKey = v);
                    Neon.Tooltip("Only for pay-as-you-go API billing. When set it is used INSTEAD of your\nClaude Pro/Max plan. Passed only to the claude process; stored only on this PC.");
                });
                Row("Model", () => Text("##cmodel", "default", () => _s.ClaudeModel, v => _s.ClaudeModel = v));
                Row("CLI path", () => Text("##cpath", "auto-detect", () => _s.ClaudePath, v => _s.ClaudePath = v));
                Row("Milestone budget", () =>
                {
                    // Each milestone run stops at the first limit it reaches and leaves what it tried on the board.
                    int turns = _s.MilestoneMaxTurns, minutes = _s.MilestoneMaxMinutes;
                    float usd = (float)_s.MilestoneMaxUsd;
                    ImGui.PushItemWidth(110);
                    if (ImGui.InputInt("turns##mt", ref turns, 10)) { _s.MilestoneMaxTurns = Math.Clamp(turns, 1, 1000); _dirty = true; }
                    ImGui.SameLine(0, 14);
                    if (ImGui.InputInt("min##mm", ref minutes, 15)) { _s.MilestoneMaxMinutes = Math.Clamp(minutes, 0, 600); _dirty = true; }
                    ImGui.SameLine(0, 14);
                    if (ImGui.InputFloat("$##mu", ref usd, 1, 5, "%.0f")) { _s.MilestoneMaxUsd = Math.Clamp(usd, 0, 500); _dirty = true; }
                    ImGui.PopItemWidth();
                    Neon.Tooltip("Per milestone run: agentic turns, wall-clock minutes (0 = no limit) and dollars (0 = no cap;\nwith a Claude plan this is the run's nominal cost). When a run stops at a limit, Prisma puts\nwhat it tried on the BOARD and offers CONTINUE.");
                });
            }
            if (Section("ADVANCED"))
            {
                Row("Player arguments", () => Text("##extra", "--seed 42", () => _s.ExtraArgs, v => _s.ExtraArgs = v));
                Row("Profile", () => Text("##profile", "default", () => _s.Profile, v => _s.Profile = v));
                Row("", () => Toggle("Release build", () => _s.ReleaseBuild, v => _s.ReleaseBuild = v, "Off = Debug: slower, easier to debug"));
                Row("", () => Toggle("Verbose logs", () => _s.VerboseLogs, v => _s.VerboseLogs = v));
            }
            if (Section("TOOLCHAIN"))
            {
                StatusRow("git", _tools.Git != null, _tools.Git != null ? $"{_tools.GitVersion}" : "not found - install GitHub Desktop");
                StatusRow(".NET SDK", _tools.Dotnet != null, _tools.Dotnet != null ? _tools.DotnetSdk ?? "" : "installed automatically on START");
                if (OperatingSystem.IsWindows()) StatusRow("VC++ runtime", _tools.VcRuntime, _tools.VcRuntime ? "present" : "missing - aka.ms/vs/17/release/vc_redist.x64.exe");
                ImGui.Dummy(new Vector2(0, 4));
                if (SmallButton("RESCAN", 120, !_jobs.Busy)) Task.Run(() => { _tools.Detect(_s); _jobs.RefreshLocalState(); });
                ImGui.SameLine();
                if (SmallButton("INSTALL .NET", 160, !_jobs.Busy && _tools.Dotnet == null)) _jobs.InstallDotnet();
                ImGui.SameLine();
                if (SmallButton("DATA FOLDER", 160, true)) OpenFolder(LauncherSettings.DataDir);
            }
            if (Section("ABOUT"))
            {
                DrawAboutVersions();
                ImGui.Dummy(new Vector2(0, 8));
                ImGui.PushFont(Neon.Small);
                ImGui.TextColored(Neon.Ink, "Prisma v0.1  -  Froglet Inc.");
                ImGui.TextColored(Neon.Dim, "Cosmic Shore's own C# on our own renderer, physics, UI, audio and netcode.");
                ImGui.TextColored(Neon.Dim, "Dear ImGui, Silk.NET (MIT)  ·  Chakra Petch, Aldrich (OFL)  ·  Roboto Mono (Apache 2.0)");
                ImGui.PopFont();
            }
            ImGui.PopItemWidth();
            ImGui.EndChild();
        }

        /// <summary>"MinigameDuelForCellMultiplayer_Gameplay" -> "Duel For Cell Multiplayer".</summary>
        static string SceneName(string s)
        {
            if (s == "") return "Default (Bootstrap)";
            var n = s.Replace("_Gameplay", "").Replace("Minigame", "").Replace("_", " ");
            n = System.Text.RegularExpressions.Regex.Replace(n, "(?<=[a-z])(?=[A-Z])", " ").Trim();
            return n.Length == 0 ? s : n;
        }

        bool Section(string name)
        {
            var dl = ImGui.GetWindowDrawList();
            ImGui.Dummy(new Vector2(0, 6));
            var p = ImGui.GetCursorScreenPos();
            float w = Math.Min(760, ImGui.GetContentRegionAvail().X - 10);
            bool open = _open.Contains(name);
            if (ImGui.InvisibleButton("sec" + name, new Vector2(w, 36))) { if (!open) _open.Add(name); else _open.Remove(name); open = !open; }
            bool hov = ImGui.IsItemHovered();
            if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var col = open ? Neon.Cyan : hov ? Neon.Ink : Neon.Dim;
            Neon.Chevron(dl, p + new Vector2(10, 18), open, Neon.U(col));
            dl.AddText(Neon.Heading, 20, p + new Vector2(28, 7), Neon.U(col), name);
            dl.AddLine(new Vector2(p.X, p.Y + 36), new Vector2(p.X + w, p.Y + 36), Neon.U(Neon.Cyan, open ? 0.35f : 0.12f), 1f);
            if (open) ImGui.Dummy(new Vector2(0, 8));
            return open;
        }

        void Row(string label, Action control)
        {
            var p = ImGui.GetCursorScreenPos();
            if (label.Length > 0)
            {
                ImGui.PushFont(Neon.Small);
                ImGui.GetWindowDrawList().AddText(p + new Vector2(28, 8), Neon.U(Neon.Dim), label);
                ImGui.PopFont();
            }
            ImGui.SetCursorScreenPos(p + new Vector2(220, 0));
            ImGui.BeginGroup();
            control();
            ImGui.EndGroup();
        }

        bool Disclosure(string id, string label, ref bool open)
        {
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            ImGui.PushFont(Neon.Small);
            var ts = ImGui.CalcTextSize(label);
            ImGui.PopFont();
            if (ImGui.InvisibleButton(id, new Vector2(ts.X + 26, 24))) open = !open;
            bool hov = ImGui.IsItemHovered();
            if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var col = hov || open ? Neon.Ink : Neon.Dim;
            Neon.Chevron(dl, p + new Vector2(6, 12), open, Neon.U(col));
            dl.AddText(Neon.Small, 15, p + new Vector2(20, 3), Neon.U(col), label);
            return open;
        }

        /// <summary>A macOS segmented control: one rounded track, the chosen segment raised and tinted.</summary>
        void Segmented(string id, string[] items, int current, Action<int> set, Vector4 accent)
        {
            var dl = ImGui.GetWindowDrawList();
            ImGui.PushFont(Neon.Small);
            float h = 30, pad = 18;
            var widths = items.Select(i => ImGui.CalcTextSize(i).X + pad * 2).ToArray();
            var a0 = ImGui.GetCursorScreenPos();
            var total = new Vector2(widths.Sum() + 4, h + 4);
            dl.AddRectFilled(a0, a0 + total, Neon.U(new Vector4(0.10f, 0.11f, 0.17f, 1f), 0.85f), 9);
            dl.AddRect(a0, a0 + total, Neon.U(Neon.Ink, 0.08f), 9);
            float x = a0.X + 2;
            for (int i = 0; i < items.Length; i++)
            {
                var a = new Vector2(x, a0.Y + 2); var sz = new Vector2(widths[i], h);
                ImGui.SetCursorScreenPos(a);
                if (ImGui.InvisibleButton(id + i, sz) && i != current) { set(i); _dirty = true; }
                bool hov = ImGui.IsItemHovered(), on = i == current;
                if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (on)
                {
                    dl.AddRectFilled(a + new Vector2(0, 1), a + sz + new Vector2(0, 1), Neon.U(new Vector4(0, 0, 0, 1), 0.3f), 7);
                    dl.AddRectFilled(a, a + sz, Neon.U(Neon.Mix(new Vector4(0.22f, 0.24f, 0.32f, 1f), accent, 0.35f)), 7);
                }
                else if (i > 0 && i - 1 != current)
                    dl.AddLine(new Vector2(a.X, a.Y + 7), new Vector2(a.X, a.Y + h - 7), Neon.U(Neon.Ink, 0.12f));
                var ts = ImGui.CalcTextSize(items[i]);
                dl.AddText(a + new Vector2((sz.X - ts.X) * 0.5f, (h - ts.Y) * 0.5f), Neon.U(on || hov ? Neon.Ink : Neon.Dim), items[i]);
                x += widths[i];
            }
            ImGui.SetCursorScreenPos(a0);
            ImGui.Dummy(total);
            ImGui.PopFont();
        }

        void StatusRow(string name, bool ok, string detail)
        {
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            dl.AddCircleFilled(p + new Vector2(34, 12), 5, Neon.U(ok ? Neon.Lime : Neon.Amber));
            ImGui.SetCursorScreenPos(p + new Vector2(48, 0));
            ImGui.TextColored(Neon.Ink, name);
            ImGui.SameLine(220);
            ImGui.PushFont(Neon.Small);
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 2);
            ImGui.TextColored(Neon.Dim, detail);
            ImGui.PopFont();
        }

        // ---------------------------------------------------------------- CONSOLE

        void DrawConsole(Vector2 a, Vector2 b)
        {
            PageHeader(a, "CONSOLE");
            ImGui.SetCursorScreenPos(new Vector2(b.X - 300, a.Y + 4));
            if (SmallButton("COPY", 90, true)) ImGui.SetClipboardText(_jobs.Log.AllText());
            ImGui.SameLine();
            if (SmallButton("CLEAR", 90, true)) _jobs.Log.Clear();
            ImGui.SameLine();
            ImGui.Checkbox("follow", ref _autoScroll);

            var la = new Vector2(a.X, a.Y + 60);
            var dl = ImGui.GetWindowDrawList();
            Neon.ChamferFill(dl, la, b, 10, Neon.U(Neon.Space0, 0.75f));
            ImGui.SetCursorScreenPos(la + new Vector2(16, 12));
            ImGui.PushFont(Neon.Mono);
            ImGui.BeginChild("##log", b - la - new Vector2(32, 24));
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
                        ImGui.TextColored(Neon.Mix(Neon.Dim, Neon.Space0, 0.3f), l.Time.ToString("HH:mm:ss"));
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
            var a = new Vector2(RailW, size.Y - 40);
            var b = new Vector2(size.X, size.Y);
            var col = _jobs.Busy ? Neon.Magenta : _jobs.LastOk == false ? Neon.Red : _jobs.GameRunning ? Neon.Lime : Neon.Cyan;
            dl.AddRectFilled(a, b, Neon.U(Neon.Space0, 0.85f));
            dl.AddLine(a, new Vector2(b.X, a.Y), Neon.U(col, 0.35f), 1f);
            dl.AddCircleFilled(a + new Vector2(22, 20), 4.5f, Neon.U(col, 0.6f + 0.4f * MathF.Sin(Neon.Time * 3f)));

            string state = _jobs.Busy ? _jobs.Stage
                : _jobs.GameRunning ? "Game running"
                : _jobs.LastOk == false ? "Stopped - see console"
                : _jobs.LastOk == true ? "Done" : "Ready";
            dl.AddText(Neon.Small, 15, a + new Vector2(36, 11), Neon.U(Neon.Ink), Trim(state, 60));
            ImGui.PushFont(Neon.Small);
            float sw = ImGui.CalcTextSize(Trim(state, 60)).X;
            ImGui.PopFont();
            var last = _jobs.Busy ? _jobs.Log.LastLine : "";
            if (!string.IsNullOrEmpty(last))
                dl.AddText(Neon.Mono, 13, a + new Vector2(52 + sw, 13), Neon.U(Neon.Dim), Trim(last, 90));

            if (_jobs.Busy)
            {
                Neon.Progress(dl, new Vector2(b.X - 420, a.Y + 11), new Vector2(b.X - 120, b.Y - 11), _jobs.Progress, col);
                ImGui.SetCursorScreenPos(new Vector2(b.X - 108, a.Y + 6));
                if (Neon.Button("cancel", "CANCEL", new Vector2(96, 28), Neon.Red, Neon.Small, 15)) _jobs.Cancel();
            }
            else if (_jobs.LastOk == false && _page != Page.Console)
            {
                ImGui.SetCursorScreenPos(new Vector2(b.X - 148, a.Y + 6));
                if (Neon.Button("seelog", "CONSOLE", new Vector2(136, 28), Neon.Red, Neon.Small, 15)) _page = Page.Console;
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

        /// <summary>A macOS switch: a pill track that fills with the accent, a white knob that glides.</summary>
        void Toggle(string label, Func<bool> get, Action<bool> set, string? tip = null)
        {
            bool v = get();
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            var size = new Vector2(40, 22);
            if (ImGui.InvisibleButton("##t" + label, new Vector2(size.X + 12 + ImGui.CalcTextSize(label).X, size.Y))) { v = !v; set(v); _dirty = true; }
            bool hov = ImGui.IsItemHovered();
            if (hov) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (tip != null) Neon.Tooltip(tip);
            var id = ImGui.GetID("##t" + label);
            _switchPos.TryGetValue(id, out float k);
            k += ((v ? 1f : 0f) - k) * Math.Min(1f, ImGui.GetIO().DeltaTime * 16f);
            _switchPos[id] = k;
            var track = Neon.Mix(new Vector4(0.24f, 0.25f, 0.32f, 1f), Neon.Cyan, k);
            dl.AddRectFilled(p, p + size, Neon.U(track, 0.95f), size.Y * 0.5f);
            if (k > 0.5f) dl.AddRect(p - new Vector2(1), p + size + new Vector2(1), Neon.U(Neon.Cyan, 0.25f * k), size.Y * 0.5f, ImDrawFlags.None, 2f);
            var knob = new Vector2(p.X + 11 + k * (size.X - 22), p.Y + size.Y * 0.5f);
            dl.AddCircleFilled(knob + new Vector2(0, 1), 9, Neon.U(new Vector4(0, 0, 0, 1), 0.25f), 24);
            dl.AddCircleFilled(knob, 9, Neon.U(new Vector4(1, 1, 1, 1), hov ? 1f : 0.96f), 24);
            dl.AddText(p + new Vector2(size.X + 12, 2), Neon.U(v ? Neon.Ink : Neon.Dim), label);
        }

        readonly Dictionary<uint, float> _switchPos = new();

        void Text(string id, string hint, Func<string> get, Action<string> set)
        {
            var v = get() ?? "";
            if (ImGui.InputTextWithHint(id, hint, ref v, 512)) { set(v); _dirty = true; }
        }

        readonly HashSet<string> _revealed = new();

        /// <summary>A key/token field shown as dots, with SHOW / HIDE to check what was pasted.</summary>
        void SecretField(string id, string hint, Func<string?> get, Action<string> set, float width = 360)
        {
            bool shown = _revealed.Contains(id);
            var v = get() ?? "";
            ImGui.PushItemWidth(width);
            if (ImGui.InputTextWithHint(id, hint, ref v, 256, shown ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password)) { set(v); _dirty = true; }
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 8);
            ImGui.PushID(id);
            if (SmallButton(shown ? "HIDE" : "SHOW", 70, v.Length > 0)) { if (shown) _revealed.Remove(id); else _revealed.Add(id); }
            ImGui.PopID();
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

        static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception) { /* no browser */ }
        }

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
