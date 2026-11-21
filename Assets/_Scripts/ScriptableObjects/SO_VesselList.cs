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

    public bool TryGetVesselByClass(VesselClassType vesselClass, out SO_Vessel vessel) =>
        TryGetVesselByClass(vesselClass, out vessel, reportMissing: true);

    /// <summary>
    /// <paramref name="reportMissing"/> false is the PROBE form - "does this list carry that
    /// class?" - for a caller that has a fallback and must not warn per rebuild (the Toy Box's
    /// hull pictures). Keep the loud form for a caller about to spend the result.
    /// </summary>
    public bool TryGetVesselByClass(VesselClassType vesselClass, out SO_Vessel vessel, bool reportMissing)
    {
        vessel = VesselList?.FirstOrDefault(x => x && x.Class == vesselClass);
        if (vessel) return true;
        if (reportMissing)
            CSDebug.LogWarning($"Vessel of type {vesselClass} not found in VesselList.");
        return false;
    }
}
