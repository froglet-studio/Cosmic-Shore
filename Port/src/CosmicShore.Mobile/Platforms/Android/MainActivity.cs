using System;
using System.IO;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using CosmicShore.Engine;
using Silk.NET.Windowing.Sdl.Android;

namespace CosmicShore.Mobile
{
    /// <summary>
    /// The Android entry point (Unity's UnityPlayerActivity): a full-screen landscape SDL activity
    /// that unpacks the build's player data on first launch, brings up FMOD's Java side, and runs
    /// the game. Package name, label and version come from the project's Player Settings via cs-build.
    /// </summary>
    [Activity(
        MainLauncher = true,
        Theme = "@android:style/Theme.NoTitleBar.Fullscreen",
        ScreenOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape,
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden
            | ConfigChanges.Keyboard | ConfigChanges.Navigation | ConfigChanges.ScreenLayout | ConfigChanges.UiMode)]
    public sealed class MainActivity : SilkActivity
    {
        protected override void OnRun()
        {
            var files = FilesDir!.AbsolutePath;
            var data = PlayerDataInstaller.Install(name => Assets!.Open(name), Path.Combine(files, "PlayerData"),
                CacheDir!.AbsolutePath, Console.WriteLine);
            InitFmodJava();
            MobileHost.Run(RuntimePlatform.Android, data, Path.Combine(files, "save"));
        }

        /// <summary>FMOD's Android runtime needs its Java half initialised with the app context (org.fmod.FMOD.init).</summary>
        void InitFmodJava()
        {
            try
            {
                Java.Lang.JavaSystem.LoadLibrary("fmod");
                IntPtr cls = JNIEnv.FindClass("org/fmod/FMOD");
                IntPtr init = JNIEnv.GetStaticMethodID(cls, "init", "(Landroid/content/Context;)Z");
                bool ok = JNIEnv.CallStaticBooleanMethod(cls, init, new JValue(this));
                Console.WriteLine($"[fmod] org.fmod.FMOD.init → {ok}");
            }
            catch (Exception e)
            {
                Console.WriteLine("[fmod] FMOD Java init unavailable — audio stays silent: " + e.Message);
            }
        }
    }
}
