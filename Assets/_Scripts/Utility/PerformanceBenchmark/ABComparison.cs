#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The pure half of the DiagnosticsHUD <c>ab</c> console command: parsing, the arm
    /// schedule, the per-arm accumulator, the paired-delta statistics, the drift and cap
    /// checks, and the one-line verdict. Everything here is deterministic and Unity-free so
    /// it can be tested in edit mode; DiagnosticsHUD owns only the clock and the sampling.
    ///
    /// <para><b>Why paired, counterbalanced rounds.</b> A single "A then B" comparison
    /// charges any drift in the world (mass still growing, a population still settling,
    /// thermals) to whichever arm ran second. Each ROUND therefore records one A and one B
    /// back to back and the statistic is the per-round difference B - A; the order flips
    /// every round (A B | B A | A B ...), so a LINEAR drift cancels exactly over an even
    /// number of rounds and to one third of its size over three. The <c>freeze</c> command is
    /// what removes the drift instead of cancelling it.</para>
    ///
    /// <para><b>Why the error bar is a standard error.</b> The rounds ARE the replicates:
    /// frames inside one recording are autocorrelated (a GC pause, a spatial-index rebuild),
    /// so a per-frame deviation would claim a precision the run does not have. With three
    /// rounds the standard error of the paired deltas is a rough but honest bar, and a delta
    /// inside two of them is reported as noise.</para>
    /// </summary>
    public static class ABComparison
    {
        public const string CommandName = "ab";
        public const int DefaultSeconds = 10;
        public const int DefaultRounds = 3;
        public const int MaxSeconds = 120;
        public const int MaxRounds = 10;

        /// <summary>
        /// Wait after running an arm's command before the census and the recording. Covers
        /// the command's own spike (renderers hide/show and prismpath each scan every object)
        /// and FrameTimingManager's ~4-frame reporting lag.
        /// </summary>
        public const float SettleSeconds = 3f;

        /// <summary>Gap between the pre-recording census (a spike of its own) and the first recorded frame.</summary>
        public const float PostCensusGapSeconds = 0.5f;

        /// <summary>A count that moves more than this fraction inside one arm means the world changed under the measurement.</summary>
        public const float DriftTolerance = 0.05f;

        /// <summary>A delta within this many standard errors of zero is reported as noise.</summary>
        public const float NoiseSigmas = 2f;

        public enum Arm { A = 0, B = 1 }

        public struct Request
        {
            public string CommandA, CommandB;
            public int Seconds, Rounds;
        }

        // ── parsing ───────────────────────────────────────────────────────

        public static string Usage =>
            "usage: ab \"<command A>\" \"<command B>\" [seconds] [rounds]   " +
            "e.g. ab \"renderers hide *Spindle\" \"renderers show\" 20 6   (ab stop cancels)";

        /// <summary>
        /// Parses the arguments DiagnosticsHUD hands a handler. The console splits on spaces
        /// and knows nothing about quotes, so the arguments are re-joined and the two quoted
        /// commands recovered here; runs of spaces inside a command collapse to one, which no
        /// command can tell apart.
        /// </summary>
        public static bool TryParse(string[] args, out Request request, out string error)
        {
            request = new Request { Seconds = DefaultSeconds, Rounds = DefaultRounds };
            error = null;

            string joined = args == null ? "" : string.Join(" ", args).Trim();
            var quoted = new List<string>(2);
            var rest = new StringBuilder();
            int i = 0;
            while (i < joined.Length)
            {
                char c = joined[i];
                if (c == '"')
                {
                    int close = joined.IndexOf('"', i + 1);
                    if (close < 0) { error = "unmatched quote. " + Usage; return false; }
                    quoted.Add(joined.Substring(i + 1, close - i - 1).Trim());
                    i = close + 1;
                    continue;
                }
                rest.Append(c);
                i++;
            }

            if (quoted.Count != 2 || quoted[0].Length == 0 || quoted[1].Length == 0)
            {
                error = "need exactly two quoted commands. " + Usage;
                return false;
            }

            var numbers = rest.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (numbers.Length > 2) { error = "too many arguments. " + Usage; return false; }
            for (int n = 0; n < numbers.Length; n++)
            {
                if (!int.TryParse(numbers[n], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) || v <= 0)
                {
                    error = $"'{numbers[n]}' is not a positive whole number. " + Usage;
                    return false;
                }
                if (n == 0) request.Seconds = Math.Min(v, MaxSeconds);
                else request.Rounds = Math.Min(v, MaxRounds);
            }

            request.CommandA = quoted[0];
            request.CommandB = quoted[1];
            return true;
        }

        /// <summary>The first word of a console line, lower-cased - how DiagnosticsHUD resolves a handler.</summary>
        public static string CommandNameOf(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return "";
            string trimmed = commandLine.Trim();
            int space = trimmed.IndexOf(' ');
            return (space < 0 ? trimmed : trimmed.Substring(0, space)).ToLowerInvariant();
        }

        // ── schedule ──────────────────────────────────────────────────────

        /// <summary>
        /// The recording order: round r (0-based) runs A then B when r is even and B then A
        /// when r is odd. Entry 2r and 2r+1 always belong to round r.
        /// </summary>
        public static Arm[] Schedule(int rounds)
        {
            rounds = Math.Max(0, rounds);
            var order = new Arm[rounds * 2];
            for (int r = 0; r < rounds; r++)
            {
                bool flip = (r & 1) == 1;
                order[2 * r] = flip ? Arm.B : Arm.A;
                order[2 * r + 1] = flip ? Arm.A : Arm.B;
            }
            return order;
        }

        // ── statistics ────────────────────────────────────────────────────

        public struct Delta
        {
            /// <summary>Mean of the per-round B - A differences.</summary>
            public float Mean;
            /// <summary>Standard error of that mean; 0 when there is only one round.</summary>
            public float StdErr;
            public int Rounds;

            /// <summary>At least two rounds and the mean clears <see cref="NoiseSigmas"/> standard errors.</summary>
            public bool IsReal => Rounds >= 2 && Math.Abs(Mean) > NoiseSigmas * StdErr;
        }

        /// <summary>
        /// Paired difference over rounds. <paramref name="a"/>[r] and <paramref name="b"/>[r]
        /// are the same round's two arms; the lists must be the same length.
        /// </summary>
        public static Delta PairedDelta(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            if (a == null || b == null) throw new ArgumentNullException(a == null ? nameof(a) : nameof(b));
            if (a.Count != b.Count) throw new ArgumentException("arms must have one reading per round each");

            int n = a.Count;
            if (n == 0) return default;

            double sum = 0;
            for (int r = 0; r < n; r++) sum += b[r] - a[r];
            double mean = sum / n;

            double se = 0;
            if (n >= 2)
            {
                double ss = 0;
                for (int r = 0; r < n; r++)
                {
                    double d = (b[r] - a[r]) - mean;
                    ss += d * d;
                }
                se = Math.Sqrt(ss / (n - 1)) / Math.Sqrt(n);
            }

            return new Delta { Mean = (float)mean, StdErr = (float)se, Rounds = n };
        }

        /// <summary>
        /// True when the largest reading exceeds the smallest by more than
        /// <paramref name="tolerance"/> of the smallest. A zero minimum with a non-zero maximum
        /// is drift (something appeared); all zeros is not.
        /// </summary>
        public static bool Drifted(IReadOnlyList<int> readings, float tolerance, out float spread)
        {
            spread = 0f;
            if (readings == null || readings.Count < 2) return false;

            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < readings.Count; i++)
            {
                if (readings[i] < min) min = readings[i];
                if (readings[i] > max) max = readings[i];
            }
            if (max <= 0) return false;
            if (min <= 0) { spread = float.PositiveInfinity; return true; }

            spread = (max - min) / (float)min;
            return spread > tolerance;
        }

        /// <summary>
        /// Whether a recording's frame time means anything. A capped or idle frame hides any
        /// change smaller than its idle time; busy CPU and GPU still mean something there, but
        /// the frame time and fps do not, and a stall usually means throttling or a hitch.
        /// </summary>
        public static bool IsFrameTrustworthy(FrameBoundness.FrameLimit limit) =>
            limit != FrameBoundness.FrameLimit.AtNamedCap &&
            limit != FrameBoundness.FrameLimit.CappedByIdle &&
            limit != FrameBoundness.FrameLimit.Stalled &&
            limit != FrameBoundness.FrameLimit.IdleNoCap;

        // ── accumulation ──────────────────────────────────────────────────

        /// <summary>Per-frame sums for one arm's recording. Timing sums count only frames that carried timing data.</summary>
        public sealed class ArmAccumulator
        {
            readonly List<float> _frameMs = new(2048);
            double _frameSum, _cpuSum, _busySum, _gpuSum, _drawSum, _batchSum, _setPassSum, _gcKbSum;
            int _timedFrames;

            public int Frames => _frameMs.Count;
            public int TimedFrames => _timedFrames;

            public void Add(float frameMs, float cpuMs, float busyCpuMs, float gpuMs,
                            int draws, int batches, int setPass, double gcKb)
            {
                _frameMs.Add(frameMs);
                _frameSum += frameMs;
                _drawSum += draws;
                _batchSum += batches;
                _setPassSum += setPass;
                _gcKbSum += gcKb;

                if (cpuMs > 0.001f || gpuMs > 0.001f)
                {
                    _timedFrames++;
                    _cpuSum += cpuMs;
                    _busySum += busyCpuMs;
                    _gpuSum += gpuMs;
                }
            }

            public ArmRecord ToRecord(Arm arm, int round)
            {
                int n = _frameMs.Count;
                var r = new ArmRecord { arm = arm.ToString(), round = round + 1, frames = n, timedFrames = _timedFrames };
                if (n > 0)
                {
                    r.avgFrameMs = (float)(_frameSum / n);
                    r.avgDraws = (float)(_drawSum / n);
                    r.avgBatches = (float)(_batchSum / n);
                    r.avgSetPass = (float)(_setPassSum / n);
                    r.avgGcKbPerFrame = (float)(_gcKbSum / n);

                    var sorted = new List<float>(_frameMs);
                    sorted.Sort();
                    int p99 = (int)Math.Round(0.99 * (n - 1));
                    r.p99FrameMs = sorted[Math.Min(Math.Max(p99, 0), n - 1)];
                }
                if (_timedFrames > 0)
                {
                    r.avgCpuMs = (float)(_cpuSum / _timedFrames);
                    r.avgBusyCpuMs = (float)(_busySum / _timedFrames);
                    r.avgGpuMs = (float)(_gpuSum / _timedFrames);
                }
                return r;
            }
        }

        /// <summary>One arm's recording in one round - one row of the saved report.</summary>
        [Serializable]
        public class ArmRecord
        {
            public string arm;
            public int round;
            public int frames, timedFrames;
            public float avgFrameMs, p99FrameMs;
            public float avgCpuMs, avgBusyCpuMs, avgGpuMs;
            public float avgDraws, avgBatches, avgSetPass, avgGcKbPerFrame;

            /// <summary>Instanced prism entities at the first and last recorded frame.</summary>
            public int prismEntsStart, prismEntsEnd;

            /// <summary>Enabled renderers (the culling population) just before and just after the recording.</summary>
            public int renderersStart, renderersEnd;

            public string frameLimit;
            public bool frameTrustworthy;
            public float idleMs;

            /// <summary>What the arm's command answered when it was run for this recording.</summary>
            public string commandResult;
        }

        [Serializable]
        public class Report
        {
            public string scene, timestamp, commandA, commandB, prismPath;
            public int seconds, rounds;
            public float settleSeconds;
            public bool frozenAtStart, frozenAtEnd, completed;
            public string summary;
            public float cpuBusyDelta, cpuBusyStdErr, gpuDelta, gpuStdErr, frameDelta, frameStdErr;
            public float drawsDelta, gcKbDelta;
            public List<string> warnings = new();
            public List<ArmRecord> recordings = new();
        }

        // ── verdict ───────────────────────────────────────────────────────

        /// <summary>
        /// Fills the report's deltas and warnings from its recordings and returns the one-line
        /// console verdict. Recordings are paired by round; an incomplete round is dropped from
        /// the statistics (it is still in the saved file).
        /// </summary>
        public static string Summarize(Report report)
        {
            var aRec = new List<ArmRecord>();
            var bRec = new List<ArmRecord>();
            PairRounds(report.recordings, aRec, bRec);

            Delta cpu = PairedDelta(Select(aRec, r => r.avgBusyCpuMs), Select(bRec, r => r.avgBusyCpuMs));
            Delta gpu = PairedDelta(Select(aRec, r => r.avgGpuMs), Select(bRec, r => r.avgGpuMs));
            Delta frame = PairedDelta(Select(aRec, r => r.avgFrameMs), Select(bRec, r => r.avgFrameMs));
            Delta draws = PairedDelta(Select(aRec, r => r.avgDraws), Select(bRec, r => r.avgDraws));
            Delta gc = PairedDelta(Select(aRec, r => r.avgGcKbPerFrame), Select(bRec, r => r.avgGcKbPerFrame));

            report.cpuBusyDelta = cpu.Mean; report.cpuBusyStdErr = cpu.StdErr;
            report.gpuDelta = gpu.Mean; report.gpuStdErr = gpu.StdErr;
            report.frameDelta = frame.Mean; report.frameStdErr = frame.StdErr;
            report.drawsDelta = draws.Mean;
            report.gcKbDelta = gc.Mean;

            report.warnings.Clear();
            CollectWarnings(report, aRec, bRec);

            bool gpuTimed = HasGpuTiming(aRec) && HasGpuTiming(bRec);
            var sb = new StringBuilder(160);
            sb.Append("ab: CPU busy B-A = ").Append(Ms(cpu));
            sb.Append(" · GPU ").Append(gpuTimed ? Ms(gpu) : "n/a");
            sb.Append(" · frame ").Append(Ms(frame));
            sb.Append(" · draws ").Append(Signed(draws.Mean, "F0"));
            sb.Append(" · GC/f ").Append(Signed(gc.Mean, "F1")).Append(" KB");
            sb.Append(" (").Append(cpu.Rounds).Append(cpu.Rounds == 1 ? " round" : " rounds");
            sb.Append(", frozen: ").Append(report.frozenAtStart && report.frozenAtEnd ? "yes" : "no").Append(')');
            if (!report.completed) sb.Append(" INCOMPLETE");
            if (report.warnings.Count > 0)
                sb.Append(" - ").Append(report.warnings.Count).Append(report.warnings.Count == 1 ? " WARNING" : " WARNINGS")
                  .Append(": ").Append(report.warnings[0]);

            report.summary = sb.ToString();
            return report.summary;
        }

        static void PairRounds(List<ArmRecord> recordings, List<ArmRecord> a, List<ArmRecord> b)
        {
            var byRound = new SortedDictionary<int, ArmRecord[]>();
            foreach (var r in recordings)
            {
                if (!byRound.TryGetValue(r.round, out var pair)) byRound[r.round] = pair = new ArmRecord[2];
                pair[r.arm == nameof(Arm.A) ? 0 : 1] = r;
            }
            foreach (var pair in byRound.Values)
            {
                if (pair[0] == null || pair[1] == null) continue;
                a.Add(pair[0]);
                b.Add(pair[1]);
            }
        }

        static List<float> Select(List<ArmRecord> records, Func<ArmRecord, float> field)
        {
            var list = new List<float>(records.Count);
            foreach (var r in records) list.Add(field(r));
            return list;
        }

        static bool HasGpuTiming(List<ArmRecord> records)
        {
            if (records.Count == 0) return false;
            foreach (var r in records) if (r.avgGpuMs <= 0.001f) return false;
            return true;
        }

        static void CollectWarnings(Report report, List<ArmRecord> aRec, List<ArmRecord> bRec)
        {
            if (!report.frozenAtStart || !report.frozenAtEnd)
                report.warnings.Add(report.frozenAtStart != report.frozenAtEnd
                    ? "freeze changed during the run - arms were taken in different states"
                    : "world not frozen - growth lands on the later arm ('freeze on' first)");

            // Drift is the SAME arm reading differently over time. Between arms the counts are
            // allowed to differ - that is often the treatment itself (renderers hide changes the
            // renderer count, prismpath changes the entity count).
            foreach (var r in report.recordings)
            {
                if (Drifted(new[] { r.prismEntsStart, r.prismEntsEnd }, DriftTolerance, out float s))
                    report.warnings.Add($"prism entities drifted {Pct(s)} inside arm {r.arm} round {r.round}");
                if (Drifted(new[] { r.renderersStart, r.renderersEnd }, DriftTolerance, out s))
                    report.warnings.Add($"enabled renderers drifted {Pct(s)} inside arm {r.arm} round {r.round}");
            }
            CheckAcrossRounds(report, aRec, "A");
            CheckAcrossRounds(report, bRec, "B");

            foreach (var r in report.recordings)
                if (!r.frameTrustworthy)
                    report.warnings.Add($"arm {r.arm} round {r.round} frame is {r.frameLimit} - frame time is not a measurement ('fps uncap', click the Game view)");

            if (!report.completed)
                report.warnings.Add("run did not complete - only the finished rounds are counted");
        }

        static void CheckAcrossRounds(Report report, List<ArmRecord> records, string arm)
        {
            if (records.Count < 2) return;
            var ents = new List<int>(records.Count);
            var rends = new List<int>(records.Count);
            foreach (var r in records) { ents.Add(r.prismEntsEnd); rends.Add(r.renderersEnd); }
            if (Drifted(ents, DriftTolerance, out float s))
                report.warnings.Add($"prism entities drifted {Pct(s)} across arm {arm}'s rounds");
            if (Drifted(rends, DriftTolerance, out s))
                report.warnings.Add($"enabled renderers drifted {Pct(s)} across arm {arm}'s rounds");
        }

        static string Ms(Delta d)
        {
            string s = Signed(d.Mean, "F1") + " ms";
            if (d.Rounds < 2) return s + " (1 round, no error bar)";
            s += " ±" + d.StdErr.ToString("F1", CultureInfo.InvariantCulture);
            return d.IsReal ? s : s + " (noise)";
        }

        static string Signed(float v, string format) =>
            (v >= 0f ? "+" : "-") + Math.Abs(v).ToString(format, CultureInfo.InvariantCulture);

        static string Pct(float spread) =>
            float.IsInfinity(spread) ? "from zero" : (spread * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%";

        /// <summary>The readable companion to the JSON: the verdict, then one row per recording.</summary>
        public static string BuildText(Report r)
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine($"Cosmic Shore A/B - {r.scene}   {r.timestamp}");
            sb.AppendLine($"A: {r.commandA}");
            sb.AppendLine($"B: {r.commandB}");
            sb.AppendLine($"{r.rounds} rounds x {r.seconds}s per arm, {r.settleSeconds:F1}s settle · prism path {r.prismPath}");
            sb.AppendLine(r.summary);
            foreach (var w in r.warnings) sb.AppendLine("WARNING: " + w);
            sb.AppendLine("round arm  frames  frame ms  p99 ms  busy cpu  gpu ms  draws  GC KB/f  ents(start>end)  renderers(start>end)  limit");
            foreach (var x in r.recordings)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,5} {1,3}  {2,6}  {3,8:F2}  {4,6:F1}  {5,8:F2}  {6,6:F2}  {7,5:F0}  {8,7:F1}  {9,7}>{10,-7}  {11,9}>{12,-9}  {13}",
                    x.round, x.arm, x.frames, x.avgFrameMs, x.p99FrameMs, x.avgBusyCpuMs, x.avgGpuMs,
                    x.avgDraws, x.avgGcKbPerFrame, x.prismEntsStart, x.prismEntsEnd,
                    x.renderersStart, x.renderersEnd, x.frameLimit));
            return sb.ToString();
        }
    }
}
#endif
