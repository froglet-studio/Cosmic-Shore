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

        public Material GetTeamCrystalMaterial(Domains domain, int index)
        {
            switch (index)
            {
                case 0: return TeamMaterialSets[domain].CrystalMaterial;
                case 1: return TeamMaterialSets[domain].CrystalMaterial1;
                case 2: return TeamMaterialSets[domain].CrystalMaterial2;
                case 3: return TeamMaterialSets[domain].CrystalMaterial3;
                default:
                    Debug.LogWarning($"Invalid crystal material index {index} for domain {domain}. Returning default crystal material.");
                    break;
            }
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
