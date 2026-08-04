using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Fail-loud enforcement for the prism occlusion corridor
    /// (Docs/PRISM_ANIMATION.md §4.6 — a platform law, not a per-vessel or per-mode feature).
    ///
    /// The corridor's failure modes are SILENT: a prism whose material cannot fade simply
    /// stays solid, leaving an invisible hole in the corridor that nothing reports. That is
    /// exactly the opt-in-by-omission this system was rebuilt to abolish, so — per the
    /// project's fail-loud policy, and mirroring <see cref="PrismClockDiagnostics"/> — a
    /// prism that binds a corridor-incapable material screams ONCE, naming the material and
    /// which of the two requirements it misses:
    ///
    ///   1. The material's shader must be one of the corridor-wired prism graphs
    ///      (<see cref="WiredPrismShaderNames"/>). A prism on any other shader — the legacy
    ///      SpreadFresnel/TriangleFresnel family, or a newly authored graph — can never fade.
    ///   2. An OPAQUE material must additionally enable alpha test. URP compiles the Alpha
    ///      output away entirely on an opaque surface without <c>_ALPHATEST_ON</c>, so the
    ///      corridor's fade would be computed and then discarded. Transparent materials
    ///      blend and need nothing extra.
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
        /// </summary>
        public static readonly string[] WiredPrismShaderNames =
        {
            "BlockGraph",
            "ExplodingBlockGraph",
        };

        static readonly HashSet<int> _checked = new();

        static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        static readonly int AlphaClipId = Shader.PropertyToID("_AlphaClip");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
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

            // Transparent materials blend, so the corridor's alpha multiply is enough.
            bool transparent = material.HasProperty(SurfaceId) && material.GetFloat(SurfaceId) > 0.5f;
            if (transparent) return true;

            // Opaque surfaces need alpha test or URP discards the Alpha output entirely.
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

            float alpha = material.HasProperty(AlphaId) ? material.GetFloat(AlphaId) : 1f;
            if (alpha < 0.999f)
            {
                fault = $"has _Alpha {alpha}, not 1 — with alpha test on, this prism is clipped away ENTIRELY";
                return false;
            }
            return true;
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
                "(Docs/PRISM_ANIMATION.md §4.6), not an opt-in: no vessel and no minigame may render live prism " +
                "mass outside it.",
                context);
        }
    }
}
