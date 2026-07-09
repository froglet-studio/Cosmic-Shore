using System.Collections.Generic;
using System.Text;
using CosmicShore.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Enforces the "every vessel presents four ability icons" contract. A vessel HUD is compliant
    /// when it contains a <see cref="VesselAbilityBar"/> with a four-slot <c>VesselAbilitySetSO</c>
    /// assigned. Run the menu item to report the fleet; flip <see cref="EnforceOnBuild"/> to make a
    /// non-compliant fleet fail the build (kept off until every vessel has been migrated so builds
    /// aren't broken while the ability sets are still being authored).
    /// </summary>
    public static class VesselAbilityIconValidator
    {
        // Set to true once every vessel HUD has a compliant VesselAbilityBar to make the four-icon
        // contract a hard build gate ("impossible to ship a vessel without four icons").
        const bool EnforceOnBuild = false;

        static readonly string[] SearchFolders =
        {
            "Assets/_Prefabs/Spacevessels",
            "Assets/_Prefabs/UI Elements/VesselHUD",
        };

        [MenuItem("Tools/Cosmic Shore/Validate Vessel Ability Icons")]
        public static void Validate()
        {
            var report = Scan(out int compliant, out int barNoSet, out int noBar, out int total);
            Debug.Log($"[VesselAbilityIcons] Scanned {total} vessel/HUD prefab(s): " +
                      $"{compliant} compliant, {barNoSet} bar-without-set, {noBar} missing a bar.\n{report}");

            if (barNoSet > 0 || noBar > 0)
                Debug.LogWarning("[VesselAbilityIcons] Some vessels do not yet present four ability " +
                                 "icons. Add a VesselAbilityBar + a 4-slot VesselAbilitySetSO to each " +
                                 "(unfilled slots show an obvious placeholder).");
        }

        static string Scan(out int compliant, out int barNoSet, out int noBar, out int total)
        {
            compliant = barNoSet = noBar = total = 0;
            var sb = new StringBuilder();

            var guids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
            var seen = new HashSet<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!seen.Add(path)) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) continue;

                total++;
                var bar = go.GetComponentInChildren<VesselAbilityBar>(true);
                if (!bar)
                {
                    noBar++;
                    sb.AppendLine($"  ✗ no ability bar   — {path}");
                }
                else if (!bar.HasAbilitySet)
                {
                    barNoSet++;
                    sb.AppendLine($"  ⚠ bar, no set      — {path}");
                }
                else
                {
                    compliant++;
                    sb.AppendLine($"  ✓ four icons       — {path}");
                }
            }

            return sb.ToString();
        }

        sealed class BuildGate : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                if (!EnforceOnBuild) return;

                Scan(out _, out int barNoSet, out int noBar, out _);
                if (barNoSet + noBar > 0)
                    throw new BuildFailedException(
                        $"[VesselAbilityIcons] {barNoSet + noBar} vessel/HUD prefab(s) do not present " +
                        "four ability icons. Run Tools > Cosmic Shore > Validate Vessel Ability Icons.");
            }
        }
    }
}
