using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// FrogletTools ▸ Build ▸ <b>Pending Tool Changes</b> — the last gate before a branch ships.
    ///
    /// <para>Editor tools produce DATA: a rewritten scene, a re-authored prefab, a new SO. That
    /// data lands in the working tree, not in the branch, and the failure mode is silent — the
    /// tool gets committed, its output does not, the PR merges, and the feature is broken on
    /// every other machine with nothing in the diff to explain why.</para>
    ///
    /// <para>This window answers one question: <i>is anything a tool wrote still sitting
    /// uncommitted?</i> It lists what each tool recorded, and — because a tool can only record
    /// what it was written to record — every other dirty project file too, so nothing hides in
    /// the gap. Each group commits and pushes on its own, and a one-off tool can be retired here
    /// once its output is safely on the branch.</para>
    /// </summary>
    public sealed class FrogletToolShipWindow : EditorWindow
    {
        readonly Dictionary<string, FrogletToolShipContext> _contexts = new();
        readonly Dictionary<string, List<string>> _staging = new();
        readonly Dictionary<string, bool> _expanded = new();
        readonly HashSet<string> _selected = new();

        List<string> _tools = new();
        List<string> _unattributed = new();
        string _branch = string.Empty;
        string _status = string.Empty;
        bool _statusIsError;
        Vector2 _scroll;

        /// <summary>Set by a button, run after the GUI pass — mutating state mid-layout throws.</summary>
        Action _deferred;

        [MenuItem("FrogletTools/Build/Pending Tool Changes", false, 20)]
        [FrogletTool(FrogletToolCategory.Build, Importance = 5,
            Description = "Uncommitted asset output from editor tools. Validate, push, retire.",
            DocPath = "Docs/TOOLING.md#tool-output-is-a-deliverable")]
        public static void Open()
        {
            var w = GetWindow<FrogletToolShipWindow>("Tool Changes");
            w.minSize = new Vector2(560f, 380f);
            w.Refresh();
            w.Show();
        }

        void OnEnable() => Refresh();

        // ── Data ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// One `git status` per refresh, never per repaint — the per-tool split is computed from
        /// that single snapshot.
        /// </summary>
        void Refresh()
        {
            _contexts.Clear();
            _staging.Clear();

            if (!FrogletGit.IsAvailable)
            {
                _branch = string.Empty;
                _tools = new List<string>();
                _unattributed = new List<string>();
                return;
            }

            _branch = FrogletGit.CurrentBranch();
            _tools = new List<string>(FrogletToolChangeLedger.Tools());

            var dirty = new List<string>();
            foreach (var change in FrogletGit.Status())
            {
                if (change.IsIgnored) continue;
                if (FrogletToolShipPanel.IsProjectPath(change.Path)) dirty.Add(change.Path);
            }

            var allRecorded = new List<string>();

            foreach (var tool in _tools)
            {
                var scripts = FrogletToolChangeLedger.ScriptsFor(tool);
                var scriptArray = new string[scripts.Count];
                for (var i = 0; i < scripts.Count; i++) scriptArray[i] = scripts[i];

                _contexts[tool] = new FrogletToolShipContext(tool) { ToolScriptPaths = scriptArray };

                var paths = FrogletToolChangeLedger.PathsFor(tool);
                FrogletToolShipPanel.ResolveStaging(paths, dirty, out var staging, out _);
                _staging[tool] = staging;

                allRecorded.AddRange(paths);
            }

            FrogletToolShipPanel.ResolveStaging(allRecorded, dirty, out _, out _unattributed);
            _selected.RemoveWhere(p => !_unattributed.Contains(p));
        }

        // ── Drawing ──────────────────────────────────────────────────────────────

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "Pending Tool Changes",
                "Asset output an editor tool wrote that is not on the branch yet. Push it before you ship.",
                FrogletEditorPalette.Coral);

            if (!FrogletGit.IsAvailable)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "git is not on PATH, so this window cannot read the working tree. " +
                    "Check `git status` in a terminal before opening a pull request.",
                    MessageType.Warning);
                return;
            }

            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                DrawLedgerSection();
                FrogletEditorPalette.HorizontalRule();
                DrawUnattributedSection();
                GUILayout.Space(8);
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, _statusIsError ? MessageType.Error : MessageType.Info);

            RunDeferred();
        }

        /// <summary>
        /// Hands the pending action to the next editor tick. Running it inline would mutate the
        /// lists this GUI pass is still reading and open a modal from inside OnGUI — both throw
        /// layout errors.
        /// </summary>
        void RunDeferred()
        {
            if (_deferred == null) return;
            var action = _deferred;
            _deferred = null;
            EditorApplication.delayCall += () =>
            {
                action();
                if (this != null) Repaint(); // Retire can destroy the window mid-action
            };
        }

        void DrawToolbar()
        {
            var isProtected = FrogletGit.IsProtectedBranch(_branch);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var pillRect = GUILayoutUtility.GetRect(210f, 15f, GUILayout.Width(210f), GUILayout.Height(15f));
                FrogletEditorPalette.StatusPill(
                    pillRect,
                    string.IsNullOrEmpty(_branch) ? "NO BRANCH" : _branch.ToUpperInvariant(),
                    isProtected ? FrogletEditorPalette.Error : FrogletEditorPalette.Ok);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    _deferred = () =>
                    {
                        FrogletToolShipPanel.SaveEverything(interactive: true);
                        Refresh();
                    };

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                    _deferred = Refresh;
            }

            if (isProtected)
            {
                EditorGUILayout.HelpBox(
                    $"'{_branch}' is a protected branch — nothing here will push. Switch to a feature branch.",
                    MessageType.Error);
            }
        }

        void DrawLedgerSection()
        {
            var canPush = !FrogletGit.IsProtectedBranch(_branch);

            GUILayout.Space(6);
            GUILayout.Label("Recorded by tools", FrogletEditorPalette.SectionHeader);

            if (_tools.Count == 0)
            {
                GUILayout.Label("No tool has recorded output on this machine.", FrogletEditorPalette.Subtitle);
                return;
            }

            foreach (var tool in _tools)
            {
                if (!_contexts.TryGetValue(tool, out var ctx)) continue;
                if (!_staging.TryGetValue(tool, out var staging)) staging = new List<string>();

                var toolName = tool; // captured by the deferred actions below

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        _expanded.TryGetValue(tool, out var open);
                        var label = staging.Count == 0
                            ? $"{tool} — clean"
                            : $"{tool} — {staging.Count} uncommitted file(s)";
                        _expanded[tool] = EditorGUILayout.Foldout(open, label, true);

                        GUILayout.FlexibleSpace();

                        if (FrogletEditorPalette.ColorButton("Validate & Push", FrogletEditorPalette.Ok, 130f, 20f,
                                enabled: staging.Count > 0 && canPush))
                            _deferred = () =>
                            {
                                FrogletToolShipPanel.ValidateAndPush(ctx, interactive: true);
                                SetStatus(ctx.LastMessage, ctx.LastMessageIsError);
                                Refresh();
                            };

                        if (ctx.ToolScriptPaths.Length > 0
                            && FrogletEditorPalette.ColorButton("Retire", FrogletEditorPalette.Error, 80f, 20f,
                                tooltip: "Delete this tool's own scripts and commit the removal.",
                                enabled: canPush, outline: true))
                            _deferred = () =>
                            {
                                FrogletToolShipPanel.RetireTool(ctx, null, interactive: true);
                                SetStatus(ctx.LastMessage, ctx.LastMessageIsError);
                                Refresh();
                            };

                        if (FrogletEditorPalette.ColorButton("Forget", FrogletEditorPalette.Muted, 70f, 20f,
                                tooltip: "Drop this record without committing or deleting anything.", outline: true))
                            _deferred = () =>
                            {
                                if (EditorUtility.DisplayDialog("Forget record",
                                        $"Drop the record for '{toolName}'?\n\nNothing is committed or deleted — " +
                                        "you lose only the note that this tool wrote these files.",
                                        "Forget", "Cancel"))
                                {
                                    FrogletToolChangeLedger.Forget(toolName);
                                    Refresh();
                                }
                            };
                    }

                    if (_expanded.TryGetValue(tool, out var expanded) && expanded)
                    {
                        EditorGUI.indentLevel++;
                        if (staging.Count == 0)
                            GUILayout.Label("Everything this tool wrote is committed.", FrogletEditorPalette.Subtitle);
                        foreach (var p in staging)
                            GUILayout.Label("+  " + p, FrogletEditorPalette.CardBody);
                        foreach (var s in ctx.ToolScriptPaths)
                            GUILayout.Label("tool:  " + s, FrogletEditorPalette.Subtitle);
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }

        void DrawUnattributedSection()
        {
            GUILayout.Label("Other uncommitted project files", FrogletEditorPalette.SectionHeader);
            GUILayout.Label(
                "Dirty files under Assets/, Packages/ or ProjectSettings/ that no tool claimed. " +
                "A tool can only record what it was written to record — read this list before shipping.",
                FrogletEditorPalette.Subtitle);

            if (_unattributed.Count == 0)
            {
                GUILayout.Label("Nothing. The rest of the working tree is clean.", FrogletEditorPalette.Subtitle);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select all", EditorStyles.miniButtonLeft, GUILayout.Width(80f)))
                    foreach (var p in _unattributed) _selected.Add(p);

                if (GUILayout.Button("Select none", EditorStyles.miniButtonRight, GUILayout.Width(85f)))
                    _selected.Clear();

                GUILayout.FlexibleSpace();

                if (FrogletEditorPalette.ColorButton($"Push {_selected.Count} selected",
                        FrogletEditorPalette.Info, 160f, 20f,
                        enabled: _selected.Count > 0 && !FrogletGit.IsProtectedBranch(_branch)))
                    _deferred = PushSelected;
            }

            EditorGUI.indentLevel++;
            foreach (var p in _unattributed)
            {
                var on = _selected.Contains(p);
                var next = EditorGUILayout.ToggleLeft(p, on);
                if (next && !on) _selected.Add(p);
                else if (!next && on) _selected.Remove(p);
            }
            EditorGUI.indentLevel--;
        }

        void PushSelected()
        {
            var ctx = new FrogletToolShipContext("manual selection")
            {
                CommitScope = "assets",
                ExtraPaths = new List<string>(_selected).ToArray(),
                CommitSubject = n => $"chore(assets): commit {n} file(s) selected in Pending Tool Changes",
            };

            FrogletToolShipPanel.ValidateAndPush(ctx, interactive: true);
            SetStatus(ctx.LastMessage, ctx.LastMessageIsError);
            Refresh();
        }

        void SetStatus(string message, bool isError)
        {
            _status = message;
            _statusIsError = isError;
        }
    }
}
