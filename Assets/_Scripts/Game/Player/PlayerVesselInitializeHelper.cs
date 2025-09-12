using CosmicShore.Core;

namespace CosmicShore.Game
{
    public static class PlayerVesselInitializeHelper
    {
        public static void SetShipProperties(ThemeManagerDataContainerSO themeManagerData, IShip ship, SO_Captain so_captain = null)
        {
            // TODO - Get Captains from data containers
            /*if (so_captain == null && CaptainManager.Instance != null)
            {
                var so_captains = CaptainManager.Instance.GetAllSOCaptains().Where(x => x.Ship.Class == ship.ShipStatus.ShipType).ToList();
                so_captain = so_captains[Random.Range(0, 3)];
                var captain = CaptainManager.Instance.GetCaptainByName(so_captain.Name);
                if (captain != null)
                {
                    ship.AssignCaptain(so_captain);
                    ship.SetResourceLevels(captain.ResourceLevels);
                }
            }*/

            var materialSet = themeManagerData.TeamMaterialSets[ship.ShipStatus.Team];
            ship.SetShipMaterial(materialSet.ShipMaterial);
            ship.SetBlockSilhouettePrefab(materialSet.BlockSilhouettePrefab);
            ship.SetAOEExplosionMaterial(materialSet.AOEExplosionMaterial);
            ship.SetAOEConicExplosionMaterial(materialSet.AOEConicExplosionMaterial);
            ship.SetSkimmerMaterial(materialSet.SkimmerMaterial);
        }
    }
}