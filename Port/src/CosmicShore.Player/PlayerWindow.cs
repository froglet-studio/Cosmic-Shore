using System;
using CosmicShore.Engine;
using CosmicShore.Render;
using ColorSpace = CosmicShore.Render.ColorSpace;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace CosmicShore.Player
{
    /// <summary>The windowed player: boot, tick, draw (the uGUI tree today; 3D joins it next).</summary>
    public sealed class PlayerWindow
    {
        readonly string _scene;
        readonly System.Collections.Generic.SortedDictionary<int, string> _shots;
        readonly int _lastFrame;
        readonly InputScript _script;
        readonly int _width, _height;

        IView _window;
        GL _gl;
        PlayerBoot _boot;
        TextureCache _textures;
        FrameTarget _frame;
        FrameTarget _scene3d;
        SceneRenderer _sceneRenderer;
        SkyboxPass _skybox;
        PostPass _post;
        readonly CosmicShore.Engine.Rendering.VolumeStack _volumes = new();
        UguiRenderer _ui;
        TmpTextRenderer _tmp;
        PresentPass _present;
        SilkInputBridge _inputBridge;
        int _frameIndex;

        /// <summary>A camera's off-screen target: the HDR scene it draws into and the post-processed result the UI samples.</summary>
        sealed class RtTarget { public FrameTarget Scene, Out; public int Frame = -1; }
        readonly System.Runtime.CompilerServices.ConditionalWeakTable<RenderTexture, RtTarget> _rt = new();
        readonly System.Collections.Generic.Dictionary<(int, int), PostPass> _rtPost = new();
        bool _inFrame;

        /// <param name="lastFrame">Close after this frame (-1 = run until the window closes).</param>
        public PlayerWindow(string scene, int width, int height,
            System.Collections.Generic.SortedDictionary<int, string> shots, int lastFrame, InputScript script)
        {
            _scene = scene;
            _width = width;
            _height = height;
            _shots = shots;
            _lastFrame = lastFrame;
            _script = script;
        }

        bool Scripted => _lastFrame >= 0;

        /// <summary>--render-from N: skip drawing before frame N (a test run ticks through menus fast, then renders).</summary>
        public static int RenderFrom;

        /// <summary>--fullscreen: open full-screen at the desktop's resolution (F11 still toggles).</summary>
        public static bool StartFullscreen;

        /// <summary>--check-shaders: link every project Shader Graph on this context, print the result, exit (code = failures).</summary>
        public static bool CheckShaders;

        /// <summary>--shader-gallery FRAME[:LEGEND]: lay out the compiled-graph gallery at FRAME (0 = off).</summary>
        public static int GalleryFrame;
        public static string GalleryLegend;

        /// <summary>
        /// --hidden: an invisible window (GL still runs). Frames render at <c>--size</c> through the
        /// control port's virtual-resolution target, so screenshots and <c>ui_sweep</c> work with
        /// nothing on the desktop to close.
        /// </summary>
        public static bool StartHidden;

        /// <summary>--position X,Y: the window's top-left on the desktop (null = the platform's choice).</summary>
        public static (int x, int y)? StartPosition;

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(_width, _height),
                Title = "Cosmic Shore",
                VSync = CosmicShore.Render.RenderQuality.VSync,
                FramesPerSecond = CosmicShore.Render.RenderQuality.VSync ? 0 : CosmicShore.Render.RenderQuality.TargetFps,
                PreferredStencilBufferBits = 8,
                PreferredDepthBufferBits = 24,
            };
            if (StartFullscreen) options = options with { WindowState = WindowState.Fullscreen };
            if (StartHidden) options = options with { IsVisible = false };
            if (StartPosition is { } pos) options = options with { Position = new Vector2D<int>(pos.x, pos.y) };
            // COSMIC_SHORE_GLES=1 runs the desktop player on an OpenGL ES 3.0 context — the exact
            // render path a phone takes, so the mobile build can be checked without one.
            if (Environment.GetEnvironmentVariable("COSMIC_SHORE_GLES") == "1")
                options = options with { API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 0)) };
            RunOn(Window.Create(options));
        }

        /// <summary>
        /// Runs the player on a view someone else created — a phone's full-screen GL ES surface
        /// (SDL's activity on Android, its UIKit app on iOS) instead of a desktop window.
        /// </summary>
        public void RunOn(IView view)
        {
            _window = view;
            if (view is IWindow titled) ModelViewer.SetTitle = t => titled.Title = t;
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
            SessionReport.Write("window closed");
            _boot?.Dispose();
            _inputBridge?.Dispose();
            Control?.Dispose();
        }

        /// <summary>Called after GL is up, before the game boots — a mobile host adds its touch backend here.</summary>
        public Action<IView> OnInput;

        /// <summary>Called every frame just before the engine ticks, with the step — a host's extra input backend samples here.</summary>
        public Action<float> BeforeTick;

        /// <summary>The control port (--control-port): commands from tools run here between frames.</summary>
        public ControlServer Control;

        void OnLoad()
        {
            _gl = GL.GetApi(_window);
            GlCaps.Detect(_gl);
            var dump = Environment.GetEnvironmentVariable("COSMIC_SHORE_DUMP_SHADERS");
            if (!string.IsNullOrEmpty(dump))
            {
                System.IO.Directory.CreateDirectory(dump);
                GlProgram.DumpSource = (name, src) => System.IO.File.WriteAllText(System.IO.Path.Combine(dump, name), src);
            }
            Screen.width = _window.FramebufferSize.X;
            Screen.height = _window.FramebufferSize.Y;
            _textures = new TextureCache(_gl);
            _frame = new FrameTarget(_gl);
            _ui = new UguiRenderer(_gl, _textures);
            _tmp = new TmpTextRenderer(_gl);
            _ui.Tmp = _tmp;
            _present = new PresentPass(_gl);
            _scene3d = new FrameTarget(_gl);
            _sceneRenderer = new SceneRenderer(_gl, _textures);
            if (CheckShaders)
            {
                Environment.ExitCode = ShaderCheck.Run((g, es) => GraphProgramCache.TryLink(_gl, g, es));
                _window.Close();
                return;
            }
            _skybox = new SkyboxPass(_gl);
            _post = new PostPass(_gl);
            if (SessionReport.Enabled && GpuTimer.Supported) _gpu = new GpuTimer(_gl, "textures", "clear", "sky", "collect", "post", "ui", "present", "opaque", "transparent");
            _textures.External = t => t is RenderTexture rt && _rt.TryGetValue(rt, out var target) ? target.Out.Color : 0u;
            Camera.RenderRequested = cam => { if (cam != null && cam.targetTexture != null) RenderToTexture(cam, force: true); };

            _inputBridge = new SilkInputBridge(_window);
            OnInput?.Invoke(_window);
            _script.EnsureDevices();
            // This device draws Entities Graphics entities (SceneRenderer's entity pass), so the
            // game's Entities Graphics support probe passes as it does on a desktop GPU. A
            // headless run never gets here and stays on the MeshRenderer path, like -nographics.
            SystemInfo.supportsComputeShaders = true;
            if (Control != null) { Control.Quit = () => _window.Close(); Control.FrameMs = () => _lastFrameMs; }
            if (StartHidden && Control != null) Control.VirtualSize = (_width, _height);
            _boot = new PlayerBoot { NoScene = ModelViewer.Path != null };
            SessionReport.Log = _boot.Log;
            SessionReport.Frame = () => _frameIndex;
            _boot.Start(_scene);
            _tmp.Fonts = _boot.Runtime.Fonts;
        }

        double _titleAt;

        /// <summary>
        /// The window title doubles as a network stats monitor: with several players tiled on one
        /// desktop it says which one is which and how its link is doing (docs/MULTIPLAYER.md §6.4).
        /// </summary>
        void UpdateNetTitle()
        {
            if (_window is not IWindow w || ModelViewer.Path != null) return;
            double now = Environment.TickCount64 / 1000.0;
            if (now - _titleAt < 1.0) return;
            _titleAt = now;
            var title = CosmicShore.Engine.Networking.NetStats.Title(Environment.GetEnvironmentVariable("COSMIC_SHORE_PROFILE"));
            if (w.Title != title) w.Title = title;
        }

        void OnUpdate(double dt)
        {
            float step = Scripted ? 1f / 60f : (float)Math.Min(dt, 0.1);
            _inputBridge.BeforeTick();
            BeforeTick?.Invoke(step);
            _script.BeforeTick(_frameIndex);
            if (GalleryFrame > 0 && _frameIndex == GalleryFrame) ShaderGallery.Build(GalleryLegend);
            if (ModelViewer.Path != null)
            {
                if (_frameIndex == 1) ModelViewer.Build();
                ModelViewer.Tick(step);
            }
            Control?.BeforeTick(_frameIndex);
            UpdateNetTitle();
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            CosmicShore.Engine.GameLoop.PhaseTiming = s_timing || SessionReport.Enabled;
            _boot.Tick(step);
            ParityRun.AfterTick(_frameIndex);
            SessionReport.SimTime(System.Diagnostics.Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
            if (s_timing && _frameIndex % 30 == 0)
                Console.WriteLine($"[tick] simulation {(System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F1} ms (frame {_frameIndex})"
                    + $" | avg/30: {CosmicShore.Engine.GameLoop.Current?.TakePhaseReport(30)} | {GcReport()}");
            _inputBridge.AfterTick();
        }

        int _gc0, _gc1, _gc2; TimeSpan _gcPause;

        /// <summary>GC activity since the last report: collections per generation and pause ms per frame.</summary>
        string GcReport()
        {
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var pause = GC.GetTotalPauseDuration();
            string r = $"gc {g0 - _gc0}/{g1 - _gc1}/{g2 - _gc2} pause {(pause - _gcPause).TotalMilliseconds / 30:F1}/f heap {GC.GetTotalMemory(false) >> 20} MB";
            _gc0 = g0; _gc1 = g1; _gc2 = g2; _gcPause = pause;
            return r;
        }

        double _lastFrameMs;
        // GPU time per render pass for the session report (desktop GL, --session-report only). A pass's
        // time is GPU wall time, so while the frame is CPU-bound it includes the GPU waiting for
        // the pass's commands: read it next to cpu.renderP50Ms.
        GpuTimer _gpu;
        Action<int> _gpuSceneSplit;
        static readonly bool s_timing = Environment.GetEnvironmentVariable("COSMIC_SHORE_RENDER_TIMING") == "1";
        readonly System.Diagnostics.Stopwatch _frameClock = System.Diagnostics.Stopwatch.StartNew();

        void OnRender(double dt)
        {
            _lastFrameMs = _frameClock.Elapsed.TotalMilliseconds;
            SessionReport.FrameTime(_lastFrameMs);
            _frameClock.Restart();
            int winW = _window.FramebufferSize.X, winH = _window.FramebufferSize.Y;
            if (winW <= 0 || winH <= 0) return;
            // The control port's `resize WxH`: the game sees (and captures) that resolution
            // whatever the desktop allows; the window shows it scaled.
            var vs = Control?.VirtualSize;
            int w = vs?.w ?? winW, h = vs?.h ?? winH;
            Screen.width = w;
            Screen.height = h;
            if (_frameIndex + 1 < RenderFrom && !_shots.ContainsKey(_frameIndex + 1) && !FrameRecorder.Wants(_frameIndex + 1) && Control is not { WantsFrame: true })
            {
                _frameIndex++;
                if (Scripted && _frameIndex >= _lastFrame)
                {
                    _boot.Log.PrintSummary();
                    _window.Close();
                }
                return;
            }
            long r0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (_gpu?.BeginFrame() is { } gpuMs) SessionReport.GpuTime(_gpu.Passes, gpuMs);
            _gpu?.Begin(0);
            _frame.Ensure(w, h);
            // Enabled cameras aimed at a RenderTexture draw every frame (the preview window, the
            // connecting panel's arena view) before the screen camera, as the original does.
            foreach (var cam in Camera.allCameras)
                if (cam.targetTexture != null) RenderToTexture(cam, force: false);
            _gpu?.Begin(1);
            Render3D(w, h);
            _gpu?.Begin(5);
            _frame.Bind();
            _gl.ClearStencil(0);
            _gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            _ui.Render(w, h);
            _gpu?.Begin(6);
            if (vs != null)
            {
                EnsureVirtual(w, h);
                _present.Draw(_frame.Color, w, h, _virtualFbo);
                _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _virtualFbo);
                _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
                _gl.BlitFramebuffer(0, 0, w, h, 0, 0, winW, winH, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            }
            else _present.Draw(_frame.Color, w, h);
            _gpu?.EndFrame();
            // CPU time spent issuing the frame's GL work (the GPU's own time needs timer queries).
            SessionReport.RenderTime(System.Diagnostics.Stopwatch.GetElapsedTime(r0).TotalMilliseconds);

            _frameIndex++;
            if (_shots.TryGetValue(_frameIndex, out var path))
            {
                Capture(path, w, h);
                Console.WriteLine($"screenshot → {path} ({w}x{h}) frame {_frameIndex} — scene {_sceneRenderer.DrawCalls} draws / {_sceneRenderer.Instances} instances, {_lastFrameMs:F0} ms/frame");
            }
            if (FrameRecorder.TryPath(_frameIndex, out var recPath))
                Capture(recPath, w, h);
            Control?.AfterPresent(p => Capture(p, w, h), w, h);
            if (Scripted && _frameIndex >= _lastFrame)
            {
                _boot.Log.PrintSummary();
                _window.Close();
            }
        }

        /// <summary>The camera's view into the HDR scene target, then the post stack into the UI frame.</summary>
        void Render3D(int w, int h)
        {
            // Render scale: the 3D view at a multiple of the window (supersampling above 1); the
            // post stack's composite resamples it to the window. MSAA as Unity's URP asset authors it.
            int sw = Math.Max(1, (int)MathF.Round(w * RenderQuality.RenderScale));
            int sh = Math.Max(1, (int)MathF.Round(h * RenderQuality.RenderScale));
            _scene3d.Ensure(sw, sh, RenderQuality.Msaa);
            _scene3d.Bind();
            var cam = ScreenCamera();
            var c = cam != null ? cam.backgroundColor : Color.black;
            _gl.ClearColor(ColorSpace.ToLinear(c.r), ColorSpace.ToLinear(c.g), ColorSpace.ToLinear(c.b), 1f);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            var post = new PostSettings();
            if (cam != null && cam.isActiveAndEnabled)
            {
                post = PostSettings.For(cam, _volumes);
                _gpu?.Begin(2);
                _skybox.Draw(cam);
                _gpu?.Begin(3);
                _sceneRenderer.GpuPass = _gpu == null ? null : _gpuSceneSplit ??= p => _gpu.Begin(7 + p);
                _sceneRenderer.Render(cam, sw, sh);
                _sceneRenderer.GpuPass = null;
                float tanY = MathF.Tan(cam.fieldOfView * 0.5f * MathF.PI / 180f);
                post.TanHalfFovY = tanY;
                post.TanHalfFovX = tanY * cam.aspect;
            }
            else post.Panini = false;
            _gpu?.Begin(4);
            _scene3d.Resolve();
            _post.Draw(_scene3d.Color, w, h, _frame.Fbo, post);
        }

        /// <summary>The camera that draws to the screen: Camera.main, else the deepest enabled camera with no target texture.</summary>
        static Camera ScreenCamera()
        {
            var main = Camera.main;
            if (main != null && main.targetTexture == null) return main;
            Camera best = null;
            foreach (var c in Camera.allCameras)
                if (c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
            return best;
        }

        /// <summary>Draw <paramref name="cam"/> into its target texture (skybox, scene, the gameplay post stack).</summary>
        void RenderToTexture(Camera cam, bool force)
        {
            var rt = cam.targetTexture;
            if (rt == null || rt.width <= 0 || rt.height <= 0 || _inFrame) return;
            var target = _rt.GetValue(rt, _ => new RtTarget { Scene = new FrameTarget(_gl), Out = new FrameTarget(_gl) });
            // One draw per camera per presented frame unless the game explicitly asks (Camera.Render()).
            if (!force && target.Frame == _frameIndex) return;
            target.Frame = _frameIndex;
            int w = rt.width, h = rt.height;
            target.Scene.Ensure(w, h, RenderQuality.Msaa);
            target.Out.Ensure(w, h);
            if (!_rtPost.TryGetValue((w, h), out var post)) _rtPost[(w, h)] = post = new PostPass(_gl);

            _inFrame = true;
            try
            {
                target.Scene.Bind();
                var c = cam.backgroundColor;
                _gl.ClearColor(ColorSpace.ToLinear(c.r), ColorSpace.ToLinear(c.g), ColorSpace.ToLinear(c.b), c.a);
                _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
                if (cam.clearFlags == CameraClearFlags.Skybox) _skybox.Draw(cam);
                _sceneRenderer.Render(cam, w, h);
                var settings = PostSettings.For(cam, _volumes);
                float tanY = MathF.Tan(cam.fieldOfView * 0.5f * MathF.PI / 180f);
                settings.TanHalfFovY = tanY;
                settings.TanHalfFovX = tanY * cam.aspect;
                target.Scene.Resolve();
                post.Draw(target.Scene.Color, w, h, target.Out.Fbo, settings);
                rt.MarkModified();
            }
            finally { _inFrame = false; }
        }

        uint _virtualFbo, _virtualColor;
        int _virtualW, _virtualH;

        /// <summary>The 8-bit sRGB frame at the virtual resolution (present target and read-back source).</summary>
        unsafe void EnsureVirtual(int w, int h)
        {
            if (_virtualFbo != 0 && w == _virtualW && h == _virtualH) return;
            if (_virtualFbo != 0) { _gl.DeleteFramebuffer(_virtualFbo); _gl.DeleteTexture(_virtualColor); }
            _virtualW = w; _virtualH = h;
            _virtualColor = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, _virtualColor);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _virtualFbo = _gl.GenFramebuffer();
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _virtualFbo);
            _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _virtualColor, 0);
        }

        unsafe void Capture(string path, int w, int h)
        {
            var pixels = new byte[w * h * 4];
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, Control?.VirtualSize != null && _virtualFbo != 0 ? _virtualFbo : 0u);
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            CosmicShore.Client.MiniPng.Write(path, pixels, w, h, flipY: true);
        }
    }
}
