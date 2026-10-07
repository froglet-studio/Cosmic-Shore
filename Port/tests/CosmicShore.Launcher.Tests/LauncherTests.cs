using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CosmicShore.Launcher;

// Every test shares one scratch data folder (settings, chats, usage): run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CosmicShore.Launcher.Tests
{
    static class Setup
    {
        public static readonly string Data = Path.Combine(Path.GetTempPath(), "prisma-launcher-tests-" + Environment.ProcessId);

        /// <summary>Everything the launcher writes goes to a scratch folder, never the real Prisma data.</summary>
        [ModuleInitializer]
        internal static void Init() => Environment.SetEnvironmentVariable("PRISMA_DATA_DIR", Data);

        public static string Git(string dir, params string[] args)
        {
            var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = dir };
            foreach (var a in new[] { "-c", "user.name=Test", "-c", "user.email=t@example.com", "-c", "init.defaultBranch=main" }.Concat(args)) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEnd(); var e = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception($"git {string.Join(' ', args)}: {e}");
            return o.Trim();
        }

        public static string Temp(string name)
        {
            var d = Path.Combine(Data, name + "-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public class UsageTests
    {
        // A real rate_limit_event from `claude -p --output-format stream-json --verbose` (Claude Code 2.1).
        const string Event = """{"status":"allowed","resetsAt":1791424200,"rateLimitType":"five_hour","overageStatus":"rejected","isUsingOverage":false,"unifiedWindows":{"five_hour":{"utilization":0.07,"resetsAt":1791424200},"seven_day":{"utilization":0.31,"resetsAt":1792011600}}}""";

        [Fact]
        public void Plan_windows_read_from_rate_limit_event_and_survive_a_restart()
        {
            var cli = new ClaudeCli(new LauncherSettings());
            using var d = JsonDocument.Parse(Event);
            cli.OnRateLimit(d.RootElement);
            var w = cli.Windows;
            Assert.Equal(new[] { "five_hour", "seven_day" }, w.Select(x => x.Key));
            Assert.Equal("Current session", w[0].Label);
            Assert.Equal("Current week (all models)", w[1].Label);
            Assert.Equal(0.07, w[0].Utilization, 3);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1791424200), w[0].ResetsAt);
            Assert.Equal("allowed", cli.UsageStatus);

            var again = new ClaudeCli(new LauncherSettings());
            Assert.Equal(0.31, again.Windows.Single(x => x.Key == "seven_day").Utilization, 3);
        }

        [Fact]
        public void A_window_whose_reset_passed_reads_zero_after_a_restart()
        {
            var cli = new ClaudeCli(new LauncherSettings());
            using var d = JsonDocument.Parse("""{"status":"allowed","unifiedWindows":{"five_hour":{"utilization":0.9,"resetsAt":1000}}}""");
            cli.OnRateLimit(d.RootElement);
            var again = new ClaudeCli(new LauncherSettings());
            Assert.Equal(0, again.Windows.Single(x => x.Key == "five_hour").Utilization);
        }
    }

    public class ChatTests
    {
        static void Feed(ClaudeChat chat, string line) =>
            typeof(ClaudeChat).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(chat, new object[] { line, new System.Text.StringBuilder() });

        static void UserSays(ClaudeChat chat, string text)
        {
            typeof(ClaudeChat).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(chat, new object[] { ChatRole.User, text });
            if (chat.Title.Length == 0) chat.Title = text;
        }

        [Fact]
        public void A_chat_keeps_its_transcript_edits_session_and_context_window_across_restarts()
        {
            var s = new LauncherSettings();
            var tools = new Toolchain();
            var store = new ChatStore(s, tools, _ => { });
            var chat = store.New();
            UserSays(chat, "Fix the score");
            var file = Path.Combine(Setup.Data, "ws", "Assets", "_Scripts", "Score.cs");
            Feed(chat, """{"type":"system","subtype":"init","session_id":"sess-1","model":"claude-sonnet-5-5"}""");
            Feed(chat, "{\"type\":\"assistant\",\"session_id\":\"sess-1\",\"message\":{\"usage\":{\"input_tokens\":10,\"cache_read_input_tokens\":40000},\"content\":[{\"type\":\"text\",\"text\":\"On it.\"},{\"type\":\"tool_use\",\"id\":\"t1\",\"name\":\"Edit\",\"input\":{\"file_path\":" + JsonSerializer.Serialize(file) + ",\"old_string\":\"a\",\"new_string\":\"b\"}}]}}");
            Feed(chat, """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"ok"}]}}""");
            Feed(chat, """{"type":"rate_limit_event","rate_limit_info":{"status":"allowed","unifiedWindows":{"five_hour":{"utilization":0.2,"resetsAt":1791424200}}}}""");
            Feed(chat, """{"type":"result","subtype":"success","total_cost_usd":0.5,"num_turns":3,"modelUsage":{"claude-haiku-5-5":{"contextWindow":200000},"claude-sonnet-5-5":{"contextWindow":1000000}}}""");

            Assert.Equal(1_000_000, chat.ContextWindow);
            Assert.Equal(40010, chat.ContextTokens);
            Assert.Equal(0.2, store.Cli.Windows.Single(w => w.Key == "five_hour").Utilization, 3);
            Assert.Contains(file, chat.EditedFiles());
            chat.Save();

            // A second chat runs beside the first.
            var other = store.New();
            Assert.NotSame(chat, other);
            UserSays(other, "Something else");
            other.Save();

            var reopened = new ChatStore(s, tools, _ => { });
            var back = reopened.All.Single(c => c.Id == chat.Id);
            Assert.Equal("Fix the score", back.Title);
            Assert.Equal(1_000_000, back.ContextWindow);
            Assert.Equal(0.5, back.CostUsd, 3);
            Assert.Contains(file, back.EditedFiles());
            Assert.Single(reopened.EditorsOf(file), c => c.Id == chat.Id);
            Assert.Equal("sess-1", typeof(ClaudeChat).GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(back));
            Assert.Contains(reopened.All, c => c.Id == other.Id);

            reopened.Delete(back);
            Assert.DoesNotContain(new ChatStore(s, tools, _ => { }).All, c => c.Id == chat.Id);
        }

        [Fact]
        public void New_reuses_an_empty_game_chat_instead_of_piling_up_blanks()
        {
            var store = new ChatStore(new LauncherSettings(), new Toolchain(), _ => { });
            var a = store.New();
            var b = store.New();
            Assert.Same(a, b);
            var m = store.ForMilestone("C1", "Parity harness");
            Assert.Equal(ClaudeChat.Scope.Milestone, m.CurrentScope);
            Assert.Same(m, store.ForMilestone("C1", "Parity harness"));
        }
    }

    public class EditorToolTests
    {
        // The same inputs and outputs as cs-asset's Text_written_by_set_reads_back_unchanged, which proves
        // these quoted forms survive set -> dataset unchanged.
        [Theory]
        [InlineData("Plain words", "Plain words")]
        [InlineData("Fly: fast, then 'stop' # now", "'Fly: fast, then ''stop'' # now'")]
        [InlineData("Two\nlines with \"quotes\"", "\"Two\\nlines with \\\"quotes\\\"\"")]
        [InlineData("", "''")]
        [InlineData("-dash first", "'-dash first'")]
        public void Text_is_quoted_for_cs_asset_set(string text, string yaml) => Assert.Equal(yaml, EditorTool.YamlScalar(text));
    }

    public class SourceControlTests
    {
        static (SourceControl git, Workspace ws, string dir, string bare) Repo()
        {
            var bare = Setup.Temp("remote");
            Setup.Git(bare, "init", "-q", "--bare");
            var dir = Setup.Temp("ws");
            Setup.Git(dir, "init", "-q");
            File.WriteAllText(Path.Combine(dir, "a.txt"), "one\ntwo\n");
            Setup.Git(dir, "add", "-A");
            Setup.Git(dir, "commit", "-q", "-m", "base");
            Setup.Git(dir, "remote", "add", "origin", bare);
            Setup.Git(dir, "push", "-q", "origin", "HEAD:refs/heads/bleeding-edge");
            Setup.Git(dir, "fetch", "-q", "origin");
            Setup.Git(dir, "checkout", "-q", "--detach", "origin/bleeding-edge"); // where START leaves it
            var s = new LauncherSettings { ManagedPath = dir, RemoteUrl = "https://github.com/froglet-studio/Cosmic-Shore.git" };
            var tools = new Toolchain();
            tools.Detect(s);
            var ws = new Workspace(s, tools);
            return (new SourceControl(s, tools, ws), ws, dir, bare);
        }

        [Fact]
        public void Changes_commit_to_a_new_branch_from_a_detached_workspace_and_push()
        {
            var (git, ws, dir, bare) = Repo();
            File.WriteAllText(Path.Combine(dir, "a.txt"), "one\nTWO\nthree\n");
            File.WriteAllText(Path.Combine(dir, "new.cs"), "class X {}\n");
            var st = git.Refresh().Result!;
            Assert.True(st.Detached);
            Assert.Equal(2, ws.PendingChanges());
            var a = st.Changes.Single(c => c.Path == "a.txt");
            Assert.Equal(('M', 2, 1), (a.Letter, a.Added, a.Removed));
            var n = st.Changes.Single(c => c.Path == "new.cs");
            Assert.True(n.Untracked);
            Assert.Contains("+class X {}", git.Diff(n).Result);
            Assert.Contains("+TWO", git.Diff(a).Result);

            var log = new LogBuffer();
            Assert.Null(git.Commit("prisma/fix-score", "fix: score", "Tester", "t@example.com", log).Result);
            st = git.Last!;
            Assert.Equal("prisma/fix-score", st.Branch);
            Assert.Empty(st.Changes);
            Assert.Equal(1, st.Ahead);
            Assert.True(st.Log[0].Unpushed);
            Assert.Equal("Tester", Setup.Git(dir, "log", "-1", "--format=%an"));

            Assert.Null(git.Push(log).Result);
            st = git.Last!;
            Assert.Equal(0, st.Ahead);
            Assert.Equal("origin/prisma/fix-score", st.Upstream);
            Assert.Equal("fix: score", Setup.Git(bare, "log", "-1", "--format=%s", "prisma/fix-score"));
            Assert.Equal("https://github.com/froglet-studio/Cosmic-Shore/compare/bleeding-edge...prisma%2Ffix-score?expand=1", git.PullRequestUrl("prisma/fix-score", "bleeding-edge"));
        }

        [Fact]
        public void Revert_and_discard_put_files_back()
        {
            var (git, ws, dir, _) = Repo();
            File.WriteAllText(Path.Combine(dir, "a.txt"), "changed\n");
            File.WriteAllText(Path.Combine(dir, "junk.txt"), "x\n");
            var st = git.Refresh().Result!;
            var log = new LogBuffer();
            Assert.Null(git.Revert(st.Changes.Single(c => c.Path == "junk.txt"), log).Result);
            Assert.False(File.Exists(Path.Combine(dir, "junk.txt")));
            Assert.Null(git.DiscardAll(log).Result);
            Assert.Equal("one\ntwo\n", File.ReadAllText(Path.Combine(dir, "a.txt")).Replace("\r", ""));
            Assert.Equal(0, ws.PendingChanges());
        }

        [Fact]
        public void Updating_the_workspace_refuses_to_throw_away_unsaved_edits()
        {
            var (_, ws, dir, _) = Repo();
            File.WriteAllText(Path.Combine(dir, "a.txt"), "the agent's edit\n");
            var log = new LogBuffer();
            bool ok = ws.Sync("bleeding-edge", log, (_, _) => { }, default).Result;
            Assert.False(ok);
            Assert.Equal("the agent's edit\n", File.ReadAllText(Path.Combine(dir, "a.txt")));
            var lines = new System.Collections.Generic.List<LogLine>();
            log.CopyTo(lines);
            Assert.Contains(lines, l => l.Text.Contains("unsaved change"));
        }

        [Theory]
        [InlineData("prisma/fix-score", true)]
        [InlineData("has space", false)]
        [InlineData("a..b", false)]
        [InlineData("-x", false)]
        [InlineData("ok/", false)]
        public void Branch_names(string name, bool ok) => Assert.Equal(ok, SourceControl.ValidBranch(name));
    }
}
