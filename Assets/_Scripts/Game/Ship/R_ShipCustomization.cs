using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Utility component for applying ship specific customisation such as
    /// materials and cosmetic settings.
    /// </summary>
    public class R_ShipCustomization : MonoBehaviour
    {
        IShip _ship;

        public void Initialize(IShip ship) => _ship = ship;

        public void SetShipMaterial(Material material)
        {
            ShipHelper.ApplyShipMaterial(material, _ship.ShipStatus.ShipGeometries);
        }

        public void SetBlockSilhouettePrefab(GameObject prefab)
        {
            _ship.ShipStatus.Silhouette?.SetBlockPrefab(prefab);
        }

        public void SetAOEExplosionMaterial(Material material)
        {
            _ship.ShipStatus.AOEExplosionMaterial = material;
        }

        public void SetAOEConicExplosionMaterial(Material material)
        {
            _ship.ShipStatus.AOEConicExplosionMaterial = material;
        }

        public void SetSkimmerMaterial(Material material)
        {
            _ship.ShipStatus.SkimmerMaterial = material;
        }
    }
}
