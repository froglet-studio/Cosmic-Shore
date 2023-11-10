using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Vessel List", menuName = "CosmicShore/VesselList", order = 11)]
[System.Serializable]
public class SO_VesselList : ScriptableObject
{
    public List<SO_Vessel> VesselList;
}