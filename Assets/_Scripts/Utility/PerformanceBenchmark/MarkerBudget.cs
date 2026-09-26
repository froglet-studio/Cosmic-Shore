#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The pure half of <c>diag</c>'s per-system timings: which named Profiler markers a run
    /// times by default, how an operator adds more, and how one marker's per-frame series becomes
    /// the numbers a report quotes (mean, median, p95, max, how often it ran).
    ///
    /// <para>Why it exists: <c>prof</c> answers "where does the frame go" but only in the Editor,
    /// and only with the Profiler recording, which inflates script cost. The frame target is a
    /// DEVELOPMENT BUILD, and the industry-standard way to track a per-system budget there is to
    /// time named markers with <c>ProfilerRecorder</c>, with no Profiler attached
    /// (<see cref="MarkerBudgetRecorder"/>). This is that, folded into the <c>diag</c> report
    /// every scenario already produces.</para>
    ///
    /// <para>Deliberately free of Unity types so the statistics are unit-tested outside the
    /// Editor. Percentiles are the SAME nearest-rank rule <c>diag</c> has always used for its
    /// p99 frame time, so a marker's p95 and the frame's p99 are computed one way.</para>
    /// </summary>
    public static class MarkerBudget
    {
        /// <summary>
        /// The markers every <c>diag</c> run times unless told otherwise. Two kinds:
        /// <list type="bullet">
        /// <item>Unity's own frame phases, so a report says how the frame divides (scripts in
        /// <c>Update</c>, coroutines, physics, animation, UI, rendering) without a Profiler.</item>
        /// <item>This project's markers inside the systems the scenarios found expensive: the
        /// creature and gunfight paths of Wildlife Liberation, the collider-LOD tick, prism
        /// debris. <c>PERFORMANCE_OPTIMIZATION.md</c> §1.0 names why each is here.</item>
        /// </list>
        /// A marker that does not exist in the running build is reported as <c>found: false</c>
        /// rather than dropped, so a renamed marker cannot silently fall out of every report.
        /// </summary>
        public static readonly string[] DefaultMarkers =
        {
            // Unity frame phases (main thread).
            "PlayerLoop",
            "BehaviourUpdate",
            "CoroutinesDelayedCalls",
            "LateBehaviourUpdate",
            "UniTaskLoopRunnerPreLateUpdate",
            "Physics.Simulate",
            "Animators.Update",
            "UGUI.Rendering.UpdateBatches",
            "Inl_UniversalRenderTotal",

            // Creatures (Wildlife Liberation, every fauna cell).
            "Fauna.BodySync",
            "LightFauna.Tick.Goal",
            "LightFauna.Tick.Vessels",
            "LightFauna.Tick.PrismScan",
            "LightFauna.Feed",
            "LightFauna.Hunt",

            // Gunfight (Sparrow full-auto and every round's flight step).
            "FullAuto.Fire",
            "Projectile.Growth",
            "Projectile.SweepVessels",
            "Projectile.SweepPrisms",
            "Projectile.Fuze",

            // Prism mass.
            "LOD.Sweep",
            "LOD.Drain",
            "PrismDebris.RefreshConvergence",
        };

        /// <summary>The <c>diag</c> argument prefix that adds markers: <c>m=Name1,Name2</c>.</summary>
        public const string ExtraMarkersPrefix = "m=";

        /// <summary>
        /// The marker list for one run: the defaults, then any extras from an <c>m=</c> argument,
        /// in order and without duplicates. Names are case-sensitive, because Profiler marker
        /// names are.
        /// </summary>
        public static List<string> ResolveMarkers(IEnumerable<string> extras)
        {
            var list = new List<string>(DefaultMarkers.Length + 4);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var m in DefaultMarkers)
                if (seen.Add(m)) list.Add(m);
            if (extras != null)
                foreach (var m in extras)
                {
                    var name = m?.Trim();
                    if (!string.IsNullOrEmpty(name) && seen.Add(name)) list.Add(name);
                }
            return list;
        }

        /// <summary>
        /// Parses one <c>m=A,B,C</c> argument into marker names; anything else returns false so
        /// the caller can treat it as a label or a duration.
        /// </summary>
        public static bool TryParseExtraMarkers(string arg, out List<string> names)
        {
            names = null;
            if (arg == null || !arg.StartsWith(ExtraMarkersPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            names = new List<string>();
            foreach (var part in arg.Substring(ExtraMarkersPrefix.Length).Split(','))
            {
                var name = part.Trim();
                if (name.Length > 0) names.Add(name);
            }
            return true;
        }

        /// <summary>
        /// Nearest-rank percentile of an ASCENDING list: <c>sorted[round(p × (n − 1))]</c>, the rule
        /// <c>diag</c> already uses for its p99 frame time. 0 for an empty list.
        ///
        /// <para>Rounds half to EVEN, because that is what <c>Mathf.RoundToInt</c> does and the p99
        /// already in every report was computed with it — a different midpoint rule here would make
        /// the same list give two answers depending on which percentile asked.</para>
        /// </summary>
        public static float Percentile(IReadOnlyList<float> sorted, float p)
        {
            int n = sorted?.Count ?? 0;
            if (n == 0) return 0f;
            int i = (int)Math.Round(Math.Min(1f, Math.Max(0f, p)) * (n - 1), MidpointRounding.ToEven);
            return sorted[Math.Min(n - 1, Math.Max(0, i))];
        }

        [Serializable]
        public class MarkerStat
        {
            public string name;
            /// <summary>The Profiler category the name resolved in (a name can exist in several).</summary>
            public string category;
            /// <summary>False when no marker of this name existed in the running build.</summary>
            public bool found;
            /// <summary>"ms" for a timing marker; a counter's own unit otherwise (its values are raw).</summary>
            public string unit;
            /// <summary>
            /// Milliseconds per frame, over EVERY frame of the run: a frame the marker did not
            /// run in counts as 0, so an intermittent cost reads as its true average rather than
            /// as its cost when it happens. <see cref="presentPct"/> and <see cref="maxMs"/> say
            /// how intermittent and how bad.
            /// </summary>
            public float avgMs, p50Ms, p95Ms, maxMs;
            public float presentPct;
            /// <summary>How many times the marker ran per frame, averaged the same way.</summary>
            public float avgCalls;
            /// <summary>Frames this marker has data for, out of <see cref="runFrames"/>.</summary>
            public int framesWithData, runFrames;
            /// <summary>True when the recorder's buffer wrapped and the earliest frames were lost.</summary>
            public bool truncated;
        }

        /// <summary>
        /// Folds one marker's per-frame series into its report row. <paramref name="msPerFrame"/>
        /// and <paramref name="callsPerFrame"/> are parallel and may be SHORTER than the run (a
        /// recorder only has data for frames it saw); the missing frames count as zeros against
        /// <paramref name="runFrames"/>, which is what makes a marker's average comparable with
        /// the frame time average of the same report.
        /// </summary>
        public static MarkerStat Summarize(string name, string category, bool found,
                                           IReadOnlyList<float> msPerFrame, IReadOnlyList<float> callsPerFrame,
                                           int runFrames, bool truncated = false)
        {
            var stat = new MarkerStat
            {
                name = name,
                category = category ?? "",
                found = found,
                unit = "ms",
                truncated = truncated,
            };

            int have = msPerFrame?.Count ?? 0;
            int frames = Math.Max(runFrames, have);
            stat.framesWithData = have;
            stat.runFrames = frames;
            if (frames == 0) return stat;

            var values = new List<float>(frames);
            double sum = 0, calls = 0;
            int present = 0;
            for (int i = 0; i < have; i++)
            {
                float v = Math.Max(0f, msPerFrame[i]);
                values.Add(v);
                sum += v;
                if (v > 0f) present++;
                if (callsPerFrame != null && i < callsPerFrame.Count) calls += Math.Max(0f, callsPerFrame[i]);
            }
            for (int i = have; i < frames; i++) values.Add(0f);
            values.Sort();

            stat.avgMs = (float)(sum / frames);
            stat.avgCalls = (float)(calls / frames);
            stat.presentPct = 100f * present / frames;
            stat.p50Ms = Percentile(values, 0.50f);
            stat.p95Ms = Percentile(values, 0.95f);
            stat.maxMs = values[frames - 1];
            return stat;
        }

        /// <summary>Report rows, biggest average first; markers that were not found go last.</summary>
        public static void SortForReport(List<MarkerStat> stats)
        {
            stats.Sort((a, b) =>
            {
                if (a.found != b.found) return a.found ? -1 : 1;
                int byAvg = b.avgMs.CompareTo(a.avgMs);
                return byAvg != 0 ? byAvg : string.CompareOrdinal(a.name, b.name);
            });
        }
    }
}
#endif
