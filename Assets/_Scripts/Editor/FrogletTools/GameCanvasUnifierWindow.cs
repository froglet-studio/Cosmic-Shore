using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// FrogletTools &gt; Game Modes &gt; GameCanvas Unifier.
    ///
    /// Drives <see cref="GameCanvasUnifier"/> in the order the retirement has to happen:
    /// <b>1 Report</b> (read-only), <b>2 Absorb</b> the shipped canvas into <c>CORE/GameCanvas</c>
    /// (one prefab write, no scene touched), <b>3 Re-point</b> each fork scene (dry run first,
    /// then one scene, then the rest), <b>4 Delete</b> the fork. Every step logs what it did into
    /// the panel below the buttons, and every asset it writes is recorded for the ship panel.
    ///
    /// Read <c>Docs/GAMECANVAS.md</c> §9 before running it: the shipped canvas is not the fork
    /// prefab, and the donor scene is what defines it.
    /// </summary>
    public sealed class GameCanvasUnifierWindow : EditorWindow
    {
        static readonly FrogletToolShipContext Ship = new(GameCanvasUnifier.ToolName)
        {
            // Permanent tool: the report and the re-point are the guard that keeps the canvas
            // unified after the fork is gone, so there is nothing to retire.
            Validate = ValidateOutput,
            CommitType = "refactor",
            CommitScope = "ui",
            CommitSubject = n => $"refactor(ui): unify GameCanvas — {n} file(s) re-pointed to CORE/GameCanvas",
        };

        Vector2 _scroll;
        Vector2 _logScroll;
        List<GameCanvasUnifier.SceneRow> _rows;
        int _forkRefs = -1;
        string _donor = GameCanvasUnifier.DefaultDonorScene;
        bool _addAdaptiveScaler = true;
        bool _clearStatsToTrack = true;
        string _survivorPrefixes = "";
        bool _keepSceneRefs;
        bool _carrySameNamed;
        string _log = "";
        bool _logIsError;

        [MenuItem("FrogletTools/Game Modes/GameCanvas Unifier", false, 11)]
        [FrogletTool(FrogletToolCategory.GameModes, Importance = 5,
            Description = "Retire the GameCanvas fork: absorb the shipped canvas into CORE/GameCanvas and re-point every scene to it.",
            DocPath = "Docs/GAMECANVAS.md")]
        public static void Open()
        {
            var w = GetWindow<GameCanvasUnifierWindow>("GameCanvas Unifier");
            w.minSize = new Vector2(760f, 560f);
            w.Show();
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            try { _rows = GameCanvasUnifier.Report(out _forkRefs); }
            catch (Exception e) { _rows = new List<GameCanvasUnifier.SceneRow>(); _forkRefs = -1; SetLog("Report failed: " + e.Message, true); }
        }

        // ── GUI ──────────────────────────────────────────────────────────────────

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "GameCanvas Unifier",
                "One in-game canvas prefab. Report → Absorb → Re-point (dry run first, one scene first) → Delete the fork.",
                FrogletEditorPalette.Ruby);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            {
                DrawReport();
                DrawAbsorb();
                DrawRepoint();
                DrawDelete();
                DrawLog();
                FrogletToolShipPanel.Draw(Ship, this);
                GUILayout.Space(12);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawReport()
        {
            Section("1 · REPORT  (read-only — scene YAML, no scene opened)", FrogletEditorPalette.Azure);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Rescan", FrogletEditorPalette.Azure, 90f, tooltip: "Re-read every canvas-bearing scene."))
                    Refresh();
                GUILayout.Space(8);
                var forkExists = System.IO.File.Exists(GameCanvasUnifier.ForkPrefabPath);
                GUILayout.Label(forkExists
                        ? $"Fork prefab present · referenced by {_forkRefs} file(s)"
                        : "Fork prefab deleted",
                    FrogletEditorPalette.Subtitle);
                GUILayout.FlexibleSpace();
                GUILayout.Label("Offline twin: Tools/Build/gamecanvas_unification_report.py  (--check is the CI gate)", FrogletEditorPalette.Subtitle);
            }

            if (_rows == null || _rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No canvas-bearing scenes found under Assets/_Scenes.", MessageType.Info);
                return;
            }

            var header = GUILayoutUtility.GetRect(0, 18f, GUILayout.ExpandWidth(true));
            FrogletEditorPalette.DrawRect(header, FrogletEditorPalette.SurfaceRaised);
            DrawCells(header, FrogletEditorPalette.SectionLabel, "family", "scene", "overrides", "−GO", "−comp", "+GO", "+comp");
            foreach (var r in _rows)
            {
                var row = GUILayoutUtility.GetRect(0, 18f, GUILayout.ExpandWidth(true));
                var accent = r.Family == "FORK" ? FrogletEditorPalette.Ruby : FrogletEditorPalette.Jade;
                FrogletEditorPalette.DrawAccentStripe(row, FrogletEditorPalette.Adapt(accent), 3f);
                DrawCells(row, FrogletEditorPalette.CardBody, r.Family, r.SceneName, r.Overrides.ToString(),
                    r.RemovedGameObjects.ToString(), r.RemovedComponents.ToString(), r.AddedGameObjects.ToString(), r.AddedComponents.ToString());
            }
            GUILayout.Space(6);
        }

        static void DrawCells(Rect row, GUIStyle style, params string[] cells)
        {
            float[] widths = { 60f, 330f, 80f, 50f, 60f, 50f, 60f };
            float x = row.x + 10f;
            for (int i = 0; i < cells.Length && i < widths.Length; i++)
            {
                GUI.Label(new Rect(x, row.y, widths[i], row.height), cells[i], style);
                x += widths[i];
            }
        }

        void DrawAbsorb()
        {
            Section("2 · ABSORB  the shipped canvas into CORE/GameCanvas.prefab  (one prefab write, no scene touched)", FrogletEditorPalette.Violet);
            EditorGUILayout.LabelField(
                "Takes the donor scene's canvas AS IT SHIPS (fork prefab minus its 9 removed objects and 3 removed components, " +
                "plus the 3 scene-added components and 2 added objects), unpacks it one level in memory, and merges it into " +
                "CORE by hierarchy path. CORE keeps every fileID it has, so the ten scenes already on it are untouched.",
                FrogletEditorPalette.CardBodyWrapped);
            GUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Donor scene", GUILayout.Width(80f));
                _donor = EditorGUILayout.TextField(_donor);
                if (GUILayout.Button("…", GUILayout.Width(28f)))
                {
                    var picked = EditorUtility.OpenFilePanel("Donor scene", "Assets/_Scenes/Multiplayer Scenes", "unity");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        var i = picked.IndexOf("Assets/", StringComparison.Ordinal);
                        if (i >= 0) _donor = picked.Substring(i);
                    }
                }
            }
            _addAdaptiveScaler = EditorGUILayout.ToggleLeft(
                "Add AdaptiveCanvasScaler to the canvas root (Joust / Scurry / Skim Race / Maelstrom carry it; the other twelve do not)",
                _addAdaptiveScaler);
            _clearStatsToTrack = EditorGUILayout.ToggleLeft(
                "Clear EventDrivenStatsProvider.statsToTrack so Resources/GameModeStatsProfile decides per mode",
                _clearStatsToTrack);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Dry run", FrogletEditorPalette.Info, 110f,
                        tooltip: "Open the donor additively, unpack in memory, list every change. Writes nothing.", outline: true))
                    Run(() => GameCanvasUnifier.Absorb(Options(), dryRun: true));
                GUILayout.Space(6);
                if (FrogletEditorPalette.ColorButton("ABSORB into CORE", FrogletEditorPalette.Violet, 160f,
                        tooltip: "Merge and save Assets/_Prefabs/CORE/GameCanvas.prefab."))
                {
                    if (EditorUtility.DisplayDialog("Absorb the shipped canvas into CORE/GameCanvas",
                            $"CORE/GameCanvas.prefab will be rewritten to match the canvas that ships in\n{_donor}\n\n" +
                            "Run the dry run first and read the deletion list. The donor scene is opened additively and " +
                            "closed WITHOUT saving; if Unity asks, choose Don't Save.\n\nProceed?", "Absorb", "Cancel"))
                        Run(() => GameCanvasUnifier.Absorb(Options(), dryRun: false));
                }
            }
            GUILayout.Space(6);
        }

        GameCanvasUnifier.AbsorbOptions Options() => new()
        {
            DonorScenePath = _donor,
            AddAdaptiveCanvasScaler = _addAdaptiveScaler,
            ClearStatsToTrack = _clearStatsToTrack,
        };

        void DrawRepoint()
        {
            Section("3 · RE-POINT  each fork scene to CORE/GameCanvas  (dry run first; one scene first; play-test; then the rest)", FrogletEditorPalette.Ruby);
            EditorGUILayout.LabelField(
                "Replaces the scene's fork instance with a CORE instance at the same place. Preserved: overrides whose " +
                "propertyPath starts with a survivor prefix (none by default — statsToTrack lives in GameModeStatsProfile now), " +
                "scene-added objects / components the prefab does not now carry, and every scene-side reference INTO the canvas " +
                "(countdownTimer, volumeUI, added panels), resolved by hierarchy path. Everything else is dropped and listed.",
                FrogletEditorPalette.CardBodyWrapped);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Survivor prefixes", GUILayout.Width(110f));
                _survivorPrefixes = EditorGUILayout.TextField(_survivorPrefixes);
            }
            _keepSceneRefs = EditorGUILayout.ToggleLeft("Also keep overrides that point at scene objects (off: the canvas resolves its controller itself)", _keepSceneRefs);
            _carrySameNamed = EditorGUILayout.ToggleLeft("Carry same-named additions (off: a scene-added 'NotificationUI' beside the prefab's own is dropped as a leftover)", _carrySameNamed);
            GUILayout.Space(4);

            var forkScenes = _rows?.Where(r => r.Family == "FORK").Select(r => r.ScenePath).ToList() ?? new List<string>();
            if (forkScenes.Count == 0)
            {
                EditorGUILayout.HelpBox("No scene is on the fork. Step 3 is complete.", MessageType.Info);
            }
            else
            {
                foreach (var scenePath in forkScenes)
                {
                    var row = GUILayoutUtility.GetRect(0, 26f, GUILayout.ExpandWidth(true));
                    FrogletEditorPalette.DrawCard(row, FrogletEditorPalette.Surface, FrogletEditorPalette.Adapt(FrogletEditorPalette.Ruby).WithAlpha(0.35f));
                    GUI.Label(new Rect(row.x + 10f, row.y + 4f, row.width - 240f, 18f), System.IO.Path.GetFileNameWithoutExtension(scenePath), FrogletEditorPalette.CardBody);
                    var dry = new Rect(row.xMax - 226f, row.y + 3f, 90f, 20f);
                    var fix = new Rect(row.xMax - 130f, row.y + 3f, 120f, 20f);
                    var local = scenePath;
                    if (FrogletEditorPalette.ColorButton(dry, "Dry run", FrogletEditorPalette.Info, "Open, analyse, list; do not modify.", outline: true))
                        Run(() => GameCanvasUnifier.Repoint(local, RepointOptions(), dryRun: true));
                    if (FrogletEditorPalette.ColorButton(fix, "Re-point scene", FrogletEditorPalette.Ruby, "Replace the instance and SAVE the scene."))
                    {
                        if (EditorUtility.DisplayDialog("Re-point scene",
                                $"{local}\n\nThe fork instance is replaced by a CORE/GameCanvas instance and the scene is saved. Read the dry run first.",
                                "Re-point", "Cancel"))
                            Run(() => GameCanvasUnifier.Repoint(local, RepointOptions(), dryRun: false));
                    }
                    GUILayout.Space(2);
                }
                GUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (FrogletEditorPalette.ColorButton("Dry run ALL", FrogletEditorPalette.Info, 120f, outline: true))
                        Run(() => RunAll(forkScenes, dryRun: true));
                    GUILayout.Space(6);
                    if (FrogletEditorPalette.ColorButton($"RE-POINT ALL {forkScenes.Count}", FrogletEditorPalette.Ruby, 160f,
                            tooltip: "Every remaining fork scene, in order, each saved. Only after one scene has been play-tested."))
                    {
                        if (EditorUtility.DisplayDialog("Re-point every remaining fork scene",
                                $"{forkScenes.Count} scene(s) will be opened, re-pointed and saved.\n\nDo this only after one scene has been re-pointed and play-tested.",
                                "Re-point all", "Cancel"))
                            Run(() => RunAll(forkScenes, dryRun: false));
                    }
                }
            }
            GUILayout.Space(6);
        }

        GameCanvasUnifier.RepointOptions RepointOptions() => new()
        {
            SurvivorPropertyPrefixes = _survivorPrefixes.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList(),
            KeepSceneReferenceOverrides = _keepSceneRefs,
            CarrySameNamedAdditions = _carrySameNamed,
        };

        GameCanvasUnifier.Log RunAll(List<string> scenes, bool dryRun)
        {
            var all = new GameCanvasUnifier.Log();
            try
            {
                for (int i = 0; i < scenes.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("GameCanvas Unifier", scenes[i], (float)i / scenes.Count);
                    var one = GameCanvasUnifier.Repoint(scenes[i], RepointOptions(), dryRun);
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
            Section("4 · DELETE  the fork  (enabled once nothing references its guid)", FrogletEditorPalette.Gold);
            var forkExists = System.IO.File.Exists(GameCanvasUnifier.ForkPrefabPath);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Delete GameCanvas-SkimRace.prefab", FrogletEditorPalette.Gold, 240f,
                        tooltip: "Refuses while any scene or prefab still references the fork.", enabled: forkExists && _forkRefs == 0))
                {
                    if (EditorUtility.DisplayDialog("Delete the fork", "Delete Assets/_Prefabs/GameCanvas-SkimRace.prefab?", "Delete", "Cancel"))
                        Run(GameCanvasUnifier.DeleteFork);
                }
                GUILayout.Space(8);
                GUILayout.Label(!forkExists ? "Already deleted." : _forkRefs == 0 ? "No references remain." : $"{_forkRefs} file(s) still reference it.",
                    FrogletEditorPalette.Subtitle);
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
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(220f), GUILayout.MaxHeight(420f));
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
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GameCanvasUnifier.CorePrefabPath) == null)
                problems.Add($"{GameCanvasUnifier.CorePrefabPath} does not load.");
            return problems.Count == 0
                ? FrogletToolValidation.Pass(forkExists ? "CORE loads; fork still present (intermediate state is fine to push)." : "CORE loads; fork retired; no dangling guid.")
                : FrogletToolValidation.Fail("GameCanvas unification output is inconsistent.", problems);
        }
    }
}
