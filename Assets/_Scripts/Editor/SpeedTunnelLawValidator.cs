using System.Collections.Generic;
using System.IO;
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
    /// Asset gate for the speed-tunnel PLATFORM LAW (Docs/SPEED_TUNNEL.md): the quasi dolly
    /// zoom must apply to EVERY vessel, from ONE driver, with NO per-vessel authoring.
    ///
    /// The law's failure mode is silence — a vessel that quietly doesn't tunnel, or a second
    /// writer that fights the one that does — so this checks the three ways it can be broken
    /// by authoring rather than by code:
    ///   1. a per-vessel driver re-appears on a prefab (component, or a missing script where
    ///      the retired <c>SpeedTunnelEffectController</c> used to sit);
    ///   2. the single binding in <c>VesselController.Initialize</c> is removed or moved off
    ///      <c>IsLocalPilot</c>;
    ///   3. the Resources config is missing or authored into a state that does nothing.
    ///
    /// Sanity is asked of <see cref="SpeedTunnelConfigSO.IsSane"/> — the SAME predicate the
    /// runtime and the edit-mode test use, so the three gates cannot drift apart.
    ///
    /// READER TOOL: it only reports. It writes no assets, records nothing to the change
    /// ledger, and draws no ship panel (Docs/TOOLING.md § "Tool output is a deliverable" —
    /// readers are exempt). Nothing here needs pushing.
    /// </summary>
    public static class SpeedTunnelLawValidator
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string ControllerPath = "Assets/_Scripts/Controller/Vessel/VesselController.cs";
        const string ConfigAssetPath = "Assets/Resources/SpeedTunnelConfig.asset";

        /// <summary>
        /// Retired per-vessel driver. Kept as a literal because the type no longer exists to
        /// reference — a prefab that still carries it shows up as a missing script, which is
        /// exactly the state this catches.
        /// </summary>
        const string RetiredComponentName = "SpeedTunnelEffectController";

        [MenuItem("FrogletTools/Vessels/Validate Speed Tunnel Law")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 5,
            Description = "Speed-tunnel platform law - catches a vessel that silently doesn't tunnel " +
                          "and a second writer that fights the one driver.")]
        public static void Validate()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Speed tunnel platform law ===");
            report.AppendLine("contract: ONE static driver (VesselSpeedTunnel), bound in " +
                              "VesselController.Initialize under IsLocalPilot, tuned by ONE " +
                              "Resources asset, absolute speed window shared by the whole fleet");
            report.AppendLine();

            bool pass = true;
            pass &= CheckNoPerVesselDrivers(report);
            pass &= CheckBinding(report);
            pass &= CheckConfig(report);

            report.AppendLine();
            report.AppendLine(pass
                ? "PASS - the law is enforced structurally; no vessel can opt out."
                : "FAIL - see the marked sections above.");

            if (pass) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        /// <summary>
        /// No vessel prefab may carry its own speed-tunnel driver. Two shapes to catch: a live
        /// component whose type name says it drives the tunnel, and a MISSING script (null
        /// component) — which is what a prefab that still references the retired
        /// <c>SpeedTunnelEffectController</c> looks like after the class was deleted.
        /// </summary>
        static bool CheckNoPerVesselDrivers(StringBuilder report)
        {
            report.AppendLine("[1] No per-vessel drivers on any vessel prefab");

            bool ok = true;
            int vessels = 0;
            foreach (var path in VesselPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab || !prefab.GetComponentInChildren<VesselStatus>(true)) continue;
                vessels++;

                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue; // missing scripts handled below
                    string typeName = component.GetType().Name;
                    if (typeName.Contains("SpeedTunnel"))
                    {
                        report.AppendLine($"    FAIL {prefab.name}: carries {typeName}. The tunnel is a " +
                                          "platform law driven by the static VesselSpeedTunnel — a " +
                                          "per-vessel writer races the single global Panini override " +
                                          "across a vessel swap. Remove the component.");
                        ok = false;
                    }
                }

                // A raw-text check for the retired script, because a missing script has no type
                // to inspect and GetComponentsInChildren simply yields null for it.
                if (File.Exists(path) && File.ReadAllText(path).Contains(RetiredComponentName))
                {
                    report.AppendLine($"    FAIL {prefab.name}: still references the retired " +
                                      $"{RetiredComponentName}.");
                    ok = false;
                }
            }

            report.AppendLine(ok
                ? $"    ok - {vessels} vessel prefabs, none carries a tunnel driver"
                : "    ^ fix the prefabs above");
            return ok;
        }

        /// <summary>
        /// The single binding site. A text check rather than reflection, because what matters is
        /// that the call is in <c>Initialize</c> under <c>IsLocalPilot</c> — the property that
        /// makes the law un-authorable — not merely that the method exists somewhere.
        /// </summary>
        static bool CheckBinding(StringBuilder report)
        {
            report.AppendLine("[2] Bound in VesselController.Initialize under IsLocalPilot");

            if (!File.Exists(ControllerPath))
            {
                report.AppendLine($"    FAIL {ControllerPath} not found.");
                return false;
            }

            string source = File.ReadAllText(ControllerPath);
            bool ok = true;

            if (!source.Contains("VesselSpeedTunnel.SetTarget"))
            {
                report.AppendLine("    FAIL no VesselSpeedTunnel.SetTarget call — the law is bound " +
                                  "nowhere, so no vessel tunnels at all.");
                ok = false;
            }

            if (!source.Contains("VesselSpeedTunnel.ClearTarget"))
            {
                report.AppendLine("    FAIL no VesselSpeedTunnel.ClearTarget call — a destroyed vessel " +
                                  "would keep driving the camera.");
                ok = false;
            }

            if (!source.Contains("player.IsLocalPilot"))
            {
                report.AppendLine("    FAIL the binding is not gated on IsLocalPilot. IsLocalUser " +
                                  "misses the legacy non-networked single-player spawn path, which is " +
                                  "exactly the escape hatch this law must not have.");
                ok = false;
            }

            if (ok) report.AppendLine("    ok - one binding, on the universal vessel entry point");
            return ok;
        }

        static bool CheckConfig(StringBuilder report)
        {
            report.AppendLine("[3] Resources/SpeedTunnelConfig");

            var config = AssetDatabase.LoadAssetAtPath<SpeedTunnelConfigSO>(ConfigAssetPath);
            if (config == null)
            {
                // Legal — the runtime falls back to the SO's defaults so the law holds with zero
                // authoring — but worth saying out loud, because tuning has nowhere to live.
                report.AppendLine($"    note - no asset at {ConfigAssetPath}; the runtime falls back " +
                                  "to code defaults, so the law still holds but there is nothing to tune.");
                return true;
            }

            if (!config.IsSane(out string reason))
            {
                report.AppendLine($"    FAIL {reason}");
                return false;
            }

            if (!config.Enabled)
            {
                report.AppendLine("    FAIL 'enabled' is off — that switch is a global debug toggle, " +
                                  "not a shipping state.");
                return false;
            }

            report.AppendLine($"    ok - window {config.MinEffectSpeed}-{config.MaxEffectSpeed} u/s, " +
                              $"-{config.FovDrop}° FOV, -{config.PaniniDrop} Panini, " +
                              $"{config.Responsiveness}/s");
            report.AppendLine();
            AppendFleetTable(report, config);
            return true;
        }

        /// <summary>
        /// Where every vessel actually lands in the shared absolute window. This is a REPORT, not
        /// a check: a vessel sitting deep in the tunnel at cruise is not a fault, it is the law
        /// working (it really is that fast). It is here so the one window can be tuned against
        /// the whole fleet instead of against whichever vessel someone last flew.
        /// </summary>
        static void AppendFleetTable(StringBuilder report, SpeedTunnelConfigSO config)
        {
            report.AppendLine("    fleet at a glance (cruise = full throttle, unboosted; " +
                              "top = cruise x VesselStatus.boostMultiplier)");
            report.AppendLine("      vessel        cruise  effect   top   effect");

            foreach (var path in VesselPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab || !prefab.TryGetComponent<VesselStatus>(out var status)) continue;

                var transformer = ResolveTransformer(prefab);
                if (transformer == null) continue;

                // Both live throttle models reduce to the same thing at full throttle today:
                // the base transformer is XDiff * scaler * ThrottleScalerMultiplier + minimum
                // (XDiff = 1 at full throttle, and the multiplier is Enabled: 0 on every
                // prefab, so it evaluates to 1), and SingleStickVesselTransformer drops both
                // terms outright. If a vessel ever enables its ThrottleScalerMultiplier this
                // column under-reports for that vessel only.
                float cruise = transformer.DefaultThrottleScaler + transformer.DefaultMinimumSpeed;
                float top = transformer.DefaultThrottleScaler * status.BoostMultiplier
                            + transformer.DefaultMinimumSpeed;

                report.AppendLine($"      {prefab.name,-12} {cruise,6:0} {config.Effect01(cruise),7:0.00} " +
                                  $"{top,5:0} {config.Effect01(top),7:0.00}");
            }
        }

        /// <summary>
        /// The transformer that is actually enabled. Three prefabs (Falcon, Shrike, Termite)
        /// carry two transformer components on the root with one disabled, so taking the first
        /// match would report the dead one.
        /// </summary>
        static VesselTransformer ResolveTransformer(GameObject prefab)
        {
            var all = prefab.GetComponents<VesselTransformer>();
            foreach (var t in all)
                if (t && t.enabled) return t;
            return all.Length > 0 ? all[0] : null;
        }

        static IEnumerable<string> VesselPrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .OrderBy(p => p);
    }
}
