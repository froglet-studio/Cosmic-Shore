using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The Claude Code CLI as one shared thing for every chat: where it is, whether it is signed in,
    /// installing it, and the account's plan usage (the session and weekly limits Claude Code's
    /// /usage shows), which every run reports in its rate_limit_event.
    /// </summary>
    public sealed class ClaudeCli
    {
        readonly LauncherSettings _s;
        public ClaudeCli(LauncherSettings s) { _s = s; LoadUsage(); }

        public string? Cli { get; private set; }
        public bool Installing { get; private set; }

        /// <summary>One plan-usage window: "five_hour" is Claude Code's "Current session", "seven_day" its "Current week".</summary>
        public sealed record UsageWindow(string Key, double Utilization, DateTimeOffset? ResetsAt)
        {
            public string Label => Key switch
            {
                "five_hour" => "Current session",
                "seven_day" => "Current week (all models)",
                "seven_day_opus" => "Current week (Opus)",
                "seven_day_sonnet" => "Current week (Sonnet)",
                _ => Key.Replace('_', ' '),
            };
        }

        readonly object _usageLock = new();
        List<UsageWindow> _windows = new();
        /// <summary>The plan's usage windows as of <see cref="UsageAt"/> (empty until a run reports them, or on an API key).</summary>
        public List<UsageWindow> Windows { get { lock (_usageLock) return _windows.ToList(); } }
        public DateTime UsageAt { get; private set; }
        /// <summary>"allowed", "allowed_warning" or "rejected" (at the limit).</summary>
        public string UsageStatus { get; private set; } = "";

        static string UsageFile => Path.Combine(LauncherSettings.DataDir, "usage.json");

        static UsageWindow Window(string key, JsonElement e) => new(key,
            e.TryGetProperty("utilization", out var u) && u.ValueKind == JsonValueKind.Number ? u.GetDouble() : 0,
            e.TryGetProperty("resetsAt", out var r) && r.ValueKind == JsonValueKind.Number ? DateTimeOffset.FromUnixTimeSeconds(r.GetInt64()) : null);

        /// <summary>Reads a run's rate_limit_event: the plan's windows with how much of each is used and when it resets.</summary>
        public void OnRateLimit(JsonElement info)
        {
            var list = new List<UsageWindow>();
            if (info.TryGetProperty("unifiedWindows", out var w) && w.ValueKind == JsonValueKind.Object)
                foreach (var p in w.EnumerateObject()) list.Add(Window(p.Name, p.Value));
            else if (info.TryGetProperty("rateLimitType", out var rt) && rt.ValueKind == JsonValueKind.String)
                list.Add(Window(rt.GetString()!, info));
            UsageStatus = info.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() ?? "" : "";
            if (list.Count == 0) return;
            lock (_usageLock)
            {
                // A window this run did not mention keeps its last reading.
                _windows = _windows.Where(x => list.All(n => n.Key != x.Key)).Concat(list)
                    .OrderBy(x => x.Key == "five_hour" ? 0 : x.Key == "seven_day" ? 1 : 2).ThenBy(x => x.Key).ToList();
            }
            UsageAt = DateTime.Now;
            try { File.WriteAllText(UsageFile, JsonSerializer.Serialize(new UsageState(UsageAt, UsageStatus, Windows))); } catch { }
        }

        sealed record UsageState(DateTime At, string Status, List<UsageWindow> Windows);

        void LoadUsage()
        {
            try
            {
                if (!File.Exists(UsageFile)) return;
                var st = JsonSerializer.Deserialize<UsageState>(File.ReadAllText(UsageFile));
                if (st == null) return;
                UsageAt = st.At; UsageStatus = st.Status;
                // A window whose reset has passed starts again from zero.
                _windows = st.Windows.Select(x => x.ResetsAt is { } r && r < DateTimeOffset.Now ? x with { Utilization = 0, ResetsAt = null } : x).ToList();
            }
            catch { }
        }

        /// <summary>Finds the claude CLI: the configured path, the native installer's folder, then PATH.</summary>
        public string? Detect()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(_s.ClaudePath)) candidates.Add(_s.ClaudePath.Trim());
            candidates.Add(Path.Combine(home, ".local", "bin", OperatingSystem.IsWindows() ? "claude.exe" : "claude"));
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (dir.Length == 0) continue;
                foreach (var name in OperatingSystem.IsWindows() ? new[] { "claude.exe", "claude.cmd" } : new[] { "claude" })
                    candidates.Add(Path.Combine(dir, name));
            }
            Cli = candidates.FirstOrDefault(File.Exists);
            return Cli;
        }

        /// <summary>null until checked; whether the CLI holds a sign-in (a Claude Pro/Max plan or a Console account).</summary>
        public bool? SignedIn { get; private set; }

        /// <summary>Asks the CLI whether it is signed in (<c>claude auth status</c>).</summary>
        public void RefreshAuth()
        {
            if (Cli == null && Detect() == null) { SignedIn = null; return; }
            var json = ProcessRunner.Capture(Cli!, "auth", "status", "--json");
            try { using var d = JsonDocument.Parse(json ?? "{}"); SignedIn = d.RootElement.TryGetProperty("loggedIn", out var l) && l.ValueKind == JsonValueKind.True; }
            catch { SignedIn = null; }
        }

        /// <summary>
        /// Opens a terminal running <c>claude auth login --claudeai</c>: the browser sign-in that puts
        /// Claude Code on the user's Claude Pro/Max plan instead of pay-as-you-go API billing. It needs
        /// a real terminal (it may ask to paste a code), so it is not run inside the launcher.
        /// </summary>
        public string? SignIn()
        {
            if (Cli == null && Detect() == null) return null;
            var cli = Cli!;
            try
            {
                if (OperatingSystem.IsWindows())
                    Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"\"{cli}\" auth login --claudeai & echo. & echo You can close this window. & pause\"") { UseShellExecute = true });
                else if (OperatingSystem.IsMacOS())
                    Process.Start("osascript", new[] { "-e", $"tell application \"Terminal\" to do script \"'{cli}' auth login --claudeai\"", "-e", "tell application \"Terminal\" to activate" });
                else
                    Process.Start("x-terminal-emulator", new[] { "-e", cli, "auth", "login", "--claudeai" });
                return "Finish signing in in the window that opened, then come back here.";
            }
            catch (Exception e) { return "Could not open a terminal: " + e.Message + $". Run  {cli} auth login  yourself."; }
        }

        public float InstallProgress { get; private set; } = -1;
        public string InstallStatus { get; private set; } = "";
        CancellationTokenSource? _installCts;

        public void CancelInstall() => _installCts?.Cancel();

        const string Releases = "https://downloads.claude.ai/claude-code-releases";

        static string Platform()
        {
            string arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";
            return (OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : "linux") + "-" + arch;
        }

        /// <summary>
        /// Installs Claude Code the way Anthropic's install script does (latest version, manifest
        /// checksum, then the binary's own <c>install</c> step), but with a progress bar: the script
        /// downloads a ~250 MB binary with its progress display switched off, which looks frozen.
        /// If the binary's setup step stalls, the verified binary is placed in ~/.local/bin itself.
        /// </summary>
        public async Task<(bool ok, string message)> Install(LogBuffer log)
        {
            if (Installing) return (false, "");
            Installing = true;
            _installCts = new CancellationTokenSource();
            var ct = _installCts.Token;
            string? download = null;
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Prisma/0.1");
                InstallStatus = "Finding the latest version";
                string version = (await http.GetStringAsync($"{Releases}/latest", ct)).Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+"))
                    throw new Exception("downloads.claude.ai did not answer with a version (blocked network, or a region Claude Code is not offered in).");
                string platform = Platform();
                using var manifest = JsonDocument.Parse(await http.GetStringAsync($"{Releases}/{version}/manifest.json", ct));
                if (!manifest.RootElement.GetProperty("platforms").TryGetProperty(platform, out var entry))
                    throw new Exception($"Claude Code {version} has no build for {platform}.");
                string checksum = entry.GetProperty("checksum").GetString()!.ToLowerInvariant();
                string exeName = OperatingSystem.IsWindows() ? "claude.exe" : "claude";

                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var dir = Path.Combine(home, ".claude", "downloads");
                Directory.CreateDirectory(dir);
                download = Path.Combine(dir, $"claude-{version}-{platform}" + (OperatingSystem.IsWindows() ? ".exe" : ""));
                log.Add(LogKind.Command, $"> download Claude Code {version} ({platform})");
                using (var resp = await http.GetAsync($"{Releases}/{version}/{platform}/{exeName}", System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    long total = resp.Content.Headers.ContentLength ?? (entry.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0);
                    await using var src = await resp.Content.ReadAsStreamAsync(ct);
                    await using var dst = File.Create(download);
                    var buf = new byte[1 << 16];
                    long got = 0; int n;
                    while ((n = await src.ReadAsync(buf, ct)) > 0)
                    {
                        await dst.WriteAsync(buf.AsMemory(0, n), ct);
                        got += n;
                        InstallProgress = total > 0 ? (float)got / total : -1;
                        InstallStatus = total > 0 ? $"Downloading {got >> 20} / {total >> 20} MB" : $"Downloading {got >> 20} MB";
                    }
                }
                InstallProgress = -1;
                InstallStatus = "Verifying";
                string actual;
                await using (var f = File.OpenRead(download))
                    actual = Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(f, ct)).ToLowerInvariant();
                if (actual != checksum) throw new Exception("The download failed its checksum. Try again.");
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(download, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                InstallStatus = "Setting up";
                bool setUp = false;
                try
                {
                    using var setupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    setupCts.CancelAfter(TimeSpan.FromMinutes(3));
                    var r = await ProcessRunner.Run(download, new[] { "install", "latest" }, null, log, setupCts.Token, closeStdin: true);
                    setUp = r.ExitCode == 0;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    log.Add(LogKind.Info, "Claude Code's setup step did not finish in 3 minutes - placing the binary directly.");
                }
                if (!setUp || Detect() == null)
                {
                    // What the setup step does at its core: the binary in ~/.local/bin.
                    var bin = Path.Combine(home, ".local", "bin");
                    Directory.CreateDirectory(bin);
                    File.Copy(download, Path.Combine(bin, exeName), overwrite: true);
                    if (!OperatingSystem.IsWindows())
                        File.SetUnixFileMode(Path.Combine(bin, exeName), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                bool ok = Detect() != null;
                if (ok) RefreshAuth();
                return (ok, ok ? $"Claude Code {version} installed." : "Install finished but claude was not found - see CONSOLE.");
            }
            catch (OperationCanceledException) { return (false, "Install cancelled."); }
            catch (Exception e)
            {
                log.Add(LogKind.Error, "Claude Code install: " + e.Message);
                return (false, "Install failed: " + e.Message);
            }
            finally
            {
                try { if (download != null && File.Exists(download)) File.Delete(download); } catch { }
                Installing = false; InstallProgress = -1; InstallStatus = "";
            }
        }

    }
}
