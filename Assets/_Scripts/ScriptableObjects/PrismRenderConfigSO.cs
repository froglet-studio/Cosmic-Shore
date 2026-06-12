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
        [Tooltip("Render prisms through Entities Graphics companion entities (instanced batches) instead of per-prism MeshRenderers. Disable to A/B against the legacy path — flip requires re-entering play mode for existing prisms.")]
        [SerializeField] private bool useInstancedRendering = true;

        public bool UseInstancedRendering => useInstancedRendering;
    }
}
