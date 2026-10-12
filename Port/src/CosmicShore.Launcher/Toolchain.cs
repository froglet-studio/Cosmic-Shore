using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Finds the two tools the launcher drives - git and the .NET SDK - wherever a tester is
    /// likely to have them, and installs the SDK privately when it is missing. Nothing here
    /// needs admin rights: the SDK goes into the launcher's own data folder.
    /// </summary>
    public sealed class Toolchain
    {
        public const int RequiredDotnetMajor = 10;

        public string? Git { get; private set; }
        public string? GitVersion { get; private set; }
        public bool GitLfs { get; private set; }
        public string? Dotnet { get; private set; }
        public string? DotnetSdk { get; private set; }
        public bool VcRuntime { get; private set; } = true;
        /// <summary>Blender / Maya (mayapy), which Unity and cs-asset need for .blend and .ma/.mb models; null when not found.</summary>
        public string? Blender { get; private set; }
        public string? BlenderVersion { get; private set; }
        public string? MayaPy { get; private set; }
        public string? MayaVersion { get; private set; }

        public bool Ready => Git != null && Dotnet != null;

        static bool Windows => OperatingSystem.IsWindows();
        static string Exe(string name) => Windows ? name + ".exe" : name;

        public static string LocalDotnetDir => Path.Combine(LauncherSettings.DataDir, "dotnet");

        public void Detect(LauncherSettings s)
        {
            Git = FindGit(s.GitPath);
            GitVersion = Git == null ? null : ProcessRunner.Capture(Git, "--version")?.Replace("git version ", "");
            GitLfs = Git != null && ProcessRunner.Capture(Git, "lfs", "version") != null;
            (Dotnet, DotnetSdk) = FindDotnet(s.DotnetPath);
            if (Windows)
                VcRuntime = File.Exists(Path.Combine(Environment.SystemDirectory, "vcruntime140.dll"));
        }

        /// <summary>
        /// Blender and Maya: SETTINGS' paths go out as PRISMA_BLENDER / PRISMA_MAYAPY to every process
        /// Prisma starts (cs-asset converts .blend/.ma/.mb through them), then the same search cs-asset does.
        /// Separate from <see cref="Detect"/>: asking Blender its version takes a second or more.
        /// </summary>
        public void DetectDcc(LauncherSettings s)
        {
            Environment.SetEnvironmentVariable("PRISMA_BLENDER", string.IsNullOrWhiteSpace(s.BlenderPath) ? null : s.BlenderPath.Trim());
            Environment.SetEnvironmentVariable("PRISMA_MAYAPY", string.IsNullOrWhiteSpace(s.MayaPyPath) ? null : s.MayaPyPath.Trim());
            Blender = Prisma.DccLocator.FindBlender();
            BlenderVersion = Blender == null ? null : Prisma.DccLocator.BlenderVersion(Blender);
            MayaPy = Prisma.DccLocator.FindMayaPy();
            MayaVersion = MayaPy == null ? null : Prisma.DccLocator.MayaVersion(MayaPy);
        }

        static string? FindGit(string configured)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
            candidates.Add("git");
            if (Windows)
            {
                // GitHub Desktop ships its own git; most of our testers have that and nothing else.
                var gh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktop");
                if (Directory.Exists(gh))
                    candidates.AddRange(Directory.GetDirectories(gh, "app-*")
                        .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                        .Select(d => Path.Combine(d, "resources", "app", "git", "cmd", "git.exe")));
                foreach (var pf in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
                    candidates.Add(Path.Combine(Environment.GetFolderPath(pf), "Git", "cmd", "git.exe"));
            }
            else
            {
                candidates.Add("/usr/bin/git");
                candidates.Add("/opt/homebrew/bin/git");
                candidates.Add("/usr/local/bin/git");
            }
            foreach (var c in candidates)
            {
                if (c != "git" && !File.Exists(c)) continue;
                if (ProcessRunner.Capture(c, "--version") != null) return c;
            }
            return null;
        }

        static (string?, string?) FindDotnet(string configured)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
            candidates.Add(Path.Combine(LocalDotnetDir, Exe("dotnet")));
            var root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!string.IsNullOrEmpty(root)) candidates.Add(Path.Combine(root, Exe("dotnet")));
            candidates.Add("dotnet");
            if (Windows) candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"));
            else { candidates.Add("/usr/share/dotnet/dotnet"); candidates.Add("/usr/local/share/dotnet/dotnet"); candidates.Add("/opt/dotnet/dotnet"); }

            foreach (var c in candidates)
            {
                if (c != "dotnet" && !File.Exists(c)) continue;
                var sdks = ProcessRunner.Capture(c, "--list-sdks");
                if (sdks == null) continue;
                var best = sdks.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Split(' ')[0].Trim())
                    .Where(v => int.TryParse(v.Split('.')[0], out var major) && major >= RequiredDotnetMajor)
                    .OrderByDescending(v => v, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (best != null) return (c, best);
            }
            return (null, null);
        }

        /// <summary>The environment every dotnet child process needs, so a private SDK is found by the build's own sub-processes too.</summary>
        public Dictionary<string, string> DotnetEnv()
        {
            var env = new Dictionary<string, string>();
            if (Dotnet == null || Dotnet == "dotnet") return env;
            var dir = Path.GetDirectoryName(Path.GetFullPath(Dotnet))!;
            env["DOTNET_ROOT"] = dir;
            env["PATH"] = dir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            env["DOTNET_MULTILEVEL_LOOKUP"] = "0";
            return env;
        }

        /// <summary>Installs the .NET SDK into the launcher's own folder with Microsoft's official install script.</summary>
        public async Task<bool> InstallDotnet(LogBuffer log, CancellationToken ct)
        {
            Directory.CreateDirectory(LocalDotnetDir);
            string script = Path.Combine(LauncherSettings.DataDir, Windows ? "dotnet-install.ps1" : "dotnet-install.sh");
            string url = Windows ? "https://dot.net/v1/dotnet-install.ps1" : "https://dot.net/v1/dotnet-install.sh";
            log.Add(LogKind.Info, $"Downloading the official .NET install script ({url})");
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                await File.WriteAllBytesAsync(script, await http.GetByteArrayAsync(url, ct), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.Add(LogKind.Error, "Download failed: " + ex.Message);
                return false;
            }

            log.Add(LogKind.Info, $"Installing .NET {RequiredDotnetMajor} SDK into {LocalDotnetDir} (about 250 MB, once)");
            ProcessRunner.Result r = Windows
                ? await ProcessRunner.Run("powershell", new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
                    "-Channel", $"{RequiredDotnetMajor}.0", "-InstallDir", LocalDotnetDir }, null, log, ct)
                : await ProcessRunner.Run("bash", new[] { script, "--channel", $"{RequiredDotnetMajor}.0", "--install-dir", LocalDotnetDir }, null, log, ct);
            if (r.ExitCode != 0) { log.Add(LogKind.Error, ".NET install failed."); return false; }
            log.Add(LogKind.Success, ".NET SDK installed.");
            return true;
        }
    }
}
