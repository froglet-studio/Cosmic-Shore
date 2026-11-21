using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Config for the instanced prism rendering path (Entities Graphics /
    /// BatchRendererGroup — see Docs/PRISM_ECS_MIGRATION.md). Place the asset
    /// at Resources/PrismRenderConfig to control the master toggle; when no
    /// asset exists the path defaults ON.
    /// </summary>
    [CreateAssetMenu(fileName = "PrismRenderConfig", menuName = "ScriptableObjects/Rendering/Prism Render Config")]
    public class PrismRenderConfigSO : ScriptableObject
    {
        [Header("Instanced Rendering")]
        [Tooltip("Render prisms through Entities Graphics companion entities (instanced batches) instead of per-prism MeshRenderers. " +
                 "OPT-IN: defaults OFF so the proven legacy path is always the baseline. " +
                 "Before enabling, the prism ShaderGraphs (BlockGraph / ExplodingBlockGraph / SuctionGraph) must have their " +
                 "_BrightColor/_DarkColor/_Spread/_ExplosionAmount/_Opacity/_State/_Location properties set to 'Hybrid Per Instance' " +
                 "or per-instance colors/animation will not reach the shader. See Docs/PRISM_ECS_MIGRATION.md §7.")]
        [SerializeField] private bool useInstancedRendering;

        public bool UseInstancedRendering => useInstancedRendering;

        [Header("No-Compute Devices (MeshRenderer stamp path)")]
        [Tooltip("Only read on a device where Entities Graphics cannot run (a WebGL2 browser — Docs/PRISM_ANIMATION.md " +
                 "§4.11), where death visuals are drawn by small pooled MeshRenderer pieces instead of batched entities. " +
                 "The most pieces that may draw at once: each is one draw call outside the SRP Batcher, so this bounds the " +
                 "cost of a mass death. Past half of it pieces shorten (quicker puffs) so the pool recycles; past all of it " +
                 "a death has no visual. Ignored on every device with Entities Graphics.")]
        [SerializeField, Min(0)] private int meshRendererDebrisCap = 96;

        public int MeshRendererDebrisCap => Mathf.Max(0, meshRendererDebrisCap);

        // Clock-material animation has NO toggle (STRICT MODE — Docs/PRISM_ANIMATION.md):
        // it is the only prism animation path. The former useClockAnimation opt-in was
        // removed 2026-08-01 at the prompter's direction ("no legacy fallback"). Unwired
        // graphs fail LOUD at the stamp sites, they do not fall back.
    }
}
