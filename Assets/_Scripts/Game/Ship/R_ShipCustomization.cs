using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Utility component for applying ship specific customisation such as
    /// materials and cosmetic settings.
    /// </summary>
    public class R_ShipCustomization : MonoBehaviour
    {
        [SerializeField]
        List<GameObject> _shipGeometries;

        IShipStatus _shipStatus;

        public void Initialize(IShipStatus shipStatus)
        {
            if (!TryPassNullChecks(shipStatus))
                return;

            _shipStatus = shipStatus;

            if (_shipStatus.ShipGeometries == null || _shipStatus.ShipGeometries.Count == 0)
                _shipStatus.ShipGeometries = _shipGeometries;
            else
                _shipStatus.ShipGeometries.AddRange(_shipGeometries);

            ApplyShipMaterial(_shipStatus.ShipMaterial);
        }

        void ApplyShipMaterial(Material material) =>
            ShipHelper.ApplyShipMaterial(material, _shipGeometries);

        bool TryPassNullChecks(IShipStatus shipStatus)
        {
            if (shipStatus == null)
            {
                Debug.LogError("ShipStatus is null. Cannot initialize R_ShipCustomization.");
                return false;
            }
            if (_shipGeometries == null || _shipGeometries.Count == 0)
            {
                Debug.LogError("Ship geometries are not set. Cannot apply ship material.");
                return false;
            }
            return true;
        }
    }
}
