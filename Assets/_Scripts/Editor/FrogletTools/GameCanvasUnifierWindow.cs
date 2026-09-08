using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// FrogletTools &gt; Game Modes &gt; GameCanvas Unifier — three steps, no options.
    ///
    /// <b>1 Fix prefab</b> puts <c>CORE/GameCanvas.prefab</c> at the canvas contract
    /// (the shipped canvas absorbed from the donor scene while a fork still exists; 1920x1080;
    /// AdaptiveCanvasScaler; smart re-anchor — see <see cref="GameCanvasUnifier.FixPrefab"/>).
    /// <b>2 Fix scenes</b> re-points every fork scene to CORE and drops the redundant overrides on
    /// the scenes already there; every row has a dry run, and one scene can be fixed on its own for
    /// a play-test before the rest. <b>3 Delete the fork</b> once nothing references it.
    /// Every step logs into the panel below; every asset written is recorded for the ship panel.
    ///
    /// Record: <c>Docs/GAMECANVAS.md</c> §9. The Prefab Kit links here.
    /// </summary>
    public sealed class GameCanvasUnifierWindow : EditorWindow
    {
        static readonly FrogletToolShipContext Ship = new(GameCanvasUnifier.ToolName)
        {
            // Permanent tool: the contract check and the per-scene fix are the guard that keeps
            // the canvas unified after the fork is gone, so there is nothing to retire.
            Validate = ValidateOutput,
            CommitType = "refactor",
            CommitScope = "ui",
            CommitSubject = n => $"refactor(ui): unify GameCanvas — {n} file(s) on CORE/GameCanvas at 1920x1080",
        };

        Vector2 _scroll;
        Vector2 _logScroll;
        List<GameCanvasUnifier.SceneRow> _rows;
        GameCanvasUnifier.ContractStatus _core;
        int _forkRefs = -1;
        string _log = "";
        bool _logIsError;

        [MenuItem("FrogletTools/Game Modes/GameCanvas Unifier", false, 11)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 5,
            Description = "One in-game canvas at 1920x1080: fix the prefab, fix each scene, delete the fork.",
            DocPath = "Docs/GAMECANVAS.md")]
        public static void Open()
        {
            var w = GetWindow<GameCanvasUnifierWindow>("GameCanvas Unifier");
            w.minSize = new Vector2(640f, 520f);
            w.Show();
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            try
            {
                _rows = GameCanvasUnifier.Report(out _forkRefs);
                _core = GameCanvasUnifier.CoreStatus();
            }
            catch (Exception e)
            {
                _rows = new List<GameCanvasUnifier.SceneRow>();
                _forkRefs = -1;
                SetLog("Report failed: " + e.Message, true);
            }
        }

        // ── GUI ──────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "GameCanvas Unifier",
                "One in-game canvas, 1920x1080.  1 Fix prefab  →  2 Fix scenes (dry run, then one, then all)  →  3 Delete the fork.",
                FrogletEditorPalette.Ruby);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                DrawPrefab();
                DrawScenes();
                DrawDelete();
                DrawLog();
                FrogletToolShipPanel.Draw(Ship, this);
                GUILayout.Space(12);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawPrefab()
        {
            Section("1 · PREFAB   CORE/GameCanvas.prefab", FrogletEditorPalette.Violet);
            var forkExists = System.IO.File.Exists(GameCanvasUnifier.ForkPrefabPath);
            bool ok = _core != null && _core.AtContract && (_core.Absorbed || !forkExists);

            var row = GUILayoutUtility.GetRect(0, 30f, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawCard(row, FrogletEditorPalette.Surface, FrogletEditorPalette.Adapt(FrogletEditorPalette.Violet).WithAlpha(0.35f));
            FrogletEditorPalette.StatusPill(new Rect(row.x + 8f, row.y + 5f, 150f, 20f),
                _core == null || !_core.Loads ? "MISSING" : ok ? "1920x1080 ✓" : _core.Summary.ToUpperInvariant(),
                ok ? FrogletEditorPalette.Ok : FrogletEditorPalette.Error);
            GUI.Label(new Rect(row.x + 166f, row.y + 6f, row.width - 420f, 18f),
                ok ? "At the contract. Scenes can be fixed."
                   : forkExists && _core != null && !_core.Absorbed
                       ? "Absorbs the shipped canvas from MinigameRampage, then 1920x1080 + AdaptiveCanvasScaler + re-anchor."
                       : "Sets 1920x1080 (Canvas Upgrader x2.4 if still 800x450), AdaptiveCanvasScaler, smart re-anchor.",
                FrogletEditorPalette.CardBody);
            if (FrogletEditorPalette.ColorButton(new Rect(row.xMax - 226f, row.y + 5f, 90f, 20f), "Dry run", FrogletEditorPalette.Info,
                    "List every change. Writes nothing.", outline: true))
                Run(() => GameCanvasUnifier.FixPrefab(dryRun: true));
            if (FrogletEditorPalette.ColorButton(new Rect(row.xMax - 130f, row.y + 5f, 120f, 20f), "Fix prefab", FrogletEditorPalette.Violet,
                    "Rewrite Assets/_Prefabs/CORE/GameCanvas.prefab to the contract."))
            {
                if (EditorUtility.DisplayDialog("Fix CORE/GameCanvas.prefab",
                        "CORE/GameCanvas.prefab will be rewritten to the canvas contract (1920x1080, AdaptiveCanvasScaler, re-anchored).\n\n" +
                        "Read the dry run first. If a donor scene is opened it is closed WITHOUT saving; if Unity asks, choose Don't Save.",
                        "Fix prefab", "Cancel"))
                    Run(() => GameCanvasUnifier.FixPrefab(dryRun: false));
            }
            GUILayout.Space(6);
        }

        void DrawScenes()
        {
            Section("2 · SCENES   fork → re-point to CORE · CORE → drop overrides that repeat the prefab", FrogletEditorPalette.Ruby);
            bool canFix = _core != null && _core.AtContract;
            if (!canFix) EditorGUILayout.HelpBox("Fix the prefab first — a scene is never put on a canvas that is not 1920x1080.", MessageType.Warning);

            if (_rows == null || _rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No canvas-bearing scenes found under Assets/_Scenes.", MessageType.Info);
                return;
            }

            var ordered = _rows.OrderBy(r => r.Family == "FORK" ? 0 : 1).ThenBy(r => r.SceneName).ToList();
            foreach (var r in ordered)
            {
                bool fork = r.Family == "FORK";
                var accent = fork ? FrogletEditorPalette.Ruby : r.Overrides == 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Jade;
                var row = GUILayoutUtility.GetRect(0, 26f, GUILayout.ExpandWidth(true));
                FrogletEditorPalette.DrawCard(row, FrogletEditorPalette.Surface, FrogletEditorPalette.Adapt(accent).WithAlpha(0.35f));
                FrogletEditorPalette.DrawAccentStripe(row, FrogletEditorPalette.Adapt(accent), 3f);
                FrogletEditorPalette.StatusPill(new Rect(row.x + 10f, row.y + 3f, 120f, 20f),
                    fork ? "ON FORK" : r.Overrides == 0 ? "CLEAN" : $"CORE · {r.Overrides} OVR",
                    fork ? FrogletEditorPalette.Error : r.Overrides == 0 ? FrogletEditorPalette.Ok : FrogletEditorPalette.Warn);
                GUI.Label(new Rect(row.x + 138f, row.y + 4f, row.width - 380f, 18f), r.SceneName, FrogletEditorPalette.CardBody);

                var local = r.ScenePath;
                if (FrogletEditorPalette.ColorButton(new Rect(row.xMax - 226f, row.y + 3f, 90f, 20f), "Dry run", FrogletEditorPalette.Info,
                        "Open, analyse, list; do not modify.", enabled: canFix, outline: true))
                    Run(() => GameCanvasUnifier.FixScene(local, dryRun: true));
                if (FrogletEditorPalette.ColorButton(new Rect(row.xMax - 130f, row.y + 3f, 120f, 20f), "Fix scene", FrogletEditorPalette.Ruby,
                        fork ? "Replace the fork instance with CORE/GameCanvas and SAVE the scene." : "Drop redundant overrides and SAVE the scene.", enabled: canFix))
                {
                    if (EditorUtility.DisplayDialog("Fix scene", $"{local}\n\nThe scene is modified and saved. Read the dry run first.", "Fix", "Cancel"))
                        Run(() => GameCanvasUnifier.FixScene(local, dryRun: false));
                }
                GUILayout.Space(2);
            }

            GUILayout.Space(4);
            var all = ordered.Select(r => r.ScenePath).ToList();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Dry run ALL", FrogletEditorPalette.Info, 120f, enabled: canFix, outline: true))
                    Run(() => RunAll(all, dryRun: true));
                GUILayout.Space(6);
                if (FrogletEditorPalette.ColorButton($"FIX ALL {all.Count}", FrogletEditorPalette.Ruby, 140f,
                        tooltip: "Every scene, in order, each saved. Only after one scene has been fixed and play-tested.", enabled: canFix))
                {
                    if (EditorUtility.DisplayDialog("Fix every scene",
                            $"{all.Count} scene(s) will be opened, fixed and saved.\n\nDo this only after one scene has been fixed and play-tested.",
                            "Fix all", "Cancel"))
                        Run(() => RunAll(all, dryRun: false));
                }
                GUILayout.FlexibleSpace();
                if (FrogletEditorPalette.ColorButton("Rescan", FrogletEditorPalette.Azure, 80f, tooltip: "Re-read every scene's YAML (no scene opened).", outline: true))
                    Refresh();
            }
            GUILayout.Space(6);
        }

        GameCanvasUnifier.Log RunAll(List<string> scenes, bool dryRun)
        {
            var all = new GameCanvasUnifier.Log();
            try
            {
                for (int i = 0; i < scenes.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("GameCanvas Unifier", scenes[i], (float)i / scenes.Count);
                    var one = GameCanvasUnifier.FixScene(scenes[i], dryRun);
                    all.Lines.AddRange(one.Lines);
                    all.Warnings.AddRange(one.Warnings);
                    all.Lines.Add("");
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            return all;
        }

        void DrawDelete()
        {
            Section("3 · DELETE   GameCanvas-SkimRace.prefab", FrogletEditorPalette.Gold);
            var forkExists = System.IO.File.Exists(GameCanvasUnifier.ForkPrefabPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Delete the fork", FrogletEditorPalette.Gold, 140f,
                        tooltip: "Refuses while any scene or prefab still references the fork.", enabled: forkExists && _forkRefs == 0))
                {
                    if (EditorUtility.DisplayDialog("Delete the fork", "Delete Assets/_Prefabs/GameCanvas-SkimRace.prefab?", "Delete", "Cancel"))
                        Run(GameCanvasUnifier.DeleteFork);
                }
                GUILayout.Space(8);
                GUILayout.Label(!forkExists ? "Already deleted." : _forkRefs == 0 ? "No references remain." : $"{_forkRefs} file(s) still reference it — fix those scenes first.",
                    FrogletEditorPalette.Subtitle);
                GUILayout.FlexibleSpace();
                GUILayout.Label("CI gate: Tools/Build/gamecanvas_unification_report.py --check", FrogletEditorPalette.Subtitle);
            }
            GUILayout.Space(6);
        }

        void DrawLog()
        {
            Section("LOG", FrogletEditorPalette.Slate);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy", GUILayout.Width(60f))) EditorGUIUtility.systemCopyBuffer = _log;
                if (GUILayout.Button("Clear", GUILayout.Width(60f))) _log = "";
                GUILayout.FlexibleSpace();
            }
            if (_logIsError) EditorGUILayout.HelpBox("The last run reported warnings — read them before continuing.", MessageType.Warning);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(200f), GUILayout.MaxHeight(400f));
            EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ── plumbing ─────────────────────────────────────────────────────────────

        void Run(Func<GameCanvasUnifier.Log> op)
        {
            // Deferred: these open scenes and pop dialogs, which must not happen inside OnGUI.
            EditorApplication.delayCall += () =>
            {
                GameCanvasUnifier.Log log;
                try { log = op(); }
                catch (Exception e) { log = new GameCanvasUnifier.Log(); log.Warn(e.ToString()); }
                SetLog(log.ToString(), !log.Ok);
                foreach (var w in log.Warnings) Debug.LogWarning("[GameCanvasUnifier] " + w);
                Refresh();
                Repaint();
            };
        }

        void SetLog(string text, bool isError)
        {
            _log = text;
            _logIsError = isError;
            Debug.Log("[GameCanvasUnifier]\n" + text);
        }

        static void Section(string title, Color accent)
        {
            GUILayout.Space(8);
            var r = GUILayoutUtility.GetRect(0, 20f, GUILayout.ExpandWidth(true));
            var a = FrogletEditorPalette.Adapt(accent);
            FrogletEditorPalette.DrawRect(r, a.WithAlpha(0.12f));
            FrogletEditorPalette.DrawAccentStripe(r, a, 4f);
            GUI.Label(new Rect(r.x + 12f, r.y, r.width - 20f, r.height), title,
                new GUIStyle(FrogletEditorPalette.SectionLabel) { normal = { textColor = a } });
            GUILayout.Space(3);
        }

        static FrogletToolValidation ValidateOutput()
        {
            var problems = new List<string>();
            var forkRefs = GameCanvasUnifier.FilesReferencingGuid(GameCanvasUnifier.ForkGuid);
            var forkExists = System.IO.File.Exists(GameCanvasUnifier.ForkPrefabPath);
            if (!forkExists && forkRefs.Count > 0)
                problems.Add($"The fork is gone but {forkRefs.Count} file(s) still reference its guid: {string.Join(", ", forkRefs.Take(5))}");
            var core = GameCanvasUnifier.CoreStatus();
            if (!core.Loads) problems.Add($"{GameCanvasUnifier.CorePrefabPath} does not load.");
            else if (!core.AtContract) problems.Add($"{GameCanvasUnifier.CorePrefabPath} is not at the canvas contract: {core.Summary}.");
            return problems.Count == 0
                ? FrogletToolValidation.Pass(forkExists ? "CORE at 1920x1080; fork still present (intermediate state is fine to push)." : "CORE at 1920x1080; fork retired; no dangling guid.")
                : FrogletToolValidation.Fail("GameCanvas unification output is inconsistent.", problems);
        }
    }
}
