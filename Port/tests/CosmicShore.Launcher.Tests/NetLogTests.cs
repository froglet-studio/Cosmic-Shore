using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Amoebius;
using CosmicShore.Launcher;

namespace CosmicShore.Launcher.Tests
{
    /// <summary>
    /// The online test's logs: the file the game writes (<see cref="NetTestLogFormat"/>, the Unity editor's format) and
    /// SEND TO CLAUDE (<see cref="NetLogSender"/>), which adds them to <c>bugledger/&lt;user&gt;</c> without touching
    /// the workspace or the branch's other files.
    /// </summary>
    public class NetLogTests
    {
        static readonly (string?, string?)[] Solo = { ("start", null), ("party", "Idle->Hosting"), ("clientApproved", "0 host") };

        [Fact]
        public void Only_evidence_of_another_player_makes_a_multiplayer_test()
        {
            Assert.Null(NetTestLogFormat.MultiplayerEvidence("host", Solo));          // solo online: the host approves itself
            Assert.Null(NetTestLogFormat.MultiplayerEvidence("none", null));
            Assert.Equal("role client", NetTestLogFormat.MultiplayerEvidence("client", null));
            Assert.Equal("role spectator", NetTestLogFormat.MultiplayerEvidence("spectator", Solo));
            Assert.Equal("client 2 approved", NetTestLogFormat.MultiplayerEvidence("host", Solo.Append(("clientApproved", "2 guest"))));
            // A guest approves nobody: joining a party is its evidence (ys1's side of Caleb's test wrote nothing).
            Assert.Equal("joined another player's party",
                NetTestLogFormat.MultiplayerEvidence("none", Solo.Append(("party", "InPresenceLobby->JoiningParty"))));
            Assert.Null(NetTestLogFormat.MultiplayerEvidence("host", Solo.Append(("party", "JoiningParty->HostingParty"))));
            foreach (var e in new[] { "clientLeft", "leaverToAI", "refused", "bounce" })
                Assert.Equal(e, NetTestLogFormat.MultiplayerEvidence("host", Solo.Append((e, "x"))));
        }

        [Fact]
        public void File_names_and_branch_paths_match_the_unity_editor()
        {
            var name = NetTestLogFormat.FileName(new DateTime(2026, 10, 15, 10, 15, 0, DateTimeKind.Utc), "P2");
            Assert.Equal("net_20261015_101500_p2.json", name);
            Assert.Equal("p2", NetTestLogFormat.PeerOf(name));
            Assert.Equal("main", NetTestLogFormat.PeerName("  "));
            // BugHuntReports.Slug / BugLedgerSender.BranchFor: lower case, anything else becomes '-'.
            Assert.Equal("bugledger/yash-khan", NetTestLogFormat.Branch("Yash Khan"));
            Assert.Equal("BugLedger/shared/reports/yash-khan/net/" + name, NetTestLogFormat.ReportPath("Yash Khan", name));
            Assert.Equal("main, p2 and p3", NetTestLogFormat.Players(new[] { "main", "p2", "p3" }));
            Assert.Equal(new[] { "main", "p2", "p3" }, NetTestLogFormat.PeersOf(new[] { "net_1_2_p3.json", "net_1_2_main.json", "net_1_2_p2.json", "net_1_3_p2.json" }));
        }

        [Fact]
        public void The_file_is_the_unity_schema_and_keeps_the_newest_lines_when_too_big()
        {
            var info = new NetTestLogFormat.Info { Peer = "p2", Why = "second player (p2)", Exit = "window closed", User = "tester", Started = DateTime.UtcNow, Ended = DateTime.UtcNow };
            var lines = Enumerable.Range(0, 4000).Select(i => new NetTestLogFormat.Line(i, i % 2 == 0 ? "log" : "warning", "line " + i + new string('x', 100), i % 2 == 0 ? null : "at Foo()")).ToList();
            string record = """{"schema":"cosmicshore.netsession.v1","role":"client","lifecycle":[{"t":1.5,"e":"start","detail":""}]}""";

            using var doc = JsonDocument.Parse(NetTestLogFormat.Build(record, info, lines, dropped: 3, errors: 0, warnings: 2000, maxBytes: 100_000));
            var root = doc.RootElement;
            Assert.Equal(NetTestLogFormat.Schema, root.GetProperty("schema").GetString());
            Assert.Equal("amoebius", root.GetProperty("engine").GetString());
            Assert.Equal("p2", root.GetProperty("peer").GetString());
            Assert.Equal("client", root.GetProperty("record").GetProperty("role").GetString());
            var console = root.GetProperty("console");
            var kept = console.GetProperty("lines").EnumerateArray().ToList();
            Assert.True(kept.Count < lines.Count);
            Assert.Equal("line 3999", kept[^1].GetProperty("msg").GetString()![..9]);           // the end of the session survives
            Assert.Equal(3 + lines.Count - kept.Count, console.GetProperty("dropped").GetInt32()); // and the cut is counted
            Assert.Equal("at Foo()", kept[^1].GetProperty("at").GetString());
            Assert.False(kept[^2].TryGetProperty("at", out _));
        }

        static (string ws, string bare) Repo()
        {
            var bare = Setup.Temp("ledger-remote");
            Setup.Git(bare, "init", "-q", "--bare");
            var ws = Setup.Temp("ledger-ws");
            Setup.Git(ws, "init", "-q");
            File.WriteAllText(Path.Combine(ws, "game.txt"), "the game\n");
            Setup.Git(ws, "add", "-A");
            Setup.Git(ws, "commit", "-q", "-m", "base");
            Setup.Git(ws, "remote", "add", "origin", bare);
            Setup.Git(ws, "push", "-q", "origin", "HEAD:refs/heads/bleeding-edge");
            Setup.Git(ws, "config", "user.name", "Tester");
            Setup.Git(ws, "config", "user.email", "t@example.com");
            return (ws, bare);
        }

        static FileInfo Log(string dir, string peer, string body = "{}")
        {
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, NetTestLogFormat.FileName(DateTime.UtcNow, peer));
            File.WriteAllText(path, body + "\n");
            return new FileInfo(path);
        }

        [Fact]
        public void Send_adds_the_logs_to_the_users_ledger_branch_and_keeps_everything_else()
        {
            var (ws, bare) = Repo();
            // The Unity editor sent an issue earlier: it must survive an Amoebius send untouched.
            Setup.Git(ws, "checkout", "-q", "-b", "ledger-seed");
            Directory.CreateDirectory(Path.Combine(ws, "BugLedger/shared/issues"));
            File.WriteAllText(Path.Combine(ws, "BugLedger/shared/issues/abc.bug.json"), "{\"id\":\"abc\"}\n");
            Setup.Git(ws, "add", "-A");
            Setup.Git(ws, "commit", "-q", "-m", "unity send");
            Setup.Git(ws, "push", "-q", "origin", "HEAD:refs/heads/bugledger/tester");
            Setup.Git(ws, "checkout", "-q", "-");
            File.WriteAllText(Path.Combine(ws, "game.txt"), "an unsaved edit\n");   // the person's work in progress
            string head = Setup.Git(ws, "rev-parse", "HEAD"), status = Setup.Git(ws, "status", "--porcelain");

            var logs = Setup.Temp("netlogs");
            var main = Log(logs, "main", "{\"peer\":\"main\"}");
            var p2 = Log(logs, "p2", "{\"peer\":\"p2\"}");
            var r = NetLogSender.Send("git", ws, NetLogSender.Candidates(logs), "Tester", null, CancellationToken.None).Result;

            Assert.True(r.Ok, r.Error);
            Assert.Equal(2, r.Sent);
            Assert.Equal("bugledger/tester", r.Branch);
            var files = Setup.Git(bare, "ls-tree", "-r", "--name-only", "bugledger/tester").Split('\n');
            Assert.Contains("BugLedger/shared/issues/abc.bug.json", files);
            Assert.Contains("BugLedger/shared/reports/tester/net/" + main.Name, files);
            Assert.Contains("BugLedger/shared/reports/tester/net/" + p2.Name, files);
            Assert.Equal("{\"peer\":\"p2\"}", Setup.Git(bare, "show", "bugledger/tester:BugLedger/shared/reports/tester/net/" + p2.Name));
            Assert.Contains("main and p2", Setup.Git(bare, "log", "-1", "--format=%s", "bugledger/tester"));
            // The workspace never moved: same commit, same unsaved edit, nothing staged.
            Assert.Equal(head, Setup.Git(ws, "rev-parse", "HEAD"));
            Assert.Equal(status, Setup.Git(ws, "status", "--porcelain"));

            // Sent logs are not sent again; a new one goes alone.
            var again = NetLogSender.Send("git", ws, NetLogSender.Candidates(logs), "Tester", null, CancellationToken.None).Result;
            Assert.True(again.Ok, again.Error);
            Assert.Equal(0, again.Sent);
            Thread.Sleep(1100); // a new second: a new file name
            var p3 = Log(logs, "p3");
            var third = NetLogSender.Send("git", ws, NetLogSender.Candidates(logs), "Tester", null, CancellationToken.None).Result;
            Assert.Equal(1, third.Sent);
            Assert.Contains("BugLedger/shared/reports/tester/net/" + p3.Name, Setup.Git(bare, "ls-tree", "-r", "--name-only", "bugledger/tester"));
            // base, the Unity send, two Amoebius sends: the send with nothing new made no commit.
            Assert.Equal("4", Setup.Git(bare, "rev-list", "--count", "bugledger/tester"));
        }

        [Fact]
        public void The_first_send_creates_the_branch()
        {
            var (ws, bare) = Repo();
            var logs = Setup.Temp("netlogs");
            var main = Log(logs, "main");
            var r = NetLogSender.Send("git", ws, NetLogSender.Candidates(logs), "New Person", null, CancellationToken.None).Result;
            Assert.True(r.Ok, r.Error);
            Assert.Equal(1, r.Sent);
            Assert.Equal("BugLedger/shared/reports/new-person/net/" + main.Name, Setup.Git(bare, "ls-tree", "-r", "--name-only", "bugledger/new-person"));
            Assert.DoesNotContain("bugledger", Setup.Git(bare, "rev-parse", "--abbrev-ref", "bleeding-edge"));
        }

        [Fact]
        public void A_log_already_offered_is_not_new()
        {
            var dir = Setup.Temp("netlogs");
            var a = Log(dir, "main");
            Assert.Single(NetTestLogFormat.NewFiles(dir, new HashSet<string>()));
            Assert.Empty(NetTestLogFormat.NewFiles(dir, new HashSet<string> { a.Name }));
            Assert.Empty(NetTestLogFormat.NewFiles(Path.Combine(dir, "missing"), new HashSet<string>()));
        }
    }
}
