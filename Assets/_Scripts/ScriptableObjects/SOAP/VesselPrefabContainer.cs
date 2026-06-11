using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "DataContainer_VesselPrefab", menuName = "ScriptableObjects/Data Containers/VesselPrefabContainer")]
    public class VesselPrefabContainer : ScriptableObject
    {
        [SerializeField]
        Transform[] _shipPrefabs;

        public bool TryGetShipPrefab(VesselClassType vesselType, out Transform shipPrefabTransform)
        {
            shipPrefabTransform = null;

            if (_shipPrefabs == null || _shipPrefabs.Length == 0)
            {
                CSDebug.LogError("No Vessel Prefabs found! This should never happen!");
                return false;
            }

            foreach (var prefab in _shipPrefabs)
            {
                if (prefab == null)
                    continue;

                if (!prefab.TryGetComponent(out IVesselStatus shipStatus))
                {
                    CSDebug.LogWarning($"Vessel prefab {prefab.name} does not have a VesselStatus component — skipping.");
                    continue;
                }

                if (shipStatus.VesselType != vesselType)
                    continue;

                shipPrefabTransform = prefab.transform;
            }

            if (shipPrefabTransform == null)
            {
                CSDebug.LogError($"No Vessel Prefab found matching vessel type {vesselType}!");
                return false;
            }

            return true;
        }
    }
}