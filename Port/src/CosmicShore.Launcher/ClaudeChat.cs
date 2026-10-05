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
        readonly object _lock = new();
        readonly List<ChatItem> _items = new();
        Process? _proc;
        string? _session;

        public ClaudeChat(LauncherSettings s) { _s = s; }

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

        /// <summary>Anthropic's official installer (no Node needed).</summary>
        public async Task Install(LogBuffer log)
        {
            if (Installing) return;
            Installing = true;
            try
            {
                var (file, args) = OperatingSystem.IsWindows()
                    ? ("powershell", new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", "irm https://claude.ai/install.ps1 | iex" })
                    : ("bash", new[] { "-c", "curl -fsSL https://claude.ai/install.sh | bash" });
                Add(ChatRole.System, "Installing Claude Code...");
                var r = await ProcessRunner.Run(file, args, null, log, CancellationToken.None, null);
                Add(Detect() != null ? ChatRole.System : ChatRole.Error,
                    Detect() != null ? "Claude Code installed." : $"Install failed (exit {r.ExitCode}) - see CONSOLE.");
            }
            finally { Installing = false; }
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
                psi.ArgumentList.Add("--append-system-prompt");
                psi.ArgumentList.Add("You are running inside the Froglet Engine Launcher, in a checkout of the Cosmic Shore repository " +
                                     $"(branch {_s.Branch}). Keep replies short; the user reads them in a small chat panel.");
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
                }
            }
            catch (Exception ex) { Add(ChatRole.Error, ex.Message); }
            finally { Busy = false; }
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
