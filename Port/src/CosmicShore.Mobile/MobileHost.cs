using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Player;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace CosmicShore.Mobile
{
    /// <summary>
    /// The phone player: the same <see cref="PlayerWindow"/> the desktop build runs (boot scene 0,
    /// tick, draw), on a full-screen OpenGL ES 3.0 view, reading the build's packaged player data,
    /// with touch as its input. Each platform's entry point (Android activity, iOS app) only works
    /// out where the data and the writable folder are, then calls <see cref="Run"/>.
    /// </summary>
    public static class MobileHost
    {
        /// <param name="shots">Frame → screenshot path (a desktop dry run of the phone host captures with these).</param>
        /// <param name="lastFrame">Close after this frame; -1 runs until the app is closed.</param>
        public static void Run(RuntimePlatform platform, string dataRoot, string persistentPath,
            SortedDictionary<int, string> shots = null, int lastFrame = -1)
        {
            Console.WriteLine($"[mobile] {platform}: data {dataRoot}, saves {persistentPath}");
            Environment.SetEnvironmentVariable("COSMIC_SHORE_PROJECT", dataRoot);
            // LAN party sessions live in the app's own sandbox (a phone has no shared folder).
            Environment.SetEnvironmentVariable("COSMIC_SHORE_NET_DIR", System.IO.Path.Combine(persistentPath, "sessions"));
            Application.platform = platform;
            Application.persistentDataPathOverride = persistentPath;
            SystemInfo.deviceType = DeviceType.Handheld;   // the game's InputController picks touch on a handheld

            Silk.NET.Windowing.Sdl.SdlWindowing.Use();
            Silk.NET.Input.Sdl.SdlInput.Use();
            var options = ViewOptions.Default with
            {
                API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 0)),
                VSync = true,
                PreferredDepthBufferBits = 24,
                PreferredStencilBufferBits = 8,
            };
            var view = Window.GetView(options);
            // Silk's SDL view reads SDL_GetError after its own calls and throws on ANY pending error
            // string, including a harmless "not supported" left by an unrelated query (a swap-interval
            // mode the driver lacks). Nothing here acts on SDL errors, so each frame starts clean —
            // subscribed first, so it runs before the player's own handlers.
            var sdl = Silk.NET.SDL.Sdl.GetApi();
            view.Update += _ => sdl.ClearError();
            view.Render += _ => sdl.ClearError();

            TouchBridge touch = null;
            var player = new PlayerWindow(null, 0, 0, shots ?? new SortedDictionary<int, string>(), lastFrame, new InputScript())
            {
                OnInput = v => touch = new TouchBridge(v),
                BeforeTick = step => touch?.BeforeTick(step),
            };
            try { player.RunOn(view); }
            finally { touch?.Dispose(); }
        }
    }
}
