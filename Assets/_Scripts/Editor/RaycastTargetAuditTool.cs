using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Audits and fixes UI raycast-target bloat — the driver of EventSystem.Update
    /// cost. Every Graphic with raycastTarget ON is hit-tested per pointer move per
    /// frame; a scan of this project found Menu_Main carrying ~859 enabled targets
    /// and the Squirrel vessel's HUD another ~148, almost all pure display.
    ///
    /// A graphic NEEDS its raycast target only when clicks must land on it:
    /// this tool keeps any graphic that has a Selectable (Button/Toggle/Slider/…)
    /// or any IEventSystemHandler (EventTrigger, custom pointer handlers) on itself
    /// or an ancestor — those form interactive hit areas — and flags everything
    /// else as a candidate to disable.
    ///
    /// Deliberately supervised (no blind batch edit): run it per scene or prefab,
    /// review the candidate list, disable, then click through the affected UI once.
    /// Scene changes are Undo-able; prefab assets are edited through the prefab
    /// contents workflow and saved in place.
    /// </summary>
    public class RaycastTargetAuditTool : EditorWindow
    {
        Vector2 _scroll;
        readonly List<Graphic> _sceneCandidates = new();
        readonly List<string> _prefabCandidatePaths = new();
        string _lastReport = "Scan the open scene or the selected prefabs to begin.";

        [MenuItem("FrogletTools/Interface/Raycast Target Audit")]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 3,
            Description = "Find graphics needlessly eating raycasts.")]
        static void Open() => GetWindow<RaycastTargetAuditTool>("Raycast Audit");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Scans for Graphics with 'Raycast Target' enabled that no Selectable or " +
                "pointer handler needs. Scope: the current Project selection when it contains " +
                "prefabs (or folders of prefabs), otherwise every root of the open scene.",
                MessageType.Info);

            if (GUILayout.Button("Scan"))
                Scan();

            using (new EditorGUI.DisabledScope(_sceneCandidates.Count == 0))
            {
                if (GUILayout.Button($"Select {_sceneCandidates.Count} scene candidates in Hierarchy"))
                    SelectSceneCandidates();
                if (GUILayout.Button($"Disable {_sceneCandidates.Count} scene candidates (Undo-able)"))
                    DisableSceneCandidates();
            }

            using (new EditorGUI.DisabledScope(_prefabCandidatePaths.Count == 0))
            {
                if (GUILayout.Button($"Disable candidates in {_prefabCandidatePaths.Count} selected prefab(s)"))
                    DisableInPrefabs();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------------
        //  Scan
        // ------------------------------------------------------------------

        void Scan()
        {
            _sceneCandidates.Clear();
            _prefabCandidatePaths.Clear();

            var report = new System.Text.StringBuilder();
            var prefabPaths = CollectSelectedPrefabPaths();

            if (prefabPaths.Count > 0)
            {
                foreach (var path in prefabPaths)
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var (total, kept, candidates) = Classify(root, null);
                        report.AppendLine($"{path}\n  raycastTargets on: {total}, needed: {kept}, candidates: {candidates.Count}");
                        if (candidates.Count > 0)
                            _prefabCandidatePaths.Add(path);
                        foreach (var g in candidates)
                            report.AppendLine($"    - {PathOf(g.transform, root.transform)}");
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            else
            {
                int total = 0, kept = 0;
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                foreach (var root in scene.GetRootGameObjects())
                {
                    var (t, k, candidates) = Classify(root, _sceneCandidates);
                    total += t;
                    kept += k;
                }
                report.AppendLine($"Scene '{scene.name}': raycastTargets on: {total}, needed: {kept}, candidates: {_sceneCandidates.Count}");
                foreach (var g in _sceneCandidates)
                    report.AppendLine($"  - {PathOf(g.transform, null)}");
            }

            _lastReport = report.ToString();
            Debug.Log($"[RaycastTargetAudit]\n{_lastReport}");
        }

        static List<string> CollectSelectedPrefabPaths()
        {
            var paths = new List<string>();
            foreach (var obj in Selection.objects)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;

                if (path.EndsWith(".prefab"))
                {
                    if (!paths.Contains(path)) paths.Add(path);
                }
                else if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
                    {
                        var p = AssetDatabase.GUIDToAssetPath(guid);
                        if (!paths.Contains(p)) paths.Add(p);
                    }
                }
            }
            return paths;
        }

        /// <summary>
        /// Splits a hierarchy's enabled raycast targets into needed (a Selectable or
        /// pointer handler on self/ancestor uses them as a hit area) vs candidates.
        /// </summary>
        static (int total, int kept, List<Graphic> candidates) Classify(GameObject root, List<Graphic> sink)
        {
            int total = 0, kept = 0;
            var candidates = sink ?? new List<Graphic>();

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget) continue;
                total++;

                if (NeedsRaycast(graphic))
                    kept++;
                else
                    candidates.Add(graphic);
            }
            return (total, kept, candidates);
        }

        static bool NeedsRaycast(Graphic graphic)
        {
            // Interactive hit area: any Selectable on this object or an ancestor
            // (a Button's child graphics extend its clickable surface)...
            if (graphic.GetComponentInParent<Selectable>(true) != null)
                return true;

            // ...or any pointer/event handler (EventTrigger, custom IPointerClick
            // implementations, drag handlers, ScrollRect, etc.).
            for (var t = graphic.transform; t != null; t = t.parent)
            {
                if (t.TryGetComponent(out IEventSystemHandler _))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        //  Apply
        // ------------------------------------------------------------------

        void SelectSceneCandidates()
        {
            var gos = new List<GameObject>();
            foreach (var g in _sceneCandidates)
                if (g) gos.Add(g.gameObject);
            Selection.objects = gos.ToArray();
        }

        void DisableSceneCandidates()
        {
            int changed = 0;
            foreach (var g in _sceneCandidates)
            {
                if (!g) continue;
                Undo.RecordObject(g, "Disable Raycast Target");
                g.raycastTarget = false;
                EditorUtility.SetDirty(g);
                changed++;
            }
            Debug.Log($"[RaycastTargetAudit] Disabled raycastTarget on {changed} scene graphics (Undo available). Save the scene to keep.");
            _sceneCandidates.Clear();
        }

        void DisableInPrefabs()
        {
            int prefabs = 0, changed = 0;
            foreach (var path in _prefabCandidatePaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var (_, _, candidates) = Classify(root, null);
                    if (candidates.Count == 0) continue;
                    foreach (var g in candidates)
                    {
                        g.raycastTarget = false;
                        changed++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[RaycastTargetAudit] Disabled raycastTarget on {changed} graphics across {prefabs} prefab(s).");
            _prefabCandidatePaths.Clear();
        }

        static string PathOf(Transform t, Transform stopAt)
        {
            var parts = new List<string>();
            while (t != null && t != stopAt)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
