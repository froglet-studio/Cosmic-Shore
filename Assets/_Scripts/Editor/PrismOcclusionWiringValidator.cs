using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Asset-side gate for the camera↔vessel prism occlusion corridor
    /// (Docs/PRISM_ANIMATION.md §4.7 — a PLATFORM LAW: it must not be possible to author a
    /// vessel or a minigame in which the corridor is off).
    ///
    /// The corridor has no per-scene wiring to eyeball, so every way it can be broken is an
    /// asset fact, and all of them are checked here:
    ///
    ///   1. Each wired prism graph declares the two UNEXPOSED globals + the Custom Function.
    ///      (Checked against the graph TEXT — an unexposed ShaderGraph property is declared
    ///      outside UnityPerMaterial, so Material.HasProperty can never see it. Same trap
    ///      the clock validator documents for _PrismClock.)
    ///   2. Every material on those graphs is corridor-capable — OPAQUE + alpha-tested, no
    ///      transparent prism materials at all (the screen-door dither is THE prism
    ///      transparency mechanism; authored sub-1 alpha is its coverage). A material that
    ///      misses this is an INVISIBLE HOLE: that prism silently stays solid in front of
    ///      the ship — or renders a second, inconsistent kind of transparency beside the
    ///      dither.
    ///   3. Every PREFAB carrying a Prism renders with a wired prism shader. This is the
    ///      check that makes the law enforceable at authoring time: a new prism prefab on a
    ///      new or legacy shader is caught here, not by someone noticing their ship is hidden.
    ///
    /// FrogletTools > Ecology > Prism Animation > Validate Occlusion Corridor.
    /// </summary>
    public static class PrismOcclusionWiringValidator
    {
        public static readonly string[] GraphPaths =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        public const string CorridorHlslPath = "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl";
        public const string CorridorHlslGuid = "bf8e2c1fa76142c89ba03b2e1ae46201";
        public const string CorridorFunctionName = "PrismOcclusionFade";
        const string AlphaTestKeyword = "_ALPHATEST_ON";

        public static readonly string[] CorridorGlobalProps = { "_PrismOcclusionTarget", "_PrismOcclusionParams" };

        // Known, deliberate exclusions from the prism-prefab census (Docs/PRISM_ANIMATION.md
        // §4.7) — legacy prism prefabs on pre-corridor shaders, every one of them DEAD.
        // GreenDartBlock/TriangleBlock are the SpreadFresnel/TriangleFresnel family §3.7 I
        // says not to extend (referenced only by the Recording Studio scenes); TrailRing and
        // TrailPentagon are referenced by nothing at all. Listed rather than silently skipped
        // so the exclusion stays visible: if one is ever revived as live gameplay mass it
        // must be rebased, not added to this list.
        public static readonly string[] KnownLegacyPrismPrefabs =
        {
            "Assets/_Prefabs/Trails/GreenDartBlock.prefab",
            "Assets/_Prefabs/Trails/TriangleBlock.prefab",
            "Assets/_Prefabs/Trails/TrailRing.prefab",
            "Assets/_Prefabs/Trails/TrailPentagon.prefab",
        };

        /// <summary>
        /// Live graphs that deliberately do NOT carry the corridor. Unlike
        /// <see cref="KnownLegacyPrismPrefabs"/> (DEAD prefabs on pre-corridor
        /// shaders), these are LIVE: batched implosion debris draws
        /// ImplodingPrismMaterial on SuctionGraph (<c>PrismDebris.ConfigureImplosion</c>
        /// reads <c>sharedMaterial</c> off PrismImplosion.prefab). Consumption VFX is a
        /// sub-second implode of mass being removed — never standing mass that can
        /// occlude. Named so the exclusion cannot look like an omission. Do not add
        /// SuctionGraph to <c>PrismOcclusionDiagnostics.WiredPrismShaderNames</c>
        /// without wiring PrismOcclusionFade into it — <c>IsCorridorCapable</c> would
        /// then fail every suction material at runtime.
        /// </summary>
        public static readonly string[] KnownCorridorExcludedGraphs = { "SuctionGraph" };

        [MenuItem("FrogletTools/Ecology/Prism Animation/Validate Occlusion Corridor")]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 5,
            Description = "Occlusion-corridor platform law - catches silent holes (a prism that can never fade hides the ship).")]
        public static void Validate()
        {
            var report = new StringBuilder();
            report.AppendLine("[PrismOcclusion] CORRIDOR WIRING — Docs/PRISM_ANIMATION.md §4.7 (PLATFORM LAW: no vessel and no minigame may opt out)");
            bool pass = true;

            pass &= CheckGraphWiring(report);

            // ---- 3. materials on those graphs: the silent-hole check ----
            report.AppendLine("— Materials on the wired prism graphs:");
            int opaque = 0, transparent = 0, matFailed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null) continue;
                if (!PrismOcclusionDiagnostics.IsWiredPrismShader(mat.shader.name)) continue;

                bool isTransparent = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f;
                if (isTransparent) transparent++; else opaque++;

                // ONE rule, shared with the runtime scream (PrismOcclusionDiagnostics) and the
                // edit-mode coverage test, so the gates can never drift apart.
                if (!PrismOcclusionDiagnostics.IsCorridorCapable(mat, out string fault))
                {
                    report.AppendLine($"   ❌ {mat.name}: {fault}");
                    matFailed++;
                }
            }
            report.AppendLine(matFailed == 0
                ? $"   ✅ {opaque} material(s) opaque + alpha-test enabled (transparent prism materials: {transparent}, must be 0)"
                : $"   ❌ {matFailed} material(s) misconfigured — run `python3 Tools/Shaders/enable_prism_alpha_clip.py`");
            pass &= matFailed == 0;

            // ---- 4. prism PREFAB census — the authoring-time gate ----
            report.AppendLine("— Prefabs carrying a Prism (every one must render on a wired prism graph):");
            int prismPrefabs = 0, prefabFailed = 0, legacyExcluded = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null || go.GetComponentInChildren<Prism>(true) == null) continue;

                prismPrefabs++;
                if (KnownLegacyPrismPrefabs.Contains(path))
                {
                    legacyExcluded++;
                    continue;
                }

                // Only the renderer ON the Prism GameObject is prism mass — a prefab that
                // merely CONTAINS a prism (TermiteDrone's drone body) renders other things
                // with other shaders, quite legitimately.
                var offenders = new List<string>();
                foreach (var prism in go.GetComponentsInChildren<Prism>(true))
                {
                    var renderer = prism.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null || mat.shader == null) continue;
                        if (!PrismOcclusionDiagnostics.IsWiredPrismShader(mat.shader.name))
                            offenders.Add($"{mat.name} ({mat.shader.name})");
                    }
                }
                if (offenders.Count > 0)
                {
                    report.AppendLine($"   ❌ {path}: renders with non-corridor shader(s) {string.Join(", ", offenders.Distinct())} — "
                                      + "live prism mass on an unwired shader can never fade and WILL hide the ship. "
                                      + "Rebase onto a wired prism graph, or add its graph to the census in "
                                      + "Tools/Shaders/wire_prism_occlusion_corridor.py.");
                    prefabFailed++;
                }
            }
            report.AppendLine(prefabFailed == 0
                ? $"   ✅ {prismPrefabs - legacyExcluded} prism prefab(s) on wired graphs ({legacyExcluded} known legacy decor prefab(s) excluded by name)"
                : $"   ❌ {prefabFailed}/{prismPrefabs} prism prefab(s) render outside the corridor");
            pass &= prefabFailed == 0;

            // ---- 5. config ----
            var config = Resources.Load<PrismOcclusionConfigSO>("PrismOcclusionConfig");
            if (config == null)
                report.AppendLine("   ⚠ No Resources/PrismOcclusionConfig asset — the SO's own defaults apply (corridor on, radius 18).");
            else if (!config.Enabled)
                report.AppendLine("   ⚠ PrismOcclusionConfig: DISABLED — the publisher writes a zero radius and the shader early-outs.");
            else if (config.OuterRadiusScale <= 0f)
            {
                report.AppendLine("   ❌ PrismOcclusionConfig: outerRadiusScale <= 0 reads as 'off' — set a positive scale or clear 'enabled'.");
                pass = false;
            }
            else
                report.AppendLine($"   ✅ PrismOcclusionConfig: outer edge at {config.OuterRadiusScale}× the vessel's circumscribing radius, "
                                  + $"clear core at {config.InnerRadiusScale}×, core alpha {config.CoreAlpha}");

            if (Application.isPlaying)
            {
                float hull = PrismOcclusionCorridor.TargetRadius;
                float outerScale = config != null ? config.OuterRadiusScale : 1f;
                float innerScale = config != null ? config.InnerRadiusScale : 0.5f;
                report.AppendLine(PrismOcclusionCorridor.IsActive
                    ? $"   ▶ live: corridor open onto '{PrismOcclusionCorridor.Target?.name}' — measured hull radius "
                      + $"{hull:F2}, so the gradient runs {hull * innerScale:F2} (fully clear) → {hull * outerScale:F2} (fully opaque)"
                    : PrismOcclusionCorridor.IsSuppressed
                        ? "   ▶ live: corridor SUPPRESSED (manual replay camera — expected)"
                        : "   ▶ live: corridor OFF (no local pilot vessel yet)");
            }

            report.AppendLine(pass
                ? "RESULT: ✅ OCCLUSION CORRIDOR WIRED EVERYWHERE — fly so a prism wall sits between the camera and the ship; the corridor should dissolve."
                : "RESULT: ❌ CORRIDOR INCOMPLETE — every ❌ above is prism mass that will hide the player's ship.");

            if (pass) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        /// <summary>
        /// Graph-text census: HLSL GUID + per-graph unexposed corridor globals +
        /// PrismOcclusionFade CF + compile. Shared by Validate Occlusion Corridor
        /// (this menu) and Validate Clock Wiring so the clock menu cannot print
        /// ALL PRESENT while the corridor graphs are unwired. Prefab census and
        /// material opaque+clip stay on this menu only.
        /// </summary>
        public static bool CheckGraphWiring(StringBuilder report)
        {
            bool pass = true;

            // ---- 1. the HLSL asset, at the GUID the graphs pin ----
            string hlslGuidOnDisk = AssetDatabase.AssetPathToGUID(CorridorHlslPath);
            if (string.IsNullOrEmpty(hlslGuidOnDisk))
            {
                report.AppendLine($"❌ {CorridorHlslPath} NOT FOUND");
                pass = false;
            }
            else if (hlslGuidOnDisk != CorridorHlslGuid)
            {
                report.AppendLine($"❌ {CorridorHlslPath} GUID drifted ({hlslGuidOnDisk} != {CorridorHlslGuid}) — the graphs' Custom Functions point at the old one");
                pass = false;
            }
            else
                report.AppendLine($"✅ {CorridorHlslPath} (GUID pinned)");

            // ---- 2. each graph: two UNEXPOSED globals + the Custom Function node ----
            foreach (var graphPath in GraphPaths)
            {
                report.AppendLine($"— {Path.GetFileNameWithoutExtension(graphPath)}");
                if (!File.Exists(graphPath))
                {
                    report.AppendLine($"   ❌ {graphPath} NOT FOUND");
                    pass = false;
                    continue;
                }

                // Normalize CRLF before splitting — a Windows checkout otherwise collapses
                // the whole file into one "block" (the same trap the clock validator hit).
                string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

                foreach (var prop in CorridorGlobalProps)
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

                if (text.Contains($"\"m_FunctionName\": \"{CorridorFunctionName}\""))
                {
                    report.AppendLine($"   ✅ Custom Function node '{CorridorFunctionName}' present");
                    if (!text.Contains($"\"m_FunctionSource\": \"{CorridorHlslGuid}\""))
                    {
                        report.AppendLine($"   ❌ '{CorridorFunctionName}' does not source {CorridorHlslPath}");
                        pass = false;
                    }
                }
                else
                {
                    report.AppendLine($"   ❌ Custom Function node '{CorridorFunctionName}' NOT found — re-run Tools/Shaders/wire_prism_occlusion_corridor.py");
                    pass = false;
                }

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
                if (shader != null && ShaderUtil.ShaderHasError(shader))
                {
                    report.AppendLine("   ❌ HAS COMPILE ERRORS — check the shader inspector (git checkout the .shadergraph and re-run the wirer)");
                    pass = false;
                }
                else if (shader != null)
                    report.AppendLine("   ✅ compiles clean");
            }

            report.AppendLine("— Deliberate graph exclusion:");
            report.AppendLine("   ✅ SuctionGraph is a named corridor exclusion (consumption VFX / ImplodingPrismMaterial on PrismImplosion.prefab — Docs/PRISM_ANIMATION.md §4.7). It is live, unlike KnownLegacyPrismPrefabs. Do not add it to WiredPrismShaderNames without wiring PrismOcclusionFade.");

            return pass;
        }

    }
}
