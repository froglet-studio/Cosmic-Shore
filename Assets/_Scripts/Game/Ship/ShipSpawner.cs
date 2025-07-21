using System;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ShipSpawner : MonoBehaviour
    {
        [SerializeField] 
        ShipPrefabContainer _shipPrefabContainer;
        
        public bool SpawnShip(ShipClassType shipType, out IShip ship)
        {
            if (shipType == ShipClassType.Random)
            {
                var values = Enum.GetValues(typeof(ShipClassType));
                var random = new System.Random();
                shipType = (ShipClassType)values.GetValue(random.Next(1, values.Length));
            }
            
            ship = null;
            
            if (!_shipPrefabContainer.TryGetShipPrefab(shipType, out Transform shipPrefab))
            {
                Debug.LogError($"Could not find ship prefab for {shipType}");
                return false;
            }

            Instantiate(shipPrefab).TryGetComponent(out ship);
            return true;
        }
    }
}