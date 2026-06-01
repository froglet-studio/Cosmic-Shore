#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Editor-only bridge to the Unity Profiler for spike attribution. Reads the most
    /// expensive markers (by self time) on a given profiler frame so a spike can be blamed
    /// on a concrete subsystem. Fully guarded — any API hiccup degrades to "no markers"
    /// rather than throwing, since this is an enhancement on top of an already-useful tool.
    /// </summary>
    public static class SpikeAnalyzer
    {
        /// <summary>Index of the last fully-captured profiler frame (or -1).</summary>
        public static int LastFrameIndex => ProfilerDriver.lastFrameIndex;

        public static void SetProfilerEnabled(bool enabled)
        {
            try
            {
                ProfilerDriver.enabled = enabled;
                UnityEngine.Profiling.Profiler.enabled = enabled;
            }
            catch { /* profiler unavailable — ignore */ }
        }

        /// <summary>
        /// Fills <paramref name="output"/> with up to <paramref name="topN"/> markers from the given
        /// profiler frame, ranked by self time (ms) on the main thread. Returns true if any were found.
        /// </summary>
        public static bool TryGetTopMarkers(int frameIndex, int topN, List<MarkerSample> output)
        {
            output.Clear();
            if (frameIndex < 0) return false;

            try
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(
                    frameIndex,
                    threadIndex: 0, // main thread
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                    HierarchyFrameDataView.columnSelfTime,
                    sortAscending: false);

                if (view == null || !view.valid) return false;

                var samples = new List<MarkerSample>(64);
                var stack = new Stack<int>();
                var children = new List<int>();
                stack.Push(view.GetRootItemID());

                while (stack.Count > 0)
                {
                    int id = stack.Pop();

                    children.Clear();
                    view.GetItemChildren(id, children);
                    for (int i = 0; i < children.Count; i++)
                        stack.Push(children[i]);

                    if (id == view.GetRootItemID()) continue;

                    float selfMs = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnSelfTime);
                    if (selfMs <= 0f) continue;

                    string name = view.GetItemName(id);
                    if (string.IsNullOrEmpty(name)) continue;

                    samples.Add(new MarkerSample { name = name, ms = selfMs });
                }

                samples.Sort((a, b) => b.ms.CompareTo(a.ms));
                for (int i = 0; i < samples.Count && i < topN; i++)
                    output.Add(samples[i]);

                return output.Count > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
