using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Asset gate for the VESSEL VISION BAND platform law (Docs/VESSEL_VISION.md): every vessel
    /// must be markable at range, from ONE shader splice and ONE stamp site, with NO per-vessel
    /// authoring.
    ///
    /// The law's failure mode is silence — an unmarked ship just looks like a ship — so this
    /// checks the five ways it can be broken without anything erroring:
    ///   1. the splice is gone from VesselGraph, or points at the wrong HLSL;
    ///   2. the tint property has stopped being EXPOSED, which silently severs the per-vessel
    ///      channel while leaving the graph looking wired;
    ///   3. the shipped HLSL has lost one of the two cutoffs or the quantizer's guard;
    ///   4. the single stamp in <c>VesselHelper.SetShipProperties</c> is removed, moved, or
    ///      joined by a second owner;
    ///   5. a vessel prefab carries no hull material on the wired shader, so that ONE ship is
    ///      unmarkable while every other ship in the fleet is fine — the hardest failure to
    ///      notice and the one this exists for.
    ///
    /// Sanity of the config is asked of <see cref="VesselVisionShadingConfigSO.IsSane"/> and the
    /// source predicates of <see cref="VesselVisionLawSource"/> — the SAME methods the runtime and
    /// the edit-mode test use, so the gates cannot drift apart.
    ///
    /// READER TOOL: it only reports. It writes no assets, records nothing to the change ledger,
    /// and draws no ship panel (Docs/TOOLING.md § "Tool output is a deliverable" — readers are
    /// exempt). Nothing here needs pushing.
    /// </summary>
    public static class VesselVisionLawValidator
    {
        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string GraphPath = "Assets/_Graphics/Materials/Graphs/VesselGraph.shadergraph";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/VesselVisionShading.hlsl";
        const string HelperPath = "Assets/_Scripts/Controller/Vessel/VesselHelper.cs";
        const string ConfigAssetPath = "Assets/Resources/VesselVisionShadingConfig.asset";
        const string ScriptRoot = "Assets/_Scripts";

        [MenuItem("FrogletTools/Vessels/Validate Vessel Vision Band")]
        [FrogletTool(FrogletToolCategory.Vessels, Importance = 5,
            Description = "Vessel vision band platform law - catches a vessel that silently cannot " +
                          "be picked out at range, and a severed per-vessel colour channel.")]
        public static void Validate()
        {
            var report = new StringBuilder();
            report.AppendLine("=== Vessel vision band platform law ===");
            report.AppendLine("contract: ONE shader splice (VesselGraph), ONE stamp site " +
                              "(VesselHelper.SetShipProperties), ONE Resources asset, an absolute " +
                              "distance band shared by the whole fleet");
            report.AppendLine();

            bool ok = true;
            ok &= CheckGraph(report);
            ok &= CheckHlsl(report);
            ok &= CheckStampSite(report);
            ok &= CheckConfig(report);
            ok &= CheckVesselMaterials(report);

            report.AppendLine();
            report.AppendLine(ok
                ? "RESULT: the law holds."
                : "RESULT: the law is BROKEN — see the failures above.");

            if (ok) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }

        static bool CheckGraph(StringBuilder report)
        {
            report.AppendLine("[1] VesselGraph splice");
            string text = ReadAsset(GraphPath);
            if (!VesselVisionLawSource.GraphIsWired(text, out string reason))
            {
                report.AppendLine($"    FAIL  {reason}");
                return false;
            }
            report.AppendLine("    ok    VesselVisionShade node present, sourced from " +
                              "VesselVisionShading.hlsl, _VesselVisionTint exposed.");
            return true;
        }

        static bool CheckHlsl(StringBuilder report)
        {
            report.AppendLine("[2] VesselVisionShading.hlsl");
            string text = ReadAsset(HlslPath);
            if (!VesselVisionLawSource.HlslDeclaresLaw(text, out string reason))
            {
                report.AppendLine($"    FAIL  {reason}");
                return false;
            }
            report.AppendLine("    ok    both cutoffs and the cel quantizer guard are intact.");
            report.AppendLine("    note  behaviour is proven separately by " +
                              "'python3 Tools/Shaders/verify_vessel_vision_band.py', which compiles " +
                              "and RUNS the shipped file. Run it after any edit to the HLSL.");
            return true;
        }

        static bool CheckStampSite(StringBuilder report)
        {
            report.AppendLine("[3] the single stamp site");

            // Counted across the whole script tree rather than in one file: a second owner of the
            // per-vessel channel is the failure, and it would by definition live somewhere else.
            int sites = 0;
            var offenders = new List<string>();
            foreach (var file in Directory.EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories))
            {
                string body = File.ReadAllText(file);
                int at = body.IndexOf(VesselVisionLawSource.StampInvocation, System.StringComparison.Ordinal);
                while (at >= 0)
                {
                    sites++;
                    offenders.Add(file.Replace('\\', '/'));
                    at = body.IndexOf(VesselVisionLawSource.StampInvocation, at + 1,
                                      System.StringComparison.Ordinal);
                }
            }

            if (!VesselVisionLawSource.StampHasExactlyOneCallSite(ReadAsset(HelperPath), sites,
                                                                 out string reason))
            {
                report.AppendLine($"    FAIL  {reason}");
                foreach (var o in offenders.Distinct()) report.AppendLine($"          {o}");
                return false;
            }
            report.AppendLine("    ok    exactly one, inside VesselHelper.SetShipProperties.");
            return true;
        }

        static bool CheckConfig(StringBuilder report)
        {
            report.AppendLine("[4] Resources/VesselVisionShadingConfig");
            var config = AssetDatabase.LoadAssetAtPath<VesselVisionShadingConfigSO>(ConfigAssetPath);
            if (config == null)
            {
                // Not fatal by design — the law falls back to the SO's own defaults — but it means
                // nobody can tune it, which is worth saying out loud.
                report.AppendLine("    warn  no asset at " + ConfigAssetPath + "; the law runs on the " +
                                  "SO's built-in defaults and has no tuning surface.");
                return true;
            }
            if (!config.IsSane(out string reason))
            {
                report.AppendLine($"    FAIL  {reason}");
                return false;
            }
            report.AppendLine($"    ok    band {config.NearFadeStart:0}→{config.NearFullStart:0} .. " +
                              $"{config.FarFullEnd:0}→{config.FarFadeEnd:0} u, strength " +
                              $"{config.Strength:0.00}, {config.CelSteps} cel tones.");
            return true;
        }

        static bool CheckVesselMaterials(StringBuilder report)
        {
            report.AppendLine("[5] every vessel can wear the mark");

            var wiredShader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (wiredShader == null)
            {
                report.AppendLine("    FAIL  VesselGraph did not load as a Shader — the graph is " +
                                  "missing or failed to compile.");
                return false;
            }

            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder });
            if (guids.Length == 0)
            {
                report.AppendLine($"    warn  no prefabs under {VesselFolder}.");
                return true;
            }

            bool ok = true;
            int marked = 0;
            foreach (var guid in guids.OrderBy(g => g))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // Only vessels: the folder also holds components, jets and skimmer parts, and a
                // sub-assembly having no hull material is not a fault.
                if (prefab.GetComponent<CosmicShore.Gameplay.VesselCustomization>() == null) continue;

                int wired = prefab.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r != null)
                    .SelectMany(r => r.sharedMaterials)
                    .Count(m => m != null && m.shader == wiredShader);

                if (wired == 0)
                {
                    report.AppendLine($"    FAIL  {Path.GetFileNameWithoutExtension(path)} has no " +
                                      "renderer material on VesselGraph, so it can never wear the " +
                                      "mark and will be the one vessel other pilots cannot pick out " +
                                      "at range.");
                    ok = false;
                }
                else
                {
                    marked++;
                }
            }

            if (ok) report.AppendLine($"    ok    {marked} vessel prefab(s), all markable.");
            return ok;
        }

        static string ReadAsset(string path)
        {
            string full = Path.Combine(Directory.GetCurrentDirectory(), path);
            return File.Exists(full) ? File.ReadAllText(full) : null;
        }
    }
}
