using CosmicShore.Core;

namespace CosmicShore.Game
{
    public static class PlayerVesselInitializeHelper
    {
        public static void SetShipProperties(ThemeManagerDataContainerSO themeManagerData, IVessel vessel, SO_Captain so_captain = null)
        {
            // TODO - Get Captains from data containers
            /*if (so_captain == null && CaptainManager.Instance != null)
            {
                var so_captains = CaptainManager.Instance.GetAllSOCaptains().Where(x => x.Vessel.Class == vessel.VesselStatus.ShipType).ToList();
                so_captain = so_captains[Random.Range(0, 3)];
                var captain = CaptainManager.Instance.GetCaptainByName(so_captain.Name);
                if (captain != null)
                {
                    vessel.AssignCaptain(so_captain);
                    vessel.SetResourceLevels(captain.ResourceLevels);
                }
            }*/

            var materialSet = themeManagerData.TeamMaterialSets[vessel.VesselStatus.Team];
            vessel.SetShipMaterial(materialSet.ShipMaterial);
            vessel.SetBlockSilhouettePrefab(materialSet.BlockSilhouettePrefab);
            vessel.SetAOEExplosionMaterial(materialSet.AOEExplosionMaterial);
            vessel.SetAOEConicExplosionMaterial(materialSet.AOEConicExplosionMaterial);
            vessel.SetSkimmerMaterial(materialSet.SkimmerMaterial);
        }
    }
}