using System;
using System.Diagnostics;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Scenes;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Render;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace CosmicShore.Client
{
    /// <summary>
    /// `--mode game`: the port running the Unity project's OWN content. Boots the
    /// content runtime over the repository's Assets/ tree, loads a scene from the build
    /// list (default: Menu_Main) through the real SceneManager, ticks the engine, and
    /// draws through CosmicShore.Render (linear color, sRGB textures, the uGUI tree).
    /// </summary>
    public sealed class GameHostWindow
    {
        readonly string _scene;
        readonly string _screenshotPath;
        readonly int _screenshotFrame;
        readonly int _width, _height;
        readonly bool _scripts;
        readonly bool _boot;

        IWindow _window;
        GL _gl;
        GameLoop _loop;
        ContentRuntime _runtime;
        TextureCache _textures;
        FrameTarget _frame;
        UguiRenderer _ui;
        TmpTextRenderer _tmp;
        PresentPass _present;
        int _frameIndex;
        readonly Stopwatch _clock = new();

        public GameHostWindow(string scene, int width, int height, bool scripts, bool boot, string screenshotPath, int screenshotFrame)
        {
            _scene = scene;
            _width = width;
            _height = height;
            _scripts = scripts;
            _boot = boot;
            _screenshotPath = screenshotPath;
            _screenshotFrame = screenshotFrame;
        }

        public void Run()
        {
            var options = WindowOptions.Default with
            {
                Size = new Vector2D<int>(_width, _height),
                Title = "Cosmic Shore",
                VSync = true,
                PreferredStencilBufferBits = 8,
            };
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
            _loop?.Dispose();
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

            var root = AssetDatabase.FindProjectRoot()
                ?? throw new InvalidOperationException("Unity project not found — run from inside the repository (or set COSMIC_SHORE_PROJECT).");
            var sw = Stopwatch.StartNew();
            _loop = new GameLoop("Boot");
            _runtime = new ContentRuntime(root, new[] { typeof(CosmicShore.Utility.GameDataSO).Assembly },
                new InstantiateOptions
                {
                    IncludeScript = t => _scripts || t.Namespace?.StartsWith("CosmicShore.Engine", StringComparison.Ordinal) == true,
                });
            _runtime.Install();
            _tmp.Fonts = _runtime.Fonts;
            if (_boot) _runtime.BootRootScopes();
            SceneManager.LoadScene(_scene);
            var load = _runtime.Loads.LastOrDefault().result;
            Console.WriteLine($"[game] {_scene} loaded in {sw.ElapsedMilliseconds} ms — {load?.GameObjects} GameObjects, {load?.Components} components"
                + (load != null && load.MissingScripts.Count > 0 ? $", {load.MissingScripts.Values.Sum()} components with no ported script" : ""));
            _clock.Start();
        }

        void OnUpdate(double dt)
        {
            float step = _screenshotPath != null ? 1f / 60f : (float)Math.Min(dt, 0.1);
            _loop.Tick(step);
        }

        void OnRender(double dt)
        {
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            if (w <= 0 || h <= 0) return;
            Screen.width = w;
            Screen.height = h;
            _frame.Ensure(w, h);
            _frame.Bind();

            var bg = CameraBackground();
            _gl.ClearColor(bg.X, bg.Y, bg.Z, 1f);
            _gl.ClearStencil(0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            _ui.Render(w, h);
            _present.Draw(_frame.Color, w, h);

            _frameIndex++;
            if (_screenshotPath != null && _frameIndex == _screenshotFrame)
            {
                Capture(_screenshotPath, w, h);
                Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}, ui draws {_ui.DrawCalls}+{_tmp.DrawCalls} text, graphics {_ui.GraphicsDrawn}, texts {_tmp.TextsDrawn}, layouts {_tmp.Layouts}");
                _window.Close();
            }
        }

        System.Numerics.Vector3 CameraBackground()
        {
            var cam = Camera.main;
            if (cam == null) return new System.Numerics.Vector3(0f, 0f, 0f);
            var c = cam.backgroundColor;
            return new System.Numerics.Vector3(CosmicShore.Render.ColorSpace.ToLinear(c.r), CosmicShore.Render.ColorSpace.ToLinear(c.g), CosmicShore.Render.ColorSpace.ToLinear(c.b));
        }

        unsafe void Capture(string path, int w, int h)
        {
            var pixels = new byte[w * h * 4];
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            fixed (byte* p = pixels)
                _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            MiniPng.Write(path, pixels, w, h, flipY: true);
        }
    }
}
