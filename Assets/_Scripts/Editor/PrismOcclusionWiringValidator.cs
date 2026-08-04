using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Editor
{
    /// <summary>
    /// One-click audit of the camera↔vessel prism occlusion corridor
    /// (Docs/PRISM_ANIMATION.md §5 C1). The corridor is entirely shader-side, driven by
    /// two global uniforms, so there is nothing in a scene to eyeball — every failure
    /// mode is a missing property, a missing Custom Function node, or a material that
    /// did not opt into alpha test. All three are checkable from assets alone.
    ///
    /// The nastiest failure is silent: a prism material without <c>_ALPHATEST_ON</c>
    /// compiles the alpha output away entirely on an Opaque surface, so that prism
    /// simply never fades and the corridor has an invisible hole in it. This reports it.
    ///
    /// FrogletTools > Ecology > Prism Animation > Validate Occlusion Corridor.
    /// </summary>
    public static class PrismOcclusionWiringValidator
    {
        const string GraphPath = "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl";
        const string HlslGuid = "bf8e2c1fa76142c89ba03b2e1ae46201";
        const string FunctionName = "PrismOcclusionFade";
        const string AlphaTestKeyword = "_ALPHATEST_ON";

        static readonly string[] GlobalProps = { "_PrismOcclusionTarget", "_PrismOcclusionParams" };

        [MenuItem("FrogletTools/Ecology/Prism Animation/Validate Occlusion Corridor")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 4,
            Description = "Camera-to-vessel see-through corridor - catches silent holes (a prism material without alpha test never fades).")]
        public static void Validate()
        {
            var report = new StringBuilder();
            report.AppendLine("[PrismOcclusion] CORRIDOR WIRING — Docs/PRISM_ANIMATION.md §5 C1");
            bool pass = true;

            // ---- 1. the HLSL asset, at the GUID the graph pins ----
            string hlslGuidOnDisk = AssetDatabase.AssetPathToGUID(HlslPath);
            if (string.IsNullOrEmpty(hlslGuidOnDisk))
            {
                report.AppendLine($"❌ {HlslPath} NOT FOUND");
                pass = false;
            }
            else if (hlslGuidOnDisk != HlslGuid)
            {
                report.AppendLine($"❌ {HlslPath} GUID drifted ({hlslGuidOnDisk} != {HlslGuid}) — the graph's Custom Function points at the old one");
                pass = false;
            }
            else
                report.AppendLine($"✅ {HlslPath} (GUID pinned)");

            // ---- 2. the graph: two UNEXPOSED globals + the Custom Function node ----
            if (!File.Exists(GraphPath))
            {
                report.AppendLine($"❌ {GraphPath} NOT FOUND");
                pass = false;
            }
            else
            {
                // Normalize CRLF before splitting — a Windows checkout otherwise
                // collapses the whole file into one "block" (same trap the clock
                // validator hit).
                string text = File.ReadAllText(GraphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

                foreach (var prop in GlobalProps)
                {
                    var block = blocks.FirstOrDefault(b =>
                        (b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") ||
                         b.Contains($"\"m_OverrideReferenceName\": \"{prop}\"")) &&
                        b.Contains("ShaderProperty"));
                    if (block == null)
                    {
                        report.AppendLine($"   ❌ property {prop} MISSING — re-run Tools/Shaders/wire_prism_occlusion_corridor.py");
                        pass = false;
                    }
                    else if (block.Contains("\"m_GeneratePropertyBlock\": true"))
                    {
                        report.AppendLine($"   ❌ {prop} is EXPOSED — it must be unexposed (a global) or Shader.SetGlobalVector cannot drive it");
                        pass = false;
                    }
                    else if (block.Contains("\"hlslDeclarationOverride\": 3"))
                    {
                        report.AppendLine($"   ❌ {prop} is Hybrid Per Instance — it is ONE value for the whole frame, not per prism");
                        pass = false;
                    }
                    else
                        report.AppendLine($"   ✅ property {prop} (global, unexposed)");
                }

                if (text.Contains($"\"m_FunctionName\": \"{FunctionName}\""))
                {
                    report.AppendLine($"   ✅ Custom Function node '{FunctionName}' present");
                    if (!text.Contains($"\"m_FunctionSource\": \"{HlslGuid}\""))
                    {
                        report.AppendLine($"   ❌ '{FunctionName}' does not source {HlslPath}");
                        pass = false;
                    }
                }
                else
                {
                    report.AppendLine($"   ❌ Custom Function node '{FunctionName}' NOT found — re-run Tools/Shaders/wire_prism_occlusion_corridor.py");
                    pass = false;
                }
            }

            // ---- 3. materials: the silent-hole check ----
            report.AppendLine("— Materials compiled against BlockGraph:");
            int opaque = 0, transparent = 0, failed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null) continue;
                // Exact suffix match: EndsWith("BlockGraph") would also swallow
                // "ExplodingBlockGraph".
                if (mat.shader.name != "Shader Graphs/BlockGraph" && !mat.shader.name.EndsWith("/BlockGraph"))
                    continue;

                var missing = GlobalProps.Where(p => !mat.HasProperty(p)).ToList();
                if (missing.Count > 0)
                {
                    report.AppendLine($"   ❌ {mat.name}: shader does not declare {string.Join(", ", missing)} (reimport the graph)");
                    failed++;
                    continue;
                }

                bool isTransparent = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f;
                if (isTransparent)
                {
                    // Blending materials need no clip — the corridor's alpha multiply is enough.
                    transparent++;
                    continue;
                }

                opaque++;
                bool clipOn = mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") > 0.5f;
                bool keywordOn = mat.IsKeywordEnabled(AlphaTestKeyword);
                float alpha = mat.HasProperty("_Alpha") ? mat.GetFloat("_Alpha") : 1f;

                if (!clipOn || !keywordOn)
                {
                    report.AppendLine($"   ❌ {mat.name}: OPAQUE without alpha test "
                                      + $"(_AlphaClip={(clipOn ? 1 : 0)}, {AlphaTestKeyword}={(keywordOn ? "on" : "OFF")}) "
                                      + "— this prism will NEVER fade (a silent hole in the corridor). "
                                      + "Fix: python3 Tools/Shaders/enable_prism_alpha_clip.py");
                    failed++;
                }
                else if (!Mathf.Approximately(alpha, 1f))
                {
                    report.AppendLine($"   ❌ {mat.name}: _Alpha is {alpha}, not 1 — with alpha test on, this prism is clipped away ENTIRELY");
                    failed++;
                }
            }
            report.AppendLine(failed == 0
                ? $"   ✅ {opaque} opaque material(s) alpha-test enabled, {transparent} transparent material(s) blend as-is"
                : $"   ❌ {failed} material(s) misconfigured (see above)");
            pass &= failed == 0;

            // ---- 4. shader compile state ----
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(GraphPath);
            if (shader != null && ShaderUtil.ShaderHasError(shader))
            {
                report.AppendLine("   ❌ BlockGraph HAS COMPILE ERRORS — check the shader inspector (git checkout the .shadergraph and re-run the wirer)");
                pass = false;
            }
            else if (shader != null)
                report.AppendLine("   ✅ BlockGraph compiles clean");

            // ---- 5. config ----
            var config = Resources.Load<PrismOcclusionConfigSO>("PrismOcclusionConfig");
            if (config == null)
                report.AppendLine("   ⚠ No Resources/PrismOcclusionConfig asset — the SO's own defaults apply (corridor on, radius 18).");
            else if (!config.Enabled)
                report.AppendLine("   ⚠ PrismOcclusionConfig: DISABLED — the publisher writes a zero radius and the shader early-outs.");
            else if (config.OuterRadius <= 0f)
            {
                report.AppendLine("   ❌ PrismOcclusionConfig: outerRadius <= 0 reads as 'off' — set a positive radius or clear 'enabled'.");
                pass = false;
            }
            else
                report.AppendLine($"   ✅ PrismOcclusionConfig: radius {config.OuterRadius} (feather from {config.InnerRadius}), core alpha {config.CoreAlpha}");

            if (Application.isPlaying)
            {
                report.AppendLine(PrismOcclusionCorridor.IsActive
                    ? $"   ▶ live: corridor open onto '{PrismOcclusionCorridor.Target?.name}'"
                    : "   ▶ live: corridor OFF (no camera follow target — expected in the menu camera / replay camera states)");
            }

            report.AppendLine(pass
                ? "RESULT: ✅ OCCLUSION CORRIDOR WIRED — fly so a prism wall sits between the camera and the ship; the corridor should dissolve."
                : "RESULT: ❌ CORRIDOR INCOMPLETE — every ❌ above is a prism that will not fade.");

            if (pass) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }
    }
}
