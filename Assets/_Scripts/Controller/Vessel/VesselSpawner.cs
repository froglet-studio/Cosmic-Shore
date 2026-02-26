using System;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class VesselSpawner : MonoBehaviour
    {
        [FormerlySerializedAs("_shipPrefabContainer")] [SerializeField] 
        VesselPrefabContainer vesselPrefabContainer;
        
        public bool SpawnShip(VesselClassType vesselType, out IVessel vessel)
        {
            if (vesselType == VesselClassType.Random)
            {
                var values = Enum.GetValues(typeof(VesselClassType));
                var random = new System.Random();
                vesselType = (VesselClassType)values.GetValue(random.Next(1, values.Length));
            }
            
            vessel = null;
            
            if (!vesselPrefabContainer.TryGetShipPrefab(vesselType, out Transform shipPrefab))
            {
                CSDebug.LogError($"Could not find vessel prefab for {vesselType}");
                return false;
            }

            Instantiate(shipPrefab).TryGetComponent(out vessel);
            return true;
        }
    }
}