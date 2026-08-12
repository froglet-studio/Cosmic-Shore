using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>Result of a tool's own validation pass, plus the panel's built-in checks.</summary>
    public readonly struct FrogletToolValidation
    {
        public readonly bool Passed;
        public readonly string Summary;
        public readonly IReadOnlyList<string> Problems;

        FrogletToolValidation(bool passed, string summary, IReadOnlyList<string> problems)
        {
            Passed = passed;
            Summary = summary ?? string.Empty;
            Problems = problems ?? Array.Empty<string>();
        }

        public static FrogletToolValidation Pass(string summary)
            => new(true, summary, Array.Empty<string>());

        public static FrogletToolValidation Fail(string summary, params string[] problems)
            => new(false, summary, problems ?? Array.Empty<string>());

        public static FrogletToolValidation Fail(string summary, IReadOnlyList<string> problems)
            => new(false, summary, problems);
    }

    /// <summary>
    /// What a tool tells the ship panel about itself: its name, the scripts that ARE the tool
    /// (so it can retire itself), and optionally its own validation pass.
    /// </summary>
    public sealed class FrogletToolShipContext
    {
        public FrogletToolShipContext(string toolName)
        {
            ToolName = toolName;
        }

        /// <summary>Ledger key and commit-message subject. Keep it stable across sessions.</summary>
        public string ToolName;

        /// <summary>
        /// The tool's own source files, deleted by "Retire Tool". Repo-relative, e.g.
        /// <c>"Assets/_Scripts/Editor/FrogletTools/MyOneOffWirer.cs"</c>. Leave empty for a
        /// permanent tool — the retire button then hides itself.
        /// </summary>
        public string[] ToolScriptPaths = Array.Empty<string>();

        /// <summary>
        /// Scratch assets the tool created that must NOT ship (temp SOs, capture prefabs).
        /// Deleted alongside the scripts by "Retire Tool".
        /// </summary>
        public string[] ScratchAssetPaths = Array.Empty<string>();

        /// <summary>
        /// Paths to commit that the ledger would not know about. Normally unnecessary — record
        /// writes through <see cref="FrogletToolChangeLedger"/> as they happen instead.
        /// </summary>
        public string[] ExtraPaths = Array.Empty<string>();

        /// <summary>
        /// The tool's own correctness check, run before anything is staged. Return
        /// <see cref="FrogletToolValidation.Fail"/> and nothing is committed.
        /// </summary>
        public Func<FrogletToolValidation> Validate;

        /// <summary>Conventional-commit scope. Default: <c>chore(tools)</c>.</summary>
        public string CommitType = "chore";
        public string CommitScope = "tools";

        /// <summary>
        /// Overrides the commit subject; the argument is the staged file count. Leave null for
        /// the default <c>chore(tools): &lt;ToolName&gt; output — N file(s)</c>.
        /// </summary>
        public Func<int, string> CommitSubject;

        // ── Panel state (not authored) ───────────────────────────────────────────
        internal List<string> CachedStaging = new();
        internal List<string> CachedUntouched = new();
        internal string CachedBranch = string.Empty;
        internal double CachedStamp;
        internal bool ShowDetail;
        internal string LastMessage = string.Empty;
        internal bool LastMessageIsError;
    }

    /// <summary>
    /// The shipping surface every Claude-authored editor tool should draw at the bottom of its
    /// window: <b>Validate &amp; Push</b> and <b>Retire Tool</b>.
    ///
    /// <para>Why it exists: a tool's real deliverable is the ASSET CHANGE it makes, and that
    /// change lands in the working tree, not in the branch. Committing the tool and forgetting
    /// its output ships code that expects data nobody pushed. These two buttons make the human's
    /// "I ran it, it worked" and the branch's contents the same event.</para>
    ///
    /// <list type="bullet">
    /// <item><b>Validate &amp; Push</b> — save assets and scenes, run the built-in checks plus the
    /// tool's own, then stage <i>only</i> this tool's recorded paths, commit, and push to the
    /// current feature branch. Everything else dirty in the tree is listed and deliberately left
    /// alone.</item>
    /// <item><b>Retire Tool</b> — once the output is verified and pushed, delete the tool's own
    /// scripts and scratch assets and commit that removal, so a one-off wirer does not calcify
    /// into permanent surface area.</item>
    /// </list>
    ///
    /// Neither button will touch a protected branch.
    /// </summary>
    public static class FrogletToolShipPanel
    {
        // ── Drawing ──────────────────────────────────────────────────────────────

        /// <summary>Draw the panel in layout flow. Call at the bottom of the tool's OnGUI.</summary>
        public static void Draw(FrogletToolShipContext ctx, EditorWindow owner = null)
        {
            if (ctx == null) return;

            FrogletEditorPalette.HorizontalRule();

            if (!FrogletGit.IsAvailable)
            {
                EditorGUILayout.HelpBox(
                    "git is not on PATH, so this tool cannot commit its own output. Commit the " +
                    "files it wrote by hand before opening a pull request.",
                    MessageType.Warning);
                return;
            }

            if (Event.current.type == EventType.Layout) RefreshIfStale(ctx, force: false);

            var branch = ctx.CachedBranch;
            var isProtected = FrogletGit.IsProtectedBranch(branch);
            var pending = ctx.CachedStaging.Count;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Ship this tool's output", FrogletEditorPalette.SectionHeader);
                GUILayout.FlexibleSpace();

                var pillRect = GUILayoutUtility.GetRect(190f, 16f, GUILayout.Width(190f), GUILayout.Height(16f));
                FrogletEditorPalette.StatusPill(
                    pillRect,
                    string.IsNullOrEmpty(branch) ? "NO BRANCH" : branch.ToUpperInvariant(),
                    isProtected ? FrogletEditorPalette.Error : FrogletEditorPalette.Ok);
            }

            if (isProtected)
            {
                EditorGUILayout.HelpBox(
                    $"'{branch}' is a protected branch. Switch to a feature branch before pushing " +
                    "tool output.",
                    MessageType.Error);
            }

            GUILayout.Label(
                pending == 0
                    ? "No uncommitted changes recorded for this tool."
                    : $"{pending} uncommitted file(s) recorded for this tool" +
                      (ctx.CachedUntouched.Count > 0
                          ? $"; {ctx.CachedUntouched.Count} other dirty file(s) will be left alone."
                          : "."),
                FrogletEditorPalette.Subtitle);

            using (new EditorGUILayout.HorizontalScope())
            {
                // Every action defers to delayCall: mutating the lists this GUI pass is reading
                // (or opening a modal from inside OnGUI) throws layout errors.
                if (FrogletEditorPalette.ColorButton("Validate & Push", FrogletEditorPalette.Ok, 150f,
                        tooltip: "Save, validate, then commit and push only this tool's output.",
                        enabled: pending > 0 && !isProtected))
                    EditorApplication.delayCall += () =>
                    {
                        ValidateAndPush(ctx, interactive: true);
                        RefreshIfStale(ctx, force: true);
                        if (owner != null) owner.Repaint();
                    };

                if (ctx.ToolScriptPaths is { Length: > 0 }
                    && FrogletEditorPalette.ColorButton("Retire Tool", FrogletEditorPalette.Error, 130f,
                        tooltip: "Delete this tool's own scripts and scratch assets, and commit the removal.",
                        enabled: !isProtected, outline: true))
                    EditorApplication.delayCall += () => RetireTool(ctx, owner, interactive: true);

                if (FrogletEditorPalette.ColorButton("Refresh", FrogletEditorPalette.Info, 90f,
                        tooltip: "Re-read git status.", outline: true))
                    EditorApplication.delayCall += () =>
                    {
                        RefreshIfStale(ctx, force: true);
                        if (owner != null) owner.Repaint();
                    };

                GUILayout.FlexibleSpace();
            }

            if (pending > 0 || ctx.CachedUntouched.Count > 0)
            {
                ctx.ShowDetail = EditorGUILayout.Foldout(ctx.ShowDetail, "What would be committed", true);
                if (ctx.ShowDetail)
                {
                    EditorGUI.indentLevel++;
                    foreach (var p in ctx.CachedStaging)
                        GUILayout.Label("+  " + p, FrogletEditorPalette.CardBody);
                    foreach (var p in ctx.CachedUntouched)
                        GUILayout.Label("–  " + p + "   (left alone)", FrogletEditorPalette.Subtitle);
                    EditorGUI.indentLevel--;
                }
            }

            if (!string.IsNullOrEmpty(ctx.LastMessage))
            {
                EditorGUILayout.HelpBox(ctx.LastMessage,
                    ctx.LastMessageIsError ? MessageType.Error : MessageType.Info);
            }
        }

        // ── Validate & Push ──────────────────────────────────────────────────────

        /// <summary>
        /// Save, validate, stage this tool's paths only, commit and push. Returns false when
        /// something went wrong (nothing was committed, or the push failed after a local commit —
        /// the message says which). Safe to call from an automated pass with
        /// <paramref name="interactive"/> false: it then never opens a dialog and refuses rather
        /// than guessing.
        /// </summary>
        public static bool ValidateAndPush(FrogletToolShipContext ctx, bool interactive)
        {
            if (ctx == null) return false;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return Report(ctx, "Exit play mode before pushing tool output.", true, interactive);

            if (EditorApplication.isCompiling)
                return Report(ctx, "Scripts are still compiling. Wait for the compile to finish.", true, interactive);

            // 1. Everything the tool wrote must actually be ON DISK before git can see it.
            if (!SaveEverything(interactive))
                return Report(ctx, "Save was cancelled — nothing was committed.", true, interactive);

            // 2. What is dirty, and which of it belongs to this tool?
            RefreshIfStale(ctx, force: true);

            if (ctx.CachedStaging.Count == 0)
                return Report(ctx, "Nothing to push: this tool has no uncommitted output.", false, interactive);

            var branch = ctx.CachedBranch;
            if (FrogletGit.IsProtectedBranch(branch))
                return Report(ctx, $"Refusing to push to protected branch '{branch}'.", true, interactive);

            // 3. Validate — built-in checks first, then the tool's own.
            var problems = new List<string>();
            BuiltInChecks(ctx.CachedStaging, problems);

            if (ctx.Validate != null)
            {
                FrogletToolValidation own;
                try
                {
                    own = ctx.Validate();
                }
                catch (Exception ex)
                {
                    own = FrogletToolValidation.Fail("The tool's validator threw: " + ex.Message);
                }

                if (!own.Passed)
                {
                    problems.Add(string.IsNullOrEmpty(own.Summary) ? "Tool validation failed." : own.Summary);
                    foreach (var p in own.Problems) problems.Add("  " + p);
                }
            }

            if (problems.Count > 0)
            {
                var message = "Validation failed — nothing was committed:\n\n• " +
                              string.Join("\n• ", problems);
                if (interactive) EditorUtility.DisplayDialog("Validate & Push", message, "OK");
                Debug.LogError($"[{ctx.ToolName}] {message}");
                return Report(ctx, message, true, false);
            }

            // 4. Confirm exactly what lands.
            if (interactive)
            {
                var body =
                    $"Commit and push {ctx.CachedStaging.Count} file(s) to '{branch}':\n\n" +
                    Bullets(ctx.CachedStaging, 12) +
                    (ctx.CachedUntouched.Count > 0
                        ? $"\n\n{ctx.CachedUntouched.Count} other dirty file(s) will NOT be staged:\n" +
                          Bullets(ctx.CachedUntouched, 6)
                        : string.Empty);

                if (!EditorUtility.DisplayDialog("Validate & Push", body, "Commit & Push", "Cancel"))
                    return Report(ctx, "Cancelled.", false, false);
            }

            // 5. Stage, commit, push.
            var add = FrogletGit.Add(ctx.CachedStaging);
            if (!add.Ok)
                return Report(ctx, "git add failed:\n" + add.Text, true, interactive);

            var subject = ctx.CommitSubject != null
                ? ctx.CommitSubject(ctx.CachedStaging.Count)
                : $"{ctx.CommitType}({ctx.CommitScope}): {ctx.ToolName} output — " +
                  $"{ctx.CachedStaging.Count} file(s)";

            // Path-scoped: anything the human had already staged of their own stays staged and
            // out of this commit.
            var commit = FrogletGit.Commit(subject, ctx.CachedStaging);
            if (!commit.Ok)
            {
                if (FrogletGit.NothingToCommit(commit))
                    return Report(ctx, "Nothing to commit — these files already match HEAD.", false, interactive);
                return Report(ctx, "git commit failed:\n" + commit.Text, true, interactive);
            }

            var push = FrogletGit.PushWithRetry(branch);
            if (!push.Ok)
                return Report(ctx,
                    "Committed locally, but the push failed:\n" + push.Text +
                    "\n\nThe commit is safe — push it by hand.", true, interactive);

            FrogletToolChangeLedger.Forget(ctx.ToolName, ctx.CachedStaging);
            RefreshIfStale(ctx, force: true);

            var done = $"Pushed {subject} to origin/{branch}.";
            Debug.Log($"[{ctx.ToolName}] {done}");
            return Report(ctx, done, false, false);
        }

        // ── Retire ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Delete the tool's own scripts and scratch assets and commit the removal. Refuses while
        /// the tool still has unpushed output — retiring first would strand it.
        /// </summary>
        public static bool RetireTool(FrogletToolShipContext ctx, EditorWindow owner, bool interactive)
        {
            if (ctx == null) return false;

            if (ctx.ToolScriptPaths == null || ctx.ToolScriptPaths.Length == 0)
                return Report(ctx, "This tool declares no scripts to retire.", true, interactive);

            RefreshIfStale(ctx, force: true);

            var branch = ctx.CachedBranch;
            if (FrogletGit.IsProtectedBranch(branch))
                return Report(ctx, $"Refusing to commit a retirement on protected branch '{branch}'.", true, interactive);

            if (ctx.CachedStaging.Count > 0)
                return Report(ctx,
                    $"This tool still has {ctx.CachedStaging.Count} uncommitted file(s). " +
                    "Validate & Push them first — retiring now would strand the output.", true, interactive);

            var targets = new List<string>();
            foreach (var p in ctx.ToolScriptPaths) AddRetireTarget(targets, p);
            foreach (var p in ctx.ScratchAssetPaths) AddRetireTarget(targets, p);

            if (targets.Count == 0)
                return Report(ctx, "Nothing to retire — the declared paths are already gone.", false, interactive);

            if (interactive)
            {
                var body = "Delete this tool and commit the removal?\n\n" + Bullets(targets, 12) +
                           "\n\nThe output it produced stays — only the tool goes.";
                if (!EditorUtility.DisplayDialog("Retire Tool", body, "Delete & Commit", "Cancel"))
                    return Report(ctx, "Cancelled.", false, false);
            }

            if (owner != null) owner.Close(); // the script defining it is about to disappear

            var deleted = new List<string>();
            foreach (var p in targets)
            {
                if (AssetDatabase.DeleteAsset(p)) deleted.Add(p);
                else Debug.LogWarning($"[{ctx.ToolName}] Could not delete {p}.");
            }

            if (deleted.Count == 0)
                return Report(ctx, "No files were deleted.", true, interactive);

            var add = FrogletGit.Add(deleted);
            if (!add.Ok)
                return Report(ctx, "Files deleted, but git add failed:\n" + add.Text, true, interactive);

            var subject = $"chore(tools): retire {ctx.ToolName} after verification";
            var commit = FrogletGit.Commit(subject, deleted);
            if (!commit.Ok)
                return Report(ctx, "Files deleted, but git commit failed:\n" + commit.Text, true, interactive);

            var push = FrogletGit.PushWithRetry(branch);
            if (!push.Ok)
                return Report(ctx, "Retirement committed locally, but the push failed:\n" + push.Text, true, interactive);

            FrogletToolChangeLedger.Forget(ctx.ToolName);
            AssetDatabase.Refresh();

            Debug.Log($"[{ctx.ToolName}] Retired: {deleted.Count} file(s) deleted and pushed to origin/{branch}.");
            return true;
        }

        static void AddRetireTarget(List<string> targets, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            var p = FrogletGit.ToRepoRelative(path);
            if (p == null || !p.StartsWith(FrogletGit.ProjectPrefix + "Assets/", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[FrogletToolShipPanel] Refusing to retire '{path}' — outside Assets/.");
                return;
            }

            var absolute = FrogletGit.ToAbsolute(p);
            if (!File.Exists(absolute) && !Directory.Exists(absolute)) return;
            if (!targets.Contains(p)) targets.Add(p);
        }

        // ── Shared helpers (also used by the standalone window) ──────────────────

        /// <summary>
        /// Save every dirty asset and scene, so the working tree matches what the editor has in
        /// memory. Returns false only when an interactive scene save is cancelled.
        /// </summary>
        public static bool SaveEverything(bool interactive)
        {
            AssetDatabase.SaveAssets();

            var saved = interactive
                ? EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
                : EditorSceneManager.SaveOpenScenes();

            AssetDatabase.Refresh();
            return saved;
        }

        /// <summary>
        /// Split the working tree's dirty project files into "covered by these paths" and
        /// "everything else". Covered means the exact path, its <c>.meta</c>, or anything under it
        /// when the recorded path is a folder.
        /// </summary>
        public static void ResolveStaging(IEnumerable<string> recorded,
                                          out List<string> staging,
                                          out List<string> untouched)
        {
            var dirty = new List<string>();
            foreach (var change in FrogletGit.Status())
            {
                if (change.IsIgnored) continue;
                if (IsProjectPath(change.Path)) dirty.Add(change.Path);
            }

            ResolveStaging(recorded, dirty, out staging, out untouched);
        }

        /// <summary>
        /// Same split against a working-tree snapshot the caller already has. Use this when
        /// splitting several tools' paths at once — one `git status` for all of them, never one
        /// per tool per repaint.
        /// </summary>
        public static void ResolveStaging(IEnumerable<string> recorded,
                                          IReadOnlyList<string> dirtyProjectPaths,
                                          out List<string> staging,
                                          out List<string> untouched)
        {
            staging = new List<string>();
            untouched = new List<string>();

            var candidates = new List<string>();
            foreach (var r in recorded)
            {
                if (string.IsNullOrWhiteSpace(r)) continue;
                var p = r.Replace('\\', '/').Trim().TrimEnd('/');
                if (p.Length > 0 && !candidates.Contains(p)) candidates.Add(p);
            }

            foreach (var path in dirtyProjectPaths)
            {
                var covered = false;
                foreach (var c in candidates)
                {
                    if (Covers(c, path)) { covered = true; break; }
                }

                (covered ? staging : untouched).Add(path);
            }

            staging.Sort(StringComparer.OrdinalIgnoreCase);
            untouched.Sort(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The checks that apply to any tool output, whatever the tool does. Appends to
        /// <paramref name="problems"/>; an empty list means the staged set is coherent.
        /// </summary>
        public static void BuiltInChecks(IReadOnlyList<string> staging, List<string> problems)
        {
            if (EditorUtility.scriptCompilationFailed)
                problems.Add("Scripts do not compile — fix the compile errors before pushing.");

            foreach (var path in staging)
            {
                var absolute = FrogletGit.ToAbsolute(path);
                var isMeta = path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
                var underAssets = path.StartsWith(FrogletGit.ProjectPrefix + "Assets/",
                    StringComparison.OrdinalIgnoreCase);

                if (isMeta)
                {
                    // A .meta whose asset is gone is residue; committing it re-imports nothing
                    // and leaves a phantom entry in the project.
                    var owner = path.Substring(0, path.Length - 5);
                    var ownerAbs = FrogletGit.ToAbsolute(owner);
                    var ownerStaged = Contains(staging, owner);
                    if (!File.Exists(ownerAbs) && !Directory.Exists(ownerAbs) && !ownerStaged)
                        problems.Add($"{path} has no asset beside it (orphan .meta).");
                    continue;
                }

                if (!underAssets) continue;
                if (!File.Exists(absolute) && !Directory.Exists(absolute)) continue; // a deletion — fine

                // Every asset Unity imports carries a .meta, and shipping one without it is the
                // classic half-committed tool output: the asset lands with a fresh GUID on the
                // next machine and every reference to it breaks.
                if (!File.Exists(absolute + ".meta") && !Directory.Exists(absolute + ".meta")
                    && !Contains(staging, path + ".meta"))
                {
                    problems.Add($"{path} has no .meta — let Unity import it (focus the editor), then retry.");
                }
            }

            var sceneCount = SceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path))
                    problems.Add($"{scene.path} still has unsaved changes.");
            }
        }

        /// <summary>
        /// Is this repo-relative path part of the Unity project? Honours
        /// <see cref="FrogletGit.ProjectPrefix"/>, so a project nested inside a larger repo still
        /// classifies its own files correctly instead of reporting everything as unattributed.
        /// </summary>
        public static bool IsProjectPath(string path)
        {
            var prefix = FrogletGit.ProjectPrefix;
            return path.StartsWith(prefix + "Assets/", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(prefix + "ProjectSettings/", StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(prefix + "Packages/", StringComparison.OrdinalIgnoreCase);
        }

        static bool Covers(string candidate, string dirtyPath)
            => string.Equals(candidate, dirtyPath, StringComparison.OrdinalIgnoreCase)
               || string.Equals(candidate + ".meta", dirtyPath, StringComparison.OrdinalIgnoreCase)
               || dirtyPath.StartsWith(candidate + "/", StringComparison.OrdinalIgnoreCase);

        static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (var v in list)
                if (string.Equals(v, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>
        /// Re-reads git status. Deliberately NOT on a timer: `git status` on a Unity repo is tens
        /// of thousands of paths, and polling it from OnGUI hitches the editor. It refreshes once
        /// when the panel first appears, on the Refresh button, and after every action.
        /// </summary>
        static void RefreshIfStale(FrogletToolShipContext ctx, bool force)
        {
            if (!force && ctx.CachedStamp > 0) return;

            ctx.CachedStamp = EditorApplication.timeSinceStartup;
            ctx.CachedBranch = FrogletGit.CurrentBranch();

            // Publish the tool's own scripts to the ledger, so Pending Tool Changes can retire it
            // even after this window is gone.
            if (ctx.ToolScriptPaths is { Length: > 0 })
                FrogletToolChangeLedger.RegisterToolScripts(ctx.ToolName, ctx.ToolScriptPaths);

            var recorded = new List<string>(FrogletToolChangeLedger.PathsFor(ctx.ToolName));
            if (ctx.ExtraPaths != null) recorded.AddRange(ctx.ExtraPaths);

            ResolveStaging(recorded, out var staging, out var untouched);
            ctx.CachedStaging = staging;
            ctx.CachedUntouched = untouched;
        }

        static string Bullets(IReadOnlyList<string> paths, int max)
        {
            var lines = new List<string>();
            for (var i = 0; i < paths.Count && i < max; i++) lines.Add("  • " + paths[i]);
            if (paths.Count > max) lines.Add($"  … and {paths.Count - max} more");
            return string.Join("\n", lines);
        }

        static bool Report(FrogletToolShipContext ctx, string message, bool isError, bool interactive)
        {
            ctx.LastMessage = message;
            ctx.LastMessageIsError = isError;

            if (interactive) EditorUtility.DisplayDialog("Ship tool output", message, "OK");
            else if (isError) Debug.LogWarning($"[{ctx.ToolName}] {message}");

            return !isError;
        }
    }
}
