using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Disposable handle returned by <see cref="LoadInsights.Measure"/> for `using`-scoped spans.
    /// A struct so the instrumentation is allocation-free.
    /// </summary>
    public readonly struct LoadSpanScope : IDisposable
    {
        /// <summary>
        /// Inert scope for gameplay-hot call sites that guard on <see cref="LoadInsights.IsRecording"/>
        /// before building a span label — so the disarmed path allocates nothing:
        /// <c>using var _ = LoadInsights.IsRecording ? LoadInsights.Measure(...) : LoadSpanScope.None;</c>
        /// </summary>
        public static readonly LoadSpanScope None = new(-1);

        readonly int _handle;
        internal LoadSpanScope(int handle) { _handle = handle; }
        public void Dispose() => LoadInsights.End(_handle);
    }

    /// <summary>
    /// Load-time span recorder — answers "what actually took the load time?" for one game launch.
    ///
    /// Mirrors the <see cref="NetMarkers"/> placement model: gameplay/pipeline code calls the
    /// static API at load-bearing points; the tool reads the result. Recording only happens when
    /// the user armed <b>Record Insight Mode</b> (Performance Benchmark window → Load Time
    /// Insights tab, persisted via PlayerPrefs) AND the <see cref="LoadInsightsRuntime"/> host is
    /// present (editor + development builds). Otherwise every call is a single bool check — safe
    /// to leave in shipping code, matching the NetMarkers contract.
    ///
    /// A recording runs from <see cref="BeginLoad"/> (game launch) to <see cref="CompleteLoad"/>
    /// (first playable moment — turn started). Spans may nest and overlap across async flows; at
    /// completion every millisecond of wall-clock is attributed to exactly ONE span (innermost
    /// active wins) so category percentages sum to 100. The finished
    /// <see cref="LoadInsightReport"/> is auto-saved as JSON + readable .txt under
    /// <c>persistentDataPath/Benchmarks/LoadInsights/</c>.
    /// </summary>
    public static class LoadInsights
    {
        public const string OutputSubfolder = "Benchmarks/LoadInsights";
        public const string ArmedPrefKey = "CSM.LoadInsights.Armed";

        /// <summary>Frames longer than this during a load are recorded as stalls.</summary>
        public const float StallThresholdMs = 150f;

        const int MaxSpans = 4096;
        const int MaxStalls = 64;
        const int MaxErrors = 100;
        const int MaxMarks = 256;

        static readonly object s_gate = new();
        static readonly double s_msPerTick = 1000.0 / Stopwatch.Frequency;

        // Set by LoadInsightsRuntime (editor + dev builds). Without a host the recorder is inert,
        // so a stale PlayerPrefs flag can never record in a release build.
        internal static bool HostAvailable;
        internal static int MainThreadId = -1;

        static bool s_armedCached;
        static bool s_armedRead;

        static volatile bool s_recording;
        static long s_startTs;

        // Handles are generation-packed (gen * MaxSpans + id) so a handle held across an
        // abort/restart (e.g. SceneLoader's scene-load span) can never close a span that
        // belongs to a NEWER recording.
        static int s_generation;

        // Recording state (guarded by s_gate).
        static List<LoadInsightSpan> s_spans;
        static List<int> s_active;             // open span ids in begin order; last = innermost
        static List<LoadMark> s_marks;
        static List<LoadStall> s_stalls;
        static List<SweepError> s_errors;
        static Dictionary<string, long> s_counters;
        static Dictionary<string, (double ms, long count, double max)> s_accums;
        static int s_dropped;
        static int s_frames;
        static int s_lastSampledFrame = -1;
        static float s_worstFrameMs;
        static float s_visualReadyMs = -1f;
        static string s_trigger = "";

        // Context pushed by instrumentation (host may complete after scene changes).
        static string s_sceneFrom = "", s_sceneTo = "", s_gameMode = "";
        static int s_intensity, s_totalPlayers, s_humanPlayers, s_aiBackfill;
        static bool s_isMultiplayer;

        /// <summary>The most recently completed (or recovered) report this session.</summary>
        public static LoadInsightReport LastReport { get; private set; }

        /// <summary>Disk path of <see cref="LastReport"/>'s JSON. Empty until saved.</summary>
        public static string LastReportPath { get; private set; } = "";

        public static bool IsRecording => s_recording;

        /// <summary>
        /// Record Insight Mode. While armed (and a host exists), every game launch is recorded
        /// until disarmed. Persisted so it survives domain reloads and editor restarts.
        /// </summary>
        public static bool Armed
        {
            get
            {
                if (!s_armedRead) { s_armedCached = PlayerPrefs.GetInt(ArmedPrefKey, 0) == 1; s_armedRead = true; }
                return s_armedCached;
            }
            set
            {
                s_armedCached = value;
                s_armedRead = true;
                PlayerPrefs.SetInt(ArmedPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // ── Live readout (editor tab status line) ───────

        public static float ElapsedMs => s_recording ? (float)((Stopwatch.GetTimestamp() - s_startTs) * s_msPerTick) : 0f;
        public static int LiveSpanCount { get { lock (s_gate) return s_spans?.Count ?? 0; } }
        public static int LiveStallCount { get { lock (s_gate) return s_stalls?.Count ?? 0; } }

        public static string CurrentActivity
        {
            get
            {
                lock (s_gate)
                {
                    if (!s_recording || s_active == null || s_active.Count == 0) return "—";
                    var span = s_spans[s_active[^1]];
                    return $"{span.categoryName}: {span.label}";
                }
            }
        }

        // ── Recording lifecycle ─────────────────────────

        /// <summary>
        /// Starts recording a load (no-op unless armed + host present). Called at game launch;
        /// a second call while recording abandons the first as superseded.
        /// </summary>
        public static void BeginLoad(string trigger)
        {
            if (!HostAvailable || !Armed) return;
            if (s_recording) CompleteInternal($"Superseded by new load ({trigger})", aborted: true);

            lock (s_gate)
            {
                s_spans = new List<LoadInsightSpan>(256);
                s_active = new List<int>(16);
                s_marks = new List<LoadMark>(64);
                s_stalls = new List<LoadStall>(MaxStalls);
                s_errors = new List<SweepError>(16);
                s_counters = new Dictionary<string, long>(16);
                s_accums = new Dictionary<string, (double, long, double)>(16);
                s_dropped = 0;
                s_frames = 0;
                s_lastSampledFrame = -1;
                s_worstFrameMs = 0f;
                s_visualReadyMs = -1f;
                s_trigger = trigger;
                s_sceneFrom = SafeActiveSceneName();
                s_sceneTo = "";
                s_gameMode = "";
                s_intensity = 0; s_totalPlayers = 0; s_humanPlayers = 0; s_aiBackfill = 0;
                s_isMultiplayer = false;

                s_startTs = Stopwatch.GetTimestamp();
                s_generation++;
                s_recording = true;
                s_marks.Add(new LoadMark { atMs = 0f, label = $"Load started — {trigger}" });
            }
            UnityEngine.Debug.Log($"[LoadInsights] ● Recording load — {trigger}");
        }

        /// <summary>Finalizes the recording as a successful load (playable reached).</summary>
        public static void CompleteLoad(string reason)
        {
            if (!s_recording) return;
            CompleteInternal(reason, aborted: false);
        }

        /// <summary>Finalizes the recording as aborted (returned to menu, play mode exit, timeout…).</summary>
        public static void AbortLoad(string reason)
        {
            if (!s_recording) return;
            CompleteInternal($"Aborted: {reason}", aborted: true);
        }

        /// <summary>Stamps the user-visible "loaded" moment (splash cleared / OnClientReady).</summary>
        public static void MarkVisualReady()
        {
            if (!s_recording) return;
            lock (s_gate)
            {
                if (!s_recording) return;
                if (s_visualReadyMs < 0f) s_visualReadyMs = NowMsUnlocked();
                AddMarkUnlocked("Client ready — splash cleared, vessel visible");
            }
        }

        /// <summary>Game parameters for the report header. Call once at launch (GameDataSO knows them all).</summary>
        public static void SetGameContext(string sceneTo, string gameMode, int intensity,
            int totalPlayers, int humanPlayers, int aiBackfill, bool isMultiplayer)
        {
            if (!s_recording) return;
            lock (s_gate)
            {
                if (!s_recording) return;
                s_sceneTo = sceneTo ?? "";
                s_gameMode = gameMode ?? "";
                s_intensity = intensity;
                s_totalPlayers = totalPlayers;
                s_humanPlayers = humanPlayers;
                s_aiBackfill = aiBackfill;
                s_isMultiplayer = isMultiplayer;
            }
        }

        // ── Span / mark / counter API (instrumentation surface) ──

        /// <summary>Scoped span: <c>using (LoadInsights.Measure(cat, "label")) { … }</c>.</summary>
        public static LoadSpanScope Measure(LoadInsightCategory category, string label,
            bool isWait = false, bool isHumanWait = false)
            => new(Begin(category, label, isWait, isHumanWait));

        /// <summary>
        /// Opens a span and returns its handle (-1 when not recording — safe to pass to End).
        /// Use for spans that cross methods/frames; pair with <see cref="End"/>.
        /// </summary>
        public static int Begin(LoadInsightCategory category, string label,
            bool isWait = false, bool isHumanWait = false)
        {
            if (!s_recording) return -1;
            lock (s_gate)
            {
                if (!s_recording) return -1;
                if (s_spans.Count >= MaxSpans) { s_dropped++; return -1; }

                int id = s_spans.Count;
                int parent = s_active.Count > 0 ? s_active[^1] : -1;
                var span = new LoadInsightSpan
                {
                    id = id,
                    parentId = parent,
                    depth = parent >= 0 ? s_spans[parent].depth + 1 : 0,
                    label = label,
                    category = (int)category,
                    categoryName = LoadInsightCategories.DisplayName(category),
                    startMs = NowMsUnlocked(),
                    isWait = isWait || isHumanWait,
                    isHumanWait = isHumanWait,
                    offMainThread = MainThreadId >= 0 &&
                                    System.Threading.Thread.CurrentThread.ManagedThreadId != MainThreadId
                };
                s_spans.Add(span);
                s_active.Add(id);
                return s_generation * MaxSpans + id;
            }
        }

        /// <summary>Closes a span opened by <see cref="Begin"/>. -1 and stale handles are ignored.</summary>
        public static void End(int handle)
        {
            if (handle < 0 || !s_recording) return;
            lock (s_gate)
            {
                if (!s_recording || s_spans == null) return;
                int generation = handle / MaxSpans;
                int id = handle % MaxSpans;
                if (generation != s_generation || id >= s_spans.Count) return; // stale handle from an earlier recording
                var span = s_spans[id];
                if (span.endMs >= 0f) return; // already closed
                span.endMs = NowMsUnlocked();
                span.durationMs = span.endMs - span.startMs;
                s_active.Remove(id);
            }
        }

        /// <summary>Drops an instant event on the load timeline.</summary>
        public static void Mark(string label)
        {
            if (!s_recording) return;
            lock (s_gate)
            {
                if (!s_recording) return;
                AddMarkUnlocked(label);
            }
        }

        /// <summary>Bumps a named tally (e.g. "Prisms spawned during load").</summary>
        public static void Count(string counter, long delta = 1)
        {
            if (!s_recording) return;
            lock (s_gate)
            {
                if (!s_recording) return;
                s_counters.TryGetValue(counter, out long v);
                s_counters[counter] = v + delta;
            }
        }

        // ── Hot-path accumulators ───────────────────────
        // Per-item spans would blow the span budget on a 25k-prism lay; accumulators aggregate
        // sub-stage timings by label instead (total/count/max), answering "what INSIDE the big
        // span is slow" for pennies. Usage:
        //     long t = LoadInsights.AccumulateStart();          // 0 when not recording
        //     DoStageA(); t = LoadInsights.AccumulateSample("StageA", t);
        //     DoStageB(); t = LoadInsights.AccumulateSample("StageB", t);

        /// <summary>Timestamp to feed <see cref="AccumulateSample"/>. 0 (inert) when not recording.</summary>
        public static long AccumulateStart() => s_recording ? Stopwatch.GetTimestamp() : 0L;

        /// <summary>
        /// Adds (now − start) to the named accumulator and returns a fresh timestamp for the next
        /// stage. Inert when <paramref name="startTimestamp"/> is 0 or recording has ended.
        /// </summary>
        public static long AccumulateSample(string label, long startTimestamp)
        {
            if (startTimestamp == 0L || !s_recording) return 0L;
            long now = Stopwatch.GetTimestamp();
            double ms = (now - startTimestamp) * s_msPerTick;
            lock (s_gate)
            {
                if (!s_recording || s_accums == null) return 0L;
                s_accums.TryGetValue(label, out var a);
                a.ms += ms;
                a.count++;
                if (ms > a.max) a.max = ms;
                s_accums[label] = a;
            }
            return now;
        }

        // ── Host feeders (LoadInsightsRuntime) ──────────

        /// <summary>
        /// Per-frame sample while recording: frame count, worst frame, stall capture. Called by
        /// the host's Update AND by CompleteInternal (so the frame the load ends on is captured
        /// even when the endpoint fires before the host's Update that frame). Deduped by
        /// Time.frameCount — main-thread callers only.
        /// </summary>
        internal static void RecordFrame(float frameMs)
        {
            if (!s_recording) return;
            int frame = Time.frameCount;
            lock (s_gate)
            {
                if (!s_recording || frame == s_lastSampledFrame) return;
                s_lastSampledFrame = frame;
                s_frames++;
                if (frameMs > s_worstFrameMs) s_worstFrameMs = frameMs;
                if (frameMs < StallThresholdMs) return;

                float endMs = NowMsUnlocked();
                var stall = new LoadStall
                {
                    atMs = endMs,
                    durationMs = frameMs,
                    during = StallCulpritUnlocked(endMs - frameMs, endMs)
                };
                if (s_stalls.Count < MaxStalls) { s_stalls.Add(stall); return; }

                // Full: replace the smallest stall if this one is bigger.
                int min = 0;
                for (int i = 1; i < s_stalls.Count; i++)
                    if (s_stalls[i].durationMs < s_stalls[min].durationMs) min = i;
                if (s_stalls[min].durationMs < frameMs) s_stalls[min] = stall;
            }
        }

        /// <summary>
        /// Names the span that best explains a stalled frame: run the same innermost-wins
        /// attribution the pie uses, restricted to [frameStart, frameEnd], and return the span
        /// that claimed the most of that window. The naive "innermost active at frame end"
        /// misattributes single-frame monsters — a 100s spawn that opens and closes mid-frame
        /// isn't active anymore when the frame ends. Runs only for stalls (≤64/load), so the
        /// O(spans log spans) sweep is negligible.
        /// </summary>
        static string StallCulpritUnlocked(float frameStartMs, float frameEndMs)
        {
            int n = s_spans.Count;
            if (n == 0) return "—";

            var events = new List<(float t, int kind, int idx)>(32);
            for (int i = 0; i < n; i++)
            {
                var s = s_spans[i];
                float end = s.endMs >= 0f ? s.endMs : float.MaxValue;
                if (end <= frameStartMs || s.startMs >= frameEndMs) continue; // outside the window
                events.Add((s.startMs, 1, i));
                events.Add((end, 0, i));
            }
            if (events.Count == 0) return "—";
            events.Sort((a, b) => a.t != b.t ? a.t.CompareTo(b.t) : a.kind.CompareTo(b.kind));

            var claimed = new Dictionary<int, float>(8);
            var active = new List<int>(8);
            float cursor = float.MinValue;

            void Claim(float upTo)
            {
                float from = Mathf.Max(cursor, frameStartMs);
                float to = Mathf.Min(upTo, frameEndMs);
                if (to <= from || active.Count == 0) return;
                int inner = active[^1];
                claimed.TryGetValue(inner, out float v);
                claimed[inner] = v + (to - from);
            }

            foreach (var (t, kind, idx) in events)
            {
                Claim(t);
                cursor = t;
                if (kind == 1) active.Add(idx);
                else active.Remove(idx);
            }
            Claim(frameEndMs);

            int bestIdx = -1;
            float bestMs = 0f;
            foreach (var kv in claimed)
                if (kv.Value > bestMs) { bestMs = kv.Value; bestIdx = kv.Key; }
            return bestIdx >= 0 ? s_spans[bestIdx].label : "—";
        }

        internal static void RecordError(string type, string message)
        {
            if (!s_recording) return;
            lock (s_gate)
            {
                if (!s_recording || s_errors.Count >= MaxErrors) return;
                s_errors.Add(new SweepError
                {
                    timeSeconds = (float)(NowMsUnlocked() / 1000f),
                    type = type,
                    message = message
                });
            }
        }

        static string InFlightPath =>
            Path.Combine(Application.persistentDataPath, OutputSubfolder, "_loadinsights_inflight.json");

        /// <summary>
        /// Writes a provisional snapshot so a force-killed app still leaves evidence — exactly the
        /// "user gave up after 10 minutes" case this tool exists for. Called every few seconds by
        /// the host while recording.
        /// </summary>
        internal static void SaveInFlight()
        {
            if (!s_recording) return;
            try
            {
                LoadInsightReport snapshot = null;
                lock (s_gate)
                {
                    if (s_recording)
                        snapshot = BuildReportUnlocked("(in flight)", provisional: true);
                }
                if (snapshot == null) return;
                Directory.CreateDirectory(Path.GetDirectoryName(InFlightPath));
                File.WriteAllText(InFlightPath, JsonUtility.ToJson(snapshot, false));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[LoadInsights] In-flight snapshot failed: {e.Message}");
            }
        }

        /// <summary>
        /// Called once at host startup: if the previous session died mid-load, promote its last
        /// in-flight snapshot to a real (interrupted) report so the story isn't lost.
        /// </summary>
        internal static void RecoverInFlight()
        {
            try
            {
                if (!File.Exists(InFlightPath)) return;
                var report = LoadInsightReport.LoadFromFile(InFlightPath);
                File.Delete(InFlightPath);
                if (report == null || report.spans == null || report.spans.Count == 0) return;

                report.interrupted = true;
                report.completionReason = "INTERRUPTED — application terminated during load (recovered snapshot)";
                LastReportPath = report.SaveToFile(OutputSubfolder);
                LastReport = report;
                UnityEngine.Debug.LogWarning($"[LoadInsights] Recovered an interrupted load recording → {LastReportPath}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[LoadInsights] In-flight recovery failed: {e.Message}");
            }
        }

        // ── Finalize ────────────────────────────────────

        static void CompleteInternal(string reason, bool aborted)
        {
            // Capture the frame the load is ending on: unscaledDeltaTime still holds the
            // just-finished frame's duration, and the host's Update may not have sampled it yet
            // (endpoint events can fire earlier in the same frame). RecordFrame dedups by
            // frameCount, so this never double-counts. Guarded — every completion path is
            // main-thread, but a Time access must never be able to kill finalization.
            try { RecordFrame(Time.unscaledDeltaTime * 1000f); }
            catch { /* off-main or teardown — skip the final frame sample */ }

            LoadInsightReport report;
            lock (s_gate)
            {
                if (!s_recording) return;
                report = BuildReportUnlocked(reason, provisional: false);
                s_recording = false;
                s_spans = null; s_active = null; s_marks = null; s_stalls = null;
                s_errors = null; s_counters = null; s_accums = null;
            }

            try
            {
                LastReportPath = report.SaveToFile(OutputSubfolder);
                LastReport = report;
                try { if (File.Exists(InFlightPath)) File.Delete(InFlightPath); } catch { /* best-effort */ }
                UnityEngine.Debug.Log($"[LoadInsights] ■ {(aborted ? "Load recording ended" : "Load report ready")} — " +
                                      $"{report.totalMs / 1000f:F2}s · {reason} → {LastReportPath}");
            }
            catch (Exception e)
            {
                LastReport = report;
                UnityEngine.Debug.LogError($"[LoadInsights] Failed to save load report: {e.Message}");
            }
        }

        /// <summary>Assembles a report from current state. Provisional = clone spans (recording continues).</summary>
        static LoadInsightReport BuildReportUnlocked(string reason, bool provisional)
        {
            float endMs = NowMsUnlocked();

            var spans = s_spans;
            if (provisional)
            {
                spans = new List<LoadInsightSpan>(s_spans.Count);
                foreach (var s in s_spans)
                    spans.Add(new LoadInsightSpan
                    {
                        id = s.id, parentId = s.parentId, depth = s.depth, label = s.label,
                        category = s.category, categoryName = s.categoryName,
                        startMs = s.startMs, endMs = s.endMs, durationMs = s.durationMs,
                        isWait = s.isWait, isHumanWait = s.isHumanWait,
                        offMainThread = s.offMainThread
                    });
            }

            // Close anything still open at the boundary.
            foreach (var s in spans)
            {
                if (s.endMs >= 0f) continue;
                s.endMs = endMs;
                s.durationMs = s.endMs - s.startMs;
                s.truncated = true;
            }

            var report = new LoadInsightReport
            {
                trigger = s_trigger,
                completionReason = reason,
                sceneFrom = s_sceneFrom,
                sceneTo = string.IsNullOrEmpty(s_sceneTo) ? SafeActiveSceneName() : s_sceneTo,
                gameMode = s_gameMode,
                intensity = s_intensity,
                totalPlayers = s_totalPlayers,
                humanPlayers = s_humanPlayers,
                aiBackfill = s_aiBackfill,
                isMultiplayer = s_isMultiplayer,
                totalMs = endMs,
                visualReadyMs = s_visualReadyMs,
                framesDuringLoad = s_frames,
                avgFpsDuringLoad = endMs > 0f ? s_frames / (endMs / 1000f) : 0f,
                worstFrameMs = s_worstFrameMs,
                droppedSpans = s_dropped,
                spans = spans,
                stalls = new List<LoadStall>(s_stalls.OrderByDescending(s => s.durationMs)),
                marks = new List<LoadMark>(s_marks),
                errors = new List<SweepError>(s_errors),
                counters = s_counters.Select(kv => new LoadCounter { name = kv.Key, value = kv.Value }).ToList(),
                accumulators = s_accums
                    .Select(kv => new LoadAccumulator
                    {
                        label = kv.Key,
                        count = kv.Value.count,
                        totalMs = (float)kv.Value.ms,
                        maxSingleMs = (float)kv.Value.max
                    })
                    .OrderByDescending(a => a.totalMs)
                    .ToList()
            };

            // Workload census at the boundary — same source as the benchmark's game-load stats.
            try
            {
                var live = GameLoadSampler.Sample(null);
                if (live.activePrisms > 0)
                    report.counters.Add(new LoadCounter { name = "Active prisms when load ended", value = live.activePrisms });
            }
            catch { /* managers absent in tooling contexts */ }

            report.PopulateEnvironment();
            PopulateNetworkContext(report);
            ComputeAttribution(report);
            BuildTopCosts(report);
            BuildHints(report);
            return report;
        }

        static void PopulateNetworkContext(LoadInsightReport report)
        {
            try
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                if (nm == null || !nm.IsListening)
                {
                    report.networkRole = "Local";
                    return;
                }
                report.networkRole = nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client";
                if (nm.IsServer) report.connectedClients = nm.ConnectedClientsIds.Count;
            }
            catch
            {
                report.networkRole = "Unknown";
            }
        }

        /// <summary>
        /// Exact wall-clock attribution: walk the span boundaries in time order and hand every
        /// segment to the innermost span active during it (none → Unattributed). Fills each span's
        /// exclusiveMs, the per-category slices, and the wait/work split. Percentages sum to 100.
        /// </summary>
        static void ComputeAttribution(LoadInsightReport report)
        {
            var spans = report.spans;
            int n = spans.Count;

            // (time, isStart, spanIndex) — ends sort before starts at equal times so butt-joined
            // spans don't overlap for a zero-length segment.
            var events = new List<(float t, int kind, int idx)>(n * 2);
            for (int i = 0; i < n; i++)
            {
                events.Add((spans[i].startMs, 1, i));
                events.Add((spans[i].endMs, 0, i));
            }
            events.Sort((a, b) => a.t != b.t ? a.t.CompareTo(b.t) : a.kind.CompareTo(b.kind));

            var catMs = new Dictionary<int, (float ms, float wait, int count)>();
            var seen = new HashSet<int>();
            var active = new List<int>(16);
            float cursor = 0f;
            float waitMs = 0f, humanWaitMs = 0f, unattributedMs = 0f;

            void Attribute(float from, float to)
            {
                float len = to - from;
                if (len <= 0f) return;
                if (active.Count == 0)
                {
                    unattributedMs += len;
                    return;
                }
                var innermost = spans[active[^1]];
                innermost.exclusiveMs += len;
                if (innermost.isWait) waitMs += len;
                if (innermost.isHumanWait) humanWaitMs += len;

                catMs.TryGetValue(innermost.category, out var agg);
                agg.ms += len;
                if (innermost.isWait) agg.wait += len;
                if (seen.Add(innermost.id)) agg.count++;
                catMs[innermost.category] = agg;
            }

            foreach (var (t, kind, idx) in events)
            {
                Attribute(cursor, Mathf.Min(t, report.totalMs));
                cursor = Mathf.Min(t, report.totalMs);
                if (kind == 1) active.Add(idx);
                else active.Remove(idx);
            }
            Attribute(cursor, report.totalMs);

            report.waitMs = waitMs;
            report.humanWaitMs = humanWaitMs;
            report.unattributedMs = unattributedMs;
            report.workMs = Mathf.Max(0f, report.totalMs - waitMs);

            report.slices = new List<LoadCategorySlice>();
            foreach (var kv in catMs)
            {
                report.slices.Add(new LoadCategorySlice
                {
                    category = kv.Key,
                    name = LoadInsightCategories.DisplayName((LoadInsightCategory)kv.Key),
                    attributedMs = kv.Value.ms,
                    percent = report.totalMs > 0f ? kv.Value.ms / report.totalMs * 100f : 0f,
                    spanCount = kv.Value.count,
                    waitMs = kv.Value.wait
                });
            }
            if (unattributedMs > 0.5f)
            {
                report.slices.Add(new LoadCategorySlice
                {
                    category = LoadInsightCategories.UnattributedIndex,
                    name = LoadInsightCategories.UnattributedName,
                    attributedMs = unattributedMs,
                    percent = report.totalMs > 0f ? unattributedMs / report.totalMs * 100f : 0f,
                    spanCount = 0,
                    waitMs = 0f
                });
            }
            report.slices.Sort((a, b) => b.attributedMs.CompareTo(a.attributedMs));
        }

        static void BuildTopCosts(LoadInsightReport report)
        {
            var byKey = new Dictionary<(int cat, string label), LoadTopCost>();
            foreach (var s in report.spans)
            {
                var key = (s.category, s.label);
                if (!byKey.TryGetValue(key, out var t))
                {
                    t = new LoadTopCost
                    {
                        label = s.label,
                        category = s.category,
                        categoryName = s.categoryName,
                        isWait = s.isWait
                    };
                    byKey[key] = t;
                }
                t.count++;
                t.totalMs += s.durationMs;
                t.exclusiveMs += s.exclusiveMs;
                if (s.durationMs > t.maxSingleMs) t.maxSingleMs = s.durationMs;
            }
            report.topCosts = byKey.Values
                .OrderByDescending(t => t.exclusiveMs)
                .ThenByDescending(t => t.totalMs)
                .Take(25)
                .ToList();
        }

        // ── Insight generation ──────────────────────────

        static void BuildHints(LoadInsightReport report)
        {
            var hints = report.hints;
            float total = report.totalMs;
            if (total <= 0f) return;

            if (total >= 60_000f)
                hints.Add(Hint("load-very-long", HintSeverity.Blocker, "Load exceeds a minute",
                    $"This load took {total / 1000f:F1}s — most players force-quit long before this.",
                    "Attack the biggest slices below; anything over ~15s needs structural fixes, not tuning."));
            else if (total >= 20_000f)
                hints.Add(Hint("load-long", HintSeverity.Warning, "Long load",
                    $"This load took {total / 1000f:F1}s.",
                    "Aim for <10s: trim the top slices below."));

            var scripted = report.slices.FirstOrDefault(s => s.category == (int)LoadInsightCategory.ScriptedDelay);
            if (scripted != null && scripted.attributedMs > 500f)
                hints.Add(Hint("scripted-delays", HintSeverity.Warning, "Hardcoded delays are on the critical path",
                    $"{scripted.attributedMs / 1000f:F1}s ({scripted.percent:F0}%) of this load is fixed UniTask.Delay-style gates.",
                    "These are free wins — replace fixed delays with event-driven readiness " +
                    "(SOAP events / NetworkVariable callbacks) or shrink the constants."));

            var netcode = report.slices.FirstOrDefault(s => s.category == (int)LoadInsightCategory.Netcode);
            if (netcode != null && netcode.percent >= 35f)
                hints.Add(Hint("netcode-dominant", HintSeverity.Warning, "Netcode sync dominates the load",
                    $"{netcode.attributedMs / 1000f:F1}s ({netcode.percent:F0}%) went to session/replication waits ({report.networkRole}).",
                    "Check roster RPC retries, replication waits, and per-player spawn round-trips; " +
                    "batch spawns and cut fixed replication delays."));

            var dominator = report.topCosts.FirstOrDefault();
            if (dominator != null && total > 5_000f && dominator.exclusiveMs / total >= 0.5f)
                hints.Add(Hint("single-dominator", HintSeverity.Warning, "One cost dominates this load",
                    $"\"{dominator.label}\" alone is {dominator.exclusiveMs / total * 100f:F0}% " +
                    $"of the load ({dominator.exclusiveMs / 1000f:F1}s).",
                    "See the hot-path breakdown for its per-stage costs. Cut its workload (content/" +
                    "intensity tuning), pool or pre-instantiate its prefabs, or spread the work across frames."));

            var worstStall = report.stalls.OrderByDescending(s => s.durationMs).FirstOrDefault();
            if (worstStall != null && worstStall.durationMs >= 1000f)
                hints.Add(Hint("giant-stall", HintSeverity.Warning, "A single frame froze for over a second",
                    $"Worst frame: {worstStall.durationMs / 1000f:F1}s during \"{worstStall.during}\".",
                    "That work runs synchronously in one frame — spread it across frames " +
                    "(coroutine/UniTask batches) or pool/preload the assets."));

            float spawnMs = report.slices
                .Where(s => s.category is (int)LoadInsightCategory.Flora or (int)LoadInsightCategory.Fauna
                    or (int)LoadInsightCategory.Prisms or (int)LoadInsightCategory.Crystals
                    or (int)LoadInsightCategory.Environment or (int)LoadInsightCategory.AiBackfill)
                .Sum(s => s.attributedMs);
            if (total > 5_000f && spawnMs / total >= 0.30f)
                hints.Add(Hint("spawn-heavy", HintSeverity.Info, "Object spawning is the main workload",
                    $"{spawnMs / 1000f:F1}s ({spawnMs / total * 100f:F0}%) went to spawning environment/lifeforms/AI.",
                    "Pool the prefabs, pre-instantiate during the splash, or spread instantiation across more frames."));

            if (report.humanWaitMs > 3_000f)
                hints.Add(Hint("human-wait", HintSeverity.Info, "Part of this 'load' was humans, not code",
                    $"{report.humanWaitMs / 1000f:F1}s was spent waiting for players to press Ready.",
                    "Subtract this before comparing runs — it isn't engineering time."));

            if (total > 5_000f && report.unattributedMs / total >= 0.35f)
                hints.Add(Hint("unattributed", HintSeverity.Info, "A large share of the load is unattributed",
                    $"{report.unattributedMs / 1000f:F1}s ({report.unattributedMs / total * 100f:F0}%) had no active span.",
                    "Add LoadInsights.Measure(...) spans around the code that runs there " +
                    "(check the stalls list and timeline gaps for where)."));

            if (report.errors.Count > 0)
                hints.Add(Hint("errors-during-load", HintSeverity.Warning, "Errors fired during the load",
                    $"{report.errors.Count} error(s)/exception(s) during load — failures often add retry waits.",
                    "Fix the errors first; they may be the hidden cause of the slow path."));

            if (report.droppedSpans > 0)
                hints.Add(Hint("dropped-spans", HintSeverity.Info, "Span buffer filled",
                    $"{report.droppedSpans} spans were dropped; their time is folded into parents/unattributed.",
                    "Instrument hot loops with Count() tallies instead of per-item spans."));
        }

        static BenchmarkHint Hint(string id, HintSeverity severity, string title, string finding, string fix) =>
            new() { id = id, severity = severity, title = title, finding = finding, fixAdvice = fix };

        // ── Helpers ─────────────────────────────────────

        static void AddMarkUnlocked(string label)
        {
            if (s_marks.Count >= MaxMarks) return;
            s_marks.Add(new LoadMark { atMs = NowMsUnlocked(), label = label });
        }

        static float NowMsUnlocked() => (float)((Stopwatch.GetTimestamp() - s_startTs) * s_msPerTick);

        static string SafeActiveSceneName()
        {
            try { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; }
            catch { return "?"; }
        }
    }
}
