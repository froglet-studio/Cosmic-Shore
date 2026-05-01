using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
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

        /// <summary>
        /// Re-applies the current <see cref="IVesselStatus.ShipMaterial"/> to the
        /// ship geometry mesh renderers. Use this when something downstream changes
        /// the vessel's domain (and thus material reference) AFTER
        /// <see cref="Initialize"/> has already painted the mesh — e.g., the menu
        /// autopilot's domain swap. Without this, the mesh keeps the original
        /// material even though <c>vesselStatus.ShipMaterial</c> points to a new one.
        /// </summary>
        public void RefreshShipMaterial()
        {
            if (vesselStatus == null) return;
            ApplyShipMaterial(vesselStatus.ShipMaterial);
        }

        void ApplyShipMaterial(Material material) =>
            ShipHelper.ApplyShipMaterial(material, _shipGeometries);

        bool TryPassNullChecks(IVesselStatus vesselStatus)
        {
            if (vesselStatus == null)
            {
                CSDebug.LogError("VesselStatus is null. Cannot initialize VesselCustomization.");
                return false;
            }
            if (_shipGeometries == null || _shipGeometries.Count == 0)
            {
                CSDebug.LogError("Vessel geometries are not set. Cannot apply vessel material.");
                return false;
            }
            return true;
        }
    }
}
