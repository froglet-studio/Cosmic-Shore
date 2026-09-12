using System;
using System.Collections.Generic;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// The Stage &amp; Push tab of <see cref="DiagnosticsWindow"/> — the ledger's own version
    /// control surface, over <see cref="BugLedgerPublisher"/>.
    ///
    /// Day-to-day ledger writes live in the gitignored <c>BugLedger/local/</c> store, so git never
    /// sees them. This tab shows the DIFFERENCE between that local store and the published
    /// <c>shared/</c> set as pending changes (ADD / MOD / DEL); the human stages a selection with
    /// the [+]/[−] buttons, writes a commit comment, and Commit &amp; Push applies exactly the
    /// staged files into <c>shared/</c>, then adds, commits and pushes ONLY those ledger paths —
    /// with a step progress bar while it runs. Staging is a UI selection until that moment:
    /// nothing touches a tracked file while the human is still choosing.
    /// </summary>
    sealed class BugLedgerStageView
    {
        const string StagedKey = "BugLedger.StagedIds";
        const string CommentKey = "BugLedger.StageComment";
        const string DefaultComment = "bugledger: update";

        Vector2 _scroll;
        readonly HashSet<string> _staged = new(StringComparer.OrdinalIgnoreCase);
        string _comment;
        bool _restored;
        bool _opWasRunning;

        int _lastStamp = -1;
        List<BugLedger.BugLedgerPendingChange> _pending = new();

        public bool NeedsRepaint => _lastStamp != BugLedger.ChangeStamp || BugLedgerPublisher.IsBusy;

        public void Draw(Action<Action> defer)
        {
            RestoreOnce();
            SyncPending();

            if (!BugLedgerPublisher.GitAvailable)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "git was not found on PATH — staging still works conceptually (the ledger stays local), " +
                    "but publishing needs the git CLI this editor can run.",
                    MessageType.Warning);
                return;
            }

            DrawStatusRow();
            DrawOperationBar();
            FrogletEditorPalette.HorizontalRule();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                DrawIncoming();
                DrawPending();
                FrogletEditorPalette.HorizontalRule();
                DrawCommitRow(defer);
                GUILayout.Space(8f);
            }
            EditorGUILayout.EndScrollView();
        }

        void RestoreOnce()
        {
            if (_restored) return;
            _restored = true;
            BugLedgerPublisher.Warm();
            var stored = SessionState.GetString(StagedKey, "");
            foreach (var id in stored.Split('\n'))
                if (id.Length > 0) _staged.Add(id);
            _comment = SessionState.GetString(CommentKey, DefaultComment);
        }

        void SyncPending()
        {
            int stamp = BugLedger.ChangeStamp;
            bool opJustFinished = _opWasRunning && !BugLedgerPublisher.IsBusy;
            _opWasRunning = BugLedgerPublisher.IsBusy;
            if (stamp == _lastStamp && !opJustFinished) return;
            _lastStamp = stamp;

            _pending = BugLedger.ComputePendingChanges();

            // A change that no longer exists (pushed, or the issue resolved meanwhile) leaves
            // the staged selection on its own.
            var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var change in _pending) live.Add(change.Id);
            _staged.RemoveWhere(id => !live.Contains(id));
            PersistStaged();
        }

        void PersistStaged()
            => SessionState.SetString(StagedKey, string.Join("\n", _staged));

        // ── Status row ───────────────────────────────────────────────────────────

        void DrawStatusRow()
        {
            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6f);
                var pendRect = GUILayoutUtility.GetRect(88f, 18f, GUILayout.Width(88f));
                FrogletEditorPalette.StatusPill(pendRect, $"{_pending.Count} PENDING",
                    _pending.Count > 0 ? FrogletEditorPalette.Info : FrogletEditorPalette.Ok);
                GUILayout.Space(4f);
                var stagedRect = GUILayoutUtility.GetRect(84f, 18f, GUILayout.Width(84f));
                FrogletEditorPalette.StatusPill(stagedRect, $"{_staged.Count} STAGED",
                    _staged.Count > 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Muted);

                GUILayout.Space(8f);
                var branch = BugLedgerPublisher.CanPush ? $"on {BugLedgerPublisher.Branch}" : "no pushable branch (detached HEAD?)";
                var remote = BugLedgerPublisher.Behind switch
                {
                    < 0 => "",
                    0 => " · in sync with origin",
                    _ => $" · origin is AHEAD by {BugLedgerPublisher.Behind}",
                };
                GUILayout.Label(branch + remote, FrogletEditorPalette.Subtitle);

                GUILayout.FlexibleSpace();

                bool idle = !BugLedgerPublisher.IsBusy;
                if (FrogletEditorPalette.ColorButton("Fetch", FrogletEditorPalette.Info, 54f, 20f,
                        "git fetch origin, then show how this branch compares and which ledger changes are incoming. Read-only.",
                        enabled: idle, outline: true))
                    BugLedgerPublisher.StartFetch();
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Stage All", FrogletEditorPalette.Ok, 74f, 20f,
                        enabled: idle && _pending.Count > 0 && _staged.Count < _pending.Count, outline: true))
                {
                    foreach (var change in _pending) _staged.Add(change.Id);
                    PersistStaged();
                }
                GUILayout.Space(4f);
                if (FrogletEditorPalette.ColorButton("Unstage All", FrogletEditorPalette.Warn, 84f, 20f,
                        enabled: idle && _staged.Count > 0, outline: true))
                {
                    _staged.Clear();
                    PersistStaged();
                }
                GUILayout.Space(6f);
            }
            GUILayout.Space(4f);
        }

        // ── The progress bar (any VCS client's fetch/push readout) ───────────────

        void DrawOperationBar()
        {
            var op = BugLedgerPublisher.Current;
            if (op == null) return;

            if (op.Running)
            {
                var rect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
                rect = new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height);
                EditorGUI.ProgressBar(rect, op.Progress, $"{op.Title}: {op.Step}");
                GUILayout.Space(2f);
                return;
            }

            if (string.IsNullOrEmpty(op.Result)) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6f);
                var pill = GUILayoutUtility.GetRect(44f, 16f, GUILayout.Width(44f));
                FrogletEditorPalette.StatusPill(pill, op.Failed ? "FAIL" : "OK",
                    op.Failed ? FrogletEditorPalette.Error : FrogletEditorPalette.Ok);
                GUILayout.Space(6f);
                GUILayout.Label(op.Result, FrogletEditorPalette.CardBodyWrapped);
                GUILayout.Space(6f);
            }
            GUILayout.Space(2f);
        }

        // ── Incoming (from the last fetch) ───────────────────────────────────────

        void DrawIncoming()
        {
            var incoming = BugLedgerPublisher.IncomingChanges;
            if (incoming == null || incoming.Count == 0) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Incoming from origin ({incoming.Count}) — lands on your next pull",
                    FrogletEditorPalette.SectionHeader);
            }
            foreach (var line in incoming)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(16f);
                    GUILayout.Label(line, FrogletEditorPalette.CardBody);
                }
            }
            GUILayout.Space(6f);
        }

        // ── Pending changes ──────────────────────────────────────────────────────

        void DrawPending()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label($"Pending changes ({_pending.Count})", FrogletEditorPalette.SectionHeader);
                GUILayout.Label("local ledger vs the published shared/ set", FrogletEditorPalette.Subtitle);
            }

            if (_pending.Count == 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8f);
                    GUILayout.Label("Everything published — the local ledger and shared/ agree.",
                        FrogletEditorPalette.Subtitle);
                }
                return;
            }

            bool idle = !BugLedgerPublisher.IsBusy;
            foreach (var change in _pending)
            {
                var rect = GUILayoutUtility.GetRect(0f, 30f, GUILayout.ExpandWidth(true));
                rect = new Rect(rect.x + 6f, rect.y + 1f, rect.width - 12f, rect.height - 3f);
                bool staged = _staged.Contains(change.Id);
                var accent = ChangeColor(change.Kind);

                FrogletEditorPalette.DrawCard(rect,
                    staged ? FrogletEditorPalette.SurfaceRaised : FrogletEditorPalette.Surface,
                    staged ? FrogletEditorPalette.Ok.WithAlpha(0.55f) : FrogletEditorPalette.Muted.WithAlpha(0.25f));
                FrogletEditorPalette.DrawAccentStripe(rect, accent);

                var badge = new Rect(rect.x + 8f, rect.y + 6f, 42f, 16f);
                FrogletEditorPalette.StatusPill(badge, ChangeLabel(change.Kind), accent);

                GUI.Label(new Rect(rect.x + 56f, rect.y + 2f, rect.width - 160f, 15f),
                    change.Title, FrogletEditorPalette.CardTitle);
                GUI.Label(new Rect(rect.x + 56f, rect.y + 15f, rect.width - 160f, 13f),
                    change.Id + (staged ? " · staged" : ""), FrogletEditorPalette.CardBody);

                var toggle = new Rect(rect.xMax - 34f, rect.y + 4f, 26f, 20f);
                if (staged)
                {
                    if (FrogletEditorPalette.ColorButton(toggle, "−", FrogletEditorPalette.Warn,
                            "Unstage — keep the change local for now.", enabled: idle, outline: true))
                    {
                        _staged.Remove(change.Id);
                        PersistStaged();
                    }
                }
                else if (FrogletEditorPalette.ColorButton(toggle, "+", FrogletEditorPalette.Ok,
                             "Stage for the next commit & push.", enabled: idle))
                {
                    _staged.Add(change.Id);
                    PersistStaged();
                }
                GUILayout.Space(2f);
            }
        }

        static Color ChangeColor(BugLedger.BugLedgerChangeKind kind) => kind switch
        {
            BugLedger.BugLedgerChangeKind.Add => FrogletEditorPalette.Ok,
            BugLedger.BugLedgerChangeKind.Remove => FrogletEditorPalette.Error,
            _ => FrogletEditorPalette.Warn,
        };

        static string ChangeLabel(BugLedger.BugLedgerChangeKind kind) => kind switch
        {
            BugLedger.BugLedgerChangeKind.Add => "ADD",
            BugLedger.BugLedgerChangeKind.Remove => "DEL",
            _ => "MOD",
        };

        // ── Commit & push ────────────────────────────────────────────────────────

        void DrawCommitRow(Action<Action> defer)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label("Commit comment", FrogletEditorPalette.SectionHeader);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                EditorGUI.BeginChangeCheck();
                _comment = EditorGUILayout.TextArea(_comment, GUILayout.MinHeight(34f));
                if (EditorGUI.EndChangeCheck())
                    SessionState.SetString(CommentKey, _comment);
                GUILayout.Space(8f);
            }

            GUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label(
                    "Pushes ONLY files under BugLedger/ — never anything else in your working tree, staged or not.",
                    FrogletEditorPalette.Subtitle);
                GUILayout.FlexibleSpace();

                bool canPush = !BugLedgerPublisher.IsBusy && _staged.Count > 0 && BugLedgerPublisher.CanPush;
                if (FrogletEditorPalette.ColorButton($"Commit & Push ({_staged.Count})", FrogletEditorPalette.Ok,
                        150f, 24f,
                        BugLedgerPublisher.CanPush
                            ? $"Apply the staged changes into shared/, then git add + commit + push to origin/{BugLedgerPublisher.Branch} — ledger paths only."
                            : "No pushable branch (detached HEAD?).",
                        enabled: canPush))
                {
                    var ids = new List<string>(_staged);
                    var message = string.IsNullOrWhiteSpace(_comment) ? DefaultComment : _comment.Trim();
                    defer(() =>
                    {
                        if (EditorUtility.DisplayDialog("Publish bug ledger changes",
                                $"Commit {ids.Count} staged change(s) and push to origin/{BugLedgerPublisher.Branch}?\n\n" +
                                $"\"{FirstLine(message)}\"\n\n" +
                                "Only files under BugLedger/ are staged, committed and pushed — nothing else in your working tree is touched.",
                                "Commit & Push", "Cancel"))
                            BugLedgerPublisher.StartPublish(ids, message);
                    });
                }
                GUILayout.Space(6f);
            }
        }

        static string FirstLine(string s)
        {
            int newline = s.IndexOf('\n');
            return newline >= 0 ? s[..newline] : s;
        }
    }
}
