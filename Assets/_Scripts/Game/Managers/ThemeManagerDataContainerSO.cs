using CosmicShore.Utilities;
using System.Collections.Generic;
using UnityEngine;


namespace CosmicShore.Core
{
    [CreateAssetMenu(fileName = "ThemeManagerDataContainer", menuName = "ScriptableObjects/DataContainers/ThemeManagerDataContainerSO")]
    public class ThemeManagerDataContainerSO : ScriptableObject
    {
        public SO_MaterialSet BaseMaterialSet;
        public SO_ColorSet ColorSet;

        public Dictionary<Teams, SO_MaterialSet> TeamMaterialSets { get; set; }

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
