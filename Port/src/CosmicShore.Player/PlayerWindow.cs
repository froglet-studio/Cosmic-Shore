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
        UguiRenderer _ui;
        TmpTextRenderer _tmp;
        PresentPass _present;
        SilkInputBridge _inputBridge;
        int _frameIndex;

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

            _inputBridge = new SilkInputBridge(_window);
            _script.EnsureDevices();
            _boot = new PlayerBoot();
            _boot.Start(_scene);
            _tmp.Fonts = _boot.Runtime.Fonts;
        }

        void OnUpdate(double dt)
        {
            float step = Scripted ? 1f / 60f : (float)Math.Min(dt, 0.1);
            _inputBridge.BeforeTick();
            _script.BeforeTick(_frameIndex);
            _boot.Loop.Tick(step);
            _inputBridge.AfterTick();
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
            if (_shots.TryGetValue(_frameIndex, out var path))
            {
                Capture(path, w, h);
                Console.WriteLine($"screenshot → {path} ({w}x{h}) frame {_frameIndex}");
            }
            if (Scripted && _frameIndex >= _lastFrame)
            {
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
