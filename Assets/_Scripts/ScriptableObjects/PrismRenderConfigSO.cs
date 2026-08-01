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

        // Clock-material animation has NO toggle (STRICT MODE — Docs/PRISM_ANIMATION.md):
        // it is the only prism animation path. The former useClockAnimation opt-in was
        // removed 2026-08-01 at the prompter's direction ("no legacy fallback"). Unwired
        // graphs fail LOUD at the stamp sites, they do not fall back.
    }
}
