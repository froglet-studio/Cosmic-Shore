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

        [Tooltip("Which material slot on a MESH renderer wears the DOMAIN colour. The platform " +
                 "contract is 1 (submesh 0 = shared body material, submesh 1 = the domain part), " +
                 "and every vessel authored to it leaves this alone. Set 0 only when the FBX " +
                 "authors its submeshes the other way round, so the domain lands on the hull " +
                 "instead of on a trim detail.")]
        [SerializeField] int _domainMaterialSlot = 1;

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
        /// ship geometry mesh renderers. Needed when the vessel's domain (and thus
        /// material reference) changes AFTER <see cref="Initialize"/> has already
        /// painted the mesh - the one-shot paint does not follow reference swaps.
        /// Called by <see cref="ShipHelper.SetShipProperties"/> on already-painted
        /// vessels; prefer that entry point over calling this directly, so the
        /// references and the mesh can never go out of sync.
        /// No-op before <see cref="Initialize"/> (vesselStatus is still null).
        /// </summary>
        public void RefreshShipMaterial()
        {
            if (vesselStatus == null) return;
            ApplyShipMaterial(vesselStatus.ShipMaterial);
        }

        void ApplyShipMaterial(Material material) =>
            ShipHelper.ApplyShipMaterial(material, _shipGeometries, _domainMaterialSlot);

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
