using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The CLAUDE page: Prisma's agent, laid out the way Claude Code lays out a session -
    /// a bullet per message and tool call, each tool's result folded under it, plans with approve
    /// buttons, to-do lists as checklists, the game's screenshots inline - with model, effort and
    /// permission mode on the bar, one-click test runs, session reports and voice.
    /// </summary>
    public sealed partial class LauncherApp
    {
        Voice? _voice;
        readonly ConcurrentQueue<string> _heard = new();
        readonly Dictionary<byte[], (uint tex, Vector2 size)> _shotTex = new(ReferenceEqualityComparer.Instance);
        readonly List<string> _chatHistory = new();
        int _historyAt = -1;
        bool _chatBusyWas;

        /// <summary>The launcher's fonts are Latin-only: spell the symbols Claude likes in ASCII.</summary>
        static string Glyphs(string s) => s
            .Replace("\u2192", "->").Replace("\u2190", "<-").Replace("\u21d2", "=>").Replace("\u2014", " - ").Replace("\u2013", "-")
            .Replace("\u2026", "...").Replace("\u2713", "v").Replace("\u2714", "v").Replace("\u2717", "x").Replace("\u2022", "-")
            .Replace("\u2018", "'").Replace("\u2019", "'").Replace("\u201c", "\"").Replace("\u201d", "\"").Replace("\u00d7", "x")
            .Replace("\u2265", ">=").Replace("\u2264", "<=").Replace("\u2260", "!=").Replace("\u2248", "~");

        string ChatDir => _ws.Exists ? _ws.Dir : LauncherSettings.DataDir;

        void SendChat(string text, ClaudeChat.Mode mode, string? extraDir = null)
        {
            text = text.Trim();
            if (text.Length == 0) return;
            if (text.StartsWith('/') && SlashCommand(text)) return;
            _chatHistory.Add(text);
            _historyAt = -1;
            _chat.Send(text, ChatDir, mode, extraDir ?? LauncherJobs.SessionsDir);
        }

        /// <summary>Claude Code's own slash commands that make sense here.</summary>
        bool SlashCommand(string text)
        {
            var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string arg = parts.Length > 1 ? parts[1].Trim() : "";
            switch (parts[0].ToLowerInvariant())
            {
                case "/clear": if (!_chat.Busy) _chat.NewChat(); return true;
                case "/new": _chats.New(); return true;
                case "/usage": case "/context": case "/cost": _usageOpen = true; return true;
                case "/rename" when arg.Length > 0: _chat.Title = arg; _chat.Save(); return true;
                case "/plan": _s.ChatMode = 0; _dirty = true; return true;
                case "/edit": _s.ChatMode = 1; _dirty = true; return true;
                case "/auto": _s.ChatMode = 2; _dirty = true; return true;
                case "/model": _s.ClaudeModel = arg; _dirty = true; return true;
                case "/effort": _s.ClaudeEffort = arg; _dirty = true; return true;
                case "/test": SendChat(Prompts.Tests, (ClaudeChat.Mode)_s.ChatMode); return true;
                case "/smoke": SendChat(Prompts.Smoke, (ClaudeChat.Mode)_s.ChatMode); return true;
                case "/session": SendLastSession(); return true;
                default: return false; // anything else goes to Claude as typed
            }
        }

        static class Prompts
        {
            public const string Tests =
                "Run the engine's unit tests (engine_test). Report the result in one line; for any failure, name the test, the likely cause in the engine and the fix.";
            public const string Smoke =
                "Run engine_smoke. Report whether the game booted to the expected scene, every error or exception it logged, and for each one where in Port/ it most likely comes from.";
            public const string Parity =
                "Pick one gameplay system the engine may not reproduce exactly (read Port/docs/ROADMAP.md for the open list). Read the game's own source for it in Assets/_Scripts, " +
                "run it in the engine with the game tools, compare what you see with what the source says Unity does, and list every difference with the engine file to change.";
        }

        void SendLastSession()
        {
            var last = LauncherJobs.Sessions().FirstOrDefault();
            if (last == null) { _chat.Note("No play session yet: press START, play, close the game, then try again."); return; }
            SendChat("Analyse my last play session in Prisma. The report is " + last.FullName +
                     " (scenes, frame-time percentiles, every distinct error/warning/exception with counts, and the crash if any). " +
                     "Rank what needs fixing in the engine, say which Port/ code each item points to, and propose the fixes.",
                (ClaudeChat.Mode)_s.ChatMode, LauncherJobs.SessionsDir);
        }

        bool _usageOpen;

        void DrawChat(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            if (!_chatDetected)
            {
                _chatDetected = true;
                Task.Run(() => { _chat.Detect(); _chat.RefreshAuth(); });
            }
            if (_chat.Cli == null)
            {
                PageHeader(a, "PRISMA AGENT", "Works on Cosmic Shore  ·  powered by Claude");
                DrawChatInstall(dl, a, b);
                return;
            }
            // Conversations down the left, the way Claude Code keeps sessions: each its own transcript and context.
            const float listW = 250;
            DrawChatList(a, new Vector2(a.X + listW, b.Y));
            a = new Vector2(a.X + listW + 18, a.Y);

            bool milestone = _chat.CurrentScope == ClaudeChat.Scope.Milestone;
            PageHeader(a, milestone ? "MILESTONE " + _chat.Milestone : "PRISMA AGENT",
                milestone ? $"{_chat.MilestoneTitle}  ·  engine work on Prisma (Port/)  ·  powered by Claude"
                          : "Works on Cosmic Shore  ·  powered by Claude");

            DrawChatBar(a, b);

            float inputH = 84, chipsH = 38, statusH = 24;
            var ta = new Vector2(a.X, a.Y + 112);
            var tb = new Vector2(b.X, b.Y - inputH - chipsH - statusH - 20);
            Neon.ChamferFill(dl, ta, tb, 10, Neon.U(Neon.Space0, 0.74f));
            ImGui.SetCursorScreenPos(ta + new Vector2(20, 14));
            ImGui.BeginChild("##chat", tb - ta - new Vector2(40, 28));
            _chat.Snapshot(_chatSnap);
            float wrap = tb.X - ta.X - 110;
            if (_chatSnap.Count == 0) DrawChatWelcome();
            foreach (var it in _chatSnap) DrawChatItem(it, wrap);
            if (_chat.Busy) DrawWorking();
            // Follow the conversation: new rows, and the moment a turn ends (a plan card gains its buttons).
            if (_chatSnap.Count != _chatSeen || _chat.Busy != _chatBusyWas) { ImGui.SetScrollHereY(1f); _chatSeen = _chatSnap.Count; _chatBusyWas = _chat.Busy; }
            ImGui.EndChild();

            DrawChatStatus(new Vector2(a.X + 4, tb.Y + 6), b.X);
            DrawChips(new Vector2(a.X, tb.Y + statusH + 6));
            DrawChatInput(a, b, inputH);
            if (_usageOpen) DrawUsage(a, b);
        }

        /// <summary>The chat list: NEW CHAT, then every conversation, running ones first, each with its scope and when it was last used.</summary>
        void DrawChatList(Vector2 a, Vector2 b)
        {
            var dl = ImGui.GetWindowDrawList();
            Neon.ChamferFill(dl, a, b, 10, Neon.U(Neon.Space0, 0.6f));
            ImGui.SetCursorScreenPos(a + new Vector2(12, 12));
            if (SmallButton("+  NEW CHAT", b.X - a.X - 24, true)) _chats.New();
            Neon.Tooltip("A new conversation. The others keep their history and can keep working.");
            ImGui.SetCursorScreenPos(a + new Vector2(6, 60));
            ImGui.BeginChild("##chatlist", new Vector2(b.X - a.X - 12, b.Y - a.Y - 66));
            ClaudeChat? delete = null;
            float w = b.X - a.X - 14;
            foreach (var c in _chats.All)
            {
                if (c.Empty && !ReferenceEquals(c, _chat)) continue;
                var p = ImGui.GetCursorScreenPos();
                ImGui.PushID(c.Id);
                bool active = ReferenceEquals(c, _chat);
                if (ImGui.InvisibleButton("row", new Vector2(w, 50))) _chats.Show(c);
                bool hov = ImGui.IsItemHovered();
                var d = ImGui.GetWindowDrawList();
                if (active || hov) d.AddRectFilled(p, p + new Vector2(w, 50), Neon.U(active ? Neon.Cyan : Neon.Ink, active ? 0.10f : 0.05f), 8);
                if (active) d.AddRectFilled(p + new Vector2(0, 10), p + new Vector2(3, 40), Neon.U(Neon.Cyan), 2);
                if (c.Busy) d.AddCircleFilled(p + new Vector2(12, 17), 3.5f, Neon.U(Neon.Amber, 0.55f + 0.45f * MathF.Sin(Neon.Time * 6)));
                string title = c.Title.Length > 0 ? c.Title : "New chat";
                d.AddText(Neon.Small, 15, p + new Vector2(c.Busy ? 22 : 12, 7), Neon.U(active ? Neon.Ink : Neon.Mix(Neon.Ink, Neon.Dim, 0.3f)), Trim(Glyphs(title), (int)(w / 8.2f)));
                string tag = c.CurrentScope == ClaudeChat.Scope.Milestone ? "ENGINE " + c.Milestone : "GAME";
                d.AddText(Neon.Small, 12, p + new Vector2(12, 29), Neon.U(c.CurrentScope == ClaudeChat.Scope.Milestone ? Neon.Violet : Neon.Dim),
                    c.Busy ? $"{tag}  ·  working" : $"{tag}  ·  {Ago(c.Updated)}");
                if (hov && !c.Busy)
                {
                    var xa = p + new Vector2(w - 26, 14);
                    bool xh = ImGui.IsMouseHoveringRect(xa, xa + new Vector2(20, 20));
                    d.AddLine(xa + new Vector2(5, 5), xa + new Vector2(15, 15), Neon.U(xh ? Neon.Red : Neon.Dim), 1.6f);
                    d.AddLine(xa + new Vector2(15, 5), xa + new Vector2(5, 15), Neon.U(xh ? Neon.Red : Neon.Dim), 1.6f);
                    if (xh) { ImGui.SetTooltip("Delete this chat"); if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) delete = c; }
                }
                ImGui.PopID();
                ImGui.SetCursorScreenPos(p + new Vector2(0, 54));
            }
            ImGui.Dummy(Vector2.Zero);
            ImGui.EndChild();
            if (delete != null) _chats.Delete(delete);
        }

        static string Ago(DateTime t)
        {
            var d = DateTime.Now - t;
            return d.TotalMinutes < 1 ? "just now" : d.TotalHours < 1 ? $"{(int)d.TotalMinutes} min ago" : d.TotalDays < 1 ? $"{(int)d.TotalHours} h ago"
                : d.TotalDays < 7 ? t.ToString("ddd HH:mm") : t.ToString("MMM d");
        }

        static string Resets(DateTimeOffset? at)
        {
            if (at is not { } r) return "";
            var local = r.ToLocalTime();
            return "Resets " + (local.Date == DateTime.Today ? local.ToString("h:mmtt").ToLowerInvariant() : local.ToString("MMM d, h:mmtt").Replace("AM", "am").Replace("PM", "pm"));
        }

        /// <summary>
        /// Claude Code's /usage and /context in one card: this chat's context window, the plan's
        /// session and weekly limits with when each resets, and what the chat cost on an API key.
        /// </summary>
        void DrawUsage(Vector2 a, Vector2 b)
        {
            var size = new Vector2(520, 0);
            var cli = _chats.Cli;
            var windows = cli.Windows;
            float h = 120 + Math.Max(1, windows.Count) * 58 + 56;
            var pa = new Vector2(b.X - size.X - 10, b.Y - h - 190);
            var pb = pa + new Vector2(size.X, h);
            var fg = ImGui.GetForegroundDrawList();
            Neon.ChamferFill(fg, pa, pb, 12, Neon.U(Neon.Space0, 0.97f));
            Neon.ChamferFill(fg, pa, pb, 12, Neon.U(Neon.Panel));
            Neon.ChamferGlow(fg, pa, pb, 12, Neon.Cyan, 0.5f);
            fg.AddText(Neon.Strong, 17, pa + new Vector2(22, 16), Neon.U(Neon.Ink), "USAGE");
            fg.AddText(Neon.Small, 13, pa + new Vector2(90, 19), Neon.U(Neon.Dim), "as Claude Code's /usage and /context show it");
            void Bar(float y, string label, double frac, string right, string under)
            {
                fg.AddText(Neon.Small, 14, new Vector2(pa.X + 22, y), Neon.U(Neon.Ink), label);
                var ra = new Vector2(pa.X + 22, y + 22); var rb = new Vector2(pb.X - 110, y + 32);
                fg.AddRectFilled(ra, rb, Neon.U(Neon.Ink, 0.08f), 5);
                float f = (float)Math.Clamp(frac, 0, 1);
                if (f > 0) fg.AddRectFilled(ra, new Vector2(ra.X + (rb.X - ra.X) * f, rb.Y), Neon.U(f > 0.9f ? Neon.Red : f > 0.7f ? Neon.Amber : Neon.Cyan), 5);
                fg.AddText(Neon.Small, 14, new Vector2(rb.X + 12, y + 18), Neon.U(Neon.Ink), right);
                if (under.Length > 0) fg.AddText(Neon.Small, 12, new Vector2(pa.X + 22, y + 36), Neon.U(Neon.Dim), under);
            }
            float y0 = pa.Y + 54;
            long win = _chat.ContextWindow > 0 ? _chat.ContextWindow : 200_000;
            Bar(y0, "Context  ·  " + (_chat.Title.Length > 0 ? Trim(Glyphs(_chat.Title), 44) : "this chat"), (double)_chat.ContextTokens / win,
                $"{(double)_chat.ContextTokens / win:0%}", $"{_chat.ContextTokens / 1000.0:0.0}k of {win / 1000}k tokens  ·  {_chat.Turns} turns" + (_chat.ContextWindow > 0 ? "" : "  ·  window assumed until the first reply"));
            float y = y0 + 64;
            if (windows.Count == 0)
            {
                fg.AddText(Neon.Small, 14, new Vector2(pa.X + 22, y), Neon.U(Neon.Dim),
                    string.IsNullOrWhiteSpace(_s.AnthropicApiKey) ? "Plan limits appear after the first reply (Claude reports them with every run)." : "On an API key there are no plan limits; you pay per token.");
                y += 58;
            }
            foreach (var w in windows)
            {
                Bar(y, w.Label, w.Utilization, $"{w.Utilization:0%} used", Resets(w.ResetsAt));
                y += 58;
            }
            double total = _chats.All.Sum(c => c.CostUsd);
            string foot = $"This chat ${_chat.CostUsd:0.00}  ·  all chats ${total:0.00}" + (string.IsNullOrWhiteSpace(_s.AnthropicApiKey) ? "  (list price; a plan is not billed per token)" : "");
            if (cli.UsageAt != default) foot += $"\nLimits as of {cli.UsageAt:HH:mm}" + (cli.UsageStatus == "rejected" ? "  ·  AT THE LIMIT" : cli.UsageStatus == "allowed_warning" ? "  ·  close to the limit" : "");
            fg.AddText(Neon.Small, 13, new Vector2(pa.X + 22, y + 4), Neon.U(Neon.Dim), foot);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(pa, pb)) _usageOpen = false;
            if (ImGui.IsKeyPressed(ImGuiKey.Escape)) _usageOpen = false;
        }

        void DrawChatBar(Vector2 a, Vector2 b)
        {
            // Its own row under the title, so it fits beside the chat list at any window width.
            ImGui.SetCursorScreenPos(new Vector2(a.X, a.Y + 64));
            if (_chat.SignedIn == false && string.IsNullOrWhiteSpace(_s.AnthropicApiKey))
            {
                if (SmallButton("SIGN IN", 110, true)) _chat.SignIn();
                Neon.Tooltip("Sign in with your Claude account to use your Pro/Max plan.");
                ImGui.SameLine(0, 12);
            }
            ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(0, 4));
            ImGui.PushItemWidth(120);
            int model = Math.Max(0, Array.IndexOf(ClaudeChat.Models, string.IsNullOrWhiteSpace(_s.ClaudeModel) ? "default" : _s.ClaudeModel.Trim()));
            if (model < 0) model = 0;
            if (ImGui.Combo("##model", ref model, ClaudeChat.Models, ClaudeChat.Models.Length)) { _s.ClaudeModel = model == 0 ? "" : ClaudeChat.Models[model]; _dirty = true; }
            Neon.Tooltip("Model. 'default' is your account's default; the others are the latest of each family.");
            ImGui.SameLine(0, 8);
            int effort = Math.Max(0, Array.IndexOf(ClaudeChat.Efforts, string.IsNullOrWhiteSpace(_s.ClaudeEffort) ? "default" : _s.ClaudeEffort.Trim()));
            if (ImGui.Combo("##effort", ref effort, ClaudeChat.Efforts, ClaudeChat.Efforts.Length)) { _s.ClaudeEffort = effort == 0 ? "" : ClaudeChat.Efforts[effort]; _dirty = true; }
            Neon.Tooltip("Effort: how hard Claude thinks before answering. Higher is slower and costs more.");
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 12);
            Segmented("cmode", new[] { "PLAN", "EDIT", "AUTO" }, Math.Clamp(_s.ChatMode, 0, 2), i => _s.ChatMode = i, Neon.Magenta);
            Neon.Tooltip("PLAN: reads, runs the game and proposes a plan; changes nothing (Claude Code's plan mode).\n" +
                         "EDIT: may edit engine files.\nAUTO: may also run any command.\n" +
                         "In every mode Assets/, Packages/ and ProjectSettings/ are off limits.");
            ImGui.SameLine(0, 12);
            if (SmallButton("CLEAR", 80, !_chat.Busy)) _chat.NewChat();
            Neon.Tooltip("Start this chat over (Claude Code's /clear). + NEW CHAT keeps it and opens another.");
        }

        void DrawChatWelcome()
        {
            ImGui.Dummy(new Vector2(0, 8));
            ImGui.PushFont(Neon.Heading); ImGui.TextColored(Neon.Ink, _chat.CurrentScope == ClaudeChat.Scope.Game ? "Prisma Agent" : "Milestone " + _chat.Milestone); ImGui.PopFont();
            ImGui.PushFont(Neon.Small);
            foreach (var l in new[]
            {
                _chat.CurrentScope == ClaudeChat.Scope.Game
                    ? "Works on the game (Assets/) - only on what you ask. Ask about a bug or a run and it reads TRACKS."
                    : "Engine work on Prisma (Port/) for this checkpoint. The game in Assets/ is read-only here.",
                "It can run the tests, start the game, look at it and drive it, and suggest bugs and tasks for your BOARD.",
                "PLAN proposes before touching anything; approve the plan to let it build.",
                "Chips below run tests, a smoke test, or analyse your last play session.",
                "Its edits stay in Prisma's workspace until you save them on the GIT page.",
                "Commands: /new  /clear  /rename TITLE  /usage  /plan  /edit  /auto  /model NAME  /effort LEVEL  /test  /smoke  /session",
            }) ImGui.TextColored(Neon.Dim, l);
            ImGui.PopFont();
        }

        static void Bullet(ImDrawListPtr dl, Vector2 at, Vector4 col, float r = 4f) => dl.AddCircleFilled(at + new Vector2(r, 11), r, Neon.U(col), 12);

        void DrawChatItem(ChatItem it, float wrap)
        {
            it.Text = Glyphs(it.Text);
            if (it.Detail != null) it.Detail = Glyphs(it.Detail);
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            switch (it.Role)
            {
                case ChatRole.User:
                {
                    ImGui.Dummy(new Vector2(0, 8));
                    p = ImGui.GetCursorScreenPos();
                    ImGui.SetCursorScreenPos(p + new Vector2(22, 0));
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
                    ImGui.TextColored(Neon.Mix(Neon.Ink, Neon.Dim, 0.35f), it.Text);
                    ImGui.PopTextWrapPos();
                    var end = ImGui.GetItemRectMax();
                    dl.AddRectFilled(p - new Vector2(4, 4), new Vector2(p.X + wrap + 30, end.Y + 4), Neon.U(Neon.Cyan, 0.05f), 4);
                    dl.AddText(Neon.Mono, 16, p + new Vector2(2, 1), Neon.U(Neon.Cyan), ">");
                    ImGui.Dummy(new Vector2(0, 4));
                    break;
                }
                case ChatRole.Assistant:
                    ImGui.Dummy(new Vector2(0, 4));
                    Bullet(dl, ImGui.GetCursorScreenPos(), Neon.Ink);
                    ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(22, 0));
                    ImGui.BeginGroup();
                    Markdown(it.Text, wrap);
                    ImGui.EndGroup();
                    break;
                case ChatRole.Tool:
                {
                    bool done = it.Detail != null;
                    var col = !done ? Neon.Mix(Neon.Dim, Neon.Cyan, 0.5f + 0.5f * MathF.Sin(Neon.Time * 6)) : it.Failed ? Neon.Red : Neon.Lime;
                    ImGui.Dummy(new Vector2(0, 2));
                    Bullet(dl, ImGui.GetCursorScreenPos(), col);
                    ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(22, 0));
                    ImGui.PushFont(Neon.Mono); ImGui.TextColored(Neon.Ink, it.Text); ImGui.PopFont();
                    if (it.Detail != null)
                    {
                        var q = ImGui.GetCursorScreenPos() + new Vector2(30, 0);
                        dl.AddLine(q + new Vector2(-14, 0), q + new Vector2(-14, 9), Neon.U(Neon.Dim, 0.7f));
                        dl.AddLine(q + new Vector2(-14, 9), q + new Vector2(-5, 9), Neon.U(Neon.Dim, 0.7f));
                        ImGui.SetCursorScreenPos(q);
                        ImGui.PushFont(Neon.Mono);
                        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap - 20);
                        ImGui.TextColored(it.Failed ? Neon.Red : Neon.Dim, it.Detail);
                        ImGui.PopTextWrapPos();
                        ImGui.PopFont();
                    }
                    if (it.Image != null) DrawShot(it.Image, wrap);
                    break;
                }
                case ChatRole.Todo:
                    ImGui.Dummy(new Vector2(0, 2));
                    Bullet(dl, ImGui.GetCursorScreenPos(), Neon.Amber);
                    ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(22, 0));
                    ImGui.TextColored(Neon.Ink, "Update todos");
                    foreach (var (text, status) in it.Todos ?? new())
                    {
                        var q = ImGui.GetCursorScreenPos() + new Vector2(30, 4);
                        var box = new Vector2(12, 12);
                        if (status == "completed") dl.AddRectFilled(q, q + box, Neon.U(Neon.Lime, 0.9f), 2);
                        else dl.AddRect(q, q + box, Neon.U(status == "in_progress" ? Neon.Cyan : Neon.Dim), 2, ImDrawFlags.None, 1.5f);
                        ImGui.SetCursorScreenPos(q + new Vector2(22, -4));
                        ImGui.PushFont(Neon.Small);
                        ImGui.TextColored(status == "completed" ? Neon.Dim : status == "in_progress" ? Neon.Cyan : Neon.Ink, text);
                        ImGui.PopFont();
                    }
                    break;
                case ChatRole.Plan:
                {
                    ImGui.Dummy(new Vector2(0, 6));
                    var pa = ImGui.GetCursorScreenPos();
                    dl.ChannelsSplit(2);
                    dl.ChannelsSetCurrent(1);
                    ImGui.SetCursorScreenPos(pa + new Vector2(16, 12));
                    ImGui.BeginGroup();
                    ImGui.PushFont(Neon.Heading); ImGui.TextColored(Neon.Magenta, "PLAN"); ImGui.PopFont();
                    Markdown(it.Text, wrap - 30);
                    ImGui.Dummy(new Vector2(0, 6));
                    bool latest = ReferenceEquals(it, _chatSnap.LastOrDefault(x => x.Role == ChatRole.Plan));
                    if (latest && !_chat.Busy)
                    {
                        ImGui.PushID(it.ToolId ?? "plan");
                        if (SmallButton("APPROVE + EDIT", 170, true)) { _s.ChatMode = 1; _dirty = true; SendChat("The plan is approved. Implement it.", ClaudeChat.Mode.Edit); }
                        ImGui.SameLine(0, 8);
                        if (SmallButton("APPROVE + AUTO", 170, true)) { _s.ChatMode = 2; _dirty = true; SendChat("The plan is approved. Implement it.", ClaudeChat.Mode.Auto); }
                        ImGui.SameLine(0, 8);
                        ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, "or type changes to keep planning"); ImGui.PopFont();
                        ImGui.PopID();
                    }
                    ImGui.EndGroup();
                    var pb = new Vector2(pa.X + wrap + 40, ImGui.GetItemRectMax().Y + 12);
                    dl.ChannelsSetCurrent(0);
                    Neon.ChamferFill(dl, pa, pb, 8, Neon.U(Neon.Magenta, 0.06f));
                    Neon.ChamferGlow(dl, pa, pb, 8, Neon.Magenta, 0.6f);
                    dl.ChannelsMerge();
                    ImGui.SetCursorScreenPos(new Vector2(pa.X, pb.Y + 6));
                    ImGui.Dummy(new Vector2(0, 0));
                    break;
                }
                case ChatRole.System:
                    ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, it.Text); ImGui.PopFont();
                    break;
                case ChatRole.Error:
                    Bullet(dl, ImGui.GetCursorScreenPos(), Neon.Red);
                    ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(22, 0));
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
                    ImGui.TextColored(Neon.Red, it.Text);
                    ImGui.PopTextWrapPos();
                    break;
            }
        }

        /// <summary>Enough Markdown for Claude's replies: headings, bullets, numbered lists, code blocks, bold and inline code markers dropped.</summary>
        void Markdown(string text, float wrap)
        {
            bool code = false;
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                var line = raw;
                if (line.TrimStart().StartsWith("```")) { code = !code; continue; }
                if (code)
                {
                    var p = ImGui.GetCursorScreenPos();
                    ImGui.GetWindowDrawList().AddRectFilled(p - new Vector2(4, 0), p + new Vector2(wrap, 19), Neon.U(Neon.Space1, 0.9f));
                    ImGui.PushFont(Neon.Mono); ImGui.TextColored(Neon.Mix(Neon.Ink, Neon.Cyan, 0.3f), line.Length == 0 ? " " : line); ImGui.PopFont();
                    continue;
                }
                line = line.Replace("**", "").Replace("`", "");
                if (line.StartsWith("#"))
                {
                    ImGui.PushFont(Neon.Heading);
                    ImGui.TextColored(Neon.Ink, line.TrimStart('#', ' '));
                    ImGui.PopFont();
                    continue;
                }
                var trimmed = line.TrimStart();
                int indent = (line.Length - trimmed.Length) / 2;
                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    var p = ImGui.GetCursorScreenPos() + new Vector2(8 + indent * 18, 0);
                    ImGui.GetWindowDrawList().AddCircleFilled(p + new Vector2(0, 11), 2.4f, Neon.U(Neon.Dim));
                    ImGui.SetCursorScreenPos(p + new Vector2(12, 0));
                    ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap - 20 - indent * 18);
                    ImGui.TextUnformatted(trimmed[2..]);
                    ImGui.PopTextWrapPos();
                    continue;
                }
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
                ImGui.TextWrapped(line.Length == 0 ? " " : line);
                ImGui.PopTextWrapPos();
            }
        }

        /// <summary>A screenshot a tool returned (game_screenshot), drawn in the transcript.</summary>
        unsafe void DrawShot(byte[] png, float wrap)
        {
            if (!_shotTex.TryGetValue(png, out var t))
            {
                try
                {
                    var img = ImageResult.FromMemory(png, ColorComponents.RedGreenBlueAlpha);
                    uint tex = _gl.GenTexture();
                    _gl.BindTexture(TextureTarget.Texture2D, tex);
                    fixed (byte* p = img.Data)
                        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
                    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    t = (tex, new Vector2(img.Width, img.Height));
                }
                catch { t = (0, Vector2.Zero); }
                _shotTex[png] = t;
            }
            if (t.tex == 0) return;
            float w = MathF.Min(wrap * 0.6f, 520), h = w * t.size.Y / t.size.X;
            var a = ImGui.GetCursorScreenPos() + new Vector2(30, 4);
            ImGui.SetCursorScreenPos(a);
            ImGui.Image((IntPtr)t.tex, new Vector2(w, h));
            ImGui.GetWindowDrawList().AddRect(a, a + new Vector2(w, h), Neon.U(Neon.Cyan, 0.35f));
            ImGui.Dummy(new Vector2(0, 4));
        }

        void DrawWorking()
        {
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos() + new Vector2(8, 12);
            float t = Neon.Time;
            for (int i = 0; i < 6; i++)
            {
                float ang = t * 2.4f + i * MathF.PI / 3;
                dl.AddLine(p, p + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * 7, Neon.U(Neon.Amber, 0.5f + 0.5f * MathF.Sin(t * 5 + i)), 2f);
            }
            ImGui.SetCursorScreenPos(ImGui.GetCursorScreenPos() + new Vector2(22, 2));
            int secs = (int)(DateTime.UtcNow - _chat.BusySince).TotalSeconds;
            string[] words = { "Working", "Building", "Reading", "Thinking", "Checking" };
            ImGui.TextColored(Neon.Amber, $"{words[(secs / 6) % words.Length]}...  ");
            ImGui.SameLine(0, 0);
            ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, $"{secs}s  ·  STOP to interrupt"); ImGui.PopFont();
        }

        /// <summary>Claude Code's status line: model, mode, how full the context is, the plan's session and week; click for the full card.</summary>
        void DrawChatStatus(Vector2 at, float right)
        {
            var dl = ImGui.GetWindowDrawList();
            string model = _chat.ActiveModel ?? (string.IsNullOrWhiteSpace(_s.ClaudeModel) ? "default model" : _s.ClaudeModel);
            string mode = _s.ChatMode switch { 0 => "plan mode", 1 => "accept edits", _ => "auto" };
            long win = _chat.ContextWindow > 0 ? _chat.ContextWindow : 200_000;
            string ctx = _chat.ContextTokens > 0 ? $"  ·  context {(double)_chat.ContextTokens / win:0%}" : "";
            bool api = !string.IsNullOrWhiteSpace(_s.AnthropicApiKey);
            string plan = "";
            foreach (var w in _chats.Cli.Windows.Where(w => w.Key is "five_hour" or "seven_day"))
                plan += $"  ·  {(w.Key == "five_hour" ? "session" : "week")} {w.Utilization:0%}";
            string cost = api && _chat.CostUsd > 0 ? $"  ·  ${_chat.CostUsd:0.00}" : "";
            string signed = _chat.SignedIn == true ? (api ? "  ·  API key" : "  ·  Claude plan") : "";
            string others = _chats.Running - (_chat.Busy ? 1 : 0) is var n and > 0 ? $"  ·  {n} other chat{(n == 1 ? "" : "s")} working" : "";
            string text = $"{model}  ·  {mode}{ctx}{plan}{cost}{signed}{others}";
            dl.AddCircleFilled(at + new Vector2(4, 9), 3.5f, Neon.U(_chat.Busy ? Neon.Amber : Neon.Lime));
            ImGui.PushFont(Neon.Small);
            float tw = ImGui.CalcTextSize(text).X * 15f / Neon.Small.FontSize;
            ImGui.PopFont();
            var ta = at + new Vector2(14, 1);
            bool hov = ImGui.IsMouseHoveringRect(ta, ta + new Vector2(tw, 18));
            dl.AddText(Neon.Small, 15, ta, Neon.U(hov ? Neon.Ink : Neon.Dim), text);
            if (hov)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                ImGui.SetTooltip("Usage: context, session and weekly limits (/usage)");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _usageOpen = !_usageOpen;
            }
        }

        void DrawChips(Vector2 at)
        {
            ImGui.SetCursorScreenPos(at);
            bool can = !_chat.Busy;
            var mode = (ClaudeChat.Mode)_s.ChatMode;
            if (SmallButton("RUN TESTS", 120, can)) SendChat(Prompts.Tests, mode);
            Neon.Tooltip("Claude runs the engine's unit tests and explains any failure.");
            ImGui.SameLine(0, 8);
            if (SmallButton("SMOKE TEST", 130, can)) SendChat(Prompts.Smoke, mode);
            Neon.Tooltip("Boots the game headless and reports every error on the way.");
            ImGui.SameLine(0, 8);
            if (SmallButton("LAST SESSION", 140, can && LauncherJobs.Sessions().Count > 0)) SendLastSession();
            Neon.Tooltip("Sends the report of your last play session (scenes, frame times, errors) to Claude.");
            ImGui.SameLine(0, 8);
            if (SmallButton("PARITY CHECK", 140, can)) SendChat(Prompts.Parity, ClaudeChat.Mode.Plan);
            Neon.Tooltip("Claude compares one system with the Unity game and lists the differences (plan mode).");
        }

        void DrawChatInput(Vector2 a, Vector2 b, float inputH)
        {
            while (_heard.TryDequeue(out var said)) _chatInput = (_chatInput.TrimEnd() + " " + said).Trim();
            var ia = new Vector2(a.X, b.Y - inputH);
            ImGui.SetCursorScreenPos(ia);
            float buttons = 128 + 2 * 50 + 24;
            bool send = ImGui.InputTextMultiline("##chatin", ref _chatInput, 8000, new Vector2(b.X - a.X - buttons, inputH),
                ImGuiInputTextFlags.CtrlEnterForNewLine | ImGuiInputTextFlags.EnterReturnsTrue);
            if (ImGui.IsItemActive() && _chatHistory.Count > 0 && ImGui.IsKeyPressed(ImGuiKey.UpArrow) && (_chatInput.Length == 0 || _historyAt >= 0))
            {
                _historyAt = _historyAt < 0 ? _chatHistory.Count - 1 : Math.Max(0, _historyAt - 1);
                _chatInput = _chatHistory[_historyAt];
            }
            if (_chatInput.Length == 0 && !ImGui.IsItemActive())
                ImGui.GetWindowDrawList().AddText(Neon.Small, 15, ia + new Vector2(10, 8), Neon.U(Neon.Dim, 0.8f),
                    _s.ChatMode == 0 ? "Ask, or describe what to change - Claude plans first" : "Tell Claude what to do with the engine   (Enter sends, Ctrl+Enter new line)");

            // voice: talk instead of typing, and optionally hear the replies
            ImGui.SameLine(0, 8);
            _voice ??= new Voice(t => _heard.Enqueue(t));
            bool listening = _voice.Listening;
            if (Neon.IconButton("mic", (dl, c, col) => IconMic(dl, c, listening ? Neon.U(Neon.Red) : col), 42, _voice.Supported))
            {
                if (listening) _voice.StopListening(); else _voice.StartListening();
            }
            Neon.Tooltip(!_voice.Supported ? "Voice needs Windows (its built-in speech recognition)." :
                listening ? "Listening - click to stop." : _voice.Error ?? "Talk instead of typing.");
            ImGui.SameLine(0, 6);
            bool speak = _s.VoiceReplies;
            if (Neon.IconButton("spk", (dl, c, col) => IconSpeaker(dl, c, speak ? Neon.U(Neon.Cyan) : col), 42, _voice.Supported))
            {
                _s.VoiceReplies = !speak; _dirty = true;
                if (speak) _voice.StopSpeaking();
            }
            Neon.Tooltip(speak ? "Reading replies aloud - click to stop." : "Read Claude's replies aloud.");
            ImGui.SameLine(0, 10);
            bool can = !_chat.Busy && _chatInput.Trim().Length > 0;
            if (_chat.Busy)
            {
                if (Neon.Button("chatstop", "STOP", new Vector2(128, inputH), Neon.Red, Neon.Heading, 22)) _chat.Stop();
            }
            else if (Neon.Button("chatsend", "SEND", new Vector2(128, inputH), Neon.Magenta, Neon.Heading, 22, can) || (send && can))
            {
                if (_voice.Listening) _voice.StopListening();
                SendChat(_chatInput, (ClaudeChat.Mode)_s.ChatMode);
                _chatInput = "";
            }
        }

        static void IconMic(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddRectFilled(c + new Vector2(-4, -10), c + new Vector2(4, 3), col, 4);
            dl.PathArcTo(c + new Vector2(0, -1), 8, 0, MathF.PI, 16);
            dl.PathStroke(col, ImDrawFlags.None, 1.8f);
            dl.AddLine(c + new Vector2(0, 7), c + new Vector2(0, 11), col, 1.8f);
            dl.AddLine(c + new Vector2(-5, 11), c + new Vector2(5, 11), col, 1.8f);
        }

        static void IconSpeaker(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddRectFilled(c + new Vector2(-9, -3), c + new Vector2(-4, 3), col);
            dl.AddTriangleFilled(c + new Vector2(-4, -3), c + new Vector2(2, -8), c + new Vector2(2, 3), col);
            dl.AddTriangleFilled(c + new Vector2(-4, 3), c + new Vector2(2, -3), c + new Vector2(2, 8), col);
            dl.PathArcTo(c + new Vector2(3, 0), 6, -0.9f, 0.9f, 10); dl.PathStroke(col, ImDrawFlags.None, 1.6f);
            dl.PathArcTo(c + new Vector2(3, 0), 10, -0.9f, 0.9f, 12); dl.PathStroke(col, ImDrawFlags.None, 1.6f);
        }

        void DrawChatInstall(ImDrawListPtr dl, Vector2 a, Vector2 b)
        {
            var c = new Vector2((a.X + b.X) * 0.5f, (a.Y + b.Y) * 0.5f - 60);
            CenterText(dl, Neon.Heading, 22, c.X, c.Y, Neon.Ink, "Claude Code is not installed");
            ImGui.SetCursorScreenPos(new Vector2(c.X - 110, c.Y + 50));
            if (!_chat.Installing)
            {
                if (Neon.Button("instclaude", "INSTALL", new Vector2(220, 56), Neon.Magenta, Neon.Heading, 22, true))
                    Task.Run(() => _chat.Install(_jobs.Log));
            }
            else
            {
                // The download is ~250 MB: show it moving.
                var pa = new Vector2(c.X - 220, c.Y + 50); var pb = new Vector2(c.X + 220, c.Y + 62);
                dl.AddRectFilled(pa, pb, Neon.U(Neon.Space0, 0.9f), 6);
                float f = _chat.InstallProgress >= 0 ? _chat.InstallProgress : (float)(0.5 + 0.5 * Math.Sin(ImGui.GetTime() * 3));
                var fa = _chat.InstallProgress >= 0 ? pa : new Vector2(pa.X + (pb.X - pa.X) * f * 0.75f, pa.Y);
                var fb = _chat.InstallProgress >= 0 ? new Vector2(pa.X + (pb.X - pa.X) * f, pb.Y) : new Vector2(fa.X + (pb.X - pa.X) * 0.25f, pb.Y);
                dl.AddRectFilled(fa, fb, Neon.U(Neon.Magenta), 6);
                ImGui.SetCursorScreenPos(new Vector2(pa.X, pb.Y + 10));
                ImGui.PushFont(Neon.Small); ImGui.TextColored(Neon.Dim, _chat.InstallStatus); ImGui.PopFont();
                ImGui.SetCursorScreenPos(new Vector2(pb.X - 80, pb.Y + 6));
                if (SmallButton("CANCEL", 80, true)) _chat.CancelInstall();
            }
            ImGui.SetCursorScreenPos(new Vector2(c.X - 220, c.Y + 130));
            SecretField("##akey0", "Anthropic API key (optional)", () => _s.AnthropicApiKey, v => _s.AnthropicApiKey = v, 362);
            Neon.Tooltip("Only for pay-as-you-go API billing (console.anthropic.com).\nOn a Claude Pro/Max plan leave it empty and SIGN IN instead. Stored only on this PC.");
        }
    }
}
