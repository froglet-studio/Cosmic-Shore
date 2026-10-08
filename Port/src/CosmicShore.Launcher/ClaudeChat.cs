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
        /// <summary>The file an Edit/Write/NotebookEdit call changed (repository-relative when it can be), for the GIT page.</summary>
        public string? File;
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
        /// <summary>The model's context window as the CLI reports it (modelUsage.contextWindow); 0 until a run ends.</summary>
        public long ContextWindow { get; private set; }
        public int Turns { get; private set; }
        /// <summary>How many files the last run's edit calls changed (the GIT page has them).</summary>
        public int LastRunEdits { get; private set; }

        /// <summary>This conversation's identity in the chat list: a short id, a title (its first message, or the milestone), when it was last used.</summary>
        public string Id { get; private set; } = DateTime.UtcNow.ToString("yyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N")[..4];
        public string Title { get; set; } = "";
        public DateTime Created { get; private set; } = DateTime.Now;
        public DateTime Updated { get; private set; } = DateTime.Now;
        /// <summary>Raised on the chat thread when a run ends and the conversation was saved (the chat list re-sorts).</summary>
        public event Action? Saved;

        /// <summary>Raised on the chat thread when a reply finishes (voice reads it aloud).</summary>
        public event Action<string>? ReplyFinished;

        /// <summary>A milestone run that ended short: it hit its turn, time or cost budget, or failed.</summary>
        public sealed record SessionStop(string Milestone, string Title, string Reason, string Tried, string LastWords);

        /// <summary>Raised on the chat thread when a milestone run stops short (never for the user's own STOP).</summary>
        public event Action<SessionStop>? MilestoneStopped;

        // Why a run ended early, as "at the turn limit" - set by the result event.
        volatile string? _stopReason;
        volatile string? _lastError;
        volatile bool _userStopped;

        readonly LauncherSettings _s;
        readonly Toolchain _tools;
        readonly ClaudeCli _cli;
        readonly object _lock = new();
        readonly List<ChatItem> _items = new();
        Process? _proc;
        string? _session;

        public ClaudeChat(LauncherSettings s, Toolchain tools, ClaudeCli cli, Scope scope = Scope.Game, string? milestone = null, string? title = null)
        {
            _s = s; _tools = tools; _cli = cli;
            CurrentScope = scope; Milestone = milestone; MilestoneTitle = title;
            if (scope == Scope.Milestone) Title = $"{milestone} - {title}";
        }

        public bool Busy { get; private set; }
        public double CostUsd { get; private set; }
        /// <summary>Whether the conversation has anything in it worth keeping in the list.</summary>
        public bool Empty { get { lock (_lock) return !_items.Any(i => i.Role == ChatRole.User); } }

        public void Snapshot(List<ChatItem> into) { lock (_lock) { into.Clear(); foreach (var i in _items) into.Add(i.Copy()); } }
        public int Count { get { lock (_lock) return _items.Count; } }
        void Add(ChatRole r, string t) { lock (_lock) _items.Add(new ChatItem(r, t)); }

        /// <summary>A launcher-side note in the transcript (not sent to Claude).</summary>
        public void Note(string t) => Add(ChatRole.System, t);

        /// <summary>Empties this conversation (Claude Code's /clear): the transcript and the CLI session start over.</summary>
        public void NewChat()
        {
            Stop();
            lock (_lock) _items.Clear();
            _session = null; CostUsd = 0; ContextTokens = 0; ContextWindow = 0; Turns = 0; ActiveModel = null;
            if (CurrentScope == Scope.Game) Title = "";
            Save();
        }

        public void Stop()
        {
            _userStopped = true;
            try { if (_proc is { HasExited: false }) _proc.Kill(entireProcessTree: true); } catch { }
        }

        // The CLI itself (where it is, sign-in, install, plan usage) is shared by every chat.
        public ClaudeCli Shared => _cli;
        public string? Cli => _cli.Cli;
        public bool? SignedIn => _cli.SignedIn;
        public bool Installing => _cli.Installing;
        public float InstallProgress => _cli.InstallProgress;
        public string InstallStatus => _cli.InstallStatus;
        public string? Detect() => _cli.Detect();
        public void RefreshAuth() => _cli.RefreshAuth();
        public void CancelInstall() => _cli.CancelInstall();
        public void SignIn() { if (_cli.SignIn() is { } note) Note(note); }
        public async Task Install(LogBuffer log)
        {
            var (ok, message) = await _cli.Install(log);
            if (message.Length > 0) Add(ok ? ChatRole.System : ChatRole.Error, message);
        }

        public void Send(string text, string workDir, Mode mode, string? extraDir = null)
        {
            if (Busy || string.IsNullOrWhiteSpace(text)) return;
            if (Cli == null && Detect() == null) { Add(ChatRole.Error, "Claude Code is not installed."); return; }
            Add(ChatRole.User, text.Trim());
            if (Title.Length == 0) Title = TitleFrom(text);
            Updated = DateTime.Now;
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

        public Scope CurrentScope { get; }
        public string? Milestone { get; }
        public string? MilestoneTitle { get; }

        // Every chat runs in Prisma's workspace, a checkout separate from the user's own clone. Its edits
        // stay there until the user saves them on the GIT page, so the agent leaves git to them.
        const string WorkspaceNote =
            " The checkout is Prisma's workspace, not the user's own clone: your edits stay here until the user reviews, commits and pushes them on Prisma's " +
            "GIT page. Do not commit, push or switch branches unless the user asks you to.";

        // Only what the user asks for: the tracks and the board are there to read when the question is about them,
        // not a standing order to go and fix whatever the last runs recorded.
        const string GameScope =
            "You are the Prisma Agent, powered by Claude, running inside Prisma - Froglet's own engine - on a checkout of the Cosmic Shore repository. " +
            "You work on the GAME: Cosmic Shore's code and content (Assets/), as it runs in Prisma. Follow the repository's root CLAUDE.md for game work. " +
            "Do what the user asks and nothing more: do not go looking for other problems, and do not investigate Prisma (Port/) or its recorded problems unless the request is about them. " +
            "You never change Prisma itself (Port/); engine work happens in Prisma's MILESTONES sessions, so when a cause you meet is in the engine, say so in one line and carry on. " +
            "When a request is about a bug, a crash, performance or a play run, prisma_tracks has every run Prisma recorded (performance per scene, features, audio, each problem " +
            "with when it was first and last seen) and the prisma tools (engine_smoke, game_start, game_screenshot, game_logs ...) reproduce and prove a fix. " +
            "For data and models without Unity: asset_datasets / asset_dataset (ScriptableObject data sets; edit a field with cs-asset set), asset_model / " +
            "asset_model_preview (FBX as Unity imports it), asset_froglet_tools (the FrogletTools and their source). Scene and hierarchy edits go through cs-asset. " +
            "A board item's 'done when' is its acceptance test: run it and show the result before calling that work done. " +
            "Keep replies short; the user reads them in Prisma's chat panel." + WorkspaceNote;

        string MilestoneScope() =>
            $"You are running milestone {Milestone} ({MilestoneTitle}) inside Prisma, Froglet's own engine for Cosmic Shore, on a checkout of the Cosmic Shore repository. " +
            "This session works on the ENGINE: Port/ (Prisma's source, tools, tests and docs). Read Port/CLAUDE.md and Port/docs/ROADMAP.md first; the checkpoint's " +
            "exit criterion is in Port/docs/milestones.json. Assets/, Packages/ and ProjectSettings/ are the Unity project: read them as the game's input, never change them. " +
            $"Prove every step with the prisma tools (engine_build, engine_test, engine_smoke, game_* ...). When the work moves the checkpoint, update {Milestone}'s entry in " +
            "Port/docs/milestones.json: status (todo, in-progress, done) and a dated note with the evidence. The exit criterion is the acceptance test: never set a " +
            "checkpoint to done until you have run that check and it passed, and put the command and its result in the note. Problems you find but do not fix go on " +
            "the board with prisma_board_suggest, each with its own criterion. Keep replies short." + WorkspaceNote;

        // Edit(path) rules cover every file-editing tool (Edit, Write, NotebookEdit) in every mode.
        static readonly string[] UnityDenies = { "Edit(Assets/**)", "Edit(Packages/**)", "Edit(ProjectSettings/**)" };

        static readonly string[] EngineDenies = { "Edit(Port/**)" };

        void Run(string text, string workDir, Mode mode, string? extraDir)
        {
            var reply = new System.Text.StringBuilder();
            bool milestone = CurrentScope == Scope.Milestone;
            int exit = 0;
            _stopReason = null;
            _lastError = null;
            _userStopped = false;
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
                psi.ArgumentList.Add((CurrentScope == Scope.Game ? GameScope : MilestoneScope()) + $"\n\nBranch: {_s.Branch}." + (mode == Mode.Plan
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
                exit = _proc.ExitCode;
                if (exit != 0 && !_userStopped)
                {
                    if (_stopReason != null) Add(ChatRole.Error, $"Stopped {_stopReason}.");
                    else if (_lastError == null)
                    {
                        var e = err.Result.Trim();
                        Add(ChatRole.Error, e.Length > 0 ? e : $"claude exited with {exit}");
                    }
                    RefreshAuth(); // a sign-in that expired shows up as SIGN IN
                }
            }
            catch (Exception ex) { Add(ChatRole.Error, ex.Message); exit = -1; }
            finally
            {
                if (milestone && !_userStopped && (_stopReason != null || exit != 0))
                {
                    string reason = _stopReason ?? (_lastError is { } le ? "after an error: " + (le.Length > 90 ? le[..89] + "..." : le) : $"after claude exited with {exit}");
                    var stop = new SessionStop(Milestone ?? "", MilestoneTitle ?? "", reason, Tried(), LastWords());
                    try { MilestoneStopped?.Invoke(stop); } catch { }
                }
                // Headless Claude has no plan-approval tool: a plan-mode turn's final message is the plan.
                if (mode == Mode.Plan)
                    lock (_lock)
                    {
                        int user = _items.FindLastIndex(x => x.Role == ChatRole.User);
                        int last = _items.FindLastIndex(x => x.Role == ChatRole.Assistant);
                        if (last > user && user >= 0 && !_items.Skip(user).Any(x => x.Role == ChatRole.Plan)) _items[last].Role = ChatRole.Plan;
                    }
                lock (_lock)
                {
                    int user = _items.FindLastIndex(x => x.Role == ChatRole.User);
                    LastRunEdits = _items.Skip(Math.Max(0, user)).Where(x => x.File != null).Select(x => x.File!).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                }
                Busy = false;
                Updated = DateTime.Now;
                Save();
                try { Saved?.Invoke(); } catch { }
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
            var path = Path.Combine(LauncherSettings.DataDir, "chats", $"mcp-{Id}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
                if (type == "rate_limit_event" && root.TryGetProperty("rate_limit_info", out var rl) && rl.ValueKind == JsonValueKind.Object) _cli.OnRateLimit(rl);
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
                    // The window of the model this chat runs on (modelUsage also lists helper models).
                    if (root.TryGetProperty("modelUsage", out var mu) && mu.ValueKind == JsonValueKind.Object)
                        foreach (var mm in mu.EnumerateObject())
                            if (mm.Value.TryGetProperty("contextWindow", out var cw) && cw.ValueKind == JsonValueKind.Number &&
                                (ActiveModel == null || mm.Name == ActiveModel || ContextWindow == 0))
                                ContextWindow = cw.GetInt64();
                    string sub = root.TryGetProperty("subtype", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() ?? "" : "";
                    // A run that ends on a limit says so in subtype (error_max_turns, error_max_budget_usd ...), with no result text.
                    if (sub == "error_max_turns") _stopReason = "at the turn limit";
                    else if (sub.StartsWith("error_max_budget", StringComparison.Ordinal)) _stopReason = "at the budget limit";
                    else if (root.TryGetProperty("is_error", out var ie) && ie.ValueKind == JsonValueKind.True)
                    {
                        string? text = root.TryGetProperty("result", out var res) ? res.ToString()
                            : root.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array
                                ? string.Join("\n", errs.EnumerateArray().Select(x => x.ToString())) : null;
                        if (!string.IsNullOrWhiteSpace(text)) { Add(ChatRole.Error, text!); _lastError = text!.Split('\n')[0]; }
                    }
                }
            }
        }

        /// <summary>What the current run tried: its tool calls since the last user message, oldest first.</summary>
        string Tried()
        {
            lock (_lock)
            {
                int user = _items.FindLastIndex(x => x.Role == ChatRole.User);
                var tools = _items.Skip(Math.Max(0, user + 1)).Where(x => x.Role == ChatRole.Tool)
                                  .Select(x => (x.Failed ? "FAILED " : "") + x.Text.Replace('\n', ' ')).ToList();
                if (tools.Count == 0) return "No tool calls.";
                var shown = tools.Count > 30 ? tools.Take(10).Append($"... {tools.Count - 20} more ...").Concat(tools.TakeLast(10)) : tools;
                return string.Join("\n", shown.Select(t => "- " + (t.Length > 160 ? t[..159] + "..." : t)));
            }
        }

        string LastWords()
        {
            lock (_lock)
            {
                var last = _items.LastOrDefault(x => x.Role is ChatRole.Assistant or ChatRole.Plan);
                return last == null ? "" : last.Text.Length > 1200 ? last.Text[..1200] + "..." : last.Text;
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
            string? file = name is "Edit" or "Write" or "MultiEdit" or "NotebookEdit" && input.ValueKind == JsonValueKind.Object &&
                           (input.TryGetProperty("file_path", out var fp) || input.TryGetProperty("notebook_path", out fp)) && fp.ValueKind == JsonValueKind.String
                ? fp.GetString() : null;
            lock (_lock) _items.Add(new ChatItem(ChatRole.Tool, Describe(block)) { ToolId = id, File = file });
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

        // ------------------------------------------------------------------ the chat list

        public static string ChatsDir => Path.Combine(LauncherSettings.DataDir, "chats");
        string FilePath => Path.Combine(ChatsDir, Id + ".json");

        static string TitleFrom(string text)
        {
            var line = text.Trim().Split('\n')[0].Trim();
            return line.Length > 60 ? line[..57].TrimEnd() + "..." : line;
        }

        /// <summary>Every file this conversation's edit calls touched, as the CLI named them.</summary>
        public HashSet<string> EditedFiles()
        {
            lock (_lock) return _items.Where(i => i.File != null).Select(i => i.File!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        sealed record SavedItem(ChatRole Role, string Text, string? Detail, string? ToolId, string? File, bool Failed, List<string[]>? Todos);
        sealed record SavedChat(string Id, string Title, Scope Scope, string? Milestone, string? MilestoneTitle, string? Session, DateTime Created, DateTime Updated,
                                double Cost, long ContextTokens, long ContextWindow, int Turns, string? Model, List<SavedItem> Items);

        /// <summary>Writes the conversation to chats/ID.json (screenshots are not kept). An empty one is not kept at all.</summary>
        public void Save()
        {
            try
            {
                if (Empty) { if (File.Exists(FilePath)) File.Delete(FilePath); return; }
                List<SavedItem> items;
                lock (_lock) items = _items.Select(i => new SavedItem(i.Role, i.Text, i.Detail, i.ToolId, i.File, i.Failed,
                    i.Todos?.Select(t => new[] { t.text, t.status }).ToList())).ToList();
                Directory.CreateDirectory(ChatsDir);
                var tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(new SavedChat(Id, Title, CurrentScope, Milestone, MilestoneTitle, _session, Created, Updated,
                    CostUsd, ContextTokens, ContextWindow, Turns, ActiveModel, items)));
                File.Move(tmp, FilePath, overwrite: true);
            }
            catch { /* a chat that cannot be saved still works for this run */ }
        }

        public static ClaudeChat? Load(string path, LauncherSettings s, Toolchain tools, ClaudeCli cli)
        {
            try
            {
                var c = JsonSerializer.Deserialize<SavedChat>(File.ReadAllText(path));
                if (c == null) return null;
                var chat = new ClaudeChat(s, tools, cli, c.Scope, c.Milestone, c.MilestoneTitle)
                {
                    Id = c.Id, Title = c.Title, _session = c.Session, Created = c.Created, Updated = c.Updated, CostUsd = c.Cost,
                    ContextTokens = c.ContextTokens, ContextWindow = c.ContextWindow, Turns = c.Turns, ActiveModel = c.Model,
                };
                foreach (var i in c.Items)
                    chat._items.Add(new ChatItem(i.Role, i.Text)
                    {
                        Detail = i.Detail, ToolId = i.ToolId, File = i.File, Failed = i.Failed,
                        Todos = i.Todos?.Select(t => (t[0], t.Length > 1 ? t[1] : "pending")).ToList(),
                    });
                return chat;
            }
            catch { return null; }
        }

        /// <summary>Removes the conversation from the list (the CLI's own session history stays where Claude Code keeps it).</summary>
        public void Delete()
        {
            Stop();
            try { File.Delete(FilePath); File.Delete(Path.Combine(ChatsDir, $"mcp-{Id}.json")); } catch { }
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
