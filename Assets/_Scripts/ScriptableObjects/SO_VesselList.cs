using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Utility;

[CreateAssetMenu(fileName = "New Vessel List", menuName = "CosmicShore/Vessel/VesselList", order = 12)]
[System.Serializable]
public class SO_VesselList : ScriptableObject
{
    [FormerlySerializedAs("ShipList")]
    public List<SO_Vessel> VesselList;

    public bool TryGetVesselByClass(VesselClassType vesselClass, out SO_Vessel vessel)
    {
        vessel = VesselList.FirstOrDefault(x => x.Class == vesselClass);
        if (vessel == null)
        {
            CSDebug.LogWarning($"Vessel of type {vesselClass} not found in VesselList.");
            return false;
        }
        return true;
    }
}
