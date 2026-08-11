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
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    float radius = PrismOcclusionCorridor.MeasureCircumscribedRadius(root.transform);
                    radii.Add((root.name, radius, TopContributors(root.transform)));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
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

            report.AppendLine($"— Fleet median hull radius: {median:F2}. The corridor law wants these to track");
            report.AppendLine("  each hull's SIZE — a radius far off its vessel's visual bulk means a stray");
            report.AppendLine("  renderer (named above) or a rig whose bounds lie (see MeasureCircumscribedRadius's");
            report.AppendLine("  doc comment for the skinned-bounds rule).");

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
                if (filter.GetComponentInParent<CosmicShore.Gameplay.Skimmer>() != null) continue;
                entries.Add((CornerDistance(filter.sharedMesh.bounds, filter.transform, origin),
                             $"{filter.name} (mesh '{filter.sharedMesh.name}')"));
            }
            foreach (var skinned in vessel.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skinned.sharedMesh == null) continue;
                if (!skinned.enabled) continue;
                if (skinned.GetComponentInParent<CosmicShore.Gameplay.Skimmer>() != null) continue;
                var space = skinned.rootBone != null ? skinned.rootBone : skinned.transform;
                entries.Add((CornerDistance(skinned.localBounds, space, origin),
                             $"{skinned.name} (skinned '{skinned.sharedMesh.name}', root bone " +
                             $"{(skinned.rootBone != null ? skinned.rootBone.name : "<self>")})"));
            }

            entries.Sort((a, b) => b.dist.CompareTo(a.dist));
            var sb = new StringBuilder("top: ");
            for (int i = 0; i < Mathf.Min(3, entries.Count); i++)
                sb.Append($"{entries[i].label} @ {entries[i].dist:F2}{(i < Mathf.Min(3, entries.Count) - 1 ? " · " : string.Empty)}");
            return entries.Count == 0 ? "top: (no hull renderers found)" : sb.ToString();
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
