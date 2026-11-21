using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CosmicShore.Build
{
    /// <summary>Runs the platform tools a build hands off to (dotnet, the Android SDK installer).</summary>
    public static class Toolchain
    {
        /// <summary>The repository's port sources (the mobile player project lives there).</summary>
        public static string MobileProject(string project)
        {
            var path = Path.Combine(project, "Port", "src", "CosmicShore.Mobile", "CosmicShore.Mobile.csproj");
            if (!File.Exists(path)) throw new BuildException($"mobile player project not found at {path}");
            return path;
        }

        public static string Dotnet()
        {
            var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrEmpty(root))
            {
                var exe = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
                if (File.Exists(exe)) return exe;
            }
            return "dotnet";
        }

        /// <summary>Runs a tool with its output streamed to the console; returns the exit code.</summary>
        public static int Run(string file, IEnumerable<string> args, string? workingDirectory = null, IDictionary<string, string>? env = null)
        {
            var psi = new ProcessStartInfo(file) { UseShellExecute = false, WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory };
            foreach (var a in args) psi.ArgumentList.Add(a);
            if (env != null) foreach (var (k, v) in env) psi.Environment[k] = v;
            Console.WriteLine("> " + file + " " + string.Join(" ", psi.ArgumentList.Select(Quote)));
            using var p = Process.Start(psi) ?? throw new BuildException($"could not start {file}");
            p.WaitForExit();
            return p.ExitCode;
        }

        /// <summary>Runs a tool and captures its standard output.</summary>
        public static (int Code, string Output) Capture(string file, params string[] args)
        {
            var psi = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            try
            {
                using var p = Process.Start(psi)!;
                string output = p.StandardOutput.ReadToEnd();
                p.StandardError.ReadToEnd();
                p.WaitForExit();
                return (p.ExitCode, output);
            }
            catch (System.ComponentModel.Win32Exception) { return (-1, ""); }
        }

        static string Quote(string a) => a.Contains(' ') ? "\"" + a + "\"" : a;

        /// <summary>Makes sure a .NET SDK workload is installed (android / ios), installing it when it is not.</summary>
        public static void EnsureWorkload(string name)
        {
            var (code, list) = Capture(Dotnet(), "workload", "list");
            if (code == 0 && list.Split('\n').Any(l => l.TrimStart().StartsWith(name + " ", StringComparison.Ordinal))) return;
            Console.WriteLine($"installing the .NET '{name}' workload (one time)...");
            if (Run(Dotnet(), new[] { "workload", "install", name }) != 0)
                throw new BuildException($"could not install the .NET '{name}' workload. Run `dotnet workload install {name}` "
                    + (OperatingSystem.IsWindows() ? "from an Administrator terminal" : "with sudo") + ", then build again.");
        }

        /// <summary>A file a Git LFS clone has not fetched (a pointer, not the binary).</summary>
        public static bool IsRealFile(string path) => File.Exists(path) && !PlayerDataBuilder.IsLfsPointer(path);

        /// <summary>Writes the player data and its archive (data.pak + data.hash) for a build.</summary>
        public static PlayerDataBuilder.Report StagePlayerData(PlayerDataBuilder builder, string dataDir, string? pakDir)
        {
            var report = builder.Build(dataDir, Console.WriteLine);
            if (pakDir != null)
            {
                Directory.CreateDirectory(pakDir);
                Console.WriteLine("packing player data...");
                PlayerDataBuilder.Pack(dataDir, Path.Combine(pakDir, "data.pak"));
                File.WriteAllText(Path.Combine(pakDir, "data.hash"), report.ContentHash);
            }
            return report;
        }
    }
}
