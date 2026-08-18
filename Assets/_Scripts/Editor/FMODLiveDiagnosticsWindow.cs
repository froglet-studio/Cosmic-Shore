using System;
using System.Collections.Generic;
using CosmicShore.Editor.Froglet;
using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FrogletTools ▸ Performance ▸ FMOD Live Diagnostics — answers, in one play session,
    /// WHY <c>RuntimeManager.Update()</c> is eating the frame.
    ///
    /// <para>That profiler row is nearly all <b>self</b> time with zero managed children and
    /// zero GC alloc, so Unity's hierarchy can never name the cause: the cost is native, inside
    /// <c>studioSystem.update()</c>, paying for work that gameplay code queued. This window
    /// reads FMOD's own counters instead of guessing:</para>
    ///
    /// <list type="bullet">
    ///   <item><b>Live instance count per event</b> — the money number. FMOD frees a released
    ///   instance only when it STOPS, so a one-shot fired at a LOOPING (or very long) event
    ///   never frees and accumulates forever. A four-figure count on one row names the offender
    ///   outright.</item>
    ///   <item><b>Channels playing (real / total)</b> — total far above real means heavy
    ///   virtualisation, i.e. far more voices than the platform's real-channel budget.</item>
    ///   <item><b>CPU usage</b> — FMOD's own split (studio update, DSP, stream). If studio
    ///   update is the big one, it is instance bookkeeping; if DSP is, it is actual mixing.</item>
    /// </list>
    ///
    /// <para><b>READER TOOL.</b> It writes no assets, so it carries no
    /// <c>FrogletToolShipPanel</c> / change ledger (Docs/TOOLING.md § "Tool output is a
    /// deliverable"). Nothing here mutates FMOD state — every call is a getter.</para>
    /// </summary>
    public class FMODLiveDiagnosticsWindow : EditorWindow
    {
        const int TopRowsDefault = 25;

        Vector2 _scroll;
        int _topRows = TopRowsDefault;
        bool _onlyNonZero = true;
        string _status = "Enter play mode to sample.";

        // Snapshot, rebuilt each repaint while playing.
        readonly List<(string path, int count)> _rows = new();
        int _totalInstances;
        int _channels, _realChannels;
        float _studioUpdate, _dsp, _stream, _coreUpdate;
        bool _sampled;

        [MenuItem("FrogletTools/Performance/FMOD Live Diagnostics")]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 5,
            Description = "Live FMOD counters: instances per event, channels, CPU split. " +
                          "Use when RuntimeManager.Update() is eating the frame.")]
        static void Open()
        {
            var w = GetWindow<FMODLiveDiagnosticsWindow>("FMOD Live");
            w.minSize = new Vector2(560, 420);
            w.Show();
        }

        void OnInspectorUpdate() => Repaint();   // ~10 Hz, cheap; keeps the numbers live

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("FMOD Live Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "RuntimeManager.Update() is all SELF time with no managed children, so the " +
                "Unity profiler can never name what is costing it. These are FMOD's own " +
                "counters.\n\n" +
                "Read it like this:\n" +
                "  • one event with a huge Instances count -> that event is accumulating " +
                "instances (a one-shot fired at a looping/long event never frees).\n" +
                "  • Total >> Real channels -> mass virtualisation.\n" +
                "  • Studio update % high -> instance bookkeeping; DSP % high -> real mixing.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                _topRows = Mathf.Max(1, EditorGUILayout.IntField("Top rows", _topRows));
                _onlyNonZero = EditorGUILayout.ToggleLeft("Only live events", _onlyNonZero);
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter play mode — these counters only exist at runtime.",
                                        MessageType.Warning);
                return;
            }

            Sample();

            if (!_sampled)
            {
                EditorGUILayout.HelpBox(_status, MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Totals", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Live event instances", _totalInstances.ToString("N0"));
            EditorGUILayout.LabelField("Channels (real / total)", $"{_realChannels:N0} / {_channels:N0}");
            EditorGUILayout.LabelField("CPU: studio update", $"{_studioUpdate:F2} %");
            EditorGUILayout.LabelField("CPU: dsp / stream / core update",
                $"{_dsp:F2} % / {_stream:F2} % / {_coreUpdate:F2} %");
            EditorGUI.indentLevel--;

            if (_totalInstances > 400)
            {
                EditorGUILayout.HelpBox(
                    $"{_totalInstances:N0} live instances is far past what a mix needs. The top " +
                    "row below is almost certainly being fired as a one-shot at an event that " +
                    "never stops on its own.", MessageType.Error);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Log snapshot to console (paste-able)")) LogSnapshot();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Events by live instance count", EditorStyles.miniBoldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            int shown = 0;
            foreach (var (path, count) in _rows)
            {
                if (_onlyNonZero && count <= 0) continue;
                if (shown++ >= _topRows) break;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(count.ToString("N0"), GUILayout.Width(70));
                    EditorGUILayout.LabelField(path);
                }
            }
            if (shown == 0) EditorGUILayout.LabelField("(no live instances)");
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Rebuilds the snapshot from FMOD. Every call is a getter — this never changes the mix.
        /// Wrapped because RuntimeManager throws if the studio system is not up yet.
        /// </summary>
        void Sample()
        {
            _sampled = false;
            _rows.Clear();
            _totalInstances = 0;

            FMOD.Studio.System studio;
            try { studio = RuntimeManager.StudioSystem; }
            catch (Exception ex) { _status = "FMOD studio system unavailable: " + ex.Message; return; }
            if (!studio.isValid()) { _status = "FMOD studio system is not valid yet."; return; }

            if (studio.getCPUUsage(out var studioCpu, out var coreCpu) == FMOD.RESULT.OK)
            {
                _studioUpdate = studioCpu.update;
                _dsp = coreCpu.dsp; _stream = coreCpu.stream; _coreUpdate = coreCpu.update;
            }

            try
            {
                var core = RuntimeManager.CoreSystem;
                if (core.isValid()) core.getChannelsPlaying(out _channels, out _realChannels);
            }
            catch (Exception) { /* core not up yet - totals above still stand */ }

            if (studio.getBankList(out FMOD.Studio.Bank[] banks) != FMOD.RESULT.OK || banks == null)
            {
                _status = "No banks loaded — every event lookup fails, so nothing can play. " +
                          "Check FMOD ▸ Edit Settings ▸ Load Banks.";
                return;
            }

            foreach (var bank in banks)
            {
                if (!bank.isValid()) continue;
                if (bank.getEventList(out FMOD.Studio.EventDescription[] descs) != FMOD.RESULT.OK) continue;
                if (descs == null) continue;

                foreach (var desc in descs)
                {
                    if (!desc.isValid()) continue;
                    if (desc.getInstanceCount(out int count) != FMOD.RESULT.OK) continue;
                    desc.getPath(out string path);
                    _rows.Add((string.IsNullOrEmpty(path) ? "(unnamed event)" : path, count));
                    _totalInstances += count;
                }
            }

            _rows.Sort((a, b) => b.count.CompareTo(a.count));
            _sampled = true;
        }

        void LogSnapshot()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[FMOD Live Diagnostics] snapshot");
            sb.AppendLine($"  live instances : {_totalInstances:N0}");
            sb.AppendLine($"  channels       : {_realChannels:N0} real / {_channels:N0} total");
            sb.AppendLine($"  cpu            : studio.update {_studioUpdate:F2}%  dsp {_dsp:F2}%  " +
                          $"stream {_stream:F2}%  core.update {_coreUpdate:F2}%");
            sb.AppendLine("  top events by live instance count:");
            int shown = 0;
            foreach (var (path, count) in _rows)
            {
                if (count <= 0 || shown++ >= _topRows) break;
                sb.AppendLine($"    {count,7:N0}  {path}");
            }
            if (shown == 0) sb.AppendLine("    (none)");
            Debug.Log(sb.ToString());
        }
    }
}
