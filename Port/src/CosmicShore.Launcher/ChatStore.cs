using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// Every agent conversation, the way Claude Code keeps sessions: each chat has its own transcript,
    /// its own CLI session to resume, its own context, and can run while another one does. They are
    /// kept in chats/ID.json and come back when Prisma starts. One is shown at a time (<see cref="Active"/>).
    /// </summary>
    public sealed class ChatStore
    {
        readonly LauncherSettings _s;
        readonly Toolchain _tools;
        readonly object _lock = new();
        readonly List<ClaudeChat> _all = new();
        readonly Action<ClaudeChat> _wire;

        public ClaudeCli Cli { get; }
        public ClaudeChat Active { get; private set; }

        /// <summary>Most recently used first.</summary>
        public List<ClaudeChat> All { get { lock (_lock) return _all.OrderByDescending(c => c.Busy).ThenByDescending(c => c.Updated).ToList(); } }
        public int Running { get { lock (_lock) return _all.Count(c => c.Busy); } }

        /// <param name="wire">Called once for every chat, new or loaded, to hook its events.</param>
        public ChatStore(LauncherSettings s, Toolchain tools, Action<ClaudeChat> wire)
        {
            _s = s; _tools = tools; _wire = wire;
            Cli = new ClaudeCli(s);
            if (Directory.Exists(ClaudeChat.ChatsDir))
                foreach (var f in Directory.GetFiles(ClaudeChat.ChatsDir, "*.json").Where(f => !Path.GetFileName(f).StartsWith("mcp-")))
                    if (ClaudeChat.Load(f, s, tools, Cli) is { } c)
                    {
                        // Milestone sessions no longer run in Prisma (engine work happens in Claude Code
                        // at the repository root): their old chats are cleared on the first start.
                        if (c.CurrentScope == ClaudeChat.Scope.Milestone) { c.Delete(); continue; }
                        _all.Add(c); wire(c);
                    }
            Active = All.FirstOrDefault(c => c.CurrentScope == ClaudeChat.Scope.Game) ?? New();
        }

        /// <summary>A fresh conversation, shown at once. An empty game chat already open is reused instead of piling up.</summary>
        public ClaudeChat New(ClaudeChat.Scope scope = ClaudeChat.Scope.Game, string? milestone = null, string? title = null)
        {
            lock (_lock)
            {
                if (scope == ClaudeChat.Scope.Game && _all.FirstOrDefault(c => c.CurrentScope == scope && c.Empty && !c.Busy) is { } blank)
                    return Active = blank;
                var chat = new ClaudeChat(_s, _tools, Cli, scope, milestone, title);
                _all.Add(chat);
                _wire(chat);
                return Active = chat;
            }
        }

        public void Show(ClaudeChat chat) { lock (_lock) if (_all.Contains(chat)) Active = chat; }

        public void Delete(ClaudeChat chat)
        {
            lock (_lock)
            {
                chat.Delete();
                _all.Remove(chat);
                if (ReferenceEquals(Active, chat)) Active = _all.OrderByDescending(c => c.Updated).FirstOrDefault() ?? NewLocked();
            }
        }

        ClaudeChat NewLocked()
        {
            var chat = new ClaudeChat(_s, _tools, Cli);
            _all.Add(chat);
            _wire(chat);
            return chat;
        }

        public void StopAll() { foreach (var c in All) { c.Stop(); c.Save(); } }

        /// <summary>Which chats edited a file (by path, as their edit calls named it).</summary>
        public List<ClaudeChat> EditorsOf(string fullPath)
        {
            var norm = Path.GetFullPath(fullPath);
            return All.Where(c => c.EditedFiles().Any(f =>
            {
                try { return string.Equals(Path.GetFullPath(f), norm, StringComparison.OrdinalIgnoreCase); } catch { return false; }
            })).ToList();
        }
    }
}
