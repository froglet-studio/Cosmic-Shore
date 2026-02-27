using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Vessel List", menuName = "CosmicShore/Vessel/ShipList", order = 12)]
    [System.Serializable]
    public class SO_ShipList : ScriptableObject
    {
        public List<SO_Ship> ShipList;

        public bool TryGetShipSOByShipType(VesselClassType vesselClass, out SO_Ship shipSO)
        {
            shipSO = ShipList.FirstOrDefault(x => x.Class == vesselClass);
            if (shipSO == null)
            {
                CSDebug.LogWarning($"Vessel of type {vesselClass} not found in ShipList.");
                return false;
            }
            return true;
        }
    }
}
