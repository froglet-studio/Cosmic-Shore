#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the camera↔vessel prism occlusion corridor as a PLATFORM LAW
    /// (Docs/PRISM_ANIMATION.md §4.7): it must not be possible to author a vessel, a prism,
    /// or a minigame in which the corridor is off.
    ///
    /// Why a test and not just the validator menu item: the corridor's failure mode is
    /// SILENT. A prism whose material cannot fade simply stays solid in front of the ship —
    /// no error, no visual tell in a screenshot, nothing to notice until someone complains
    /// that they can't see their ship. That is exactly how the previous per-vessel opt-in
    /// system stayed broken. These assertions run from assets alone (no play mode) and fail
    /// the moment new prism content lands outside the corridor.
    ///
    /// Every check shares its rule with the runtime scream
    /// (<see cref="PrismOcclusionDiagnostics.IsCorridorCapable"/>) so the gates cannot drift.
    /// </summary>
    public class PrismOcclusionCoverageTests
    {
        // Same lists the validator prints — duplicating them here is how a Specs
        // edit and a test list silently drift, which is the class of miss this
        // gate exists to close.
        static readonly string[] WiredGraphPaths = PrismOcclusionWiringValidator.GraphPaths;
        const string HlslPath = PrismOcclusionWiringValidator.CorridorHlslPath;
        const string FunctionName = PrismOcclusionWiringValidator.CorridorFunctionName;
        const string DebrisFunctionName = PrismOcclusionWiringValidator.DebrisCorridorFunctionName;
        static readonly string[] GlobalProps = PrismOcclusionWiringValidator.CorridorGlobalProps;
        static readonly HashSet<string> KnownLegacyPrismPrefabs =
            new HashSet<string>(PrismOcclusionWiringValidator.KnownLegacyPrismPrefabs);

        [Test]
        public void SuctionGraph_IsANamedCorridorExclusion_NotASilentOmission()
        {
            // Live consumption VFX (batched implosion debris draws ImplodingPrismMaterial
            // on SuctionGraph — PrismDebris.ConfigureImplosion reads sharedMaterial off
            // PrismImplosion.prefab). Unlike KnownLegacyPrismPrefabs (DEAD), this graph
            // is LIVE. Named so the exclusion cannot look like an omission. Do not add
            // it to WiredPrismShaderNames without wiring PrismOcclusionFade — that would
            // fail IsCorridorCapable on every suction material at runtime.
            Assert.IsFalse(PrismOcclusionDiagnostics.WiredPrismShaderNames.Contains("SuctionGraph"),
                "SuctionGraph is consumption VFX, not standing mass — wiring it into " +
                "WiredPrismShaderNames without wiring PrismOcclusionFade would fail " +
                "IsCorridorCapable on every suction material.");
            CollectionAssert.Contains(PrismOcclusionWiringValidator.KnownCorridorExcludedGraphs, "SuctionGraph");
            Assert.IsFalse(PrismOcclusionWiringValidator.GraphPaths.Any(p => p.Contains("SuctionGraph")),
                "GraphPaths must not include SuctionGraph — the corridor is not wired into it.");
        }

        [Test]
        public void CorridorHlsl_ExistsAndDeclaresTheFadeFunction()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing — the corridor has no GPU half.");
            string hlsl = File.ReadAllText(HlslPath);
            foreach (var fn in new[] { FunctionName, DebrisFunctionName })
                Assert.IsTrue(hlsl.Contains($"void {fn}_float("),
                    $"{HlslPath} does not declare {fn}_float — ShaderGraph appends the precision suffix, " +
                    "so the function name must match exactly or every prism graph fails to compile.");

            // Both entry points must be thin wrappers over ONE body: live mass and its
            // debris differ only in the nose clearance they pass, and the day that stops
            // being true the corridor's SHAPE can drift between them with nothing on
            // screen to say which half is wrong.
            Assert.IsTrue(hlsl.Contains("void PrismOcclusionFadeImpl("),
                $"{HlslPath} has no PrismOcclusionFadeImpl — the two entry points are no longer one body.");
            Assert.IsTrue(hlsl.Contains("PRISM_OCCLUSION_DEBRIS_NOSE_CLEARANCE"),
                $"{HlslPath} has no debris nose clearance — explosion debris is back on the live one, " +
                "which keeps a solid zone in front of the ship that debris has no collider to justify.");
        }

        [Test]
        public void EveryWiredGraph_DeclaresTheCorridorGlobalsAndTheFadeNode()
        {
            foreach (var graphPath in WiredGraphPaths)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                // Normalise CRLF first: a Windows checkout otherwise collapses the whole
                // file into one block and every block-scoped check reads the wrong property.
                string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

                foreach (var prop in GlobalProps)
                {
                    var block = blocks.FirstOrDefault(b =>
                        (b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") ||
                         b.Contains($"\"m_OverrideReferenceName\": \"{prop}\"")) &&
                        b.Contains("ShaderProperty"));
                    Assert.IsNotNull(block,
                        $"{graphPath} does not declare {prop} — run Tools/Shaders/wire_prism_occlusion_corridor.py.");
                    // Unexposed, or Shader.SetGlobalVector cannot drive it; and never Hybrid
                    // Per Instance, because it is ONE value for the whole frame.
                    Assert.IsFalse(block.Contains("\"m_GeneratePropertyBlock\": true"),
                        $"{graphPath}: {prop} is EXPOSED — it must be an unexposed global.");
                    Assert.IsFalse(block.Contains("\"hlslDeclarationOverride\": 3"),
                        $"{graphPath}: {prop} is Hybrid Per Instance — it is a frame global, not per-prism data.");
                }

                string expectedFunction = PrismOcclusionWiringValidator.CorridorFunctionFor(graphPath);
                Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{expectedFunction}\""),
                    $"{graphPath} has no {expectedFunction} Custom Function node — prisms on it can never fade.");
            }
        }

        [Test]
        public void ExplodingGraph_CarriesTheObjectAnchoredErosion()
        {
            // The exploding prism's fade is the object-anchored EROSION dither (chunks of
            // the prism body crumbling), spliced between the explosion clock and the
            // corridor node by Tools/Shaders/wire_prism_explosion_erosion.py. Without it a
            // graph revert silently returns the debris fade to the view-anchored corridor
            // kernel — the pattern crawls over tumbling debris again, with nothing else
            // failing. Full edge-level validation lives in the wirer (--check); this gate
            // catches the wholesale revert from assets alone.
            string graphPath = "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph";
            Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
            string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
            Assert.IsTrue(text.Contains("\"m_FunctionName\": \"PrismErosionFade\""),
                $"{graphPath} has no PrismErosionFade Custom Function node — the debris fade has " +
                "fallen back to the view-anchored corridor dither. " +
                "Fix: python3 Tools/Shaders/wire_prism_explosion_erosion.py");
        }

        [Test]
        public void Corridor_CapsWhatItsBaseMayTakeOfTheTunnel()
        {
            // The nose clearance and the axial grade that leads into it are both written
            // in HULL RADII and both come off the same end of a corridor whose length is
            // the CAMERA DISTANCE — which the fleet authors from 6.72 (Urchin) to 250
            // (Serpent). Uncapped, they took 1.75/rho of the tunnel, so the FULLY-CLEAR
            // corridor was empty at rho <= 1.75 and 0.384 of the length at the Squirrel's
            // rho: on a close-camera hull the corridor was nearly inert, and mass sat
            // solid in front of the ship while the same mass dissolved for a long-camera
            // one. The cap is what makes the clearance constant's own degenerate-case
            // note ("only lost inside one hull radius") true.
            //
            // Numeric proof, including that the cap is a bit-exact no-op for rho >= 3.5:
            // Tools/Shaders/verify_prism_corridor_base.py. This gate only catches the
            // wholesale revert, from assets alone.
            string hlsl = "Assets/_Graphics/Materials/Graphs/PrismOcclusionCorridor.hlsl";
            Assert.IsTrue(File.Exists(hlsl), $"{hlsl} is missing.");
            string text = File.ReadAllText(hlsl).Replace("\r\n", "\n");
            Assert.IsTrue(text.Contains("PRISM_OCCLUSION_MAX_BASE_SHARE"),
                $"{hlsl} no longer caps what the nose clearance plus its axial grade may " +
                "take of the corridor. A close-camera vessel then has little or no " +
                "see-through corridor at all and nothing else fails. " +
                "Verify with: python3 Tools/Shaders/verify_prism_corridor_base.py");
            Assert.IsTrue(text.Contains("float shrink = min(1.0, PRISM_OCCLUSION_MAX_BASE_SHARE"),
                $"{hlsl} declares PRISM_OCCLUSION_MAX_BASE_SHARE but no longer applies it — " +
                "an authored cap nothing reads is exactly the shape of a fix that was " +
                "reverted in the body and left in the header. " +
                "Verify with: python3 Tools/Shaders/verify_prism_corridor_base.py");
        }

        [Test]
        public void EveryMaterialOnAWiredGraph_CanBeDissolvedByTheCorridor()
        {
            var failures = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;
                if (!PrismOcclusionDiagnostics.IsWiredPrismShader(mat.shader.name)) continue;

                if (!PrismOcclusionDiagnostics.IsCorridorCapable(mat, out string fault))
                    failures.Add($"{path}: {fault}");
            }

            Assert.IsEmpty(failures,
                "Prism material(s) cannot be dissolved by the occlusion corridor — each is an invisible hole that " +
                "will hide the player's ship. Fix: python3 Tools/Shaders/enable_prism_alpha_clip.py\n" +
                string.Join("\n", failures));
        }

        [Test]
        public void EveryPrismPrefab_RendersOnACorridorWiredShader()
        {
            var failures = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (KnownLegacyPrismPrefabs.Contains(path)) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                // Scope matters: only the renderer ON the Prism GameObject is prism mass.
                // A prefab that merely CONTAINS a prism (TermiteDrone's drone body) renders
                // other things with other shaders quite legitimately, and sweeping the whole
                // hierarchy reports those as violations.
                foreach (var prism in go.GetComponentsInChildren<Prism>(true))
                {
                    var renderer = prism.GetComponent<Renderer>();
                    if (renderer == null) continue;
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat == null || mat.shader == null) continue;
                        if (!PrismOcclusionDiagnostics.IsWiredPrismShader(mat.shader.name))
                            failures.Add($"{path}: prism '{prism.name}' renders material '{mat.name}' on shader '{mat.shader.name}'");
                    }
                }
            }

            Assert.IsEmpty(failures,
                "Prefab(s) carrying a Prism render with a shader the occlusion corridor is not wired into, so that " +
                "mass can never go see-through. The corridor is a platform law (Docs/PRISM_ANIMATION.md §4.7): " +
                "rebase onto a wired prism graph, or add the graph to the census in " +
                "Tools/Shaders/wire_prism_occlusion_corridor.py and re-run it.\n" +
                string.Join("\n", failures));
        }

        [Test]
        public void CorridorConfig_IsSaneWhenAuthored()
        {
            var config = Resources.Load<CosmicShore.ScriptableObjects.PrismOcclusionConfigSO>("PrismOcclusionConfig");
            if (config == null) return; // no asset is legal — the SO's own defaults apply
            if (!config.Enabled) return; // deliberately off is legal, and reads as a zero radius

            // The radii are multiples of the vessel's own circumscribing radius, measured at
            // bind — so what is authored here is the SHAPE of the gradient, not its size.
            Assert.Greater(config.OuterRadiusScale, 0f,
                "PrismOcclusionConfig is enabled but outerRadiusScale <= 0, which the shader reads as 'corridor off'.");
            Assert.LessOrEqual(config.InnerRadiusScale, config.OuterRadiusScale,
                "PrismOcclusionConfig innerRadiusScale must not exceed outerRadiusScale (the feather would invert).");
            Assert.LessOrEqual(config.NearRadiusScale, config.OuterRadiusScale,
                "PrismOcclusionConfig nearRadiusScale must not exceed outerRadiusScale (the corridor would narrow toward the ship).");
            Assert.GreaterOrEqual(config.NearRadiusScale, 0f, "nearRadiusScale must not be negative.");
            Assert.Greater(config.FallbackVesselRadius, 0f,
                "fallbackVesselRadius must be positive — a vessel whose hull cannot be measured would otherwise " +
                "switch the corridor off silently, which is the platform law failing quietly.");
        }
    }
}
#endif
