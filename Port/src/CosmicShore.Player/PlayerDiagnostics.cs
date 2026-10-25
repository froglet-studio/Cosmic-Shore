using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using CosmicShore.Content;
using CosmicShore.Engine;
using CosmicShore.Engine.InputSystem;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.UI;

namespace CosmicShore.Player
{
    /// <summary>
    /// Amoebius's own diagnostics overlay (F3, or <c>--diag</c> / COSMIC_SHORE_DIAG=1 to start with it shown), top
    /// right, drawn by the engine's uGUI and refreshed twice a second. The game's DiagnosticsHUD (F7, top left) is
    /// the game's; this one is the engine's view of the same process:
    /// <list type="bullet">
    /// <item>FRAME: fps, frame time (average, 1% worst, max), simulation and render CPU, GPU when timed, the loop's phases.
    /// Read from <see cref="FrameTimingManager"/> through <see cref="FrameTimingWindow"/>, the clock F7 reads too;</item>
    /// <item>MEMORY: managed heap, allocation per frame, collections and GC pause, the process's working set;</item>
    /// <item>SCENE: the active scene, draw calls and instances, resolution;</item>
    /// <item>NETWORK: online or offline, the NetStack chain, the UGS sign-in, the lobbies this player is in, the relay,
    /// role, traffic and every peer's round trip, the network simulator and session faults.</item>
    /// </list>
    /// Nothing exists until it is first shown, so a run that never opens it has no extra objects.
    /// </summary>
    static class PlayerDiagnostics
    {
        const float Width = 470f, Pad = 12f, LineH = 17.5f, FontSize = 13.5f;

        public static bool Visible { get; private set; }
        public static bool StartVisible;

        static GameObject s_root;
        static RectTransform s_panel;
        static TextMeshProUGUI s_text;

        // FRAME reads FrameTimingManager, the clock the game's DiagnosticsHUD (F7) reads too.
        static readonly FrameTimingWindow s_frame = new();
        static long s_allocMark = -1;
        static int s_gc0, s_gc1, s_gc2;
        static TimeSpan s_gcPause;
        static readonly Stopwatch s_refresh = Stopwatch.StartNew();
        static readonly Process s_process = Process.GetCurrentProcess();
        static string s_phases = "";

        /// <summary>Per tick: F3 toggles; twice a second the text is rebuilt.</summary>
        public static void Tick(int drawCalls, int instances)
        {
            if (StartVisible) { StartVisible = false; Show(true); }
            if (Keyboard.current is { } keys && keys.f3Key.wasPressedThisFrame) Show(!Visible);
            if (!Visible || s_refresh.ElapsedMilliseconds < 500) return;
            if (s_root == null) Build();   // a scene change can take it with it if DontDestroyOnLoad is undone
            s_text.text = Text(drawCalls, instances);
            int lines = s_text.text.Count(c => c == '\n') + 1;
            s_panel.sizeDelta = new Vector2(Width, lines * LineH + 2 * Pad);
            s_refresh.Restart();
        }

        public static void Show(bool on)
        {
            Visible = on;
            if (on && s_root == null) Build();
            if (s_root != null) s_root.SetActive(on);
            s_refresh.Restart();
            if (on) s_text.text = "<color=#7FD8FF>AMOEBIUS DIAGNOSTICS</color>  measuring...";
        }

        static void Build()
        {
            s_root = new GameObject("AmoebiusDiagnostics", typeof(RectTransform));
            Engine.Object.DontDestroyOnLoad(s_root);
            var canvas = s_root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = int.MaxValue;   // over every game canvas, modal dimmers included

            var go = new GameObject("Panel", typeof(RectTransform));
            go.transform.SetParent(s_root.transform, false);
            s_panel = (RectTransform)go.transform;
            s_panel.anchorMin = s_panel.anchorMax = s_panel.pivot = new Vector2(1, 1);
            s_panel.anchoredPosition = new Vector2(-10, -10);
            s_panel.sizeDelta = new Vector2(Width, 200);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.03f, 0.04f, 0.07f, 0.82f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(s_panel, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(Pad, Pad);
            rt.offsetMax = new Vector2(-Pad, -Pad);
            s_text = textGo.AddComponent<TextMeshProUGUI>();
            if (ContentRuntime.Current?.Fonts.DefaultFont is { } font) s_text.font = font;
            s_text.fontSize = FontSize;
            s_text.richText = true;
            s_text.raycastTarget = false;
            s_text.color = new Color(0.86f, 0.9f, 0.95f, 1f);
        }

        const string Head = "#7FD8FF", Dim = "#8A93A6", Good = "#7CFC9A", Warn = "#FFD166", Bad = "#FF6B6B";

        static string Col(string color, string s) => $"<color={color}>{s}</color>";

        static string Text(int drawCalls, int instances)
        {
            var sb = new StringBuilder();

            // ── FRAME
            var f = s_frame;
            f.Read();
            int newFrames = f.NewFrames;
            string fpsColor = f.Fps >= 55 ? Good : f.Fps >= 30 ? Warn : Bad;
            sb.Append(Col(Head, "FRAME")).Append("   ").Append(Col(fpsColor, $"{f.Fps:0} fps")).Append($"   {f.AverageMs:0.0} ms avg · 1% {f.Worst1PercentMs:0.0} · max {f.MaxMs:0.0}\n");
            sb.Append($"cpu  sim {f.SimulationMs:0.0} ms · render {f.RenderMs:0.0} ms");
            sb.Append(f.GpuMs is { } gpu ? $" · gpu {gpu:0.0} ms" : Col(Dim, " · gpu not timed"));
            sb.Append('\n');
            if (GameLoop.Current is { } loop && newFrames > 0)
            {
                var phases = loop.TakePhaseReport(newFrames);
                if (phases.Length > 0) s_phases = phases;
            }
            if (s_phases.Length > 0) sb.Append(Col(Dim, Wrap("loop " + s_phases, 64))).Append('\n');

            // ── MEMORY
            long total = GC.GetTotalAllocatedBytes(false);
            double kbPerFrame = s_allocMark >= 0 && newFrames > 0 ? (total - s_allocMark) / 1024.0 / newFrames : 0;
            s_allocMark = total;
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            var pause = GC.GetTotalPauseDuration();
            double pauseMs = (pause - s_gcPause).TotalMilliseconds;
            s_process.Refresh();
            sb.Append(Col(Head, "MEMORY")).Append($"  heap {GC.GetTotalMemory(false) >> 20} MB · process {s_process.WorkingSet64 >> 20} MB\n");
            sb.Append($"alloc {kbPerFrame:0.0} KB/frame · gc {g0 - s_gc0}/{g1 - s_gc1}/{g2 - s_gc2} · pause {pauseMs:0.0} ms (last 0.5 s)\n");
            s_gc0 = g0; s_gc1 = g1; s_gc2 = g2; s_gcPause = pause;

            // ── SCENE
            sb.Append(Col(Head, "SCENE")).Append($"   {CosmicShore.Engine.SceneManagement.SceneManager.GetActiveScene().name} · {Screen.width}x{Screen.height}\n");
            sb.Append($"draws {drawCalls} · instances {instances}\n");

            // ── NETWORK
            sb.Append(Col(Head, "NETWORK")).Append(' ');
            if (!NetDriver.Enabled)
            {
                sb.Append(Col(Dim, "offline (START)")).Append('\n');
                return sb.ToString().TrimEnd('\n');
            }
            sb.Append(Col(Good, "online (START ONLINE)")).Append('\n');
            sb.Append($"stack {NetStack.Describe()} · sessions {NetStack.DescribeSessions()}\n");
            var auth = Engine.Services.AuthenticationService.Instance;
            if (UgsSetup.SignInError != null) sb.Append(Col(Bad, Wrap("ugs sign-in failed: " + UgsSetup.SignInError, 64))).Append('\n');
            else if (auth != null && auth.IsSignedIn) sb.Append($"ugs {Col(Good, "signed in")} as {auth.PlayerId}\n");
            else sb.Append($"ugs {Col(Warn, "not signed in yet")}\n");
            if (UgsSetup.Where != null) sb.Append(Col(Dim, UgsSetup.Where)).Append('\n');

            var lobbies = NetStack.FindSessions<UgsMultiplayerService>();
            var sessions = lobbies?.DescribeSessions();
            if (sessions is { Count: > 0 }) foreach (var s in sessions) sb.Append("lobby ").Append(s).Append('\n');
            else sb.Append(Col(Dim, "lobby none")).Append('\n');
            if (lobbies?.LastProblem is { } problem) sb.Append(Col(Warn, Wrap("lobby last error: " + problem, 64))).Append('\n');

            string role = NetDriver.IsServer ? Col(Good, "HOST") + $" (port {NetDriver.ListenPort})" : NetDriver.IsClientOnly ? Col(Good, "CLIENT") : Col(Dim, "not in a match");
            sb.Append("match ").Append(role);
            if (RelaySessions.HostingJoinCode is { Length: > 0 } code) sb.Append(" · relay code ").Append(code);
            if (RelaySessions.Region is { Length: > 0 } region) sb.Append(" · region ").Append(region);
            sb.Append('\n');
            if (NetDriver.IsActive)
            {
                sb.Append($"traffic in {Size(NetStats.BytesInPerSecond)}/s · out {Size(NetStats.BytesOutPerSecond)}/s · msgs {NetStats.MsgsIn}/{NetStats.MsgsOut}\n");
                foreach (var p in NetStats.Peers.OrderBy(p => p.ClientId))
                {
                    string rtt = p.RttMs < 0 ? "-" : $"{p.RttMs:0}";
                    string rttColor = p.RttMs < 0 ? Dim : p.RttMs < 80 ? Good : p.RttMs < 160 ? Warn : Bad;
                    string name = p.ClientId == NetStats.ServerPeer && !NetDriver.IsServer ? "host" : p.ClientId == NetStats.PendingPeer ? "pending" : "client " + p.ClientId;
                    sb.Append($"  {name}: rtt ").Append(Col(rttColor, rtt + " ms")).Append($" (min {(p.MinRttMs < 0 ? "-" : $"{p.MinRttMs:0}")}) · in {Size(p.BytesIn)} · out {Size(p.BytesOut)}\n");
                }
            }
            if (NetSimulator.Settings.IsActive) sb.Append(Col(Warn, "simulator " + NetSimulator.Settings)).Append('\n');
            if (NetFaults.Any) sb.Append(Col(Warn, "faults " + NetFaults.Describe())).Append('\n');
            return sb.ToString().TrimEnd('\n');
        }

        static string Size(double b) => b >= 1 << 20 ? $"{b / (1 << 20):0.0} MB" : b >= 1024 ? $"{b / 1024:0.0} KB" : $"{b:0} B";

        /// <summary>Breaks a long line at spaces so the panel keeps its width.</summary>
        static string Wrap(string s, int width)
        {
            if (s.Length <= width) return s;
            var sb = new StringBuilder();
            int col = 0;
            foreach (var word in s.Split(' '))
            {
                if (col > 0 && col + word.Length + 1 > width) { sb.Append("\n  "); col = 2; }
                else if (col > 0) { sb.Append(' '); col++; }
                sb.Append(word);
                col += word.Length;
            }
            return sb.ToString();
        }
    }
}
