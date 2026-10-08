using System.Collections.Generic;
using System.IO;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// <b>WRITER, keeper.</b> Solves every crystal → hull fusion in
    /// <c>Resources/CrystalHullFusionConfig</c> at EDIT time and bakes the answer into a
    /// <see cref="CrystalHullFusionBakeSO"/> per entry, so the game does no fusion geometry at all
    /// (<c>Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md</c> §4).
    ///
    /// The solve is <see cref="CrystalHullFusionGeometry.Solve"/> - the same code the runtime falls
    /// back to on a worker thread - fed from the two ASSETS: the vessel prefab's hull mesh in its
    /// bind pose and the element's crystal prefab. So a bake is exactly what the runtime would have
    /// computed, computed once.
    ///
    /// Re-run it whenever the hull model, the crystal model, an entry's <c>tileFill</c> /
    /// <c>surfaceLift</c> or the solver changes: the window, its Validate step and
    /// <c>CrystalHullFusionBakeTests</c> all report a stale bake, and the game warns once and falls
    /// back to solving at runtime until it is re-baked. Idempotent: an unchanged input bakes an
    /// unchanged asset (the template mesh is rewritten in place, keeping its file ID).
    /// </summary>
    public class CrystalHullFusionBaker : EditorWindow
    {
        const string ToolName = "Bake Crystal Hull Fusions";
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string BakeFolder = "Assets/_SO_Assets/CrystalHullFusion";

        static readonly FrogletToolShipContext Ship = new FrogletToolShipContext(ToolName)
        {
            ToolScriptPaths = new[] { "Assets/_Scripts/Editor/CrystalHullFusionBaker.cs" },
            Validate = ValidateAll,
            CommitType = "feat",
            CommitScope = "crystal",
            // The panel passes the STAGED-PATH count (bake + meta + folder meta + config), not the
            // number of fusions - the first real push read "bake 4 fusions" for one - so the subject
            // names none.
            CommitSubject = _ => "feat(crystal): bake crystal hull fusions",
        };

        /// <summary>One entry's bake, judged against the assets as they are now.</summary>
        public enum BakeState { Current = 0, Missing = 1, Stale = 2, Unresolvable = 3 }

        public readonly struct Status
        {
            public readonly BakeState State;
            public readonly string Detail;
            public Status(BakeState state, string detail) { State = state; Detail = detail; }
        }

        Vector2 _scroll;
        string _report = string.Empty;
        readonly List<Status> _status = new();
        System.Action _deferred;

        [MenuItem("FrogletTools/Vessels/Bake Crystal Hull Fusions", false, 24)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 3,
            Description = "Solve where each elemental crystal's faces land on each vessel hull, at edit time, " +
                          "and bake it - so a pickup in game does no geometry. Re-run when a hull or crystal model changes.",
            DocPath = "Assets/_Scripts/Controller/Environment/Crystals/CRYSTAL_HULL_FUSION.md")]
        public static void Open()
        {
            var w = GetWindow<CrystalHullFusionBaker>("Hull Fusion Baker");
            w.minSize = new Vector2(480f, 360f);
            w.Refresh();
            w.Show();
        }

        void OnEnable() => Refresh();

        void Refresh()
        {
            _status.Clear();
            var config = CrystalHullFusionConfigSO.Load();
            if (!config) return;
            foreach (var entry in config.Entries) _status.Add(Evaluate(entry));
        }

        void OnGUI()
        {
            var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.Vessels);
            FrogletEditorPalette.Banner("Crystal Hull Fusion Baker",
                "Solves where a crystal's faces land on a hull, once, at edit time - the game just reads the answer.",
                accent);

            var config = CrystalHullFusionConfigSO.Load();
            if (!config)
            {
                EditorGUILayout.HelpBox($"Resources/{CrystalHullFusionConfigSO.ResourcePath} is missing - nothing to bake.", MessageType.Error);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(90f))) _deferred = Refresh;
                GUILayout.FlexibleSpace();
                if (FrogletEditorPalette.ColorButton("Bake all", FrogletEditorPalette.Ok, 140f, 24f))
                    _deferred = () => { _report = BakeAll(); Refresh(); };
            }

            FrogletEditorPalette.HorizontalRule();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var entries = config.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;
                var status = i < _status.Count ? _status[i] : Evaluate(entry);

                var row = EditorGUILayout.BeginVertical(GUILayout.MinHeight(46f));
                FrogletEditorPalette.DrawCard(row, FrogletEditorPalette.SurfaceRaised, FrogletEditorPalette.Surface);
                FrogletEditorPalette.DrawAccentStripe(row, PillColour(status.State));
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8f);
                    EditorGUILayout.LabelField($"{entry.vessel} × {entry.element}", FrogletEditorPalette.CardTitle, GUILayout.Width(200f));
                    var pill = GUILayoutUtility.GetRect(110f, 20f, GUILayout.Width(110f));
                    FrogletEditorPalette.StatusPill(pill, status.State.ToString().ToUpperInvariant(), PillColour(status.State));
                    GUILayout.FlexibleSpace();
                    int index = i;
                    if (GUILayout.Button("Bake", GUILayout.Width(70f)))
                        _deferred = () => { _report = BakeOne(config.Entries[index]); Refresh(); };
                }
                if (!string.IsNullOrEmpty(status.Detail))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(8f);
                        EditorGUILayout.LabelField(status.Detail, FrogletEditorPalette.CardBodyWrapped);
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4f);
            }
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_report))
            {
                FrogletEditorPalette.HorizontalRule();
                EditorGUILayout.TextArea(_report, GUILayout.MinHeight(80f));
            }

            FrogletToolShipPanel.Draw(Ship, this);

            if (_deferred != null && Event.current.type == EventType.Repaint)
            {
                var run = _deferred;
                _deferred = null;
                EditorApplication.delayCall += () => { run(); Repaint(); };
            }
        }

        static Color PillColour(BakeState state) => state switch
        {
            BakeState.Current => FrogletEditorPalette.Ok,
            BakeState.Missing => FrogletEditorPalette.Warn,
            BakeState.Stale => FrogletEditorPalette.Warn,
            _ => FrogletEditorPalette.Error,
        };

        // ══ Resolving an entry's assets ═══════════════════════════════════════════════════════

        /// <summary>The entry's hull (from the vessel prefab) and crystal (from the element set),
        /// resolved exactly as the runtime resolves them. Null hull = unresolvable, with why.</summary>
        public static bool TryResolve(CrystalHullFusionConfigSO.Entry entry, out SkinnedMeshRenderer hull,
                                      out Mesh drawn, out Mesh source, out int plates, out string why)
        {
            hull = null;
            drawn = source = null;
            plates = 0;
            why = null;

            string prefabPath = $"{VesselFolder}/{entry.vessel}.prefab";
            var vessel = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!vessel) { why = $"no vessel prefab at {prefabPath}"; return false; }
            var animation = vessel.GetComponentInChildren<VesselAnimation>(true);
            hull = CrystalHullFusion.FindHullRenderer(animation ? animation.transform : vessel.transform, requireActive: false);
            if (!hull) { why = $"{entry.vessel} has no SkinnedMeshRenderer hull"; return false; }

            var set = ElementalCrystalSetSO.Load();
            var crystal = set ? set.GetPrefab(entry.element) : null;
            if (!crystal) { why = $"Resources/{ElementalCrystalSetSO.ResourcePath} has no {entry.element} crystal"; return false; }
            if (!CrystalHullFusion.TryResolveCrystal(crystal, out drawn, out source, out plates, out _, out _))
            {
                why = $"the {entry.element} crystal has no model with a mesh";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Is this entry's bake current? The runtime's cheap fingerprint check plus the content
        /// hashes, which only edit time can afford to compute.
        /// </summary>
        public static Status Evaluate(CrystalHullFusionConfigSO.Entry entry)
        {
            if (!TryResolve(entry, out var hull, out _, out var source, out int plates, out string why))
                return new Status(BakeState.Unresolvable, why);

            var bake = entry.bake;
            if (!bake) return new Status(BakeState.Missing, "No bake - the game solves this at runtime on a worker thread.");

            if (!bake.Matches(hull.sharedMesh, source, plates, entry, CrystalHullFusion.FaceSubdivisions, out why))
                return new Status(BakeState.Stale, why);

            var hullMesh = hull.sharedMesh;
            if (hullMesh.isReadable &&
                CrystalHullFusionGeometry.ContentHash(hullMesh.vertices, hullMesh.triangles) != bake.HullHash)
                return new Status(BakeState.Stale, $"'{hullMesh.name}' was re-exported with the same vertex count but different geometry");
            if (source.isReadable &&
                CrystalHullFusionGeometry.ContentHash(source.vertices, source.triangles) != bake.CrystalHash)
                return new Status(BakeState.Stale, $"'{source.name}' was re-exported with the same vertex count but different geometry");

            var layout = bake.Solution.Layout;
            return new Status(BakeState.Current,
                $"{bake.Solution.FaceCount} faces × {bake.Solution.PointsPerFace} points, " +
                $"{layout.Unprojected} of {layout.Projected + layout.Unprojected} off the skin - {AssetDatabase.GetAssetPath(bake)}");
        }

        static FrogletToolValidation ValidateAll()
        {
            var config = CrystalHullFusionConfigSO.Load();
            if (!config) return FrogletToolValidation.Fail("No CrystalHullFusionConfig in Resources.");
            var problems = new List<string>();
            foreach (var entry in config.Entries)
            {
                if (entry == null) continue;
                var status = Evaluate(entry);
                if (status.State != BakeState.Current)
                    problems.Add($"{entry.vessel} × {entry.element}: {status.State} - {status.Detail}");
            }
            return problems.Count == 0
                ? FrogletToolValidation.Pass($"All {config.Entries.Count} crystal hull fusion bake(s) are current.")
                : FrogletToolValidation.Fail("A crystal hull fusion bake is missing or stale.", problems);
        }

        // ══ Baking ════════════════════════════════════════════════════════════════════════════

        static string BakeAll()
        {
            var config = CrystalHullFusionConfigSO.Load();
            if (!config) return "No config.";
            var log = new StringBuilder();
            foreach (var entry in config.Entries)
                if (entry != null) log.AppendLine(BakeOne(entry));
            return log.ToString();
        }

        /// <summary>Solves one entry, writes its bake asset and points the entry at it. Returns a
        /// one-line report; writes nothing when the solve fails.</summary>
        public static string BakeOne(CrystalHullFusionConfigSO.Entry entry)
        {
            string label = $"{entry.vessel} × {entry.element}";
            if (!TryResolve(entry, out var hull, out var drawn, out var source, out int plates, out string why))
                return $"✗ {label}: {why}";

            var input = CrystalHullFusion.CaptureSolveInput(hull, drawn, entry, out why);
            if (input == null) return $"✗ {label}: {why}";

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var solution = CrystalHullFusionGeometry.Solve(input, out why);
            if (solution == null) return $"✗ {label}: {why}";
            watch.Stop();

            if (!AssetDatabase.IsValidFolder(BakeFolder))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(BakeFolder)?.Replace('\\', '/'), Path.GetFileName(BakeFolder));
                FrogletToolChangeLedger.Record(ToolName, BakeFolder);
            }

            string path = $"{BakeFolder}/{entry.vessel}_{entry.element}_HullFusionBake.asset";
            var bake = AssetDatabase.LoadAssetAtPath<CrystalHullFusionBakeSO>(path);
            bool created = !bake;
            if (created)
            {
                bake = CreateInstance<CrystalHullFusionBakeSO>();
                AssetDatabase.CreateAsset(bake, path);
            }

            // The template mesh is a sub-asset, rewritten IN PLACE when it exists so its file ID - and
            // every reference to it - survives a re-bake.
            var fresh = CrystalHullFusion.BuildTemplateMesh(solution, $"{entry.vessel}_{entry.element}_FusionTemplate");
            var template = bake.TemplateMesh;
            if (template && AssetDatabase.GetAssetPath(template) == path)
            {
                EditorUtility.CopySerialized(fresh, template);
                DestroyImmediate(fresh);
            }
            else
            {
                template = fresh;
                AssetDatabase.AddObjectToAsset(template, bake);
            }

            var hullMesh = hull.sharedMesh;
            bake.EditorWrite(hullMesh, CrystalHullFusionGeometry.ContentHash(input.HullVertices, input.HullTriangles),
                source, plates, CrystalHullFusionGeometry.ContentHash(source.vertices, source.triangles),
                entry, CrystalHullFusion.FaceSubdivisions, template, solution);
            EditorUtility.SetDirty(template);
            EditorUtility.SetDirty(bake);
            FrogletToolChangeLedger.Record(ToolName, path);

            // Point the entry at its bake.
            var config = CrystalHullFusionConfigSO.Load();
            if (config && entry.bake != bake)
            {
                Undo.RecordObject(config, "Assign crystal hull fusion bake");
                entry.bake = bake;
                EditorUtility.SetDirty(config);
                FrogletToolChangeLedger.Record(ToolName, AssetDatabase.GetAssetPath(config));
            }

            AssetDatabase.SaveAssets();
            var layout = solution.Layout;
            return $"✓ {label}: {(created ? "baked" : "re-baked")} {solution.FaceCount} faces × {solution.PointsPerFace} points " +
                   $"({layout.Unprojected} of {layout.Projected + layout.Unprojected} off the skin) in {watch.ElapsedMilliseconds} ms → {path}";
        }
    }
}
