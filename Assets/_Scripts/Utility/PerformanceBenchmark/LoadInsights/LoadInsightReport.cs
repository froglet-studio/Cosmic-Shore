using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Where a load-time span's cost belongs. Values are serialized — never renumber.
    /// </summary>
    public enum LoadInsightCategory
    {
        SceneLoad = 0,      // Unity/Netcode scene load + activation
        Netcode = 1,        // session sync, replication waits, config/roster RPCs
        ScriptedDelay = 2,  // hardcoded UniTask.Delay / WaitForSeconds gates in the pipeline
        Vessels = 3,        // human player vessel instantiate + DI + init
        AiBackfill = 4,     // AI players + AI vessels
        Environment = 5,    // cell membrane/nucleus/cytoplasm, density grids, environment segments
        Flora = 6,
        Fauna = 7,
        Prisms = 8,         // prism/trail spawning
        Crystals = 9,
        Pooling = 10,       // pool prewarm / buffer fill
        UiHud = 11,         // connecting panel, cinematic, HUD build
        GameFlow = 12,      // controller lifecycle, ready sync, countdown
        Other = 13
    }

    /// <summary>Display names for <see cref="LoadInsightCategory"/> (used by report text and the editor tab).</summary>
    public static class LoadInsightCategories
    {
        public static string DisplayName(LoadInsightCategory c) => c switch
        {
            LoadInsightCategory.SceneLoad => "Scene Load",
            LoadInsightCategory.Netcode => "Netcode & Sync",
            LoadInsightCategory.ScriptedDelay => "Scripted Delays",
            LoadInsightCategory.Vessels => "Vessels & Players",
            LoadInsightCategory.AiBackfill => "AI Backfill",
            LoadInsightCategory.Environment => "Cell & Environment",
            LoadInsightCategory.Flora => "Flora",
            LoadInsightCategory.Fauna => "Fauna",
            LoadInsightCategory.Prisms => "Prisms & Trails",
            LoadInsightCategory.Crystals => "Crystals",
            LoadInsightCategory.Pooling => "Pooling & Prewarm",
            LoadInsightCategory.UiHud => "UI & HUD",
            LoadInsightCategory.GameFlow => "Game Flow & Countdown",
            _ => "Other"
        };

        /// <summary>Pseudo-category index used for time no span claimed.</summary>
        public const int UnattributedIndex = -1;
        public const string UnattributedName = "Unattributed (engine & other)";
    }

    /// <summary>One timed slice of the load. Spans nest; overlapping async spans are allowed.</summary>
    [Serializable]
    public class LoadInsightSpan
    {
        public int id;
        public int parentId = -1;      // innermost span active when this one began (-1 = root)
        public int depth;
        public string label;
        public int category;
        public string categoryName;    // redundant with category, kept so raw JSON reads clean
        public float startMs;
        public float endMs = -1f;      // -1 while open
        public float durationMs;
        public float exclusiveMs;      // wall-clock attributed to THIS span (innermost-active wins)
        public bool isWait;            // deliberate wait (delay/replication/human) rather than CPU work
        public bool isHumanWait;       // waiting on a human (e.g. Ready click) — reported separately
        public bool truncated;         // still open when the load completed
        public bool offMainThread;     // began off the main thread (UGS/Netcode continuation)
    }

    /// <summary>Per-category rollup of exclusive (attributed) wall-clock — the pie chart data.</summary>
    [Serializable]
    public class LoadCategorySlice
    {
        public int category;           // LoadInsightCategory value, or -1 = unattributed
        public string name;
        public float attributedMs;
        public float percent;
        public int spanCount;
        public float waitMs;           // portion of attributedMs inside wait-flagged spans
    }

    /// <summary>Spans aggregated by (category, label) and ranked — the "top costs" table.</summary>
    [Serializable]
    public class LoadTopCost
    {
        public string label;
        public int category;
        public string categoryName;
        public int count;
        public float totalMs;          // sum of span durations (overlaps possible across async spans)
        public float exclusiveMs;      // sum of attributed time — ranks the table
        public float maxSingleMs;
        public bool isWait;
    }

    /// <summary>A single frame during the load that exceeded the stall threshold.</summary>
    [Serializable]
    public class LoadStall
    {
        public float atMs;             // offset from load start (frame end)
        public float durationMs;       // frame time
        public string during;          // innermost active span label ("—" if none)
    }

    /// <summary>An instant event on the load timeline.</summary>
    [Serializable]
    public class LoadMark
    {
        public float atMs;
        public string label;
    }

    /// <summary>A named tally (objects spawned, RPCs observed, …).</summary>
    [Serializable]
    public class LoadCounter
    {
        public string name;
        public long value;
    }

    /// <summary>
    /// Aggregated hot-path sub-stage timing (see <see cref="LoadInsights.AccumulateSample"/>) —
    /// the per-item breakdown of loops too hot for per-item spans (e.g. a 25k-prism lay).
    /// </summary>
    [Serializable]
    public class LoadAccumulator
    {
        public string label;
        public long count;
        public float totalMs;
        public float maxSingleMs;
    }

    /// <summary>
    /// Complete result of one recorded game load: what happened, in what order, and where the
    /// wall-clock went. Attribution is exact — every millisecond between load start and playable
    /// lands in exactly one category (innermost active span wins; span-free time is
    /// "Unattributed"), so the percentages always sum to 100.
    ///
    /// Serialized to JSON next to a human/Claude-readable .txt under
    /// <c>persistentDataPath/Benchmarks/LoadInsights/</c>.
    /// </summary>
    [Serializable]
    public class LoadInsightReport
    {
        public const int CurrentSchemaVersion = 1;

        // ── Identity / source ───────────────────────────
        public int schemaVersion;
        public string reportId;
        public string timestamp;
        public SourceInfo source = new();
        public string gitBranch;
        public string gitCommitHash;

        // ── What load this was ──────────────────────────
        public string trigger;             // what started the recording
        public string completionReason;    // "Playable (turn started)", "Aborted: …", "Timeout", …
        public bool interrupted;           // recovered from an in-flight snapshot (app died mid-load)
        public string sceneFrom;
        public string sceneTo;
        public string gameMode;
        public int intensity;
        public int totalPlayers;
        public int humanPlayers;
        public int aiBackfill;
        public bool isMultiplayer;
        public string networkRole = "";    // Host / Server / Client / Local
        public int connectedClients;

        // ── Headline numbers ────────────────────────────
        public float totalMs;              // load start → playable (or abort)
        public float visualReadyMs = -1f;  // load start → OnClientReady (splash cleared)
        public float waitMs;               // attributed to wait-flagged spans
        public float humanWaitMs;          // subset of waitMs spent waiting on humans
        public float workMs;               // everything else (incl. unattributed engine time)
        public float unattributedMs;
        public int framesDuringLoad;
        public float avgFpsDuringLoad;
        public float worstFrameMs;
        public int droppedSpans;           // spans not recorded because the buffer filled

        // ── Data ────────────────────────────────────────
        public List<LoadInsightSpan> spans = new();
        public List<LoadCategorySlice> slices = new();
        public List<LoadTopCost> topCosts = new();
        public List<LoadStall> stalls = new();
        public List<LoadMark> marks = new();
        public List<LoadCounter> counters = new();
        public List<LoadAccumulator> accumulators = new();
        public List<SweepError> errors = new();
        public List<BenchmarkHint> hints = new();

        public bool IsComplete => totalMs > 0f;

        // Git identity cached per session — the in-flight snapshot rebuilds the report every few
        // seconds DURING the load being measured, and spawning a git subprocess each time would
        // perturb the very thing we're timing.
        static string s_gitCommitCache;
        static string s_gitBranchCache;

        public void PopulateEnvironment()
        {
            reportId = Guid.NewGuid().ToString("N")[..12];
            timestamp = DateTime.UtcNow.ToString("o");

            schemaVersion = CurrentSchemaVersion;
            source ??= new SourceInfo();
            source.origin = Application.isEditor ? ReportOrigin.Editor : ReportOrigin.DevBuild;
            source.platform = Application.platform.ToString();
            source.deviceModel = SystemInfo.deviceModel;
            source.unityVersion = Application.unityVersion;

            if (s_gitCommitCache == null)
            {
                s_gitCommitCache = BenchmarkReport.TryRunGit("rev-parse --short HEAD");
                s_gitBranchCache = BenchmarkReport.TryRunGit("rev-parse --abbrev-ref HEAD");
            }
            gitCommitHash = s_gitCommitCache;
            gitBranch = s_gitBranchCache;
        }

        // ── Persistence ─────────────────────────────────

        /// <summary>Writes the JSON report (+ a readable .txt sibling). Returns the JSON path.</summary>
        public string SaveToFile(string outputFolder)
        {
            string dir = Path.Combine(Application.persistentDataPath, outputFolder);
            Directory.CreateDirectory(dir);

            string scene = string.IsNullOrEmpty(sceneTo) ? "unknown" : SanitizeFileName(sceneTo);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string baseName = $"load_{scene}_{stamp}_{reportId}";

            string jsonPath = Path.Combine(dir, baseName + ".json");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(this, true));
            File.WriteAllText(Path.Combine(dir, baseName + ".txt"), BuildText());
            return jsonPath;
        }

        public static LoadInsightReport LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            try { return JsonUtility.FromJson<LoadInsightReport>(File.ReadAllText(filePath)); }
            catch { return null; }
        }

        static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // ── Text rendering (the "downloadable / paste-to-Claude" report) ──

        public string BuildText()
        {
            var sb = new StringBuilder(8192);
            const string heavy = "════════════════════════════════════════════════════════════════════";
            const string light = "────────────────────────────────────────────────────────────────────";

            sb.AppendLine(heavy);
            sb.AppendLine("  COSMIC SHORE · LOAD TIME INSIGHTS");
            sb.AppendLine(heavy);
            sb.AppendLine($"  Scene       {sceneFrom} → {sceneTo}");
            string players = totalPlayers > 0
                ? $"{totalPlayers} players ({humanPlayers} human + {aiBackfill} AI)"
                : "players n/a";
            string net = isMultiplayer
                ? $"multiplayer {networkRole}" + (connectedClients > 0 ? $" · {connectedClients} connected" : "")
                : "single player";
            sb.AppendLine($"  Game        {gameMode} · intensity {intensity} · {players} · {net}");
            // Older reports ended at countdown GO; when visual-ready IS the endpoint, don't
            // print the same number twice.
            bool visualDiffers = visualReadyMs >= 0f && Mathf.Abs(totalMs - visualReadyMs) > 50f;
            sb.AppendLine($"  Result      {completionReason} after {Sec(totalMs)}"
                          + (visualDiffers ? $"   (visually loaded at {Sec(visualReadyMs)})" : ""));
            if (interrupted)
                sb.AppendLine("  ⚠ INTERRUPTED — the app was killed mid-load; this is the last in-flight snapshot.");
            sb.AppendLine($"  Recorded    {timestamp} · {source?.origin} · {source?.platform} · {gitBranch}/{gitCommitHash}");
            if (framesDuringLoad > 0)
                sb.AppendLine($"  Frames      {framesDuringLoad} during load · avg {avgFpsDuringLoad:F1} fps · worst frame {worstFrameMs:F0} ms");
            sb.AppendLine($"  Trigger     {trigger}");

            // Where the time went — the pie in text form.
            sb.AppendLine(light);
            sb.AppendLine("  WHERE THE TIME WENT (exact wall-clock attribution — sums to 100%)");
            foreach (var s in slices.OrderByDescending(s => s.attributedMs))
            {
                if (s.attributedMs < 0.5f) continue;
                sb.AppendLine($"  {Bar(s.percent),-12} {s.name,-30} {Ms(s.attributedMs),12}   {s.percent,5:F1}%"
                              + (s.waitMs > 0.5f ? $"   (waiting {Ms(s.waitMs)})" : ""));
            }

            // Waiting vs working.
            sb.AppendLine(light);
            sb.AppendLine("  WAITING vs WORKING");
            float waitPct = totalMs > 0f ? waitMs / totalMs * 100f : 0f;
            sb.AppendLine($"  Waiting  {Sec(waitMs)} ({waitPct:F1}%) — delays, replication, sync, human ready-clicks"
                          + (humanWaitMs > 0.5f ? $" (human: {Sec(humanWaitMs)})" : ""));
            sb.AppendLine($"  Working  {Sec(workMs)} ({100f - waitPct:F1}%) — actual execution"
                          + (unattributedMs > 0.5f ? $" (of which {Sec(unattributedMs)} unattributed engine/other)" : ""));

            // Top costs.
            if (topCosts.Count > 0)
            {
                sb.AppendLine(light);
                sb.AppendLine("  TOP COSTS (ranked by attributed time)");
                sb.AppendLine("     ATTRIBUTED       TOTAL     ×     MAX  CATEGORY               WHAT");
                int rank = 1;
                foreach (var t in topCosts.Take(20))
                {
                    sb.AppendLine($"  {rank,2}. {Ms(t.exclusiveMs),9}  {Ms(t.totalMs),9}  {t.count,4}  {Ms(t.maxSingleMs),6}  {t.categoryName,-21} {t.label}{(t.isWait ? "  (wait)" : "")}");
                    rank++;
                }
            }

            // Scripted delays — the free wins. Ranked by ATTRIBUTED time: a 200ms delay whose
            // await couldn't resume during a 100s frame has a huge wall duration but only its
            // attributed share was actually "caused" by the delay.
            var scripted = topCosts.Where(t => t.category == (int)LoadInsightCategory.ScriptedDelay).ToList();
            if (scripted.Count > 0)
            {
                sb.AppendLine(light);
                sb.AppendLine("  SCRIPTED DELAYS (hardcoded waits — every attributed ms is a tuning knob)");
                foreach (var t in scripted.OrderByDescending(t => t.exclusiveMs))
                    sb.AppendLine($"  {Ms(t.exclusiveMs),9} attributed  = {t.count} × {t.label}"
                                  + (t.totalMs > t.exclusiveMs * 1.5f ? $"   (wall {Ms(t.totalMs)} — stalled by other work)" : ""));
            }

            // Hot-path breakdown — what's inside the big spans (per-item stage accumulators).
            if (accumulators is { Count: > 0 })
            {
                sb.AppendLine(light);
                sb.AppendLine("  HOT-PATH BREAKDOWN (accumulated sub-stages inside the spans above)");
                sb.AppendLine("        TOTAL        ×      AVG      MAX  STAGE");
                foreach (var a in accumulators.OrderByDescending(a => a.totalMs))
                {
                    float avg = a.count > 0 ? a.totalMs / a.count : 0f;
                    sb.AppendLine($"  {Ms(a.totalMs),11}  {N(a.count),7}  {avg,5:F2}ms  {Ms(a.maxSingleMs),7}  {a.label}");
                }
            }

            // Frame stalls.
            if (stalls.Count > 0)
            {
                sb.AppendLine(light);
                sb.AppendLine($"  FRAME STALLS during load ({stalls.Count} frames over threshold)");
                foreach (var st in stalls.OrderByDescending(s => s.durationMs).Take(12))
                    sb.AppendLine($"  at {Sec(st.atMs),8} — {st.durationMs,6:F0} ms frame — during \"{st.during}\"");
            }

            // Errors.
            if (errors.Count > 0)
            {
                sb.AppendLine(light);
                sb.AppendLine($"  ERRORS during load ({errors.Count})");
                foreach (var e in errors.Take(15))
                    sb.AppendLine($"  [{e.timeSeconds,6:F1}s] {e.type}: {e.message}");
                if (errors.Count > 15) sb.AppendLine($"  …and {errors.Count - 15} more");
            }

            // Counters.
            if (counters.Count > 0)
            {
                sb.AppendLine(light);
                sb.AppendLine("  COUNTERS");
                foreach (var c in counters.OrderByDescending(c => c.value))
                    sb.AppendLine($"  {N(c.value),8}  {c.name}");
            }

            // Insights.
            if (hints.Count > 0)
            {
                sb.AppendLine(light);
                sb.AppendLine("  INSIGHTS");
                foreach (var h in hints.OrderByDescending(h => (int)h.severity))
                {
                    sb.AppendLine($"  [{h.severity}] {h.title}");
                    if (!string.IsNullOrEmpty(h.finding)) sb.AppendLine($"      {h.finding}");
                    if (!string.IsNullOrEmpty(h.fixAdvice)) sb.AppendLine($"      Fix: {h.fixAdvice}");
                }
            }

            // Timeline.
            sb.AppendLine(light);
            sb.AppendLine("  TIMELINE (▶ marks · spans indented by nesting)");
            AppendTimeline(sb);

            sb.AppendLine(heavy);
            if (droppedSpans > 0)
                sb.AppendLine($"  note: {droppedSpans} spans were dropped (span buffer full) — detail merged into parents.");
            sb.AppendLine("  Generated by FrogletTools > Performance Benchmark > Load Time Insights.");
            return sb.ToString();
        }

        void AppendTimeline(StringBuilder sb)
        {
            // Merge spans + marks into one chronological stream; cap output so a fauna-heavy
            // load can't produce a 3000-line report.
            const int maxLines = 160;
            var events = new List<(float at, int order, string line)>(spans.Count + marks.Count);

            foreach (var m in marks)
                events.Add((m.atMs, 0, $"  ▶ {m.label}"));

            foreach (var sp in spans)
            {
                string indent = new string(' ', Math.Min(sp.depth, 8) * 2);
                string flags = (sp.isWait ? " (wait)" : "") + (sp.truncated ? " (still running at end)" : "")
                             + (sp.offMainThread ? " (off-main)" : "");
                events.Add((sp.startMs, 1, $"  {indent}• [{sp.durationMs,7:F0} ms] {sp.categoryName}: {sp.label}{flags}"));
            }

            int emitted = 0;
            foreach (var e in events.OrderBy(e => e.at).ThenBy(e => e.order))
            {
                if (emitted++ >= maxLines)
                {
                    sb.AppendLine($"  … {events.Count - maxLines} more timeline entries (see JSON for the full list)");
                    break;
                }
                sb.AppendLine($"  {e.at / 1000f,8:F3}s{e.line}");
            }
        }

        static string Bar(float percent)
        {
            int blocks = Mathf.Clamp(Mathf.RoundToInt(percent / 10f), 0, 10);
            if (blocks == 0 && percent >= 0.5f) blocks = 1;
            return new string('█', blocks) + new string('·', 10 - blocks);
        }

        // Invariant culture: reports get pasted across machines/locales — "1,11,813 ms"-style
        // regional digit grouping (observed on an en-IN Windows editor) hurts readability.
        static string N(long v) => v.ToString("N0", CultureInfo.InvariantCulture);
        static string Ms(float ms) => ms >= 100f
            ? ms.ToString("N0", CultureInfo.InvariantCulture) + " ms"
            : ms.ToString("F1", CultureInfo.InvariantCulture) + " ms";
        static string Sec(float ms) => (ms / 1000f).ToString("F2", CultureInfo.InvariantCulture) + " s";
    }
}
