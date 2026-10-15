using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CosmicShore.Engine.Profiling
{
    /// <summary>
    /// The engine's collector behind <see cref="ProfilerMarker"/>: what Unity's profiler does for a
    /// named marker, so the game's own instruments (<c>diag</c>'s <c>MarkerBudgetRecorder</c>, any
    /// <c>ProfilerRecorder</c> on a marker) read real numbers here instead of zeros.
    ///
    /// <para><b>Main (loop) thread only</b>, like the <c>CollectOnlyOnCurrentThread</c> recorders
    /// <c>diag</c> uses: a Begin/End from another thread is ignored. Jobs run synchronously on the
    /// loop thread in this engine (no Burst, no workers), so a job's marker counts here where in
    /// Unity it would be on a worker; compare those markers with that in mind.</para>
    ///
    /// <para>Per marker and per frame it sums <b>inclusive</b> time (every sample, nested ones
    /// included, as Unity's <c>SumAllSamplesInFrame</c>), the call count, and the bytes the loop
    /// thread allocated inside the scope. The allocation figure is this engine's extra: allocations
    /// come from the game's own IL, so it transfers to Unity far better than milliseconds do.</para>
    ///
    /// <para>Cost: two <see cref="Stopwatch.GetTimestamp"/> and two allocation-counter reads per
    /// sample, no allocation after a marker's first frame.</para>
    /// </summary>
    public static class MarkerCollector
    {
        /// <summary>Off with COSMIC_SHORE_MARKERS=off; a disabled collector makes every marker a no-op again.</summary>
        public static bool Enabled = !string.Equals(Environment.GetEnvironmentVariable("COSMIC_SHORE_MARKERS"), "off",
            StringComparison.OrdinalIgnoreCase);

        /// <summary>Thread whose samples count; the game loop sets it when built and every tick. Nothing is recorded while it is 0.</summary>
        public static int LoopThreadId;

        const int HistogramBuckets = 2001; // 0.01 ms buckets to 20 ms; the last bucket holds everything above
        static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;
        static readonly double TicksToNs = 1e9 / Stopwatch.Frequency;

        static readonly object s_lock = new();
        static readonly Dictionary<string, int> s_ids = new(StringComparer.Ordinal);
        static int s_count = 1; // id 0 is "no marker" (a default ProfilerMarker)

        // Registry, indexed by id. Grown under the lock; read on the loop thread.
        static string[] s_names = new string[256];
        static string[] s_categories = new string[256];
        static bool[] s_declared = new bool[256];

        // This frame.
        static long[] s_frameTicks = new long[256];
        static int[] s_frameCalls = new int[256];
        static long[] s_frameBytes = new long[256];
        static int[] s_touched = new int[256];
        static int s_touchedCount;

        // Since the last ResetTotals.
        static long[] s_totalTicks = new long[256];
        static long[] s_totalCalls = new long[256];
        static long[] s_totalBytes = new long[256];
        static long[] s_maxTicks = new long[256];
        static int[] s_activeFrames = new int[256];
        static int[][] s_histogram = new int[256][];
        static int s_totalFrames;

        static List<MarkerSampleBuffer>[] s_listeners = new List<MarkerSampleBuffer>[256];

        struct Open { public int id; public long ticks; public long bytes; }
        static Open[] s_stack = new Open[64];
        static int s_depth;

        static long s_frameStartBytes = -1;

        /// <summary>Bytes the loop thread allocated in the last closed frame (the "GC Allocated In Frame" counter).</summary>
        public static long LastFrameAllocatedBytes { get; private set; }

        /// <summary>Frames closed (by <see cref="EndFrame"/>) since the last <see cref="ResetTotals"/>.</summary>
        public static int TotalFrames => s_totalFrames;

        /// <summary>
        /// The id for <paramref name="name"/>, registering it if new. <paramref name="declared"/> marks a
        /// name a <see cref="ProfilerMarker"/> was built with (only those are enumerable, as in Unity);
        /// a recorder may register a name first, before the marker's type is initialised.
        /// </summary>
        public static int Register(string category, string name, bool declared)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            lock (s_lock)
            {
                if (!s_ids.TryGetValue(name, out int id))
                {
                    id = s_count++;
                    if (id >= s_names.Length) Grow(s_names.Length * 2);
                    s_ids.Add(name, id);
                    s_names[id] = name;
                    s_categories[id] = category ?? "Scripts";
                }
                if (declared && !s_declared[id])
                {
                    s_declared[id] = true;
                    if (category != null) s_categories[id] = category;
                }
                return id;
            }
        }

        /// <summary>The id already registered for <paramref name="name"/>, or 0.</summary>
        public static int Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            lock (s_lock) return s_ids.TryGetValue(name, out int id) ? id : 0;
        }

        public static string NameOf(int id) => id > 0 && id < s_count ? s_names[id] : null;
        public static string CategoryOf(int id) => id > 0 && id < s_count ? s_categories[id] : null;

        /// <summary>Every marker a <see cref="ProfilerMarker"/> declared, as ids (what <c>ProfilerRecorderHandle.GetAvailable</c> lists).</summary>
        public static void GetDeclared(List<int> ids)
        {
            ids.Clear();
            lock (s_lock)
                for (int i = 1; i < s_count; i++)
                    if (s_declared[i]) ids.Add(i);
        }

        static void Grow(int size)
        {
            Array.Resize(ref s_names, size);
            Array.Resize(ref s_categories, size);
            Array.Resize(ref s_declared, size);
            Array.Resize(ref s_frameTicks, size);
            Array.Resize(ref s_frameCalls, size);
            Array.Resize(ref s_frameBytes, size);
            Array.Resize(ref s_touched, size);
            Array.Resize(ref s_totalTicks, size);
            Array.Resize(ref s_totalCalls, size);
            Array.Resize(ref s_totalBytes, size);
            Array.Resize(ref s_maxTicks, size);
            Array.Resize(ref s_activeFrames, size);
            Array.Resize(ref s_histogram, size);
            Array.Resize(ref s_listeners, size);
        }

        static bool OnLoopThread()
        {
            int loop = LoopThreadId;
            return loop != 0 && Environment.CurrentManagedThreadId == loop;
        }

        public static void Begin(int id)
        {
            if (id <= 0 || !Enabled || !OnLoopThread()) return;
            if (s_depth == s_stack.Length) Array.Resize(ref s_stack, s_stack.Length * 2);
            s_stack[s_depth++] = new Open { id = id, bytes = GC.GetAllocatedBytesForCurrentThread(), ticks = Stopwatch.GetTimestamp() };
        }

        public static void End(int id)
        {
            if (id <= 0 || !Enabled || !OnLoopThread()) return;
            long now = Stopwatch.GetTimestamp();
            long bytes = GC.GetAllocatedBytesForCurrentThread();

            // Close the innermost open sample of this marker. A Begin with no End above it (a scope
            // left open across a yield, an exception without a using) is dropped, not charged.
            int at = s_depth - 1;
            while (at >= 0 && s_stack[at].id != id) at--;
            if (at < 0) return;
            var open = s_stack[at];
            s_depth = at;

            if (s_frameCalls[id] == 0) s_touched[s_touchedCount++] = id;
            s_frameTicks[id] += now - open.ticks;
            s_frameCalls[id]++;
            s_frameBytes[id] += bytes - open.bytes;
        }

        /// <summary>
        /// Closes the frame: folds each marker that ran into its totals and histogram, hands the
        /// frame's sample to every running recorder, and clears the frame. The game loop calls it at
        /// the end of every tick. Markers that did not run add no sample (Unity's recorders likewise).
        /// </summary>
        public static void EndFrame()
        {
            if (!Enabled) return;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            LastFrameAllocatedBytes = s_frameStartBytes < 0 ? 0 : allocated - s_frameStartBytes;
            s_frameStartBytes = allocated;
            s_totalFrames++;
            for (int i = 0; i < s_touchedCount; i++)
            {
                int id = s_touched[i];
                long ticks = s_frameTicks[id];
                int calls = s_frameCalls[id];

                s_totalTicks[id] += ticks;
                s_totalCalls[id] += calls;
                s_totalBytes[id] += s_frameBytes[id];
                s_activeFrames[id]++;
                if (ticks > s_maxTicks[id]) s_maxTicks[id] = ticks;
                var histogram = s_histogram[id] ??= new int[HistogramBuckets];
                histogram[Math.Min(HistogramBuckets - 1, (int)(ticks * TicksToMs * 100))]++;

                var listeners = s_listeners[id];
                if (listeners != null)
                {
                    long ns = (long)(ticks * TicksToNs);
                    for (int l = 0; l < listeners.Count; l++) listeners[l].Push(ns, calls);
                }

                s_frameTicks[id] = 0;
                s_frameCalls[id] = 0;
                s_frameBytes[id] = 0;
            }
            s_touchedCount = 0;
            // Nothing synchronous is open between ticks: a sample still open here was begun and never
            // ended in this frame (across a yield, or an exception without a using) and can't be charged.
            s_depth = 0;
        }

        /// <summary>Starts the session totals over (the session report does at its steady-state frame).</summary>
        public static void ResetTotals()
        {
            s_totalFrames = 0;
            for (int id = 1; id < s_count; id++)
            {
                s_totalTicks[id] = 0;
                s_totalCalls[id] = 0;
                s_totalBytes[id] = 0;
                s_maxTicks[id] = 0;
                s_activeFrames[id] = 0;
                if (s_histogram[id] != null) Array.Clear(s_histogram[id]);
            }
        }

        internal static void Listen(MarkerSampleBuffer buffer)
        {
            if (buffer.MarkerId <= 0) return;
            lock (s_lock)
            {
                var list = s_listeners[buffer.MarkerId] ??= new List<MarkerSampleBuffer>(2);
                if (!list.Contains(buffer)) list.Add(buffer);
            }
        }

        internal static void Unlisten(MarkerSampleBuffer buffer)
        {
            if (buffer.MarkerId <= 0) return;
            lock (s_lock) s_listeners[buffer.MarkerId]?.Remove(buffer);
        }

        /// <summary>One marker's figures since the last <see cref="ResetTotals"/>.</summary>
        public readonly struct Summary
        {
            public readonly string Name, Category;
            /// <summary>Milliseconds per frame over EVERY frame of the window (what the marker costs the frame budget).</summary>
            public readonly double AvgMsPerFrame;
            /// <summary>Percentiles and max over the frames the marker ran in.</summary>
            public readonly double P50Ms, P95Ms, MaxMs;
            public readonly double CallsPerFrame, KBPerFrame;
            public readonly int ActiveFrames, Frames;

            internal Summary(string name, string category, double avg, double p50, double p95, double max,
                double calls, double kb, int active, int frames)
            {
                Name = name; Category = category; AvgMsPerFrame = avg; P50Ms = p50; P95Ms = p95; MaxMs = max;
                CallsPerFrame = calls; KBPerFrame = kb; ActiveFrames = active; Frames = frames;
            }
        }

        /// <summary>Every marker that ran since the last <see cref="ResetTotals"/>, most expensive first.</summary>
        public static List<Summary> Summarize()
        {
            var list = new List<Summary>();
            int frames = Math.Max(1, s_totalFrames);
            for (int id = 1; id < s_count; id++)
            {
                if (s_activeFrames[id] == 0) continue;
                var histogram = s_histogram[id];
                list.Add(new Summary(s_names[id], s_categories[id],
                    s_totalTicks[id] * TicksToMs / frames,
                    Percentile(histogram, s_activeFrames[id], 0.50),
                    Percentile(histogram, s_activeFrames[id], 0.95),
                    s_maxTicks[id] * TicksToMs,
                    (double)s_totalCalls[id] / frames,
                    s_totalBytes[id] / 1024.0 / frames,
                    s_activeFrames[id], s_totalFrames));
            }
            list.Sort((a, b) => b.AvgMsPerFrame.CompareTo(a.AvgMsPerFrame));
            return list;
        }

        static double Percentile(int[] histogram, int total, double p)
        {
            if (histogram == null || total == 0) return 0;
            int want = (int)Math.Ceiling(total * p), seen = 0;
            for (int i = 0; i < histogram.Length; i++)
                if ((seen += histogram[i]) >= want) return i / 100.0;
            return (histogram.Length - 1) / 100.0;
        }
    }

    /// <summary>
    /// A recorder's per-frame samples of one marker: a ring of (nanoseconds, calls) the collector
    /// pushes at each frame's end while the recorder runs. Shared by every copy of the
    /// <c>ProfilerRecorder</c> struct that owns it, as Unity's native recorder handle is.
    /// </summary>
    public sealed class MarkerSampleBuffer
    {
        public readonly int MarkerId;
        readonly long[] _values;
        readonly long[] _counts;
        int _next, _count;

        public bool Running { get; private set; }
        public bool WrappedAround { get; private set; }
        public int Capacity => _values.Length;
        public int Count => _count;

        public MarkerSampleBuffer(int markerId, int capacity)
        {
            MarkerId = markerId;
            capacity = Math.Max(1, capacity);
            _values = new long[capacity];
            _counts = new long[capacity];
        }

        public void Start() { if (Running) return; Running = true; MarkerCollector.Listen(this); }
        public void Stop() { Running = false; MarkerCollector.Unlisten(this); }

        /// <summary>Unity's contract: clears the samples AND stops collection.</summary>
        public void Reset() { Stop(); _next = 0; _count = 0; WrappedAround = false; }

        internal void Push(long value, long calls)
        {
            if (!Running) return;
            _values[_next] = value;
            _counts[_next] = calls;
            _next = (_next + 1) % _values.Length;
            if (_count < _values.Length) _count++;
            else WrappedAround = true;
        }

        /// <summary>Sample <paramref name="index"/>, oldest first.</summary>
        public (long value, long count) Get(int index)
        {
            if (index < 0 || index >= _count) return (0, 0);
            int start = _count < _values.Length ? 0 : _next;
            int at = (start + index) % _values.Length;
            return (_values[at], _counts[at]);
        }

        /// <summary>The newest sample, or zeros before the first.</summary>
        public (long value, long count) Last => _count == 0 ? (0, 0) : Get(_count - 1);
    }
}
