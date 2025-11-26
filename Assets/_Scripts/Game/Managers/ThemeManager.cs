using UnityEngine;


namespace CosmicShore.Core
{
    public class ThemeManager : MonoBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO _dataContainer;

        void Awake()
        {
            var GreenTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.JadeColors, "Green");
            var RedTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.RubyColors, "Red");
            var GoldTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.GoldColors, "Gold");
            var BlueTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.BlueColors, "Blue");

            _dataContainer.TeamMaterialSets = new()
            {
                { Teams.Jade, GreenTeamMaterialSet },
                { Teams.Ruby, RedTeamMaterialSet },
                { Teams.Blue, BlueTeamMaterialSet },
                { Teams.Gold, GoldTeamMaterialSet },
                { Teams.Unassigned, BlueTeamMaterialSet },
            };
        }

        SO_MaterialSet GenerateDomainMaterialSet(DomainColorSet colorSet, string domainName)
        {
            SO_MaterialSet materialSet = ScriptableObject.CreateInstance<SO_MaterialSet>();
            materialSet.name = $"{domainName}TeamMaterialSet";

            // Copy all materials from the base set
            materialSet.ShipMaterial = new Material(_dataContainer.BaseMaterialSet.ShipMaterial);
            materialSet.BlockMaterial = new Material(_dataContainer.BaseMaterialSet.BlockMaterial);
            materialSet.TransparentBlockMaterial =
                new Material(_dataContainer.BaseMaterialSet.TransparentBlockMaterial);
            materialSet.CrystalMaterial = new Material(_dataContainer.BaseMaterialSet.CrystalMaterial);
            materialSet.ExplodingBlockMaterial = new Material(_dataContainer.BaseMaterialSet.ExplodingBlockMaterial);
            materialSet.ShieldedBlockMaterial = new Material(_dataContainer.BaseMaterialSet.ShieldedBlockMaterial);
            materialSet.TransparentShieldedBlockMaterial =
                new Material(_dataContainer.BaseMaterialSet.TransparentShieldedBlockMaterial);
            materialSet.SuperShieldedBlockMaterial =
                new Material(_dataContainer.BaseMaterialSet.SuperShieldedBlockMaterial);
            materialSet.TransparentSuperShieldedBlockMaterial =
                new Material(_dataContainer.BaseMaterialSet.TransparentSuperShieldedBlockMaterial);
            materialSet.DangerousBlockMaterial = new Material(_dataContainer.BaseMaterialSet.DangerousBlockMaterial);
            materialSet.TransparentDangerousBlockMaterial =
                new Material(_dataContainer.BaseMaterialSet.TransparentDangerousBlockMaterial);
            materialSet.AOEExplosionMaterial = new Material(_dataContainer.BaseMaterialSet.AOEExplosionMaterial);
            materialSet.AOEConicExplosionMaterial =
                new Material(_dataContainer.BaseMaterialSet.AOEConicExplosionMaterial);
            materialSet.SpikeMaterial = new Material(_dataContainer.BaseMaterialSet.SpikeMaterial);
            materialSet.SkimmerMaterial = new Material(_dataContainer.BaseMaterialSet.SkimmerMaterial);

            // Copy prefab reference
            materialSet.BlockSilhouettePrefab = _dataContainer.BaseMaterialSet.BlockSilhouettePrefab;

            // Set colors for materials that use domain-specific colors
            materialSet.BlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.BlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.TransparentBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.TransparentBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.CrystalMaterial.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);

            materialSet.ExplodingBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.ExplodingBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.DangerousBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.TransparentDangerousBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.ShieldedBlockMaterial.SetColor("_BrightColor", colorSet.ShieldedInsideBlockColor);
            materialSet.ShieldedBlockMaterial.SetColor("_DarkColor", colorSet.ShieldedOutsideBlockColor);

            materialSet.TransparentShieldedBlockMaterial.SetColor("_BrightColor", colorSet.ShieldedInsideBlockColor);
            materialSet.TransparentShieldedBlockMaterial.SetColor("_DarkColor", colorSet.ShieldedOutsideBlockColor);

            materialSet.SuperShieldedBlockMaterial.SetColor("_BrightColor", colorSet.SuperShieldedInsideBlockColor);
            materialSet.SuperShieldedBlockMaterial.SetColor("_DarkColor", colorSet.SuperShieldedOutsideBlockColor);

            materialSet.TransparentSuperShieldedBlockMaterial.SetColor("_BrightColor",
                colorSet.SuperShieldedInsideBlockColor);
            materialSet.TransparentSuperShieldedBlockMaterial.SetColor("_DarkColor",
                colorSet.SuperShieldedOutsideBlockColor);

            materialSet.ShipMaterial.SetColor("_Color1", colorSet.ShipColor1);
            materialSet.ShipMaterial.SetColor("_Color2", colorSet.ShipColor2);

            materialSet.AOEExplosionMaterial.SetColor("_TextureColor", colorSet.AOETextureColor);
            materialSet.AOEExplosionMaterial.SetColor("_FresnelColor", colorSet.AOEFresnelColor);

            materialSet.AOEConicExplosionMaterial.SetColor("_Color", colorSet.AOEConicColor);
            materialSet.AOEConicExplosionMaterial.SetColor("_EdgeColor", colorSet.AOEConicEdgeColor);

            materialSet.SpikeMaterial.SetColor("_LightColor", colorSet.SpikeLightColor);
            materialSet.SpikeMaterial.SetColor("_DarkColor", colorSet.SpikeDarkColor);

            materialSet.SkimmerMaterial.SetColor("_Color", colorSet.SkimmerColor);

            return materialSet;
        }
    }
}