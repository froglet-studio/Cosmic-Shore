using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Utility component for applying vessel specific customisation such as
    /// materials and cosmetic settings.
    /// </summary>
    public class VesselCustomization : MonoBehaviour
    {
        [SerializeField]
        List<GameObject> _shipGeometries;

        IVesselStatus vesselStatus;

        public void Initialize(IVesselStatus vesselStatus)
        {
            if (!TryPassNullChecks(vesselStatus))
                return;

            this.vesselStatus = vesselStatus;

            if (this.vesselStatus.ShipGeometries == null || this.vesselStatus.ShipGeometries.Count == 0)
                this.vesselStatus.ShipGeometries = _shipGeometries;
            else
                this.vesselStatus.ShipGeometries.AddRange(_shipGeometries);

            ApplyShipMaterial(this.vesselStatus.ShipMaterial);
        }

        void ApplyShipMaterial(Material material) =>
            ShipHelper.ApplyShipMaterial(material, _shipGeometries);

        bool TryPassNullChecks(IVesselStatus vesselStatus)
        {
            if (vesselStatus == null)
            {
                Debug.LogError("VesselStatus is null. Cannot initialize VesselCustomization.");
                return false;
            }
            if (_shipGeometries == null || _shipGeometries.Count == 0)
            {
                Debug.LogError("Vessel geometries are not set. Cannot apply vessel material.");
                return false;
            }
            return true;
        }
    }
}
