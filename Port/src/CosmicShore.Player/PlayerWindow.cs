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

        IWindow _window;
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

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(_width, _height),
                Title = "Cosmic Shore",
                VSync = true,
                PreferredStencilBufferBits = 8,
                PreferredDepthBufferBits = 24,
            };
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
            _boot?.Dispose();
            _inputBridge?.Dispose();
        }

        void OnLoad()
        {
            _gl = GL.GetApi(_window);
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
            _skybox = new SkyboxPass(_gl);
            _post = new PostPass(_gl);
            _textures.External = t => t is RenderTexture rt && _rt.TryGetValue(rt, out var target) ? target.Out.Color : 0u;
            Camera.RenderRequested = cam => { if (cam != null && cam.targetTexture != null) RenderToTexture(cam, force: true); };

            _inputBridge = new SilkInputBridge(_window);
            _script.EnsureDevices();
            // This device draws Entities Graphics entities (SceneRenderer's entity pass), so the
            // game's Entities Graphics support probe passes as it does on a desktop GPU. A
            // headless run never gets here and stays on the MeshRenderer path, like -nographics.
            SystemInfo.supportsComputeShaders = true;
            _boot = new PlayerBoot();
            _boot.Start(_scene);
            _tmp.Fonts = _boot.Runtime.Fonts;
        }

        void OnUpdate(double dt)
        {
            float step = Scripted ? 1f / 60f : (float)Math.Min(dt, 0.1);
            _inputBridge.BeforeTick();
            _script.BeforeTick(_frameIndex);
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            _boot.Tick(step);
            if (s_timing && _frameIndex % 30 == 0)
                Console.WriteLine($"[tick] simulation {(System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F1} ms (frame {_frameIndex})");
            _inputBridge.AfterTick();
        }

        double _lastFrameMs;
        static readonly bool s_timing = Environment.GetEnvironmentVariable("COSMIC_SHORE_RENDER_TIMING") == "1";
        readonly System.Diagnostics.Stopwatch _frameClock = System.Diagnostics.Stopwatch.StartNew();

        void OnRender(double dt)
        {
            _lastFrameMs = _frameClock.Elapsed.TotalMilliseconds;
            _frameClock.Restart();
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            if (w <= 0 || h <= 0) return;
            Screen.width = w;
            Screen.height = h;
            if (_frameIndex + 1 < RenderFrom && !_shots.ContainsKey(_frameIndex + 1) && !FrameRecorder.Wants(_frameIndex + 1))
            {
                _frameIndex++;
                return;
            }
            _frame.Ensure(w, h);
            // Enabled cameras aimed at a RenderTexture draw every frame (the preview window, the
            // connecting panel's arena view) before the screen camera, as the original does.
            foreach (var cam in Camera.allCameras)
                if (cam.targetTexture != null) RenderToTexture(cam, force: false);
            Render3D(w, h);
            _frame.Bind();
            _gl.ClearStencil(0);
            _gl.Clear(ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            _ui.Render(w, h);
            _present.Draw(_frame.Color, w, h);

            _frameIndex++;
            if (_shots.TryGetValue(_frameIndex, out var path))
            {
                Capture(path, w, h);
                Console.WriteLine($"screenshot → {path} ({w}x{h}) frame {_frameIndex} — scene {_sceneRenderer.DrawCalls} draws / {_sceneRenderer.Instances} instances, {_lastFrameMs:F0} ms/frame");
            }
            if (FrameRecorder.TryPath(_frameIndex, out var recPath))
                Capture(recPath, w, h);
            if (Scripted && _frameIndex >= _lastFrame)
            {
                _boot.Log.PrintSummary();
                _window.Close();
            }
        }

        /// <summary>The camera's view into the HDR scene target, then the post stack into the UI frame.</summary>
        void Render3D(int w, int h)
        {
            _scene3d.Ensure(w, h);
            _scene3d.Bind();
            var cam = ScreenCamera();
            var c = cam != null ? cam.backgroundColor : Color.black;
            _gl.ClearColor(ColorSpace.ToLinear(c.r), ColorSpace.ToLinear(c.g), ColorSpace.ToLinear(c.b), 1f);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
            var post = new PostSettings();
            if (cam != null && cam.isActiveAndEnabled)
            {
                post = PostSettings.For(cam, _volumes);
                _skybox.Draw(cam);
                _sceneRenderer.Render(cam, w, h);
                float tanY = MathF.Tan(cam.fieldOfView * 0.5f * MathF.PI / 180f);
                post.TanHalfFovY = tanY;
                post.TanHalfFovX = tanY * cam.aspect;
            }
            else post.Panini = false;
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
            target.Scene.Ensure(w, h);
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
                post.Draw(target.Scene.Color, w, h, target.Out.Fbo, settings);
                rt.MarkModified();
            }
            finally { _inFrame = false; }
        }

        unsafe void Capture(string path, int w, int h)
        {
            var pixels = new byte[w * h * 4];
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            CosmicShore.Client.MiniPng.Write(path, pixels, w, h, flipY: true);
        }
    }
}
