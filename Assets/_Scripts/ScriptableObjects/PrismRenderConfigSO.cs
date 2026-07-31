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

        [Header("Clock-Material Animation (Docs/PRISM_ANIMATION.md)")]
        [Tooltip("Drive prism animations (grow-in bloom, color transitions, explosion/implosion) from the GPU clock via " +
                 "one-shot initial-condition stamps instead of per-frame CPU updates — the clock-material law. " +
                 "OPT-IN: defaults OFF. Before enabling, the prism ShaderGraphs must be wired to PrismClockAnimation.hlsl " +
                 "and declare the clock properties (_GrowStartTime/_GrowRate/_GrowStartFrac, _ColorStartTime/_ColorDuration/" +
                 "_StartBrightColor/_StartDarkColor/_StartSpread, _ExplodeStartTime/_ExplodeSpeed/_ExplodeDuration, " +
                 "_SuctionStartTime/_SuctionDuration/_SuctionDirection/_SuctionGrowDelay) as Hybrid Per Instance. " +
                 "See Docs/PRISM_ANIMATION.md §4.4 for the wiring + verification protocol.")]
        [SerializeField] private bool useClockAnimation;

        public bool UseClockAnimation => useClockAnimation;
    }
}
