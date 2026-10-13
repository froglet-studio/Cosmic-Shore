using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Material Set", menuName = "ScriptableObjects/MaterialSet", order = 19)]
    [System.Serializable]
    public class SO_MaterialSet : ScriptableObject
    {
        [SerializeField] public Material ShipMaterial;
        [SerializeField] public Material BlockMaterial;
        [SerializeField] public Material TransparentBlockMaterial;
        [Tooltip("A crystal material ThemeManager recolours per domain. A domain-owned crystal model wears the clone of the slot that holds its OWN default material (ThemeManagerDataContainerSO.GetTeamCrystalMaterial). The base set: the omni body.")]
        [SerializeField] public Material CrystalMaterial;
        [Tooltip("Second crystal material slot. The base set: the omni's depth prisms (OmniDepthPrisms).")]
        [SerializeField] public Material CrystalMaterial1;
        [Tooltip("Optional, empty in the base set since 2026-10-13 (it named a retired breathing tone material).")]
        [SerializeField] public Material CrystalMaterial2;
        [Tooltip("Optional, empty in the base set since 2026-10-13 (it named a retired breathing tone material).")]
        [SerializeField] public Material CrystalMaterial3;
        [Tooltip("Optional, empty in the base set (it named the omni's retired stationary Shepard rim, Docs/PALETTE.md §2.10).")]
        [SerializeField] public Material CrystalMaterial4;
        [SerializeField] public Material ExplodingBlockMaterial;
        [SerializeField] public Material ShieldedBlockMaterial;
        [SerializeField] public Material TransparentShieldedBlockMaterial;
        [SerializeField] public Material SuperShieldedBlockMaterial;
        [SerializeField] public Material TransparentSuperShieldedBlockMaterial;
        [SerializeField] public Material DangerousBlockMaterial;
        [SerializeField] public Material TransparentDangerousBlockMaterial;
        [SerializeField] public Material AOEExplosionMaterial;
        [SerializeField] public Material AOEConicExplosionMaterial;
        [SerializeField] public Material SpikeMaterial;
        [SerializeField] public Material SkimmerMaterial;
    }
}
