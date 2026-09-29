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
        readonly string _screenshotPath;
        readonly int _screenshotFrame;
        readonly int _width, _height;

        IWindow _window;
        GL _gl;
        PlayerBoot _boot;
        TextureCache _textures;
        FrameTarget _frame;
        UguiRenderer _ui;
        TmpTextRenderer _tmp;
        PresentPass _present;
        int _frameIndex;

        public PlayerWindow(string scene, int width, int height, string screenshotPath, int screenshotFrame)
        {
            _scene = scene;
            _width = width;
            _height = height;
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
                PreferredDepthBufferBits = 24,
            };
            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Run();
            _boot?.Dispose();
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

            _boot = new PlayerBoot();
            _boot.Start(_scene);
            _tmp.Fonts = _boot.Runtime.Fonts;
        }

        void OnUpdate(double dt)
        {
            float step = _screenshotPath != null ? 1f / 60f : (float)Math.Min(dt, 0.1);
            _boot.Loop.Tick(step);
        }

        void OnRender(double dt)
        {
            int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
            if (w <= 0 || h <= 0) return;
            Screen.width = w;
            Screen.height = h;
            _frame.Ensure(w, h);
            _frame.Bind();

            var cam = Camera.main;
            var c = cam != null ? cam.backgroundColor : Color.black;
            _gl.ClearColor(ColorSpace.ToLinear(c.r), ColorSpace.ToLinear(c.g), ColorSpace.ToLinear(c.b), 1f);
            _gl.ClearStencil(0);
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

            _ui.Render(w, h);
            _present.Draw(_frame.Color, w, h);

            _frameIndex++;
            if (_screenshotPath != null && _frameIndex == _screenshotFrame)
            {
                Capture(_screenshotPath, w, h);
                Console.WriteLine($"screenshot → {_screenshotPath} ({w}x{h}) frame {_frameIndex}");
                _boot.Log.PrintSummary();
                _window.Close();
            }
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
