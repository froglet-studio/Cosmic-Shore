#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// The Unity-bound half of <c>diag</c>'s per-system timings: one <see cref="ProfilerRecorder"/>
    /// per named marker, started with the run and read out when it ends. The statistics live
    /// in <see cref="MarkerBudget"/>.
    ///
    /// <para><b>Works in a Development build with no Profiler attached</b>, which is the point: it
    /// is how a per-system budget is tracked in the build the frame target is judged in, at a
    /// fraction of the Profiler's own overhead (each recorder times ONE marker).</para>
    ///
    /// <para><b>Names are resolved through <see cref="ProfilerRecorderHandle.GetAvailable"/></b>,
    /// never by guessing a category. A recorder is keyed on (category, name) and Unity's built-in
    /// samples do not all live in the category their name suggests, so a guessed category is a
    /// recorder that silently reads zero. Enumerating the available handles costs one hitch at
    /// the start of a run; <c>diag</c> discards its first frames for exactly that reason.</para>
    ///
    /// <para><b>Main thread only</b> (<see cref="ProfilerRecorderOptions.CollectOnlyOnCurrentThread"/>),
    /// so a marker's number is main-thread time, comparable with the frame's CPU budget. Every
    /// sample of a marker inside one frame is summed into that frame's value
    /// (<see cref="ProfilerRecorderOptions.SumAllSamplesInFrame"/>).</para>
    /// </summary>
    public sealed class MarkerBudgetRecorder : IDisposable
    {
        // Member names checked against the Unity 6000.3 scripting reference: the wrap flag is
        // "...CapacityReached". "...CapacityExceeded" does not exist and failed the compile once.
        const ProfilerRecorderOptions Options =
            ProfilerRecorderOptions.SumAllSamplesInFrame |
            ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread;

        struct Entry
        {
            public string name;
            public string category;
            public bool found;
            public bool isTime;
            public string unit;
            public ProfilerRecorder recorder;
        }

        readonly List<Entry> _entries = new();
        readonly List<ProfilerRecorderSample> _samples = new();
        int _capacity;

        /// <summary>Markers requested for the run in flight.</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// Starts one recorder per name. <paramref name="capacity"/> is the most frames a
        /// recorder keeps; a longer run keeps the LATEST frames and reports itself truncated.
        /// </summary>
        public void Start(IReadOnlyList<string> names, int capacity)
        {
            Dispose();
            _capacity = Math.Max(16, capacity);

            var available = new List<ProfilerRecorderHandle>(4096);
            ProfilerRecorderHandle.GetAvailable(available);
            var byName = new Dictionary<string, ProfilerRecorderHandle>(StringComparer.Ordinal);
            var wanted = new HashSet<string>(names, StringComparer.Ordinal);
            for (int i = 0; i < available.Count; i++)
            {
                var description = ProfilerRecorderHandle.GetDescription(available[i]);
                string name = description.Name;
                if (name != null && wanted.Contains(name) && !byName.ContainsKey(name))
                    byName.Add(name, available[i]);
            }

            for (int i = 0; i < names.Count; i++)
            {
                var entry = new Entry { name = names[i], category = "", unit = "" };
                if (byName.TryGetValue(names[i], out var handle))
                {
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    entry.found = true;
                    entry.category = description.Category.Name;
                    entry.isTime = description.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
                    entry.unit = entry.isTime ? "ms" : description.UnitType.ToString();
                    entry.recorder = new ProfilerRecorder(handle, _capacity, Options);
                    entry.recorder.Start();
                }
                _entries.Add(entry);
            }
        }

        /// <summary>
        /// Drops everything collected so far and keeps recording. Called on the first frame
        /// <c>diag</c> actually samples, so the start-up hitch (including this class's own
        /// handle enumeration) is in no marker's numbers.
        ///
        /// <para><see cref="ProfilerRecorder.Reset"/> STOPS the recorder as well as clearing it
        /// (Unity: "Sets Count to 0 and WrappedAround to false and stops collection"), so it must
        /// be started again here. Without the restart every marker reads zero for the whole run,
        /// and nothing reports that: a stopped recorder looks exactly like a marker that never ran.</para>
        /// </summary>
        public void ResetSamples()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!_entries[i].found) continue;
                var recorder = _entries[i].recorder;
                recorder.Reset();
                recorder.Start();
            }
        }

        /// <summary>
        /// Stops every recorder and folds its frames into a report row. <paramref name="runFrames"/>
        /// is the number of frames the run itself sampled; see <see cref="MarkerBudget.Summarize"/>
        /// for why a marker is averaged over the run rather than over its own samples.
        /// </summary>
        public List<MarkerBudget.MarkerStat> Stop(int runFrames)
        {
            var stats = new List<MarkerBudget.MarkerStat>(_entries.Count);
            var ms = new List<float>(_capacity);
            var calls = new List<float>(_capacity);

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (!e.found)
                {
                    stats.Add(MarkerBudget.Summarize(e.name, "", false, null, null, runFrames));
                    continue;
                }

                e.recorder.Stop();
                _samples.Clear();
                e.recorder.CopyTo(_samples, false);
                ms.Clear();
                calls.Clear();
                for (int s = 0; s < _samples.Count; s++)
                {
                    long value = _samples[s].Value;
                    ms.Add(e.isTime ? value * 1e-6f : value);
                    calls.Add(_samples[s].Count);
                }

                var stat = MarkerBudget.Summarize(e.name, e.category, true, ms, calls, runFrames,
                                                  e.recorder.WrappedAround);
                stat.unit = e.unit;
                stats.Add(stat);
            }

            Dispose();
            MarkerBudget.SortForReport(stats);
            return stats;
        }

        public void Dispose()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].found) _entries[i].recorder.Dispose();
            _entries.Clear();
        }
    }
}
#endif
