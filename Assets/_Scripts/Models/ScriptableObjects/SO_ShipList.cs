using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Ship List", menuName = "CosmicShore/Ship/ShipList", order = 12)]
[System.Serializable]
public class SO_ShipList : ScriptableObject
{
    public List<SO_Ship> ShipList;
}