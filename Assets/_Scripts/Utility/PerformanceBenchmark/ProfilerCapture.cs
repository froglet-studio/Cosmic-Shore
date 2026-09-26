#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The pure half of the <c>prof</c> console command: parse the request, fold the Profiler's
    /// per-frame main-thread hierarchy into per-frame AVERAGES keyed by call path, and build a
    /// report that answers what the four Profiler screenshots per scenario used to answer:
    ///
    /// <list type="bullet">
    /// <item><c>tree</c> — the Hierarchy view, opened to the leaves, averaged over N frames rather
    /// than read off one frame somebody happened to click. Pruned by <c>min</c> (ms) and
    /// <c>mingc</c> (KB), sorted by <c>sort</c>, optionally started at <c>root</c>.</item>
    /// <item><c>topSelf</c> — the Inverted Hierarchy: which markers cost the most of their OWN
    /// time, summed across every path they appear on.</item>
    /// <item><c>topGc</c> — the GC Alloc sort, by SELF allocation. The Profiler's GC column is
    /// INCLUSIVE, so ranking it raw puts <c>PlayerLoop</c> first every time; self allocation
    /// (a node's minus its children's) is what names the allocator.</item>
    /// <item><c>typicalFrameTree</c> / <c>spikeFrameTree</c> — the frame nearest the median and
    /// the slowest frame, each as its own tree, so a spike can be told apart from the
    /// steady state without anyone scrubbing the graph for a tall bar.</item>
    /// <item><c>threads</c> — the Timeline's question in numbers: how busy each worker and the
    /// render thread were, versus idle or waiting.</item>
    /// </list>
    ///
    /// The Unity-bound reader (<see cref="ProfilerFrameReader"/>, Editor only) walks the frames
    /// and feeds <see cref="Accumulator"/> / <see cref="ThreadAccumulator"/>; everything that
    /// decides what the report says lives here, where it can be tested without a Profiler.
    ///
    /// The Profiler's own recording overhead inflates absolute times, so a <c>prof</c> report is
    /// for ATTRIBUTION (which rows are big, relative to each other); <c>diag</c> and <c>ab</c>
    /// remain the numbers to quote.
    /// </summary>
    public static class ProfilerCapture
    {
        public const string CommandName = "prof";

        public const int DefaultFrames = 180, MinFrames = 10, MaxFrames = 600;
        public const float DefaultMinMs = 0.05f;
        public const float DefaultMinGcKB = 1f;
        public const int DefaultDepth = 12, MaxDepth = 64;
        public const int DefaultTop = 30, MaxTop = 200;

        /// <summary>
        /// Threads other than the main thread are read on every Nth captured frame only:
        /// opening a view per thread per frame is most of the cost of a capture, and a
        /// per-thread busy/idle AVERAGE does not need every frame.
        /// </summary>
        public const int ThreadSampleStride = 6;

        /// <summary>
        /// The reader does not descend into a node smaller than this that allocates nothing.
        /// Safe for every total the report prints: a child can never exceed its parent's
        /// total time or its parent's (inclusive) allocation.
        /// </summary>
        public const float WalkFloorMs = 0.002f;

        public const string PathSeparator = " / ";
        public const string PlayerLoopName = "PlayerLoop";

        public enum SortKey { Total = 0, Self = 1, Gc = 2, Calls = 3 }

        // ── request ─────────────────────────────────────────────────────────

        [Serializable]
        public class Options
        {
            public string label = "";
            public int frames = DefaultFrames;
            public string root = "";
            public string sort = "total";
            public float minMs = DefaultMinMs;
            public float minGcKB = DefaultMinGcKB;
            public int depth = DefaultDepth;
            public int top = DefaultTop;
            [NonSerialized] public SortKey sortKey = SortKey.Total;
        }

        public static string Usage =>
            "usage: prof [label] [frames] [root=<name>] [sort=total|self|gc|calls] [min=<ms>] " +
            "[mingc=<KB>] [depth=<n>] [top=<n>]   e.g. prof S2_Lattice 180 root=UpdateScene sort=self   (prof stop cancels)";

        /// <summary>
        /// Parses the tokens DiagnosticsHUD hands a handler. One free word is the label, one
        /// integer is the frame count, and everything else is <c>key=value</c>. Order is free.
        /// </summary>
        public static bool TryParse(string[] args, out Options options, out string error)
        {
            options = new Options();
            error = null;
            bool haveLabel = false, haveFrames = false;
            if (args == null) return true;

            foreach (string raw in args)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                string a = raw.Trim();

                int eq = a.IndexOf('=');
                if (eq > 0)
                {
                    string key = a.Substring(0, eq).ToLowerInvariant();
                    string value = a.Substring(eq + 1);
                    if (value.Length == 0) { error = $"'{key}=' needs a value - {Usage}"; return false; }

                    switch (key)
                    {
                        case "root":
                            options.root = value;
                            break;
                        case "sort":
                            if (!TryParseSort(value, out options.sortKey))
                            { error = $"sort must be total, self, gc or calls - {Usage}"; return false; }
                            options.sort = options.sortKey.ToString().ToLowerInvariant();
                            break;
                        case "min":
                            if (!TryParseNonNegative(value, out options.minMs))
                            { error = $"min must be a number of ms >= 0 - {Usage}"; return false; }
                            break;
                        case "mingc":
                            if (!TryParseNonNegative(value, out options.minGcKB))
                            { error = $"mingc must be a number of KB >= 0 - {Usage}"; return false; }
                            break;
                        case "depth":
                            if (!TryParsePositive(value, out int depth))
                            { error = $"depth must be a whole number >= 1 - {Usage}"; return false; }
                            options.depth = Math.Min(depth, MaxDepth);
                            break;
                        case "top":
                            if (!TryParsePositive(value, out int top))
                            { error = $"top must be a whole number >= 1 - {Usage}"; return false; }
                            options.top = Math.Min(top, MaxTop);
                            break;
                        default:
                            error = $"unknown option '{key}' - {Usage}";
                            return false;
                    }
                    continue;
                }

                if (int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out int frames))
                {
                    if (haveFrames) { error = $"two frame counts given - {Usage}"; return false; }
                    if (frames < 1) { error = $"frames must be positive - {Usage}"; return false; }
                    options.frames = Math.Max(MinFrames, Math.Min(MaxFrames, frames));
                    haveFrames = true;
                    continue;
                }

                if (haveLabel) { error = $"'{a}' is neither key=value nor a frame count - {Usage}"; return false; }
                options.label = a;
                haveLabel = true;
            }
            return true;
        }

        static bool TryParseSort(string s, out SortKey key)
        {
            switch (s.ToLowerInvariant())
            {
                case "total": key = SortKey.Total; return true;
                case "self":  key = SortKey.Self;  return true;
                case "gc":    key = SortKey.Gc;    return true;
                case "calls": key = SortKey.Calls; return true;
                default:      key = SortKey.Total; return false;
            }
        }

        static bool TryParseNonNegative(string s, out float value) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        static bool TryParsePositive(string s, out int value) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 1;

        // ── paths ───────────────────────────────────────────────────────────

        public static string Join(string parentPath, string name) =>
            string.IsNullOrEmpty(parentPath) ? name : parentPath + PathSeparator + name;

        /// <summary>The node a path hangs off: for a <c>GC.Alloc</c> row, the code that allocated.</summary>
        public static string OwnerOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int last = path.LastIndexOf(PathSeparator, StringComparison.Ordinal);
            if (last < 0) return "";
            string parent = path.Substring(0, last);
            int prev = parent.LastIndexOf(PathSeparator, StringComparison.Ordinal);
            return prev < 0 ? parent : parent.Substring(prev + PathSeparator.Length);
        }

        /// <summary>Time or allocation the Editor spends on itself, which a player build does not.</summary>
        public static bool IsEditorOnly(string name) =>
            name == "EditorLoop" || (name != null && name.StartsWith("EditorOnly", StringComparison.Ordinal));

        // ── accumulation ────────────────────────────────────────────────────

        /// <summary>
        /// Folds N frames of the main thread into per-path sums. Paths must arrive parent-first
        /// (a pre-order walk), which is what makes a child's parent resolvable on arrival.
        /// Averages are PER CAPTURED FRAME, including frames a path did not appear in — a
        /// marker that runs once every ten frames at 5 ms costs 0.5 ms a frame, and that is
        /// the number that competes with everything else for the frame budget.
        /// </summary>
        public sealed class Accumulator
        {
            internal sealed class Node
            {
                public string path, parentPath, name;
                public int depth;
                public double sumTotal, sumSelf, sumGc, sumCalls;
                public float maxTotal;
                public int present;
                public Node parent;
                public readonly List<Node> children = new();
            }

            readonly Dictionary<string, Node> _byPath = new();
            internal readonly List<Node> Roots = new();

            public readonly List<int> FrameIndices = new();
            public readonly List<float> FrameMs = new();
            /// <summary>The top-level <c>PlayerLoop</c> total per frame (0 where it was absent).</summary>
            public readonly List<float> PlayerLoopMs = new();
            public int FrameCount => FrameMs.Count;
            public int NodeCount => _byPath.Count;

            public void BeginFrame(int frameIndex, float frameMs)
            {
                FrameIndices.Add(frameIndex);
                FrameMs.Add(frameMs);
                PlayerLoopMs.Add(0f);
            }

            /// <summary>True when any captured frame had a top-level <c>PlayerLoop</c>.</summary>
            public bool HasPlayerLoop
            {
                get
                {
                    foreach (float v in PlayerLoopMs) if (v > 0f) return true;
                    return false;
                }
            }

            /// <summary>
            /// The series the typical and spike frames are picked from: the game's own
            /// <c>PlayerLoop</c> when the capture has one, else the whole frame. In the Editor the
            /// slowest WHOLE frame is regularly an Editor repaint (S1's 104 ms frame was 86 ms of
            /// <c>EditorLoop</c>), which says nothing about the game.
            /// </summary>
            public IReadOnlyList<float> SelectionSeries => HasPlayerLoop ? PlayerLoopMs : FrameMs;

            public void AddNode(string path, string parentPath, string name, int depth,
                                float totalMs, float selfMs, float gcBytes, float calls)
            {
                if (!_byPath.TryGetValue(path, out var n))
                {
                    n = new Node { path = path, parentPath = parentPath, name = name, depth = depth };
                    _byPath[path] = n;
                    if (!string.IsNullOrEmpty(parentPath) && _byPath.TryGetValue(parentPath, out var p))
                    {
                        n.parent = p;
                        p.children.Add(n);
                    }
                    else
                    {
                        Roots.Add(n);
                    }
                }
                if (depth == 0 && name == PlayerLoopName && PlayerLoopMs.Count > 0)
                    PlayerLoopMs[PlayerLoopMs.Count - 1] = totalMs;

                n.sumTotal += totalMs;
                n.sumSelf += selfMs;
                n.sumGc += gcBytes;
                n.sumCalls += calls;
                if (totalMs > n.maxTotal) n.maxTotal = totalMs;
                n.present++;
            }
        }

        public readonly struct ThreadItemSample
        {
            public readonly string Name;
            public readonly float Ms;
            public ThreadItemSample(string name, float ms) { Name = name; Ms = ms; }
        }

        /// <summary>
        /// Per-thread busy vs waiting, from each thread's TOP-LEVEL samples. A top-level sample
        /// named <c>Idle</c> or containing <c>WaitFor</c> / <c>Semaphore.Wait</c> is waiting;
        /// everything else is work. Only top level: a wait nested inside a render pass is part of
        /// that pass, and the pass itself is still what the thread was doing.
        /// </summary>
        public sealed class ThreadAccumulator
        {
            internal sealed class Thread
            {
                public string name, group;
                public int samples;
                public double busy, wait;
                public readonly Dictionary<string, double> items = new();
            }

            readonly Dictionary<string, Thread> _byKey = new();
            internal readonly List<Thread> Order = new();
            public int SampledFrames { get; private set; }

            public void BeginFrame() => SampledFrames++;

            public void AddThread(string name, string group, IReadOnlyList<ThreadItemSample> topLevel)
            {
                if (topLevel == null || topLevel.Count == 0) return;
                string key = (group ?? "") + "/" + (name ?? "");
                if (!_byKey.TryGetValue(key, out var t))
                {
                    t = new Thread { name = name ?? "", group = group ?? "" };
                    _byKey[key] = t;
                    Order.Add(t);
                }
                t.samples++;
                for (int i = 0; i < topLevel.Count; i++)
                {
                    var item = topLevel[i];
                    if (IsWait(item.Name)) t.wait += item.Ms;
                    else t.busy += item.Ms;
                    t.items.TryGetValue(item.Name ?? "", out double sum);
                    t.items[item.Name ?? ""] = sum + item.Ms;
                }
            }
        }

        /// <summary>
        /// True for a top-level thread sample that is the thread BLOCKED rather than working.
        ///
        /// <para><c>GfxTask_ReadValue</c> is the D3D12 task worker waiting for its next
        /// command. It carries no "Wait" in its name, and before it was listed here that
        /// thread reported ~100% busy in every capture - at 7.5 ms in a light scene and 71 ms
        /// in a heavy one, i.e. exactly the frame time, which work cannot be. The sibling
        /// <c>GfxTask_Execute</c> is real work and stays busy.</para>
        /// </summary>
        public static bool IsWait(string name) =>
            name != null &&
            (name.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
             name.IndexOf("WaitFor", StringComparison.Ordinal) >= 0 ||
             name.StartsWith("Semaphore.Wait", StringComparison.Ordinal) ||
             name.Equals("GfxTask_ReadValue", StringComparison.Ordinal));

        /// <summary>One frame's main thread as a tree (for the typical and spike frames).</summary>
        public sealed class FrameNode
        {
            public string name;
            public float totalMs, selfMs, gcBytes, calls;
            public readonly List<FrameNode> children = new();
        }

        // ── report ──────────────────────────────────────────────────────────

        [Serializable]
        public class Row
        {
            public int depth;
            public string name;
            public string path;
            public float avgTotalMs, avgSelfMs, maxTotalMs, avgGcKB, avgSelfGcKB, avgCalls, presentPct;
            public bool editorOnly;
        }

        [Serializable]
        public class NameRow
        {
            public string name;
            public float avgSelfMs, avgSelfGcKB, avgCalls;
            public bool editorOnly;
        }

        [Serializable]
        public class FrameRow
        {
            public int depth;
            public string name;
            public float totalMs, selfMs, gcKB, calls;
        }

        [Serializable]
        public class ThreadItem
        {
            public string name;
            public float avgMs;
        }

        [Serializable]
        public class ThreadRow
        {
            public string name, group;
            public int samples;
            public float avgBusyMs, avgWaitMs, busyPct;
            public List<ThreadItem> top = new();
        }

        [Serializable]
        public class FrameStats
        {
            public float avgMs, p50Ms, p99Ms, minMs, maxMs;
        }

        [Serializable]
        public class Report
        {
            public string scene, timestamp, label;
            public Options options = new();
            public int framesRequested, framesRead, firstFrame, lastFrame;
            public bool completed, deepProfiling, profilerWasRecording;
            public FrameStats mainThreadFrameMs = new();
            public FrameStats playerLoopMs = new();
            /// <summary>"PlayerLoop" or "frame": which series the typical and spike frames were picked from.</summary>
            public string framesPickedBy = "";
            public int typicalFrame = -1, spikeFrame = -1;
            public float typicalFrameMs, spikeFrameMs;
            public List<string> rootsMatched = new();
            public List<Row> tree = new();
            public List<NameRow> topSelf = new();
            public List<Row> topGc = new();
            public List<FrameRow> typicalFrameTree = new();
            public List<FrameRow> spikeFrameTree = new();
            public int threadSampleStride = ThreadSampleStride, threadSampledFrames;
            public List<ThreadRow> threads = new();
            public List<string> notes = new();
        }

        /// <summary>
        /// Positions (not frame indices) of the frame nearest the median and the slowest frame.
        /// Ties break to the EARLIER frame so a report is deterministic. -1 when empty.
        /// </summary>
        public static void PickTypicalAndSpike(IReadOnlyList<float> frameMs, out int typicalPos, out int spikePos)
        {
            typicalPos = spikePos = -1;
            if (frameMs == null || frameMs.Count == 0) return;

            float median = Percentile(frameMs, 0.5f);
            float bestDist = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < frameMs.Count; i++)
            {
                float d = Math.Abs(frameMs[i] - median);
                if (d < bestDist) { bestDist = d; typicalPos = i; }
                if (frameMs[i] > max) { max = frameMs[i]; spikePos = i; }
            }
        }

        /// <summary>Nearest-rank percentile, the rule diag uses. 0 when empty.</summary>
        public static float Percentile(IReadOnlyList<float> values, float p)
        {
            if (values == null || values.Count == 0) return 0f;
            var sorted = new List<float>(values);
            sorted.Sort();
            int i = (int)Math.Round(Math.Max(0f, Math.Min(1f, p)) * (sorted.Count - 1), MidpointRounding.AwayFromZero);
            return sorted[i];
        }

        /// <summary>Everything the report says, from the accumulated frames.</summary>
        public static void Build(Report report, Accumulator acc, ThreadAccumulator threads,
                                 FrameNode typicalTree, FrameNode spikeTree, Options options)
        {
            report.options = options;
            report.framesRead = acc.FrameCount;

            if (acc.FrameCount > 0)
            {
                report.mainThreadFrameMs = Stats(acc.FrameMs);
                if (acc.HasPlayerLoop) report.playerLoopMs = Stats(acc.PlayerLoopMs);
                report.framesPickedBy = acc.HasPlayerLoop ? PlayerLoopName : "frame";
                report.firstFrame = acc.FrameIndices[0];
                report.lastFrame = acc.FrameIndices[acc.FrameIndices.Count - 1];
            }

            var roots = FindRoots(acc, options.root);
            roots.Sort((a, b) =>
            {
                int c = MergedKey(b, options.sortKey).CompareTo(MergedKey(a, options.sortKey));
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
            foreach (var r in roots) report.rootsMatched.Add(r.path);
            if (!string.IsNullOrEmpty(options.root) && roots.Count == 0)
                report.notes.Add($"root '{options.root}' matched nothing on the main thread - the tree is empty");

            int frames = Math.Max(1, acc.FrameCount);
            foreach (var r in roots) EmitMerged(r, 0, frames, options, report.tree);

            BuildTopSelf(roots, frames, options, report.topSelf);
            BuildTopGc(roots, frames, options, report.topGc);

            if (typicalTree != null) EmitFrame(typicalTree, options, report.typicalFrameTree);
            if (spikeTree != null) EmitFrame(spikeTree, options, report.spikeFrameTree);

            if (threads != null)
            {
                report.threadSampledFrames = threads.SampledFrames;
                BuildThreads(threads, report.threads);
            }

            if (report.deepProfiling)
                report.notes.Add("Deep Profile was ON: every script call is instrumented and times are several times too large. Turn it off and capture again.");
            report.notes.Add("Times include the Profiler's own recording overhead - use this report to RANK rows, and diag/ab for the numbers to quote.");
            report.notes.Add("Averages are per captured frame, including frames a row did not appear in (presentPct says how often it did).");
        }

        static FrameStats Stats(IReadOnlyList<float> values)
        {
            double sum = 0; float min = float.MaxValue, max = float.MinValue;
            foreach (float ms in values) { sum += ms; if (ms < min) min = ms; if (ms > max) max = ms; }
            return new FrameStats
            {
                avgMs = (float)(sum / values.Count),
                p50Ms = Percentile(values, 0.5f),
                p99Ms = Percentile(values, 0.99f),
                minMs = min,
                maxMs = max,
            };
        }

        static List<Accumulator.Node> FindRoots(Accumulator acc, string root)
        {
            var result = new List<Accumulator.Node>();
            if (string.IsNullOrEmpty(root)) { result.AddRange(acc.Roots); return result; }

            var stack = new Stack<Accumulator.Node>();
            for (int i = acc.Roots.Count - 1; i >= 0; i--) stack.Push(acc.Roots[i]);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n.name != null && n.name.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(n); // the highest match on this branch; its subtree is included whole
                    continue;
                }
                for (int i = n.children.Count - 1; i >= 0; i--) stack.Push(n.children[i]);
            }
            return result;
        }

        static double SelfGcSum(Accumulator.Node n)
        {
            double children = 0;
            foreach (var c in n.children) children += c.sumGc;
            return Math.Max(0, n.sumGc - children);
        }

        static bool Passes(float totalMs, float gcKB, Options o) =>
            totalMs >= o.minMs || (gcKB > 0f && gcKB >= o.minGcKB);

        static double MergedKey(Accumulator.Node n, SortKey key) => key switch
        {
            SortKey.Self => n.sumSelf,
            SortKey.Gc => n.sumGc,
            SortKey.Calls => n.sumCalls,
            _ => n.sumTotal,
        };

        static void EmitMerged(Accumulator.Node n, int relDepth, int frames, Options o, List<Row> into)
        {
            float avgTotal = (float)(n.sumTotal / frames);
            float avgGcKB = (float)(n.sumGc / frames / 1024.0);
            // Both criteria are inclusive, so a node that fails cannot have a child that passes:
            // pruning here never hides anything the thresholds would have shown.
            if (!Passes(avgTotal, avgGcKB, o)) return;

            into.Add(new Row
            {
                depth = relDepth,
                name = n.name,
                path = n.path,
                avgTotalMs = avgTotal,
                avgSelfMs = (float)(n.sumSelf / frames),
                maxTotalMs = n.maxTotal,
                avgGcKB = avgGcKB,
                avgSelfGcKB = (float)(SelfGcSum(n) / frames / 1024.0),
                avgCalls = (float)(n.sumCalls / frames),
                presentPct = 100f * n.present / frames,
                editorOnly = IsEditorOnly(n.name),
            });

            if (relDepth >= o.depth) return;

            var kids = new List<Accumulator.Node>(n.children);
            kids.Sort((a, b) =>
            {
                int c = MergedKey(b, o.sortKey).CompareTo(MergedKey(a, o.sortKey));
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
            foreach (var c in kids) EmitMerged(c, relDepth + 1, frames, o, into);
        }

        static void Descendants(Accumulator.Node n, List<Accumulator.Node> into)
        {
            into.Add(n);
            foreach (var c in n.children) Descendants(c, into);
        }

        static void BuildTopSelf(List<Accumulator.Node> roots, int frames, Options o, List<NameRow> into)
        {
            var all = new List<Accumulator.Node>();
            foreach (var r in roots) Descendants(r, all);

            var byName = new Dictionary<string, (double self, double gc, double calls)>();
            var order = new List<string>();
            foreach (var n in all)
            {
                string key = n.name ?? "";
                if (!byName.TryGetValue(key, out var v)) { v = (0, 0, 0); order.Add(key); }
                byName[key] = (v.self + n.sumSelf, v.gc + SelfGcSum(n), v.calls + n.sumCalls);
            }

            var rows = new List<NameRow>();
            foreach (string name in order)
            {
                var v = byName[name];
                if (v.self <= 0) continue;
                rows.Add(new NameRow
                {
                    name = name,
                    avgSelfMs = (float)(v.self / frames),
                    avgSelfGcKB = (float)(v.gc / frames / 1024.0),
                    avgCalls = (float)(v.calls / frames),
                    editorOnly = IsEditorOnly(name),
                });
            }
            rows.Sort((a, b) =>
            {
                int c = b.avgSelfMs.CompareTo(a.avgSelfMs);
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
            for (int i = 0; i < rows.Count && i < o.top; i++) into.Add(rows[i]);
        }

        static void BuildTopGc(List<Accumulator.Node> roots, int frames, Options o, List<Row> into)
        {
            var all = new List<Accumulator.Node>();
            foreach (var r in roots) Descendants(r, all);

            var rows = new List<Row>();
            foreach (var n in all)
            {
                double self = SelfGcSum(n);
                if (self <= 0) continue;
                rows.Add(new Row
                {
                    depth = n.depth,
                    name = n.name,
                    path = n.path,
                    avgTotalMs = (float)(n.sumTotal / frames),
                    avgSelfMs = (float)(n.sumSelf / frames),
                    maxTotalMs = n.maxTotal,
                    avgGcKB = (float)(n.sumGc / frames / 1024.0),
                    avgSelfGcKB = (float)(self / frames / 1024.0),
                    avgCalls = (float)(n.sumCalls / frames),
                    presentPct = 100f * n.present / frames,
                    editorOnly = IsEditorOnly(n.name) || IsEditorOnly(OwnerOf(n.path)),
                });
            }
            rows.Sort((a, b) =>
            {
                int c = b.avgSelfGcKB.CompareTo(a.avgSelfGcKB);
                return c != 0 ? c : string.CompareOrdinal(a.path, b.path);
            });
            for (int i = 0; i < rows.Count && i < o.top; i++) into.Add(rows[i]);
        }

        static float FrameKey(FrameNode n, SortKey key) => key switch
        {
            SortKey.Self => n.selfMs,
            SortKey.Gc => n.gcBytes,
            SortKey.Calls => n.calls,
            _ => n.totalMs,
        };

        /// <summary>A single frame's tree, rooted at <c>root</c> like the merged tree.</summary>
        static void EmitFrame(FrameNode syntheticRoot, Options o, List<FrameRow> into)
        {
            var starts = new List<FrameNode>();
            if (string.IsNullOrEmpty(o.root)) starts.AddRange(syntheticRoot.children);
            else FindFrameRoots(syntheticRoot, o.root, starts);

            foreach (var s in starts) EmitFrameNode(s, 0, o, into);
        }

        static void FindFrameRoots(FrameNode n, string root, List<FrameNode> into)
        {
            foreach (var c in n.children)
            {
                if (c.name != null && c.name.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0) into.Add(c);
                else FindFrameRoots(c, root, into);
            }
        }

        static void EmitFrameNode(FrameNode n, int relDepth, Options o, List<FrameRow> into)
        {
            float gcKB = n.gcBytes / 1024f;
            if (!Passes(n.totalMs, gcKB, o)) return;
            into.Add(new FrameRow { depth = relDepth, name = n.name, totalMs = n.totalMs, selfMs = n.selfMs, gcKB = gcKB, calls = n.calls });
            if (relDepth >= o.depth) return;

            var kids = new List<FrameNode>(n.children);
            kids.Sort((a, b) =>
            {
                int c = FrameKey(b, o.sortKey).CompareTo(FrameKey(a, o.sortKey));
                return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
            });
            foreach (var c in kids) EmitFrameNode(c, relDepth + 1, o, into);
        }

        const int ThreadTopItems = 5;

        static void BuildThreads(ThreadAccumulator acc, List<ThreadRow> into)
        {
            int frames = Math.Max(1, acc.SampledFrames);
            foreach (var t in acc.Order)
            {
                float busy = (float)(t.busy / frames), wait = (float)(t.wait / frames);
                var row = new ThreadRow
                {
                    name = t.name,
                    group = t.group,
                    samples = t.samples,
                    avgBusyMs = busy,
                    avgWaitMs = wait,
                    busyPct = busy + wait > 0f ? 100f * busy / (busy + wait) : 0f,
                };
                var items = new List<KeyValuePair<string, double>>(t.items);
                items.Sort((a, b) =>
                {
                    int c = b.Value.CompareTo(a.Value);
                    return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
                });
                for (int i = 0; i < items.Count && i < ThreadTopItems; i++)
                    row.top.Add(new ThreadItem { name = items[i].Key, avgMs = (float)(items[i].Value / frames) });
                into.Add(row);
            }
            // Busiest first: the main-thread question is always "who else was working".
            into.Sort((a, b) =>
            {
                int c = b.avgBusyMs.CompareTo(a.avgBusyMs);
                return c != 0 ? c : string.CompareOrdinal(a.group + a.name, b.group + b.name);
            });
        }

        // ── output ──────────────────────────────────────────────────────────

        static string F(float v, string fmt = "F2") => v.ToString(fmt, CultureInfo.InvariantCulture);

        /// <summary>The one console line.</summary>
        public static string Summarize(Report r, string savedPath = null)
        {
            var sb = new StringBuilder();
            sb.Append($"prof: {r.framesRead} frames");
            if (!string.IsNullOrEmpty(r.options?.root)) sb.Append($" under '{r.options.root}'");
            sb.Append($" - main thread {F(r.mainThreadFrameMs.avgMs, "F1")} ms avg (p99 {F(r.mainThreadFrameMs.p99Ms, "F1")}, max {F(r.mainThreadFrameMs.maxMs, "F1")})");
            if (r.framesPickedBy == PlayerLoopName)
                sb.Append($", PlayerLoop {F(r.playerLoopMs.avgMs, "F1")} (max {F(r.playerLoopMs.maxMs, "F1")})");

            int shown = 0;
            foreach (var s in r.topSelf)
            {
                if (s.editorOnly) continue;
                sb.Append(shown == 0 ? " - top self: " : ", ");
                sb.Append($"{s.name} {F(s.avgSelfMs)}");
                if (++shown == 3) break;
            }
            foreach (var g in r.topGc)
            {
                if (g.editorOnly) continue;
                string owner = g.name == "GC.Alloc" ? OwnerOf(g.path) : g.name;
                sb.Append($" - top alloc: {owner} {F(g.avgSelfGcKB, "F1")} KB/f");
                break;
            }
            if (!r.completed) sb.Append(" [INCOMPLETE]");
            if (r.deepProfiling) sb.Append(" [DEEP PROFILE ON - times inflated]");
            if (!string.IsNullOrEmpty(savedPath)) sb.Append($" - saved {savedPath}");
            return sb.ToString();
        }

        const int TextTreeRows = 120;

        /// <summary>The readable .txt beside the .json.</summary>
        public static string BuildText(Report r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"prof - {r.scene} - {r.timestamp}" + (string.IsNullOrEmpty(r.label) ? "" : $" - {r.label}"));
            var o = r.options ?? new Options();
            sb.AppendLine($"frames {r.framesRead}/{r.framesRequested} (profiler frames {r.firstFrame}..{r.lastFrame})  " +
                          $"root='{o.root}' sort={o.sort} min={F(o.minMs)}ms mingc={F(o.minGcKB)}KB depth={o.depth} top={o.top}" +
                          (r.completed ? "" : "  [INCOMPLETE]"));
            var s = r.mainThreadFrameMs;
            sb.AppendLine($"main thread ms: avg {F(s.avgMs)}  p50 {F(s.p50Ms)}  p99 {F(s.p99Ms)}  min {F(s.minMs)}  max {F(s.maxMs)}");
            var pl = r.playerLoopMs;
            if (r.framesPickedBy == PlayerLoopName)
                sb.AppendLine($"PlayerLoop ms:  avg {F(pl.avgMs)}  p50 {F(pl.p50Ms)}  p99 {F(pl.p99Ms)}  min {F(pl.minMs)}  max {F(pl.maxMs)}");
            sb.AppendLine($"typical frame {r.typicalFrame} ({F(r.typicalFrameMs)} ms) - spike frame {r.spikeFrame} ({F(r.spikeFrameMs)} ms) - picked by {r.framesPickedBy}");
            foreach (string note in r.notes) sb.AppendLine("note: " + note);

            sb.AppendLine();
            sb.AppendLine("== TOP SELF TIME (ms/frame, summed across paths) ==");
            foreach (var t in r.topSelf)
                sb.AppendLine($"  {F(t.avgSelfMs),8}  {t.name}  (calls {F(t.avgCalls, "F1")}, self alloc {F(t.avgSelfGcKB)} KB){(t.editorOnly ? "  [editor]" : "")}");

            sb.AppendLine();
            sb.AppendLine("== TOP SELF ALLOCATION (KB/frame) ==");
            foreach (var g in r.topGc)
                sb.AppendLine($"  {F(g.avgSelfGcKB),8}  {(g.name == "GC.Alloc" ? OwnerOf(g.path) : g.name)}  <-  {g.path}{(g.editorOnly ? "  [editor]" : "")}");

            sb.AppendLine();
            sb.AppendLine("== THREADS (sampled every " + r.threadSampleStride + " frames; busy vs top-level Idle/WaitFor) ==");
            foreach (var t in r.threads)
            {
                sb.Append($"  {t.group}/{t.name}: busy {F(t.avgBusyMs)} ms, waiting {F(t.avgWaitMs)} ms ({F(t.busyPct, "F0")}% busy) -");
                foreach (var it in t.top) sb.Append($" {it.name} {F(it.avgMs)};");
                sb.AppendLine();
            }

            AppendTree(sb, "== MERGED TREE (avg ms/frame: total | self | alloc KB | calls | present%) ==", r.tree);
            AppendFrame(sb, $"== TYPICAL FRAME {r.typicalFrame} ({F(r.typicalFrameMs)} ms: total | self | alloc KB | calls) ==", r.typicalFrameTree);
            AppendFrame(sb, $"== SPIKE FRAME {r.spikeFrame} ({F(r.spikeFrameMs)} ms: total | self | alloc KB | calls) ==", r.spikeFrameTree);
            return sb.ToString();
        }

        static void AppendTree(StringBuilder sb, string title, List<Row> rows)
        {
            sb.AppendLine();
            sb.AppendLine(title);
            for (int i = 0; i < rows.Count && i < TextTreeRows; i++)
            {
                var x = rows[i];
                sb.AppendLine($"  {new string(' ', 2 * x.depth)}{x.name}  {F(x.avgTotalMs)} | {F(x.avgSelfMs)} | {F(x.avgGcKB)} | {F(x.avgCalls, "F1")} | {F(x.presentPct, "F0")}%");
            }
            if (rows.Count > TextTreeRows) sb.AppendLine($"  ... {rows.Count - TextTreeRows} more rows in the .json");
        }

        static void AppendFrame(StringBuilder sb, string title, List<FrameRow> rows)
        {
            sb.AppendLine();
            sb.AppendLine(title);
            for (int i = 0; i < rows.Count && i < TextTreeRows; i++)
            {
                var x = rows[i];
                sb.AppendLine($"  {new string(' ', 2 * x.depth)}{x.name}  {F(x.totalMs)} | {F(x.selfMs)} | {F(x.gcKB)} | {F(x.calls, "F0")}");
            }
            if (rows.Count > TextTreeRows) sb.AppendLine($"  ... {rows.Count - TextTreeRows} more rows in the .json");
        }
    }
}
#endif
