using System;
using Android.App;
using Android.Content.PM;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using CosmicShore.Engine;
using Screen = CosmicShore.Engine.Screen;
using ScreenOrientation = Android.Content.PM.ScreenOrientation;
using SilkWindow = Silk.NET.Windowing.Window; // Activity.Window property shadows the type

namespace CosmicShore.Client.Android
{
    /// <summary>
    /// Android head for the playable port — the same RaceWindow / FreestyleWindow
    /// presentation hosts as desktop, on an SDL view with a GLES 3.0 context.
    ///
    /// Default mode is the SkimRace (touch: the game's REAL dual-thumb scheme via
    /// <see cref="SdlTouchBackend"/>; a Bluetooth gamepad uses the same ported
    /// GamepadInputStrategy as desktop). Launch extras pick the mode/config:
    ///
    ///   adb shell am start -n studio.froglet.cosmicshore.port/.MainActivity \
    ///       -e mode freestyle -e seed 7 -e crystals 20 -e rivals 2
    ///
    /// (The Name property pins the manifest activity name — without it the binding
    /// generator emits a crc64 namespace hash, which would break this command on
    /// any namespace change.)
    /// </summary>
    [Activity(Name = "studio.froglet.cosmicshore.port.MainActivity",
        Label = "Cosmic Shore", MainLauncher = true, Immersive = true,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize |
                               ConfigChanges.ScreenLayout | ConfigChanges.KeyboardHidden |
                               ConfigChanges.Keyboard | ConfigChanges.Navigation,
        Theme = "@android:style/Theme.NoTitleBar.Fullscreen")]
    public class MainActivity : SilkActivity
    {
        protected override void OnRun()
        {
            // Engine platform shims before anything reads them: the ported
            // TouchInputStrategy sizes its virtual thumbsticks from Screen.dpi
            // (one inch, exactly like the Unity build on device).
            SystemInfo.deviceType = DeviceType.Handheld;
            var metrics = Resources.DisplayMetrics;
            Screen.dpi = (int)metrics.DensityDpi;
            Screen.width = Math.Max(metrics.WidthPixels, metrics.HeightPixels);  // sensor landscape
            Screen.height = Math.Min(metrics.WidthPixels, metrics.HeightPixels);

            string mode = Intent?.GetStringExtra("mode")?.ToLowerInvariant() ?? "race";
            int seed = IntExtra("seed", 42);
            int crystals = IntExtra("crystals", 30);
            int rivals = IntExtra("rivals", 3);

            var options = ViewOptions.Default with
            {
                API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability,
                    ContextFlags.Default, new APIVersion(3, 0)),
                VSync = true,
            };
            var view = SilkWindow.GetView(options);

            // Host hooks subscribe BEFORE the window class adds its own, so each Update
            // tick pumps fingers into the EnhancedTouch shim first (multicast delegate
            // order = subscription order) and Screen tracks the real surface size.
            var touch = new SdlTouchBackend();
            view.Load += () => SyncScreen(view);
            view.Resize += _ => SyncScreen(view);
            view.Update += _ => touch.Pump();

            if (mode == "freestyle")
                new FreestyleWindow(seed, null, 0).Run(view);
            else
                new RaceWindow(seed, crystals, rivals, null, 0).Run(view);
        }

        static void SyncScreen(IView view)
        {
            // Touch normalization + HUD math key off these; framebuffer size is the
            // surface SDL reports finger coordinates against.
            Screen.width = Math.Max(8, view.FramebufferSize.X);
            Screen.height = Math.Max(8, view.FramebufferSize.Y);
        }

        int IntExtra(string key, int fallback)
        {
            string raw = Intent?.GetStringExtra(key);
            return int.TryParse(raw, out int value) ? value : fallback;
        }
    }
}
