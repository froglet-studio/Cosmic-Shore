using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FrogletTools > Amoebius > Launch Amoebius: opens Prisma (Froglet's own engine for Cosmic Shore,
    /// <c>Port/</c>) straight from the checkout this editor has open, so it is always the version
    /// the branch carries - no zip, no download. The first launch builds <c>Prisma.exe</c> from
    /// <c>Port/src/CosmicShore.Launcher</c> into <c>Library/Prisma</c>; later launches start it at
    /// once and rebuild only when the launcher's source changed (after a pull, say). Without a
    /// .NET 10 SDK on the machine it falls back to <c>Port/dist/Prisma-Windows.zip</c>, and Prisma
    /// installs its own SDK the first time START is pressed, after which builds work. Prisma is told
    /// which clone opened it, so its PLAY page follows the branch Unity has checked out. FrogletTools > Amoebius > Vessel
    /// Studio Page opens it on its VESSEL STUDIO page (<c>--page studios</c>); FrogletTools > Vessels > Vessel Studio
    /// (<c>Studios/VesselStudioWindow</c>, the studio home) opens the studio pages themselves through <see cref="OpenStudio"/>:
    /// built from this checkout and served by Amoebius (/vessel-studio D33), never the raw files.
    /// READER: writes only under the gitignored <c>Library/</c>, never assets - no ship panel.
    /// </summary>
    public static class LaunchPrisma
    {
        static string Root => Directory.GetParent(Application.dataPath)!.FullName;
        static string OutDir => Path.Combine(Root, "Library", "Prisma");
        static string ExeName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Prisma.exe" : "Prisma";
        static string Exe => Path.Combine(OutDir, ExeName);
        static string StampFile => Path.Combine(OutDir, "source.stamp");
        static string LauncherProject => Path.Combine(Root, "Port", "src", "CosmicShore.Launcher");

        static Process _build;
        static StringBuilder _buildLog;
        static string _buildStamp;
        static double _buildStarted;

        [MenuItem("FrogletTools/Amoebius/Launch Amoebius", false, 0)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 5,
            Description = "Opens Amoebius, our own engine, built from this checkout (rebuilt only when its source changed).",
            DocPath = "Port/docs/LAUNCHER.md")]
        public static void Launch()
        {
            if (_build != null) { EditorUtility.DisplayDialog("Amoebius", "Amoebius is still being built - it opens by itself when the build finishes.", "OK"); return; }
            if (!Directory.Exists(LauncherProject))
            {
                EditorUtility.DisplayDialog("Amoebius", "This checkout has no Port/src/CosmicShore.Launcher. Pull a branch that contains Amoebius (bleeding-edge does).", "OK");
                return;
            }
            string stamp = SourceStamp();
            if (File.Exists(Exe) && File.Exists(StampFile) && File.ReadAllText(StampFile).Trim() == stamp)
            {
                Start(Exe);
                return;
            }
            // A Prisma started from Library/Prisma keeps its .exe locked: close it before rebuilding.
            if (File.Exists(Exe) && !CloseRunningCopy()) return;

            string dotnet = FindDotnet10();
            if (dotnet == null)
            {
                UseZip();
                return;
            }
            Build(dotnet, stamp);
        }

        /// <summary>The Prisma page the next start opens (<c>--page</c>), then cleared. Null = Prisma's own default.</summary>
        static string _openPage;

        [MenuItem("FrogletTools/Amoebius/Vessel Studio Page", false, 3)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 2,
            Description = "Opens Amoebius on its VESSEL STUDIO page (pick a vessel; PLAY IN ENGINE runs the game's own vessel in Amoebius).",
            DocPath = "Port/docs/LAUNCHER.md")]
        public static void OpenAmoebiusStudiosPage()
        {
            _openPage = "studios";
            Launch();
        }

        /// <summary>
        /// A Vessel Studio page (<paramref name="file"/>: <c>index.html</c>, <c>stoat.html</c>, ...) the way every surface opens
        /// it (/vessel-studio D33): the build of THIS checkout's branch, served by Amoebius on 127.0.0.1, with Sync, Ask,
        /// Requests and Decisions working. An Amoebius already serving this checkout (its <c>&lt;data&gt;/studio/server.json</c>,
        /// process alive) opens it at once in an app window; otherwise Amoebius starts with <c>--page studios:&lt;file&gt;</c>
        /// and opens it itself. The home's cards and OPEN THE HUB use it.
        /// </summary>
        internal static void OpenStudio(string file)
        {
            string served = RunningStudioServer();
            if (served != null)
            {
                OpenStudioWindow(served + Uri.EscapeDataString(file) + "#amoebius");
                return;
            }
            _openPage = "studios:" + file;
            Launch();
        }

        [Serializable]
        class StudioServerFile { public int pid; public int port; public string @base; public string root; }

        /// <summary>The base URL of an Amoebius studio server already serving this checkout, else null.</summary>
        static string RunningStudioServer()
        {
            try
            {
                string data = Environment.GetEnvironmentVariable("PRISMA_DATA_DIR");
                if (string.IsNullOrEmpty(data))
                    data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prisma");
                string path = Path.Combine(data, "studio", "server.json");
                if (!File.Exists(path)) return null;
                var s = JsonUtility.FromJson<StudioServerFile>(File.ReadAllText(path));
                if (s == null || s.port <= 0 || string.IsNullOrEmpty(s.@base) || !s.@base.StartsWith("http://127.0.0.1:", StringComparison.Ordinal)) return null;
                if (!SamePath(s.root, Root)) return null;   // serving another checkout: start one for this branch
                using (var p = Process.GetProcessById(s.pid)) if (p.HasExited) return null;
                return s.@base;
            }
            catch (Exception) { return null; }   // no process with that id, unreadable file: start Amoebius
        }

        static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            string Norm(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var cmp = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(Norm(a), Norm(b), cmp);
        }

        /// <summary>
        /// A served studio URL as its own app window (Edge, always on Windows 10/11, or Chrome: <c>--app</c>, no tabs or
        /// address bar), with a window profile under <c>Library/</c> so the studio's layout is remembered; the default
        /// browser when neither is installed. Amoebius's OPEN IN AMOEBIUS does the same (StudioCatalog.AppWindowArgs).
        /// </summary>
        static void OpenStudioWindow(string url)
        {
            string profile = Path.Combine(Root, "Library", "VesselStudioWindow");
            foreach (var exe in AppBrowserCandidates())
            {
                if (!File.Exists(exe)) continue;
                try
                {
                    Process.Start(new ProcessStartInfo(exe,
                        $"--app=\"{url}\" --user-data-dir=\"{profile}\" --window-size=1600,960 --no-first-run --no-default-browser-check")
                        { UseShellExecute = false });
                    return;
                }
                catch (Exception) { /* try the next browser */ }
            }
            Application.OpenURL(url);
        }

        static string[] AppBrowserCandidates()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var roots = new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("LOCALAPPDATA") }
                    .Where(r => !string.IsNullOrEmpty(r));
                return roots.SelectMany(r => new[]
                {
                    Path.Combine(r, "Microsoft", "Edge", "Application", "msedge.exe"),
                    Path.Combine(r, "Google", "Chrome", "Application", "chrome.exe"),
                }).ToArray();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new[] { "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge", "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" };
            return new[] { "/usr/bin/microsoft-edge", "/usr/bin/google-chrome", "/usr/bin/chromium", "/usr/bin/chromium-browser" };
        }

        [MenuItem("FrogletTools/Amoebius/Rebuild Amoebius", false, 1)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 2,
            Description = "Builds Amoebius from this checkout again even if its source looks unchanged, then opens it.",
            DocPath = "Port/docs/LAUNCHER.md")]
        public static void Rebuild()
        {
            try { if (File.Exists(StampFile)) File.Delete(StampFile); } catch (IOException) { }
            Launch();
        }

        [MenuItem("FrogletTools/Amoebius/Show Amoebius Folder", false, 2)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 1,
            Description = "Reveals Library/Prisma, where the built Prisma.exe lives.")]
        public static void Reveal()
        {
            Directory.CreateDirectory(OutDir);
            EditorUtility.RevealInFinder(File.Exists(Exe) ? Exe : OutDir);
        }

        /// <summary>
        /// What the launcher is built from: every source file's path, size and write time. A pull
        /// that changes any of them changes the stamp, so the next launch rebuilds.
        /// </summary>
        static string SourceStamp()
        {
            var roots = new[] { LauncherProject, Path.Combine(Root, "Port", "src", "Shared") };
            var files = roots.Where(Directory.Exists)
                .SelectMany(r => Directory.EnumerateFiles(r, "*", SearchOption.AllDirectories))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                            !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                .Append(Path.Combine(Root, "Port", "Directory.Build.props"))
                .Where(File.Exists)
                .OrderBy(f => f, StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (var f in files)
            {
                var info = new FileInfo(f);
                sb.Append(f.Substring(Root.Length)).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
            }
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", "");
        }

        /// <summary>A dotnet with a .NET 10 SDK: DOTNET_ROOT, PATH, the standard install, or the private one Prisma installs.</summary>
        static string FindDotnet10()
        {
            string name = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var candidates = new[]
                {
                    Environment.GetEnvironmentVariable("DOTNET_ROOT"),
                    Path.Combine(local, "Prisma", "dotnet"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                    "/usr/local/share/dotnet", "/usr/share/dotnet",
                }
                .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                .Where(d => !string.IsNullOrEmpty(d))
                .Select(d => Path.Combine(d, name))
                .Where(File.Exists)
                .Distinct();
            foreach (var c in candidates)
            {
                var sdks = Run(c, "--list-sdks", 20000);
                if (sdks != null && sdks.Split('\n').Any(l => l.TrimStart().StartsWith("10.", StringComparison.Ordinal))) return c;
            }
            return null;
        }

        static string Run(string file, string args, int timeoutMs)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                });
                string o = p!.StandardOutput.ReadToEnd();
                return p.WaitForExit(timeoutMs) && p.ExitCode == 0 ? o : null;
            }
            catch (Exception) { return null; }
        }

        static void Build(string dotnet, string stamp)
        {
            Directory.CreateDirectory(OutDir);
            string rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? (RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64")
                : "linux-x64";
            var psi = new ProcessStartInfo(dotnet,
                $"publish \"{LauncherProject}\" -c Release -r {rid} --self-contained -p:PublishSingleFile=true " +
                $"-p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o \"{OutDir}\" -nologo")
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                WorkingDirectory = Root,
            };
            psi.EnvironmentVariables["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            psi.EnvironmentVariables["DOTNET_NOLOGO"] = "1";
            _buildLog = new StringBuilder();
            _buildStamp = stamp;
            _buildStarted = EditorApplication.timeSinceStartup;
            try
            {
                _build = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _build.OutputDataReceived += (_, e) => { if (e.Data != null) lock (_buildLog) _buildLog.AppendLine(e.Data); };
                _build.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (_buildLog) _buildLog.AppendLine(e.Data); };
                _build.Start();
                _build.BeginOutputReadLine();
                _build.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                _build = null;
                Debug.LogError($"[Amoebius] Could not start the build with {dotnet}: {e.Message}");
                return;
            }
            EditorApplication.update += WatchBuild;
        }

        /// <summary>Shows the build's progress without blocking the editor, then opens Prisma (or says why not).</summary>
        static void WatchBuild()
        {
            if (_build == null) { EditorApplication.update -= WatchBuild; return; }
            double secs = EditorApplication.timeSinceStartup - _buildStarted;
            if (!_build.HasExited)
            {
                // The first build restores packages (a minute or two); later ones take seconds.
                if (EditorUtility.DisplayCancelableProgressBar("Building Amoebius",
                        $"Building Prisma.exe from this checkout ({(int)secs}s)... the first build takes a minute or two.",
                        (float)(1 - Math.Exp(-secs / 60.0)) * 0.95f))
                {
                    try { _build.Kill(); } catch (InvalidOperationException) { }
                    Finish();
                    Debug.LogWarning("[Amoebius] Build cancelled.");
                }
                return;
            }
            _build.WaitForExit();
            int code = _build.ExitCode;
            Finish();
            if (code == 0 && File.Exists(Exe))
            {
                File.WriteAllText(StampFile, _buildStamp);
                // The build also leaves its pre-rename copy and symbols; the folder keeps only Prisma.
                foreach (var extra in Directory.GetFiles(OutDir, "FrogletLauncher*").Concat(Directory.GetFiles(OutDir, "*.pdb")))
                    try { File.Delete(extra); } catch (IOException) { }
                Debug.Log($"[Amoebius] Built from this checkout in {(int)secs}s - opening it.");
                Start(Exe);
                return;
            }
            var logPath = Path.Combine(OutDir, "build.log");
            lock (_buildLog) File.WriteAllText(logPath, _buildLog.ToString());
            string firstError;
            lock (_buildLog) firstError = _buildLog.ToString().Split('\n').FirstOrDefault(l => l.Contains(" error ")) ?? $"dotnet exited with {code}";
            Debug.LogError($"[Amoebius] The build failed: {firstError.Trim()}  (full log: {logPath})");
            if (File.Exists(Exe) && EditorUtility.DisplayDialog("Amoebius", "Building Amoebius from this checkout failed (see the Console). Open the copy built earlier?", "Open it", "Cancel"))
                Start(Exe);
        }

        static void Finish()
        {
            EditorApplication.update -= WatchBuild;
            EditorUtility.ClearProgressBar();
            _build?.Dispose();
            _build = null;
        }

        /// <summary>No .NET 10 SDK yet: run the Prisma.exe shipped in Port/dist (it installs its own SDK on its first START).</summary>
        static void UseZip()
        {
            var zip = Path.Combine(Root, "Port", "dist", "Prisma-Windows.zip");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(zip))
            {
                EditorUtility.DisplayDialog("Amoebius", "Building Amoebius needs the .NET 10 SDK, and there is no ready-made copy for this machine.\n\n" +
                    "Install the .NET 10 SDK (dotnet.microsoft.com), then launch again.", "OK");
                return;
            }
            Directory.CreateDirectory(OutDir);
            using (var z = ZipFile.OpenRead(zip))
            {
                var entry = z.Entries.FirstOrDefault(e => e.Name.Equals("Prisma.exe", StringComparison.OrdinalIgnoreCase));
                if (entry == null) { Debug.LogError($"[Amoebius] {zip} holds no Prisma.exe."); return; }
                entry.ExtractToFile(Exe, overwrite: true);
            }
            // No stamp: once Prisma has installed its SDK, the next launch builds from source.
            try { if (File.Exists(StampFile)) File.Delete(StampFile); } catch (IOException) { }
            Debug.Log("[Amoebius] No .NET 10 SDK found - opened the ready-made copy from Port/dist. Press START in Amoebius once; " +
                      "it installs its own SDK, and later launches build from this checkout.");
            Start(Exe);
        }

        /// <summary>Asks to close a Prisma started from Library/Prisma, which holds its .exe open. Returns false when the user declines.</summary>
        static bool CloseRunningCopy()
        {
            var running = Process.GetProcessesByName("Prisma").Where(p =>
            {
                try { return string.Equals(Path.GetFullPath(p.MainModule!.FileName), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase); }
                catch (Exception) { return false; }
            }).ToList();
            if (running.Count == 0) return true;
            if (!EditorUtility.DisplayDialog("Amoebius", "Amoebius's source changed since it was built, and the old Amoebius is still open.\n\nClose it and rebuild?", "Close and rebuild", "Cancel"))
                return false;
            foreach (var p in running)
            {
                try { p.CloseMainWindow(); if (!p.WaitForExit(5000)) p.Kill(); } catch (Exception) { }
            }
            return true;
        }

        static void Start(string exe)
        {
            try
            {
                // --clone: Prisma's PLAY follows the branch this checkout is on (and says when it plays another).
                // --page: a menu item that opens a particular page (Vessel Studio -> STUDIOS) asks for it once.
                string args = $"--clone \"{Root}\"" + (_openPage != null ? $" --page {_openPage}" : "");
                _openPage = null;
                Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
            }
            catch (Exception e) { Debug.LogError($"[Amoebius] Could not start {exe}: {e.Message}"); }
        }
    }
}
