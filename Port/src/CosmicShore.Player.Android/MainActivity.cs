using System;
using System.IO;
using System.IO.Compression;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.Android;
using CosmicShore.Engine;
using Screen = CosmicShore.Engine.Screen;
using ScreenOrientation = Android.Content.PM.ScreenOrientation;
using Environment = System.Environment;
using SilkWindow = Silk.NET.Windowing.Window; // Activity.Window shadows the type

[assembly: UsesPermission(Android.Manifest.Permission.Internet)]
[assembly: UsesPermission(Android.Manifest.Permission.AccessNetworkState)]
[assembly: UsesPermission(Android.Manifest.Permission.Vibrate)]
[assembly: UsesFeature(GLESVersion = 0x00030002, Required = true)]

namespace CosmicShore.Player
{
    /// <summary>
    /// The Android head of the port's player: the same <see cref="PlayerWindow"/> the desktop runs
    /// (boot build scene 0, let the game's own AppManager/SceneLoader drive everything after), on
    /// the SDL surface <see cref="SilkActivity"/> owns, with an OpenGL ES 3.2 context.
    ///
    /// Three things are Android's own and live here: the project content (an APK asset, unpacked
    /// once into private storage — see <see cref="EnsureContent"/>), FMOD's Java half (FMOD.init
    /// must run with a Context before the Studio runtime starts), and the platform identity the
    /// game reads (handheld device, Android platform, real DPI — the touch strategy sizes its
    /// thumbsticks off Screen.dpi, exactly as the Unity build does on device).
    ///
    /// The application id differs from every other Cosmic Shore build (Unity's
    /// com.FrogletGames.TailGlider, the July port's studio.froglet.cosmicshore.port), so all of
    /// them install side by side. The Name below pins the manifest activity name so
    /// <c>adb shell am start -n studio.froglet.cosmicshore.engine/studio.froglet.cosmicshore.engine.MainActivity</c>
    /// keeps working whatever the C# namespace becomes.
    /// </summary>
    [Activity(Name = "studio.froglet.cosmicshore.engine.MainActivity",
        Label = "Cosmic Shore (Engine)", MainLauncher = true, Immersive = true,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize |
                               ConfigChanges.ScreenLayout | ConfigChanges.KeyboardHidden |
                               ConfigChanges.Keyboard | ConfigChanges.Navigation | ConfigChanges.UiMode,
        Theme = "@android:style/Theme.NoTitleBar.Fullscreen")]
    public class MainActivity : SilkActivity
    {
        static bool s_fmodJavaUp;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            // FMOD's Java side needs the Context before libfmod initialises (its Android output
            // and asset access go through it). Done on the UI thread, where the app class loader
            // resolves org.fmod.FMOD; the SDL thread that runs OnRun cannot see app classes.
            try
            {
                var cls = JNIEnv.FindClass("org/fmod/FMOD");
                var init = JNIEnv.GetStaticMethodID(cls, "init", "(Landroid/content/Context;)V");
                JNIEnv.CallStaticVoidMethod(cls, init, new JValue(this));
                JNIEnv.DeleteGlobalRef(cls);
                s_fmodJavaUp = true;
            }
            catch (Exception e)
            {
                Android.Util.Log.Warn("CosmicShore", "FMOD Java init failed - audio will be silent: " + e.Message);
            }
            base.OnCreate(savedInstanceState);
        }

        protected override void OnDestroy()
        {
            if (s_fmodJavaUp)
            {
                try
                {
                    var cls = JNIEnv.FindClass("org/fmod/FMOD");
                    var close = JNIEnv.GetStaticMethodID(cls, "close", "()V");
                    JNIEnv.CallStaticVoidMethod(cls, close);
                    JNIEnv.DeleteGlobalRef(cls);
                }
                catch (Exception) { }
            }
            base.OnDestroy();
        }

        protected override void OnRun()
        {
            Console.SetOut(new LogcatWriter("CosmicShore"));
            Console.SetError(new LogcatWriter("CosmicShore"));

            // Platform identity, before anything reads it.
            SystemInfo.deviceType = DeviceType.Handheld;
            CosmicShore.Engine.Application.platform = RuntimePlatform.Android;
            var metrics = Resources.DisplayMetrics;
            Screen.dpi = metrics.Xdpi > 0 ? (metrics.Xdpi + metrics.Ydpi) * 0.5f : (float)metrics.DensityDpi;
            Screen.width = Math.Max(metrics.WidthPixels, metrics.HeightPixels);   // sensor landscape
            Screen.height = Math.Min(metrics.WidthPixels, metrics.HeightPixels);

            string project = EnsureContent();
            Environment.SetEnvironmentVariable("COSMIC_SHORE_PROJECT", project);
            // The FMOD runtime is packaged as APK native libraries; PlayerAudio loads them by soname.
            Environment.SetEnvironmentVariable("COSMIC_SHORE_FMOD_LIB", s_fmodJavaUp ? "libfmodstudio.so" : null);
            if (!s_fmodJavaUp) Environment.SetEnvironmentVariable("COSMIC_SHORE_AUDIO", "off");

            AndroidSdl.ConfigureHints();
            var options = ViewOptions.Default with
            {
                // Compatability is the profile the July head was device-verified with (SDL maps the ES
                // API to an ES context whatever the profile says); 3.2 is what the renderer needs.
                API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 2)),
                VSync = true,
                PreferredDepthBufferBits = 24,
                PreferredStencilBufferBits = 8,
            };
            var view = SilkWindow.GetView(options);

            // Host hooks subscribe BEFORE PlayerWindow adds its own, so each Update pumps the
            // fingers into the Input System and drives the soft keyboard before the engine ticks.
            var touch = new AndroidTouchBridge();
            var keyboard = new SoftKeyboardBridge();
            view.Load += () => { SyncScreen(view); touch.Install(); };
            view.Resize += _ => SyncScreen(view);
            view.FramebufferResize += _ => SyncScreen(view);
            view.Update += _ => { touch.Pump(); keyboard.Pump(); };

            Toast(project == null ? "Cosmic Shore: content missing from this build" : "Loading Cosmic Shore...");
            var player = new PlayerWindow(null, Screen.width, Screen.height,
                new System.Collections.Generic.SortedDictionary<int, string>(), -1, new InputScript());
            player.Run(view);
        }

        static void SyncScreen(IView view)
        {
            Screen.width = Math.Max(8, view.FramebufferSize.X);
            Screen.height = Math.Max(8, view.FramebufferSize.Y);
        }

        void Toast(string text)
            => RunOnUiThread(() => Android.Widget.Toast.MakeText(this, text, ToastLength.Long)?.Show());

        /// <summary>
        /// Unpacks the APK's content pack (tools/pack_android_content.py) into private storage the
        /// first time this build runs, and returns the project root the content bridge reads. A
        /// content.version stamp beside the tree makes every later launch a single file read; a
        /// new build with different content replaces the old tree.
        /// </summary>
        string EnsureContent()
        {
            string version;
            try
            {
                using var vs = Assets.Open("content.version");
                using var vr = new StreamReader(vs);
                version = vr.ReadToEnd().Trim();
            }
            catch (Exception e)
            {
                Console.WriteLine("[android] no content pack in this APK: " + e.Message);
                return null;
            }

            string baseDir = Path.Combine(FilesDir.AbsolutePath, "content");
            string root = Path.Combine(baseDir, version);
            string stamp = Path.Combine(root, ".complete");
            if (File.Exists(stamp)) return root;

            Toast("First launch: unpacking game content...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, recursive: true);   // stale builds
            Directory.CreateDirectory(root);
            string zipPath = Path.Combine(CacheDir.AbsolutePath, "content.zip");
            using (var src = Assets.Open("content.zip"))
            using (var dst = File.Create(zipPath))
                src.CopyTo(dst, 1 << 20);
            ZipFile.ExtractToDirectory(zipPath, root, overwriteFiles: true);
            File.Delete(zipPath);
            File.WriteAllText(stamp, version);
            Console.WriteLine($"[android] content {version} unpacked in {sw.ElapsedMilliseconds} ms");
            return root;
        }
    }

    /// <summary>Console → logcat, line by line (adb logcat -s CosmicShore).</summary>
    sealed class LogcatWriter : TextWriter
    {
        readonly string _tag;
        readonly System.Text.StringBuilder _line = new();
        public LogcatWriter(string tag) { _tag = tag; }
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_line)
            {
                if (value == '\n') Flush();
                else if (value != '\r') _line.Append(value);
            }
        }

        public override void Write(string value)
        {
            if (value == null) return;
            foreach (var c in value) Write(c);
        }

        public override void Flush()
        {
            lock (_line)
            {
                if (_line.Length == 0) return;
                Android.Util.Log.Info(_tag, _line.ToString());
                _line.Clear();
            }
        }
    }
}
