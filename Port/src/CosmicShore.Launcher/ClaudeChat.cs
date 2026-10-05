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
    public enum ChatRole { User, Assistant, Tool, System, Error }

    public sealed record ChatItem(ChatRole Role, string Text);

    /// <summary>
    /// Claude Code inside the launcher. Each message runs the official <c>claude</c> CLI in print
    /// mode in the workspace (so it reads and edits this branch's files), streams its JSON events
    /// back as chat items, and resumes the same session for the next message. The API key, when
    /// given, is passed only to that process (ANTHROPIC_API_KEY); without one the CLI uses its own
    /// sign-in.
    /// </summary>
    public sealed class ClaudeChat
    {
        public enum Mode { Ask = 0, Edit = 1, Auto = 2 }

        readonly LauncherSettings _s;
        readonly Toolchain _tools;
        readonly object _lock = new();
        readonly List<ChatItem> _items = new();
        Process? _proc;
        string? _session;

        public ClaudeChat(LauncherSettings s, Toolchain tools) { _s = s; _tools = tools; }

        public bool Busy { get; private set; }
        public double CostUsd { get; private set; }
        public string? Cli { get; private set; }
        public bool Installing { get; private set; }

        public void Snapshot(List<ChatItem> into) { lock (_lock) { into.Clear(); into.AddRange(_items); } }
        public int Count { get { lock (_lock) return _items.Count; } }
        void Add(ChatRole r, string t) { lock (_lock) _items.Add(new ChatItem(r, t)); }

        public void NewChat()
        {
            Stop();
            lock (_lock) _items.Clear();
            _session = null; CostUsd = 0;
        }

        public void Stop()
        {
            try { if (_proc is { HasExited: false }) _proc.Kill(entireProcessTree: true); } catch { }
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
        public void SignIn()
        {
            if (Cli == null && Detect() == null) return;
            var cli = Cli!;
            try
            {
                if (OperatingSystem.IsWindows())
                    Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"\"{cli}\" auth login --claudeai & echo. & echo You can close this window. & pause\"") { UseShellExecute = true });
                else if (OperatingSystem.IsMacOS())
                    Process.Start("osascript", new[] { "-e", $"tell application \"Terminal\" to do script \"'{cli}' auth login --claudeai\"", "-e", "tell application \"Terminal\" to activate" });
                else
                    Process.Start("x-terminal-emulator", new[] { "-e", cli, "auth", "login", "--claudeai" });
                Add(ChatRole.System, "Finish signing in in the window that opened, then come back here.");
            }
            catch (Exception e) { Add(ChatRole.Error, "Could not open a terminal: " + e.Message + $". Run  {cli} auth login  yourself."); }
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
        public async Task Install(LogBuffer log)
        {
            if (Installing) return;
            Installing = true;
            _installCts = new CancellationTokenSource();
            var ct = _installCts.Token;
            string? download = null;
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("FrogletLauncher/0.1");
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
                Add(ok ? ChatRole.System : ChatRole.Error, ok ? $"Claude Code {version} installed." : "Install finished but claude was not found - see CONSOLE.");
            }
            catch (OperationCanceledException) { Add(ChatRole.System, "Install cancelled."); }
            catch (Exception e)
            {
                log.Add(LogKind.Error, "Claude Code install: " + e.Message);
                Add(ChatRole.Error, "Install failed: " + e.Message);
            }
            finally
            {
                try { if (download != null && File.Exists(download)) File.Delete(download); } catch { }
                Installing = false; InstallProgress = -1; InstallStatus = "";
            }
        }

        public void Send(string text, string workDir, Mode mode)
        {
            if (Busy || string.IsNullOrWhiteSpace(text)) return;
            if (Cli == null && Detect() == null) { Add(ChatRole.Error, "Claude Code is not installed."); return; }
            Add(ChatRole.User, text.Trim());
            Busy = true;
            Task.Run(() => Run(text.Trim(), workDir, mode));
        }

        void Run(string text, string workDir, Mode mode)
        {
            try
            {
                var psi = new ProcessStartInfo(Cli!)
                {
                    WorkingDirectory = Directory.Exists(workDir) ? workDir : Environment.CurrentDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                foreach (var a in new[] { "-p", "--output-format", "stream-json", "--verbose",
                             "--permission-mode", mode switch { Mode.Ask => "plan", Mode.Edit => "acceptEdits", _ => "bypassPermissions" } })
                    psi.ArgumentList.Add(a);
                if (_session != null) { psi.ArgumentList.Add("--resume"); psi.ArgumentList.Add(_session); }
                if (!string.IsNullOrWhiteSpace(_s.ClaudeModel)) { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(_s.ClaudeModel.Trim()); }
                bool engine = WireEngine(psi, psi.WorkingDirectory);
                psi.ArgumentList.Add("--append-system-prompt");
                psi.ArgumentList.Add("You are running inside the Froglet Engine Launcher, in a checkout of the Cosmic Shore repository " +
                                     $"(branch {_s.Branch}). Keep replies short; the user reads them in a small chat panel." +
                                     (engine ? " The froglet-engine MCP tools build the engine and start, see and drive the running game; read Port/CLAUDE.md before engine work." : ""));
                if (!string.IsNullOrWhiteSpace(_s.AnthropicApiKey)) psi.Environment["ANTHROPIC_API_KEY"] = _s.AnthropicApiKey.Trim();

                _proc = Process.Start(psi)!;
                _proc.StandardInput.Write(text);
                _proc.StandardInput.Close();
                var err = _proc.StandardError.ReadToEndAsync();
                string? line;
                while ((line = _proc.StandardOutput.ReadLine()) != null) Parse(line);
                _proc.WaitForExit();
                if (_proc.ExitCode != 0)
                {
                    var e = err.Result.Trim();
                    Add(ChatRole.Error, e.Length > 0 ? e : $"claude exited with {_proc.ExitCode}");
                    RefreshAuth(); // a sign-in that expired shows up as SIGN IN
                }
            }
            catch (Exception ex) { Add(ChatRole.Error, ex.Message); }
            finally { Busy = false; }
        }

        /// <summary>
        /// Connects the engine's MCP server (Port/src/CosmicShore.Mcp) when the workspace has it:
        /// Claude can then build the engine and start, see and drive the game. The config is written
        /// per run with the launcher's own dotnet, which may be a private SDK not on PATH.
        /// </summary>
        bool WireEngine(ProcessStartInfo psi, string workDir)
        {
            var project = Path.Combine(workDir, "Port", "src", "CosmicShore.Mcp");
            if (_tools.Dotnet == null || !Directory.Exists(project)) return false;
            var env = new Dictionary<string, string>(_tools.DotnetEnv()) { ["COSMIC_SHORE_REPO"] = workDir };
            var config = new Dictionary<string, object>
            {
                ["mcpServers"] = new Dictionary<string, object>
                {
                    ["froglet-engine"] = new Dictionary<string, object>
                    {
                        ["command"] = _tools.Dotnet,
                        ["args"] = new[] { "run", "--project", project, "--" },
                        ["env"] = env,
                    },
                },
            };
            var path = Path.Combine(LauncherSettings.DataDir, "engine-mcp.json");
            File.WriteAllText(path, JsonSerializer.Serialize(config));
            psi.ArgumentList.Add("--mcp-config");
            psi.ArgumentList.Add(path);
            // Its tools build and run the game but edit no files, so every mode may use them.
            psi.ArgumentList.Add("--allowedTools");
            psi.ArgumentList.Add("mcp__froglet-engine");
            return true;
        }

        void Parse(string line)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(line); } catch { return; }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("session_id", out var sid) && sid.ValueKind == JsonValueKind.String) _session = sid.GetString();
                string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                if (type == "assistant" && root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
                {
                    foreach (var block in content.EnumerateArray())
                    {
                        var bt = block.GetProperty("type").GetString();
                        if (bt == "text") { var s = block.GetProperty("text").GetString(); if (!string.IsNullOrWhiteSpace(s)) Add(ChatRole.Assistant, s!.Trim()); }
                        else if (bt == "tool_use") Add(ChatRole.Tool, Describe(block));
                    }
                }
                else if (type == "result")
                {
                    if (root.TryGetProperty("total_cost_usd", out var c) && c.ValueKind == JsonValueKind.Number) CostUsd += c.GetDouble();
                    if (root.TryGetProperty("is_error", out var ie) && ie.ValueKind == JsonValueKind.True
                        && root.TryGetProperty("result", out var res)) Add(ChatRole.Error, res.ToString());
                }
            }
        }

        static string Describe(JsonElement toolUse)
        {
            string name = toolUse.TryGetProperty("name", out var n) ? n.GetString() ?? "tool" : "tool";
            if (!toolUse.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object) return name;
            foreach (var key in new[] { "file_path", "path", "command", "pattern", "url", "description" })
                if (input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var s = v.GetString()!.Replace('\n', ' ');
                    return name + "  " + (s.Length > 90 ? s[..89] + "..." : s);
                }
            return name;
        }
    }
}
