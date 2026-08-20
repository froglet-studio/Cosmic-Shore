using System;
using System.IO;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FrogletTools ▸ Diagnostics — one window, two tabs:
    ///
    /// <para><b>Crash Detector</b> — the UI over <see cref="CrashDetectorMonitor"/>: whether the
    /// watchdog is running, the previous session's verdict, the crash reports on disk, and the
    /// machine-local settings.</para>
    ///
    /// <para><b>Bug Ledger</b> — the UI over <see cref="BugLedger"/>: the shared, committable
    /// issue list with auto-capture, fix-then-validate lifecycle, and manual filing
    /// (<see cref="BugLedgerView"/>).</para>
    ///
    /// READER tool per Docs/TOOLING.md — nothing here writes Assets/, so there is no ship panel.
    /// The crash detector writes gitignored logs; the bug ledger writes the committable
    /// <c>BugLedger/</c> store at the project root, which the developer pushes like any other
    /// project data.
    /// </summary>
    public sealed class DiagnosticsWindow : EditorWindow
    {
        const int CrashTab = 0;
        const int BugsTab = 1;

        [SerializeField] int _tab = CrashTab;

        Vector2 _scroll;
        string[] _reports = Array.Empty<string>();
        long _journalBytes;
        BugLedgerView _bugView;

        /// <summary>Set by a button, run after the GUI pass — mutating state mid-layout throws.</summary>
        Action _deferred;

        [MenuItem("FrogletTools/Diagnostics/Crash Detector", false, 10)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
            Description = "Editor crash watchdog — journals errors off-thread, reports abnormal exits on the next launch.")]
        public static void OpenCrashDetector() => Open(CrashTab);

        [MenuItem("FrogletTools/Diagnostics/Bug Ledger", false, 11)]
        [FrogletTool(FrogletToolCategory.Diagnostics, Importance = 4,
            Description = "Shared committable bug list — red errors file themselves, and a fix only closes once the game validates it.")]
        public static void OpenBugLedger() => Open(BugsTab);

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
            // events across threads. OnInspectorUpdate is ~10 Hz — cheap and plenty.
            if (_tab == BugsTab && _bugView is { NeedsRepaint: true }) Repaint();
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
                _tab == CrashTab
                    ? "Watches this editor for abnormal exits — even hangs and hard kills — and writes a report to Logs/CrashDetector/ on the next launch."
                    : "The team's live bug list — errors file themselves into BugLedger/, and a fix is only believed once the game stays clean.",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Diagnostics));

            DrawTabBar();

            if (_tab == BugsTab)
            {
                _bugView ??= new BugLedgerView();
                _bugView.Draw(action => _deferred = action);
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
                GUILayout.FlexibleSpace();
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
                        "Track this crash as a Bug Ledger issue (committable, so the team sees it).", outline: true))
                    _deferred = () =>
                    {
                        BugLedger.ReportCustom(
                            $"Editor crash — {Path.GetFileName(reportPath)}",
                            $"Filed from the crash detector. Local report: {reportPath}\n" +
                            "Paste the relevant journal/log tail here so the issue travels with the evidence.");
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

                if (EditorGUI.EndChangeCheck())
                {
                    settings.HeartbeatSeconds = heartbeat;
                    settings.CaptureWarnings = warnings;
                    settings.StackTraceLines = stackLines;
                    settings.MaxReportsKept = maxReports;
                    settings.MaxJournalMB = journalCap;
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
    }
}
