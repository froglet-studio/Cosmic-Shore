#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.Profiling;
using UnityEditorInternal;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Editor-only bridge to the Unity Profiler for spike attribution. Reads the most
    /// expensive markers (by self time) on a given profiler frame so a spike can be blamed
    /// on a concrete subsystem. Fully guarded - any API hiccup degrades to "no markers"
    /// rather than throwing, since this is an enhancement on top of an already-useful tool.
    /// </summary>
    public static class SpikeAnalyzer
    {
        /// <summary>Index of the last fully-captured profiler frame (or -1).</summary>
        public static int LastFrameIndex => ProfilerDriver.lastFrameIndex;

        /// <summary>Oldest profiler frame still in the ring buffer. Frames below this have scrolled out.</summary>
        public static int FirstFrameIndex => ProfilerDriver.firstFrameIndex;

        public static void SetProfilerEnabled(bool enabled)
        {
            try
            {
                ProfilerDriver.enabled = enabled;
                UnityEngine.Profiling.Profiler.enabled = enabled;
            }
            catch { /* profiler unavailable - ignore */ }
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

                    // Ignore "editor spikes" - profiler/editor UI, GPU/CPU sync waits, JIT,
                    // asset loading and structural PlayerLoop nodes - so the breakdown
                    // concentrates on gameplay work.
                    if (IsEditorOrEngineNoise(name)) continue;

                    samples.Add(new MarkerSample
                    {
                        name = name,
                        ms = selfMs,
                        isScript = IsScriptSample(name)
                    });
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

        // --- Filtering ------------------------------------------------------

        /// <summary>
        /// Dropped entirely from spike attribution: profiler/editor UI, GPU/CPU sync waits,
        /// structural PlayerLoop nodes, JIT and asset loading. These are the "editor spikes"
        /// we never act on, so removing them lets the real gameplay cost surface.
        /// </summary>
        static readonly string[] s_noise =
        {
            "EditorLoop", "GUIView", "GUIUtility", "GUIClip", "IMGUIContainer", "InspectorWindow",
            "EditorOverlay", "Overlay", "Toolbar", "DockArea", "HostView", "EditorApplication",
            "EditorWindow", "RepaintAll", "GameView", "SceneView", "ProjectBrowser", "ConsoleWindow",
            "Handles", "EditorGUI", "DragAndDrop", "Profiler", "ProfilerWindow",
            "Gfx.WaitFor", "Gfx.PresentFrame", "Gfx.ProcessCommands", "WaitForTargetFPS",
            "Semaphore.WaitForSignal", "GfxDeviceClient", "PresentFrame", "Mono.JIT",
            "AssetGarbageCollect", "GarbageCollectAssets", "UnloadUnusedAssets",
            "Application.TickTimer", "PlayerLoop", "Initialization.", "WaitForEndOfFrame",
            "Render Thread",
        };

        static bool IsEditorOrEngineNoise(string name)
        {
            for (int i = 0; i < s_noise.Length; i++)
                if (name.IndexOf(s_noise[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            return false;
        }

        /// <summary>
        /// A gameplay script sample. Unity names per-MonoBehaviour method samples "Type.Method()"
        /// and coroutines "Foo() [Coroutine: MoveNext]"; our own ProfilerMarkers carry CS prefixes.
        /// </summary>
        static bool IsScriptSample(string name) =>
            name.IndexOf("()", StringComparison.Ordinal) >= 0 ||
            name.IndexOf("[Coroutine", StringComparison.Ordinal) >= 0 ||
            name.IndexOf("CSM.", StringComparison.Ordinal) >= 0 ||
            name.IndexOf("CosmicShore", StringComparison.Ordinal) >= 0;
    }
}
#endif
