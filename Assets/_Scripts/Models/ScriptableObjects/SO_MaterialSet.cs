using UnityEngine;

[CreateAssetMenu(fileName = "Material Set", menuName = "CosmicShore/MaterialSet", order = 19)]
[System.Serializable]
public class SO_MaterialSet : ScriptableObject
{
    [SerializeField] public Material ShipMaterial;
    [SerializeField] public Material BlockMaterial;
    [SerializeField] public Material TransparentBlockMaterial;
    [SerializeField] public Material CrystalMaterial;
    [SerializeField] public Material ExplodingBlockMaterial;
    [SerializeField] public Material ShieldedBlockMaterial;
    [SerializeField] public Material TransparentShieldedBlockMaterial;
    [SerializeField] public Material SuperShieldedBlockMaterial;
    [SerializeField] public Material TransparentSuperShieldedBlockMaterial;
    [SerializeField] public Material DangerousBlockMaterial;
    [SerializeField] public Material TransparentDangerousBlockMaterial;
    [SerializeField] public GameObject BlockSilhouettePrefab; // TODO: Move to separate SO
    [SerializeField] public Material AOEExplosionMaterial;
    [SerializeField] public Material AOEConicExplosionMaterial;
    [SerializeField] public Material SpikeMaterial;
    [SerializeField] public Material SkimmerMaterial;
}