using System;
using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The Bug Ledger tab of <see cref="DiagnosticsWindow"/> — the UI over <see cref="BugLedger"/>.
    /// Owns only IMGUI state (scroll, expansion, form drafts); every mutation goes through the
    /// ledger's API and every colour through <see cref="FrogletEditorPalette"/>.
    /// </summary>
    sealed class BugLedgerView
    {
        Vector2 _scroll;
        string _expandedId;
        string _notesDraftId;
        string _notesDraft = "";
        bool _showNewForm;
        string _newTitle = "";
        string _newNotes = "";

        int _lastStamp = -1;
        List<BugLedgerIssue> _sorted = new();
        int _open, _validating, _ignored;

        /// <summary>True when the ledger changed since the last draw — the host should Repaint.</summary>
        public bool NeedsRepaint => _lastStamp != BugLedger.ChangeStamp;

        public void Draw(Action<Action> defer)
        {
            SyncFromLedger();

            DrawStatusRow(defer);
            DrawCaptureLine(defer);
            FrogletEditorPalette.HorizontalRule();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                if (_showNewForm) DrawNewBugForm();

                DrawIssues(defer);
                FrogletEditorPalette.HorizontalRule();
                DrawSettings(defer);
                GUILayout.Space(8f);
            }
            EditorGUILayout.EndScrollView();
        }

        void SyncFromLedger()
        {
            int stamp = BugLedger.ChangeStamp;
            if (stamp == _lastStamp) return;
            _lastStamp = stamp;

            _sorted = BugLedger.Snapshot();
            _sorted.Sort(CompareIssues);
            BugLedger.CountsByState(out _open, out _validating, out _ignored);
        }

        static int CompareIssues(BugLedgerIssue a, BugLedgerIssue b)
        {
            int rankA = StateRank(a);
            int rankB = StateRank(b);
            if (rankA != rankB) return rankA.CompareTo(rankB);
            // ISO-8601 UTC strings sort lexicographically; newest activity first within a state.
            int seen = string.CompareOrdinal(b.LastSeenUtc, a.LastSeenUtc);
            return seen != 0 ? seen : string.CompareOrdinal(b.CreatedUtc, a.CreatedUtc);
        }

        static int StateRank(BugLedgerIssue issue)
            => issue.IsOpen ? 0 : issue.IsValidating ? 1 : 2;

        // ── Status row ───────────────────────────────────────────────────────────

        void DrawStatusRow(Action<Action> defer)
        {
            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6f);
                Pill($"{_open} OPEN", _open > 0 ? FrogletEditorPalette.Error : FrogletEditorPalette.Ok, 74f);
                GUILayout.Space(4f);
                Pill($"{_validating} VALIDATING", _validating > 0 ? FrogletEditorPalette.Warn : FrogletEditorPalette.Muted, 104f);
                GUILayout.Space(4f);
                Pill($"{_ignored} IGNORED", FrogletEditorPalette.Muted, 86f);

                GUILayout.FlexibleSpace();

                if (FrogletEditorPalette.ColorButton("Refresh", FrogletEditorPalette.Info, 66f, 20f,
                        "Re-scan BugLedger/issues/ — picks up pulls and teammates' pushes.", outline: true))
                    defer(BugLedger.RefreshFromDisk);
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Open Folder", FrogletEditorPalette.Info, 90f, 20f,
                        "Reveal the committable BugLedger/ store in the file browser.", outline: true))
                    defer(RevealStore);
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton(_showNewForm ? "Cancel" : "+ New Bug", FrogletEditorPalette.Info, 84f, 20f,
                        "File a custom bug by hand — for anything auto-capture cannot see.", outline: !_showNewForm))
                {
                    _showNewForm = !_showNewForm;
                    GUI.FocusControl(null);
                }
                GUILayout.Space(6f);
            }
            GUILayout.Space(4f);
        }

        static void Pill(string label, Color accent, float width)
        {
            var rect = GUILayoutUtility.GetRect(width, 18f, GUILayout.Width(width));
            FrogletEditorPalette.StatusPill(rect, label, accent);
        }

        void DrawCaptureLine(Action<Action> defer)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                var line = BugLedger.AutoCaptureEnabled
                    ? "Auto-capture ON — every distinct error/exception/assert files itself; a fixed issue closes only after the game proves it stayed silent."
                    : "Auto-capture OFF — bugs are filed by hand only, and auto-validation is suspended (a clean session proves nothing while nothing listens).";
                GUILayout.Label(line, FrogletEditorPalette.Subtitle);
                GUILayout.FlexibleSpace();
                if (FrogletEditorPalette.ColorButton("Test Capture", FrogletEditorPalette.Warn, 92f, 20f,
                        "Logs one synthetic error so you can watch it get filed, fixed and validated.",
                        enabled: BugLedger.AutoCaptureEnabled, outline: true))
                    defer(() => Debug.LogError("[BugLedgerTest] Synthetic error — file me, mark me fixed, then watch validation close me."));
                GUILayout.Space(6f);
            }
        }

        // ── New bug form ─────────────────────────────────────────────────────────

        void DrawNewBugForm()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label("New bug", FrogletEditorPalette.SectionHeader);
                GUILayout.Label($"filed as {Environment.UserName} · no signature, so it resolves manually",
                    FrogletEditorPalette.Subtitle);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope())
                {
                    _newTitle = EditorGUILayout.TextField("Title", _newTitle);
                    GUILayout.Label("Notes (repro steps, scene, what 'working' looks like):", FrogletEditorPalette.Subtitle);
                    _newNotes = EditorGUILayout.TextArea(_newNotes, GUILayout.MinHeight(48f));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (FrogletEditorPalette.ColorButton("File Bug", FrogletEditorPalette.Ok, 76f, 20f,
                                enabled: !string.IsNullOrWhiteSpace(_newTitle)))
                        {
                            BugLedger.ReportCustom(_newTitle, _newNotes);
                            _newTitle = "";
                            _newNotes = "";
                            _showNewForm = false;
                            GUI.FocusControl(null);
                        }
                    }
                }
                GUILayout.Space(8f);
            }
            GUILayout.Space(6f);
        }

        // ── Issue list ───────────────────────────────────────────────────────────

        void DrawIssues(Action<Action> defer)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Issues ({_sorted.Count})", FrogletEditorPalette.SectionHeader);
            }

            if (_sorted.Count == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8f);
                    GUILayout.Label("Ledger is clear — nothing tracked.", FrogletEditorPalette.Subtitle);
                }
                return;
            }

            foreach (var issue in _sorted)
            {
                DrawIssueRow(issue, defer);
                if (_expandedId == issue.Id) DrawIssueDetails(issue, defer);
                GUILayout.Space(2f);
            }
        }

        void DrawIssueRow(BugLedgerIssue issue, Action<Action> defer)
        {
            var accent = issue.IsOpen ? FrogletEditorPalette.Error
                       : issue.IsValidating ? FrogletEditorPalette.Warn
                       : FrogletEditorPalette.Muted;

            var rect = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
            rect = new Rect(rect.x + 6f, rect.y + 1f, rect.width - 12f, rect.height - 3f);
            FrogletEditorPalette.DrawCard(rect, FrogletEditorPalette.Surface,
                FrogletEditorPalette.Muted.WithAlpha(0.25f));
            FrogletEditorPalette.DrawAccentStripe(rect, accent);

            float buttonsWidth = ButtonsWidth(issue);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 3f, rect.width - buttonsWidth - 20f, 16f),
                issue.Title, FrogletEditorPalette.CardTitle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 19f, rect.width - buttonsWidth - 20f, 14f),
                MetaLine(issue), FrogletEditorPalette.CardBody);

            float x = rect.xMax - buttonsWidth;
            float y = rect.y + 8f;

            bool expanded = _expandedId == issue.Id;
            if (FrogletEditorPalette.ColorButton(new Rect(x, y, 26f, 20f), expanded ? "▾" : "▸",
                    FrogletEditorPalette.Info, "Details, notes, and the captured sample.", outline: true))
            {
                _expandedId = expanded ? null : issue.Id;
                GUI.FocusControl(null);
            }
            x += 30f;

            var id = issue.Id;
            if (issue.IsOpen)
            {
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 44f, 20f), "Fix", FrogletEditorPalette.Ok,
                        issue.HasSignature
                            ? "Mark fixed. The issue moves to VALIDATING and closes itself only once the error stays silent for its clean-session quota."
                            : "No signature to auto-validate — resolves after a confirm."))
                    defer(() => MarkFixedFlow(issue));
                x += 48f;
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 52f, 20f), "Ignore", FrogletEditorPalette.Muted,
                        "Park it. Matching errors never reopen it, and the same signature is never re-filed.", outline: true))
                    BugLedger.Ignore(id);
                x += 56f;
            }
            else if (issue.IsValidating)
            {
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 56f, 20f), "Reopen", FrogletEditorPalette.Warn,
                        "Back to OPEN (clean-session progress resets).", outline: true))
                    BugLedger.Reopen(id);
                x += 60f;
                bool paused = issue.ValidationPaused;
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 26f, 20f), paused ? "▶" : "❚❚",
                        paused ? FrogletEditorPalette.Ok : FrogletEditorPalette.Warn,
                        paused ? "Resume auto-validation for this issue."
                               : "Pause auto-validation — nothing counts for or against it until resumed.",
                        outline: true))
                    BugLedger.SetValidationPaused(id, !paused);
                x += 30f;
            }
            else // ignored
            {
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 56f, 20f), "Reopen", FrogletEditorPalette.Warn,
                        "Un-park it — back to OPEN.", outline: true))
                    BugLedger.Reopen(id);
                x += 60f;
            }

            if (FrogletEditorPalette.ColorButton(new Rect(x, y, 26f, 20f), "✕", FrogletEditorPalette.Error,
                    "Delete this issue outright (no validation, file removed).", outline: true))
                defer(() =>
                {
                    if (EditorUtility.DisplayDialog("Delete issue",
                            $"Delete \"{issue.Title}\"?\n\nThe ledger file is removed. If the error is real, auto-capture will file it again next time it fires.",
                            "Delete", "Cancel"))
                        BugLedger.Delete(id);
                });
        }

        static float ButtonsWidth(BugLedgerIssue issue)
            => issue.IsOpen ? 30f + 48f + 56f + 30f
             : issue.IsValidating ? 30f + 60f + 30f + 30f
             : 30f + 60f + 30f;

        static string MetaLine(BugLedgerIssue issue)
        {
            var parts = new List<string>(8) { issue.Id, issue.Kind };
            if (!string.IsNullOrEmpty(issue.LogType)) parts.Add(issue.LogType);
            if (issue.TimesSeen > 0) parts.Add($"seen {issue.TimesSeen}×");
            if (!string.IsNullOrEmpty(issue.LastSeenUtc)) parts.Add($"last {Local(issue.LastSeenUtc)}");
            if (issue.IsValidating)
            {
                parts.Add($"clean {issue.CleanSessions}/{issue.CleanSessionsRequired} ({ScopeWord(issue)})");
                if (issue.ValidationPaused) parts.Add("validation paused");
            }
            if (issue.Regressions > 0) parts.Add($"{issue.Regressions} regression{(issue.Regressions == 1 ? "" : "s")}");
            return string.Join(" · ", parts);
        }

        static string ScopeWord(BugLedgerIssue issue)
            => issue.Scope == "PlayMode" ? "play runs"
             : issue.Scope == "EditMode" ? "editor sessions"
             : "manual";

        void DrawIssueDetails(BugLedgerIssue issue, Action<Action> defer)
        {
            if (_notesDraftId != issue.Id)
            {
                _notesDraftId = issue.Id;
                _notesDraft = issue.Notes ?? "";
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(18f);
                using (new EditorGUILayout.VerticalScope())
                {
                    if (!string.IsNullOrEmpty(issue.Sample))
                        GUILayout.Label(issue.Sample, FrogletEditorPalette.CardBodyWrapped);
                    if (!string.IsNullOrEmpty(issue.Stack))
                        GUILayout.Label(issue.Stack, FrogletEditorPalette.CardBodyWrapped);

                    var info = $"filed by {issue.Reporter} on {issue.Machine} · created {Local(issue.CreatedUtc)}";
                    if (!string.IsNullOrEmpty(issue.FixedBy))
                        info += $" · marked fixed by {issue.FixedBy} {Local(issue.FixedUtc)}";
                    GUILayout.Label(info, FrogletEditorPalette.Subtitle);

                    GUILayout.Label("Notes:", FrogletEditorPalette.Subtitle);
                    _notesDraft = EditorGUILayout.TextArea(_notesDraft, GUILayout.MinHeight(40f));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool dirty = _notesDraft != (issue.Notes ?? "");
                        if (FrogletEditorPalette.ColorButton("Save Notes", FrogletEditorPalette.Ok, 84f, 20f,
                                enabled: dirty, outline: true))
                            BugLedger.SaveNotes(issue.Id, _notesDraft);
                        GUILayout.Space(4f);
                        if (FrogletEditorPalette.ColorButton("Resolve Now", FrogletEditorPalette.Warn, 92f, 20f,
                                "Close and delete this issue immediately, bypassing validation.", outline: true))
                        {
                            var id = issue.Id;
                            var title = issue.Title;
                            defer(() =>
                            {
                                if (EditorUtility.DisplayDialog("Resolve without validation",
                                        $"Close \"{title}\" now?\n\nThe ledger file is deleted without waiting for clean sessions.",
                                        "Resolve", "Cancel"))
                                    BugLedger.ResolveNow(id);
                            });
                        }
                        GUILayout.Space(4f);
                        if (FrogletEditorPalette.ColorButton("Show File", FrogletEditorPalette.Info, 76f, 20f,
                                "Reveal this issue's JSON in the file browser — commit it to share the bug.", outline: true))
                        {
                            var id = issue.Id;
                            defer(() => EditorUtility.RevealInFinder(BugLedger.IssuePath(id)));
                        }
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.Space(4f);
                }
                GUILayout.Space(12f);
            }
        }

        // ── Settings ─────────────────────────────────────────────────────────────

        void DrawSettings(Action<Action> defer)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label("Settings (machine-local, UserSettings/)", FrogletEditorPalette.SectionHeader);
            }

            var settings = BugLedgerSettings.instance;
            EditorGUI.BeginChangeCheck();
            bool autoCapture = EditorGUILayout.ToggleLeft(
                new GUIContent("  Auto-capture errors into the ledger",
                    "File an issue for every distinct error/exception/assert signature. Also gates validation."),
                settings.AutoCaptureEnabled);
            int cleanRequired = EditorGUILayout.IntSlider(
                new GUIContent("Clean sessions to close",
                    "Stamped into each NEW issue at creation; existing issues keep the quota they were filed with."),
                settings.DefaultCleanSessionsRequired, 1, 10);
            int minPlay = EditorGUILayout.IntSlider(
                new GUIContent("Min play run (seconds)",
                    "A shorter play run is not evidence and credits nothing."),
                settings.MinValidationPlaySeconds, 0, 300);
            int minEditor = EditorGUILayout.IntSlider(
                new GUIContent("Min editor session (minutes)",
                    "Editor sessions shorter than this don't credit edit-mode-scoped issues at quit."),
                settings.MinEditorSessionMinutes, 0, 120);
            int maxAuto = EditorGUILayout.IntSlider(
                new GUIContent("Auto-issue cap",
                    "A runaway error generator must not mint files; past the cap new signatures are dropped with one warning."),
                settings.MaxAutoIssues, 10, 1000);

            if (EditorGUI.EndChangeCheck())
            {
                settings.DefaultCleanSessionsRequired = cleanRequired;
                settings.MinValidationPlaySeconds = minPlay;
                settings.MinEditorSessionMinutes = minEditor;
                settings.MaxAutoIssues = maxAuto;
                settings.SaveNow();
                BugLedger.ApplySettings(settings);

                if (autoCapture != BugLedger.AutoCaptureEnabled)
                    defer(() => BugLedger.SetAutoCaptureEnabled(autoCapture));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label(
                    "How validation works: mark an issue Fixed and it turns VALIDATING. Each qualifying session where its " +
                    "error stays silent (a play run for play-mode bugs, a full editor session for edit-mode ones) counts one " +
                    "clean session; at its quota the issue closes and its file is deleted. If the error recurs, it reopens as a regression.",
                    FrogletEditorPalette.Subtitle);
                GUILayout.Space(8f);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        void RevealStore()
        {
            var readme = System.IO.Path.Combine(BugLedger.RootDir, "README.md");
            if (System.IO.File.Exists(readme)) EditorUtility.RevealInFinder(readme);
            else if (System.IO.Directory.Exists(BugLedger.IssuesDir)) EditorUtility.RevealInFinder(BugLedger.IssuesDir);
            else EditorUtility.RevealInFinder(BugLedger.RootDir);
        }

        void MarkFixedFlow(BugLedgerIssue issue)
        {
            if (issue.HasSignature)
            {
                BugLedger.MarkFixed(issue.Id);
                return;
            }
            if (EditorUtility.DisplayDialog("Resolve custom bug",
                    $"\"{issue.Title}\" has no error signature, so the game cannot auto-validate it.\n\nResolve and delete it now?",
                    "Resolve", "Cancel"))
                BugLedger.ResolveNow(issue.Id);
        }

        static string Local(string isoUtc)
        {
            if (DateTime.TryParse(isoUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var utc))
                return utc.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
            return "—";
        }
    }
}
