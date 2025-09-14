using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Soap
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "ShipPrefabContainer", menuName = "ScriptableObjects/Data Containers/Vessel Prefab Container")]
    public class ShipPrefabContainer : ScriptableObject
    {
        [SerializeField]
        Transform[] _shipPrefabs;

        public bool TryGetShipPrefab(VesselClassType vesselType, out Transform shipPrefabTransform)
        {
            shipPrefabTransform = null;

            if (_shipPrefabs.Length == 0)
            {
                Debug.LogError("No Vessel Prefabs found! This should never happen!");
                return false;
            }

            foreach (var prefab in _shipPrefabs)
            {
                if (!prefab.TryGetComponent(out IVesselStatus shipStatus))
                {
                    Debug.LogError($"Vessel prefab {prefab} does not have a VesselStatus component attached!");
                    return false;
                }

                if (shipStatus.VesselType != vesselType)
                    continue;

                shipPrefabTransform = prefab.transform;
            }

            if (shipPrefabTransform == null)
            {
                Debug.LogError("No Vessel Prefabs found matching the needed vessel type!");
                return false;
            }

            return true;
        }
    }
}