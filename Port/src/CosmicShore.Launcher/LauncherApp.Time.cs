using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// TIME: the game timed by the engine itself. BENCHMARK runs chosen scenes for a fixed number of
    /// frames, each run in its own game process that closes itself when done, and tabulates the
    /// session reports (frame time percentiles, load, simulation and GPU time, GC). MULTIPLAYER
    /// starts several game windows on this machine that join each other like players on a LAN.
    /// </summary>
    public sealed partial class LauncherApp
    {
        List<BenchSession>? _benchHistory;
        List<string>? _benchReplays;
        int _benchPick;
        string _benchFilter = "";

        static readonly string[] BenchSizes = { "1280x720", "1920x1080", "2560x1440" };
        static readonly int[] BenchFrameChoices = { 600, 1800, 3600, 7200 };

        static void IconClock(ImDrawListPtr dl, Vector2 c, uint col)
        {
            dl.AddCircle(c + new Vector2(0, 1), 10, col, 24, 1.8f);
            dl.AddLine(c + new Vector2(0, 1), c + new Vector2(0, -5), col, 1.8f);
            dl.AddLine(c + new Vector2(0, 1), c + new Vector2(5, 4), col, 1.8f);
            dl.AddLine(c + new Vector2(-3, -12), c + new Vector2(3, -12), col, 1.8f);
        }

        void DrawTime(Vector2 a, Vector2 b)
        {
            PageHeader(a, "TIME", "The game timed by Prisma: benchmark runs that close themselves, and several players on one machine");
            var dl = ImGui.GetWindowDrawList();
            float top = a.Y + 76, leftW = Math.Min(430, (b.X - a.X) * 0.42f);
            var la = new Vector2(a.X, top);
            var lb = new Vector2(a.X + leftW, top + 500);
            Card(dl, la, lb);
            DrawBenchSetup(dl, la, lb);
            var ma = new Vector2(a.X, lb.Y + 14);
            var mb = new Vector2(a.X + leftW, Math.Max(ma.Y + 210, b.Y));
            Card(dl, ma, mb);
            DrawMultiplayer(dl, ma, mb);
            var ra = new Vector2(lb.X + 14, top);
            Card(dl, ra, b);
            DrawBenchResults(dl, ra, b);
        }

        void DrawBenchSetup(ImDrawListPtr dl, Vector2 a, Vector2 b)
        {
            dl.AddText(Neon.Strong, 15, a + new Vector2(16, 12), Neon.U(Neon.Ink), "BENCHMARK");
            dl.AddText(Neon.Small, 12, a + new Vector2(16, 34), Neon.U(Neon.Dim), "Each item runs in its own game and closes. Replays play a real match.");
            _benchReplays ??= _jobs.BenchReplays();
            var scenes = _benchReplays.Concat(_jobs.Scenes).ToList();
            ImGui.SetCursorScreenPos(a + new Vector2(16, 58));
            ImGui.PushItemWidth(b.X - a.X - 150);
            ImGui.InputTextWithHint("##benchfilter", "filter scenes", ref _benchFilter, 64);
            ImGui.PopItemWidth();
            ImGui.SameLine(0, 8);
            if (SmallButton(_s.BenchScenes.Count == 0 ? "ALL" : "NONE", 100, scenes.Count > 0))
            {
                _s.BenchScenes = _s.BenchScenes.Count == 0 ? scenes.Where(BenchMatches).ToList() : new List<string>();
                _dirty = true;
            }
            ImGui.SetCursorScreenPos(a + new Vector2(10, 104));
            ImGui.BeginChild("##benchscenes", new Vector2(b.X - a.X - 20, 150));
            if (scenes.Count == 0) ImGui.TextColored(Neon.Dim, "No workspace yet: press START on PLAY once.");
            foreach (var sc in scenes.Where(BenchMatches))
            {
                bool on = _s.BenchScenes.Contains(sc);
                bool replay = sc.StartsWith("replay:", StringComparison.Ordinal);
                if (replay) ImGui.PushStyleColor(ImGuiCol.Text, Neon.Cyan);
                bool clicked = ImGui.Checkbox((replay ? "REPLAY  " + sc[7..] : sc) + "##bs" + sc, ref on);
                if (replay) { ImGui.PopStyleColor(); Neon.Tooltip("Recorded input from boot into a real match (parity replay): times actual play. Uses PLAY's save slot, so log in once on PLAY first."); }
                if (clicked)
                {
                    if (on) _s.BenchScenes.Add(sc); else _s.BenchScenes.Remove(sc);
                    _dirty = true;
                }
            }
            ImGui.EndChild();

            float y = a.Y + 264;
            ImGui.SetCursorScreenPos(new Vector2(a.X + 16, y));
            Label("Frames per run");
            ImGui.SetCursorScreenPos(new Vector2(a.X + 16, y + 18));
            Segmented("benchframes", BenchFrameChoices.Select(f => f >= 3600 ? $"{f / 60 / 60}m" : $"{f / 60}s").ToArray(),
                Math.Max(0, Array.IndexOf(BenchFrameChoices, _s.BenchFrames)), i => { _s.BenchFrames = BenchFrameChoices[i]; _dirty = true; }, Neon.Cyan);
            Neon.Tooltip("At 60 fps: 10 s, 30 s, 1 min, 2 min of play per run.");
            ImGui.SetCursorScreenPos(new Vector2(a.X + 230, y));
            Label("Runs");
            ImGui.SetCursorScreenPos(new Vector2(a.X + 230, y + 18));
            Segmented("benchruns", new[] { "1", "2", "3" }, Math.Clamp(_s.BenchRuns - 1, 0, 2), i => { _s.BenchRuns = i + 1; _dirty = true; }, Neon.Cyan);

            y += 70;
            ImGui.SetCursorScreenPos(new Vector2(a.X + 16, y));
            Segmented("benchmode", new[] { "WINDOW", "HEADLESS" }, _s.BenchHeadless ? 1 : 0, i => { _s.BenchHeadless = i == 1; _dirty = true; }, Neon.Cyan);
            Neon.Tooltip("WINDOW renders on this GPU (what players see). HEADLESS times the simulation only, no rendering.");
            if (!_s.BenchHeadless)
            {
                ImGui.SameLine(0, 10);
                ImGui.PushItemWidth(120);
                Combo("##benchsize", BenchSizes, _s.BenchSize, v => _s.BenchSize = v);
                ImGui.PopItemWidth();
                ImGui.SameLine(0, 10);
                Toggle("VSync", () => _s.BenchVSync, v => _s.BenchVSync = v, "Off measures how fast a frame really is; on caps it at the display rate.");
            }

            y += 48;
            ImGui.SetCursorScreenPos(new Vector2(a.X + 16, y));
            Segmented("benchgc", new[] { "GC DEFAULT", "LOW LATENCY", "A/B" }, Math.Clamp(_s.BenchGc, 0, 2), i => { _s.BenchGc = i; _dirty = true; }, Neon.Violet);
            Neon.Tooltip(".NET's garbage collector mode. LOW LATENCY (SustainedLowLatency) avoids blocking full\n" +
                         "collections while memory allows. A/B runs every item in both modes; the LOW rows\n" +
                         "compare against the DEFAULT row of the same benchmark.");

            y += 48;
            ImGui.SetCursorScreenPos(new Vector2(a.X + 16, y));
            bool running = _jobs.Busy && _jobs.JobName == "Benchmark";
            float bw = b.X - a.X - 32;
            if (running)
            {
                if (Neon.Button("benchstop", "STOP", new Vector2(bw, 52), Neon.Red, Neon.Title, 24)) _jobs.Cancel();
            }
            else if (Neon.Button("benchrun", $"RUN  {_s.BenchScenes.Count} SCENE{(_s.BenchScenes.Count == 1 ? "" : "S")}", new Vector2(bw, 52), Neon.Lime, Neon.Title, 24,
                         enabled: !_jobs.Busy && _s.BenchScenes.Count > 0))
            {
                var picked = scenes.Where(_s.BenchScenes.Contains).ToList();
                _jobs.Benchmark(picked, _s.BenchFrames, _s.BenchRuns, _s.BenchHeadless, _s.BenchVSync, _s.BenchSize, _s.BenchGc);
                _benchHistory = null; _benchPick = 0;
            }
            if (running) dl.AddText(Neon.Small, 12, new Vector2(a.X + 16, y + 58), Neon.U(Neon.Amber), _jobs.Stage);
        }

        bool BenchMatches(string scene) => _benchFilter.Length == 0 || scene.Contains(_benchFilter, StringComparison.OrdinalIgnoreCase);

        void DrawMultiplayer(ImDrawListPtr dl, Vector2 a, Vector2 b)
        {
            dl.AddText(Neon.Strong, 15, a + new Vector2(16, 12), Neon.U(Neon.Ink), "MULTIPLAYER");
            dl.AddText(Neon.Small, 12, a + new Vector2(16, 34), Neon.U(Neon.Dim), "Game windows on this PC that play together over the local network.");
            ImGui.SetCursorScreenPos(a + new Vector2(16, 60));
            Label("Players");
            ImGui.SetCursorScreenPos(a + new Vector2(16, 78));
            Segmented("mpplayers", new[] { "2", "3", "4" }, Math.Clamp(_s.MpPlayers - 2, 0, 2), i => { _s.MpPlayers = i + 2; _dirty = true; }, Neon.Magenta);
            ImGui.SameLine(0, 12);
            ImGui.PushItemWidth(b.X - ImGui.GetCursorScreenPos().X - 16);
            var choices = new[] { "" }.Concat(_jobs.Scenes).ToArray();
            Combo("##mpscene", choices, _s.MpScene, v => _s.MpScene = v, v => v.Length == 0 ? "start at Bootstrap (log in, menu)" : v);
            ImGui.PopItemWidth();

            int live = _jobs.LocalPlayersRunning;
            ImGui.SetCursorScreenPos(a + new Vector2(16, 128));
            float bw = b.X - a.X - 32;
            if (live > 0)
            {
                if (Neon.Button("mpstop", $"CLOSE {live} PLAYER{(live == 1 ? "" : "S")}", new Vector2(bw, 48), Neon.Red, Neon.Title, 22)) _jobs.StopLocalPlayers();
            }
            else if (Neon.Button("mpgo", $"START {_s.MpPlayers} PLAYERS", new Vector2(bw, 48), Neon.Magenta, Neon.Title, 22, enabled: !_jobs.Busy))
                _jobs.LaunchLocalPlayers(_s.MpPlayers, _s.MpScene, _s.MpSize);
            dl.AddText(Neon.Small, 12, a + new Vector2(16, 184), Neon.U(Neon.Dim),
                "Each window is its own player (profile player1, player2 ...). Host in one, join from the others.");
        }

        void DrawBenchResults(ImDrawListPtr dl, Vector2 a, Vector2 b)
        {
            dl.AddText(Neon.Strong, 15, a + new Vector2(16, 12), Neon.U(Neon.Ink), "RESULTS");
            bool running = _jobs.Busy && _jobs.JobName == "Benchmark";
            if (!running && _benchHistory == null) _benchHistory = LauncherJobs.BenchHistory();
            var history = _benchHistory ?? new List<BenchSession>();
            var shown = running ? _jobs.Bench : history.ElementAtOrDefault(_benchPick);
            // The previous benchmark on this machine is the comparison.
            var older = shown == null ? null : history.FirstOrDefault(h => h.File != shown.File && h.Started < shown.Started && h.Machine == shown.Machine);

            if (!running && history.Count > 0)
            {
                ImGui.SetCursorScreenPos(new Vector2(b.X - 336, a.Y + 6));
                ImGui.PushItemWidth(320);
                var labels = history.Select(h => $"{h.Started:dd MMM HH:mm}  ·  {Trim(h.Branch, 22)}  ·  {h.Results.Count} runs").ToArray();
                if (ImGui.BeginCombo("##benchpick", labels[Math.Clamp(_benchPick, 0, labels.Length - 1)]))
                {
                    for (int i = 0; i < labels.Length; i++) if (ImGui.Selectable(labels[i], i == _benchPick)) _benchPick = i;
                    ImGui.EndCombo();
                }
                ImGui.PopItemWidth();
            }
            if (shown == null)
            {
                dl.AddText(Neon.Small, 13, a + new Vector2(16, 44), Neon.U(Neon.Dim), "Pick scenes and press RUN. Results show here as each run closes.");
                return;
            }
            dl.AddText(Neon.Small, 12, a + new Vector2(16, 34), Neon.U(Neon.Dim),
                $"{shown.Branch} #{shown.Commit}  ·  {(shown.Headless ? "headless" : shown.Size + (shown.VSync ? ", vsync" : ", no vsync"))}  ·  {shown.Frames} frames" +
                (shown.GcMode switch { 1 => "  ·  low-latency GC", 2 => "  ·  GC A/B", _ => "" }) +
                (string.IsNullOrEmpty(shown.Gpu) ? "" : "  ·  " + Trim(shown.Gpu, 40)) + (older != null ? $"  ·  vs {older.Started:dd MMM HH:mm}" : ""));

            ImGui.SetCursorScreenPos(a + new Vector2(10, 58));
            var flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.BordersInnerH;
            if (!ImGui.BeginTable("##bench", 9, flags, new Vector2(b.X - a.X - 20, b.Y - a.Y - 70))) return;
            foreach (var h in new[] { "SCENE", "P50", "P95", "WORST", ">33ms", "SIM P95", "GPU", "LOAD", "GC/F" })
                ImGui.TableSetupColumn(h, h == "SCENE" ? ImGuiTableColumnFlags.WidthStretch : ImGuiTableColumnFlags.WidthFixed, h == "SCENE" ? 0 : 62);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.PushFont(Neon.Small);
            ImGui.TableHeadersRow();
            foreach (var r in shown.Results.ToList())
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                var label = r.Scene.StartsWith("replay:", StringComparison.Ordinal) ? "REPLAY " + r.Scene[7..] : SceneName(r.Scene);
                if (r.Gc == "low") label = "LOW GC  " + label;
                ImGui.TextColored(r.Ok ? Neon.Ink : Neon.Red, r.Run > 1 ? $"{label}  #{r.Run}" : label);
                if (!r.Ok) { if (ImGui.IsItemHovered()) ImGui.SetTooltip(r.Problem ?? "failed"); for (int i = 0; i < 8; i++) { ImGui.TableNextColumn(); ImGui.TextColored(Neon.Dim, "-"); } continue; }
                if (r.Exceptions + r.Errors > 0 && ImGui.IsItemHovered()) ImGui.SetTooltip($"{r.Exceptions} exception(s), {r.Errors} error(s) - see the report:\n{r.Report}");
                // An A/B's LOW row against its own DEFAULT row; everything else against the last benchmark.
                var was = (r.Gc == "low" ? shown.Results.FirstOrDefault(o => o.Scene == r.Scene && o.Gc == "" && o.Run == r.Run && o.Ok) : null)
                          ?? older?.Results.FirstOrDefault(o => o.Scene == r.Scene && o.Gc == r.Gc && o.Ok);
                Ms(r.P50Ms, was?.P50Ms);
                Ms(r.P95Ms, was?.P95Ms);
                Ms(r.WorstMs, was?.WorstMs);
                ImGui.TableNextColumn(); ImGui.TextColored(r.Over33 > 0 ? Neon.Amber : Neon.Dim, r.Over33.ToString(CultureInfo.InvariantCulture));
                Ms(r.SimP95Ms, was?.SimP95Ms);
                if (r.GpuP50Ms is { } gpu) Ms(gpu, was?.GpuP50Ms);
                else { ImGui.TableNextColumn(); ImGui.TextColored(Neon.Dim, "n/a"); if (ImGui.IsItemHovered()) ImGui.SetTooltip("No GPU timer queries on this run (headless, or the driver has none)."); }
                ImGui.TableNextColumn(); ImGui.TextColored(Neon.Ink, r.LoadSec < 10 ? $"{r.LoadSec:0.00} s" : $"{r.LoadSec:0.0} s");
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(r.BootSec > 0 ? $"Scene load to its first frame. Boot to the first frame: {r.BootSec:0.00} s." : "Scene load to its first frame.");
                ImGui.TableNextColumn(); ImGui.TextColored(r.GcPauseMsPerFrame > 1 ? Neon.Amber : Neon.Ink, $"{r.GcPauseMsPerFrame:0.00}");
            }
            ImGui.PopFont();
            ImGui.EndTable();

            // A time against the previous benchmark's: green when it got faster, red when slower (5% either way counts).
            static void Ms(double v, double? before)
            {
                ImGui.TableNextColumn();
                var col = v > 33.4 ? Neon.Red : v > 16.7 ? Neon.Amber : Neon.Ink;
                ImGui.TextColored(col, v < 0.1 ? "<0.1" : $"{v:0.0}");
                if (before is { } w && w > 0)
                {
                    double d = (v - w) / w;
                    // 5% either way counts, and not under 0.2 ms: sub-millisecond frames swing by
                    // hundreds of percent on nothing.
                    if (Math.Abs(d) >= 0.05 && Math.Abs(v - w) >= 0.2)
                    {
                        ImGui.SameLine(0, 4);
                        // ImGui formats the text printf-style: a percent sign is written %%.
                        ImGui.TextColored(d < 0 ? Neon.Lime : Neon.Red, $"{Math.Clamp(d * 100, -999, 999):+0;-0}%%");
                        if (ImGui.IsItemHovered()) ImGui.SetTooltip($"was {w:0.0} ms");
                    }
                }
            }
        }
    }
}
