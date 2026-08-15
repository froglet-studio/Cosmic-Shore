using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Fleet audit for the two-layer vessel jet FX law (<c>Docs/VESSEL_JET_FX.md</c>).
    ///
    /// Asset-only — no play mode. It reuses the SHIPPED resolution predicates
    /// (<see cref="VesselJetFXConfigSO.IsMountName"/> / <c>IsMountNameLoose</c>) rather than a
    /// private copy, so the report and the game cannot disagree about what counts as an engine
    /// mount. That is the same rule the elemental-morph and skimmer auditors follow.
    ///
    /// What it answers, per vessel: will this vessel end up with a domain-tinted beacon ribbon
    /// and domain-tinted engine plumes, where do the plumes land, and does anything about the
    /// wiring make that silently not happen.
    /// </summary>
    public static class VesselJetFXAudit
    {
        const string VesselPrefabFolder = "Assets/_Prefabs/Spacevessels";

        [MenuItem("FrogletTools/Vessels/Audit Vessel Jet FX")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 4,
            Description = "Reports every vessel's beacon ribbon + engine plumes and where the " +
                          "plumes mount, using the shipped resolver. Read-only.")]
        public static void Run()
        {
            var config = Resources.Load<VesselJetFXConfigSO>("VesselJetFXConfig");
            var report = new StringBuilder();
            report.AppendLine("=== Vessel Jet FX audit (Docs/VESSEL_JET_FX.md) ===");

            if (config == null)
            {
                Debug.LogError("[VesselJetFXAudit] No Resources/VesselJetFXConfig asset. " +
                               "Every vessel will run without jet FX.");
                return;
            }
            if (!config.IsSane)
                report.AppendLine("!! CONFIG OUT OF RANGE (IsSane == false) — jet FX will be skipped at runtime.");
            if (config.EnginePlumePrefab == null)
                report.AppendLine("!! No enginePlumePrefab — the plume layer is OFF fleet-wide.");
            if (config.BeaconRibbonPrefab == null)
                report.AppendLine("!! No beaconRibbonPrefab — the beacon layer is OFF fleet-wide.");
            report.AppendLine();

            int problems = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { VesselPrefabFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(p => p.StartsWith(VesselPrefabFolder + "/"))
                         .OrderBy(p => p))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;
                if (root.GetComponent<VesselStatus>() == null) continue; // not a vessel

                problems += AuditVessel(root, config, report);
            }

            report.AppendLine();
            report.AppendLine(problems == 0
                ? "All vessels satisfy the jet FX law."
                : $"{problems} vessel(s) need attention.");

            if (problems == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        static int AuditVessel(GameObject root, VesselJetFXConfigSO config, StringBuilder report)
        {
            string name = root.name;
            int problems = 0;

            bool hasController = root.GetComponent<VesselController>() != null;
            bool hasTint = root.GetComponentInChildren<VesselTrailCustomization>(true) != null;
            bool hasJetFX = root.GetComponentInChildren<VesselJetFX>(true) != null;

            var trails = root.GetComponentsInChildren<TrailRenderer>(true);
            bool beaconAuthored = trails.Length > 0;
            bool plumesAuthored = trails.Any(t => HasEngineAncestor(t.transform, root.transform, config));

            var mounts = ResolveMounts(root, config);

            report.AppendLine($"--- {name}");
            report.AppendLine($"      authored trails : {trails.Length}" +
                              $" (beacon {(beaconAuthored ? "authored" : "will be spawned")}," +
                              $" plumes {(plumesAuthored ? "authored" : "will be spawned")})");
            report.AppendLine($"      engine mounts   : {(mounts.Count == 0 ? "NONE — derived rear pair" : string.Join(", ", mounts.Select(m => m.name)))}");

            // The one thing that silently switches the whole law off for a vessel.
            if (!hasController)
            {
                report.AppendLine("      !! NO VesselController — Initialize never runs, so this vessel gets NO jet FX. " +
                                  "It also cannot be spawned by the normal player/vessel pipeline.");
                problems++;
            }

            // Not fatal (VesselStatus.JetFX GetOrAdds at runtime), but an unauthored component is
            // invisible in the inspector and the fleet contract says author it.
            if (!hasJetFX)
                report.AppendLine("      .  VesselJetFX not authored in the prefab (added at runtime by VesselStatus.JetFX).");
            if (!hasTint)
                report.AppendLine("      .  VesselTrailCustomization not authored in the prefab (added at runtime).");

            if (mounts.Count == 0 && plumesAuthored == false)
                report.AppendLine("      .  Model exposes no engine geometry; plumes will be DERIVED at the rear of the hull. " +
                                  "Worth an art pass — see Docs/VESSEL_JET_FX.md.");

            if (mounts.Count > config.MaxEnginePlumes)
            {
                report.AppendLine($"      !! {mounts.Count} mounts exceeds maxEnginePlumes ({config.MaxEnginePlumes}); " +
                                  "the surplus is dropped and the vessel will look asymmetric.");
                problems++;
            }

            return problems;
        }

        static bool HasEngineAncestor(Transform t, Transform root, VesselJetFXConfigSO config)
        {
            for (var p = t; p != null && p != root; p = p.parent)
                if (config.IsMountNameLoose(p.name)) return true;
            return false;
        }

        /// <summary>
        /// Mirrors <c>VesselJetFX.ResolveMounts</c>'s name + structural filters. Kept in step by
        /// calling the config's own predicates; the structural half is re-stated here because the
        /// runtime version needs a live hierarchy, and a prefab asset reports
        /// activeInHierarchy == false for everything in it (it is in no scene) — so this uses
        /// activeSelf up the chain instead.
        /// </summary>
        static List<Transform> ResolveMounts(GameObject root, VesselJetFXConfigSO config)
        {
            var bones = new HashSet<Transform>();
            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skinned.bones == null) continue;
                foreach (var bone in skinned.bones)
                    if (bone != null) bones.Add(bone);
            }

            var mounts = new List<Transform>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root.transform) continue;
                if (!config.IsMountName(child.name)) continue;
                if (child.GetComponentInParent<Skimmer>(true) != null) continue;
                if (child.GetComponentInParent<Canvas>(true) != null) continue;
                if (!IsPlausibleMount(child, root.transform, bones)) continue;
                mounts.Add(child);
            }
            mounts.Sort((a, b) => string.CompareOrdinal(Path(a), Path(b)));
            if (mounts.Count > config.MaxEnginePlumes)
                mounts.RemoveRange(config.MaxEnginePlumes, mounts.Count - config.MaxEnginePlumes);
            return mounts;
        }

        static bool IsPlausibleMount(Transform t, Transform root, HashSet<Transform> bones)
        {
            if (bones.Contains(t)) return true;
            if (!t.TryGetComponent<Renderer>(out var renderer) || !renderer.enabled) return false;
            for (var p = t; p != null && p != root.parent; p = p.parent)
                if (!p.gameObject.activeSelf) return false;
            return true;
        }

        static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
