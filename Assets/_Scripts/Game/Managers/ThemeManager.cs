using CosmicShore.Utility.Singleton;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    public class ThemeManager : SingletonPersistent<ThemeManager>
    {

        [SerializeField] SO_MaterialSet BaseMaterialSet;
        [SerializeField] SO_ColorSet ColorSet;

        SO_MaterialSet GreenTeamMaterialSet;
        SO_MaterialSet RedTeamMaterialSet;
        SO_MaterialSet BlueTeamMaterialSet;
        SO_MaterialSet GoldTeamMaterialSet;

        public Dictionary<Teams, SO_MaterialSet> TeamMaterialSets { get; private set; }


        public override void Awake()
        {
            base.Awake();

            GreenTeamMaterialSet = GenerateDomainMaterialSet(ColorSet.JadeColors, "Green");
            RedTeamMaterialSet = GenerateDomainMaterialSet(ColorSet.RubyColors, "Red");
            GoldTeamMaterialSet = GenerateDomainMaterialSet(ColorSet.GoldColors, "Gold");
            BlueTeamMaterialSet = GenerateDomainMaterialSet(ColorSet.BlueColors, "Blue");

            TeamMaterialSets = new() {
                { Teams.Jade, GreenTeamMaterialSet },
                { Teams.Ruby,   RedTeamMaterialSet },
                { Teams.Blue,  BlueTeamMaterialSet },
                { Teams.Gold,  GoldTeamMaterialSet },
                { Teams.Unassigned,  BlueTeamMaterialSet },
            };
        }

        SO_MaterialSet GenerateDomainMaterialSet(DomainColorSet colorSet, string domainName)
        {
            SO_MaterialSet materialSet = ScriptableObject.CreateInstance<SO_MaterialSet>();
            materialSet.name = $"{domainName}TeamMaterialSet";

            // Copy all materials from the base set
            materialSet.ShipMaterial = new Material(BaseMaterialSet.ShipMaterial);
            materialSet.BlockMaterial = new Material(BaseMaterialSet.BlockMaterial);
            materialSet.TransparentBlockMaterial = new Material(BaseMaterialSet.TransparentBlockMaterial);
            materialSet.CrystalMaterial = new Material(BaseMaterialSet.CrystalMaterial);
            materialSet.ExplodingBlockMaterial = new Material(BaseMaterialSet.ExplodingBlockMaterial);
            materialSet.ShieldedBlockMaterial = new Material(BaseMaterialSet.ShieldedBlockMaterial);
            materialSet.TransparentShieldedBlockMaterial = new Material(BaseMaterialSet.TransparentShieldedBlockMaterial);
            materialSet.SuperShieldedBlockMaterial = new Material(BaseMaterialSet.SuperShieldedBlockMaterial);
            materialSet.TransparentSuperShieldedBlockMaterial = new Material(BaseMaterialSet.TransparentSuperShieldedBlockMaterial);
            materialSet.DangerousBlockMaterial = new Material(BaseMaterialSet.DangerousBlockMaterial);
            materialSet.TransparentDangerousBlockMaterial = new Material(BaseMaterialSet.TransparentDangerousBlockMaterial);
            materialSet.AOEExplosionMaterial = new Material(BaseMaterialSet.AOEExplosionMaterial);
            materialSet.AOEConicExplosionMaterial = new Material(BaseMaterialSet.AOEConicExplosionMaterial);
            materialSet.SpikeMaterial = new Material(BaseMaterialSet.SpikeMaterial);
            materialSet.SkimmerMaterial = new Material(BaseMaterialSet.SkimmerMaterial);

            // Copy prefab reference
            materialSet.BlockSilhouettePrefab = BaseMaterialSet.BlockSilhouettePrefab;

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

            materialSet.TransparentSuperShieldedBlockMaterial.SetColor("_BrightColor", colorSet.SuperShieldedInsideBlockColor);
            materialSet.TransparentSuperShieldedBlockMaterial.SetColor("_DarkColor", colorSet.SuperShieldedOutsideBlockColor);

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

        public void SetBackgroundColor(Camera mainCamera)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    Debug.LogError("No camera found in the scene!");
                    return;
                }
            }

            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = ColorSet.EnvironmentColors.SkyColor;
        }

        public Material GetTeamBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].BlockMaterial;
        }

        public Material GetTeamTransparentBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].TransparentBlockMaterial;
        }

        public Material GetTeamCrystalMaterial(Teams team)
        {
            return TeamMaterialSets[team].CrystalMaterial;
        }

        public Material GetTeamExplodingBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].ExplodingBlockMaterial;
        }

        public Material GetTeamSpikeMaterial(Teams team)
        {
            return TeamMaterialSets[team].SpikeMaterial;
        }

        public Material GetTeamShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].ShieldedBlockMaterial;
        }

        public Material GetTeamTransparentShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].TransparentShieldedBlockMaterial;
        }

        public Material GetTeamDangerousBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].DangerousBlockMaterial;
        }

        public Material GetTeamTransparentDangerousBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].TransparentDangerousBlockMaterial;
        }

        public Material GetTeamSuperShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].SuperShieldedBlockMaterial;
        }

        public Material GetTeamTransparentSuperShieldedBlockMaterial(Teams team)
        {
            return TeamMaterialSets[team].TransparentSuperShieldedBlockMaterial;
        }
    }
}
