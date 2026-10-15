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
        int _newSeverityIndex = 1;

        static readonly string[] SeverityOptions = { BugLedgerIssueSeverity.Blocker, BugLedgerIssueSeverity.Major, BugLedgerIssueSeverity.Minor };

        int _lastStamp = -1;
        List<BugLedgerIssue> _sorted = new();
        int _open, _validating, _ignored, _pending;

        /// <summary>True when the ledger changed since the last draw — the host should Repaint.</summary>
        public bool NeedsRepaint => _lastStamp != BugLedger.ChangeStamp;

        public void Draw(Action<Action> defer, Action openStageTab)
        {
            SyncFromLedger();

            DrawStatusRow(defer, openStageTab);
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
            _pending = BugLedger.ComputePendingChanges().Count;
        }

        static int CompareIssues(BugLedgerIssue a, BugLedgerIssue b)
        {
            int rankA = StateRank(a);
            int rankB = StateRank(b);
            if (rankA != rankB) return rankA.CompareTo(rankB);
            int sev = BugLedgerIssueSeverity.Rank(a.Severity).CompareTo(BugLedgerIssueSeverity.Rank(b.Severity));
            if (sev != 0) return sev;
            // ISO-8601 UTC strings sort lexicographically; newest activity first within a state.
            int seen = string.CompareOrdinal(b.LastSeenUtc, a.LastSeenUtc);
            return seen != 0 ? seen : string.CompareOrdinal(b.CreatedUtc, a.CreatedUtc);
        }

        static int StateRank(BugLedgerIssue issue)
            => issue.IsOpen ? 0 : issue.IsValidating ? 1 : 2;

        // ── Status row ───────────────────────────────────────────────────────────

        void DrawStatusRow(Action<Action> defer, Action openStageTab)
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
                if (_pending > 0)
                {
                    GUILayout.Space(4f);
                    var pendRect = GUILayoutUtility.GetRect(112f, 18f, GUILayout.Width(112f));
                    FrogletEditorPalette.StatusPill(pendRect, $"{_pending} UNPUBLISHED", FrogletEditorPalette.Info);
                    if (GUI.Button(pendRect, new GUIContent("",
                            "Changes the team has not got yet (new bugs, notes, fixes, closes). Press Share with team to send them all, or stage them one by one in the Stage & Push tab."),
                            GUIStyle.none))
                        openStageTab();
                }

                GUILayout.FlexibleSpace();

                if (FrogletEditorPalette.ColorButton("Refresh", FrogletEditorPalette.Info, 66f, 20f,
                        "Re-read the bug files from disk. Press it after a git pull to see teammates' bugs and the fixes Claude or others pushed.", outline: true))
                    defer(BugLedger.RefreshFromDisk);
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Open Folder", FrogletEditorPalette.Info, 90f, 20f,
                        "Open the BugLedger/ folder in your file browser: one small JSON file per bug.", outline: true))
                    defer(RevealStore);
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton(_showNewForm ? "Cancel" : "+ New Bug", FrogletEditorPalette.Info, 84f, 20f,
                        "Write a bug by hand, for problems that print no red error: wrong behaviour, a visual glitch, a stuck menu. Add steps to reproduce in the notes.", outline: !_showNewForm))
                {
                    _showNewForm = !_showNewForm;
                    GUI.FocusControl(null);
                }
                GUILayout.Space(4f);
                // The ONE button a reporter needs: everything below it (Fix, Ignore, Stage & Push) is for
                // whoever fixes the code. Same scoped publish as the check-in's Share now.
                if (FrogletEditorPalette.ColorButton("Share with team", FrogletEditorPalette.Ok, 118f, 20f,
                        "Send these bugs, plus your new redlog / crash / performance files, to the team in one " +
                        "commit (BugLedger/ files only). The daily Claude sweep reads and fixes them."))
                    defer(BugHuntCheckIn.ShareWithTeam);
                GUILayout.Space(6f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label(
                    "OPEN: not fixed yet.   VALIDATING: someone fixed it; it closes itself after clean play runs, or reopens " +
                    "if the error comes back.   IGNORED: parked on purpose.   To report bugs, press Share with team. Hover any " +
                    "button for what it does.",
                    EditorStyles.wordWrappedMiniLabel);
                GUILayout.Space(8f);
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
                        "Logs one fake red error so you can watch how the ledger files it. Delete it afterwards with the X button.",
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
                    _newSeverityIndex = EditorGUILayout.Popup("Severity", _newSeverityIndex, SeverityOptions);
                    GUILayout.Label("Notes (repro steps, scene, what 'working' looks like):", FrogletEditorPalette.Subtitle);
                    _newNotes = EditorGUILayout.TextArea(_newNotes, GUILayout.MinHeight(48f));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (FrogletEditorPalette.ColorButton("File Bug", FrogletEditorPalette.Ok, 76f, 20f,
                                enabled: !string.IsNullOrWhiteSpace(_newTitle)))
                        {
                            BugLedger.ReportCustom(_newTitle, _newNotes, SeverityOptions[_newSeverityIndex]);
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
            GUI.Label(new Rect(rect.x + 10f, rect.y + 3f, rect.width - buttonsWidth - 84f, 16f),
                issue.Title, FrogletEditorPalette.CardTitle);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 19f, rect.width - buttonsWidth - 84f, 14f),
                new GUIContent(MetaLine(issue), MetaTooltip(issue)), FrogletEditorPalette.CardBody);

            // Severity pill — click cycles blocker → major → minor.
            var sevRect = new Rect(rect.xMax - buttonsWidth - 64f, rect.y + 9f, 58f, 18f);
            FrogletEditorPalette.StatusPill(sevRect, issue.Severity.ToUpperInvariant(), SeverityColor(issue.Severity));
            if (GUI.Button(sevRect, new GUIContent("", "How bad is it? Click to change.\nBLOCKER: you cannot play or test.\nMAJOR: wrong, but the game still works.\nMINOR: cosmetic.\nThe daily sweep fixes the worst first."), GUIStyle.none))
                BugLedger.SetSeverity(issue.Id, BugLedgerIssueSeverity.Next(issue.Severity));

            float x = rect.xMax - buttonsWidth;
            float y = rect.y + 8f;

            bool expanded = _expandedId == issue.Id;
            if (FrogletEditorPalette.ColorButton(new Rect(x, y, 26f, 20f), expanded ? "▾" : "▸",
                    FrogletEditorPalette.Info, "Show the details: the full console message, the code it came from, who hit it and when, and the notes (including what any fix changed).", outline: true))
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
                            ? $"I CHANGED THE CODE that fixes this. It moves to VALIDATING: it closes itself after {issue.CleanSessionsRequired} {ScopeWord(issue)} without the error, and reopens if the error comes back.\n\nJust reporting it? Do not press this. Use Share with team."
                            : "No signature to auto-validate — resolves after a confirm."))
                    defer(() => MarkFixedFlow(issue));
                x += 48f;
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 52f, 20f), "Ignore", FrogletEditorPalette.Muted,
                        "Park it: not a real bug, or not worth fixing now (for example a tool missing on your machine). It stays listed as IGNORED, never reopens and is never filed again. Reopen un-parks it.", outline: true))
                    BugLedger.Ignore(id);
                x += 56f;
            }
            else if (issue.IsValidating)
            {
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 56f, 20f), "Reopen", FrogletEditorPalette.Warn,
                        "The fix did not work, or it was marked fixed by mistake. Puts it back to OPEN so it gets fixed; the clean-run count starts over.", outline: true))
                    BugLedger.Reopen(id);
                x += 60f;
                bool paused = issue.ValidationPaused;
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 26f, 20f), paused ? "▶" : "❚❚",
                        paused ? FrogletEditorPalette.Ok : FrogletEditorPalette.Warn,
                        paused ? "Resume counting: play runs without this error count toward closing it again."
                               : "Pause the clean-run count. Use it when your next play runs will not reach the code where this bug happens (a Skein bug while you test the menu), so they do not count as proof it is fixed. Nothing counts for or against it until you resume.",
                        outline: true))
                    BugLedger.SetValidationPaused(id, !paused);
                x += 30f;
            }
            else // ignored
            {
                if (FrogletEditorPalette.ColorButton(new Rect(x, y, 56f, 20f), "Reopen", FrogletEditorPalette.Warn,
                        "Un-park it: back to OPEN, so it gets fixed and the error is tracked again.", outline: true))
                    BugLedger.Reopen(id);
                x += 60f;
            }

            if (FrogletEditorPalette.ColorButton(new Rect(x, y, 26f, 20f), "✕", FrogletEditorPalette.Error,
                    "Delete it outright. Use it for junk: a test error or a duplicate. If the error is real it is filed again the next time it happens. To report a bug do not delete it: Share with team.", outline: true))
                defer(() =>
                {
                    if (EditorUtility.DisplayDialog("Delete issue",
                            $"Delete \"{issue.Title}\"?\n\nThe ledger file is removed. If the error is real, auto-capture will file it again next time it fires.",
                            "Delete", "Cancel"))
                        BugLedger.Delete(id);
                });
        }

        static Color SeverityColor(string severity) => severity switch
        {
            BugLedgerIssueSeverity.Blocker => FrogletEditorPalette.Error,
            BugLedgerIssueSeverity.Minor => FrogletEditorPalette.Muted,
            _ => FrogletEditorPalette.Warn,
        };

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

        static string MetaTooltip(BugLedgerIssue issue)
        {
            var tip = "ID (the same bug has the same ID on every machine) · auto = caught from the console, custom = written by hand, tool = found by an audit tool · how many times it fired · when it last fired.";
            if (issue.IsValidating)
                tip += $"\n\nclean {issue.CleanSessions}/{issue.CleanSessionsRequired}: someone marked it fixed. Each {ScopeWord(issue).TrimEnd('s')} that ends without this error counts one; at {issue.CleanSessionsRequired} it closes itself. If the error fires again it reopens.";
            if (issue.Regressions > 0)
                tip += "\n\nregressions: how many times it came back after being marked fixed.";
            return tip;
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
                                "Save your notes: steps to reproduce, the mode and vessel, what you tried. They travel with the bug on the next Share.",
                                enabled: dirty, outline: true))
                            BugLedger.SaveNotes(issue.Id, _notesDraft);
                        GUILayout.Space(4f);
                        if (FrogletEditorPalette.ColorButton("Resolve Now", FrogletEditorPalette.Warn, 92f, 20f,
                                "Close it now, without waiting for clean play runs. Only when you are sure it is fixed or no longer matters.", outline: true))
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
                                "Reveal this issue's local JSON in the file browser. Sharing goes through the Stage & Push tab.", outline: true))
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
            bool autoPublish = EditorGUILayout.ToggleLeft(
                new GUIContent("  Auto-publish after each play test (pushes to the current branch)",
                    "After a play run that filed a new error, reopened a regression or closed an issue, push every " +
                    "pending ledger change — the same scoped commit Stage & Push makes, ledger paths only, refused " +
                    "while origin is ahead. A Claude session running /bug-ledger then reads and fixes them; its " +
                    "fixes come back as VALIDATING issues the next play runs close."),
                settings.AutoPublishAfterPlay);
            int checkInHours = EditorGUILayout.IntSlider(
                new GUIContent("Bug Hunt check-in (hours)",
                    "At most this often, on load or after a play test, ask whether to share new bugs and report " +
                    "files (redlog, crash, perf JSONs) — only when there is something new. 0 = never ask. " +
                    "FrogletTools > Diagnostics > Bug Hunt Check-in asks now."),
                settings.CheckInReminderHours, 0, 168);

            if (EditorGUI.EndChangeCheck())
            {
                settings.DefaultCleanSessionsRequired = cleanRequired;
                settings.MinValidationPlaySeconds = minPlay;
                settings.MinEditorSessionMinutes = minEditor;
                settings.MaxAutoIssues = maxAuto;
                settings.AutoPublishAfterPlay = autoPublish;
                settings.CheckInReminderHours = checkInHours;
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
                    "clean session; at its quota the issue closes — archived to BugLedger/local/resolved/, removed from the " +
                    "live ledger. Tool-filed findings validate differently: a full clean re-run of the tool that filed them " +
                    "closes them. If an error recurs, its issue reopens as a regression. Everything here is machine-local " +
                    "until you publish it — version control only ever sees what you stage and push from the Stage & Push " +
                    "tab. Full doc: Docs/DIAGNOSTICS.md.",
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
                // A reporter pressing Fix to "send" a bug starts the validation clock on a bug nobody
                // fixed, and an intermittent error can then close itself after two lucky runs.
                if (EditorUtility.DisplayDialog("Mark as fixed?",
                        $"\"{issue.Title}\"\n\nOnly mark it fixed if you changed the code or data that fixes it. " +
                        "It then closes itself after its clean play runs, so an unfixed bug can disappear.\n\n" +
                        "Just reporting it? Press Cancel, then Share with team.",
                        "I fixed it", "Cancel"))
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
