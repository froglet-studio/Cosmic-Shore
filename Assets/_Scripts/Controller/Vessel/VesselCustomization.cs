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
                 "and every vessel authored to it leaves this alone. Ignored entirely when " +
                 "Domain Replaces Material is set.")]
        [SerializeField] int _domainMaterialSlot = 1;

        [Tooltip("OPTIONAL, and the robust way: name the authored material(s) the domain colour " +
                 "REPLACES, and every slot wearing one is repainted - whatever index it sits at " +
                 "on each renderer. Two reasons to prefer this over the slot index: a vessel " +
                 "whose submesh order is not consistent across its parts (no single index can be " +
                 "right for all of them), and a vessel whose art has MORE than one slot that " +
                 "should carry the domain. Leave empty to use the slot index above.")]
        [SerializeField] List<Material> _domainReplacesMaterials = new();

        /// <summary>
        /// Per-geometry slot indices the domain colour is painted into, resolved ONCE from the
        /// authored materials before the first paint. It has to be cached: after that paint the
        /// slot no longer holds any of <see cref="_domainReplacesMaterials"/> (it holds the
        /// domain material), so re-resolving on a later domain change would find nothing and the
        /// ship would simply stop responding to its domain.
        /// </summary>
        int[][] _domainSlots;

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

        void ApplyShipMaterial(Material material)
        {
            if (!HasDomainMaterialIdentities())
            {
                ShipHelper.ApplyShipMaterial(material, _shipGeometries, _domainMaterialSlot);
                return;
            }

            if (_domainSlots == null) ResolveDomainSlots();
            ShipHelper.ApplyShipMaterialToSlots(material, _shipGeometries, _domainSlots);
        }

        bool HasDomainMaterialIdentities()
        {
            if (_domainReplacesMaterials == null) return false;
            for (int i = 0; i < _domainReplacesMaterials.Count; i++)
                if (_domainReplacesMaterials[i]) return true;
            return false;
        }

        bool IsDomainMaterial(Material candidate)
        {
            for (int i = 0; i < _domainReplacesMaterials.Count; i++)
                if (candidate == _domainReplacesMaterials[i]) return true;
            return false;
        }

        /// <summary>
        /// Find, per geometry, every slot whose AUTHORED material is one of
        /// <see cref="_domainReplacesMaterials"/>. Reads <c>sharedMaterials</c> deliberately -
        /// <c>materials</c> would instantiate a clone per slot and the identity comparison
        /// would then never match the asset.
        /// </summary>
        void ResolveDomainSlots()
        {
            _domainSlots = new int[_shipGeometries.Count][];
            var slots = new List<int>();

            for (int i = 0; i < _shipGeometries.Count; i++)
            {
                slots.Clear();
                var geometry = _shipGeometries[i];
                var renderer = geometry ? geometry.GetComponent<Renderer>() : null;
                if (renderer)
                {
                    var shared = renderer.sharedMaterials;
                    for (int slot = 0; slot < shared.Length; slot++)
                        if (IsDomainMaterial(shared[slot])) slots.Add(slot);

                    if (slots.Count == 0)
                        CSDebug.LogWarning(
                            $"[VesselCustomization] '{geometry.name}' wears none of the " +
                            "vessel's domain materials, so it will never take the domain " +
                            "colour. Check the prefab's material assignments.");
                }
                _domainSlots[i] = slots.ToArray();
            }
        }

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
