// ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// StallFrameMarkers.cs
// What a frozen frame spent its time on, for FrameStallWatch's "stallFrame" mark.
//
// WHY (B45, Docs/PartySystem/BUGS.md): the two-machine test of 2026-11-20 lost the party to a 21.9 s frame on the
// host's menu, a minute after a return, and the guest froze 8.2 s on its menu 11 s after one. Neither could be
// reproduced on one PC (solo, two players in MPPM, with and without the return drain, 2026-11-21), and the session
// record said only the scene, the focus and the memory. This names the engine work inside the frame: the scene load
// steps, destroys, garbage collection, and which player-loop phase (script updates, physics, rendering) held it.
//
// HOW: one ProfilerRecorder per marker of interest, capacity 1, started the first time the game is online. A
// recorder holds the LAST COMPLETED frame's total. A stall is noticed in the Update after it, and its work can sit
// in either the frame that ended or the start of the next, so the stall reads the recorders twice (the frame it is
// noticed in, and the one after) and adds the two frames' values. Nothing is read on an ordinary frame.
// Never the editor Profiler window (ProfilerDriver): switching it on during a destroy-heavy frame crashed the editor
// on 2026-11-20.
//
// A build without the profiler compiled in (a release player) gets no valid recorder, and the mark says so.
// THREAD SAFETY: main-thread only.
// ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The profiler markers a frozen frame spent its time in (see the file header). Fed by
    /// <see cref="FrameStallWatch"/>: <see cref="Fold"/> on the two frames a stall spans, then <see cref="Describe"/>.
    /// </summary>
    public static class StallFrameMarkers
    {
        /// <summary>The player loop's own phases ("Update.ScriptRunBehaviourUpdate", "EarlyUpdate.UpdatePreloading" ...).</summary>
        static readonly string[] PhasePrefixes =
        {
            "Initialization.", "EarlyUpdate.", "FixedUpdate.", "PreUpdate.", "Update.", "PreLateUpdate.", "PostLateUpdate.",
        };

        /// <summary>The engine work the B36 / B45 frames were made of (measured with the same recorders, 2026-11-20).</summary>
        static readonly string[] ExactNames =
        {
            "PlayerLoop", "Loading.UpdatePreloading", "Application.Integrate Assets in Background",
            "Application.LoadLevelAsync Integrate", "Loading.AwakeFromLoad", "UnloadScene", "Destroy",
            "GameObject.Deactivate", "GameObject.ActivateAwakeRecursively", "GC.Collect", "Instantiate",
            "Physics.Simulate", "Physics.SyncColliderTransform", "Gfx.WaitForPresentOnGfxThread",
            // The menu's first frame after a return (2026-11-22): one root canvas with about 6,900 graphics.
            "Canvas.SendWillRenderCanvases", "Canvas.BuildBatch", "UGUI.Rendering.UpdateBatches", "Layout",
            "Shader.CreateGPUProgram", "GarbageCollectAssetsProfile",
        };

        /// <summary>A hard ceiling on recorders, so a build with an unexpected marker set cannot start thousands.</summary>
        const int MaxRecorders = 200;

        /// <summary>How many markers a mark names.</summary>
        const int TopCount = 8;

        /// <summary>A marker shorter than this share of the stall is left out of the mark.</summary>
        const double MinShareOfStall = 0.05;

        /// <summary>
        /// The markers' last-completed-frame values, in nanoseconds. Tests substitute it; null (the default) reads the
        /// recorders, which is empty when none could start.
        /// </summary>
        public static Func<IEnumerable<KeyValuePair<string, long>>> Sample { get; set; }

        static readonly List<KeyValuePair<string, ProfilerRecorder>> s_recorders = new();
        static readonly Dictionary<string, long> s_peak = new();
        static bool s_started;

        /// <summary>Start the recorders, once. Cheap to call every frame after the first.</summary>
        public static void EnsureStarted()
        {
            if (s_started) return;
            s_started = true;
            try
            {
                var handles = new List<ProfilerRecorderHandle>();
                ProfilerRecorderHandle.GetAvailable(handles);
                var seen = new HashSet<string>();
                foreach (var handle in handles)
                {
                    if (s_recorders.Count >= MaxRecorders) break;
                    var description = ProfilerRecorderHandle.GetDescription(handle);
                    if (description.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                    string name = description.Name;
                    if (!Wanted(name) || !seen.Add(name)) continue;

                    var recorder = ProfilerRecorder.StartNew(description.Category, name, 1);
                    if (recorder.Valid) s_recorders.Add(new KeyValuePair<string, ProfilerRecorder>(name, recorder));
                    else recorder.Dispose();
                }
            }
            catch (Exception e)
            {
                // Instrumentation only: a build that cannot record markers says so in the mark.
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow,
                    $"[StallFrameMarkers] No marker recorders on this build ({e.GetType().Name}).");
            }
        }

        static bool Wanted(string name)
        {
            foreach (var exact in ExactNames)
                if (name == exact) return true;
            foreach (var prefix in PhasePrefixes)
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        static IEnumerable<KeyValuePair<string, long>> ReadRecorders()
        {
            foreach (var entry in s_recorders)
                if (entry.Value.Valid)
                    yield return new KeyValuePair<string, long>(entry.Key, entry.Value.LastValue);
        }

        /// <summary>Add the last completed frame's values to the stall's. A stall spans the end of one frame and the start
        /// of the next, so its work is the two frames' sum (an ordinary frame's few milliseconds ride along).</summary>
        public static void Fold()
        {
            try
            {
                foreach (var sample in (Sample ?? ReadRecorders)())
                {
                    s_peak.TryGetValue(sample.Key, out long held);
                    s_peak[sample.Key] = held + sample.Value;
                }
            }
            catch (Exception e)
            {
                CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"[StallFrameMarkers] Read failed ({e.GetType().Name}).");
            }
        }

        /// <summary>
        /// The "stallFrame" detail: the largest markers of the stall folded so far, longest first, as
        /// "Loading.UpdatePreloading 7691 ms; UnloadScene 3447 ms; ...". Clears the fold.
        /// </summary>
        public static string Describe(double stallSeconds)
        {
            long floorNs = (long)(stallSeconds * MinShareOfStall * 1e9);
            var top = new List<KeyValuePair<string, long>>();
            foreach (var entry in s_peak)
                if (entry.Value > 0 && entry.Value >= floorNs) top.Add(entry);
            s_peak.Clear();

            if (top.Count == 0)
                return s_recorders.Count == 0 && Sample == null
                    ? "no profiler markers on this build"
                    : "no recorded marker held the frame (the time was outside the player loop)";

            top.Sort((a, b) => b.Value.CompareTo(a.Value));
            var text = new StringBuilder();
            for (int i = 0; i < top.Count && i < TopCount; i++)
            {
                if (i > 0) text.Append("; ");
                text.Append(top[i].Key).Append(' ')
                    .Append((top[i].Value / 1e6).ToString("0", CultureInfo.InvariantCulture)).Append(" ms");
            }
            return text.ToString();
        }

        /// <summary>Stop and forget every recorder and anything folded (a new Play; tests).</summary>
        public static void Stop()
        {
            foreach (var entry in s_recorders) entry.Value.Dispose();
            s_recorders.Clear();
            s_peak.Clear();
            s_started = false;
        }
    }
}
