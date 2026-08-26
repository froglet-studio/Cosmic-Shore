using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Fail-loud enforcement for the prism occlusion corridor — and, since the dither
    /// became THE prism transparency mechanism (2026-08-10), for the prism material
    /// contract as a whole (Docs/PRISM_ANIMATION.md §4.7 — a platform law, not a
    /// per-vessel or per-mode feature).
    ///
    /// The corridor's failure modes are SILENT: a prism whose material cannot fade simply
    /// stays solid, leaving an invisible hole in the corridor that nothing reports. That is
    /// exactly the opt-in-by-omission this system was rebuilt to abolish, so — per the
    /// project's fail-loud policy, and mirroring <see cref="PrismClockDiagnostics"/> — a
    /// prism that binds an off-contract material screams ONCE, naming the material and
    /// which of the two requirements it misses:
    ///
    ///   1. The material's shader must be one of the corridor-wired prism graphs
    ///      (<see cref="WiredPrismShaderNames"/>). A prism on any other shader — the legacy
    ///      SpreadFresnel/TriangleFresnel family, or a newly authored graph — can never fade.
    ///   2. The material must be OPAQUE with alpha test enabled. URP compiles the Alpha
    ///      output away entirely on an opaque surface without <c>_ALPHATEST_ON</c>, so the
    ///      fade would be computed and then discarded. A TRANSPARENT prism material is now
    ///      equally a fault: every prism transparency effect — the corridor fade, the
    ///      exploding-debris fade-out, the cloak family's authored near-zero alpha — rides
    ///      the screen-door dither (PrismOcclusionFade engages its threshold for ANY
    ///      fractional final alpha), so a blending prism pays the transparent queue for an
    ///      effect the opaque clip already provides and renders a second, inconsistent
    ///      kind of transparency next to the dither. Authored sub-1 <c>_Alpha</c> /
    ///      <c>_Opacity</c> values are LEGAL on any prism material — they are the dither
    ///      coverage (the cloak ships 0.01 on purpose). Fix either fault with
    ///      <c>python3 Tools/Shaders/enable_prism_alpha_clip.py</c>.
    ///
    /// Note on (1): the corridor's two uniforms are UNEXPOSED globals (that is what lets
    /// <c>Shader.SetGlobalVector</c> drive them), and an unexposed ShaderGraph property is
    /// declared outside <c>UnityPerMaterial</c> — so it does NOT appear in the shader's
    /// property list and <c>Material.HasProperty("_PrismOcclusionParams")</c> is always
    /// false. The same trap is why <c>PrismClockWiringValidator</c> checks <c>_PrismClock</c>
    /// against the graph TEXT rather than via HasProperty. The shader-name census is the
    /// check a material can actually answer at runtime; the graph wiring itself is verified
    /// from the assets by FrogletTools > Ecology > Prism Animation > Validate Occlusion Corridor.
    ///
    /// One check per MATERIAL for the lifetime of the process, not per prism.
    /// </summary>
    public static class PrismOcclusionDiagnostics
    {
        /// <summary>
        /// The prism ShaderGraphs the corridor is wired into — the census
        /// <c>Tools/Shaders/wire_prism_occlusion_corridor.py</c> maintains. Every shader a
        /// LIVE prism can render with must be on this list. Shared with the editor validator
        /// so the runtime check and the asset gate can never disagree.
        ///
        /// Deliberate exclusion: SuctionGraph. It is live (batched implosion debris draws
        /// ImplodingPrismMaterial, read off PrismImplosion.prefab in
        /// <c>PrismDebris.ConfigureImplosion</c>) but it renders a prism DURING consumption —
        /// a sub-second implode of mass being removed — never standing mass that can occlude.
        /// Named here the same way KnownLegacyPrismPrefabs is named in the validator, so the
        /// exclusion cannot look like an omission. Do not add SuctionGraph to this list
        /// without wiring PrismOcclusionFade into it — <see cref="IsCorridorCapable"/> would
        /// then fail every suction material at runtime.
        /// </summary>
        public static readonly string[] WiredPrismShaderNames =
        {
            "BlockGraph",
            "ExplodingBlockGraph",
        };

        static readonly HashSet<int> _checked = new();

        // Warn-once means once per PLAY SESSION — see PrismClockDiagnostics.ResetWarnings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetWarnings()
        {
            _checked.Clear();
            _warnedRadiusVessels.Clear();
            _warnedUnmeasurableVessel = false;
        }

        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        const string AlphaTestKeyword = "_ALPHATEST_ON";

        /// <summary>True if <paramref name="shaderName"/> is one of the corridor-wired prism graphs.</summary>
        public static bool IsWiredPrismShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            foreach (var graph in WiredPrismShaderNames)
            {
                // Exact terminal segment: EndsWith("BlockGraph") would also swallow
                // "ExplodingBlockGraph" (and vice versa is harmless, but the trap is real —
                // it produced false failures in the clock validator once already).
                if (shaderName == graph || shaderName.EndsWith("/" + graph))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// THE rule: can the corridor dissolve a prism rendering with this material?
        /// One implementation, shared by the runtime scream below, the editor validator
        /// (FrogletTools > Ecology > Prism Animation > Validate Occlusion Corridor) and the
        /// edit-mode coverage test — so the asset gate and the runtime gate can never drift
        /// apart. Returns false and fills <paramref name="fault"/> when the material breaks it.
        /// </summary>
        public static bool IsCorridorCapable(Material material, out string fault)
        {
            fault = null;
            if (material == null) return true; // nothing bound yet; not a violation

            var shaderName = material.shader != null ? material.shader.name : null;
            if (!IsWiredPrismShader(shaderName))
            {
                fault = $"renders with shader '{shaderName}', which is NOT one of the corridor-wired prism graphs " +
                        $"({string.Join(", ", WiredPrismShaderNames)}) — prisms using it can never go see-through and " +
                        "WILL hide the player's ship. Fix: rebase onto a wired prism graph, or add its graph to the " +
                        "census in Tools/Shaders/wire_prism_occlusion_corridor.py and re-run it";
                return false;
            }

            // Transparent prism materials are off-contract: the screen-door dither IS prism
            // transparency now (fade-outs, cloak, corridor — one mechanism, composing in
            // coverage), so a blending prism pays sorting + blend overdraw for an effect the
            // opaque clip already provides, renders an inconsistent second kind of
            // transparency next to the dither, and stops writing depth.
            bool transparent = material.HasProperty(SurfaceId) && material.GetFloat(SurfaceId) > 0.5f;
            if (transparent)
            {
                fault = "is TRANSPARENT — prism transparency rides the opaque screen-door dither " +
                        "(PrismOcclusionFade clips any fractional alpha), so no prism material may use the " +
                        "transparent queue. Fix: `python3 Tools/Shaders/enable_prism_alpha_clip.py` (converts it " +
                        "to opaque + alpha clip, preserving its authored alpha as dither coverage)";
                return false;
            }

            // Opaque surfaces need alpha test or URP discards the Alpha output entirely.
            // Note authored sub-1 _Alpha / _Opacity is NOT checked here: those values are the
            // material's dither coverage (the cloak family ships near-zero on purpose), not
            // stale data — the old "_Alpha must be 1" rule predates fade-anywhere dithering.
            bool clipOn = material.HasProperty(AlphaClipId) && material.GetFloat(AlphaClipId) > 0.5f;
            bool keywordOn = material.IsKeywordEnabled(AlphaTestKeyword);
            if (!clipOn || !keywordOn)
            {
                fault = $"is OPAQUE without alpha test (_AlphaClip={(clipOn ? 1 : 0)}, " +
                        $"{AlphaTestKeyword}={(keywordOn ? "on" : "OFF")}) — URP compiles the Alpha output away on an " +
                        "opaque surface, so the corridor's fade is computed and then thrown away and this prism stays " +
                        "solid in front of the ship (an invisible hole in the corridor). Fix: " +
                        "`python3 Tools/Shaders/enable_prism_alpha_clip.py`, or tick Alpha Clip on the material";
                return false;
            }
            return true;
        }

        static bool _warnedUnmeasurableVessel;

        /// <summary>
        /// The bound vessel's hull measured to nothing, so the corridor cannot be sized from
        /// it. Screams ONCE and the corridor falls back to the config's radius rather than
        /// switching off — a corridor that silently disappears because a vessel has no mesh
        /// renderers would be the platform law failing quietly, which is the failure class
        /// this whole system was rebuilt to remove.
        /// </summary>
        public static void WarnUnmeasurableVessel(Object vessel, float fallbackRadius)
        {
            if (!Application.isPlaying || _warnedUnmeasurableVessel) return;
            _warnedUnmeasurableVessel = true;
            Debug.LogError(
                $"[PrismOcclusion] could not measure a circumscribing radius for vessel '{(vessel != null ? vessel.name : "<null>")}' — " +
                "it has no MeshFilter/SkinnedMeshRenderer hull outside its skimmers. The occlusion corridor is sized " +
                $"from the vessel's own hull, so it has fallen back to PrismOcclusionConfig.fallbackVesselRadius " +
                $"({fallbackRadius}). Give the vessel a renderable hull, or the corridor around it is a guess. " +
                "Docs/PRISM_ANIMATION.md §4.7.",
                vessel);
        }

        static readonly HashSet<int> _warnedRadiusVessels = new();

        /// <summary>
        /// A vessel's hull measured to an implausible radius and was clamped into the config's
        /// sanity band. Once per vessel, naming the measured and used values — the number the
        /// corridor is sized from should never be a surprise.
        /// </summary>
        public static void WarnImplausibleVesselRadius(Object vessel, float measured, float used)
        {
            if (!Application.isPlaying || vessel == null) return;
            if (!_warnedRadiusVessels.Add(vessel.GetInstanceID())) return;
            Debug.LogError(
                $"[PrismOcclusion] vessel '{vessel.name}' measured a circumscribing radius of {measured:F2} world " +
                $"units, outside PrismOcclusionConfig's sanity band — clamped to {used:F2}. The occlusion corridor " +
                "is sized entirely from this number, so an unclamped value would either leave the ship hidden or " +
                "dissolve the world in front of it. Check the vessel's hull renderers (a stray oversized mesh, or a " +
                "mis-scaled model), or widen the band if the hull really is this size. Docs/PRISM_ANIMATION.md §4.7.",
                vessel);
        }

        /// <summary>
        /// Verify that a material a prism is about to render with can be dissolved by the
        /// corridor, and scream once per offending material if not. Cheap after the first
        /// call per material (a HashSet probe), and silent outside play mode — edit-mode
        /// tooling legitimately runs the prism lifecycle, and the asset-side gates for that
        /// are the validator and the coverage test.
        /// </summary>
        public static void VerifyCorridorCapable(Material material, Object context)
        {
            if (!Application.isPlaying || material == null) return;
            if (!_checked.Add(material.GetInstanceID())) return;
            if (IsCorridorCapable(material, out string fault)) return;

            Debug.LogError(
                $"[PrismOcclusion] prism material '{material.name}' {fault}. " +
                "The camera↔vessel occlusion corridor is a PLATFORM LAW " +
                "(Docs/PRISM_ANIMATION.md §4.7), not an opt-in: no vessel and no minigame may render live prism " +
                "mass outside it.",
                context);
        }
    }
}
