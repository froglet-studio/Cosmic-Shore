using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FrogletTools ▸ Diagnostics — one window, four tabs:
    ///
    /// <para><b>Crash Detector</b> — the UI over <see cref="CrashDetectorMonitor"/>: whether the
    /// watchdog is running, the previous session's verdict, the crash reports on disk, and the
    /// machine-local settings.</para>
    ///
    /// <para><b>Bug Ledger</b> — the UI over <see cref="BugLedger"/>: the team's live issue list
    /// with auto-capture, fix-then-validate lifecycle, and manual filing
    /// (<see cref="BugLedgerView"/>).</para>
    ///
    /// <para><b>Stage &amp; Push</b> — the ledger's own version-control surface
    /// (<see cref="BugLedgerStageView"/> over <see cref="BugLedgerPublisher"/>): the live store is
    /// gitignored, and this tab is the ONLY route by which ledger data reaches git — staged
    /// per-issue, committed and pushed with a pathspec limited to BugLedger/.</para>
    ///
    /// <para><b>Compile Timing</b> — opt-in recorder of compile + domain-reload seconds per edit
    /// (the measurement behind Docs/ASSEMBLY_SPLIT.md).</para>
    ///
    /// READER tool per Docs/TOOLING.md — nothing here writes Assets/, so there is no ship panel.
    /// </summary>
    public sealed class DiagnosticsWindow : EditorWindow
    {
        const int CrashTab = 0;
        const int BugsTab = 1;
        const int StageTab = 2;
        const int TimingTab = 3;

        [SerializeField] int _tab = CrashTab;

        Vector2 _scroll;
        string[] _reports = Array.Empty<string>();
        long _journalBytes;
        BugLedgerView _bugView;
        BugLedgerStageView _stageView;

        /// <summary>Set by a button, run after the GUI pass — mutating state mid-layout throws.</summary>
        Action _deferred;

        [MenuItem("FrogletTools/Diagnostics/Crash Detector", false, 10)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
            Description = "Editor crash watchdog — journals errors off-thread, reports abnormal exits on the next launch.",
            DocPath = "Docs/DIAGNOSTICS.md#the-crash-detector")]
        public static void OpenCrashDetector() => Open(CrashTab);

        [MenuItem("FrogletTools/Diagnostics/Bug Ledger", false, 11)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
            Description = "Shared committable bug list — red errors file themselves, and a fix only closes once the game validates it.",
            DocPath = "Docs/DIAGNOSTICS.md#the-bug-ledger")]
        public static void OpenBugLedger() => Open(BugsTab);

        [MenuItem("FrogletTools/Diagnostics/Compile Timing", false, 12)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 2,
            Description = "Record compile + domain-reload seconds per edit, and which assemblies rebuilt.",
            DocPath = "Docs/ASSEMBLY_SPLIT.md#measuring")]
        public static void OpenCompileTiming() => Open(TimingTab);

        static void Open(int tab)
        {
            var window = GetWindow<DiagnosticsWindow>("Diagnostics");
            window.minSize = new Vector2(620f, 440f);
            window._tab = tab;
            window.Refresh();
            window.Show();
        }

        void OnEnable() => Refresh();

        void OnFocus()
        {
            // A pull or a teammate's push may have changed the issue store while we were away.
            BugLedger.RefreshFromDisk();
            Refresh();
        }

        void OnInspectorUpdate()
        {
            // The ledger mutates from a background worker; poll its stamp instead of marshaling
            // events across threads. OnInspectorUpdate is ~10 Hz — cheap and plenty (and it is
            // what animates the publisher's progress bar).
            if (_tab == BugsTab && _bugView is { NeedsRepaint: true }) Repaint();
            else if (_tab == StageTab && _stageView is { NeedsRepaint: true }) Repaint();
        }

        void Refresh()
        {
            _reports = CrashDetectorMonitor.ListReports();
            try
            {
                var journal = CrashDetectorMonitor.JournalPath;
                _journalBytes = journal != null && File.Exists(journal) ? new FileInfo(journal).Length : 0L;
            }
            catch { _journalBytes = 0; }
        }

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "Diagnostics",
                _tab switch
                {
                    BugsTab => "The team's live bug list — errors file themselves into the local ledger, and a fix is only believed once the game stays clean.",
                    StageTab => "The ledger's own version control — pick what to publish, comment it, and push. Only BugLedger/ files ever move.",
                    TimingTab => "Records what an edit costs — compile seconds, domain-reload seconds, and which assemblies Unity rebuilt.",
                    _ => "Watches this editor for abnormal exits — even hangs and hard kills — and writes a report to Logs/CrashDetector/ on the next launch.",
                },
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Diagnostics));

            DrawTabBar();

            if (_tab == BugsTab)
            {
                _bugView ??= new BugLedgerView();
                _bugView.Draw(action => _deferred = action, () => _tab = StageTab);
            }
            else if (_tab == StageTab)
            {
                _stageView ??= new BugLedgerStageView();
                _stageView.Draw(action => _deferred = action);
            }
            else if (_tab == TimingTab)
            {
                DrawTimingTab();
            }
            else
            {
                DrawCrashTab();
            }

            if (_deferred != null)
            {
                var action = _deferred;
                _deferred = null;
                action();
            }
        }

        void DrawTabBar()
        {
            GUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6f);
                if (FrogletEditorPalette.ColorButton("Crash Detector",
                        FrogletEditorPalette.ColorFor(FrogletToolCategory.Diagnostics), 118f, 22f,
                        outline: _tab != CrashTab) && _tab != CrashTab)
                {
                    _tab = CrashTab;
                    GUI.FocusControl(null);
                    Refresh();
                }
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Bug Ledger",
                        FrogletEditorPalette.ColorFor(FrogletToolCategory.Diagnostics), 96f, 22f,
                        outline: _tab != BugsTab) && _tab != BugsTab)
                {
                    _tab = BugsTab;
                    GUI.FocusControl(null);
                }
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Stage & Push",
                        FrogletEditorPalette.ColorFor(FrogletToolCategory.Diagnostics), 104f, 22f,
                        outline: _tab != StageTab) && _tab != StageTab)
                {
                    _tab = StageTab;
                    GUI.FocusControl(null);
                }
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Compile Timing",
                        FrogletEditorPalette.ColorFor(FrogletToolCategory.Diagnostics), 116f, 22f,
                        outline: _tab != TimingTab) && _tab != TimingTab)
                {
                    _tab = TimingTab;
                    GUI.FocusControl(null);
                }
                GUILayout.FlexibleSpace();
                if (FrogletEditorPalette.ColorButton("Docs", FrogletEditorPalette.Info, 48f, 22f,
                        "Open Docs/DIAGNOSTICS.md on GitHub — what these tools do and how validation works.",
                        outline: true))
                    _deferred = () => FrogletDocLinks.Open(_tab switch
                    {
                        BugsTab => "Docs/DIAGNOSTICS.md#the-bug-ledger",
                        StageTab => "Docs/DIAGNOSTICS.md#staging-and-pushing",
                        TimingTab => "Docs/ASSEMBLY_SPLIT.md#measuring",
                        _ => "Docs/DIAGNOSTICS.md#the-crash-detector",
                    });
                GUILayout.Space(6f);
            }
            GUILayout.Space(2f);
        }

        // ═════════════════════════════ Crash Detector tab ═══════════════════════

        void DrawCrashTab()
        {
            if (CrashDetectorMonitor.RootDir == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "The crash detector failed to boot this session — see the console warning from [CrashDetector].",
                    MessageType.Warning);
                return;
            }

            DrawStatusRow();
            FrogletEditorPalette.HorizontalRule();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                DrawVerdict();
                GUILayout.Space(6f);
                DrawReports();
                FrogletEditorPalette.HorizontalRule();
                DrawSettings();
                GUILayout.Space(8f);
            }
            EditorGUILayout.EndScrollView();
        }

        // ── Status ───────────────────────────────────────────────────────────────

        void DrawStatusRow()
        {
            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6f);
                var pill = GUILayoutUtility.GetRect(92f, 18f, GUILayout.Width(92f));
                if (CrashDetectorMonitor.IsMonitoring)
                    FrogletEditorPalette.StatusPill(pill, "MONITORING", FrogletEditorPalette.Ok);
                else
                    FrogletEditorPalette.StatusPill(pill, "OFF", FrogletEditorPalette.Muted);

                GUILayout.Space(8f);
                var summary = CrashDetectorMonitor.IsMonitoring
                    ? $"session {CrashDetectorMonitor.SessionId} · {CrashDetectorMonitor.CurrentState} · journal {_journalBytes / 1024} KB"
                    : "nothing is watching this session";
                GUILayout.Label(summary, FrogletEditorPalette.Subtitle);

                GUILayout.FlexibleSpace();

                if (FrogletEditorPalette.ColorButton("Refresh", FrogletEditorPalette.Info, 66f, 20f, outline: true))
                    Refresh();
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Open Folder", FrogletEditorPalette.Info, 90f, 20f,
                        "Reveal Logs/CrashDetector/ in the file browser.", outline: true))
                    _deferred = RevealLogFolder;
                GUILayout.Space(4f);
                bool hasJournal = _journalBytes > 0;
                if (FrogletEditorPalette.ColorButton("Open Journal", FrogletEditorPalette.Info, 94f, 20f,
                        "Open the live session journal.", enabled: hasJournal, outline: true))
                    _deferred = () => EditorUtility.OpenWithDefaultApp(CrashDetectorMonitor.JournalPath);
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Test Error", FrogletEditorPalette.Warn, 80f, 20f,
                        "Logs one error so you can confirm it lands in the journal.",
                        enabled: CrashDetectorMonitor.IsMonitoring, outline: true))
                    _deferred = () =>
                    {
                        Debug.LogError("[CrashDetector] Test error — this entry should appear in the session journal.");
                        Refresh();
                    };
                GUILayout.Space(6f);
            }
            GUILayout.Space(4f);
        }

        void RevealLogFolder()
        {
            var journal = CrashDetectorMonitor.JournalPath;
            if (journal != null && File.Exists(journal)) EditorUtility.RevealInFinder(journal);
            else EditorUtility.RevealInFinder(CrashDetectorMonitor.RootDir);
        }

        // ── Previous-session verdict ─────────────────────────────────────────────

        void DrawVerdict()
        {
            var verdict = CrashDetectorMonitor.LastSessionVerdict;
            if (verdict == "crashed")
            {
                var rect = GUILayoutUtility.GetRect(0f, 46f, GUILayout.ExpandWidth(true));
                rect = new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height - 4f);
                FrogletEditorPalette.DrawCard(rect,
                    FrogletEditorPalette.Error.WithAlpha(0.12f),
                    FrogletEditorPalette.Error.WithAlpha(0.6f));
                FrogletEditorPalette.DrawAccentStripe(rect, FrogletEditorPalette.Error);

                GUI.Label(new Rect(rect.x + 12f, rect.y + 5f, rect.width - 130f, 18f),
                    "The previous editor session ended abnormally.", FrogletEditorPalette.CardTitle);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 23f, rect.width - 130f, 16f),
                    "A report with the captured errors and Unity's own log tail was written at this launch.",
                    FrogletEditorPalette.CardBody);

                var reportPath = CrashDetectorMonitor.LastCrashReportPath;
                var button = new Rect(rect.xMax - 106f, rect.y + 11f, 96f, 20f);
                if (FrogletEditorPalette.ColorButton(button, "Open Report", FrogletEditorPalette.Error,
                        reportPath, enabled: File.Exists(reportPath)))
                    _deferred = () => EditorUtility.OpenWithDefaultApp(reportPath);
            }
            else
            {
                var line = verdict == "clean"
                    ? "Previous session exited cleanly."
                    : "No previous-session verdict (first run, logs cleared, or the sentinel belonged to a live editor).";
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8f);
                    GUILayout.Label(line, FrogletEditorPalette.Subtitle);
                }
            }
        }

        // ── Crash reports on disk ────────────────────────────────────────────────

        void DrawReports()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Crash reports ({_reports.Length})", FrogletEditorPalette.SectionHeader);
            }

            if (_reports.Length == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8f);
                    GUILayout.Label("None on disk — good.", FrogletEditorPalette.Subtitle);
                }
                return;
            }

            foreach (var path in _reports)
            {
                var rect = GUILayoutUtility.GetRect(0f, 36f, GUILayout.ExpandWidth(true));
                rect = new Rect(rect.x + 6f, rect.y + 1f, rect.width - 12f, rect.height - 3f);
                FrogletEditorPalette.DrawCard(rect, FrogletEditorPalette.Surface,
                    FrogletEditorPalette.Muted.WithAlpha(0.25f));
                FrogletEditorPalette.DrawAccentStripe(rect, FrogletEditorPalette.Error);

                string meta;
                try
                {
                    var info = new FileInfo(path);
                    meta = $"{info.Length / 1024} KB · {info.LastWriteTime:yyyy-MM-dd HH:mm}";
                }
                catch { meta = "(unreadable)"; }

                GUI.Label(new Rect(rect.x + 10f, rect.y + 3f, rect.width - 260f, 16f),
                    Path.GetFileName(path), FrogletEditorPalette.CardTitle);
                GUI.Label(new Rect(rect.x + 10f, rect.y + 18f, rect.width - 260f, 14f),
                    meta, FrogletEditorPalette.CardBody);

                var reportPath = path;
                var open = new Rect(rect.xMax - 244f, rect.y + 7f, 52f, 20f);
                var show = new Rect(rect.xMax - 188f, rect.y + 7f, 52f, 20f);
                var file = new Rect(rect.xMax - 132f, rect.y + 7f, 62f, 20f);
                var delete = new Rect(rect.xMax - 60f, rect.y + 7f, 26f, 20f);

                if (FrogletEditorPalette.ColorButton(open, "Open", FrogletEditorPalette.Info, reportPath, outline: true))
                    _deferred = () => EditorUtility.OpenWithDefaultApp(reportPath);
                if (FrogletEditorPalette.ColorButton(show, "Show", FrogletEditorPalette.Info,
                        "Reveal in the file browser.", outline: true))
                    _deferred = () => EditorUtility.RevealInFinder(reportPath);
                if (FrogletEditorPalette.ColorButton(file, "File Bug", FrogletEditorPalette.Warn,
                        "Track this crash as a Bug Ledger issue (committable, so the team sees it). " +
                        "Filing the same report twice updates one issue.", outline: true))
                    _deferred = () =>
                    {
                        // Tool-id dedupe: the report filename is the stable key, so a second click
                        // (or a re-open of this window) refreshes the one issue instead of minting
                        // another. A crash is a blocker by definition.
                        BugLedger.ReportFromTool(
                            "Crash Detector",
                            $"Editor crash — {Path.GetFileName(reportPath)}",
                            $"Filed from the crash detector. Local report: {reportPath}\n" +
                            "Paste the relevant journal/log tail here so the issue travels with the evidence.",
                            BugLedgerIssueSeverity.Blocker);
                        _tab = BugsTab;
                        Repaint();
                    };
                if (FrogletEditorPalette.ColorButton(delete, "✕", FrogletEditorPalette.Error,
                        "Delete this report.", outline: true))
                    _deferred = () =>
                    {
                        if (EditorUtility.DisplayDialog("Delete crash report",
                                $"Delete {Path.GetFileName(reportPath)}?\n\nThis cannot be undone.",
                                "Delete", "Cancel"))
                        {
                            try { File.Delete(reportPath); } catch { }
                            Refresh();
                        }
                    };
                GUILayout.Space(2f);
            }
        }

        // ── Settings ─────────────────────────────────────────────────────────────

        void DrawSettings()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label("Settings (machine-local, UserSettings/)", FrogletEditorPalette.SectionHeader);
            }

            var settings = CrashDetectorSettings.instance;
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUI.BeginChangeCheck();
                bool enabled = EditorGUILayout.ToggleLeft(
                    new GUIContent("  Detection enabled",
                        "Start/stop the watchdog. Takes effect immediately."),
                    settings.Enabled);
                int heartbeat = EditorGUILayout.IntSlider(
                    new GUIContent("Heartbeat (seconds)",
                        "How often the session sentinel is re-stamped by the background thread."),
                    settings.HeartbeatSeconds, 1, 30);
                bool warnings = EditorGUILayout.ToggleLeft(
                    new GUIContent("  Capture warnings too",
                        "Default off — errors, exceptions and asserts only."),
                    settings.CaptureWarnings);
                int stackLines = EditorGUILayout.IntSlider(
                    new GUIContent("Stack-trace lines per entry", "0 = message only."),
                    settings.StackTraceLines, 0, 30);
                int maxReports = EditorGUILayout.IntSlider(
                    new GUIContent("Crash reports kept", "Oldest reports past this count are pruned."),
                    settings.MaxReportsKept, 1, 50);
                int journalCap = EditorGUILayout.IntSlider(
                    new GUIContent("Journal cap (MB)",
                        "An error storm must not fill the disk; past the cap entries are dropped with one marker."),
                    settings.MaxJournalMB, 1, 32);
                int hangDump = EditorGUILayout.IntSlider(
                    new GUIContent("Hang dump after (seconds)",
                        "Main-thread unresponsiveness past this writes a live minidump (Windows only, once per " +
                        "session) whose main-thread stack names the deadlock. 0 = off."),
                    settings.HangDumpSeconds, 0, 600);

                if (EditorGUI.EndChangeCheck())
                {
                    settings.HeartbeatSeconds = heartbeat;
                    settings.CaptureWarnings = warnings;
                    settings.StackTraceLines = stackLines;
                    settings.MaxReportsKept = maxReports;
                    settings.MaxJournalMB = journalCap;
                    settings.HangDumpSeconds = hangDump;
                    settings.SaveNow();
                    CrashDetectorMonitor.ApplySettings(settings);

                    if (enabled != settings.Enabled)
                        _deferred = () =>
                        {
                            CrashDetectorMonitor.SetEnabled(enabled);
                            Refresh();
                        };
                }
            }
        }

        // ═════════════════════════════ Compile Timing tab ═══════════════════════

        void DrawTimingTab()
        {
            DrawTimingStatusRow();
            FrogletEditorPalette.HorizontalRule();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                GUILayout.Space(4f);
                EditorGUILayout.HelpBox(
                    "Records compile seconds + domain-reload seconds for every edit, and which " +
                    "assemblies Unity rebuilt. Enable it, make the same one-line edit five times, " +
                    "read the median, then disable it. Protocol: Docs/ASSEMBLY_SPLIT.md § Measuring.",
                    MessageType.Info);

                GUILayout.Space(6f);
                DrawTimingRows();
                GUILayout.Space(8f);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawTimingStatusRow()
        {
            var exists = File.Exists(CompileTimingMonitor.LogPath);

            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6f);
                var pill = GUILayoutUtility.GetRect(92f, 18f, GUILayout.Width(92f));
                if (CompileTimingMonitor.Enabled)
                    FrogletEditorPalette.StatusPill(pill, "RECORDING", FrogletEditorPalette.Ok);
                else
                    FrogletEditorPalette.StatusPill(pill, "OFF", FrogletEditorPalette.Muted);

                GUILayout.Space(8f);
                GUILayout.Label(
                    CompileTimingMonitor.Enabled
                        ? "every compile + reload is being appended to Logs/CompileTiming/"
                        : "nothing is being recorded",
                    FrogletEditorPalette.Subtitle);

                GUILayout.FlexibleSpace();

                if (FrogletEditorPalette.ColorButton(
                        CompileTimingMonitor.Enabled ? "Stop" : "Start",
                        CompileTimingMonitor.Enabled ? FrogletEditorPalette.Warn : FrogletEditorPalette.Ok,
                        66f, 20f,
                        "Recording is per machine and off by default; it never travels in the repo.",
                        outline: true))
                {
                    var enable = !CompileTimingMonitor.Enabled;
                    _deferred = () => CompileTimingMonitor.Enabled = enable;
                }
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Reveal", FrogletEditorPalette.Info, 66f, 20f,
                        "Reveal the CSV in the file browser.", enabled: exists, outline: true))
                    _deferred = () => EditorUtility.RevealInFinder(CompileTimingMonitor.LogPath);
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Clear", FrogletEditorPalette.Error, 60f, 20f,
                        "Delete the recorded cycles and start a fresh measurement.",
                        enabled: exists, outline: true))
                    _deferred = ClearTimingLog;
                GUILayout.Space(6f);
            }
            GUILayout.Space(4f);
        }

        void ClearTimingLog()
        {
            if (!EditorUtility.DisplayDialog("Clear compile timing log",
                    $"Delete {CompileTimingMonitor.LogPath}?", "Delete", "Cancel"))
                return;

            try { File.Delete(CompileTimingMonitor.LogPath); }
            catch (IOException e) { Debug.LogWarning($"[CompileTiming] {e.Message}"); }
            catch (UnauthorizedAccessException e) { Debug.LogWarning($"[CompileTiming] {e.Message}"); }
        }

        void DrawTimingRows()
        {
            if (!File.Exists(CompileTimingMonitor.LogPath))
            {
                EditorGUILayout.LabelField(
                    "No cycles recorded yet.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            string[] lines;
            try { lines = File.ReadAllLines(CompileTimingMonitor.LogPath); }
            catch (IOException e)
            {
                EditorGUILayout.HelpBox(e.Message, MessageType.Warning);
                return;
            }

            var rows = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();

            EditorGUILayout.LabelField($"{rows.Length} cycle(s) recorded", FrogletEditorPalette.SectionLabel);
            DrawTimingMedian(rows);

            GUILayout.Space(4f);
            foreach (var row in rows.Reverse().Take(50))
                EditorGUILayout.LabelField(row, EditorStyles.miniLabel);
        }

        // Median rather than mean: the first compile of a session, and any compile that raced a
        // background import, are outliers big enough to swamp an average over a handful of samples.
        static void DrawTimingMedian(IReadOnlyCollection<string> rows)
        {
            var totals = rows
                .Select(r => r.Split(','))
                .Where(c => c.Length >= 4)
                .Select(c => double.TryParse(
                    c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN)
                .Where(v => !double.IsNaN(v))
                .OrderBy(v => v)
                .ToArray();

            if (totals.Length == 0) return;

            var median = totals.Length % 2 == 1
                ? totals[totals.Length / 2]
                : (totals[totals.Length / 2 - 1] + totals[totals.Length / 2]) / 2.0;

            EditorGUILayout.LabelField(
                $"Median total: {median:F2}s   (min {totals[0]:F2}s, max {totals[^1]:F2}s)",
                EditorStyles.miniLabel);
        }
    }
}
