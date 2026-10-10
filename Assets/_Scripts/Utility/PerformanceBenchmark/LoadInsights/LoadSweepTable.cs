using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The PUBLISHED load-time targets. <c>Docs/PERFORMANCE_OPTIMIZATION.md</c> §0.6 is the
    /// publication and carries the reasoning; these constants are its machine-readable twin, and
    /// <c>LoadSweepTableTests</c> asserts the two agree so neither can drift alone. Two numbers
    /// because they are two problems with two owners: the boot is the app shell (bootstrap, DI,
    /// auth, Relay host, Menu_Main activation) and is broadly constant across modes; the launch is
    /// the arena build and is where the per-mode, per-intensity spread lives.
    /// </summary>
    public static class LoadTimeTargets
    {
        /// <summary>
        /// Engine start → main menu ready (the menu vessel has spawned and the splash fade
        /// begins). Measured with a cached sign-in, online, on the floor machine.
        /// </summary>
        public const float ColdBootToMenuSeconds = 10f;

        /// <summary>
        /// Arcade launch tap → arena complete (connecting screen done; the first playable frame
        /// is the next thing on screen). The same number for EVERY mode and intensity.
        /// </summary>
        public const float MenuToPlayableSeconds = 20f;
    }

    /// <summary>One cell of the sweep: one launch of one mode at one intensity, as the table reads it.</summary>
    [Serializable]
    public class LoadSweepCell
    {
        public string mode = "";
        public int intensity;
        public int players;
        public int aiBackfill;
        public string scene = "";
        public int repeat;

        public bool recorded;              // a Load Time Insights report exists for this launch
        public bool completed;             // it ended at a playable endpoint (not aborted / interrupted)
        public float totalMs;
        public float visualReadyMs = -1f;
        public float unattributedMs;
        public float worstFrameMs;
        public string completionReason = "";
        public string topCost = "";
        public float topCostMs;
        public int errorCount;
        public long prismsLaid;
        public string reportPath = "";
        public string note = "";
    }

    /// <summary>The whole sweep, serialized beside its table.</summary>
    [Serializable]
    public class LoadSweepSummary
    {
        public string timestamp = "";
        public string gitBranch = "";
        public string gitCommitHash = "";
        public string platform = "";
        public string origin = "";
        public float coldBootTargetSeconds = LoadTimeTargets.ColdBootToMenuSeconds;
        public float menuToPlayableTargetSeconds = LoadTimeTargets.MenuToPlayableSeconds;
        public string playersPolicy = "";
        public string notes = "";
        public bool hasBoot;
        public LoadSweepCell boot = new();
        public List<LoadSweepCell> cells = new();
    }

    /// <summary>
    /// Command-line options for the unattended sweep (<see cref="LoadSweepRunner"/>). Every option
    /// is <c>-csmloadsweep-&lt;name&gt; &lt;value&gt;</c> or <c>-csmloadsweep-&lt;name&gt;=&lt;value&gt;</c>.
    /// </summary>
    public sealed class LoadSweepOptions
    {
        public const string LaunchArg = "-csmloadsweep";
        public const string ModesArg = LaunchArg + "-modes";
        public const string IntensitiesArg = LaunchArg + "-intensities";
        public const string PlayersArg = LaunchArg + "-players";
        public const string RepeatsArg = LaunchArg + "-repeats";
        public const string CooldownArg = LaunchArg + "-cooldown";
        public const string TimeoutArg = LaunchArg + "-timeout";
        public const string IncludeMaelstromArg = LaunchArg + "-include-maelstrom";
        public const string StayArg = LaunchArg + "-stay";

        public const string PlayersMax = "max";
        public const string PlayersMin = "min";
        public const int DefaultPlayers = 4;

        /// <summary>Mode names (GameModes member names, case-insensitive). Null = every card on the roster.</summary>
        public List<string> Modes;
        public List<int> Intensities = new() { 1, 2, 3, 4 };
        /// <summary>"max", "min", or a number, clamped into each card's allowed range. Default 4 for comparability.</summary>
        public string Players = DefaultPlayers.ToString(CultureInfo.InvariantCulture);
        public int Repeats = 1;
        public float CooldownSeconds = 5f;
        public float CellTimeoutSeconds = 300f;
        /// <summary>Maelstrom is a meta-mode (its loads are the other modes' cells); off unless asked for.</summary>
        public bool IncludeMaelstrom;
        /// <summary>Stay running after the table is written instead of quitting the player.</summary>
        public bool StayWhenDone;

        public static LoadSweepOptions Parse(string[] args)
        {
            var o = new LoadSweepOptions();
            if (args == null) return o;
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                string value = null;
                int eq = key.IndexOf('=');
                if (eq >= 0)
                {
                    value = key[(eq + 1)..];
                    key = key[..eq];
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    value = args[i + 1];
                }

                switch (key.ToLowerInvariant())
                {
                    case ModesArg:
                        o.Modes = ParseNames(value);
                        break;
                    case IntensitiesArg:
                        o.Intensities = ParseInts(value, 1, 4, o.Intensities);
                        break;
                    case PlayersArg:
                        if (!string.IsNullOrWhiteSpace(value)) o.Players = value.Trim().ToLowerInvariant();
                        break;
                    case RepeatsArg:
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) && r >= 1) o.Repeats = r;
                        break;
                    case CooldownArg:
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float c) && c >= 0f) o.CooldownSeconds = c;
                        break;
                    case TimeoutArg:
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float t) && t > 0f) o.CellTimeoutSeconds = t;
                        break;
                    case IncludeMaelstromArg:
                        o.IncludeMaelstrom = true;
                        break;
                    case StayArg:
                        o.StayWhenDone = true;
                        break;
                }
            }
            return o;
        }

        /// <summary>The player count this run uses for a card: the policy clamped into the card's range.</summary>
        public int ResolvePlayers(int minAllowed, int maxAllowed)
        {
            int lo = Math.Max(1, minAllowed);
            int hi = Math.Max(lo, maxAllowed);
            if (Players == PlayersMax) return hi;
            if (Players == PlayersMin) return lo;
            if (!int.TryParse(Players, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) n = DefaultPlayers;
            return Math.Clamp(n, lo, hi);
        }

        /// <summary>True when this run wants the named mode (null list = everything).</summary>
        public bool WantsMode(string modeName)
        {
            if (Modes == null) return true;
            foreach (var m in Modes)
                if (string.Equals(m, modeName, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static List<string> ParseNames(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return null;
            var names = csv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (names.Count == 0) return null;
            if (names.Count == 1 && string.Equals(names[0], "all", StringComparison.OrdinalIgnoreCase)) return null;
            return names;
        }

        static List<int> ParseInts(string csv, int min, int max, List<int> fallback)
        {
            if (string.IsNullOrWhiteSpace(csv)) return fallback;
            var list = new List<int>();
            foreach (var part in csv.Split(','))
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v >= min && v <= max && !list.Contains(v))
                    list.Add(v);
            list.Sort();
            return list.Count > 0 ? list : fallback;
        }
    }

    /// <summary>
    /// Turns Load Time Insights reports into the sweep's table: one row per cell, WORST CELL FIRST,
    /// each row carrying its verdict against the published target. Pure C# so the editor tests can
    /// hold the ordering and the verdicts.
    /// </summary>
    public static class LoadSweepTable
    {
        const string PrismsLaidCounter = "Prisms laid during load";

        public static LoadSweepCell CellFromReport(LoadInsightReport report, int repeat, string note = null)
        {
            var cell = new LoadSweepCell { repeat = repeat, note = note ?? "" };
            if (report == null) return cell;

            cell.recorded = true;
            cell.mode = report.gameMode ?? "";
            cell.intensity = report.intensity;
            cell.players = report.totalPlayers;
            cell.aiBackfill = report.aiBackfill;
            cell.scene = report.sceneTo ?? "";
            cell.totalMs = report.totalMs;
            cell.visualReadyMs = report.visualReadyMs;
            cell.unattributedMs = report.unattributedMs;
            cell.worstFrameMs = report.worstFrameMs;
            cell.completionReason = report.completionReason ?? "";
            cell.completed = !report.interrupted &&
                             !cell.completionReason.StartsWith("Aborted", StringComparison.OrdinalIgnoreCase);
            var top = report.topCosts != null && report.topCosts.Count > 0 ? report.topCosts[0] : null;
            cell.topCost = top?.label ?? "";
            cell.topCostMs = top?.exclusiveMs ?? 0f;
            cell.errorCount = report.errors?.Count ?? 0;
            var prisms = report.counters?.FirstOrDefault(c => c.name == PrismsLaidCounter);
            cell.prismsLaid = prisms?.value ?? 0L;
            return cell;
        }

        /// <summary>PASS only for a recorded, completed load inside the target.</summary>
        public static bool Passes(LoadSweepCell cell, float targetSeconds) =>
            cell != null && cell.recorded && cell.completed && cell.totalMs <= targetSeconds * 1000f;

        public static string Verdict(LoadSweepCell cell, float targetSeconds)
        {
            if (cell == null || !cell.recorded) return "NO DATA";
            return Passes(cell, targetSeconds) ? "PASS" : "FAIL";
        }

        /// <summary>
        /// Worst first: a cell with no report or an aborted load outranks every timed one (an
        /// unknown or a failure is worse than any number), then longest load first.
        /// </summary>
        public static List<LoadSweepCell> SortedWorstFirst(IEnumerable<LoadSweepCell> cells)
        {
            return cells
                .OrderBy(c => c.recorded ? 1 : 0)
                .ThenBy(c => c.completed ? 1 : 0)
                .ThenByDescending(c => c.totalMs)
                .ToList();
        }

        public static string BuildMarkdown(LoadSweepSummary s)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(4096);
            sb.AppendLine($"# Load-time sweep — {s.timestamp}");
            sb.AppendLine();
            sb.AppendLine($"Source: {s.origin} · {s.platform} · {s.gitBranch}/{s.gitCommitHash}  ");
            sb.AppendLine($"Targets: cold boot → menu **{s.coldBootTargetSeconds.ToString("0.#", inv)} s** · " +
                          $"menu → first playable **{s.menuToPlayableTargetSeconds.ToString("0.#", inv)} s** " +
                          "(Docs/PERFORMANCE_OPTIMIZATION.md §0.6)  ");
            sb.AppendLine($"Players per cell: {s.playersPolicy}  ");
            if (!string.IsNullOrEmpty(s.notes)) sb.AppendLine($"Notes: {s.notes}  ");
            sb.AppendLine();

            sb.AppendLine("## Cold boot → main menu");
            sb.AppendLine();
            sb.AppendLine("| Verdict | Total | Top cost | Unattributed | Worst frame | Report |");
            sb.AppendLine("|---|---|---|---|---|---|");
            if (s.hasBoot && s.boot != null && s.boot.recorded)
            {
                var b = s.boot;
                sb.AppendLine($"| {Verdict(b, s.coldBootTargetSeconds)} | {Sec(b.totalMs)} | {Cost(b)} | " +
                              $"{Sec(b.unattributedMs)} | {Ms(b.worstFrameMs)} | {b.reportPath} |");
            }
            else
            {
                sb.AppendLine("| NO DATA | — | — | — | — | the boot was not recorded (see notes) |");
            }
            sb.AppendLine();

            var rows = SortedWorstFirst(s.cells ?? new List<LoadSweepCell>());
            int pass = rows.Count(c => Passes(c, s.menuToPlayableTargetSeconds));
            int noData = rows.Count(c => !c.recorded);
            int fail = rows.Count - pass - noData;

            sb.AppendLine("## Menu → first playable frame, worst cell first");
            sb.AppendLine();
            sb.AppendLine($"{rows.Count} cells: **{pass} pass · {fail} fail · {noData} no data** against " +
                          $"{s.menuToPlayableTargetSeconds.ToString("0.#", inv)} s.");
            sb.AppendLine();
            sb.AppendLine("| # | Verdict | Mode | I | Players (AI) | Total | Over target | Top cost | Unattributed | Worst frame | Prisms laid | Errors | Note |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int i = 0; i < rows.Count; i++)
            {
                var c = rows[i];
                string over = !c.recorded ? "—"
                    : c.totalMs > s.menuToPlayableTargetSeconds * 1000f
                        ? "+" + Sec(c.totalMs - s.menuToPlayableTargetSeconds * 1000f)
                        : "—";
                string note = c.note ?? "";
                if (c.recorded && !c.completed)
                    note = string.IsNullOrEmpty(note) ? c.completionReason : $"{c.completionReason}; {note}";
                sb.AppendLine($"| {i + 1} | {Verdict(c, s.menuToPlayableTargetSeconds)} | {c.mode} | {c.intensity} | " +
                              $"{c.players} ({c.aiBackfill}) | {(c.recorded ? Sec(c.totalMs) : "—")} | {over} | " +
                              $"{Cost(c)} | {(c.recorded ? Sec(c.unattributedMs) : "—")} | " +
                              $"{(c.recorded ? Ms(c.worstFrameMs) : "—")} | {(c.recorded ? c.prismsLaid.ToString("N0", inv) : "—")} | " +
                              $"{(c.recorded ? c.errorCount.ToString(inv) : "—")} | {Escape(note)} |");
            }
            sb.AppendLine();
            sb.AppendLine("Per-cell reports (JSON + text) sit beside this file; the text report carries the full attribution, " +
                          "stalls and hints for any row above.");
            return sb.ToString();
        }

        static string Sec(float ms) => (ms / 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        static string Ms(float ms) => ms.ToString("0", CultureInfo.InvariantCulture) + " ms";
        static string Cost(LoadSweepCell c) =>
            !c.recorded || string.IsNullOrEmpty(c.topCost) ? "—" : $"{Escape(c.topCost)} ({Sec(c.topCostMs)})";
        static string Escape(string text) => (text ?? "").Replace("|", "\\|").Replace("\n", " ");
    }
}
