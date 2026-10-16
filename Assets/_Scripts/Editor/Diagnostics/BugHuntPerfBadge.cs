using System;
using System.Globalization;
using System.IO;
using CosmicShore.Editor.Froglet;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The Bug Hunt &amp; Performance badge — the ONE piece of editor UI the bug hunt and the
    /// performance system have (Yash, 2026-10-15: "no extra UIs in our editor ... at the start of
    /// each Unity editor, a small indicator which says the bug hunt and performance report, with
    /// few options to do the most basic necessary steps"). Docs/DIAGNOSTICS.md § "The Bug Hunt &amp;
    /// Performance badge".
    ///
    /// <para>A small card in the bottom-left corner of every Scene view. It opens expanded once per
    /// editor start, then folds to a one-line pill. It reads two things and runs nothing slow:
    /// the Bug Ledger's counts (open, being validated) and <c>Docs/perf/status.json</c>, which every
    /// <c>Tools/Build/perf_hunt.py sweep</c> writes (the gates, regressions, the scenarios still owed
    /// a Release capture, the next step).</para>
    ///
    /// <para>Its only buttons are the basic steps, each shown only when it applies:
    /// <b>Send to Claude</b> (open errors exist: <see cref="BugLedgerSender"/>'s push to
    /// <c>bugledger/&lt;user&gt;</c>, which never touches the working tree and pushes nothing when
    /// nothing changed) and <b>Release</b> (switch Code Optimization, since a Debug capture is not a
    /// number). There is no Capture button: <see cref="PerfAutoCapture"/> captures by itself in Play mode, and its
    /// status line ("Auto-capture: S2 in 6:12") shows here while playing. It never opens a dialog: the one
    /// after-play question is <see cref="BugHuntAutoSend"/>'s.</para>
    ///
    /// <para>Reader-plus-launcher: it writes nothing under Assets, so it needs no ship panel
    /// (Docs/TOOLING.md). Hidden with × until the next editor start.</para>
    /// </summary>
    [InitializeOnLoad]
    public static class BugHuntPerfBadge
    {
        const string HiddenKey = "CosmicShore.BugHuntPerfBadge.Hidden";
        const string ExpandedKey = "CosmicShore.BugHuntPerfBadge.Expanded";
        const double RefreshSeconds = 60.0;
        const float Margin = 8f, CardWidth = 400f, Pad = 8f, RowHeight = 18f, ButtonHeight = 22f;

        [Serializable]
        class Scenario
        {
            public string label;
            public string title;
            public string scene;
            public bool owed;
        }

        [Serializable]
        class Status
        {
            public string generatedUtc;
            public bool gatesOk = true;
            public string[] regressions;
            public string[] next;
            public Scenario[] scenarios;
        }

        static Status _status;
        static DateTime _statusWrite;
        static int _open, _validating;
        static double _nextRefresh;
        static bool _wasCapturing, _wasSending;
        static string _note;

        static BugHuntPerfBadge()
        {
            if (Application.isBatchMode) return;
            SceneView.duringSceneGui += OnSceneGui;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += change =>
            {
                // Stopping play is when a new error or a new capture lands.
                if (change == PlayModeStateChange.EnteredEditMode) _nextRefresh = 0;
            };
        }

        // ── Data ────────────────────────────────────────────────────────────────

        static void Tick()
        {
            bool capturing = EditorApplication.isPlaying && DiagnosticsHUD.CaptureRunning;
            if (_wasCapturing && !capturing)
            {
                string file = DiagnosticsHUD.LastCapturePath;
                _note = string.IsNullOrEmpty(file)
                    ? "Capture finished."
                    : $"Auto-capture saved {Path.GetFileName(file)}. Stop Play: Unity asks to send it to Claude.";
                SceneView.RepaintAll();
            }
            else if (capturing != _wasCapturing)
            {
                SceneView.RepaintAll();
            }
            _wasCapturing = capturing;

            bool sending = BugLedgerSender.IsBusy;
            if (_wasSending && !sending)
            {
                _note = "Sent to Claude (or nothing had changed since the last send).";
                _nextRefresh = 0;
            }
            if (sending != _wasSending) SceneView.RepaintAll();
            _wasSending = sending;

            if (EditorApplication.timeSinceStartup < _nextRefresh) return;
            _nextRefresh = EditorApplication.timeSinceStartup + RefreshSeconds;
            Refresh();
            SceneView.RepaintAll();
        }

        static void Refresh()
        {
            try
            {
                string path = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Docs", "perf", "status.json");
                if (File.Exists(path))
                {
                    var write = File.GetLastWriteTimeUtc(path);
                    if (_status == null || write != _statusWrite)
                    {
                        _status = JsonUtility.FromJson<Status>(File.ReadAllText(path));
                        _statusWrite = write;
                    }
                }
                else
                {
                    _status = null;
                }
            }
            catch (Exception e)
            {
                _status = null;
                _note = $"Docs/perf/status.json is unreadable ({e.GetType().Name}). Ask Claude for the performance status.";
            }

            try
            {
                BugLedger.CountsByState(out _open, out _validating, out _);
            }
            catch (Exception e)
            {
                _note = $"Bug Ledger counts unavailable ({e.GetType().Name}: {e.Message}).";
            }
        }

        // ── Summary text ────────────────────────────────────────────────────────

        static int Regressions => _status?.regressions?.Length ?? 0;

        static int Owed
        {
            get
            {
                int n = 0;
                if (_status?.scenarios != null)
                    foreach (var s in _status.scenarios) if (s != null && s.owed) n++;
                return n;
            }
        }

        static Color Accent
        {
            get
            {
                if (_status != null && (!_status.gatesOk || Regressions > 0)) return FrogletEditorPalette.Error;
                if (_open > 0 || Owed > 0 || _status == null) return FrogletEditorPalette.Warn;
                return FrogletEditorPalette.Ok;
            }
        }

        static string BugLine =>
            BugLedgerSender.IsBusy ? $"{_open} open · sending to Claude..."
            : _open + _validating == 0 ? "no open console errors"
            : $"{_open} open · {_validating} fixed, being checked by your next play runs";

        static string PerfLine
        {
            get
            {
                if (_status == null) return "no status yet (ask Claude: \"performance status\")";
                string gates = _status.gatesOk ? "gates OK" : "gates RED";
                string owed = Owed > 0 ? $" · {Owed} Release capture(s) owed" : "";
                return $"{gates} · {Regressions} regression(s){owed} · {StatusAge}";
            }
        }

        static string StatusAge
        {
            get
            {
                if (_status == null || !DateTime.TryParse(_status.generatedUtc, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when))
                    return "undated";
                var age = DateTime.UtcNow - when;
                return age.TotalHours < 1 ? "checked just now"
                     : age.TotalDays < 1 ? $"checked {(int)age.TotalHours} h ago"
                     : $"checked {(int)age.TotalDays} d ago";
            }
        }

        static string NextLine => _status?.next is { Length: > 0 } ? _status.next[0] : null;

        static bool DebugCode => CompilationPipeline.codeOptimization == CodeOptimization.Debug;

        // ── Drawing ─────────────────────────────────────────────────────────────

        static void OnSceneGui(SceneView view)
        {
            if (SessionState.GetBool(HiddenKey, false) || view.camera == null) return;

            float ppp = EditorGUIUtility.pixelsPerPoint;
            float viewHeight = view.camera.pixelRect.height / ppp;
            float viewWidth = view.camera.pixelRect.width / ppp;

            Handles.BeginGUI();
            try
            {
                if (SessionState.GetBool(ExpandedKey, true)) DrawCard(viewWidth, viewHeight);
                else DrawPill(viewHeight);
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        static void DrawPill(float viewHeight)
        {
            string text = $"Bug Hunt: {_open} open" +
                          "   Performance: " + (_status == null ? "no status"
                              : !_status.gatesOk ? "gates RED"
                              : Regressions > 0 ? $"{Regressions} regression(s)"
                              : Owed > 0 ? $"{Owed} capture(s) owed" : "OK");
            var style = FrogletEditorPalette.CardBody;
            float width = Mathf.Min(style.CalcSize(new GUIContent(text)).x + 30f, CardWidth);
            var r = new Rect(Margin, viewHeight - Margin - 22f, width, 22f);

            FrogletEditorPalette.DrawCard(r, FrogletEditorPalette.SurfaceRaised.WithAlpha(0.94f), Accent.WithAlpha(0.9f));
            FrogletEditorPalette.DrawRect(new Rect(r.x + 7f, r.y + 7f, 8f, 8f), FrogletEditorPalette.Adapt(Accent));
            GUI.Label(new Rect(r.x + 20f, r.y, r.width - 22f, r.height), text, style);
            if (GUI.Button(r, new GUIContent("", "Bug Hunt & Performance: click for details and the basic steps"), GUIStyle.none))
                SessionState.SetBool(ExpandedKey, true);
        }

        static void DrawCard(float viewWidth, float viewHeight)
        {
            float width = Mathf.Min(CardWidth, viewWidth - 2f * Margin);
            float inner = width - 2f * Pad;
            var wrapped = FrogletEditorPalette.CardBodyWrapped;
            string next = NextLine;
            if (EditorApplication.isPlaying && !string.IsNullOrEmpty(PerfAutoCapture.StatusLine) && !DiagnosticsHUD.CaptureRunning)
                _note = PerfAutoCapture.StatusLine;
            float nextHeight = next == null ? 0f : wrapped.CalcHeight(new GUIContent("Next: " + next), inner);
            float noteHeight = string.IsNullOrEmpty(_note) ? 0f : wrapped.CalcHeight(new GUIContent(_note), inner);
            float height = Pad + RowHeight + 2f + 2f * RowHeight + nextHeight + noteHeight + 6f + ButtonHeight + Pad;
            var r = new Rect(Margin, viewHeight - Margin - height, width, height);

            FrogletEditorPalette.DrawCard(r, FrogletEditorPalette.SurfaceRaised.WithAlpha(0.96f), Accent.WithAlpha(0.9f));
            FrogletEditorPalette.DrawAccentStripe(r, FrogletEditorPalette.Adapt(Accent));

            float x = r.x + Pad, y = r.y + Pad;
            GUI.Label(new Rect(x, y, inner - 44f, RowHeight), "Bug Hunt & Performance", FrogletEditorPalette.CardTitle);
            if (GUI.Button(new Rect(r.xMax - Pad - 40f, y, 18f, RowHeight), new GUIContent("–", "Fold to one line"), EditorStyles.miniButton))
                SessionState.SetBool(ExpandedKey, false);
            if (GUI.Button(new Rect(r.xMax - Pad - 18f, y, 18f, RowHeight), new GUIContent("×", "Hide until the next editor start"), EditorStyles.miniButton))
                SessionState.SetBool(HiddenKey, true);
            y += RowHeight + 2f;

            Row(x, ref y, inner, "Bugs", BugLine);
            Row(x, ref y, inner, "Perf", PerfLine);
            if (next != null)
            {
                GUI.Label(new Rect(x, y, inner, nextHeight), "Next: " + next, wrapped);
                y += nextHeight;
            }
            if (noteHeight > 0f)
            {
                GUI.Label(new Rect(x, y, inner, noteHeight), _note, wrapped);
                y += noteHeight;
            }
            y += 6f;

            DrawButtons(x, y, inner);
        }

        static void Row(float x, ref float y, float width, string head, string body)
        {
            GUI.Label(new Rect(x, y, 40f, RowHeight), head, FrogletEditorPalette.CardTitle);
            GUI.Label(new Rect(x + 40f, y, width - 40f, RowHeight), body, FrogletEditorPalette.CardBody);
            y += RowHeight;
        }

        static void DrawButtons(float x, float y, float width)
        {
            bool playing = EditorApplication.isPlaying;
            bool busy = BugLedgerSender.IsBusy;
            float bx = x;

            bool Button(string label, Color accent, string tooltip, bool enabled = true)
            {
                float w = Mathf.Max(64f, EditorStyles.boldLabel.CalcSize(new GUIContent(label)).x + 18f);
                if (bx + w > x + width) return false;
                var rect = new Rect(bx, y, w, ButtonHeight);
                bx += w + 6f;
                return FrogletEditorPalette.ColorButton(rect, label, accent, tooltip, enabled);
            }

            if (!playing && (_open > 0 || busy)
                && Button(busy ? "Sending..." : "Send to Claude", FrogletEditorPalette.Ok,
                    "Push this machine's console errors (and any new redlog, crash and perf files) to your own " +
                    "bugledger/<you> branch for Claude to fix. Your working tree and branch are not touched; " +
                    "nothing is pushed when nothing changed.", !busy))
            {
                BugLedgerSender.SendInBackground();
                _note = "Sending to Claude in the background.";
            }

            if (DebugCode && Button("Release", FrogletEditorPalette.Warn,
                    "Code Optimization is Debug: C# runs ~5x slower, so a capture is not a number. Switch to Release " +
                    "(recompiles; the same as the bug icon in the status bar).", !playing))
            {
                CompilationPipeline.codeOptimization = CodeOptimization.Release;
                _note = "Code Optimization set to Release.";
            }
        }
    }
}
