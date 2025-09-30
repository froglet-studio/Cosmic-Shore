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

        public Dictionary<Domains, SO_MaterialSet> TeamMaterialSets { get; set; }

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

        public Material GetTeamBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].BlockMaterial;
        }

        public Material GetTeamTransparentBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].TransparentBlockMaterial;
        }

        public Material GetTeamCrystalMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].CrystalMaterial;
        }

        public Material GetTeamSpikeMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].SpikeMaterial;
        }

        public Material GetTeamShieldedBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].ShieldedBlockMaterial;
        }

        public Material GetTeamTransparentShieldedBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].TransparentShieldedBlockMaterial;
        }

        public Material GetTeamDangerousBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].DangerousBlockMaterial;
        }

        public Material GetTeamTransparentDangerousBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].TransparentDangerousBlockMaterial;
        }

        public Material GetTeamSuperShieldedBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].SuperShieldedBlockMaterial;
        }

        public Material GetTeamTransparentSuperShieldedBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].TransparentSuperShieldedBlockMaterial;
        }
    }
}
