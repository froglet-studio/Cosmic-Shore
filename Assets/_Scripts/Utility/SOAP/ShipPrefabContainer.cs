using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore.Soap
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "ShipPrefabContainer", menuName = "Scriptable Objects/Data Containers/Ship Prefab Container")]
    public class ShipPrefabContainer : ScriptableObject
    {
        [SerializeField]
        Transform[] _shipPrefabs;

        public bool TryGetShipPrefab(ShipClassType shipType, out Transform shipPrefabTransform)
        {
            shipPrefabTransform = null;

            if (_shipPrefabs.Length == 0)
            {
                Debug.LogError("No Ship Prefabs found! This should never happen!");
                return false;
            }

            foreach (var prefab in _shipPrefabs)
            {
                if (!prefab.TryGetComponent(out IShipStatus shipStatus))
                {
                    Debug.LogError($"Ship prefab {prefab} does not have a ShipStatus component attached!");
                    return false;
                }

                if (shipStatus.ShipType != shipType)
                    continue;

                shipPrefabTransform = prefab.transform;
            }

            if (shipPrefabTransform == null)
            {
                Debug.LogError("No Ship Prefabs found matching the needed ship type!");
                return false;
            }

            return true;
        }
    }
}