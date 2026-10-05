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
    public enum ChatRole { User, Assistant, Tool, System, Error, Plan, Todo }

    /// <summary>
    /// One row of the transcript, drawn the way Claude Code draws it: a message, or a tool call
    /// with its result folded under it (<see cref="Detail"/>), a proposed plan, or a to-do list.
    /// Tool results that carry a picture (game_screenshot) keep the PNG so the page can show it.
    /// </summary>
    public sealed class ChatItem
    {
        public ChatRole Role;
        public string Text;
        public string? Detail;
        public string? ToolId;
        public bool Failed;
        public byte[]? Image;
        public List<(string text, string status)>? Todos;
        public ChatItem(ChatRole role, string text) { Role = role; Text = text; }
        public ChatItem Copy() => (ChatItem)MemberwiseClone();
    }

    /// <summary>
    /// Claude Code inside the launcher. Each message runs the official <c>claude</c> CLI in print
    /// mode in the workspace (so it reads and edits this branch's files), streams its JSON events
    /// back as chat items, and resumes the same session for the next message. The API key, when
    /// given, is passed only to that process (ANTHROPIC_API_KEY); without one the CLI uses its own
    /// sign-in.
    /// </summary>
    public sealed class ClaudeChat
    {
        /// <summary>Claude Code's permission modes: plan (read and propose), accept edits, or run everything.</summary>
        public enum Mode { Plan = 0, Edit = 1, Auto = 2 }

        public static readonly string[] Models = { "default", "fable", "opus", "sonnet", "haiku" };
        public static readonly string[] Efforts = { "default", "low", "medium", "high", "xhigh", "max" };

        /// <summary>Model the CLI reported for the running session, its elapsed time, and the latest context size.</summary>
        public string? ActiveModel { get; private set; }
        public DateTime BusySince { get; private set; }
        public long ContextTokens { get; private set; }
        public int Turns { get; private set; }

        /// <summary>Raised on the chat thread when a reply finishes (voice reads it aloud).</summary>
        public event Action<string>? ReplyFinished;

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

        public void Snapshot(List<ChatItem> into) { lock (_lock) { into.Clear(); foreach (var i in _items) into.Add(i.Copy()); } }
        public int Count { get { lock (_lock) return _items.Count; } }
        void Add(ChatRole r, string t) { lock (_lock) _items.Add(new ChatItem(r, t)); }

        /// <summary>A launcher-side note in the transcript (not sent to Claude).</summary>
        public void Note(string t) => Add(ChatRole.System, t);

        public void NewChat()
        {
            Stop();
            lock (_lock) _items.Clear();
            _session = null; CostUsd = 0; ContextTokens = 0; Turns = 0; ActiveModel = null;
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

        public void Send(string text, string workDir, Mode mode, string? extraDir = null)
        {
            if (Busy || string.IsNullOrWhiteSpace(text)) return;
            if (Cli == null && Detect() == null) { Add(ChatRole.Error, "Claude Code is not installed."); return; }
            Add(ChatRole.User, text.Trim());
            Busy = true;
            BusySince = DateTime.UtcNow;
            Task.Run(() => Run(text.Trim(), workDir, mode, extraDir));
        }

        /// <summary>
        /// The scope every launcher conversation runs under: this is Prisma's agent. It
        /// works on Port/, reads Assets/ only as the game's input, and is refused (deny rules, which
        /// hold in every mode) any edit to what Unity reads.
        /// </summary>
        /// <summary>
        /// Who the conversation is. GAME: the Prisma Agent, powered by Claude - it works on Cosmic
        /// Shore (the game's code and content in Assets/) as it runs in Prisma, and never edits
        /// Prisma itself. MILESTONE: a roadmap checkpoint session - engine work on Port/ toward a
        /// milestone, which never edits the game. Deny rules enforce each side in every mode.
        /// </summary>
        public enum Scope { Game = 0, Milestone = 1 }

        public Scope CurrentScope { get; private set; } = Scope.Game;
        public string? Milestone { get; private set; }
        public string? MilestoneTitle { get; private set; }

        /// <summary>Switches who the conversation is; a different scope starts a fresh conversation.</summary>
        public void SetScope(Scope scope, string? milestone = null, string? title = null)
        {
            if (scope == CurrentScope && milestone == Milestone) return;
            NewChat();
            CurrentScope = scope; Milestone = milestone; MilestoneTitle = title;
            Note(scope == Scope.Game ? "Prisma Agent: working on the game." : $"Milestone {milestone} - {title}: engine work on Prisma.");
        }

        const string GameScope =
            "You are the Prisma Agent, powered by Claude, running inside Prisma - Froglet's own engine - on a checkout of the Cosmic Shore repository. " +
            "You work on the GAME: Cosmic Shore's code and content (Assets/), as it runs in Prisma. You do not change Prisma itself (Port/): " +
            "engine work happens in Prisma's MILESTONES sessions, so when a problem's cause is in the engine, say which milestone it belongs to and describe it. " +
            "Follow the repository's root CLAUDE.md for game work. Prisma records every play run as tracks (performance per scene, features, audio, every problem " +
            "with when it was first and last seen); the brief below is the latest, and prisma_tracks has the rest. Start from it: when asked to fix something, " +
            "find it in the tracks, reproduce it with the prisma tools (engine_smoke, game_start, game_screenshot, game_logs ...), fix it, and prove the fix the same way. " +
            "Keep replies short; the user reads them in Prisma's chat panel.";

        string MilestoneScope() =>
            $"You are running milestone {Milestone} ({MilestoneTitle}) inside Prisma, Froglet's own engine for Cosmic Shore, on a checkout of the Cosmic Shore repository. " +
            "This session works on the ENGINE: Port/ (Prisma's source, tools, tests and docs). Read Port/CLAUDE.md and Port/docs/ROADMAP.md first; the checkpoint's " +
            "exit criterion is in Port/docs/milestones.json. Assets/, Packages/ and ProjectSettings/ are the Unity project: read them as the game's input, never change them. " +
            $"Prove every step with the prisma tools (engine_build, engine_test, engine_smoke, game_* ...). When the work moves the checkpoint, update {Milestone}'s entry in " +
            "Port/docs/milestones.json: status (todo, in-progress, done) and a dated note with the evidence. Keep replies short.";

        // Edit(path) rules cover every file-editing tool (Edit, Write, NotebookEdit) in every mode.
        static readonly string[] UnityDenies = { "Edit(Assets/**)", "Edit(Packages/**)", "Edit(ProjectSettings/**)" };

        static readonly string[] EngineDenies = { "Edit(Port/**)" };

        /// <summary>The tracks brief the Prisma Agent starts every prompt with.</summary>
        static string TracksBrief()
        {
            try { return Prisma.PrismaTracks.Load(Path.Combine(LauncherSettings.DataDir, "tracks")).Memory(5000); }
            catch { return ""; }
        }

        void Run(string text, string workDir, Mode mode, string? extraDir)
        {
            var reply = new System.Text.StringBuilder();
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
                             "--permission-mode", mode switch { Mode.Plan => "plan", Mode.Edit => "acceptEdits", _ => "bypassPermissions" } })
                    psi.ArgumentList.Add(a);
                if (_session != null) { psi.ArgumentList.Add("--resume"); psi.ArgumentList.Add(_session); }
                var model = _s.ClaudeModel?.Trim() ?? "";
                if (model.Length > 0 && model != "default") { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(model); }
                var effort = _s.ClaudeEffort?.Trim() ?? "";
                if (effort.Length > 0 && effort != "default") { psi.ArgumentList.Add("--effort"); psi.ArgumentList.Add(effort); }
                psi.ArgumentList.Add("--disallowedTools");
                foreach (var d in CurrentScope == Scope.Game ? EngineDenies : UnityDenies) psi.ArgumentList.Add(d);
                foreach (var dir in new[] { extraDir, Path.Combine(LauncherSettings.DataDir, "tracks") })
                    if (dir != null && Directory.Exists(dir)) { psi.ArgumentList.Add("--add-dir"); psi.ArgumentList.Add(dir); }
                WireEngine(psi, psi.WorkingDirectory);
                psi.ArgumentList.Add("--append-system-prompt");
                psi.ArgumentList.Add((CurrentScope == Scope.Game ? GameScope + "\n\n" + TracksBrief() : MilestoneScope()) + $"\n\nBranch: {_s.Branch}." + (mode == Mode.Plan
                    ? " You are in PLAN mode: investigate (reading files and using the prisma tools is fine), then make your final message " +
                      "the plan itself - a short title line and numbered steps naming the files to change. The launcher shows that message as the plan " +
                      "with Approve buttons, so do not write plan files and do not ask how to submit it."
                    : ""));
                if (!string.IsNullOrWhiteSpace(_s.AnthropicApiKey)) psi.Environment["ANTHROPIC_API_KEY"] = _s.AnthropicApiKey.Trim();

                _proc = Process.Start(psi)!;
                _proc.StandardInput.Write(text);
                _proc.StandardInput.Close();
                var err = _proc.StandardError.ReadToEndAsync();
                string? line;
                while ((line = _proc.StandardOutput.ReadLine()) != null) Parse(line, reply);
                _proc.WaitForExit();
                if (_proc.ExitCode != 0)
                {
                    var e = err.Result.Trim();
                    Add(ChatRole.Error, e.Length > 0 ? e : $"claude exited with {_proc.ExitCode}");
                    RefreshAuth(); // a sign-in that expired shows up as SIGN IN
                }
            }
            catch (Exception ex) { Add(ChatRole.Error, ex.Message); }
            finally
            {
                // Headless Claude has no plan-approval tool: a plan-mode turn's final message is the plan.
                if (mode == Mode.Plan)
                    lock (_lock)
                    {
                        int user = _items.FindLastIndex(x => x.Role == ChatRole.User);
                        int last = _items.FindLastIndex(x => x.Role == ChatRole.Assistant);
                        if (last > user && user >= 0 && !_items.Skip(user).Any(x => x.Role == ChatRole.Plan)) _items[last].Role = ChatRole.Plan;
                    }
                Busy = false;
                if (reply.Length > 0) try { ReplyFinished?.Invoke(reply.ToString()); } catch { }
            }
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
                    ["prisma"] = new Dictionary<string, object>
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
            psi.ArgumentList.Add("mcp__prisma");
            return true;
        }

        void Parse(string line, System.Text.StringBuilder reply)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(line); } catch { return; }
            using (doc)
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("session_id", out var sid) && sid.ValueKind == JsonValueKind.String) _session = sid.GetString();
                string type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                if (type == "system" && root.TryGetProperty("model", out var m) && m.ValueKind == JsonValueKind.String) ActiveModel = m.GetString();
                if (type == "assistant" && root.TryGetProperty("message", out var msg))
                {
                    if (msg.TryGetProperty("usage", out var u)) ContextTokens = Tokens(u);
                    if (!msg.TryGetProperty("content", out var content)) return;
                    foreach (var block in content.EnumerateArray())
                    {
                        var bt = block.GetProperty("type").GetString();
                        if (bt == "text")
                        {
                            var s = block.GetProperty("text").GetString();
                            if (string.IsNullOrWhiteSpace(s)) continue;
                            Add(ChatRole.Assistant, s!.Trim());
                            reply.Append(s).Append('\n');
                        }
                        else if (bt == "tool_use") AddTool(block);
                    }
                }
                else if (type == "user" && root.TryGetProperty("message", out var um) && um.TryGetProperty("content", out var uc) && uc.ValueKind == JsonValueKind.Array)
                {
                    foreach (var block in uc.EnumerateArray())
                        if (block.TryGetProperty("type", out var bt) && bt.GetString() == "tool_result") AttachResult(block);
                }
                else if (type == "result")
                {
                    if (root.TryGetProperty("total_cost_usd", out var c) && c.ValueKind == JsonValueKind.Number) CostUsd += c.GetDouble();
                    if (root.TryGetProperty("num_turns", out var nt) && nt.ValueKind == JsonValueKind.Number) Turns += nt.GetInt32();
                    if (root.TryGetProperty("is_error", out var ie) && ie.ValueKind == JsonValueKind.True
                        && root.TryGetProperty("result", out var res)) Add(ChatRole.Error, res.ToString());
                }
            }
        }

        static long Tokens(JsonElement u)
        {
            long n = 0;
            foreach (var k in new[] { "input_tokens", "cache_read_input_tokens", "cache_creation_input_tokens" })
                if (u.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number) n += v.GetInt64();
            return n;
        }

        void AddTool(JsonElement block)
        {
            string name = block.TryGetProperty("name", out var n) ? n.GetString() ?? "tool" : "tool";
            string? id = block.TryGetProperty("id", out var i) ? i.GetString() : null;
            var input = block.TryGetProperty("input", out var inp) ? inp : default;
            if (name == "TodoWrite" && input.ValueKind == JsonValueKind.Object && input.TryGetProperty("todos", out var todos))
            {
                var list = todos.EnumerateArray().Select(x => (
                    x.TryGetProperty("content", out var ct) ? ct.GetString() ?? "" : "",
                    x.TryGetProperty("status", out var st) ? st.GetString() ?? "pending" : "pending")).ToList();
                lock (_lock) _items.Add(new ChatItem(ChatRole.Todo, "Update todos") { Todos = list, ToolId = id });
                return;
            }
            if (name == "ExitPlanMode" && input.ValueKind == JsonValueKind.Object && input.TryGetProperty("plan", out var plan))
            {
                lock (_lock) _items.Add(new ChatItem(ChatRole.Plan, plan.GetString() ?? "") { ToolId = id });
                return;
            }
            lock (_lock) _items.Add(new ChatItem(ChatRole.Tool, Describe(block)) { ToolId = id });
        }

        /// <summary>Folds a tool's result under its call: the first lines of text, and any picture it returned.</summary>
        void AttachResult(JsonElement block)
        {
            string? id = block.TryGetProperty("tool_use_id", out var i) ? i.GetString() : null;
            bool failed = block.TryGetProperty("is_error", out var e) && e.ValueKind == JsonValueKind.True;
            var text = new System.Text.StringBuilder();
            byte[]? image = null;
            if (block.TryGetProperty("content", out var c))
            {
                if (c.ValueKind == JsonValueKind.String) text.Append(c.GetString());
                else if (c.ValueKind == JsonValueKind.Array)
                    foreach (var part in c.EnumerateArray())
                    {
                        var pt = part.TryGetProperty("type", out var ptt) ? ptt.GetString() : null;
                        if (pt == "text") text.AppendLine(part.GetProperty("text").GetString());
                        else if (pt == "image" && part.TryGetProperty("source", out var src) && src.TryGetProperty("data", out var data))
                            try { image = Convert.FromBase64String(data.GetString() ?? ""); } catch { }
                    }
            }
            var lines = text.ToString().Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string detail = lines.Length == 0 ? (image != null ? "screenshot" : "done")
                : string.Join("\n", lines.Take(3).Select(l => l.Length > 140 ? l[..139] + "..." : l)) + (lines.Length > 3 ? $"\n... +{lines.Length - 3} lines" : "");
            lock (_lock)
            {
                var item = _items.LastOrDefault(x => x.ToolId == id && id != null);
                if (item == null) return;
                if (item.Role == ChatRole.Tool) { item.Detail = detail; item.Failed = failed; item.Image = image; }
            }
        }

        static string Describe(JsonElement toolUse)
        {
            string name = toolUse.TryGetProperty("name", out var n) ? n.GetString() ?? "tool" : "tool";
            name = name.StartsWith("mcp__prisma__") ? name["mcp__prisma__".Length..] : name;
            if (!toolUse.TryGetProperty("input", out var input) || input.ValueKind != JsonValueKind.Object) return name;
            foreach (var key in new[] { "file_path", "path", "command", "pattern", "action", "url", "target", "text", "scene", "description" })
                if (input.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var s = v.GetString()!.Replace('\n', ' ');
                    return $"{name}({(s.Length > 80 ? s[..79] + "..." : s)})";
                }
            return name;
        }
    }
}
