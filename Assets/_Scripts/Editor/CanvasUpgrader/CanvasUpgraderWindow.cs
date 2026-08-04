using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-scene-at-a-time migration of screen-space canvases from the mobile 800x450 reference
    /// resolution to a PC-ready 1920x1080, keeping the rendered output pixel-identical
    /// (every canvas-space value x2.4). Workflow per scene:
    ///
    ///  1. Scan       — list root canvases at 800x450 (prefab instances flagged: changes become
    ///                  overrides; open the prefab asset in its own Prefab Stage to avoid them —
    ///                  the game scenes have no scene canvases, their UI lives in
    ///                  GameCanvas.prefab / GameCanvas-HexRace.prefab).
    ///  2. Dry Run    — full report of every value that WOULD change; nothing is touched.
    ///  3. Upgrade    — apply the conversion (RectTransforms, TMP/legacy text sizes, layout
    ///                  groups, layout elements, shadows/outlines, RectMask2D, CanvasScaler
    ///                  referencePixelsPerUnit) in ONE collapsed Undo step, optionally adding
    ///                  <see cref="CosmicShore.UI.AdaptiveCanvasScaler"/> for runtime aspect handling.
    ///  4. Re-anchor  — snap center-anchored elements to the nearest of the 9 anchor presets by
    ///                  where they sit, without moving anything visually, so HUD corners hold on
    ///                  any aspect ratio.
    ///  5. Animations — detect scene clips animating anchoredPosition/sizeDelta under the
    ///                  canvases; optional x2.4 keyframe scaling (clips are shared assets!).
    ///  6. Code scan  — write Assets/CanvasUpgrader/CodeReferences.md listing C# that writes
    ///                  those metrics with hardcoded constants (manual fix; never auto-edited).
    ///
    /// Canvas-less UI prefabs — runtime-spawned fragments with no scaler of their own
    /// (PlayerScoreCard, toasts, feed entries, vessel HUD variants) — are detected when opened in
    /// the Prefab Stage and upgraded with the root INCLUDED, since the root's size lives in the
    /// parent canvas's units. A persistent log (ProjectSettings/CanvasUpgraderUpgradedPrefabs.txt)
    /// blocks accidental second passes, which would compound to x5.76; the scaled-animation-curve
    /// log works the same way across scenes that share clips.
    ///
    /// Verify pixel-identity at a 16:9 Game view before/after, then test 16:10, 21:9, 32:9 and
    /// 4:3. Unrelated but adjacent perf: the scan lists each canvas's enabled raycast-target
    /// count — audit those with Tools &gt; Cosmic Shore &gt; UI &gt; Raycast Target Audit.
    /// </summary>
    public class CanvasUpgraderWindow : EditorWindow
    {
        Vector2 _scroll;
        List<CanvasUpgradeProcessor.CanvasEntry> _entries = new();
        readonly List<string> _skipped = new();
        List<CanvasUpgradeProcessor.AnimatedBindingHit> _animHits = new();
        readonly HashSet<string> _scaledCurveKeys = new();
        int _pendingAnimCurves;
        RectTransform _canvasLessRoot;         // prefab-stage root when the open prefab is a spawned UI fragment
        string _canvasLessPrefabPath;
        string _canvasLessPrefabGuid;
        bool _canvasLessAlreadyUpgraded;
        bool _reanchorRecursive;
        bool _addAdaptiveScaler = true;
        float _anchorFaultTolerance = 8f;
        string _lastReport = "Scan the open scene (or the open Prefab Stage) to begin.";
        string _targetLabel = "";

        const int ReportDisplayCap = 40000;   // TextArea slows down beyond this; full text always goes to the Console/Editor.log

        [MenuItem("FrogletTools/Interface/Canvas Upgrader")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 3,
            Description = "Bring a legacy canvas up to the current UI conventions.")]
        static void Open()
        {
            var w = GetWindow<CanvasUpgraderWindow>("Canvas Upgrader");
            w.minSize = new Vector2(620, 560);
            w.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Converts screen-space canvases authored at 800x450 to 1920x1080 with pixel-identical output (x2.4). " +
                "Operates on the CURRENT scene only — or on the open Prefab Stage, which is how to upgrade " +
                "GameCanvas.prefab / GameCanvas-HexRace.prefab without creating instance overrides. " +
                "Canvas-less UI prefabs that get spawned under a canvas at runtime (score cards, toasts, HUD variants) " +
                "are detected in the Prefab Stage too and upgraded root-included. " +
                "Workflow: Scan -> Dry Run -> Upgrade -> Smart Re-anchor -> Animation clips -> Code scan.",
                MessageType.Info);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox("Exit Play Mode first — play-mode changes would be lost and Undo would not persist.", MessageType.Warning);
                return;
            }

            // ---- Scan ----
            if (GUILayout.Button($"Scan {TargetLabel()}"))
                Scan();

            if (!string.IsNullOrEmpty(_targetLabel))
                EditorGUILayout.LabelField($"Scanned: {_targetLabel}", EditorStyles.miniBoldLabel);

            DrawCanvasList();

            EditorGUILayout.Space();

            // ---- Dry run / Upgrade ----
            EditorGUILayout.LabelField("Upgrade", EditorStyles.boldLabel);
            _addAdaptiveScaler = EditorGUILayout.ToggleLeft(
                new GUIContent("Add AdaptiveCanvasScaler component on upgrade",
                    "Drives CanvasScaler.matchWidthOrHeight from the live aspect ratio at runtime (ultrawide keeps HUD size, narrow never crops)."),
                _addAdaptiveScaler);

            int upgradeable = CountSelected(includeUpgraded: false);
            using (new EditorGUI.DisabledScope(upgradeable == 0))
            {
                if (GUILayout.Button($"Dry Run — report changes for {upgradeable} canvas(es), change nothing"))
                    RunUpgrade(apply: false);
                if (GUILayout.Button($"Upgrade {upgradeable} canvas(es) to 1920x1080 (single Undo step)"))
                    RunUpgrade(apply: true);
            }

            // ---- Canvas-less UI fragment prefab (runtime-spawned under a canvas) ----
            if (_canvasLessRoot != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Canvas-less UI Prefab (runtime-spawned fragment)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"{_canvasLessPrefabPath} — scales the WHOLE hierarchy including the root", EditorStyles.miniLabel);
                if (_canvasLessAlreadyUpgraded)
                    EditorGUILayout.HelpBox(
                        $"Already upgraded per {CanvasUpgradeProcessor.UpgradedPrefabLogPath}. A second pass would compound to x5.76 — " +
                        "delete this prefab's line from that file only if you intentionally reverted it.",
                        MessageType.Warning);
                using (new EditorGUI.DisabledScope(_canvasLessAlreadyUpgraded))
                {
                    if (GUILayout.Button("Dry Run — report fragment changes, change nothing"))
                        RunPrefabRootUpgrade(apply: false);
                    if (GUILayout.Button("Upgrade fragment prefab x2.4 (single Undo step)"))
                        RunPrefabRootUpgrade(apply: true);
                }
            }

            EditorGUILayout.Space();

            // ---- Smart re-anchor ----
            EditorGUILayout.LabelField("Smart Re-anchor (run after Upgrade)", EditorStyles.boldLabel);
            _reanchorRecursive = EditorGUILayout.ToggleLeft(
                new GUIContent("Recursive (all descendants, each relative to its own parent)",
                    "Off: only direct children of each canvas are re-anchored."),
                _reanchorRecursive);

            int anySelected = CountSelected(includeUpgraded: true);
            using (new EditorGUI.DisabledScope(anySelected == 0))
            {
                if (GUILayout.Button($"Re-anchor {anySelected} canvas(es) — snap center anchors to nearest of 9 presets (single Undo step)"))
                    RunReanchor();
            }

            EditorGUILayout.Space();

            // ---- Faulted anchors ----
            EditorGUILayout.LabelField("Faulted Anchors", EditorStyles.boldLabel);
            _anchorFaultTolerance = EditorGUILayout.Slider(
                new GUIContent("Fault tolerance (px)",
                    "An element is FAULTED when its anchor region on the parent sits further than this " +
                    "from the rect it drives - anchors not wrapped around the element, so nothing holds " +
                    "its place across resolutions. The fix wraps the anchors around the element's current " +
                    "rect and zeroes the offsets, pixel-identical visually."),
                _anchorFaultTolerance, 0f, 200f);

            bool anyAnchorTarget = anySelected > 0 || _canvasLessRoot != null;
            using (new EditorGUI.DisabledScope(!anyAnchorTarget))
            {
                if (GUILayout.Button("Find faulted anchors — report only, change nothing"))
                    RunFixFaultedAnchors(apply: false);
                if (GUILayout.Button("Fix ALL faulted anchors — wrap anchors around each element (single Undo step)"))
                    RunFixFaultedAnchors(apply: true);
            }

            EditorGUILayout.Space();

            // ---- Animation clips ----
            EditorGUILayout.LabelField("Animation Clips", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(anySelected == 0))
            {
                if (GUILayout.Button("Find clips animating anchoredPosition / sizeDelta under the scanned canvases"))
                    FindAnimations();
            }
            using (new EditorGUI.DisabledScope(_pendingAnimCurves == 0))
            {
                if (GUILayout.Button($"Scale {_pendingAnimCurves} keyframe curve(s) x2.4 — clips are SHARED ASSETS (single Undo step)"))
                    RunScaleAnimationKeys();
            }

            EditorGUILayout.Space();

            // ---- Code references ----
            EditorGUILayout.LabelField("Code References", EditorStyles.boldLabel);
            if (GUILayout.Button($"Generate {CanvasUpgraderCodeScan.ReportPath} (hardcoded sizeDelta/anchoredPosition/fontSize in C#)"))
            {
                string summary = CanvasUpgraderCodeScan.Generate();
                _lastReport = summary;
                Debug.Log($"[CanvasUpgrader] {summary}");
            }

            EditorGUILayout.HelpBox(
                "Perf, while you're in here: the 'raycast targets on' counts above drive EventSystem cost and are " +
                "worth auditing per upgraded scene with FrogletTools > Interface > Raycast Target Audit " +
                "(scan, review candidates, disable, click through the UI once).",
                MessageType.None);

            // ---- Report ----
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string display = _lastReport.Length <= ReportDisplayCap
                ? _lastReport
                : _lastReport.Substring(0, ReportDisplayCap) + "\n\n[... display truncated — full report in the Console / Editor.log ...]";
            EditorGUILayout.TextArea(display, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        void DrawCanvasList()
        {
            if (_entries.Count == 0 && _skipped.Count == 0) return;

            EditorGUILayout.LabelField($"Canvases ({_entries.Count})", EditorStyles.boldLabel);
            foreach (var entry in _entries)
            {
                if (entry.Canvas == null)
                {
                    EditorGUILayout.LabelField("  (canvas destroyed — rescan)", EditorStyles.miniLabel);
                    continue;
                }
                string label = $"{entry.Canvas.name} — {entry.ChildRectCount} child RectTransforms, raycast targets on: {entry.RaycastTargetsOn}"
                               + (entry.AlreadyUpgraded ? "  [already 1920x1080 — re-anchor/anim only]" : "");
                entry.Selected = EditorGUILayout.ToggleLeft(label, entry.Selected);
                EditorGUI.indentLevel++;
                if (entry.IsPrefabInstance)
                    EditorGUILayout.LabelField($"PREFAB INSTANCE of {entry.PrefabAssetPath} — upgrading here creates overrides; prefer opening the prefab and upgrading in its Prefab Stage", EditorStyles.miniLabel);
                foreach (var note in entry.Notes)
                    EditorGUILayout.LabelField(note, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            if (_skipped.Count > 0)
            {
                EditorGUILayout.LabelField($"Skipped ({_skipped.Count})", EditorStyles.miniBoldLabel);
                EditorGUI.indentLevel++;
                foreach (var reason in _skipped)
                    EditorGUILayout.LabelField(reason, EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
        }

        // ------------------------------------------------------------------
        //  Actions
        // ------------------------------------------------------------------

        void Scan()
        {
            var (scene, label) = GetTargetScene();
            _targetLabel = label;
            _entries = CanvasUpgradeProcessor.Scan(scene, _skipped);
            _animHits.Clear();

            var report = new StringBuilder();
            report.AppendLine($"SCAN — {label}");
            foreach (var entry in _entries)
            {
                report.AppendLine($"  Canvas '{entry.Canvas.name}': {entry.ChildRectCount} child RectTransforms, "
                                  + $"reference {(entry.AlreadyUpgraded ? "1920x1080 (already upgraded)" : "800x450 (upgradeable)")}, "
                                  + $"raycast targets on: {entry.RaycastTargetsOn}"
                                  + (entry.IsPrefabInstance ? $", PREFAB INSTANCE of {entry.PrefabAssetPath}" : ""));
                foreach (var note in entry.Notes)
                    report.AppendLine($"    note: {note}");
            }
            foreach (var reason in _skipped)
                report.AppendLine($"  skipped: {reason}");
            if (_entries.Count == 0)
                report.AppendLine("  No 800x450 / 1920x1080 Scale-With-Screen-Size canvases found.");

            // A canvas-less prefab (PlayerScoreCard, toast, HUD variant, ...) is a spawned UI
            // fragment: no scaler of its own, authored in the parent canvas's units.
            _canvasLessRoot = null;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && _entries.Count == 0
                && stage.prefabContentsRoot.transform is RectTransform fragmentRoot)
            {
                _canvasLessRoot = fragmentRoot;
                _canvasLessPrefabPath = stage.assetPath;
                _canvasLessPrefabGuid = AssetDatabase.AssetPathToGUID(stage.assetPath);
                _canvasLessAlreadyUpgraded = CanvasUpgradeProcessor.IsPrefabLoggedUpgraded(_canvasLessPrefabGuid);
                report.AppendLine($"  Canvas-less UI prefab '{fragmentRoot.name}' ({stage.assetPath}) — "
                                  + (_canvasLessAlreadyUpgraded
                                      ? "ALREADY upgraded per the persistent log."
                                      : "runtime-spawned fragment; upgrade it with the fragment buttons (root included)."));
            }

            _lastReport = report.ToString();
            Debug.Log($"[CanvasUpgrader]\n{_lastReport}");
        }

        void RunUpgrade(bool apply)
        {
            var counters = new CanvasUpgradeProcessor.UpgradeCounters();
            string report;

            if (apply)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Canvas Upgrade 800x450 -> 1920x1080");
                int group = Undo.GetCurrentGroup();
                report = CanvasUpgradeProcessor.Upgrade(_entries, true, _addAdaptiveScaler, counters);
                Undo.CollapseUndoOperations(group);
                Canvas.ForceUpdateCanvases();   // one relayout for everything, not per element
            }
            else
            {
                report = CanvasUpgradeProcessor.Upgrade(_entries, false, _addAdaptiveScaler, counters);
            }

            string summary = Summarize(apply, counters);
            _lastReport = $"{summary}\n\n{report}";
            Debug.Log($"[CanvasUpgrader] {summary}\n{report}");
        }

        static string Summarize(bool applied, CanvasUpgradeProcessor.UpgradeCounters c)
        {
            var sb = new StringBuilder();
            sb.Append(applied ? "[applied] " : "[dry run] ");
            sb.Append($"{c.Canvases} canvas(es): {c.RectTransforms} RectTransforms, {c.TmpTexts} TMP texts, {c.LegacyTexts} legacy texts, ");
            sb.Append($"{c.LayoutGroups} H/V layout groups, {c.GridGroups} grids, {c.LayoutElements} layout elements, {c.Shadows} shadows/outlines, {c.RectMasks} rect masks");
            if (c.AdaptiveAdded > 0) sb.Append($"; {c.AdaptiveAdded} AdaptiveCanvasScaler added");
            if (c.RoundedInts > 0) sb.Append($"; {c.RoundedInts} int value(s) rounded (x2.4 of an int not divisible by 5 is fractional)");
            if (c.FlexibleScaled > 0) sb.Append($"; {c.FlexibleScaled} pixel-like flexible size(s) scaled — review");
            if (c.FlexibleLeftAsWeights > 0) sb.Append($"; {c.FlexibleLeftAsWeights} flexible weight(s) left unchanged");
            if (applied) sb.Append(". One Undo step (Ctrl+Z reverts everything). Save the scene/prefab to keep.");
            return sb.ToString();
        }

        void RunPrefabRootUpgrade(bool apply)
        {
            if (_canvasLessRoot == null) return;
            var counters = new CanvasUpgradeProcessor.UpgradeCounters();
            string report;

            if (apply)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Canvas-less UI Prefab Upgrade x2.4");
                int group = Undo.GetCurrentGroup();
                report = CanvasUpgradeProcessor.UpgradePrefabRoot(_canvasLessRoot, true, counters);
                Undo.CollapseUndoOperations(group);
                Canvas.ForceUpdateCanvases();
                CanvasUpgradeProcessor.MarkPrefabUpgraded(_canvasLessPrefabGuid, _canvasLessPrefabPath);
                _canvasLessAlreadyUpgraded = true;
            }
            else
            {
                report = CanvasUpgradeProcessor.UpgradePrefabRoot(_canvasLessRoot, false, counters);
            }

            string summary = Summarize(apply, counters)
                             + (apply ? $" Logged in {CanvasUpgradeProcessor.UpgradedPrefabLogPath} (commit it); save the prefab (Ctrl+S) to keep." : "");
            _lastReport = $"{summary}\n\n{report}";
            Debug.Log($"[CanvasUpgrader] {summary}\n{report}");
        }

        void RunFixFaultedAnchors(bool apply)
        {
            int group = 0;
            if (apply)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Fix Faulted Anchors");
                group = Undo.GetCurrentGroup();
            }

            string report = CanvasUpgradeProcessor.FixFaultedAnchors(
                _entries, _canvasLessRoot, _anchorFaultTolerance, apply,
                out int faulted, out int fixedCount);

            if (apply)
            {
                Undo.CollapseUndoOperations(group);
                Canvas.ForceUpdateCanvases();
            }

            string summary = apply
                ? $"Fixed {fixedCount} of {faulted} faulted anchor(s) — anchors wrapped around each element, visual positions preserved. One Undo step."
                : $"Found {faulted} faulted anchor(s) (anchor region > {_anchorFaultTolerance:0.#}px from the element it drives). Nothing changed.";
            _lastReport = $"{summary}\n\n{report}";
            Debug.Log($"[CanvasUpgrader] {summary}\n{report}");
        }

        void RunReanchor()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Canvas Smart Re-anchor");
            int group = Undo.GetCurrentGroup();
            string report = CanvasUpgradeProcessor.Reanchor(_entries, _reanchorRecursive, out int changed, out int skipped);
            Undo.CollapseUndoOperations(group);
            Canvas.ForceUpdateCanvases();

            string summary = $"Re-anchored {changed} element(s), left {skipped} untouched (stretched / edge-anchored / custom / layout-driven). One Undo step.";
            _lastReport = $"{summary}\n\n{report}";
            Debug.Log($"[CanvasUpgrader] {summary}\n{report}");
        }

        void FindAnimations()
        {
            _animHits = CanvasUpgradeProcessor.FindAnimatedRectBindings(_entries);
            _scaledCurveKeys.Clear();
            _scaledCurveKeys.UnionWith(CanvasUpgradeProcessor.LoadScaledCurveLog());
            _pendingAnimCurves = 0;

            var report = new StringBuilder();
            report.AppendLine($"ANIMATION SCAN — {_animHits.Count} RectTransform anchoredPosition/sizeDelta curve(s) found under the scanned canvases.");
            if (_animHits.Count > 0)
                report.AppendLine("Clips are SHARED ASSETS: scaling affects every scene/prefab using them. Curves already in the persistent scaled-curve log are skipped.");
            foreach (var hit in _animHits)
            {
                bool alreadyScaled = _scaledCurveKeys.Contains(hit.CurveKey);
                if (!alreadyScaled) _pendingAnimCurves++;
                report.AppendLine($"  clip {hit.ClipAssetPath} :: {hit.Binding.propertyName} on '{hit.Binding.path}' (animator: {hit.HostPath}, canvas: {hit.CanvasName})"
                                  + (alreadyScaled ? " — ALREADY SCALED, will be skipped" : ""));
            }

            _lastReport = report.ToString();
            Debug.Log($"[CanvasUpgrader]\n{_lastReport}");
        }

        void RunScaleAnimationKeys()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Scale UI Animation Keys x2.4");
            int group = Undo.GetCurrentGroup();
            string report = CanvasUpgradeProcessor.ScaleAnimationKeys(_animHits, _scaledCurveKeys, out int scaled);
            Undo.CollapseUndoOperations(group);

            string summary = $"Scaled {scaled} animation curve(s) x2.4. One Undo step. Save the project to persist the clip assets.";
            _lastReport = $"{summary}\n\n{report}";
            Debug.Log($"[CanvasUpgrader] {summary}\n{report}");
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        int CountSelected(bool includeUpgraded)
        {
            int n = 0;
            foreach (var entry in _entries)
                if (entry.Canvas != null && entry.Selected && (includeUpgraded || !entry.AlreadyUpgraded))
                    n++;
            return n;
        }

        static (Scene scene, string label) GetTargetScene()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
                return (stage.scene, $"Prefab Stage '{Path.GetFileNameWithoutExtension(stage.assetPath)}' ({stage.assetPath})");
            var scene = SceneManager.GetActiveScene();
            return (scene, $"Scene '{scene.name}'");
        }

        string TargetLabel()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null ? $"Prefab Stage '{Path.GetFileNameWithoutExtension(stage.assetPath)}'" : $"scene '{SceneManager.GetActiveScene().name}'";
        }
    }
}
