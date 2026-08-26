using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// <b>FrogletTools ▸ Vessels ▸ Audit Corridor Vessel Radii.</b>
    ///
    /// The occlusion corridor is SHIP-SIZED: its cone radii are multiples of the vessel's
    /// own circumscribing hull radius, measured at bind by
    /// <see cref="PrismOcclusionCorridor.MeasureCircumscribedRadius"/> with nothing
    /// authored per vessel (Docs/PRISM_ANIMATION.md §4.7). That makes the measurement the
    /// single point where a whole vessel's dither can silently go wrong — a hidden
    /// placeholder mesh, a skinned rig whose scale lives in the armature, an off-origin
    /// model — and nothing reports it until a player says "the Sparrow looks off".
    ///
    /// This auditor runs the EXACT runtime measurement over every vessel prefab
    /// (asset-only, no play mode), prints each vessel's radius and the corridor's
    /// resulting world-space cone radii, and names the TOP CONTRIBUTING renderers with
    /// their distances — so an inflated radius comes with its offender attached. Fleet
    /// outliers (beyond 3× the fleet median) are flagged.
    ///
    /// READER tool: reports only, writes nothing — no change ledger, no ship panel.
    /// </summary>
    public static class VesselCorridorRadiusAuditor
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";

        [MenuItem("FrogletTools/Vessels/Audit Corridor Vessel Radii", false, 62)]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Measure every vessel's corridor hull radius with the exact runtime code, " +
                          "name the top contributing renderers, and flag fleet outliers.")]
        public static void Run()
        {
            var config = Resources.Load<CosmicShore.ScriptableObjects.PrismOcclusionConfigSO>("PrismOcclusionConfig");
            float outerScale = config != null ? config.OuterRadiusScale : 1f;
            float innerScale = config != null ? config.InnerRadiusScale : 0.25f;

            var report = new StringBuilder();
            report.AppendLine("— Corridor vessel radii (the exact runtime measurement, per prefab):");

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder });
            var radii = new System.Collections.Generic.List<(string name, float radius, string detail)>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // LoadAssetAtPath, NOT LoadPrefabContents: the asset representation already
                // carries the merged hierarchy (nested prefabs included — the same traversal
                // PrismOcclusionCoverageTests relies on), and reading it needs no preview
                // SCENE. LoadPrefabContents opens one per prefab, which is both far heavier
                // and a second failure surface — it spilled native parse errors and callstacks
                // for every vessel when two prefabs carried malformed fileIDs. A reader tool
                // must not be the thing that breaks on bad data; it should report on it.
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    radii.Add((System.IO.Path.GetFileNameWithoutExtension(path), 0f,
                               "top: (prefab failed to load — check the console for parse errors)"));
                    continue;
                }

                // The folder also holds sub-prefabs a vessel is BUILT from (Skimmer,
                // VesselTail, PipCamera, VesselJet, GyroidAssemblerPrefab...). They are not
                // vessels, they have no corridor of their own, and reporting them as
                // "UNMEASURABLE" is noise that buries the real rows. VesselController is
                // the discriminator because it is the exact component whose Initialize
                // binds the corridor — if it is absent, nothing ever measures this prefab.
                if (root.GetComponent<CosmicShore.Gameplay.VesselController>() == null) continue;

                float radius = PrismOcclusionCorridor.MeasureCircumscribedRadius(root.transform);
                radii.Add((root.name, radius, TopContributors(root.transform)));
            }

            radii.Sort((a, b) => a.radius.CompareTo(b.radius));
            float median = radii.Count > 0 ? radii[radii.Count / 2].radius : 0f;

            foreach (var (name, radius, detail) in radii)
            {
                bool zero = radius <= 0f;
                bool outlier = median > 0f && radius > 3f * median;
                string flag = zero ? "  ❌ UNMEASURABLE (corridor falls back to config radius)"
                            : outlier ? $"  ⚠ OUTLIER (> 3× fleet median {median:F1})" : string.Empty;
                report.AppendLine($"   {name,-10} hull {radius,6:F2}  → cone outer {radius * outerScale,6:F2} / core {radius * innerScale,5:F2}{flag}");
                report.AppendLine($"      {detail}");
            }

            if (radii.Count == 0)
            {
                report.AppendLine("   (no vessel prefabs found — does the folder still hold them?)");
                Debug.Log(report.ToString());
                return;
            }

            report.AppendLine($"— Fleet median hull radius: {median:F2}, spread {radii[0].radius:F2}..{radii[radii.Count - 1].radius:F2}.");
            report.AppendLine("  The corridor law wants these to track each hull's SIZE, so a wide spread is a");
            report.AppendLine("  fleet-consistency problem, not a corridor one: a radius far off its vessel's");
            report.AppendLine("  visual bulk means a stray renderer (named above), a placeholder/test mesh, or a");
            report.AppendLine("  mis-scaled model instance. Fix the ART; do not compensate in the corridor config,");
            report.AppendLine("  which is fleet-wide and would just move the error onto the correct vessels.");

            Debug.Log(report.ToString());
        }

        /// <summary>The three farthest-reaching renderers, with their corner distances —
        /// the measurement's own walk, kept in lockstep with the runtime rules.</summary>
        static string TopContributors(Transform vessel)
        {
            var entries = new System.Collections.Generic.List<(float dist, string label)>();
            Vector3 origin = vessel.position;

            foreach (var filter in vessel.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                if (!filter.TryGetComponent<MeshRenderer>(out var renderer)) continue;
                if (!renderer.enabled) continue;
                if (filter.GetComponentInParent<CosmicShore.Gameplay.Skimmer>(true) != null) continue;
                entries.Add((CornerDistance(filter.sharedMesh.bounds, filter.transform, origin),
                             $"{filter.name} (mesh '{filter.sharedMesh.name}')"));
            }
            foreach (var skinned in vessel.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skinned.sharedMesh == null) continue;
                if (!skinned.enabled) continue;
                if (skinned.GetComponentInParent<CosmicShore.Gameplay.Skimmer>(true) != null) continue;
                var space = skinned.rootBone != null ? skinned.rootBone : skinned.transform;
                entries.Add((CornerDistance(skinned.localBounds, space, origin),
                             $"{skinned.name} (skinned '{skinned.sharedMesh.name}', root bone " +
                             $"{(skinned.rootBone != null ? skinned.rootBone.name : "<self>")})"));
            }

            entries.Sort((a, b) => b.dist.CompareTo(a.dist));

            // What the skimmer exclusion removed, and how big it was. A vessel whose
            // largest EXCLUDED volume dwarfs its hull is the normal, healthy case (a
            // skimmer field is meant to be bigger than the ship); a vessel reporting
            // none, while visibly carrying a forcefield, means the exclusion did not
            // fire and the "hull" number above is really the skimmer.
            float excludedMax = 0f;
            int excludedCount = 0;
            foreach (var filter in vessel.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                if (!filter.TryGetComponent<MeshRenderer>(out _)) continue;
                if (filter.GetComponentInParent<CosmicShore.Gameplay.Skimmer>(true) == null) continue;
                excludedCount++;
                excludedMax = Mathf.Max(excludedMax, CornerDistance(filter.sharedMesh.bounds, filter.transform, origin));
            }
            foreach (var skinned in vessel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.sharedMesh == null) continue;
                if (skinned.GetComponentInParent<CosmicShore.Gameplay.Skimmer>(true) == null) continue;
                excludedCount++;
                var sp = skinned.rootBone != null ? skinned.rootBone : skinned.transform;
                excludedMax = Mathf.Max(excludedMax, CornerDistance(skinned.localBounds, sp, origin));
            }
            var sb = new StringBuilder("top: ");
            for (int i = 0; i < Mathf.Min(3, entries.Count); i++)
                sb.Append($"{entries[i].label} @ {entries[i].dist:F2}{(i < Mathf.Min(3, entries.Count) - 1 ? " · " : string.Empty)}");
            sb.Append(excludedCount > 0
                ? $"  [skimmer exclusion removed {excludedCount} renderer(s), largest @ {excludedMax:F2}]"
                : "  [skimmer exclusion removed nothing]");
            return entries.Count == 0 ? "top: (no hull renderers found)" + sb.ToString().Substring("top: ".Length) : sb.ToString();
        }

        static float CornerDistance(Bounds localBounds, Transform space, Vector3 origin)
        {
            Vector3 c = localBounds.center, e = localBounds.extents;
            float maxSqr = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z));
                maxSqr = Mathf.Max(maxSqr, (space.TransformPoint(corner) - origin).sqrMagnitude);
            }
            return Mathf.Sqrt(maxSqr);
        }
    }
}
