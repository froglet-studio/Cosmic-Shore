using System;

namespace CosmicShore.Launcher
{
    public static class Program
    {
        /// <summary>
        /// A single-file .exe unpacks its native libraries (GLFW, cimgui) into a temp folder that
        /// .NET's own DllImport probes but Silk.NET's loader does not. Loading GLFW from there first
        /// makes Silk's later load-by-name find the copy already in the process.
        /// </summary>
        static void PreloadBundledNatives()
        {
            var dirs = (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string ?? "")
                .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            string[] names = OperatingSystem.IsWindows() ? new[] { "glfw3.dll" }
                : OperatingSystem.IsMacOS() ? new[] { "libglfw.3.dylib" } : new[] { "libglfw.so.3", "libglfw.so" };
            foreach (var d in dirs)
                foreach (var n in names)
                {
                    var path = System.IO.Path.Combine(d, n);
                    if (System.IO.File.Exists(path) && System.Runtime.InteropServices.NativeLibrary.TryLoad(path, out _)) return;
                }
        }

        /// <summary>
        /// FrogletLauncher [--page play|build|options|console] [--screenshot out.png --frames N] [--offline] [--auto play|update|android|ios|claude-install|launcher-update:REV] [--updated FROM]
        /// The flags exist for docs and tests: a scripted run renders N frames, saves a screenshot and exits.
        /// </summary>
        [STAThread]
        public static int Main(string[] args)
        {
            string? shot = null, page = null;
            int frames = 0;
            bool offline = false;
            string? auto = null, updatedFrom = null, installTo = null;
            int waitPid = 0;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--screenshot" when i + 1 < args.Length: shot = args[++i]; break;
                    case "--frames" when i + 1 < args.Length: int.TryParse(args[++i], out frames); break;
                    case "--page" when i + 1 < args.Length: page = args[++i]; break;
                    case "--offline": offline = true; break;
                    case "--auto" when i + 1 < args.Length: auto = args[++i]; break;
                    case "--updated" when i + 1 < args.Length: updatedFrom = args[++i]; break;
                    case "--install-to" when i + 1 < args.Length: installTo = args[++i]; break;
                    case "--wait" when i + 1 < args.Length: int.TryParse(args[++i], out waitPid); break;
                }
            }
            if (installTo != null) return LauncherUpdater.FinishInstall(installTo, waitPid, updatedFrom); // no window: swap and relaunch
            if (shot != null && frames <= 0) frames = 90;
            PreloadBundledNatives();
            new LauncherApp(new LauncherApp.Args(shot, frames, page, offline, auto, updatedFrom)).Run();
            return 0;
        }
    }
}
