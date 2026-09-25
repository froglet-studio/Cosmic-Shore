#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Editor-only: reads frames the Unity Profiler has already recorded and feeds them to
    /// <see cref="ProfilerCapture"/>, which decides everything the report says. Same API and the
    /// same guard shape as <see cref="SpikeAnalyzer"/> (whole file behind <c>UNITY_EDITOR</c>, so
    /// the runtime <see cref="DiagnosticsHUD"/> can call it without an Editor assembly).
    ///
    /// Deliberately thin: it opens a view, walks it, and forwards numbers. Any API hiccup on a
    /// frame skips that frame rather than failing the capture.
    /// </summary>
    public static class ProfilerFrameReader
    {
        public static int FirstFrame => ProfilerDriver.firstFrameIndex;
        public static int LastFrame => ProfilerDriver.lastFrameIndex;

        /// <summary>Whether the Profiler is recording. Setting it is the Record button.</summary>
        public static bool Recording
        {
            get { try { return ProfilerDriver.enabled; } catch { return false; } }
            set => SpikeAnalyzer.SetProfilerEnabled(value);
        }

        public static bool DeepProfiling
        {
            get { try { return ProfilerDriver.deepProfiling; } catch { return false; } }
        }

        const int MaxThreads = 256;
        const int ConsecutiveInvalidThreadsToStop = 4;

        // One child list per depth: a walk reuses them instead of allocating per node.
        static readonly List<List<int>> s_children = new();

        static List<int> ChildList(int depth)
        {
            while (s_children.Count <= depth) s_children.Add(new List<int>(32));
            var list = s_children[depth];
            list.Clear();
            return list;
        }

        static HierarchyFrameDataView Open(int frame, int thread) =>
            ProfilerDriver.GetHierarchyFrameDataView(
                frame,
                thread,
                HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                HierarchyFrameDataView.columnTotalTime,
                false);

        /// <summary>Folds one frame of the main thread into <paramref name="acc"/>.</summary>
        public static bool ReadMainThread(int frame, ProfilerCapture.Accumulator acc)
        {
            try
            {
                using var view = Open(frame, 0);
                if (view == null || !view.valid) return false;
                acc.BeginFrame(frame, view.frameTimeMs);
                Walk(view, view.GetRootItemID(), "", 0, acc);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static void Walk(HierarchyFrameDataView view, int parentId, string parentPath, int depth,
                         ProfilerCapture.Accumulator acc)
        {
            var kids = ChildList(depth);
            view.GetItemChildren(parentId, kids);
            for (int i = 0; i < kids.Count; i++)
            {
                int id = kids[i];
                float total = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnTotalTime);
                float gc = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnGcMemory);
                if (total < ProfilerCapture.WalkFloorMs && gc <= 0f) continue;

                string name = view.GetItemName(id);
                float self = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnSelfTime);
                float calls = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnCalls);
                string path = ProfilerCapture.Join(parentPath, name);
                acc.AddNode(path, parentPath, name, depth, total, self, gc, calls);

                if (depth + 1 < ProfilerCapture.MaxDepth)
                    Walk(view, id, path, depth + 1, acc);
            }
        }

        /// <summary>One frame of the main thread as a tree (for the typical and spike frames).</summary>
        public static ProfilerCapture.FrameNode ReadMainThreadTree(int frame, out float frameMs)
        {
            frameMs = 0f;
            try
            {
                using var view = Open(frame, 0);
                if (view == null || !view.valid) return null;
                frameMs = view.frameTimeMs;
                var root = new ProfilerCapture.FrameNode { name = "<frame>" };
                Build(view, view.GetRootItemID(), 0, root);
                return root;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static void Build(HierarchyFrameDataView view, int parentId, int depth, ProfilerCapture.FrameNode parent)
        {
            var kids = ChildList(depth);
            view.GetItemChildren(parentId, kids);
            for (int i = 0; i < kids.Count; i++)
            {
                int id = kids[i];
                float total = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnTotalTime);
                float gc = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnGcMemory);
                if (total < ProfilerCapture.WalkFloorMs && gc <= 0f) continue;

                var node = new ProfilerCapture.FrameNode
                {
                    name = view.GetItemName(id),
                    totalMs = total,
                    selfMs = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnSelfTime),
                    gcBytes = gc,
                    calls = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnCalls),
                };
                parent.children.Add(node);

                if (depth + 1 < ProfilerCapture.MaxDepth)
                    Build(view, id, depth + 1, node);
            }
        }

        static readonly List<ProfilerCapture.ThreadItemSample> s_items = new(32);

        /// <summary>
        /// Every thread except the main thread, top-level samples only. Thread indices are
        /// walked until several in a row come back invalid (a thread with no samples this
        /// frame can be invalid without being the last).
        /// </summary>
        public static void ReadThreads(int frame, ProfilerCapture.ThreadAccumulator acc)
        {
            acc.BeginFrame();
            int invalidRun = 0;
            for (int t = 1; t < MaxThreads && invalidRun < ConsecutiveInvalidThreadsToStop; t++)
            {
                HierarchyFrameDataView view;
                try { view = Open(frame, t); }
                catch (Exception) { break; }

                using (view)
                {
                    if (view == null || !view.valid) { invalidRun++; continue; }
                    invalidRun = 0;

                    try
                    {
                        s_items.Clear();
                        var kids = ChildList(0);
                        view.GetItemChildren(view.GetRootItemID(), kids);
                        for (int i = 0; i < kids.Count; i++)
                        {
                            int id = kids[i];
                            float total = view.GetItemColumnDataAsSingle(id, HierarchyFrameDataView.columnTotalTime);
                            if (total <= 0f) continue;
                            s_items.Add(new ProfilerCapture.ThreadItemSample(view.GetItemName(id), total));
                        }
                        acc.AddThread(view.threadName, view.threadGroupName, s_items);
                    }
                    catch (Exception)
                    {
                        // one unreadable thread must not cost the others
                    }
                }
            }
        }
    }
}
#endif
