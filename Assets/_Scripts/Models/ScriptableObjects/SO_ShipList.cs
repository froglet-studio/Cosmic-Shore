using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "New Ship List", menuName = "CosmicShore/Ship/ShipList", order = 12)]
[System.Serializable]
public class SO_ShipList : ScriptableObject
{
    public List<SO_Ship> ShipList;

    public bool TryGetShipSOByShipType(ShipClassType shipClass, out SO_Ship shipSO)
    {
        shipSO = ShipList.FirstOrDefault(x => x.Class == shipClass);
        if (shipSO == null)
        {
            Debug.LogWarning($"Ship of type {shipClass} not found in ShipList.");
            return false;
        }
        return true;
    }
}