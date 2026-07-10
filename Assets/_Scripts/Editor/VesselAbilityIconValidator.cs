using System.Collections.Generic;
using System.Text;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Enforces the "every vessel presents four ability icons" contract. A vessel prefab is
    /// compliant when EITHER:
    ///   (a) its HUD contains a <see cref="VesselAbilityBar"/> with a four-slot
    ///       <see cref="VesselAbilitySetSO"/> assigned (authored path), OR
    ///   (b) a <c>Resources/VesselAbilitySets/{VesselClassType}.asset</c> exists for its
    ///       <see cref="VesselStatus.VesselType"/> — the zero-wire path where
    ///       <c>VesselHUDController</c> auto-adopts a bar at runtime.
    /// Run the menu item to report the fleet; flip <see cref="EnforceOnBuild"/> to make a
    /// non-compliant fleet fail the build (kept off until the stub vessels are resurrected so
    /// builds aren't broken by prefabs that are already unspawnable for other reasons).
    /// </summary>
    public static class VesselAbilityIconValidator
    {
        // Set to true once every vessel prefab is compliant to make the four-icon contract a hard
        // build gate ("impossible to ship a vessel without four icons").
        const bool EnforceOnBuild = false;

        const string VesselPrefabFolder = "Assets/_Prefabs/Spacevessels";
        const string AbilitySetFolder = "Assets/Resources/VesselAbilitySets";

        [MenuItem("Tools/Cosmic Shore/Validate Vessel Ability Icons")]
        public static void Validate()
        {
            var report = Scan(out int compliant, out int nonCompliant, out int total);
            Debug.Log($"[VesselAbilityIcons] Scanned {total} vessel prefab(s): " +
                      $"{compliant} compliant, {nonCompliant} non-compliant.\n{report}");

            if (nonCompliant > 0)
                Debug.LogWarning("[VesselAbilityIcons] Some vessels do not present four ability " +
                                 "icons. Either author a VesselAbilityBar (+ 4-slot set) in the HUD " +
                                 "prefab, or add a Resources/VesselAbilitySets/{VesselClassType} " +
                                 "asset for runtime auto-adoption.");
        }

        static string Scan(out int compliant, out int nonCompliant, out int total)
        {
            compliant = nonCompliant = total = 0;
            var sb = new StringBuilder();

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { VesselPrefabFolder });
            var seen = new HashSet<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seen.Add(path)) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) continue;

                total++;

                // Authored path: a bar with a set somewhere under the vessel/HUD.
                var bar = go.GetComponentInChildren<VesselAbilityBar>(true);
                if (bar && bar.HasAbilitySet)
                {
                    compliant++;
                    sb.AppendLine($"  ✓ authored bar        — {path}");
                    continue;
                }

                // Zero-wire path: an ability set exists for the vessel's class, so
                // VesselHUDController auto-adopts a bar at runtime.
                var status = go.GetComponentInChildren<VesselStatus>(true);
                if (status)
                {
                    var setPath = $"{AbilitySetFolder}/{status.VesselType}.asset";
                    var set = AssetDatabase.LoadAssetAtPath<VesselAbilitySetSO>(setPath);
                    if (set)
                    {
                        compliant++;
                        sb.AppendLine($"  ✓ auto-adopt ({status.VesselType,-8}) — {path}");
                        continue;
                    }

                    nonCompliant++;
                    sb.AppendLine($"  ✗ no bar, no set for '{status.VesselType}' — {path}");
                    continue;
                }

                nonCompliant++;
                sb.AppendLine($"  ✗ no bar, no VesselStatus — {path}");
            }

            return sb.ToString();
        }

        sealed class BuildGate : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                if (!EnforceOnBuild) return;

                Scan(out _, out int nonCompliant, out _);
                if (nonCompliant > 0)
                    throw new BuildFailedException(
                        $"[VesselAbilityIcons] {nonCompliant} vessel prefab(s) do not present four " +
                        "ability icons. Run Tools > Cosmic Shore > Validate Vessel Ability Icons.");
            }
        }
    }
}
