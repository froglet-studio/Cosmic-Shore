using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ImGuiNET;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// NET: Prisma's built-in multiplayer tools (docs/MULTIPLAYER.md §6), the counterpart of Unity's
    /// Multiplayer Play Mode, Network Simulator and Runtime Network Stats Monitor. PLAYERS starts two
    /// to four game windows on this PC (a party is four) that find each other through one fresh session
    /// folder, each optionally on a simulated line. LIVE shows each player's role, traffic and RTT once a
    /// second and changes its line, pulls its cable, or arms a session fault while it runs.
    /// </summary>
    public sealed partial class LauncherApp
    {
        static readonly string[] NetLines = { "", "lan", "broadband", "dsl", "4g", "3g", "poor" };
        static readonly string[] NetFaultChoices = { "", "full", "ratelimit=3", "relayfail", "slow=2000", "down" };
        /// <summary>The Connection switch: what each segment shows, and the setting it stores (MpTransport).</summary>
        static readonly string[] NetConnections = { "UDP", "RELAY", "UGS RELAY", "TCP" }, NetConnectionKeys = { "udp", "relay", "ugs", "tcp" };

        readonly ConcurrentDictionary<int, string> _netLive = new();
        readonly Dictionary<int, string> _netLine = new(), _netFault = new();
        readonly HashSet<int> _netCableOut = new();
        double _netPollAt;
        bool _netPolling;

        static void IconNet(ImDrawListPtr dl, Vector2 c, uint col)
        {
            // Three linked nodes: a party on a network.
            var a = c + new Vector2(-9, 6); var b = c + new Vector2(9, 6); var t = c + new Vector2(0, -9);
            dl.AddLine(a, b, col, 1.6f); dl.AddLine(a, t, col, 1.6f); dl.AddLine(b, t, col, 1.6f);
            foreach (var p in new[] { a, b, t }) dl.AddCircleFilled(p, 3.6f, col, 12);
        }

        void DrawNet(Vector2 a, Vector2 b)
        {
            PageHeader(a, "NET", "Multiplayer on this PC: up to four players, a simulated line for each, live traffic and RTT, session faults");
            var dl = ImGui.GetWindowDrawList();
            float top = a.Y + 76, leftW = Math.Min(430, (b.X - a.X) * 0.40f);
            var la = new Vector2(a.X, top);
            var lb = new Vector2(a.X + leftW, b.Y);
            Card(dl, la, lb);
            DrawNetSetup(dl, la, lb);
            var ra = new Vector2(lb.X + 14, top);
            Card(dl, ra, b);
            DrawNetLive(dl, ra, b);
            PollNet();
        }

        void DrawNetSetup(ImDrawListPtr dl, Vector2 a, Vector2 b)
        {
            dl.AddText(Neon.Strong, 15, a + new Vector2(16, 12), Neon.U(Neon.Ink), "PLAYERS");
            dl.AddText(Neon.Small, 12, a + new Vector2(16, 34), Neon.U(Neon.Dim), "Game windows on this PC that play together, like PCs on a LAN.");
            ImGui.SetCursorScreenPos(a + new Vector2(16, 60));
            Label("Players");
            ImGui.SetCursorScreenPos(a + new Vector2(16, 78));
            Segmented("netplayers", new[] { "2", "3", "4" }, Math.Clamp(_s.MpPlayers - 2, 0, 2), i => { _s.MpPlayers = i + 2; _dirty = true; }, Neon.Magenta);
            ImGui.SetCursorScreenPos(a + new Vector2(16, 124));
            Label("Connection");
            ImGui.SetCursorScreenPos(a + new Vector2(16, 142));
            Segmented("nettransport", NetConnections, Math.Max(0, Array.IndexOf(NetConnectionKeys, _s.MpTransport)), i => { _s.MpTransport = NetConnectionKeys[i]; _dirty = true; }, Neon.Cyan);
            Neon.Tooltip("UDP: Froglet's transport, players connect directly (selective acks, RTT-timed resends, an unreliable channel).\n" +
                         "RELAY: the same transport through Froglet's relay server, started beside the players: each host allocates and each\n" +
                         "joiner joins by code, the way a game over the internet does (Unity Relay's protocol, docs/MULTIPLAYER.md §6.7).\n" +
                         "UGS RELAY: through UGS Relay itself; each player signs in to the game's LIVE UGS project as its profile\n" +
                         "(player1..4, the same UGS players every run). The lobby is still this PC's session folder (§6.8).\n" +
                         "TCP: the first transport. Every player uses the same.");

            ImGui.SetCursorScreenPos(a + new Vector2(16, 188));
            Label("Start in");
            ImGui.SetCursorScreenPos(a + new Vector2(16, 206));
            ImGui.PushItemWidth(b.X - a.X - 32);
            var choices = new[] { "" }.Concat(_jobs.Scenes).ToArray();
            Combo("##netscene", choices, _s.MpScene, v => _s.MpScene = v, v => v.Length == 0 ? "Bootstrap (log in, then the menu)" : v);
            ImGui.PopItemWidth();

            ImGui.SetCursorScreenPos(a + new Vector2(16, 252));
            Label("Each player's line (network simulator)");
            while (_s.MpSims.Count < Prisma.MultiplayerRun.MaxPlayers) _s.MpSims.Add("");
            for (int i = 0; i < _s.MpPlayers; i++)
            {
                dl.AddText(Neon.Strong, 14, a + new Vector2(16, 280 + i * 38), Neon.U(Neon.Ink), $"P{i + 1}");
                ImGui.SetCursorScreenPos(a + new Vector2(56, 274 + i * 38));
                ImGui.PushItemWidth(b.X - a.X - 72);
                int k = i;
                Combo($"##netsim{i}", NetLines, _s.MpSims[i], v => _s.MpSims[k] = v, v => v.Length == 0 ? "clean line" : v);
                ImGui.PopItemWidth();
            }

            int live = _jobs.LocalPlayersRunning;
            float y = 274 + _s.MpPlayers * 38 + 14;
            ImGui.SetCursorScreenPos(a + new Vector2(16, y));
            float bw = b.X - a.X - 32;
            if (live > 0)
            {
                if (Neon.Button("netstop", $"CLOSE {live} PLAYER{(live == 1 ? "" : "S")}", new Vector2(bw, 48), Neon.Red, Neon.Title, 22))
                {
                    _jobs.StopLocalPlayers();
                    _netLive.Clear(); _netLine.Clear(); _netFault.Clear(); _netCableOut.Clear();
                }
            }
            else if (Neon.Button("netgo", $"START {_s.MpPlayers} PLAYERS", new Vector2(bw, 48), Neon.Magenta, Neon.Title, 22, enabled: !_jobs.Busy))
            {
                _netLine.Clear(); _netFault.Clear(); _netCableOut.Clear();
                for (int i = 0; i < _s.MpPlayers; i++) _netLine[i + 1] = _s.MpSims[i];
                _jobs.LaunchLocalPlayers(_s.MpPlayers, _s.MpScene, _s.MpSize, _s.MpSims.Take(_s.MpPlayers).ToList(), _s.MpTransport);
            }
            var help = new[]
            {
                "Each window is its own player: profile player1, player2 ..., its own save.",
                "Party up as people would: invite from the friends list, or open the",
                "DiagnosticsHUD (F7) and run 'party invite <name>' / 'party join <name>'.",
                "Window titles show each player's role, RTT and traffic.",
            };
            for (int i = 0; i < help.Length; i++)
                dl.AddText(Neon.Small, 12, a + new Vector2(16, y + 64 + i * 17), Neon.U(Neon.Dim), help[i]);

            ImGui.SetCursorScreenPos(a + new Vector2(16, y + 64 + help.Length * 17 + 6));
            if (Neon.Button("netugscheck", "UGS RELAY CHECK", new Vector2(bw, 28), Neon.Cyan, Neon.Small, 13, enabled: !_jobs.Busy))
                _jobs.RunUgsRelayCheck();
            Neon.Tooltip("Proves UGS Relay works from Amoebius: signs in two players in the game's LIVE UGS project (the same two\n" +
                         "every time), allocates, joins by code, connects through UGS Relay and times frames both ways.\n" +
                         "The log names each step and the round-trip time. docs/MULTIPLAYER.md §6.8.");
        }

        void DrawNetLive(ImDrawListPtr dl, Vector2 a, Vector2 b)
        {
            dl.AddText(Neon.Strong, 15, a + new Vector2(16, 12), Neon.U(Neon.Ink), "LIVE");
            var players = _jobs.LocalPlayers;
            if (players.Count == 0)
            {
                dl.AddText(Neon.Small, 13, a + new Vector2(16, 44), Neon.U(Neon.Dim), "Start players on the left. Each one's link shows here once a second.");
                return;
            }
            dl.AddText(Neon.Small, 12, a + new Vector2(16, 34), Neon.U(Neon.Dim), "Session folder: " + Trim(_jobs.LocalNetDir ?? "", 90));
            float rowH = 112, y = a.Y + 62;
            foreach (var (n, port, running) in players)
            {
                var ra = new Vector2(a.X + 12, y); var rb = new Vector2(b.X - 12, y + rowH - 10);
                dl.AddRectFilled(ra, rb, Neon.U(Neon.Ink, 0.035f), 10);
                var title = $"P{n}  ·  player{n}  ·  port {port}";
                dl.AddText(Neon.Strong, 14, ra + new Vector2(12, 8), Neon.U(running ? Neon.Ink : Neon.Dim), title + (running ? "" : "  ·  closed"));
                _netLive.TryGetValue(n, out var status);
                dl.AddText(Neon.Mono, 13, ra + new Vector2(12, 30), Neon.U(running ? Neon.Cyan : Neon.Dim), running ? (status ?? "booting...") : "");
                if (running)
                {
                    float x = ra.X + 12, cy = ra.Y + 56;
                    ImGui.SetCursorScreenPos(new Vector2(x, cy));
                    ImGui.PushItemWidth(150);
                    _netLine.TryGetValue(n, out var line);
                    Combo($"##netlive{n}", NetLines, line ?? "", v => { _netLine[n] = v; SendNet(port, "netsim " + (v.Length == 0 ? "off" : v)); }, v => v.Length == 0 ? "clean line" : v);
                    ImGui.PopItemWidth();
                    ImGui.SameLine(0, 10);
                    bool cut = _netCableOut.Contains(n);
                    if (Neon.Button($"netcable{n}", cut ? "PLUG IN" : "PULL CABLE", new Vector2(120, 30), cut ? Neon.Lime : Neon.Amber, Neon.Small, 13))
                    {
                        if (cut) _netCableOut.Remove(n); else _netCableOut.Add(n);
                        SendNet(port, cut ? "netsim up" : "netsim down");
                    }
                    ImGui.SameLine(0, 10);
                    ImGui.PushItemWidth(150);
                    _netFault.TryGetValue(n, out var fault);
                    Combo($"##netfault{n}", NetFaultChoices, fault ?? "", v => { _netFault[n] = v; SendNet(port, "netfault off" + (v.Length == 0 ? "" : " " + v)); }, v => v.Length == 0 ? "no fault" : "fault: " + v);
                    ImGui.PopItemWidth();
                    ImGui.SameLine(0, 10);
                    if (Neon.Button($"netcap{n}", "CAPTURE 10 S", new Vector2(130, 30), Neon.Violet, Neon.Small, 13))
                        SendNet(port, "net capture 600");
                    Neon.Tooltip("Records 600 frames of per-frame traffic and RTT to a JSON file; the console names it.");
                }
                y += rowH;
                if (y + rowH > b.Y) break;
            }
            dl.AddText(Neon.Small, 12, new Vector2(a.X + 16, Math.Min(y + 4, b.Y - 22)), Neon.U(Neon.Dim),
                "A pulled cable drops the player from everyone after 10 s, as a real timeout does. Faults hit the session service (lobby, relay).");
        }

        void SendNet(int port, string verb)
        {
            _jobs.Log.Add(LogKind.Info, $"[port {port}] {verb}");
            _ = Task.Run(async () =>
            {
                var r = await Prisma.MultiplayerRun.Command(port, "do", verb);
                var text = (r["output"]?.ToString() ?? "").Trim();
                if (text.Length > 0) _jobs.Log.Add(LogKind.Output, $"[port {port}] {text.Split('\n').Last()}");
            });
        }

        /// <summary>Once a second, ask every live player for its link (in the background; the page never waits).</summary>
        void PollNet()
        {
            double now = ImGui.GetTime();
            if (_netPolling || now - _netPollAt < 1.0) return;
            _netPollAt = now;
            var players = _jobs.LocalPlayers.Where(p => p.running).ToList();
            if (players.Count == 0) return;
            _netPolling = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(players.Select(async p =>
                    {
                        var st = await Prisma.MultiplayerRun.Command(p.port, "state");
                        if (st["ok"]?.GetValue<bool>() != true) { _netLive[p.player] = "booting..."; return; }
                        var net = await Prisma.MultiplayerRun.Command(p.port, "do", "net json");
                        var line = Prisma.MultiplayerRun.NetLine(net["output"]?.ToString() ?? "");
                        _netLive[p.player] = $"{st["scene"]}  ·  " + (line.Length > 0 ? line : "not connected");
                    }));
                }
                finally { _netPolling = false; }
            });
        }
    }
}
