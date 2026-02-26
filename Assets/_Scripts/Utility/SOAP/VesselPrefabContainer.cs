using CosmicShore.Game.Ship;
using UnityEngine;
using CosmicShore.Utility.Recording;
using CosmicShore.Models.Enums;

namespace CosmicShore.Utility.SOAP
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "DataContainer_VesselPrefab", menuName = "ScriptableObjects/SOAP/Data Containers/VesselPrefabContainer")]
    public class VesselPrefabContainer : ScriptableObject
    {
        [SerializeField]
        Transform[] _shipPrefabs;

        public bool TryGetShipPrefab(VesselClassType vesselType, out Transform shipPrefabTransform)
        {
            shipPrefabTransform = null;

            if (_shipPrefabs.Length == 0)
            {
                CSDebug.LogError("No Vessel Prefabs found! This should never happen!");
                return false;
            }

            foreach (var prefab in _shipPrefabs)
            {
                if (!prefab.TryGetComponent(out IVesselStatus shipStatus))
                {
                    CSDebug.LogError($"Vessel prefab {prefab} does not have a VesselStatus component attached!");
                    return false;
                }

                if (shipStatus.VesselType != vesselType)
                    continue;

                shipPrefabTransform = prefab.transform;
            }

            if (shipPrefabTransform == null)
            {
                CSDebug.LogError("No Vessel Prefabs found matching the needed vessel type!");
                return false;
            }

            return true;
        }
    }
}